using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using CapitalistSimulator.Cli;
using CapitalistSimulator.Persistence;
using CapitalistSimulator.Sim;
using Novolis.Avalonia.Briefing;
using Novolis.Avalonia.Studio;

namespace CapitalistSimulator.Ui;

internal static class ControlTapExtensions
{
    public static T Tap<T>(this T control, Action<T> configure)
    {
        configure(control);
        return control;
    }
}
