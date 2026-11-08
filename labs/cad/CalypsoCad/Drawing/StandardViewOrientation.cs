namespace CalypsoCad.Drawing;

/// <summary>
/// Calypso ISO 128-15 conventions. Stern left / bow right for plan and profile.
/// Transverse views declare looking forward or aft explicitly.
/// </summary>
internal enum StandardViewOrientation
{
    /// <summary>Looking down (−Y). Drawing X = stern→bow (stern left). Drawing Y = −world X (port up on page when looking down from above… mapped as athwart).</summary>
    PlanSternLeft,

    /// <summary>Looking to port (+X). Drawing X = stern→bow. Drawing Y = world Y (up).</summary>
    ProfileSternLeft,

    /// <summary>Looking aft (−Z). Drawing X = −world X (matches existing package sheets). Drawing Y = world Y.</summary>
    TransverseLookingAft,

    /// <summary>Looking forward (+Z). Drawing X = +world X. Drawing Y = world Y.</summary>
    TransverseLookingForward,
}
