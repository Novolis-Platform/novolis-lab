namespace ArtillerySimulator;

/// <summary>
/// Authoritative session seed and piece context below the Silk host.
/// Clock + seed make headless agent runs reproducible; the piece is the mission's equipment SoT.
/// </summary>
internal sealed class BattleSession
{
    public BattleSession(IArtilleryPieceSpec piece, int seed)
    {
        Piece = piece ?? throw new ArgumentNullException(nameof(piece));
        Seed = seed;
        Clock = new FixedStepClock();
        Delivery = new DeliverySampler(piece.Errors, seed);
    }

    public IArtilleryPieceSpec Piece { get; private set; }
    public int Seed { get; }
    public string SeedHash => Seed.ToString("x8", System.Globalization.CultureInfo.InvariantCulture);
    public FixedStepClock Clock { get; }
    public DeliverySampler Delivery { get; private set; }

    public void RebindPiece(IArtilleryPieceSpec piece)
    {
        Piece = piece ?? throw new ArgumentNullException(nameof(piece));
        Delivery = new DeliverySampler(piece.Errors, Seed);
    }
}
