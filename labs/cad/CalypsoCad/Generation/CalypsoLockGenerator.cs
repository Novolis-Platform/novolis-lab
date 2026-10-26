using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using CalypsoCad.Models;
using CalypsoCad.Services;
using Novolis.Cad.Primitives;
using Novolis.Ship.Primitives;
using Novolis.Ship.Structure;

namespace CalypsoCad.Generation;

/// <summary>
/// Rev H lock-driven Calypso freighter from CAL-INT-GA-001.json (LOA 69 mid-stretch).
/// Emits spaces/walls/openings, vacuum-assisted D3 hatches, L-airlocks, 5 cabins, structure BOM/mass.
/// </summary>
internal static class CalypsoLockGenerator
{
    public const float DeckSpacing = 4f;
    public const float WallThickness = 0.15f;
    /// <summary>OML plate thickness (manufacturer CAL-HULL-CAD-001 / lock T_SHELL).</summary>
    public const float TShell = 0.008f;
    public const float TShellPublic = TShell;
    public const float OuterSkinAreaM2 = 3796.055f;
    public const float ManufacturerPad = 0.5f;

    /// <summary>Fallback when <c>hullLoft.stations</c> is missing. Numbers match CAL-INT-GA-001.json.</summary>
    private static readonly (float Z, float Beam, float H)[] ForeStations =
    [
        (0f, 3.5f, 4f),
        (3.25f, 10f, 7.5f),
        (10f, 17f, 10.5f),
        (17f, 20f, 12f),
        (41f, 20f, 12f),
        (65f, 20f, 12f),
        (69f, 20f, 12f),
    ];

    private static (float Z, float Beam, float H)[] ActiveStations = ForeStations;

    private static readonly Guid ShapeHullExt = Guid.Parse("e0000000-0000-4000-8000-000000000001");
    private static readonly Guid ShapeHullInt = Guid.Parse("e0000000-0000-4000-8000-000000000002");
    private static readonly Guid ShapeCorridor = Guid.Parse("e0000000-0000-4000-8000-000000000003");
    private static readonly Guid ShapeCargo = Guid.Parse("e0000000-0000-4000-8000-000000000004");
    private static readonly Guid ShapeHab = Guid.Parse("e0000000-0000-4000-8000-000000000005");
    private static readonly Guid ShapeEng = Guid.Parse("e0000000-0000-4000-8000-000000000006");
    private static readonly Guid ShapeUtil = Guid.Parse("e0000000-0000-4000-8000-000000000007");
    private static readonly Guid ShapeBridge = Guid.Parse("e0000000-0000-4000-8000-000000000008");
    private static readonly Guid ShapeLining = Guid.Parse("e0000000-0000-4000-8000-000000000009");

    private static readonly Guid LayerHull = Guid.Parse("d1000000-0000-4000-8000-000000000001");
    private static readonly Guid LayerWall = Guid.Parse("d1000000-0000-4000-8000-000000000002");
    private static readonly Guid LayerDoor = Guid.Parse("d1000000-0000-4000-8000-000000000003");
    private static readonly Guid LayerCorr = Guid.Parse("d1000000-0000-4000-8000-000000000004");
    private static readonly Guid LayerCargo = Guid.Parse("d1000000-0000-4000-8000-000000000005");
    private static readonly Guid LayerHab = Guid.Parse("d1000000-0000-4000-8000-000000000006");
    private static readonly Guid LayerEng = Guid.Parse("d1000000-0000-4000-8000-000000000007");
    private static readonly Guid LayerUtil = Guid.Parse("d1000000-0000-4000-8000-000000000008");
    private static readonly Guid LayerBridge = Guid.Parse("d1000000-0000-4000-8000-000000000009");
    private static readonly Guid LayerStruct = Guid.Parse("d1000000-0000-4000-8000-00000000000a");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static string DefaultOutputDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Novolis", "CalypsoCad", "generated");

    public static string Generate(string? outputDirectory = null)
    {
        var dir = outputDirectory ?? DefaultOutputDirectory;
        var stamp = DateTime.UtcNow.ToString("o");
        List<CadEntity>? preservedExterior = null;
        var existingCad = Path.Combine(dir, "calypso.cadjson");
        if (File.Exists(existingCad))
        {
            try
            {
                var prev = JsonSerializer.Deserialize<CadDocument>(File.ReadAllText(existingCad), CadJson.Options);
                preservedExterior = prev?.Entities
                    .Where(IsHandAuthoredExterior)
                    .Select(CloneEntity)
                    .ToList();
            }
            catch
            {
                // Corrupt prior — regenerate.
            }
        }

        var lockDoc = LoadLock();
        var layers = BuildLayers(stamp);
        var shapes = BuildShapes(stamp);
        var cad = BuildCad(stamp, lockDoc);
        if (preservedExterior is { Count: > 0 })
            cad.Entities.AddRange(preservedExterior);
        CadDocumentStore.WriteAll(dir, layers, shapes, cad);
        return dir;
    }

    /// <summary>
    /// Keep only hand-authored exterior (e.g. acceptance / Draft Studio saves).
    /// Generator-owned manufacturer OML/IML must be rebuilt each regenerate — otherwise an old blob sticks.
    /// </summary>
    private static bool IsHandAuthoredExterior(CadEntity entity)
    {
        if (!Novolis.Avalonia.Cad.Ship.Services.CadShipExterior.IsPreservedExterior(entity))
            return false;
        var name = entity.Name ?? "";
        if (name.Contains("nacelle", StringComparison.OrdinalIgnoreCase)
            || name.Contains("airlock-blister", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("ext-aft-cargo", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("ext-oml", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("ext-hull", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("int-iml", StringComparison.OrdinalIgnoreCase))
            return false;
        // Acceptance probe: only with handAuthored (stripped after the test so it does not stick as a bow blob).
        if (name.StartsWith("ext-user-", StringComparison.OrdinalIgnoreCase))
            return true;
        if (entity.Properties is not null
            && entity.Properties.TryGetValue("handAuthored", out var el)
            && el.ValueKind is JsonValueKind.True)
            return true;
        if (name.StartsWith("ext-acceptance", StringComparison.OrdinalIgnoreCase))
            return false;
        // Draft Studio meshes with exterior=true but not generator names.
        if (string.Equals(entity.Kind, "mesh", StringComparison.OrdinalIgnoreCase)
            && !name.StartsWith("ext-oml", StringComparison.OrdinalIgnoreCase)
            && !name.StartsWith("ext-hull", StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    private static CalypsoLockDocument LoadLock()
    {
        foreach (var path in CandidateLockPaths())
        {
            if (!File.Exists(path))
                continue;
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<CalypsoLockDocument>(json, JsonOpts)
                   ?? throw new InvalidOperationException($"Failed to parse lock JSON: {path}");
        }

        throw new FileNotFoundException(
            "CAL-INT-GA-001.json not found. Expected under docs/internals or output lock/.");
    }

    private static IEnumerable<string> CandidateLockPaths()
    {
        yield return Path.Combine(AppContext.BaseDirectory, "lock", "CAL-INT-GA-001.json");
        var proj = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
        yield return Path.Combine(proj, "docs", "internals", "CAL-INT-GA-001.json");
        yield return Path.Combine(
            @"d:\novolis\novolis-lab\labs\cad\CalypsoCad\docs\internals",
            "CAL-INT-GA-001.json");
    }

    private static CadEntity CloneEntity(CadEntity src)
    {
        var json = JsonSerializer.Serialize(src, CadJson.Options);
        return JsonSerializer.Deserialize<CadEntity>(json, CadJson.Options)
               ?? throw new InvalidOperationException("Failed to clone preserved exterior entity");
    }

    private static CadLayersDocument BuildLayers(string stamp) => new()
    {
        Name = "Calypso ship layers",
        Standard = "custom",
        StandardVersion = "1",
        CreatedAt = stamp,
        ModifiedAt = stamp,
        Layers =
        [
            Cat(LayerHull, "S-HULL", "S", "HULL", "Outer / inner hull shell", [0.42f, 0.48f, 0.55f]),
            Cat(LayerWall, "A-WALL", "A", "WALL", "Interior partitions", [0.45f, 0.55f, 0.7f]),
            Cat(LayerDoor, "A-DOOR", "A", "DOOR", "Doors and hatches", [0.7f, 0.55f, 0.3f]),
            Cat(LayerCorr, "A-CORR", "A", "CORR", "Corridors", [0.35f, 0.55f, 0.4f]),
            Cat(LayerCargo, "A-ZONE-CARGO", "A", "ZONE", "Cargo", [0.3f, 0.4f, 0.32f], ["CARG"]),
            Cat(LayerHab, "A-ZONE-HAB", "A", "ZONE", "Habitability", [0.45f, 0.52f, 0.58f], ["HAB"]),
            Cat(LayerEng, "A-ZONE-ENG", "A", "ZONE", "Engineering", [0.55f, 0.38f, 0.35f], ["ENG"]),
            Cat(LayerUtil, "A-ZONE-UTIL", "A", "ZONE", "Utilities", [0.42f, 0.44f, 0.55f], ["UTIL"]),
            Cat(LayerBridge, "A-ZONE-BRIDGE", "A", "ZONE", "Bridge", [0.4f, 0.42f, 0.48f], ["BRDG"]),
            Cat(LayerStruct, "S-STRUCT", "S", "STRUCT", "Frames, stringers, coaming", [0.55f, 0.58f, 0.62f], ["FRM"]),
        ],
    };

    private static CadCatalogLayer Cat(Guid id, string name, string disc, string major, string desc, float[] color, List<string>? minor = null) =>
        new()
        {
            Id = id,
            Name = name,
            Discipline = disc,
            Major = major,
            Minor = minor,
            Description = desc,
            DefaultColor = color,
            DefaultLineWeightMm = 0.35f,
        };

    private static CadShapesDocument BuildShapes(string stamp) => new()
    {
        Name = "Calypso Rev H lock shapes",
        CreatedAt = stamp,
        ModifiedAt = stamp,
        BaseDocument = "calypso.cadjson",
        Shapes =
        [
            Shape(ShapeHullExt, "hull-exterior", [0.42f, 0.48f, 0.55f], "hull-steel", 0.4f, 0.65f),
            Shape(ShapeHullInt, "hull-interior", [0.62f, 0.64f, 0.66f], "hull-lining", 0.72f, 0.08f),
            Shape(ShapeCorridor, "corridor-deck", [0.28f, 0.30f, 0.32f], "corridor-plate", 0.78f, 0.12f),
            Shape(ShapeCargo, "cargo-plate", [0.30f, 0.33f, 0.30f], "cargo-deck-plate", 0.62f, 0.2f),
            Shape(ShapeHab, "hab-fill", [0.48f, 0.52f, 0.56f], "hab-finish", 0.7f, 0.05f),
            Shape(ShapeEng, "eng-fill", [0.42f, 0.34f, 0.32f], "eng-deck", 0.55f, 0.18f),
            Shape(ShapeUtil, "util-fill", [0.38f, 0.40f, 0.46f], "util-deck", 0.65f, 0.1f),
            Shape(ShapeBridge, "bridge-fill", [0.36f, 0.38f, 0.42f], "bridge-console-deck", 0.48f, 0.22f),
            Shape(ShapeLining, "interior-lining", [0.70f, 0.72f, 0.74f], "cabin-lining", 0.82f, 0.04f),
        ],
    };

    private static CadShape Shape(Guid id, string name, float[] color, string preset, float roughness, float metalness) =>
        new()
        {
            Id = id,
            Name = name,
            Extensions = new CadShapeExtensions
            {
                Appearance = new CadAppearanceExtension
                {
                    Fill = new CadFill { Enabled = true, Color = color },
                    Stroke = new CadStroke { Color = color, LineWeightMm = 0.25f },
                },
                Material = new CadMaterialExtension
                {
                    Preset = preset,
                    Albedo = color,
                    Roughness = roughness,
                    Metalness = metalness,
                },
            },
        };

    private static CadDocument BuildCad(string stamp, CalypsoLockDocument lockDoc)
    {
        UseLoftStations(lockDoc);
        var env = lockDoc.Envelope ?? throw new InvalidOperationException("Lock missing envelope");
        var loa = (float)env.Loa;
        var beam = (float)env.Beam;
        var oah = (float)env.Oah;
        var entities = new List<CadEntity>();
        var spaceByKey = new Dictionary<string, CadEntity>(StringComparer.OrdinalIgnoreCase);
        var openingById = new Dictionary<string, CadEntity>(StringComparer.OrdinalIgnoreCase);

        // Manufacturer folder = outer hull SoT. Interiors nest inside IML (same meter frame).
        // Lock hullLoft is fallback only when manufacturer JSON is missing from the tree.
        if (!TryBuildManufacturerHullMeshes(loa, beam, oah, out var oml, out var iml)
            && !TryBuildHullMeshesFromLockLoft(lockDoc, loa, oah, out oml, out iml))
        {
            throw new InvalidOperationException(
                "Manufacturer CAL-HULL-CAD-001.json OML required — docs/manufacturer is the outer hull.");
        }

        entities.Add(oml);
        if (iml is not null)
            entities.Add(iml);

        // Cargo peek only — never invent a second exterior (no nacelles / blister shells).
        AddInteriorCargoDetails(entities, lockDoc, loa);

        var wallJobs = new List<(CadEntity Space, int Deck, float Up0, float Height)>();
        foreach (var comp in lockDoc.Compartments ?? [])
        {
            foreach (var (deck, up0Raw, up1Raw) in ExpandDecks(comp, lockDoc))
            {
                var key = SpaceKey(comp.Id!, deck);
                var (layer, shape) = Classify(comp.Id!);
                var clipped = ClipCompartmentToIml(comp, up0Raw, up1Raw, shellOnOuter: false);
                var space = MakeSpace(comp.Id!, deck, clipped, loa, layer, shape, shellOnOuter: false, comp);
                entities.Add(space);
                spaceByKey[key] = space;
                wallJobs.Add((space, deck, clipped.Up0, clipped.Up1 - clipped.Up0));
            }
        }

        foreach (var al in lockDoc.Airlocks ?? [])
        {
            var deck = 0;
            var fake = new LockCompartment
            {
                Id = al.Id,
                Z0 = al.Z0,
                Z1 = al.Z1,
                Y0 = al.Y0,
                Y1 = al.Y1,
                Up0 = al.Up0,
                Up1 = al.Up1,
            };
            var clipped = ClipCompartmentToIml(fake, (float)al.Up0, (float)al.Up1, shellOnOuter: true);
            var space = MakeSpace(al.Id!, deck, clipped, loa, LayerWall, ShapeLining, shellOnOuter: true, fake);
            entities.Add(space);
            spaceByKey[SpaceKey(al.Id!, deck)] = space;
            wallJobs.Add((space, deck, clipped.Up0, clipped.Up1 - clipped.Up0));
        }

        CalypsoHullNest.RefineAllSpaceFootprints(entities, loa);
        foreach (var (space, deck, up0, height) in wallJobs)
            AddRoomWalls(entities, space, deck, up0, height, loa);

        foreach (var hatch in lockDoc.Hatches ?? [])
        {
            var deck = hatch.Deck;
            var c = LockToWorld((float)hatch.Y, (float)hatch.Up, (float)hatch.Z, loa);
            var clearW = (float)hatch.ClearW;
            var clearH = (float)hatch.ClearH;
            var isOuterVacuumPreview = string.Equals(hatch.To, "SPACE", StringComparison.OrdinalIgnoreCase)
                                       || string.Equals(hatch.Faces, "outer hull", StringComparison.OrdinalIgnoreCase)
                                       || hatch.Id!.StartsWith("D3-", StringComparison.OrdinalIgnoreCase);

            CadEntity? hostWall = null;
            if (isOuterVacuumPreview)
            {
                hostWall = MakeHatchWall(hatch.Id!, deck, c, clearW, clearH, hatch.Normal);
                entities.Add(hostWall);
            }

            var fp = BuildHatchFootprint(c, clearW, clearH, hatch.Normal);

            var opening = new CadEntity
            {
                Kind = "opening",
                Name = hatch.Id,
                OpeningType = "hatch",
                LayerId = LayerDoor,
                Deck = deck,
                Height = clearH,
                HostWallId = hostWall?.Id,
                Footprint = fp,
                ConnectsSides = ["A", "B"],
            };

            var fromSpace = hatch.From ?? "A";
            var toSpace = hatch.To ?? "B";
            ShipCad.TagOpeningConnects(opening, fromSpace, toSpace);

            var isOuterVacuum = string.Equals(toSpace, "SPACE", StringComparison.OrdinalIgnoreCase)
                               || string.Equals(hatch.Faces, "outer hull", StringComparison.OrdinalIgnoreCase)
                               || hatch.Id!.StartsWith("D3-", StringComparison.OrdinalIgnoreCase);
            if (isOuterVacuum)
            {
                // Vacuum-assisted shell hatch: closed under exterior vacuum.
                ShipCad.TagVacuumAssistedHatch(opening, clearW, clearH, leafState: ShipLeafState.Closed);
            }
            else
            {
                // Standard personnel hatch. Airlock D1/D2 stay Closed (double-barrier);
                // all other schedule hatches Open for walkable circulation.
                var airlockBarrier = hatch.Id!.StartsWith("D1-", StringComparison.OrdinalIgnoreCase)
                                     || hatch.Id.StartsWith("D2-", StringComparison.OrdinalIgnoreCase);
                ShipCad.TagStandardHatch(
                    opening,
                    clearW,
                    clearH,
                    leafState: airlockBarrier ? ShipLeafState.Closed : ShipLeafState.Open);
            }

            entities.Add(opening);
            openingById[hatch.Id!] = opening;
        }

        // Port / starboard L-airlocks: vestibule = chamber A, outer = D3, inner = D1
        foreach (var side in new[] { "port", "stbd" })
        {
            var letter = side[0].ToString().ToUpperInvariant();
            var vestibuleKey = SpaceKey($"AIRLOCK_A_{side}", 0);
            if (!spaceByKey.TryGetValue(vestibuleKey, out var vestibule))
                continue;
            if (!openingById.TryGetValue($"D3-{letter}", out var outer))
                continue;
            if (!openingById.TryGetValue($"D1-{letter}", out var inner))
                continue;
            entities.Add(ShipCad.CreateAirlock($"L-Airlock {side}", vestibule.Id, outer.Id, inner.Id));
        }

        // Hab pressure volume: all non-hold, non-airlock spaces on decks −1/0/+1
        var habMembers = spaceByKey.Values
            .Where(s => s.Name is not null
                        && !s.Name.StartsWith("AIRLOCK", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(s.Name, "HOLD", StringComparison.OrdinalIgnoreCase))
            .Select(s => s.Id)
            .ToList();
        entities.Add(ShipCad.CreatePressureVolume("Habitable", habMembers, "habitable", 101.3f));

        if (spaceByKey.TryGetValue(SpaceKey("HOLD", 0), out var holdSpace)
            || (holdSpace = spaceByKey.Values.FirstOrDefault(s => s.Name == "HOLD")) is not null)
        {
            entities.Add(ShipCad.CreatePressureVolume("Hold", [holdSpace.Id], "cargo", 101.3f));
        }

        AddStructure(entities, lockDoc, loa, beam, oah);
        AddOutfit(entities, lockDoc, loa);
        AddInteriorDetail(entities, loa);
        AddPassagewayHatchFrames(entities, lockDoc, loa);
        IntegrateInteriorHatches(entities, loa);
        ClampAllBoxesInside(entities, loa);

        OpeningDerivation.Apply(entities);

        var cad = new CadDocument
        {
            Name = "Calypso — Rev F (CAL-INT-DK-001 / LOA 69)",
            Generator = new CadGenerator { Name = "CalypsoCad.Lock", Version = "2026.1.0" },
            CreatedAt = stamp,
            ModifiedAt = stamp,
            LayersDocument = "calypso.cadlayers.json",
            ShapesDocument = "calypso.cadshapejson",
            Layers =
            [
                new() { Id = LayerHull, Name = "S-HULL", Color = [0.42f, 0.48f, 0.55f] },
                new() { Id = LayerWall, Name = "A-WALL", Color = [0.45f, 0.55f, 0.7f] },
                new() { Id = LayerDoor, Name = "A-DOOR", Color = [0.7f, 0.55f, 0.3f] },
                new() { Id = LayerCorr, Name = "A-CORR", Color = [0.35f, 0.55f, 0.4f] },
                new() { Id = LayerCargo, Name = "A-ZONE-CARGO", Color = [0.3f, 0.4f, 0.32f] },
                new() { Id = LayerHab, Name = "A-ZONE-HAB", Color = [0.45f, 0.52f, 0.58f] },
                new() { Id = LayerEng, Name = "A-ZONE-ENG", Color = [0.55f, 0.38f, 0.35f] },
                new() { Id = LayerUtil, Name = "A-ZONE-UTIL", Color = [0.42f, 0.44f, 0.55f] },
                new() { Id = LayerBridge, Name = "A-ZONE-BRIDGE", Color = [0.4f, 0.42f, 0.48f] },
                new() { Id = LayerStruct, Name = "S-STRUCT", Color = [0.55f, 0.58f, 0.62f] },
            ],
            Entities = entities,
            Camera = new CadCamera { Distance = 100f, Target = [0f, 6f, 0f], Yaw = 0.85f, Pitch = 0.38f },
        };

        // Exterior vacuum seats every vacuum-class leaf Closed.
        var vacuumClosed = ShipCad.ApplyVacuumSeal(cad, cabinKPa: 101.3f, exteriorKPa: 0f);
        cad.Properties ??= new Dictionary<string, JsonElement>();
        cad.Properties["vacuumSealedHatches"] = JsonSerializer.SerializeToElement(vacuumClosed);

        ShipDocumentMetrics.SetShipEnvelope(cad, loa, beam, oah, DeckSpacing);
        cad.Properties!["canon"] = JsonSerializer.SerializeToElement("CAL-INT-DK-001 Rev F deck plans");
        cad.Properties["source"] = JsonSerializer.SerializeToElement("docs/internals/CAL-INT-GA-001.json");
        cad.Properties["deckDrawing"] = JsonSerializer.SerializeToElement("docs/internals/CAL-INT-DK-001.html");
        cad.Properties["outerHull"] = JsonSerializer.SerializeToElement(
            "docs/manufacturer/CAL-HULL-CAD-001.json OML (scaled to lock envelope if needed)");
        cad.Properties["interiorNest"] = JsonSerializer.SerializeToElement(
            "DK-001 clears / planRings nested inside manufacturer IML");
        cad.Properties["crewCabinCount"] = JsonSerializer.SerializeToElement(
            lockDoc.Stations?.CrewCabinCount ?? 5);
        cad.Properties["cabinClearD"] = JsonSerializer.SerializeToElement(
            lockDoc.Stations?.CabinClearD is > 0 ? lockDoc.Stations.CabinClearD : 7.2);
        cad.Properties["cabinClearW"] = JsonSerializer.SerializeToElement(
            lockDoc.Stations?.CabinClearW is > 0 ? lockDoc.Stations.CabinClearW : 1.92);
        cad.Properties["units"] = JsonSerializer.SerializeToElement("m");
        cad.Properties["reviewDrawingPackage"] = JsonSerializer.SerializeToElement("CAL-DNV-REVIEW-001");
        cad.Properties["classificationIntent"] = JsonSerializer.SerializeToElement(
            "Concept design package for third-party review — not DNV plan approval or rule calculation.");
        cad.Properties["coordinateFrame"] = JsonSerializer.SerializeToElement(
            new Dictionary<string, string>
            {
                ["cad"] = "+X starboard, +Y up, +Z bow (m)",
                ["lock"] = "y starboard, up from keel, zFromStem from stem (m)",
                ["manufacturer"] = "X forward, Y starboard, Z up (m)",
                ["transform"] = "CAD = (y, up, LOA/2 − zFromStem)",
            });

        AssertInteriorsInsideManufacturerHull(entities, loa, beam, oah);

        var spec = PlateMaterialSpec.Aisi316L_8mm;
        var mass = SkinMassRollup.FromFacetAreas(OuterSkinAreaM2, spec);
        var bom = new ShipBom
        {
            Drawing = "CAL-HULL-CAD-001",
            Rev = "B",
            Material = spec,
            SkinMass = mass,
            Lines = [SkinMassRollup.ToBomLine(mass)],
        };
        ShipStructureDocument.Attach(cad, spec, bom, mass);
        return cad;
    }

    private static string SpaceKey(string id, int deck) => $"{id}@{deck}";

    /// <summary>Lock (y starboard, up keel, z from stem) → Cad (+X stbd, +Y up, +Z bow).</summary>
    private static Vector3 LockToWorld(float y, float up, float zFromStem, float loa) =>
        new(y, up, loa * 0.5f - zFromStem);

    internal static Vector3 LockToWorldPublic(float y, float up, float zFromStem, float loa) =>
        LockToWorld(y, up, zFromStem, loa);

    internal static bool InsideHullPublic(float y, float up, float zFromStem, float slack) =>
        InsideHull(y, up, zFromStem, slack);

    internal static float MaxUpInsidePublic(float z, float y, float inset) => MaxUpInside(z, y, inset);

    internal static float MinUpInsidePublic(float z, float y, float inset) => MinUpInside(z, y, inset);

    private static IEnumerable<(int Deck, float Up0, float Up1)> ExpandDecks(LockCompartment comp, CalypsoLockDocument lockDoc)
    {
        var decks = lockDoc.Decks;
        var roomH = decks is null ? 3.2f : (float)decks.RoomH;
        var raw = comp.Deck;
        if (raw is JsonElement je)
        {
            if (je.ValueKind == JsonValueKind.Number)
            {
                var d = je.GetInt32();
                yield return (d, (float)comp.Up0, (float)comp.Up1);
                yield break;
            }

            var s = je.GetString() ?? "";
            if (string.Equals(s, "atrium", StringComparison.OrdinalIgnoreCase)
                || string.Equals(s, "cargo", StringComparison.OrdinalIgnoreCase))
            {
                yield return (0, (float)comp.Up0, (float)comp.Up1);
                yield break;
            }

            if (string.Equals(s, "all", StringComparison.OrdinalIgnoreCase))
            {
                // Replicate clear height on −1 / 0 / +1 using lock deck floors.
                var floors = new (int d, float y)[]
                {
                    (-1, decks is null ? 0.5f : (float)decks.M1),
                    (0, decks is null ? 4f : (float)decks.D0),
                    (1, decks is null ? 8f : (float)decks.D1),
                };
                foreach (var (d, y) in floors)
                    yield return (d, y, y + roomH);
                yield break;
            }
        }

        yield return (0, (float)comp.Up0, (float)comp.Up1);
    }

    private static (Guid Layer, Guid Shape) Classify(string id)
    {
        if (id.StartsWith("CORR", StringComparison.OrdinalIgnoreCase)
            || id is "CROSSING" or "ACCESS" or "STAIRS_P" or "ELEV_S")
            return (LayerCorr, ShapeCorridor);
        if (id is "BRIDGE")
            return (LayerBridge, ShapeBridge);
        if (id is "ENG")
            return (LayerEng, ShapeEng);
        if (id is "HOLD")
            return (LayerCargo, ShapeCargo);
        if (id.StartsWith("CABIN", StringComparison.OrdinalIgnoreCase)
            || id.StartsWith("CREW_", StringComparison.OrdinalIgnoreCase)
            || id.StartsWith("PAX_", StringComparison.OrdinalIgnoreCase)
            || id.StartsWith("VEST_", StringComparison.OrdinalIgnoreCase)
            || id is "CREW" or "INFIRMARY" or "GALLEY" or "LOUNGE" or "STORE_P1")
            return (LayerHab, ShapeHab);
        if (id is "FUEL" or "UTILITY_M1")
            return (LayerUtil, ShapeUtil);
        return (LayerWall, ShapeLining);
    }

    private static CadEntity MakeSpace(
        string id,
        int deck,
        ClippedClear clear,
        float loa,
        Guid layer,
        Guid shape,
        bool shellOnOuter,
        LockCompartment? source = null)
    {
        _ = shellOnOuter;
        var up0 = clear.Up0;
        var up1 = clear.Up1;
        // Prefer CAL-INT-DK-001 planRing (y, zFromStem) when present — exact deck-plan footprint.
        var points = TryFootprintFromPlanRing(source, clear, loa, up0);
        if (points is null
            || (source?.PlanRing is { Count: 4 } && clear.Z0 > 17f))
            points = FootprintPlanRing(clear, loa, up0, yInset: TShell + 0.02f);
        float cx = 0f, cz = 0f;
        foreach (var p in points)
        {
            cx += p[0];
            cz += p[2];
        }

        cx /= points.Count;
        cz /= points.Count;
        var space = new CadEntity
        {
            Kind = "space",
            Name = id,
            LayerId = layer,
            ShapeId = shape,
            Deck = deck,
            Height = up1 - up0,
            Points = points,
            Hooks =
            [
                new CadHook
                {
                    Id = Guid.NewGuid(),
                    Tag = id,
                    Position = [cx, (up0 + up1) * 0.5f, cz],
                },
            ],
        };
        space.Properties ??= new Dictionary<string, JsonElement>();
        if (clear.WasClipped)
            space.Properties["clippedToManufacturerHull"] = JsonSerializer.SerializeToElement(true);
        space.Properties["planRing"] = JsonSerializer.SerializeToElement(source?.PlanRing is { Count: >= 3 });
        space.Properties["drawing"] = JsonSerializer.SerializeToElement("CAL-INT-DK-001");
        return space;
    }

    /// <summary>
    /// Map lock/JSON planRing [y, zFromStem] → Cad floor points, trimmed to the clipped Z band.
    /// </summary>
    private static List<float[]>? TryFootprintFromPlanRing(
        LockCompartment? source,
        ClippedClear clear,
        float loa,
        float up0)
    {
        if (source?.PlanRing is not { Count: >= 3 } ring)
            return null;

        var pts = new List<float[]>(ring.Count);
        foreach (var p in ring)
        {
            if (p is null || p.Length < 2)
                continue;
            var y = (float)p[0];
            var zStem = (float)p[1];
            if (zStem < clear.Z0 - 1e-3f || zStem > clear.Z1 + 1e-3f)
                continue;
            var inset = TShell + WallThickness * 0.5f + 0.02f;
            var limit = MathF.Min(HalfBeamInside(zStem, up0, inset), HalfBeamInside(zStem, clear.Up1, inset));
            var lo = MathF.Max(clear.Y0, -limit);
            var hi = MathF.Min(clear.Y1, limit);
            y = hi >= lo ? Math.Clamp(y, lo, hi) : Math.Clamp(0f, clear.Y0, clear.Y1);
            var w = LockToWorld(y, up0, zStem, loa);
            pts.Add([w.X, up0, w.Z]);
        }

        return pts.Count >= 3 ? pts : null;
    }

    /// <summary>
    /// Hull-clipped plan footprint (y,z) → Cad points at floor. Follows fore taper instead of AABB squares.
    /// </summary>
    private static List<float[]> FootprintPlanRing(ClippedClear clear, float loa, float up0, float yInset, int samples = 16)
    {
        var ring = new List<float[]>(samples * 2 + 2);
        float ClipY(float y, float zStem)
        {
            var inset = yInset + WallThickness * 0.5f;
            var hb = MathF.Min(HalfBeamInside(zStem, up0, inset), HalfBeamInside(zStem, clear.Up1, inset));
            if (hb < 0.1f)
                hb = 0.1f;
            var lo = MathF.Max(clear.Y0, -hb);
            var hi = MathF.Min(clear.Y1, hb);
            return hi >= lo ? Math.Clamp(y, lo, hi) : Math.Clamp(0f, clear.Y0, clear.Y1);
        }

        for (var i = 0; i <= samples; i++)
        {
            var z = clear.Z0 + (clear.Z1 - clear.Z0) * i / samples;
            var w = LockToWorld(ClipY(clear.Y1, z), up0, z, loa);
            ring.Add([w.X, up0, w.Z]);
        }

        for (var i = samples; i >= 0; i--)
        {
            var z = clear.Z0 + (clear.Z1 - clear.Z0) * i / samples;
            var w = LockToWorld(ClipY(clear.Y0, z), up0, z, loa);
            ring.Add([w.X, up0, w.Z]);
        }

        return ring;
    }

    /// <summary>Clear AABB nested inside manufacturer IML (or OML for shell-mounted airlocks).</summary>
    private readonly record struct ClippedClear(float Y0, float Y1, float Up0, float Up1, float Z0, float Z1, bool WasClipped);

    private static ClippedClear ClipCompartmentToIml(LockCompartment comp, float up0, float up1,         bool shellOnOuter)
    {
        _ = shellOnOuter;
        var y0 = (float)comp.Y0;
        var y1 = (float)comp.Y1;
        var z0 = (float)comp.Z0;
        var z1 = (float)comp.Z1;
        (y0, y1, z0, z1) = CalypsoDrafting.CenterOnSpine(y0, y1, z0, z1);
        // Shell-mounted airlocks stay inside the plate. Nothing interior crosses the OML.
        var inset = TShell + 0.02f;
        var zA = MathF.Min(z0, z1);
        var zB = MathF.Max(z0, z1);
        const float step = 0.125f;
        var wantH = MathF.Max(0.5f, up1 - up0);

        // Push the forward face aft until the manufacturer crown clears this deck's floor+height
        // (lock notes like LOUNGE "clipped to hull" — AABB must not poke through the taper).
        float MinZWhereHeightAtLeast(float need)
        {
            for (var z = zA; z <= zB + 1e-4f; z += step)
            {
                if (HullHeightAt(z) - inset + 1e-3f >= need)
                    return z;
            }

            return zB + 1f;
        }

        var zForFull = MinZWhereHeightAtLeast(up0 + wantH);
        if (zForFull <= zB)
            zA = MathF.Max(zA, zForFull);
        else
        {
            var zForFloor = MinZWhereHeightAtLeast(up0 + 0.5f);
            if (zForFloor <= zB)
                zA = MathF.Max(zA, zForFloor);
        }

        var halfBeam = float.PositiveInfinity;
        var height = float.PositiveInfinity;
        var midUp = (up0 + up1) * 0.5f;
        foreach (var z in SampleStations(zA, zB))
        {
            // Octagon athwart limit at floor/mid/ceiling — not rectangular beam×0.5 (corners poke on bow decks).
            var hbRect = HullBeamAt(z) * 0.5f - inset;
            var hbOct = MathF.Min(
                HalfBeamInside(z, up0, inset),
                MathF.Min(HalfBeamInside(z, up1, inset), HalfBeamInside(z, midUp, inset)));
            halfBeam = MathF.Min(halfBeam, MathF.Min(hbRect, hbOct));
            height = MathF.Min(height, HullHeightAt(z) - inset);
        }

        if (!float.IsFinite(halfBeam) || halfBeam < 0.25f)
            halfBeam = 0.25f;
        if (!float.IsFinite(height) || height < inset + 0.5f)
            height = inset + 0.5f;

        var keel = inset;
        var cy0 = MathF.Max(y0, -halfBeam);
        var cy1 = MathF.Min(y1, halfBeam);
        var side = MathF.Max(MathF.Abs(cy0), MathF.Abs(cy1));
        var crown = float.PositiveInfinity;
        var sole = float.NegativeInfinity;
        foreach (var z in SampleStations(zA, zB))
        {
            crown = MathF.Min(crown, MaxUpInside(z, side, inset));
            sole = MathF.Max(sole, MinUpInside(z, side, inset));
        }

        var cup0 = MathF.Max(up0, MathF.Max(keel, sole));
        var cup1 = MathF.Min(up1, MathF.Min(height, crown));
        if (cy1 <= cy0)
        {
            var mid = (y0 + y1) * 0.5f;
            cy0 = MathF.Max(-halfBeam, mid - 0.25f);
            cy1 = MathF.Min(halfBeam, mid + 0.25f);
        }

        if (cup1 - cup0 < 0.45f)
        {
            // Stay under the octagon crown. Do not stretch back out to the rectangular section height.
            var cap = MathF.Min(height, float.IsFinite(crown) ? crown : height);
            var floor = MathF.Max(keel, float.IsFinite(sole) ? sole : keel);
            cup1 = cap;
            cup0 = MathF.Max(floor, MathF.Min(up0, cap - MathF.Min(wantH, MathF.Max(0.5f, cap - floor))));
            if (cup1 - cup0 < 0.45f)
                cup0 = MathF.Max(floor, cup1 - 0.5f);
        }

        var clipped = MathF.Abs(cy0 - y0) > 1e-3f
                      || MathF.Abs(cy1 - y1) > 1e-3f
                      || MathF.Abs(cup0 - up0) > 1e-3f
                      || MathF.Abs(cup1 - up1) > 1e-3f
                      || MathF.Abs(zA - MathF.Min(z0, z1)) > 1e-3f
                      || MathF.Abs(zB - MathF.Max(z0, z1)) > 1e-3f;
        return new ClippedClear(cy0, cy1, cup0, cup1, zA, zB, clipped);
    }

    private static IEnumerable<float> SampleStations(float z0, float z1)
    {
        var a = MathF.Min(z0, z1);
        var b = MathF.Max(z0, z1);
        yield return a;
        yield return (a + b) * 0.5f;
        yield return b;
        foreach (var (z, _, _) in ActiveStations)
        {
            if (z > a && z < b)
                yield return z;
        }
    }

    private static float HullBeamAt(float zFromStem) => SampleLoft(zFromStem, station => station.Beam);

    private static float HullHeightAt(float zFromStem) => SampleLoft(zFromStem, station => station.H);

    private static float SampleLoft(float zFromStem, Func<(float Z, float Beam, float H), float> pick)
    {
        var stations = ActiveStations;
        if (zFromStem <= stations[0].Z)
            return pick(stations[0]);
        if (zFromStem >= stations[^1].Z)
            return pick(stations[^1]);
        for (var i = 0; i < stations.Length - 1; i++)
        {
            var a = stations[i];
            var b = stations[i + 1];
            if (zFromStem >= a.Z && zFromStem <= b.Z)
            {
                var t = (zFromStem - a.Z) / MathF.Max(1e-4f, b.Z - a.Z);
                return pick(a) + t * (pick(b) - pick(a));
            }
        }

        return pick(stations[^1]);
    }

    /// <summary>
    /// Soft assert: every space footprint stays inside manufacturer OML AABB at its stations
    /// (fore taper + midbody). Hard-fail only on gross poke-through.
    /// </summary>
    private static void AssertInteriorsInsideManufacturerHull(List<CadEntity> entities, float loa, float beam, float oah)
    {
        foreach (var space in entities.Where(e => string.Equals(e.Kind, "space", StringComparison.OrdinalIgnoreCase)))
        {
            if (space.Points is not { Count: >= 4 })
                continue;
            foreach (var p in space.Points)
            {
                if (p.Length < 3)
                    continue;
                var y = p[0];
                var upFloor = p[1];
                var upCeil = upFloor + MathF.Max(0f, space.Height);
                var zFromStem = loa * 0.5f - p[2];
                if (!InsideHull(y, upFloor, zFromStem, 0.02f) || !InsideHull(y, upCeil, zFromStem, 0.02f))
                {
                    throw new InvalidOperationException(
                        $"Interior '{space.Name}' poke-through manufacturer hull at y={y:F3} up={upFloor:F3}..{upCeil:F3} zStem={zFromStem:F3}.");
                }
            }
        }

        foreach (var box in entities.Where(e => string.Equals(e.Kind, "box", StringComparison.OrdinalIgnoreCase)))
            AssertBoxInside(box, loa);

        foreach (var wall in entities.Where(e => string.Equals(e.Kind, "wall", StringComparison.OrdinalIgnoreCase)))
            AssertWallInside(wall, loa);

        _ = beam;
        _ = oah;
    }

    private static void AssertBoxInside(CadEntity box, float loa)
    {
        if (box.Points is not { Count: >= 2 })
            return;
        var c = box.Points[0];
        var h = box.Points[1];
        for (var ix = -1; ix <= 1; ix += 2)
        for (var iy = -1; iy <= 1; iy += 2)
        for (var iz = -1; iz <= 1; iz += 2)
        {
            var y = c[0] + ix * h[0];
            var up = c[1] + iy * h[1];
            var zFromStem = loa * 0.5f - (c[2] + iz * h[2]);
            if (!InsideHull(y, up, zFromStem, 0.02f))
            {
                throw new InvalidOperationException(
                    $"Solid '{box.Name}' poke-through manufacturer hull at y={y:F3} up={up:F3} zStem={zFromStem:F3}.");
            }
        }
    }

    private static void AssertWallInside(CadEntity wall, float loa)
    {
        var name = wall.Name ?? "";
        // Vacuum and cargo hatches ride the OML — coaming walls are allowed to meet the plate.
        if (name.StartsWith("hatch-host-", StringComparison.OrdinalIgnoreCase))
            return;
        if (wall.A is not { Length: >= 3 } a0 || wall.B is not { Length: >= 3 } b0)
            return;
        var floor = a0[1];
        var h = MathF.Max(0.5f, wall.Height);
        var halfT = MathF.Max(0.05f, wall.Thickness * 0.5f);
        var ax = a0[0];
        var az = a0[2];
        var bx = b0[0];
        var bz = b0[2];
        var dx = bx - ax;
        var dz = bz - az;
        var len = MathF.Sqrt(dx * dx + dz * dz);
        if (len < 1e-4f)
            return;
        var nx = -dz / len;
        var nz = dx / len;
        foreach (var t in new[] { -halfT, halfT })
        {
            var yA = ax + nx * t;
            var yB = bx + nx * t;
            var zA = az + nz * t;
            var zB = bz + nz * t;
            foreach (var up in new[] { floor, floor + h })
            {
                foreach (var (y, z) in new[] { (yA, zA), (yB, zB) })
                {
                    var zFromStem = loa * 0.5f - z;
                    if (!InsideHull(y, up, zFromStem, 0.015f))
                    {
                        throw new InvalidOperationException(
                            $"Wall '{wall.Name}' poke-through manufacturer hull at y={y:F3} up={up:F3} zStem={zFromStem:F3}.");
                    }
                }
            }
        }
    }

    private const float SectionChamfer = 2.5f;

    internal static void SectionAtPublic(float z, out float halfB, out float halfH, out float mid, out float cx, out float cy) =>
        SectionAt(z, out halfB, out halfH, out mid, out cx, out cy);

    private static void SectionAt(float z, out float halfB, out float halfH, out float mid, out float cx, out float cy)
    {
        var beam = MathF.Max(0.2f, HullBeamAt(z));
        var height = MathF.Max(0.2f, HullHeightAt(z));
        halfB = beam * 0.5f;
        halfH = height * 0.5f;
        mid = height * 0.5f;
        cx = MathF.Min(SectionChamfer * (beam / 20f), halfB * 0.45f);
        cy = MathF.Min(SectionChamfer * (height / 12f), halfH * 0.45f);
    }

    /// <summary>Athwart half-width of the octagon at this height, inside the plate by <paramref name="inset"/>.</summary>
    internal static float HalfBeamInsidePublic(float z, float up, float inset) => HalfBeamInside(z, up, inset);

    private static float HalfBeamInside(float z, float up, float inset)
    {
        SectionAt(z, out var halfB, out var halfH, out var mid, out var cx, out var cy);
        var local = up - mid;
        float half;
        if (local >= halfH - cy)
        {
            var t = Math.Clamp((local - (halfH - cy)) / MathF.Max(cy, 1e-4f), 0f, 1f);
            half = halfB - t * cx;
        }
        else if (local <= -halfH + cy)
        {
            var t = Math.Clamp(((-halfH + cy) - local) / MathF.Max(cy, 1e-4f), 0f, 1f);
            half = halfB - t * cx;
        }
        else
        {
            half = halfB;
        }

        return half - inset;
    }

    private static float MaxUpInside(float z, float y, float inset)
    {
        SectionAt(z, out var halfB, out var halfH, out var mid, out var cx, out var cy);
        return mid + LocalHalf(MathF.Abs(y), halfB, halfH, cx, cy) - inset;
    }

    private static float MinUpInside(float z, float y, float inset)
    {
        SectionAt(z, out var halfB, out var halfH, out var mid, out var cx, out var cy);
        return mid - LocalHalf(MathF.Abs(y), halfB, halfH, cx, cy) + inset;
    }

    private static float LocalHalf(float ay, float halfB, float halfH, float cx, float cy)
    {
        if (ay <= halfB - cx)
            return halfH;
        if (ay >= halfB)
            return halfH - cy;
        var t = (ay - (halfB - cx)) / MathF.Max(cx, 1e-4f);
        return halfH - t * cy;
    }

    private static bool InsideHull(float y, float up, float zFromStem, float slack)
    {
        var zMax = ActiveStations[^1].Z;
        if (zFromStem < -slack || zFromStem > zMax + slack)
            return false;
        if (MathF.Abs(y) > HalfBeamInside(zFromStem, up, 0f) + slack)
            return false;
        if (up > MaxUpInside(zFromStem, y, 0f) + slack)
            return false;
        if (up < MinUpInside(zFromStem, y, 0f) - slack)
            return false;
        return true;
    }

    private static void AddRoomWalls(List<CadEntity> entities, CadEntity space, int deck, float floorY, float height, float loa)
    {
        _ = loa;
        if (space.Points is not { Count: >= 4 } pts)
            return;

        // Centerline sits inboard of the footprint so the slab (thickness centered on the line) stays inside.
        var area = 0f;
        for (var i = 0; i < pts.Count; i++)
        {
            var p = pts[i];
            var q = pts[(i + 1) % pts.Count];
            area += p[0] * q[2] - q[0] * p[2];
        }

        var halfT = WallThickness * 0.5f + 0.02f;
        for (var i = 0; i < pts.Count; i++)
        {
            var a0 = pts[i];
            var b0 = pts[(i + 1) % pts.Count];
            var dx = b0[0] - a0[0];
            var dz = b0[2] - a0[2];
            var len = MathF.Sqrt(dx * dx + dz * dz);
            if (len < 1e-4f)
                continue;
            var nx = (area >= 0f ? -dz : dz) / len;
            var nz = (area >= 0f ? dx : -dx) / len;
            float[] a = [a0[0] + nx * halfT, floorY, a0[2] + nz * halfT];
            float[] b = [b0[0] + nx * halfT, floorY, b0[2] + nz * halfT];
            entities.Add(new CadEntity
            {
                Kind = "wall",
                Name = $"w-{space.Name}-{i}",
                LayerId = LayerWall,
                Deck = deck,
                A = a,
                B = b,
                Thickness = WallThickness,
                Height = height,
                Sides = new CadWallSides
                {
                    A = new CadWallSide { ShapeId = ShapeLining },
                    B = new CadWallSide { ShapeId = ShapeHullInt },
                },
            });
        }
    }

    /// <summary>True when the hatch leaf faces fore/aft (lock ±Z); host wall runs athwart in CAD (+X).</summary>
    private static bool HatchNormalFacesForeAft(string? normal)
    {
        if (string.IsNullOrWhiteSpace(normal))
            return false;
        var n = normal.Trim();
        if (n.Contains("fore", StringComparison.OrdinalIgnoreCase)
            || n.Contains("aft", StringComparison.OrdinalIgnoreCase))
            return true;
        if (n.Contains('Z', StringComparison.OrdinalIgnoreCase)
            && !n.Contains('Y', StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    private static List<float[]> BuildHatchFootprint(Vector3 center, float clearW, float clearH, string? normal)
    {
        var halfW = clearW * 0.5f;
        var halfD = WallThickness * 0.55f;
        var sill = center.Y - clearH * 0.5f;
        Vector3 a, b;
        if (HatchNormalFacesForeAft(normal))
        {
            a = new Vector3(center.X - halfW, sill, center.Z);
            b = new Vector3(center.X + halfW, sill, center.Z);
        }
        else
        {
            a = new Vector3(center.X, sill, center.Z - halfW);
            b = new Vector3(center.X, sill, center.Z + halfW);
        }

        var dir = b - a;
        dir.Y = 0;
        if (dir.LengthSquared() < 1e-6f)
            dir = Vector3.UnitZ;
        else
            dir = Vector3.Normalize(dir);
        var perp = Vector3.Normalize(Vector3.Cross(dir, Vector3.UnitY)) * halfD;
        return
        [
            SvgCoords.ToArray(a - perp),
            SvgCoords.ToArray(b - perp),
            SvgCoords.ToArray(b + perp),
            SvgCoords.ToArray(a + perp),
        ];
    }

    private static CadEntity MakeHatchWall(string id, int deck, Vector3 center, float clearW, float clearH, string? normal)
    {
        Vector3 a, b;
        var half = clearW * 0.5f;
        if (HatchNormalFacesForeAft(normal))
        {
            a = center + new Vector3(-half, 0f, 0f);
            b = center + new Vector3(half, 0f, 0f);
        }
        else
        {
            a = center + new Vector3(0f, 0f, -half);
            b = center + new Vector3(0f, 0f, half);
        }

        a = new Vector3(a.X, center.Y - clearH * 0.5f, a.Z);
        b = new Vector3(b.X, center.Y - clearH * 0.5f, b.Z);
        return new CadEntity
        {
            Kind = "wall",
            Name = $"hatch-host-{id}",
            LayerId = LayerWall,
            Deck = deck,
            A = SvgCoords.ToArray(a),
            B = SvgCoords.ToArray(b),
            Thickness = WallThickness,
            Height = clearH,
            Sides = new CadWallSides
            {
                A = new CadWallSide { ShapeId = ShapeLining },
                B = new CadWallSide { ShapeId = ShapeHullExt },
            },
        };
    }

    /// <summary>Snaps personnel/cargo hatches onto real partition walls so OpeningDerivation cuts walkable gaps.</summary>
    private static void IntegrateInteriorHatches(List<CadEntity> entities, float loa)
    {
        _ = loa;
        var hostsToRemove = new HashSet<Guid>();
        foreach (var opening in entities.Where(e => string.Equals(e.Kind, "opening", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            if (opening.Name?.StartsWith("D3-", StringComparison.OrdinalIgnoreCase) == true)
                continue;
            var connects = ShipCad.GetOpeningConnects(opening);
            if (connects.Count < 2
                || connects.Any(c => string.Equals(c, "SPACE", StringComparison.OrdinalIgnoreCase)))
                continue;

            var center = OpeningFootprintCenter(opening);
            CadEntity? bestWall = null;
            var bestDist = float.MaxValue;
            Vector3 bestA = default;
            Vector3 bestB = default;
            foreach (var wall in entities.Where(w => string.Equals(w.Kind, "wall", StringComparison.OrdinalIgnoreCase)))
            {
                if (wall.Deck != opening.Deck)
                    continue;
                if (wall.Name?.StartsWith("hatch-host-", StringComparison.OrdinalIgnoreCase) == true)
                    continue;
                if (!TryParsePartitionWallSpace(wall.Name, out var spaceName))
                    continue;
                if (!connects.Any(c => string.Equals(c, spaceName, StringComparison.OrdinalIgnoreCase)))
                    continue;
                if (wall.A is not { Length: >= 3 } || wall.B is not { Length: >= 3 })
                    continue;
                var a = SvgCoords.FromArray(wall.A);
                var b = SvgCoords.FromArray(wall.B);
                var d = PointToSegmentDistanceXZ(center, a, b);
                if (d < bestDist)
                {
                    bestDist = d;
                    bestWall = wall;
                    bestA = a;
                    bestB = b;
                }
            }

            if (bestWall is null || bestDist > 2.75f)
                continue;

            var along = OpeningClearWidthAlongWall(opening, bestA, bestB);
            var mid = (bestA + bestB) * 0.5f;
            var dir = bestB - bestA;
            dir.Y = 0;
            var len = dir.Length();
            if (len < 0.2f)
                continue;
            dir /= len;
            var half = Math.Min(len * 0.48f, Math.Max(0.35f, along * 0.5f));
            var sill = MathF.Min(bestA.Y, bestB.Y);
            var pA = new Vector3(mid.X, sill, mid.Z) - dir * half;
            var pB = new Vector3(mid.X, sill, mid.Z) + dir * half;
            var perp = Vector3.Normalize(Vector3.Cross(dir, Vector3.UnitY)) * (WallThickness * 0.55f);
            opening.Footprint =
            [
                SvgCoords.ToArray(pA - perp),
                SvgCoords.ToArray(pB - perp),
                SvgCoords.ToArray(pB + perp),
                SvgCoords.ToArray(pA + perp),
            ];
            opening.HostWallId = bestWall.Id;

            var orphan = entities.FirstOrDefault(e =>
                string.Equals(e.Name, $"hatch-host-{opening.Name}", StringComparison.OrdinalIgnoreCase));
            if (orphan is not null)
                hostsToRemove.Add(orphan.Id);
        }

        if (hostsToRemove.Count > 0)
            entities.RemoveAll(e => hostsToRemove.Contains(e.Id));
    }

    private static void AddPassagewayHatchFrames(List<CadEntity> entities, CalypsoLockDocument lockDoc, float loa)
    {
        foreach (var hatch in lockDoc.Hatches ?? [])
        {
            if (string.Equals(hatch.To, "SPACE", StringComparison.OrdinalIgnoreCase)
                || hatch.Id!.StartsWith("D3-", StringComparison.OrdinalIgnoreCase))
                continue;
            var clearW = (float)hatch.ClearW;
            var clearH = (float)hatch.ClearH;
            var c = LockToWorld((float)hatch.Y, (float)hatch.Up, (float)hatch.Z, loa);
            var sill = c.Y - clearH * 0.5f;
            var frameH = MathF.Min(clearH, 2.15f);
            var jamb = 0.07f;
            if (HatchNormalFacesForeAft(hatch.Normal))
            {
                TryAddBar(entities, loa, $"outfit-hatch-jamb-{hatch.Id}-port", c.X - clearW * 0.5f - jamb, sill + frameH * 0.5f, c.Z, jamb, frameH * 0.5f, jamb);
                TryAddBar(entities, loa, $"outfit-hatch-jamb-{hatch.Id}-stbd", c.X + clearW * 0.5f + jamb, sill + frameH * 0.5f, c.Z, jamb, frameH * 0.5f, jamb);
                TryAddBar(entities, loa, $"outfit-hatch-lintel-{hatch.Id}", c.X, sill + frameH - 0.05f, c.Z, clearW * 0.5f + jamb, 0.05f, jamb);
            }
            else
            {
                TryAddBar(entities, loa, $"outfit-hatch-jamb-{hatch.Id}-fore", c.X, sill + frameH * 0.5f, c.Z - clearW * 0.5f - jamb, jamb, frameH * 0.5f, jamb);
                TryAddBar(entities, loa, $"outfit-hatch-jamb-{hatch.Id}-aft", c.X, sill + frameH * 0.5f, c.Z + clearW * 0.5f + jamb, jamb, frameH * 0.5f, jamb);
                TryAddBar(entities, loa, $"outfit-hatch-lintel-{hatch.Id}", c.X, sill + frameH - 0.05f, c.Z, jamb, 0.05f, clearW * 0.5f + jamb);
            }
        }
    }

    private static bool TryParsePartitionWallSpace(string? wallName, out string spaceName)
    {
        spaceName = "";
        if (wallName is null || !wallName.StartsWith("w-", StringComparison.OrdinalIgnoreCase))
            return false;
        var rest = wallName[2..];
        var dash = rest.LastIndexOf('-');
        if (dash <= 0)
            return false;
        spaceName = rest[..dash];
        return spaceName.Length > 0;
    }

    private static Vector3 OpeningFootprintCenter(CadEntity opening)
    {
        if (opening.Footprint is not { Count: > 0 } fp)
            return Vector3.Zero;
        var sum = Vector3.Zero;
        foreach (var p in fp)
            sum += SvgCoords.FromArray(p);
        return sum / fp.Count;
    }

    private static float OpeningClearWidthAlongWall(CadEntity opening, Vector3 wallA, Vector3 wallB)
    {
        if (opening.Footprint is not { Count: >= 3 } fp)
            return 1f;
        var dir = wallB - wallA;
        dir.Y = 0;
        if (dir.LengthSquared() < 1e-6f)
            return 1f;
        dir = Vector3.Normalize(dir);
        var minT = float.PositiveInfinity;
        var maxT = float.NegativeInfinity;
        foreach (var p in fp)
        {
            var v = SvgCoords.FromArray(p) - wallA;
            v.Y = 0;
            var t = Vector3.Dot(v, dir);
            minT = MathF.Min(minT, t);
            maxT = MathF.Max(maxT, t);
        }

        if (!float.IsFinite(minT) || maxT - minT < 0.3f)
            return MathF.Max(0.9f, opening.Height > 0 ? 1f : 1f);
        return maxT - minT;
    }

    private static float PointToSegmentDistanceXZ(Vector3 p, Vector3 a, Vector3 b)
    {
        var ab = b - a;
        ab.Y = 0;
        var len2 = ab.LengthSquared();
        if (len2 < 1e-8f)
            return Vector2.Distance(new Vector2(p.X, p.Z), new Vector2(a.X, a.Z));
        var t = Math.Clamp(Vector3.Dot(p - a, ab) / len2, 0f, 1f);
        var proj = a + ab * t;
        return Vector2.Distance(new Vector2(p.X, p.Z), new Vector2(proj.X, proj.Z));
    }

    /// <summary>
    /// Manufacturer CAD JSON (docs/manufacturer) is the outer hull. Map PAD AABB into Cad
    /// (+X stbd, +Y up from keel, +Z bow), scaling if manufacturer envelope ≠ lock envelope.
    /// </summary>
    private static bool TryBuildManufacturerHullMeshes(
        float lockLoa,
        float lockBeam,
        float lockOah,
        out CadEntity oml,
        out CadEntity? iml)
    {
        oml = null!;
        iml = null;
        string? path = null;
        foreach (var p in CandidateHullCadPaths())
        {
            if (File.Exists(p))
            {
                path = p;
                break;
            }
        }

        if (path is null)
            return false;

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;
        if (!root.TryGetProperty("points", out var pointsEl) || !root.TryGetProperty("faces", out var facesEl))
            return false;

        var mfgLoa = lockLoa;
        var mfgBeam = lockBeam;
        var mfgOah = lockOah;
        if (root.TryGetProperty("envelope", out var env))
        {
            if (env.TryGetProperty("LOA", out var eLoa))
                mfgLoa = eLoa.GetSingle();
            if (env.TryGetProperty("BEAM", out var eBeam))
                mfgBeam = eBeam.GetSingle();
            if (env.TryGetProperty("OAH", out var eOah))
                mfgOah = eOah.GetSingle();
        }

        var sx = mfgBeam > 1e-3f ? lockBeam / mfgBeam : 1f;
        var sy = mfgOah > 1e-3f ? lockOah / mfgOah : 1f;
        var sz = mfgLoa > 1e-3f ? lockLoa / mfgLoa : 1f;

        var byId = new Dictionary<string, Vector3>(StringComparer.Ordinal);
        foreach (var p in pointsEl.EnumerateArray())
        {
            var id = p.GetProperty("id").GetString();
            if (id is null || id == "O")
                continue;
            var mx = p.GetProperty("x").GetSingle();
            var my = p.GetProperty("y").GetSingle();
            var mz = p.GetProperty("z").GetSingle();
            // Manufacturer (X aft→stem/forward, Y port→stbd, Z keel→crown with PAD)
            // → Cad (+X stbd from CL, +Y up from keel, +Z bow from midship)
            var x = (my - ManufacturerPad - mfgBeam * 0.5f) * sx;
            var y = (mz - ManufacturerPad) * sy;
            var z = ((mx - ManufacturerPad) - mfgLoa * 0.5f) * sz;
            byId[id] = new Vector3(x, y, z);
        }

        if (!TryBuildShellMesh(byId, facesEl, "OML", LayerHull, ShapeHullExt, exterior: true, path, sx, sy, sz, out oml))
            return false;

        if (TryBuildShellMesh(byId, facesEl, "IML", LayerHull, ShapeHullInt, exterior: false, path, sx, sy, sz, out var imlMesh)
            || TryBuildShellMeshFromRings(root, byId, "IML", LayerHull, ShapeHullInt, exterior: false, path, sx, sy, sz, out imlMesh))
            iml = imlMesh;

        return true;
    }

    /// <summary>
    /// Manufacturer JSON lofts OML faces only; IML is rings+points. Loft IML rings so interiors have a nest shell.
    /// </summary>
    private static bool TryBuildShellMeshFromRings(
        JsonElement root,
        Dictionary<string, Vector3> byId,
        string shellName,
        Guid layer,
        Guid shape,
        bool exterior,
        string sourcePath,
        float sx,
        float sy,
        float sz,
        out CadEntity mesh)
    {
        mesh = null!;
        if (!root.TryGetProperty("rings", out var ringsRoot)
            || !ringsRoot.TryGetProperty(shellName, out var ringsEl)
            || ringsEl.ValueKind != JsonValueKind.Array)
            return false;

        var rings = new List<List<string>>();
        foreach (var ring in ringsEl.EnumerateArray())
        {
            if (!ring.TryGetProperty("vertIds", out var vids))
                continue;
            var ids = vids.EnumerateArray().Select(e => e.GetString()).Where(s => s is not null).Cast<string>().ToList();
            if (ids.Count >= 3)
                rings.Add(ids);
        }

        if (rings.Count < 2)
            return false;

        var verts = new List<float[]>();
        var inds = new List<int>();
        var indexOf = new Dictionary<(int, int, int), int>();

        int AddVert(Vector3 v)
        {
            var key = (
                (int)MathF.Round(v.X * 1000f),
                (int)MathF.Round(v.Y * 1000f),
                (int)MathF.Round(v.Z * 1000f));
            if (indexOf.TryGetValue(key, out var existing))
                return existing;
            var i = verts.Count;
            indexOf[key] = i;
            verts.Add([v.X, v.Y, v.Z]);
            return i;
        }

        for (var r = 0; r < rings.Count - 1; r++)
        {
            var a = rings[r];
            var b = rings[r + 1];
            var n = Math.Min(a.Count, b.Count);
            for (var i = 0; i < n; i++)
            {
                var j = (i + 1) % n;
                if (!byId.TryGetValue(a[i], out var a0)
                    || !byId.TryGetValue(a[j], out var a1)
                    || !byId.TryGetValue(b[j], out var b1)
                    || !byId.TryGetValue(b[i], out var b0))
                    continue;
                var i0 = AddVert(a0);
                var i1 = AddVert(a1);
                var i2 = AddVert(b1);
                var i3 = AddVert(b0);
                inds.Add(i0); inds.Add(i1); inds.Add(i2);
                inds.Add(i0); inds.Add(i2); inds.Add(i3);
            }
        }

        if (inds.Count < 3)
            return false;

        mesh = new CadEntity
        {
            Kind = "mesh",
            Name = exterior ? "ext-oml-hull" : "int-iml-hull",
            LayerId = layer,
            ShapeId = shape,
            Color = exterior ? [0.42f, 0.50f, 0.58f] : [0.62f, 0.64f, 0.66f],
            MeshVertices = verts,
            MeshIndices = inds,
            Properties = new Dictionary<string, JsonElement>
            {
                [ShipPropertyKeys.Exterior] = JsonSerializer.SerializeToElement(exterior),
                ["source"] = JsonSerializer.SerializeToElement($"CAL-HULL-CAD-001.json {shellName} rings loft"),
                ["sourcePath"] = JsonSerializer.SerializeToElement(sourcePath),
                ["triangleCount"] = JsonSerializer.SerializeToElement(inds.Count / 3),
                ["scale"] = JsonSerializer.SerializeToElement(new[] { sx, sy, sz }),
                ["role"] = JsonSerializer.SerializeToElement(
                    exterior
                        ? "outer hull (manufacturer OML)"
                        : "inner mold line — interiors nest inside this"),
            },
        };
        return true;
    }

    private static bool TryBuildShellMesh(
        Dictionary<string, Vector3> byId,
        JsonElement facesEl,
        string shellName,
        Guid layer,
        Guid shape,
        bool exterior,
        string sourcePath,
        float sx,
        float sy,
        float sz,
        out CadEntity mesh)
    {
        mesh = null!;
        var verts = new List<float[]>();
        var inds = new List<int>();
        var indexOf = new Dictionary<(int, int, int), int>();

        int AddVert(Vector3 v)
        {
            var key = (
                (int)MathF.Round(v.X * 1000f),
                (int)MathF.Round(v.Y * 1000f),
                (int)MathF.Round(v.Z * 1000f));
            if (indexOf.TryGetValue(key, out var existing))
                return existing;
            var i = verts.Count;
            indexOf[key] = i;
            verts.Add([v.X, v.Y, v.Z]);
            return i;
        }

        foreach (var face in facesEl.EnumerateArray())
        {
            if (!face.TryGetProperty("shell", out var shell) ||
                !string.Equals(shell.GetString(), shellName, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!face.TryGetProperty("verts", out var vids))
                continue;
            var ids = vids.EnumerateArray().Select(e => e.GetString()).Where(s => s is not null).Cast<string>().ToList();
            if (ids.Count < 3)
                continue;
            var poly = new List<Vector3>();
            foreach (var id in ids)
            {
                if (!byId.TryGetValue(id, out var v))
                {
                    poly.Clear();
                    break;
                }

                poly.Add(v);
            }

            if (poly.Count < 3)
                continue;

            var i0 = AddVert(poly[0]);
            for (var i = 1; i + 1 < poly.Count; i++)
            {
                inds.Add(i0);
                inds.Add(AddVert(poly[i]));
                inds.Add(AddVert(poly[i + 1]));
            }
        }

        if (inds.Count < 3)
            return false;

        var name = exterior ? "ext-oml-hull" : "int-iml-hull";
        mesh = new CadEntity
        {
            Kind = "mesh",
            Name = name,
            LayerId = layer,
            ShapeId = shape,
            Color = exterior ? [0.42f, 0.50f, 0.58f] : [0.62f, 0.64f, 0.66f],
            MeshVertices = verts,
            MeshIndices = inds,
            Properties = new Dictionary<string, JsonElement>
            {
                [ShipPropertyKeys.Exterior] = JsonSerializer.SerializeToElement(exterior),
                ["source"] = JsonSerializer.SerializeToElement($"CAL-HULL-CAD-001.json {shellName} faces"),
                ["sourcePath"] = JsonSerializer.SerializeToElement(sourcePath),
                ["triangleCount"] = JsonSerializer.SerializeToElement(inds.Count / 3),
                ["scale"] = JsonSerializer.SerializeToElement(new[] { sx, sy, sz }),
                ["role"] = JsonSerializer.SerializeToElement(
                    exterior
                        ? "outer hull (manufacturer OML)"
                        : "inner mold line — interiors nest inside this"),
            },
        };
        return true;
    }

    private static IEnumerable<string> CandidateHullCadPaths()
    {
        yield return Path.Combine(AppContext.BaseDirectory, "lock", "CAL-HULL-CAD-001.json");
        var proj = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
        yield return Path.Combine(proj, "docs", "manufacturer", "CAL-HULL-CAD-001.json");
        yield return Path.Combine(
            @"d:\novolis\novolis-lab\labs\cad\CalypsoCad\docs\manufacturer",
            "CAL-HULL-CAD-001.json");
    }

    /// <summary>Faceted octagon loft from lock hullLoft stations (self-contained when manufacturer JSON absent).</summary>
    private static bool TryBuildHullMeshesFromLockLoft(
        CalypsoLockDocument lockDoc,
        float loa,
        float oah,
        out CadEntity oml,
        out CadEntity? iml)
    {
        oml = null!;
        iml = null;
        var stations = lockDoc.HullLoft?.Stations;
        if (stations is not { Count: >= 2 })
            return false;

        oml = BuildLoftMeshFromStations(stations, loa, oah, inset: 0f, "ext-oml-hull", ShapeHullExt, exterior: true);
        iml = BuildLoftMeshFromStations(stations, loa, oah, inset: TShell, "int-iml-hull", ShapeHullInt, exterior: false);
        return true;
    }

    private static CadEntity BuildLoftMeshFromStations(
        List<LockHullStation> stations,
        float loa,
        float oah,
        float inset,
        string name,
        Guid shape,
        bool exterior)
    {
        var rings = new List<List<Vector3>>(stations.Count);
        foreach (var st in stations)
        {
            var hb = MathF.Max(0.15f, (float)st.HalfBeam - inset);
            var hh = MathF.Max(0.15f, (float)st.HalfHeight - inset);
            var z = (float)st.ZFromStem;
            var locals = st.Verts is { Count: >= 8 }
                ? st.Verts.Select(v => ScaleVertTowardCenter(v, (float)st.HalfBeam, (float)st.HalfHeight, inset, oah)).ToList()
                : OctagonVerts(hb, hh, oah);
            rings.Add(locals.Select(v => LockToWorld(v.X, v.Y, z, loa)).ToList());
        }

        var verts = new List<float[]>();
        var inds = new List<int>();
        var indexOf = new Dictionary<(int, int, int), int>();

        int AddVert(Vector3 v)
        {
            var key = (
                (int)MathF.Round(v.X * 1000f),
                (int)MathF.Round(v.Y * 1000f),
                (int)MathF.Round(v.Z * 1000f));
            if (indexOf.TryGetValue(key, out var existing))
                return existing;
            var i = verts.Count;
            indexOf[key] = i;
            verts.Add([v.X, v.Y, v.Z]);
            return i;
        }

        for (var r = 0; r + 1 < rings.Count; r++)
        {
            var a = rings[r];
            var b = rings[r + 1];
            var n = Math.Min(a.Count, b.Count);
            for (var i = 0; i < n; i++)
            {
                var i1 = (i + 1) % n;
                var a0 = AddVert(a[i]);
                var a1 = AddVert(a[i1]);
                var b0 = AddVert(b[i]);
                var b1 = AddVert(b[i1]);
                inds.Add(a0);
                inds.Add(b0);
                inds.Add(a1);
                inds.Add(a1);
                inds.Add(b0);
                inds.Add(b1);
            }
        }

        // Stem + aft caps (fan)
        void Cap(List<Vector3> ring, bool reverse)
        {
            if (ring.Count < 3)
                return;
            var i0 = AddVert(ring[0]);
            for (var i = 1; i + 1 < ring.Count; i++)
            {
                var ia = AddVert(ring[i]);
                var ib = AddVert(ring[i + 1]);
                if (reverse)
                {
                    inds.Add(i0);
                    inds.Add(ib);
                    inds.Add(ia);
                }
                else
                {
                    inds.Add(i0);
                    inds.Add(ia);
                    inds.Add(ib);
                }
            }
        }

        Cap(rings[0], reverse: true);
        Cap(rings[^1], reverse: false);

        return new CadEntity
        {
            Kind = "mesh",
            Name = name,
            LayerId = LayerHull,
            ShapeId = shape,
            Color = exterior ? [0.42f, 0.50f, 0.58f] : [0.62f, 0.64f, 0.66f],
            MeshVertices = verts,
            MeshIndices = inds,
            Properties = new Dictionary<string, JsonElement>
            {
                [ShipPropertyKeys.Exterior] = JsonSerializer.SerializeToElement(exterior),
                ["source"] = JsonSerializer.SerializeToElement("CAL-INT-GA-001.json hullLoft"),
                ["triangleCount"] = JsonSerializer.SerializeToElement(inds.Count / 3),
            },
        };
    }

    private static Vector2 ScaleVertTowardCenter(LockHullVert v, float halfBeam, float halfHeight, float inset, float oah)
    {
        var mid = oah * 0.5f;
        var sx = halfBeam > 1e-3f ? MathF.Max(0.15f, halfBeam - inset) / halfBeam : 1f;
        var sy = halfHeight > 1e-3f ? MathF.Max(0.15f, halfHeight - inset) / halfHeight : 1f;
        return new Vector2((float)v.Y * sx, mid + ((float)v.Up - mid) * sy);
    }

    private static List<Vector2> OctagonVerts(float halfBeam, float halfHeight, float oah)
    {
        const float chamfer = 2.5f;
        var cx = MathF.Min(chamfer * (halfBeam * 2f / 20f), halfBeam * 0.45f);
        var cy = MathF.Min(chamfer * (halfHeight * 2f / 12f), halfHeight * 0.45f);
        var mid = oah * 0.5f;
        return
        [
            new(-halfBeam + cx, mid + halfHeight),
            new(halfBeam - cx, mid + halfHeight),
            new(halfBeam, mid + halfHeight - cy),
            new(halfBeam, mid - halfHeight + cy),
            new(halfBeam - cx, mid - halfHeight),
            new(-halfBeam + cx, mid - halfHeight),
            new(-halfBeam, mid - halfHeight + cy),
            new(-halfBeam, mid + halfHeight - cy),
        ];
    }

    public static string DescribeLock()
    {
        var doc = LoadLock();
        var env = doc.Envelope;
        string N(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
        var lines = new List<string>
        {
            $"LOA={N(env?.Loa ?? 0)} BEAM={N(env?.Beam ?? 0)} OAH={N(env?.Oah ?? 0)}",
            $"decks m1={N(doc.Decks?.M1 ?? 0)} d0={N(doc.Decks?.D0 ?? 0)} d1={N(doc.Decks?.D1 ?? 0)} roomH={N(doc.Decks?.RoomH ?? 0)}",
            "compartments=" + string.Join(",", (doc.Compartments ?? []).Select(c => c.Id).Concat((doc.Airlocks ?? []).Select(a => a.Id))),
            "hatches=" + string.Join(",", (doc.Hatches ?? []).Select(h => h.Id)),
            "frames=" + string.Join(",", doc.Frames?.StationIds ?? doc.HullLoft?.Stations?.Select(s => s.Id) ?? []),
            "outfit=" + string.Join(",", (doc.Outfit ?? []).Select(o => $"{o.Compartment}:{o.Role}")),
        };
        var hold = doc.Hold;
        if (hold is not null)
            lines.Add($"door={N(hold.DoorW)}x{N(hold.DoorH)} sill={N(hold.Sill)}");
        return string.Join(Environment.NewLine, lines);
    }

    private static void UseLoftStations(CalypsoLockDocument lockDoc)
    {
        var stations = lockDoc.HullLoft?.Stations;
        if (stations is not { Count: >= 2 })
        {
            ActiveStations = ForeStations;
            return;
        }

        ActiveStations = stations
            .OrderBy(s => s.ZFromStem)
            .Select(s => ((float)s.ZFromStem, (float)(s.HalfBeam * 2.0), (float)(s.HalfHeight * 2.0)))
            .ToArray();
    }

    private static void AddStructure(List<CadEntity> entities, CalypsoLockDocument lockDoc, float loa, float beam, float oah)
    {
        _ = beam;
        _ = oah;
        var web = lockDoc.Frames?.WebM is > 0 ? (float)lockDoc.Frames.WebM : 0.15f;
        var depth = lockDoc.Frames?.DepthM is > 0 ? (float)lockDoc.Frames.DepthM : 0.25f;
        var wanted = lockDoc.Frames?.StationIds;
        var stations = lockDoc.HullLoft?.Stations ?? [];
        const float frameBay = 4f;
        var halfDepthZ = web * 0.5f;
        var deckUps = DeckStructureElevations(lockDoc);

        var zMax = ActiveStations[^1].Z;
        var skin = TShell + 0.05f;

        void PlaceFrame(string id, float zStem, bool bowAligned = false)
        {
            var z = Math.Clamp(zStem, web * 0.5f + skin, zMax - web * 0.5f - skin);
            SectionAt(z, out var halfB, out var halfH, out _, out _, out _);
            if (halfB < 0.5f || halfH < 0.5f)
                return;
            var frameName = id.StartsWith("frame-", StringComparison.OrdinalIgnoreCase) ? id : $"frame-{id}";
            var gaps = CollectAthwartPassageGaps(lockDoc, z, margin: 0.28f);
            CalypsoShipbuildingFrames.AddTransverseFrameWeb(
                entities, loa, frameName, z, web, halfDepthZ, gaps, bowAligned);
            CalypsoShipbuildingFrames.AddTransverseDeckBeams(
                entities, loa, z, frameName, web, halfDepthZ, deckUps, gaps);
        }

        var loftZs = stations
            .Where(s => wanted is null or { Count: 0 } || wanted.Contains(s.Id ?? "", StringComparer.OrdinalIgnoreCase))
            .Select(s => (s.Id ?? "st", (float)s.ZFromStem));
        foreach (var (id, zStem) in CalypsoDrafting.BuildFrameSchedule(loftZs, loa, frameBay))
        {
            var bow = zStem < 18f;
            PlaceFrame(id, zStem, bow);
        }

        var zStart = 17.4f;
        var zEnd = loa - TShell - 0.2f;
        CalypsoShipbuildingFrames.AddDeckLongitudinalGirders(
            entities, loa, zStart, zEnd, web, frameBay, deckUps);

        var doorW = lockDoc.Hold?.DoorW is > 0 ? (float)lockDoc.Hold.DoorW : 14f;
        var doorH = lockDoc.Hold?.DoorH is > 0 ? (float)lockDoc.Hold.DoorH : 8.5f;
        var sill = lockDoc.Hold?.Sill is > 0 ? (float)lockDoc.Hold.Sill : 0.25f;
        var apron = lockDoc.Hold?.ApronD is > 0 ? (float)lockDoc.Hold.ApronD : 12.25f;
        var stern = loa - TShell - web * 0.5f - 0.04f;
        var halfW = doorW * 0.5f;
        TryAddBar(entities, loa, "coaming-aft-sill", 0f, sill + web * 0.5f, stern, halfW, web * 0.5f, web * 0.5f);
        TryAddBar(entities, loa, "coaming-aft-head", 0f, sill + doorH - web * 0.5f, stern, halfW, web * 0.5f, web * 0.5f);
        TryAddBar(entities, loa, "coaming-aft-port", -halfW, sill + doorH * 0.5f, stern, web * 0.5f, doorH * 0.5f - web, web * 0.5f);
        TryAddBar(entities, loa, "coaming-aft-stbd", halfW, sill + doorH * 0.5f, stern, web * 0.5f, doorH * 0.5f - web, web * 0.5f);
        var rampHalf = MathF.Max(0.3f, (apron - 0.2f) * 0.5f);
        var rampZ = stern - web - rampHalf;
        TryAddBar(entities, loa, "ramp-aft", 0f, sill + 0.06f, rampZ, halfW, 0.04f, rampHalf);
    }

    private static List<float> DeckStructureElevations(CalypsoLockDocument lockDoc)
    {
        var m1 = lockDoc.Decks?.M1 is > 0 ? (float)lockDoc.Decks.M1 : 0.5f;
        var d0 = lockDoc.Decks?.D0 is > 0 ? (float)lockDoc.Decks.D0 : 4f;
        var d1 = lockDoc.Decks?.D1 is > 0 ? (float)lockDoc.Decks.D1 : 8f;
        return [m1, d0, d1];
    }

    private static List<(float Y0, float Y1)> CollectAthwartPassageGaps(CalypsoLockDocument lockDoc, float zStem, float margin)
    {
        var gaps = new List<(float Y0, float Y1)>();
        foreach (var comp in lockDoc.Compartments ?? [])
        {
            if (comp.Id is null)
                continue;
            if (comp.Id is not ("CORR_P" or "CORR_S" or "CROSSING" or "STAIRS_P" or "ACCESS" or "ELEV_S"))
                continue;
            var z0 = (float)comp.Z0;
            var z1 = (float)comp.Z1;
            if (zStem < z0 - margin || zStem > z1 + margin)
                continue;
            gaps.Add(((float)comp.Y0 - margin, (float)comp.Y1 + margin));
        }

        var hold = lockDoc.Hold;
        if (hold is not null)
        {
            var hz0 = hold.CatwalkFore > 0 ? (float)hold.CatwalkFore : 49.3f;
            var hz1 = hold.C40Aft > 0 ? (float)hold.C40Aft : 69f;
            if (zStem >= hz0 - margin && zStem <= hz1 + margin)
            {
                var catHalf = (hold.CatwalkD is > 0 ? (float)hold.CatwalkD : 6f) * 0.5f;
                gaps.Add((-catHalf - margin, catHalf + margin));
                if (zStem >= hz1 - 1.5f)
                {
                    var doorHalf = (hold.DoorW is > 0 ? (float)hold.DoorW : 14f) * 0.5f;
                    gaps.Add((-doorHalf - margin, doorHalf + margin));
                }
            }
        }

        var eng = lockDoc.Compartments?.FirstOrDefault(c =>
            string.Equals(c.Id, "ENG", StringComparison.OrdinalIgnoreCase));
        if (eng is not null && zStem >= (float)eng.Z0 - margin && zStem <= (float)eng.Z1 + margin)
        {
            gaps.Add(((float)eng.Y0 - margin, (float)eng.Y1 + margin));
            var corrHalf = lockDoc.Circulation?.CorrW is > 0 ? (float)lockDoc.Circulation.CorrW * 0.5f : 1.5f;
            gaps.Add((-corrHalf - margin, corrHalf + margin));
        }

        foreach (var hatch in lockDoc.Hatches ?? [])
        {
            var hz = (float)hatch.Z;
            if (MathF.Abs(hz - zStem) > 0.45f)
                continue;
            var cw = hatch.ClearW > 0 ? (float)hatch.ClearW : 1f;
            var cy = (float)hatch.Y;
            gaps.Add((cy - cw * 0.55f - margin, cy + cw * 0.55f + margin));
        }

        gaps.Sort((a, b) => a.Y0.CompareTo(b.Y0));
        var merged = new List<(float Y0, float Y1)>();
        foreach (var g in gaps)
        {
            if (merged.Count == 0 || g.Y0 > merged[^1].Y1 + 0.05f)
                merged.Add(g);
            else
                merged[^1] = (merged[^1].Y0, MathF.Max(merged[^1].Y1, g.Y1));
        }

        return merged;
    }

    /// <summary>Intervals of [min,max] minus gap list (gaps use Y0/Y1 as generic bounds).</summary>
    internal static List<(float A, float B)> SolidSpansPublic(
        float min,
        float max,
        IReadOnlyList<(float Y0, float Y1)> gaps) => SolidSpans(min, max, gaps);

    private static List<(float A, float B)> SolidSpans(
        float min,
        float max,
        IReadOnlyList<(float Y0, float Y1)> gaps)
    {
        var spans = new List<(float, float)> { (min, max) };
        foreach (var (g0, g1) in gaps)
        {
            var next = new List<(float, float)>();
            foreach (var (a, b) in spans)
            {
                if (g1 <= a + 1e-4f || g0 >= b - 1e-4f)
                {
                    next.Add((a, b));
                    continue;
                }

                if (g0 > a + 0.06f)
                    next.Add((a, MathF.Min(b, g0)));
                if (g1 < b - 0.06f)
                    next.Add((MathF.Max(a, g1), b));
            }

            spans = next;
        }

        return spans;
    }

    internal static void TryAddStructureBar(
        List<CadEntity> entities,
        float loa,
        string name,
        float y,
        float up,
        float zFromStem,
        float halfY,
        float halfUp,
        float halfZ,
        string? fabPartId = null) =>
        TryAddBar(entities, loa, name, y, up, zFromStem, halfY, halfUp, halfZ, fabPartId);

    private static void TryAddBar(
        List<CadEntity> entities,
        float loa,
        string name,
        float y,
        float up,
        float zFromStem,
        float halfY,
        float halfUp,
        float halfZ,
        string? fabPartId = null)
    {
        var hy = MathF.Abs(halfY);
        var hu = MathF.Abs(halfUp);
        var hz = MathF.Abs(halfZ);
        if (hy < 0.02f || hu < 0.02f || hz < 0.02f)
            return;
        for (var n = 0; n < 10; n++)
        {
            if (BoxFits(y, up, zFromStem, hy, hu, hz))
            {
                AddFrameBar(entities, loa, name, y, up, zFromStem, hy, hu, hz, fabPartId);
                return;
            }

            hy *= 0.8f;
            hu *= 0.8f;
            if (hy < 0.04f || hu < 0.04f)
                return;
        }
    }

    private static void AddFrameBar(
        List<CadEntity> entities,
        float loa,
        string name,
        float y,
        float up,
        float zFromStem,
        float halfY,
        float halfUp,
        float halfZ,
        string? fabPartId = null)
    {
        var center = LockToWorld(y, up, zFromStem, loa);
        entities.Add(MakeSolid(name, LayerStruct, ShapeHullInt, center, new Vector3(MathF.Abs(halfY), MathF.Abs(halfUp), MathF.Abs(halfZ)), [0.55f, 0.58f, 0.62f], "structure", fabPartId));
    }

    private static void AddOutfit(List<CadEntity> entities, CalypsoLockDocument lockDoc, float loa)
    {
        foreach (var item in lockDoc.Outfit ?? [])
        {
            if (string.IsNullOrWhiteSpace(item.Compartment) || string.IsNullOrWhiteSpace(item.Role))
                continue;
            var space = FindSpace(entities, item.Compartment);
            if (space is null || !TrySpaceAabb(space, loa, out var minY, out var maxY, out var floor, out var ceil, out var minStem, out var maxStem))
                continue;
            var y = (minY + maxY) * 0.5f + (float)item.Dx;
            var up = floor + (float)item.Dy;
            var zStem = (minStem + maxStem) * 0.5f + (float)item.Dz;
            var hy = MathF.Max(0.05f, (float)item.Sx * 0.5f);
            var hu = MathF.Max(0.05f, (float)item.Sy * 0.5f);
            var hz = MathF.Max(0.05f, (float)item.Sz * 0.5f);
            PlaceInSpace(entities, loa, space, $"outfit-{item.Compartment}-{item.Role}", y, up, zStem, hy, hu, hz, [0.42f, 0.46f, 0.5f], "outfit");
        }
    }

    /// <summary>DK0 bridge: captain aft-center facing bow, dual pilot consoles forward, side consoles, eight watch seats.</summary>
    private static void AddBridgeOutfit(
        List<CadEntity> entities,
        float loa,
        CadEntity space,
        float floor,
        float ceil,
        float minY,
        float maxY,
        float minStem,
        float maxStem,
        float spanY,
        float spanZ)
    {
        var midY = (minY + maxY) * 0.5f;
        var forwardZ = minStem + spanZ * 0.1f;
        var captainZ = minStem + spanZ * 0.28f;
        var seatRowA = minStem + spanZ * 0.52f;
        var seatRowB = minStem + spanZ * 0.78f;
        float[] console = [0.38f, 0.42f, 0.48f];
        float[] seat = [0.32f, 0.36f, 0.42f];
        float[] glow = [0.45f, 0.72f, 0.82f];

        PlaceInSpace(entities, loa, space, "outfit-BRIDGE-captain-chair", midY, floor + 0.48f, captainZ, 0.34f, 0.42f, 0.34f, seat, "outfit");
        PlaceInSpace(entities, loa, space, "outfit-BRIDGE-pilot-port", minY + spanY * 0.22f, floor + 0.72f, forwardZ, 0.55f, 0.48f, 0.38f, console, "outfit");
        PlaceInSpace(entities, loa, space, "outfit-BRIDGE-pilot-stbd", maxY - spanY * 0.22f, floor + 0.72f, forwardZ, 0.55f, 0.48f, 0.38f, console, "outfit");
        if (!HasNamed(entities, "outfit-BRIDGE-console"))
            PlaceInSpace(entities, loa, space, "outfit-BRIDGE-console", midY, floor + 0.82f, forwardZ + spanZ * 0.04f, 1.15f, 0.52f, 0.42f, console, "outfit");
        PlaceInSpace(entities, loa, space, "outfit-BRIDGE-console-glow", midY, floor + 1.02f, forwardZ + spanZ * 0.04f, 0.9f, 0.06f, 0.35f, glow, "outfit");

        PlaceInSpace(entities, loa, space, "outfit-BRIDGE-side-console-port", minY + 0.35f, floor + 0.88f, captainZ, 0.22f, 0.38f, 0.55f, console, "outfit");
        PlaceInSpace(entities, loa, space, "outfit-BRIDGE-side-console-stbd", maxY - 0.35f, floor + 0.88f, captainZ, 0.22f, 0.38f, 0.55f, console, "outfit");

        var seatY = MathF.Min(0.42f, spanY * 0.18f);
        for (var i = 0; i < 4; i++)
        {
            var z = seatRowA + (seatRowB - seatRowA) * (i / 3f);
            PlaceInSpace(entities, loa, space, $"outfit-BRIDGE-seat-port-{i + 1}", minY + seatY, floor + 0.46f, z, 0.24f, 0.36f, 0.24f, seat, "outfit");
            PlaceInSpace(entities, loa, space, $"outfit-BRIDGE-seat-stbd-{i + 1}", maxY - seatY, floor + 0.46f, z, 0.24f, 0.36f, 0.24f, seat, "outfit");
        }

        PlaceInSpace(entities, loa, space, "outfit-BRIDGE-overhead", midY, ceil - 0.12f, (minStem + maxStem) * 0.5f, MathF.Min(0.8f, spanY * 0.3f), 0.04f, MathF.Min(0.5f, spanZ * 0.25f), [0.4f, 0.42f, 0.46f], "outfit");
    }

    /// <summary>Furnishings the lock lists once per role, repeated across the cabin band and service spaces.</summary>
    private static void AddInteriorDetail(List<CadEntity> entities, float loa)
    {
        foreach (var space in entities.Where(e => string.Equals(e.Kind, "space", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            var name = space.Name ?? "";
            if (!TrySpaceAabb(space, loa, out var minY, out var maxY, out var floor, out var ceil, out var minStem, out var maxStem))
                continue;
            var midY = (minY + maxY) * 0.5f;
            var midZ = (minStem + maxStem) * 0.5f;
            var spanY = maxY - minY;
            var spanZ = maxStem - minStem;
            var prefix = $"outfit-{name}";

            if (name.StartsWith("CREW_", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("PAX_", StringComparison.OrdinalIgnoreCase))
            {
                var berthZ = MathF.Min(0.95f, spanZ * 0.32f);
                var berthY = MathF.Min(0.32f, spanY * 0.28f);
                if (!HasNamed(entities, $"{prefix}-berth"))
                    PlaceInSpace(entities, loa, space, $"{prefix}-berth", minY + berthY + 0.08f, floor + 0.42f, midZ, berthY, 0.16f, berthZ, [0.82f, 0.8f, 0.74f], "outfit");
                PlaceInSpace(entities, loa, space, $"{prefix}-berth-upper", minY + berthY + 0.08f, floor + 1.15f, midZ, berthY, 0.14f, berthZ, [0.78f, 0.76f, 0.7f], "outfit");
                PlaceInSpace(entities, loa, space, $"{prefix}-locker", maxY - 0.28f, floor + 0.85f, maxStem - 0.35f, 0.16f, 0.7f, 0.2f, [0.32f, 0.36f, 0.4f], "outfit");
                PlaceInSpace(entities, loa, space, $"{prefix}-worktop", maxY - 0.34f, floor + 0.72f, minStem + 0.45f, 0.22f, 0.04f, 0.28f, [0.45f, 0.4f, 0.34f], "outfit");
            }
            else if (name.StartsWith("STAIRS", StringComparison.OrdinalIgnoreCase))
            {
                const int treads = 8;
                var alongZ = spanZ >= spanY;
                for (var i = 0; i < treads; i++)
                {
                    var t = (i + 0.5f) / treads;
                    var rise = floor + 0.18f + (ceil - floor - 0.4f) * t;
                    if (alongZ)
                        PlaceInSpace(entities, loa, space, $"{prefix}-tread-{i}", midY, rise, minStem + spanZ * t, MathF.Min(0.45f, spanY * 0.35f), 0.04f, 0.16f, [0.55f, 0.56f, 0.58f], "outfit");
                    else
                        PlaceInSpace(entities, loa, space, $"{prefix}-tread-{i}", minY + spanY * t, rise, midZ, 0.16f, 0.04f, MathF.Min(0.45f, spanZ * 0.35f), [0.55f, 0.56f, 0.58f], "outfit");
                }
            }
            else if (name.StartsWith("CORR_", StringComparison.OrdinalIgnoreCase))
            {
                var walkHalfY = MathF.Min(0.32f, spanY * 0.22f);
                PlaceInSpace(entities, loa, space, $"{prefix}-walk-deck", midY, floor + 0.03f, midZ, walkHalfY, 0.025f, spanZ * 0.46f, [0.38f, 0.4f, 0.44f], "outfit");
                PlaceInSpace(entities, loa, space, $"{prefix}-rail-inboard", minY + 0.1f, floor + 0.88f, midZ, 0.035f, 0.035f, spanZ * 0.44f, [0.58f, 0.6f, 0.64f], "outfit");
                PlaceInSpace(entities, loa, space, $"{prefix}-rail-outboard", maxY - 0.1f, floor + 0.88f, midZ, 0.035f, 0.035f, spanZ * 0.44f, [0.58f, 0.6f, 0.64f], "outfit");
                var count = Math.Max(2, (int)(spanZ / 3.5f));
                for (var i = 0; i < count; i++)
                {
                    var t = (i + 0.5f) / count;
                    PlaceInSpace(entities, loa, space, $"{prefix}-trunk-{i}", midY, ceil - 0.16f, minStem + spanZ * t, 0.09f, 0.035f, MathF.Min(0.7f, spanZ / count * 0.4f), [0.5f, 0.52f, 0.55f], "outfit");
                }
            }
            else if (string.Equals(name, "CROSSING", StringComparison.OrdinalIgnoreCase))
            {
                PlaceInSpace(entities, loa, space, $"{prefix}-deck-plate", midY, floor + 0.03f, midZ, spanY * 0.42f, 0.025f, spanZ * 0.42f, [0.4f, 0.42f, 0.46f], "outfit");
            }
            else if (string.Equals(name, "BRIDGE", StringComparison.OrdinalIgnoreCase) && space.Deck == 0)
            {
                AddBridgeOutfit(entities, loa, space, floor, ceil, minY, maxY, minStem, maxStem, spanY, spanZ);
            }
            else if (string.Equals(name, "ENG", StringComparison.OrdinalIgnoreCase))
            {
                PlaceInSpace(entities, loa, space, $"{prefix}-tank-port", minY + 0.5f, floor + 1.1f, minStem + spanZ * 0.3f, 0.28f, 0.7f, 0.28f, [0.48f, 0.34f, 0.28f], "outfit");
                PlaceInSpace(entities, loa, space, $"{prefix}-tank-stbd", maxY - 0.5f, floor + 1.1f, minStem + spanZ * 0.3f, 0.28f, 0.7f, 0.28f, [0.48f, 0.34f, 0.28f], "outfit");
                PlaceInSpace(entities, loa, space, $"{prefix}-set-aft", midY, floor + 0.7f, maxStem - 0.8f, MathF.Min(0.6f, spanY * 0.25f), 0.45f, 0.35f, [0.4f, 0.42f, 0.44f], "outfit");
            }
            else if (string.Equals(name, "GALLEY", StringComparison.OrdinalIgnoreCase))
            {
                PlaceInSpace(entities, loa, space, $"{prefix}-counter", minY + 0.4f, floor + 0.9f, midZ, 0.28f, 0.04f, MathF.Min(1.4f, spanZ * 0.35f), [0.72f, 0.73f, 0.74f], "outfit");
                PlaceInSpace(entities, loa, space, $"{prefix}-hood", minY + 0.4f, ceil - 0.2f, midZ, 0.32f, 0.08f, MathF.Min(1.1f, spanZ * 0.3f), [0.45f, 0.47f, 0.5f], "outfit");
            }
            else if (string.Equals(name, "INFIRMARY", StringComparison.OrdinalIgnoreCase))
            {
                PlaceInSpace(entities, loa, space, $"{prefix}-cot", midY, floor + 0.5f, midZ, MathF.Min(0.4f, spanY * 0.3f), 0.12f, MathF.Min(0.95f, spanZ * 0.3f), [0.86f, 0.88f, 0.9f], "outfit");
                PlaceInSpace(entities, loa, space, $"{prefix}-cabinet", maxY - 0.3f, floor + 0.9f, midZ, 0.18f, 0.7f, 0.35f, [0.4f, 0.46f, 0.48f], "outfit");
            }
            else if (string.Equals(name, "LOUNGE", StringComparison.OrdinalIgnoreCase))
            {
                if (!HasNamed(entities, "outfit-LOUNGE-bar"))
                    PlaceInSpace(entities, loa, space, $"{prefix}-table", midY, floor + 0.72f, midZ, MathF.Min(0.7f, spanY * 0.25f), 0.04f, MathF.Min(1.2f, spanZ * 0.25f), [0.42f, 0.36f, 0.3f], "outfit");
            }
            else if (string.Equals(name, "HOLD", StringComparison.OrdinalIgnoreCase))
            {
                if (!HasNamed(entities, "outfit-HOLD-gantry"))
                {
                    PlaceInSpace(entities, loa, space, $"{prefix}-rail-port", minY + 1.2f, floor + 5.5f, midZ, 0.08f, 0.08f, MathF.Min(6f, spanZ * 0.35f), [0.62f, 0.5f, 0.22f], "outfit");
                    PlaceInSpace(entities, loa, space, $"{prefix}-rail-stbd", maxY - 1.2f, floor + 5.5f, midZ, 0.08f, 0.08f, MathF.Min(6f, spanZ * 0.35f), [0.62f, 0.5f, 0.22f], "outfit");
                }
            }
            else if (name.StartsWith("AIRLOCK_A_", StringComparison.OrdinalIgnoreCase))
            {
                var leafName = $"outfit-{name}-leaf";
                if (!HasNamed(entities, leafName))
                {
                    PlaceInSpace(entities, loa, space, $"{prefix}-bench", midY, floor + 0.35f, minStem + 0.35f, MathF.Min(0.35f, spanY * 0.25f), 0.12f, MathF.Min(0.5f, spanZ * 0.2f), [0.5f, 0.52f, 0.55f], "outfit");
                    PlaceInSpace(entities, loa, space, $"{prefix}-handhold", maxY - 0.15f, floor + 1.35f, midZ, 0.04f, 0.04f, MathF.Min(0.6f, spanZ * 0.25f), [0.58f, 0.6f, 0.64f], "outfit");
                }
            }
        }
    }

    private static CadEntity? FindSpace(List<CadEntity> entities, string name) =>
        entities.FirstOrDefault(e =>
            string.Equals(e.Kind, "space", StringComparison.OrdinalIgnoreCase)
            && string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));

    private static bool HasNamed(List<CadEntity> entities, string name) =>
        entities.Any(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));

    private static bool TrySpaceAabb(
        CadEntity space,
        float loa,
        out float minY,
        out float maxY,
        out float floor,
        out float ceil,
        out float minStem,
        out float maxStem)
    {
        minY = float.PositiveInfinity;
        maxY = float.NegativeInfinity;
        minStem = float.PositiveInfinity;
        maxStem = float.NegativeInfinity;
        floor = 0f;
        ceil = 0f;
        if (space.Points is not { Count: >= 3 } pts)
            return false;
        floor = pts[0][1];
        foreach (var p in pts)
        {
            minY = MathF.Min(minY, p[0]);
            maxY = MathF.Max(maxY, p[0]);
            var stem = loa * 0.5f - p[2];
            minStem = MathF.Min(minStem, stem);
            maxStem = MathF.Max(maxStem, stem);
        }

        ceil = floor + MathF.Max(0.4f, space.Height);
        return maxY - minY > 0.2f && maxStem - minStem > 0.2f && ceil > floor;
    }

    private static void PlaceInSpace(
        List<CadEntity> entities,
        float loa,
        CadEntity space,
        string name,
        float y,
        float up,
        float zStem,
        float hy,
        float hu,
        float hz,
        float[] color,
        string role)
    {
        if (!TrySpaceAabb(space, loa, out var minY, out var maxY, out var floor, out var ceil, out var minStem, out var maxStem))
            return;
        hy = MathF.Min(MathF.Abs(hy), MathF.Max(0.04f, (maxY - minY) * 0.45f));
        hu = MathF.Min(MathF.Abs(hu), MathF.Max(0.04f, (ceil - floor) * 0.45f));
        hz = MathF.Min(MathF.Abs(hz), MathF.Max(0.04f, (maxStem - minStem) * 0.45f));
        var yLo = minY + hy + 0.02f;
        var yHi = maxY - hy - 0.02f;
        var uLo = floor + hu + 0.02f;
        var uHi = ceil - hu - 0.02f;
        var zLo = minStem + hz + 0.02f;
        var zHi = maxStem - hz - 0.02f;
        if (yHi < yLo || uHi < uLo || zHi < zLo)
            return;
        y = Math.Clamp(y, yLo, yHi);
        up = Math.Clamp(up, uLo, uHi);
        zStem = Math.Clamp(zStem, zLo, zHi);
        for (var n = 0; n < 8 && !BoxFits(y, up, zStem, hy, hu, hz); n++)
        {
            hy *= 0.8f;
            hu *= 0.8f;
            hz *= 0.8f;
            if (hy < 0.04f || hu < 0.04f || hz < 0.04f)
                return;
        }

        if (!BoxFits(y, up, zStem, hy, hu, hz))
            return;
        var center = LockToWorld(y, up, zStem, loa);
        entities.Add(MakeSolid(name, LayerHab, ShapeHab, center, new Vector3(hy, hu, hz), color, role));
    }

    private static bool BoxFits(float y, float up, float zFromStem, float hy, float hu, float hz)
    {
        for (var ix = -1; ix <= 1; ix += 2)
        for (var iy = -1; iy <= 1; iy += 2)
        for (var iz = -1; iz <= 1; iz += 2)
        {
            if (!InsideHull(y + ix * hy, up + iy * hu, zFromStem + iz * hz, -0.005f))
                return false;
        }

        return true;
    }

    private static void ClampAllBoxesInside(List<CadEntity> entities, float loa)
    {
        foreach (var box in entities.Where(e => string.Equals(e.Kind, "box", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            if (box.Points is not { Count: >= 2 })
                continue;
            var c = box.Points[0];
            var h = box.Points[1];
            var y = c[0];
            var up = c[1];
            var zStem = loa * 0.5f - c[2];
            var hy = MathF.Abs(h[0]);
            var hu = MathF.Abs(h[1]);
            var hz = MathF.Abs(h[2]);
            var ok = BoxFits(y, up, zStem, hy, hu, hz);
            for (var n = 0; n < 12 && !ok; n++)
            {
                hy *= 0.82f;
                hu *= 0.82f;
                hz *= 0.82f;
                var mid = HullHeightAt(zStem) * 0.5f;
                y *= 0.8f;
                up = mid + (up - mid) * 0.8f;
                zStem = Math.Clamp(zStem, 0.25f, loa - 0.25f);
                ok = hy >= 0.03f && hu >= 0.03f && hz >= 0.03f && BoxFits(y, up, zStem, hy, hu, hz);
            }

            if (!ok)
            {
                entities.Remove(box);
                continue;
            }

            var w = LockToWorld(y, up, zStem, loa);
            box.Points[0] = [w.X, w.Y, w.Z];
            box.Points[1] = [hy, hu, hz];
            box.Height = hu * 2f;
            box.Thickness = hy * 2f;
        }
    }

    private static CadEntity MakeSolid(
        string name,
        Guid layer,
        Guid shape,
        Vector3 center,
        Vector3 half,
        float[] color,
        string role,
        string? fabPartId = null)
    {
        var props = new Dictionary<string, JsonElement>
        {
            ["interiorOnly"] = JsonSerializer.SerializeToElement(true),
            ["exterior"] = JsonSerializer.SerializeToElement(false),
            ["role"] = JsonSerializer.SerializeToElement(role),
        };
        if (!string.IsNullOrEmpty(fabPartId))
            props[ShipPropertyKeys.FabPartId] = JsonSerializer.SerializeToElement(fabPartId);
        return new CadEntity
        {
            Kind = "box",
            Name = name,
            LayerId = layer,
            ShapeId = shape,
            Color = color,
            Points =
            [
                [center.X, center.Y, center.Z],
                [half.X, half.Y, half.Z],
            ],
            Height = half.Y * 2f,
            Thickness = half.X * 2f,
            Properties = props,
        };
    }

    /// <summary>
    /// Intentionally empty: manufacturer OML is the only outer hull.
    /// Lock exterior.nacelles / airlockBlisters are silhouette hints for drawings — not CAD solids.
    /// </summary>
    private static void AddExteriorFromLock(
        List<CadEntity> entities,
        CalypsoLockDocument lockDoc,
        float loa,
        float beam,
        float oah)
    {
        _ = entities;
        _ = lockDoc;
        _ = loa;
        _ = beam;
        _ = oah;
    }

    /// <summary>C40 stacks in the hold — interior cargo, not a second exterior hull.</summary>
    private static void AddInteriorCargoDetails(List<CadEntity> entities, CalypsoLockDocument lockDoc, float loa)
    {
        var hold = lockDoc.Hold;
        var c40 = lockDoc.Exterior?.C40;
        var c40L = c40?.L is > 0 ? (float)c40.L : 12.192f;
        var c40W = c40?.W is > 0 ? (float)c40.W : 2.438f;
        var c40H = c40?.H is > 0 ? (float)c40.H : 2.591f;
        var cell = c40?.Cell is > 0 ? (float)c40.Cell : 0.2f;
        var cols = c40?.Cols is > 0 ? c40.Cols : 5;
        var tiers = c40?.Tiers is > 0 ? c40.Tiers : 3;
        var gridW = cols * c40W + (cols - 1) * cell;
        var c40Fore = c40?.Fore is > 0
            ? (float)c40.Fore
            : hold?.C40Fore is > 0 ? (float)hold.C40Fore : loa - 1f - c40L;
        var zMid = LockToWorld(0, 0, c40Fore + c40L * 0.5f, loa).Z;
        var left = -gridW * 0.5f;
        for (var col = 0; col < cols; col++)
        {
            var x = left + col * (c40W + cell) + c40W * 0.5f;
            for (var tier = 0; tier < tiers; tier++)
            {
                var y = 1f + c40H * (tier + 0.5f);
                entities.Add(new CadEntity
                {
                    Kind = "box",
                    Name = $"C40-c{col}-t{tier}",
                    LayerId = LayerCargo,
                    ShapeId = ShapeCargo,
                    Color = [0.66f, 0.50f, 0.24f],
                    Points =
                    [
                        [x, y, zMid],
                        [c40W * 0.5f, c40H * 0.5f, c40L * 0.5f],
                    ],
                    Height = c40H,
                    Thickness = c40W,
                    Properties = new Dictionary<string, JsonElement>
                    {
                        // Hold cargo — visible in cutaway/interior, not as a sealed-exterior orange blob.
                        ["interiorOnly"] = JsonSerializer.SerializeToElement(true),
                        ["exterior"] = JsonSerializer.SerializeToElement(false),
                    },
                });
            }
        }
    }

    private sealed class CalypsoLockDocument
    {
        public LockEnvelope? Envelope { get; set; }
        public LockDecks? Decks { get; set; }
        public LockHold? Hold { get; set; }
        public List<LockCompartment>? Compartments { get; set; }
        public List<LockAirlock>? Airlocks { get; set; }
        public List<LockHatch>? Hatches { get; set; }
        public LockStations? Stations { get; set; }
        public LockHullLoft? HullLoft { get; set; }
        public LockExterior? Exterior { get; set; }
        public LockFrames? Frames { get; set; }
        public List<LockOutfitItem>? Outfit { get; set; }
        public LockCirculation? Circulation { get; set; }
    }

    private sealed class LockCirculation
    {
        [JsonPropertyName("CORR_W")]
        public double CorrW { get; set; }
    }

    private sealed class LockFrames
    {
        public double WebM { get; set; }
        public double DepthM { get; set; }
        public int StringerCount { get; set; }
        public List<string>? StationIds { get; set; }
    }

    private sealed class LockOutfitItem
    {
        public string? Compartment { get; set; }
        public string? Role { get; set; }
        public double Dx { get; set; }
        public double Dy { get; set; }
        public double Dz { get; set; }
        public double Sx { get; set; }
        public double Sy { get; set; }
        public double Sz { get; set; }
    }

    private sealed class LockHullLoft
    {
        public List<LockHullStation>? Stations { get; set; }
    }

    private sealed class LockHullStation
    {
        public string? Id { get; set; }
        public double ZFromStem { get; set; }
        public double HalfBeam { get; set; }
        public double HalfHeight { get; set; }
        public List<LockHullVert>? Verts { get; set; }
    }

    private sealed class LockHullVert
    {
        public double Y { get; set; }
        public double Up { get; set; }
    }

    private sealed class LockExterior
    {
        public List<LockNacelle>? Nacelles { get; set; }
        public LockAftDoor? AftDoor { get; set; }
        public List<LockBlister>? AirlockBlisters { get; set; }
        public LockC40? C40 { get; set; }
    }

    private sealed class LockNacelle
    {
        public string? Id { get; set; }
        public double Y { get; set; }
        public double Up { get; set; }
        public double ZFromStem { get; set; }
        public double Radius { get; set; }
        public double Length { get; set; }
    }

    private sealed class LockAftDoor
    {
        public string? Id { get; set; }
        public double Y { get; set; }
        public double Up { get; set; }
        public double ZFromStem { get; set; }
        public double HalfW { get; set; }
        public double HalfH { get; set; }
        public double HalfD { get; set; }
    }

    private sealed class LockBlister
    {
        public string? Id { get; set; }
        public double Y { get; set; }
        public double Up { get; set; }
        public double ZFromStem { get; set; }
        public double HalfY { get; set; }
        public double HalfUp { get; set; }
        public double HalfZ { get; set; }
    }

    private sealed class LockC40
    {
        public double Fore { get; set; }
        public int Cols { get; set; }
        public int Tiers { get; set; }
        public double L { get; set; }
        public double W { get; set; }
        public double H { get; set; }
        public double Cell { get; set; }
    }

    private sealed class LockHold
    {
        [JsonPropertyName("DOOR_W")]
        public double DoorW { get; set; }
        [JsonPropertyName("DOOR_H")]
        public double DoorH { get; set; }
        [JsonPropertyName("SILL")]
        public double Sill { get; set; }
        [JsonPropertyName("C40_FORE")]
        public double C40Fore { get; set; }
        [JsonPropertyName("APRON_D")]
        public double ApronD { get; set; }
        [JsonPropertyName("CATWALK_D")]
        public double CatwalkD { get; set; }
        [JsonPropertyName("C40_AFT")]
        public double C40Aft { get; set; }
        [JsonPropertyName("CATWALK_FORE")]
        public double CatwalkFore { get; set; }
    }

    private sealed class LockEnvelope
    {
        [JsonPropertyName("LOA")]
        public double Loa { get; set; }
        [JsonPropertyName("BEAM")]
        public double Beam { get; set; }
        [JsonPropertyName("OAH")]
        public double Oah { get; set; }
    }

    private sealed class LockDecks
    {
        [JsonPropertyName("m1")]
        public double M1 { get; set; }
        [JsonPropertyName("d0")]
        public double D0 { get; set; }
        [JsonPropertyName("d1")]
        public double D1 { get; set; }
        [JsonPropertyName("roomH")]
        public double RoomH { get; set; }
    }

    private sealed class LockCompartment
    {
        public string? Id { get; set; }
        public JsonElement Deck { get; set; }
        public double Z0 { get; set; }
        public double Z1 { get; set; }
        public double Y0 { get; set; }
        public double Y1 { get; set; }
        public double Up0 { get; set; }
        public double Up1 { get; set; }
        /// <summary>Optional [y, zFromStem] ring from CAL-INT-DK-001 / lock planRing (authoritative footprint).</summary>
        public List<double[]>? PlanRing { get; set; }
    }

    private sealed class LockAirlock
    {
        public string? Id { get; set; }
        public double Z0 { get; set; }
        public double Z1 { get; set; }
        public double Y0 { get; set; }
        public double Y1 { get; set; }
        public double Up0 { get; set; }
        public double Up1 { get; set; }
    }

    private sealed class LockHatch
    {
        public string? Id { get; set; }
        public int Deck { get; set; }
        public double ClearW { get; set; }
        public double ClearH { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double Up { get; set; }
        public string? Normal { get; set; }
        public string? From { get; set; }
        public string? To { get; set; }
        public string? Faces { get; set; }
    }

    private sealed class LockStations
    {
        [JsonPropertyName("CREW_CABIN_COUNT")]
        public int CrewCabinCount { get; set; }
        [JsonPropertyName("CABIN_CLEAR_D")]
        public double CabinClearD { get; set; }
        [JsonPropertyName("CABIN_CLEAR_W")]
        public double CabinClearW { get; set; }
        [JsonPropertyName("CABIN_MODULE_W")]
        public double CabinModuleW { get; set; }
    }
}
