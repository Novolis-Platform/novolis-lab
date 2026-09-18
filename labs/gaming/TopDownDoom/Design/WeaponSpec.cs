namespace TopDownDoom.Design;

public sealed record WeaponSpec(
    string Name,
    TimeSpan FireInterval,
    int PelletCount,
    float SpreadDegrees,
    float ProjectileSpeed,
    int Damage,
    float SplashRadius,
    bool CausesSelfDamage);
