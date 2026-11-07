namespace ArtillerySimulator;

/// <summary>First shipped level — Odderøya battery vs road columns into Kvadraturen.</summary>
internal sealed class KristiansandDefenseLevel : IGameLevel
{
    public const string LevelId = "kristiansand.defense";

    public string Id => LevelId;
    public string DisplayName => "Defense of Kristiansand";
    public string SiteName => "Odderøya";
    public string Blurb =>
        "One gun on the Odderøya fort. Road columns toward Kvadraturen. Downtown sits inside HE min.";
    public string PieceSpecId => ProductIdentity.PieceSpecId;
    public bool IsPlayable => true;

    public void RebuildMap(TerrainWorld terrain) => terrain.Rebuild();

    public IDefenseMission StartMission(TerrainWorld terrain, IArtilleryPieceSpec piece, int seed) =>
        new KristiansandDefenseMission(terrain, piece, seed);
}
