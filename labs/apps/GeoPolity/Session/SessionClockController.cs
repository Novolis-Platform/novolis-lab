using Novolis.Geopolitics.Core;

namespace GeoPolity.Session;

/// <summary>HardPause session clock: paused by default; speed presets 1–5.</summary>
public sealed class SessionClockController
{
    public bool Running { get; private set; }

    /// <summary>When true, pulses are blocked (modal / HardPause overlay).</summary>
    public bool ModalBlocks { get; set; }

    public int DaysPerPulse { get; private set; } = 1;

    public int PulseMs { get; private set; } = 280;

    public string SpeedLabel { get; private set; } = "day";

    public string? StatusNote { get; set; } = "paused — Space to run, 1–5 for speed";

    public bool ShouldPulse => Running && !ModalBlocks;

    public void Pause()
    {
        Running = false;
        StatusNote = "paused";
    }

    public void Resume()
    {
        Running = true;
        StatusNote = "running";
    }

    public void ToggleRun()
    {
        if (Running)
        {
            Pause();
        }
        else
        {
            Resume();
        }
    }

    /// <summary>Presets: 1=day, 2=week, 3=month, 4=year, 5=5y. Auto-resumes.</summary>
    public void SetSpeedPreset(int preset)
    {
        switch (preset)
        {
            case 1:
                SetSpeed(1, 320, "day");
                break;
            case 2:
                SetSpeed(7, 200, "week");
                break;
            case 3:
                SetSpeed(WorldState.DaysPerMonth, 140, "month");
                break;
            case 4:
                SetSpeed(WorldState.DaysPerYear, 90, "year");
                break;
            case 5:
                SetSpeed(WorldState.DaysPerYear * 5, 60, "5 years");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(preset), preset, "Speed preset must be 1–5.");
        }
    }

    public void SetSpeed(int days, int ms, string label)
    {
        DaysPerPulse = Math.Max(1, days);
        PulseMs = Math.Clamp(ms, 16, 2000);
        SpeedLabel = label;
        Running = true;
        StatusNote = $"speed {label}";
    }
}
