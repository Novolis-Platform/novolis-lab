using System.Numerics;

namespace TopDownDoom.Game.ImportedDoom;

internal readonly record struct ImportedDoomThingSpawn(
    ImportedDoomThing Thing,
    Vector3 Position);
