using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using MobilityLab.Experiment;
using Novolis.Geopolitics.Core;

namespace MobilityLab;

internal sealed class MainWindow : Window
{
    static readonly Color Navy = Color.Parse("#0b1c2c");
    static readonly Color Panel = Color.Parse("#152a3d");
    static readonly Color Teal = Color.Parse("#3ecfaf");
    static readonly Color Copper = Color.Parse("#d4a04a");
    static readonly Color Ink = Color.Parse("#f0f4f8");
    static readonly Color Fail = Color.Parse("#e07070");
    static readonly Color PassMark = Color.Parse("#d4a04a");
    static readonly Color AlphaLine = Color.Parse("#f0845c");
    static readonly Color BetaLine = Color.Parse("#3ecfaf");
    static readonly Color ChartBg = Color.Parse("#0a1622");
    static readonly Color ShockLine = Color.Parse("#e8eef4");

    readonly NumericUpDown _alphaTax = MakeTaxBox(0.38);
    readonly NumericUpDown _betaTax = MakeTaxBox(0.14);
    readonly NumericUpDown _months = MakeIntBox(48, 12, 120);
    readonly NumericUpDown _shockMonth = MakeIntBox(12, 0, 60);
    readonly CheckBox _batteryMode = MakeCheck("Battery study (dose / placebo / ensemble)", true);
    readonly CheckBox _includeDose = MakeCheck("Dose grid", true);
    readonly CheckBox _includePlacebo = MakeCheck("Placebo twin", true);
    readonly CheckBox _includeEnsemble = MakeCheck("Seed ensemble", true);
    readonly CheckBox _warShock = MakeCheck("War shock (confounder)", false);

    readonly TextBlock _status = new()
    {
        Text = "HardPause — Battery mode default. Run the science battery.",
        Foreground = new SolidColorBrush(Ink),
        FontSize = 16,
        FontWeight = FontWeight.SemiBold,
        Margin = new Thickness(0, 4, 0, 0),
    };

    readonly TextBlock _mapText = MakeReadable(15, mono: true);
    readonly TextBlock _metricsText = MakeReadable(15);
    readonly StackPanel _scoreRows = new() { Spacing = 10 };
    readonly TextBlock _feedText = MakeReadable(14, mono: true);
    readonly TextBox _reportBox = new()
    {
        AcceptsReturn = true,
        TextWrapping = TextWrapping.Wrap,
        FontFamily = new FontFamily("Consolas, 'Courier New', monospace"),
        FontSize = 14,
        Foreground = new SolidColorBrush(Ink),
        Background = new SolidColorBrush(ChartBg),
        MinHeight = 280,
        MaxHeight = 480,
        IsReadOnly = true,
        BorderThickness = new Thickness(0),
    };

    readonly Canvas _popSeries = new() { Height = 170, Background = new SolidColorBrush(ChartBg), ClipToBounds = true };
    readonly Canvas _pushSeries = new() { Height = 120, Background = new SolidColorBrush(ChartBg), ClipToBounds = true };
    readonly Canvas _doseSeries = new() { Height = 140, Background = new SolidColorBrush(ChartBg), ClipToBounds = true };
    readonly TextBlock _popRaw = MakeReadable(13, mono: true);
    readonly TextBlock _pushRaw = MakeReadable(13, mono: true);
    readonly TextBlock _doseRaw = MakeReadable(13, mono: true);

    TaxMobilityWorld.Model? _model;
    BatteryResult? _battery;
    Queue<string> _log = new();
    ExperimentSpec _spec = ExperimentSpec.ShockDefault;
    string _lastMarkdown = "";

    public MainWindow()
    {
        Title = "MobilityLab — science battery";
        Width = 1320;
        Height = 980;
        MinWidth = 1024;
        MinHeight = 720;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Navy);
        Content = BuildLayout();
        ResetExperiment();
    }

    Control BuildLayout()
    {
        var brand = new TextBlock
        {
            Text = "MobilityLab",
            FontSize = 42,
            FontWeight = FontWeight.Bold,
            Foreground = new SolidColorBrush(Copper),
            FontFamily = new FontFamily("Georgia, 'Times New Roman', serif"),
        };

        var hypothesis = new TextBlock
        {
            Text =
                "Science battery: shock design + ATT vs CF, dose–response, high-tax placebo twin, multi-seed ensemble, " +
                "and fiscal/production estimands. Formulas stay in Wave 1 kernels — this harness explores the policy surface.\n" +
                "Battery Run is the primary path; Single steps one treated world.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Ink),
            FontSize = 16,
            LineHeight = 24,
            Margin = new Thickness(0, 10, 0, 4),
            MaxWidth = 1180,
        };

        var controls = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            Children =
            {
                Labeled("α tax", _alphaTax),
                Labeled("β tax", _betaTax),
                Labeled("Months", _months),
                Labeled("Shock M", _shockMonth),
                _batteryMode,
                _includeDose,
                _includePlacebo,
                _includeEnsemble,
                _warShock,
                PrimaryButton("Run", (_, _) => RunStudy()),
                SecondaryButton("Step (single)", (_, _) => StepOnce()),
                SecondaryButton("Reset", (_, _) => ResetExperiment()),
                PrimaryButton("Copy markdown report", async (_, _) => await CopyReportAsync()),
            },
        };

        var charts = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                ChartBlock("Population (α copper · β teal) — vertical = shock", _popSeries, _popRaw),
                ChartBlock("Emigration pressure (shared 0–1)", _pushSeries, _pushRaw),
                ChartBlock("Dose curve: ATT pop % vs α tax", _doseSeries, _doseRaw),
            },
        };

        var mapPanel = Section(
            "Province map (primary)",
            new ScrollViewer
            {
                MaxHeight = 360,
                Content = _mapText,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            });

        var body = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("2.2*,*"),
            Margin = new Thickness(0, 12, 0, 0),
        };
        body.Children.Add(charts);
        Grid.SetColumn(charts, 0);
        body.Children.Add(mapPanel);
        Grid.SetColumn(mapPanel, 1);
        mapPanel.Margin = new Thickness(14, 0, 0, 0);

        var evidence = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            Margin = new Thickness(0, 14, 0, 0),
        };
        var left = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                Section("Study scorecard", _scoreRows),
                Section("Effect sizes", _metricsText),
            },
        };
        var right = Section(
            "Month feed (recent)",
            new ScrollViewer
            {
                MaxHeight = 280,
                Content = _feedText,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            });
        evidence.Children.Add(left);
        Grid.SetColumn(left, 0);
        evidence.Children.Add(right);
        Grid.SetColumn(right, 1);
        right.Margin = new Thickness(14, 0, 0, 0);

        var reportSection = Section("Markdown report — Copy or select-all", _reportBox);
        reportSection.Margin = new Thickness(0, 14, 0, 0);

        return new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new StackPanel
            {
                Margin = new Thickness(28, 24, 28, 32),
                Spacing = 6,
                Children = { brand, hypothesis, controls, _status, body, evidence, reportSection },
            },
        };
    }

    Border ChartBlock(string title, Canvas canvas, TextBlock raw)
    {
        var host = new Border
        {
            Background = new SolidColorBrush(Panel),
            Padding = new Thickness(12),
            CornerRadius = new CornerRadius(3),
            Child = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    new TextBlock
                    {
                        Text = title,
                        FontSize = 15,
                        FontWeight = FontWeight.SemiBold,
                        Foreground = new SolidColorBrush(Ink),
                    },
                    canvas,
                    raw,
                },
            },
        };
        host.SizeChanged += (_, e) =>
        {
            canvas.Width = Math.Max(120, e.NewSize.Width - 24);
            DrawAllCharts();
        };
        return host;
    }

    static Border Section(string title, Control child) => new()
    {
        Background = new SolidColorBrush(Panel),
        Padding = new Thickness(14),
        CornerRadius = new CornerRadius(3),
        Child = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                new TextBlock
                {
                    Text = title,
                    FontSize = 17,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = new SolidColorBrush(Copper),
                },
                child,
            },
        },
    };

    static StackPanel Labeled(string label, Control control) => new()
    {
        Orientation = Orientation.Horizontal,
        Spacing = 8,
        Margin = new Thickness(0, 0, 16, 10),
        VerticalAlignment = VerticalAlignment.Center,
        Children =
        {
            new TextBlock
            {
                Text = label,
                FontSize = 15,
                FontWeight = FontWeight.SemiBold,
                Foreground = new SolidColorBrush(Ink),
                VerticalAlignment = VerticalAlignment.Center,
            },
            control,
        },
    };

    static NumericUpDown MakeTaxBox(double value) => new()
    {
        Minimum = 0.05m,
        Maximum = 0.55m,
        Increment = 0.01m,
        Value = (decimal)value,
        Width = 96,
        FormatString = "0.00",
        FontSize = 15,
        MinHeight = 36,
    };

    static NumericUpDown MakeIntBox(int value, int min, int max) => new()
    {
        Minimum = min,
        Maximum = max,
        Increment = 1m,
        Value = value,
        Width = 88,
        FormatString = "0",
        FontSize = 15,
        MinHeight = 36,
    };

    static CheckBox MakeCheck(string content, bool on) => new()
    {
        Content = content,
        IsChecked = on,
        Foreground = new SolidColorBrush(Ink),
        FontSize = 14,
        MinHeight = 36,
        Margin = new Thickness(0, 0, 12, 10),
        VerticalContentAlignment = VerticalAlignment.Center,
    };

    static Button PrimaryButton(string text, EventHandler<Avalonia.Interactivity.RoutedEventArgs> onClick)
    {
        var b = new Button
        {
            Content = text,
            Margin = new Thickness(0, 0, 10, 10),
            Padding = new Thickness(18, 10),
            Background = new SolidColorBrush(Copper),
            Foreground = new SolidColorBrush(Navy),
            FontWeight = FontWeight.Bold,
            FontSize = 15,
            MinHeight = 40,
        };
        b.Click += onClick;
        return b;
    }

    static Button SecondaryButton(string text, EventHandler<Avalonia.Interactivity.RoutedEventArgs> onClick)
    {
        var b = new Button
        {
            Content = text,
            Margin = new Thickness(0, 0, 10, 10),
            Padding = new Thickness(16, 10),
            Background = new SolidColorBrush(Color.Parse("#1e3a52")),
            Foreground = new SolidColorBrush(Ink),
            FontSize = 15,
            MinHeight = 40,
        };
        b.Click += onClick;
        return b;
    }

    static TextBlock MakeReadable(double size, bool mono = false) => new()
    {
        FontFamily = mono
            ? new FontFamily("Consolas, 'Courier New', monospace")
            : new FontFamily("Segoe UI, Candara, Calibri, sans-serif"),
        FontSize = size,
        Foreground = new SolidColorBrush(Ink),
        TextWrapping = TextWrapping.Wrap,
        LineHeight = size + 6,
    };

    StudySpec ReadStudy() => new(
        Months: (int)(_months.Value ?? 48m),
        AlphaTax: (double)(_alphaTax.Value ?? 0.38m),
        BetaTax: (double)(_betaTax.Value ?? 0.14m),
        GammaTax: 0.12,
        BaselineMonths: (int)(_shockMonth.Value ?? 12m),
        ShockMonth: (int)(_shockMonth.Value ?? 12m),
        WarShockOn: _warShock.IsChecked == true,
        AgentsEnabled: false,
        IncludeDose: _includeDose.IsChecked == true,
        IncludePlacebo: _includePlacebo.IsChecked == true,
        IncludeEnsemble: _includeEnsemble.IsChecked == true,
        Seeds: StudySpec.Default.Seeds,
        DoseGrid: StudySpec.Default.DoseGrid);

    ExperimentSpec ReadSingleSpec()
    {
        var shock = (int)(_shockMonth.Value ?? 12m);
        return new ExperimentSpec(
            AlphaTax: (double)(_alphaTax.Value ?? 0.38m),
            BetaTax: (double)(_betaTax.Value ?? 0.14m),
            GammaTax: 0.12,
            Months: (int)(_months.Value ?? 48m),
            Seed: 42,
            WarShockOn: _warShock.IsChecked == true,
            AgentsEnabled: false,
            BaselineMonths: shock,
            ShockMonth: shock);
    }

    void ResetExperiment()
    {
        _spec = ReadSingleSpec();
        _model = ExperimentHost.CreateFresh(_spec);
        _battery = null;
        _log = new Queue<string>();
        _log.Enqueue($"Reset · battery={_batteryMode.IsChecked == true} shockM={_spec.ShockMonth}");
        _status.Text = "HardPause — ready.";
        _lastMarkdown = "";
        _reportBox.Text = "(Run the science battery to generate a markdown report.)";
        _scoreRows.Children.Clear();
        _metricsText.Text = "";
        _doseRaw.Text = "";
        DrawMap();
        DrawAllCharts();
        RebuildFeed();
    }

    void RunStudy()
    {
        if (_batteryMode.IsChecked == true)
            RunBattery();
        else
            RunSingle();
    }

    void RunBattery()
    {
        var study = ReadStudy();
        _status.Text = "Running science battery…";
        var sw = Stopwatch.StartNew();
        _battery = BatteryRunner.Run(study);
        sw.Stop();
        _model = _battery.Primary.Model;
        _spec = _battery.Primary.Spec;
        _log = new Queue<string>();
        _log.Enqueue(
            $"Battery done {sw.Elapsed.TotalSeconds:0.00}s · checks {_battery.PassCount}/{_battery.CheckCount}");
        _status.Text =
            $"Battery complete — {_battery.PassCount}/{_battery.CheckCount} " +
            $"{(_battery.AllPass ? "PASS" : "MIXED")}.";
        _status.Foreground = new SolidColorBrush(_battery.AllPass ? PassMark : Ink);
        ApplyBattery(_battery, sw.Elapsed);
    }

    void RunSingle()
    {
        _spec = ReadSingleSpec();
        var sw = Stopwatch.StartNew();
        var host = ExperimentHost.Run(_spec);
        sw.Stop();
        _model = host.Model;
        _battery = null;
        _log = host.Log;
        _status.Text =
            $"Single complete — {host.Result.PassCount}/{host.Result.CheckCount} " +
            $"{(host.Result.AllPass ? "PASS" : "MIXED")}.";
        _status.Foreground = new SolidColorBrush(host.Result.AllPass ? PassMark : Ink);
        ApplySingle(host.Result, sw.Elapsed);
    }

    void StepOnce()
    {
        if (_model is null)
            ResetExperiment();
        if (_model is null)
            return;

        if (_model.History.Months.Count == 0)
        {
            _spec = ReadSingleSpec();
            _model = ExperimentHost.CreateFresh(_spec);
            TaxMobilityWorld.LockTreatmentTaxes(_model);
        }

        var monthIndex = _model.History.Months.Count;
        if (monthIndex >= _spec.Months)
        {
            var result = ExperimentHost.EvaluateAgainstCounterfactual(_model);
            ApplySingle(result, TimeSpan.Zero);
            return;
        }

        TaxMobilityMonth.MaybeApplyWarShock(_model, _log, monthIndex);
        TaxMobilityMonth.Advance(_model, _log);
        _status.Text = $"Stepped to M{_model.History.Months.Count} / {_spec.Months}";
        var live = ExperimentHost.EvaluateAgainstCounterfactual(_model);
        ApplySingle(live, null);
    }

    void ApplyBattery(BatteryResult battery, TimeSpan elapsed)
    {
        RebuildScorecard(battery.StudyChecks, battery.PassCount, battery.CheckCount, battery.AllPass);
        var e = battery.Primary.Result.Effects;
        var a = battery.Aggregates;
        _metricsText.Text =
            $"ATT pop %  {e.AttAlphaPopPct:+0.0%;-0.0%}   DID {e.DidPopGrowth:+0.0%;-0.0%}   ATT push {e.AttMeanPush:+0.000;-0.000}\n" +
            $"ATT tax rev {e.AttCumTaxRevenue:+0.0;-0.0}   ATT prod {e.AttMeanProduction:+0.00;-0.00}   absorb {e.GammaAbsorbShare:0.0%}\n" +
            $"Placebo DID {a.PlaceboDid:+0.0%;-0.0%}   dose mono {a.DoseMonotonic}   tax@-5% {a.TaxAtAttMinus5Pct?.ToString("0.00") ?? "n/a"}\n" +
            $"Ensemble ATT% mean/min/max  {a.EnsembleMeanAttPct:+0.0%;-0.0%} / {a.EnsembleMinAttPct:+0.0%;-0.0%} / {a.EnsembleMaxAttPct:+0.0%;-0.0%}\n" +
            (e.HasEventStudy
                ? $"Event pre DID {e.PreShockDidGrowth:+0.0%;-0.0%}   pre/post net mig {e.PreShockMeanNetMig:0} / {e.PostShockMeanNetMig:0}"
                : "Event study: n/a (static)");
        DrawMap();
        DrawAllCharts();
        RebuildFeed();
        _lastMarkdown = MarkdownReport.BuildBattery(battery, elapsed);
        _reportBox.Text = _lastMarkdown;
    }

    void ApplySingle(ExperimentResult result, TimeSpan? elapsed)
    {
        RebuildScorecard(result.Checks, result.PassCount, result.CheckCount, result.AllPass);
        var e = result.Effects;
        _metricsText.Text =
            $"ATT pop %  {e.AttAlphaPopPct:+0.0%;-0.0%}   DID {e.DidPopGrowth:+0.0%;-0.0%}\n" +
            $"ATT push {e.AttMeanPush:+0.000;-0.000}   ATT tax {e.AttCumTaxRevenue:+0.0;-0.0}   ATT prod {e.AttMeanProduction:+0.00;-0.00}\n" +
            $"α {result.AlphaPopStart:0} → {result.AlphaPopEnd:0}   β {result.BetaPopStart:0} → {result.BetaPopEnd:0}";
        DrawMap();
        DrawAllCharts();
        RebuildFeed();
        if (_model is not null && _model.History.Months.Count > 0)
        {
            _lastMarkdown = MarkdownReport.Build(result, _model, elapsed);
            _reportBox.Text = _lastMarkdown;
        }
    }

    void RebuildScorecard(IReadOnlyList<CouplingCheck> checks, int pass, int total, bool allPass)
    {
        _scoreRows.Children.Clear();
        _scoreRows.Children.Add(new TextBlock
        {
            Text = $"Checks {pass}/{total}" + (allPass ? " — all PASS" : " — review FAILs"),
            FontSize = 18,
            FontWeight = FontWeight.Bold,
            Foreground = new SolidColorBrush(allPass ? PassMark : Ink),
            Margin = new Thickness(0, 0, 0, 4),
        });

        foreach (var c in checks)
        {
            _scoreRows.Children.Add(new Border
            {
                Background = new SolidColorBrush(Color.Parse("#0f2030")),
                Padding = new Thickness(12, 10),
                CornerRadius = new CornerRadius(3),
                Child = new StackPanel
                {
                    Spacing = 4,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = $"{(c.Pass ? "PASS" : "FAIL")}  ·  {c.Claim}",
                            FontSize = 16,
                            FontWeight = FontWeight.Bold,
                            Foreground = new SolidColorBrush(c.Pass ? PassMark : Fail),
                        },
                        new TextBlock
                        {
                            Text = c.Detail,
                            FontSize = 14,
                            Foreground = new SolidColorBrush(Ink),
                            TextWrapping = TextWrapping.Wrap,
                        },
                    },
                },
            });
        }
    }

    void RebuildFeed()
    {
        var recent = _log.Reverse().Take(16).Reverse();
        _feedText.Text = string.Join('\n', recent);
    }

    async Task CopyReportAsync()
    {
        if (string.IsNullOrWhiteSpace(_lastMarkdown))
        {
            _status.Text = "No report yet — press Run first.";
            return;
        }

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
        {
            _reportBox.Focus();
            _reportBox.SelectAll();
            return;
        }

        await clipboard.SetTextAsync(_lastMarkdown);
        _status.Text = "Markdown report copied to clipboard.";
        _reportBox.Focus();
        _reportBox.SelectAll();
    }

    void DrawMap()
    {
        if (_model is null)
        {
            _mapText.Text = "";
            return;
        }

        var lines = _model.World.Provinces
            .OrderBy(p => p.Id.Value)
            .Select(p =>
            {
                var home = PolityTag(p.HomePolityId);
                var own = PolityTag(p.OwnerId);
                var occ = p.OwnerId == p.HomePolityId ? "" : $"  OCC {home}->{own}";
                return $"{p.Name,-4}  owner {own}   pop {p.Population,10:0}{occ}";
            });
        _mapText.Text = string.Join('\n', lines);
    }

    static string PolityTag(PolityId id) => id.Value switch { 0 => "α", 1 => "β", _ => "γ" };

    void DrawAllCharts()
    {
        DrawPopPush();
        DrawDose();
    }

    void DrawPopPush()
    {
        _popSeries.Children.Clear();
        _pushSeries.Children.Clear();
        if (_model is null || _model.History.Months.Count < 2)
        {
            _popRaw.Text = "Raw: (need >=2 months)";
            _pushRaw.Text = "";
            return;
        }

        var months = _model.History.Months;
        var aPop = months.Select(m => m.Alpha.Population).ToList();
        var bPop = months.Select(m => m.Beta.Population).ToList();
        var aPush = months.Select(m => m.Alpha.EmigrationPressure).ToList();
        var bPush = months.Select(m => m.Beta.EmigrationPressure).ToList();

        DrawShared(_popSeries, aPop, bPop, AlphaLine, BetaLine, shockMonth: _model.Spec.ShockMonth);
        DrawShared(_pushSeries, aPush, bPush, AlphaLine, BetaLine, fixedMin: 0, fixedMax: 1,
            shockMonth: _model.Spec.ShockMonth);

        var last = months[^1];
        _popRaw.Text =
            $"Raw end  α {last.Alpha.Population:0}   β {last.Beta.Population:0}   γ {last.Gamma.Population:0}   tax {last.Alpha.HouseholdTaxRate:0.00}";
        _pushRaw.Text =
            $"Raw end  α push {last.Alpha.EmigrationPressure:0.00}   β push {last.Beta.EmigrationPressure:0.00}   α App {last.Alpha.Approval:0.00}";
    }

    void DrawDose()
    {
        _doseSeries.Children.Clear();
        if (_battery is null || _battery.DoseCurve.Count < 2)
        {
            _doseRaw.Text = _battery is null ? "Dose: run Battery to plot ATT pop % vs tax." : "Dose: need >=2 grid points.";
            return;
        }

        var xs = _battery.DoseCurve.Select(d => d.Tax).ToList();
        var ys = _battery.DoseCurve.Select(d => d.AttPopPct).ToList();
        DrawXY(_doseSeries, xs, ys, Copper);
        var a = _battery.Aggregates;
        _doseRaw.Text =
            $"ATT@0.22 {a.DoseAttAt022:+0.0%;-0.0%}   ATT@0.45 {a.DoseAttAt045:+0.0%;-0.0%}   " +
            $"mono={a.DoseMonotonic}   tax@-5%={a.TaxAtAttMinus5Pct?.ToString("0.00") ?? "n/a"}";
    }

    void DrawShared(
        Canvas canvas,
        IReadOnlyList<double> a,
        IReadOnlyList<double> b,
        Color aColor,
        Color bColor,
        double? fixedMin = null,
        double? fixedMax = null,
        int shockMonth = 0)
    {
        var w = canvas.Bounds.Width > 10 ? canvas.Bounds.Width : canvas.Width;
        var h = canvas.Height;
        if (w < 40 || h < 40 || a.Count < 2)
            return;

        var min = fixedMin ?? Math.Min(a.Min(), b.Min());
        var max = fixedMax ?? Math.Max(a.Max(), b.Max());
        var span = Math.Max(1e-9, max - min);

        canvas.Children.Add(MakeGridLine(w, h * 0.25));
        canvas.Children.Add(MakeGridLine(w, h * 0.5));
        canvas.Children.Add(MakeGridLine(w, h * 0.75));
        canvas.Children.Add(MakePoly(a, aColor, w, h, min, span, 3));
        canvas.Children.Add(MakePoly(b, bColor, w, h, min, span, 3));

        if (shockMonth > 0 && shockMonth <= a.Count)
        {
            const double pad = 10;
            var x = pad + (w - 2 * pad) * (shockMonth - 1) / Math.Max(1, a.Count - 1);
            canvas.Children.Add(new Avalonia.Controls.Shapes.Line
            {
                StartPoint = new Point(x, pad),
                EndPoint = new Point(x, h - pad),
                Stroke = new SolidColorBrush(ShockLine),
                StrokeThickness = 1.5,
                StrokeDashArray = [4, 3],
                Opacity = 0.7,
            });
        }
    }

    void DrawXY(Canvas canvas, IReadOnlyList<double> xs, IReadOnlyList<double> ys, Color color)
    {
        var w = canvas.Bounds.Width > 10 ? canvas.Bounds.Width : canvas.Width;
        var h = canvas.Height;
        if (w < 40 || h < 40 || xs.Count < 2)
            return;

        var xmin = xs.Min();
        var xmax = xs.Max();
        var ymin = Math.Min(0, ys.Min());
        var ymax = Math.Max(0, ys.Max());
        var xspan = Math.Max(1e-9, xmax - xmin);
        var yspan = Math.Max(1e-9, ymax - ymin);
        const double pad = 10;

        // zero line
        var y0 = h - pad - (0 - ymin) / yspan * (h - 2 * pad);
        canvas.Children.Add(new Avalonia.Controls.Shapes.Line
        {
            StartPoint = new Point(pad, y0),
            EndPoint = new Point(w - pad, y0),
            Stroke = new SolidColorBrush(Color.Parse("#24384c")),
            StrokeThickness = 1,
        });

        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            for (var i = 0; i < xs.Count; i++)
            {
                var x = pad + (w - 2 * pad) * (xs[i] - xmin) / xspan;
                var y = h - pad - (ys[i] - ymin) / yspan * (h - 2 * pad);
                if (i == 0)
                    ctx.BeginFigure(new Point(x, y), false);
                else
                    ctx.LineTo(new Point(x, y));
            }
        }

        canvas.Children.Add(new Avalonia.Controls.Shapes.Path
        {
            Data = geo,
            Stroke = new SolidColorBrush(color),
            StrokeThickness = 3,
            StrokeLineCap = PenLineCap.Round,
            StrokeJoin = PenLineJoin.Round,
        });
    }

    static Avalonia.Controls.Shapes.Line MakeGridLine(double w, double y) => new()
    {
        StartPoint = new Point(0, y),
        EndPoint = new Point(w, y),
        Stroke = new SolidColorBrush(Color.Parse("#24384c")),
        StrokeThickness = 1,
    };

    static Avalonia.Controls.Shapes.Path MakePoly(
        IReadOnlyList<double> series,
        Color color,
        double w,
        double h,
        double min,
        double span,
        double thickness)
    {
        const double pad = 10;
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            for (var i = 0; i < series.Count; i++)
            {
                var x = pad + (w - 2 * pad) * i / (series.Count - 1);
                var t = (series[i] - min) / span;
                var y = h - pad - t * (h - 2 * pad);
                if (i == 0)
                    ctx.BeginFigure(new Point(x, y), false);
                else
                    ctx.LineTo(new Point(x, y));
            }
        }

        return new Avalonia.Controls.Shapes.Path
        {
            Data = geo,
            Stroke = new SolidColorBrush(color),
            StrokeThickness = thickness,
            StrokeLineCap = PenLineCap.Round,
            StrokeJoin = PenLineJoin.Round,
        };
    }
}
