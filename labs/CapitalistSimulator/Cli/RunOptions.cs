using CapitalistSimulator.Sim;

namespace CapitalistSimulator.Cli;

internal enum AppMode
{
    Headless,
    Avalonia,
}

internal sealed class RunOptions
{
    public AppMode Mode { get; init; } = AppMode.Avalonia;
    public ScenarioId Scenario { get; init; } = ScenarioId.Sandbox;
    public int Days { get; init; } = 36;
    public int Seed { get; init; } = 42;
    public decimal StartingCash { get; init; } = 2_000_000;
    public int AiCount { get; init; } = 2;
    public double AiAggressiveness { get; init; } = 0.55;
    public bool Quiet { get; init; }
    public string? SaveName { get; init; }
    public string? LoadName { get; init; }
    public string? PlaytestWin { get; init; }

    public static RunOptions Parse(string[] args)
    {
        var mode = AppMode.Avalonia;
        var scenario = ScenarioId.Sandbox;
        var days = 36;
        var seed = 42;
        var cash = 2_000_000m;
        var ai = 2;
        var agg = 0.55;
        var quiet = false;
        string? save = null;
        string? load = null;
        string? playtestWin = null;

        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i];
            string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"Missing value for {a}");

            switch (a)
            {
                case "--mode":
                    mode = Enum.Parse<AppMode>(Next(), ignoreCase: true);
                    break;
                case "--scenario":
                    scenario = Enum.Parse<ScenarioId>(Next(), ignoreCase: true);
                    break;
                case "--days":
                    days = int.Parse(Next().TrimEnd('d', 'D'));
                    break;
                case "--seed":
                    seed = int.Parse(Next());
                    break;
                case "--cash":
                    cash = decimal.Parse(Next());
                    break;
                case "--ai":
                    ai = int.Parse(Next());
                    break;
                case "--ai-agg":
                    agg = double.Parse(Next());
                    break;
                case "--quiet":
                    quiet = true;
                    break;
                case "--save":
                    save = Next();
                    break;
                case "--load":
                    load = Next();
                    break;
                case "--playtest-win":
                    playtestWin = Next();
                    mode = AppMode.Headless;
                    break;
                case "--help":
                case "-h":
                    throw new ArgumentException(HelpText);
                default:
                    if (a.StartsWith('-'))
                        throw new ArgumentException($"Unknown arg: {a}\n{HelpText}");
                    break;
            }
        }

        return new RunOptions
        {
            Mode = mode,
            Scenario = scenario,
            Days = days,
            Seed = seed,
            StartingCash = cash,
            AiCount = ai,
            AiAggressiveness = agg,
            Quiet = quiet,
            SaveName = save,
            LoadName = load,
            PlaytestWin = playtestWin,
        };
    }

    public const string HelpText =
        """
        Capitalist Simulator — Capitalism 2 homage

          --mode avalonia|headless
          --scenario Sandbox|RetailProfit|WineDominance
          --days N          headless advance days (default 36)
          --seed N
          --cash N
          --ai N
          --ai-agg 0..1
          --save NAME
          --load NAME
          --quiet
          --playtest-win retail|wine|both
        """;
}
