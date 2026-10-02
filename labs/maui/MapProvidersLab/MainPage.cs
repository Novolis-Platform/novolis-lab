using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Novolis.IO.Maps;
using Novolis.Maui.GraphicalProfile;
using Novolis.Maui.Map;
using Novolis.Math.Geometry;

namespace Novolis.Lab.MapProviders;

/// <summary>
/// Small provider comparison surface for the shared MAUI map control.
/// Each tab uses the same viewport and raster adapter with a different preset.
/// </summary>
public sealed class MainPage : TabbedPage
{
    static readonly GeoCoordinate DefaultCenter = new(58.14623, 7.99517);

    readonly HttpClient _httpClient;
    readonly List<XyzMapSource> _sources = [];
    readonly string _cacheDirectory;

    /// <summary>Creates one tab for every keyless raster preset.</summary>
    public MainPage(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _cacheDirectory = Path.Combine(
            FileSystem.Current.CacheDirectory,
            "Novolis",
            "MapProvidersLab",
            "tiles");
        Directory.CreateDirectory(_cacheDirectory);

        Title = "Map Providers";
        SetDynamicResource(
            VisualElement.BackgroundColorProperty,
            GraphicalProfile.BackgroundResourceKey);

        foreach (var template in MapPresets.All)
            Children.Add(CreateProviderPage(template));
    }

    ContentPage CreateProviderPage(XyzMapTemplate template)
    {
        var mapHost = new Grid();
        var loadingMessage = new Label
        {
            Text = "Open this tab to load the map",
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            HorizontalTextAlignment = TextAlignment.Center,
            FontSize = 15,
        };
        loadingMessage.SetDynamicResource(
            Label.TextColorProperty,
            GraphicalProfile.MutedResourceKey);
        mapHost.Add(loadingMessage);

        MapView? map = null;

        var providerName = new Label
        {
            Text = template.Name,
            FontSize = 17,
            FontAttributes = FontAttributes.Bold,
        };
        providerName.SetDynamicResource(
            Label.TextColorProperty,
            GraphicalProfile.TextResourceKey);

        var providerDetails = new Label
        {
            Text = $"Shared XYZ source · {template.Attribution} · wheel zoom · drag to pan",
            FontSize = 12,
            LineBreakMode = LineBreakMode.TailTruncation,
        };
        providerDetails.SetDynamicResource(
            Label.TextColorProperty,
            GraphicalProfile.MutedResourceKey);

        var zoomOut = ChromeButton("−");
        zoomOut.IsEnabled = false;
        zoomOut.Clicked += (_, _) =>
        {
            if (map is not null)
                SetZoom(map, map.Viewport.Zoom - 1);
        };
        var zoomIn = ChromeButton("+");
        zoomIn.IsEnabled = false;
        zoomIn.Clicked += (_, _) =>
        {
            if (map is not null)
                SetZoom(map, map.Viewport.Zoom + 1);
        };
        var refresh = ChromeButton("Retry tiles");
        refresh.IsEnabled = false;
        refresh.Clicked += (_, _) => map?.RetryTiles();
        refresh.SetDynamicResource(
            Button.BackgroundColorProperty,
            GraphicalProfile.ActionResourceKey);
        refresh.SetDynamicResource(
            Button.TextColorProperty,
            GraphicalProfile.OnActionResourceKey);

        var controls = new HorizontalStackLayout
        {
            Spacing = 8,
            Children = { zoomOut, zoomIn, refresh },
        };

        var header = new VerticalStackLayout
        {
            Padding = new Thickness(16, 14, 16, 10),
            Spacing = 4,
            Children =
            {
                providerName,
                providerDetails,
                controls,
            },
        };
        header.SetDynamicResource(
            VisualElement.BackgroundColorProperty,
            GraphicalProfile.SurfaceResourceKey);

        var layout = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
            },
        };
        layout.Add(header, 0, 0);
        layout.Add(mapHost, 0, 1);

        var page = new ContentPage
        {
            Title = template.Name,
            Content = layout,
            Padding = 0,
        };
        page.SetDynamicResource(
            VisualElement.BackgroundColorProperty,
            GraphicalProfile.BackgroundResourceKey);

        var initialized = false;
        page.Appearing += (_, _) =>
        {
            if (!initialized)
            {
                initialized = true;
                var source = new XyzMapSource(
                    _httpClient,
                    template,
                    _cacheDirectory,
                    "Novolis.MapProvidersLab/1.0");
                _sources.Add(source);

                map = new MapView
                {
                    TileSource = source,
                    Viewport = new MapViewport(DefaultCenter, 6),
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                };
#if WINDOWS
                map.HandlerChanged += (_, _) => WindowsMapInput.Attach(map);
#endif
                mapHost.Children.Clear();
                mapHost.Add(map);
                zoomOut.IsEnabled = true;
                zoomIn.IsEnabled = true;
                refresh.IsEnabled = true;
            }

            if (map is not null)
                map.TileLoadingEnabled = true;
        };
        page.Disappearing += (_, _) =>
        {
            if (map is not null)
                map.TileLoadingEnabled = false;
        };

        return page;
    }

    static Button ChromeButton(string text)
    {
        var button = new Button
        {
            Text = text,
            FontSize = 13,
            Padding = new Thickness(12, 6),
            MinimumHeightRequest = 42,
        };
        button.SetDynamicResource(
            Button.BackgroundColorProperty,
            GraphicalProfile.RaisedResourceKey);
        button.SetDynamicResource(
            Button.TextColorProperty,
            GraphicalProfile.TextResourceKey);
        return button;
    }

    static void SetZoom(MapView map, double zoom) =>
        map.Viewport = new MapViewport(
            map.Viewport.Center,
            global::System.Math.Clamp(
                zoom,
                MapViewport.MinimumZoom,
                MapViewport.MaximumZoom));
}
