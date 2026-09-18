namespace HumanoidLab.Ui;

/// <summary>Orthographic projection for stick panes.</summary>
internal enum StickViewMode
{
    /// <summary>World X → screen X, world Y → screen Y (front).</summary>
    FrontXy,

    /// <summary>World Z → screen X, world Y → screen Y (side; good for walk +Z).</summary>
    SideZy,
}
