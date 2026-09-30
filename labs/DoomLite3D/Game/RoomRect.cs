using System.Numerics;
using Novolis.Math.Arrays;

namespace DoomLite3D.Game;

internal readonly record struct RoomRect(int X, int Z, int Width, int Height, RoomKind Kind);
