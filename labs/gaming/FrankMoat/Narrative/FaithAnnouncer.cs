using FrankMoat.Weapons;

namespace FrankMoat.Narrative;

internal sealed class FaithAnnouncer
{
    private bool _rangeBriefed;
    private bool _rotaryNoted;
    private bool _waveTwo;
    private bool _waveThree;

    public FaithCue Cue { get; } = new();

    public void Reset()
    {
        _rangeBriefed = false;
        _rotaryNoted = false;
        _waveTwo = false;
        _waveThree = false;
        Cue.Say("Range occupancy: one. Undead occupancy: rising.", 4.2f);
    }

    public void Tick(float dt, int wave, WeaponId weapon, int undead)
    {
        Cue.Tick(dt);
        if (!_rangeBriefed)
        {
            _rangeBriefed = true;
            return;
        }

        if (!_rotaryNoted && weapon == WeaponId.Rotary)
        {
            _rotaryNoted = true;
            Cue.Say("HRRS rotary is authorized. HR still disputes the colloquial name.");
        }

        if (!_waveTwo && wave >= 2)
        {
            _waveTwo = true;
            Cue.Say("Containment on the north line has failed. Again.");
        }

        if (!_waveThree && wave >= 3)
        {
            _waveThree = true;
            Cue.Say("Directive 4761 remains in force. The express elevator stays locked.");
        }

        if (undead == 0 && wave >= 3 && Cue.Timer <= 0f)
        {
            Cue.Say("Range is quiet. You are still not going home.");
        }
    }
}
