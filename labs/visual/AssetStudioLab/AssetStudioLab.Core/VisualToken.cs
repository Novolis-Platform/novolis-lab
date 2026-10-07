namespace AssetStudioLab;

/// <summary>One token from visual source.</summary>
public readonly record struct VisualToken(
    VisualTokenKind Kind,
    string Text,
    int Position,
    VisualValue? Number = null);
