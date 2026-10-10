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
