using System.Numerics;

namespace KatoriLab.Demo;

/// <summary>
/// Full flowing dojo kata: door rei → walk to center → opening pose → ken → closing rei.
/// Stylized educational dogfood — not an official Tenshin Shōden Katori Shintō-ryū kata.
/// </summary>
internal static class KenTimeline
{
    public const float Duration = 36f;
    public const float BokkenLength = 1.02f;

    /// <summary>Door on −Z; shomen / center near origin facing +Z.</summary>
    public const float DoorZ = -2.4f;
    public const float CenterZ = 0f;

    public static readonly (string Id, float Time, string Label)[] Phases =
    [
        ("door", 1.2f, "Door — rei"),
        ("walk", 4.5f, "Walk to center"),
        ("opening", 8.5f, "Opening pose"),
        ("chudan", 12.5f, "Chūdan-no-kamae"),
        ("jodan", 17.0f, "Jōdan-no-kamae"),
        ("kesagiri", 19.8f, "Kesagiri"),
        ("gedan", 23.0f, "Gedan-no-kamae"),
        ("closing", 29.5f, "Closing rei"),
        ("leave", 33.5f, "Return to door"),
    ];

    /// <summary>Hip-local key: grip = mid-tsuka, tipDir unit toward kissaki. Walk∈[0,1] drives step cycle.</summary>
    readonly record struct Key(
        float T,
        string Label,
        Vector3 RootOffset,
        float SpineYaw,
        float SpinePitch,
        float HeadPitch,
        Vector3 GripLocal,
        Vector3 TipDir,
        float Stance,
        float TwoHand,
        float Walk);

    static readonly Key[] Keys =
    [
        // ——— Enter at door, face shomen (+Z), bow ———
        new(0.0f, "At the door", Root(DoorZ), 0f, 0.04f, 0.04f,
            GripHip, TipUp, 0.12f, 0f, 0f),
        new(0.8f, "Door — rei", Root(DoorZ, y: -0.02f), 0f, 0.48f, 0.42f,
            GripHip, TipUp, 0.12f, 0f, 0f),
        new(1.8f, "Door — rising", Root(DoorZ), 0f, 0.08f, 0.05f,
            GripHip, TipUp, 0.15f, 0f, 0f),
        new(2.4f, "Turn in", Root(DoorZ + 0.15f), 0.04f, 0.05f, 0.02f,
            GripHip, TipUp, 0.25f, 0f, 0.15f),

        // ——— Walk to center (sword at hip, quiet steps) ———
        new(3.2f, "Walk to center", Root(DoorZ + 0.55f), 0.02f, 0.04f, 0f,
            GripHip, TipUp, 0.55f, 0f, 1f),
        new(4.5f, "Walk to center", Root(DoorZ + 1.15f), 0.02f, 0.04f, 0f,
            GripHip, TipUp, 0.55f, 0f, 1f),
        new(5.8f, "Walk to center", Root(DoorZ + 1.75f), 0.02f, 0.04f, 0f,
            GripHip, TipUp, 0.55f, 0f, 1f),
        new(7.0f, "Arrive", Root(CenterZ - 0.15f), 0.04f, 0.04f, 0f,
            GripHip, TipUp, 0.35f, 0f, 0.35f),

        // ——— Opening pose in the middle before beginning ———
        new(7.8f, "Opening pose", Root(CenterZ), 0.06f, 0.05f, 0.02f,
            GripHip, TipUp, 0.20f, 0f, 0f),
        new(8.5f, "Opening — rei", Root(CenterZ, y: -0.015f), 0.04f, 0.38f, 0.32f,
            GripHip, TipUp, 0.18f, 0f, 0f),
        new(9.6f, "Opening — still", Root(CenterZ), 0.08f, 0.05f, 0.02f,
            GripHip, TipUp, 0.22f, 0f, 0f),
        new(10.4f, "Compose", Root(CenterZ), 0.12f, 0.04f, -0.02f,
            V(0.14f, 0.02f, 0.20f), V(0.10f, 0.65f, 0.75f), 0.55f, 0.45f, 0f),

        // ——— Begin: draw into chūdan ———
        new(11.4f, "To Chūdan", Root(CenterZ, x: 0.02f, y: -0.03f), 0.18f, 0.06f, -0.04f,
            V(0.06f, 0.14f, 0.34f), V(0.04f, 0.42f, 0.91f), 0.85f, 0.90f, 0f),
        new(12.5f, "Chūdan-no-kamae", Root(CenterZ, x: 0.03f, y: -0.04f), 0.22f, 0.05f, -0.06f,
            GripChudan, TipChudan, 1f, 1f, 0f),
        new(14.8f, "Chūdan-no-kamae", Root(CenterZ, x: 0.03f, y: -0.04f), 0.20f, 0.05f, -0.05f,
            GripChudan, TipChudan, 1f, 1f, 0f),

        // ——— Jōdan (grip mid well above the skull so hands/blade clear the head) ———
        new(15.8f, "To Jōdan", Root(CenterZ, x: 0.01f, y: -0.02f), 0.16f, -0.06f, 0.04f,
            V(0.02f, 0.82f, -0.06f), V(-0.05f, 0.68f, -0.73f), 1f, 1f, 0f),
        new(17.0f, "Jōdan-no-kamae", Root(CenterZ - 0.01f, y: -0.02f), 0.14f, -0.10f, 0.08f,
            GripJodan, TipJodan, 1f, 1f, 0f),
        new(18.6f, "Jōdan-no-kamae", Root(CenterZ - 0.01f, y: -0.02f), 0.14f, -0.10f, 0.08f,
            GripJodan, TipJodan, 1f, 1f, 0f),

        // ——— Kesagiri ———
        new(19.1f, "Cutting (kesagiri)", Root(CenterZ, x: 0.04f, y: -0.03f), 0.28f, 0.02f, -0.04f,
            V(0.10f, 0.70f, 0.06f), V(0.55f, 0.45f, 0.70f), 1f, 1f, 0f),
        new(19.8f, "Kesagiri", Root(CenterZ, x: 0.08f, y: -0.05f), 0.48f, 0.12f, -0.12f,
            V(0.10f, 0.28f, 0.22f), V(-0.25f, 0.05f, 0.97f), 1f, 1f, 0f),
        new(20.5f, "Kesagiri", Root(CenterZ, x: 0.10f, y: -0.06f), 0.55f, 0.16f, -0.16f,
            V(0.12f, 0.12f, 0.24f), V(-0.55f, -0.40f, 0.73f), 1f, 1f, 0f),

        // ——— Gedan, recover chūdan ———
        new(22.0f, "Gedan-no-kamae", Root(CenterZ, x: 0.04f, y: -0.04f), 0.24f, 0.08f, -0.06f,
            GripGedan, TipGedan, 1f, 1f, 0f),
        new(24.0f, "Gedan-no-kamae", Root(CenterZ, x: 0.04f, y: -0.04f), 0.22f, 0.08f, -0.06f,
            GripGedan, TipGedan, 1f, 1f, 0f),
        new(25.5f, "Recover to Chūdan", Root(CenterZ, x: 0.03f, y: -0.04f), 0.22f, 0.05f, -0.05f,
            GripChudan, TipChudan, 1f, 1f, 0f),
        new(27.2f, "Chūdan-no-kamae", Root(CenterZ, x: 0.03f, y: -0.04f), 0.20f, 0.05f, -0.05f,
            GripChudan, TipChudan, 1f, 1f, 0f),

        // ——— Closing rei at center, then return toward door ———
        new(28.2f, "Nōtō", Root(CenterZ), 0.10f, 0.05f, 0.02f,
            V(0.14f, 0.02f, 0.18f), V(0.10f, 0.70f, 0.70f), 0.45f, 0.35f, 0f),
        new(29.0f, "Closing rei", Root(CenterZ, y: -0.015f), 0.04f, 0.40f, 0.34f,
            GripHip, TipUp, 0.18f, 0f, 0f),
        new(30.2f, "Closing — rising", Root(CenterZ), 0.04f, 0.05f, 0.02f,
            GripHip, TipUp, 0.20f, 0f, 0f),
        new(31.2f, "Return to door", Root(CenterZ - 0.4f), 0.02f, 0.04f, 0f,
            GripHip, TipUp, 0.50f, 0f, 1f),
        new(32.8f, "Return to door", Root(DoorZ + 0.8f), 0.02f, 0.04f, 0f,
            GripHip, TipUp, 0.50f, 0f, 1f),
        new(34.2f, "At the door", Root(DoorZ), 0f, 0.05f, 0.03f,
            GripHip, TipUp, 0.15f, 0f, 0.2f),
        new(35.0f, "Door — rei", Root(DoorZ, y: -0.02f), 0f, 0.42f, 0.36f,
            GripHip, TipUp, 0.12f, 0f, 0f),
        new(Duration, "At the door", Root(DoorZ), 0f, 0.04f, 0.04f,
            GripHip, TipUp, 0.12f, 0f, 0f),
    ];

    static Vector3 GripHip => V(0.22f, -0.22f, 0.06f);
    static Vector3 TipUp => V(0.04f, 0.99f, 0.04f);
    static Vector3 GripChudan => V(0.05f, 0.18f, 0.38f);
    static Vector3 TipChudan => V(0.03f, 0.45f, 0.89f);
    // Above + behind the crown; kept inside arm reach so holds can lock cleanly.
    static Vector3 GripJodan => V(0.03f, 0.84f, -0.24f);
    static Vector3 TipJodan => V(-0.02f, 0.58f, -0.82f);
    static Vector3 GripGedan => V(0.06f, 0.06f, 0.30f);
    static Vector3 TipGedan => V(-0.06f, -0.50f, 0.86f);

    public readonly record struct Sample(
        string Label,
        Vector3 RootOffset,
        float SpineYaw,
        float SpinePitch,
        float HeadPitch,
        Vector3 GripLocal,
        Vector3 TipDir,
        float Stance,
        float TwoHand,
        float Walk);

    public static float TimeForPhase(string phase)
    {
        var p = phase.Trim().ToLowerInvariant();
        foreach (var (id, time, _) in Phases)
        {
            if (p.Contains(id, StringComparison.Ordinal))
                return time;
        }

        // Aliases
        if (p.Contains("open", StringComparison.Ordinal) || p.Contains("center", StringComparison.Ordinal))
            return TimeForPhase("opening");
        if (p.Contains("rei", StringComparison.Ordinal))
            return TimeForPhase("door");

        return 12.5f;
    }

    public static string PhaseName(float time) => Evaluate(time).Label;

    public static Sample Evaluate(float timeSeconds)
    {
        var t = timeSeconds % Duration;
        if (t < 0f)
            t += Duration;

        var i = 0;
        while (i + 1 < Keys.Length && Keys[i + 1].T <= t)
            i++;

        var a = Keys[i];
        var b = Keys[Math.Min(i + 1, Keys.Length - 1)];
        if (MathF.Abs(b.T - a.T) < 1e-5f)
            return ToSample(a);

        var u = (t - a.T) / (b.T - a.T);
        var cutting = a.Label.Contains("Kesagiri", StringComparison.Ordinal)
                      || a.Label.Contains("Cutting", StringComparison.Ordinal)
                      || b.Label.Contains("Kesagiri", StringComparison.Ordinal);
        var walking = a.Walk > 0.4f || b.Walk > 0.4f;
        var s = cutting ? EaseInOutCubic(u)
            : walking ? Smooth(u)
            : Smooth(u);

        var tip = SlerpDir(Vector3.Normalize(a.TipDir), Vector3.Normalize(b.TipDir), s);
        // Soft hip bob while walking.
        var root = Vector3.Lerp(a.RootOffset, b.RootOffset, s);
        var walk = Lerp(a.Walk, b.Walk, s);
        if (walk > 0.05f)
            root.Y += MathF.Abs(MathF.Sin(t * MathF.PI * 1.7f)) * 0.012f * walk;

        return new Sample(
            s < 0.5f ? a.Label : b.Label,
            root,
            Lerp(a.SpineYaw, b.SpineYaw, s),
            Lerp(a.SpinePitch, b.SpinePitch, s),
            Lerp(a.HeadPitch, b.HeadPitch, s),
            Vector3.Lerp(a.GripLocal, b.GripLocal, s),
            tip,
            Lerp(a.Stance, b.Stance, s),
            Lerp(a.TwoHand, b.TwoHand, s),
            walk);
    }

    static Sample ToSample(Key k) =>
        new(k.Label, k.RootOffset, k.SpineYaw, k.SpinePitch, k.HeadPitch,
            k.GripLocal, Vector3.Normalize(k.TipDir), k.Stance, k.TwoHand, k.Walk);

    static Vector3 Root(float z, float x = 0f, float y = 0f) => new(x, y, z);
    static Vector3 V(float x, float y, float z) => new(x, y, z);
    static float Lerp(float a, float b, float t) => a + (b - a) * t;
    static float Smooth(float t) => t * t * (3f - 2f * t);
    static float EaseInOutCubic(float t) =>
        t < 0.5f ? 4f * t * t * t : 1f - MathF.Pow(-2f * t + 2f, 3f) / 2f;

    static Vector3 SlerpDir(Vector3 a, Vector3 b, float t)
    {
        a = Vector3.Normalize(a);
        b = Vector3.Normalize(b);
        var dot = Math.Clamp(Vector3.Dot(a, b), -1f, 1f);
        if (dot > 0.9995f)
            return Vector3.Normalize(Vector3.Lerp(a, b, t));
        if (dot < -0.9995f)
        {
            var axis = Vector3.Cross(a, Vector3.UnitY);
            if (axis.LengthSquared() < 1e-6f)
                axis = Vector3.Cross(a, Vector3.UnitX);
            axis = Vector3.Normalize(axis);
            return Vector3.Normalize(Vector3.Transform(a, Quaternion.CreateFromAxisAngle(axis, MathF.PI * t)));
        }

        var theta = MathF.Acos(dot);
        var sin = MathF.Sin(theta);
        return Vector3.Normalize(a * (MathF.Sin((1f - t) * theta) / sin) + b * (MathF.Sin(t * theta) / sin));
    }
}
