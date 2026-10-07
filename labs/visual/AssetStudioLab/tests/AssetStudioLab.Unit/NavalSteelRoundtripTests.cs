namespace AssetStudioLab.Unit;

public sealed class NavalSteelRoundtripTests
{
    [Test]
    public async Task Source_and_stack_are_views_of_the_same_tree()
    {
        var parsed = VisualLanguageParser.Parse(VisualSamples.NavalSteel);
        await Assert.That(parsed.Success).IsTrue();
        var document = parsed.Document!;
        var again = VisualLanguageParser.Parse(VisualLanguageFormatter.Format(document));
        await Assert.That(again.Success).IsTrue();
        await Assert.That(again.Document!.Find("NavalSteel")).IsNotNull();

        var material = document.Find("NavalSteel")!;
        await Assert.That(material.Properties.Any(p => p.Name == "color")).IsTrue();
        await Assert.That(material.Properties.Any(p => p.Assign == VisualAssignOp.Layer && p.Name == "edgeWear")).IsTrue();

        var noise = material.Properties.First(p => p.Assign == VisualAssignOp.Multiply);
        var scale = noise.Value.Args.First(a => a.Name == "scale").Value.Literal!;
        await Assert.That(scale.Kind).IsEqualTo(VisualKind.Distance);
        await Assert.That(scale.Scalar).IsEqualTo(0.018).Within(1e-9);

        var stack = VisualStackPrinter.Print(document);
        await Assert.That(stack).Contains("NavalSteel");
        await Assert.That(stack).Contains("Layers");
        await Assert.That(stack).Contains("Noise");
    }
}
