using System.Numerics;
using Novolis.Simulation.Humanoid;

namespace KatoriLab.Demo;

/// <summary>
/// Headless correctness gates for the full dojo kata (shared by --kata-smoke and unit tests).
/// </summary>
internal static class KataCorrectness
{
    public readonly record struct Check(string Id, bool Ok, string Detail);

    public static IReadOnlyList<Check> RunAll(KatoriKataDriver? driver = null)
    {
        driver ??= new KatoriKataDriver { HoldMode = true };
        driver.HoldMode = true;
        var checks = new List<Check>();

        checks.Add(CheckDuration());
        checks.Add(CheckPhaseSeeks());
        checks.Add(CheckDoorToCenter(driver));
        checks.Add(CheckWalkArmHang(driver));
        checks.Add(CheckTimelineTipUnit());
        checks.Add(CheckChudanGeometry(driver));
        checks.Add(CheckJodanGeometry(driver));
        checks.Add(CheckHoldLock(driver, "chudan", maxR: 0.08f, maxL: 0.10f));
        checks.Add(CheckHoldLock(driver, "jodan", maxR: 0.08f, maxL: 0.10f));
        checks.Add(CheckKenGripLayout(driver));
        checks.Add(CheckJodanHeadClearance(driver));
        checks.Add(CheckChudanToJodanTravel(driver));
        checks.Add(CheckKesagiriTravel(driver));
        checks.Add(CheckTipContinuity());
        checks.Add(CheckClosingReturn(driver));
        return checks;
    }

    public static bool AllPassed(IReadOnlyList<Check> checks) => checks.All(c => c.Ok);

    static Check CheckDuration() =>
        KenTimeline.Duration >= 30f
            ? new("duration", true, $"dur={KenTimeline.Duration:0.#}s")
            : new("duration", false, $"dur={KenTimeline.Duration:0.#}s expected >= 30");

    static Check CheckPhaseSeeks()
    {
        string[] ids = ["door", "walk", "opening", "chudan", "jodan", "kesagiri", "gedan", "closing", "leave"];
        float prev = -1f;
        foreach (var id in ids)
        {
            var t = KenTimeline.TimeForPhase(id);
            if (t <= prev)
                return new("phase-order", false, $"{id}@{t:0.##} not after {prev:0.##}");
            prev = t;
        }

        return new("phase-order", true, string.Join(",", ids.Select(i => $"{i}@{KenTimeline.TimeForPhase(i):0.#}")));
    }

    static Check CheckDoorToCenter(KatoriKataDriver driver)
    {
        driver.SeekPhase("door");
        var doorZ = driver.SamplePose().Hips.Z;
        driver.SeekPhase("opening");
        var openZ = driver.SamplePose().Hips.Z;
        var delta = openZ - doorZ;
        return delta >= 1.5f
            ? new("door-to-center", true, $"doorZ={doorZ:0.###} openZ={openZ:0.###} Δ={delta:0.###}")
            : new("door-to-center", false, $"doorZ={doorZ:0.###} openZ={openZ:0.###} Δ={delta:0.###} expected ≥1.5");
    }

    static Check CheckWalkArmHang(KatoriKataDriver driver)
    {
        driver.Seek(5.7f);
        var pose = driver.SamplePose();
        var lShoulder = driver.World.Position(HumanoidBone.LeftShoulder);
        var drop = lShoulder.Y - pose.LeftHand.Y;
        var span = MathF.Abs(pose.LeftHand.X - pose.Hips.X);
        var ok = drop >= 0.20f && span <= 0.45f;
        return new("walk-arm-hang", ok, $"leftDrop={drop:0.###} leftSpanX={span:0.###}");
    }

    static Check CheckTimelineTipUnit()
    {
        for (var t = 0f; t <= KenTimeline.Duration; t += 0.25f)
        {
            var tip = KenTimeline.Evaluate(t).TipDir;
            var len = tip.Length();
            if (MathF.Abs(len - 1f) > 0.02f)
                return new("tip-unit", false, $"t={t:0.##} |tip|={len:0.###}");
        }

        return new("tip-unit", true, "sampled 0.25s steps");
    }

    static Check CheckChudanGeometry(KatoriKataDriver driver)
    {
        driver.SeekPhase("chudan");
        var pose = driver.SamplePose();
        var blade = pose.Kissaki - pose.Kashira;
        if (blade.LengthSquared() < 1e-6f)
            return new("chudan-geom", false, "zero blade");
        var dir = Vector3.Normalize(blade);
        var forward = Vector3.Dot(dir, Vector3.UnitZ);
        var tipY = pose.Kissaki.Y;
        var ok = forward >= 0.70f && tipY is >= 1.15f and <= 1.55f;
        return new("chudan-geom", ok, $"forwardDot={forward:0.###} tipY={tipY:0.###}");
    }

    static Check CheckJodanGeometry(KatoriKataDriver driver)
    {
        driver.SeekPhase("jodan");
        var pose = driver.SamplePose();
        var blade = pose.Kissaki - pose.Kashira;
        if (blade.LengthSquared() < 1e-6f)
            return new("jodan-geom", false, "zero blade");
        var dir = Vector3.Normalize(blade);
        var forward = Vector3.Dot(dir, Vector3.UnitZ);
        var up = Vector3.Dot(dir, Vector3.UnitY);
        var tipY = pose.Kissaki.Y;
        // Tip rear-high (upDot soft — blade may lie mostly behind the crown).
        var ok = forward <= -0.35f && tipY >= 1.55f;
        return new("jodan-geom", ok, $"forwardDot={forward:0.###} upDot={up:0.###} tipY={tipY:0.###}");
    }

    static Check CheckHoldLock(KatoriKataDriver driver, string phase, float maxR, float maxL)
    {
        driver.SeekPhase(phase);
        var h = driver.SampleHolds();
        var ok = h.RightHandError <= maxR && h.LeftHandError <= maxL;
        return new($"hold-{phase}", ok, $"rErr={h.RightHandError:0.####} lErr={h.LeftHandError:0.####}");
    }

    static Check CheckKenGripLayout(KatoriKataDriver driver)
    {
        driver.SeekPhase("chudan");
        var blade = driver.Kissaki - driver.Kashira;
        if (blade.LengthSquared() < 1e-6f)
            return new("ken-grip", false, "zero blade");
        var dir = Vector3.Normalize(blade);
        // Along blade from kashira→kissaki: left (secondary) then right (primary) then tsuba.
        var leftT = Vector3.Dot(driver.HoldSecondaryWorld - driver.Kashira, dir);
        var rightT = Vector3.Dot(driver.HoldPrimaryWorld - driver.Kashira, dir);
        var tsubaT = Vector3.Dot(driver.Tsuba - driver.Kashira, dir);
        var tipT = Vector3.Dot(driver.Kissaki - driver.Kashira, dir);
        var span = Vector3.Distance(driver.HoldPrimaryWorld, driver.HoldSecondaryWorld);
        var orderOk = leftT < rightT && rightT < tsubaT && tsubaT < tipT;
        var spanOk = span is >= 0.18f and <= 0.32f;
        var ok = orderOk && spanOk;
        return new("ken-grip", ok,
            $"leftT={leftT:0.###} rightT={rightT:0.###} tsubaT={tsubaT:0.###} span={span:0.###} order={orderOk}");
    }

    static Check CheckJodanHeadClearance(KatoriKataDriver driver)
    {
        driver.SeekPhase("jodan");
        var head = driver.World.Position(HumanoidBone.Head) + new Vector3(0f, 0.06f, 0f);
        const float minClear = 0.14f;
        var dR = Vector3.Distance(driver.HoldPrimaryWorld, head);
        var dL = Vector3.Distance(driver.HoldSecondaryWorld, head);
        var dT = Vector3.Distance(driver.Tsuba, head);
        var ok = dR >= minClear && dL >= minClear && dT >= minClear;
        return new("jodan-clear", ok, $"dR={dR:0.###} dL={dL:0.###} dTsuba={dT:0.###}");
    }

    static Check CheckChudanToJodanTravel(KatoriKataDriver driver)
    {
        var delta = driver.MeasureVertexDelta(
            KenTimeline.TimeForPhase("chudan"),
            KenTimeline.TimeForPhase("jodan"));
        var ok = delta.MaxDelta >= 0.12f && delta.UpperBodyMaxDelta >= 0.10f;
        return new("chudan-jodan-travel", ok, $"max={delta.MaxDelta:0.####} upper={delta.UpperBodyMaxDelta:0.####}");
    }

    static Check CheckKesagiriTravel(KatoriKataDriver driver)
    {
        var travel = driver.MeasureBoneTravel(
            KenTimeline.TimeForPhase("jodan"),
            KenTimeline.TimeForPhase("kesagiri"));
        return travel.RightHand >= 0.08f
            ? new("kesagiri-travel", true, $"rHand={travel.RightHand:0.####}")
            : new("kesagiri-travel", false, $"rHand={travel.RightHand:0.####} expected ≥0.08");
    }

    static Check CheckTipContinuity()
    {
        // Tip direction should not jump more than ~90° in a single 1/30s step outside the cut.
        var prev = KenTimeline.Evaluate(0f).TipDir;
        for (var t = 1f / 30f; t < KenTimeline.Duration; t += 1f / 30f)
        {
            var cur = KenTimeline.Evaluate(t).TipDir;
            var label = KenTimeline.Evaluate(t).Label;
            var cutting = label.Contains("Kesagiri", StringComparison.Ordinal)
                          || label.Contains("Cutting", StringComparison.Ordinal);
            var dot = Vector3.Dot(Vector3.Normalize(prev), Vector3.Normalize(cur));
            var minDot = cutting ? -0.15f : 0.55f;
            if (dot < minDot)
                return new("tip-continuity", false, $"t={t:0.###} label={label} tipDot={dot:0.###}");
            prev = cur;
        }

        return new("tip-continuity", true, "30 Hz tip slerp");
    }

    static Check CheckClosingReturn(KatoriKataDriver driver)
    {
        driver.SeekPhase("closing");
        var closeZ = driver.SamplePose().Hips.Z;
        driver.SeekPhase("leave");
        var leaveZ = driver.SamplePose().Hips.Z;
        // Leave should move back toward the door (−Z).
        var ok = leaveZ < closeZ - 0.3f;
        return new("closing-return", ok, $"closeZ={closeZ:0.###} leaveZ={leaveZ:0.###}");
    }
}
