using System.Drawing;
using System.Numerics;
using Novolis.Cad.Primitives;
using Novolis.Cad.SceneBridge;
using Novolis.Raylib.Interact;
using Novolis.Raylib.Rendering;
using Novolis.Raylib.Timing;
using Novolis.Raylib.Windowing;

namespace CalypsoInternalsCad.View;

/// <summary>Minimal orbit viewer for tessellated CAL-INT CAD (Esc / Q to quit).</summary>
internal static class InternalsCadViewer
{
    private readonly record struct TriMesh(Vector3[] Vertices, int[] Indices, Color Color);

    public static void Run(CadDocument cad, string title)
    {
        ArgumentNullException.ThrowIfNull(cad);
        var meshes = BuildMeshes(cad);
        if (meshes.Count == 0)
            throw new InvalidOperationException("No tessellatable entities to view.");

        var bounds = ComputeBounds(meshes);
        var center = (bounds.Min + bounds.Max) * 0.5f;
        var radius = MathF.Max(8f, Vector3.Distance(bounds.Min, bounds.Max) * 0.55f);

        Window.Init(1400, 900, title);
        if (!Window.IsReady())
            throw new InvalidOperationException("Raylib window failed to initialize.");

        Time.SetTargetFPS(60);

        var yaw = 0.55f;
        var pitch = 0.35f;
        var dist = radius * 1.35f;
        var dragging = false;
        var last = Vector2.Zero;

        try
        {
            while (!Window.ShouldClose())
            {
                if (Input.IsKeyPressed(KeyboardKey.Escape))
                    break;

                var wheel = Input.GetMouseWheelMove();
                if (MathF.Abs(wheel) > 0.001f)
                    dist = Math.Clamp(dist * (1f - wheel * 0.08f), radius * 0.35f, radius * 4f);

                if (Input.IsMouseButtonPressed(MouseButton.Left))
                {
                    dragging = true;
                    last = Input.GetMousePosition();
                }

                if (!Input.IsMouseButtonDown(MouseButton.Left))
                    dragging = false;

                if (dragging)
                {
                    var now = Input.GetMousePosition();
                    yaw += (now.X - last.X) * 0.005f;
                    pitch = Math.Clamp(pitch + (now.Y - last.Y) * 0.005f, -1.2f, 1.2f);
                    last = now;
                }

                var eye = center + new Vector3(
                    MathF.Cos(pitch) * MathF.Sin(yaw) * dist,
                    MathF.Sin(pitch) * dist,
                    MathF.Cos(pitch) * MathF.Cos(yaw) * dist);

                var camera = new Camera
                {
                    Position = eye,
                    Target = center,
                    Up = Vector3.UnitY,
                    Fovy = 45f,
                    Projection = CameraProjection.Perspective,
                };

                Graphics.BeginDrawing();
                Graphics.ClearBackground(Color.FromArgb(255, 18, 22, 28));
                World.Begin(camera);
                World.DrawGrid(40, 2f);
                foreach (var m in meshes)
                    DrawMesh(m);
                World.End();
                Graphics.DrawText(
                    "CAL-INT to CAD/OBJ  |  drag orbit  |  wheel zoom  |  Esc quit",
                    16,
                    16,
                    18,
                    Color.FromArgb(255, 245, 245, 245));
                Graphics.DrawText(
                    $"{meshes.Count} mesh groups · midship orbit",
                    16,
                    40,
                    16,
                    Color.FromArgb(255, 160, 180, 200));
                Graphics.EndDrawing();
            }
        }
        finally
        {
            if (Window.IsReady())
                Window.Close();
        }
    }

    private static List<TriMesh> BuildMeshes(CadDocument cad)
    {
        var list = new List<TriMesh>();
        foreach (var entity in cad.Entities)
        {
            var mesh = CadEntityTessellator.TryTessellate(entity);
            if (mesh is null || mesh.TriangleCount == 0)
                continue;
            list.Add(new TriMesh(
                mesh.Vertices.ToArray(),
                mesh.Indices.ToArray(),
                ToColor(GuessColor(entity))));
        }

        return list;
    }

    private static void DrawMesh(TriMesh mesh)
    {
        var inds = mesh.Indices;
        var verts = mesh.Vertices;
        for (var i = 0; i + 2 < inds.Length; i += 3)
        {
            var a = verts[inds[i]];
            var b = verts[inds[i + 1]];
            var c = verts[inds[i + 2]];
            World.DrawTriangle(a, b, c, mesh.Color);
            var edge = Color.FromArgb(
                180,
                Math.Clamp(mesh.Color.R - 40, 0, 255),
                Math.Clamp(mesh.Color.G - 40, 0, 255),
                Math.Clamp(mesh.Color.B - 40, 0, 255));
            World.DrawLine(a, b, edge);
            World.DrawLine(b, c, edge);
            World.DrawLine(c, a, edge);
        }
    }

    private static (Vector3 Min, Vector3 Max) ComputeBounds(List<TriMesh> meshes)
    {
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        foreach (var m in meshes)
        {
            foreach (var v in m.Vertices)
            {
                min = Vector3.Min(min, v);
                max = Vector3.Max(max, v);
            }
        }

        return (min, max);
    }

    private static Vector3 GuessColor(CadEntity entity)
    {
        var name = entity.Name ?? "";
        if (name.Contains("oml", StringComparison.OrdinalIgnoreCase))
            return new Vector3(0.42f, 0.50f, 0.58f);
        if (name.Contains("iml", StringComparison.OrdinalIgnoreCase))
            return new Vector3(0.70f, 0.72f, 0.74f);
        if (name.Contains("HOLD", StringComparison.OrdinalIgnoreCase))
            return new Vector3(0.30f, 0.45f, 0.32f);
        if (name.Contains("ENG", StringComparison.OrdinalIgnoreCase))
            return new Vector3(0.65f, 0.40f, 0.35f);
        if (name.Contains("BRIDGE", StringComparison.OrdinalIgnoreCase))
            return new Vector3(0.35f, 0.55f, 0.65f);
        if (name.Contains("AIRLOCK", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("D3-", StringComparison.OrdinalIgnoreCase))
            return new Vector3(0.85f, 0.55f, 0.20f);
        if (string.Equals(entity.Kind, "wall", StringComparison.OrdinalIgnoreCase))
            return new Vector3(0.55f, 0.58f, 0.62f);
        return new Vector3(0.50f, 0.52f, 0.56f);
    }

    private static Color ToColor(Vector3 c) =>
        Color.FromArgb(
            255,
            Math.Clamp((int)(c.X * 255f), 0, 255),
            Math.Clamp((int)(c.Y * 255f), 0, 255),
            Math.Clamp((int)(c.Z * 255f), 0, 255));
}
