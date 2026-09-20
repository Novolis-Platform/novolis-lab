using Novolis.CodeGen.Bindings;
using Novolis.CodeGen.Bindings.Roslyn;

namespace TinyExprBindings;

/// <summary>
/// TinyExpr host — the happy path without custom phases or Roslyn hooks.
/// Audio/Raylib add verifiers, companions, and hooks on top of this same shape.
/// </summary>
internal static class TinyExprBindingCodegen
{
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
            .AddJob(
                BindingEmitJob.LibraryImport(
                    "tinyexpr interop",
                    TinyExprManifest.Interop.Id,
                    "TinyExprNative",
                    GeneratedFiles[0],
                    "TinyExprBindings.Generated",
                    "TinyExprBindings",
                    libraryConstantName: "TinyExprDll",
                    typeSummary: "Low-level TinyExpr C entry points."))
            .AddJob(
                BindingEmitJob.FacadeForward(
                    "tinyexpr facade",
                    TinyExprManifest.Facades.Id,
                    "TinyExpr",
                    GeneratedFiles[1],
                    "TinyExprBindings",
                    "TinyExprBindings",
                    facadeMethodImpl: TinyExprManifest.Interop.Policy.FacadeMethodImpl));

        return BindingCodegen.Generate(project, options, log);
    }
}
