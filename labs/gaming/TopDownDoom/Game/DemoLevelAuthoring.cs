using Novolis.Math.Geometry;
using Novolis.Rendering.TwoD;
using TopDownDoom.Design;

namespace TopDownDoom.Game;

internal static class DemoLevelAuthoring
{
    public static LevelFlow CreateFlow() => new(
        Start: new Room(new RoomId("start"), "Start room — movement lesson"),
        Gates: [new KeyGate(new KeyColor("blue"), new DoorId("blue-gate"), new RoomId("arena"))],
        Encounters:
        [
            new CombatEncounter(
                "Blue key trap",
                "pickup:blue-key",
                InitialSpawns: [],
                Reinforcements:
                [
                    new MonsterSpawn(MonsterRole.Fodder, 28f, 8f, "closet-west"),
                    new MonsterSpawn(MonsterRole.Fodder, 30f, 10f, "closet-west"),
                    new MonsterSpawn(MonsterRole.Projectile, 32f, 6f, "closet-east"),
                    new MonsterSpawn(MonsterRole.Charger, 34f, 12f, "closet-east"),
                    new MonsterSpawn(MonsterRole.Bruiser, 36f, 9f, "closet-north"),
                    new MonsterSpawn(MonsterRole.Hitscan, 33f, 16f, "arena"),
                ],
                LockedDoors: [new DoorId("arena-exit")],
                Reward: new EncounterReward(0, 0, 12, IsSecret: false)),
            new CombatEncounter(
                "First lesson",
                "enter:corridor",
                InitialSpawns:
                [
                    new MonsterSpawn(MonsterRole.Fodder, 12f, 6f),
                    new MonsterSpawn(MonsterRole.Fodder, 14f, 8f),
                    new MonsterSpawn(MonsterRole.Charger, 16f, 17f),
                ],
                Reinforcements: [],
                LockedDoors: [],
                Reward: new EncounterReward(15, 0, 8, IsSecret: false)),
        ],
        Secrets: [new Secret(new RoomId("ammo-nook"), "Cracked wall behind fodder closet")],
        Exit: new ExitCondition(new RoomId("final-arena"), "exit-switch"));

    public static void BuildGeometry(TwoDScene scene, DoomLevelState level)
    {
        scene.Camera.ClearColor = new Rgba32(14, 10, 18);

        AddOuterShell(scene);
        AddStartRoom(scene);
        AddCorridor(scene);
        RegisterBlueGate(scene, level);
        RegisterArena(scene, level);
        AddSecretNook(scene);
    }

    public static void SeedEntities(TopDownCombatWorld world)
    {
        world.PlayerPosition = new System.Numerics.Vector3(5f, 0f, 5.5f);
        world.Pickups.Add(new Pickup(PickupKind.Armor, new System.Numerics.Vector3(6.5f, 0f, 7f)));
        world.Pickups.Add(new Pickup(PickupKind.Health, new System.Numerics.Vector3(8f, 0f, 8.5f)));
        world.Pickups.Add(new Pickup(PickupKind.Ammo, new System.Numerics.Vector3(14f, 0f, 6f)));
        world.Pickups.Add(new Pickup(PickupKind.BlueKey, new System.Numerics.Vector3(24f, 0f, 11f)));
        world.Pickups.Add(new Pickup(PickupKind.Health, new System.Numerics.Vector3(32f, 0f, 18f)));
        world.Pickups.Add(new Pickup(PickupKind.Ammo, new System.Numerics.Vector3(35f, 0f, 6f)));
        world.Pickups.Add(new Pickup(PickupKind.Exit, new System.Numerics.Vector3(38f, 0f, 12f)));

        world.Barrels.Add(new ExplosiveBarrel(new System.Numerics.Vector3(16f, 0f, 16f)));
        world.Barrels.Add(new ExplosiveBarrel(new System.Numerics.Vector3(22f, 0f, 18f)));
        world.Barrels.Add(new ExplosiveBarrel(new System.Numerics.Vector3(31f, 0f, 14f)));

        world.SpawnMonster(MonsterRole.Hitscan, 29f, 5f);
    }

    private static void AddOuterShell(TwoDScene scene)
    {
        ObliqueWallDrawer.AddWall(scene, 0f, 0f, 42f, 1f);
        ObliqueWallDrawer.AddWall(scene, 0f, 23f, 42f, 24f);
        ObliqueWallDrawer.AddWall(scene, 0f, 0f, 1f, 24f);
        ObliqueWallDrawer.AddWall(scene, 41f, 0f, 42f, 24f);
    }

    private static void AddStartRoom(TwoDScene scene)
    {
        ObliqueWallDrawer.AddWall(scene, 1f, 1f, 10f, 2f);
        ObliqueWallDrawer.AddWall(scene, 1f, 10f, 10f, 11f, new Rgba32(55, 48, 62));
    }

    private static void AddCorridor(TwoDScene scene)
    {
        ObliqueWallDrawer.AddWall(scene, 10f, 4f, 11f, 18f);
        ObliqueWallDrawer.AddWall(scene, 18f, 4f, 19f, 18f);
        ObliqueWallDrawer.AddDoorFrame(scene, 19f, 8.5f, 20f, 14.5f);
    }

    private static void RegisterBlueGate(TwoDScene scene, DoomLevelState level)
    {
        var (_, north, blocker) = ObliqueWallDrawer.AddWall(scene, 19f, 9f, 20f, 14f, new Rgba32(50, 60, 100));
        level.BlueGateVisuals.Add(north);
        level.BlueGateColliders.Add(blocker);
    }

    private static void RegisterArena(TwoDScene scene, DoomLevelState level)
    {
        ObliqueWallDrawer.AddWall(scene, 20f, 1f, 40f, 2f);
        ObliqueWallDrawer.AddWall(scene, 20f, 21f, 40f, 22f);

        var (_, northCloset, northBlock) = ObliqueWallDrawer.AddWall(scene, 26f, 2f, 27f, 8f);
        level.ClosetNorthVisuals.Add(northCloset);
        level.ClosetNorthColliders.Add(northBlock);

        var (_, eastCloset, eastBlock) = ObliqueWallDrawer.AddWall(scene, 33f, 14f, 34f, 21f);
        level.ClosetEastVisuals.Add(eastCloset);
        level.ClosetEastColliders.Add(eastBlock);

        ObliqueWallDrawer.AddWall(scene, 36f, 10f, 40f, 11f);
    }

    private static void AddSecretNook(TwoDScene scene)
    {
        ObliqueWallDrawer.AddWall(scene, 6f, 14f, 9f, 15f);
        ObliqueWallDrawer.AddSecretCrack(scene, 7.2f, 14.4f);
    }
}
