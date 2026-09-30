namespace CursorRemote.Services;

/// <summary>
/// Desktop shell preferences for the Cursor Remote host window.
/// </summary>
public sealed class HostShellOptions
{
    private bool _closeToTray = true;

    public event EventHandler? Changed;

    public bool CloseToTray
    {
        get => _closeToTray;
        set
        {
            if (_closeToTray == value)
                return;
            _closeToTray = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
