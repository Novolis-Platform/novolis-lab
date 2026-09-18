using Novolis.Math.Geometry;
using Novolis.ThreeD;

namespace CorellianFreighterBuilder;

/// <summary>
/// Bake Assimp imports (CGTrader FBX drop, etc.) into SceneLab .nov3djson.
/// </summary>
internal static class ExternalMeshImport
{
    public static EditableMesh Load(string path, float targetLengthMeters = 34.37f) =>
        AssimpMeshImporter.ImportEditable(path, new MeshImportOptions
        {
            TargetLengthMeters = targetLengthMeters,
            CenterAtOrigin = true,
            LongestAxisToPositiveZ = true,
            PreTransformVertices = true,
        });

    public static void WriteScene(EditableMesh mesh, string outPath, string displayName) =>
        WriteComposed(outPath, displayName, ("Exterior", mesh));

    /// <summary>
    /// Exterior FBX + procedural interior as sibling mesh nodes (no CSG against the hull).
    /// </summary>
    public static void WriteExteriorWithInterior(
        EditableMesh exterior,
        EditableMesh interior,
        string outPath,
        string displayName = "YT-1300 (CGTrader exterior + procedural interior)")
    {
        WriteComposed(outPath, displayName,
            ("Exterior (CGTrader)", exterior),
            ("Interior (procedural)", interior));
    }

    public static void WriteComposed(
        string outPath,
        string displayName,
        params (string Name, EditableMesh Mesh)[] parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        if (parts.Length == 0)
            throw new ArgumentException("At least one mesh part is required.", nameof(parts));

        var doc = new SceneDocument
        {
            Name = displayName,
            CreatedAt = DateTimeOffset.UtcNow,
            ModifiedAt = DateTimeOffset.UtcNow,
        };
        var root = new GroupNode { Name = "Root" };
        var cam = new CameraNode
        {
            Name = "Camera",
            ParentId = root.Id,
            Transform = new SceneTransform { Position = [28f, 14f, 32f] },
            Target = [0, 0.2f, 2f],
            FovDeg = 36f,
        };

        doc.Nodes.Add(root);
        doc.Nodes.Add(cam);

        MeshNode? firstMesh = null;
        var totalVerts = 0;
        var totalTris = 0;
        foreach (var (name, mesh) in parts)
        {
            var meshNode = new MeshNode
            {
                Name = name,
                ParentId = root.Id,
                Primitive = MeshPrimitiveKind.Box,
            };
            MeshEditBake.WriteBaked(meshNode, mesh);
            doc.Nodes.Add(meshNode);
            firstMesh ??= meshNode;
            totalVerts += mesh.VertexCount;
            totalTris += mesh.TriangleCount;
            Console.WriteLine($"  part|{name}|verts={mesh.VertexCount}|tris={mesh.TriangleCount}");
        }

        doc.Nodes.AddRange([
            new LightNode
            {
                Name = "Key",
                ParentId = root.Id,
                LightKind = LightKind.Spot,
                Intensity = 3.8f,
                Transform = new SceneTransform { Position = [22, 16, 18], RotationDeg = [40, -35, 0] },
            },
            new LightNode
            {
                Name = "Fill",
                ParentId = root.Id,
                LightKind = LightKind.Omni,
                Intensity = 2.0f,
                Color = [0.85f, 0.9f, 1f],
                Transform = new SceneTransform { Position = [0, 2f, -18f] },
            },
            new LightNode
            {
                Name = "Rim",
                ParentId = root.Id,
                LightKind = LightKind.Infinite,
                Intensity = 0.5f,
                Transform = new SceneTransform { RotationDeg = [-55, 30, 0] },
            },
        ]);
        doc.ActiveCameraId = null;
        doc.SelectionId = firstMesh?.Id;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);
        SceneSerializer.Save(doc, outPath);
        Console.WriteLine($"COMPOSE|{outPath}|parts={parts.Length}|verts={totalVerts}|tris={totalTris}");
    }
}
