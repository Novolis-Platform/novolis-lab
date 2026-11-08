namespace CalypsoCad.Geometry;

/// <summary>One station along a hull knuckle path (lock Y / Up / stem Z).</summary>
internal readonly record struct HullKnuckleSample(float ZStem, float Y, float Up);
