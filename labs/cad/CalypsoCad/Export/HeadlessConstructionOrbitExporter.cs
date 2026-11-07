using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json;
using CalypsoCad.Host;
using CalypsoCad.Rendering;
using Novolis.Math.Geometry;
using Novolis.Silk;

namespace CalypsoCad.Export;

/// <summary>
/// Fast lazy 4K 24fps orbit of the <b>one</b> ship model through visual layer stages.
/// OML is edge-guide only (never solid). Frames stream raw into ffmpeg — nothing kept on disk.
/// </summary>
internal static class HeadlessConstructionOrbitExporter
{
    public const int Width = 3840;
    public const int Height = 2160;
    public const int Fps = 24;

    /// <summary>~10 s total (5 stages × 48 frames). Full model every frame — layer flags only.</summary>
    private const int FramesPerStage = 48;

    public static IReadOnlyList<string> Export(
        CalypsoSession session,
        CalypsoRenderer renderer)
    {
        var outDir = CalypsoProjectPaths.ConstructionOrbitDirectory;
        Directory.CreateDirectory(outDir);

        // Never keep a frames/ dump from a prior run.
        var staleFrames = Path.Combine(outDir, "frames");
        if (Directory.Exists(staleFrames))
            Directory.Delete(staleFrames, recursive: true);

        var stages = BuildLayerStages();
        var totalFrames = stages.Count * FramesPerStage;
        var mp4 = Path.Combine(outDir, "calypso-construction-orbit-4k24.mp4");
        var keyDir = Path.Combine(outDir, "_keys-tmp");
        Directory.CreateDirectory(keyDir);

        var previousLayers = session.VisualLayers;
        var previousView = session.ViewMode;
        var previousWire = session.WireMeshMode;
        var previousReveal = session.RevealEntityIds;
        var previousStatus = session.StatusText;
        var previousDeck = session.DeckFilter;
        var previousCut = session.CutPlaneLongitudinal;
        var previousCutUser = session.CutPlaneUserDriven;
        var previousCutOff = session.CutPlaneOffset;
        var previousOml = session.ShowOmlGuide;

        // Full document always — progressive RevealEntityIds made frames look like separate models.
        session.RevealEntityIds = null;
        session.ShowOmlGuide = true;
        session.ViewMode = CalypsoViewMode.Orbit;
        session.DeckFilter = null;

        var prepares = new List<Action>(totalFrames);
        var keyFrameIndices = new HashSet<int>
        {
            0,
            FramesPerStage,
            FramesPerStage * 2,
            FramesPerStage * 3,
            totalFrames - 1,
        };
        var keyPaths = new Dictionary<int, string>();

        for (var s = 0; s < stages.Count; s++)
        {
            var stage = stages[s];
            for (var f = 0; f < FramesPerStage; f++)
            {
                var global = s * FramesPerStage + f;
                var tGlobal = totalFrames <= 1 ? 0f : global / (float)(totalFrames - 1);
                var stageIndex = s;
                var localF = f;
                prepares.Add(() =>
                {
                    session.VisualLayers = stage.Layers; // no HullShell — OML guide only
                    session.WireMeshMode = stage.Wire;
                    session.ShowOmlGuide = true;
                    session.RevealEntityIds = null;
                    session.CutPlaneLongitudinal = true;
                    session.CutPlaneUserDriven = stage.Wire == CalypsoWireMeshMode.CutawayPartial;
                    session.CutPlaneOffset = stage.Wire == CalypsoWireMeshMode.CutawayPartial
                        ? -1.5f * MathF.Sin(tGlobal * MathF.PI * 2f)
                        : 0f;
                    ApplyLazyOrbit(renderer, tGlobal);
                    session.StatusText =
                        $"{stageIndex + 1}/{stages.Count} {stage.Title} · {localF + 1}/{FramesPerStage}";
                });
            }
        }

        var ffmpeg = FindFfmpeg()
            ?? throw new InvalidOperationException("ffmpeg not found on PATH — required for construction-orbit.");

        // Capture size may not be 4K (Silk FB quirk); pad/scale in the filter graph.
        var psi = new ProcessStartInfo
        {
            FileName = ffmpeg,
            Arguments =
                $"-y -f rawvideo -pix_fmt rgba -video_size {{W}}x{{H}} -framerate {Fps} -i pipe:0 " +
                $"-vf \"scale={Width}:{Height}:force_original_aspect_ratio=decrease," +
                $"pad={Width}:{Height}:(ow-iw)/2:(oh-ih)/2:black,setsar=1\" " +
                $"-c:v libx264 -preset veryfast -crf 20 -pix_fmt yuv420p -movflags +faststart \"{mp4}\"",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        Console.WriteLine(
            $"Construction orbit (stream): {stages.Count} layer stages × {FramesPerStage} = {totalFrames} → {mp4}");

        string? fbSize = null;
        Process? proc = null;
        var outputs = new List<string>();
        try
        {
            var written = SilkHeadlessCapture.CaptureSequence(
                Width,
                Height,
                prepares,
                frame => renderer.DrawFrame(frame, 0f),
                (pixels, w, h, index) =>
                {
                    if (proc is null)
                    {
                        fbSize = $"{w}x{h}";
                        psi.Arguments = psi.Arguments.Replace("{W}x{H}", fbSize, StringComparison.Ordinal);
                        proc = Process.Start(psi)
                               ?? throw new InvalidOperationException("Failed to start ffmpeg");
                        _ = DrainAsync(proc.StandardError);
                    }

                    WriteRawFrame(proc.StandardInput.BaseStream, pixels, w, h);

                    if (keyFrameIndices.Contains(index))
                    {
                        var keyPath = Path.Combine(keyDir, $"key-{index:D4}.png");
                        File.WriteAllBytes(keyPath, Novolis.Silk.Capture.RgbaPng.Encode(pixels, w, h));
                        keyPaths[index] = keyPath;
                    }

                    if ((index + 1) % 48 == 0 || index + 1 == totalFrames)
                        Console.WriteLine($"  streamed {index + 1}/{totalFrames}");

                    return true;
                },
                warmFramesPerStep: 0);

            if (proc is null)
                throw new InvalidOperationException("No frames captured.");

            proc.StandardInput.Close();
            if (!proc.WaitForExit(10 * 60 * 1000))
            {
                try { proc.Kill(entireProcessTree: true); } catch { /* ignore */ }
                throw new InvalidOperationException("ffmpeg encode timed out.");
            }

            if (proc.ExitCode != 0 || !File.Exists(mp4) || new FileInfo(mp4).Length == 0)
                throw new InvalidOperationException($"ffmpeg exit {proc.ExitCode} — MP4 missing/empty.");

            outputs.Add(mp4);
            Console.WriteLine($"Wrote construction orbit MP4: {mp4} ({written} frames, fb={fbSize})");

            PublishKey(keyPaths, 0, outDir, "key-01-structure.png", outputs);
            PublishKey(keyPaths, FramesPerStage, outDir, "key-02-passage.png", outputs);
            PublishKey(keyPaths, FramesPerStage * 2, outDir, "key-03-interiors.png", outputs);
            PublishKey(keyPaths, totalFrames - 1, outDir, "key-final-all-layers.png", outputs);

            File.WriteAllText(
                Path.Combine(outDir, "manifest.json"),
                JsonSerializer.Serialize(new
                {
                    generatedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                    width = Width,
                    height = Height,
                    fps = Fps,
                    frameCount = written,
                    framebuffer = fbSize,
                    streamed = true,
                    framesOnDisk = false,
                    oml = "edge-guide-only",
                    reveal = "none-full-model-layer-stages",
                    stages = stages.Select((st, i) => new
                    {
                        index = i + 1,
                        st.Id,
                        st.Title,
                        layers = st.Layers.ToString(),
                        wire = st.Wire.ToString(),
                    }),
                }, new JsonSerializerOptions { WriteIndented = true }));
            outputs.Add(Path.Combine(outDir, "manifest.json"));
            return outputs;
        }
        finally
        {
            try { proc?.Dispose(); } catch { /* ignore */ }
            try
            {
                if (Directory.Exists(keyDir))
                    Directory.Delete(keyDir, recursive: true);
            }
            catch { /* ignore */ }

            session.VisualLayers = previousLayers;
            session.ViewMode = previousView;
            session.WireMeshMode = previousWire;
            session.RevealEntityIds = previousReveal;
            session.StatusText = previousStatus;
            session.DeckFilter = previousDeck;
            session.CutPlaneLongitudinal = previousCut;
            session.CutPlaneUserDriven = previousCutUser;
            session.CutPlaneOffset = previousCutOff;
            session.ShowOmlGuide = previousOml;
        }
    }

    private static void PublishKey(
        Dictionary<int, string> keys, int index, string outDir, string name, List<string> outputs)
    {
        if (!keys.TryGetValue(index, out var src) || !File.Exists(src))
            return;
        var dest = Path.Combine(outDir, name);
        File.Copy(src, dest, overwrite: true);
        outputs.Add(dest);
    }

    private static void WriteRawFrame(Stream stdin, Rgba32[] pixels, int w, int h)
    {
        var expected = checked(w * h);
        if (pixels.Length < expected)
            throw new InvalidOperationException($"RGBA length {pixels.Length} < {expected}");
        stdin.Write(MemoryMarshal.AsBytes(pixels.AsSpan(0, expected)));
    }

    private static async Task DrainAsync(StreamReader reader)
    {
        try { await reader.ReadToEndAsync().ConfigureAwait(false); }
        catch { /* ignore */ }
    }

    private static void ApplyLazyOrbit(CalypsoRenderer renderer, float t)
    {
        var yaw = 0.55f + t * (MathF.PI * 2.15f);
        var pitch = 0.24f + 0.12f * MathF.Sin(t * MathF.PI * 2f);
        var dist = 100f - 8f * MathF.Sin(t * MathF.PI);
        renderer.SetOrbitPose(new Vector3(0f, 4.2f, 0f), dist, yaw, pitch);
    }

    private sealed record LayerStage(
        string Id, string Title, CalypsoVisualLayers Layers, CalypsoWireMeshMode Wire);

    /// <summary>Full ship every stage — only which draw groups are on. Never HullShell.</summary>
    private static List<LayerStage> BuildLayerStages() =>
    [
        new("structure", "Structure · frames / BH / girders",
            CalypsoVisualLayers.StructureNoHull, CalypsoWireMeshMode.Wire),
        new("passage", "Structure + passageway cutouts",
            CalypsoVisualLayers.StructureNoHull, CalypsoWireMeshMode.CutawayPartial),
        new("interiors", "Interiors · partitions / spaces",
            CalypsoVisualLayers.InteriorStructureNoHull, CalypsoWireMeshMode.CutawayPartial),
        new("fitout", "Fitout · outfit + openings",
            CalypsoVisualLayers.InteriorStructureNoHull | CalypsoVisualLayers.Outfit,
            CalypsoWireMeshMode.CutawayPartial),
        new("all", "All layers (no solid OML)",
            CalypsoVisualLayers.InteriorStructureNoHull
            | CalypsoVisualLayers.Outfit
            | CalypsoVisualLayers.SpaceFinish,
            CalypsoWireMeshMode.CutawayPartial),
    ];

    private static string? FindFfmpeg()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = OperatingSystem.IsWindows() ? "where" : "which",
                Arguments = "ffmpeg",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            if (p is null)
                return null;
            var line = p.StandardOutput.ReadLine();
            p.WaitForExit(5000);
            return string.IsNullOrWhiteSpace(line) ? null : line.Trim().Trim('"');
        }
        catch
        {
            return null;
        }
    }
}
