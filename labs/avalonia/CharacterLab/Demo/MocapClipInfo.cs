using System.Numerics;
using Novolis.Game.Humanoid;
using Novolis.Simulation.Humanoid;
using Novolis.Simulation.Humanoid.Import;

namespace CharacterLab.Demo;

internal readonly record struct MocapClipInfo(string Id, string Label, string Source, string? FileName);
