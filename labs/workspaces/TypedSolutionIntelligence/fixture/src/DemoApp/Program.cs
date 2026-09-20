using Novolis.Sample;
using Services;

var identityService = new IdentityService();
var widget = new Widget { Name = identityService.Resolve("lab") };
Console.WriteLine($"{widget.Name}:{WidgetKind.Gadget}");
