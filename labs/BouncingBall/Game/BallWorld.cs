using System.Numerics;
using Novolis.Physics.Collision.Simple;
using Novolis.Simulation.World;
using Novolis.Simulation.World.Builders;

namespace BouncingBall.Game;

/// <summary>Many spheres in one room: spawn passes and library physics stepping.</summary>
internal sealed class BallWorld
{
    private const float PassSpeedMin = 7.2f;
    private const float PassSpeedMax = 9.2f;
    private const float PassLoft = 0.22f;

    private readonly SphereInStaticWorldSimulator _simulator = new()
    {
        Options =
        {
            Radius = SphereRadius.Value,
        },
    };

    private int _passSeed;
    private Random _passRng;
    private readonly List<SphereState> _spheres = [];
    private RoomInteriorBounds _bounds;
    private InteriorClampVolume _interiorClamp;

    public IReadOnlyList<SphereState> Spheres => _spheres;
    public int BallCount => _spheres.Count;
    public int ActiveBallCount => _simulator.LastStats.ActiveCount;
    public int SleepingBallCount => _simulator.LastStats.SleepingCount;
    public int BallBallContactsLastFrame => _simulator.LastStats.SphereContacts;
    public int BallBallPairChecksLastFrame => _simulator.LastStats.SpherePairChecks;
    public int IntegratorReflectionsLastFrame => _simulator.LastStats.IntegratorReflections;
    public int PhysicsSubStepsLastFrame => _simulator.LastStats.PhysicsSubSteps;
    public int BallBallSolveIterationsLastFrame => _simulator.LastStats.SphereContactIterations;
    public int ClampedBallsLastFrame => _simulator.LastStats.ClampedCount;
    public bool BallBallSkippedLastFrame => _simulator.LastStats.SphereContactSkipped;

    public BallWorld(BvhStaticWorld collisionWorld, int? passSeed = null)
    {
        CollisionWorld = collisionWorld;
        _passSeed = passSeed ?? unchecked(Environment.TickCount ^ (int)0x5DEECE66D);
        _passRng = new Random(_passSeed);
    }

    public BvhStaticWorld CollisionWorld { get; }

    public void BindRoom(RoomWorld room)
    {
        _bounds = room.InteriorBounds;
        _interiorClamp = room.InteriorBounds.ToInteriorClamp();
    }

    public void SpawnBall(RoomWorld room)
    {
        BindRoom(room);
        _spheres.Add(CreatePassSphere(room, spreadSpawn: false));
        _simulator.MarkPileUnsettled();
        _simulator.DepenetrateSpawnedRange(_spheres, _spheres.Count - 1, _spheres.Count, _interiorClamp);
    }

    public void SpawnBalls(RoomWorld room, int count)
    {
        if (count <= 0)
            return;

        BindRoom(room);
        var start = _spheres.Count;
        if (_spheres.Capacity < start + count)
            _spheres.Capacity = start + count;

        for (var i = 0; i < count; i++)
            _spheres.Add(CreatePassSphere(room, spreadSpawn: true, batchIndex: i, batchSize: count));

        _simulator.MarkPileUnsettled();
        _simulator.DepenetrateSpawnedRange(_spheres, start, _spheres.Count, _interiorClamp);
    }

    public void ClearAndSpawnOne(RoomWorld room)
    {
        _spheres.Clear();
        _simulator.ResetPileState();
        ChainPassSeed(_passRng.NextDouble(), _passRng.NextDouble());
        SpawnBall(room);
    }

    public void Step(float deltaSeconds) =>
        _simulator.Step(CollisionWorld, _spheres, _interiorClamp, deltaSeconds);

    private SphereState CreatePassSphere(RoomWorld room, bool spreadSpawn, int batchIndex = 0, int batchSize = 1)
    {
        var (spawn, velocity) = spreadSpawn
            ? SampleCircleWallPass(room, batchIndex, batchSize)
            : SamplePerimeterPass(room);
        spawn = ClampPosition(spawn, _bounds);
        velocity = ClampVelocity(velocity);
        return new SphereState(spawn, velocity);
    }

    private void ChainPassSeed(double roll0, double roll1, double roll2 = 0, int salt = 0)
    {
        _passSeed = unchecked((int)(
            (uint)_passSeed * 1597334677u
            + (uint)(roll0 * uint.MaxValue)
            + (uint)(roll1 * uint.MaxValue) * 3812015801u
            + (uint)(roll2 * uint.MaxValue) * 3965336299u
            + (uint)salt * 1442695041u));
        if (_passSeed == 0)
            _passSeed = 1;
        _passRng = new Random(_passSeed);
    }

    private (Vector3 Spawn, Vector3 Velocity) SamplePerimeterPass(RoomWorld room)
    {
        var yawRoll = _passRng.NextDouble();
        var speedRoll = _passRng.NextDouble();
        ChainPassSeed(yawRoll, speedRoll);

        var yaw = (float)(yawRoll * MathF.Tau);
        var horizontal = new Vector3(MathF.Sin(yaw), 0f, MathF.Cos(yaw));
        var spawn = room.RoomCenter - horizontal * WallStandRadius(room, horizontal) + new Vector3(0f, 1.15f, 0f);
        var speed = PassSpeedMin + (float)speedRoll * (PassSpeedMax - PassSpeedMin);
        var direction = Vector3.Normalize(horizontal + new Vector3(0f, PassLoft, 0f));
        return (spawn, direction * speed);
    }

    private (Vector3 Spawn, Vector3 Velocity) SampleCircleWallPass(RoomWorld room, int batchIndex, int batchSize)
    {
        var baseYawRoll = _passRng.NextDouble();
        var yawJitterRoll = _passRng.NextDouble();
        var speedRoll = _passRng.NextDouble();
        ChainPassSeed(baseYawRoll, yawJitterRoll, speedRoll, batchIndex);

        var baseYaw = (float)(baseYawRoll * MathF.Tau);
        var slotYaw = baseYaw + MathF.Tau * batchIndex / batchSize;
        var jitterSpan = MathF.Tau / Math.Max(batchSize, 1);
        var yaw = slotYaw + (float)((yawJitterRoll - 0.5) * jitterSpan * 0.55f);

        var towardCenter = new Vector3(MathF.Sin(yaw), 0f, MathF.Cos(yaw));
        var spawn = room.RoomCenter - towardCenter * WallStandRadius(room, towardCenter) + new Vector3(0f, 1.05f, 0f);
        var speed = PassSpeedMin + (float)speedRoll * (PassSpeedMax - PassSpeedMin);
        var direction = Vector3.Normalize(towardCenter + new Vector3(0f, PassLoft, 0f));
        return (spawn, direction * speed);
    }

    private static float WallStandRadius(RoomWorld room, Vector3 towardCenter)
    {
        var bounds = room.InteriorBounds;
        var c = room.RoomCenter;
        var dx = towardCenter.X;
        var dz = towardCenter.Z;
        var len = MathF.Sqrt(dx * dx + dz * dz);
        if (len < 1e-5f)
            return MathF.Min(c.X - bounds.MinX, c.Z - bounds.MinZ) * 0.94f;

        dx /= len;
        dz /= len;

        var radius = float.MaxValue;
        if (dx > 1e-5f)
            radius = MathF.Min(radius, (c.X - bounds.MinX) / dx);
        else if (dx < -1e-5f)
            radius = MathF.Min(radius, (bounds.MaxX - c.X) / -dx);

        if (dz > 1e-5f)
            radius = MathF.Min(radius, (c.Z - bounds.MinZ) / dz);
        else if (dz < -1e-5f)
            radius = MathF.Min(radius, (bounds.MaxZ - c.Z) / -dz);

        return radius * 0.94f;
    }

    private static Vector3 ClampPosition(Vector3 p, RoomInteriorBounds b) =>
        new(
            Math.Clamp(p.X, b.MinX, b.MaxX),
            Math.Clamp(p.Y, b.MinY, b.MaxY),
            Math.Clamp(p.Z, b.MinZ, b.MaxZ));

    private static Vector3 ClampVelocity(Vector3 v)
    {
        const float maxSpeed = 18f;
        var speed = v.Length();
        if (speed <= maxSpeed || speed < 1e-6f)
            return v;

        return v * (maxSpeed / speed);
    }
}
