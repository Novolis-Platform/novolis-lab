namespace TapDuelFootball.Game;

/// <summary>Hotseat tap-tug: each tap nudges the ball toward the opponent's end zone.</summary>
public sealed class TapDuelMatch
{
    /// <summary>Ball Z along the field axis; 0 is midfield. Positive toward Player 2.</summary>
    public float BallZ { get; private set; }

    /// <summary>World Z of Player 1's goal line (bottom). Crossing below awards Player 2.</summary>
    public float Player1GoalZ { get; }

    /// <summary>World Z of Player 2's goal line (top). Crossing above awards Player 1.</summary>
    public float Player2GoalZ { get; }

    /// <summary>World units the ball moves per tap.</summary>
    public float StepSize { get; }

    /// <summary>When set, the match is over.</summary>
    public int? Winner { get; private set; }

    public bool IsFinished => Winner is not null;

    public TapDuelMatch(float fieldHalfLength = 8f, float stepSize = 0.55f)
    {
        if (fieldHalfLength <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(fieldHalfLength));
        }

        if (stepSize <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(stepSize));
        }

        Player1GoalZ = -fieldHalfLength;
        Player2GoalZ = fieldHalfLength;
        StepSize = stepSize;
        Reset();
    }

    public void Reset()
    {
        BallZ = 0f;
        Winner = null;
    }

    /// <summary>Player 1 (bottom) pushes the ball toward Player 2 (+Z).</summary>
    public void TapPlayer1()
    {
        if (IsFinished)
        {
            return;
        }

        BallZ += StepSize;
        Evaluate();
    }

    /// <summary>Player 2 (top) pushes the ball toward Player 1 (−Z).</summary>
    public void TapPlayer2()
    {
        if (IsFinished)
        {
            return;
        }

        BallZ -= StepSize;
        Evaluate();
    }

    private void Evaluate()
    {
        if (BallZ >= Player2GoalZ)
        {
            Winner = 1;
            BallZ = Player2GoalZ;
        }
        else if (BallZ <= Player1GoalZ)
        {
            Winner = 2;
            BallZ = Player1GoalZ;
        }
    }
}
