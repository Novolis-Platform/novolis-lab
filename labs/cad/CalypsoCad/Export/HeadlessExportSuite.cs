using CalypsoCad.Fabrication;
using CalypsoCad.Review;

namespace CalypsoCad.Export;

/// <summary>
/// Single product export path for <c>--headless-export</c>:
/// companions → docs → stills → ship objects → videos.
/// </summary>
internal static class HeadlessExportSuite
{
    public static int Run(bool jsonOnly = false)
    {
        Console.WriteLine(
            "CalypsoCad --headless-export — stills @8K, video @4K, ship OBJ/STL/FBX/GLB");
        var failures = 0;

        void Step(string name, Func<int> run)
        {
            Console.WriteLine($"── {name}");
            try
            {
                var code = run();
                if (code != 0)
                {
                    failures++;
                    Console.WriteLine($"  FAIL {name} (exit {code})");
                }
                else
                {
                    Console.WriteLine($"  OK  {name}");
                }
            }
            catch (Exception ex)
            {
                failures++;
                Console.WriteLine($"  FAIL {name}: {ex.Message}");
                Console.Error.WriteLine(ex);
            }
        }

        Step("generate CAD companions", () =>
        {
            var dir = CalypsoHeadlessHost.EnsureGenerated();
            return File.Exists(Path.Combine(dir, "calypso.cadjson")) ? 0 : 2;
        });

        if (jsonOnly)
            return failures == 0 ? 0 : 1;

        Step("blueprints + manufacturer sheets", () => CalypsoAcceptanceGates.Blueprints());
        Step("fabrication release (CAL-MFG-002)", () =>
        {
            var html = CalypsoFabReleaseEmitter.Emit();
            Console.WriteLine($"  → {html}");
            return 0;
        });
        Step("structure audit descriptors", () => CalypsoAcceptanceGates.StructureAudit());
        Step("DNV design-review package", () =>
        {
            var html = CalypsoDnvReviewEmitter.Emit(runHeadlessExports: false);
            Console.WriteLine($"  → {html}");
            return 0;
        });
        Step("hull-first Views A–D", () => CalypsoAcceptanceGates.HullFirst());
        Step("bulkhead polygon-mesh views", () => CalypsoAcceptanceGates.BulkheadMesh());
        Step("curated 8K stills", () => CalypsoAcceptanceGates.CuratedStills());
        Step("ship mesh objects (OBJ/STL/FBX/GLB)", () => ShipObjectExporter.Run());
        Step("construction-orbit 4K video", () => CalypsoAcceptanceGates.ConstructionOrbit());
        Step("walkthrough 4K video", () => CalypsoAcceptanceGates.Walkthrough());

        Console.WriteLine(failures == 0
            ? "CalypsoCad --headless-export OK"
            : $"CalypsoCad --headless-export FAILED ({failures} step(s))");
        return failures == 0 ? 0 : 1;
    }
}
