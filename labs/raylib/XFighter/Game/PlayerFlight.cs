using System.Numerics;
using Novolis.Raylib.Game;
using Novolis.Raylib.Interact;
using Novolis.Raylib.Rendering;
using Novolis.Simulation.SpaceCombat;
using Novolis.Simulation.View;

namespace XFighter.Game;

internal sealed class PlayerFlight
{
    private readonly CraftState _craft = new()
    {
        Profile = CraftProfile.FighterDefault,
        Speed = 22f,
        PlayerControlled = true,
    };

    public PlayerFlight() => _craft.ResetVitals();

    public float Yaw
    {
        get => _craft.Yaw;
        set => _craft.Yaw = value;
    }

    public float Pitch
    {
        get => _craft.Pitch;
        set => _craft.Pitch = value;
    }

    public float Roll
    {
        get => _craft.Roll;
        set => _craft.Roll = value;
    }

    public float Speed
    {
        get => _craft.Speed;
        set => _craft.Speed = value;
    }

    public Vector3 Position
    {
        get => _craft.Position;
        set => _craft.Position = value;
    }

    public Vector3 Forward => _craft.Forward;

    public float Throttle01 => _craft.Throttle01;

    public void Update(RayGameContext ctx)
    {
        var delta = ctx.MouseDelta;
        var intent = new FlightIntent
        {
            YawDelta = delta.X * 0.0022f,
            PitchDelta = -delta.Y * 0.0022f,
            RollLeft = ctx.IsKeyDown(KeyboardKey.A) ? 1f : 0f,
            RollRight = ctx.IsKeyDown(KeyboardKey.D) ? 1f : 0f,
            ThrottleUp = ctx.IsKeyDown(KeyboardKey.W) ? 1f : 0f,
            ThrottleDown = ctx.IsKeyDown(KeyboardKey.S) ? 1f : 0f,
        };
        ArcadeFlight.Apply(_craft, intent, ctx.DeltaSeconds);
    }

    public Novolis.Raylib.Rendering.Camera BuildCamera()
    {
        var pose = CraftCamera.Cockpit(Position, Forward, Roll);
        return Novolis.Raylib.Rendering.Camera.Perspective(
            pose.Position, pose.Target, pose.Up, pose.FieldOfViewDegrees);
    }
}
