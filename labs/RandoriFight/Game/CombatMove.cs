namespace RandoriFight.Game;

internal readonly struct CombatMove(
    float startup,
    float active,
    float recovery,
    float range,
    float radius,
    float damage,
    float hitStun,
    float strikeHeight)
{
    public float Startup { get; } = startup;
    public float Active { get; } = active;
    public float Recovery { get; } = recovery;
    public float Range { get; } = range;
    public float Radius { get; } = radius;
    public float Damage { get; } = damage;
    public float HitStun { get; } = hitStun;
    public float StrikeHeight { get; } = strikeHeight;
    public float Total => Startup + Active + Recovery;
}

internal static class CombatMoves
{
    public static readonly CombatMove Men = new(0.14f, 0.09f, 0.26f, 1.35f, 0.38f, 16f, 0.28f, 1.52f);
    public static readonly CombatMove Kesa = new(0.1f, 0.1f, 0.24f, 1.25f, 0.44f, 13f, 0.24f, 1.12f);
    public static readonly CombatMove Thrust = new(0.06f, 0.07f, 0.22f, 1.55f, 0.28f, 11f, 0.2f, 1.05f);
    public static readonly CombatMove Do = new(0.09f, 0.09f, 0.23f, 1.3f, 0.4f, 14f, 0.25f, 1.02f);
    public static readonly CombatMove Kote = new(0.08f, 0.08f, 0.21f, 1.15f, 0.36f, 10f, 0.18f, 0.82f);
    public static readonly CombatMove Kirioroshi = new(0.12f, 0.1f, 0.28f, 1.4f, 0.4f, 17f, 0.3f, 1.38f);

    public static CombatMove For(FighterState state) => state switch
    {
        FighterState.Men => Men,
        FighterState.Kesa => Kesa,
        FighterState.Thrust => Thrust,
        FighterState.Do => Do,
        FighterState.Kote => Kote,
        FighterState.Kirioroshi => Kirioroshi,
        _ => Men,
    };
}
