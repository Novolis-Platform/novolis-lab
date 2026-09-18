namespace PulseStrip.Game;

using System.Drawing;
using System.Numerics;
using Novolis.Math.Geometry;
using Novolis.Raylib.Game;
using Novolis.Raylib.Rendering;
using Novolis.Simulation.Racing.Tracks;
using PulseStrip.Core;

/// <summary>
/// High-fidelity AG look: solid ribbon track (Wipeout TRF-style quads),
/// Synert ship meshes, Synert-style chase cam, race HUD.
/// Reference: https://github.com/Synert/WipeoutClone · BallisticNG design language (closed source).
/// </summary>
internal static class PulseStripLook
{
    public static readonly Color Void = Color.FromArgb(255, 1, 2, 10);
    public static readonly Color Underworld = Color.FromArgb(255, 8, 4, 22);
    public static readonly Color DeckDark = Color.FromArgb(255, 8, 16, 48);
    public static readonly Color DeckLite = Color.FromArgb(255, 14, 32, 78);
    public static readonly Color WallInner = Color.FromArgb(255, 28, 8, 48);
    public static readonly Color WallOuter = Color.FromArgb(255, 90, 10, 70);
    public static readonly Color RailCyan = Color.FromArgb(255, 0, 255, 255);
    public static readonly Color RailMagenta = Color.FromArgb(255, 255, 0, 170);
    public static readonly Color Gate = Color.FromArgb(255, 255, 50, 210);
    public static readonly Color PlayerAccent = Color.FromArgb(255, 0, 230, 255);
    public static readonly Color AiAccent = Color.FromArgb(255, 255, 180, 40);
    public static readonly Color Plasma = Color.FromArgb(255, 255, 40, 220);
    public static readonly Color Boost = Color.FromArgb(255, 255, 140, 30);
    public static readonly Color HudCyan = Color.FromArgb(255, 60, 240, 255);
    public static readonly Color HudAmber = Color.FromArgb(255, 255, 190, 60);
    public static readonly Color HudDim = Color.FromArgb(180, 140, 160, 190);
    public static readonly Color HullFill = Color.FromArgb(255, 210, 220, 230);
    public static readonly Color HullAi = Color.FromArgb(255, 50, 190, 130);

    // Synert/WipeoutClone ShipController camera defaults
    private const float CamBackInit = 9.0f;
    private const float CamBackExtra = 4.0f;
    private const float CamUp = 4.0f;
    private const float CamRight = -0.4f;

    public static Camera ChaseCamera(HoverCraftState player)
    {
        var speedT = Math.Clamp((float)(player.Speed / BoostRef), 0f, 1f);
        var back = CamBackInit + CamBackExtra * speedT;
        var fwd = player.Forward.LengthSquared() > 1e-6f
            ? Vector3.Normalize(player.Forward)
            : Vector3.UnitZ;
        var upHint = Vector3.UnitY;
        var right = Vector3.Cross(fwd, upHint);
        if (right.LengthSquared() < 1e-6f)
            right = Vector3.UnitX;
        right = Vector3.Normalize(right);
        var up = Vector3.Normalize(Vector3.Cross(right, fwd));
        // Bank stores TwistRadians/π — restore surface roll for chase cam.
        var bankQ = Quaternion.CreateFromAxisAngle(fwd, player.Bank * MathF.PI * 0.85f);
        up = Vector3.Normalize(Vector3.Transform(up, bankQ));
        right = Vector3.Normalize(Vector3.Cross(fwd, up));

        var eye = player.Position
                  - fwd * back
                  + up * CamUp
                  + right * CamRight;
        var target = player.Position + fwd * 14f + up * 0.4f;
        var fovy = 72f + speedT * 16f;
        return Camera.Perspective(eye, target, up, fovy);
    }

    private const float BoostRef = 78f;

    public static void DrawAtmosphere(RayGameContext ctx, Vector3 focus)
    {
        ctx.DrawPlane(new Vector3(focus.X, -22f, focus.Z), new Vector2(500f, 500f), Underworld);
        const float grid = 28f;
        var ox = MathF.Floor(focus.X / grid) * grid;
        var oz = MathF.Floor(focus.Z / grid) * grid;
        var line = Color.FromArgb(70, 60, 30, 100);
        for (var i = -6; i <= 6; i++)
        {
            var x = ox + i * grid;
            var z = oz + i * grid;
            ctx.DrawBolt(new Vector3(x, -21.85f, oz - 6 * grid), new Vector3(x, -21.85f, oz + 6 * grid), line);
            ctx.DrawBolt(new Vector3(ox - 6 * grid, -21.85f, z), new Vector3(ox + 6 * grid, -21.85f, z), line);
        }
    }

    public static void DrawCircuit(RayGameContext ctx, RaceTrack track, TrackRibbonMesh ribbon, int tick)
    {
        _ = tick;
        // Solid deck
        for (var i = 0; i < ribbon.DeckIndices.Length; i += 3)
        {
            var a = ribbon.DeckVerts[ribbon.DeckIndices[i]];
            var b = ribbon.DeckVerts[ribbon.DeckIndices[i + 1]];
            var c = ribbon.DeckVerts[ribbon.DeckIndices[i + 2]];
            var shade = ((i / 6) & 1) == 0 ? DeckDark : DeckLite;
            PulseStripNativeDraw.Triangle(a, b, c, shade);
        }

        // Solid walls
        for (var i = 0; i < ribbon.WallIndices.Length; i += 3)
        {
            var a = ribbon.WallVerts[ribbon.WallIndices[i]];
            var b = ribbon.WallVerts[ribbon.WallIndices[i + 1]];
            var c = ribbon.WallVerts[ribbon.WallIndices[i + 2]];
            PulseStripNativeDraw.Triangle(a, b, c, (i / 6 & 1) == 0 ? WallInner : WallOuter);
        }

        // Neon rail lips
        DrawRailLoop(ctx, ribbon.RailBottom, RailCyan, stride: 2);
        DrawRailLoop(ctx, ribbon.RailTop, RailMagenta, stride: 2);

        // Centerline dashes only — gates stay invisible (lap scoring still uses Track.Gates).
        var samples = track.CenterLineSamples;
        var step = Math.Max(1, samples.Count / 120);
        for (var i = 0; i < samples.Count; i += step * 2)
        {
            var a = samples[i];
            var b = samples[Math.Min(i + step, samples.Count - 1)];
            ctx.DrawBolt(new Vector3(a.X, 0.04f, a.Z), new Vector3(b.X, 0.04f, b.Z), Color.FromArgb(200, 200, 220, 255));
        }
    }

    private static void DrawRailLoop(RayGameContext ctx, Vector3[] rail, Color color, int stride)
    {
        if (rail.Length < stride * 2)
            return;
        var rings = rail.Length / stride;
        for (var side = 0; side < stride; side++)
        {
            for (var i = 0; i < rings; i++)
            {
                var a = rail[i * stride + side];
                var b = rail[((i + 1) % rings) * stride + side];
                ctx.DrawBolt(a, b, color);
            }
        }
    }

    public static void DrawShip(RayGameContext ctx, HoverCraftState craft, bool player)
    {
        var mesh = player ? ShipMeshCache.Player : ShipMeshCache.Rival;
        var fill = player ? HullFill : HullAi;
        var accent = player ? PlayerAccent : AiAccent;
        var fwd = craft.Forward.LengthSquared() > 1e-6f
            ? Vector3.Normalize(craft.Forward)
            : Vector3.UnitZ;
        var bankQ = Quaternion.CreateFromAxisAngle(fwd, craft.Bank * MathF.PI);
        var up = Vector3.Normalize(Vector3.Transform(Vector3.UnitY, bankQ));
        if (MathF.Abs(Vector3.Dot(up, fwd)) > 0.95f)
            up = Vector3.Normalize(Vector3.Cross(fwd, Vector3.UnitX));
        var basis = Matrix4x4.CreateWorld(craft.Position, fwd, up);

        if (mesh is not null && mesh.TriangleCount > 0)
            DrawMesh(mesh, basis, fill, accent);
        else
            DrawProxyDart(ctx, craft, fill, accent);

        var right = Vector3.Normalize(Vector3.Cross(fwd, up));
        var exhaust = craft.Position - fwd * 1.5f;
        var thrustColor = craft.Boosting ? Boost : accent;
        var len = craft.Boosting ? 2.8f : 1.1f;
        DrawThruster(exhaust + right * 0.35f, fwd, len, thrustColor);
        DrawThruster(exhaust - right * 0.35f, fwd, len, thrustColor);

        if (craft.ShieldActive)
            DrawShieldShell(craft.Position, 1.7f, PlayerAccent);
    }

    private static void DrawMesh(TriangleMesh mesh, Matrix4x4 basis, Color fill, Color edge)
    {
        var max = Math.Min(mesh.TriangleCount, 2500);
        for (var t = 0; t < max; t++)
        {
            mesh.GetTriangle(t, out var a, out var b, out var c);
            var wa = Vector3.Transform(a, basis);
            var wb = Vector3.Transform(b, basis);
            var wc = Vector3.Transform(c, basis);
            PulseStripNativeDraw.Triangle(wa, wb, wc, fill);
            // Sparse edge accent
            if ((t & 7) == 0)
            {
                // edges drawn via existing line API through a tiny helper — skip for fill density
            }
        }

        // Outline every Nth triangle edges for AG silhouette
        for (var t = 0; t < max; t += 5)
        {
            mesh.GetTriangle(t, out var a, out var b, out var c);
            var wa = Vector3.Transform(a, basis);
            var wb = Vector3.Transform(b, basis);
            var wc = Vector3.Transform(c, basis);
            DrawEdge(wa, wb, edge);
            DrawEdge(wb, wc, edge);
            DrawEdge(wc, wa, edge);
        }
    }

    private static void DrawEdge(Vector3 a, Vector3 b, Color color)
    {
        // Use native line via temporary — RayGameContext not available; use triangle thin? 
        // Fall back: very thin quad
        var mid = (a + b) * 0.5f;
        var dir = b - a;
        if (dir.LengthSquared() < 1e-8f)
            return;
        dir = Vector3.Normalize(dir);
        var side = Vector3.Normalize(Vector3.Cross(dir, Vector3.UnitY));
        if (side.LengthSquared() < 1e-6f)
            side = Vector3.UnitX;
        side *= 0.015f;
        PulseStripNativeDraw.Quad(a + side, b + side, b - side, a - side, color);
    }

    private static void DrawThruster(Vector3 origin, Vector3 forward, float length, Color color)
    {
        var tip = origin - forward * length;
        var right = Vector3.Normalize(new Vector3(forward.Z, 0f, -forward.X)) * 0.12f;
        var up = Vector3.UnitY * 0.12f;
        var dim = Color.FromArgb(160, color.R, color.G, color.B);
        PulseStripNativeDraw.Triangle(tip, origin + right, origin - right, color);
        PulseStripNativeDraw.Triangle(tip, origin + up, origin - up, dim);
    }

    private static void DrawShieldShell(Vector3 center, float radius, Color color)
    {
        const int seg = 12;
        for (var i = 0; i < seg; i++)
        {
            var a0 = i * MathF.Tau / seg;
            var a1 = (i + 1) * MathF.Tau / seg;
            var p0 = center + new Vector3(MathF.Cos(a0), 0, MathF.Sin(a0)) * radius;
            var p1 = center + new Vector3(MathF.Cos(a1), 0, MathF.Sin(a1)) * radius;
            var top = center + Vector3.UnitY * (radius * 0.35f);
            PulseStripNativeDraw.Triangle(top, p0, p1, Color.FromArgb(50, color.R, color.G, color.B));
        }
    }

    private static void DrawProxyDart(RayGameContext ctx, HoverCraftState c, Color fill, Color accent)
    {
        var fwd = c.Forward.LengthSquared() > 1e-6f ? Vector3.Normalize(c.Forward) : Vector3.UnitZ;
        var bankQ = Quaternion.CreateFromAxisAngle(fwd, c.Bank * MathF.PI);
        var up = Vector3.Normalize(Vector3.Transform(Vector3.UnitY, bankQ));
        if (MathF.Abs(Vector3.Dot(up, fwd)) > 0.95f)
            up = Vector3.Normalize(Vector3.Cross(fwd, Vector3.UnitX));
        var right = Vector3.Normalize(Vector3.Cross(fwd, up));
        var nose = c.Position + fwd * 1.6f;
        var tailL = c.Position - fwd * 1.1f + right * 0.7f;
        var tailR = c.Position - fwd * 1.1f - right * 0.7f;
        var top = c.Position + up * 0.45f;
        PulseStripNativeDraw.Triangle(nose, tailL, tailR, fill);
        PulseStripNativeDraw.Triangle(nose, top, tailL, accent);
        PulseStripNativeDraw.Triangle(nose, tailR, top, accent);
        ctx.DrawShipBox(c.Position, new Vector3(0.8f, 0.25f, 1.6f), fill);
    }

    public static void DrawPickup(TrackPickup pad, int tick)
    {
        var bob = MathF.Sin(tick * 0.12f + pad.Id) * 0.2f;
        var p = pad.Position + Vector3.UnitY * (0.6f + bob);
        var color = pad.Kind == PickupKind.Weapon ? Boost : RailCyan;
        var r = 0.35f;
        // Small octahedron — not a giant sphere
        var up = p + Vector3.UnitY * r;
        var dn = p - Vector3.UnitY * r;
        var f = p + Vector3.UnitZ * r;
        var b = p - Vector3.UnitZ * r;
        var l = p - Vector3.UnitX * r;
        var rt = p + Vector3.UnitX * r;
        PulseStripNativeDraw.Triangle(up, f, rt, color);
        PulseStripNativeDraw.Triangle(up, rt, b, color);
        PulseStripNativeDraw.Triangle(up, b, l, color);
        PulseStripNativeDraw.Triangle(up, l, f, color);
        PulseStripNativeDraw.Triangle(dn, rt, f, color);
        PulseStripNativeDraw.Triangle(dn, b, rt, color);
        PulseStripNativeDraw.Triangle(dn, l, b, color);
        PulseStripNativeDraw.Triangle(dn, f, l, color);
    }

    public static void DrawSpeedStreaks(HoverCraftState player)
    {
        if (player.Speed < 10)
            return;
        var fwd = player.Forward.LengthSquared() > 1e-6f
            ? Vector3.Normalize(player.Forward)
            : Vector3.UnitZ;
        var bankQ = Quaternion.CreateFromAxisAngle(fwd, player.Bank * MathF.PI);
        var up = Vector3.Normalize(Vector3.Transform(Vector3.UnitY, bankQ));
        if (MathF.Abs(Vector3.Dot(up, fwd)) > 0.95f)
            up = Vector3.UnitY;
        var right = Vector3.Normalize(Vector3.Cross(fwd, up));
        var count = player.Boosting ? 10 : 5;
        for (var i = 0; i < count; i++)
        {
            var lat = (i - count * 0.5f) * 0.55f;
            var start = player.Position + fwd * (3f + i * 1.1f) + right * lat + up * 0.3f;
            var end = start - fwd * (0.8f + (float)player.Speed * 0.06f);
            var alpha = player.Boosting ? 180 : 90;
            // thin streak quad
            var side = right * 0.02f;
            PulseStripNativeDraw.Quad(start + side, end + side, end - side, start - side,
                Color.FromArgb(alpha, 180, 240, 255));
        }
    }

    public static void DrawRaceHud(RayGameContext ctx, HoverRaceSimulation sim, HoverCraftState player, string circuitName)
    {
        var w = ctx.Width;
        var h = ctx.Height;
        ctx.HudRect(0, 0, w, 34, Color.FromArgb(170, 0, 0, 0));
        ctx.HudText("PULSESTRIP", 16, 7, 18, HudCyan);
        ctx.HudText(circuitName.ToUpperInvariant(), 170, 9, 14, HudDim);

        ctx.HudText($"P{player.Place}", 20, h - 118, 58, HudCyan);
        ctx.HudText($"/{sim.State.Craft.Count}", 130, h - 78, 22, HudDim);
        ctx.HudText($"LAP {Math.Min(player.CompletedLaps + 1, sim.State.TargetLaps)}/{sim.State.TargetLaps}", 24, h - 48, 18, Color.White);

        var kph = player.Speed * 3.6f * 12.0; // theatrical like Synert (ships "60% too big" note)
        ctx.HudText($"{kph:0}", w - 210, h - 110, 44, HudAmber);
        ctx.HudText("KPH", w - 210, h - 58, 14, HudDim);

        DrawBar(ctx, 24, h - 155, 200, 12, (float)(player.Health / 100.0), Color.FromArgb(255, 40, 200, 160), "SHIELD");
        DrawBar(ctx, 24, h - 136, 200, 8, (float)player.BoostFuel, Boost, "BOOST");

        ctx.HudText("WPN", w - 210, 44, 12, HudDim);
        for (var i = 0; i < 5; i++)
            ctx.HudRect(w - 210 + i * 22, 62, 18, 14, i < player.WeaponAmmo ? Plasma : Color.FromArgb(90, 40, 40, 55));

        if (player.Boosting)
            ctx.HudText("BOOST", w / 2 - 36, 44, 20, Boost);

        var y = 44;
        foreach (var c in sim.State.Craft.OrderBy(x => x.Place))
        {
            ctx.HudText($"P{c.Place} {(c.Id == 0 ? "YOU" : c.Name)}", w - 150, y, 14, c.Id == 0 ? HudCyan : HudDim);
            y += 18;
        }
    }

    private static void DrawBar(RayGameContext ctx, int x, int y, int width, int height, float t, Color fill, string label)
    {
        t = Math.Clamp(t, 0f, 1f);
        ctx.HudText(label, x, y - 14, 11, HudDim);
        ctx.HudRect(x, y, width, height, Color.FromArgb(160, 20, 20, 30));
        ctx.HudRect(x, y, (int)(width * t), height, fill);
    }
}
