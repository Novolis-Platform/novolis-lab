namespace CalypsoCad.Generation.Fabrication;

internal sealed record NestItem(string PartId, float Width, float Height, List<(float X, float Y)> Profile);
