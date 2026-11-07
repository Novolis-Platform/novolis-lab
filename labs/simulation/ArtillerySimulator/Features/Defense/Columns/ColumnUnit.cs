using System.Numerics;

namespace ArtillerySimulator;

internal struct ColumnUnit
{
    public int Id;
    public int Lane;
    public float Arc;
    public float X;
    public float Z;
    public float Offset;
    public float Hold;
    public float Scare;
    public bool Passing;
    public bool Alive;
    public string Posture;
}
