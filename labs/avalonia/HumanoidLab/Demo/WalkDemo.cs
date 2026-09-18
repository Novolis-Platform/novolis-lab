using Novolis.Game.Humanoid;
using Novolis.Simulation.Humanoid;
using HumanoidLab.Ui;

namespace HumanoidLab.Demo;

internal sealed class WalkDemo
{
    private readonly HumanoidBindPose _bind;
    private readonly HumanoidClipBank _bank;
    private readonly HumanoidPose _pose = new();
    private float _time;

    public WalkDemo(HumanoidBindPose bind, HumanoidClipBank bank)
    {
        _bind = bind;
        _bank = bank;
    }

    public void Tick(float dt, StickFigurePane pane)
    {
        _time += dt;
        if (!_bank.Sample(LocomotionClipKind.Walk, _time, _pose, _bind))
            return;

        var world = HumanoidPoseSolver.SolveWorld(_bind, _pose);
        pane.ViewMode = StickViewMode.SideZy;
        pane.Caption = "Walk — capsule mannequin + clip FK";
        pane.ClearExtras();
        pane.SetBoneGuides(HumanoidDebugDraw.BuildSegments(world));
        pane.SetMannequin(MannequinBuilder.FromWorldPose(world), MannequinBuilder.HeadCenter(world));
    }
}
