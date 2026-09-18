using System.Numerics;
using Novolis.Math.Geometry;
using Novolis.ThreeD;

namespace ChiselCorvetteBuilder;

internal static class ShipArray
{
    public static EditableMesh Linear(EditableMesh cell, int count, Vector3 offset)
    {
        ArgumentNullException.ThrowIfNull(cell);
        count = System.Math.Max(1, count);
        var result = cell.Clone();
        for (var i = 1; i < count; i++)
        {
            var copy = cell.Clone();
            copy.Transform(Matrix4x4.CreateTranslation(offset * i));
            result = MeshBoolean.Concat(result, copy);
        }
        return result;
    }

    public static EditableMesh Grid(EditableMesh cell, int rows, int cols, Vector3 rowPitch, Vector3 colPitch)
    {
        ArgumentNullException.ThrowIfNull(cell);
        rows = System.Math.Max(1, rows);
        cols = System.Math.Max(1, cols);
        EditableMesh? result = null;
        for (var r = 0; r < rows; r++)
        {
            for (var c = 0; c < cols; c++)
            {
                var copy = cell.Clone();
                copy.Transform(Matrix4x4.CreateTranslation(rowPitch * r + colPitch * c));
                result = result is null ? copy : MeshBoolean.Concat(result, copy);
            }
        }
        return result ?? cell.Clone();
    }

    public static EditableMesh SymmetricX(EditableMesh portHalf)
    {
        ArgumentNullException.ThrowIfNull(portHalf);
        var stbd = portHalf.Clone();
        stbd.Transform(Matrix4x4.CreateScale(-1f, 1f, 1f));
        stbd.ReverseWinding();
        return MeshBoolean.Concat(portHalf, stbd);
    }
}
