namespace ArtillerySimulator;

internal struct PendingWave
{
    public PendingWave(string roadToken, float atSeconds, bool spawned)
    {
        RoadToken = roadToken;
        AtSeconds = atSeconds;
        Spawned = spawned;
    }

    public string RoadToken;
    public float AtSeconds;
    public bool Spawned;
}
