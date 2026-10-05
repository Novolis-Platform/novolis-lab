namespace FrankMoat.Weapons;

internal static class WeaponCatalog
{
    public static readonly WeaponSpec M1911 = new(
        WeaponId.M1911,
        "M1911",
        ".45 ACP",
        WeaponFireMode.Semi,
        MagazineSize: 7,
        ChamberPlusOne: true,
        TubeFeed: false,
        FireIntervalSeconds: 0.16f,
        ReloadSeconds: 1.15f,
        ShellInsertSeconds: 0f,
        SpinUpSeconds: 0f,
        PelletCount: 1,
        SpreadDegrees: 1.4f,
        ProjectileSpeed: 95f,
        Damage: 28,
        RecoilShake: 0.09f,
        ReserveStart: 56,
        InspectLine: "Emotional support. Seven in the stick, one in the pipe.");

    public static readonly WeaponSpec Pump12 = new(
        WeaponId.Pump12,
        "Pump 12",
        "12 gauge",
        WeaponFireMode.Pump,
        MagazineSize: 8,
        ChamberPlusOne: false,
        TubeFeed: true,
        FireIntervalSeconds: 0.62f,
        ReloadSeconds: 0f,
        ShellInsertSeconds: 0.38f,
        SpinUpSeconds: 0f,
        PelletCount: 8,
        SpreadDegrees: 11f,
        ProjectileSpeed: 70f,
        Damage: 11,
        RecoilShake: 0.22f,
        ReserveStart: 32,
        InspectLine: "Tube-fed. I can top this up while they are still walking.");

    public static readonly WeaponSpec Rotary = new(
        WeaponId.Rotary,
        "HRRS Rotary",
        "7.62 linked",
        WeaponFireMode.Rotary,
        MagazineSize: 200,
        ChamberPlusOne: false,
        TubeFeed: false,
        FireIntervalSeconds: 0.035f,
        ReloadSeconds: 3.4f,
        ShellInsertSeconds: 0f,
        SpinUpSeconds: 0.42f,
        PelletCount: 1,
        SpreadDegrees: 3.2f,
        ProjectileSpeed: 110f,
        Damage: 14,
        RecoilShake: 0.05f,
        ReserveStart: 400,
        InspectLine: "Engineering's toy. HR insists it is not a minigun.");

    public static IReadOnlyList<WeaponSpec> All { get; } = [M1911, Pump12, Rotary];

    public static WeaponSpec FromId(WeaponId id) => id switch
    {
        WeaponId.Pump12 => Pump12,
        WeaponId.Rotary => Rotary,
        _ => M1911,
    };
}
