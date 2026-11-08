namespace ArtillerySimulator;

internal static class OpforDifficultyCurve
{
    public static void ApplyRoundDifficulty(DefenseBattlefieldState state) =>
        state.ColumnSpeed = DefenseBattlefieldState.BaseColumnSpeedMetersPerSecond
            * (1f + 0.18f * (state.Round - 1));

    public static float WaveIntervalSeconds(DefenseBattlefieldState state) =>
        Math.Clamp(95f - (state.Round - 1) * 12f, 35f, 95f);

    public static int ColumnsThisWave(DefenseBattlefieldState state) =>
        Math.Clamp(1 + (state.Round - 1) / 2 + (state.WaveSerial % 3 == 0 ? 1 : 0), 1, 4);

    public static int VehiclesPerColumn(DefenseBattlefieldState state) =>
        Math.Clamp(4 + state.Round, 4, 10);
}
