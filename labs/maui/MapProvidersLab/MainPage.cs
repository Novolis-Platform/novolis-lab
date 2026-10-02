using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using Novolis.IO.Maps;
using Novolis.Maui.GraphicalProfile;
using Novolis.Maui.Map;
using Novolis.Math.Geometry;

namespace Novolis.Lab.MapProviders;

/// <summary>
/// Feature gallery for the shared MAUI map control.
/// Each tab uses the same interaction, overlay, and measurement surface with
/// a different raster source, including a non-map procedural source.
/// </summary>
public sealed class MainPage : TabbedPage, IDisposable
{
    static readonly GeoCoordinate DefaultCenter = new(58.14623, 7.99517);

    readonly HttpClient _httpClient;
    readonly List<IDisposable> _sources = [];
    readonly string _cacheDirectory;
    bool _disposed;

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

        foreach (var definition in CreateSourceDefinitions())
            Children.Add(CreateSourcePage(definition));
    }

    IEnumerable<MapSourceDefinition> CreateSourceDefinitions()
    {
        foreach (var template in MapPresets.All)
        {
            var capturedTemplate = template;
            yield return new MapSourceDefinition(
                capturedTemplate.Name,
                $"Network XYZ raster · {capturedTemplate.Attribution}",
                () => new XyzMapSource(
                    _httpClient,
                    capturedTemplate,
                    _cacheDirectory,
                    "Novolis.MapProvidersLab/1.0"));
        }

        yield return new MapSourceDefinition(
            "procedural-starfield",
            "Generated raster scene · no network · deterministic tiles",
            () => new ProceduralMapRasterSource(256));
    }

    ContentPage CreateSourcePage(MapSourceDefinition definition)
    {
        var mapHost = new Grid();
        var loadingMessage = new Label
        {
            Text = "Open this tab to load the source",
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
        IMapRasterSource? source = null;
        var markers = CreateSampleMarkers().ToList();
        var tracks = CreateSampleTracks().ToList();
        var polygons = CreateSamplePolygons().ToList();
        var circles = CreateSampleCircles().ToList();
        var drawingNumber = 0;
        Button? finish = null;
        Button? cancel = null;
        Label? selectionMessage = null;
        Label? performanceMessage = null;

        var providerName = new Label
        {
            Text = definition.Name,
            FontSize = 17,
            FontAttributes = FontAttributes.Bold,
        };
        providerName.SetDynamicResource(
            Label.TextColorProperty,
            GraphicalProfile.TextResourceKey);

        var providerDetails = new Label
        {
            Text = definition.Description,
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
            UpdateCounters();
        };
        var zoomIn = ChromeButton("+");
        zoomIn.IsEnabled = false;
        zoomIn.Clicked += (_, _) =>
        {
            if (map is not null)
                SetZoom(map, map.Viewport.Zoom + 1);
            UpdateCounters();
        };
        var refresh = ChromeButton("Retry tiles");
        refresh.IsEnabled = false;
        refresh.Clicked += (_, _) =>
        {
            map?.RetryTiles();
            UpdateCounters();
        };
        refresh.SetDynamicResource(
            Button.BackgroundColorProperty,
            GraphicalProfile.ActionResourceKey);
        refresh.SetDynamicResource(
            Button.TextColorProperty,
            GraphicalProfile.OnActionResourceKey);

        var center = ChromeButton("Center sample");
        center.IsEnabled = false;
        center.Clicked += (_, _) =>
        {
            map?.SetViewport(DefaultCenter, 13);
            UpdateStatus("Centered the shared sample viewport.");
        };

        var reset = ChromeButton("Reset overlays");
        reset.IsEnabled = false;
        reset.Clicked += (_, _) =>
        {
            ResetOverlays();
            map?.CancelDrawing();
            UpdateStatus("Restored the sample POIs, track, polygon, and circle.");
        };

        var diagnostics = ChromeButton("Refresh counters");
        diagnostics.IsEnabled = false;
        diagnostics.Clicked += (_, _) => UpdateCounters();

        var cameraControls = ToolRow(
            zoomOut,
            zoomIn,
            center,
            refresh,
            reset,
            diagnostics);

        var copyCoordinate = ChromeButton("Copy coordinate");
        copyCoordinate.IsEnabled = false;
        copyCoordinate.Clicked += async (_, _) =>
        {
            if (map is null)
                return;

            var copied = await map.CopySelectedCoordinateAsync();
            UpdateStatus(copied
                ? "Copied the selected coordinate. Ctrl+Shift+C copies JSON."
                : "Select a point or POI before copying its coordinate.");
        };

        var copyJson = ChromeButton("Copy JSON");
        copyJson.IsEnabled = false;
        copyJson.Clicked += async (_, _) =>
        {
            if (map is null)
                return;

            var copied = await map.CopySelectedJsonAsync();
            UpdateStatus(copied
                ? "Copied the selected coordinate and marker metadata as JSON."
                : "Select a point or POI before copying JSON.");
        };

        var interactionControls = ToolRow(copyCoordinate, copyJson);

        var point = ChromeButton("Draw point");
        point.IsEnabled = false;
        point.Clicked += (_, _) => BeginDrawing(GeoDrawingKind.Point);

        var distance = ChromeButton("Measure distance");
        distance.IsEnabled = false;
        distance.Clicked += (_, _) => BeginDrawing(GeoDrawingKind.Polyline);

        var area = ChromeButton("Measure area");
        area.IsEnabled = false;
        area.Clicked += (_, _) => BeginDrawing(GeoDrawingKind.Polygon);

        var circle = ChromeButton("Draw circle");
        circle.IsEnabled = false;
        circle.Clicked += (_, _) => BeginDrawing(GeoDrawingKind.Circle);

        finish = ChromeButton("Finish drawing");
        finish.IsEnabled = false;
        finish.Clicked += (_, _) =>
        {
            if (map?.CompleteDrawing() is null)
                UpdateStatus("Add enough points before finishing this drawing.");
        };

        cancel = ChromeButton("Cancel drawing");
        cancel.IsEnabled = false;
        cancel.Clicked += (_, _) =>
        {
            map?.CancelDrawing();
            UpdateStatus("Drawing canceled.");
        };

        var drawingControls = ToolRow(
            point,
            distance,
            area,
            circle,
            finish,
            cancel);

        selectionMessage = new Label
        {
            Text = "Tap a POI or any map position. Drawings become overlays when completed.",
            FontSize = 12,
            LineBreakMode = LineBreakMode.WordWrap,
        };
        selectionMessage.SetDynamicResource(
            Label.TextColorProperty,
            GraphicalProfile.TextResourceKey);

        performanceMessage = new Label
        {
            Text = "Tile requests: waiting for the source",
            FontSize = 11,
            LineBreakMode = LineBreakMode.TailTruncation,
        };
        performanceMessage.SetDynamicResource(
            Label.TextColorProperty,
            GraphicalProfile.MutedResourceKey);

        var shortcutMessage = new Label
        {
            Text = "Windows shortcuts: Ctrl+C coordinate · Ctrl+Shift+C JSON · arrows pan · +/- zoom · Enter finish · Esc cancel",
            FontSize = 11,
            LineBreakMode = LineBreakMode.WordWrap,
        };
        shortcutMessage.SetDynamicResource(
            Label.TextColorProperty,
            GraphicalProfile.MutedResourceKey);

        var controls = new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                cameraControls,
                interactionControls,
                drawingControls,
                selectionMessage,
                performanceMessage,
                shortcutMessage,
            },
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
            Title = definition.Name,
            Content = layout,
            Padding = 0,
        };
        page.SetDynamicResource(
            VisualElement.BackgroundColorProperty,
            GraphicalProfile.BackgroundResourceKey);

        var initialized = false;
        var counterTimer = new Timer(
            _ => MainThread.BeginInvokeOnMainThread(UpdateCounters),
            null,
            Timeout.Infinite,
            Timeout.Infinite);
        _sources.Add(counterTimer);

        void SetControlsEnabled(bool enabled)
        {
            foreach (var control in new View[]
            {
                zoomOut,
                zoomIn,
                center,
                refresh,
                reset,
                diagnostics,
                copyCoordinate,
                copyJson,
                point,
                distance,
                area,
                circle,
                finish!,
                cancel!,
            })
            {
                control.IsEnabled = enabled;
            }
        }

        void UpdateCounters()
        {
            if (_disposed || map is null)
                return;

            var counters = map.PerformanceCounters;
            performanceMessage!.Text =
                $"Requests {counters.TileRequestsStarted}/{counters.TileRequestsCompleted} · " +
                $"decoded {counters.CurrentDecodedTiles}/{counters.PeakDecodedTiles} · " +
                $"redraws {counters.RedrawRequests} · canceled {counters.RefreshCancellations}";
            finish!.IsEnabled = map.IsDrawing;
            cancel!.IsEnabled = map.IsDrawing;
        }

        void UpdateStatus(string message)
        {
            selectionMessage!.Text = message;
            UpdateCounters();
        }

        void BeginDrawing(GeoDrawingKind kind)
        {
            if (map is null || !map.BeginDrawing(kind))
                return;

            finish!.IsEnabled = true;
            cancel!.IsEnabled = true;
            UpdateStatus(
                kind == GeoDrawingKind.Polyline
                    ? "Distance mode: click points, then Finish drawing."
                    : kind == GeoDrawingKind.Polygon
                        ? "Area mode: click at least three points, then Finish drawing."
                        : kind == GeoDrawingKind.Circle
                            ? "Circle mode: click a center and an edge, then Finish drawing."
                            : "Point mode: click one location.");
        }

        void ResetOverlays()
        {
            markers = CreateSampleMarkers().ToList();
            tracks = CreateSampleTracks().ToList();
            polygons = CreateSamplePolygons().ToList();
            circles = CreateSampleCircles().ToList();
            ApplyOverlays();
        }

        void ApplyOverlays()
        {
            if (map is null)
                return;

            map.Markers = markers.ToArray();
            map.Tracks = tracks.ToArray();
            map.Polygons = polygons.ToArray();
            map.Circles = circles.ToArray();
        }

        void AddDrawingOverlay(GeoDrawing drawing)
        {
            var id = $"drawing-{++drawingNumber}";
            switch (drawing.Kind)
            {
                case GeoDrawingKind.Point:
                    markers.Add(new MapMarker(
                        id,
                        drawing.Points[0],
                        "Drawn point",
                        8,
                        Colors.Gold,
                        new Dictionary<string, string>
                        {
                            ["source"] = definition.Name,
                            ["kind"] = "point",
                        },
                        Tag: drawing));
                    break;
                case GeoDrawingKind.Polyline:
                    tracks.Add(new MapTrackOverlay(
                        id,
                        drawing.Points,
                        $"Distance · {GeoMeasurementText.FormatDistance(drawing.LengthMeters)}",
                        Colors.Gold,
                        Colors.OrangeRed));
                    break;
                case GeoDrawingKind.Polygon:
                    polygons.Add(new MapPolygonOverlay(
                        id,
                        drawing.Points,
                        $"Area · {GeoMeasurementText.FormatArea(drawing.AreaSquareMeters)}",
                        Colors.Gold,
                        Colors.Gold.WithAlpha(0.2f)));
                    break;
                case GeoDrawingKind.Circle:
                    circles.Add(new MapCircleOverlay(
                        id,
                        new GeoCircle(
                            drawing.Points[0],
                            drawing.RadiusMeters.GetValueOrDefault()),
                        $"Radius · {GeoMeasurementText.FormatDistance(drawing.RadiusMeters.GetValueOrDefault())}",
                        Colors.Gold));
                    break;
            }

            ApplyOverlays();
            UpdateStatus(DescribeDrawing(drawing));
        }

        SetControlsEnabled(false);
        page.Appearing += (_, _) =>
        {
            if (!initialized)
            {
                initialized = true;
                source = definition.Create();
                if (source is IDisposable disposable)
                    _sources.Add(disposable);

                map = new MapView
                {
                    TileSource = source,
                    Viewport = new MapViewport(DefaultCenter, 13),
                    InteractionOptions = new MapInteractionOptions
                    {
                        EnableClipboardShortcuts = true,
                        EnableKeyboardNavigation = true,
                        EnableDrawing = true,
                        ShowMeasurementResults = true,
                    },
                    Markers = markers.ToArray(),
                    Tracks = tracks.ToArray(),
                    Polygons = polygons.ToArray(),
                    Circles = circles.ToArray(),
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                };
                map.PointSelected += coordinate =>
                    UpdateStatus(
                        $"Selected {GeoCoordinateText.Format(coordinate)} · Ctrl+C copies coordinates.");
                map.MarkerSelected += marker =>
                    UpdateStatus(
                        $"POI {marker.Label ?? marker.Id} · {GeoCoordinateText.Format(marker.Position)} · " +
                        $"{marker.Metadata?.Count ?? 0} metadata fields · Ctrl+Shift+C copies JSON.");
                map.DrawingCompleted += AddDrawingOverlay;
#if WINDOWS
                map.HandlerChanged += (_, _) =>
                    WindowsMapInput.Attach(map, UpdateStatus);
#endif
                mapHost.Children.Clear();
                mapHost.Add(map);
                SetControlsEnabled(true);
                UpdateStatus(
                    definition.Name == "procedural-starfield"
                        ? "Procedural starfield active: these tiles are generated locally, not downloaded."
                        : "Source active. Tap a POI, copy its metadata, or start a drawing.");
            }

            if (map is not null)
            {
                map.TileLoadingEnabled = true;
                counterTimer.Change(TimeSpan.Zero, TimeSpan.FromMilliseconds(500));
                UpdateCounters();
            }
        };
        page.Disappearing += (_, _) =>
        {
            counterTimer.Change(Timeout.Infinite, Timeout.Infinite);
            if (map is not null)
            {
                map.TileLoadingEnabled = false;
                map.ClearTiles();
            }
        };

        return page;
    }

    static IReadOnlyList<MapMarker> CreateSampleMarkers() =>
    [
        new(
            "harbor",
            DefaultCenter,
            "Demo POI",
            9,
            Colors.Orange,
            new Dictionary<string, string>
            {
                ["category"] = "harbor",
                ["source"] = "MapProvidersLab",
            },
            Tag: "sample-harbor"),
        new(
            "north",
            new GeoCoordinate(58.153, 8.006),
            "North POI",
            7,
            Colors.CornflowerBlue,
            new Dictionary<string, string>
            {
                ["category"] = "sample",
                ["role"] = "north",
            },
            Tag: "sample-north"),
        new(
            "south",
            new GeoCoordinate(58.139, 7.985),
            "South POI",
            7,
            Colors.MediumSeaGreen,
            new Dictionary<string, string>
            {
                ["category"] = "sample",
                ["role"] = "south",
            },
            Tag: "sample-south"),
    ];

    static IReadOnlyList<MapTrackOverlay> CreateSampleTracks() =>
    [
        new(
            "sample-track",
            [
                new GeoCoordinate(58.138, 7.979),
                new GeoCoordinate(58.144, 7.990),
                DefaultCenter,
                new GeoCoordinate(58.153, 8.006),
            ],
            "Sample route",
            Colors.CornflowerBlue,
            Colors.Orange),
    ];

    static IReadOnlyList<MapPolygonOverlay> CreateSamplePolygons() =>
    [
        new(
            "sample-area",
            [
                new GeoCoordinate(58.143, 7.981),
                new GeoCoordinate(58.151, 7.981),
                new GeoCoordinate(58.155, 7.999),
                new GeoCoordinate(58.145, 8.011),
            ],
            "Sample area",
            Colors.MediumSeaGreen,
            Colors.MediumSeaGreen.WithAlpha(0.15f)),
    ];

    static IReadOnlyList<MapCircleOverlay> CreateSampleCircles() =>
    [
        new(
            "sample-radius",
            new GeoCircle(DefaultCenter, 700),
            "700 m radius",
            Colors.DarkOrange),
    ];

    static string DescribeDrawing(GeoDrawing drawing) =>
        drawing.Kind switch
        {
            GeoDrawingKind.Point =>
                $"Point overlay added at {GeoCoordinateText.Format(drawing.Points[0])}.",
            GeoDrawingKind.Polyline =>
                $"Distance overlay added: {GeoMeasurementText.FormatDistance(drawing.LengthMeters)}.",
            GeoDrawingKind.Polygon =>
                $"Area overlay added: {GeoMeasurementText.FormatArea(drawing.AreaSquareMeters)}.",
            GeoDrawingKind.Circle =>
                $"Circle overlay added: radius {GeoMeasurementText.FormatDistance(drawing.RadiusMeters.GetValueOrDefault())}.",
            _ => "Drawing overlay added.",
        };

    static ScrollView ToolRow(params View[] views)
    {
        var row = new HorizontalStackLayout
        {
            Spacing = 6,
        };
        foreach (var view in views)
            row.Children.Add(view);

        return new ScrollView
        {
            Orientation = ScrollOrientation.Horizontal,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Never,
            Content = row,
        };
    }

    /// <summary>Stops provider activity when the host window is destroyed.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        foreach (var source in _sources)
            source.Dispose();
        _sources.Clear();
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

    sealed record MapSourceDefinition(
        string Name,
        string Description,
        Func<IMapRasterSource> Create);
}
