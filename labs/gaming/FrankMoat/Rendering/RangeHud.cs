using FrankMoat.Art;
using FrankMoat.Game;
using FrankMoat.Weapons;
using Novolis.Math.Geometry;
using Novolis.Rendering.TwoD;

namespace FrankMoat.Rendering;

internal static class RangeHud
{
    public static void Draw(TwoDScene scene, RangeWorld world, RangeArt art, int width, bool inspecting)
    {
        scene.Hud.Elements.Clear();
        var barHeight = inspecting ? 236f : 108f;
        var plate = scene.Hud.AddSprite(art.White, TwoDSourceRect.Full, 0, 0, width, barHeight);
        plate.Tint = new Rgba32(8, 10, 14, 210);

        var gun = world.Weapon;
        scene.Hud.AddText("WASD move  MOUSE aim  LMB fire  R reload  1/2/3 guns  H inspect", 12, 8, 1.6f, new Rgba32(188, 190, 196));
        scene.Hud.AddText(
            $"HP {world.Frank.Health}   {gun.Spec.Name}  {gun.Spec.Chambering}   {gun.RoundsInGun}/{gun.Spec.MagazineSize}  RSV {gun.Reserve}",
            12,
            30,
            1.9f,
            new Rgba32(230, 226, 210));
        scene.Hud.AddText(
            $"WAVE {world.Wave}/3   KILLS {world.Kills}   FX {world.Particles.Count}",
            12,
            52,
            1.9f,
            new Rgba32(230, 226, 210));

        if (gun.Reloading)
        {
            scene.Hud.AddText(gun.Spec.TubeFeed ? "TOPPING TUBE" : "RELOAD", 12, 74, 2f, new Rgba32(220, 180, 70));
        }
        else if (gun.Spec.Mode == WeaponFireMode.Rotary && gun.Spin is > 0f and < 1f)
        {
            scene.Hud.AddText("SPINNING UP", 12, 74, 2f, new Rgba32(220, 180, 70));
        }

        if (world.Faith.Cue.Text.Length > 0)
        {
            scene.Hud.AddText("FAITH  " + world.Faith.Cue.Text, 12, 96, 1.7f, new Rgba32(120, 196, 190));
        }

        if (inspecting)
        {
            scene.Hud.AddText(gun.Spec.Name, 12, 140, 3.2f, new Rgba32(240, 220, 160));
            scene.Hud.AddText(gun.Spec.InspectLine, 12, 178, 2f, new Rgba32(220, 210, 190));
            scene.Hud.AddText("H to return", 12, 210, 1.6f, new Rgba32(160, 160, 170));
        }
    }
}
