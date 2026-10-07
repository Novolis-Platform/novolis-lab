namespace AssetStudioLab;

/// <summary>Thrown when visual source cannot be parsed.</summary>
public sealed class VisualParseException : Exception
{
    public VisualParseException(string message)
        : base(message)
    {
    }
}
