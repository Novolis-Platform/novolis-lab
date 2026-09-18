using System.Drawing;
using System.Numerics;
using MeshBench.Models;
using Novolis.Raylib.Abstractions;
using Novolis.Raylib.Colors;
using Novolis.Raylib.Rendering;
using Novolis.Simulation.View;
using RayCamera = Novolis.Raylib.Rendering.Camera;

namespace MeshBench.Services;

/// <summary>Fast 3D preview draw for the Raylib host.</summary>
internal sealed class RaylibSceneRenderer : IRaylibFrameRenderer
{
    private static readonly Color PreviewBackground = Color.FromArgb(255, 28, 28, 32);
    private static readonly Color WireColor = Color.FromArgb(255, 64, 64, 72);
    private static readonly Color HudColor = Color.FromArgb(255, 180, 180, 190);

    private Func<MeshSceneDocument> _getScene = () => new();
    private Func<OrbitCameraRig> _getOrbit = () => new();

    public void Bind(Func<MeshSceneDocument> getScene, Func<OrbitCameraRig> getOrbit)
    {
        _getScene = getScene;
        _getOrbit = getOrbit;
    }

    public void OnFrame(float deltaSeconds, int screenWidth, int screenHeight)
    {
        _ = deltaSeconds;
        _ = screenWidth;
        _ = screenHeight;
        var scene = _getScene();
        var orbit = _getOrbit();
        var eye = orbit.BuildEyePosition();

        Graphics.ClearBackground(PreviewBackground);
        var camera = RayCamera.Perspective(eye, orbit.Target, Vector3.UnitY, orbit.FieldOfViewDegrees);
        World.Begin(camera);

        World.DrawGrid(16, 1.2f);
        foreach (var part in scene.Parts)
        {
            var center = ToVector3(part.Center);
            var color = ToColor(part.Color);
            if (part.Kind.Equals("sphere", StringComparison.OrdinalIgnoreCase))
            {
                World.DrawSphere(center, part.Radius, color);
                World.DrawSphereWires(center, part.Radius, 8, 8, WireColor);
            }
            else
            {
                var size = ToVector3(part.HalfExtents) * 2f;
                World.DrawCubeV(center, size, color);
                World.DrawCubeWiresV(center, size, WireColor);
            }
        }

        World.End();
        Graphics.DrawText($"Preview — {scene.Parts.Count} meshes", 8, 8, 14, HudColor);
    }

    private static Vector3 ToVector3(float[] values) =>
        values.Length >= 3 ? new Vector3(values[0], values[1], values[2]) : Vector3.Zero;

    private static Color ToColor(float[] rgb)
    {
        var r = rgb.Length > 0 ? (byte)(rgb[0] * 255) : (byte)200;
        var g = rgb.Length > 1 ? (byte)(rgb[1] * 255) : (byte)200;
        var b = rgb.Length > 2 ? (byte)(rgb[2] * 255) : (byte)200;
        return Color.FromArgb(255, r, g, b);
    }
}
