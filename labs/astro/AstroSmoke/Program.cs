using Novolis.Astro.Abstractions;
using Novolis.Astro.Assessment;
using Novolis.Astro.Catalog;
using Novolis.Astro.Overlay;
using Novolis.Astro.Plotting;
using Novolis.Astro.Routing;
using Novolis.Physics.Astro;

var catalog = DemoCatalog.Create();
var cost = RangeBandCostModel.CreatePrototypeCompatible();
var graph = RouteGraph.Build(catalog.All, maxRangeLy: 12, cost);
var transit = new ConstantSpeedTransitProfile(speedLyPerDay: 1.0);
var route = RoutePlanner.Find("sol", "haven", graph, transit);

Console.WriteLine($"Catalog: {catalog.Count} systems");
Console.WriteLine($"1 ly = {AstronomicalUnits.LyToMeters(1):E3} m; 1 pc ≈ {AstronomicalUnits.MetersToLy(AstronomicalUnits.PcToMeters(1)):0.###} ly");

var habit = new HabitabilityAssessor();
var strategic = new StrategicValueAssessor();
foreach (var system in catalog.All.OrderBy(s => s.Coords.DistanceFromOrigin))
{
    var h = habit.Assess(system);
    var s = strategic.Assess(system);
    var tags = system.Tags.Count == 0 ? "" : " [" + string.Join(',', system.Tags) + "]";
    Console.WriteLine(
        $"  {system.Id.Value,-12} {system.Name,-18} {system.SpectralClass,-4} {system.Coords.DistanceFromOrigin,5:0.0} ly  habit={h.Score:0}/{h.Tier,-8} strategic={s.Score:0}/{s.Tier}{tags}");
}

var gen = new SystemProfileGenerator();
var solProfile = gen.Generate(catalog.GetRequired("sol"), campaignSeed: 1001);
Console.WriteLine(
    $"Sol profile: mining={solProfile.Potential.Mining:0.00} agri={solProfile.Potential.Agriculture:0.00} " +
    $"industry={solProfile.Potential.Industry:0.00} elements={solProfile.Elements.Count}");

var overlay = new CatalogOverlay();
overlay.Bind(new OverlayEntry("Home", "sol", new Dictionary<string, string> { ["role"] = "origin" }));
overlay.Bind(new OverlayEntry("Nearest Neighbor", "proxima", new Dictionary<string, string> { ["role"] = "scout" }));
overlay.Bind(new OverlayEntry("Bright Beacon", "sirius", new Dictionary<string, string> { ["role"] = "nav-fix" }));
overlay.Bind(new OverlayEntry("Frontier Gate", "eridani", new Dictionary<string, string> { ["role"] = "staging" }));
overlay.Bind(new OverlayEntry("Refuge", "haven", new Dictionary<string, string> { ["role"] = "destination" }));
var overlayErrors = overlay.Validate(catalog);
Console.WriteLine(overlayErrors.Count == 0
    ? $"Overlay: {overlay.Entries.Count} aliases (valid)"
    : $"Overlay errors: {string.Join("; ", overlayErrors)}");

if (!route.Found)
{
    Console.Error.WriteLine("Route: not found");
    return 1;
}

var waypoints = route.WaypointIds.Select(id => catalog.GetRequired(id).Coords).ToList();
var bands = string.Join(", ", route.Accumulation.CountsByBand.Select(kv => $"{kv.Key}={kv.Value}"));
Console.WriteLine(
    $"Route: {string.Join(" → ", route.WaypointIds)} | {route.Accumulation.TotalLy:0.##} ly | cost {route.Accumulation.TotalCost:0.##} | {route.Accumulation.TotalDurationSeconds / 86400.0:0.##} d | bands [{bands}]");

var svgPath = Path.Combine(Path.GetTempPath(), "novolis-astro-smoke.svg");
var tsvPath = Path.Combine(Path.GetTempPath(), "novolis-astro-smoke.tsv");
File.WriteAllText(svgPath, PathSvgExporter.Export(waypoints));
File.WriteAllText(tsvPath, PathTsvExporter.Export(waypoints));
Console.WriteLine($"Plot: {svgPath}");
Console.WriteLine($"Tsv:  {tsvPath}");
Console.WriteLine("AstroSmoke OK");
return 0;

file static class DemoCatalog
{
    public static StarCatalog Create()
    {
        var catalog = new StarCatalog();
        catalog.Add(new StarSystem("sol", "Sol", new StarCoords(0, 0, 0), SpectralClass.G, ["home", "hub"]));
        catalog.Add(new StarSystem("proxima", "Proxima Centauri", new StarCoords(4.2, 0.1, -0.3), SpectralClass.M, ["nearest"]));
        catalog.Add(new StarSystem("alpha-cen", "Alpha Centauri", new StarCoords(4.4, 0.05, -0.2), SpectralClass.G, ["binary"]));
        catalog.Add(new StarSystem("barnard", "Barnard's Star", new StarCoords(5.9, 0.4, 1.1), SpectralClass.M));
        catalog.Add(new StarSystem("wolf359", "Wolf 359", new StarCoords(7.8, -0.6, 0.9), SpectralClass.M));
        catalog.Add(new StarSystem("sirius", "Sirius", new StarCoords(8.6, -0.2, 0.5), SpectralClass.A, ["bright"]));
        catalog.Add(new StarSystem("eridani", "Epsilon Eridani", new StarCoords(10.5, 0.3, -1.2), SpectralClass.K, ["frontier"]));
        catalog.Add(new StarSystem("procyon", "Procyon", new StarCoords(11.5, -1.6, -0.3), SpectralClass.F, ["bright"]));
        catalog.Add(new StarSystem("tau", "Tau Ceti", new StarCoords(11.9, -0.5, 0.4), SpectralClass.G, ["candidate"]));
        catalog.Add(new StarSystem("axolotl", "Axolotl", new StarCoords(13.6, 0.4, -0.5), SpectralClass.K, ["waystation"]));
        catalog.Add(new StarSystem("altair", "Altair", new StarCoords(16.7, 0.1, 1.5), SpectralClass.A));
        catalog.Add(new StarSystem("haven", "Haven", new StarCoords(16.0, 0.2, 0.8), SpectralClass.G, ["refuge", "colony"]));
        catalog.Add(new StarSystem("rimward", "Rimward Anchor", new StarCoords(19.5, -0.3, -0.6), SpectralClass.K, ["rim"]));
        return catalog;
    }
}
