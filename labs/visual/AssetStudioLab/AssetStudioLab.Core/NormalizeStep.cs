using Novolis.CodeGen.Pipeline;

namespace AssetStudioLab;

/// <summary>Writes a normalized copy of the AST (units already SI from parse).</summary>
public sealed class NormalizeStep : IPipelineStep
{
    public string Id => "normalize";
    public string Description => "Normalize visual AST";
    public IReadOnlyList<string> DependsOn { get; } = ["validate"];

    public IReadOnlyList<string> InputPaths(PipelineContext context) =>
        [Path.Combine(context.StepArtifactsDir("parse_definitions"), "document.json")];

    public IReadOnlyList<string> ExpectedOutputPaths(PipelineContext context) =>
        [Path.Combine(context.StepArtifactsDir(Id), "normalized.json")];

    public async ValueTask<StepExecutionResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
    {
        var input = InputPaths(context)[0];
        var json = await File.ReadAllTextAsync(input, cancellationToken).ConfigureAwait(false);
        var output = ExpectedOutputPaths(context)[0];
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        await File.WriteAllTextAsync(output, json, cancellationToken).ConfigureAwait(false);
        return ParseDefinitionsStep.Succeeded(context, InputPaths(context), output);
    }
}
