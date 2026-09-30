using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using CursorRemote.Protocol;

namespace CursorRemote.Desktop.Windows;

internal readonly record struct CaptureRegion(
    int OriginX,
    int OriginY,
    int Width,
    int Height);
