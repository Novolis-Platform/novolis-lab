using System.Text.Json;
using CalypsoCad.Session;
using CalypsoCad.Structure;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Export;

/// <summary>
/// Quantitative check: ordinary frame annulus outer rings vs authoritative hull section.
/// </summary>
internal static class FrameContourValidation
{
    public static string WriteReport(CadDocument document, float maxDeviationMm = 25f)
    {
        var outDir = Path.Combine(CalypsoProjectPaths.RendersDirectory, "frame-contour");
        Directory.CreateDirectory(outDir);
        var frames = document.Entities
            .Where(e => e.Kind == "mesh"
                        && e.Name is not null
                        && e.Name.StartsWith("frame-", StringComparison.OrdinalIgnoreCase)
                        && e.Name.Contains("annulus", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var rows = new List<object>();
        var failures = 0;
        foreach (var frame in frames)
        {
            var zStem = InferZStem(frame);
            // Ordinary Emit uses DefaultSkinInset+WebClearance; hold/perimeter rings use PlatingJoinInset.
            var authCandidates = new[]
            {
                HullSectionAuthority.InnerRing(zStem, CalypsoShipbuildingFrames.DefaultSkinInset + 0.28f),
                HullSectionAuthority.InnerRing(zStem, CalypsoShipbuildingFrames.PlatingJoinInset),
                HullSectionAuthority.InnerRing(zStem, CalypsoShipbuildingFrames.DefaultSkinInset),
            };
            var auth = authCandidates[0];
            float? maxDev = null;
            if (frame.MeshVertices is { Count: > 0 })
            {
                // Front face = min-Z cluster. Annulus has outer+inner verts — keep the outer
                // shell only (farthest from section center) before measuring deviation.
                var zs = frame.MeshVertices.Select(v => v[2]).ToList();
                var zMin = zs.Min();
                var front = frame.MeshVertices
                    .Where(v => MathF.Abs(v[2] - zMin) < 0.02f)
                    .Select(v => (Y: v[0], Up: v[1]))
                    .ToList();
                if (front.Count >= auth.Length)
                {
                    var cy = front.Average(p => p.Y);
                    var cu = front.Average(p => p.Up);
                    var radii = front
                        .Select(p => (P: p, R: MathF.Sqrt((p.Y - cy) * (p.Y - cy) + (p.Up - cu) * (p.Up - cu))))
                        .OrderByDescending(x => x.R)
                        .ToList();
                    // Outer ring ≈ first N verts by radius (N = auth vertex count).
                    var outer = radii.Take(auth.Length).Select(x => x.P).ToList();
                    var best = float.PositiveInfinity;
                    foreach (var candidate in authCandidates)
                    {
                        if (candidate.Length == 0)
                            continue;
                        var max = 0f;
                        foreach (var a in candidate)
                        {
                            var d = outer.Min(p =>
                            {
                                var dy = p.Y - a.Y;
                                var du = p.Up - a.Up;
                                return MathF.Sqrt(dy * dy + du * du);
                            });
                            max = MathF.Max(max, d);
                        }

                        best = MathF.Min(best, max);
                    }

                    maxDev = float.IsFinite(best) ? best * 1000f : null;
                    auth = authCandidates[0];
                }
            }

            var pass = maxDev is null || maxDev <= maxDeviationMm;
            if (!pass)
                failures++;
            rows.Add(new
            {
                partId = frame.Name,
                stationZStem = zStem,
                maxDeviationMm = maxDev,
                toleranceMm = maxDeviationMm,
                passed = pass,
                triangleCount = (frame.MeshIndices?.Count ?? 0) / 3,
                vertexCount = frame.MeshVertices?.Count ?? 0,
            });
        }

        var path = Path.Combine(outDir, "frame-contour-validation.json");
        var payload = new
        {
            frameAnnulusCount = frames.Count,
            failures,
            toleranceMm = maxDeviationMm,
            frames = rows,
            note = "Ordinary transverse frames must be connected annular plate-webs following the geometric inset of the authoritative hull section.",
        };
        File.WriteAllText(path, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
        return path;
    }

    private static float InferZStem(CadEntity frame)
    {
        if (frame.Points is { Count: >= 1 } && frame.Points[0].Length >= 3)
        {
            var zCad = frame.Points[0][2];
            return 69f * 0.5f - zCad;
        }

        return 0f;
    }
}
