using System.Diagnostics;
using System.Numerics;
using Novolis.Raylib.Audio;
using Novolis.Raylib.Capture;
using Novolis.Raylib.Debug;
using Novolis.Raylib.Interact;
using Novolis.Raylib.Logging;
using Novolis.Raylib.Rendering;
using Novolis.Raylib.Timing;
using Novolis.Raylib.Windowing;
using Novolis.Ship.Topology;

namespace CalypsoCad.Services;

/// <summary>
/// Headless camera-path walkthrough using <see cref="FrameCaptureSession"/>
/// (<c>Novolis.Raylib.Capture</c> — per-frame <c>LoadImageFromScreen</c> / PNG stream after
/// <c>EndDrawing</c>). There is no raylib BeginVideo / MP4 binding in this stack; GIF hotkeys
/// (Ctrl+F12) are compile-time convenience only. Frames are encoded with ffmpeg when available.
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

        RaylibDebug.Start();
        Logger.SetTraceLogLevel(TraceLogLevel.Warning);
        AudioDevice.Init();
        try
        {
            Window.SetConfigFlags(WindowStateFlags.Hidden);
            Window.Init(width, height, "CalypsoCad walkthrough");
            if (!Window.IsReady())
                throw new InvalidOperationException("Raylib window failed to initialize (display/GLFW).");

            try
            {
                Time.SetTargetFPS(60);
                Window.SetExitKey((KeyboardKey)0);

                var previousSelected = session.SelectedSpaceId;
                var previousView = session.ViewMode;
                var previousWire = session.WireMeshMode;
                var previousDeck = session.DeckFilter;

                var frames = new List<string>();
                var frameIndex = 0;

                using var capture = new FrameCaptureSession(new CaptureStreamOptions
                {
                    CaptureEveryNFrames = 1,
                    MaxBufferedFrames = 64,
                });

                // Warm the presentation hook once before counting frames.
                Present(renderer);

                // --- Act 1: exterior orbit flyby ---
                session.ViewMode = CalypsoViewMode.Orbit;
                session.WireMeshMode = CalypsoWireMeshMode.None;
                session.DeckFilter = null;
                session.CutPlaneLongitudinal = true;
                const int orbitFrames = 60;
                for (var i = 0; i < orbitFrames; i++)
                {
                    var t = i / (float)(orbitFrames - 1);
                    var yaw = 0.35f + t * (MathF.PI * 1.85f);
                    var pitch = 0.18f + 0.22f * MathF.Sin(t * MathF.PI);
                    var dist = 92f - 8f * MathF.Sin(t * MathF.PI);
                    renderer.SetOrbitPose(new Vector3(0f, 3.8f, 0f), dist, yaw, pitch);
                    session.StatusText = $"walkthrough:orbit:{i + 1}/{orbitFrames}";
                    CaptureFrame(renderer, capture, framesDir, ref frameIndex, frames);
                }

                // --- Act 1b: stern ramp + side pods ---
                const int detailFrames = 24;
                for (var i = 0; i < detailFrames; i++)
                {
                    var t = i / (float)(detailFrames - 1);
                    var yaw = MathF.PI * (0.85f + t * 0.35f);
                    renderer.SetOrbitPose(new Vector3(0f, 2.8f, -20f), 48f - 6f * t, yaw, 0.14f + 0.08f * t);
                    session.StatusText = $"walkthrough:ramp-pods:{i + 1}/{detailFrames}";
                    CaptureFrame(renderer, capture, framesDir, ref frameIndex, frames);
                }

                // --- Act 2: cutaway orbit (slide invisible plane along beam) ---
                session.WireMeshMode = CalypsoWireMeshMode.CutawayPartial;
                session.CutPlaneLongitudinal = true;
                session.CutPlaneUserDriven = true;
                const int cutFrames = 36;
                for (var i = 0; i < cutFrames; i++)
                {
                    var t = i / (float)(cutFrames - 1);
                    session.CutPlaneOffset = -8f + t * 16f;
                    var yaw = MathF.PI * 0.45f + t * 0.55f;
                    renderer.SetOrbitPose(new Vector3(0f, 4f, 0f), 82f, yaw, 0.28f);
                    session.StatusText = $"walkthrough:cutaway:{i + 1}/{cutFrames} off={session.CutPlaneOffset:0.0}";
                    CaptureFrame(renderer, capture, framesDir, ref frameIndex, frames);
                }

                session.CutPlaneOffset = 0f;
                session.CutPlaneUserDriven = false;

                // --- Act 3: walking camera — HOLD aft (DK−1) through every open hatch ---
                session.WireMeshMode = CalypsoWireMeshMode.None;
                session.ViewMode = CalypsoViewMode.Interior;
                session.SelectedHookId = null;
                var walkPath = ShipWalk.BuildDeckMinusOneAftTour(session.Document);
                var byId = session.Document.Entities.ToDictionary(e => e.Id);
                // Subsample dense waypoints so the reel stays watchable (~2–3 min at 15 fps).
                var stride = Math.Max(1, walkPath.Count / 180);
                for (var i = 0; i < walkPath.Count; i += stride)
                {
                    var wp = walkPath[i];
                    byId.TryGetValue(wp.SpaceId, out var space);
                    if (space is not null)
                        session.SelectedSpaceId = space.Id;
                    renderer.SetInteriorWalkPose(wp.Eye, wp.Look, space);
                    session.StatusText = $"walkthrough:walk:{wp.Label}:{i + 1}/{walkPath.Count}";
                    CaptureFrame(renderer, capture, framesDir, ref frameIndex, frames);
                }

                if (walkPath.Count == 0)
                {
                    // Fallback: short HOLD standing beat if graph is empty.
                    var hold = session.Spaces.FirstOrDefault(s =>
                        string.Equals(s.Name, "HOLD", StringComparison.OrdinalIgnoreCase));
                    if (hold is not null)
                    {
                        session.SelectedSpaceId = hold.Id;
                        session.DeckFilter = hold.Deck;
                        renderer.SyncInteriorFromSelection();
                        for (var i = 0; i < 12; i++)
                        {
                            session.StatusText = $"walkthrough:hold-fallback:{i + 1}/12";
                            CaptureFrame(renderer, capture, framesDir, ref frameIndex, frames);
                        }
                    }
                }

                // --- Act 4: catwalk hold beat ---
                renderer.ApplyInteriorPreset("catwalk-containers");
                var (cEye, cLook) = renderer.GetInteriorPose();
                const int catFrames = 18;
                for (var i = 0; i < catFrames; i++)
                {
                    var t = i / (float)(catFrames - 1);
                    var eye = cEye + new Vector3(t * 0.4f, -t * 0.3f, -t * 1.2f);
                    var look = cLook + new Vector3(0f, -0.4f * t, -1.5f * t);
                    renderer.SetInteriorPose(eye, look);
                    session.StatusText = $"walkthrough:catwalk:{i + 1}/{catFrames}";
                    CaptureFrame(renderer, capture, framesDir, ref frameIndex, frames);
                }

                session.SelectedSpaceId = previousSelected;
                session.ViewMode = previousView;
                session.WireMeshMode = previousWire;
                session.DeckFilter = previousDeck;
                renderer.SyncInteriorFromSelection();

                var outputs = new List<string>(frames);
                var assembled = TryAssembleVideo(framesDir, exportsDir, fps);
                outputs.AddRange(assembled);

                CopyKeyframe(frames, exportsDir, 0, "walkthrough-orbit-start.png", outputs);
                CopyKeyframe(frames, exportsDir, Math.Min(orbitFrames / 2, frames.Count - 1), "walkthrough-orbit-mid.png", outputs);
                CopyKeyframe(frames, exportsDir, Math.Min(orbitFrames + detailFrames / 2, frames.Count - 1), "walkthrough-ramp-pods.png", outputs);
                CopyKeyframe(frames, exportsDir, Math.Min(orbitFrames + detailFrames + cutFrames / 2, frames.Count - 1), "walkthrough-cutaway.png", outputs);
                CopyKeyframe(frames, exportsDir, Math.Min(orbitFrames + detailFrames + cutFrames + 8, frames.Count - 1), "walkthrough-walk-start.png", outputs);
                CopyKeyframe(frames, exportsDir, frames.Count - 1, "walkthrough-catwalk-end.png", outputs);

                return outputs;
            }
            finally
            {
                Window.Close();
            }
        }
        finally
        {
            AudioDevice.Close();
            RaylibDebug.Reset();
        }
    }

    private static void Present(CalypsoRenderer renderer)
    {
        Window.PollInputEvents();
        Graphics.BeginDrawing();
        renderer.DrawFrame(Time.GetFrameTime(), Window.GetScreenWidth(), Window.GetScreenHeight());
        Graphics.EndDrawing();
    }

    private static void CaptureFrame(
        CalypsoRenderer renderer,
        FrameCaptureSession capture,
        string framesDir,
        ref int frameIndex,
        List<string> frames)
    {
        // One warm present, then the counted capture present.
        Present(renderer);
        Present(renderer);

        var reader = capture.Reader;
        CapturedFrame? last = null;
        if (reader is not null)
        {
            while (reader.TryRead(out var frame))
                last = frame;
        }

        // Fallback if the presentation hook missed a beat.
        if (last is null)
        {
            if (!ScreenFramebufferCapture.TryExportFramebufferToPng(out var png) || png is not { Length: > 0 })
                return;
            var path = Path.Combine(framesDir, $"frame-{frameIndex:D4}.png");
            File.WriteAllBytes(path, png);
            frames.Add(path);
            frameIndex++;
            return;
        }

        {
            var path = Path.Combine(framesDir, $"frame-{frameIndex:D4}.png");
            File.WriteAllBytes(path, last.Png);
            frames.Add(path);
            frameIndex++;
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

    /// <summary>
    /// Assemble MP4/GIF with ffmpeg. Raylib has no BeginVideo binding here; Novolis.Raylib.Capture
    /// streams PNG frames (same LoadImageFromScreen path as stills).
    /// </summary>
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
