namespace PulseStrip.Core;

/// <summary>Keyboard/gamepad-style player controller; inject decisions each frame.</summary>
public sealed class PlayerHoverController : IHoverController
{
    public string Name { get; }
    public HoverControlDecision Current { get; set; }

    public PlayerHoverController(string name = "Player")
    {
        Name = name;
    }

    public HoverControlDecision Decide(in HoverObservation observation) => Current;
}
