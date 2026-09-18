using KatoriLab.Demo;

namespace KatoriLab;

/// <summary>Headless QA — delegates to <see cref="KataCorrectness"/> (same gates as unit tests).</summary>
internal static class KataSmoke
{
    public static int Run()
    {
        try
        {
            var driver = new KatoriKataDriver { HoldMode = true };
            Console.WriteLine($"kata-smoke: clip={driver.ClipId} source={driver.SkinSource} dur={driver.DurationSeconds:0.##}s");

            var checks = KataCorrectness.RunAll(driver);
            var failed = 0;
            foreach (var c in checks)
            {
                Console.WriteLine($"kata-smoke: {(c.Ok ? "OK" : "FAIL")} {c.Id} — {c.Detail}");
                if (!c.Ok)
                    failed++;
            }

            if (failed > 0)
            {
                Console.Error.WriteLine($"kata-smoke: {failed}/{checks.Count} checks failed.");
                return 1;
            }

            Console.WriteLine("kata-smoke: OK");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"kata-smoke: {ex}");
            return 10;
        }
    }
}
