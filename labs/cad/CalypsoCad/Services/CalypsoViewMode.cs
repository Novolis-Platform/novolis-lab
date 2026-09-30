using System.Drawing;
using System.Numerics;
using CalypsoCad.Generation;
using CalypsoCad.Models;
using CalypsoCad.Services;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Services;

internal enum CalypsoViewMode
{
    Plan,
    Orbit,
    Interior,
}
