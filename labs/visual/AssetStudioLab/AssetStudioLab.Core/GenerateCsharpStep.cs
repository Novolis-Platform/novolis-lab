using Novolis.CodeGen.Pipeline;
using Novolis.CodeGen.Reflection.Dump;

namespace AssetStudioLab;

/// <summary>Emits fluent C# plus a DumpVar snapshot of the AST.</summary>
public sealed class GenerateCsharpStep : IPipelineStep
{
    public string Id => "generate_csharp";
    public string Description => "Generate C# from visual AST";
    public IReadOnlyList<string> DependsOn { get; } = ["normalize"];

    public IReadOnlyList<string> InputPaths(PipelineContext context) =>
        [Path.Combine(context.StepArtifactsDir("normalize"), "normalized.json")];

    public IReadOnlyList<string> ExpectedOutputPaths(PipelineContext context) =>
        [Path.Combine(context.StepArtifactsDir(Id), "Materials.g.cs")];

    public async ValueTask<StepExecutionResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
    {
        var document = VisualJson.Deserialize(
            await File.ReadAllTextAsync(InputPaths(context)[0], cancellationToken).ConfigureAwait(false));
        var artifacts = context.StepArtifactsDir(Id);
        Directory.CreateDirectory(artifacts);
        var output = ExpectedOutputPaths(context)[0];
        await File.WriteAllTextAsync(output, VisualCsharpEmitter.Emit(document), cancellationToken).ConfigureAwait(false);
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(artifacts, "DocumentDump.g.cs"),
                document.DumpVar(),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await File.WriteAllTextAsync(
                Path.Combine(artifacts, "DocumentDump.g.cs"),
                "// DumpVar skipped: " + ex.Message,
                cancellationToken).ConfigureAwait(false);
        }
        return ParseDefinitionsStep.Succeeded(context, InputPaths(context), output);
    }
}
