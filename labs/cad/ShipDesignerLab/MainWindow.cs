using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Novolis.Avalonia.Cad.Commands;
using Novolis.Avalonia.Cad.Core;
using Novolis.Avalonia.Cad.Services;
using Novolis.Avalonia.Cad.Session;
using Novolis.Avalonia.Cad.Ship;
using Novolis.Avalonia.Cad.Ui;
using Novolis.Avalonia.Ship;
using Novolis.Avalonia.Ship.Design;
using Novolis.Avalonia.Ship.Design.Services;
using Novolis.Avalonia.Ship.Design.Session;
using Novolis.Cad.Primitives;
using Novolis.Ship.Design;

namespace ShipDesignerLab;

internal sealed class MainWindow : Window
{
    private readonly CadSessionService _cad;
    private readonly ShipDesignSession _design;
    private readonly CadDocumentSession _session;
    private readonly CadEditorSettings _settings;
    private readonly CadCommandBus _bus;
    private readonly CadCommandDispatcher _dispatcher;
    private readonly CadToolController _tools;
    private readonly CadModelRenderer _modelRenderer;
    private CadEditorSurface _editor = null!;
    private TextBlock _status = null!;

    public MainWindow(CadSessionService cad, ShipDesignSession design)
    {
        _cad = cad;
        _design = design;
        _session = cad.Document;
        _settings = cad.Settings;
        _bus = cad.Bus;
        _dispatcher = cad.Dispatcher;
        _tools = new CadToolController(_dispatcher, _settings);
        _modelRenderer = new CadModelRenderer(_session, _settings);

        Title = "Novolis Ship Designer Lab";
        Width = 1440;
        Height = 920;

        Content = BuildLayout();
        _cad.Editor = _editor;
        _cad.ExportRoot = Path.Combine(_settings.DataRoot, "exports");
        _cad.FitHandler = () =>
        {
            if (_design.Workspace == ShipWorkspaceKind.Model)
                _editor.Fit();
        };
        _cad.ActionResult += OnActionResult;
        _editor.SetWorkspace(CadWorkspace.Cad);
    }

    private Control BuildLayout()
    {
        _editor = new CadEditorSurface(_session, _settings, _bus, _dispatcher, _tools, _modelRenderer);
        _status = new TextBlock
        {
            Text = "PLAN",
            Margin = new Thickness(8, 4),
            Foreground = Novolis.Avalonia.GraphicalProfile.GraphicalProfile.TextBrush,
        };

        var menu = new Menu();
        var file = new MenuItem { Header = "_File" };
        file.Items.Add(MenuCmd("New Ship…", () =>
        {
            _design.NewShip(ShipDesignSession.DefaultDefinition("New Ship"));
            _status.Text = "Created New Ship — draw Wall / Room on the deck plan";
        }));
        file.Items.Add(MenuCmd("Clear to blank…", () =>
        {
            _design.ClearToBlank();
            _status.Text = "Clean slate — Create ship panel or File → New Ship";
        }));
        file.Items.Add(MenuCmd("Open .shipjson…", OnOpenShip));
        file.Items.Add(MenuCmd("Save .shipjson", () =>
        {
            _design.Save();
            _status.Text = $"Saved {_design.Path}";
        }));
        file.Items.Add(MenuCmd("Save As .shipjson…", OnSaveShipAs));
        file.Items.Add(new Separator());
        file.Items.Add(MenuCmd("Export .cadjson…", OnExportCad));
        file.Items.Add(MenuCmd("Import Calypso seed…", OnImportShip));
        file.Items.Add(new Separator());
        file.Items.Add(MenuCmd("Exit", Close));
        var ship = new MenuItem { Header = "_Ship" };
        ship.Items.Add(MenuCmd(
            "Validate Ship",
            () => _cad.Execute(new CadCommandDto { ActionId = ShipChrome.ValidateShipActionId })));
        ship.Items.Add(MenuCmd(
            "Refresh airtight",
            () => _cad.Execute(new CadCommandDto { ActionId = ShipChrome.RefreshAirtightActionId })));
        ship.Items.Add(MenuCmd("Export scene (.nov3djson)", OnEvaluateScene));
        menu.Items.Add(file);
        menu.Items.Add(ship);

        var shell = ShipDesignChrome.CreateShell(_cad, _design, _editor, _status);
        var root = new DockPanel();
        DockPanel.SetDock(menu, Dock.Top);
        root.Children.Add(menu);
        root.Children.Add(shell);
        return root;
    }

    private static MenuItem MenuCmd(string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action();
        return item;
    }

    private async void OnOpenShip()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open ship .shipjson",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Ship JSON") { Patterns = ["*.shipjson"] },
                new FilePickerFileType("CAD JSON") { Patterns = ["*.cadjson"] },
            ],
        });
        if (files.Count == 0)
            return;

        var path = files[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
            return;

        if (path.EndsWith(".cadjson", StringComparison.OrdinalIgnoreCase))
        {
            _cad.Execute(new CadCommandDto { ActionId = CadSessionActionIds.Open, Path = path });
            _design.ImportCadDocument(_session.Document);
            _status.Text = $"Imported CAD → design {path}";
            return;
        }

        _design.OpenFromPath(path);
        _status.Text = $"Opened {path}";
    }

    private async void OnSaveShipAs()
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save ship .shipjson",
            DefaultExtension = "shipjson",
            FileTypeChoices =
            [
                new FilePickerFileType("Ship JSON") { Patterns = ["*.shipjson"] },
            ],
        });
        var path = file?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
            return;

        _design.SaveTo(path);
        _status.Text = $"Saved {path}";
    }

    private async void OnExportCad()
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export flat .cadjson",
            DefaultExtension = "cadjson",
            FileTypeChoices =
            [
                new FilePickerFileType("CAD JSON") { Patterns = ["*.cadjson"] },
            ],
        });
        var path = file?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
            return;

        _session.SaveTo(path);
        _status.Text = $"Exported CAD {path}";
    }

    private void OnImportShip()
    {
        var result = _cad.Execute(new CadCommandDto { ActionId = CadShipChrome.ImportShipActionId });
        if (result.Ok)
            _design.ImportCadDocument(_session.Document);
        _status.Text = result.Message ?? (result.Ok ? "Imported → ShipDesign" : "Import failed");
    }

    private void OnEvaluateScene()
    {
        var outDir = Path.Combine(_settings.DataRoot, "exports");
        Directory.CreateDirectory(outDir);
        var path = Path.Combine(outDir, "ship-analyze.nov3djson");
        var eval = ShipDesignEvaluator.Evaluate(_design.Design, path);
        _status.Text =
            $"ANALYZE scene export: {eval.MeshNodeCount} meshes from {eval.ObjectCount} objects, {eval.CutoutCount} cutouts → {path}";
    }

    private void OnActionResult(CadActionResultEventDto e) =>
        _status.Text = string.IsNullOrWhiteSpace(e.Message) ? e.ActionId : e.Message;
}
