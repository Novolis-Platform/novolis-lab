using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CalypsoCad.Host;
using CalypsoCad.Rendering;
using CalypsoCad.Export;

namespace CalypsoCad.Generation;

/// <summary>Writes cursor/canvas TSX with embedded headless PNG data URLs.</summary>
internal static class CalypsoRenderCanvasEmitter
{
    private static readonly (string FileName, string Const, string Title, string Caption)[] MfgAnglesFigures =
    [
        ("orbit-bow-quarter.png", "ORBIT_BOW_Q", "Bow quarter", "Starboard bow quarter — stem taper and forward frames."),
        ("orbit-bow-on.png", "ORBIT_BOW_ON", "Bow on", "On-axis bow view along stem."),
        ("orbit-three-quarter-high.png", "ORBIT_3Q_HI", "Three-quarter high", "Elevated three-quarter orbit over midbody."),
        ("orbit-broadside.png", "ORBIT_BROAD", "Broadside", "Port/starboard profile — full 69 m LOA read."),
        ("orbit-low-pass.png", "ORBIT_LOW", "Low pass", "Low grazing orbit emphasizing sheer and ramp."),
        ("orbit-stern-quarter.png", "ORBIT_STERN_Q", "Stern quarter", "Aft quarter — hold door, coaming, ramp geometry."),
        ("orbit-stern-on.png", "ORBIT_STERN_ON", "Stern on", "Aft elevation on centerline."),
        ("orbit-top-down.png", "ORBIT_TOP", "Top down", "Plan-like orbit — beam and parallel midbody."),
        ("orbit-cutaway-long.png", "ORBIT_CUT_LONG", "Longitudinal cutaway", "Partial cutaway along centerline — decks, frames, passages (visual CI)."),
        ("orbit-cutaway-beam.png", "ORBIT_CUT_BEAM", "Beam cutaway", "Athwart cut — corridor and hold vertical extent."),
        ("orbit-ramp-close.png", "ORBIT_RAMP", "Ramp close", "Aft ramp and sill detail for CAL-MFG coaming parts."),
        ("plan-deck-m1.png", "PLAN_M1", "Deck −1 (M1)", "Lower deck plan — ENG, HOLD, catwalk hatches."),
        ("plan-deck-0.png", "PLAN_0", "Deck 0", "Main deck — bridge, crew, corridors (3 m CORR_W)."),
        ("plan-deck-p1.png", "PLAN_P1", "Deck +1 (P1)", "Passenger deck band and upper spine."),
        ("layer-cutaway-structure.png", "LAYER_STRUCT", "Structure cutaway", "S-STRUCT + passage voids — CAL-MFG-002 frame webs."),
        ("layer-structure-wire.png", "LAYER_WIRE", "Structure wire", "Frame wire read without solid skin."),
        ("layer-cutaway-fitout.png", "LAYER_FIT", "Fitout cutaway", "Partitions and outfit boxes on structure."),
        ("interior-solid-bridge.png", "INT_BRIDGE", "Bridge DK0", "Captain, pilots, watch seats — solid interior (visual CI)."),
        ("interior-solid-engineering.png", "INT_ENG", "Engineering", "ENG atrium outfit and tanks."),
        ("interior-cutaway-portCorridor.png", "INT_CORR", "Port corridor cutaway", "CORR_P vertical slice — hatch frames."),
        ("interior-solid-cargoVoid.png", "INT_HOLD", "Cargo void", "HOLD volume and C40 stack context."),
    ];

    public static string EmitMfgAngles(string canvasPath, string? exportsDirectory = null, bool runHeadless = false)
    {
        var (exportsDir, generatedUtc) = ResolveExports(exportsDirectory, runHeadless);
        return WriteCanvas(
            canvasPath,
            exportsDir,
            generatedUtc,
            componentName: "CalypsoMfgAngles",
            title: "Calypso manufacturing angles (CAL-MFG-002)",
            subtitle: "21 headless orbit / plan / layer / interior renders · 2560×1440 Silk export",
            figures: MfgAnglesFigures,
            grouped: false);
    }

    public static string EmitMfg002FullRenders(string canvasPath, string? exportsDirectory = null, bool runHeadless = false)
    {
        var (exportsDir, generatedUtc) = ResolveExports(exportsDirectory, runHeadless);
        var allPngs = Directory.Exists(exportsDir)
            ? Directory.GetFiles(exportsDir, "*.png", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .Where(n => n is not null)
                .Cast<string>()
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList()
            : [];

        var known = new HashSet<string>(MfgAnglesFigures.Select(f => f.FileName), StringComparer.OrdinalIgnoreCase);
        var figures = new List<(string FileName, string Const, string Title, string Caption)>(MfgAnglesFigures);
        foreach (var file in allPngs)
        {
            if (known.Contains(file))
                continue;
            figures.Add((file, ToConstId(file), HumanTitle(file), $"Headless export · {file}"));
        }

        return WriteCanvas(
            canvasPath,
            exportsDir,
            generatedUtc,
            componentName: "CalypsoMfg002Renders",
            title: "CAL-MFG-002 — full headless render set",
            subtitle: $"{figures.Count} PNGs embedded · post–frame-web-plate geometry · source: --headless",
            figures: figures,
            grouped: true);
    }

    private static (string ExportsDir, string GeneratedUtc) ResolveExports(string? exportsDirectory, bool runHeadless)
    {
        if (runHeadless)
        {
            var session = new CalypsoSession();
            session.RegenerateAndLoad();
            var renderer = new CalypsoRenderer(session);
            renderer.Fit();
            renderer.SyncInteriorFromSelection();
            var stills = HeadlessPngExporter.ExportViews(session, renderer, session.GeneratedDirectory);
            if (stills.Count == 0)
                throw new InvalidOperationException("Headless PNG export produced no frames.");
            exportsDirectory = CalypsoProjectPaths.RendersExportsDirectory;
        }
        else
        {
            _ = CalypsoLockGenerator.Generate();
            exportsDirectory ??= CalypsoProjectPaths.RendersExportsDirectory;
        }

        return (exportsDirectory, DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC");
    }

    private static string WriteCanvas(
        string canvasPath,
        string exportsDirectory,
        string generatedUtc,
        string componentName,
        string title,
        string subtitle,
        IReadOnlyList<(string FileName, string Const, string Title, string Caption)> figures,
        bool grouped)
    {
        var sb = new StringBuilder();
        sb.AppendLine("import { H1, H2, Stack, Text, useHostTheme } from \"cursor/canvas\";");
        sb.AppendLine();

        foreach (var fig in figures)
        {
            var png = Path.Combine(exportsDirectory, fig.FileName);
            if (!File.Exists(png))
                throw new FileNotFoundException($"Missing export for canvas: {png}. Run --headless first.", png);
            var b64 = Convert.ToBase64String(File.ReadAllBytes(png));
            sb.AppendLine($"const {fig.Const} = \"data:image/png;base64,{b64}\";");
        }

        sb.AppendLine();
        sb.AppendLine($"const GENERATED = {JsonEscape(generatedUtc)};");
        sb.AppendLine($"const EXPORTS_DIR = {JsonEscape(exportsDirectory)};");
        sb.AppendLine($"const FIGURE_COUNT = {figures.Count.ToString(CultureInfo.InvariantCulture)};");
        sb.AppendLine();

        if (grouped)
        {
            sb.AppendLine("type Fig = { constId: string; title: string; caption: string; file: string };");
            sb.AppendLine("const FIGS: Fig[] = [");
            foreach (var fig in figures)
                sb.AppendLine(
                    $"  {{ constId: \"{fig.Const}\", title: {JsonEscape(fig.Title)}, caption: {JsonEscape(fig.Caption)}, file: {JsonEscape(fig.FileName)} }},");
            sb.AppendLine("];");
            sb.AppendLine();
        }

        sb.AppendLine("function Figure({ title, caption, src }: { title: string; caption: string; src: string }) {");
        sb.AppendLine("  const theme = useHostTheme();");
        sb.AppendLine("  return (");
        sb.AppendLine("    <Stack gap={8}>");
        sb.AppendLine("      <H2>{title}</H2>");
        sb.AppendLine("      <img");
        sb.AppendLine("        src={src}");
        sb.AppendLine("        alt={title}");
        sb.AppendLine(
            "        style={{ width: \"100%\", maxWidth: \"100%\", display: \"block\", borderRadius: 4, border: `1px solid ${theme.stroke.secondary}` }}");
        sb.AppendLine("      />");
        sb.AppendLine("      <Text tone=\"secondary\" size=\"small\">{caption}</Text>");
        sb.AppendLine("    </Stack>");
        sb.AppendLine("  );");
        sb.AppendLine("}");
        sb.AppendLine();

        sb.AppendLine($"export default function {componentName}() {{");
        sb.AppendLine("  const theme = useHostTheme();");
        sb.AppendLine("  const byConst: Record<string, string> = {");
        foreach (var fig in figures)
            sb.AppendLine($"    {fig.Const}: {fig.Const},");
        sb.AppendLine("  };");
        sb.AppendLine("  return (");
        sb.AppendLine("    <Stack gap={24} style={{ padding: 16, width: \"100%\", maxWidth: \"100%\", color: theme.text.primary }}>");
        sb.AppendLine($"      <H1>{EscapeTs(title)}</H1>");
        sb.AppendLine("      <Text tone=\"secondary\" size=\"small\">");
        sb.AppendLine($"        {EscapeTs(subtitle)} · {{GENERATED}} · {{FIGURE_COUNT}} figures · {{EXPORTS_DIR}}");
        sb.AppendLine("      </Text>");

        if (!grouped)
        {
            WriteAnglesLayout(sb, figures);
        }
        else
        {
            WriteGroupedLayout(sb);
        }

        sb.AppendLine("    </Stack>");
        sb.AppendLine("  );");
        sb.AppendLine("}");

        var dir = Path.GetDirectoryName(canvasPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(canvasPath, sb.ToString(), Encoding.UTF8);
        return canvasPath;
    }

    private static void WriteAnglesLayout(StringBuilder sb, IReadOnlyList<(string FileName, string Const, string Title, string Caption)> figures)
    {
        void Section(string heading, IEnumerable<(string FileName, string Const, string Title, string Caption)> items)
        {
            sb.AppendLine($"      <H2>{EscapeTs(heading)}</H2>");
            sb.AppendLine("      <Stack gap={28}>");
            foreach (var fig in items)
                sb.AppendLine(
                    $"        <Figure title=\"{EscapeTs(fig.Title)}\" caption=\"{EscapeTs(fig.Caption)}\" src={{{fig.Const}}} />");
            sb.AppendLine("      </Stack>");
        }

        var orbit = figures.Where(f => f.FileName.StartsWith("orbit-", StringComparison.OrdinalIgnoreCase)).ToList();
        var plans = figures.Where(f => f.FileName.StartsWith("plan-", StringComparison.OrdinalIgnoreCase)).ToList();
        var layers = figures.Where(f => f.FileName.StartsWith("layer-", StringComparison.OrdinalIgnoreCase)).ToList();
        var interiors = figures.Where(f => f.FileName.StartsWith("interior-", StringComparison.OrdinalIgnoreCase)).ToList();

        Section("Orbit — bow and profile", orbit.Take(5));
        Section("Orbit — aft and cutaways", orbit.Skip(5));
        Section("Deck plans", plans);
        Section("Layer presets (G key)", layers);
        Section("Interiors", interiors);
    }

    private static void WriteGroupedLayout(StringBuilder sb)
    {
        sb.AppendLine("      {(() => {");
        sb.AppendLine("        const sections = new Map<string, Fig[]>();");
        sb.AppendLine("        for (const f of FIGS) {");
        sb.AppendLine("          let key = \"Other exports\";");
        sb.AppendLine("          if (f.file.startsWith(\"orbit-\")) key = \"Orbit exterior\";");
        sb.AppendLine("          else if (f.file.startsWith(\"plan-\")) key = \"Deck plans\";");
        sb.AppendLine("          else if (f.file.startsWith(\"layer-\")) key = \"Layer presets\";");
        sb.AppendLine("          else if (f.file.startsWith(\"interior-\")) key = \"Interior tour\";");
        sb.AppendLine("          else if (f.file.startsWith(\"catwalk-\")) key = \"Catwalk POV\";");
        sb.AppendLine("          if (!sections.has(key)) sections.set(key, []);");
        sb.AppendLine("          sections.get(key)!.push(f);");
        sb.AppendLine("        }");
        sb.AppendLine("        const order = [\"Orbit exterior\", \"Deck plans\", \"Layer presets\", \"Interior tour\", \"Catwalk POV\", \"Other exports\"];");
        sb.AppendLine("        return order.filter((k) => sections.has(k)).map((key) => (");
        sb.AppendLine("          <Stack key={key} gap={12}>");
        sb.AppendLine("            <H2>{key}</H2>");
        sb.AppendLine("            <Stack gap={28}>");
        sb.AppendLine("              {sections.get(key)!.map((f) => (");
        sb.AppendLine("                <Figure key={f.file} title={f.title} caption={f.caption} src={byConst[f.constId]} />");
        sb.AppendLine("              ))}");
        sb.AppendLine("            </Stack>");
        sb.AppendLine("          </Stack>");
        sb.AppendLine("        ));");
        sb.AppendLine("      })()}");
    }

    private static string ToConstId(string fileName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var id = Regex.Replace(stem, "[^A-Za-z0-9]+", "_").Trim('_').ToUpperInvariant();
        return string.IsNullOrEmpty(id) ? "IMG" : "IMG_" + id;
    }

    private static string HumanTitle(string fileName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName).Replace('-', ' ');
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(stem);
    }

    private static string JsonEscape(string s) => JsonSerializer.Serialize(s);

    private static string EscapeTs(string s) =>
        s.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
}
