using System.Text;
using Novolis.Math.Geometry;

namespace AssetStudioLab;

/// <summary>Baked texture products from a material IR/AST.</summary>
public sealed record BakedMaps(int Width, int Height, Rgba32[] Albedo, Rgba32[] Roughness, Rgba32[] Normal);
