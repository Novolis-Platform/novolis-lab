using System.Numerics;
using System.Runtime.InteropServices;
using Novolis.Math.Arrays;
using Novolis.Math.Geometry;
using Novolis.Physics.Collision.Simple;
using Novolis.Physics.Joints;
using Novolis.Simulation.Humanoid;
using Novolis.Simulation.Humanoid.Skinning;
using Novolis.Simulation.World;
using Novolis.Simulation.World.Builders;
using HumanoidLab.Ui;

namespace HumanoidLab.Demo;

internal readonly record struct RagdollStatus(
    float TimeSeconds,
    bool Tipped,
    float MaxSpeed,
    float KineticEnergy,
    float BoneError,
    int Sleeping,
    int SphereCount,
    float MinY,
    float MaxY,
    Vector3 Hip,
    float EntropyPerSecond,
    bool AutoTipEnabled);
