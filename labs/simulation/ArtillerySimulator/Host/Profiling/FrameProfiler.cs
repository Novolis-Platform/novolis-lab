using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace ArtillerySimulator;

/// <summary>Accumulates per-section frame timings for headless <c>--profile</c> runs.</summary>
internal sealed class FrameProfiler
{
    private readonly Dictionary<string, SectionStats> _sections = new(StringComparer.Ordinal);
    private readonly Stopwatch _frame = new();
    private readonly int _warmupFrames;
    private long _seen;
    private long _frames;
    private double _frameTotalMs;
    private double _frameMinMs = double.MaxValue;
    private double _frameMaxMs;
    private int _terrainLines;
    private int _roadLines;
    private int _dynamicLines;
    private int _hudLines;
    private bool _recording;

    public FrameProfiler(int warmupFrames = 15) => _warmupFrames = Math.Max(0, warmupFrames);

    public long Frames => _frames;

    /// <summary>Most recent completed frame duration (ms), including warmup frames.</summary>
    public double LastFrameMs { get; private set; }

    public void Reset()
    {
        _sections.Clear();
        _seen = 0;
        _frames = 0;
        _frameTotalMs = 0;
        _frameMinMs = double.MaxValue;
        _frameMaxMs = 0;
        LastFrameMs = 0;
        _recording = false;
        _terrainLines = 0;
        _roadLines = 0;
        _dynamicLines = 0;
        _hudLines = 0;
    }

    public void BeginFrame()
    {
        _seen++;
        _recording = _seen > _warmupFrames;
        _frame.Restart();
    }

    public void EndFrame()
    {
        _frame.Stop();
        LastFrameMs = _frame.Elapsed.TotalMilliseconds;
        if (!_recording)
            return;

        var ms = LastFrameMs;
        _frames++;
        _frameTotalMs += ms;
        if (ms < _frameMinMs)
            _frameMinMs = ms;
        if (ms > _frameMaxMs)
            _frameMaxMs = ms;
    }

    public double AverageFps()
    {
        var avg = _frames > 0 ? _frameTotalMs / _frames : LastFrameMs;
        return avg > 1e-6 ? 1000.0 / avg : 0;
    }

    public double InstantFps() => LastFrameMs > 1e-6 ? 1000.0 / LastFrameMs : 0;

    public void Time(string name, Action action)
    {
        var sw = Stopwatch.StartNew();
        action();
        sw.Stop();
        if (_recording)
            Accrue(name, sw.Elapsed.TotalMilliseconds);
    }

    public T Time<T>(string name, Func<T> action)
    {
        var sw = Stopwatch.StartNew();
        var result = action();
        sw.Stop();
        if (_recording)
            Accrue(name, sw.Elapsed.TotalMilliseconds);
        return result;
    }

    public void SetGeometry(int terrainLines, int roadLines, int dynamicLines, int hudLines)
    {
        _terrainLines = terrainLines;
        _roadLines = roadLines;
        _dynamicLines = dynamicLines;
        _hudLines = hudLines;
    }

    public string ToReport()
    {
        var sb = new StringBuilder(1024);
        var avg = _frames > 0 ? _frameTotalMs / _frames : 0;
        var fps = avg > 1e-6 ? 1000.0 / avg : 0;
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"frames={_frames}  avg={avg:F2}ms  min={SafeMin():F2}ms  max={_frameMaxMs:F2}ms  ~{fps:F1} FPS");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"geometry  terrainLines={_terrainLines}  roadLines={_roadLines}  dynamicLines={_dynamicLines}  hudLines={_hudLines}");
        sb.AppendLine("sections (avg ms / frame, share of frame):");
        foreach (var (name, stats) in _sections.OrderByDescending(kv => kv.Value.TotalMs))
        {
            var sectionAvg = _frames > 0 ? stats.TotalMs / _frames : 0;
            var share = avg > 1e-6 ? 100.0 * sectionAvg / avg : 0;
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"  {name,-18} {sectionAvg,8:F2} ms  {share,5:F1}%  (n={stats.Calls})");
        }

        return sb.ToString();
    }

    public string ToJson()
    {
        var avg = _frames > 0 ? _frameTotalMs / _frames : 0;
        var fps = avg > 1e-6 ? 1000.0 / avg : 0;
        var sb = new StringBuilder(2048);
        sb.Append('{');
        sb.Append(CultureInfo.InvariantCulture,
            $"\"frames\":{_frames},\"avgMs\":{avg:F3},\"minMs\":{SafeMin():F3},\"maxMs\":{_frameMaxMs:F3},\"fps\":{fps:F2},");
        sb.Append(CultureInfo.InvariantCulture,
            $"\"terrainLines\":{_terrainLines},\"roadLines\":{_roadLines},\"dynamicLines\":{_dynamicLines},\"hudLines\":{_hudLines},");
        sb.Append("\"sections\":{");
        var first = true;
        foreach (var (name, stats) in _sections.OrderByDescending(kv => kv.Value.TotalMs))
        {
            if (!first)
                sb.Append(',');
            first = false;
            var sectionAvg = _frames > 0 ? stats.TotalMs / _frames : 0;
            sb.Append(CultureInfo.InvariantCulture,
                $"\"{Escape(name)}\":{{\"avgMs\":{sectionAvg:F3},\"calls\":{stats.Calls}}}");
        }

        sb.Append("}}");
        return sb.ToString();
    }

    private void Accrue(string name, double ms)
    {
        if (!_sections.TryGetValue(name, out var stats))
        {
            stats = new SectionStats();
            _sections[name] = stats;
        }

        stats.TotalMs += ms;
        stats.Calls++;
    }

    private double SafeMin() => _frames > 0 ? _frameMinMs : 0;

    private static string Escape(string s) => s.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);

    private sealed class SectionStats
    {
        public double TotalMs;
        public long Calls;
    }
}
