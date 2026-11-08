namespace CalypsoCad.Blueprints.DrawingPackage;

/// <summary>One BOM row in the drawing package (structure plate or volume space).</summary>
internal sealed record DrawingPackageBomLine(
    string LineId,
    string Category,
    string PartOrSpaceId,
    string Description,
    string Unit,
    float Qty,
    float? MassKg,
    float? AreaM2,
    string? Material,
    string? DrawingNumber);
