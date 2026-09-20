namespace Novolis.Sample;

public sealed class Widget
{
    public required string Name { get; init; }
}

public enum WidgetKind
{
    Gadget = 0,
}

internal sealed class HiddenPart
{
}
