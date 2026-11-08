namespace ArtillerySimulator;

/// <summary>
/// Frame-thread battery mutations. Discrete intents also arrive via <see cref="BattleCommand"/> channel.
/// </summary>
internal interface IBatteryApplier
{
    BattleCommandResult Apply(BattleCommand command);

    /// <summary>Continuous handwheel nudge — never channelled (alloc-free per frame).</summary>
    void NudgeOrder(float deltaElevationDegrees, float deltaTraverseDegrees);

    void MarkAimDirty();

    void ToggleOptionalShellFollow();

    void SnapCameraToGun();

    void SnapCameraToBattle();

    void ShowMapOverview();

    /// <summary>Tab — next tube in the level battery (no-op when only one mount).</summary>
    void CycleGun();
}
