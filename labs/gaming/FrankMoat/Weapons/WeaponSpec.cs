namespace FrankMoat.Weapons;

internal sealed record WeaponSpec(
    WeaponId Id,
    string Name,
    string Chambering,
    WeaponFireMode Mode,
    int MagazineSize,
    bool ChamberPlusOne,
    bool TubeFeed,
    float FireIntervalSeconds,
    float ReloadSeconds,
    float ShellInsertSeconds,
    float SpinUpSeconds,
    int PelletCount,
    float SpreadDegrees,
    float ProjectileSpeed,
    int Damage,
    float RecoilShake,
    int ReserveStart,
    string InspectLine);
