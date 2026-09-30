using System.Numerics;

namespace TopDownDoom.Game;

internal sealed class CombatFx(CombatFxKind kind, Vector3 position, float duration, float scale = 1f)
{
    public CombatFxKind Kind = kind;
    public Vector3 Position = position;
    public float Duration = duration;
    public float Time;
    public float Scale = scale;
}
