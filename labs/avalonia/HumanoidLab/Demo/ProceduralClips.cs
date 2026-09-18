using System.Numerics;
using Novolis.Game.Humanoid;
using Novolis.Simulation.Humanoid;

namespace HumanoidLab.Demo;

/// <summary>Procedural walk + bow clips for the Avalonia lab (no mocap assets).</summary>
internal static class ProceduralClips
{
    public static HumanoidClipBank CreateBank(HumanoidBindPose bind)
    {
        return new HumanoidClipBank()
            .Set(LocomotionClipKind.Walk, CreateWalk(bind))
            .Set("bow", CreateBowDraw(bind));
    }

    public static HumanoidAnimationClip CreateWalk(HumanoidBindPose bind)
    {
        var hips = bind[HumanoidBone.Hips];
        var clip = new HumanoidAnimationClip("walk") { Loop = true };

        // Side view faces +Z; swing legs around local X, arms counter-swing.
        const float leg = 0.55f;
        const float arm = 0.4f;
        const float period = 1.0f;

        void Key(float t, float phase)
        {
            var s = MathF.Sin(phase);
            var c = MathF.Cos(phase);
            clip.AddKey(new HumanoidKeyframe
            {
                TimeSeconds = t,
                RootTranslation = hips + new Vector3(0f, 0.02f * MathF.Abs(c), 0.35f * t),
                LocalRotations =
                {
                    [HumanoidBone.LeftUpLeg] = Quaternion.CreateFromAxisAngle(Vector3.UnitX, leg * s),
                    [HumanoidBone.RightUpLeg] = Quaternion.CreateFromAxisAngle(Vector3.UnitX, -leg * s),
                    [HumanoidBone.LeftLeg] = Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.Max(0f, -leg * s) * 0.55f),
                    [HumanoidBone.RightLeg] = Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.Max(0f, leg * s) * 0.55f),
                    [HumanoidBone.LeftArm] = Quaternion.CreateFromAxisAngle(Vector3.UnitX, -arm * s),
                    [HumanoidBone.RightArm] = Quaternion.CreateFromAxisAngle(Vector3.UnitX, arm * s),
                    [HumanoidBone.Spine] = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.06f * s),
                },
            });
        }

        Key(0f, 0f);
        Key(period * 0.25f, MathF.PI * 0.5f);
        Key(period * 0.5f, MathF.PI);
        Key(period * 0.75f, MathF.PI * 1.5f);
        Key(period, MathF.PI * 2f);
        return clip;
    }

    /// <summary>Arm raise / draw keys; draw hand refined with TwoBoneIk in the demo tick.</summary>
    public static HumanoidAnimationClip CreateBowDraw(HumanoidBindPose bind)
    {
        var hips = bind[HumanoidBone.Hips];
        var clip = new HumanoidAnimationClip("bow") { Loop = true };

        var restArms = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, -0.15f);
        var raiseBow = Quaternion.CreateFromAxisAngle(Vector3.UnitY, -0.35f) *
                       Quaternion.CreateFromAxisAngle(Vector3.UnitZ, -1.15f);
        var raiseDraw = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.55f) *
                        Quaternion.CreateFromAxisAngle(Vector3.UnitZ, 0.85f);

        clip.AddKey(new HumanoidKeyframe
        {
            TimeSeconds = 0f,
            RootTranslation = hips,
            LocalRotations =
            {
                [HumanoidBone.RightArm] = restArms,
                [HumanoidBone.LeftArm] = Quaternion.Inverse(restArms),
            },
        });
        clip.AddKey(new HumanoidKeyframe
        {
            TimeSeconds = 0.7f,
            RootTranslation = hips,
            LocalRotations =
            {
                [HumanoidBone.RightArm] = raiseBow,
                [HumanoidBone.RightForeArm] = Quaternion.CreateFromAxisAngle(Vector3.UnitY, -0.2f),
                [HumanoidBone.LeftArm] = raiseDraw,
                [HumanoidBone.LeftForeArm] = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.9f),
                [HumanoidBone.Spine1] = Quaternion.CreateFromAxisAngle(Vector3.UnitY, -0.12f),
            },
        });
        clip.AddKey(new HumanoidKeyframe
        {
            TimeSeconds = 1.4f,
            RootTranslation = hips,
            LocalRotations =
            {
                [HumanoidBone.RightArm] = raiseBow,
                [HumanoidBone.RightForeArm] = Quaternion.CreateFromAxisAngle(Vector3.UnitY, -0.25f),
                [HumanoidBone.LeftArm] = raiseDraw,
                [HumanoidBone.LeftForeArm] = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 1.15f),
                [HumanoidBone.Spine1] = Quaternion.CreateFromAxisAngle(Vector3.UnitY, -0.18f),
            },
        });
        clip.AddKey(new HumanoidKeyframe
        {
            TimeSeconds = 2.0f,
            RootTranslation = hips,
            LocalRotations =
            {
                [HumanoidBone.RightArm] = restArms,
                [HumanoidBone.LeftArm] = Quaternion.Inverse(restArms),
            },
        });
        return clip;
    }
}
