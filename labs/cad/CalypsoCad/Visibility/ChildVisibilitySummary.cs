namespace CalypsoCad.Visibility;

/// <summary>Display-only aggregate of descendant effective visibility — not an editable state.</summary>
internal enum ChildVisibilitySummary
{
    None,
    AllVisible,
    AllHidden,
    Mixed,
}
