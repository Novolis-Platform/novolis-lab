using Novolis.Rendering.TwoD;

namespace TopDownDoom.Art;

internal static class DirectionalArtLoader
{
    private static readonly string[] Suffixes = ["down", "up", "right", "up_right", "down_right"];

    public static DirectionalClips? TryLoadFolder(
        TwoDTextureRegistry registry,
        string folder,
        float worldHalfHeight,
        float idleFps = 8f,
        float moveFps = 11f)
    {
        if (!Directory.Exists(folder))
        {
            return null;
        }

        var clips = new DirectionalClips { WorldHalfHeight = worldHalfHeight };
        var any = false;
        foreach (var suffix in Suffixes)
        {
            var idle = CharacterAtlasBuilder.TryBuildClipFromExactPrefix(registry, folder, $"idle_{suffix}", idleFps);
            if (idle is not null)
            {
                clips.AddIdle(suffix, idle);
                any = true;
            }

            var move = CharacterAtlasBuilder.TryBuildClipFromExactPrefix(registry, folder, $"jump_{suffix}", moveFps)
                ?? CharacterAtlasBuilder.TryBuildClipFromExactPrefix(registry, folder, $"walk_{suffix}", moveFps);
            if (move is not null)
            {
                clips.AddMove(suffix, move);
            }
        }

        return any ? clips : null;
    }
}
