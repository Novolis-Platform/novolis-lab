using XFighter.Game;

namespace XFighter.Tests;

public sealed class WingmanChatterTests
{
    [Test]
    public async Task Update_sets_line_after_idle_timer()
    {
        var chatter = new WingmanChatter(new Random(1));
        string? line = null;
        for (var i = 0; i < 200 && line is null; i++)
            line = chatter.Update(0.1f, 2, 1f, 0);

        await Assert.That(line).IsNotNull();
        await Assert.That(chatter.CurrentLine).IsNotNull();
    }

    [Test]
    public async Task AnnounceWave_sets_leader_line()
    {
        var chatter = new WingmanChatter(new Random(2));
        var line = chatter.AnnounceWave();
        await Assert.That(line).IsNotNull();
        await Assert.That(chatter.CurrentLine).IsNotNull();
        await Assert.That(chatter.CurrentSpeaker).IsEqualTo("RED LEADER");
    }
}
