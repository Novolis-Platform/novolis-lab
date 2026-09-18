using System.Numerics;

namespace RandoriFight.Game;

/// <summary>Local-space body and katana geometry (facing +X before mirror).</summary>
internal readonly struct KatanaPose(
    Vector3 leftFoot,
    Vector3 rightFoot,
    Vector3 hips,
    Vector3 chest,
    Vector3 head,
    Vector3 leftHand,
    Vector3 rightHand,
    Vector3 bladeRoot,
    Vector3 bladeTip)
{
    public Vector3 LeftFoot { get; } = leftFoot;
    public Vector3 RightFoot { get; } = rightFoot;
    public Vector3 Hips { get; } = hips;
    public Vector3 Chest { get; } = chest;
    public Vector3 Head { get; } = head;
    public Vector3 LeftHand { get; } = leftHand;
    public Vector3 RightHand { get; } = rightHand;
    public Vector3 BladeRoot { get; } = bladeRoot;
    public Vector3 BladeTip { get; } = bladeTip;
}

internal static class KatanaPoses
{
    private static readonly KatanaPose Chudan = Build(
        leftFoot: new(-0.2f, 0f, 0.14f),
        rightFoot: new(0.12f, 0f, -0.1f),
        hips: new(0f, 0.48f, 0f),
        chest: new(0.02f, 1.02f, 0f),
        head: new(0.04f, 1.58f, 0f),
        leftHand: new(0.02f, 0.98f, 0.02f),
        rightHand: new(0.08f, 1.02f, 0f),
        bladeRoot: new(0.1f, 1.04f, 0f),
        bladeTip: new(0.82f, 1.18f, 0f));

    public static KatanaPose Solve(FighterState state, float phase, float walkPhase, float stateTime = 0f)
    {
        return state switch
        {
            FighterState.Walk => Blend(Chudan, WalkKamae(walkPhase), 0.35f),
            FighterState.Men => SampleMen(phase),
            FighterState.Kesa => SampleKesa(phase),
            FighterState.Thrust => SampleThrust(phase),
            FighterState.Do => SampleDo(phase),
            FighterState.Kote => SampleKote(phase),
            FighterState.Kirioroshi => SampleKirioroshi(phase),
            FighterState.Parry => ParryKamae,
            FighterState.HitStun => Blend(Chudan, HitReact, Math.Clamp(stateTime / 0.2f, 0f, 1f)),
            FighterState.Ko => KoPose,
            _ => Chudan,
        };
    }

    private static KatanaPose SampleMen(float t)
    {
        if (t < 0.38f)
        {
            var u = EaseIn(t / 0.38f);
            return Blend(Chudan, Jodan, u);
        }

        if (t < 0.62f)
        {
            var u = EaseOut((t - 0.38f) / 0.24f);
            return Blend(Jodan, MenCut, u);
        }

        var r = EaseInOut((t - 0.62f) / 0.38f);
        return Blend(MenCut, Chudan, r);
    }

    private static KatanaPose SampleKesa(float t)
    {
        if (t < 0.35f)
        {
            var u = EaseIn(t / 0.35f);
            return Blend(Chudan, KesaWindup, u);
        }

        if (t < 0.62f)
        {
            var u = EaseOut((t - 0.35f) / 0.27f);
            return Blend(KesaWindup, KesaCut, u);
        }

        var r = EaseInOut((t - 0.62f) / 0.38f);
        return Blend(KesaCut, Chudan, r);
    }

    private static KatanaPose SampleThrust(float t)
    {
        if (t < 0.45f)
        {
            var u = EaseIn(t / 0.45f);
            return Blend(Chudan, ThrustExtend, u);
        }

        var r = EaseInOut((t - 0.45f) / 0.55f);
        return Blend(ThrustExtend, Chudan, r);
    }

    private static KatanaPose SampleDo(float t)
    {
        if (t < 0.32f)
        {
            var u = EaseIn(t / 0.32f);
            return Blend(Chudan, DoWindup, u);
        }

        if (t < 0.58f)
        {
            var u = EaseOut((t - 0.32f) / 0.26f);
            return Blend(DoWindup, DoCut, u);
        }

        var r = EaseInOut((t - 0.58f) / 0.42f);
        return Blend(DoCut, Chudan, r);
    }

    private static KatanaPose SampleKote(float t)
    {
        if (t < 0.34f)
        {
            var u = EaseIn(t / 0.34f);
            return Blend(Chudan, KoteWindup, u);
        }

        if (t < 0.6f)
        {
            var u = EaseOut((t - 0.34f) / 0.26f);
            return Blend(KoteWindup, KoteCut, u);
        }

        var r = EaseInOut((t - 0.6f) / 0.4f);
        return Blend(KoteCut, Chudan, r);
    }

    private static KatanaPose SampleKirioroshi(float t)
    {
        if (t < 0.36f)
        {
            var u = EaseIn(t / 0.36f);
            return Blend(Chudan, KirioroshiWindup, u);
        }

        if (t < 0.6f)
        {
            var u = EaseOut((t - 0.36f) / 0.24f);
            return Blend(KirioroshiWindup, KirioroshiCut, u);
        }

        var r = EaseInOut((t - 0.6f) / 0.4f);
        return Blend(KirioroshiCut, Chudan, r);
    }

    private static KatanaPose WalkKamae(float walk)
    {
        var sway = MathF.Sin(walk) * 0.05f;
        return Build(
            leftFoot: new(-0.2f + sway, 0f, 0.14f),
            rightFoot: new(0.12f - sway, 0f, -0.1f),
            hips: new(0f, 0.48f, 0f),
            chest: new(0.02f, 1.02f, sway * 0.2f),
            head: new(0.04f, 1.58f, 0f),
            leftHand: new(0.02f, 0.98f, 0.02f),
            rightHand: new(0.08f, 1.02f, 0f),
            bladeRoot: new(0.1f, 1.04f, 0f),
            bladeTip: new(0.82f, 1.18f + sway * 0.05f, 0f));
    }

    private static readonly KatanaPose Jodan = Build(
        leftFoot: new(-0.18f, 0f, 0.1f),
        rightFoot: new(0.14f, 0f, -0.12f),
        hips: new(-0.04f, 0.5f, 0f),
        chest: new(-0.06f, 1.08f, 0f),
        head: new(-0.04f, 1.62f, 0f),
        leftHand: new(0.02f, 1.18f, 0f),
        rightHand: new(0.06f, 1.28f, 0f),
        bladeRoot: new(0.04f, 1.22f, 0f),
        bladeTip: new(-0.18f, 1.92f, -0.04f));

    private static readonly KatanaPose MenCut = Build(
        leftFoot: new(-0.22f, 0f, 0.16f),
        rightFoot: new(0.16f, 0f, -0.12f),
        hips: new(0.08f, 0.44f, 0f),
        chest: new(0.14f, 0.96f, 0f),
        head: new(0.12f, 1.5f, 0f),
        leftHand: new(0.1f, 0.92f, 0f),
        rightHand: new(0.16f, 0.98f, 0f),
        bladeRoot: new(0.18f, 0.98f, 0f),
        bladeTip: new(0.95f, 1.08f, 0f));

    private static readonly KatanaPose KesaWindup = Build(
        leftFoot: new(-0.2f, 0f, 0.12f),
        rightFoot: new(0.1f, 0f, -0.08f),
        hips: new(-0.02f, 0.5f, 0f),
        chest: new(-0.04f, 1.06f, 0f),
        head: new(0f, 1.6f, 0f),
        leftHand: new(0.04f, 1.06f, 0.04f),
        rightHand: new(0.12f, 1.14f, 0f),
        bladeRoot: new(0.14f, 1.1f, 0f),
        bladeTip: new(0.42f, 1.52f, 0f));

    private static readonly KatanaPose KesaCut = Build(
        leftFoot: new(-0.24f, 0f, 0.14f),
        rightFoot: new(0.18f, 0f, -0.1f),
        hips: new(0.1f, 0.46f, 0f),
        chest: new(0.16f, 0.98f, 0f),
        head: new(0.1f, 1.52f, 0f),
        leftHand: new(0.14f, 0.9f, 0f),
        rightHand: new(0.2f, 0.94f, 0f),
        bladeRoot: new(0.22f, 0.94f, 0f),
        bladeTip: new(0.92f, 0.78f, 0f));

    private static readonly KatanaPose ThrustExtend = Build(
        leftFoot: new(-0.24f, 0f, 0.1f),
        rightFoot: new(0.2f, 0f, -0.14f),
        hips: new(0.12f, 0.46f, 0f),
        chest: new(0.2f, 1f, 0f),
        head: new(0.18f, 1.56f, 0f),
        leftHand: new(0.22f, 1.02f, 0f),
        rightHand: new(0.28f, 1.04f, 0f),
        bladeRoot: new(0.3f, 1.04f, 0f),
        bladeTip: new(1.28f, 1.06f, 0f));

    private static readonly KatanaPose DoWindup = Build(
        leftFoot: new(-0.2f, 0f, 0.1f),
        rightFoot: new(0.12f, 0f, -0.1f),
        hips: new(-0.02f, 0.5f, 0f),
        chest: new(-0.02f, 1.06f, 0f),
        head: new(0f, 1.6f, 0f),
        leftHand: new(-0.06f, 1.1f, 0.06f),
        rightHand: new(0.02f, 1.16f, 0f),
        bladeRoot: new(0f, 1.12f, 0f),
        bladeTip: new(-0.35f, 1.38f, 0.12f));

    private static readonly KatanaPose DoCut = Build(
        leftFoot: new(-0.22f, 0f, 0.14f),
        rightFoot: new(0.16f, 0f, -0.12f),
        hips: new(0.1f, 0.45f, 0f),
        chest: new(0.16f, 0.95f, 0f),
        head: new(0.12f, 1.48f, 0f),
        leftHand: new(0.12f, 0.9f, 0f),
        rightHand: new(0.18f, 0.92f, 0f),
        bladeRoot: new(0.2f, 0.92f, 0f),
        bladeTip: new(1.05f, 0.98f, 0f));

    private static readonly KatanaPose KoteWindup = Build(
        leftFoot: new(-0.2f, 0f, 0.12f),
        rightFoot: new(0.1f, 0f, -0.08f),
        hips: new(0f, 0.5f, 0f),
        chest: new(0.02f, 1.04f, 0f),
        head: new(0.04f, 1.58f, 0f),
        leftHand: new(0.04f, 1.02f, 0.04f),
        rightHand: new(0.1f, 1.06f, 0f),
        bladeRoot: new(0.12f, 1.06f, 0f),
        bladeTip: new(0.55f, 1.28f, 0f));

    private static readonly KatanaPose KoteCut = Build(
        leftFoot: new(-0.23f, 0f, 0.14f),
        rightFoot: new(0.17f, 0f, -0.1f),
        hips: new(0.12f, 0.44f, 0f),
        chest: new(0.18f, 0.92f, 0f),
        head: new(0.14f, 1.46f, 0f),
        leftHand: new(0.16f, 0.78f, 0f),
        rightHand: new(0.22f, 0.8f, 0f),
        bladeRoot: new(0.24f, 0.8f, 0f),
        bladeTip: new(1.02f, 0.72f, 0f));

    private static readonly KatanaPose KirioroshiWindup = Build(
        leftFoot: new(-0.18f, 0f, 0.1f),
        rightFoot: new(0.14f, 0f, -0.1f),
        hips: new(-0.05f, 0.51f, 0f),
        chest: new(-0.08f, 1.1f, 0f),
        head: new(-0.06f, 1.64f, 0f),
        leftHand: new(-0.02f, 1.2f, -0.02f),
        rightHand: new(0.04f, 1.26f, 0f),
        bladeRoot: new(0.02f, 1.2f, 0f),
        bladeTip: new(-0.22f, 1.85f, -0.06f));

    private static readonly KatanaPose KirioroshiCut = Build(
        leftFoot: new(-0.24f, 0f, 0.16f),
        rightFoot: new(0.18f, 0f, -0.12f),
        hips: new(0.12f, 0.42f, 0f),
        chest: new(0.2f, 0.9f, 0f),
        head: new(0.16f, 1.44f, 0f),
        leftHand: new(0.14f, 0.86f, 0f),
        rightHand: new(0.2f, 0.9f, 0f),
        bladeRoot: new(0.22f, 0.9f, 0f),
        bladeTip: new(1.02f, 0.82f, 0f));

    private static readonly KatanaPose ParryKamae = Build(
        leftFoot: new(-0.2f, 0f, 0.12f),
        rightFoot: new(0.1f, 0f, -0.1f),
        hips: new(0f, 0.48f, 0f),
        chest: new(0.04f, 1.02f, 0f),
        head: new(0.04f, 1.58f, 0f),
        leftHand: new(0.06f, 1.02f, 0f),
        rightHand: new(0.1f, 1.06f, 0f),
        bladeRoot: new(0.12f, 0.92f, 0f),
        bladeTip: new(0.14f, 1.62f, 0f));

    private static readonly KatanaPose HitReact = Build(
        leftFoot: new(-0.14f, 0f, 0.08f),
        rightFoot: new(0.08f, 0f, -0.06f),
        hips: new(-0.1f, 0.46f, 0f),
        chest: new(-0.14f, 0.96f, 0f),
        head: new(-0.1f, 1.5f, 0f),
        leftHand: new(-0.04f, 0.94f, 0.06f),
        rightHand: new(0.02f, 0.98f, 0f),
        bladeRoot: new(0.02f, 0.96f, 0f),
        bladeTip: new(0.35f, 1.22f, 0.08f));

    private static readonly KatanaPose KoPose = Build(
        leftFoot: new(-0.28f, 0f, 0.2f),
        rightFoot: new(0.22f, 0f, -0.16f),
        hips: new(-0.2f, 0.28f, 0f),
        chest: new(-0.24f, 0.62f, 0f),
        head: new(-0.18f, 1.02f, 0f),
        leftHand: new(-0.1f, 0.58f, 0.1f),
        rightHand: new(-0.04f, 0.62f, 0f),
        bladeRoot: new(-0.02f, 0.6f, 0f),
        bladeTip: new(0.42f, 0.72f, 0.12f));

    private static KatanaPose Build(
        Vector3 leftFoot,
        Vector3 rightFoot,
        Vector3 hips,
        Vector3 chest,
        Vector3 head,
        Vector3 leftHand,
        Vector3 rightHand,
        Vector3 bladeRoot,
        Vector3 bladeTip) =>
        new(leftFoot, rightFoot, hips, chest, head, leftHand, rightHand, bladeRoot, bladeTip);

    private static KatanaPose Blend(KatanaPose a, KatanaPose b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new(
            Vector3.Lerp(a.LeftFoot, b.LeftFoot, t),
            Vector3.Lerp(a.RightFoot, b.RightFoot, t),
            Vector3.Lerp(a.Hips, b.Hips, t),
            Vector3.Lerp(a.Chest, b.Chest, t),
            Vector3.Lerp(a.Head, b.Head, t),
            Vector3.Lerp(a.LeftHand, b.LeftHand, t),
            Vector3.Lerp(a.RightHand, b.RightHand, t),
            Vector3.Lerp(a.BladeRoot, b.BladeRoot, t),
            Vector3.Lerp(a.BladeTip, b.BladeTip, t));
    }

    private static float EaseIn(float t) => t * t;
    private static float EaseOut(float t) => 1f - (1f - t) * (1f - t);
    private static float EaseInOut(float t) => t < 0.5f ? 2f * t * t : 1f - MathF.Pow(-2f * t + 2f, 2f) / 2f;
}
