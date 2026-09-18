using System.Drawing;
using System.Numerics;
using Novolis.Raylib.Game;

namespace XFighter.Game;

internal sealed class Starfield
{
    private readonly Vector3[] _stars;
    private readonly float[] _brightness;

    public Starfield(int count, Random rng)
    {
        _stars = new Vector3[count];
        _brightness = new float[count];
        for (var i = 0; i < count; i++)
        {
            _stars[i] = new Vector3(
                (float)(rng.NextDouble() * 200 - 100),
                (float)(rng.NextDouble() * 80 - 40),
                (float)(rng.NextDouble() * -20 - 10));
            _brightness[i] = 0.35f + (float)rng.NextDouble() * 0.65f;
        }
    }

    public void Draw(RayGameContext ctx, Vector3 playerPos, float speed, Vector3 forward)
    {
        var streak = Math.Clamp((speed - 18f) / 30f, 0f, 1f);
        for (var i = 0; i < _stars.Length; i++)
        {
            var p = _stars[i] - playerPos * 0.02f;
            var b = (byte)(180 + _brightness[i] * 75);
            var c = Color.FromArgb(255, b, b, (byte)(b + 20));

            if (streak > 0.05f)
            {
                var tail = p + forward * (2f + streak * 6f * _brightness[i]);
                ctx.DrawBolt(p, tail, Color.FromArgb((int)(120 + streak * 100), b, b, (byte)(b + 40)));
            }

            var s = 0.08f + _brightness[i] * 0.12f;
            ctx.DrawGlowSphere(p, s, c);
        }
    }
}
