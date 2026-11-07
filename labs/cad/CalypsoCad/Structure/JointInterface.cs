namespace CalypsoCad.Structure;

/// <summary>Mating geometry for a joint (lock Y/Up/Zstem + local frames).</summary>
internal readonly record struct JointInterface(
    float Y,
    float Up,
    float ZStem,
    float NormalY,
    float NormalUp,
    float NormalZ,
    float ToleranceM = 0.05f);
