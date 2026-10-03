using Novolis.Inspection.Http;
using Novolis.Inspection.Json;
using Novolis.Inspection.ObjectGraph;
using Novolis.Inspection.Xml;

var json = new JsonInspectionFormatter().Format("""{"items":[1,2],"state":"ready"}""");
var xml = new XmlInspectionFormatter().Format("""<root id="1"><item>ready</item></root>""");
var graph = new ObjectGraphInspectionFormatter().Format(new { Name = "inspection", Values = new[] { 1, 2, 3 } });
var http = new HttpInspectionFormatter().Format(new HttpMessageInspection(
    "response",
    "GET",
    "https://example.test/items",
    200,
    new Dictionary<string, string[]> { ["content-type"] = ["application/json"] },
    """{"state":"ready"}"""));
Console.WriteLine($"JSON children: {json.Root.Items.Count}");
Console.WriteLine($"XML children: {xml.Root.Items.Count}");
Console.WriteLine($"Object children: {graph.Root.Items.Count}");
Console.WriteLine($"HTTP children: {http.Root.Items.Count}");
