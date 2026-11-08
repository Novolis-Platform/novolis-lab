using System.Globalization;
using System.Numerics;
using System.Text.Json;
using CalypsoCad.Session;
using CalypsoCad.Viewport;
using CalypsoCad.Visibility;
using Novolis.Silk;
using Novolis.Silk.Capture;

namespace CalypsoCad.Export;

/// <summary>
/// Fast 4K 24fps orbit of the one ship model through visual layer stages.
/// Uses Silk native video (exact offscreen FBO → H.264); no PNG frame dumps.
/// OML is edge-guide only (never solid).
/// </summary>
internal static class HeadlessConstructionOrbitExporter
{
    public const int Width = SilkCaptureSize.UhdWidth;
    public const int Height = SilkCaptureSize.UhdHeight;
    public const int Fps = 24;

    /// <summary>~10 s total (5 stages × 48 frames). Full model every frame — layer flags only.</summary>
    private const int FramesPerStage = 48;

    public static IReadOnlyList<string> Export(
        CalypsoSession session,
        CalypsoRenderer renderer)
    {
        var outDir = CalypsoProjectPaths.ConstructionOrbitDirectory;
        Directory.CreateDirectory(outDir);

        var staleFrames = Path.Combine(outDir, "frames");
        if (Directory.Exists(staleFrames))
            Directory.Delete(staleFrames, recursive: true);

        var stages = BuildLayerStages();
        var totalFrames = stages.Count * FramesPerStage;
        var mp4 = Path.Combine(outDir, "calypso-construction-orbit-4k24.mp4");
        var keyDir = Path.Combine(outDir, "_keys-tmp");
        Directory.CreateDirectory(keyDir);

        var previousVis = session.Visibility.CaptureSnapshot();
        var previousView = session.ViewMode;
        var previousWire = session.WireMeshMode;
        var previousStatus = session.StatusText;
        var previousDeck = session.DeckFilter;
        var previousCut = session.CutPlaneLongitudinal;
        var previousCutUser = session.CutPlaneUserDriven;
        var previousCutOff = session.CutPlaneOffset;

        session.Visibility.ClearEntityRevealFilter();
        session.Visibility.SetLocal("ref-oml-edge-guide", LocalVisibility.Visible, recordUndo: false);
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
                    session.VisualLayers = stage.Layers;
                    session.WireMeshMode = stage.Wire;
                    session.Visibility.ApplySnapshot(stage.Visibility(), recordUndo: false, asPreset: stage.Id);
                    session.Visibility.SetLocal("ref-oml-edge-guide", LocalVisibility.Visible, recordUndo: false);
                    session.Visibility.ClearEntityRevealFilter();
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

        Console.WriteLine(
            $"Construction orbit (Silk native video): {stages.Count} stages × {FramesPerStage} = {totalFrames} @ {Width}x{Height} → {mp4}");

        var outputs = new List<string>();
        try
        {
            var result = SilkHeadlessCapture.CaptureVideo(
                new VideoEncodeOptions
                {
                    OutputPath = mp4,
                    Width = Width,
                    Height = Height,
                    Fps = Fps,
                    Crf = 18,
                    Preset = "medium",
                },
                prepares,
                frame => renderer.DrawFrame(frame, 0f),
                onFrame: (pixels, w, h, index) =>
                {
                    if (keyFrameIndices.Contains(index))
                    {
                        var keyPath = Path.Combine(keyDir, $"key-{index:D4}.png");
                        File.WriteAllBytes(keyPath, RgbaPng.Encode(pixels, w, h));
                        keyPaths[index] = keyPath;
                    }

                    if ((index + 1) % 48 == 0 || index + 1 == totalFrames)
                        Console.WriteLine($"  encoded {index + 1}/{totalFrames}");

                    return true;
                },
                warmFramesPerStep: 0);

            outputs.Add(result.OutputPath);
            Console.WriteLine(
                $"Wrote construction orbit MP4: {result.OutputPath} ({result.FrameCount} frames, exact {result.Width}x{result.Height})");

            PublishKey(keyPaths, 0, outDir, "key-01-structure.png", outputs);
            PublishKey(keyPaths, FramesPerStage, outDir, "key-02-passage.png", outputs);
            PublishKey(keyPaths, FramesPerStage * 2, outDir, "key-03-interiors.png", outputs);
            PublishKey(keyPaths, totalFrames - 1, outDir, "key-final-all-layers.png", outputs);

            File.WriteAllText(
                Path.Combine(outDir, "manifest.json"),
                JsonSerializer.Serialize(new
                {
                    generatedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                    width = result.Width,
                    height = result.Height,
                    fps = result.Fps,
                    frameCount = result.FrameCount,
                    framebuffer = $"{result.Width}x{result.Height}",
                    encoder = "Novolis.Silk.Capture.RgbaVideoEncoder",
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
            try
            {
                if (Directory.Exists(keyDir))
                    Directory.Delete(keyDir, recursive: true);
            }
            catch { /* ignore */ }

            session.Visibility.ApplySnapshot(previousVis, recordUndo: false, asPreset: previousVis.Label);
            session.ViewMode = previousView;
            session.WireMeshMode = previousWire;
            session.StatusText = previousStatus;
            session.DeckFilter = previousDeck;
            session.CutPlaneLongitudinal = previousCut;
            session.CutPlaneUserDriven = previousCutUser;
            session.CutPlaneOffset = previousCutOff;
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

    private static void ApplyLazyOrbit(CalypsoRenderer renderer, float t)
    {
        var yaw = 0.55f + t * (MathF.PI * 2.15f);
        var pitch = 0.24f + 0.12f * MathF.Sin(t * MathF.PI * 2f);
        var dist = 100f - 8f * MathF.Sin(t * MathF.PI);
        renderer.SetOrbitPose(new Vector3(0f, 4.2f, 0f), dist, yaw, pitch);
    }

    private sealed record LayerStage(
        string Id,
        string Title,
        CalypsoVisualLayers Layers,
        CalypsoWireMeshMode Wire,
        Func<VisibilitySnapshot> Visibility);

    private static List<LayerStage> BuildLayerStages() =>
    [
        new("structure", "Structure · frames / BH / girders",
            CalypsoVisualLayers.StructureNoHull, CalypsoWireMeshMode.Wire,
            VisibilityPresets.PrimaryStructure),
        new("passage", "Structure + passageway cutouts",
            CalypsoVisualLayers.StructureNoHull, CalypsoWireMeshMode.CutawayPartial,
            VisibilityPresets.PrimaryStructure),
        new("interiors", "Interiors · partitions / spaces",
            CalypsoVisualLayers.InteriorStructureNoHull, CalypsoWireMeshMode.CutawayPartial,
            VisibilityPresets.InteriorStructure),
        new("fitout", "Fitout · outfit + openings",
            CalypsoVisualLayers.InteriorStructureNoHull | CalypsoVisualLayers.Outfit,
            CalypsoWireMeshMode.CutawayPartial,
            VisibilityPresets.InteriorStructure),
        new("all", "All layers (no solid OML)",
            CalypsoVisualLayers.InteriorStructureNoHull
            | CalypsoVisualLayers.Outfit
            | CalypsoVisualLayers.SpaceFinish,
            CalypsoWireMeshMode.CutawayPartial,
            VisibilityPresets.ArrangementNoSolidOml),
    ];
}
