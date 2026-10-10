using System.Numerics;
using Novolis.Rendering.Materials;
using Novolis.Rendering.Scene;
using Novolis.Modeling;
using RenderLightKind = Novolis.Rendering.Scene.LightKind;
using RenderScene = Novolis.Rendering.Scene.Scene;

namespace Novolis.Lab.Compose;

/// <summary>
/// Maps an evaluated Modeling document to a path-trace authoring <see cref="RenderScene"/>
/// without a Modeling↔Rendering library reference.
/// </summary>
public static class SceneDocumentRenderingBridge
{
    static readonly StandardMaterial DefaultMaterial = new()
    {
        BaseColor = new Vector3(0.75f, 0.75f, 0.78f),
        Roughness = 0.45f,
    };

    /// <summary>Evaluates <paramref name="document"/> and maps meshes plus lights.</summary>
    public static RenderScene ToScene(SceneDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var evaluator = new SceneEvaluator();
        evaluator.Bind(document);
        return ToScene(evaluator.Cache);
    }

    /// <summary>Maps an already-evaluated look cache to a trace scene.</summary>
    public static RenderScene ToScene(LookCache cache)
    {
        ArgumentNullException.ThrowIfNull(cache);

        var materials = cache.Materials
            .Select(n => n.Source)
            .OfType<MaterialNode>()
            .ToDictionary(m => m.Id);
        var meshNodes = cache.Meshes
            .Select(n => n.Source)
            .OfType<MeshNode>()
            .ToDictionary(m => m.Id);

        var meshes = new List<MeshInstance>(cache.EvaluatedMeshes.Count);
        foreach (var mesh in cache.EvaluatedMeshes)
        {
            meshes.Add(new MeshInstance(
                mesh.Vertices,
                mesh.Indices,
                ResolveMaterial(mesh.SourceId, meshNodes, materials),
                mesh.World));
        }

        var lights = new List<LightDefinition>();
        foreach (var node in cache.Lights)
        {
            if (node.Source is not LightNode light || !light.Enabled)
                continue;
            lights.Add(MapLight(light, node));
        }

        return new RenderScene(meshes, lights);
    }

    static IMaterial ResolveMaterial(
        Guid sourceId,
        Dictionary<Guid, MeshNode> meshNodes,
        Dictionary<Guid, MaterialNode> materials)
    {
        if (meshNodes.TryGetValue(sourceId, out var mesh)
            && mesh.MaterialId is { } materialId
            && materials.TryGetValue(materialId, out var node))
        {
            var color = node.Color is { Length: >= 3 }
                ? new Vector3(node.Color[0], node.Color[1], node.Color[2])
                : DefaultMaterial.BaseColor;
            return new StandardMaterial
            {
                BaseColor = color,
                Roughness = node.Roughness,
                Metallic = node.Metallic,
            };
        }

        return DefaultMaterial;
    }

    static LightDefinition MapLight(LightNode light, EvaluatedNode evaluated)
    {
        var color = light.Color is { Length: >= 3 }
            ? new Vector3(light.Color[0], light.Color[1], light.Color[2])
            : Vector3.One;

        if (light.LightKind == Novolis.Modeling.LightKind.Infinite)
        {
            var direction = Vector3.TransformNormal(-Vector3.UnitZ, evaluated.WorldMatrix);
            if (direction.LengthSquared() < 1e-12f)
                direction = -Vector3.UnitY;
            return new LightDefinition(
                RenderLightKind.Directional,
                Vector3.Normalize(direction),
                color,
                light.Intensity);
        }

        return new LightDefinition(
            RenderLightKind.Point,
            evaluated.WorldPosition,
            color,
            light.Intensity);
    }
}
