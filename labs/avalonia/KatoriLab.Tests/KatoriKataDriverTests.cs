using KatoriLab.Demo;
using Novolis.Simulation.Humanoid;

namespace KatoriLab.Tests;

public sealed class KatoriKataDriverTests
{
    [Test]
    public async Task Correctness_suite_passes()
    {
        var checks = KataCorrectness.RunAll();
        var failed = checks.Where(c => !c.Ok).Select(c => $"{c.Id}: {c.Detail}").ToArray();
        await Assert.That(failed).IsEmpty();
    }

    [Test]
    public async Task Walk_left_arm_is_not_t_pose()
    {
        var driver = new KatoriKataDriver { HoldMode = true };
        driver.Seek(5.7f);
        var pose = driver.SamplePose();
        var lShoulder = driver.World.Position(HumanoidBone.LeftShoulder);
        var drop = lShoulder.Y - pose.LeftHand.Y;
        var span = MathF.Abs(pose.LeftHand.X - pose.Hips.X);
        await Assert.That(drop).IsGreaterThanOrEqualTo(0.20f);
        await Assert.That(span).IsLessThanOrEqualTo(0.45f);
    }

    [Test]
    public async Task Chudan_tip_faces_forward_near_throat()
    {
        var driver = new KatoriKataDriver { HoldMode = true };
        driver.SeekPhase("chudan");
        var pose = driver.SamplePose();
        var dir = System.Numerics.Vector3.Normalize(pose.Kissaki - pose.Kashira);
        var forward = System.Numerics.Vector3.Dot(dir, System.Numerics.Vector3.UnitZ);
        await Assert.That(forward).IsGreaterThanOrEqualTo(0.70f);
        await Assert.That(pose.Kissaki.Y).IsGreaterThanOrEqualTo(1.15f);
        await Assert.That(pose.Kissaki.Y).IsLessThanOrEqualTo(1.55f);
    }

    [Test]
    public async Task Jodan_tip_is_high_and_rear()
    {
        var driver = new KatoriKataDriver { HoldMode = true };
        driver.SeekPhase("jodan");
        var pose = driver.SamplePose();
        var dir = System.Numerics.Vector3.Normalize(pose.Kissaki - pose.Kashira);
        var forward = System.Numerics.Vector3.Dot(dir, System.Numerics.Vector3.UnitZ);
        await Assert.That(forward).IsLessThanOrEqualTo(-0.35f);
        await Assert.That(pose.Kissaki.Y).IsGreaterThanOrEqualTo(1.6f);
    }

    [Test]
    public async Task Two_hand_kamae_locks_holds()
    {
        var driver = new KatoriKataDriver { HoldMode = true };
        foreach (var phase in new[] { "chudan", "jodan", "gedan" })
        {
            driver.SeekPhase(phase);
            var h = driver.SampleHolds();
            await Assert.That(h.RightHandError).IsLessThanOrEqualTo(0.08f);
            await Assert.That(h.LeftHandError).IsLessThanOrEqualTo(0.10f);
        }
    }

    [Test]
    public async Task Starts_at_the_door()
    {
        var driver = new KatoriKataDriver { HoldMode = true };
        var t = driver.TimeSeconds;
        var z = driver.SamplePose().Hips.Z;
        await Assert.That(t).IsEqualTo(0f);
        await Assert.That(z).IsEqualTo(KenTimeline.DoorZ);
    }

    [Test]
    public async Task Ken_grip_is_left_kashira_right_tsuba_with_span()
    {
        var driver = new KatoriKataDriver { HoldMode = true };
        driver.SeekPhase("chudan");
        var dir = System.Numerics.Vector3.Normalize(driver.Kissaki - driver.Kashira);
        var leftT = System.Numerics.Vector3.Dot(driver.HoldSecondaryWorld - driver.Kashira, dir);
        var rightT = System.Numerics.Vector3.Dot(driver.HoldPrimaryWorld - driver.Kashira, dir);
        var tsubaT = System.Numerics.Vector3.Dot(driver.Tsuba - driver.Kashira, dir);
        var span = System.Numerics.Vector3.Distance(driver.HoldPrimaryWorld, driver.HoldSecondaryWorld);
        await Assert.That(leftT).IsLessThan(rightT);
        await Assert.That(rightT).IsLessThan(tsubaT);
        await Assert.That(span).IsGreaterThanOrEqualTo(0.18f);
        await Assert.That(span).IsLessThanOrEqualTo(0.32f);
    }

    [Test]
    public async Task Jodan_hands_and_tsuba_clear_the_head()
    {
        var driver = new KatoriKataDriver { HoldMode = true };
        driver.SeekPhase("jodan");
        var head = driver.World.Position(HumanoidBone.Head) + new System.Numerics.Vector3(0f, 0.06f, 0f);
        await Assert.That(System.Numerics.Vector3.Distance(driver.HoldPrimaryWorld, head)).IsGreaterThanOrEqualTo(0.14f);
        await Assert.That(System.Numerics.Vector3.Distance(driver.HoldSecondaryWorld, head)).IsGreaterThanOrEqualTo(0.14f);
        await Assert.That(System.Numerics.Vector3.Distance(driver.Tsuba, head)).IsGreaterThanOrEqualTo(0.14f);
    }
}
