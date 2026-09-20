namespace TinyExprBindings;

internal static class Program
{
    private static int Main(string[] args)
    {
        var repoRoot = RepoRoot.Find();
        return args switch
        {
            [] or ["generate"] => Generate(repoRoot),
            ["show"] => ShowGeneratedFiles(repoRoot),
            ["help"] or ["--help"] or ["-h"] => PrintHelp(),
            _ => UnknownCommand(),
        };
    }

    private static int Generate(string repoRoot)
    {
        var exitCode = TinyExprBindingCodegen.Generate(repoRoot, Console.Out);
        if (exitCode == 0)
        {
            Console.WriteLine();
            Console.WriteLine("Open TinyExprManifest.cs / TinyExprNativeSignatures.cs for the typed C ABI.");
            Console.WriteLine("Run with 'show' to print the generated interop and façade files.");
        }

        return exitCode;
    }

    private static int ShowGeneratedFiles(string repoRoot)
    {
        foreach (var relativePath in TinyExprBindingCodegen.GeneratedFiles)
        {
            var path = Path.Combine(repoRoot, relativePath);
            Console.WriteLine($"// {relativePath}");
            Console.WriteLine(File.ReadAllText(path));
        }

        return 0;
    }

    private static int PrintHelp()
    {
        Console.WriteLine("""
            TinyExprBindings — minimal C ABI binding-emitter walkthrough

            Commands:
              generate  Emit the TinyExpr LibraryImport and friendly façade.
              show      Print the generated source files.

            TinyExpr is a zlib-licensed, two-file C expression evaluator.
            This lab generates bindings only; it never loads a native library.
            """);
        return 0;
    }

    private static int UnknownCommand()
    {
        Console.Error.WriteLine("Unknown command. Use 'generate', 'show', or 'help'.");
        return 1;
    }
}
