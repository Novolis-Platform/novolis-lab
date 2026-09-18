namespace PulseStrip.Game;

using System.Numerics;
using Novolis.Simulation.Racing.Tracks;
using PulseStrip.Core;

/// <summary>
/// Builds a twisted AG ribbon (deck + walls) from 3D centerline + Möbius frames.
/// </summary>
internal sealed class TrackRibbonMesh
{
    public required Vector3[] DeckVerts { get; init; }
    public required int[] DeckIndices { get; init; }
    public required Vector3[] WallVerts { get; init; }
    public required int[] WallIndices { get; init; }
    public required Vector3[] RailTop { get; init; }
    public required Vector3[] RailBottom { get; init; }
    public required MobiusTrackFrames.SurfaceFrame[] Frames { get; init; }

    public static TrackRibbonMesh Build(RaceTrack track, float wallHeight = 3.2f, int maxSegments = 0)
    {
        var samples = track.CenterLineSamples;
        var half = (float)track.Geometry.HalfWidth;
        var framesAll = MobiusTrackFrames.Build(samples);
        if (samples.Count < 3 || framesAll.Length == 0)
        {
            return new TrackRibbonMesh
            {
                DeckVerts = [],
                DeckIndices = [],
                WallVerts = [],
                WallIndices = [],
                RailTop = [],
                RailBottom = [],
                Frames = [],
            };
        }

        if (maxSegments <= 0)
            maxSegments = Math.Clamp(samples.Count / 2, 400, 2400);
        var step = Math.Max(1, samples.Count / maxSegments);
        var ringCount = (samples.Count + step - 1) / step;
        var deckVerts = new List<Vector3>(ringCount * 2);
        var wallVerts = new List<Vector3>(ringCount * 4);
        var railTop = new List<Vector3>(ringCount * 2);
        var railBottom = new List<Vector3>(ringCount * 2);
        var usedFrames = new List<MobiusTrackFrames.SurfaceFrame>(ringCount);

        for (var i = 0; i < samples.Count; i += step)
        {
            var f = framesAll[i];
            usedFrames.Add(f);
            var left = f.Position - f.Right * half;
            var right = f.Position + f.Right * half;
            deckVerts.Add(left);
            deckVerts.Add(right);

            wallVerts.Add(left);
            wallVerts.Add(left + f.Up * wallHeight);
            wallVerts.Add(right);
            wallVerts.Add(right + f.Up * wallHeight);

            railBottom.Add(left);
            railBottom.Add(right);
            railTop.Add(left + f.Up * wallHeight);
            railTop.Add(right + f.Up * wallHeight);
        }

        var deckIdx = new List<int>();
        var wallIdx = new List<int>();
        var rings = deckVerts.Count / 2;
        for (var i = 0; i < rings; i++)
        {
            var j = (i + 1) % rings;
            var l0 = i * 2;
            var r0 = i * 2 + 1;
            var l1 = j * 2;
            var r1 = j * 2 + 1;
            deckIdx.Add(l0); deckIdx.Add(r0); deckIdx.Add(r1);
            deckIdx.Add(l0); deckIdx.Add(r1); deckIdx.Add(l1);

            var wl0 = i * 4;
            var wl1 = i * 4 + 1;
            var wl0n = j * 4;
            var wl1n = j * 4 + 1;
            wallIdx.Add(wl0); wallIdx.Add(wl1); wallIdx.Add(wl1n);
            wallIdx.Add(wl0); wallIdx.Add(wl1n); wallIdx.Add(wl0n);

            var wr0 = i * 4 + 2;
            var wr1 = i * 4 + 3;
            var wr0n = j * 4 + 2;
            var wr1n = j * 4 + 3;
            wallIdx.Add(wr0); wallIdx.Add(wr1n); wallIdx.Add(wr1);
            wallIdx.Add(wr0); wallIdx.Add(wr0n); wallIdx.Add(wr1n);
        }

        return new TrackRibbonMesh
        {
            DeckVerts = deckVerts.ToArray(),
            DeckIndices = deckIdx.ToArray(),
            WallVerts = wallVerts.ToArray(),
            WallIndices = wallIdx.ToArray(),
            RailTop = railTop.ToArray(),
            RailBottom = railBottom.ToArray(),
            Frames = usedFrames.ToArray(),
        };
    }
}
