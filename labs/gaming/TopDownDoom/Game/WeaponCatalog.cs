using TopDownDoom.Design;

namespace TopDownDoom.Game;

internal static class WeaponCatalog
{
    public static readonly WeaponSpec Pistol = new(
        "Pistol", TimeSpan.FromMilliseconds(280), 1, 2f, 28f, 8, 0f, false);

    public static readonly WeaponSpec Shotgun = new(
        "Shotgun", TimeSpan.FromMilliseconds(720), 8, 14f, 24f, 6, 0f, false);

    public static readonly WeaponSpec SuperShotgun = new(
        "Super Shotgun", TimeSpan.FromSeconds(1.1), 12, 22f, 20f, 9, 0f, false);

    public static readonly WeaponSpec Chaingun = new(
        "Chaingun", TimeSpan.FromMilliseconds(90), 1, 4f, 32f, 5, 0f, false);

    public static readonly WeaponSpec RocketLauncher = new(
        "Rocket Launcher", TimeSpan.FromMilliseconds(900), 1, 0f, 18f, 40, 2.2f, true);

    public static readonly WeaponSpec PlasmaRifle = new(
        "Plasma Rifle", TimeSpan.FromMilliseconds(120), 1, 2f, 34f, 7, 0f, false);

    public static readonly WeaponSpec Bfg = new(
        "BFG", TimeSpan.FromSeconds(2.4), 1, 0f, 14f, 120, 4.5f, false);

    public static readonly WeaponSpec Chainsaw = new(
        "Chainsaw", TimeSpan.FromMilliseconds(60), 1, 25f, 0f, 12, 0f, false);

    public static readonly WeaponSpec GrenadeLauncher = new(
        "Grenade Launcher", TimeSpan.FromMilliseconds(700), 1, 0f, 14f, 35, 1.8f, true);

    public static IReadOnlyList<WeaponSpec> All { get; } =
    [
        Pistol, Shotgun, SuperShotgun, Chaingun, RocketLauncher,
        PlasmaRifle, Bfg, Chainsaw, GrenadeLauncher,
    ];
}
