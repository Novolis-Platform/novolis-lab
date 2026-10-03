using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Layouts;
using Microsoft.Maui.Storage;
using Novolis.Astro.Abstractions;
using Novolis.Astro.Catalog;
using Novolis.Astro.Catalog.Data;
using Novolis.IO.Ndjson;
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
    static readonly JsonSerializerOptions CatalogSnapshotJson = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

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
        Children.Add(CreateCelestialPage(CatalogPackId.NearSol100));
        Children.Add(CreateCelestialPage(CatalogPackId.HygLocal1901));
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

    ContentPage CreateCelestialPage(CatalogPackId pack)
    {
        var provenance = CatalogPacks.GetProvenance(pack);
        var title = pack == CatalogPackId.NearSol100 ? "Near-Sol 100" : "HYG Local 1901";
        var scene = new ProjectedSceneView
        {
            ShowLabels = pack == CatalogPackId.NearSol100,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
        };
#if WINDOWS
        scene.HandlerChanged += (_, _) => WindowsMapInput.Attach(scene);
#endif
        var sourceTitle = new Label
        {
            Text = title,
            FontSize = 17,
            FontAttributes = FontAttributes.Bold,
        };
        sourceTitle.SetDynamicResource(
            Label.TextColorProperty,
            GraphicalProfile.TextResourceKey);

        var provenanceLabel = new Label
        {
            Text =
                $"{provenance.SourceDescription} · {provenance.CartesianFrame} · " +
                $"Sol origin normalized: {provenance.IsSolNormalized}",
            FontSize = 12,
            LineBreakMode = LineBreakMode.WordWrap,
        };
        provenanceLabel.SetDynamicResource(
            Label.TextColorProperty,
            GraphicalProfile.MutedResourceKey);

        var projectionPicker = new Picker
        {
            Title = "Projection",
            ItemsSource = Enum.GetValues<CelestialProjectionKind>().ToArray(),
            SelectedItem = CelestialProjectionKind.SolCenteredCartesian,
        };
        projectionPicker.SetDynamicResource(
            Picker.TextColorProperty,
            GraphicalProfile.TextResourceKey);

        var fit = ChromeButton("Fit stars");
        var labels = ChromeButton(scene.ShowLabels ? "Hide labels" : "Show labels");
        var selectedDetails = new Label
        {
            Text = "Load the generated catalog snapshot to inspect a real star.",
            FontSize = 12,
            LineBreakMode = LineBreakMode.WordWrap,
        };
        selectedDetails.SetDynamicResource(
            Label.TextColorProperty,
            GraphicalProfile.TextResourceKey);

        var loadStatus = new Label
        {
            Text = "NDJSON snapshot waiting for this tab.",
            FontSize = 11,
            LineBreakMode = LineBreakMode.WordWrap,
        };
        loadStatus.SetDynamicResource(
            Label.TextColorProperty,
            GraphicalProfile.MutedResourceKey);

        var header = new VerticalStackLayout
        {
            Padding = new Thickness(16, 14, 16, 10),
            Spacing = 6,
            Children =
            {
                sourceTitle,
                provenanceLabel,
                new FlexLayout
                {
                    Direction = FlexDirection.Row,
                    Wrap = FlexWrap.Wrap,
                    AlignItems = FlexAlignItems.Center,
                    Children = { projectionPicker, fit, labels },
                },
                selectedDetails,
                loadStatus,
            },
        };
        FlexLayout.SetGrow(projectionPicker, 1);
        projectionPicker.Margin = new Thickness(0, 0, 8, 4);
        fit.Margin = new Thickness(0, 0, 8, 4);
        labels.Margin = new Thickness(0, 0, 8, 4);
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
        layout.Add(scene, 0, 1);

        var page = new ContentPage
        {
            Title = title,
            Content = layout,
            Padding = 0,
        };
        page.SetDynamicResource(
            VisualElement.BackgroundColorProperty,
            GraphicalProfile.BackgroundResourceKey);

        IReadOnlyList<CelestialCatalogEntry>? entries = null;
        var needsFit = false;
        var loading = false;

        CelestialProjectionKind GetSelectedProjection() =>
            projectionPicker.SelectedItem is CelestialProjectionKind selected
                ? selected
                : CelestialProjectionKind.SolCenteredCartesian;

        void ApplyProjection()
        {
            if (entries is null)
                return;

            var projection = GetSelectedProjection();
            scene.Points = entries
                .Select(entry =>
                {
                    var projected = CelestialProjections.ProjectStar(entry, projection);
                    return new ProjectedScenePoint(
                        projected.Id,
                        projected.Coordinate.X,
                        projected.Coordinate.Y,
                        projected.Label,
                        projected.RadiusPixels,
                        projected.ApparentMagnitude,
                        tag: projected);
                })
                .ToArray();
            needsFit = true;
            FitSceneIfReady();
            loadStatus.Text =
                $"Loaded {entries.Count:N0} NDJSON records · {projection} · " +
                $"{provenance.CartesianFrame} source frame.";
        }

        void FitSceneIfReady()
        {
            if (!needsFit || scene.Width <= 0 || scene.Height <= 0)
                return;

            needsFit = false;
            scene.FitToPoints();
        }

        projectionPicker.SelectedIndexChanged += (_, _) => ApplyProjection();
        fit.Clicked += (_, _) =>
        {
            needsFit = true;
            FitSceneIfReady();
        };
        labels.Clicked += (_, _) =>
        {
            scene.ShowLabels = !scene.ShowLabels;
            labels.Text = scene.ShowLabels ? "Hide labels" : "Show labels";
        };
        scene.SizeChanged += (_, _) => FitSceneIfReady();
        scene.PointSelected += point =>
        {
            if (point.Tag is not CelestialProjectedStar star)
                return;

            var entry = star.Entry;
            selectedDetails.Text =
                $"{entry.Name} · source {entry.SourceId} · RA {entry.Equatorial.RightAscensionDegrees:0.###}° · " +
                $"Dec {entry.Equatorial.DeclinationDegrees:0.###}° · " +
                $"distance {entry.Equatorial.DistanceLightYears:0.###} ly · " +
                $"magnitude {(entry.ApparentMagnitude?.ToString("0.##") ?? "unknown")} · " +
                $"Cartesian ({entry.SolRelativeCartesian.X:0.###}, " +
                $"{entry.SolRelativeCartesian.Y:0.###}, {entry.SolRelativeCartesian.Z:0.###}) ly";
        };

        page.Appearing += async (_, _) =>
        {
            if (entries is not null || loading)
                return;

            loading = true;
            loadStatus.Text = "Reading the packageable NDJSON snapshot…";
            try
            {
                entries = await LoadCatalogSnapshotAsync(pack, _cacheDirectory);
                ApplyProjection();
            }
            catch (Exception exception)
            {
                loadStatus.Text = $"Could not load the generated catalog: {exception.Message}";
            }
            finally
            {
                loading = false;
            }
        };

        return page;
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
        var drawingOrigins = new Dictionary<MapOverlayKey, GeoDrawing>();
        var shapeEntries = new ObservableCollection<ShapeCatalogEntry>();
        var drawingNumber = 0;
        var synchronizingShapeSelection = false;
        Button? finish = null;
        Button? cancel = null;
        Button? erase = null;
        Button? select = null;
        Button? clear = null;
        CollectionView? shapeCatalog = null;
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

        var rectangle = ChromeButton("Draw rectangle");
        rectangle.IsEnabled = false;
        rectangle.Clicked += (_, _) => BeginDrawing(GeoDrawingKind.Rectangle);

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
            rectangle,
            finish,
            cancel);

        select = ChromeButton("Select shape");
        select.IsEnabled = false;
        select.Clicked += (_, _) =>
        {
            if (map is null
                || GetShapeCatalogSelectedKey() is not { } key
                || !map.SelectOverlay(key))
            {
                UpdateStatus("Select a shape in the catalog first.");
                return;
            }

            UpdateStatus($"Selected {key}.");
        };

        erase = ChromeButton("Erase selected");
        erase.IsEnabled = false;
        erase.Clicked += (_, _) =>
        {
            if (map?.RequestEraseSelectedOverlay() != true)
                UpdateStatus("Select an overlay before erasing it.");
        };

        clear = ChromeButton("Clear shapes");
        clear.IsEnabled = false;
        clear.Clicked += (_, _) =>
        {
            ClearOverlays();
            UpdateStatus("Cleared every host-owned shape. Reset overlays restores the sample set.");
        };

        var selectionControls = ToolRow(select, erase, clear);

        var shapeCatalogTitle = new Label
        {
            Text = "Shape catalog",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
        };
        shapeCatalogTitle.SetDynamicResource(
            Label.TextColorProperty,
            GraphicalProfile.TextResourceKey);

        var shapeCatalogHint = new Label
        {
            Text = "Select a row or overlay. Delete/Backspace requests host erasure.",
            FontSize = 11,
            LineBreakMode = LineBreakMode.TailTruncation,
        };
        shapeCatalogHint.SetDynamicResource(
            Label.TextColorProperty,
            GraphicalProfile.MutedResourceKey);

        shapeCatalog = new CollectionView
        {
            ItemsSource = shapeEntries,
            SelectionMode = SelectionMode.Single,
            HeightRequest = 172,
            ItemTemplate = new DataTemplate(() =>
            {
                var title = new Label
                {
                    FontSize = 12,
                    FontAttributes = FontAttributes.Bold,
                    LineBreakMode = LineBreakMode.TailTruncation,
                };
                title.SetDynamicResource(
                    Label.TextColorProperty,
                    GraphicalProfile.TextResourceKey);
                title.SetBinding(Label.TextProperty, nameof(ShapeCatalogEntry.Title));

                var details = new Label
                {
                    FontSize = 11,
                    LineBreakMode = LineBreakMode.TailTruncation,
                };
                details.SetDynamicResource(
                    Label.TextColorProperty,
                    GraphicalProfile.MutedResourceKey);
                details.SetBinding(Label.TextProperty, nameof(ShapeCatalogEntry.Details));

                return new VerticalStackLayout
                {
                    Padding = new Thickness(12, 6),
                    Spacing = 1,
                    Children = { title, details },
                };
            }),
            EmptyView = new Label
            {
                Text = "No shapes. Draw one or reset the sample overlays.",
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                FontSize = 12,
            },
        };
        shapeCatalog.SetDynamicResource(
            VisualElement.BackgroundColorProperty,
            GraphicalProfile.RaisedResourceKey);

        var shapeCatalogHost = new VerticalStackLayout
        {
            Padding = new Thickness(16, 10, 16, 12),
            Spacing = 4,
            Children = { shapeCatalogTitle, shapeCatalogHint, shapeCatalog },
        };
        shapeCatalogHost.SetDynamicResource(
            VisualElement.BackgroundColorProperty,
            GraphicalProfile.SurfaceResourceKey);

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
            Text = "Windows shortcuts: Ctrl+C coordinate · Ctrl+Shift+C JSON · arrows pan · +/- zoom · Enter finish · Esc cancel · Delete/Backspace erase",
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
                selectionControls,
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

        mapHost.MinimumHeightRequest = 240;
        var mapAndCatalog = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto),
            },
        };
        mapAndCatalog.Add(mapHost, 0, 0);
        mapAndCatalog.Add(shapeCatalogHost, 0, 1);

        var layout = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
            },
        };
        layout.Add(header, 0, 0);
        layout.Add(mapAndCatalog, 0, 1);
        layout.SizeChanged += (_, _) =>
        {
            if (shapeCatalog is not null)
            {
                shapeCatalog.HeightRequest = layout.Height > layout.Width
                    ? 132
                    : 172;
            }
        };

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
                rectangle,
                finish!,
                cancel!,
                select!,
                erase!,
                clear!,
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
            select!.IsEnabled = GetShapeCatalogSelectedKey() is not null;
            erase!.IsEnabled = map.SelectedOverlay is not null;
            clear!.IsEnabled = shapeEntries.Count > 0;
        }

        void UpdateStatus(string message)
        {
            selectionMessage!.Text = message;
            UpdateCounters();
        }

        MapOverlayKey? GetShapeCatalogSelectedKey() =>
            shapeCatalog?.SelectedItem is ShapeCatalogEntry entry
                ? entry.Key
                : null;

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
                            : kind == GeoDrawingKind.Rectangle
                                ? "Rectangle mode: click two opposite corners, then Finish drawing."
                            : "Point mode: click one location.");
        }

        void ResetOverlays()
        {
            markers = CreateSampleMarkers().ToList();
            tracks = CreateSampleTracks().ToList();
            polygons = CreateSamplePolygons().ToList();
            circles = CreateSampleCircles().ToList();
            drawingOrigins.Clear();
            drawingNumber = 0;
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
            RefreshShapeCatalog(map.SelectedOverlay);
        }

        void ClearOverlays()
        {
            markers.Clear();
            tracks.Clear();
            polygons.Clear();
            circles.Clear();
            drawingOrigins.Clear();
            map?.ClearSelection();
            ApplyOverlays();
        }

        void AddDrawingOverlay(GeoDrawing drawing)
        {
            var id = $"drawing-{++drawingNumber}";
            MapOverlayKey key;
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
                    key = new MapOverlayKey(MapOverlayKind.Marker, id);
                    break;
                case GeoDrawingKind.Polyline:
                    tracks.Add(new MapTrackOverlay(
                        id,
                        drawing.Points,
                        $"Distance · {GeoMeasurementText.FormatDistance(drawing.LengthMeters)}",
                        Colors.Gold,
                        Colors.OrangeRed));
                    key = new MapOverlayKey(MapOverlayKind.Track, id);
                    break;
                case GeoDrawingKind.Polygon:
                    polygons.Add(new MapPolygonOverlay(
                        id,
                        drawing.Points,
                        $"Area · {GeoMeasurementText.FormatArea(drawing.AreaSquareMeters)}",
                        Colors.Gold,
                        Colors.Gold.WithAlpha(0.2f)));
                    key = new MapOverlayKey(MapOverlayKind.Polygon, id);
                    break;
                case GeoDrawingKind.Circle:
                    circles.Add(new MapCircleOverlay(
                        id,
                        new GeoCircle(
                            drawing.Points[0],
                            drawing.RadiusMeters.GetValueOrDefault()),
                        $"Radius · {GeoMeasurementText.FormatDistance(drawing.RadiusMeters.GetValueOrDefault())}",
                        Colors.Gold));
                    key = new MapOverlayKey(MapOverlayKind.Circle, id);
                    break;
                case GeoDrawingKind.Rectangle:
                    polygons.Add(new MapPolygonOverlay(
                        id,
                        drawing.Rectangle!.Value.Corners,
                        $"Rectangle · {GeoMeasurementText.FormatArea(drawing.AreaSquareMeters)}",
                        Colors.Gold,
                        Colors.Gold.WithAlpha(0.16f)));
                    key = new MapOverlayKey(MapOverlayKind.Polygon, id);
                    break;
                default:
                    return;
            }

            drawingOrigins[key] = drawing;
            ApplyOverlays();
            map?.SelectOverlay(key);
            UpdateStatus(DescribeDrawing(drawing));
        }

        void RefreshShapeCatalog(MapOverlayKey? selectedOverlay)
        {
            synchronizingShapeSelection = true;
            try
            {
                shapeEntries.Clear();
                foreach (var entry in CreateShapeCatalog(
                    markers,
                    tracks,
                    polygons,
                    circles,
                    drawingOrigins,
                    selectedOverlay))
                {
                    shapeEntries.Add(entry);
                }

                shapeCatalog!.SelectedItem = selectedOverlay is { } selected
                    ? shapeEntries.FirstOrDefault(entry => entry.Key == selected)
                    : null;
            }
            finally
            {
                synchronizingShapeSelection = false;
            }

            UpdateCounters();
        }

        void RemoveOverlay(MapOverlayKey key)
        {
            var removed = key.Kind switch
            {
                MapOverlayKind.Marker => markers.RemoveAll(item => item.Id == key.Id),
                MapOverlayKind.Track => tracks.RemoveAll(item => item.Id == key.Id),
                MapOverlayKind.Polygon => polygons.RemoveAll(item => item.Id == key.Id),
                MapOverlayKind.Circle => circles.RemoveAll(item => item.Id == key.Id),
                _ => 0,
            };
            if (removed == 0)
            {
                UpdateStatus($"The selected {key.Kind} no longer exists.");
                return;
            }

            drawingOrigins.Remove(key);
            map?.ClearSelection();
            ApplyOverlays();
            UpdateStatus($"Erased {key}.");
        }

        shapeCatalog.SelectionChanged += (_, _) =>
        {
            if (synchronizingShapeSelection || map is null)
                return;

            if (GetShapeCatalogSelectedKey() is { } key)
            {
                if (!map.SelectOverlay(key))
                    RefreshShapeCatalog(null);
            }
            else
            {
                map.ClearSelection();
            }
        };

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
                        EnableOverlayErasure = true,
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
                map.OverlaySelectionChanged += key =>
                {
                    RefreshShapeCatalog(key);
                    if (key is { } selected)
                        UpdateStatus($"Selected {selected}.");
                };
                map.OverlayEraseRequested += RemoveOverlay;
#if WINDOWS
                map.HandlerChanged += (_, _) =>
                    WindowsMapInput.Attach(map, UpdateStatus);
#endif
                mapHost.Children.Clear();
                mapHost.Add(map);
                SetControlsEnabled(true);
                RefreshShapeCatalog(null);
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
            GeoDrawingKind.Rectangle =>
                $"Rectangle overlay added: {GeoMeasurementText.FormatArea(drawing.AreaSquareMeters)}.",
            _ => "Drawing overlay added.",
        };

    static async Task<IReadOnlyList<CelestialCatalogEntry>> LoadCatalogSnapshotAsync(
        CatalogPackId pack,
        string cacheDirectory)
    {
        var provenance = CatalogPacks.GetProvenance(pack);
        var packageVersion = typeof(CatalogPacks).Assembly.GetName().Version?.ToString()
            ?? "current";
        var buildId = typeof(CatalogPacks).Assembly.ManifestModule.ModuleVersionId
            .ToString("N");
        var directory = Path.Combine(cacheDirectory, "catalogs", packageVersion, buildId);
        Directory.CreateDirectory(directory);
        var snapshot = new FileInfo(Path.Combine(directory, $"{pack}.ndjson"));
        if (!snapshot.Exists)
        {
            var temporary = snapshot.FullName + ".tmp";
            try
            {
                await using var source = CatalogPacks.OpenNdjson(pack);
                await using (var destination = new FileStream(
                    temporary,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 64 * 1024,
                    useAsync: true))
                {
                    await source.CopyToAsync(destination);
                }

                File.Move(temporary, snapshot.FullName, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
        }

        var reader = new NdjsonFileReader(
            new NdjsonOpenOptions(MaxTake: provenance.RecordCount));
        await using var document = await reader.OpenAsync(snapshot);
        var slice = await document.ReadAsync(take: provenance.RecordCount);
        if (slice.Records.Count != provenance.RecordCount)
        {
            throw new InvalidOperationException(
                $"The {pack} snapshot contained {slice.Records.Count} records; expected {provenance.RecordCount}.");
        }

        var entries = new List<CelestialCatalogEntry>(slice.Records.Count);
        foreach (var record in slice.Records)
        {
            if (!record.IsValid || record.Json is not { } json)
                throw new InvalidOperationException(
                    $"The {pack} snapshot contains invalid record {record.Number}: {record.Error?.Message}");

            var entry = JsonSerializer.Deserialize<CelestialCatalogEntry>(
                json.GetRawText(),
                CatalogSnapshotJson)
                ?? throw new InvalidOperationException(
                    $"The {pack} snapshot record {record.Number} was empty.");
            entries.Add(entry);
        }

        return entries;
    }

    static IEnumerable<ShapeCatalogEntry> CreateShapeCatalog(
        IEnumerable<MapMarker> markers,
        IEnumerable<MapTrackOverlay> tracks,
        IEnumerable<MapPolygonOverlay> polygons,
        IEnumerable<MapCircleOverlay> circles,
        IReadOnlyDictionary<MapOverlayKey, GeoDrawing> drawingOrigins,
        MapOverlayKey? selectedOverlay)
    {
        foreach (var marker in markers)
        {
            var key = new MapOverlayKey(MapOverlayKind.Marker, marker.Id);
            yield return new ShapeCatalogEntry(
                key,
                CatalogTitle(key, marker.Label, drawingOrigins),
                $"1 vertex · {GeoCoordinateText.Format(marker.Position, 4)} · {SelectionState(key, selectedOverlay)}");
        }

        foreach (var track in tracks)
        {
            var key = new MapOverlayKey(MapOverlayKind.Track, track.Id);
            yield return new ShapeCatalogEntry(
                key,
                CatalogTitle(key, track.Label, drawingOrigins),
                $"{track.Points.Count} vertices · distance {GeoMeasurementText.FormatDistance(
                    GeoPathMetrics.PolylineLength(track.Points))} · {SelectionState(key, selectedOverlay)}");
        }

        foreach (var polygon in polygons)
        {
            var key = new MapOverlayKey(MapOverlayKind.Polygon, polygon.Id);
            yield return new ShapeCatalogEntry(
                key,
                CatalogTitle(key, polygon.Label, drawingOrigins),
                $"{polygon.Points.Count} vertices · perimeter {GeoMeasurementText.FormatDistance(
                    GeoPathMetrics.PolylineLength(polygon.Points, close: true))} · area {GeoMeasurementText.FormatArea(
                    GeoPathMetrics.PolygonAreaSquareMeters(polygon.Points))} · {SelectionState(key, selectedOverlay)}");
        }

        foreach (var circle in circles)
        {
            var key = new MapOverlayKey(MapOverlayKind.Circle, circle.Id);
            yield return new ShapeCatalogEntry(
                key,
                CatalogTitle(key, circle.Label, drawingOrigins),
                $"1 vertex · radius {GeoMeasurementText.FormatDistance(circle.Circle.RadiusMeters)} · circumference {GeoMeasurementText.FormatDistance(
                    2 * global::System.Math.PI * circle.Circle.RadiusMeters)} · {SelectionState(key, selectedOverlay)}");
        }
    }

    static string CatalogTitle(
        MapOverlayKey key,
        string? label,
        IReadOnlyDictionary<MapOverlayKey, GeoDrawing> drawingOrigins)
    {
        var kind = drawingOrigins.TryGetValue(key, out var drawing)
            ? $"Drawing / {drawing.Kind}"
            : key.Kind.ToString();
        return $"{kind} · {label ?? key.Id}";
    }

    static string SelectionState(MapOverlayKey key, MapOverlayKey? selectedOverlay) =>
        key == selectedOverlay ? "selected" : "not selected";

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

    sealed record ShapeCatalogEntry(
        MapOverlayKey Key,
        string Title,
        string Details);
}
