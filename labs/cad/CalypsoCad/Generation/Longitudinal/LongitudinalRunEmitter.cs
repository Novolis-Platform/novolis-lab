using CalypsoCad.Structure;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Generation.Longitudinal;

/// <summary>
/// Emits one logical longitudinal run as manufacturable segments between joint stations.
/// Lateral position is sampled per segment from a continuous path function — not a bay-minimum rectangle.
/// </summary>
internal static class LongitudinalRunEmitter
{
    public readonly record struct RunSpec(
        string RunId,
        string AssemblyId,
        string Role,
        float Up,
        float HalfCrossY,
        float HalfUp,
        float Z0,
        float Z1,
        Func<float, float> YAtStem,
        string? Purpose = null);

    /// <summary>
    /// Place bar segments from <paramref name="z0"/> to <paramref name="z1"/>, cutting at sorted joint stations.
    /// </summary>
    public static int Emit(
        List<CadEntity> entities,
        float loa,
        float web,
        IReadOnlyList<float> jointZs,
        RunSpec run)
    {
        if (run.Z1 <= run.Z0 + 0.4f)
            return 0;

        var cuts = jointZs
            .Where(z => z > run.Z0 + 0.15f && z < run.Z1 - 0.15f)
            .Distinct()
            .OrderBy(z => z)
            .ToList();
        cuts.Insert(0, run.Z0);
        cuts.Add(run.Z1);

        var placed = 0;
        for (var i = 0; i < cuts.Count - 1; i++)
        {
            var a = cuts[i];
            var b = cuts[i + 1];
            var len = b - a;
            if (len < 0.45f)
                continue;

            var zc = (a + b) * 0.5f;
            var halfLenZ = len * 0.5f + web * 0.35f; // slight overlap into joints
            halfLenZ = MathF.Min(halfLenZ, (run.Z1 - run.Z0) * 0.5f + web);
            var y = run.YAtStem(zc);
            if (!float.IsFinite(y))
                continue;

            var name = $"{run.RunId}-s{i}";
            var ok = CalypsoLockGenerator.TryAddStructureBar(
                entities,
                loa,
                name,
                y,
                run.Up,
                zc,
                run.HalfCrossY,
                run.HalfUp,
                halfLenZ,
                meta: new StructuralSolidMeta(
                    run.Role,
                    run.AssemblyId,
                    StationZ: zc,
                    DeckUp: run.Up,
                    Joins: null));
            if (ok)
                placed++;
        }

        return placed;
    }

    public static List<float> MergeJoints(
        float z0,
        float z1,
        IEnumerable<float> frameZs,
        IEnumerable<float> bulkheadZs)
    {
        var list = frameZs.Concat(bulkheadZs)
            .Where(z => z >= z0 - 0.05f && z <= z1 + 0.05f)
            .Distinct()
            .OrderBy(z => z)
            .ToList();
        return list;
    }
}
