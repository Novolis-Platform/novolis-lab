namespace MeshBench.Models;

internal sealed class MeshPartRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "Part";

    public string Kind { get; set; } = "box";

    public float[] Center { get; set; } = [0f, 0.5f, 0f];

    public float[] HalfExtents { get; set; } = [0.5f, 0.5f, 0.5f];

    public float Radius { get; set; } = 0.5f;

    public float[] Color { get; set; } = [0.72f, 0.35f, 0.28f];

    public MeshPartRecord Clone()
    {
        return new MeshPartRecord
        {
            Id = Guid.NewGuid(),
            Name = Name + " copy",
            Kind = Kind,
            Center = (float[])Center.Clone(),
            HalfExtents = (float[])HalfExtents.Clone(),
            Radius = Radius,
            Color = (float[])Color.Clone(),
        };
    }

    public string Summary =>
        Kind.Equals("sphere", StringComparison.OrdinalIgnoreCase)
            ? $"{Name} — sphere r={Radius:0.##}"
            : $"{Name} — box {HalfExtents[0]:0.##}×{HalfExtents[1]:0.##}×{HalfExtents[2]:0.##}";
}
