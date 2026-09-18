using CharacterLab.Demo;
using Novolis.Simulation.Humanoid.Import;

namespace CharacterLab;

/// <summary>Headless QA for CMU BVH mocap + hold-point IK (wire path).</summary>
internal static class DrillSmoke
{
    public static int Run()
    {
        try
        {
            var driver = new MocapParadeDriver();
            Console.WriteLine($"drill-smoke: clips={driver.Clips.Count} default={driver.ActiveClipId} source={driver.SkinSource}");

            var bvh = driver.Clips.FirstOrDefault(c => c.Source == "cmu-bvh");
            if (bvh.Id is null)
            {
                Console.Error.WriteLine("drill-smoke: no CMU BVH clips under assets/mocap.");
                return 1;
            }

            // Import smoke: shipped file must yield a real clip.
            var path = Path.Combine(
                AppContext.BaseDirectory, "assets", "mocap", bvh.FileName ?? $"{bvh.Id}.bvh");
            if (!File.Exists(path))
            {
                // Fall back to source tree relative to output.
                path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "assets", "mocap", bvh.FileName ?? $"{bvh.Id}.bvh"));
            }

            var imported = BvhHumanoidImporter.ImportFile(path, metersPerUnit: 0.01f, BvhHumanoidImporter.RenameCmuJoint);
            Console.WriteLine($"drill-smoke: import {Path.GetFileName(path)} keys={imported.Keys.Count} dur={imported.DurationSeconds:0.##}s");
            if (imported.Keys.Count <= 10 || imported.DurationSeconds <= 0f)
            {
                Console.Error.WriteLine("drill-smoke: BVH import too short.");
                return 2;
            }

            driver.SelectClip(bvh.Id);
            driver.HoldMode = true;
            var early = driver.DurationSeconds * 0.1f;
            var mid = driver.DurationSeconds * 0.5f;
            var travel = driver.MeasureBoneTravel(early, mid);
            Console.WriteLine(
                $"drill-smoke: mocap travel rHand={travel.RightHand:0.####} rFoot={travel.RightFoot:0.####} hips={travel.Hips:0.####}");
            if (travel.RightHand < 0.05f && travel.RightFoot < 0.05f && travel.Hips < 0.05f)
            {
                Console.Error.WriteLine("drill-smoke: mocap joint travel too small (retarget dead?).");
                return 3;
            }

            driver.Seek(mid);
            var holds = driver.SampleHolds();
            Console.WriteLine($"drill-smoke: hold lock mid-clip rErr={holds.RightHandError:0.####} lErr={holds.LeftHandError:0.####}");
            if (holds.RightHandError > 0.06f || holds.LeftHandError > 0.08f)
            {
                Console.Error.WriteLine("drill-smoke: hands not locked to rifle hold points.");
                return 8;
            }

            // Synthetic drill still available and must animate.
            driver.SelectClip("synthetic-drill");
            driver.HoldMode = true;
            var delta = driver.MeasureVertexDelta(
                DrillClips.TimeForPhase("order"),
                DrillClips.TimeForPhase("present"));
            Console.WriteLine(
                $"drill-smoke: synthetic order→present max={delta.MaxDelta:0.####} upperMax={delta.UpperBodyMaxDelta:0.####}");
            if (delta.MaxDelta < 0.15f)
            {
                Console.Error.WriteLine("drill-smoke: synthetic joint travel too small.");
                return 4;
            }

            driver.SeekPhase("present");
            holds = driver.SampleHolds();
            Console.WriteLine($"drill-smoke: synthetic present rErr={holds.RightHandError:0.####} lErr={holds.LeftHandError:0.####}");
            if (holds.RightHandError > 0.06f || holds.LeftHandError > 0.08f)
            {
                Console.Error.WriteLine("drill-smoke: synthetic hold lock failed.");
                return 9;
            }

            Console.WriteLine("drill-smoke: OK");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"drill-smoke: {ex}");
            return 10;
        }
    }
}
