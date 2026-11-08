using Microsoft.Extensions.Logging;
using Novolis.Physics.Ballistics;

namespace ArtillerySimulator;

internal sealed class LevelTransitionService
{
    private readonly BattleSession _session;
    private readonly CrewDrill _crew;
    private readonly ArtilleryRoundPool _rounds;
    private readonly ArtilleryBattleLoop _battle;
    private readonly AimPreviewService _aim;
    private readonly ArtilleryShellController _shell;
    private readonly ILogger _log;

    public LevelTransitionService(
        BattleSession session,
        CrewDrill crew,
        ArtilleryRoundPool rounds,
        ArtilleryBattleLoop battle,
        AimPreviewService aim,
        ArtilleryShellController shell,
        ILogger logger)
    {
        _session = session;
        _crew = crew;
        _rounds = rounds;
        _battle = battle;
        _aim = aim;
        _shell = shell;
        _log = logger;
    }

    public void BindPiece(IArtilleryPieceSpec piece, ref GunModel gun, ref ProjectileBallisticSimulation ballistics)
    {
        _session.RebindPiece(piece);
        gun = new GunModel(piece);
        ballistics = new ProjectileBallisticSimulation(piece.Projectile, DragCoefficientLaw.BoatTail);
        _aim.Bind(gun, ballistics);
        _battle.Bind(gun, _crew, ballistics);
        _aim.MarkDirty();
    }

    public void SelectLevel(int delta, ref GunModel gun, ref ProjectileBallisticSimulation ballistics)
    {
        var next = GameLevelCatalog.NextPlayable(_shell.Level, delta);
        if (string.Equals(next.Id, _shell.Level.Id, StringComparison.OrdinalIgnoreCase))
            return;

        _shell.SetLevel(next);
        BindPiece(next.ResolvePiece(), ref gun, ref ballistics);
        _rounds.ResetAll(_battle.ImpactHandled);
        _log.LogInformation("Selected level {LevelId}", next.Id);
    }
}
