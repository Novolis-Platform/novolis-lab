using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CursorRemote.Services;
using CursorRemote.Ui;

namespace CursorRemote.Desktop.Host;

/// <summary>
/// Minimize/close-to-tray behavior for the Windows host window.
/// </summary>
public sealed class HostTrayController : IHostDesktopChrome, IDisposable
{
    private readonly HostShellOptions _options;
    private readonly HostActivityLog _log;
    private Window? _window;
    private TrayIcon? _tray;
    private bool _forceExit;
    private bool _attached;

    public HostTrayController(HostShellOptions options, HostActivityLog log)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public void AttachMainWindow(Window window) => Attach(window);

    public void Attach(Window window)
    {
        if (_attached)
            return;

        _window = window ?? throw new ArgumentNullException(nameof(window));
        _attached = true;

        _window.Closing += OnClosing;
        _window.PropertyChanged += OnWindowPropertyChanged;
        EnsureTray();
        _log.Info("Tray ready — close/minimize can hide to the notification area.");
    }

    public void ShowWindow()
    {
        if (_window is null)
            return;

        Dispatcher.UIThread.Post(() =>
        {
            _window.Show();
            _window.WindowState = WindowState.Normal;
            _window.Activate();
        });
    }

    public void ExitApplication()
    {
        _forceExit = true;
        if (_window is null)
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.Shutdown();
            return;
        }

        Dispatcher.UIThread.Post(() => _window.Close());
    }

    public void Dispose()
    {
        if (_window is not null)
        {
            _window.Closing -= OnClosing;
            _window.PropertyChanged -= OnWindowPropertyChanged;
        }

        if (_tray is not null)
        {
            _tray.IsVisible = false;
            _tray.Dispose();
            _tray = null;
        }

        if (Avalonia.Application.Current is { } app)
            TrayIcon.SetIcons(app, null);
    }

    private void OnClosing(object? sender, WindowClosingEventArgs args)
    {
        if (_forceExit || !_options.CloseToTray)
            return;

        args.Cancel = true;
        HideToTray("Closed to tray.");
    }

    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs args)
    {
        if (args.Property != Window.WindowStateProperty)
            return;
        if (!_options.CloseToTray)
            return;
        if (_window?.WindowState != WindowState.Minimized)
            return;

        HideToTray("Minimized to tray.");
    }

    private void HideToTray(string reason)
    {
        if (_window is null)
            return;

        EnsureTray();
        _window.Hide();
        _log.Info(reason);
    }

    private void EnsureTray()
    {
        if (_tray is not null || Avalonia.Application.Current is null)
            return;

        WindowIcon? icon = _window?.Icon;
        if (icon is null)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "icon.png");
            if (File.Exists(path))
            {
                using var stream = File.OpenRead(path);
                icon = new WindowIcon(stream);
            }
        }

        var menu = new NativeMenu();
        var open = new NativeMenuItem($"Open {ProductBrand.Name}");
        open.Click += (_, _) => ShowWindow();
        var exit = new NativeMenuItem("Exit");
        exit.Click += (_, _) => ExitApplication();
        menu.Add(open);
        menu.Add(new NativeMenuItemSeparator());
        menu.Add(exit);

        _tray = new TrayIcon
        {
            Icon = icon,
            ToolTipText = ProductBrand.TrayTooltip,
            IsVisible = true,
            Menu = menu,
        };
        _tray.Clicked += (_, _) => ShowWindow();

        TrayIcon.SetIcons(Avalonia.Application.Current, [_tray]);
    }
}
