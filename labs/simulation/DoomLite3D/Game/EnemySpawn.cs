using System.Numerics;
using Novolis.Math.Arrays;

namespace DoomLite3D.Game;

internal readonly record struct EnemySpawn(GridIndex Cell, int SpriteIndex, EnemyKind Kind = EnemyKind.Grunt);
