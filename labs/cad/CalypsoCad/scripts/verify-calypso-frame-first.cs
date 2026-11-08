//   dotnet run --file d:\novolis\novolis-lab\labs\cad\CalypsoCad\scripts\verify-calypso-frame-first.cs
#:package Novolis.Cad.Primitives@2026.1.*
#:property TargetFramework=net10.0
#:property PublishAot=false
#:property PackAsTool=false
#:property ManagePackageVersionsCentrally=false
#:property ImplicitUsings=enable
#:property Nullable=enable

using System.Text.Json;
using Novolis.Cad.Primitives;

var generated = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "Novolis", "CalypsoCad", "generated");
var cadPath = Path.Combine(generated, "calypso.cadjson");
if (!File.Exists(cadPath))
{
    Console.Error.WriteLine($"Missing {cadPath} — run CalypsoCad --generate-only first.");
    Environment.Exit(1);
}

var doc = JsonSerializer.Deserialize<CadDocument>(File.ReadAllText(cadPath))!;
var failures = 0;
void Check(string name, bool ok, string detail = "")
{
    if (ok)
        Console.WriteLine($"  OK  {name}");
    else
    {
        failures++;
        Console.WriteLine($"  FAIL {name}{(detail.Length == 0 ? "" : ": " + detail)}");
    }
}

var webPlates = doc.Entities.Count(e => e.Name?.Contains("-webplate-", StringComparison.OrdinalIgnoreCase) == true);
var knees = doc.Entities.Count(e => e.Name?.Contains("-knee-", StringComparison.OrdinalIgnoreCase) == true);
var girders = doc.Entities.Count(e => e.Name?.StartsWith("girder-", StringComparison.OrdinalIgnoreCase) == true);
var deckBeams = doc.Entities.Count(e => e.Name?.Contains("-deckbeam-", StringComparison.OrdinalIgnoreCase) == true);
var brackets = doc.Entities.Count(e => e.Name?.Contains("-flange-", StringComparison.OrdinalIgnoreCase) == true);

Console.WriteLine("Calypso frame-first structure");
Check("transverse web plates", webPlates >= 200, $"count={webPlates}");
Check("no portal knees", knees == 0, $"kneeSolids={knees}");
Check("longitudinal girders", girders >= 24, $"count={girders}");
Check("transverse deck beams", deckBeams >= 30, $"count={deckBeams}");
Check("frame flanges on octagon", brackets >= 40, $"count={brackets}");

Environment.Exit(failures == 0 ? 0 : 1);
