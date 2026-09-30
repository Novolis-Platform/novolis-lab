using System.Numerics;

namespace KatoriLab.Demo;

/// <summary>Named grip / tip points in ken local space (+Z along blade toward kissaki, origin at weapon center).</summary>
internal readonly record struct KenHoldPoint(string Name, Vector3 LocalPosition);
