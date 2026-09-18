using System.Numerics;
using System.Text.Json;
using CalypsoCad.Models;
using CalypsoCad.Services;
using Novolis.Cad.Primitives;

namespace CalypsoCad.Generation;

/// <summary>
/// RevE / RevG Calypso transport (65×20×12 m, hold 22×19×9 m) from calypso-deckplans_revG.svg + Chapter 16.
/// Scale 0.1 m/SVG unit. Decks −1 / 0 / +1.
/// </summary>
internal static class CalypsoRevGGenerator
{
    public const float DeckSpacing = 4f;
    public const float RoomHeight = 3.6f;
    public const float WallThickness = 0.15f;
    public const float CargoHeight = 9f;
    /// <summary>Engineering full OAH from deck −1 floor through +1 crown (~12 m).</summary>
    public const float EngOahHeight = 12f;
    public const float DoorHeight = 2.2f;
    public const float DoorWidth = 1.0f;
    public const float CorridorDoorWidth = 2.0f;
    /// <summary>Canvas / RevE: all passageway doors ≥ 1.0 m clear.</summary>
    public const float PersonnelDoorWidth = 1.0f;
    public const float HSill = 0.15f;
    public const float ClearHeightTypical = 2.05f;

    // HILS-C40 external (ISO 20' class) — 5 athwart × 1 deep × 3 high.
    private const float C40L = 12.192f;
    private const float C40W = 2.438f;
    private const float C40H = 2.591f;
    private const float C40Cell = 0.2f;
    private const float CatwalkDepthM = 3.0f;
    private const float RampGapM = 1.0f;
    private const float RampDepthM = 4.0f;
    private const float RampHeightM = 3.2f;
    private static readonly float C40GridW = 5f * C40W + 4f * C40Cell; // 12.99 m

    // Stations from FP (hull bow SVG Y=78 → STN 0).
    private const float FpSvgY = 78f;
    private const float StnEngBh = 38.8f;
    private const float StnHoldBh = 47.0f;

    // Engineering footprint SVG (112,466 96×82) — fore face = WT-BH @ STN 38.8.
    private const float EngSvgX = 112f;
    private const float EngSvgY = 466f;
    private const float EngSvgW = 96f;
    private const float EngSvgH = 82f;

    private static readonly Guid ShapeHullExt = Guid.Parse("e0000000-0000-4000-8000-000000000001");
    private static readonly Guid ShapeHullInt = Guid.Parse("e0000000-0000-4000-8000-000000000002");
    private static readonly Guid ShapeCorridor = Guid.Parse("e0000000-0000-4000-8000-000000000003");
    private static readonly Guid ShapeCargo = Guid.Parse("e0000000-0000-4000-8000-000000000004");
    private static readonly Guid ShapeHab = Guid.Parse("e0000000-0000-4000-8000-000000000005");
    private static readonly Guid ShapeEng = Guid.Parse("e0000000-0000-4000-8000-000000000006");
    private static readonly Guid ShapeUtil = Guid.Parse("e0000000-0000-4000-8000-000000000007");
    private static readonly Guid ShapeBridge = Guid.Parse("e0000000-0000-4000-8000-000000000008");
    private static readonly Guid ShapeLining = Guid.Parse("e0000000-0000-4000-8000-000000000009");
    private static readonly Guid ShapeNacelle = Guid.Parse("e0000000-0000-4000-8000-00000000000a");

    private static readonly Guid LayerHull = Guid.Parse("d1000000-0000-4000-8000-000000000001");
    private static readonly Guid LayerWall = Guid.Parse("d1000000-0000-4000-8000-000000000002");
    private static readonly Guid LayerDoor = Guid.Parse("d1000000-0000-4000-8000-000000000003");
    private static readonly Guid LayerCorr = Guid.Parse("d1000000-0000-4000-8000-000000000004");
    private static readonly Guid LayerCargo = Guid.Parse("d1000000-0000-4000-8000-000000000005");
    private static readonly Guid LayerHab = Guid.Parse("d1000000-0000-4000-8000-000000000006");
    private static readonly Guid LayerEng = Guid.Parse("d1000000-0000-4000-8000-000000000007");
    private static readonly Guid LayerUtil = Guid.Parse("d1000000-0000-4000-8000-000000000008");
    private static readonly Guid LayerBridge = Guid.Parse("d1000000-0000-4000-8000-000000000009");

    // Cabin band SVG: 112,328 size 96×72 → five ~19.2-wide cabins.
    private const float CabinBandX = 112f;
    private const float CabinBandY = 328f;
    private const float CabinBandW = 96f;
    private const float CabinBandH = 72f;
    private const int CrewCabinCount = 5;
    private const int BerthCount = 10;

    // Cargo footprint: STN 47 → AP (hull stern SVG Y≈728). Claimed 22×19×9 clipped to LOA ≈18×19×9.
    private const float CargoSvgX = 65f;   // CL 160 ± 9.5 m
    private const float CargoSvgY = 548f;  // STN 47
    private const float CargoSvgW = 190f;  // 19.0 m beam
    private const float CargoSvgH = 180f;  // 18.0 m (STN 47→65)

    public static string DefaultOutputDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Novolis", "CalypsoCad", "generated");

    public static string Generate(string? outputDirectory = null) =>
        CalypsoLockGenerator.Generate(outputDirectory);

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
            Cat(Guid.Parse("d1000000-0000-4000-8000-00000000000a"), "A-ANNO", "A", "ANNO", "Labels", [0.79f, 0.82f, 0.85f]),
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
        Name = "Calypso Rev G shapes",
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
            Shape(ShapeNacelle, "nacelle-steel", [0.38f, 0.42f, 0.48f], "nacelle-steel", 0.35f, 0.7f),
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

    private static CadDocument BuildCad(string stamp)
    {
        var entities = new List<CadEntity>();

        AddHull(entities, -1);
        AddHull(entities, 0);
        AddHull(entities, 1);
        // Exterior hull/pods/meshes are authored in Draft Studio / Cad Studio and saved into .cadjson —
        // not emitted here. Regenerate preserves entities tagged exterior (see Generate).

        AddCirculationShafts(entities);
        AddDeckMinus1(entities);
        AddDeck0(entities);
        AddDeckPlus1(entities);

        AddEngineeringSystem(entities);
        AddCargoSystem(entities);
        AddVestibules(entities);
        AddShipOpenings(entities);
        AddArrayMetadata(entities);

        OpeningDerivation.Apply(entities);

        return new CadDocument
        {
            Name = "Calypso — RevE / RevG",
            Generator = new CadGenerator { Name = "CalypsoCad", Version = "2026.1.0" },
            CreatedAt = stamp,
            ModifiedAt = stamp,
            LayersDocument = "calypso.cadlayers.json",
            ShapesDocument = "calypso.cadshapejson",
            Layers =
            [
                DocLayer(LayerHull, "S-HULL", [0.42f, 0.48f, 0.55f]),
                DocLayer(LayerWall, "A-WALL", [0.45f, 0.55f, 0.7f]),
                DocLayer(LayerDoor, "A-DOOR", [0.7f, 0.55f, 0.3f]),
                DocLayer(LayerCorr, "A-CORR", [0.35f, 0.55f, 0.4f]),
                DocLayer(LayerCargo, "A-ZONE-CARGO", [0.3f, 0.4f, 0.32f]),
                DocLayer(LayerHab, "A-ZONE-HAB", [0.45f, 0.52f, 0.58f]),
                DocLayer(LayerEng, "A-ZONE-ENG", [0.55f, 0.38f, 0.35f]),
                DocLayer(LayerUtil, "A-ZONE-UTIL", [0.42f, 0.44f, 0.55f]),
                DocLayer(LayerBridge, "A-ZONE-BRIDGE", [0.4f, 0.42f, 0.48f]),
            ],
            Entities = entities,
            Camera = new CadCamera { Distance = 95f, Target = [0f, 4f, 0f], Yaw = 0.85f, Pitch = 0.38f },
            Properties = new Dictionary<string, JsonElement>
            {
                ["deckSpacingMeters"] = JsonSerializer.SerializeToElement(DeckSpacing),
                ["shipLoaMeters"] = JsonSerializer.SerializeToElement(65),
                ["beamMeters"] = JsonSerializer.SerializeToElement(20),
                ["heightMeters"] = JsonSerializer.SerializeToElement(12),
                ["cargoHoldMeters"] = JsonSerializer.SerializeToElement(new[] { 22, 19, 9 }),
                ["engineeringOahMeters"] = JsonSerializer.SerializeToElement(EngOahHeight),
                ["wtBhEngStationMeters"] = JsonSerializer.SerializeToElement(StnEngBh),
                ["wtBhHoldStationMeters"] = JsonSerializer.SerializeToElement(StnHoldBh),
                ["registry"] = JsonSerializer.SerializeToElement("ST-7749-63325116"),
                ["canon"] = JsonSerializer.SerializeToElement("RevE/RevG + Chapter 16"),
                ["source"] = JsonSerializer.SerializeToElement("calypso-deckplans_revG.svg"),
                ["arrangement"] = JsonSerializer.SerializeToElement(
                    "hab stack −1/0/+1 fwd-mid; eng full OAH; hold continuous 9 m void aft"),
            },
        };
    }

    private static CadLayer DocLayer(Guid id, string name, float[] color) =>
        new() { Id = id, Name = name, Color = color };

    private static float DeckY(int deck) => (deck + 1) * DeckSpacing;

    private static void AddHull(List<CadEntity> entities, int deck)
    {
        float[][] svg =
        [
            [160, 78], [212, 102], [248, 152], [258, 222], [258, 688], [254, 706], [242, 728],
            [78, 728], [66, 706], [62, 688], [62, 222], [72, 152], [108, 102],
        ];
        var y = DeckY(deck);
        var pts = svg.Select(p => SvgCoords.ToArray(SvgCoords.ToWorld(p[0], p[1], y))).ToList();
        for (var i = 0; i < pts.Count; i++)
        {
            entities.Add(Wall($"hull-{deck}-{i}", LayerHull, deck, pts[i], pts[(i + 1) % pts.Count], ShapeHullExt, ShapeHullInt));
        }
    }

    private static CadEntity CloneEntity(CadEntity src)
    {
        // Round-trip clone so regenerate cannot alias previous document instances.
        var json = JsonSerializer.Serialize(src, CadJson.Options);
        return JsonSerializer.Deserialize<CadEntity>(json, CadJson.Options)
               ?? throw new InvalidOperationException("Failed to clone preserved exterior entity");
    }

    private static void AddCirculationShafts(List<CadEntity> entities)
    {
        foreach (var deck in new[] { -1, 0, 1 })
        {
            var stairsHooks = deck == 0
                ? Hooks("StairsCore", 116 + 17, 258 + 19, deck)
                : null;
            var elevHooks = deck == 0
                ? Hooks("ElevCore", 170 + 17, 258 + 19, deck)
                : null;

            AddRoom(entities, "Stairs", LayerWall, ShapeLining, deck, 116, 258, 34, 38, stairsHooks);
            AddRoom(entities, "Elev", LayerWall, ShapeLining, deck, 170, 258, 34, 38, elevHooks);
        }
    }

    private static void AddDeckMinus1(List<CadEntity> entities)
    {
        const int d = -1;
        AddCorridors(entities, d);
        AddRoom(entities, "Reactor Service", LayerUtil, ShapeUtil, d, 112, 104, 96, 144);
        AddRoom(entities, "Power", LayerUtil, ShapeUtil, d, 112, 328, 48, 72);
        AddRoom(entities, "Life", LayerUtil, ShapeUtil, d, 160, 328, 48, 72);
        AddRoom(entities, "Water", LayerUtil, ShapeUtil, d, 112, 400, 48, 66);
        AddRoom(entities, "Ballast", LayerUtil, ShapeUtil, d, 160, 400, 48, 66);
        // Engineering is full-OAH continuous void — see AddEngineeringSystem.
    }

    private static void AddDeck0(List<CadEntity> entities)
    {
        const int d = 0;
        AddCorridors(entities, d);
        AddRoom(entities, "Bridge Access", LayerCorr, ShapeCorridor, d, 150, 252, 20, 56);

        var bridgePts = BridgeFootprint();
        var bridgeCenter = Centroid(bridgePts);
        AddSpace(entities, "Bridge", LayerBridge, ShapeBridge, d, RoomHeight, bridgePts, hooks:
        [
            Hook("Bridge", bridgeCenter),
            Hook("OwnerLock", bridgeCenter + new Vector3(0f, 0f, -1.2f)),
            Hook("PhotoWallBridge", SvgCoords.ToWorld(170f, 242f, DeckY(d))),
        ]);
        for (var i = 0; i < bridgePts.Count; i++)
        {
            entities.Add(Wall($"bridge-wall-{i}", LayerWall, d, bridgePts[i], bridgePts[(i + 1) % bridgePts.Count], ShapeLining, ShapeHullExt));
        }

        AddRoom(entities, "Airlock Port", LayerWall, ShapeLining, d, 76, 284, 16, 70,
            Hooks("AirlockPort", 76 + 8, 284 + 35, d));
        AddRoom(entities, "Airlock Starboard", LayerWall, ShapeLining, d, 228, 284, 16, 70,
            Hooks("AirlockStarboard", 228 + 8, 284 + 35, d));

        AddCrewCabins(entities, d);
        AddRoom(entities, "Infirmary", LayerHab, ShapeHab, d, 112, 400, 48, 66);
        AddRoom(entities, "Galley", LayerHab, ShapeHab, d, 160, 400, 48, 66,
            Hooks("GalleyGrowthChart", 160 + 4, 400 + 8, d));

        AttachHookToNamedSpace(entities, "Crossing Hallway", d, Hook("ArmoryCrossing", SvgCoords.ToWorld(160f, 318f, DeckY(d))));
    }

    private static void AddDeckPlus1(List<CadEntity> entities)
    {
        const int d = 1;
        AddCorridors(entities, d);
        AddRoom(entities, "Passenger Lounge", LayerHab, ShapeHab, d, 112, 104, 96, 144);
        AddBerths(entities, d);
        AddRoom(entities, "Sanitary", LayerHab, ShapeHab, d, 112, 400, 48, 66);
        AddRoom(entities, "Store", LayerHab, ShapeHab, d, 160, 400, 48, 66);
        // Engineering Void removed — continuous eng OAH in AddEngineeringSystem.
    }

    /// <summary>
    /// One full-OAH engineering volume aft of hab stack (STN 38.8–47), not three stacked rooms.
    /// </summary>
    private static void AddEngineeringSystem(List<CadEntity> entities)
    {
        var flags = new CadSpaceFlags { Enclosed = true, Hollow = true };
        var center = SvgCoords.ToWorld(
            EngSvgX + EngSvgW * 0.5f,
            EngSvgY + EngSvgH * 0.5f,
            DeckY(-1) + EngOahHeight * 0.45f);

        AddSpace(entities, "Engineering", LayerEng, ShapeEng, -1, EngOahHeight,
            EngSvgX, EngSvgY, EngSvgW, EngSvgH, flags,
            [Hook("EngCore", center)]);

        var engSpace = entities.Last(e => e.Kind == "space" && e.Name == "Engineering");
        engSpace.Properties = new Dictionary<string, JsonElement>
        {
            ["fullOah"] = JsonSerializer.SerializeToElement(true),
            ["continuousVoid"] = JsonSerializer.SerializeToElement(true),
            ["stationForeMeters"] = JsonSerializer.SerializeToElement(StnEngBh),
            ["stationAftMeters"] = JsonSerializer.SerializeToElement(StnHoldBh),
            ["heightClass"] = JsonSerializer.SerializeToElement("full-oah"),
        };

        // Perimeter walls at full OAH (deck −1 base). Fore face is WT-BH only (below).
        var y0 = DeckY(-1);
        Vector3 Bl(float x, float sy) => SvgCoords.ToWorld(x, sy, y0);
        var tl = Bl(EngSvgX, EngSvgY);
        var tr = Bl(EngSvgX + EngSvgW, EngSvgY);
        var br = Bl(EngSvgX + EngSvgW, EngSvgY + EngSvgH);
        var bl = Bl(EngSvgX, EngSvgY + EngSvgH);
        entities.Add(Wall("eng-oah-e", LayerWall, -1, SvgCoords.ToArray(tr), SvgCoords.ToArray(br), ShapeEng, ShapeHullInt, EngOahHeight));
        entities.Add(Wall("eng-oah-s", LayerWall, -1, SvgCoords.ToArray(br), SvgCoords.ToArray(bl), ShapeEng, ShapeHullInt, EngOahHeight));
        entities.Add(Wall("eng-oah-w", LayerWall, -1, SvgCoords.ToArray(bl), SvgCoords.ToArray(tl), ShapeEng, ShapeHullInt, EngOahHeight));

        // WT-BH eng fore face — named continuous bulkhead @ STN 38.8 (also cut on each plan deck).
        foreach (var deck in new[] { -1, 0, 1 })
        {
            var y = DeckY(deck);
            var wbl = SvgCoords.ToArray(SvgCoords.ToWorld(EngSvgX, EngSvgY, y));
            var wbr = SvgCoords.ToArray(SvgCoords.ToWorld(EngSvgX + EngSvgW, EngSvgY, y));
            var h = deck == -1 ? EngOahHeight : RoomHeight;
            var wall = Wall($"wt-bh-eng-d{deck}", LayerWall, deck, wbl, wbr, ShapeHullInt, ShapeEng, h);
            wall.Properties = WtProps(StnEngBh, "wt-bh-eng");
            entities.Add(wall);
        }
    }

    private static Dictionary<string, JsonElement> WtProps(float stationMeters, string tag) =>
        new()
        {
            ["watertight"] = JsonSerializer.SerializeToElement(true),
            ["stationMeters"] = JsonSerializer.SerializeToElement(stationMeters),
            ["tag"] = JsonSerializer.SerializeToElement(tag),
        };

    /// <summary>VEST-BR / VEST-P / VEST-S junction solids — framed openings only (no open T).</summary>
    private static void AddVestibules(List<CadEntity> entities)
    {
        foreach (var deck in new[] { -1, 0, 1 })
        {
            AddRoom(entities, "VEST-BR", LayerCorr, ShapeCorridor, deck, 146, 300, 28, 28);
            AddRoom(entities, "VEST-P", LayerCorr, ShapeCorridor, deck, 88, 304, 28, 28);
            AddRoom(entities, "VEST-S", LayerCorr, ShapeCorridor, deck, 204, 304, 28, 28);
        }
    }

    private static void AddCrewCabins(List<CadEntity> entities, int deck)
    {
        var cabinW = CabinBandW / CrewCabinCount;
        var protoId = Guid.Parse("f0000000-0000-4000-8000-000000000001");
        entities.Add(new CadEntity
        {
            Kind = "arrayInstance",
            Name = "CrewCabinArray",
            LayerId = LayerHab,
            Deck = deck,
            PrototypeId = protoId,
            BaseTransform = new CadTransform
            {
                Center = SvgCoords.ToArray(SvgCoords.ToWorld(CabinBandX + cabinW * 0.5f, CabinBandY + CabinBandH * 0.5f, DeckY(deck))),
                RotationY = 0f,
                Scale = [1f, 1f, 1f],
            },
            Counts = [CrewCabinCount, 1, 1],
            Spacing = [cabinW * SvgCoords.Scale, 0f, 0f],
        });

        for (var i = 0; i < CrewCabinCount; i++)
        {
            var x = CabinBandX + i * cabinW;
            AddRoom(entities, $"Crew Cabin {i + 1}", LayerHab, ShapeHab, deck, x, CabinBandY, cabinW, CabinBandH,
                Hooks($"Cabin{i + 1}", x + cabinW * 0.5f, CabinBandY + CabinBandH * 0.5f, deck));
        }
    }

    private static void AddBerths(List<CadEntity> entities, int deck)
    {
        // 10 berths in 5 columns × 2 rows within the same band footprint.
        var colW = CabinBandW / 5f;
        var rowH = CabinBandH / 2f;
        var protoId = Guid.Parse("f0000000-0000-4000-8000-000000000002");
        entities.Add(new CadEntity
        {
            Kind = "arrayInstance",
            Name = "BerthArray",
            LayerId = LayerHab,
            Deck = deck,
            PrototypeId = protoId,
            BaseTransform = new CadTransform
            {
                Center = SvgCoords.ToArray(SvgCoords.ToWorld(CabinBandX + colW * 0.5f, CabinBandY + rowH * 0.5f, DeckY(deck))),
            },
            Counts = [5, 1, 2],
            Spacing = [colW * SvgCoords.Scale, 0f, -rowH * SvgCoords.Scale],
        });

        var n = 0;
        for (var row = 0; row < 2; row++)
        for (var col = 0; col < 5; col++)
        {
            n++;
            var x = CabinBandX + col * colW;
            var y = CabinBandY + row * rowH;
            AddRoom(entities, $"Berth {n}", LayerHab, ShapeHab, deck, x, y, colW, rowH);
        }
    }

    private static void AddCargoSystem(List<CadEntity> entities)
    {
        var flags = new CadSpaceFlags { Enclosed = true, Hollow = true };
        // Continuous hold void (deck −1 base); plan claim 22×19×9, geometry clipped to AP ≈18×19×9.
        var center = SvgCoords.ToWorld(
            CargoSvgX + CargoSvgW * 0.5f,
            CargoSvgY + CargoSvgH * 0.5f,
            DeckY(-1) + CargoHeight * 0.45f);
        AddSpace(entities, "Cargo Void", LayerCargo, ShapeCargo, -1, CargoHeight,
            CargoSvgX, CargoSvgY, CargoSvgW, CargoSvgH, flags,
            [Hook("CargoVoidEye", center)]);
        var cargo = entities.Last(e => e.Kind == "space" && e.Name == "Cargo Void");
        cargo.Properties = new Dictionary<string, JsonElement>
        {
            ["continuousVoid"] = JsonSerializer.SerializeToElement(true),
            ["stationForeMeters"] = JsonSerializer.SerializeToElement(StnHoldBh),
            ["heightClass"] = JsonSerializer.SerializeToElement("hold-9m"),
            ["envelopeMeters"] = JsonSerializer.SerializeToElement(new[] { 22, 19, 9 }),
            ["geometryMeters"] = JsonSerializer.SerializeToElement(new[]
            {
                CargoSvgH * SvgCoords.Scale,
                CargoSvgW * SvgCoords.Scale,
                CargoHeight,
            }),
            ["c40Stow"] = JsonSerializer.SerializeToElement("5x1x3"),
            ["c40GridMeters"] = JsonSerializer.SerializeToElement(new[] { C40GridW, C40L, 3f * C40H }),
            ["rampGapMeters"] = JsonSerializer.SerializeToElement(RampGapM),
            ["sideAisleMeters"] = JsonSerializer.SerializeToElement((CargoSvgW * SvgCoords.Scale - C40GridW) * 0.5f),
        };

        // Fore catwalk: full bay athwartships × 3 m F–A on decks 0/+1 (clear of C40 stack).
        var catwalkSvgH = CatwalkDepthM / SvgCoords.Scale;
        foreach (var deck in new[] { 0, 1 })
        {
            AddSpace(entities, "Cargo Catwalk", LayerCargo, ShapeCargo, deck, 0.3f,
                CargoSvgX, CargoSvgY, CargoSvgW, catwalkSvgH,
                new CadSpaceFlags { Enclosed = false, Hollow = false },
                deck == 0
                    ? Hooks("CargoCatwalk", CargoSvgX + CargoSvgW * 0.5f, CargoSvgY + catwalkSvgH * 0.5f, deck)
                    : null);
        }

        // WT-BH hold / armored interface @ STN 47.0 — continuous face on each plan deck.
        foreach (var deck in new[] { -1, 0, 1 })
        {
            var y = DeckY(deck);
            var bl = SvgCoords.ToArray(SvgCoords.ToWorld(CargoSvgX, CargoSvgY, y));
            var br = SvgCoords.ToArray(SvgCoords.ToWorld(CargoSvgX + CargoSvgW, CargoSvgY, y));
            var h = deck == -1 ? CargoHeight : RoomHeight;
            var wall = Wall($"wt-bh-hold-d{deck}", LayerWall, deck, bl, br, ShapeHullInt, ShapeCargo, h);
            wall.Properties = WtProps(StnHoldBh, "wt-bh-hold");
            entities.Add(wall);
        }

        AddC40Stow(entities);
    }

    /// <summary>HILS-C40 packing from calypso-three-deck-c40 canvas: 5 abreast × 1 deep × 3 high.</summary>
    private static void AddC40Stow(List<CadEntity> entities)
    {
        var deckY = DeckY(-1);
        var catwalkSvgH = CatwalkDepthM / SvgCoords.Scale;
        // Grid flush aft of catwalk; container length along keel (SVG +Y / world −Z).
        var gridForeSvgY = CargoSvgY + catwalkSvgH;
        var gridMidSvgY = gridForeSvgY + C40L / SvgCoords.Scale * 0.5f;
        var gridLeftSvgX = SvgCoords.SvgCenterX - C40GridW / SvgCoords.Scale * 0.5f;

        for (var col = 0; col < 5; col++)
        {
            var svgX = gridLeftSvgX + col * (C40W + C40Cell) / SvgCoords.Scale + C40W / SvgCoords.Scale * 0.5f;

            for (var tier = 0; tier < 3; tier++)
            {
                var y = deckY + C40H * (tier + 0.5f);
                var c = SvgCoords.ToWorld(svgX, gridMidSvgY, y);
                entities.Add(new CadEntity
                {
                    Kind = "box",
                    Name = $"C40-c{col}-t{tier}",
                    LayerId = LayerCargo,
                    Deck = -1,
                    ShapeId = ShapeCargo,
                    Color = [0.55f, 0.42f, 0.22f],
                    Points =
                    [
                        SvgCoords.ToArray(c),
                        [C40W * 0.5f, C40H * 0.5f, C40L * 0.5f],
                    ],
                    Height = C40H,
                    Thickness = C40W,
                    Properties = new Dictionary<string, JsonElement>
                    {
                        ["hils"] = JsonSerializer.SerializeToElement("C40"),
                        ["col"] = JsonSerializer.SerializeToElement(col),
                        ["tier"] = JsonSerializer.SerializeToElement(tier),
                        ["externalMeters"] = JsonSerializer.SerializeToElement(new[] { C40L, C40W, C40H }),
                    },
                });
            }
        }
    }

    private static void AddShipOpenings(List<CadEntity> entities)
    {
        // --- Deck 0 schedule (blueprint BP-01 tags) ---
        // VEST-BR: Bridge Access (N), Crossing W, Crossing E
        AddScheduledOpening(entities, "PD-01", "Bridge Door", 0, 160f, 300f, "door",
            "BridgeDoor", CorridorDoorWidth, 0.8f, DoorHeight, ["VEST-BR", "Bridge Access"]);
        AddScheduledOpening(entities, "PD-01W", "VEST-BR West", 0, 146f, 314f, "door",
            null, CorridorDoorWidth, 0.6f, DoorHeight, ["VEST-BR", "Crossing Hallway"]);
        AddScheduledOpening(entities, "PD-01E", "VEST-BR East", 0, 174f, 314f, "door",
            null, CorridorDoorWidth, 0.6f, DoorHeight, ["VEST-BR", "Crossing Hallway"]);

        // VEST-P: Crossing (E), Port Corridor (S), Airlock (W)
        AddScheduledOpening(entities, "PD-VP-E", "VEST-P East", 0, 116f, 318f, "door",
            null, CorridorDoorWidth, 0.6f, DoorHeight, ["VEST-P", "Crossing Hallway"]);
        AddScheduledOpening(entities, "PD-VP-S", "VEST-P South", 0, 102f, 332f, "door",
            null, CorridorDoorWidth, 0.6f, DoorHeight, ["VEST-P", "Port Corridor"]);
        AddScheduledOpening(entities, "AH-P-IN", "Airlock Port Inner", 0, 92f, 319f, "hatch",
            null, DoorWidth, 0.8f, DoorHeight, ["VEST-P", "Airlock Port"]);

        // VEST-S: Crossing (W), Stbd Corridor (S), Airlock (E)
        AddScheduledOpening(entities, "PD-VS-W", "VEST-S West", 0, 204f, 318f, "door",
            null, CorridorDoorWidth, 0.6f, DoorHeight, ["VEST-S", "Crossing Hallway"]);
        AddScheduledOpening(entities, "PD-VS-S", "VEST-S South", 0, 218f, 332f, "door",
            null, CorridorDoorWidth, 0.6f, DoorHeight, ["VEST-S", "Starboard Corridor"]);
        AddScheduledOpening(entities, "AH-S-IN", "Airlock Starboard Inner", 0, 228f, 319f, "hatch",
            null, DoorWidth, 0.8f, DoorHeight, ["VEST-S", "Airlock Starboard"]);

        AddScheduledOpening(entities, "AH-P", "Airlock Port Outer", 0, 76f, 319f, "hatch",
            "AirlockPortOuter", DoorWidth, 0.8f, DoorHeight, ["Airlock Port", "exterior"]);
        AddScheduledOpening(entities, "AH-S", "Airlock Starboard Outer", 0, 244f, 319f, "hatch",
            "AirlockStarboardOuter", DoorWidth, 0.8f, DoorHeight, ["Airlock Starboard", "exterior"]);

        // Shafts
        AddScheduledOpening(entities, "PD-02", "Stairs Door", 0, 150f, 268f, "door",
            null, PersonnelDoorWidth, 0.6f, DoorHeight, ["Stairs", "Bridge Access"]);
        AddScheduledOpening(entities, "PD-03", "Elev Door", 0, 170f, 268f, "door",
            null, PersonnelDoorWidth, 0.6f, DoorHeight, ["Elev", "Bridge Access"]);

        // Cabins
        var cabinW = CabinBandW / CrewCabinCount;
        for (var i = 0; i < CrewCabinCount; i++)
        {
            var x = CabinBandX + i * cabinW + cabinW * 0.5f;
            AddScheduledOpening(entities, $"PD-C{i + 1}", $"Cabin {i + 1} Door", 0, x, CabinBandY, "door",
                null, DoorWidth, 0.6f, DoorHeight, [$"Crew Cabin {i + 1}", "corridor"]);
        }

        AddScheduledOpening(entities, "PD-10", "Infirmary Door", 0, 112f, 425f, "door",
            null, PersonnelDoorWidth, 0.6f, DoorHeight, ["Infirmary", "Port Corridor"]);
        AddScheduledOpening(entities, "PD-11", "Galley Door", 0, 208f, 425f, "door",
            null, PersonnelDoorWidth, 0.6f, DoorHeight, ["Galley", "Starboard Corridor"]);

        // Twin WT corridor doors corr → eng @ STN 38.8 (on wt-bh-eng fore face)
        AddScheduledOpening(entities, "PD-20", "WT Eng Port", 0, EngSvgX + 20f, EngSvgY, "door",
            null, CorridorDoorWidth, 0.8f, DoorHeight, ["Port Corridor", "Engineering"], hostNameHint: "wt-bh-eng");
        AddScheduledOpening(entities, "PD-21", "WT Eng Stbd", 0, EngSvgX + EngSvgW - 20f, EngSvgY, "door",
            null, CorridorDoorWidth, 0.8f, DoorHeight, ["Starboard Corridor", "Engineering"], hostNameHint: "wt-bh-eng");

        // Eng side personnel doors
        AddScheduledOpening(entities, "PD-22", "Eng Side Port", 0, EngSvgX, 492f, "door",
            null, PersonnelDoorWidth, 0.6f, DoorHeight, ["Engineering", "exterior"]);
        AddScheduledOpening(entities, "PD-23", "Eng Side Stbd", 0, EngSvgX + EngSvgW, 492f, "door",
            null, PersonnelDoorWidth, 0.6f, DoorHeight, ["Engineering", "exterior"]);

        // Twin cargo hatches on armored WT-BH @ STN 47
        AddScheduledOpening(entities, "CD-P", "Cargo Hatch Port", 0, CargoSvgX + 40f, CargoSvgY, "hatch",
            null, 2.0f, 1.0f, DoorHeight, ["Engineering", "Cargo Void"], hostNameHint: "wt-bh-hold");
        AddScheduledOpening(entities, "CD-S", "Cargo Hatch Stbd", 0, CargoSvgX + CargoSvgW - 40f, CargoSvgY, "hatch",
            "ArmoredCargoDoor", 2.0f, 1.0f, DoorHeight, ["Engineering", "Cargo Void"], hostNameHint: "wt-bh-hold");

        // Full five-abreast roll-out ramp (canvas: GRID_W × 4.0 × 3.2).
        AddScheduledOpening(entities, "RAMP", "Aft Ramp", 0, CargoSvgX + CargoSvgW * 0.5f, CargoSvgY + CargoSvgH, "ramp",
            "AftRamp", C40GridW, RampDepthM, RampHeightM, ["Cargo Void", "exterior"]);

        // +1 lounge / berths
        AddScheduledOpening(entities, "PD-L1", "Lounge Door", 1, 160f, 248f, "door",
            null, DoorWidth, 0.8f, DoorHeight, ["Passenger Lounge", "Crossing Hallway"]);
        for (var i = 0; i < 5; i++)
        {
            var x = CabinBandX + i * (CabinBandW / 5f) + (CabinBandW / 5f) * 0.5f;
            AddScheduledOpening(entities, $"PD-B{i + 1}", $"Berth Door {i + 1}", 1, x, CabinBandY, "door",
                null, DoorWidth, 0.6f, DoorHeight, [$"Berth", "corridor"]);
        }
    }

    private static void AddArrayMetadata(List<CadEntity> entities)
    {
        var deck0Walls = entities.Where(e => e.Kind == "wall" && e.Deck == 0).Select(e => e.Id).Take(40).ToList();
        if (deck0Walls.Count >= 2)
        {
            entities.Add(new CadEntity
            {
                Kind = "weld",
                Name = "Deck0 room-boundary weld",
                MemberIds = deck0Walls,
                TouchEpsilonMeters = 0.01f,
                LayerId = LayerWall,
            });
        }

        var cargo = entities.FirstOrDefault(e => e.Kind == "space" && e.Name == "Cargo Void" && e.Deck == -1);
        var hull = entities.FirstOrDefault(e => e.Kind == "wall" && e.Deck == -1 && e.Name?.StartsWith("hull--1") == true);
        if (cargo is not null && hull is not null)
        {
            entities.Add(new CadEntity
            {
                Kind = "boolean",
                Name = "CargoVoid subtract",
                Operation = "subtract",
                LeftId = cargo.Id,
                RightId = hull.Id,
                Mode = "solid",
                TouchEpsilonMeters = 0.01f,
                LayerId = LayerCargo,
            });
        }
    }

    private static void AddCorridors(List<CadEntity> entities, int deck)
    {
        AddRoom(entities, "Crossing Hallway", LayerCorr, ShapeCorridor, deck, 92, 308, 136, 20);
        AddRoom(entities, "Port Corridor", LayerCorr, ShapeCorridor, deck, 92, 328, 20, 212);
        AddRoom(entities, "Starboard Corridor", LayerCorr, ShapeCorridor, deck, 208, 328, 20, 212);
    }

    private static void AddRoom(
        List<CadEntity> entities,
        string name,
        Guid layer,
        Guid floorShape,
        int deck,
        float svgX,
        float svgY,
        float svgW,
        float svgH,
        List<CadHook>? hooks = null)
    {
        AddSpace(entities, name, layer, floorShape, deck, RoomHeight, svgX, svgY, svgW, svgH, hooks: hooks);
        AddRectWalls(entities, deck, svgX, svgY, svgW, svgH, ShapeLining, floorShape);
    }

    private static void AddScheduledOpening(
        List<CadEntity> entities,
        string tag,
        string name,
        int deck,
        float svgX,
        float svgY,
        string openingType,
        string? hookTag,
        float widthM,
        float depthM,
        float heightM,
        string[] connects,
        string? hostNameHint = null)
    {
        var y = DeckY(deck);
        var c = SvgCoords.ToWorld(svgX, svgY, y);
        List<float[]> fp =
        [
            SvgCoords.ToArray(c + new Vector3(-widthM * 0.5f, 0f, -depthM * 0.5f)),
            SvgCoords.ToArray(c + new Vector3(widthM * 0.5f, 0f, -depthM * 0.5f)),
            SvgCoords.ToArray(c + new Vector3(widthM * 0.5f, 0f, depthM * 0.5f)),
            SvgCoords.ToArray(c + new Vector3(-widthM * 0.5f, 0f, depthM * 0.5f)),
        ];

        Guid? hostId = null;
        var best = float.MaxValue;
        foreach (var wall in entities.Where(e => e.Kind == "wall" && e.Deck == deck && e.A is not null && e.B is not null))
        {
            if (hostNameHint is not null &&
                wall.Name?.Contains(hostNameHint, StringComparison.OrdinalIgnoreCase) != true)
                continue;

            var a = SvgCoords.FromArray(wall.A!);
            var b = SvgCoords.FromArray(wall.B!);
            var mid = (a + b) * 0.5f;
            var d = Vector2.Distance(new Vector2(c.X, c.Z), new Vector2(mid.X, mid.Z));
            if (d < best)
            {
                best = d;
                hostId = wall.Id;
            }
        }

        // If hint filtered everything away, fall back to nearest wall.
        if (hostId is null)
        {
            best = float.MaxValue;
            foreach (var wall in entities.Where(e => e.Kind == "wall" && e.Deck == deck && e.A is not null && e.B is not null))
            {
                var a = SvgCoords.FromArray(wall.A!);
                var b = SvgCoords.FromArray(wall.B!);
                var mid = (a + b) * 0.5f;
                var d = Vector2.Distance(new Vector2(c.X, c.Z), new Vector2(mid.X, mid.Z));
                if (d < best)
                {
                    best = d;
                    hostId = wall.Id;
                }
            }
        }

        // ClearWidth matches cut for corridor/WT and ≥1.0 passageway (canvas policy).
        var clearW = widthM;
        var clearH = Math.Max(1.5f, heightM - HSill);
        entities.Add(new CadEntity
        {
            Kind = "opening",
            Name = $"{tag} {name}",
            LayerId = LayerDoor,
            Deck = deck,
            Height = heightM,
            OpeningType = openingType,
            Footprint = fp,
            HostWallId = best < 4.5f ? hostId : null,
            ConnectsSides = ["A", "B"],
            Hooks = hookTag is null ? null : [Hook(hookTag, c)],
            Properties = new Dictionary<string, JsonElement>
            {
                ["tag"] = JsonSerializer.SerializeToElement(tag),
                ["openingType"] = JsonSerializer.SerializeToElement(openingType),
                ["W_open"] = JsonSerializer.SerializeToElement(widthM),
                ["H_open"] = JsonSerializer.SerializeToElement(heightM),
                ["H_sill"] = JsonSerializer.SerializeToElement(HSill),
                ["clearWidth"] = JsonSerializer.SerializeToElement(clearW),
                ["clearHeight"] = JsonSerializer.SerializeToElement(clearH),
                ["ClearWidth"] = JsonSerializer.SerializeToElement(clearW),
                ["ClearHeight"] = JsonSerializer.SerializeToElement(clearH),
                ["airtightWhenClosed"] = JsonSerializer.SerializeToElement(true),
                ["leafState"] = JsonSerializer.SerializeToElement("Closed"),
                ["connects"] = JsonSerializer.SerializeToElement(connects),
            },
        });
    }

    private static List<float[]> BridgeFootprint()
    {
        float[][] svg =
        [
            [160, 104], [206, 122], [230, 156], [230, 252], [178, 252], [170, 242],
            [150, 242], [142, 252], [90, 252], [90, 156], [114, 122],
        ];
        return svg.Select(p => SvgCoords.ToArray(SvgCoords.ToWorld(p[0], p[1], DeckY(0)))).ToList();
    }

    private static void AddSpace(
        List<CadEntity> entities,
        string name,
        Guid layer,
        Guid floorShape,
        int deck,
        float height,
        float svgX,
        float svgY,
        float svgW,
        float svgH,
        CadSpaceFlags? flags = null,
        List<CadHook>? hooks = null)
    {
        var y = DeckY(deck);
        var p0 = SvgCoords.ToArray(SvgCoords.ToWorld(svgX, svgY, y));
        var p1 = SvgCoords.ToArray(SvgCoords.ToWorld(svgX + svgW, svgY, y));
        var p2 = SvgCoords.ToArray(SvgCoords.ToWorld(svgX + svgW, svgY + svgH, y));
        var p3 = SvgCoords.ToArray(SvgCoords.ToWorld(svgX, svgY + svgH, y));
        entities.Add(new CadEntity
        {
            Kind = "space",
            Name = name,
            LayerId = layer,
            Deck = deck,
            Height = height,
            FloorShapeId = floorShape,
            Points = [p0, p1, p2, p3],
            ShapeId = floorShape,
            Flags = flags ?? new CadSpaceFlags { Enclosed = true, Hollow = false },
            Hooks = hooks,
        });
    }

    private static void AddSpace(
        List<CadEntity> entities,
        string name,
        Guid layer,
        Guid floorShape,
        int deck,
        float height,
        List<float[]> points,
        CadSpaceFlags? flags = null,
        List<CadHook>? hooks = null)
    {
        entities.Add(new CadEntity
        {
            Kind = "space",
            Name = name,
            LayerId = layer,
            Deck = deck,
            Height = height,
            FloorShapeId = floorShape,
            Points = points,
            ShapeId = floorShape,
            Flags = flags ?? new CadSpaceFlags { Enclosed = true, Hollow = false },
            Hooks = hooks,
        });
    }

    private static void AddRectWalls(
        List<CadEntity> entities,
        int deck,
        float svgX,
        float svgY,
        float svgW,
        float svgH,
        Guid sideA,
        Guid sideB,
        float height = RoomHeight)
    {
        var y = DeckY(deck);
        Vector3 Bl(float x, float sy) => SvgCoords.ToWorld(x, sy, y);
        var tl = Bl(svgX, svgY);
        var tr = Bl(svgX + svgW, svgY);
        var br = Bl(svgX + svgW, svgY + svgH);
        var bl = Bl(svgX, svgY + svgH);
        entities.Add(Wall($"rw-{deck}-{svgX:0}-{svgY:0}-n", LayerWall, deck, SvgCoords.ToArray(tl), SvgCoords.ToArray(tr), sideA, sideB, height));
        entities.Add(Wall($"rw-{deck}-{svgX:0}-{svgY:0}-e", LayerWall, deck, SvgCoords.ToArray(tr), SvgCoords.ToArray(br), sideA, sideB, height));
        entities.Add(Wall($"rw-{deck}-{svgX:0}-{svgY:0}-s", LayerWall, deck, SvgCoords.ToArray(br), SvgCoords.ToArray(bl), sideA, sideB, height));
        entities.Add(Wall($"rw-{deck}-{svgX:0}-{svgY:0}-w", LayerWall, deck, SvgCoords.ToArray(bl), SvgCoords.ToArray(tl), sideA, sideB, height));
    }

    private static CadEntity Wall(
        string name,
        Guid layer,
        int deck,
        float[] a,
        float[] b,
        Guid shapeA,
        Guid shapeB,
        float height = RoomHeight) =>
        new()
        {
            Kind = "wall",
            Name = name,
            LayerId = layer,
            Deck = deck,
            Thickness = WallThickness,
            Height = height,
            A = a,
            B = b,
            Sides = new CadWallSides
            {
                A = new CadWallSide { ShapeId = shapeA },
                B = new CadWallSide { ShapeId = shapeB },
            },
        };

    private static List<CadHook> Hooks(string tag, float svgX, float svgY, int deck) =>
        [Hook(tag, SvgCoords.ToWorld(svgX, svgY, DeckY(deck)))];

    private static CadHook Hook(string tag, Vector3 pos) =>
        new() { Id = Guid.NewGuid(), Tag = tag, Position = SvgCoords.ToArray(pos) };

    private static Vector3 Centroid(List<float[]> pts)
    {
        var sum = Vector3.Zero;
        foreach (var p in pts)
            sum += SvgCoords.FromArray(p);
        return sum / pts.Count;
    }

    private static void AttachHookToNamedSpace(List<CadEntity> entities, string name, int deck, CadHook hook)
    {
        var space = entities.FirstOrDefault(e => e.Kind == "space" && e.Name == name && e.Deck == deck && e.Height > 0.5f);
        if (space is null)
            return;
        space.Hooks ??= [];
        space.Hooks.Add(hook);
    }
}
