using System.Numerics;

namespace KatoriLab.Demo;

internal readonly record struct VertexDeltaReport(
    string PhaseA,
    string PhaseB,
    float TimeA,
    float TimeB,
    float MaxDelta,
    float MeanDelta,
    float UpperBodyMaxDelta,
    float LowerBodyMeanDelta,
    float BindHeadY);
