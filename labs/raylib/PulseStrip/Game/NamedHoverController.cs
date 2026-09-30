namespace PulseStrip.Game;

using System.Drawing;
using System.Numerics;
using Novolis.Audio;
using Novolis.Game.MenuFlows;
using Novolis.Raylib.Game;
using Novolis.Raylib.Interact;
using Novolis.Simulation.Racing.Tracks;
using PulseStrip.Audio;
using PulseStrip.Core;
using PulseStrip.Core.Ai;

internal sealed class NamedHoverController(string name, IHoverController inner) : IHoverController
{
    public string Name { get; } = name;
    public HoverControlDecision Decide(in HoverObservation observation) => inner.Decide(in observation);
}
