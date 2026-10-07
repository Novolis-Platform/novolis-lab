namespace AssetStudioLab.Unit;

public sealed class TypeSystemTests
{
    [Test]
    public async Task Bloom_cannot_plug_into_roughness()
    {
        var parsed = VisualLanguageParser.Parse("""
            material Bad {
                roughness bloom()
            }
            """);
        await Assert.That(parsed.Success).IsTrue();
        var ir = VisualCompiler.Compile(parsed.Document!);
        await Assert.That(ir.Success).IsFalse();
        await Assert.That(ir.Diagnostics.Any(d => d.Message.Contains("Bloom", StringComparison.OrdinalIgnoreCase))).IsTrue();
    }

    [Test]
    public async Task Light_times_roughness_is_rejected()
    {
        var parsed = VisualLanguageParser.Parse("""
            material Bad {
                roughness point(color: red, intensity: 10) * 0.5
            }
            """);
        await Assert.That(parsed.Success).IsTrue();
        var ir = VisualCompiler.Compile(parsed.Document!);
        await Assert.That(ir.Success).IsFalse();
        await Assert.That(ir.Diagnostics.Any(d => d.Message.Contains("Light", StringComparison.OrdinalIgnoreCase))).IsTrue();
    }
}
