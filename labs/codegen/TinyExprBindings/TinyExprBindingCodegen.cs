using Novolis.CodeGen.Bindings;
using Novolis.CodeGen.Bindings.Roslyn;

namespace TinyExprBindings;

internal static class TinyExprBindingCodegen
{
    private const string ManifestPath = "labs/codegen/TinyExprBindings/TinyExprManifest.cs";

    public static IReadOnlyList<string> GeneratedFiles { get; } =
    [
        "labs/codegen/TinyExprBindings/Generated/TinyExprNative.g.cs",
        "labs/codegen/TinyExprBindings/Generated/TinyExpr.g.cs",
    ];

    public static int Generate(string repoRoot, TextWriter log)
    {
        var options = BindingCodegenOptions.Physical(
            repoRoot,
            TinyExprManifest.Source,
            "dotnet run --project labs/codegen/TinyExprBindings -- generate");

        var project = BindingProject.Create("TinyExpr")
            .RequireCompanion(ManifestPath, "the TinyExpr typed C-ABI manifest")
            .AddJob(
                new BindingEmitJob(
                    "tinyexpr interop",
                    FragmentKind.InteropExports,
                    "tinyexpr",
                    new LibraryImportEmitter(),
                    new EmitTarget(
                        "TinyExprNative",
                        EmitStrategy.LibraryImport,
                        GeneratedFiles[0],
                        "TinyExprBindings.Generated",
                        "TinyExprBindings",
                        "TinyExprDll",
                        "Low-level TinyExpr C entry points.",
                        null,
                        null)))
            .AddJob(
                new BindingEmitJob(
                    "tinyexpr facade",
                    FragmentKind.FacadeTypes,
                    "tinyexpr-facade",
                    new FacadeForwardEmitter(),
                    new EmitTarget(
                        "TinyExpr",
                        EmitStrategy.FacadeForward,
                        GeneratedFiles[1],
                        "TinyExprBindings",
                        "TinyExprBindings",
                        FacadeMethodImpl: "AggressiveInlining"),
                    FormatPolicy: BindingFormatPolicy.NormalizeWhitespace,
                    Slice: "TinyExpr"));

        var run = new BindingCodegenRun<TinyExprCodegenPhase, BindingEmitContext>
        {
            Project = project,
            Options = options,
            SelectPhase = _ => TinyExprCodegenPhase.Emit,
            CreateContext = (_, fragment, outputPath, fingerprint) => new BindingEmitContext
            {
                Environment = options.Environment,
                OutputPath = outputPath,
                Fragment = fragment,
                ManifestSha256 = fingerprint,
                RegenerateHint = options.RegenerateHint,
            },
        };

        return new BindingCodegenHost<TinyExprCodegenPhase, BindingEmitContext>().Generate(run, log);
    }
}

internal enum TinyExprCodegenPhase
{
    Emit,
}
