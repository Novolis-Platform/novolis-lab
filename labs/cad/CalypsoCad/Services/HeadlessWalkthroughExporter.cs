using System.Diagnostics;
using System.Numerics;
using Novolis.Ship.Topology;

namespace CalypsoCad.Services;

/// <summary>
/// Headless camera-path walkthrough via Silk capture. Frames are encoded with ffmpeg when available.
/// </summary>
internal static class HeadlessWalkthroughExporter
{
    public static IReadOnlyList<string> Export(
        CalypsoSession session,
        CalypsoRenderer renderer,
        string generatedRoot,
        int width = 1920,
        int height = 1080,
        int fps = 15)
    {
        var exportsDir = ViewportPngExporter.ExportsDirectory(generatedRoot);
        var framesDir = Path.Combine(exportsDir, "walkthrough");
        if (Directory.Exists(framesDir))
            Directory.Delete(framesDir, recursive: true);
        Directory.CreateDirectory(framesDir);

        var previousSelected = session.SelectedSpaceId;
        var previousView = session.ViewMode;
        var previousWire = session.WireMeshMode;
        var previousDeck = session.DeckFilter;

        var jobs = new List<(string Path, Action Prepare)>();
        var frameIndex = 0;
        string NextPath() => Path.Combine(framesDir, $"frame-{frameIndex++:D4}.png");

        const int orbitFrames = 60;
        for (var i = 0; i < orbitFrames; i++)
        {
            var t = i / (float)(orbitFrames - 1);
            var yaw = 0.35f + t * (MathF.PI * 1.85f);
            var pitch = 0.18f + 0.22f * MathF.Sin(t * MathF.PI);
            var dist = 92f - 8f * MathF.Sin(t * MathF.PI);
            var idx = i;
            jobs.Add((NextPath(), () =>
            {
                session.ViewMode = CalypsoViewMode.Orbit;
                session.WireMeshMode = CalypsoWireMeshMode.None;
                session.DeckFilter = null;
                session.CutPlaneLongitudinal = true;
                renderer.SetOrbitPose(new Vector3(0f, 3.8f, 0f), dist, yaw, pitch);
                session.StatusText = $"walkthrough:orbit:{idx + 1}/{orbitFrames}";
            }));
        }

        const int detailFrames = 24;
        for (var i = 0; i < detailFrames; i++)
        {
            var t = i / (float)(detailFrames - 1);
            var yaw = MathF.PI * (0.85f + t * 0.35f);
            var dist = 48f - 6f * t;
            var pitch = 0.14f + 0.08f * t;
            var idx = i;
            jobs.Add((NextPath(), () =>
            {
                session.ViewMode = CalypsoViewMode.Orbit;
                session.WireMeshMode = CalypsoWireMeshMode.None;
                renderer.SetOrbitPose(new Vector3(0f, 2.8f, -20f), dist, yaw, pitch);
                session.StatusText = $"walkthrough:ramp:{idx + 1}/{detailFrames}";
            }));
        }

        const int cutFrames = 36;
        for (var i = 0; i < cutFrames; i++)
        {
            var t = i / (float)(cutFrames - 1);
            var offset = -8f + t * 16f;
            var yaw = MathF.PI * 0.45f + t * 0.55f;
            var idx = i;
            jobs.Add((NextPath(), () =>
            {
                session.ViewMode = CalypsoViewMode.Orbit;
                session.WireMeshMode = CalypsoWireMeshMode.CutawayPartial;
                session.CutPlaneLongitudinal = true;
                session.CutPlaneUserDriven = true;
                session.CutPlaneOffset = offset;
                renderer.SetOrbitPose(new Vector3(0f, 4f, 0f), 82f, yaw, 0.28f);
                session.StatusText = $"walkthrough:cutaway:{idx + 1}/{cutFrames} off={offset:0.0}";
            }));
        }

        var walkPath = ShipWalk.BuildDeckMinusOneAftTour(session.Document);
        var byId = session.Document.Entities.ToDictionary(e => e.Id);
        var stride = Math.Max(1, walkPath.Count / 180);
        var walkStartIndex = frameIndex;
        if (walkPath.Count == 0)
        {
            for (var i = 0; i < 12; i++)
            {
                var idx = i;
                jobs.Add((NextPath(), () =>
                {
                    session.WireMeshMode = CalypsoWireMeshMode.None;
                    session.ViewMode = CalypsoViewMode.Interior;
                    session.SelectedHookId = null;
                    session.CutPlaneOffset = 0f;
                    session.CutPlaneUserDriven = false;
                    var hold = session.Spaces.FirstOrDefault(s =>
                        string.Equals(s.Name, "HOLD", StringComparison.OrdinalIgnoreCase));
                    if (hold is not null)
                    {
                        session.SelectedSpaceId = hold.Id;
                        session.DeckFilter = hold.Deck;
                        renderer.SyncInteriorFromSelection();
                    }

                    session.StatusText = $"walkthrough:hold-fallback:{idx + 1}/12";
                }));
            }
        }
        else
        {
            for (var i = 0; i < walkPath.Count; i += stride)
            {
                var wp = walkPath[i];
                var idx = i;
                jobs.Add((NextPath(), () =>
                {
                    session.WireMeshMode = CalypsoWireMeshMode.None;
                    session.ViewMode = CalypsoViewMode.Interior;
                    session.SelectedHookId = null;
                    session.CutPlaneOffset = 0f;
                    session.CutPlaneUserDriven = false;
                    byId.TryGetValue(wp.SpaceId, out var space);
                    if (space is not null)
                        session.SelectedSpaceId = space.Id;
                    renderer.SetInteriorWalkPose(wp.Eye, wp.Look, space);
                    session.StatusText = $"walkthrough:walk:{wp.Label}:{idx + 1}/{walkPath.Count}";
                }));
            }
        }

        const int catFrames = 18;
        for (var i = 0; i < catFrames; i++)
        {
            var t = i / (float)(catFrames - 1);
            var idx = i;
            jobs.Add((NextPath(), () =>
            {
                renderer.ApplyInteriorPreset("catwalk-containers");
                var (cEye, cLook) = renderer.GetInteriorPose();
                var eye = cEye + new Vector3(t * 0.4f, -t * 0.3f, -t * 1.2f);
                var look = cLook + new Vector3(0f, -0.4f * t, -1.5f * t);
                renderer.SetInteriorPose(eye, look);
                session.StatusText = $"walkthrough:catwalk:{idx + 1}/{catFrames}";
            }));
        }

        try
        {
            var frames = SilkHeadlessCapture.CaptureSequence(
                width,
                height,
                jobs,
                frame => renderer.DrawFrame(frame, 0f),
                warmFramesPerStep: 1).ToList();

            var outputs = new List<string>(frames);
            outputs.AddRange(TryAssembleVideo(framesDir, exportsDir, fps));

            CopyKeyframe(frames, exportsDir, 0, "walkthrough-orbit-start.png", outputs);
            CopyKeyframe(frames, exportsDir, Math.Min(orbitFrames / 2, frames.Count - 1), "walkthrough-orbit-mid.png", outputs);
            CopyKeyframe(frames, exportsDir, Math.Min(orbitFrames + detailFrames / 2, frames.Count - 1), "walkthrough-ramp.png", outputs);
            CopyKeyframe(frames, exportsDir, Math.Min(orbitFrames + detailFrames + cutFrames / 2, frames.Count - 1), "walkthrough-cutaway.png", outputs);
            CopyKeyframe(frames, exportsDir, Math.Min(walkStartIndex + 8, frames.Count - 1), "walkthrough-walk-start.png", outputs);
            CopyKeyframe(frames, exportsDir, frames.Count - 1, "walkthrough-catwalk-end.png", outputs);

            return outputs;
        }
        finally
        {
            session.SelectedSpaceId = previousSelected;
            session.ViewMode = previousView;
            session.WireMeshMode = previousWire;
            session.DeckFilter = previousDeck;
            session.CutPlaneOffset = 0f;
            session.CutPlaneUserDriven = false;
            renderer.SyncInteriorFromSelection();
        }
    }

    private static void CopyKeyframe(List<string> frames, string exportsDir, int index, string name, List<string> outputs)
    {
        if (frames.Count == 0 || index < 0 || index >= frames.Count)
            return;
        var dest = Path.Combine(exportsDir, name);
        File.Copy(frames[index], dest, overwrite: true);
        outputs.Add(dest);
    }

    private static List<string> TryAssembleVideo(string framesDir, string exportsDir, int fps)
    {
        var written = new List<string>();
        var ffmpeg = FindFfmpeg();
        if (ffmpeg is null)
        {
            Console.WriteLine("ffmpeg not found on PATH — walkthrough PNG frames only (no MP4/GIF).");
            return written;
        }

        var pattern = Path.Combine(framesDir, "frame-%04d.png");
        var mp4 = Path.Combine(exportsDir, "walkthrough.mp4");
        var gif = Path.Combine(exportsDir, "walkthrough.gif");

        if (Run(ffmpeg, $"-y -framerate {fps} -i \"{pattern}\" -c:v libx264 -pix_fmt yuv420p -crf 20 \"{mp4}\"") == 0
            && File.Exists(mp4))
        {
            written.Add(mp4);
            Console.WriteLine($"Wrote walkthrough MP4: {mp4}");
        }

        var palette = Path.Combine(framesDir, "palette.png");
        if (Run(ffmpeg, $"-y -framerate {fps} -i \"{pattern}\" -vf \"fps={fps},scale=1280:-1:flags=lanczos,palettegen\" \"{palette}\"") == 0
            && Run(ffmpeg, $"-y -framerate {fps} -i \"{pattern}\" -i \"{palette}\" -lavfi \"fps={fps},scale=1280:-1:flags=lanczos[x];[x][1:v]paletteuse\" \"{gif}\"") == 0
            && File.Exists(gif))
        {
            written.Add(gif);
            Console.WriteLine($"Wrote walkthrough GIF: {gif}");
        }

        return written;
    }

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

    private static int Run(string fileName, string args)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            if (p is null)
                return -1;
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            if (!p.WaitForExit(180_000))
            {
                try { p.Kill(entireProcessTree: true); } catch { /* ignore */ }
                return -1;
            }

            return p.ExitCode;
        }
        catch
        {
            return -1;
        }
    }
}
