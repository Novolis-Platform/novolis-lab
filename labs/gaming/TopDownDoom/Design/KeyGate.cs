namespace TopDownDoom.Design;

public sealed record KeyGate(KeyColor Color, DoorId Door, RoomId UnlocksInto);
