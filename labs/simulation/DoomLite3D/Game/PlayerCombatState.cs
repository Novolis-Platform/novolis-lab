namespace DoomLite3D.Game;

internal sealed class PlayerCombatState
{
    public const float DefaultMaxHealth = 100f;
    public const int DefaultMaxAmmo = 30;
    public const int DefaultReserveAmmo = 90;
    public const float FireCooldownSeconds = 0.35f;
    public const float ReloadSeconds = 1f;
    public const float EnemyMeleeRange = 1.2f;
    public const float EnemyMeleeDamage = 12f;
    public const float EnemyMeleeCooldown = 0.5f;
    public const float WeaponDamage = 15f;

    public float Health { get; private set; } = DefaultMaxHealth;
    public float MaxHealth { get; } = DefaultMaxHealth;
    public int Ammo { get; private set; } = DefaultMaxAmmo;
    public int MaxAmmo { get; } = DefaultMaxAmmo;
    public int ReserveAmmo { get; private set; } = DefaultReserveAmmo;

    public float FireCooldown { get; set; }
    public float ReloadTimer { get; private set; }
    public bool IsDead => Health <= 0f;
    public bool IsReloading => ReloadTimer > 0f;

    public bool LastShotFired { get; private set; }
    public bool LastShotHit { get; set; }
    public float MuzzleFlashTimer { get; private set; }
    public float HitFlashTimer { get; private set; }

    public void Reset()
    {
        Health = MaxHealth;
        Ammo = MaxAmmo;
        ReserveAmmo = DefaultReserveAmmo;
        FireCooldown = 0f;
        ReloadTimer = 0f;
        MuzzleFlashTimer = 0f;
        HitFlashTimer = 0f;
        LastShotFired = false;
        LastShotHit = false;
    }

    public void Tick(float dt)
    {
        FireCooldown = Math.Max(0f, FireCooldown - dt);
        MuzzleFlashTimer = Math.Max(0f, MuzzleFlashTimer - dt);
        HitFlashTimer = Math.Max(0f, HitFlashTimer - dt);
        LastShotFired = false;
        LastShotHit = false;

        if (ReloadTimer <= 0f)
            return;

        ReloadTimer -= dt;
        if (ReloadTimer > 0f)
            return;

        var need = MaxAmmo - Ammo;
        if (need <= 0 || ReserveAmmo <= 0)
            return;

        var take = Math.Min(need, ReserveAmmo);
        Ammo += take;
        ReserveAmmo -= take;
    }

    public bool CanFire() => !IsDead && !IsReloading && FireCooldown <= 0f && Ammo > 0;

    public bool TryConsumeShot()
    {
        if (!CanFire())
            return false;

        Ammo--;
        FireCooldown = FireCooldownSeconds;
        LastShotFired = true;
        MuzzleFlashTimer = 0.08f;
        return true;
    }

    public void RegisterHit() => HitFlashTimer = 0.12f;

    public bool TryStartReload()
    {
        if (IsDead || IsReloading || Ammo >= MaxAmmo || ReserveAmmo <= 0)
            return false;

        ReloadTimer = ReloadSeconds;
        return true;
    }

    public void TakeDamage(float amount)
    {
        if (IsDead)
            return;
        Health = Math.Max(0f, Health - amount);
    }
}
