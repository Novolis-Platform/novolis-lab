namespace CalypsoCad.Model;

/// <summary>Authoritative physical / organizational system categories for Calypso components.</summary>
internal enum CadSystem
{
    Hull,
    Transverse,
    Longitudinal,
    Deck,
    Closures,
    Machinery,
    Interior,
    Cargo,

    /// <summary>Explorer organization / ship root — not a manufactured system.</summary>
    Organization,

    /// <summary>Nonphysical reference geometry containers.</summary>
    Reference,

    /// <summary>Diagnostics and unclassified buckets.</summary>
    Diagnostics,
}
