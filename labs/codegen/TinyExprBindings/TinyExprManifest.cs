using Novolis.CodeGen.Bindings;

namespace TinyExprBindings;

/// <summary>Typed C ABI + façade manifests for TinyExpr — the same shape Audio/Raylib use.</summary>
internal static class TinyExprManifest
{
    public static InteropExportsFragment Interop { get; } = new(
        Id: "tinyexpr",
        SchemaVersion: 1,
        Header: null,
        Description: "TinyExpr C expression evaluator LibraryImport surface.",
        DllName: "tinyexpr",
        Policy: new InteropPolicySpec(
            SuppressGcTransitionByFunction: [],
            NeverSuppressGcTransition: [],
            FacadeMethodImpl: "AggressiveInlining",
            UseDisableRuntimeMarshalling: true),
        Structs: [],
        Imports:
        [
            new("te_compile", TinyExprNativeSignatures.Compile, "Compiles an expression and returns an opaque TinyExpr handle."),
            new("te_eval", TinyExprNativeSignatures.Eval, "Evaluates a compiled TinyExpr expression."),
            new("te_free", TinyExprNativeSignatures.Free, "Frees a compiled TinyExpr expression."),
            new("te_interp", TinyExprNativeSignatures.Interp, "Parses and evaluates an expression in one call."),
        ]);

    public static FacadeTypesFragment Facades { get; } = new(
        Id: "facades",
        Types:
        [
            new FacadeTypeSpec(
                Name: "TinyExpr",
                Namespace: "TinyExprBindings",
                Folder: "Generated",
                TypeSummary: "Friendly forwards for TinyExpr's small C ABI.",
                Usings: ["TinyExprBindings.Generated"],
                Methods:
                [
                    new(
                        "Interpret",
                        "double Interpret(string expression, out int error)",
                        "TinyExprNative.te_interp(expression, out error)",
                        "Parses and evaluates one expression."),
                    new(
                        "Compile",
                        "nint Compile(string expression, nint variables, int variableCount, out int error)",
                        "TinyExprNative.te_compile(expression, variables, variableCount, out error)",
                        "Compiles an expression and returns an opaque handle."),
                    new(
                        "Evaluate",
                        "double Evaluate(nint expression)",
                        "TinyExprNative.te_eval(expression)",
                        "Evaluates an opaque compiled-expression handle."),
                    new(
                        "Free",
                        "void Free(nint expression)",
                        "TinyExprNative.te_free(expression)",
                        "Frees an opaque compiled-expression handle."),
                ]),
        ]);

    public static BindingManifestSource Source { get; } = BindingManifestSource.Create(Interop, Facades);
}
