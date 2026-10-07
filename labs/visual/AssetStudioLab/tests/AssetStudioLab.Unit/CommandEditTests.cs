namespace AssetStudioLab.Unit;

public sealed class CommandEditTests
{
    [Test]
    public async Task Commands_mutate_the_same_definition_graph()
    {
        var result = VisualCommandEditor.Apply(
            VisualDocument.Empty,
            "Metal(0.7); Roughness(0.65); Noise(Roughness, 0.02, 0.08); EdgeWear(0.1);");
        await Assert.That(result.Success).IsTrue();
        var material = result.Document.Definitions.First(d => d.Kind == VisualDefinitionKind.Material);
        await Assert.That(material.Properties.Any(p => p.Name == "roughness" && p.Assign == VisualAssignOp.Set)).IsTrue();
        await Assert.That(material.Properties.Any(p => p.Name.Equals("roughness", StringComparison.OrdinalIgnoreCase) && p.Assign == VisualAssignOp.Multiply)).IsTrue();
        await Assert.That(material.Properties.Any(p => p.Assign == VisualAssignOp.Layer)).IsTrue();
    }

    [Test]
    public async Task Geometry_commands_build_the_same_graph()
    {
        var result = VisualCommandEditor.Apply(
            VisualDocument.Empty,
            "Box(2m, 3m, 0.2m); Bevel(4mm); Material(NavalSteel);");
        await Assert.That(result.Success).IsTrue();
        var geometry = result.Document.Definitions.First(d => d.Kind == VisualDefinitionKind.Geometry);
        await Assert.That(geometry.Properties.Any(p => p.Name == "Box")).IsTrue();
        await Assert.That(geometry.Properties.Any(p => p.Name == "Bevel")).IsTrue();
        await Assert.That(geometry.Properties.Any(p => p.Name == "Material")).IsTrue();
    }
}
