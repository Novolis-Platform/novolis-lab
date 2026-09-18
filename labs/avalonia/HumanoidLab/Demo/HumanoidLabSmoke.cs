using System.Numerics;
using Novolis.Simulation.Humanoid;

namespace HumanoidLab;

/// <summary>Headless smoke: FullBodyIk + BakeLocal without Avalonia UI.</summary>
internal static class HumanoidLabSmoke
{
    public static int Run()
    {
        var bind = HumanoidBindPose.CreateDefaultTPose(1.8f);
        var pose = HumanoidPose.FromBind(bind);
        var world = HumanoidPoseSolver.SolveWorld(bind, pose);
        var hipsBefore = world.Position(HumanoidBone.Hips);

        var left = world.Position(HumanoidBone.LeftHand) + new Vector3(-0.12f, 0.2f, 0.15f);
        var right = world.Position(HumanoidBone.RightHand) + new Vector3(0.12f, 0.2f, 0.15f);
        var targets = HumanoidFullBodyIkTargets.WithDefaults();
        targets.LeftHand = left;
        targets.RightHand = right;
        HumanoidFullBodyIk.Apply(world, bind, targets);

        var leftErr = Vector3.Distance(world.Position(HumanoidBone.LeftHand), left);
        var rightErr = Vector3.Distance(world.Position(HumanoidBone.RightHand), right);
        var hipsErr = Vector3.Distance(world.Position(HumanoidBone.Hips), hipsBefore);
        if (leftErr > 1e-2f || rightErr > 1e-2f)
        {
            Console.Error.WriteLine($"HumanoidLabSmoke FAIL hand reach L={leftErr:F4} R={rightErr:F4}");
            return 1;
        }

        if (hipsErr > 1e-4f)
        {
            Console.Error.WriteLine($"HumanoidLabSmoke FAIL hips moved {hipsErr:F6}");
            return 1;
        }

        HumanoidPoseSolver.BakeLocal(bind, world, pose);
        var again = HumanoidPoseSolver.SolveWorld(bind, pose);
        var bakeErr = 0f;
        for (var i = 0; i < (int)HumanoidBone.Count; i++)
        {
            var bone = (HumanoidBone)i;
            bakeErr = MathF.Max(bakeErr, Vector3.Distance(world.Position(bone), again.Position(bone)));
        }

        // After IK, bake→FK restores hierarchical bind lengths; tips stay close via aiming.
        if (bakeErr > 0.35f)
        {
            Console.Error.WriteLine($"HumanoidLabSmoke FAIL bake round-trip maxErr={bakeErr:F3}");
            return 1;
        }

        Console.WriteLine($"HumanoidLabSmoke OK reach L={leftErr:F4} R={rightErr:F4} bakeMax={bakeErr:F3}");
        return 0;
    }
}
