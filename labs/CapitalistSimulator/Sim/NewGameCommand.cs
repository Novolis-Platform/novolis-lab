namespace CapitalistSimulator.Sim;

internal sealed record NewGameCommand(
    ScenarioId Scenario,
    decimal StartingCash,
    int AiCount,
    double AiAggressiveness,
    int Seed) : PlayerCommand;
