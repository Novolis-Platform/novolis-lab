using System.Numerics;
using CalypsoCad.Session;
using CalypsoCad.Viewport;
using Novolis.Ship.Topology;
using Novolis.Silk;
using Novolis.Silk.Capture;

namespace CalypsoCad.Export;

/// <summary>
/// Headless camera-path walkthrough via Silk native video (exact offscreen FBO → H.264).
/// Keyframe PNGs only — no PNG frame dump.
/// </summary>
internal static class HeadlessWalkthroughExporter
{
    public static IReadOnlyList<string> Export(
        CalypsoSession session,
        CalypsoRenderer renderer,
        string generatedRoot,
        int width = SilkCaptureSize.UhdWidth,
        int height = SilkCaptureSize.UhdHeight,
        int fps = 24)
    {
        var exportsDir = ViewportPngExporter.ExportsDirectory(generatedRoot);
        Directory.CreateDirectory(exportsDir);
        var framesDir = Path.Combine(exportsDir, "walkthrough");
        if (Directory.Exists(framesDir))
            Directory.Delete(framesDir, recursive: true);

        var previousSelected = session.SelectedSpaceId;
        var previousView = session.ViewMode;
        var previousWire = session.WireMeshMode;
        var previousDeck = session.DeckFilter;

        var prepares = new List<Action>();
        const int orbitFrames = 60;
        for (var i = 0; i < orbitFrames; i++)
        {
            var t = i / (float)(orbitFrames - 1);
            var yaw = 0.35f + t * (MathF.PI * 1.85f);
            var pitch = 0.18f + 0.22f * MathF.Sin(t * MathF.PI);
            var dist = 92f - 8f * MathF.Sin(t * MathF.PI);
            var idx = i;
            prepares.Add(() =>
            {
                session.ViewMode = CalypsoViewMode.Orbit;
                session.WireMeshMode = CalypsoWireMeshMode.None;
                session.DeckFilter = null;
                session.CutPlaneLongitudinal = true;
                renderer.SetOrbitPose(new Vector3(0f, 3.8f, 0f), dist, yaw, pitch);
                session.StatusText = $"walkthrough:orbit:{idx + 1}/{orbitFrames}";
            });
        }

        const int detailFrames = 24;
        for (var i = 0; i < detailFrames; i++)
        {
            var t = i / (float)(detailFrames - 1);
            var yaw = MathF.PI * (0.85f + t * 0.35f);
            var dist = 48f - 6f * t;
            var pitch = 0.14f + 0.08f * t;
            var idx = i;
            prepares.Add(() =>
            {
                session.ViewMode = CalypsoViewMode.Orbit;
                session.WireMeshMode = CalypsoWireMeshMode.None;
                renderer.SetOrbitPose(new Vector3(0f, 2.8f, -20f), dist, yaw, pitch);
                session.StatusText = $"walkthrough:ramp:{idx + 1}/{detailFrames}";
            });
        }

        const int cutFrames = 36;
        for (var i = 0; i < cutFrames; i++)
        {
            var t = i / (float)(cutFrames - 1);
            var offset = -8f + t * 16f;
            var yaw = MathF.PI * 0.45f + t * 0.55f;
            var idx = i;
            prepares.Add(() =>
            {
                session.ViewMode = CalypsoViewMode.Orbit;
                session.WireMeshMode = CalypsoWireMeshMode.CutawayPartial;
                session.CutPlaneLongitudinal = true;
                session.CutPlaneUserDriven = true;
                session.CutPlaneOffset = offset;
                renderer.SetOrbitPose(new Vector3(0f, 4f, 0f), 82f, yaw, 0.28f);
                session.StatusText = $"walkthrough:cutaway:{idx + 1}/{cutFrames} off={offset:0.0}";
            });
        }

        var walkPath = ShipWalk.BuildDeckMinusOneAftTour(session.Document);
        var byId = session.Document.Entities.ToDictionary(e => e.Id);
        var stride = Math.Max(1, walkPath.Count / 180);
        var walkStartIndex = prepares.Count;
        if (walkPath.Count == 0)
        {
            for (var i = 0; i < 12; i++)
            {
                var idx = i;
                prepares.Add(() =>
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
                });
            }
        }
        else
        {
            for (var i = 0; i < walkPath.Count; i += stride)
            {
                var wp = walkPath[i];
                var idx = i;
                prepares.Add(() =>
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
                });
            }
        }

        const int catFrames = 18;
        for (var i = 0; i < catFrames; i++)
        {
            var t = i / (float)(catFrames - 1);
            var idx = i;
            prepares.Add(() =>
            {
                renderer.ApplyInteriorPreset("catwalk-containers");
                var (cEye, cLook) = renderer.GetInteriorPose();
                var eye = cEye + new Vector3(t * 0.4f, -t * 0.3f, -t * 1.2f);
                var look = cLook + new Vector3(0f, -0.4f * t, -1.5f * t);
                renderer.SetInteriorPose(eye, look);
                session.StatusText = $"walkthrough:catwalk:{idx + 1}/{catFrames}";
            });
        }

        var keyIndices = new Dictionary<int, string>
        {
            [0] = "walkthrough-orbit-start.png",
            [orbitFrames / 2] = "walkthrough-orbit-mid.png",
            [orbitFrames + detailFrames / 2] = "walkthrough-ramp.png",
            [orbitFrames + detailFrames + cutFrames / 2] = "walkthrough-cutaway.png",
            [Math.Min(walkStartIndex + 8, prepares.Count - 1)] = "walkthrough-walk-start.png",
            [prepares.Count - 1] = "walkthrough-catwalk-end.png",
        };

        var mp4 = Path.Combine(exportsDir, "walkthrough.mp4");
        var outputs = new List<string>();
        try
        {
            Console.WriteLine(
                $"Walkthrough (Silk native video): {prepares.Count} frames @ {width}x{height} → {mp4}");
            var result = SilkHeadlessCapture.CaptureVideo(
                new VideoEncodeOptions
                {
                    OutputPath = mp4,
                    Width = width,
                    Height = height,
                    Fps = fps,
                    Crf = 18,
                    Preset = "medium",
                },
                prepares,
                frame => renderer.DrawFrame(frame, 0f),
                onFrame: (pixels, w, h, index) =>
                {
                    if (keyIndices.TryGetValue(index, out var name))
                    {
                        var path = Path.Combine(exportsDir, name);
                        File.WriteAllBytes(path, RgbaPng.Encode(pixels, w, h));
                        outputs.Add(path);
                    }

                    return true;
                },
                warmFramesPerStep: 0);

            outputs.Insert(0, result.OutputPath);
            Console.WriteLine(
                $"Wrote walkthrough MP4: {result.OutputPath} ({result.FrameCount} frames, exact {result.Width}x{result.Height})");
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
}
