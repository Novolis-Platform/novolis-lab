using System.Drawing;
using System.Numerics;
using Novolis.Physics.Ballistics;
using Novolis.Silk;

namespace ArtillerySimulator;

internal static class AimWireComposer
{
    public static void Draw(
        SilkWireBatch wires,
        TerrainWorld terrain,
        AimPreviewService aim,
        bool unsafeSol)
    {
        var ordered = unsafeSol ? BallisticArcColors.OrderedUnsafe : BallisticArcColors.Ordered;
        var mpiColor = unsafeSol ? BallisticArcColors.MpiMarkUnsafe : BallisticArcColors.MpiMark;

        DrawProbabilityFootprint(wires, terrain, aim, unsafeSol);

        if (aim.Tube.Count >= 2)
            BallisticArcWire.Draw(wires, aim.Tube, BallisticArcColors.Tube, maxSegments: 320);

        if (aim.Ordered.Count >= 2)
        {
            BallisticArcWire.Draw(wires, aim.Ordered, ordered, maxSegments: 480);
            var mpi = aim.Ordered[^1];
            wires.WireSphere(mpi, 28f, mpiColor, segments: 10);
            wires.Ring(mpi, 40f, mpiColor);
            var ground = terrain.Field.SampleHeight(mpi.X, mpi.Z);
            wires.Line(mpi, new Vector3(mpi.X, ground + 1f, mpi.Z), mpiColor);
        }
    }

    private static void DrawProbabilityFootprint(
        SilkWireBatch wires,
        TerrainWorld terrain,
        AimPreviewService aim,
        bool unsafeSol)
    {
        if (aim.Dispersion.Count < 2 || aim.Ordered.Count < 2)
            return;

        var footprint = unsafeSol
            ? BallisticArcColors.ProbabilityFootprintUnsafe
            : BallisticArcColors.ProbabilityFootprint;

        var impacts = new List<Vector3>(aim.Dispersion.Count + 1);
        foreach (var arc in aim.Dispersion)
            impacts.Add(arc[^1]);
        impacts.Add(aim.Ordered[^1]);

        var cx = 0f;
        var cz = 0f;
        foreach (var p in impacts)
        {
            cx += p.X;
            cz += p.Z;
        }

        cx /= impacts.Count;
        cz /= impacts.Count;
        var radius = 0f;
        foreach (var p in impacts)
        {
            var dx = p.X - cx;
            var dz = p.Z - cz;
            radius = MathF.Max(radius, MathF.Sqrt(dx * dx + dz * dz));
        }

        radius = Math.Clamp(radius, 40f, 900f);
        var y = terrain.Field.SampleHeight(cx, cz) + 2.5f;
        wires.Ring(new Vector3(cx, y, cz), radius, footprint, segments: 36, yLift: 0f);
    }
}
