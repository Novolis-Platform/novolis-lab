using Novolis.CodeGen.Bindings;

namespace TinyExprBindings;

internal static class TinyExprManifest
{
    public static BindingManifestSource Source { get; } = BindingManifestSource.Create(
        new InteropExportsFragment(
            Id: "tinyexpr",
            SchemaVersion: 1,
            Header: null,
            Description: "TinyExpr C expression evaluator imports.",
            DllName: "tinyexpr",
            Policy: new InteropPolicySpec([], [], null, UseDisableRuntimeMarshalling: true),
            Structs: [],
            Imports:
            [
                new InteropImportSpec(
                    "te_compile",
                    NativeSignature.Create(
                        NativeType.NativeInt,
                        new NativeParameter("expression", NativeType.Utf8String),
                        new NativeParameter("variables", NativeType.NativeInt),
                        new NativeParameter("variableCount", NativeType.Int32),
                        new NativeParameter("error", NativeType.Int32, NativeParameterModifier.Out)),
                    "Compiles an expression and returns an opaque TinyExpr handle."),
                new InteropImportSpec(
                    "te_eval",
                    NativeSignature.Create(
                        NativeType.Double,
                        new NativeParameter("expression", NativeType.NativeInt)),
                    "Evaluates a compiled TinyExpr expression."),
                new InteropImportSpec(
                    "te_free",
                    NativeSignature.Create(
                        NativeType.Void,
                        new NativeParameter("expression", NativeType.NativeInt)),
                    "Frees a compiled TinyExpr expression."),
                new InteropImportSpec(
                    "te_interp",
                    NativeSignature.Create(
                        NativeType.Double,
                        new NativeParameter("expression", NativeType.Utf8String),
                        new NativeParameter("error", NativeType.Int32, NativeParameterModifier.Out)),
                    "Parses and evaluates an expression in one call."),
            ],
            Usings: null),
        new FacadeTypesFragment(
            Id: "tinyexpr-facade",
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
                        new FacadeMethodSpec(
                            "Interpret",
                            "double Interpret(string expression, out int error)",
                            "TinyExprNative.te_interp(expression, out error)",
                            "Parses and evaluates one expression."),
                        new FacadeMethodSpec(
                            "Compile",
                            "nint Compile(string expression, nint variables, int variableCount, out int error)",
                            "TinyExprNative.te_compile(expression, variables, variableCount, out error)",
                            "Compiles an expression and returns an opaque handle."),
                        new FacadeMethodSpec(
                            "Evaluate",
                            "double Evaluate(nint expression)",
                            "TinyExprNative.te_eval(expression)",
                            "Evaluates an opaque compiled-expression handle."),
                        new FacadeMethodSpec(
                            "Free",
                            "void Free(nint expression)",
                            "TinyExprNative.te_free(expression)",
                            "Frees an opaque compiled-expression handle."),
                    ]),
            ]));
}
