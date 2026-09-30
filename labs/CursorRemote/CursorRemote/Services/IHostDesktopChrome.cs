using Avalonia.Controls;

namespace CursorRemote.Services;

/// <summary>
/// Optional Desktop-only chrome (tray). Android does not register an implementation.
/// </summary>
public interface IHostDesktopChrome
{
    void AttachMainWindow(Window window);
}
