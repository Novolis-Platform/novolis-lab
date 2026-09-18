namespace PulseStrip.Core;

/// <summary>Produces <see cref="HoverControlDecision"/> from a <see cref="HoverObservation"/>.</summary>
public interface IHoverController
{
    string Name { get; }
    HoverControlDecision Decide(in HoverObservation observation);
}
