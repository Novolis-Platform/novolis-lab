# TinyExpr C-ABI binding walkthrough

This is the smallest complete consumer of `Novolis.CodeGen.Bindings`:

- a typed `NativeSignature` manifest for four functions from [TinyExpr](https://github.com/codeplea/tinyexpr);
- one standard `BindingCodegenHost` run with no custom Roslyn hooks;
- generated `LibraryImport` declarations plus a small public façade.

TinyExpr is a zlib-licensed C99 expression evaluator contained in `tinyexpr.c` and `tinyexpr.h`. Its public API is deliberately small:

```c
double te_interp(const char *expression, int *error);
te_expr *te_compile(const char *expression, const te_variable *variables, int var_count, int *error);
double te_eval(const te_expr *expression);
void te_free(te_expr *expression);
```

## Run it

Generate and inspect the bindings:

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\codegen\TinyExprBindings\TinyExprBindings.csproj -- generate
dotnet run --project d:\novolis\novolis-lab\labs\codegen\TinyExprBindings\TinyExprBindings.csproj -- show
```

The source of truth is `TinyExprManifest.cs`. Each ABI parameter has an explicit name and `NativeType`; the only host setup is two `BindingEmitJob` declarations in `TinyExprBindingCodegen.cs`.
`LibraryImport` uses generated unsafe code, so the lab project deliberately includes `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>`.

## Calling TinyExpr

The lab compiles and regenerates without a native binary. It intentionally does not bundle TinyExpr or prescribe a C toolchain. To call it at runtime, provide a TinyExpr native library under the loader name `tinyexpr` (`tinyexpr.dll` on Windows) and invoke the generated façade:

```csharp
var value = TinyExpr.Interpret("sqrt(3^2 + 4^2)", out var error);
if (error != 0)
    throw new InvalidOperationException($"TinyExpr parse error at {error}.");
```

`te_compile` returns an opaque `nint` because `te_expr` is a native-owned structure. `TinyExpr.Free` must be called for every successful compile. The lab keeps ownership policy visible in the façade rather than pretending this generic C-ABI emitter can infer it.
