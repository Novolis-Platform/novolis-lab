namespace AssetStudioLab.Unit;

public sealed class TimelineAndHarnessTests
{
    [Test]
    public async Task Timeline_can_branch_a_material_experiment()
    {
        await using var timeline = await VisualStudioTimeline.CreateAsync();
        var clean = await timeline.SaveAsync(VisualSamples.NavalSteel, "Clean");
        await timeline.BranchAsync("Worn", clean.Id);
        await timeline.SaveAsync(VisualSamples.NavalSteel.Replace("0.08", "0.8"), "Derelict");
        var rows = await timeline.RowsAsync();
        await Assert.That(rows.Any(r => r.Branch.Contains("Worn", StringComparison.OrdinalIgnoreCase) || r.Label.Contains("Derelict"))).IsTrue();
        await Assert.That(rows.Count).IsGreaterThanOrEqualTo(2);
    }

    [Test]
    public async Task Headless_harness_succeeds()
    {
        var report = await AssetStudioHarness.RunAsync();
        await Assert.That(report.Success).IsTrue();
        await Assert.That(report.PipelineExit).IsEqualTo(0);
        await Assert.That(report.Silk).IsNotNull();
        await Assert.That(report.BeamLuminance).IsGreaterThan(0.001f);
    }
}
