namespace AssetStudioLab.Unit;

public sealed class PipelineAndCodegenTests
{
    [Test]
    public async Task Unchanged_source_skips_rebake()
    {
        var work = Path.Combine(Path.GetTempPath(), "asset-studio-pipe-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        var first = await VisualAssetPipeline.RunAsync(work, VisualSamples.NavalSteel, force: true);
        await Assert.That(first).IsEqualTo(0);
        var log = await File.ReadAllTextAsync(Path.Combine(work, "steps", "bake_products", "step.log"));
        var second = await VisualAssetPipeline.RunAsync(work, VisualSamples.NavalSteel, force: false);
        await Assert.That(second).IsEqualTo(0);
        var csharp = await File.ReadAllTextAsync(Path.Combine(work, "steps", "generate_csharp", "artifacts", "Materials.g.cs"));
        await Assert.That(csharp).Contains("NavalSteel");
        await Assert.That(csharp).Contains("WithRoughness");
        await Assert.That(File.Exists(Path.Combine(work, "steps", "generate_csharp", "artifacts", "DocumentDump.g.cs"))).IsTrue();
        _ = log;
    }

    [Test]
    public async Task Parameterized_material_instances_share_a_definition()
    {
        var definition = VisualLanguageParser.Parse(VisualSamples.ShipSteel).Document!.Definitions[0];
        var pristine = VisualInstantiator.Instantiate(
            definition,
            new Dictionary<string, VisualExpr> { ["wear"] = VisualExpr.Lit(VisualValue.FromScalar(0.02)) });
        var wreck = VisualInstantiator.Instantiate(
            definition,
            new Dictionary<string, VisualExpr> { ["wear"] = VisualExpr.Lit(VisualValue.FromScalar(0.8)) });
        var a = pristine.Properties.First(p => p.Name == "edgeWear").Value.Args[0].Value.Literal!.Scalar;
        var b = wreck.Properties.First(p => p.Name == "edgeWear").Value.Args[0].Value.Literal!.Scalar;
        await Assert.That(a).IsEqualTo(0.02).Within(1e-9);
        await Assert.That(b).IsEqualTo(0.8).Within(1e-9);
    }

    [Test]
    public async Task Json_roundtrips_the_ast()
    {
        var document = VisualLanguageParser.Parse(VisualSamples.NavalSteel).Document!;
        var json = VisualJson.Serialize(document);
        var copy = VisualJson.Deserialize(json);
        await Assert.That(copy.Find("NavalSteel")).IsNotNull();
        await Assert.That(copy.Find("NavalSteel")!.Properties.Count).IsEqualTo(document.Find("NavalSteel")!.Properties.Count);
    }
}
