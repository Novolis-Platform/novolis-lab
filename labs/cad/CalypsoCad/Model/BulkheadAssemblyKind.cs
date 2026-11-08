namespace CalypsoCad.Model;

/// <summary>Transverse bulkhead assembly specialization.</summary>
internal enum BulkheadAssemblyKind
{
    /// <summary>Mostly closed plate with designated access penetrations.</summary>
    Solid,

    /// <summary>Pressure-tight closed plate (habitability / machinery boundaries).</summary>
    Pressure,

    /// <summary>Annular transverse bulkhead with a substantial central opening.</summary>
    Ring,
}
