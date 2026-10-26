using System.Globalization;
using System.Text;
using System.Text.Json;
using CalypsoCad.Models;
using Novolis.Cad.Primitives;
using Novolis.Ship.Primitives;
using Novolis.Ship.Structure;
using Novolis.Ship.Topology;

namespace CalypsoCad.Generation;

internal static class CalypsoFunctionalCanvasEmitter
{
    private static readonly (string FileName, string Const, string Title, string Caption)[] Figures =
    [
        ("orbit-bow-quarter.png", "BOW_QTR", "Bow quarter", "OML + interiors · bow must stay inside blue wire"),
        ("layer-structure-wire.png", "LAYER_STRUCT_WIRE", "Structure layer (wire)", "Transverse web plates + flanges + girders"),
        ("layer-cutaway-structure.png", "LAYER_STRUCT", "Layer: structure + passageway cutouts", "S-STRUCT only · transverse rings with corridor/hatch gaps (cyan voids)"),
        ("layer-cutaway-fitout.png", "LAYER_FITOUT", "Layer: partitions + outfit", "A-WALL + outfit · no frame solids · hatch openings highlighted"),
        ("orbit-cutaway-long.png", "CUTAWAY", "Longitudinal cutaway (all layers)", "Combined orbit cutaway · wet-ship read"),
        ("plan-deck-m1.png", "PLAN_M1", "Deck −1 (M1)", "Plan · machinery / hold lower · verify ENG hatches and corridor spine"),
        ("plan-deck-0.png", "PLAN_0", "Deck 0", "Plan · crew / bridge / airlocks · footprints inside IML octagon"),
        ("plan-deck-p1.png", "PLAN_P1", "Deck +1 (P1)", "Plan · PAX band and upper hold · hatch gaps on partition walls"),
        ("interior-cutaway-portCorridor.png", "INT_CORR_CUT", "Port corridor · structure layer", "Interior cutaway · frames at spine + passageway cutout markers"),
        ("orbit-ramp-close.png", "RAMP", "Aft ramp", "Orbit · C40 hold door, coaming, ramp inside stern"),
        ("interior-solid-bridge.png", "INT_BRIDGE", "Bridge", "Interior solid · lock console + seats · CAL-INT bow stack"),
        ("interior-solid-portCorridor.png", "INT_CORR", "Port corridor (CORR_P)", "Interior · 3 m clear walk deck, rails, trunks"),
        ("interior-solid-airlockPort.png", "INT_AIRLOCK", "Port L-airlock", "Interior · vestibule + lock leaf · D3 exterior sealed"),
        ("interior-solid-cabin1.png", "INT_CREW1", "Crew cabin (CREW_1)", "Interior · 1.92×7.2 m clear · berths and locker"),
        ("interior-solid-pax1.png", "INT_PAX1", "Passenger cabin (PAX_1)", "Interior · deck +1 · lock berth + outfit band"),
        ("interior-solid-lounge.png", "INT_LOUNGE", "Lounge", "Interior · deck +1 bow · lock bar outfit"),
        ("interior-solid-engineering.png", "INT_ENG", "Engineering", "Interior · atrium tanks · service hatches on −1/0"),
    ];

    public static string Emit(string canvasPath, string? exportsDirectory = null)
    {
        var generated = CalypsoLockGenerator.Generate();
        exportsDirectory ??= Path.Combine(generated, "exports");
        var cadPath = Path.Combine(generated, "calypso.cadjson");
        var doc = JsonSerializer.Deserialize<CadDocument>(File.ReadAllText(cadPath), CadJson.Options)
                  ?? throw new InvalidOperationException("Failed to read calypso.cadjson");

        var checks = CalypsoAcceptance.Evaluate(doc);
        var circulation = ShipWalk.BuildCirculationTour(doc);
        var hold = ShipCad.Spaces(doc).FirstOrDefault(s =>
            string.Equals(s.Name, "HOLD", StringComparison.OrdinalIgnoreCase));
        var corr = ShipCad.Spaces(doc).FirstOrDefault(s =>
            string.Equals(s.Name, "CORR_P", StringComparison.OrdinalIgnoreCase) && s.Deck == 0)
            ?? ShipCad.Spaces(doc).FirstOrDefault(s =>
                string.Equals(s.Name, "CORR_P", StringComparison.OrdinalIgnoreCase));
        var visitedOps = new HashSet<Guid>(circulation.VisitedOpeningIds);
        if (hold is not null)
        {
            foreach (var id in ShipWalk.BuildOpenHatchCoverTourDetailed(doc, hold.Id, startAtAft: true).VisitedOpeningIds)
                visitedOps.Add(id);
        }

        var entryIds = new List<Guid>();
        if (hold is not null)
            entryIds.Add(hold.Id);
        if (corr is not null)
            entryIds.Add(corr.Id);
        var reachable = entryIds.Count == 0
            ? 0
            : ShipWalk.CountReachableOpenInteriorHatches(doc, entryIds);
        var coveragePct = reachable == 0
            ? 100
            : (int)Math.Round(visitedOps.Count * 100f / reachable, MidpointRounding.AwayFromZero);
        var decksVisited = ShipWalk.CountDistinctDecksOnTour(doc, circulation.Waypoints);
        var spaceCount = ShipCad.Spaces(doc).Count();
        var loa = ShipDocumentMetrics.GetLoaMeters(doc);

        var routeLabels = new List<string>();
        var last = "";
        foreach (var wp in circulation.Waypoints)
        {
            var label = wp.Label;
            if (string.IsNullOrWhiteSpace(label) || string.Equals(label, last, StringComparison.OrdinalIgnoreCase))
                continue;
            routeLabels.Add(label);
            last = label;
            if (routeLabels.Count >= 14)
                break;
        }

        var routeText = routeLabels.Count == 0
            ? "CORR_P spine tour (no waypoints)."
            : string.Join(" → ", routeLabels);

        var sb = new StringBuilder();
        sb.AppendLine("import {");
        sb.AppendLine("  Callout,");
        sb.AppendLine("  H1,");
        sb.AppendLine("  H2,");
        sb.AppendLine("  Row,");
        sb.AppendLine("  Stack,");
        sb.AppendLine("  Stat,");
        sb.AppendLine("  Table,");
        sb.AppendLine("  Text,");
        sb.AppendLine("  useHostTheme,");
        sb.AppendLine("} from \"cursor/canvas\";");
        sb.AppendLine();

        foreach (var fig in Figures)
        {
            var png = Path.Combine(exportsDirectory, fig.FileName);
            if (!File.Exists(png))
                throw new FileNotFoundException($"Missing export for canvas: {png}. Run --headless first.", png);
            var b64 = Convert.ToBase64String(File.ReadAllBytes(png));
            sb.AppendLine($"const {fig.Const} = \"data:image/png;base64,{b64}\";");
        }

        sb.AppendLine();
        var checksJson = JsonSerializer.Serialize(
            checks.Select(c => new { name = c.Name, ok = c.Ok, detail = c.Detail }),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        sb.AppendLine($"const ACCEPTANCE = {checksJson} as {{ name: string; ok: boolean; detail: string }}[];");
        sb.AppendLine($"const ROUTE = {JsonSerializer.Serialize(routeText)};");
        sb.AppendLine($"const GENERATED = {JsonSerializer.Serialize($"CAL-INT-GA-001 + CAL-HULL-CAD-001 · {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC")};");
        sb.AppendLine($"const EXPORTS_DIR = {JsonSerializer.Serialize(exportsDirectory)};");
        sb.AppendLine();

        sb.AppendLine("function Figure({ title, caption, src }: { title: string; caption: string; src: string }) {");
        sb.AppendLine("  const theme = useHostTheme();");
        sb.AppendLine("  return (");
        sb.AppendLine("    <Stack gap={8}>");
        sb.AppendLine("      <H2>{title}</H2>");
        sb.AppendLine("      <img");
        sb.AppendLine("        src={src}");
        sb.AppendLine("        alt={title}");
        sb.AppendLine("        style={{ width: \"100%\", maxWidth: \"100%\", display: \"block\", borderRadius: 4, border: `1px solid ${theme.stroke.secondary}` }}");
        sb.AppendLine("      />");
        sb.AppendLine("      <Text tone=\"secondary\" size=\"small\">{caption}</Text>");
        sb.AppendLine("    </Stack>");
        sb.AppendLine("  );");
        sb.AppendLine("}");
        sb.AppendLine();

        sb.AppendLine("export default function CalypsoFunctionalVessel() {");
        sb.AppendLine("  const rows = ACCEPTANCE.map((c) => [c.name, c.ok ? \"pass\" : \"fail\", c.detail]);");
        sb.AppendLine("  const tones = ACCEPTANCE.map((c) => (c.ok ? \"success\" : \"danger\"));");
        sb.AppendLine("  return (");
        sb.AppendLine("    <Stack gap={28} style={{ padding: 16, width: \"100%\", maxWidth: \"100%\" }}>");
        sb.AppendLine("      <H1>Calypso functional vessel (ops_plus)</H1>");
        sb.AppendLine("      <Text tone=\"secondary\" size=\"small\">");
        sb.AppendLine("        Source: {GENERATED} · PNG exports: {EXPORTS_DIR}");
        sb.AppendLine("      </Text>");
        sb.AppendLine("      <Row gap={16} wrap>");
        sb.AppendLine($"        <Stat label=\"LOA (m)\" value=\"{loa.ToString("0.##", CultureInfo.InvariantCulture)}\" />");
        sb.AppendLine($"        <Stat label=\"Spaces\" value=\"{spaceCount}\" />");
        sb.AppendLine($"        <Stat label=\"Open hatch coverage\" value=\"{coveragePct}%\" tone=\"{(coveragePct >= 70 ? "success" : "warning")}\" />");
        sb.AppendLine($"        <Stat label=\"Decks on spine tour\" value=\"{decksVisited}\" />");
        sb.AppendLine("      </Row>");
        sb.AppendLine("      <Callout tone=\"info\">");
        sb.AppendLine("        <Text size=\"small\">Circulation spine (CORR_P tour): {ROUTE}</Text>");
        sb.AppendLine("      </Callout>");
        sb.AppendLine("      <H2>Acceptance (CalypsoAcceptance)</H2>");
        sb.AppendLine("      <Table");
        sb.AppendLine("        headers={[\"Check\", \"Result\", \"Detail\"]}");
        sb.AppendLine("        rows={rows}");
        sb.AppendLine("        rowTone={tones}");
        sb.AppendLine("        striped");
        sb.AppendLine("      />");
        sb.AppendLine("      <H2>Orthographic and interior evidence</H2>");
        sb.AppendLine("      <Stack gap={32}>");

        foreach (var fig in Figures)
            sb.AppendLine($"        <Figure title=\"{EscapeTs(fig.Title)}\" caption=\"{EscapeTs(fig.Caption)}\" src={{{fig.Const}}} />");

        sb.AppendLine("      </Stack>");
        sb.AppendLine("    </Stack>");
        sb.AppendLine("  );");
        sb.AppendLine("}");
        sb.AppendLine();

        var dir = Path.GetDirectoryName(canvasPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(canvasPath, sb.ToString(), Encoding.UTF8);
        return canvasPath;
    }

    private static string EscapeTs(string s) =>
        s.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
}
