using System.Numerics;
using Novolis.Game.Humanoid;
using Novolis.Simulation.Humanoid;
using HumanoidLab.Ui;

namespace HumanoidLab.Demo;

internal sealed class BowDemo
{
    private readonly HumanoidBindPose _bind;
    private readonly HumanoidClipBank _bank;
    private readonly HumanoidPose _pose = new();
    private float _time;

    public BowDemo(HumanoidBindPose bind, HumanoidClipBank bank)
    {
        _bind = bind;
        _bank = bank;
    }

    public void Tick(float dt, StickFigurePane pane)
    {
        _time += dt;
        if (!_bank.TryGet("bow", out var clip))
            return;
        clip.Sample(_time, _pose, _bind);

        var world = HumanoidPoseSolver.SolveWorld(_bind, _pose);

        var shoulder = world.Position(HumanoidBone.LeftArm);
        var drawAmount = 0.5f + 0.5f * MathF.Sin(_time * MathF.PI);
        var drawTarget = shoulder + new Vector3(-0.05f - 0.28f * drawAmount, 0.05f, -0.12f);
        var upper = Vector3.Distance(_bind[HumanoidBone.LeftArm], _bind[HumanoidBone.LeftForeArm]);
        var lower = Vector3.Distance(_bind[HumanoidBone.LeftForeArm], _bind[HumanoidBone.LeftHand]);
        TwoBoneIk.ApplyLimb(
            world,
            HumanoidBone.LeftArm,
            HumanoidBone.LeftForeArm,
            HumanoidBone.LeftHand,
            drawTarget,
            upper,
            lower,
            Vector3.UnitY);

        var grip = world.Position(HumanoidBone.RightHand);
        var tipUp = grip + new Vector3(0.02f, 0.42f, 0.02f);
        var tipDown = grip + new Vector3(0.02f, -0.42f, 0.02f);
        var belly = grip + new Vector3(0.12f, 0f, 0f);
        var drawHand = world.Position(HumanoidBone.LeftHand);

        pane.ViewMode = StickViewMode.FrontXy;
        pane.Caption = "Bow — mannequin + TwoBoneIk draw";
        pane.ClearExtras();
        pane.SetBoneGuides(HumanoidDebugDraw.BuildSegments(world));
        pane.SetMannequin(MannequinBuilder.FromWorldPose(world), MannequinBuilder.HeadCenter(world));
        pane.SetOverlayPolyline(tipUp, belly, tipDown);
        pane.SetOverlaySegments(tipUp, drawHand, tipDown, drawHand);
    }
}
