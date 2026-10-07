namespace AssetStudioLab;

/// <summary>Runs the visual platform without a window.</summary>
public static class AssetStudioHarness
{
    public static async Task<AssetStudioReport> RunAsync(string? workDirectory = null)
    {
        var parsed = VisualLanguageParser.Parse(VisualSamples.NavalSteel);
        if (!parsed.Success)
            return Fail("Parse failed: " + parsed.Errors[0].Message);

        var document = parsed.Document!;
        var commands = VisualCommandEditor.Apply(VisualDocument.Empty, "Metal(0.7); Roughness(0.65); Noise(Roughness, 0.02, 0.08); EdgeWear(0.1);");
        if (!commands.Success)
            return Fail(commands.Message);

        var ir = VisualCompiler.Compile(document);
        if (!ir.Success)
            return Fail(ir.Diagnostics[0].Message);

        var flashlight = VisualLanguageParser.Parse(VisualSamples.MarineFlashlight).Document!.Definitions[0];
        var fog = VisualLanguageParser.Parse(VisualSamples.ShipFog).Document!.Definitions[0];
        var beam = BeamComposition.Render(flashlight, fog, 48, 48);
        var luminance = BeamComposition.MeanLuminance(beam);

        var work = workDirectory ?? Path.Combine(Path.GetTempPath(), "asset-studio-bake-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        var exit = await VisualAssetPipeline.RunAsync(work, VisualSamples.NavalSteel);

        await using var timeline = await VisualStudioTimeline.CreateAsync();
        var clean = await timeline.SaveAsync(VisualSamples.NavalSteel, "Clean");
        await timeline.BranchAsync("Worn", clean.Id);
        var wornSource = VisualLanguageParser.Parse(VisualSamples.NavalSteel).Document!;
        var worn = VisualCommandEditor.Apply(wornSource, "EdgeWear(0.8)");
        await timeline.SaveAsync(VisualLanguageFormatter.Format(worn.Document), "Derelict");
        var rows = await timeline.RowsAsync();

        var silk = ir.Definitions.Count > 0 ? SilkVisualLowering.Lower(ir.Definitions[0]) : null;
        return new AssetStudioReport(
            exit == 0 && luminance > 0.001f && ir.Success,
            VisualLanguageFormatter.Format(document),
            VisualStackPrinter.Print(document),
            VisualCsharpEmitter.Emit(document),
            ir,
            silk,
            luminance,
            exit,
            rows.Select(r => $"{r.Branch} {r.Label}").ToArray(),
            "ok");
    }

    private static AssetStudioReport Fail(string message) =>
        new(false, "", "", "", new VisualIr([], [new CompileDiagnostic(CompileDiagnosticSeverity.Error, message)]), null, 0, 1, [], message);
}
