using Novolis.CodeGen.Pipeline;

namespace AssetStudioLab;

/// <summary>Runs parse → validate → normalize → IR → bake → C# with fingerprint skip.</summary>
public static class VisualAssetPipeline
{
    public static readonly string[] Profile =
    [
        "parse_definitions",
        "validate",
        "normalize",
        "compile_ir",
        "bake_products",
        "generate_csharp",
    ];

    public static PipelineRunner Create(string repoRoot)
    {
        var layout = new VisualPipelineLayout(repoRoot);
        IPipelineStep[] steps =
        [
            new ParseDefinitionsStep(),
            new ValidateStep(),
            new NormalizeStep(),
            new CompileIrStep(),
            new BakeProductsStep(),
            new GenerateCsharpStep(),
        ];
        return new PipelineRunner(steps, layout);
    }

    public static async Task<int> RunAsync(string repoRoot, string source, bool force = false, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.Combine(repoRoot, "source"));
        await File.WriteAllTextAsync(Path.Combine(repoRoot, "source", "asset.nvisual"), source, cancellationToken)
            .ConfigureAwait(false);
        var runner = Create(repoRoot);
        return await runner.RunProfileAsync(Profile, force, cancellationToken).ConfigureAwait(false);
    }
}
