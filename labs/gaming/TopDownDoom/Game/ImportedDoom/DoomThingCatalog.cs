using TopDownDoom.Design;
using TopDownDoom.Game;

namespace TopDownDoom.Game.ImportedDoom;

internal static class DoomThingCatalog
{
    public static bool IsPlayerStart(short type) => type is 1 or 2 or 3 or 4;

    public static MonsterRole? TryMonsterRole(short type) => type switch
    {
        3004 or 9 => MonsterRole.Hitscan,
        3001 => MonsterRole.Projectile,
        3002 or 58 => MonsterRole.Charger,
        3003 => MonsterRole.Fodder,
        3005 or 3006 => MonsterRole.Flier,
        64 => MonsterRole.Summoner,
        7 or 16 => MonsterRole.Boss,
        _ => null,
    };

    public static PickupKind? TryPickup(short type) => type switch
    {
        2011 or 2012 or 2014 => PickupKind.Health,
        2015 or 2018 => PickupKind.Armor,
        2007 or 2008 or 2048 or 2049 or 17 => PickupKind.Ammo,
        2001 or 2002 or 2003 or 2004 or 2005 or 2006 => PickupKind.Ammo,
        5 or 6 or 13 or 39 => PickupKind.BlueKey,
        _ => null,
    };

    public static bool IsExitThing(short type) => type is 11 or 52;
}
