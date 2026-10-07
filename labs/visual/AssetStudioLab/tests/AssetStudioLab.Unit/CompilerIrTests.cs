namespace AssetStudioLab.Unit;

public sealed class CompilerIrTests
{
    [Test]
    public async Task Edge_wear_lowers_to_a_curvature_field_not_an_authoring_noun()
    {
        var document = VisualLanguageParser.Parse(VisualSamples.NavalSteel).Document!;
        var ir = VisualCompiler.Compile(document);
        await Assert.That(ir.Success).IsTrue();
        var material = ir.Definitions.First(d => d.Name == "NavalSteel");
        await Assert.That(material.Nodes.Any(n => n.Op == "scalar_from_curvature")).IsTrue();
        await Assert.That(material.Nodes.Any(n => n.Op.Equals("edgeWear", StringComparison.OrdinalIgnoreCase))).IsFalse();
        await Assert.That(material.Nodes.Any(n => n.Op == "hash_noise")).IsTrue();
        var silk = SilkVisualLowering.Lower(material);
        await Assert.That(silk.Mixers.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task Constants_fold_and_duplicates_dedup()
    {
        var parsed = VisualLanguageParser.Parse("""
            material Fold {
                roughness 0.5 * 0.5
            }
            """);
        var ir = VisualCompiler.Compile(parsed.Document!);
        await Assert.That(ir.Success).IsTrue();
        var nodes = ir.Definitions[0].Nodes;
        await Assert.That(nodes.Any(n => n.Op == "const")).IsTrue();
        await Assert.That(nodes.Any(n => n.Op == "mul")).IsFalse();
    }

    [Test]
    public async Task Red_bolt_flashlight_and_fog_parse()
    {
        foreach (var source in new[] { VisualSamples.RedBolt, VisualSamples.MarineFlashlight, VisualSamples.ShipFog, VisualSamples.GeometryPanel })
        {
            var parsed = VisualLanguageParser.Parse(source);
            await Assert.That(parsed.Success).IsTrue();
        }
    }
}
