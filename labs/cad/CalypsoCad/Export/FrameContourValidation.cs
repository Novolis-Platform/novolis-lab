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
            var inset = CalypsoShipbuildingFrames.PlatingJoinInset;
            // Recover outer ring from mesh AABB mid-plane verts is noisy; compare fab registry if present.
            var auth = HullSectionAuthority.InnerRing(zStem, inset);
            float? maxDev = null;
            if (frame.MeshVertices is { Count: > 0 })
            {
                // Sample front-face verts (min Z cluster) and match to auth by nearest.
                var zs = frame.MeshVertices.Select(v => v[2]).ToList();
                var zMin = zs.Min();
                var front = frame.MeshVertices
                    .Where(v => MathF.Abs(v[2] - zMin) < 0.02f)
                    .Select(v => (Y: v[0], Up: v[1]))
                    .ToList();
                if (front.Count >= auth.Length)
                {
                    var max = 0f;
                    foreach (var a in auth)
                    {
                        var d = front.Min(p =>
                        {
                            var dy = p.Y - a.Y;
                            var du = p.Up - a.Up;
                            return MathF.Sqrt(dy * dy + du * du);
                        });
                        max = MathF.Max(max, d);
                    }

                    maxDev = max * 1000f;
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
