using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Novolis.Avalonia.Cad.Commands;
using Novolis.Avalonia.Cad.Core;
using Novolis.Avalonia.Cad.Services;
using Novolis.Avalonia.Cad.Session;
using Novolis.Avalonia.Cad.Ui;
using Novolis.Avalonia.GraphicalProfile;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Host;

/// <summary>
/// Thin Calypso host around <see cref="CadSessionService"/> / <see cref="CadEditorSurface"/>.
/// Silk inspection remains available via <c>--explorer</c>.
/// </summary>
internal sealed class CalypsoCadMainWindow : Window
{
    private readonly CadSessionService _session;
    private readonly CadEditorSurface _editor;
    private readonly TextBlock _status = new() { Text = "Ready", Margin = new global::Avalonia.Thickness(8, 4) };

    public CalypsoCadMainWindow(CadSessionService session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        Title = "Calypso CAD";
        Width = 1400;
        Height = 900;
        MinWidth = 1000;
        MinHeight = 640;
        Background = GraphicalProfile.BackgroundBrush;
        GraphicalProfileBinding.Bind(this, BackgroundProperty, GraphicalProfile.BackgroundResourceKey);

        var tools = new CadToolController(_session.Dispatcher, _session.Settings);
        var modelRenderer = new CadModelRenderer(_session.Document, _session.Settings);
        _editor = new CadEditorSurface(
            _session.Document,
            _session.Settings,
            _session.Bus,
            _session.Dispatcher,
            tools,
            modelRenderer);
        _session.Editor = _editor;

        var bar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new global::Avalonia.Thickness(8),
        };
        bar.Children.Add(MakeButton("New", OnNew));
        bar.Children.Add(MakeButton("Open…", OnOpen));
        bar.Children.Add(MakeButton("Save", () =>
        {
            _session.Document.Save();
            _status.Text = $"Saved → {_session.Document.DocumentPath}";
        }));
        bar.Children.Add(MakeButton("Generate Calypso", OnGenerate));
        bar.Children.Add(MakeButton("Structural bay", OnGenerateBay));

        var root = new DockPanel();
        DockPanel.SetDock(bar, Dock.Top);
        DockPanel.SetDock(_status, Dock.Bottom);
        root.Children.Add(bar);
        root.Children.Add(_status);
        root.Children.Add(_editor);
        Content = root;

        _session.Document.OpenOrCreateDefault();
        _status.Text = "Empty / last document — Generate Calypso to load the freighter.";
    }

    private static Button MakeButton(string label, Action action)
    {
        var b = new Button { Content = label, Padding = new global::Avalonia.Thickness(10, 4) };
        b.Click += (_, _) => action();
        return b;
    }

    private void OnNew()
    {
        _session.Document.NewDocument();
        _status.Text = "New empty document.";
    }

    private async void OnOpen()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open CAD document",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("CAD JSON") { Patterns = ["*.cadjson", "*.json"] },
            ],
        });
        if (files.Count == 0)
            return;
        var path = files[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
            return;
        CalypsoCadComposition.LoadCadJson(_session, path);
        _status.Text = $"Opened → {path}";
    }

    private void OnGenerate() => RunGenerate(CalypsoCadComposition.GenerateActionId);

    private void OnGenerateBay() => RunGenerate(CalypsoCadComposition.GenerateBayActionId);

    private void RunGenerate(string actionId)
    {
        var result = _session.Execute(new CadCommandDto
        {
            ActionId = actionId,
            Properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["confirmDirty"] = "force",
            },
        });
        _status.Text = result.Ok
            ? $"Generated → {result.Message}"
            : $"Generate failed: {result.Message}";
    }
}
