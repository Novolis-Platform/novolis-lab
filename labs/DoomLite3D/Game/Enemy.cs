using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using Novolis.Simulation.World;
using Novolis.Raylib.Game;
using Novolis.Raylib.Interact;
using Novolis.Raylib.Rendering;
using RayCamera = Novolis.Raylib.Rendering.Camera;

namespace DoomLite3D.Game;

internal sealed class Enemy
{
    public Vector3 Position;
    public bool Alive = true;
    public EnemyKind Kind = EnemyKind.Grunt;
    public int SpriteIndex;
    public float Health = 30f;
    public float MaxHealth = 30f;
    public float HitRadius = 1.1f;
    public float MoveRadius = 0.55f;
    public float BillboardSize = 1.35f;
    public float ChaseSpeed = 1.8f;
    public float MeleeCooldown;
    public float RangedCooldown;
}
