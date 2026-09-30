using System.Numerics;
using Novolis.Physics.Ballistics;
using Novolis.Raylib.Game;
using Novolis.Raylib.Interact;
using Novolis.Simulation.View;
using RayCamera = Novolis.Raylib.Rendering.Camera;

namespace ArtillerySimulator.Game;

internal enum CameraMode
{
    Freecam,
    Orbit,
}
