using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CoverageStudio.Models;

internal enum HostPhase
{
    Queued,
    Building,
    Testing,
    Parsing,
    Succeeded,
    Failed,
    Cancelled,
}
