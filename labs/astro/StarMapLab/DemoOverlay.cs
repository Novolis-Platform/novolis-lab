using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Novolis.Astro.Abstractions;
using Novolis.Astro.Assessment;
using Novolis.Astro.Catalog;
using Novolis.Astro.Overlay;
using Novolis.Astro.Routing;
using Novolis.Avalonia.StarMap;
using Novolis.Avalonia.Studio;
using Novolis.Physics.Astro;

namespace StarMapLab;

static class DemoOverlay
{
    public static CatalogOverlay Create()
    {
        var overlay = new CatalogOverlay();
        overlay.Bind(new OverlayEntry("Home", "sol", new Dictionary<string, string> { ["role"] = "origin" }));
        overlay.Bind(new OverlayEntry("Nearest Neighbor", "proxima-centauri", new Dictionary<string, string> { ["role"] = "scout" }));
        overlay.Bind(new OverlayEntry("Bright Beacon", "sirius", new Dictionary<string, string> { ["role"] = "nav-fix" }));
        overlay.Bind(new OverlayEntry("Frontier Gate", "epsilon-eridani", new Dictionary<string, string> { ["role"] = "staging" }));
        overlay.Bind(new OverlayEntry("Candidate World", "tau-ceti", new Dictionary<string, string> { ["role"] = "survey" }));
        overlay.Bind(new OverlayEntry("Cygnus Waypoint", "61-cygni", new Dictionary<string, string> { ["role"] = "resupply" }));
        overlay.Bind(new OverlayEntry("Aquila Beacon", "altair", new Dictionary<string, string> { ["role"] = "destination" }));
        overlay.Bind(new OverlayEntry("Dragon's Tail", "sigma-draconis", new Dictionary<string, string> { ["role"] = "outbound" }));
        return overlay;
    }
}
