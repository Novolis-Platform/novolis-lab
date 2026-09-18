using Novolis.Rendering.TwoD;

namespace TopDownDoom.Art;

internal sealed class CharacterAnimationSet
{
    public CharacterAnimationSet(
        TwoDAnimationClip walk,
        float worldHalfHeight,
        TwoDAnimationClip? shoot = null,
        TwoDAnimationClip? death = null,
        DirectionalClips? facing = null)
    {
        Walk = walk;
        WorldHalfHeight = worldHalfHeight;
        Shoot = shoot;
        Death = death;
        Facing = facing;
    }

    public TwoDAnimationClip Walk { get; }
    public TwoDAnimationClip? Shoot { get; }
    public TwoDAnimationClip? Death { get; }
    public float WorldHalfHeight { get; }
    public DirectionalClips? Facing { get; }

    public (TwoDAnimationClip Clip, bool FlipX, float HalfHeight) Resolve(
        float facingRadians,
        bool moving,
        bool shooting)
    {
        if (Facing is not null)
        {
            var (clip, flip) = Facing.Select(facingRadians, moving, shooting);
            return (clip, flip, Facing.WorldHalfHeight);
        }

        var active = shooting && Shoot is not null ? Shoot : Walk;
        return (active, false, WorldHalfHeight);
    }
}
