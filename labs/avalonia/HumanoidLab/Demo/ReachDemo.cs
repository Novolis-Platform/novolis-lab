using System.Numerics;
using Novolis.Simulation.Humanoid;
using HumanoidLab.Ui;

namespace HumanoidLab.Demo;

/// <summary>Full-body IK dogfood: idle sway + click-drag L/R hand / head targets.</summary>
internal sealed class ReachDemo
{
    private readonly HumanoidBindPose _bind;
    private readonly HumanoidPose _pose;
    private float _time;
    private bool _dragging;
    private ReachDragTarget _dragTarget = ReachDragTarget.None;
    private Vector3 _leftHand;
    private Vector3 _rightHand;
    private Vector3 _head;
    private Vector3 _leftFoot;
    private Vector3 _rightFoot;
    private bool _targetsSeeded;
    private StickFigurePane? _boundPane;

    public ReachDemo(HumanoidBindPose bind)
    {
        _bind = bind;
        _pose = HumanoidPose.FromBind(bind);
    }

    public void Tick(float dt, StickFigurePane pane)
    {
        BindPane(pane);
        _time += dt;

        if (!_targetsSeeded || !_dragging)
            SeedOrSwayTargets();

        var world = HumanoidPoseSolver.SolveWorld(_bind, _pose);

        var targets = HumanoidFullBodyIkTargets.WithDefaults();
        targets.LeftHand = _leftHand;
        targets.RightHand = _rightHand;
        targets.LeftFoot = _leftFoot;
        targets.RightFoot = _rightFoot;
        targets.Head = _head;
        HumanoidFullBodyIk.Apply(world, _bind, targets);
        HumanoidPoseSolver.BakeLocal(_bind, world, _pose);

        pane.ViewMode = StickViewMode.FrontXy;
        pane.EnableReachDrag = true;
        pane.Caption = _dragging
            ? $"Reach — dragging {_dragTarget} (BakeLocal pose)"
            : "Reach — drag amber handles (hands/head); idle sway when free";
        pane.ClearExtras();
        pane.SetBoneGuides(HumanoidDebugDraw.BuildSegments(world));
        pane.SetMannequin(MannequinBuilder.FromWorldPose(world), MannequinBuilder.HeadCenter(world));
        pane.SetReachTargets(_leftHand, _rightHand, _head);
        pane.SetOverlaySegments(
            _leftHand, _leftHand + Vector3.UnitY * 0.05f,
            _rightHand, _rightHand + Vector3.UnitY * 0.05f,
            _head, _head + Vector3.UnitY * 0.05f);
    }

    private void BindPane(StickFigurePane pane)
    {
        if (ReferenceEquals(_boundPane, pane))
            return;
        if (_boundPane is not null)
            _boundPane.ReachTargetDragged -= OnDragged;
        _boundPane = pane;
        pane.ReachTargetDragged += OnDragged;
        pane.PointerReleased += (_, _) =>
        {
            _dragging = false;
            _dragTarget = ReachDragTarget.None;
        };
    }

    private void OnDragged(ReachDragTarget which, Vector3 world)
    {
        _dragging = true;
        _dragTarget = which;
        switch (which)
        {
            case ReachDragTarget.LeftHand:
                _leftHand = world;
                break;
            case ReachDragTarget.RightHand:
                _rightHand = world;
                break;
            case ReachDragTarget.Head:
                _head = world;
                break;
        }
    }

    private void SeedOrSwayTargets()
    {
        var rest = HumanoidPoseSolver.SolveWorld(_bind, HumanoidPose.FromBind(_bind));
        if (!_targetsSeeded)
        {
            _leftHand = rest.Position(HumanoidBone.LeftHand);
            _rightHand = rest.Position(HumanoidBone.RightHand);
            _head = rest.Position(HumanoidBone.Head);
            _leftFoot = rest.Position(HumanoidBone.LeftFoot);
            _rightFoot = rest.Position(HumanoidBone.RightFoot);
            _targetsSeeded = true;
        }

        if (_dragging)
            return;

        var sway = 0.18f * MathF.Sin(_time * 1.7f);
        var lift = 0.12f + 0.08f * MathF.Sin(_time * 2.3f);
        _leftHand = rest.Position(HumanoidBone.LeftHand) + new Vector3(-0.1f + sway, lift, 0.22f);
        _rightHand = rest.Position(HumanoidBone.RightHand) + new Vector3(0.1f - sway, lift, 0.22f);
        _leftFoot = rest.Position(HumanoidBone.LeftFoot) + new Vector3(-0.04f, 0.02f, 0.06f * MathF.Sin(_time));
        _rightFoot = rest.Position(HumanoidBone.RightFoot) + new Vector3(0.04f, 0.02f, -0.06f * MathF.Sin(_time));
        _head = rest.Position(HumanoidBone.Head) + new Vector3(0.06f * MathF.Sin(_time * 0.9f), 0.04f, 0.08f);
    }
}
