namespace ArtillerySimulator;

internal static class LaneTrafficModel
{
    public static bool CanStartPass(ColumnUnit unit, RouteLane lane, float gap) =>
        gap > 12f && MathF.Abs(unit.Offset) < 5.5f;

    public static bool ApproachesBlocked(DefenseBattlefieldState state)
    {
        if (state.Wrecks.Count == 0)
            return false;

        var lanesWithWrecks = 0;
        var seen = new HashSet<int>();
        foreach (var wreck in state.Wrecks)
        {
            if (seen.Add(wreck.Lane))
                lanesWithWrecks++;
        }

        return lanesWithWrecks >= 1;
    }

    public static float GapAhead(DefenseBattlefieldState state, ColumnUnit unit, RouteLane lane, out bool wreckAhead)
    {
        wreckAhead = false;
        var best = float.MaxValue;
        var dead = false;

        void Consider(ColumnUnit other, bool isWreck)
        {
            if (other.Lane != unit.Lane || other.Id == unit.Id)
                return;

            var toward = (unit.Arc - other.Arc) * lane.Away;
            if (toward <= 8f || toward >= best)
                return;

            best = toward;
            dead = isWreck;
        }

        foreach (var other in state.Units)
            Consider(other, isWreck: false);
        foreach (var wreck in state.Wrecks)
            Consider(wreck, isWreck: true);

        wreckAhead = dead;
        return best;
    }
}
