using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using CalypsoCad.Drawing;
using CalypsoCad.Export;
using CalypsoCad.Model;
using CalypsoCad.Publication;
using CalypsoCad.Session;
using CalypsoCad.Visibility;
using Novolis.Avalonia.GraphicalProfile;

namespace CalypsoCad.Explorer;

/// <summary>
/// Hierarchical object browser driven by <see cref="ShipPartRegistry"/>.
/// Visibility / isolate are viewport filters — they do not mutate ownership.
/// </summary>
internal sealed class CalypsoObjectBrowser : UserControl
{
    private readonly CalypsoViewportController _controller;
    private readonly TreeView _tree;
    private readonly TextBox _search;
    private readonly TextBlock _inspector;
    private readonly Dictionary<string, TreeViewItem> _itemsById = new(StringComparer.OrdinalIgnoreCase);
    private string _filter = "";

    public CalypsoObjectBrowser(CalypsoViewportController controller)
    {
        _controller = controller;

        _search = new TextBox
        {
            PlaceholderText = "Find object…",
            FontSize = 11,
            Margin = new Thickness(0, 0, 0, 4),
        };
        _search.TextChanged += (_, _) =>
        {
            _filter = _search.Text?.Trim() ?? "";
            RebuildTree();
        };

        _tree = new TreeView
        {
            FontSize = 11,
            MaxHeight = 320,
        };
        _tree.SelectionChanged += OnTreeSelectionChanged;

        _inspector = new TextBlock
        {
            FontFamily = GraphicalProfile.MonoFont,
            FontSize = 10,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0),
        };
        GraphicalProfileBinding.Bind(_inspector, TextBlock.ForegroundProperty, GraphicalProfile.MutedResourceKey);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Margin = new Thickness(0, 4, 0, 0),
            Children =
            {
                ActionBtn("Hide", HideSelected),
                ActionBtn("Show", ShowSelected),
                ActionBtn("Isolate", IsolateSelected),
                ActionBtn("Clear", ClearIsolateAndHide),
                ActionBtn("Focus", FocusSelected),
            },
        };

        var isolates = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
        isolates.Children.Add(ActionBtn("Frames", () => IsolateNode(ShipHierarchyIds.RingFrames)));
        isolates.Children.Add(ActionBtn("Bulkheads", () => IsolateNode(ShipHierarchyIds.HeavyBulkheads)));
        isolates.Children.Add(ActionBtn("Knuckles", () => IsolateNode(ShipHierarchyIds.KnuckleStringers)));
        isolates.Children.Add(ActionBtn("Primary L", () => IsolateNode(ShipHierarchyIds.Longitudinal)));
        isolates.Children.Add(ActionBtn("Deck 0", () => IsolateNode(ShipHierarchyIds.Deck0)));
        isolates.Children.Add(ActionBtn("Cargo fl", () => IsolateNode(ShipHierarchyIds.DeckM1)));
        isolates.Children.Add(ActionBtn("Int.supp", () => IsolateNode(ShipHierarchyIds.RingFrames)));
        isolates.Children.Add(ActionBtn("Exit iso", () =>
        {
            _controller.Session.Visibility.EndIsolation();
            _controller.Session.StatusText = "isolation:exit";
            RebuildTree();
        }));

        var presets = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
        foreach (var (id, label, build) in VisibilityPresets.All.Take(6))
        {
            var captured = build;
            var lid = label;
            presets.Children.Add(ActionBtn(label.Length > 14 ? label[..14] : label, () =>
            {
                _controller.Session.Visibility.ApplySnapshot(captured(), asPreset: id);
                _controller.Session.StatusText = $"preset:{lid}";
                RebuildTree();
            }));
        }

        var root = new StackPanel { Spacing = 2 };
        root.Children.Add(Header("Object browser"));
        if (_controller.Session.Visibility.IsolationActive)
            root.Children.Add(Hint("ISOLATION active — Exit iso restores prior visibility."));
        root.Children.Add(_search);
        root.Children.Add(_tree);
        root.Children.Add(actions);
        root.Children.Add(Hint("Visibility: Inherit / Visible / Hidden on each row. Isolate uses the same service."));
        root.Children.Add(isolates);
        root.Children.Add(Hint("Presets:"));
        root.Children.Add(presets);

        var drawings = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
        drawings.Children.Add(ActionBtn("Drawing", () => CreateDrawingForSelected(DrawingKind.Assembly)));
        drawings.Children.Add(ActionBtn("Section", () => CreateDrawingForSelected(DrawingKind.Section)));
        drawings.Children.Add(ActionBtn("Detail", () => CreateDrawingForSelected(DrawingKind.Detail)));
        drawings.Children.Add(ActionBtn("Pkg", GenerateDrawingPackage));
        root.Children.Add(Hint("Drawings (hierarchy subject → universal engine):"));
        root.Children.Add(drawings);

        root.Children.Add(Header("Inspector"));
        root.Children.Add(_inspector);
        Content = root;

        RebuildTree();
    }

    public void RebuildTree()
    {
        var registry = _controller.Session.Registry;
        if (registry.Assemblies.Count == 0 && _controller.Session.Document.Entities.Count > 0)
            _controller.Session.RebuildSemanticRegistry();

        var tree = ShipHierarchyBuilder.BuildExplorerTree(registry);
        _itemsById.Clear();
        _tree.Items.Clear();
        var rootItem = BuildItem(tree);
        if (rootItem is not null)
            _tree.Items.Add(rootItem);
    }

    private TreeViewItem? BuildItem(ShipExplorerNode node)
    {
        if (!PassesFilter(node))
            return null;

        var vis = _controller.Session.Visibility;
        var local = vis.GetLocal(node.Id);
        var effective = vis.IsEffectivelyVisible(node.Id);
        var mixed = vis.SummarizeChildren(node.Id) == ChildVisibilitySummary.Mixed;

        var tip = BuildVisibilityTooltip(node.Id, local, effective);
        var eye = new TextBlock
        {
            Text = effective ? (mixed ? "◑" : "◉") : "○",
            FontSize = 11,
            Width = 18,
            VerticalAlignment = VerticalAlignment.Center,
        };
        ToolTip.SetTip(eye, tip);
        GraphicalProfileBinding.Bind(eye, TextBlock.ForegroundProperty,
            effective ? GraphicalProfile.TextResourceKey : GraphicalProfile.MutedResourceKey);

        var combo = new ComboBox
        {
            Width = 78,
            FontSize = 10,
            MinHeight = 22,
            ItemsSource = new[] { "Inherit", "Visible", "Hidden" },
            SelectedIndex = local switch
            {
                LocalVisibility.Visible => 1,
                LocalVisibility.Hidden => 2,
                _ => 0,
            },
        };
        ToolTip.SetTip(combo, tip);
        var nodeId = node.Id;
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedIndex < 0)
                return;
            var state = combo.SelectedIndex switch
            {
                1 => LocalVisibility.Visible,
                2 => LocalVisibility.Hidden,
                _ => LocalVisibility.Inherit,
            };
            if (vis.GetLocal(nodeId) == state)
                return;
            vis.SetLocal(nodeId, state);
            RebuildTree();
        };

        var title = new TextBlock
        {
            Text = node.Title,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 0, 0),
        };
        GraphicalProfileBinding.Bind(title, TextBlock.ForegroundProperty, GraphicalProfile.TextResourceKey);

        var header = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Children = { eye, combo, title },
        };

        var item = new TreeViewItem
        {
            Header = header,
            Tag = node,
            IsExpanded = node.Kind is ShipExplorerNodeKind.Root or ShipExplorerNodeKind.Assembly
                         && (node.AssemblyId?.Value.StartsWith("PHYS-", StringComparison.OrdinalIgnoreCase) == true
                             || node.Kind == ShipExplorerNodeKind.Root
                             || string.Equals(node.Id, ShipHierarchyIds.KnuckleStringers, StringComparison.OrdinalIgnoreCase)
                             || string.Equals(node.Id, ShipHierarchyIds.PhysicalStructure, StringComparison.OrdinalIgnoreCase)
                             || string.Equals(node.Id, ShipHierarchyIds.Hull, StringComparison.OrdinalIgnoreCase)),
        };
        GraphicalProfileBinding.Bind(item, ForegroundProperty, GraphicalProfile.TextResourceKey);
        _itemsById[node.Id] = item;

        foreach (var child in node.Children)
        {
            var childItem = BuildItem(child);
            if (childItem is not null)
                item.Items.Add(childItem);
        }

        // Keep empty organizational nodes that match filter ancestors.
        if (item.Items.Count == 0 && node.Children.Count > 0 && !string.IsNullOrEmpty(_filter))
            return null;

        return item;
    }

    private bool PassesFilter(ShipExplorerNode node)
    {
        if (string.IsNullOrEmpty(_filter))
            return true;
        if (node.Id.Contains(_filter, StringComparison.OrdinalIgnoreCase)
            || node.Title.Contains(_filter, StringComparison.OrdinalIgnoreCase))
            return true;
        return node.Children.Any(PassesFilter);
    }

    private void OnTreeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_tree.SelectedItem is not TreeViewItem { Tag: ShipExplorerNode node })
            return;

        var session = _controller.Session;
        session.SelectedAssemblyId = node.AssemblyId;
        session.SelectedPartId = node.PartId;
        session.SelectedReferenceId = node.ReferenceId;
        session.InspectorText = FormatInspector(node);
        _inspector.Text = session.InspectorText;
        session.StatusText = $"select:{node.Id}";
    }

    private string FormatInspector(ShipExplorerNode node)
    {
        var reg = _controller.Session.Registry;
        var visBlock = FormatVisibilityBlock(node.Id);
        if (node.PartId is { } pid && reg.TryGetPart(pid, out var part))
        {
            return
                visBlock +
                $"ID          {part.Id.Value}\n" +
                $"Name        {part.DisplayName}\n" +
                $"System      {part.System}\n" +
                $"Kind        {part.Kind}\n" +
                $"Assembly    {part.Assembly.Value}\n" +
                $"Functions   {part.Functions}\n" +
                $"Station Z   {Fmt(part.StationZStem)}\n" +
                $"Deck up     {Fmt(part.DeckUp)}\n" +
                $"Fab part    {part.FabPartId ?? "unresolved"}\n" +
                $"Entity      {part.EntityId?.ToString("N") ?? "unresolved"}\n" +
                $"Classified  {part.Classified}";
        }

        if (node.AssemblyId is { } aid && reg.TryGetAssembly(aid, out var asm))
        {
            var childCount = reg.ChildAssemblies(aid).Count();
            var partCount = reg.PartsOf(aid).Count();
            return
                visBlock +
                $"Assembly    {asm.Id.Value}\n" +
                $"Name        {asm.DisplayName}\n" +
                $"System      {asm.System}\n" +
                $"Kind        {asm.Kind}\n" +
                $"Parent      {asm.ParentAssemblyId?.Value ?? "(root)"}\n" +
                $"Children    {childCount}\n" +
                $"Parts       {partCount}\n" +
                $"Functions   {asm.Functions}\n" +
                $"Station Z   {Fmt(asm.StationZStem)}\n" +
                $"Extent      {Fmt(asm.ExtentZ0)} → {Fmt(asm.ExtentZ1)}";
        }

        if (node.ReferenceId is { } rid && reg.TryGetReference(rid, out var reference))
        {
            return
                visBlock +
                $"Reference   {reference.Id}\n" +
                $"Kind        {reference.Kind}\n" +
                $"Name        {reference.DisplayName}\n" +
                $"Parent      {reference.ParentAssemblyId?.Value ?? "unresolved"}\n" +
                $"Compartment {reference.CompartmentId ?? "unresolved"}\n" +
                $"Entity      {reference.EntityId?.ToString("N") ?? "unresolved"}";
        }

        return visBlock + node.Title;
    }

    private string FormatVisibilityBlock(string nodeId)
    {
        var vis = _controller.Session.Visibility;
        var local = vis.GetLocal(nodeId);
        var effective = vis.IsEffectivelyVisible(nodeId);
        var from = vis.InheritedFrom(nodeId);
        var summary = vis.SummarizeChildren(nodeId);
        var inheritLine = local == LocalVisibility.Inherit
            ? $"Inherited from: {from ?? "(root)"}\n"
            : "Explicit override\n";
        var mixed = summary == ChildVisibilitySummary.Mixed ? "Children: mixed\n" : "";
        return
            $"Local       {local}\n" +
            $"Effective   {(effective ? "Visible" : "Hidden")}\n" +
            inheritLine +
            mixed;
    }

    private void HideSelected()
    {
        var nodeId = SelectedNodeId();
        if (nodeId is null)
            return;
        _controller.Session.Visibility.SetLocal(nodeId, LocalVisibility.Hidden);
        _controller.Session.StatusText = $"hide:{nodeId}";
        RebuildTree();
    }

    private void ShowSelected()
    {
        var nodeId = SelectedNodeId();
        if (nodeId is null)
            return;
        _controller.Session.Visibility.SetLocal(nodeId, LocalVisibility.Visible);
        _controller.Session.StatusText = $"show:{nodeId}";
        RebuildTree();
    }

    private void IsolateSelected()
    {
        var nodeId = SelectedNodeId();
        if (nodeId is null)
            return;
        IsolateNode(nodeId);
    }

    private void IsolateNode(string nodeId)
    {
        EnsureRegistry();
        _controller.Session.Visibility.BeginIsolation(nodeId);
        _controller.Session.StatusText = $"isolate:{nodeId}";
        RebuildTree();
    }

    private void EnsureRegistry()
    {
        if (_controller.Session.Registry.Assemblies.Count == 0
            && _controller.Session.Document.Entities.Count > 0)
            _controller.Session.RebuildSemanticRegistry();
    }

    private void ClearIsolateAndHide()
    {
        if (_controller.Session.Visibility.IsolationActive)
            _controller.Session.Visibility.EndIsolation();
        else
            _controller.Session.Visibility.ClearAllOverrides();
        _controller.Session.StatusText = "visibility:cleared";
        RebuildTree();
    }

    private string? SelectedNodeId()
    {
        var session = _controller.Session;
        if (session.SelectedPartId is { } pid)
            return pid.Value;
        if (session.SelectedAssemblyId is { } aid)
            return aid.Value;
        return session.SelectedReferenceId;
    }

    private string BuildVisibilityTooltip(string nodeId, LocalVisibility local, bool effective)
    {
        var vis = _controller.Session.Visibility;
        var effLabel = effective ? "Visible" : "Hidden";
        if (local == LocalVisibility.Inherit)
        {
            var from = vis.InheritedFrom(nodeId) ?? "(root)";
            return $"Local: Inherit\nEffective: {effLabel}\nInherited from: {from}";
        }

        return $"Local: {local}\nEffective: {effLabel}\nExplicit override";
    }

    private void FocusSelected()
    {
        var center = ComputeSelectionCenter();
        if (center is null)
        {
            _controller.Session.StatusText = "focus:no-geometry";
            return;
        }

        _controller.Renderer.SetOrbitPose(center.Value, 42f, 0.95f, 0.35f);
        _controller.SetView(CalypsoViewMode.Orbit);
        _controller.Session.StatusText = $"focus:{center.Value.X:0.#},{center.Value.Y:0.#},{center.Value.Z:0.#}";
    }

    private enum DrawingKind { Assembly, Section, Detail }

    private void CreateDrawingForSelected(DrawingKind kind)
    {
        var nodeId = SelectedNodeId();
        if (nodeId is null)
        {
            _controller.Session.StatusText = "drawing:no-selection";
            return;
        }

        EnsureRegistry();
        DrawingRequest request = kind switch
        {
            DrawingKind.Section => DrawingTemplates.ArbitrarySection(
                nodeId,
                EstimateStationZ(nodeId)),
            DrawingKind.Detail => DrawingTemplates.AssemblyStandard(
                nodeId,
                principal: StandardViewOrientation.PlanSternLeft) with
            {
                Title = $"Detail · {nodeId}",
            },
            _ => DrawingTemplates.AssemblyStandard(nodeId),
        };

        // Capture subject IDs into the request — never consult viewport visibility.
        var result = UniversalDrawingEngine.Render(request, _controller.Session.Document);
        var outDir = Path.Combine(CalypsoProjectPaths.BlueprintsPackageDirectory, "_preview");
        Directory.CreateDirectory(outDir);
        var path = Path.Combine(outDir, $"{SanitizeFile(nodeId)}-{kind}.html");
        File.WriteAllText(path, result.Html);
        File.WriteAllText(Path.ChangeExtension(path, ".manifest.json"),
            UniversalDrawingEngine.RenderManifestJson(result.Manifest));
        _controller.Session.StatusText =
            result.Manifest.Ok
                ? $"drawing:{kind}:{nodeId} → {path}"
                : $"drawing:warn:{nodeId} geom={result.Resolved.Geometry.Count}";
    }

    private void GenerateDrawingPackage()
    {
        try
        {
            Blueprints.DrawingPackage.DrawingPackageEmitter.Emit();
            _controller.Session.StatusText = "drawing-package:emitted";
        }
        catch (Exception ex)
        {
            _controller.Session.StatusText = $"drawing-package:{ex.Message}";
        }
    }

    private float EstimateStationZ(string nodeId)
    {
        var center = ComputeSelectionCenter();
        if (center is { } c)
            return c.Z;
        _ = nodeId;
        return 34.5f;
    }

    private static string SanitizeFile(string id)
    {
        var chars = id.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
        return new string(chars);
    }

    private HashSet<Guid> SelectedEntityIds()
    {
        var session = _controller.Session;
        var reg = session.Registry;
        if (session.SelectedPartId is { } pid)
            return reg.CollectEntityIds(pid);
        if (session.SelectedAssemblyId is { } aid)
            return reg.CollectEntityIds(aid);
        if (session.SelectedReferenceId is { } rid
            && reg.TryGetReference(rid, out var reference)
            && reference.EntityId is { } eid)
            return [eid];
        return [];
    }

    private System.Numerics.Vector3? ComputeSelectionCenter()
    {
        var ids = SelectedEntityIds();
        if (ids.Count == 0)
            return null;
        float sx = 0, sy = 0, sz = 0;
        var n = 0;
        foreach (var e in _controller.Session.Document.Entities)
        {
            if (!ids.Contains(e.Id) || e.Points is not { Count: >= 1 })
                continue;
            var p = e.Points[0];
            if (p.Length < 3)
                continue;
            sx += p[0];
            sy += p[1];
            sz += p[2];
            n++;
        }

        return n == 0 ? null : new System.Numerics.Vector3(sx / n, sy / n, sz / n);
    }

    private static string Fmt(float? v) => v is { } x ? x.ToString("0.###") : "unresolved";

    private static TextBlock Hint(string text)
    {
        var t = new TextBlock
        {
            Text = text,
            FontSize = 10,
            Margin = new Thickness(0, 2, 0, 0),
        };
        GraphicalProfileBinding.Bind(t, TextBlock.ForegroundProperty, GraphicalProfile.MutedResourceKey);
        return t;
    }

    private static TextBlock Header(string text)
    {
        var t = new TextBlock
        {
            Text = text,
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, 8, 0, 2),
            FontSize = 12,
        };
        GraphicalProfileBinding.Bind(t, TextBlock.ForegroundProperty, GraphicalProfile.TextResourceKey);
        return t;
    }

    private static Button ActionBtn(string text, Action act)
    {
        var b = new Button
        {
            Content = text,
            Padding = new Thickness(6, 2),
            FontSize = 10,
            MinWidth = 48,
        };
        b.Click += (_, _) => act();
        return b;
    }
}
