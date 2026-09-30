using System.Numerics;
using Novolis.Math.Geometry;

namespace CharacterLab.Demo;

/// <summary>
/// Named grip / barrel points in rifle local space. Hands Soft-IK / FullBodyIk lock to these after the weapon pose is placed.
/// </summary>
internal readonly record struct WeaponHoldPoint(string Name, Vector3 LocalPosition);
