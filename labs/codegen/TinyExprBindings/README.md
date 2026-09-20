# TinyExpr C-ABI binding walkthrough

This is the smallest complete consumer of `Novolis.CodeGen.Bindings` — the same pieces Audio and Raylib use, without verifiers, companions, or Roslyn hooks.

| Piece | TinyExpr | Audio / Raylib |
|-------|----------|----------------|
| Typed signatures | `TinyExprNativeSignatures.cs` | `AudioNativeSignatures` / `RaylibNativeSignatures` |
| Manifest fragments | `TinyExprManifest.cs` | `*InteropManifest` + façade manifests |
| Jobs | `BindingEmitJob.LibraryImport` + `FacadeForward` | same factories (plus shims/debug for Raylib) |
| Run | `BindingCodegen.Generate` | same, or generic host when hooks are required |

TinyExpr is a zlib-licensed C99 expression evaluator (`tinyexpr.c` / `tinyexpr.h`):

```c
double te_interp(const char *expression, int *error);
te_expr *te_compile(const char *expression, const te_variable *variables, int var_count, int *error);
double te_eval(const te_expr *expression);
void te_free(te_expr *expression);
```

## Run it

```powershell
dotnet run --project d:\novolis\novolis-lab\labs\codegen\TinyExprBindings\TinyExprBindings.csproj -- generate
dotnet run --project d:\novolis\novolis-lab\labs\codegen\TinyExprBindings\TinyExprBindings.csproj -- show
```

`LibraryImport` needs generated unsafe code, so the project sets `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>`.

## Calling TinyExpr

The lab regenerates and compiles without a native binary. To call it at runtime, provide a TinyExpr native library named `tinyexpr` (`tinyexpr.dll` on Windows):

```csharp
var value = TinyExpr.Interpret("sqrt(3^2 + 4^2)", out var error);
if (error != 0)
    throw new InvalidOperationException($"TinyExpr parse error at {error}.");
```

`te_compile` returns an opaque `nint` because `te_expr` is native-owned. Call `TinyExpr.Free` for every successful compile — ownership stays explicit in the façade.
