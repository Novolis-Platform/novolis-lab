using Novolis.Rendering.TwoD;

namespace TopDownDoom.Art;

/// <summary>8-way top-down clips — never rotate these quads; pick the facing frame instead.</summary>
internal sealed class DirectionalClips
{
    private readonly Dictionary<string, TwoDAnimationClip> _idle = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TwoDAnimationClip> _move = new(StringComparer.OrdinalIgnoreCase);

    public float WorldHalfHeight { get; init; } = 0.62f;
    public TwoDAnimationClip? ShootOverlay { get; set; }

    public void AddIdle(string suffix, TwoDAnimationClip clip) => _idle[suffix] = clip;

    public void AddMove(string suffix, TwoDAnimationClip clip) => _move[suffix] = clip;

    public (TwoDAnimationClip Clip, bool FlipX) Select(float facingRadians, bool moving, bool shooting)
    {
        var (suffix, flip) = SuffixFromRadians(facingRadians);
        if (shooting && ShootOverlay is not null)
        {
            return (ShootOverlay, flip);
        }

        if (moving && _move.TryGetValue(suffix, out var move))
        {
            return (move, flip);
        }

        if (_idle.TryGetValue(suffix, out var idle))
        {
            return (idle, flip);
        }

        if (_idle.TryGetValue("down", out var fallback))
        {
            return (fallback, flip);
        }

        return (_idle.Values.First(), false);
    }

    public static (string Suffix, bool FlipX) SuffixFromRadians(float radians)
    {
        var a = NormalizeAngle(radians);
        const float sector = MathF.PI / 4f;
        if (a < sector * 0.5f || a >= sector * 7.5f)
        {
            return ("right", false);
        }

        if (a < sector * 1.5f)
        {
            return ("down_right", false);
        }

        if (a < sector * 2.5f)
        {
            return ("down", false);
        }

        if (a < sector * 3.5f)
        {
            return ("down_right", true);
        }

        if (a < sector * 4.5f)
        {
            return ("right", true);
        }

        if (a < sector * 5.5f)
        {
            return ("up_right", true);
        }

        if (a < sector * 6.5f)
        {
            return ("up", false);
        }

        return ("up_right", false);
    }

    private static float NormalizeAngle(float radians)
    {
        while (radians < 0f)
        {
            radians += MathF.PI * 2f;
        }

        while (radians >= MathF.PI * 2f)
        {
            radians -= MathF.PI * 2f;
        }

        return radians;
    }
}
