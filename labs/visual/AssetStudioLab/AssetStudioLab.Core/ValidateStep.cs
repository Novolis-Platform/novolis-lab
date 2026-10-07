using Novolis.CodeGen.Pipeline;

namespace AssetStudioLab;

/// <summary>Validates the parsed AST.</summary>
public sealed class ValidateStep : IPipelineStep
{
    public string Id => "validate";
    public string Description => "Validate visual AST";
    public IReadOnlyList<string> DependsOn { get; } = ["parse_definitions"];

    public IReadOnlyList<string> InputPaths(PipelineContext context) =>
        [Path.Combine(context.StepArtifactsDir("parse_definitions"), "document.json")];

    public IReadOnlyList<string> ExpectedOutputPaths(PipelineContext context) =>
        [Path.Combine(context.StepArtifactsDir(Id), "diagnostics.json")];

    public async ValueTask<StepExecutionResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
    {
        var input = InputPaths(context)[0];
        var document = VisualJson.Deserialize(await File.ReadAllTextAsync(input, cancellationToken).ConfigureAwait(false));
        var ir = VisualCompiler.Compile(document);
        var output = ExpectedOutputPaths(context)[0];
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        await File.WriteAllTextAsync(output, VisualJson.SerializeDiagnostics(ir.Diagnostics), cancellationToken)
            .ConfigureAwait(false);
        if (!ir.Success)
        {
            return new StepExecutionResult
            {
                Status = StepStatus.Failed,
                Error = new StepErrorRecord { Message = ir.Diagnostics[0].Message, Type = "Validate" },
            };
        }

        return ParseDefinitionsStep.Succeeded(context, InputPaths(context), output);
    }
}
