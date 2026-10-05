namespace FrankMoat.Narrative;

internal sealed class FaithCue
{
    public string Text { get; set; } = string.Empty;

    public float Timer { get; set; }

    public void Say(string text, float seconds = 3.4f)
    {
        Text = text;
        Timer = seconds;
    }

    public void Tick(float dt)
    {
        if (Timer <= 0f)
        {
            return;
        }

        Timer -= dt;
        if (Timer <= 0f)
        {
            Text = string.Empty;
        }
    }
}
