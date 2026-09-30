using Avalonia.Controls;

namespace Novolis.Lab.Branding;

internal static class AppBrand
{
    public static void ApplyWindowIcon(Window? window)
    {
        if (window is null)
        {
            return;
        }

        var path = Path.Combine(AppContext.BaseDirectory, "icon.png");
        if (!File.Exists(path))
        {
            return;
        }

        using var stream = File.OpenRead(path);
        window.Icon = new WindowIcon(stream);
    }
}
