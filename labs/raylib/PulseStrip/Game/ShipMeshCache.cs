namespace PulseStrip.Game;

using System.Numerics;
using Novolis.Math.Geometry;
using Novolis.ThreeD;

/// <summary>
/// Loads MIT-licensed AG ship meshes from Synert/WipeoutClone
/// (https://github.com/Synert/WipeoutClone).
/// </summary>
internal static class ShipMeshCache
{
    private static TriangleMesh? _player;
    private static TriangleMesh? _rival;
    private static bool _loaded;

    public static TriangleMesh? Player
    {
        get
        {
            Ensure();
            return _player;
        }
    }

    public static TriangleMesh? Rival
    {
        get
        {
            Ensure();
            return _rival ?? _player;
        }
    }

    private static void Ensure()
    {
        if (_loaded)
            return;
        _loaded = true;

        var root = Path.Combine(AppContext.BaseDirectory, "Content", "models");
        var repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Content", "models"));
        if (Directory.Exists(repo))
            root = repo;

        _player = TryLoad(Path.Combine(root, "shipmodel.fbx"), length: 3.8f);
        _rival = TryLoad(Path.Combine(root, "ship3.fbx"), length: 3.6f) ?? _player;
    }

    private static TriangleMesh? TryLoad(string path, float length)
    {
        if (!File.Exists(path))
            return null;
        try
        {
            return AssimpMeshImporter.ImportFile(path, new MeshImportOptions
            {
                TargetLengthMeters = length,
                CenterAtOrigin = true,
                LongestAxisToPositiveZ = true,
                PreTransformVertices = true,
                GenerateNormals = true,
            });
        }
        catch
        {
            return null;
        }
    }
}
