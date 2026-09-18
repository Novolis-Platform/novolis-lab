using System.Numerics;
using System.Runtime.InteropServices;
using Novolis.Math.Geometry;
using Novolis.Physics.Cloth;
using Novolis.Physics.Collision.Simple;
using Novolis.Physics.Joints;

namespace ClothPlay.Game;

/// <summary>Headless smoke: stiff flag hang, horizontal blade cut, blast, no ground kiss.</summary>
internal static class ClothSmoke
{
    public static int Run()
    {
        try
        {
            if (!RunFlagSmoke())
                return 1;
            if (!RunHorizontalCutSmoke())
                return 1;

            Console.WriteLine("ClothSmoke OK flag+katana-cut+blast");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ClothSmoke FAIL: {ex}");
            return 1;
        }
    }

    private static bool RunFlagSmoke()
    {
        var options = new ClothSheetOptions
        {
            Columns = 10,
            Rows = 8,
            Spacing = 0.12f,
            PinMode = ClothPinMode.TopRow,
            IncludeShear = true,
            IncludeBend = true,
            ShearStiffness = 1f,
            BendStiffness = 0.85f,
        };

        var spheres = new List<SphereState>();
        var joints = new List<DistanceJoint>();
        var pins = new List<int>();
        var anchors = new List<Vector3>();
        var origin = new Vector3(0f, 3.2f, 0f);

        ClothSheetPreset.BuildHanging(
            origin,
            Vector3.UnitX,
            -Vector3.UnitY,
            options,
            spheres,
            joints,
            pins,
            anchors);

        var sim = new ClothSheetSimulator
        {
            Options =
            {
                Radius = 0.03f,
                LinearDragPerSecond = 1.8,
                SphereRestitution = 0f,
                StaticRestitution = 0f,
                SleepSpeedThreshold = 0f,
                MaxSpeedMps = 8f,
            },
            JointIterations = 24,
            JointRelaxIterations = 8,
            ConstraintPasses = 4,
            MaxStrainFraction = 3f,
            MaxStretchRatio = 1.08f,
            StretchLimitIterations = 16,
            WindAcceleration = new Vector3(2.5f, 0f, 0.3f),
        };
        sim.SetJoints(joints);
        sim.SetPins(CollectionsMarshal.AsSpan(pins), CollectionsMarshal.AsSpan(anchors));

        var world = new BvhStaticWorld(new TriangleMesh([], []));
        var clamp = WideClamp();

        for (var i = 0; i < 150; i++)
            sim.Step(world, spheres, clamp, 1f / 60f);

        for (var i = 0; i < pins.Count; i++)
        {
            var err = Vector3.Distance(spheres[pins[i]].Position, anchors[i]);
            if (err > 1e-3f)
            {
                Console.Error.WriteLine($"ClothSmoke FAIL: pin {pins[i]} drifted {err:F5}m");
                return false;
            }
        }

        // Flag bottom must stay off the floor.
        var minY = float.MaxValue;
        for (var i = 0; i < spheres.Count; i++)
        {
            if (pins.Contains(i))
                continue;
            minY = MathF.Min(minY, spheres[i].Position.Y);
        }

        if (minY < ClothSheet.GroundFailY)
        {
            Console.Error.WriteLine($"ClothSmoke FAIL: flag hit ground Y={minY:F3}");
            return false;
        }

        // Structural stretch check — dough would exceed ~20% easily.
        var maxStretch = 0f;
        foreach (var joint in joints)
        {
            var d = Vector3.Distance(spheres[joint.SphereA].Position, spheres[joint.SphereB].Position);
            maxStretch = MathF.Max(maxStretch, d / joint.RestLength);
        }

        if (maxStretch > 1.12f)
        {
            Console.Error.WriteLine($"ClothSmoke FAIL: dough stretch {maxStretch:F2}x rest");
            return false;
        }

        Console.WriteLine($"ClothSmoke flag minY={minY:F2} maxStretch={maxStretch:F2}");
        return true;
    }

    private static bool RunHorizontalCutSmoke()
    {
        var options = new ClothSheetOptions
        {
            Columns = 12,
            Rows = 10,
            Spacing = 0.12f,
            IncludeShear = true,
            IncludeBend = false,
            PinMode = ClothPinMode.None,
        };

        var spheres = new List<SphereState>();
        var joints = new List<DistanceJoint>();
        ClothSheetPreset.BuildHanging(
            new Vector3(-0.66f, 2.1f, -0.54f),
            Vector3.UnitX,
            Vector3.UnitZ,
            options,
            spheres,
            joints,
            new List<int>(),
            new List<Vector3>());

        var before = joints.Count;
        // Horizontal katana contact ridge along Z at mid X.
        var blade = new ClothBlade(
            new Vector3(0f, 2.1f, -0.7f),
            new Vector3(0f, 2.1f, 0.7f),
            halfThickness: 0.1f);
        var bladeCut = ClothCutOps.CutWithBlade(joints, spheres, blade);
        if (bladeCut.SeveredJointCount < 6)
        {
            Console.Error.WriteLine($"ClothSmoke FAIL: horizontal blade severed only {bladeCut.SeveredJointCount}");
            return false;
        }

        var mid = spheres[ClothSheetPreset.Index(6, 5, 12)].Position;
        var blast = new ClothBlast(mid, radius: 0.3f, impulseSpeed: 4f);
        var blastCut = ClothCutOps.CutWithBlast(joints, spheres, blast);
        ClothCutOps.ApplyBlastImpulse(spheres, blast);

        if (joints.Count >= before)
        {
            Console.Error.WriteLine("ClothSmoke FAIL: joints did not drop after cuts");
            return false;
        }

        Console.WriteLine(
            $"ClothSmoke cut blade={bladeCut.SeveredJointCount} blast={blastCut.SeveredJointCount} remain={joints.Count}");
        return true;
    }

    private static InteriorClampVolume WideClamp() => new()
    {
        MinX = -8f,
        MaxX = 8f,
        MinY = -1f,
        MaxY = 8f,
        MinZ = -8f,
        MaxZ = 8f,
    };
}
