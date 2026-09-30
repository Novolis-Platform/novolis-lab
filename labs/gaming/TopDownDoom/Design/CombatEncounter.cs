namespace TopDownDoom.Design;

public sealed record CombatEncounter(
    string Name,
    string Trigger,
    IReadOnlyList<MonsterSpawn> InitialSpawns,
    IReadOnlyList<MonsterSpawn> Reinforcements,
    IReadOnlyList<DoorId> LockedDoors,
    EncounterReward Reward);
