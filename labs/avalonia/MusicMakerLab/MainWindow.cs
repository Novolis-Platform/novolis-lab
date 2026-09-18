using Avalonia.Controls;
using Avalonia.Media;
using Novolis.Audio.Catalog;
using Novolis.Audio.Edit;
using Novolis.Audio.Midi;
using Novolis.Avalonia.Audio;

namespace MusicMakerLab;

internal sealed class MainWindow : Window
{
    readonly AudioEditWorkspace _arrangement;
    readonly MidiPianoWorkspace _piano;
    readonly MediaCatalogWorkspace _catalog;

    public MainWindow()
    {
        Title = "Music Maker Lab";
        Width = 1360;
        Height = 860;
        Background = AudioEditPalette.Pane;

        var project = FullDemoBuilder.Build();
        _arrangement = new AudioEditWorkspace(project)
        {
            HeaderTitle = "Arrangement — free library + By The Sword preview",
        };
        _piano = new MidiPianoWorkspace(musicProject: project)
        {
            HeaderTitle = "Orchestral Score — demos + free MIDI",
        };
        _catalog = new MediaCatalogWorkspace();
        _catalog.ScoreProduced += score =>
        {
            _piano.ApplyScore(score, toast: "From catalog explore");
            Console.WriteLine($"Catalog → score: {score.Title}");
        };
        _catalog.ItemDownloaded += (item, path) =>
        {
            Console.WriteLine($"Cached {item.Title} → {path}");
            if (item.Kind == MediaKind.Audio)
            {
                try
                {
                    var pcm = DecodePcmTransformer.Decode(path, 44_100, TimeSpan.FromSeconds(12));
                    if (pcm is not null
                        && !_arrangement.Project.Assets.Any(a =>
                            string.Equals(a.Name, item.Title, StringComparison.OrdinalIgnoreCase)))
                    {
                        AudioEditOps.AddPcm(_arrangement.Project, item.Title, pcm);
                        Avalonia.Threading.Dispatcher.UIThread.Post(() => _arrangement.Refresh());
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex.Message);
                }
            }
        };

        WireFreeMedia(_piano);
        _piano.RefreshFreeMidiList();

        var tabs = new TabControl
        {
            Background = AudioEditPalette.Pane,
        };
        tabs.Items.Add(new TabItem
        {
            Header = "Catalog",
            Content = _catalog,
            Foreground = Brushes.White,
        });
        tabs.Items.Add(new TabItem
        {
            Header = "Arrangement",
            Content = _arrangement,
            Foreground = Brushes.White,
        });
        tabs.Items.Add(new TabItem
        {
            Header = "Orchestral Score",
            Content = _piano,
            Foreground = Brushes.White,
        });
        tabs.SelectionChanged += (_, _) =>
        {
            if (tabs.SelectedIndex == 1)
                _arrangement.Refresh();
            else if (tabs.SelectedIndex == 2)
                _piano.Focus();
        };

        Content = tabs;
        Closed += (_, _) =>
        {
            _arrangement.Dispose();
            _piano.Dispose();
        };
    }

    void WireFreeMedia(MidiPianoWorkspace piano)
    {
        piano.SyncFreeLibrary = () =>
        {
            FreeMediaLibrary.EnsureCached(log: msg => Console.WriteLine("  " + msg));
            var n = FreeMediaLibrary.ImportAudioIntoProject(_arrangement.Project);
            Console.WriteLine($"Imported {n} free SFX.");
            Avalonia.Threading.Dispatcher.UIThread.Post(() => _arrangement.Refresh());
        };

        piano.ListFreeMidiTitles = () =>
            FreeMediaLibrary.CachedMidiFiles().Select(x => x.Entry.Title).ToList();

        piano.ResolveFreeMidi = title =>
        {
            var hit = FreeMediaCatalog.MidiEntries.FirstOrDefault(e =>
                string.Equals(e.Title, title, StringComparison.OrdinalIgnoreCase));
            return hit is null ? null : FreeMediaLibrary.LoadMidiScore(hit);
        };

        piano.CreateFreeAudioSketch = () =>
        {
            foreach (var id in new[] { "mixkit-2563", "mixkit-3003", "mixkit-2004", "mixkit-2573" })
            {
                var entry = FreeMediaCatalog.All.FirstOrDefault(e => e.Id == id);
                if (entry is null)
                    continue;
                var score = FreeMediaLibrary.SketchFromFreeAudio(entry);
                if (score is not null)
                    return score;
            }

            foreach (var entry in FreeMediaCatalog.AudioEntries)
            {
                var score = FreeMediaLibrary.SketchFromFreeAudio(entry);
                if (score is not null)
                    return score;
            }

            return null;
        };
    }
}
