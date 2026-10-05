using FrankMoat.Weapons;

namespace FrankMoat.Unit;

public sealed class WeaponStateTests
{
    [Test]
    public async Task PumpTopsUpOneShellAndKeepsReserve()
    {
        var gun = new WeaponState(WeaponCatalog.Pump12)
        {
            Magazine = 5,
            Reserve = 4,
        };

        gun.BeginReload();
        gun.TickReload(0.4f);

        await Assert.That(gun.Magazine).IsEqualTo(6);
        await Assert.That(gun.Reserve).IsEqualTo(3);
        await Assert.That(gun.Reloading).IsTrue();
    }

    [Test]
    public async Task PumpInterruptLeavesPartialTube()
    {
        var gun = new WeaponState(WeaponCatalog.Pump12)
        {
            Magazine = 3,
            Reserve = 8,
        };

        gun.BeginReload();
        gun.TickReload(0.4f);
        gun.InterruptReload();

        await Assert.That(gun.Magazine).IsEqualTo(4);
        await Assert.That(gun.Reloading).IsFalse();
        await Assert.That(gun.CanFire).IsTrue();
    }

    [Test]
    public async Task M1911ReloadFillsMagazineAndChamber()
    {
        var gun = new WeaponState(WeaponCatalog.M1911)
        {
            Magazine = 1,
            Chambered = false,
            Reserve = 20,
        };

        gun.BeginReload();
        gun.TickReload(2f);

        await Assert.That(gun.Magazine).IsEqualTo(7);
        await Assert.That(gun.Chambered).IsTrue();
        await Assert.That(gun.Reserve).IsEqualTo(13);
        await Assert.That(gun.Reloading).IsFalse();
    }
}
