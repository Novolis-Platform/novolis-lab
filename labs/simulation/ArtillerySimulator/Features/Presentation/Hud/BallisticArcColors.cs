using System.Drawing;

namespace ArtillerySimulator;

/// <summary>Shared palette for ballistic arcs and the on-screen legend.</summary>
internal static class BallisticArcColors
{
    public static readonly Color Ordered = Color.FromArgb(255, 64, 230, 255);   // cyan — aimed fire solution
    public static readonly Color Tube = Color.FromArgb(220, 255, 186, 72);       // amber — tube still laying
    public static readonly Color Flight = Color.FromArgb(255, 255, 120, 64);     // hot — live round
    public static readonly Color Impact = Color.FromArgb(255, 255, 90, 70);      // impact flash
    public static readonly Color MinRange = Color.FromArgb(160, 180, 90, 255);   // violet — HE min ring
    public static readonly Color MpiMark = Color.FromArgb(240, 80, 255, 180);    // MPI ground mark
    /// <summary>High-transparency sibling arcs + loft bands for the physics probability cone.</summary>
    public static readonly Color Probability = Color.FromArgb(48, 120, 220, 255);
    public static readonly Color ProbabilityFootprint = Color.FromArgb(70, 100, 200, 255);
}
