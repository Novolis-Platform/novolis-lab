using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Novolis.Simulation.Humanoid;

namespace HumanoidLab.Ui;

/// <summary>Which Reach effector is being dragged (FrontXy plane).</summary>
internal enum ReachDragTarget
{
    None = 0,
    LeftHand,
    RightHand,
    Head,
}
