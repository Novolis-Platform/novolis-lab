// dotnet run --file d:\novolis\novolis-lab\labs\cad\CalypsoCad\scripts\verify-calypso-visual-gate.cs
// Set CALYPSO_PREVIEW_SYNC=1 to refresh docs/preview from docs/renders.
#:property TargetFramework=net10.0
#:property PublishAot=false
#:property PackAsTool=false
#:property ManagePackageVersionsCentrally=false
#:property ImplicitUsings=enable
#:property Nullable=enable
using System.Diagnostics;
using System.Security.Cryptography;

var project = @"d:\novolis\novolis-lab\labs\cad\CalypsoCad\CalypsoCad.csproj";
var previewDir = @"d:\novolis\novolis-lab\labs\cad\CalypsoCad\docs\preview";
var exportsDir = @"d:\novolis\novolis-lab\labs\cad\CalypsoCad\docs\renders";

string[] curated =
[
    "orbit-cutaway-long.png",
    "layer-cutaway-structure.png",
    "plan-deck-0.png",
    "interior-solid-bridge.png",
    "layer-structure-wire.png",
];

var psi = new ProcessStartInfo("dotnet")
{
    Arguments = $"run --project \"{project}\" -p:NovolisUseProjectReferences=true -- --headless",
    UseShellExecute = false,
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    CreateNoWindow = true,
};
using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start CalypsoCad headless");
var stdout = proc.StandardOutput.ReadToEnd();
var stderr = proc.StandardError.ReadToEnd();
proc.WaitForExit();
if (proc.ExitCode != 0)
{
    Console.Error.WriteLine(stderr);
    Console.Error.WriteLine(stdout);
    Environment.Exit(proc.ExitCode);
}

var sync = string.Equals(Environment.GetEnvironmentVariable("CALYPSO_PREVIEW_SYNC"), "1", StringComparison.Ordinal);
var failures = 0;
foreach (var name in curated)
{
    var exportPath = Path.Combine(exportsDir, name);
    var baselinePath = Path.Combine(previewDir, name);
    if (!File.Exists(exportPath))
    {
        Console.Error.WriteLine($"Missing export: {exportPath}");
        failures++;
        continue;
    }

    if (sync)
    {
        Directory.CreateDirectory(previewDir);
        File.Copy(exportPath, baselinePath, overwrite: true);
        Console.WriteLine($"Synced preview: {name}");
        continue;
    }

    if (!File.Exists(baselinePath))
    {
        Console.Error.WriteLine($"Missing baseline: {baselinePath} (run with CALYPSO_PREVIEW_SYNC=1 once)");
        failures++;
        continue;
    }

    var exportHash = Sha256File(exportPath);
    var baseHash = Sha256File(baselinePath);
    if (!string.Equals(exportHash, baseHash, StringComparison.OrdinalIgnoreCase))
    {
        Console.Error.WriteLine($"Visual mismatch: {name}");
        failures++;
    }
}

if (failures > 0)
    Environment.Exit(1);

Console.WriteLine("Calypso visual gate OK");

static string Sha256File(string path)
{
    var hash = SHA256.HashData(File.ReadAllBytes(path));
    return Convert.ToHexString(hash).ToLowerInvariant();
}
