using System.Drawing;
using System.Numerics;
using Novolis.Physics.Cloth;
using Novolis.Raylib.Game;
using Novolis.Simulation.World.Builders;

namespace ClothPlay.Game;

internal enum KatanaEdge
{
    /// <summary>Sharp edge faces +Y — classic “edge up” for falling cloth to meet.</summary>
    Up,

    /// <summary>Sharp edge faces −Y — blade inverted on the stand.</summary>
    Down,
}

/// <summary>Horizontal katana (long edge, not tip-up) with collision mesh and cutting blade.</summary>
internal sealed class SwordProp
{
    private static readonly Color Steel = Color.FromArgb(255, 200, 205, 212);
    private static readonly Color EdgeHighlight = Color.FromArgb(255, 245, 248, 252);
    private static readonly Color Same = Color.FromArgb(255, 48, 46, 44);
    private static readonly Color Tsuka = Color.FromArgb(255, 90, 70, 48);
    private static readonly Color Tsuba = Color.FromArgb(255, 120, 100, 55);

    public KatanaEdge Edge { get; }
    public Vector3 BladeCenter { get; }
    public Vector3 Heel { get; }
    public Vector3 Tip { get; }

    /// <summary>Visual sharp-edge line.</summary>
    public ClothBlade Blade { get; }

    /// <summary>Top ridge contact line — where a drop meets steel (used for cutting).</summary>
    public ClothBlade ContactBlade { get; }

    /// <summary>World Y of the sharp edge.</summary>
    public float EdgeHeight { get; }

    /// <summary>World Y of the top contact ridge.</summary>
    public float ContactHeight { get; }

    private SwordProp(
        KatanaEdge edge,
        Vector3 bladeCenter,
        Vector3 heel,
        Vector3 tip,
        ClothBlade blade,
        ClothBlade contactBlade,
        float edgeHeight,
        float contactHeight)
    {
        Edge = edge;
        BladeCenter = bladeCenter;
        Heel = heel;
        Tip = tip;
        Blade = blade;
        ContactBlade = contactBlade;
        EdgeHeight = edgeHeight;
        ContactHeight = contactHeight;
    }

    /// <summary>
    /// Katana along +Z through room center: long cutting edge horizontal (edge up or down), not tip-up.
    /// </summary>
    public static SwordProp CreateKatana(Vector3 floorCenter, KatanaEdge edge = KatanaEdge.Up)
    {
        const float contactY = 1.85f;
        const float bladeHalfHeight = 0.055f;
        const float bladeHalfLength = 0.72f;

        var centerY = contactY - bladeHalfHeight;
        var edgeY = edge == KatanaEdge.Up ? contactY : centerY - bladeHalfHeight;

        var bladeCenter = floorCenter + new Vector3(0f, centerY, 0f);
        var heel = bladeCenter + new Vector3(0f, 0f, -bladeHalfLength);
        var tip = bladeCenter + new Vector3(0f, 0f, bladeHalfLength);

        var sharp = new ClothBlade(
            new Vector3(heel.X, edgeY, heel.Z),
            new Vector3(tip.X, edgeY, tip.Z),
            halfThickness: 0.08f);

        var contact = new ClothBlade(
            new Vector3(heel.X, contactY, heel.Z),
            new Vector3(tip.X, contactY, tip.Z),
            halfThickness: 0.11f);

        return new SwordProp(edge, bladeCenter, heel, tip, sharp, contact, edgeY, contactY);
    }

    public void AppendCollisionMesh(List<Vector3> verts, List<int> tris)
    {
        const float bladeHalfHeight = 0.055f;
        const float bladeHalfWidth = 0.03f;
        var halfLen = Vector3.Distance(Heel, Tip) * 0.5f;

        RoomMeshBuilder.AppendBox(
            verts,
            tris,
            BladeCenter.X,
            BladeCenter.Y,
            BladeCenter.Z,
            bladeHalfWidth,
            bladeHalfHeight,
            halfLen);

        var tipBox = Tip - new Vector3(0f, 0f, 0.12f);
        RoomMeshBuilder.AppendBox(
            verts,
            tris,
            tipBox.X,
            BladeCenter.Y,
            tipBox.Z,
            bladeHalfWidth * 0.7f,
            bladeHalfHeight * 0.85f,
            0.14f);

        var tsuba = Heel + new Vector3(0f, 0f, 0.08f);
        RoomMeshBuilder.AppendBox(verts, tris, tsuba.X, BladeCenter.Y, tsuba.Z, 0.14f, 0.02f, 0.14f);

        var handle = Heel + new Vector3(0f, 0f, -0.28f);
        RoomMeshBuilder.AppendBox(verts, tris, handle.X, BladeCenter.Y, handle.Z, 0.035f, 0.035f, 0.26f);

        var standY = BladeCenter.Y * 0.5f;
        RoomMeshBuilder.AppendBox(verts, tris, Heel.X, standY, Heel.Z, 0.04f, standY, 0.04f);
        RoomMeshBuilder.AppendBox(verts, tris, Tip.X, standY, Tip.Z, 0.04f, standY, 0.04f);
    }

    public void Draw(RayGameContext ctx)
    {
        var len = Vector3.Distance(Heel, Tip);
        ctx.DrawShipBox(BladeCenter, new Vector3(0.06f, 0.11f, len), Steel);

        ctx.DrawBolt(
            new Vector3(Heel.X, EdgeHeight, Heel.Z),
            new Vector3(Tip.X, EdgeHeight, Tip.Z),
            EdgeHighlight);

        var spineY = Edge == KatanaEdge.Up
            ? BladeCenter.Y - 0.05f
            : BladeCenter.Y + 0.05f;
        ctx.DrawBolt(
            new Vector3(Heel.X, spineY, Heel.Z),
            new Vector3(Tip.X, spineY, Tip.Z),
            Same);

        var tsuba = Heel + new Vector3(0f, 0f, 0.08f);
        ctx.DrawShipBox(new Vector3(tsuba.X, BladeCenter.Y, tsuba.Z), new Vector3(0.28f, 0.04f, 0.28f), Tsuba);

        var handle = Heel + new Vector3(0f, 0f, -0.28f);
        ctx.DrawShipBox(new Vector3(handle.X, BladeCenter.Y, handle.Z), new Vector3(0.07f, 0.07f, 0.52f), Tsuka);

        var standY = BladeCenter.Y * 0.5f;
        ctx.DrawShipBox(new Vector3(Heel.X, standY, Heel.Z), new Vector3(0.08f, BladeCenter.Y, 0.08f), Same);
        ctx.DrawShipBox(new Vector3(Tip.X, standY, Tip.Z), new Vector3(0.08f, BladeCenter.Y, 0.08f), Same);
    }
}
