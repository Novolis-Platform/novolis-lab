using Novolis.Physics.Abstractions;

namespace ArtillerySimulator;

/// <summary>Bilinear sampler over Kartverket NHM_DTM_25833 (Oslofjorden cutout).</summary>
internal sealed class OslofjordenHeightSampler : IHeightSampler
{
    public float SampleHeight(float x, float z) => OslofjordenSurface.SampleDrawHeight(x, z);

    public static float SampleMetersStatic(float x, float z)
    {
        var east = OslofjordenGeodesy.EastMeters;
        var north = OslofjordenGeodesy.NorthMeters;
        var cells = OslofjordenElevation.Count - 1;
        var u = Math.Clamp(x / east, 0f, 1f) * cells;
        var v = Math.Clamp(z / north, 0f, 1f) * cells;
        var i0 = (int)MathF.Floor(u);
        var j0 = (int)MathF.Floor(v);
        var i1 = Math.Min(i0 + 1, cells);
        var j1 = Math.Min(j0 + 1, cells);
        var tx = u - i0;
        var tz = v - j0;
        var h00 = At(i0, j0);
        var h10 = At(i1, j0);
        var h01 = At(i0, j1);
        var h11 = At(i1, j1);
        var south = h00 + (h10 - h00) * tx;
        var northH = h01 + (h11 - h01) * tx;
        return south + (northH - south) * tz;
    }

    private static float At(int i, int j) => OslofjordenElevation.Meters[j * OslofjordenElevation.Count + i];

    public static float MaxElevationMeters
    {
        get
        {
            try
            {
                short max = 0;
                foreach (var h in OslofjordenElevation.Meters)
                {
                    if (h > max)
                        max = h;
                }

                return max > 0 ? max : 200f;
            }
            catch (FileNotFoundException)
            {
                return 200f;
            }
        }
    }
}
