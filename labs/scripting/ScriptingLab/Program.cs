using Novolis.Scripting.Completion;
using Novolis.Scripting.Hosting;

var service = new CSharpScriptService();
var source = args.FirstOrDefault() ?? "1 + 2";
var diagnostics = service.Analyze(source);
foreach (var diagnostic in diagnostics)
{
    Console.WriteLine($"{diagnostic.Id}: {diagnostic.Message}");
}
var completionItems = await new CSharpScriptCompletion().CompleteAsync("pub", 3);
Console.WriteLine($"Completion items: {completionItems.Count}");

var result = await service.ExecuteAsync(source);
Console.WriteLine(result.Succeeded ? $"Result: {result.ReturnValue}" : "Script did not complete.");
