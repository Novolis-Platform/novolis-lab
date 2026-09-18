namespace TopDownDoom.Design;

public readonly record struct MonsterSpawn(
    MonsterRole Role,
    float WorldX,
    float WorldZ,
    string ClosetTag = "");

public sealed record EncounterReward(
    int HealthBonus,
    int ArmorBonus,
    int AmmoBonus,
    bool IsSecret);

public sealed record CombatEncounter(
    string Name,
    string Trigger,
    IReadOnlyList<MonsterSpawn> InitialSpawns,
    IReadOnlyList<MonsterSpawn> Reinforcements,
    IReadOnlyList<DoorId> LockedDoors,
    EncounterReward Reward);
