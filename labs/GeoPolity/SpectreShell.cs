using GeoPolity.Agent;
using GeoPolity.Session;
using Novolis.Agent.Surface;
using Spectre.Console;

namespace GeoPolity;

internal static class SpectreShell
{
    public static async Task RunAsync(GeoSession session, bool attachSessionAgent)
    {
        AgentSurface? surface = null;
        if (attachSessionAgent)
        {
            surface = AgentSurface.AttachAll(
                new GeoPolityAgentHost(session),
                GeoPolitySessionContract.Definition,
                new AgentAttachOptions
                {
                    EnableIpc = true,
                    EnableHttp = true,
                    EnableTcp = true,
                    EnableRpc = false,
                    HttpPort = 18857,
                    TcpPort = 18858,
                });
            if (surface?.HttpBaseUrl is { } url)
            {
                Console.WriteLine($"GeoPolity Agent Surface: {url}");
            }
        }

        try
        {
            AnsiConsole.Clear();
            AnsiConsole.Write(new FigletText("GeoPolity").Color(Color.SteelBlue1));
            AnsiConsole.MarkupLine("[grey]Full-world geopolitics UI · homage only · not affiliated with SuperPower[/]");
            AnsiConsole.MarkupLine(
                $"[steelblue1]{session.World.Polities.Count}[/] nations · [teal]{session.World.Provinces.Count}[/] provinces · seed [grey]{session.World.Seed}[/]");
            AnsiConsole.MarkupLine(
                "[grey]Keys[/]  [bold]Space[/] run/pause   [bold]1[/] day  [bold]2[/] week  [bold]3[/] month  [bold]4[/] year  [bold]5[/] 5y   [bold]Q[/] quit");
            AnsiConsole.WriteLine();

            await AnsiConsole.Live(Build(session))
                .AutoClear(false)
                .Overflow(VerticalOverflow.Ellipsis)
                .StartAsync(async ctx =>
                {
                    while (!session.QuitRequested)
                    {
                        ctx.UpdateTarget(Build(session));

                        while (Console.KeyAvailable)
                        {
                            var key = Console.ReadKey(intercept: true);
                            if (!HandleKey(session, key))
                            {
                                ctx.UpdateTarget(Build(session));
                                return;
                            }
                        }

                        session.PulseIfRunning();
                        await Task.Delay(session.Clock.PulseMs);
                    }
                });
        }
        finally
        {
            if (surface is not null)
            {
                await surface.DisposeAsync();
            }
        }
    }

    private static Spectre.Console.Rendering.IRenderable Build(GeoSession session)
    {
        var headlines = session.Headlines.Lines
            .Select(Dashboard.FormatHeadline)
            .ToList();
        return Dashboard.Build(
            session.World,
            session.Simulation,
            headlines,
            session.Clock.Running,
            session.Clock.DaysPerPulse,
            session.Clock.StatusNote);
    }

    private static bool HandleKey(GeoSession session, ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.Q:
                GeoSessionCommands.Quit(session);
                return false;
            case ConsoleKey.Spacebar:
                GeoSessionCommands.ToggleRun(session);
                break;
            case ConsoleKey.D1:
                GeoSessionCommands.SetSpeed(session, 1);
                break;
            case ConsoleKey.D2:
                GeoSessionCommands.SetSpeed(session, 2);
                break;
            case ConsoleKey.D3:
                GeoSessionCommands.SetSpeed(session, 3);
                break;
            case ConsoleKey.D4:
                GeoSessionCommands.SetSpeed(session, 4);
                break;
            case ConsoleKey.D5:
                GeoSessionCommands.SetSpeed(session, 5);
                break;
        }

        return true;
    }
}
