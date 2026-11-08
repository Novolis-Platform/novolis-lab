using System.Globalization;

namespace ArtillerySimulator;

/// <summary>
/// Educational K9 DOG autoloader + powered turret drill on the battle clock.
/// Autoloader rams while the turret lays; a repeat at the same data is mostly the load cycle.
/// </summary>
internal sealed class CrewDrill
{
    public const float RamSeconds = 3.2f;
    public const float ChargeSeconds = 1.6f;
    public const float ChargeSwapSeconds = 4.5f;
    public const float CloseSeconds = 0.6f;
    /// <summary>
    /// Powered-turret slew (battle clock). ~4.5°/s traverse / ~2.5°/s elevate —
    /// a 30° azimuth change takes ~7 s; Shift is for fine corrections.
    /// </summary>
    public const float ElevateMilsPerSecond = 45f;
    public const float TraverseMilsPerSecond = 80f;

    private const float OnLayMils = 1.25f;

    private Stage _stage = Stage.Ready;
    private float _remaining;
    private bool _swap;
    private int _seatedCharge;

    public float OrderedElevation { get; private set; }

    public float OrderedAzimuth { get; private set; }

    public int OrderedCharge { get; private set; }

    public bool Loading => _stage != Stage.Ready;

    /// <summary>Autoloader finished — tube may still be slewing to the ordered lay.</summary>
    public bool BreechReady => _stage == Stage.Ready;

    public void SeatOpening(GunModel gun)
    {
        OrderedElevation = gun.ElevationDegrees;
        OrderedAzimuth = gun.AzimuthDegrees;
        OrderedCharge = gun.ChargeIndex;
        _seatedCharge = gun.ChargeIndex;
        _stage = Stage.Ready;
        _remaining = 0f;
        _swap = false;
    }

    public void OrderAim(GunModel gun, float elevation, float azimuth)
    {
        OrderedElevation = Math.Clamp(elevation, gun.Spec.MinElevationDegrees, gun.Spec.MaxElevationDegrees);
        var delta = azimuth - gun.CenterAzimuthDegrees;
        while (delta > 180f)
            delta -= 360f;
        while (delta < -180f)
            delta += 360f;
        delta = Math.Clamp(delta, -gun.Spec.MaxTraverseDegrees, gun.Spec.MaxTraverseDegrees);
        OrderedAzimuth = gun.CenterAzimuthDegrees + delta;
    }

    public void NudgeOrder(GunModel gun, float elevationDelta, float azimuthDelta) =>
        OrderAim(gun, OrderedElevation + elevationDelta, OrderedAzimuth + azimuthDelta);

    public void OrderCharge(GunModel gun, int index)
    {
        index = Math.Clamp(index, 0, gun.Spec.Charges.Count - 1);
        if (index == OrderedCharge)
            return;

        OrderedCharge = index;
        gun.SetCharge(index);
        _swap = index != _seatedCharge;
        switch (_stage)
        {
            case Stage.Ready:
                _stage = Stage.Charge;
                _remaining = ChargeSwapSeconds;
                break;
            case Stage.Charge:
                _remaining = _swap ? ChargeSwapSeconds : ChargeSeconds;
                break;
            case Stage.Close when _swap:
                _stage = Stage.Charge;
                _remaining = ChargeSwapSeconds;
                break;
        }
    }

    public void BeginReload()
    {
        _stage = Stage.Ram;
        _remaining = RamSeconds;
        _swap = OrderedCharge != _seatedCharge;
    }

    public void Advance(GunModel gun, float seconds)
    {
        if (seconds > 0f)
            Slew(gun, seconds);

        if (_stage == Stage.Ready || seconds <= 0f)
            return;

        _remaining -= seconds;
        while (_remaining <= 0f && _stage != Stage.Ready)
        {
            var overflow = -_remaining;
            switch (_stage)
            {
                case Stage.Ram:
                    _stage = Stage.Charge;
                    _remaining = (_swap ? ChargeSwapSeconds : ChargeSeconds) - overflow;
                    break;
                case Stage.Charge:
                    _stage = Stage.Close;
                    _remaining = CloseSeconds - overflow;
                    break;
                default:
                    gun.SetCharge(OrderedCharge);
                    _seatedCharge = OrderedCharge;
                    _swap = false;
                    _stage = Stage.Ready;
                    _remaining = 0f;
                    break;
            }
        }
    }

    public bool Ready(GunModel gun) => BreechReady && LaySeconds(gun) <= 0.05f;

    /// <summary>Snap the tube onto the ordered lay (used when firing on a reloaded breech).</summary>
    public void SnapTube(GunModel gun)
    {
        gun.SetElevation(OrderedElevation);
        gun.NudgeAzimuth(Wrap(OrderedAzimuth - gun.AzimuthDegrees));
    }

    public float BusySeconds(GunModel gun) => MathF.Max(LoaderSeconds(), LaySeconds(gun));

    public string Token(GunModel gun)
    {
        if (_stage != Stage.Ready && LoaderSeconds() >= LaySeconds(gun))
        {
            return _stage switch
            {
                Stage.Ram => "ram",
                Stage.Charge => "charge",
                _ => "close",
            };
        }

        return LaySeconds(gun) > 0.05f ? "lay" : "ready";
    }

    public string Phrase(GunModel gun)
    {
        var lay = LaySeconds(gun);
        var load = LoaderSeconds();
        if (load <= 0.05f && lay <= 0.05f)
            return "Ready to fire.";

        if (load >= lay && _stage != Stage.Ready)
        {
            var word = _stage switch
            {
                Stage.Ram => "Ramming",
                Stage.Charge => "Seating charge",
                _ => "Closing the breech",
            };
            return string.Create(CultureInfo.InvariantCulture, $"{word}, {Ceil(load)} s.");
        }

        return string.Create(CultureInfo.InvariantCulture, $"Laying, {Ceil(lay)} s.");
    }

    private float LoaderSeconds() => _stage switch
    {
        Stage.Ram => _remaining + (_swap ? ChargeSwapSeconds : ChargeSeconds) + CloseSeconds,
        Stage.Charge => _remaining + CloseSeconds,
        Stage.Close => _remaining,
        _ => 0f,
    };

    private float LaySeconds(GunModel gun)
    {
        var elevation = MathF.Abs(OrderedElevation - gun.ElevationDegrees) * NatoMils.MilsPerDegree;
        var azimuth = MathF.Abs(Wrap(OrderedAzimuth - gun.AzimuthDegrees)) * NatoMils.MilsPerDegree;
        if (elevation <= OnLayMils && azimuth <= OnLayMils)
            return 0f;

        return MathF.Max(elevation / ElevateMilsPerSecond, azimuth / TraverseMilsPerSecond);
    }

    private void Slew(GunModel gun, float seconds)
    {
        var elevate = ElevateMilsPerSecond / NatoMils.MilsPerDegree * seconds;
        var traverse = TraverseMilsPerSecond / NatoMils.MilsPerDegree * seconds;
        gun.NudgeElevation(Math.Clamp(OrderedElevation - gun.ElevationDegrees, -elevate, elevate));
        gun.NudgeAzimuth(Math.Clamp(Wrap(OrderedAzimuth - gun.AzimuthDegrees), -traverse, traverse));
    }

    private static float Wrap(float degrees)
    {
        while (degrees > 180f)
            degrees -= 360f;
        while (degrees < -180f)
            degrees += 360f;
        return degrees;
    }

    private static float Ceil(float seconds) => MathF.Max(1f, MathF.Ceiling(seconds));

    private enum Stage
    {
        Ready,
        Ram,
        Charge,
        Close,
    }
}
