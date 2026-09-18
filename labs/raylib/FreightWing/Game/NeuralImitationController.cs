using Novolis.MachineLearning.Neural;
using Novolis.Simulation.SpaceCombat;

namespace FreightWing.Game;

/// <summary>
/// Online-imitation crew AI: learns from heuristic teachers via <see cref="ContinuousActionPolicy"/>.
/// </summary>
internal sealed class NeuralImitationController : IFlightController
{
    public const int ActionSize = 5;

    private readonly IFlightController _teacher;
    private readonly ContinuousActionPolicy _policy;
    private readonly float _neuralBlend;
    private readonly float[] _features = new float[CraftObservationFeatures.Size];
    private readonly double[] _observation = new double[CraftObservationFeatures.Size];
    private readonly double[] _actions = new double[ActionSize];
    private readonly double[] _target = new double[ActionSize];

    private NeuralImitationController(
        IFlightController teacher,
        ContinuousActionPolicy policy,
        float neuralBlend)
    {
        _teacher = teacher;
        _policy = policy;
        _neuralBlend = Math.Clamp(neuralBlend, 0f, 1f);
    }

    public static NeuralImitationController CreatePilot(Random? random = null, float neuralBlend = 0.55f)
        => new(
            new HeuristicPilotAi(),
            ContinuousActionPolicy.Create(
                "freightwing-pilot",
                CraftObservationFeatures.Size,
                ActionSize,
                hiddenSizes: [32, 24],
                random),
            neuralBlend);

    public static NeuralImitationController CreateGunner(Random? random = null, float neuralBlend = 0.65f)
        => new(
            new HeuristicGunnerAi(),
            ContinuousActionPolicy.Create(
                "freightwing-gunner",
                CraftObservationFeatures.Size,
                ActionSize,
                hiddenSizes: [32, 24],
                random),
            neuralBlend);

    public FlightIntent Tick(in CraftObservation observation)
    {
        var teacher = _teacher.Tick(observation);
        EncodeIntent(teacher, _target);

        CraftObservationFeatures.Write(observation, _features);
        for (var i = 0; i < _features.Length; i++)
            _observation[i] = _features[i];

        _ = _policy.Imitate(_observation, _target, learningRate: 0.04);
        _policy.Act(_observation, _actions);

        var neural = DecodeIntent(_actions);
        return Blend(teacher, neural, _neuralBlend);
    }

    private static void EncodeIntent(in FlightIntent intent, Span<double> target)
    {
        target[0] = Math.Clamp(intent.YawDelta / 0.08, -1.0, 1.0);
        target[1] = Math.Clamp(intent.PitchDelta / 0.08, -1.0, 1.0);
        target[2] = Math.Clamp(intent.RollRight - intent.RollLeft, -1.0, 1.0);
        target[3] = Math.Clamp(intent.ThrottleUp - intent.ThrottleDown, -1.0, 1.0);
        target[4] = intent.Fire ? 1.0 : -1.0;
    }

    private static FlightIntent DecodeIntent(ReadOnlySpan<double> actions)
    {
        var roll = (float)actions[2];
        var throttle = (float)actions[3];
        return new FlightIntent
        {
            YawDelta = (float)actions[0] * 0.08f,
            PitchDelta = (float)actions[1] * 0.08f,
            RollLeft = roll < -0.15f ? Math.Clamp(-roll, 0f, 1f) : 0f,
            RollRight = roll > 0.15f ? Math.Clamp(roll, 0f, 1f) : 0f,
            ThrottleUp = throttle > 0.1f ? Math.Clamp(throttle, 0f, 1f) : 0f,
            ThrottleDown = throttle < -0.1f ? Math.Clamp(-throttle, 0f, 1f) : 0f,
            Fire = actions[4] > 0.15,
        };
    }

    private static FlightIntent Blend(in FlightIntent teacher, in FlightIntent neural, float t)
    {
        var u = 1f - t;
        return new FlightIntent
        {
            YawDelta = teacher.YawDelta * u + neural.YawDelta * t,
            PitchDelta = teacher.PitchDelta * u + neural.PitchDelta * t,
            RollLeft = teacher.RollLeft * u + neural.RollLeft * t,
            RollRight = teacher.RollRight * u + neural.RollRight * t,
            ThrottleUp = teacher.ThrottleUp * u + neural.ThrottleUp * t,
            ThrottleDown = teacher.ThrottleDown * u + neural.ThrottleDown * t,
            Fire = t >= 0.5f ? neural.Fire || teacher.Fire : teacher.Fire || neural.Fire,
        };
    }
}
