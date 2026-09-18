using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using CalypsoCad.Services;
using Novolis.Avalonia.Cad.Commands;
using Novolis.Avalonia.Cad.Core;
using Novolis.Avalonia.Cad.Services;
using Novolis.Avalonia.Cad.Session;
using Novolis.Avalonia.Cad.Ui;
using Novolis.Avalonia.Raylib;

namespace CalypsoCad;

internal sealed class MainWindow : Window
{
    private readonly CalypsoSession _session;
    private readonly CalypsoRenderer _renderer;
    private readonly CadSessionService _cad;
    private CadSessionSurface? _cadSession;
    private readonly CadPreviewControl _preview = new();
    private RaylibHostControl _raylibHost => _preview.Host;
    private readonly ListBox _spaceList = new();
    private readonly ListBox _hookList = new();
    private readonly TextBlock _status = new() { Margin = new Thickness(8, 4), TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _path = new() { Margin = new Thickness(8, 0, 8, 8), Opacity = 0.75, FontSize = 11, TextWrapping = TextWrapping.Wrap };

    public MainWindow(CalypsoSession session, CalypsoRenderer renderer)
    {
        _session = session;
        _renderer = renderer;

        var settings = new CadEditorSettings(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Novolis",
                "CalypsoCad"),
            workspaceFolderName: "generated");
        var document = new CadDocumentSession(settings);
        var bus = new CadCommandBus(document);
        var dispatcher = new CadCommandDispatcher(document, bus, settings);
        _cad = new CadSessionService(document, settings, bus, dispatcher)
        {
            AppId = "calypso-cad",
            AppTitle = "Calypso CAD",
            ExportRoot = Path.Combine(settings.DataRoot, "generated", "exports"),
            Preview = _preview,
        };
        Novolis.Avalonia.Cad.Ship.CadShipChrome.Attach(_cad);
        _cad.AsyncExportHook = async cmd =>
        {
            var root = cmd.ExportRoot ?? _session.GeneratedDirectory;
            var saved = await ViewportPngExporter.ExportViewsAsync(
                _raylibHost, _session, _renderer, root).ConfigureAwait(true);
            return new CadCommandResultDto
            {
                Ok = saved.Count > 0,
                ActionId = CadSessionActionIds.ExportViewTour,
                Message = saved.Count == 0 ? "View tour failed." : $"Exported {saved.Count} views.",
                Paths = saved.ToArray(),
                ErrorCode = saved.Count == 0 ? "exportFailed" : null,
            };
        };
        _cadSession = CadSessionSurface.AttachAll(_cad);

        Title = "Calypso CAD — Rev G";
        Width = 1280;
        Height = 800;
        MinWidth = 900;
        MinHeight = 560;

        var regenerate = new Button { Content = "Regenerate", Margin = new Thickness(4) };
        regenerate.Click += (_, _) =>
        {
            _session.RegenerateAndLoad();
            _renderer.SyncInteriorFromSelection();
            RefreshSpaceList();
            RefreshHookList();
            UpdateStatus();
        };

        var planBtn = new Button { Content = "Plan (P)", Margin = new Thickness(4) };
        planBtn.Click += (_, _) => SetView(CalypsoViewMode.Plan);
        var orbitBtn = new Button { Content = "Orbit (O)", Margin = new Thickness(4) };
        orbitBtn.Click += (_, _) => SetView(CalypsoViewMode.Orbit);
        var interiorBtn = new Button { Content = "Interior (I)", Margin = new Thickness(4) };
        interiorBtn.Click += (_, _) => SetView(CalypsoViewMode.Interior);

        var deckAll = new Button { Content = "All decks", Margin = new Thickness(4) };
        deckAll.Click += (_, _) => { _session.DeckFilter = null; UpdateStatus(); };
        var deckM1 = new Button { Content = "Deck −1", Margin = new Thickness(4) };
        deckM1.Click += (_, _) => { _session.DeckFilter = -1; UpdateStatus(); };
        var deck0 = new Button { Content = "Deck 0", Margin = new Thickness(4) };
        deck0.Click += (_, _) => { _session.DeckFilter = 0; UpdateStatus(); };
        var deckP1 = new Button { Content = "Deck +1", Margin = new Thickness(4) };
        deckP1.Click += (_, _) => { _session.DeckFilter = 1; UpdateStatus(); };

        var exportBtn = new Button { Content = "Export PNG (E)", Margin = new Thickness(4) };
        exportBtn.Click += (_, _) => _ = ExportCurrentPngAsync();
        var exportAllBtn = new Button { Content = "Export views", Margin = new Thickness(4) };
        exportAllBtn.Click += (_, _) => _ = ExportAllViewsAsync();

        var solidBtn = new Button { Content = "Solid", Margin = new Thickness(4) };
        solidBtn.Click += (_, _) =>
        {
            _session.WireMeshMode = CalypsoWireMeshMode.None;
            UpdateStatus();
        };
        var wireBtn = new Button { Content = "Wire (W)", Margin = new Thickness(4) };
        wireBtn.Click += (_, _) =>
        {
            _session.WireMeshMode = CalypsoWireMeshMode.Wire;
            UpdateStatus();
        };
        var cutBtn = new Button { Content = "Cutaway (C)", Margin = new Thickness(4) };
        cutBtn.Click += (_, _) =>
        {
            _session.WireMeshMode = CalypsoWireMeshMode.CutawayPartial;
            UpdateStatus();
        };

        _spaceList.SelectionChanged += (_, _) =>
        {
            if (_spaceList.SelectedItem is SpaceItem item)
            {
                _session.SelectedSpaceId = item.Id;
                _session.SelectedHookId = null;
                _renderer.SyncInteriorFromSelection();
                UpdateStatus();
            }
        };

        var left = new DockPanel { Width = 280, Margin = new Thickness(8) };
        DockPanel.SetDock(regenerate, Dock.Top);
        var toolbar = new WrapPanel();
        toolbar.Children.Add(regenerate);
        toolbar.Children.Add(planBtn);
        toolbar.Children.Add(orbitBtn);
        toolbar.Children.Add(interiorBtn);
        toolbar.Children.Add(deckAll);
        toolbar.Children.Add(deckM1);
        toolbar.Children.Add(deck0);
        toolbar.Children.Add(deckP1);
        toolbar.Children.Add(exportBtn);
        toolbar.Children.Add(exportAllBtn);
        toolbar.Children.Add(solidBtn);
        toolbar.Children.Add(wireBtn);
        toolbar.Children.Add(cutBtn);
        DockPanel.SetDock(toolbar, Dock.Top);
        left.Children.Add(toolbar);

        var spacesLabel = new TextBlock { Text = "Spaces", FontWeight = FontWeight.SemiBold, Margin = new Thickness(4, 12, 4, 4) };
        DockPanel.SetDock(spacesLabel, Dock.Top);
        left.Children.Add(spacesLabel);
        left.Children.Add(_spaceList);

        var hookLabel = new TextBlock { Text = "Hooks", FontWeight = FontWeight.SemiBold, Margin = new Thickness(4, 12, 4, 4) };
        DockPanel.SetDock(hookLabel, Dock.Top);
        left.Children.Add(hookLabel);
        DockPanel.SetDock(_hookList, Dock.Top);
        left.Children.Add(_hookList);

        _hookList.SelectionChanged += (_, _) =>
        {
            if (_hookList.SelectedItem is HookItem hook)
            {
                _session.SelectedHookId = hook.Id;
                var spaceOnDeck = _session.Document.Entities
                    .FirstOrDefault(e => e.Kind == "space" && e.Deck == hook.Deck);
                _session.SelectedSpaceId = spaceOnDeck?.Id;
                SetView(CalypsoViewMode.Interior);
                _renderer.SyncInteriorFromSelection();
                UpdateStatus();
            }
            else
            {
                _session.SelectedHookId = null;
                _renderer.SyncInteriorFromSelection();
                UpdateStatus();
            }
        };

        var bottom = new StackPanel();
        bottom.Children.Add(_status);
        bottom.Children.Add(_path);
        DockPanel.SetDock(bottom, Dock.Bottom);

        var root = new DockPanel();
        DockPanel.SetDock(left, Dock.Left);
        DockPanel.SetDock(bottom, Dock.Bottom);
        root.Children.Add(bottom);
        root.Children.Add(left);
        _preview.HorizontalAlignment = HorizontalAlignment.Stretch;
        _preview.VerticalAlignment = VerticalAlignment.Stretch;
        root.Children.Add(_preview);
        Content = root;

        KeyDown += OnKeyDown;
        _preview.OrbitDrag += (dx, dy) => _renderer.OrbitDrag(dx, dy);
        _preview.Zoom += delta => _renderer.Zoom(delta);

        Opened += async (_, _) =>
        {
            _session.RegenerateAndLoad();
            var cadjson = Path.Combine(_session.GeneratedDirectory, "calypso.cadjson");
            if (File.Exists(cadjson))
                _cad.Document.OpenFromPath(cadjson);
            _cad.ExportRoot = ViewportPngExporter.ExportsDirectory(_session.GeneratedDirectory);
            _cad.Preview = _preview;
            _renderer.Bind(_raylibHost);
            _preview.Start();
            _renderer.Fit();
            _renderer.SyncInteriorFromSelection();
            RefreshSpaceList();
            RefreshHookList();
            UpdateStatus();

            // First-look PNG exports (plan / orbit / interior) via Raylib presented-frame export.
            await Task.Delay(400).ConfigureAwait(true);
            await ExportAllViewsAsync().ConfigureAwait(true);
        };

        Closing += async (_, _) =>
        {
            _preview.Stop();
            if (_cadSession is not null)
                await _cadSession.DisposeAsync().ConfigureAwait(true);
        };
    }

    private async Task ExportCurrentPngAsync()
    {
        var kind = _session.ViewMode.ToString().ToLowerInvariant();
        var path = ViewportPngExporter.AllocatePath(_session.GeneratedDirectory, kind);
        var result = _cad.Execute(new CadCommandDto
        {
            ActionId = CadSessionActionIds.ExportPreviewPng,
            Path = path,
            Kind = kind,
        });
        _session.StatusText = result.Ok ? $"Exported {path}" : result.Message;
        UpdateStatus();
        if (result.Ok)
            _path.Text = path;
        await Task.CompletedTask;
    }

    private async Task ExportAllViewsAsync()
    {
        var result = await (_cad.AsyncExportHook?.Invoke(new CadCommandDto
        {
            ActionId = CadSessionActionIds.ExportViewTour,
            ExportRoot = _session.GeneratedDirectory,
        }) ?? Task.FromResult(new CadCommandResultDto
        {
            Ok = false,
            ActionId = CadSessionActionIds.ExportViewTour,
            Message = "No tour hook.",
            ErrorCode = "noTour",
        })).ConfigureAwait(true);
        _session.StatusText = result.Message;
        UpdateStatus();
        if (result.Paths is { Length: > 0 })
            _path.Text = string.Join(Environment.NewLine, result.Paths);
    }

    private void SetView(CalypsoViewMode mode)
    {
        _session.ViewMode = mode;
        if (mode == CalypsoViewMode.Interior)
            _renderer.SyncInteriorFromSelection();
        UpdateStatus();
    }

    private void RefreshSpaceList()
    {
        var items = _session.Spaces
            .OrderBy(s => s.Deck)
            .ThenBy(s => s.Name)
            .Select(s => new SpaceItem(s.Id, $"[{s.Deck}] {s.Name}"))
            .ToList();
        _spaceList.ItemsSource = items;
        if (_session.SelectedSpaceId is { } id)
            _spaceList.SelectedItem = items.FirstOrDefault(i => i.Id == id);
    }

    private void RefreshHookList()
    {
        var items = new List<HookItem>();
        foreach (var entity in _session.Document.Entities)
        {
            if (entity.Hooks is not { Count: > 0 } hooks)
                continue;

            foreach (var h in hooks)
                items.Add(new HookItem(h.Id, $"[{entity.Deck}] {h.Tag}", entity.Deck));
        }

        items = items
            .OrderBy(i => i.Deck)
            .ThenBy(i => i.Label)
            .ToList();

        _hookList.ItemsSource = items;
        if (_session.SelectedHookId is { } id)
            _hookList.SelectedItem = items.FirstOrDefault(i => i.Id == id);
    }

    private void UpdateStatus()
    {
        var exportNote = string.IsNullOrWhiteSpace(_session.StatusText) ? "" : $" · {_session.StatusText}";
        var hookNote = _session.SelectedHook?.Tag is { } tag ? $" · hook={tag}" : "";
        _status.Text =
            $"{_session.ViewMode} · deck filter={_session.DeckFilter?.ToString() ?? "all"} · entities={_session.Document.Entities.Count}{exportNote}{hookNote}";
        if (string.IsNullOrWhiteSpace(_path.Text) || !_path.Text.Contains("exports", StringComparison.OrdinalIgnoreCase))
            _path.Text = _session.GeneratedDirectory;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.P:
                SetView(CalypsoViewMode.Plan);
                break;
            case Key.O:
                SetView(CalypsoViewMode.Orbit);
                break;
            case Key.I:
                SetView(CalypsoViewMode.Interior);
                break;
            case Key.D0:
            case Key.NumPad0:
                _session.DeckFilter = null;
                UpdateStatus();
                break;
            case Key.D1:
            case Key.NumPad1:
                _session.DeckFilter = -1;
                UpdateStatus();
                break;
            case Key.D2:
            case Key.NumPad2:
                _session.DeckFilter = 0;
                UpdateStatus();
                break;
            case Key.D3:
            case Key.NumPad3:
                _session.DeckFilter = 1;
                UpdateStatus();
                break;
            case Key.F:
                _renderer.Fit();
                break;
            case Key.S:
                _session.WireMeshMode = CalypsoWireMeshMode.None;
                UpdateStatus();
                break;
            case Key.W:
                _session.WireMeshMode = CalypsoWireMeshMode.Wire;
                if (_session.ViewMode == CalypsoViewMode.Interior)
                    _renderer.SyncInteriorFromSelection();
                UpdateStatus();
                break;
            case Key.C:
                _session.WireMeshMode = CalypsoWireMeshMode.CutawayPartial;
                if (_session.ViewMode == CalypsoViewMode.Interior)
                    _renderer.SyncInteriorFromSelection();
                UpdateStatus();
                break;
            case Key.OemOpenBrackets:
            case Key.OemComma:
                _session.WireMeshMode = CalypsoWireMeshMode.CutawayPartial;
                _session.CutPlaneUserDriven = true;
                _session.CutPlaneOffset -= 1f;
                UpdateStatus();
                break;
            case Key.OemCloseBrackets:
            case Key.OemPeriod:
                _session.WireMeshMode = CalypsoWireMeshMode.CutawayPartial;
                _session.CutPlaneUserDriven = true;
                _session.CutPlaneOffset += 1f;
                UpdateStatus();
                break;
            case Key.L:
                _session.WireMeshMode = CalypsoWireMeshMode.CutawayPartial;
                _session.CutPlaneLongitudinal = true;
                UpdateStatus();
                break;
            case Key.B:
                _session.WireMeshMode = CalypsoWireMeshMode.CutawayPartial;
                _session.CutPlaneLongitudinal = false;
                UpdateStatus();
                break;
            case Key.E:
                _ = ExportCurrentPngAsync();
                break;
        }
    }

    private sealed record SpaceItem(Guid Id, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed record HookItem(Guid Id, string Label, int Deck)
    {
        public override string ToString() => Label;
    }
}
