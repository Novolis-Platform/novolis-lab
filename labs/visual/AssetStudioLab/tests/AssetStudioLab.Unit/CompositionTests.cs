namespace AssetStudioLab.Unit;

public sealed class CompositionTests
{
    [Test]
    public async Task Flashlight_plus_fog_is_not_an_asset()
    {
        var light = VisualLanguageParser.Parse(VisualSamples.MarineFlashlight).Document!.Definitions[0];
        var fog = VisualLanguageParser.Parse(VisualSamples.ShipFog).Document!.Definitions[0];
        var beam = BeamComposition.Render(light, fog, 32, 32);
        var luminance = BeamComposition.MeanLuminance(beam);
        await Assert.That(luminance).IsGreaterThan(0.001f);
        await Assert.That(beam.Any(p => p.R > 0 || p.G > 0 || p.B > 0)).IsTrue();
    }

    [Test]
    public async Task Sphere_preview_writes_metal_pixels()
    {
        var material = VisualLanguageParser.Parse(VisualSamples.NavalSteel).Document!.Definitions[0];
        var pixels = SpherePreview.Render(material, 32, 32);
        await Assert.That(pixels.Length).IsEqualTo(32 * 32);
        await Assert.That(pixels.Any(p => p.R > 20)).IsTrue();
    }
}
