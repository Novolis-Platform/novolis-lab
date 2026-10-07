using System.Text.Json;
using Novolis.CodeGen.Pipeline;

namespace AssetStudioLab;

/// <summary>Lowers normalized AST to Visual IR plus Silk/Rendering handshake payloads.</summary>
public sealed class CompileIrStep : IPipelineStep
{
    public string Id => "compile_ir";
    public string Description => "Compile visual IR";
    public IReadOnlyList<string> DependsOn { get; } = ["normalize"];

    public IReadOnlyList<string> InputPaths(PipelineContext context) =>
        [Path.Combine(context.StepArtifactsDir("normalize"), "normalized.json")];

    public IReadOnlyList<string> ExpectedOutputPaths(PipelineContext context) =>
        [Path.Combine(context.StepArtifactsDir(Id), "ir.json")];

    public async ValueTask<StepExecutionResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
    {
        var input = InputPaths(context)[0];
        var document = VisualJson.Deserialize(await File.ReadAllTextAsync(input, cancellationToken).ConfigureAwait(false));
        var ir = VisualCompiler.Compile(document);
        var output = ExpectedOutputPaths(context)[0];
        var artifacts = context.StepArtifactsDir(Id);
        Directory.CreateDirectory(artifacts);
        var json = JsonSerializer.Serialize(ir, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(output, json, cancellationToken).ConfigureAwait(false);
        if (ir.Definitions.Count > 0)
        {
            var first = ir.Definitions[0];
            await File.WriteAllTextAsync(
                Path.Combine(artifacts, "silk.json"),
                JsonSerializer.Serialize(SilkVisualLowering.Lower(first), new JsonSerializerOptions { WriteIndented = true }),
                cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(
                Path.Combine(artifacts, "rendering.json"),
                JsonSerializer.Serialize(RenderingVisualLowering.Lower(first), new JsonSerializerOptions { WriteIndented = true }),
                cancellationToken).ConfigureAwait(false);
        }

        return ParseDefinitionsStep.Succeeded(context, InputPaths(context), output);
    }
}
