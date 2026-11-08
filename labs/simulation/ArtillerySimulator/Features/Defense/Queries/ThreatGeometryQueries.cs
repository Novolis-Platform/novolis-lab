using System.Numerics;

namespace ArtillerySimulator;

internal static class ThreatGeometryQueries
{
    public static void RefreshNearest(DefenseBattlefieldState state)
    {
        var nearest = float.MaxValue;
        foreach (var unit in state.Units)
        {
            if (!unit.Alive)
                continue;

            var dx = unit.X - state.Objective.X;
            var dz = unit.Z - state.Objective.Z;
            nearest = MathF.Min(nearest, MathF.Sqrt(dx * dx + dz * dz));
        }

        state.NearestMeters = nearest == float.MaxValue ? 0f : nearest;
    }

    public static Vector3 NearestAlive(DefenseBattlefieldState state, Vector3 gun)
    {
        var nearest = state.Objective;
        var best = float.MaxValue;
        foreach (var unit in state.Units)
        {
            if (!unit.Alive)
                continue;

            var dx = unit.X - gun.X;
            var dz = unit.Z - gun.Z;
            var distance = dx * dx + dz * dz;
            if (distance >= best)
                continue;

            best = distance;
            nearest = new Vector3(unit.X, state.Height(unit.X, unit.Z), unit.Z);
        }

        return nearest;
    }

    public static Vector3 BattleFocus(DefenseBattlefieldState state, Vector3 gun)
    {
        var nearest = NearestAlive(state, gun);
        var mid = (gun + nearest) * 0.5f;
        mid.Y = MathF.Max(gun.Y, nearest.Y) + 70f;
        return mid;
    }
}
