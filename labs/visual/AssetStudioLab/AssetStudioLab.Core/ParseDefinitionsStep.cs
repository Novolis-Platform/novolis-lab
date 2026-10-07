using Novolis.CodeGen.Pipeline;

namespace AssetStudioLab;

/// <summary>Parses <c>.nvisual</c> sources into the canonical JSON AST.</summary>
public sealed class ParseDefinitionsStep : IPipelineStep
{
    public string Id => "parse_definitions";
    public string Description => "Parse visual sources";
    public IReadOnlyList<string> DependsOn { get; } = [];

    public IReadOnlyList<string> InputPaths(PipelineContext context) =>
        Directory.Exists(context.Layout.ManifestDir)
            ? Directory.GetFiles(context.Layout.ManifestDir, "*.nvisual")
            : [];

    public IReadOnlyList<string> ExpectedOutputPaths(PipelineContext context) =>
        [Path.Combine(context.StepArtifactsDir(Id), "document.json")];

    public async ValueTask<StepExecutionResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
    {
        var sources = InputPaths(context);
        var combined = new List<VisualDefinition>();
        foreach (var path in sources)
        {
            var text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            var parsed = VisualLanguageParser.Parse(text);
            if (!parsed.Success)
            {
                return new StepExecutionResult
                {
                    Status = StepStatus.Failed,
                    Error = new StepErrorRecord { Message = parsed.Errors[0].Message, Type = "Parse" },
                };
            }

            combined.AddRange(parsed.Document!.Definitions);
        }

        var output = ExpectedOutputPaths(context)[0];
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        await File.WriteAllTextAsync(output, VisualJson.Serialize(new VisualDocument(combined)), cancellationToken)
            .ConfigureAwait(false);
        return Succeeded(context, sources, output);
    }

    internal static StepExecutionResult Succeeded(PipelineContext context, IReadOnlyList<string> inputs, string output) =>
        new()
        {
            Status = StepStatus.Succeeded,
            Inputs = StepFileFingerprint.HashFiles(inputs, context.RepoRoot),
            Outputs = StepFileFingerprint.DescribeOutputs([output], context.RepoRoot),
        };
}
