using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CoverageStudio.Models;

internal enum WorkPhase
{
    Queued,
    Running,
    Succeeded,
    Failed,
    Cancelled,
}
