using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace CoverageStudio.Services;

internal readonly record struct DotnetProcessResult(int ExitCode, string Output, bool TimedOut);
