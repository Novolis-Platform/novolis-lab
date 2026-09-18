using Novolis.Math.Geometry;
using Novolis.Rendering.TwoD;
using Novolis.Rendering.Backends.TwoD.Silk;

namespace TopDownDoom.Art;

internal static class CharacterAtlasBuilder
{
    public static TwoDAnimationClip? TryBuildClipFromNamePrefix(
        TwoDTextureRegistry registry,
        string folder,
        string namePrefix,
        float framesPerSecond)
    {
        if (!Directory.Exists(folder))
        {
            return null;
        }

        var files = Directory.EnumerateFiles(folder, "*.png", SearchOption.TopDirectoryOnly)
            .Where(p => MatchesNamePrefix(Path.GetFileName(p), namePrefix))
            .OrderBy(static p => p, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return BuildClipFromFiles(registry, files, framesPerSecond, namePrefix);
    }

    public static TwoDAnimationClip? TryBuildClipFromExactPrefix(
        TwoDTextureRegistry registry,
        string folder,
        string exactPrefix,
        float framesPerSecond) =>
        TryBuildClipFromNamePrefix(registry, folder, exactPrefix, framesPerSecond);

    private static bool MatchesNamePrefix(string fileName, string prefix)
    {
        var baseName = Path.GetFileNameWithoutExtension(fileName);
        if (!baseName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var rest = baseName[prefix.Length..];
        return rest.Length == 0 || rest[0] is ' ' or '(';
    }

    public static TwoDAnimationClip? TryBuildClipFromFolder(
        TwoDTextureRegistry registry,
        string folder,
        float framesPerSecond)
    {
        if (!Directory.Exists(folder))
        {
            return null;
        }

        var files = Directory.EnumerateFiles(folder, "*.png", SearchOption.TopDirectoryOnly)
            .OrderBy(static p => p, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return BuildClipFromFiles(registry, files, framesPerSecond, Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar)));
    }

    private static TwoDAnimationClip? BuildClipFromFiles(
        TwoDTextureRegistry registry,
        string[] files,
        float framesPerSecond,
        string label)
    {
        if (files.Length == 0)
        {
            return null;
        }

        var frames = new List<(int W, int H, Rgba32[] Pixels)>(files.Length);
        foreach (var file in files)
        {
            var texId = SilkTwoDPngLoader.LoadPng(registry, file);
            var info = registry.GetInfo(texId);
            var pixels = new Rgba32[info.Width * info.Height];
            registry.CopyPixels(texId, pixels, out _, out _);
            frames.Add((info.Width, info.Height, pixels));
        }

        var frameW = frames.Max(f => f.W);
        var frameH = frames.Max(f => f.H);
        var atlasW = frameW * frames.Count;
        var atlas = new Rgba32[atlasW * frameH];
        for (var i = 0; i < frames.Count; i++)
        {
            var (w, h, px) = frames[i];
            var ox = i * frameW + (frameW - w) / 2;
            var oy = (frameH - h) / 2;
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var src = px[y * w + x];
                    if (src.A < 8)
                    {
                        continue;
                    }

                    atlas[(oy + y) * atlasW + ox + x] = src;
                }
            }
        }

        var atlasId = registry.Register(atlas, atlasW, frameH, label);
        var sheet = new TwoDSpriteSheet(atlasId, frameW, frameH, atlasW, frameH);
        var indices = Enumerable.Range(0, frames.Count).ToArray();
        return new TwoDAnimationClip(sheet, indices, framesPerSecond);
    }
}
