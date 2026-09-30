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

/// <summary>One selectable FTL hop with machine-readable fields.</summary>
internal sealed record JumpInfo(
    int Index,
    string FromId,
    string ToId,
    string FromName,
    string ToName,
    double DistanceLy,
    double Cost,
    double CostPerLy,
    string Band,
    double BandMaxLy,
    double DurationDays,
    bool InRoute,
    int? RouteHopIndex)
{
    public override string ToString()
    {
        var route = InRoute ? $" route#{RouteHopIndex}" : "";
        return string.Create(
            CultureInfo.InvariantCulture,
            $"#{Index} {FromId} → {ToId}  {DistanceLy:0.##} ly  cost {Cost:0.##}  [{Band}]{route}");
    }

    public string ToParsable()
    {
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"index={Index}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"from={FromId}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"to={ToId}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"from_name={FromName}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"to_name={ToName}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"distance_ly={DistanceLy.ToString("0.####", CultureInfo.InvariantCulture)}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"cost={Cost.ToString("0.####", CultureInfo.InvariantCulture)}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"cost_per_ly={CostPerLy.ToString("0.####", CultureInfo.InvariantCulture)}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"band={Band}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"band_max_ly={BandMaxLy.ToString("0.####", CultureInfo.InvariantCulture)}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"duration_d={DurationDays.ToString("0.####", CultureInfo.InvariantCulture)}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"in_route={InRoute.ToString().ToLowerInvariant()}");
        if (RouteHopIndex is { } hop)
            sb.AppendLine(CultureInfo.InvariantCulture, $"route_hop_index={hop}");
        sb.AppendLine("cost_note=long_band_is_3x_short_so_one_long_jump_is_often_worse_than_several_short");
        return sb.ToString().TrimEnd();
    }
}
