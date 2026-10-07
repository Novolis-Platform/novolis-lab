using Novolis.CodeGen.Pipeline;

namespace AssetStudioLab;

/// <summary>Bakes albedo / roughness / normal maps from the compiled material.</summary>
public sealed class BakeProductsStep : IPipelineStep
{
    public string Id => "bake_products";
    public string Description => "Bake texture products";
    public IReadOnlyList<string> DependsOn { get; } = ["compile_ir"];

    public IReadOnlyList<string> InputPaths(PipelineContext context) =>
        [
            Path.Combine(context.StepArtifactsDir("normalize"), "normalized.json"),
            Path.Combine(context.StepArtifactsDir("compile_ir"), "ir.json"),
        ];

    public IReadOnlyList<string> ExpectedOutputPaths(PipelineContext context) =>
        [Path.Combine(context.StepArtifactsDir(Id), "albedo.ppm")];

    public async ValueTask<StepExecutionResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
    {
        var document = VisualJson.Deserialize(
            await File.ReadAllTextAsync(InputPaths(context)[0], cancellationToken).ConfigureAwait(false));
        var material = document.Definitions.FirstOrDefault(d => d.Kind == VisualDefinitionKind.Material)
                       ?? throw new InvalidOperationException("No material to bake.");
        var maps = MapBaker.Bake(material, 32);
        var artifacts = context.StepArtifactsDir(Id);
        Directory.CreateDirectory(artifacts);
        var albedo = Path.Combine(artifacts, "albedo.ppm");
        await File.WriteAllTextAsync(albedo, MapBaker.ToPpm(maps.Albedo, maps.Width, maps.Height), cancellationToken)
            .ConfigureAwait(false);
        await File.WriteAllTextAsync(
            Path.Combine(artifacts, "roughness.ppm"),
            MapBaker.ToPpm(maps.Roughness, maps.Width, maps.Height),
            cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(
            Path.Combine(artifacts, "normal.ppm"),
            MapBaker.ToPpm(maps.Normal, maps.Width, maps.Height),
            cancellationToken).ConfigureAwait(false);
        return ParseDefinitionsStep.Succeeded(context, InputPaths(context), albedo);
    }
}
