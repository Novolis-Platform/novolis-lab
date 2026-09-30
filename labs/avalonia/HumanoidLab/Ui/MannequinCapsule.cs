using System.Numerics;
using Novolis.Physics.Collision.Simple;
using Novolis.Simulation.Humanoid;

namespace HumanoidLab.Ui;

/// <summary>One limb of a painter-style capsule mannequin (the readable human shape).</summary>
internal readonly record struct MannequinCapsule(Vector3 A, Vector3 B, float RadiusMeters);
