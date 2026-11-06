using System.Globalization;
using System.Text;
using System.Text.Json;
using CalypsoCad.Services;

namespace CalypsoCad.Generation;

/// <summary>TodoList slide deck over progressive structure-wire build-step PNGs.</summary>
internal static class CalypsoBuildStepCanvasEmitter
{
    public static string Emit(string canvasPath, bool runExport = false)
    {
        if (runExport)
        {
            var session = new CalypsoSession();
            session.RegenerateAndLoad();
            var renderer = new CalypsoRenderer(session);
            renderer.Fit();
            var written = HeadlessBuildStepExporter.Export(session, renderer);
            if (written.Count == 0)
                throw new InvalidOperationException("Build-step export produced no frames.");
        }

        var stepsDir = CalypsoProjectPaths.BuildStepsDirectory;
        var manifestPath = Path.Combine(stepsDir, "manifest.json");
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException(
                "Missing build-step manifest. Run with --build-steps first.",
                manifestPath);

        using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var root = doc.RootElement;
        var steps = root.GetProperty("steps").EnumerateArray().Select(s => new
        {
            Id = s.GetProperty("Id").GetString()!,
            Title = s.GetProperty("Title").GetString()!,
            FileName = s.GetProperty("FileName").GetString()!,
            EntityCount = s.GetProperty("EntityCount").GetInt32(),
        }).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("import {");
        sb.AppendLine("  Button,");
        sb.AppendLine("  Card,");
        sb.AppendLine("  CardBody,");
        sb.AppendLine("  CardHeader,");
        sb.AppendLine("  H1,");
        sb.AppendLine("  H2,");
        sb.AppendLine("  Pill,");
        sb.AppendLine("  Row,");
        sb.AppendLine("  Stack,");
        sb.AppendLine("  Text,");
        sb.AppendLine("  TodoListCard,");
        sb.AppendLine("  useCanvasState,");
        sb.AppendLine("  useHostTheme,");
        sb.AppendLine("} from \"cursor/canvas\";");
        sb.AppendLine();

        for (var i = 0; i < steps.Count; i++)
        {
            var pngPath = Path.Combine(stepsDir, steps[i].FileName);
            if (!File.Exists(pngPath))
                throw new FileNotFoundException("Missing build-step PNG", pngPath);
            var b64 = Convert.ToBase64String(File.ReadAllBytes(pngPath));
            sb.Append("const STEP_").Append(i.ToString("D2", CultureInfo.InvariantCulture));
            sb.Append(" = \"data:image/png;base64,").Append(b64).AppendLine("\";");
        }

        sb.AppendLine();
        sb.AppendLine("const STEPS = [");
        for (var i = 0; i < steps.Count; i++)
        {
            var s = steps[i];
            sb.Append("  { id: ").Append(JsonSerializer.Serialize(s.Id));
            sb.Append(", title: ").Append(JsonSerializer.Serialize(s.Title));
            sb.Append(", file: ").Append(JsonSerializer.Serialize(s.FileName));
            sb.Append(", entities: ").Append(s.EntityCount);
            sb.Append(", src: STEP_").Append(i.ToString("D2", CultureInfo.InvariantCulture));
            sb.AppendLine(" },");
        }
        sb.AppendLine("];");
        sb.AppendLine();
        sb.AppendLine("export default function CalypsoStructureBuildDeck() {");
        sb.AppendLine("  const theme = useHostTheme();");
        sb.AppendLine("  const [index, setIndex] = useCanvasState(\"calypso-build-step-index\", 0);");
        sb.AppendLine("  const i = Math.min(Math.max(0, index), STEPS.length - 1);");
        sb.AppendLine("  const step = STEPS[i];");
        sb.AppendLine("  const todos = STEPS.map((s, idx) => ({");
        sb.AppendLine("    id: s.id,");
        sb.AppendLine("    content: `${idx + 1}. ${s.title}`,");
        sb.AppendLine("    status: idx < i ? \"completed\" : idx === i ? \"in_progress\" : \"pending\",");
        sb.AppendLine("  }));");
        sb.AppendLine();
        sb.AppendLine("  return (");
        sb.AppendLine("    <Stack gap={16} style={{ padding: 20, maxWidth: 1280, color: theme.text.primary }}>");
        sb.AppendLine("      <Stack gap={6}>");
        sb.AppendLine("        <H1>Calypso structure build deck</H1>");
        sb.AppendLine("        <Text tone=\"secondary\">");
        sb.AppendLine("          Same angle as layer-structure-wire. TodoList steps reveal hull, then each frame station,");
        sb.AppendLine("          longitudinals, then each hatch — cumulative generation read.");
        sb.AppendLine("        </Text>");
        sb.AppendLine("        <Row gap={8} style={{ flexWrap: \"wrap\" }}>");
        sb.AppendLine($"          <Pill tone=\"info\">{steps.Count} slides</Pill>");
        sb.AppendLine("          <Pill>docs/renders/build-steps</Pill>");
        sb.AppendLine("          <Pill tone=\"neutral\">CAL-MFG-002</Pill>");
        sb.AppendLine("        </Row>");
        sb.AppendLine("      </Stack>");
        sb.AppendLine();
        sb.AppendLine("      <Row gap={16} style={{ alignItems: \"flex-start\" }}>");
        sb.AppendLine("        <Stack gap={12} style={{ width: 320, flexShrink: 0 }}>");
        sb.AppendLine("          <TodoListCard");
        sb.AppendLine("            todos={todos}");
        sb.AppendLine("            defaultExpanded");
        sb.AppendLine("            onTodoClick={(todo) => {");
        sb.AppendLine("              const next = STEPS.findIndex((s) => s.id === todo.id);");
        sb.AppendLine("              if (next >= 0) setIndex(next);");
        sb.AppendLine("            }}");
        sb.AppendLine("          />");
        sb.AppendLine("          <Row gap={8}>");
        sb.AppendLine("            <Button");
        sb.AppendLine("              disabled={i <= 0}");
        sb.AppendLine("              onClick={() => setIndex(Math.max(0, i - 1))}");
        sb.AppendLine("            >");
        sb.AppendLine("              Prev");
        sb.AppendLine("            </Button>");
        sb.AppendLine("            <Button");
        sb.AppendLine("              disabled={i >= STEPS.length - 1}");
        sb.AppendLine("              onClick={() => setIndex(Math.min(STEPS.length - 1, i + 1))}");
        sb.AppendLine("            >");
        sb.AppendLine("              Next");
        sb.AppendLine("            </Button>");
        sb.AppendLine("          </Row>");
        sb.AppendLine("        </Stack>");
        sb.AppendLine();
        sb.AppendLine("        <Card style={{ flex: 1, minWidth: 0 }}>");
        sb.AppendLine("          <CardHeader>");
        sb.AppendLine("            <Stack gap={4}>");
        sb.AppendLine("              <H2>{`${i + 1}/${STEPS.length} · ${step.title}`}</H2>");
        sb.AppendLine("              <Text tone=\"secondary\">{`${step.file} · ${step.entities} entities revealed`}</Text>");
        sb.AppendLine("            </Stack>");
        sb.AppendLine("          </CardHeader>");
        sb.AppendLine("          <CardBody>");
        sb.AppendLine("            <img");
        sb.AppendLine("              src={step.src}");
        sb.AppendLine("              alt={step.title}");
        sb.AppendLine("              style={{ width: \"100%\", height: \"auto\", borderRadius: 8, display: \"block\" }}");
        sb.AppendLine("            />");
        sb.AppendLine("          </CardBody>");
        sb.AppendLine("        </Card>");
        sb.AppendLine("      </Row>");
        sb.AppendLine("    </Stack>");
        sb.AppendLine("  );");
        sb.AppendLine("}");
        sb.AppendLine();

        Directory.CreateDirectory(Path.GetDirectoryName(canvasPath)!);
        File.WriteAllText(canvasPath, sb.ToString());
        return canvasPath;
    }
}
