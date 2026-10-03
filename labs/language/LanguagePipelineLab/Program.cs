using Novolis.Language.RoboSharp;

var source = """
    move 2
    turn right
    print "pipeline"
    """;
var compilation = new RoboSharpCompiler().Compile(source);
Console.WriteLine($"Tokens: {compilation.SyntaxTree.Tokens.Count}");
Console.WriteLine($"Diagnostics: {compilation.Diagnostics.Count}");
foreach (var instruction in compilation.TeachingInstructions)
{
    Console.WriteLine($"IL: {instruction.Display}");
}

if (compilation.Program is not null)
{
    var session = new RoboSharpExecutionSession(compilation.Program);
    while (!session.Snapshot.IsCompleted)
    {
        var step = session.Step();
        Console.WriteLine($"{step.Kind}: {step.Description}");
    }
}
