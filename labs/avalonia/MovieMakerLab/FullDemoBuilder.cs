using Novolis.Avalonia.Video;
using Novolis.Video.Edit;

namespace MovieMakerLab;

/// <summary>
/// Builds a demo with a filled media library; storyboard starts with two clips so transitions
/// and "add from library" are usable immediately.
/// </summary>
internal static class FullDemoBuilder
{
    public static (MovieProject Project, string AssetRoot) Build()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Novolis",
            "MovieMakerLab",
            "demo-assets");
        Directory.CreateDirectory(root);

        var project = new MovieProject("Movie Maker Full Demo", 640, 360);

        var skyPath = Path.Combine(root, "sky.bmp");
        var hillsPath = Path.Combine(root, "hills.bmp");
        var duskPath = Path.Combine(root, "dusk.bmp");
        BmpFile.WriteBgraFrame(skyPath, BmpFile.CreateGradient(640, 360, new Rgba8(40, 120, 200), new Rgba8(220, 240, 255)));
        BmpFile.WriteBgraFrame(hillsPath, BmpFile.CreateGradient(640, 360, new Rgba8(30, 90, 50), new Rgba8(120, 160, 70)));
        BmpFile.WriteBgraFrame(duskPath, BmpFile.CreateGradient(640, 360, new Rgba8(120, 40, 60), new Rgba8(40, 20, 80)));

        // Library inventory
        var sky = MovieEditOps.AddImage(project, "Sky still", skyPath, TimeSpan.FromSeconds(3));
        var hills = MovieEditOps.AddImage(project, "Hills still", hillsPath, TimeSpan.FromSeconds(3));
        var dusk = MovieEditOps.AddImage(project, "Dusk still", duskPath, TimeSpan.FromSeconds(2.5));
        MovieEditOps.AddColorCard(project, "End card", new Rgba8(20, 28, 40), TimeSpan.FromSeconds(2));

        var bedPath = Path.Combine(root, "bed-tone.wav");
        var stingPath = Path.Combine(root, "sting-tone.wav");
        MovieEditOps.AddToneAudio(project, "Bed tone A3", bedPath, 220, TimeSpan.FromSeconds(12));
        MovieEditOps.AddToneAudio(project, "Sting E4", stingPath, 329.63, TimeSpan.FromSeconds(1.2));

        // Starter storyboard — user adds dusk / end card / audio from the library
        var c0 = MovieEditOps.AppendToStoryboard(project, sky);
        MovieEditOps.AppendToStoryboard(project, hills);
        MovieEditOps.SetOutTransition(c0, TransitionKind.Fade, TimeSpan.FromSeconds(0.8));
        _ = dusk; // remains in the library for Add to storyboard

        MovieEditOps.AddTextOverlay(
            project,
            "Welcome aboard",
            TimeSpan.FromSeconds(0.4),
            TimeSpan.FromSeconds(2.2),
            new Rgba8(255, 245, 220),
            anchorY: 0.78);

        return (project, root);
    }

    public static void WarmStills(MovieEditWorkspace workspace)
    {
        foreach (var asset in workspace.Project.Assets)
        {
            if (asset.Kind != MediaKind.Image || asset.Path is null || !File.Exists(asset.Path))
                continue;
            var frame = BmpFile.ReadToBgra(asset.Path, workspace.Project.Width, workspace.Project.Height);
            workspace.RegisterStill(asset.Id, frame);
        }

        workspace.Refresh();
    }
}
