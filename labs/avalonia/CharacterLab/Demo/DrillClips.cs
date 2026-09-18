using System.Numerics;
using Novolis.Game.Humanoid;
using Novolis.Simulation.Humanoid;

namespace CharacterLab.Demo;

/// <summary>Procedural full-body drill: Order Arms → Present Arms → Hand Salute (loop).</summary>
internal static class DrillClips
{
    public const float Duration = 10f;

    public static readonly (string Id, float Time, string Label)[] Phases =
    [
        ("order", 0.6f, "Order Arms"),
        ("present", 3.6f, "Present Arms"),
        ("salute", 6.6f, "Hand Salute"),
        ("recover", 8.5f, "Recover"),
    ];

    public static HumanoidClipBank CreateBank(HumanoidBindPose bind) =>
        new HumanoidClipBank().Set("drill", CreateDrill(bind));

    public static float TimeForPhase(string phase)
    {
        var p = phase.Trim().ToLowerInvariant();
        foreach (var (id, time, _) in Phases)
        {
            if (p.Contains(id, StringComparison.Ordinal))
                return time;
        }

        return 0.6f;
    }

    public static HumanoidAnimationClip CreateDrill(HumanoidBindPose bind)
    {
        var hips = bind[HumanoidBone.Hips];
        var clip = new HumanoidAnimationClip("drill") { Loop = true };

        // Order arms — attention, weight on right, rifle hand down, slight torso set.
        var order = Pose(
            rArm: Q(z: -0.28f, x: 0.35f),
            rFore: Q(x: -0.55f),
            lArm: Q(z: 0.22f, x: 0.08f),
            lFore: Q(x: -0.12f),
            rUpLeg: Q(x: 0.10f, y: -0.04f),
            rLeg: Q(x: -0.16f),
            lUpLeg: Q(x: -0.04f, y: 0.03f),
            lLeg: Q(x: -0.06f),
            spine: Q(y: 0.06f, x: 0.04f),
            spine1: Q(x: 0.03f),
            spine2: Q(x: 0.05f, y: 0.04f),
            neck: Q(x: -0.04f),
            head: Q(x: -0.06f),
            rShoulder: Q(z: -0.12f),
            lShoulder: Q(z: 0.08f));

        // Present arms — reach forward; keep spine lean modest so auto-skin stays readable.
        var present = Pose(
            rArm: Q(z: -0.85f, y: -0.75f, x: -0.45f),
            rFore: Q(x: -1.35f),
            lArm: Q(z: 0.80f, y: 0.70f, x: -0.40f),
            lFore: Q(x: -1.25f),
            rUpLeg: Q(x: 0.18f, y: -0.04f),
            rLeg: Q(x: -0.28f),
            lUpLeg: Q(x: 0.16f, y: 0.04f),
            lLeg: Q(x: -0.24f),
            spine: Q(y: 0.03f, x: 0.12f),
            spine1: Q(x: 0.10f),
            spine2: Q(x: 0.12f, y: 0.03f),
            neck: Q(x: -0.06f),
            head: Q(x: -0.08f),
            rShoulder: Q(z: -0.30f, y: -0.15f),
            lShoulder: Q(z: 0.30f, y: 0.15f));

        // Hand salute — chest up, right hand to brow, left steadies rifle, weight shift.
        var salute = Pose(
            rArm: Q(z: -1.75f, y: -0.95f, x: -0.85f),
            rFore: Q(x: -1.95f),
            lArm: Q(z: 0.35f, x: 0.18f),
            lFore: Q(x: -0.75f),
            rUpLeg: Q(x: 0.18f, y: -0.08f),
            rLeg: Q(x: -0.28f),
            lUpLeg: Q(x: 0.10f, y: 0.06f),
            lLeg: Q(x: -0.16f),
            spine: Q(y: 0.14f, x: -0.10f),
            spine1: Q(x: -0.06f, y: 0.08f),
            spine2: Q(x: -0.12f, y: 0.10f),
            neck: Q(x: 0.04f, y: -0.08f),
            head: Q(x: 0.06f, y: -0.10f),
            rShoulder: Q(z: -0.55f, y: -0.35f),
            lShoulder: Q(z: 0.18f));

        void Key(float t, Dictionary<HumanoidBone, Quaternion> locals, Vector3? root = null)
        {
            clip.AddKey(new HumanoidKeyframe
            {
                TimeSeconds = t,
                RootTranslation = root ?? hips,
                LocalRotations = locals,
            });
        }

        Key(0.0f, order);
        Key(1.5f, order, hips + new Vector3(0.01f, 0f, 0f));
        Key(2.8f, Blend(order, present, 0.5f), hips + new Vector3(0.03f, -0.01f, 0.05f));
        Key(3.6f, present, hips + new Vector3(0.05f, -0.02f, 0.09f));
        Key(5.0f, present, hips + new Vector3(0.05f, -0.02f, 0.09f));
        Key(5.8f, Blend(present, salute, 0.5f), hips + new Vector3(0.02f, -0.01f, 0.04f));
        Key(6.6f, salute, hips + new Vector3(-0.03f, 0f, 0.02f));
        Key(8.0f, salute, hips + new Vector3(-0.03f, 0f, 0.02f));
        Key(9.0f, Blend(salute, order, 0.5f), hips + new Vector3(-0.01f, 0f, 0.01f));
        Key(Duration, order);
        return clip;
    }

    public static string PhaseName(float timeInClip)
    {
        var t = timeInClip % Duration;
        if (t < 1.5f) return "Order Arms";
        if (t < 2.8f) return "Moving to Present Arms";
        if (t < 5.0f) return "Present Arms";
        if (t < 5.8f) return "Moving to Salute";
        if (t < 8.0f) return "Hand Salute";
        if (t < 9.0f) return "Recover";
        return "Order Arms";
    }

    static Quaternion Q(float x = 0f, float y = 0f, float z = 0f) =>
        Quaternion.CreateFromAxisAngle(Vector3.UnitZ, z) *
        Quaternion.CreateFromAxisAngle(Vector3.UnitY, y) *
        Quaternion.CreateFromAxisAngle(Vector3.UnitX, x);

    static Dictionary<HumanoidBone, Quaternion> Pose(
        Quaternion? rArm = null,
        Quaternion? rFore = null,
        Quaternion? lArm = null,
        Quaternion? lFore = null,
        Quaternion? rUpLeg = null,
        Quaternion? rLeg = null,
        Quaternion? lUpLeg = null,
        Quaternion? lLeg = null,
        Quaternion? spine = null,
        Quaternion? spine1 = null,
        Quaternion? spine2 = null,
        Quaternion? neck = null,
        Quaternion? head = null,
        Quaternion? rShoulder = null,
        Quaternion? lShoulder = null)
    {
        var d = new Dictionary<HumanoidBone, Quaternion>();
        void Put(HumanoidBone b, Quaternion? q)
        {
            if (q is { } v)
                d[b] = v;
        }

        Put(HumanoidBone.RightArm, rArm);
        Put(HumanoidBone.RightForeArm, rFore);
        Put(HumanoidBone.LeftArm, lArm);
        Put(HumanoidBone.LeftForeArm, lFore);
        Put(HumanoidBone.RightUpLeg, rUpLeg);
        Put(HumanoidBone.RightLeg, rLeg);
        Put(HumanoidBone.LeftUpLeg, lUpLeg);
        Put(HumanoidBone.LeftLeg, lLeg);
        Put(HumanoidBone.Spine, spine);
        Put(HumanoidBone.Spine1, spine1);
        Put(HumanoidBone.Spine2, spine2);
        Put(HumanoidBone.Neck, neck);
        Put(HumanoidBone.Head, head);
        Put(HumanoidBone.RightShoulder, rShoulder);
        Put(HumanoidBone.LeftShoulder, lShoulder);
        return d;
    }

    static Dictionary<HumanoidBone, Quaternion> Blend(
        Dictionary<HumanoidBone, Quaternion> a,
        Dictionary<HumanoidBone, Quaternion> b,
        float t)
    {
        var keys = a.Keys.Union(b.Keys);
        var d = new Dictionary<HumanoidBone, Quaternion>();
        foreach (var bone in keys)
        {
            var qa = a.GetValueOrDefault(bone, Quaternion.Identity);
            var qb = b.GetValueOrDefault(bone, Quaternion.Identity);
            d[bone] = Quaternion.Slerp(qa, qb, t);
        }

        return d;
    }
}
