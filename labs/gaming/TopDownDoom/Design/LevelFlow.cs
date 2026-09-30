namespace TopDownDoom.Design;

public sealed record LevelFlow(
    Room Start,
    IReadOnlyList<KeyGate> Gates,
    IReadOnlyList<CombatEncounter> Encounters,
    IReadOnlyList<Secret> Secrets,
    ExitCondition Exit);
