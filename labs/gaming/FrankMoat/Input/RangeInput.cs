namespace FrankMoat.Input;

internal readonly record struct RangeInput(
    float MoveX,
    float MoveZ,
    bool ShootHeld,
    bool ShootPressed,
    bool ReloadPressed,
    bool InspectPressed,
    bool CyclePressed,
    WeaponHotkey Hotkey,
    int ZoomTicks);
