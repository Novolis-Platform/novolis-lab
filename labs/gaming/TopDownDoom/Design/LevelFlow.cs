namespace TopDownDoom.Design;

public readonly record struct RoomId(string Value);

public readonly record struct DoorId(string Value);

public readonly record struct KeyColor(string Value);

public sealed record Room(RoomId Id, string Label);

public sealed record KeyGate(KeyColor Color, DoorId Door, RoomId UnlocksInto);

public sealed record Secret(RoomId Room, string Hint);

public sealed record ExitCondition(RoomId FinalArena, string SwitchTag);

public sealed record LevelFlow(
    Room Start,
    IReadOnlyList<KeyGate> Gates,
    IReadOnlyList<CombatEncounter> Encounters,
    IReadOnlyList<Secret> Secrets,
    ExitCondition Exit);
