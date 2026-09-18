using System.Numerics;
using RandoriFight.Game.Skeleton;

namespace RandoriFight.Game;

internal sealed class Fighter
{
    public float PositionX { get; private set; }
    public int Facing { get; private set; } = 1;
    public float Health { get; private set; } = 100f;
    public FighterState State { get; private set; } = FighterState.Idle;
    public float StateTime { get; private set; }
    public bool IsPlayer { get; init; }
    public bool AttackHitApplied { get; private set; }

    private CombatMove _activeMove;
    private float _walkSpeed = 3.6f;

    public Vector3 WorldPosition => new(PositionX, FightArena.FloorY, 0f);

    public bool IsAlive => Health > 0f && State != FighterState.Ko;
    public bool IsParrying => State == FighterState.Parry;
    public bool IsInHitStun => State == FighterState.HitStun;
    public bool IsAttacking => State is FighterState.Men or FighterState.Kesa or FighterState.Thrust
        or FighterState.Do or FighterState.Kote or FighterState.Kirioroshi;

    public KatanaPose CurrentPose =>
        KatanaPoses.Solve(State, MoveNormalizedTime, MoveAnimPhase, StateTime);

    public SkeletonFrame CurrentSkeleton =>
        HumanoidSkeleton.SolveFromLandmarks(CurrentPose, WorldPosition, Facing);

    public bool IsStrikeWindow => IsInActivePhase();

    public float MoveNormalizedTime =>
        IsAttacking ? Math.Clamp(StateTime / _activeMove.Total, 0f, 1f) : 0f;

    public void Reset(float x, int facing)
    {
        PositionX = x;
        Facing = facing;
        Health = 100f;
        State = FighterState.Idle;
        StateTime = 0f;
        AttackHitApplied = false;
    }

    public void FaceToward(float otherX)
    {
        if (IsAttacking || IsInHitStun)
            return;

        Facing = otherX >= PositionX ? 1 : -1;
    }

    public void SetWalkInput(float direction)
    {
        if (!CanMove())
            return;

        if (MathF.Abs(direction) < 0.01f)
        {
            if (State == FighterState.Walk)
                Enter(FighterState.Idle);
            return;
        }

        Facing = direction > 0f ? 1 : -1;
        State = FighterState.Walk;
    }

    public void TryParry(bool held)
    {
        if (!IsAlive || IsInHitStun || IsAttacking)
            return;

        if (held)
        {
            State = FighterState.Parry;
            StateTime = 0f;
            return;
        }

        if (State == FighterState.Parry)
            Enter(FighterState.Idle);
    }

    public bool TryTechnique(FighterState technique) =>
        TryAttack(technique, CombatMoves.For(technique));

    public void Update(float deltaSeconds, float moveInput, bool parryHeld)
    {
        StateTime += deltaSeconds;

        if (State == FighterState.Walk)
        {
            PositionX = FightArena.ClampX(PositionX + moveInput * _walkSpeed * deltaSeconds);
            if (MathF.Abs(moveInput) < 0.01f)
                Enter(FighterState.Idle);
            return;
        }

        if (State == FighterState.Parry)
        {
            if (!parryHeld)
                Enter(FighterState.Idle);
            return;
        }

        if (State == FighterState.HitStun && StateTime >= _activeMove.HitStun)
            Enter(FighterState.Idle);

        if (!IsAttacking)
            return;

        if (StateTime >= _activeMove.Total)
            Enter(FighterState.Idle);
    }

    public void ApplyDamage(float amount, float hitStun)
    {
        if (!IsAlive)
            return;

        if (IsParrying)
        {
            Health -= amount * 0.08f;
            State = FighterState.Parry;
            StateTime = 0f;
            return;
        }

        Health = MathF.Max(0f, Health - amount);
        _activeMove = new CombatMove(0f, 0f, 0f, 0f, 0f, 0f, hitStun, 1f);
        State = FighterState.HitStun;
        StateTime = 0f;
        AttackHitApplied = false;

        if (Health <= 0f)
        {
            Health = 0f;
            State = FighterState.Ko;
        }
    }

    public bool TryApplyAttackHit(Fighter defender)
    {
        if (!IsAttacking || AttackHitApplied || !IsInActivePhase())
            return false;

        var tip = CurrentSkeleton.BladeTip;
        var target = defender.WorldPosition + new Vector3(0f, _activeMove.StrikeHeight, 0f);
        if (Vector3.Distance(tip, target) > _activeMove.Radius)
            return false;

        AttackHitApplied = true;
        defender.ApplyDamage(_activeMove.Damage, _activeMove.HitStun);
        return true;
    }

    public float MoveAnimPhase => State == FighterState.Walk ? StateTime * 7.5f : 0f;

    private bool CanMove() =>
        IsAlive && State is FighterState.Idle or FighterState.Walk;

    private bool TryAttack(FighterState state, CombatMove move)
    {
        if (!IsAlive || State is not (FighterState.Idle or FighterState.Walk))
            return false;

        BeginAttack(state, move);
        return true;
    }

    private bool IsInActivePhase()
    {
        if (!IsAttacking)
            return false;

        var t = MoveNormalizedTime;
        return State switch
        {
            FighterState.Men => t is >= 0.36f and <= 0.58f,
            FighterState.Kesa => t is >= 0.34f and <= 0.6f,
            FighterState.Thrust => t is >= 0.4f and <= 0.58f,
            FighterState.Do => t is >= 0.32f and <= 0.58f,
            FighterState.Kote => t is >= 0.35f and <= 0.6f,
            FighterState.Kirioroshi => t is >= 0.38f and <= 0.62f,
            _ => StateTime >= _activeMove.Startup && StateTime <= _activeMove.Startup + _activeMove.Active,
        };
    }

    private void BeginAttack(FighterState state, CombatMove move)
    {
        State = state;
        _activeMove = move;
        StateTime = 0f;
        AttackHitApplied = false;
    }

    private void Enter(FighterState state)
    {
        State = state;
        StateTime = 0f;
        AttackHitApplied = false;
    }
}
