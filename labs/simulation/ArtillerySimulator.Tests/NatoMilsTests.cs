namespace ArtillerySimulator.Tests;

public sealed class NatoMilsTests
{
    [Test]
    public async Task Full_circle_is_6400_mils()
    {
        await Assert.That(NatoMils.DegreesToMils(360f)).IsEqualTo(6400f).Within(0.01f);
        await Assert.That(NatoMils.MilsToDegrees(6400f)).IsEqualTo(360f).Within(0.01f);
    }

    [Test]
    public async Task Round_trip_preserves_degrees()
    {
        const float degrees = 42.5f;
        var mils = NatoMils.DegreesToMils(degrees);
        await Assert.That(NatoMils.MilsToDegrees(mils)).IsEqualTo(degrees).Within(1e-4f);
    }

    [Test]
    public async Task SigmaFromPe_scales_by_PeToSigma()
    {
        await Assert.That(NatoMils.SigmaFromPe(1.6f)).IsEqualTo(1.6f * NatoMils.PeToSigma);
    }
}
