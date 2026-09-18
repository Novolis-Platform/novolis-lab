using System.Drawing;
using System.Numerics;
using Novolis.Avalonia.Cad.Ship.Services;
using Novolis.Avalonia.Raylib;
using Novolis.Cad.Primitives;
using Novolis.Raylib.Colors;
using Novolis.Raylib.Rendering;
using Novolis.Ship.Primitives;
using Novolis.Ship.Topology;
using Novolis.Simulation.View;
using RayCamera = Novolis.Raylib.Rendering.Camera;

namespace CalypsoCad.Services;

internal sealed class CalypsoRenderer
{
    private static readonly Color Background = Color.FromArgb(255, 8, 10, 14);
    private static readonly Color GridMajor = Color.FromArgb(255, 40, 46, 54);
    private static readonly Color GridMinor = Color.FromArgb(255, 28, 32, 38);
    private static readonly Color Hud = Color.FromArgb(255, 180, 196, 210);
    private static readonly Color WallFallback = Color.FromArgb(255, 110, 120, 130);
    private static readonly Color DeckPlate = Color.FromArgb(255, 52, 56, 60);
    private static readonly Color Lining = Color.FromArgb(255, 168, 172, 176);
    private static readonly Color Bulkhead = Color.FromArgb(255, 90, 96, 104);
    private static readonly Color Steel = Color.FromArgb(255, 78, 86, 96);
    private static readonly Color AccentLight = Color.FromArgb(255, 210, 170, 90);
    private static readonly Color AccentCyan = Color.FromArgb(255, 90, 170, 190);
    private static readonly Color OpeningFrame = Color.FromArgb(255, 140, 120, 70);
    private static readonly Vector3 LightDir = Vector3.Normalize(new Vector3(-0.25f, 0.85f, 0.35f));

    private readonly CalypsoSession _session;
    private readonly OrbitCameraRig _orbit = new()
    {
        Target = new Vector3(0f, 4f, 0f),
        Distance = 90f,
        MinDistance = 3f,
        MaxDistance = 250f,
        Yaw = 0.85f,
        Pitch = 0.4f,
    };

    private Vector3 _interiorEye = new(0, 5.6f, 5f);
    private Vector3 _interiorTarget = new(0, 5.4f, 0f);
    private Dictionary<int, List<CadEntity>> _openingsByDeck = new();
    /// <summary>When set, interior draws an ensemble of spaces (e.g. DK0 catwalk + hold + corridors).</summary>
    private string? _interiorEnsemble;

    public CalypsoRenderer(CalypsoSession session) => _session = session;

    public OrbitCameraRig Orbit => _orbit;

    public void Bind(RaylibHostControl host) =>
        host.FrameRendering += (_, e) => DrawFrame(e.DeltaSeconds, e.ScreenWidth, e.ScreenHeight);

    public void Fit()
    {
        ApplyOrbitPreset("bow-quarter");
    }

    /// <summary>Warship / fan-render exterior camera presets.</summary>
    public void ApplyOrbitPreset(string id)
    {
        _interiorEnsemble = null;
        _session.ViewMode = CalypsoViewMode.Orbit;
        switch (id)
        {
            case "broadside":
                SetOrbitPose(new Vector3(0f, 4f, 0f), 100f, MathF.PI * 0.5f, 0.22f);
                _session.CutPlaneLongitudinal = true;
                break;
            case "stern-quarter":
                SetOrbitPose(new Vector3(0f, 3.5f, -8f), 90f, 2.4f, 0.35f);
                _session.CutPlaneLongitudinal = true;
                break;
            case "cutaway-long":
                SetOrbitPose(new Vector3(0f, 4f, 0f), 85f, MathF.PI * 0.5f, 0.30f);
                _session.CutPlaneLongitudinal = true;
                _session.WireMeshMode = CalypsoWireMeshMode.CutawayPartial;
                break;
            case "cutaway-beam":
                SetOrbitPose(new Vector3(0f, 5f, 0f), 88f, 0.15f, 0.28f);
                _session.CutPlaneLongitudinal = false;
                _session.WireMeshMode = CalypsoWireMeshMode.CutawayPartial;
                break;
            case "bow-on":
                SetOrbitPose(new Vector3(0f, 4f, 8f), 88f, 0f, 0.18f);
                _session.CutPlaneLongitudinal = true;
                break;
            case "stern-on":
                SetOrbitPose(new Vector3(0f, 4f, -8f), 88f, MathF.PI, 0.18f);
                _session.CutPlaneLongitudinal = true;
                break;
            case "top-down":
                SetOrbitPose(new Vector3(0f, 0f, 0f), 110f, 0.4f, 1.35f);
                _session.CutPlaneLongitudinal = true;
                break;
            case "low-pass":
                SetOrbitPose(new Vector3(0f, 2f, 0f), 78f, 1.1f, 0.08f);
                _session.CutPlaneLongitudinal = true;
                break;
            case "ramp-close":
                // Stern hatch-ramp face-on, slightly elevated.
                SetOrbitPose(new Vector3(0f, 2.2f, -28f), 38f, MathF.PI, 0.12f);
                _session.CutPlaneLongitudinal = true;
                break;
            case "pod-port":
                // Port side pod: engines aft + FTL graviton stack mid-fore.
                SetOrbitPose(new Vector3(-12f, 3.6f, -2f), 42f, MathF.PI * 0.72f, 0.18f);
                _session.CutPlaneLongitudinal = true;
                break;
            case "pod-stbd":
                SetOrbitPose(new Vector3(12f, 3.6f, -2f), 42f, MathF.PI * 0.28f, 0.18f);
                _session.CutPlaneLongitudinal = true;
                break;
            case "pod-ftl":
                // Close look at starboard FTL graviton emitter rings.
                SetOrbitPose(new Vector3(14f, 3.6f, 6f), 28f, MathF.PI * 0.35f, 0.1f);
                _session.CutPlaneLongitudinal = true;
                break;
            case "three-quarter-high":
                SetOrbitPose(new Vector3(0f, 6f, 4f), 105f, 0.95f, 0.55f);
                _session.CutPlaneLongitudinal = true;
                break;
            case "bow-quarter":
            default:
                SetOrbitPose(new Vector3(0f, 4f, 6f), 95f, 0.9f, 0.45f);
                _session.CutPlaneLongitudinal = true;
                break;
        }
    }

    public void SetOrbitPose(Vector3 target, float distance, float yaw, float pitch)
    {
        _orbit.Target = target;
        _orbit.Distance = Math.Clamp(distance, _orbit.MinDistance, _orbit.MaxDistance);
        _orbit.Yaw = yaw;
        _orbit.Pitch = Math.Clamp(pitch, -0.1f, MathF.PI * 0.49f);
    }

    public void SetInteriorPose(Vector3 eye, Vector3 target)
    {
        _interiorEye = eye;
        _interiorTarget = target;
        SanitizeInteriorCamera(_session.SelectedSpace);
    }

    /// <summary>
    /// Walking-camera pose: clamp eye into the active space (or union of walk ensemble spaces)
    /// so the camera never passes through walls. Open hatch leaves are not drawn.
    /// </summary>
    public void SetInteriorWalkPose(Vector3 eye, Vector3 target, CadEntity? space)
    {
        _interiorEnsemble = "walk";
        if (space is not null)
        {
            _session.SelectedSpaceId = space.Id;
            _session.DeckFilter = space.Deck;
            _interiorEye = ShipWalk.ClampToSpace(space, eye, ShipWalk.DefaultInset);
        }
        else
        {
            _interiorEye = eye;
        }

        _interiorTarget = target;
        if (Vector3.DistanceSquared(_interiorEye, _interiorTarget) < 0.25f)
            _interiorTarget = _interiorEye + new Vector3(0f, 0f, 3f);
    }

    public (Vector3 Eye, Vector3 Target) GetInteriorPose() => (_interiorEye, _interiorTarget);

    /// <summary>
    /// Mid-deck catwalk / hold POV presets (DK0 standing height over the hold).
    /// Draws cargo void + C40 stack + twin corridors as an ensemble.
    /// </summary>
    public void ApplyInteriorPreset(string id)
    {
        _session.ViewMode = CalypsoViewMode.Interior;
        _session.WireMeshMode = CalypsoWireMeshMode.None;
        _session.SelectedHookId = null;
        _session.DeckFilter = null;

        var cargoVoid = _session.Spaces.FirstOrDefault(s =>
            string.Equals(s.Name, "Cargo Void", StringComparison.OrdinalIgnoreCase) && s.Flags?.Hollow == true);
        var catwalk = _session.Spaces.FirstOrDefault(s =>
            string.Equals(s.Name, "Cargo Catwalk", StringComparison.OrdinalIgnoreCase) && s.Deck == 0);
        var portCorr = _session.Spaces.FirstOrDefault(s =>
            string.Equals(s.Name, "Port Corridor", StringComparison.OrdinalIgnoreCase) && s.Deck == 0);
        var stbdCorr = _session.Spaces.FirstOrDefault(s =>
            string.Equals(s.Name, "Starboard Corridor", StringComparison.OrdinalIgnoreCase) && s.Deck == 0);

        if (cargoVoid is null)
        {
            SyncInteriorFromSelection();
            return;
        }

        BoundsOf(cargoVoid.Points!.Select(SvgCoords.FromArray).ToArray(), out var vmin, out var vmax);
        var vcenter = (vmin + vmax) * 0.5f;
        var vsize = vmax - vmin;

        Vector3 cmin = vmin, cmax = vmax;
        if (catwalk?.Points is { Count: >= 3 })
            BoundsOf(catwalk.Points.Select(SvgCoords.FromArray).ToArray(), out cmin, out cmax);
        else
        {
            // Fallback: 3 m band at fore of hold at DK0 elevation.
            cmin = new Vector3(vmin.X, 0f, vmax.Z - 3.2f);
            cmax = new Vector3(vmax.X, 0.3f, vmax.Z);
        }

        var standY = vmin.Y + 8.25f;
        var catZFore = Math.Min(cmax.Z - 0.35f, vmax.Z - 0.85f);
        var catZMid = (cmin.Z + cmax.Z) * 0.5f;
        // C40 pack sits aft of catwalk; aim at mid-stack below eye so tops + columns read.
        var stackLook = new Vector3(vcenter.X, vmin.Y + 4.2f, vcenter.Z);

        _interiorEnsemble = "catwalk-dk0";
        _session.SelectedSpaceId = cargoVoid.Id;

        switch (id)
        {
            case "catwalk-containers-quarter":
            case "catwalk-containers":
            default:
            {
                var aisleX = vmin.X + 1.25f;
                _interiorEye = new Vector3(aisleX, standY, Math.Min(catZFore, vmax.Z - 0.7f));
                _interiorTarget = new Vector3(aisleX, vmin.Y + 1.4f, vmin.Z + 1.8f);
                break;
            }
            case "catwalk-passage-port":
                // Aft end of port corridor (DK0), looking forward down the passageway.
                _session.SelectedSpaceId = portCorr?.Id ?? cargoVoid.Id;
                if (portCorr?.Points is { Count: >= 3 })
                {
                    BoundsOf(portCorr.Points.Select(SvgCoords.FromArray).ToArray(), out var pmin, out var pmax);
                    var px = (pmin.X + pmax.X) * 0.5f;
                    var py = pmin.Y + 1.55f;
                    _interiorEye = new Vector3(px, py, pmin.Z + 1.2f);
                    _interiorTarget = new Vector3(px, py, pmax.Z - 2.0f);
                }
                else
                {
                    _interiorEye = new Vector3(vmin.X + 1.6f, standY, catZFore);
                    _interiorTarget = new Vector3(vmin.X + 1.6f, standY, catZFore + 16f);
                }
                break;
            case "catwalk-passage-stbd":
                _session.SelectedSpaceId = stbdCorr?.Id ?? cargoVoid.Id;
                if (stbdCorr?.Points is { Count: >= 3 })
                {
                    BoundsOf(stbdCorr.Points.Select(SvgCoords.FromArray).ToArray(), out var smin, out var smax);
                    var sx = (smin.X + smax.X) * 0.5f;
                    var sy = smin.Y + 1.55f;
                    _interiorEye = new Vector3(sx, sy, smin.Z + 1.2f);
                    _interiorTarget = new Vector3(sx, sy, smax.Z - 2.0f);
                }
                else
                {
                    _interiorEye = new Vector3(vmax.X - 1.6f, standY, catZFore);
                    _interiorTarget = new Vector3(vmax.X - 1.6f, standY, catZFore + 16f);
                }
                break;
            case "catwalk-span":
                _interiorEye = new Vector3(vmin.X + 1.8f, standY - 0.3f, catZFore);
                _interiorTarget = new Vector3(vmax.X - 2.0f, vmin.Y + 3.5f, catZMid);
                break;
        }

        SanitizeInteriorCamera(_session.SelectedSpace);
    }

    private float InteriorFovDegrees()
    {
        var space = _session.SelectedSpace;
        if (space?.Points is not { Count: >= 3 } pts)
            return 55f;
        BoundsOf(pts.Select(SvgCoords.FromArray).ToArray(), out var min, out var max);
        var shortSide = Math.Min(max.X - min.X, max.Z - min.Z);
        if (IsCargoVoidHollow(space))
            return 46f;
        if (shortSide < 2.5f)
            return 42f;
        if (shortSide < 4f)
            return 50f;
        return 55f;
    }

    public void OrbitDrag(float dx, float dy)
    {
        if (_session.ViewMode == CalypsoViewMode.Interior)
        {
            var forward = Vector3.Normalize(_interiorTarget - _interiorEye);
            var right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
            var yaw = Quaternion.CreateFromAxisAngle(Vector3.UnitY, -dx * 0.01f);
            var pitched = Vector3.Transform(forward, yaw);
            pitched = Vector3.Transform(pitched, Quaternion.CreateFromAxisAngle(right, -dy * 0.008f));
            if (pitched.LengthSquared() > 0.01f)
                _interiorTarget = _interiorEye + Vector3.Normalize(pitched) * 10f;
            return;
        }

        _orbit.AddLookDelta(dx * 0.01f, dy * 0.01f);
    }

    public void Zoom(float delta)
    {
        if (_session.ViewMode == CalypsoViewMode.Interior)
            return;
        _orbit.AdjustDistance(delta > 0 ? -2.5f : 2.5f);
    }

    public void SyncInteriorFromSelection()
    {
        _interiorEnsemble = null;
        var hook = _session.SelectedHook;
        if (hook is { Position: { } posArr } && posArr.Length >= 3)
        {
            var hookPos = SvgCoords.FromArray(posArr);
            PlaceInteriorAlongAxis(hookPos, hintLook: new Vector3(0f, hookPos.Y + 1.55f, 0f));
            return;
        }

        var space = _session.SelectedSpace;
        if (space is null || space.Points is not { Count: >= 3 } pts)
            return;

        var ring = pts.Select(SvgCoords.FromArray).ToArray();
        BoundsOf(ring, out var min, out var max);
        var center = (min + max) * 0.5f;
        var size = max - min;

        // Look along the long axis of the compartment (corridor feel). Prefer +Z (bow).
        Vector3 along;
        float halfLen;
        if (size.Z >= size.X)
        {
            along = new Vector3(0f, 0f, 1f);
            halfLen = Math.Max(1.2f, size.Z * 0.42f);
        }
        else
        {
            along = new Vector3(1f, 0f, 0f);
            halfLen = Math.Max(1.2f, size.X * 0.42f);
        }

        // Prefer looking toward ship center / bow when near periphery.
        if (Vector3.Dot(center, along) < 0f)
            along = -along;

        // Crossing is a wide, shallow junction — look along the hall (+X), stay mid-span.
        var name = space.Name ?? "";
        if (name.Contains("Crossing", StringComparison.OrdinalIgnoreCase))
        {
            along = Vector3.UnitX;
            halfLen = 0.2f;
        }
        else if (name.Contains("Engineering", StringComparison.OrdinalIgnoreCase))
        {
            // Enter from the forward bulkhead looking aft at the plant — avoid sitting inside tanks.
            along = -Vector3.UnitZ;
            halfLen = Math.Max(1.0f, size.Z * 0.32f);
        }

        var shortSide = Math.Min(size.X, size.Z);
        var eyeY = min.Y + Math.Clamp(space.Height * 0.42f, 1.45f, 1.75f);
        // Clearance from bulkheads (walls draw ~0.15 m into the room).
        var inset = shortSide < 2.8f ? Math.Min(0.95f, shortSide * 0.42f) : 0.55f;
        if (shortSide < 2.8f)
        {
            // Stay near mid-span so end walls / door leaves don't fill the FOV.
            halfLen = Math.Min(halfLen, Math.Max(0.35f, Math.Max(size.X, size.Z) * 0.18f));
            eyeY = min.Y + Math.Min(1.55f, space.Height * 0.4f);
        }

        var eye = new Vector3(center.X, eyeY, center.Z) - along * halfLen;
        // Lock to centerline of the short axis (avoids drifting into side bulkheads).
        if (Math.Abs(along.X) > 0.5f)
            eye.Z = center.Z;
        else
            eye.X = center.X;
        eye.X = Math.Clamp(eye.X, min.X + inset, max.X - inset);
        eye.Z = Math.Clamp(eye.Z, min.Z + inset, max.Z - inset);
        var look = eye + along * Math.Max(6f, halfLen * 2.2f);
        look.Y = eyeY;
        if (Math.Abs(along.X) > 0.5f)
            look.Z = center.Z;
        else
            look.X = center.X;

        // Cargo hold only — Engineering is also Flags.Hollow but is a room, not a C40 shaft.
        if (IsCargoVoidHollow(space))
            PlaceCargoHoldCamera(min, max, center, size, out eye, out look);

        _interiorEye = eye;
        _interiorTarget = look;
        SanitizeInteriorCamera(space);
    }

    private static bool IsCargoVoidHollow(CadEntity? space) =>
        space is not null
        && space.Flags?.Hollow == true
        && string.Equals(space.Name, "Cargo Void", StringComparison.OrdinalIgnoreCase);

    /// <summary>High port-fore corner looking aft down the side aisle (look ray stays in-aisle, not into the pack).</summary>
    private static void PlaceCargoHoldCamera(
        Vector3 min, Vector3 max, Vector3 center, Vector3 size, out Vector3 eye, out Vector3 look)
    {
        var aisleX = min.X + 1.25f;
        eye = new Vector3(aisleX, min.Y + 8.25f, max.Z - 0.7f);
        // Keep look.X == eye.X so the ray runs parallel to the container flank.
        look = new Vector3(aisleX, min.Y + 1.4f, min.Z + 1.8f);
    }

    /// <summary>Keep the eye outside walls, deck, ceiling, and solid C40 boxes.</summary>
    private void SanitizeInteriorCamera(CadEntity? space)
    {
        if (space?.Points is not { Count: >= 3 } pts)
            return;

        BoundsOf(pts.Select(SvgCoords.FromArray).ToArray(), out var min, out var max);
        var size = max - min;
        var shortSide = Math.Min(size.X, size.Z);
        var wallClear = shortSide < 2.8f ? Math.Min(0.95f, shortSide * 0.4f) : 0.55f;
        var floorClear = 0.35f;
        var ceilClear = 0.45f;
        var h = Math.Max(2.2f, space.Height);

        var eye = _interiorEye;
        eye.X = Math.Clamp(eye.X, min.X + wallClear, max.X - wallClear);
        eye.Z = Math.Clamp(eye.Z, min.Z + wallClear, max.Z - wallClear);
        eye.Y = Math.Clamp(eye.Y, min.Y + floorClear, min.Y + h - ceilClear);

        // Push out of any C40 AABB with a generous margin (cargo / catwalk views).
        if (IsCargoVoidHollow(space) || _interiorEnsemble == "catwalk-dk0"
            || (space.Name?.Contains("Cargo", StringComparison.OrdinalIgnoreCase) ?? false))
        {
            for (var iter = 0; iter < 4; iter++)
            {
                var moved = false;
                foreach (var box in _session.Document.Entities)
                {
                    if (box.Kind != "box" || box.Name?.StartsWith("C40", StringComparison.OrdinalIgnoreCase) != true)
                        continue;
                    if (box.Points is not { Count: >= 2 })
                        continue;
                    var c = SvgCoords.FromArray(box.Points[0]);
                    var he = SvgCoords.FromArray(box.Points[1]);
                    var margin = 1.1f;
                    var bmin = c - new Vector3(Math.Abs(he.X) + margin, Math.Abs(he.Y) + margin, Math.Abs(he.Z) + margin);
                    var bmax = c + new Vector3(Math.Abs(he.X) + margin, Math.Abs(he.Y) + margin, Math.Abs(he.Z) + margin);
                    if (eye.X < bmin.X || eye.X > bmax.X || eye.Y < bmin.Y || eye.Y > bmax.Y || eye.Z < bmin.Z || eye.Z > bmax.Z)
                        continue;

                    // Prefer escaping upward / forward (fore of stack) / outboard into the aisle.
                    var up = bmax.Y + 0.2f - eye.Y;
                    var fore = bmax.Z + 0.2f - eye.Z; // toward bow (+Z)
                    var port = bmin.X - 0.2f - eye.X; // toward port (−X)
                    if (up >= fore && up >= Math.Abs(port))
                        eye.Y = bmax.Y + 0.25f;
                    else if (fore >= Math.Abs(port))
                        eye.Z = bmax.Z + 0.25f;
                    else
                        eye.X = port < 0 ? bmin.X - 0.25f : bmax.X + 0.25f;
                    moved = true;
                }

                eye.X = Math.Clamp(eye.X, min.X + wallClear, max.X - wallClear);
                eye.Z = Math.Clamp(eye.Z, min.Z + wallClear, max.Z - wallClear);
                eye.Y = Math.Clamp(eye.Y, min.Y + floorClear, min.Y + h - ceilClear);
                if (!moved)
                    break;
            }
        }

        _interiorEye = eye;

        var look = _interiorTarget;
        if (Vector3.DistanceSquared(eye, look) < 0.25f)
            look = eye + new Vector3(0f, 0f, 6f);
        // Keep look at standing height for corridors so we don't stare into ceiling wedges.
        if (shortSide < 3.5f && !IsCargoVoidHollow(space))
            look.Y = eye.Y;
        _interiorTarget = look;
    }

    private void PlaceInteriorAlongAxis(Vector3 eyeBase, Vector3 hintLook)
    {
        var eyeY = eyeBase.Y + 1.55f;
        _interiorEye = new Vector3(eyeBase.X, eyeY, eyeBase.Z);
        var look = new Vector3(hintLook.X, eyeY, hintLook.Z);
        if (Vector3.DistanceSquared(_interiorEye, look) < 0.4f)
            look = _interiorEye + new Vector3(0f, 0f, 8f);
        _interiorTarget = look;
        SanitizeInteriorCamera(_session.SelectedSpace);
    }

    public void DrawFrame(float deltaSeconds, int screenWidth, int screenHeight)
    {
        _ = deltaSeconds;
        _ = screenWidth;
        _ = screenHeight;
        Graphics.ClearBackground(Background);

        RayCamera camera;
        if (_session.ViewMode == CalypsoViewMode.Plan)
        {
            var eye = new Vector3(0f, 120f, 0.01f);
            var target = new Vector3(0f, 0f, 0f);
            camera = RayCamera.Perspective(eye, target, new Vector3(0, 0, -1), 35f);
        }
        else if (_session.ViewMode == CalypsoViewMode.Interior)
        {
            camera = RayCamera.Perspective(_interiorEye, _interiorTarget, Vector3.UnitY, InteriorFovDegrees());
        }
        else
        {
            var eye = _orbit.BuildEyePosition();
            camera = RayCamera.Perspective(eye, _orbit.Target, Vector3.UnitY, _orbit.FieldOfViewDegrees);
        }

        _openingsByDeck = _session.Document.Entities
            .Where(e => e.Kind == "opening")
            .GroupBy(e => e.Deck)
            .ToDictionary(g => g.Key, g => g.ToList());

        var deckForSpaces = _session.DeckFilter;
        if (_session.ViewMode == CalypsoViewMode.Interior && _session.SelectedSpace is { } selected)
            deckForSpaces = selected.Deck;

        HashSet<int>? deckForWalls = null;
        if (deckForSpaces is { } d)
        {
            deckForWalls = new HashSet<int> { d };
            if (_session.ViewMode == CalypsoViewMode.Interior && IsCargoVoidHollow(_session.SelectedSpace))
            {
                deckForWalls.Add(d - 1);
                deckForWalls.Add(d + 1);
            }
        }

        World.Begin(camera);
        if (_session.ViewMode != CalypsoViewMode.Interior)
            DrawGrid();

        var cutaway = _session.WireMeshMode == CalypsoWireMeshMode.CutawayPartial;
        var solidOrbit = _session.ViewMode == CalypsoViewMode.Orbit &&
                         _session.WireMeshMode == CalypsoWireMeshMode.None;
        SyncCutPlane(cutaway);
        GetCutPlane(cutaway, out var frameCutPt, out var frameCutN);
        if (cutaway)
            DrawCutFaceCue();

        // Solid orbit: sealed exterior hull/meshes.
        // Cutaway: exterior triangles on the camera side of the invisible plane are culled.
        if (solidOrbit)
        {
            CadShipExterior.Draw(_session.Document);
        }
        else
        {
            if (cutaway || _session.ViewMode == CalypsoViewMode.Orbit)
            {
                if (cutaway)
                    CadShipExterior.Draw(_session.Document, frameCutPt, frameCutN);
                else
                    CadShipExterior.Draw(_session.Document);
            }

            foreach (var entity in _session.Document.Entities)
            {
                if (entity.Kind == "wall" && deckForWalls is { } walls && !WallVisibleOnDecks(entity, walls))
                    continue;

                if (entity.Kind == "space" && deckForSpaces is { } spacesDeck && !SpaceVisibleOnDeck(entity, spacesDeck))
                    continue;

                // Exterior solids/meshes already drawn above — skip duplicate in cutaway orbit.
                if ((cutaway || _session.ViewMode == CalypsoViewMode.Orbit)
                    && CadShipExterior.IsExteriorDrawable(entity))
                    continue;

                switch (entity.Kind)
                {
                    case "space":
                        DrawSpace(entity);
                        break;
                    case "wall":
                        DrawWall(entity);
                        break;
                    case "opening":
                        // Interior: only doors that touch the active / ensemble spaces (not every deck door).
                        if (_session.ViewMode == CalypsoViewMode.Interior)
                        {
                            // Ramp geometry is owned by the hold solid pass — skip here (reads as floating planks).
                            if (string.Equals(entity.OpeningType, "ramp", StringComparison.OrdinalIgnoreCase))
                                break;
                            if (OpeningTouchesVisibleInterior(entity))
                            {
                                // Narrow halls: empty doorways only — leaves/lintels read as floating pillars.
                                if (IsNarrowInteriorSpace())
                                    break;
                                if (!OpeningOccludesInteriorCamera(entity))
                                    DrawOpeningFrame(entity);
                            }
                        }
                        else if (string.Equals(entity.OpeningType, "ramp", StringComparison.OrdinalIgnoreCase)
                                 || (entity.Name?.Contains("Armored", StringComparison.OrdinalIgnoreCase) ?? false))
                            DrawOpeningFrame(entity);
                        break;
                    case "box":
                        // C40 stack is hold cargo — fine in interior; in orbit cutaway it reads as one giant orange slab.
                        if (cutaway
                            && _session.ViewMode == CalypsoViewMode.Orbit
                            && (entity.Name?.StartsWith("C40", StringComparison.OrdinalIgnoreCase) ?? false))
                            break;
                        DrawSolidBox(entity);
                        break;
                    case "sphere":
                    case "cylinder":
                    case "cone":
                    case "wedge":
                    case "mesh":
                        if (cutaway)
                            CadShipExterior.DrawOne(entity, frameCutPt, frameCutN);
                        else
                            CadShipExterior.DrawOne(entity);
                        break;
                }
            }
        }

        if (_session.ViewMode == CalypsoViewMode.Interior && _session.SelectedHook is { Position: { } hp } && hp.Length >= 3)
            DrawHookMarker(SvgCoords.FromArray(hp));

        World.End();

        var spaceName = _session.SelectedSpace?.Name ?? "(none)";
        var hookTag = _session.SelectedHook?.Tag ?? "(no-hook)";
        var wire = _session.WireMeshMode.ToString();
        var cutHint = _session.WireMeshMode == CalypsoWireMeshMode.CutawayPartial
            ? $" | cut={(_session.CutPlaneLongitudinal ? "long" : "beam")}@{_session.CutPlaneOffset:0.0}m"
            : "";
        Graphics.DrawText(
            $"{_session.ViewMode} | {wire}{cutHint} | deck={(_session.DeckFilter?.ToString() ?? "all")} | space={spaceName} | hook={hookTag}",
            8, 8, 14, Hud);
        Graphics.DrawText(_session.StatusText, 8, 28, 12, Hud);
        Graphics.DrawText(
            "P plan  O orbit  I interior  W wire  C cutaway  S solid  [ ] slide cut  L/B cut axis  1/2/3 decks  0 all  F fit  E export",
            8, 48, 12, Hud);
    }

    private static void DrawGrid()
    {
        const float extent = 40f;
        const float step = 2f;
        for (float o = -extent; o <= extent; o += step)
        {
            var major = Math.Abs(o) < 0.01f || Math.Abs(o % 10f) < 0.01f;
            var c = major ? GridMajor : GridMinor;
            World.DrawLine(new Vector3(-extent, 0.01f, o), new Vector3(extent, 0.01f, o), c);
            World.DrawLine(new Vector3(o, 0.01f, -extent), new Vector3(o, 0.01f, extent), c);
        }
    }

    private void DrawSpace(CadEntity space)
    {
        if (space.Points is not { Count: >= 3 } pts)
            return;

        var selected = space.Id == _session.SelectedSpaceId;
        var interior = _session.ViewMode == CalypsoViewMode.Interior;
        var wire = _session.WireMeshMode == CalypsoWireMeshMode.Wire;
        var cutaway = _session.WireMeshMode == CalypsoWireMeshMode.CutawayPartial;
        GetCutPlane(cutaway, out var cutPt, out var cutN);

        var floorMat = _session.ResolveShapeMaterial(space.FloorShapeId ?? space.ShapeId, DeckPlate);
        // Interior: force industrial deck/lining so zone CAD colors don't paint Mondrian fields.
        var floorColor = interior
            ? ShadeColor(DeckPlate, 0.75f, 0.15f, Vector3.UnitY, LightDir)
            : ShadeColor(floorMat.color, floorMat.roughness, floorMat.metalness, Vector3.UnitY, LightDir);
        if (selected && !interior)
            floorColor = Color.FromArgb(255, Math.Min(255, floorColor.R + 30), Math.Min(255, floorColor.G + 30), Math.Min(255, floorColor.B + 18));

        var ring = pts.Select(SvgCoords.FromArray).ToArray();
        BoundsOf(ring, out var min, out var max);
        var center = (min + max) * 0.5f;
        var size = max - min;
        var h = Math.Max(0.5f, space.Height);
        // Full-OAH engineering / voids that aren't the cargo shaft still enclose as a deck room.
        var encloseH = IsCargoVoidHollow(space) ? Math.Min(h, 9f) : Math.Min(h, 3.6f);

        _openingsByDeck.TryGetValue(space.Deck, out var deckOpenings);
        // Only openings that connect to THIS space — otherwise one long hallway wall is carved away by every nearby door.
        List<CadEntity>? openings = null;
        if (deckOpenings is { Count: > 0 })
        {
            openings = deckOpenings.Where(o => OpeningConnectsToSpace(o, space.Name)).ToList();
            if (openings.Count == 0)
                openings = null;
        }

        var showOutline = interior
            ? (wire || cutaway)
            : (_session.ViewMode == CalypsoViewMode.Plan || wire || cutaway);
        if (showOutline)
        {
            for (var i = 0; i < ring.Length; i++)
            {
                var a = ring[i];
                var b = ring[(i + 1) % ring.Length];
                if (openings is { Count: > 0 } && SegmentIntersectsOpening(a, b, openings))
                    continue;
                var mid = (a + b) * 0.5f;
                if (cutaway && CulledByCutPlane(mid, cutPt, cutN))
                    continue;
                var edge = interior ? Bulkhead : floorMat.color;
                World.DrawLine(a + new Vector3(0, 0.04f, 0), b + new Vector3(0, 0.04f, 0), edge);
                if (interior && selected)
                    World.DrawLine(a + new Vector3(0, h, 0), b + new Vector3(0, h, 0), Bulkhead);
            }
        }

        if (wire)
            return;

        if (interior && !InteriorSpaceVisible(space, selected))
            return;

        if (interior && IsCargoVoidHollow(space))
        {
            DrawHollowShaft(ring, min, max, center, size, h, cutaway, cutPt, cutN);
            return;
        }

        if (!cutaway || !CulledByCutPlane(center, cutPt, cutN))
        {
            if (_session.ViewMode == CalypsoViewMode.Plan)
            {
                DrawPolygonFloor(ring, min, max, min.Y + 0.03f, floorColor, tile: 0.85f);
            }
            else if (cutaway && !interior)
            {
                // Cutaway orbit: single muted slab (no tile grid — tiles z-fight under outlines).
                var muted = Color.FromArgb(100, DeckPlate.R, DeckPlate.G, DeckPlate.B);
                World.DrawCube(
                    new Vector3(center.X, min.Y + 0.03f, center.Z),
                    Math.Max(0.4f, size.X),
                    0.06f,
                    Math.Max(0.4f, size.Z),
                    muted);
            }
            else if (interior)
            {
                // Single slab floor (no tile grid gaps that read as CAD wires).
                var deck = ShadeColor(Color.FromArgb(255, 72, 76, 82), 0.65f, 0.12f, Vector3.UnitY, LightDir);
                World.DrawCube(
                    new Vector3(center.X, min.Y + 0.04f, center.Z),
                    Math.Max(0.4f, size.X),
                    0.08f,
                    Math.Max(0.4f, size.Z),
                    deck);
            }
            // Solid orbit: no open room floors — closed shell carries the silhouette.
        }

        if (interior)
        {
            // No deck panel wire grid — it reads as CAD construction lines.
            DrawCeilingAndLightStrip(center, size, encloseH, cutaway, cutPt, cutN);
            DrawPerimeterBulkheads(ring, encloseH, openings, cutaway, cutPt, cutN);
            if (InteriorSpaceVisible(space, selected))
                DrawCompartmentProps(space, center, size, min, max, encloseH);
        }
    }

    private bool IsNarrowInteriorSpace()
    {
        var space = _session.SelectedSpace;
        if (space?.Points is not { Count: >= 3 } pts)
            return false;
        BoundsOf(pts.Select(SvgCoords.FromArray).ToArray(), out var min, out var max);
        return Math.Min(max.X - min.X, max.Z - min.Z) < 2.8f;
    }

    private bool OpeningOccludesInteriorCamera(CadEntity opening)
    {
        if (_session.ViewMode != CalypsoViewMode.Interior)
            return false;
        if (opening.Footprint is not { Count: >= 3 } fp)
            return false;
        BoundsOf(fp.Select(SvgCoords.FromArray).ToArray(), out var omin, out var omax);
        var oc = (omin + omax) * 0.5f;
        oc.Y = _interiorEye.Y;
        var toDoor = oc - _interiorEye;
        var dist = toDoor.Length();
        if (dist < 0.85f)
            return true;
        if (dist > 4.5f)
            return false;
        var look = _interiorTarget - _interiorEye;
        if (look.LengthSquared() < 1e-4f)
            return false;
        look = Vector3.Normalize(look);
        toDoor = Vector3.Normalize(toDoor);
        // Door dead ahead and close — hide leaf so we look through the opening.
        return Vector3.Dot(look, toDoor) > 0.82f && dist < 2.4f;
    }

    private static bool OpeningConnectsToSpace(CadEntity opening, string? spaceName)
    {
        if (string.IsNullOrEmpty(spaceName))
            return false;
        if (opening.Properties is not null &&
            opening.Properties.TryGetValue("connects", out var connectsEl) &&
            connectsEl.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var item in connectsEl.EnumerateArray())
            {
                if (string.Equals(item.GetString(), spaceName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        return opening.Name?.Contains(spaceName, StringComparison.OrdinalIgnoreCase) == true;
    }

    private bool OpeningTouchesVisibleInterior(CadEntity opening)
    {
        if (opening.Deck != (_session.SelectedSpace?.Deck ?? opening.Deck) &&
            _interiorEnsemble != "catwalk-dk0")
        {
            // Still allow if deck matches any visible ensemble space.
        }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (_session.SelectedSpace?.Name is { } sel)
            names.Add(sel);
        foreach (var space in _session.Spaces)
        {
            if (InteriorSpaceVisible(space, space.Id == _session.SelectedSpaceId))
                names.Add(space.Name ?? "");
        }

        if (opening.Properties is not null &&
            opening.Properties.TryGetValue("connects", out var connectsEl) &&
            connectsEl.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var item in connectsEl.EnumerateArray())
            {
                var n = item.GetString();
                if (n is not null && names.Contains(n))
                    return true;
            }
        }

        // Fallback: opening footprint near selected space bounds.
        if (_session.SelectedSpace?.Points is { Count: >= 3 } pts && opening.Footprint is { Count: >= 3 } fp)
        {
            BoundsOf(pts.Select(SvgCoords.FromArray).ToArray(), out var smin, out var smax);
            BoundsOf(fp.Select(SvgCoords.FromArray).ToArray(), out var omin, out var omax);
            var oc = (omin + omax) * 0.5f;
            const float pad = 0.6f;
            if (oc.X >= smin.X - pad && oc.X <= smax.X + pad &&
                oc.Z >= smin.Z - pad && oc.Z <= smax.Z + pad)
                return true;
        }

        return false;
    }

    private bool ShowC40InInterior()
    {
        if (_interiorEnsemble == "catwalk-dk0")
            return true;
        var sel = _session.SelectedSpace?.Name ?? "";
        return sel.Contains("Cargo", StringComparison.OrdinalIgnoreCase);
    }

    private bool InteriorSpaceVisible(CadEntity space, bool selected)
    {
        if (selected)
            return true;

        if (_interiorEnsemble == "walk")
        {
            var selDeck = _session.SelectedSpace?.Deck ?? space.Deck;
            var name = space.Name ?? "";
            // Adjacent walkable volumes on the same deck + continuous voids (hold / eng atrium).
            if (space.Deck == selDeck)
                return true;
            return name is "HOLD" or "ENG";
        }

        if (_interiorEnsemble != "catwalk-dk0")
            return false;

        var nm = space.Name ?? "";
        if (string.Equals(nm, "Cargo Void", StringComparison.OrdinalIgnoreCase) && space.Flags?.Hollow == true)
            return true;
        if (string.Equals(nm, "Cargo Catwalk", StringComparison.OrdinalIgnoreCase) && space.Deck == 0)
            return true;
        if (space.Deck != 0)
            return false;
        return nm is "Port Corridor" or "Starboard Corridor" or "Crossing Hallway"
            or "VEST-P" or "VEST-S" or "VEST-BR" or "Engineering";
    }

    private void DrawCompartmentProps(CadEntity space, Vector3 center, Vector3 size, Vector3 min, Vector3 max, float h)
    {
        var name = space.Name ?? "";
        var console = ShadeColor(Color.FromArgb(255, 48, 52, 58), 0.5f, 0.35f, Vector3.UnitY, LightDir);
        var glow = ShadeColor(AccentCyan, 0.3f, 0.5f, Vector3.UnitY, LightDir);
        var steel = ShadeColor(Steel, 0.45f, 0.55f, Vector3.UnitY, LightDir);
        var woodish = ShadeColor(Color.FromArgb(255, 90, 78, 62), 0.7f, 0.05f, Vector3.UnitY, LightDir);
        var appliance = ShadeColor(Color.FromArgb(255, 95, 100, 108), 0.55f, 0.25f, Vector3.UnitY, LightDir);

        if (name.Contains("Catwalk", StringComparison.OrdinalIgnoreCase))
        {
            // Walking plate + handrails so DK0 catwalk POVs read as a balcony over the hold.
            World.DrawCube(new Vector3(center.X, min.Y + 0.06f, center.Z), Math.Max(0.5f, size.X * 0.98f), 0.1f, Math.Max(0.5f, size.Z * 0.98f), steel);
            World.DrawCube(new Vector3(min.X + 0.12f, min.Y + 0.55f, center.Z), 0.06f, 1.0f, Math.Max(0.4f, size.Z * 0.9f), steel);
            World.DrawCube(new Vector3(max.X - 0.12f, min.Y + 0.55f, center.Z), 0.06f, 1.0f, Math.Max(0.4f, size.Z * 0.9f), steel);
            World.DrawLine(new Vector3(min.X + 0.12f, min.Y + 1.05f, min.Z + 0.2f), new Vector3(min.X + 0.12f, min.Y + 1.05f, max.Z - 0.2f), AccentLight);
            World.DrawLine(new Vector3(max.X - 0.12f, min.Y + 1.05f, min.Z + 0.2f), new Vector3(max.X - 0.12f, min.Y + 1.05f, max.Z - 0.2f), AccentLight);
            return;
        }

        if (name.Contains("Bridge", StringComparison.OrdinalIgnoreCase) && !name.Contains("Access", StringComparison.OrdinalIgnoreCase))
        {
            var viewW = Math.Min(size.X * 0.78f, 10f);
            var viewH = Math.Min(h * 0.52f, 1.9f);
            var viewY = min.Y + h * 0.58f;
            var glass = Color.FromArgb(255, 18, 28, 40);
            var frame = ShadeColor(Steel, 0.4f, 0.5f, new Vector3(0, 0, -1), LightDir);
            World.DrawCube(new Vector3(center.X, viewY, max.Z - 0.08f), viewW, viewH, 0.12f, glass);
            // Heavy mullions (3) — not a cage / floating HUD bar
            World.DrawCube(new Vector3(center.X, viewY + viewH * 0.5f, max.Z - 0.04f), viewW + 0.2f, 0.14f, 0.16f, frame);
            World.DrawCube(new Vector3(center.X, viewY - viewH * 0.5f, max.Z - 0.04f), viewW + 0.2f, 0.14f, 0.16f, frame);
            World.DrawCube(new Vector3(center.X - viewW * 0.33f, viewY, max.Z - 0.05f), 0.16f, viewH, 0.16f, frame);
            World.DrawCube(new Vector3(center.X + viewW * 0.33f, viewY, max.Z - 0.05f), 0.16f, viewH, 0.16f, frame);
            World.DrawCube(new Vector3(center.X, viewY, max.Z - 0.05f), 0.16f, viewH, 0.16f, frame);

            var consoleZ0 = min.Z + size.Z * 0.28f;
            var consoleZ1 = min.Z + size.Z * 0.62f;
            foreach (var x in new[] { min.X + 0.55f, max.X - 0.55f })
            {
                World.DrawCube(new Vector3(x, min.Y + 0.55f, (consoleZ0 + consoleZ1) * 0.5f), 0.7f, 1.0f, Math.Abs(consoleZ1 - consoleZ0), console);
                World.DrawCube(new Vector3(x, min.Y + 1.08f, (consoleZ0 + consoleZ1) * 0.5f), 0.55f, 0.08f, Math.Abs(consoleZ1 - consoleZ0) * 0.85f, glow);
                // Pilot seat
                World.DrawCube(new Vector3(x + (x < center.X ? 0.55f : -0.55f), min.Y + 0.35f, (consoleZ0 + consoleZ1) * 0.5f), 0.4f, 0.45f, 0.4f, steel);
                World.DrawCube(new Vector3(x + (x < center.X ? 0.55f : -0.55f), min.Y + 0.62f, (consoleZ0 + consoleZ1) * 0.5f), 0.38f, 0.08f, 0.38f, woodish);
            }

            World.DrawCube(new Vector3(center.X, min.Y + 0.5f, center.Z + size.Z * 0.08f), 1.5f, 0.95f, 1.0f, console);
            World.DrawCube(new Vector3(center.X, min.Y + 1.02f, center.Z + size.Z * 0.08f), 1.15f, 0.06f, 0.65f, glow);
            World.DrawCube(new Vector3(center.X - 0.35f, min.Y + 1.12f, center.Z + size.Z * 0.08f), 0.12f, 0.08f, 0.2f, AccentCyan);
            World.DrawCube(new Vector3(center.X + 0.35f, min.Y + 1.12f, center.Z + size.Z * 0.08f), 0.12f, 0.08f, 0.2f, AccentLight);
            World.DrawCube(new Vector3(center.X, min.Y + 0.35f, center.Z - 0.35f), 0.45f, 0.5f, 0.45f, steel);
            World.DrawCube(new Vector3(center.X, min.Y + 0.35f, center.Z - 0.45f), 0.42f, 0.35f, 0.12f, woodish);
            World.DrawCube(new Vector3(center.X, min.Y + 0.04f, center.Z), Math.Min(1.0f, size.X * 0.22f), 0.04f, size.Z * 0.65f,
                ShadeColor(Color.FromArgb(255, 90, 94, 100), 0.6f, 0.15f, Vector3.UnitY, LightDir));
            // Aft bulkhead display — keep clear of ceiling so tour cameras don't clip a wedge.
            World.DrawCube(new Vector3(center.X, min.Y + 1.15f, min.Z + 0.2f), Math.Min(3.2f, size.X * 0.35f), 0.7f, 0.08f, woodish);
            return;
        }

        if (name.Contains("Galley", StringComparison.OrdinalIgnoreCase))
        {
            var runZ = Math.Max(1.6f, size.Z * 0.75f);
            World.DrawCube(new Vector3(min.X + 0.55f, min.Y + 0.5f, center.Z), 0.65f, 0.95f, runZ, console);
            World.DrawCube(new Vector3(min.X + 0.55f, min.Y + 1.0f, center.Z), 0.55f, 0.06f, runZ * 0.95f, steel);
            // Appliances sit proud of the counter face so they read in tour cameras
            World.DrawCube(new Vector3(min.X + 0.95f, min.Y + 0.7f, center.Z - runZ * 0.28f), 0.45f, 0.55f, 0.5f, appliance);
            World.DrawCube(new Vector3(min.X + 0.95f, min.Y + 0.7f, center.Z), 0.45f, 0.55f, 0.5f, Color.FromArgb(255, 70, 90, 110));
            World.DrawCube(new Vector3(min.X + 0.95f, min.Y + 0.65f, center.Z + runZ * 0.28f), 0.4f, 0.4f, 0.4f, steel);
            World.DrawCube(new Vector3(min.X + 0.95f, min.Y + 0.9f, center.Z + runZ * 0.28f), 0.28f, 0.08f, 0.28f, AccentCyan);
            World.DrawCube(new Vector3(max.X - 0.45f, min.Y + 0.9f, min.Z + 0.35f), 0.08f, 1.4f, 0.08f, AccentLight);
            return;
        }

        if (name.Contains("Infirmary", StringComparison.OrdinalIgnoreCase))
        {
            World.DrawCube(new Vector3(center.X, min.Y + 0.45f, center.Z), Math.Min(2.2f, size.X * 0.7f), 0.55f, Math.Min(1.0f, size.Z * 0.55f), Color.FromArgb(255, 200, 205, 210));
            World.DrawCube(new Vector3(center.X, min.Y + 0.75f, center.Z), Math.Min(2.0f, size.X * 0.65f), 0.06f, Math.Min(0.9f, size.Z * 0.5f), Color.FromArgb(255, 230, 235, 240));
            World.DrawCube(new Vector3(max.X - 0.45f, min.Y + 0.85f, center.Z), 0.55f, 1.5f, 0.55f, console);
            World.DrawCube(new Vector3(max.X - 0.45f, min.Y + 1.4f, center.Z), 0.35f, 0.08f, 0.35f, glow);
            return;
        }

        if (name.Contains("Cabin", StringComparison.OrdinalIgnoreCase) || name.StartsWith("Berth", StringComparison.OrdinalIgnoreCase))
        {
            var bunkLen = Math.Max(1.5f, size.Z * 0.58f);
            World.DrawCube(new Vector3(min.X + 0.55f, min.Y + 0.35f, center.Z), 0.95f, 0.35f, bunkLen, console);
            World.DrawCube(new Vector3(min.X + 0.55f, min.Y + 1.15f, center.Z), 0.95f, 0.35f, bunkLen, console);
            World.DrawCube(new Vector3(min.X + 0.55f, min.Y + 0.12f, center.Z), 0.9f, 0.18f, bunkLen * 0.9f, steel);
            World.DrawCube(new Vector3(max.X - 0.35f, min.Y + 1.0f, min.Z + 0.4f), 0.45f, 1.9f, 0.5f, steel);
            World.DrawCube(new Vector3(max.X - 0.4f, min.Y + 0.9f, max.Z - 0.45f), 0.55f, 1.7f, 0.55f, Color.FromArgb(255, 70, 78, 88));
            World.DrawCube(new Vector3(max.X - 0.55f, min.Y + 0.4f, center.Z), 0.7f, 0.08f, 0.45f, woodish);
            World.DrawCube(new Vector3(max.X - 0.55f, min.Y + 0.22f, center.Z), 0.08f, 0.35f, 0.08f, steel);
            World.DrawCube(new Vector3(max.X - 0.85f, min.Y + 0.28f, center.Z), 0.35f, 0.08f, 0.35f, console);
            World.DrawCube(new Vector3(min.X + 0.2f, min.Y + h - 0.35f, center.Z), 0.25f, 0.2f, 0.45f, appliance);
            World.DrawCube(new Vector3(min.X + 0.15f, min.Y + 1.0f, min.Z + 0.35f), 0.08f, 0.12f, 0.12f, glow);
            for (var i = 0; i < 4; i++)
            {
                var z = center.Z - bunkLen * 0.35f + i * (bunkLen * 0.22f);
                World.DrawLine(new Vector3(min.X + 0.15f, min.Y + 0.55f, z), new Vector3(min.X + 1.0f, min.Y + 0.55f, z), AccentLight);
                World.DrawLine(new Vector3(min.X + 0.15f, min.Y + 1.35f, z), new Vector3(min.X + 1.0f, min.Y + 1.35f, z), AccentLight);
            }
            return;
        }

        if (name.Contains("Lounge", StringComparison.OrdinalIgnoreCase))
        {
            World.DrawCube(new Vector3(center.X, min.Y + 0.55f, min.Z + 1.2f), Math.Min(4f, size.X * 0.55f), 1.0f, 0.7f, woodish);
            World.DrawCube(new Vector3(center.X, min.Y + h - 0.25f, center.Z), Math.Min(6f, size.X * 0.7f), 0.08f, Math.Min(4f, size.Z * 0.5f), glow);
            for (var i = -1; i <= 1; i++)
            {
                var x = center.X + i * 0.85f;
                World.DrawCylinder(new Vector3(x, min.Y + 0.35f, min.Z + 1.85f), 0.18f, 0.18f, 0.45f, 8, console);
                World.DrawCube(new Vector3(x, min.Y + 0.58f, min.Z + 1.85f), 0.35f, 0.06f, 0.35f, woodish);
            }
            return;
        }

        if (name.Contains("Engineering", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Reactor", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Power", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Life", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Water", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Ballast", StringComparison.OrdinalIgnoreCase))
        {
            for (var i = 0; i < 3; i++)
            {
                var z = min.Z + size.Z * (0.25f + i * 0.25f);
                World.DrawCube(new Vector3(min.X + 0.5f, min.Y + 1.0f, z), 0.6f, 1.8f, 0.7f, steel);
                World.DrawCube(new Vector3(max.X - 0.5f, min.Y + 1.0f, z), 0.6f, 1.8f, 0.7f, steel);
                World.DrawCube(new Vector3(min.X + 0.5f, min.Y + 1.6f, z), 0.35f, 0.08f, 0.35f, glow);
            }
            World.DrawCylinder(new Vector3(center.X - size.X * 0.15f, min.Y + 0.9f, center.Z), 0.45f, 0.45f, 1.6f, 12, appliance);
            World.DrawCylinder(new Vector3(center.X + size.X * 0.15f, min.Y + 0.9f, center.Z), 0.45f, 0.45f, 1.6f, 12, appliance);
            World.DrawCube(new Vector3(center.X, min.Y + h - 0.4f, center.Z), Math.Max(0.8f, size.X * 0.5f), 0.1f, 0.1f, steel);
            World.DrawCube(new Vector3(center.X, min.Y + h - 0.55f, center.Z), 0.1f, 0.1f, Math.Max(0.8f, size.Z * 0.4f), steel);
            return;
        }

        if (name.Contains("Airlock", StringComparison.OrdinalIgnoreCase))
        {
            World.DrawCube(new Vector3(center.X, min.Y + 0.35f, center.Z), Math.Min(1.4f, size.X * 0.7f), 0.45f, 0.45f, console);
            foreach (var (dx, dz) in new[] { (-0.35f, -0.35f), (0.35f, -0.35f), (-0.35f, 0.35f), (0.35f, 0.35f) })
            {
                World.DrawCube(new Vector3(center.X + dx * size.X * 0.35f, min.Y + 1.2f, center.Z + dz * size.Z * 0.35f), 0.07f, 1.5f, 0.07f, steel);
                World.DrawSphere(new Vector3(center.X + dx * size.X * 0.35f, min.Y + 1.85f, center.Z + dz * size.Z * 0.35f), 0.06f, AccentLight);
            }
            var hatch = ShadeColor(Steel, 0.4f, 0.55f, Vector3.UnitZ, LightDir);
            World.DrawCube(new Vector3(center.X, min.Y + 1.1f, min.Z + 0.12f), Math.Max(0.8f, size.X * 0.85f), 2.0f, 0.1f, hatch);
            World.DrawCube(new Vector3(center.X, min.Y + 1.1f, max.Z - 0.12f), Math.Max(0.8f, size.X * 0.85f), 2.0f, 0.1f, hatch);
            return;
        }

        if (name.Contains("Corridor", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Hallway", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Access", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("VEST-", StringComparison.OrdinalIgnoreCase))
        {
            var span = Math.Max(0.5f, Math.Max(size.Z, size.X) * 0.9f);
            var alongZ = size.Z >= size.X;
            // Overhead raceway flush into ceiling slab (avoid a second floating beam layer).
            if (alongZ)
                World.DrawCube(new Vector3(center.X, min.Y + h - 0.16f, center.Z), 0.22f, 0.08f, span * 0.85f, glow);
            else
                World.DrawCube(new Vector3(center.X, min.Y + h - 0.16f, center.Z), span * 0.85f, 0.08f, 0.22f, glow);
            // Deck centerline (muted)
            if (alongZ)
                World.DrawCube(new Vector3(center.X, min.Y + 0.05f, center.Z), 0.2f, 0.03f, span, ShadeColor(AccentLight, 0.55f, 0.15f, Vector3.UnitY, LightDir));
            else
                World.DrawCube(new Vector3(center.X, min.Y + 0.05f, center.Z), span, 0.03f, 0.2f, ShadeColor(AccentLight, 0.55f, 0.15f, Vector3.UnitY, LightDir));
            return;
        }

        if (name.Contains("Stairs", StringComparison.OrdinalIgnoreCase))
        {
            for (var i = 0; i < 6; i++)
                World.DrawCube(new Vector3(center.X, min.Y + 0.15f + i * 0.28f, min.Z + 0.4f + i * 0.35f), Math.Max(0.8f, size.X * 0.6f), 0.1f, 0.35f, steel);
            var railX = center.X + size.X * 0.35f;
            World.DrawLine(new Vector3(railX, min.Y + 0.9f, min.Z + 0.3f), new Vector3(railX, min.Y + 2.4f, max.Z - 0.3f), AccentLight);
            World.DrawLine(new Vector3(center.X - size.X * 0.35f, min.Y + 0.9f, min.Z + 0.3f), new Vector3(center.X - size.X * 0.35f, min.Y + 2.4f, max.Z - 0.3f), AccentLight);
            return;
        }

        if (name.Contains("Elev", StringComparison.OrdinalIgnoreCase))
        {
            var car = center + new Vector3(0, h * 0.4f, 0);
            var carSx = Math.Max(0.8f, size.X * 0.7f);
            var carSz = Math.Max(0.8f, size.Z * 0.7f);
            World.DrawCube(car, carSx, h * 0.75f, carSz, console);
            World.DrawCube(car + new Vector3(0, 0, carSz * 0.5f), carSx * 0.7f, h * 0.55f, 0.06f, steel);
            World.DrawCube(car + new Vector3(-carSx * 0.32f, 0, carSz * 0.5f), 0.06f, h * 0.55f, 0.08f, AccentLight);
            World.DrawCube(car + new Vector3(carSx * 0.32f, 0, carSz * 0.5f), 0.06f, h * 0.55f, 0.08f, AccentLight);
            return;
        }

        if (name.Contains("Cargo", StringComparison.OrdinalIgnoreCase) && space.Flags?.Hollow != true)
        {
            for (var z = min.Z + 1.5f; z < max.Z - 1f; z += 3f)
                World.DrawCube(new Vector3(center.X, min.Y + 0.12f, z), Math.Max(0.5f, size.X * 0.7f), 0.08f, 0.25f, steel);
        }
    }

    private void DrawSolidBox(CadEntity box)
    {
        // Points[0]=center, Points[1]=halfExtents (or Thickness/Height fallback).
        if (box.Points is not { Count: >= 2 })
            return;
        var c = SvgCoords.FromArray(box.Points[0]);
        var he = SvgCoords.FromArray(box.Points[1]);
        var isC40 = box.Name?.StartsWith("C40", StringComparison.OrdinalIgnoreCase) == true;
        var isNacelle = box.Name?.Contains("nacelle", StringComparison.OrdinalIgnoreCase) == true;
        // Nacelles: exterior-only. C40 stow: visible in orbit + cargo interior (+ cutaway).
        if (isNacelle && _session.ViewMode == CalypsoViewMode.Interior)
            return;
        if (isC40 && _session.ViewMode == CalypsoViewMode.Plan)
            return; // keep plan readable; containers show in orbit/interior
        if (isC40 && _session.ViewMode == CalypsoViewMode.Interior && !ShowC40InInterior())
            return;
        // Near containers fill the FOV and read as "camera inside a wall" — keep a standoff.
        if (isC40 && _session.ViewMode == CalypsoViewMode.Interior)
        {
            var near = Math.Max(Math.Abs(he.X), Math.Max(Math.Abs(he.Y), Math.Abs(he.Z))) + 6.5f;
            if (Vector3.Distance(c, _interiorEye) < near)
                return;
        }

        var color = box.Color is { Length: >= 3 } rgb
            ? Color.FromArgb(255,
                (int)(Math.Clamp(rgb[0], 0, 1) * 255),
                (int)(Math.Clamp(rgb[1], 0, 1) * 255),
                (int)(Math.Clamp(rgb[2], 0, 1) * 255))
            : _session.ResolveShapeColor(box.ShapeId, Steel);
        // Soften C40 fleet tint variety (was muddy olive/orange banding).
        if (isC40 && box.Name is { } c40Name)
        {
            // Stable single fleet color — per-box hash made adjacent faces read as translucent layers.
            _ = c40Name;
            color = Color.FromArgb(255, 168, 128, 62);
        }
        var cutaway = _session.WireMeshMode == CalypsoWireMeshMode.CutawayPartial;
        GetCutPlane(cutaway, out var cutPt, out var cutN);
        if (cutaway && CulledByCutPlane(c, cutPt, cutN))
            return;
        var sx = Math.Abs(he.X) * 2f;
        var sy = Math.Abs(he.Y) * 2f;
        var sz = Math.Abs(he.Z) * 2f;
        var body = ShadeColor(color, isC40 ? 0.55f : 0.4f, isC40 ? 0.25f : 0.65f, Vector3.UnitY, LightDir);
        World.DrawCube(c, sx, sy, sz, body);
        var wireMode = _session.WireMeshMode == CalypsoWireMeshMode.Wire;
        if (wireMode)
            World.DrawCubeWiresV(c, new Vector3(sx, sy, sz), Bulkhead);

        if (isC40)
        {
            // Solid body only in close interior POVs — rib overlays still z-fight at grazing angles.
            if (!cutaway && _session.ViewMode != CalypsoViewMode.Interior)
                DrawC40Detail(c, sx, sy, sz, color);
            return;
        }

        // End-cap disks + panel seams (nacelles / exterior boxes).
        var cap = ShadeColor(Steel, 0.4f, 0.55f, Vector3.UnitZ, LightDir);
        var r = Math.Min(sx, sy) * 0.42f;
        World.DrawCylinder(c + new Vector3(0, 0, sz * 0.52f), r, r * 0.85f, 0.22f, 12, cap);
        World.DrawCylinder(c + new Vector3(0, 0, -sz * 0.52f), r * 0.85f, r, 0.22f, 12, cap);
        World.DrawCube(c + new Vector3(0, 0, sz * 0.5f), sx * 0.95f, sy * 0.95f, 0.08f, cap);
        World.DrawCube(c + new Vector3(0, 0, -sz * 0.5f), sx * 0.95f, sy * 0.95f, 0.08f, cap);
        for (var i = -1; i <= 1; i++)
        {
            var y = c.Y + i * sy * 0.28f;
            World.DrawLine(new Vector3(c.X - sx * 0.45f, y, c.Z - sz * 0.48f), new Vector3(c.X - sx * 0.45f, y, c.Z + sz * 0.48f), Bulkhead);
            World.DrawLine(new Vector3(c.X + sx * 0.45f, y, c.Z - sz * 0.48f), new Vector3(c.X + sx * 0.45f, y, c.Z + sz * 0.48f), Bulkhead);
        }
    }

    private void DrawC40Detail(Vector3 c, float sx, float sy, float sz, Color baseColor)
    {
        // Keep detail sparse and clearly proud of the body to avoid z-fight hash on close POVs.
        var steel = ShadeColor(Steel, 0.4f, 0.5f, Vector3.UnitY, LightDir);
        var door = ShadeColor(Color.FromArgb(255, 48, 52, 56), 0.5f, 0.35f, Vector3.UnitZ, LightDir);
        var rib = ShadeColor(Color.FromArgb(255,
            Math.Max(0, baseColor.R - 35), Math.Max(0, baseColor.G - 28), Math.Max(0, baseColor.B - 18)), 0.5f, 0.25f, Vector3.UnitX, LightDir);

        // Only end-door + three side ribs (not a corrugation forest).
        for (var i = -1; i <= 1; i++)
        {
            var zOff = i * (sz * 0.28f);
            World.DrawCube(c + new Vector3(-sx * 0.54f, 0, zOff), 0.12f, sy * 0.78f, 0.1f, rib);
            World.DrawCube(c + new Vector3(sx * 0.54f, 0, zOff), 0.12f, sy * 0.78f, 0.1f, rib);
        }
        World.DrawCube(c + new Vector3(0, 0, -sz * 0.54f), sx * 0.72f, sy * 0.78f, 0.14f, door);
        World.DrawCube(c + new Vector3(0, 0, -sz * 0.56f), 0.1f, sy * 0.7f, 0.1f, steel);
    }

    private static void DrawPolygonFloor(Vector3[] ring, Vector3 min, Vector3 max, float y, Color color, float tile)
    {
        var sizeX = Math.Max(0.2f, max.X - min.X);
        var sizeZ = Math.Max(0.2f, max.Z - min.Z);
        // Small compartments: single AABB slab (grid sampling underfills).
        if (sizeX < 2.2f || sizeZ < 2.2f)
        {
            World.DrawCube(new Vector3((min.X + max.X) * 0.5f, y, (min.Z + max.Z) * 0.5f), sizeX, 0.05f, sizeZ, color);
            return;
        }

        var drawn = 0;
        for (var x = min.X + tile * 0.5f; x <= max.X; x += tile)
        for (var z = min.Z + tile * 0.5f; z <= max.Z; z += tile)
        {
            if (!PointInPolygonXZ(new Vector3(x, 0, z), ring))
                continue;
            World.DrawCube(new Vector3(x, y, z), tile * 0.95f, 0.05f, tile * 0.95f, color);
            drawn++;
        }

        if (drawn == 0)
            World.DrawCube(new Vector3((min.X + max.X) * 0.5f, y, (min.Z + max.Z) * 0.5f), sizeX, 0.05f, sizeZ, color);
    }

    private static bool PointInPolygonXZ(Vector3 p, Vector3[] ring)
    {
        var inside = false;
        for (int i = 0, j = ring.Length - 1; i < ring.Length; j = i++)
        {
            var xi = ring[i].X;
            var zi = ring[i].Z;
            var xj = ring[j].X;
            var zj = ring[j].Z;
            var intersect = ((zi > p.Z) != (zj > p.Z)) &&
                            (p.X < (xj - xi) * (p.Z - zi) / ((zj - zi) + 1e-8f) + xi);
            if (intersect)
                inside = !inside;
        }
        return inside;
    }

    private void DrawHollowShaft(
        Vector3[] ring, Vector3 min, Vector3 max, Vector3 center, Vector3 size, float h,
        bool cutaway, Vector3 cutPt, Vector3 cutN)
    {
        // Hold walls: flush panels only (ribs read as floating pillars beside C40).
        var wallH = Math.Min(h, 9f);
        for (var i = 0; i < ring.Length; i++)
        {
            var a = ring[i];
            var b = ring[(i + 1) % ring.Length];
            var mid = (a + b) * 0.5f;
            if (cutaway && CulledByCutPlane(mid, cutPt, cutN))
                continue;
            DrawInteriorWallPanel(a, b, wallH, ribs: false);
        }

        // Hold deck plate
        var holdDeck = ShadeColor(Color.FromArgb(255, 52, 56, 62), 0.65f, 0.2f, Vector3.UnitY, LightDir);
        World.DrawCube(new Vector3(center.X, min.Y + 0.04f, center.Z), Math.Max(1f, size.X * 0.98f), 0.1f, Math.Max(1f, size.Z * 0.98f), holdDeck);

        var steel = ShadeColor(Steel, 0.5f, 0.4f, Vector3.UnitY, LightDir);
        var amber = ShadeColor(Color.FromArgb(255, 120, 100, 55), 0.55f, 0.25f, Vector3.UnitY, LightDir);

        // Overhead gantry ONLY above clear height of C40 stack (~7.8 m) — no beams through containers.
        var gantryY = min.Y + Math.Min(wallH * 0.92f, 8.4f);
        World.DrawCube(new Vector3(center.X, gantryY, center.Z), Math.Max(2f, size.X * 0.85f), 0.14f, 0.14f, steel);
        World.DrawCube(new Vector3(center.X, gantryY, center.Z), 0.14f, 0.14f, Math.Max(2f, size.Z * 0.85f), steel);

        // Fore catwalk band (DK0 plate height) — clear of C40 aft stack.
        var catZ = max.Z - 1.2f;
        var catW = Math.Max(1f, size.X * 0.7f);
        World.DrawCube(new Vector3(center.X, min.Y + 4.05f, catZ), catW, 0.1f, 2.2f, steel);
        World.DrawCube(new Vector3(center.X - catW * 0.5f, min.Y + 4.55f, catZ), 0.06f, 0.9f, 2.0f, steel);
        World.DrawCube(new Vector3(center.X + catW * 0.5f, min.Y + 4.55f, catZ), 0.06f, 0.9f, 2.0f, steel);

        // Tie-downs only in the side aisles (not under the container footprint).
        if (!cutaway)
        {
            for (var z = min.Z + 1.5f; z < max.Z - 2.5f; z += 3.2f)
            {
                World.DrawCube(new Vector3(min.X + 0.55f, min.Y + 0.1f, z), 0.3f, 0.1f, 0.3f, amber);
                World.DrawCube(new Vector3(max.X - 0.55f, min.Y + 0.1f, z), 0.3f, 0.1f, 0.3f, amber);
            }
        }

        // Aft ramp cue — solid interior only (cutaway makes the steps read as floating planks).
        if (!cutaway)
        {
            for (var i = 0; i < 4; i++)
            {
                World.DrawCube(
                    new Vector3(center.X, min.Y + 0.1f + i * 0.35f, min.Z + 0.6f + i * 0.55f),
                    Math.Max(2.5f, size.X * 0.35f),
                    0.12f,
                    0.5f,
                    amber);
            }
        }
    }

    private void DrawCeilingAndLightStrip(Vector3 center, Vector3 size, float h, bool cutaway, Vector3 cutPt, Vector3 cutN)
    {
        var ceilingCenter = center + new Vector3(0, h - 0.04f, 0);
        if (cutaway && CulledByCutPlane(ceilingCenter, cutPt, cutN))
            return;

        // Continuous ceiling slab that actually encloses the room (inset so it doesn't z-fight walls).
        var ceiling = ShadeColor(Color.FromArgb(255, 64, 68, 74), 0.8f, 0.08f, -Vector3.UnitY, LightDir);
        World.DrawCube(ceilingCenter, Math.Max(0.4f, size.X - 0.08f), 0.18f, Math.Max(0.4f, size.Z - 0.08f), ceiling);

        var alongZ = size.Z >= size.X;
        var cove = ShadeColor(AccentCyan, 0.45f, 0.3f, -Vector3.UnitY, LightDir);
        if (alongZ)
            World.DrawCube(ceilingCenter + new Vector3(0, -0.14f, 0), Math.Min(0.28f, size.X * 0.18f), 0.05f, size.Z * 0.75f, cove);
        else
            World.DrawCube(ceilingCenter + new Vector3(0, -0.14f, 0), size.X * 0.75f, 0.05f, Math.Min(0.28f, size.Z * 0.18f), cove);
    }

    private void DrawDeckPanelGrid(Vector3 min, Vector3 max, float h)
    {
        const float step = 1.2f;
        var line = Color.FromArgb(180, 70, 74, 78);
        for (var x = min.X; x <= max.X + 0.01f; x += step)
            World.DrawLine(new Vector3(x, h, min.Z), new Vector3(x, h, max.Z), line);
        for (var z = min.Z; z <= max.Z + 0.01f; z += step)
            World.DrawLine(new Vector3(min.X, h, z), new Vector3(max.X, h, z), line);
    }

    private void DrawPerimeterBulkheads(
        Vector3[] ring, float h, List<CadEntity>? openings, bool cutaway, Vector3 cutPt, Vector3 cutN)
    {
        for (var i = 0; i < ring.Length; i++)
        {
            var a = ring[i];
            var b = ring[(i + 1) % ring.Length];
            var mid = (a + b) * 0.5f;
            if (cutaway && CulledByCutPlane(mid, cutPt, cutN))
                continue;

            // Carve door clearances so leaves sit in gaps (avoids z-fight pillars in hallways).
            foreach (var (sa, sb) in SplitSegmentAroundOpenings(a, b, openings))
                DrawInteriorWallPanel(sa, sb, h);
        }
    }

    /// <summary>Split wall edge into solid runs, carving out opening footprints that touch the edge.</summary>
    private static List<(Vector3 A, Vector3 B)> SplitSegmentAroundOpenings(Vector3 a, Vector3 b, List<CadEntity>? openings)
    {
        var result = new List<(Vector3, Vector3)>();
        var dir = b - a;
        dir.Y = 0;
        var len = dir.Length();
        if (len < 0.05f)
            return result;
        dir /= len;

        var cuts = new List<(float t0, float t1)>();
        if (openings is { Count: > 0 })
        {
            foreach (var opening in openings)
            {
                if (opening.Footprint is not { Count: >= 3 } fp)
                    continue;
                BoundsOf(fp.Select(SvgCoords.FromArray).ToArray(), out var omin, out var omax);
                var oc = (omin + omax) * 0.5f;
                if (!ClosestPointOnSegmentXZ(a, b, oc, out var onEdge, out var t))
                    continue;
                var dist = Vector2.Distance(new Vector2(onEdge.X, onEdge.Z), new Vector2(oc.X, oc.Z));
                var halfW = Math.Max(omax.X - omin.X, omax.Z - omin.Z) * 0.55f;
                if (dist > halfW + 0.35f)
                    continue;
                var ht = Math.Clamp(halfW / len, 0.02f, 0.45f);
                cuts.Add((Math.Clamp(t - ht, 0f, 1f), Math.Clamp(t + ht, 0f, 1f)));
            }
        }

        if (cuts.Count == 0)
        {
            result.Add((a, b));
            return result;
        }

        cuts.Sort((x, y) => x.t0.CompareTo(y.t0));
        // Merge overlapping cut intervals
        var merged = new List<(float t0, float t1)> { cuts[0] };
        for (var i = 1; i < cuts.Count; i++)
        {
            var last = merged[^1];
            if (cuts[i].t0 <= last.t1 + 0.01f)
                merged[^1] = (last.t0, Math.Max(last.t1, cuts[i].t1));
            else
                merged.Add(cuts[i]);
        }

        float cursor = 0f;
        foreach (var (t0, t1) in merged)
        {
            if (t0 - cursor > 0.02f)
                result.Add((a + dir * (cursor * len), a + dir * (t0 * len)));
            cursor = t1;
        }
        if (1f - cursor > 0.02f)
            result.Add((a + dir * (cursor * len), b));
        return result;
    }

    private static bool ClosestPointOnSegmentXZ(Vector3 a, Vector3 b, Vector3 p, out Vector3 onEdge) =>
        ClosestPointOnSegmentXZ(a, b, p, out onEdge, out _);

    private static bool ClosestPointOnSegmentXZ(Vector3 a, Vector3 b, Vector3 p, out Vector3 onEdge, out float t)
    {
        var ab = new Vector2(b.X - a.X, b.Z - a.Z);
        var len2 = ab.LengthSquared();
        if (len2 < 1e-8f)
        {
            onEdge = a;
            t = 0f;
            return false;
        }
        t = Math.Clamp(((p.X - a.X) * ab.X + (p.Z - a.Z) * ab.Y) / len2, 0f, 1f);
        onEdge = new Vector3(a.X + ab.X * t, a.Y, a.Z + ab.Y * t);
        return true;
    }

    private void DrawInteriorWallPanel(Vector3 a, Vector3 b, float h, bool ribs = true)
    {
        var dir = b - a;
        dir.Y = 0;
        var len = dir.Length();
        if (len < 0.05f)
            return;
        dir /= len;
        var normal = Vector3.Normalize(Vector3.Cross(dir, Vector3.UnitY));
        var lining = ShadeColor(Lining, 0.8f, 0.05f, normal, LightDir);
        var steel = ShadeColor(Steel, 0.45f, 0.55f, normal, LightDir);
        // Sit on the footprint edge (slightly outward) so the room volume stays clear of the camera.
        var mid = Vector3.Lerp(a, b, 0.5f) - normal * 0.04f;
        var panelH = Math.Min(h, 3.55f);

        // One continuous slab — dual bands z-fought and read as floating caps.
        DrawWallSlab(mid + new Vector3(0, panelH * 0.5f, 0), dir, len, panelH * 0.98f, 0.08f, lining);

        // Structural ribs only on long runs (skip narrow corridor clutter / cargo shaft).
        if (!ribs || len < 4.5f)
            return;
        const float spacing = 3.0f;
        for (var d = 1.0f; d < len - 0.5f; d += spacing)
        {
            var p = a + dir * d - normal * 0.02f;
            World.DrawCube(p + new Vector3(0, panelH * 0.5f, 0), 0.06f, panelH * 0.88f, 0.06f, steel);
        }
    }

    private static void DrawWallRibs(Vector3 a, Vector3 b, float h, Color color)
    {
        var dir = b - a;
        dir.Y = 0;
        var len = dir.Length();
        if (len < 0.1f)
            return;
        dir /= len;
        const float spacing = 1.5f;
        for (var d = 0f; d <= len + 0.01f; d += spacing)
        {
            var p = a + dir * d;
            World.DrawCube(p + new Vector3(0, h * 0.5f, 0), 0.07f, h, 0.07f, color);
        }
    }

    private void DrawDoorJamb(Vector3 a, Vector3 b, float h)
    {
        var jambH = Math.Min(h, 2.2f);
        var frame = ShadeColor(Steel, 0.4f, 0.45f, Vector3.UnitY, LightDir);
        World.DrawCube(a + new Vector3(0, jambH * 0.5f, 0), 0.14f, jambH, 0.14f, frame);
        World.DrawCube(b + new Vector3(0, jambH * 0.5f, 0), 0.14f, jambH, 0.14f, frame);
        var lintel = (a + b) * 0.5f + new Vector3(0, jambH, 0);
        var span = Vector3.Distance(new Vector3(a.X, 0, a.Z), new Vector3(b.X, 0, b.Z));
        World.DrawCube(lintel, Math.Max(0.2f, span), 0.12f, 0.14f, frame);
    }

    private void DrawOpeningFrame(CadEntity opening)
    {
        if (opening.Footprint is not { Count: >= 3 } fp)
            return;
        var ring = fp.Select(SvgCoords.FromArray).ToArray();
        BoundsOf(ring, out var min, out var max);
        var c = (min + max) * 0.5f;
        var size = max - min;
        var h = Math.Max(1.8f, opening.Height > 0 ? opening.Height : 2.1f);
        var cutaway = _session.WireMeshMode == CalypsoWireMeshMode.CutawayPartial;
        GetCutPlane(cutaway, out var cutPt, out var cutN);
        if (cutaway && CulledByCutPlane(c + new Vector3(0, h * 0.5f, 0), cutPt, cutN))
            return;
        var frame = ShadeColor(Steel, 0.45f, 0.4f, Vector3.UnitY, LightDir);
        var sx = Math.Max(0.4f, size.X);
        var sz = Math.Max(0.2f, size.Z);
        if (_session.WireMeshMode == CalypsoWireMeshMode.Wire)
            World.DrawCubeWiresV(c + new Vector3(0, h * 0.5f, 0), new Vector3(sx, h, sz), frame);
        // Lintel flush to leaf top (steel — OpeningFrame amber reads as floating candy)
        World.DrawCube(c + new Vector3(0, h * 0.95f, 0), sx * 1.02f, 0.1f, Math.Max(sx, sz) * 0.15f, frame);

        var kind = opening.OpeningType ?? "door";
        var tag = opening.Properties is not null &&
                  opening.Properties.TryGetValue("tag", out var tagEl) &&
                  tagEl.ValueKind == System.Text.Json.JsonValueKind.String
            ? tagEl.GetString() ?? ""
            : "";
        var armored = opening.Name?.Contains("Armored", StringComparison.OrdinalIgnoreCase) == true
                      || tag.StartsWith("CD-", StringComparison.OrdinalIgnoreCase)
                      || (opening.Name?.Contains("Cargo Hatch", StringComparison.OrdinalIgnoreCase) ?? false);
        if (string.Equals(kind, "ramp", StringComparison.OrdinalIgnoreCase))
        {
            // Stepped wedge sloping aft (stern = +Z / max.Z side of footprint).
            var stepCount = 4;
            var rampW = Math.Max(2.5f, sx * 0.95f);
            var rampDepth = Math.Max(2.2f, sz);
            var amber = ShadeColor(Color.FromArgb(255, 140, 110, 55), 0.55f, 0.25f, Vector3.UnitY, LightDir);
            for (var i = 0; i < stepCount; i++)
            {
                var t = (i + 0.5f) / stepCount;
                var y = min.Y + 0.08f + i * (h * 0.22f);
                var z = max.Z - t * rampDepth;
                var thick = Math.Max(0.35f, rampDepth / stepCount);
                World.DrawCube(new Vector3(c.X, y, z), rampW, 0.14f, thick, amber);
            }
            return;
        }

        // Open leaves: frame/lintel only — walkable clear opening (no slab through the hatch).
        if (ShipCad.GetLeafState(opening) == ShipLeafState.Open)
            return;

        // Door / hatch leaf: thickness always on the short footprint axis so mis-oriented
        // schedules can't fill a 2 m corridor with a face-on slab.
        var leafH = Math.Min(h, 2.05f);
        var leafThick = string.Equals(kind, "hatch", StringComparison.OrdinalIgnoreCase) ? 0.1f : 0.06f;
        var leafColor = armored
            ? ShadeColor(Color.FromArgb(255, 120, 105, 70), 0.45f, 0.4f, Vector3.UnitZ, LightDir)
            : ShadeColor(Color.FromArgb(255, 58, 62, 68), 0.5f, 0.4f, Vector3.UnitZ, LightDir);
        var face = Math.Max(sx, sz) * 0.88f;
        var leafDepth = Math.Clamp(Math.Min(sx, sz) * 0.35f, 0.05f, leafThick);
        // Skip absurd leaves that would still fill a corridor after orientation fix.
        if (Math.Min(sx, sz) > 1.2f && Math.Max(sx, sz) > 2.5f)
            return;
        if (sx >= sz)
            World.DrawCube(c + new Vector3(0, leafH * 0.5f, 0), face, leafH * 0.9f, leafDepth, leafColor);
        else
            World.DrawCube(c + new Vector3(0, leafH * 0.5f, 0), leafDepth, leafH * 0.9f, face, leafColor);
        World.DrawCube(c + new Vector3(0, leafH * 0.45f, 0) + (sx >= sz ? new Vector3(face * 0.28f, 0, 0) : new Vector3(0, 0, face * 0.28f)),
            0.04f, 0.12f, 0.05f, AccentCyan);
    }

    private static void DrawHookMarker(Vector3 p)
    {
        World.DrawSphere(p + new Vector3(0, 1.2f, 0), 0.12f, AccentCyan);
        World.DrawLine(p, p + new Vector3(0, 1.2f, 0), AccentCyan);
    }

    private void DrawWall(CadEntity wall)
    {
        var segments = GetBaseline(wall);
        if (segments.Count == 0)
            return;

        var wire = _session.WireMeshMode == CalypsoWireMeshMode.Wire;
        var cutaway = _session.WireMeshMode == CalypsoWireMeshMode.CutawayPartial;
        GetCutPlane(cutaway, out var cutPt, out var cutN);

        var interior = _session.ViewMode == CalypsoViewMode.Interior;
        var matA = _session.ResolveShapeMaterial(wall.Sides?.A?.ShapeId ?? wall.ShapeId, WallFallback);
        var matB = _session.ResolveShapeMaterial(wall.Sides?.B?.ShapeId ?? wall.ShapeId, WallFallback);
        // Interior: prefer lining/steel over CAD zone colors on partitions.
        var colorA = interior ? Lining : matA.color;
        var colorB = interior ? Steel : matB.color;
        var halfT = Math.Max(0.05f, wall.Thickness * 0.5f);
        var h = Math.Max(0.5f, wall.Height);

        foreach (var (a, b) in segments)
        {
            var dir = b - a;
            dir.Y = 0;
            if (dir.LengthSquared() < 1e-6f)
                continue;
            dir = Vector3.Normalize(dir);
            var normalA = Vector3.Normalize(Vector3.Cross(dir, Vector3.UnitY));
            var mid = (a + b) * 0.5f;
            var length = Vector3.Distance(new Vector3(a.X, 0, a.Z), new Vector3(b.X, 0, b.Z));
            var center = mid + new Vector3(0, h * 0.5f, 0);

            if (cutaway && CulledByCutPlane(center, cutPt, cutN))
                continue;

            if (wire)
            {
                World.DrawLine(a + new Vector3(0, 0.04f, 0), b + new Vector3(0, 0.04f, 0), Bulkhead);
                World.DrawLine(a + new Vector3(0, h, 0), b + new Vector3(0, h, 0), Bulkhead);
                DrawWallRibs(a, b, h, Steel);
                continue;
            }

            // Interior / cutaway orbit: skip exterior hull skins (rim shell carries silhouette).
            if (interior || cutaway)
            {
                if (wall.Name?.StartsWith("hull", StringComparison.OrdinalIgnoreCase) == true)
                    continue;
                if (interior)
                    continue;
            }

            var offset = normalA * (halfT * 0.55f);
            var shadedA = ShadeColor(colorA, matA.roughness, matA.metalness, normalA, LightDir);
            var shadedB = ShadeColor(colorB, matB.roughness, matB.metalness, -normalA, LightDir);
            DrawWallSlab(center + offset, dir, length, h, halfT * 0.9f, shadedA);
            DrawWallSlab(center - offset, dir, length, h, halfT * 0.9f, shadedB);
            // No CubeWires in solid orbit — wireframe edges make the ship look like CAD greybox.

            if (wall.Name?.StartsWith("hull", StringComparison.OrdinalIgnoreCase) == true)
                DrawHullSeamsAndRivets(a, b, h, normalA);
        }
    }

    private static void DrawHullSeamsAndRivets(Vector3 a, Vector3 b, float h, Vector3 outward)
    {
        var dir = b - a;
        dir.Y = 0;
        var len = dir.Length();
        if (len < 0.4f)
            return;
        dir /= len;
        var seam = Color.FromArgb(200, 90, 96, 104);
        var rivet = Color.FromArgb(255, 120, 126, 134);
        // Panel seam chords along baseline + mid height
        World.DrawLine(a + outward * 0.12f, b + outward * 0.12f, seam);
        World.DrawLine(a + new Vector3(0, h * 0.5f, 0) + outward * 0.12f, b + new Vector3(0, h * 0.5f, 0) + outward * 0.12f, seam);
        World.DrawLine(a + new Vector3(0, h, 0) + outward * 0.12f, b + new Vector3(0, h, 0) + outward * 0.12f, seam);
        // Sparse rivet dots every ~1.7 m
        const float spacing = 1.7f;
        for (var d = 0.35f; d < len - 0.2f; d += spacing)
        {
            var p = a + dir * d + outward * 0.15f;
            World.DrawSphere(p + new Vector3(0, 0.25f, 0), 0.05f, rivet);
            World.DrawSphere(p + new Vector3(0, h * 0.5f, 0), 0.05f, rivet);
            World.DrawSphere(p + new Vector3(0, h - 0.25f, 0), 0.05f, rivet);
        }
    }

    private static void DrawWallSlab(Vector3 center, Vector3 dir, float length, float height, float thickness, Color color)
    {
        if (MathF.Abs(dir.X) < 0.15f || MathF.Abs(dir.Z) < 0.15f)
        {
            var sx = MathF.Abs(dir.X) >= MathF.Abs(dir.Z) ? length : thickness;
            var sz = MathF.Abs(dir.Z) > MathF.Abs(dir.X) ? length : thickness;
            World.DrawCube(center, sx, height, sz, color);
            return;
        }

        // Diagonal hull: continuous strip of thin posts + top/bottom chords (not a cube lattice).
        var a = center - dir * (length * 0.5f) - new Vector3(0, height * 0.5f, 0);
        var b = center + dir * (length * 0.5f) - new Vector3(0, height * 0.5f, 0);
        World.DrawLine(a, b, color);
        World.DrawLine(a + new Vector3(0, height, 0), b + new Vector3(0, height, 0), color);
        const float step = 0.7f;
        for (var t = 0f; t <= 1.001f; t += step / Math.Max(0.7f, length))
        {
            var p = Vector3.Lerp(a, b, t);
            World.DrawCube(p + new Vector3(0, height * 0.5f, 0), thickness, height, thickness, color);
        }
    }

    private void SyncCutPlane(bool cutaway)
    {
        if (!cutaway)
            return;

        Vector3 eye;
        if (_session.ViewMode == CalypsoViewMode.Interior)
            eye = _interiorEye;
        else if (_session.ViewMode == CalypsoViewMode.Orbit)
            eye = _orbit.BuildEyePosition();
        else
            eye = new Vector3(0f, 120f, 0.01f);

        if (_session.ViewMode == CalypsoViewMode.Interior)
        {
            var space = _session.SelectedSpace;
            var origin = space is not null
                ? CalypsoSession.SpaceCentroid(space) + new Vector3(0f, Math.Max(1.5f, space.Height * 0.4f), 0f)
                : new Vector3(0f, 4f, 0f);
            var towardEye = eye - origin;
            towardEye.Y = 0f;
            if (towardEye.LengthSquared() < 1e-4f)
                towardEye = Vector3.UnitX;
            _session.CutPlaneOrigin = origin;
            _session.CutPlaneNormal = Vector3.Normalize(towardEye);
            return;
        }

        // Orbit / plan: world longitudinal (default) or beam cut; normal faces camera.
        // Offset slides the invisible plane; user keys mark CutPlaneUserDriven.
        var axis = _session.CutPlaneLongitudinal ? Vector3.UnitX : Vector3.UnitZ;
        if (Vector3.Dot(eye - new Vector3(0f, 4f, 0f), axis) < 0f)
            axis = -axis;
        var maxOff = _session.CutPlaneLongitudinal ? 10f : 34f;
        var offset = Math.Clamp(_session.CutPlaneOffset, -maxOff, maxOff);
        _session.CutPlaneOffset = offset;
        // Origin sits on the cut axis; normal points at the camera (culled half-space).
        var mid = new Vector3(0f, 4f, 0f) + (_session.CutPlaneLongitudinal
            ? new Vector3(offset, 0f, 0f)
            : new Vector3(0f, 0f, offset));
        _session.CutPlaneOrigin = mid;
        _session.CutPlaneNormal = axis;
    }

    private void GetCutPlane(bool cutaway, out Vector3 cutPt, out Vector3 cutN)
    {
        if (!cutaway)
        {
            cutPt = default;
            cutN = default;
            return;
        }

        cutPt = _session.CutPlaneOrigin;
        cutN = _session.CutPlaneNormal;
        if (cutN.LengthSquared() < 1e-6f)
            cutN = Vector3.UnitX;
        else
            cutN = Vector3.Normalize(cutN);
    }

    /// <summary>True when <paramref name="p"/> is on the camera side of the slicing plane (culled).</summary>
    private static bool CulledByCutPlane(Vector3 p, Vector3 cutPt, Vector3 cutN) =>
        Vector3.Dot(p - cutPt, cutN) > 0f;

    private void DrawCutFaceCue()
    {
        var o = _session.CutPlaneOrigin;
        var n = _session.CutPlaneNormal;
        if (n.LengthSquared() < 1e-6f)
            return;
        n = Vector3.Normalize(n);
        // Edge strip in the plane: span along LOA (Z) for longitudinal cut, along beam (X) for beam cut.
        var along = MathF.Abs(n.X) > MathF.Abs(n.Z)
            ? new Vector3(0f, 0f, 1f)
            : new Vector3(1f, 0f, 0f);
        var cue = Color.FromArgb(160, 140, 125, 55);
        const float halfSpan = 34f;
        for (var y = 0.5f; y <= 11.5f; y += 3.5f)
        {
            var a = o + along * (-halfSpan) + new Vector3(0f, y - o.Y, 0f);
            var b = o + along * halfSpan + new Vector3(0f, y - o.Y, 0f);
            a -= n * Vector3.Dot(a - o, n);
            b -= n * Vector3.Dot(b - o, n);
            World.DrawLine(a, b, cue);
        }
    }

    private static bool SpaceVisibleOnDeck(CadEntity space, int deck)
    {
        if (space.Deck == deck)
            return true;
        if (space.Properties is null)
            return false;
        if (space.Properties.TryGetValue("continuousVoid", out var cv) && cv.ValueKind == System.Text.Json.JsonValueKind.True)
            return deck is >= -1 and <= 1;
        if (space.Properties.TryGetValue("fullOah", out var fo) && fo.ValueKind == System.Text.Json.JsonValueKind.True)
            return deck is >= -1 and <= 1;
        return false;
    }

    private static bool WallVisibleOnDecks(CadEntity wall, HashSet<int> decks)
    {
        if (decks.Contains(wall.Deck))
            return true;
        // Full-OAH / hold continuous walls live on deck −1 but must read when neighbouring decks are active.
        if (wall.Height >= 8f && decks.Overlaps([-1, 0, 1]))
            return wall.Deck == -1;
        return false;
    }

    private static void BoundsOf(Vector3[] ring, out Vector3 min, out Vector3 max)
    {
        min = ring[0];
        max = ring[0];
        foreach (var p in ring)
        {
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }
    }

    private static Color ShadeColor(Color albedo, float roughness, float metalness, Vector3 normal, Vector3 lightDir)
    {
        roughness = Math.Clamp(roughness, 0f, 1f);
        metalness = Math.Clamp(metalness, 0f, 1f);

        var n = Vector3.Normalize(normal);
        var l = Vector3.Normalize(lightDir);
        var diff = MathF.Max(0f, Vector3.Dot(n, l));

        var ambient = 0.22f;
        var diffuseTerm = 0.55f + diff * 0.55f * (1f - roughness * 0.35f);
        var specularTerm = metalness * (1f - roughness) * MathF.Pow(diff, 12f) * 0.45f;

        var ar = albedo.R / 255f;
        var ag = albedo.G / 255f;
        var ab = albedo.B / 255f;

        var r = Math.Clamp(ambient * ar + diffuseTerm * ar + specularTerm, 0f, 1f);
        var g = Math.Clamp(ambient * ag + diffuseTerm * ag + specularTerm, 0f, 1f);
        var b = Math.Clamp(ambient * ab + diffuseTerm * ab + specularTerm, 0f, 1f);

        return Color.FromArgb(255, (int)(r * 255f), (int)(g * 255f), (int)(b * 255f));
    }

    private bool SegmentIntersectsOpening(Vector3 a, Vector3 b, List<CadEntity> openings)
    {
        foreach (var opening in openings)
        {
            if (SegmentIntersectsOpening(a, b, opening))
                return true;
        }

        return false;
    }

    private static bool SegmentIntersectsOpening(Vector3 a, Vector3 b, CadEntity opening)
    {
        if (opening.Footprint is not { Count: >= 3 } fp)
            return false;

        var cx = 0f;
        var cz = 0f;
        foreach (var p in fp)
        {
            var v = SvgCoords.FromArray(p);
            cx += v.X;
            cz += v.Z;
        }
        cx /= fp.Count;
        cz /= fp.Count;

        var maxD2 = 0f;
        foreach (var p in fp)
        {
            var v = SvgCoords.FromArray(p);
            var dx = v.X - cx;
            var dz = v.Z - cz;
            maxD2 = MathF.Max(maxD2, dx * dx + dz * dz);
        }

        var radius = MathF.Sqrt(maxD2);
        var mid = (a + b) * 0.5f;
        var ddx = mid.X - cx;
        var ddz = mid.Z - cz;
        var dist = MathF.Sqrt(ddx * ddx + ddz * ddz);

        return dist <= radius * 1.15f + 0.03f;
    }

    private static List<(Vector3 A, Vector3 B)> GetBaseline(CadEntity wall)
    {
        var list = new List<(Vector3, Vector3)>();
        if (wall.Points is { Count: >= 2 } pts)
        {
            for (var i = 0; i < pts.Count - 1; i++)
                list.Add((SvgCoords.FromArray(pts[i]), SvgCoords.FromArray(pts[i + 1])));
            return list;
        }

        if (wall.A is { Length: >= 3 } && wall.B is { Length: >= 3 })
            list.Add((SvgCoords.FromArray(wall.A), SvgCoords.FromArray(wall.B)));
        return list;
    }
}
