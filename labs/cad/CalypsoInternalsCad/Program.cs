using CalypsoInternalsCad.Pipeline;
using CalypsoInternalsCad.View;
using Novolis.Ship.Primitives;

namespace CalypsoInternalsCad;

internal static class Program
{
    public static int Main(string[] args)
    {
        var view = args.Any(a => string.Equals(a, "--view", StringComparison.OrdinalIgnoreCase));
        var outArg = FindArgValue(args, "--out");

        try
        {
            Console.WriteLine("CalypsoInternalsCad — CAL-INT drawings → CAD + OBJ");
            var result = InternalsCadPipeline.Run(outArg);
            var loa = ShipDocumentMetrics.GetLoaMeters(result.Cad);
            Console.WriteLine($"  out     {result.Directory}");
            Console.WriteLine($"  LOA     {loa:0.###} m");
            Console.WriteLine(
                $"  CAD     {result.Cad.Entities.Count} entities (spaces/walls/openings/meshes)");
            Console.WriteLine(
                $"  OBJ     {result.Obj.Groups} groups · {result.Obj.Vertices} verts · {result.Obj.Triangles} tris (skipped {result.Obj.SkippedEntities})");
            Console.WriteLine($"  files   calypso-internals.cadjson / .obj / .mtl / manifest.json");

            if (view)
            {
                Console.WriteLine("  view    opening Raylib orbit…");
                InternalsCadViewer.Run(result.Cad, "CalypsoInternalsCad — CAL-INT");
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static string? FindArgValue(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }

        return null;
    }
}
