// dotnet run --file d:\novolis\novolis-lab\labs\cad\CalypsoCad\scripts\verify-calypso-fab-release.cs
#:property TargetFramework=net10.0
#:property PublishAot=false
#:property PackAsTool=false
#:property ManagePackageVersionsCentrally=false
#:property ImplicitUsings=enable
#:property Nullable=enable
using System.Diagnostics;

var project = @"d:\novolis\novolis-lab\labs\cad\CalypsoCad\CalypsoCad.csproj";
var psi = new ProcessStartInfo("dotnet")
{
    Arguments = $"run --project \"{project}\" -p:NovolisUseProjectReferences=true -- --fab-release",
    UseShellExecute = false,
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    CreateNoWindow = true,
};
using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start CalypsoCad");
var stdout = proc.StandardOutput.ReadToEnd();
var stderr = proc.StandardError.ReadToEnd();
proc.WaitForExit();
if (proc.ExitCode != 0)
{
    Console.Error.WriteLine(stderr);
    Console.Error.WriteLine(stdout);
    Environment.Exit(proc.ExitCode);
}

psi.Arguments = $"run --project \"{project}\" -p:NovolisUseProjectReferences=true -- --acceptance";
using var acc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start acceptance");
stdout = acc.StandardOutput.ReadToEnd();
stderr = acc.StandardError.ReadToEnd();
acc.WaitForExit();
Console.WriteLine(stdout);
if (acc.ExitCode != 0)
{
    Console.Error.WriteLine(stderr);
    Environment.Exit(acc.ExitCode);
}

if (!stdout.Contains("fab release:", StringComparison.Ordinal))
{
    Console.Error.WriteLine("Acceptance did not run fab release checks.");
    Environment.Exit(1);
}

if (stdout.Contains("FAIL fab release:", StringComparison.Ordinal))
{
    Console.Error.WriteLine("Fab release checks failed.");
    Environment.Exit(1);
}

var visual = new ProcessStartInfo("dotnet")
{
    Arguments = "run --file d:\\novolis\\novolis-lab\\labs\\cad\\CalypsoCad\\scripts\\verify-calypso-visual-gate.cs",
    UseShellExecute = false,
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    CreateNoWindow = true,
};
using var vis = Process.Start(visual) ?? throw new InvalidOperationException("Failed to start visual gate");
stdout = vis.StandardOutput.ReadToEnd();
stderr = vis.StandardError.ReadToEnd();
vis.WaitForExit();
Console.WriteLine(stdout);
if (vis.ExitCode != 0)
{
    Console.Error.WriteLine(stderr);
    Environment.Exit(vis.ExitCode);
}

Console.WriteLine("Calypso fab release verify OK");
