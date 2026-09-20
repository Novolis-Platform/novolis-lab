using Novolis.CodeGen.Bindings;

namespace TinyExprBindings;

/// <summary>Reusable TinyExpr C ABI signatures (parameter names are part of emit parity).</summary>
internal static class TinyExprNativeSignatures
{
    public static NativeSignature Compile { get; } = NativeSignature.Create(
        NativeType.NativeInt,
        new NativeParameter("expression", NativeType.Utf8String),
        new NativeParameter("variables", NativeType.NativeInt),
        new NativeParameter("variableCount", NativeType.Int32),
        new NativeParameter("error", NativeType.Int32, NativeParameterModifier.Out));

    public static NativeSignature Eval { get; } = NativeSignature.Create(
        NativeType.Double,
        new NativeParameter("expression", NativeType.NativeInt));

    public static NativeSignature Free { get; } = NativeSignature.Create(
        NativeType.Void,
        new NativeParameter("expression", NativeType.NativeInt));

    public static NativeSignature Interp { get; } = NativeSignature.Create(
        NativeType.Double,
        new NativeParameter("expression", NativeType.Utf8String),
        new NativeParameter("error", NativeType.Int32, NativeParameterModifier.Out));
}
