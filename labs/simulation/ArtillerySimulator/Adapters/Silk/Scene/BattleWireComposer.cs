using System.Numerics;
using Novolis.Physics.Ballistics;
using Novolis.Silk;

namespace ArtillerySimulator;

/// <summary>Thin orchestrator for the dynamic world-line pass.</summary>
internal static class BattleWireComposer
{
    public static void Compose(
        SilkWireBatch wires,
        FrameViewModel vm,
        Vector3 cameraPosition)
    {
        SkyHazeWireComposer.Draw(wires, cameraPosition, vm.Terrain);
        vm.Terrain.DrawDynamic(wires);
        DefenseWireBridge.Draw(wires, vm.Mission);
        wires.Ring(vm.Terrain.GunBaseline, vm.Gun.Spec.MinEffectiveRangeMeters, BallisticArcColors.MinRange);
        AimWireComposer.Draw(wires, vm.Terrain, vm.Aim, vm.SolutionUnsafe);
        vm.Gun.Draw(
            wires,
            vm.Terrain.GunBaseline,
            showPivotGlow: vm.Crew.BreechReady,
            loading: vm.Crew.Loading);
        ShotFxWireComposer.DrawMuzzleFlash(wires, vm.Gun, vm.Terrain, vm.Battle.MuzzleFlash);
        ShotFxWireComposer.DrawShots(wires, vm.Terrain, vm.Rounds, vm.Battle, vm.Mission, vm.Gun);
        vm.Particles.Draw(wires);
    }
}
