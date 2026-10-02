using Novolis.Maui.GraphicalProfile;

namespace Novolis.Lab.MapProviders;

/// <summary>Application root for the map provider comparison lab.</summary>
public sealed class App : Application
{
    readonly MainPage _mainPage;

    /// <summary>Creates the application and installs the shared visual profile.</summary>
    public App(MainPage mainPage, GraphicalProfileInstaller profileInstaller)
    {
        _mainPage = mainPage ?? throw new ArgumentNullException(nameof(mainPage));
        if (profileInstaller is null)
            throw new ArgumentNullException(nameof(profileInstaller));

        profileInstaller.Install(this);
    }

    /// <inheritdoc />
    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(_mainPage);
        window.Destroying += (_, _) => _mainPage.Dispose();
        return window;
    }
}
