namespace FrankMoat.Weapons;

internal sealed class WeaponState
{
    public WeaponState(WeaponSpec spec)
    {
        Spec = spec;
        Magazine = spec.MagazineSize;
        Chambered = spec.ChamberPlusOne;
        Reserve = spec.ReserveStart;
    }

    public WeaponSpec Spec { get; private set; }

    public int Magazine { get; set; }

    public bool Chambered { get; set; }

    public int Reserve { get; set; }

    public float Cooldown { get; set; }

    public float ReloadTimer { get; set; }

    public bool Reloading { get; set; }

    public float Spin { get; set; }

    public int RoundsInGun => Magazine + (Chambered ? 1 : 0);

    public bool CanFire => !Reloading && Cooldown <= 0f && RoundsInGun > 0;

    public void Equip(WeaponSpec spec)
    {
        Spec = spec;
        Reloading = false;
        ReloadTimer = 0f;
        Spin = 0f;
        Cooldown = 0f;
    }

    public void ConsumeRound()
    {
        if (Chambered)
        {
            Chambered = Magazine > 0;
            if (Magazine > 0)
            {
                Magazine--;
            }

            return;
        }

        if (Magazine > 0)
        {
            Magazine--;
        }
    }

    public void BeginReload()
    {
        if (Reloading || Reserve <= 0)
        {
            return;
        }

        if (Spec.TubeFeed)
        {
            if (Magazine >= Spec.MagazineSize)
            {
                return;
            }

            Reloading = true;
            ReloadTimer = Spec.ShellInsertSeconds;
            return;
        }

        if (Magazine >= Spec.MagazineSize && (!Spec.ChamberPlusOne || Chambered))
        {
            return;
        }

        Reloading = true;
        ReloadTimer = Spec.ReloadSeconds;
    }

    public void InterruptReload()
    {
        Reloading = false;
        ReloadTimer = 0f;
    }

    public void TickReload(float dt)
    {
        if (!Reloading)
        {
            return;
        }

        ReloadTimer -= dt;
        if (ReloadTimer > 0f)
        {
            return;
        }

        if (Spec.TubeFeed)
        {
            if (Reserve > 0 && Magazine < Spec.MagazineSize)
            {
                Magazine++;
                Reserve--;
            }

            if (Reserve > 0 && Magazine < Spec.MagazineSize)
            {
                ReloadTimer = Spec.ShellInsertSeconds;
                return;
            }

            Reloading = false;
            return;
        }

        var capacity = Spec.MagazineSize;
        var need = capacity - Magazine;
        if (Spec.ChamberPlusOne && !Chambered && Reserve > need)
        {
            need++;
        }

        var take = Math.Min(need, Reserve);
        Reserve -= take;
        var intoMag = Math.Min(take, capacity - Magazine);
        Magazine += intoMag;
        take -= intoMag;
        if (take > 0 && Spec.ChamberPlusOne)
        {
            Chambered = true;
        }

        Reloading = false;
    }
}
