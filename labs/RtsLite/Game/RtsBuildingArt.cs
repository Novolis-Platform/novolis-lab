using System.Drawing;
using System.Numerics;
using Novolis.Raylib.Game;
using Novolis.Raylib.Rendering;

namespace RtsLite.Game;

/// <summary>Billboard UVs from buildings_set_small.png (336×193).</summary>
internal sealed class RtsBuildingArt
{
    private readonly Texture _sheet;

    public RtsBuildingArt(RayGameContext ctx, string assetPath)
    {
        _sheet = ctx.LoadTexture(assetPath);
    }

    public void Draw(
        Camera camera,
        RayGameContext ctx,
        RtsBuildingType type,
        Vector3 worldCenter,
        float scale,
        Color tint)
    {
        if (!_sheet.IsValid)
            return;

        var src = SourceRect(type);
        var pos = worldCenter + new Vector3(0f, scale * 0.2f, 0f);
        ctx.DrawBillboardPro(
            camera,
            _sheet,
            src,
            pos,
            new Vector2(scale * 1.15f, scale * 1.15f),
            tint);
    }

    private static System.Drawing.RectangleF SourceRect(RtsBuildingType type)
    {
        var (x, y, w, h) = type switch
        {
            RtsBuildingType.ConstructionYard => (0f, 72f, 168f, 121f),
            RtsBuildingType.PowerPlant => (268f, 0f, 68f, 68f),
            RtsBuildingType.Barracks => (178f, 48f, 158f, 145f),
            RtsBuildingType.OreRefinery => (175f, 45f, 161f, 148f),
            RtsBuildingType.WarFactory => (2f, 74f, 166f, 119f),
            _ => (0f, 0f, 64f, 64f),
        };
        return new System.Drawing.RectangleF(x, y, w, h);
    }
}
