namespace PulseStrip.Core;

using System.Numerics;
using Novolis.Simulation.Racing.Tracks;

/// <summary>
/// Bakes long AG circuits by stamping road/wall along the spline
/// (avoids O(W×H×S) full-grid scans so ~100× CompactOval length is practical).
/// </summary>
public sealed class PulseStripTrackBuilder
{
    public const int SampleCount = 4000;

    public RaceTrack Build(ITrackDefinition definition)
    {
        var spec = definition.BuildSpec;
        var validation = CenterSplineMath.Validate(spec.CenterLine, sampleCount: 2000);
        if (!validation.Ok)
            throw new InvalidOperationException($"Center spline invalid: {validation.Message}");

        var rawPts = spec.CenterLine.ControlPoints;
        var minP = rawPts[0];
        var maxP = rawPts[0];
        foreach (var p in rawPts)
        {
            minP = Vector3.Min(minP, p);
            maxP = Vector3.Max(maxP, p);
        }

        var half = spec.TrackHalfWidth;
        var wall = spec.WallThickness;
        var paintR = half + wall + 2.0;
        var margin = (float)(paintR + 8);
        var shift = new Vector3(-(minP.X - margin), 0f, -(minP.Z - margin));
        // Keep elevation so hills / valleys / Möbius seating stay in 3D.
        var shiftedPts = rawPts.Select(p => new Vector3(p.X + shift.X, p.Y, p.Z + shift.Z)).ToArray();
        var loop = new SplineLoop(shiftedPts);

        var revalidate = CenterSplineMath.Validate(loop, 2000);
        if (!revalidate.Ok)
            throw new InvalidOperationException($"Shifted spline invalid: {revalidate.Message}");

        var sampler = new CatmullRomSplineSampler();
        var samples = sampler.SampleEvenly(loop, SampleCount);
        var positions = samples.Select(s => s.Position).ToArray();
        var tangents = samples.Select(s => s.Tangent).ToArray();

        var arcLens = new double[SampleCount];
        arcLens[0] = 0;
        for (var i = 1; i < SampleCount; i++)
            arcLens[i] = arcLens[i - 1] + Vector3.Distance(positions[i], positions[i - 1]);
        var totalArc = arcLens[SampleCount - 1] + Vector3.Distance(positions[SampleCount - 1], positions[0]);

        var progressMap = new TrackProgressMap
        {
            Samples = positions,
            Tangents = tangents,
            CumulativeArcLengths = arcLens,
            TotalArcLength = totalArc,
        };

        var max = positions[0];
        foreach (var p in positions)
            max = Vector3.Max(max, p);
        var width = (int)Math.Ceiling(max.X + margin);
        var height = (int)Math.Ceiling(max.Z + margin);
        width = Math.Clamp(width, 32, 8192);
        height = Math.Clamp(height, 32, 8192);

        var cells = new TrackCell[width, height];
        var rCeil = (int)Math.Ceiling(paintR);
        var halfF = (float)half;
        var wallF = (float)wall;
        for (var s = 0; s < SampleCount; s++)
        {
            var p = positions[s];
            var cx = (int)MathF.Floor(p.X);
            var cz = (int)MathF.Floor(p.Z);
            for (var dz = -rCeil; dz <= rCeil; dz++)
            {
                for (var dx = -rCeil; dx <= rCeil; dx++)
                {
                    var col = cx + dx;
                    var row = cz + dz;
                    if ((uint)col >= (uint)width || (uint)row >= (uint)height)
                        continue;
                    var dist = MathF.Sqrt(dx * dx + dz * dz);
                    if (dist <= halfF)
                        cells[col, row] = TrackCell.Road;
                    else if (dist <= halfF + wallF && cells[col, row] == TrackCell.Empty)
                        cells[col, row] = TrackCell.Wall;
                }
            }
        }

        var gates = new List<TrackGate>();
        foreach (var (index, t) in spec.GateSamples.Select((t, i) => (i, t)))
        {
            var sampleIdx = (int)(t * SampleCount) % SampleCount;
            var sample = samples[sampleIdx];
            var a = sample.Position - sample.Normal * halfF;
            var b = sample.Position + sample.Normal * halfF;
            gates.Add(new TrackGate(index, a, b, t));
        }

        var startIdx = (int)(spec.StartSample * SampleCount) % SampleCount;
        var startSample = samples[startIdx];
        var sc = (int)startSample.Position.X;
        var sr = (int)startSample.Position.Z;
        if ((uint)sc < (uint)width && (uint)sr < (uint)height)
            cells[sc, sr] = TrackCell.StartFinish;

        var leftBoundary = samples.Select(s => s.Position - s.Normal * halfF).ToArray();
        var rightBoundary = samples.Select(s => s.Position + s.Normal * halfF).ToArray();

        return new RaceTrack
        {
            Id = definition.Id,
            Name = definition.Name,
            Width = width,
            Height = height,
            Cells = cells,
            CenterLineSamples = positions,
            Gates = gates,
            StartPose = new TrackStartPose(startSample.Position, startSample.Tangent),
            ProgressMap = progressMap,
            Geometry = new TrackGeometry
            {
                LeftBoundary = leftBoundary,
                RightBoundary = rightBoundary,
                HalfWidth = half,
            },
        };
    }
}
