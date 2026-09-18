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
    public const float OuterSkinAreaM2 = 3796.055f;
    public const float ManufacturerPad = 0.5f;

    /// <summary>Fore taper stations — same numbers as docs/internals/calypso-lock.mjs FORE_STATIONS.</summary>
    private static readonly (float Z, float Beam, float H)[] ForeStations =
    [
        (0f, 3.5f, 4f),
        (3.25f, 10f, 7.5f),
        (10f, 17f, 10.5f),
        (17f, 20f, 12f),
    ];

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
                AddRoomWalls(entities, space, deck, clipped.Up0, clipped.Up1 - clipped.Up0, loa);
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
            // Airlock outer face rides the OML; clip only to OML half-extents (not IML inset).
            var clipped = ClipCompartmentToIml(fake, (float)al.Up0, (float)al.Up1, shellOnOuter: true);
            var space = MakeSpace(al.Id!, deck, clipped, loa, LayerWall, ShapeLining, shellOnOuter: true, fake);
            entities.Add(space);
            spaceByKey[SpaceKey(al.Id!, deck)] = space;
            AddRoomWalls(entities, space, deck, clipped.Up0, clipped.Up1 - clipped.Up0, loa);
        }

        foreach (var hatch in lockDoc.Hatches ?? [])
        {
            var deck = hatch.Deck;
            var c = LockToWorld((float)hatch.Y, (float)hatch.Up, (float)hatch.Z, loa);
            var clearW = (float)hatch.ClearW;
            var clearH = (float)hatch.ClearH;
            var wall = MakeHatchWall(hatch.Id!, deck, c, clearW, clearH, hatch.Normal);
            entities.Add(wall);

            var halfW = clearW * 0.5f;
            var halfD = WallThickness;
            List<float[]> fp =
            [
                SvgCoords.ToArray(c + new Vector3(-halfW, 0f, -halfD)),
                SvgCoords.ToArray(c + new Vector3(halfW, 0f, -halfD)),
                SvgCoords.ToArray(c + new Vector3(halfW, 0f, halfD)),
                SvgCoords.ToArray(c + new Vector3(-halfW, 0f, halfD)),
            ];

            var opening = new CadEntity
            {
                Kind = "opening",
                Name = hatch.Id,
                OpeningType = "hatch",
                LayerId = LayerDoor,
                Deck = deck,
                Height = clearH,
                HostWallId = wall.Id,
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
        var up0 = clear.Up0;
        var up1 = clear.Up1;
        // Prefer CAL-INT-DK-001 planRing (y, zFromStem) when present — exact deck-plan footprint.
        var points = TryFootprintFromPlanRing(source, clear, loa, up0)
                     ?? FootprintPlanRing(clear, loa, up0, yInset: shellOnOuter ? 0f : TShell);
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
            // Drop vertices outside the vertical-fit Z band (fore taper trim).
            if (zStem < clear.Z0 - 1e-3f || zStem > clear.Z1 + 1e-3f)
                continue;
            // Keep athwartships clear inside the clipped Y band.
            y = Math.Clamp(y, clear.Y0, clear.Y1);
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
            var hb = HullBeamAt(zStem) * 0.5f - yInset - 0.02f;
            if (hb < 0.1f)
                hb = 0.1f;
            return Math.Clamp(y, -hb, hb);
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

    private static ClippedClear ClipCompartmentToIml(LockCompartment comp, float up0, float up1, bool shellOnOuter)
    {
        var y0 = (float)comp.Y0;
        var y1 = (float)comp.Y1;
        var z0 = (float)comp.Z0;
        var z1 = (float)comp.Z1;
        var inset = shellOnOuter ? 0f : TShell;
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

        if (zB - zA < 0.2f)
        {
            // No usable length under the hull for this clear — collapse to a stub at the aft face.
            zA = MathF.Max(zA, zB - 0.25f);
        }

        var halfBeam = float.PositiveInfinity;
        var height = float.PositiveInfinity;
        foreach (var z in SampleStations(zA, zB))
        {
            halfBeam = MathF.Min(halfBeam, HullBeamAt(z) * 0.5f - inset);
            height = MathF.Min(height, HullHeightAt(z) - inset);
        }

        if (!float.IsFinite(halfBeam) || halfBeam < 0.25f)
            halfBeam = 0.25f;
        if (!float.IsFinite(height) || height < inset + 0.5f)
            height = inset + 0.5f;

        var keel = inset;
        var cy0 = MathF.Max(y0, -halfBeam);
        var cy1 = MathF.Min(y1, halfBeam);
        var cup0 = MathF.Max(up0, keel);
        var cup1 = MathF.Min(up1, height);
        if (cy1 <= cy0)
        {
            var mid = (y0 + y1) * 0.5f;
            cy0 = MathF.Max(-halfBeam, mid - 0.25f);
            cy1 = MathF.Min(halfBeam, mid + 0.25f);
        }

        if (cup1 - cup0 < 0.45f)
        {
            // Prefer a usable clear under the local crown rather than a paper-thin slab.
            cup1 = height;
            cup0 = MathF.Max(keel, MathF.Min(up0, height - MathF.Min(wantH, height - keel)));
            if (cup1 - cup0 < 0.45f)
                cup0 = MathF.Max(keel, cup1 - 0.5f);
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
        foreach (var (z, _, _) in ForeStations)
        {
            if (z > a && z < b)
                yield return z;
        }
    }

    private static float HullBeamAt(float zFromStem)
    {
        if (zFromStem <= ForeStations[0].Z)
            return ForeStations[0].Beam;
        if (zFromStem >= 17f)
            return 20f;
        for (var i = 0; i < ForeStations.Length - 1; i++)
        {
            var a = ForeStations[i];
            var b = ForeStations[i + 1];
            if (zFromStem >= a.Z && zFromStem <= b.Z)
            {
                var t = (zFromStem - a.Z) / (b.Z - a.Z);
                return a.Beam + t * (b.Beam - a.Beam);
            }
        }

        return 20f;
    }

    private static float HullHeightAt(float zFromStem)
    {
        if (zFromStem <= ForeStations[0].Z)
            return ForeStations[0].H;
        if (zFromStem >= 17f)
            return 12f;
        for (var i = 0; i < ForeStations.Length - 1; i++)
        {
            var a = ForeStations[i];
            var b = ForeStations[i + 1];
            if (zFromStem >= a.Z && zFromStem <= b.Z)
            {
                var t = (zFromStem - a.Z) / (b.Z - a.Z);
                return a.H + t * (b.H - a.H);
            }
        }

        return 12f;
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
                var zCad = p[2];
                var zFromStem = loa * 0.5f - zCad;
                var hb = HullBeamAt(zFromStem) * 0.5f + 0.05f; // tiny tolerance for shell airlocks
                var hh = HullHeightAt(zFromStem) + 0.05f;
                if (MathF.Abs(y) > hb + 1e-2f || upFloor < -0.05f || upCeil > hh)
                {
                    throw new InvalidOperationException(
                        $"Interior '{space.Name}' poke-through manufacturer hull at y={y:F3} up={upFloor:F3}..{upCeil:F3} zStem={zFromStem:F3} " +
                        $"(allowed |y|≤{hb:F3}, up≤{hh:F3}). Manufacturer OML is outer; interiors must fit inside.");
                }
            }
        }

        _ = beam;
        _ = oah;
    }

    private static void AddRoomWalls(List<CadEntity> entities, CadEntity space, int deck, float floorY, float height, float loa)
    {
        if (space.Points is not { Count: >= 4 })
            return;
        for (var i = 0; i < space.Points.Count; i++)
        {
            var a = space.Points[i];
            var b = space.Points[(i + 1) % space.Points.Count];
            // Snap wall Y to floor
            a = [a[0], floorY, a[2]];
            b = [b[0], floorY, b[2]];
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

    private static CadEntity MakeHatchWall(string id, int deck, Vector3 center, float clearW, float clearH, string? normal)
    {
        Vector3 a, b;
        var half = clearW * 0.5f;
        // Align wall along the coaming; normal hints which axis the leaf faces.
        if (normal is not null && (normal.Contains('Y', StringComparison.OrdinalIgnoreCase)))
        {
            // Wall along Z (opening faces ±Y / athwartships)
            a = center + new Vector3(0f, 0f, -half);
            b = center + new Vector3(0f, 0f, half);
        }
        else
        {
            // Wall along X (opening faces ±Z / fore-aft)
            a = center + new Vector3(-half, 0f, 0f);
            b = center + new Vector3(half, 0f, 0f);
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
