using System.Numerics;

namespace TopDownDoom.Game.ImportedDoom;

internal sealed class ImportedDoomWallIndex
{
    private readonly Dictionary<(int X, int Z), List<ImportedDoomWallSegment>> _cells = [];
    private readonly float _cellSize;

    public ImportedDoomWallIndex(IReadOnlyList<ImportedDoomWallSegment> segments, float cellSize = 4f)
    {
        ArgumentNullException.ThrowIfNull(segments);
        _cellSize = Math.Max(0.5f, cellSize);
        foreach (var segment in segments)
        {
            Add(segment);
        }
    }

    public IEnumerable<ImportedDoomWallSegment> Query(Vector3 origin, Vector3 direction, float length)
    {
        var end = origin + direction * length;
        var minX = ToCell(MathF.Min(origin.X, end.X) - 0.5f);
        var maxX = ToCell(MathF.Max(origin.X, end.X) + 0.5f);
        var minZ = ToCell(MathF.Min(origin.Z, end.Z) - 0.5f);
        var maxZ = ToCell(MathF.Max(origin.Z, end.Z) + 0.5f);
        var seen = new HashSet<int>();
        for (var x = minX; x <= maxX; x++)
        {
            for (var z = minZ; z <= maxZ; z++)
            {
                if (!_cells.TryGetValue((x, z), out var cell))
                {
                    continue;
                }

                foreach (var segment in cell)
                {
                    if (seen.Add(segment.SourceLineIndex))
                    {
                        yield return segment;
                    }
                }
            }
        }
    }

    private void Add(ImportedDoomWallSegment segment)
    {
        var minX = ToCell(MathF.Min(segment.Start.X, segment.End.X) - 0.05f);
        var maxX = ToCell(MathF.Max(segment.Start.X, segment.End.X) + 0.05f);
        var minZ = ToCell(MathF.Min(segment.Start.Z, segment.End.Z) - 0.05f);
        var maxZ = ToCell(MathF.Max(segment.Start.Z, segment.End.Z) + 0.05f);
        for (var x = minX; x <= maxX; x++)
        {
            for (var z = minZ; z <= maxZ; z++)
            {
                if (!_cells.TryGetValue((x, z), out var cell))
                {
                    cell = [];
                    _cells[(x, z)] = cell;
                }

                cell.Add(segment);
            }
        }
    }

    private int ToCell(float value) => (int)MathF.Floor(value / _cellSize);
}
