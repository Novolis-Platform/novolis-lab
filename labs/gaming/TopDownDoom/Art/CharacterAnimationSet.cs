using Novolis.Rendering.Planar;

namespace TopDownDoom.Art;

internal sealed class CharacterAnimationSet
{
    public CharacterAnimationSet(
        PlanarAnimationClip walk,
        float worldHalfHeight,
        PlanarAnimationClip? shoot = null,
        PlanarAnimationClip? death = null,
        DirectionalClips? facing = null)
    {
        Walk = walk;
        WorldHalfHeight = worldHalfHeight;
        Shoot = shoot;
        Death = death;
        Facing = facing;
    }

    public PlanarAnimationClip Walk { get; }
    public PlanarAnimationClip? Shoot { get; }
    public PlanarAnimationClip? Death { get; }
    public float WorldHalfHeight { get; }
    public DirectionalClips? Facing { get; }

    public (PlanarAnimationClip Clip, bool FlipX, float HalfHeight) Resolve(
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
