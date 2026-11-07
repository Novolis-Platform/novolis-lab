namespace ArtillerySimulator;

/// <summary>
/// One playable map / scenario under K9 DOG.
/// Register implementations in <see cref="GameLevelCatalog"/> — do not hardcode maps in the host.
/// </summary>
internal interface IGameLevel
{
    string Id { get; }
    string DisplayName { get; }
    string SiteName { get; }
    string Blurb { get; }
    string PieceSpecId { get; }

    /// <summary>False = listed in catalog / splash but not bootable yet.</summary>
    bool IsPlayable { get; }

    IArtilleryPieceSpec ResolvePiece() =>
        ArtilleryPieceCatalog.TryGet(PieceSpecId, out var piece)
            ? piece
            : ArtilleryPieceCatalog.Default;

    /// <summary>Rebuild heightfield / roads for this level into the shared terrain host.</summary>
    void RebuildMap(TerrainWorld terrain);

    /// <summary>Start a fresh mission on an already-rebuilt terrain with the session piece and seed.</summary>
    IDefenseMission StartMission(TerrainWorld terrain, IArtilleryPieceSpec piece, int seed);
}
