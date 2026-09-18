/**
 * Calypso design lock — shared envelope + internals (fabrication authority).
 * LOA 69 mid-stretch from hatch/internals GA; L_fore 17 / L_aft 4 locked.
 *
 * Import from manufacturer generators and generate-internals.mjs.
 */

export const ceilQuarter = (m) => Math.ceil(m * 4 - 1e-9) / 4;
export const nice = (m) => {
  const q = Math.round(m * 4) / 4;
  if (Math.abs(m - q) < 1e-6) return String(q);
  return m.toFixed(3);
};
export const fmt = (m) => {
  const q = Math.round(m * 4) / 4;
  if (Math.abs(m - q) < 1e-6) return `${q} m`;
  return `${m.toFixed(3)} m`;
};

// ─── Outer envelope ──────────────────────────────────────────────────────────
export const L_FORE = 17;
export const L_AFT = 4;
export const BEAM = 20;
export const OAH = 12;
export const CHAMFER = 2.5;
export const T_SHELL = 0.008; // 316L OML plate
export const MATERIAL = "AISI 316L";
export const T_BH = 0.15; // transverse / longitudinal BH face (clear dims inside)

export const FORE_STATIONS = [
  { z: 0, beam: 3.5, h: 4, halfBeam: 1.75, halfHeight: 2, id: "ST0", mark: "stem" },
  { z: 3.25, beam: 10, h: 7.5, halfBeam: 5, halfHeight: 3.75, id: "ST1", mark: "fore-1" },
  { z: 10, beam: 17, h: 10.5, halfBeam: 8.5, halfHeight: 5.25, id: "ST2", mark: "fore-2" },
  {
    z: L_FORE,
    beam: 20,
    h: 12,
    halfBeam: BEAM / 2,
    halfHeight: OAH / 2,
    id: "ST3",
    mark: "shoulder",
  },
];

// ─── Hold / C40 / aft door ───────────────────────────────────────────────────
export const C40_L = 12.192;
export const C40_W = 2.438;
export const C40_H = 2.591;
export const CELL = 0.2;
export const COLS = 5;
export const TIERS = 3;
export const GRID_W = COLS * C40_W + (COLS - 1) * CELL;
export const STACK_H = TIERS * C40_H;
export const CATWALK_D = 6;
export const GAP_CATWALK_C40 = 0.5;
export const RAMP_GAP = 1;
export const HOLD_L = CATWALK_D + GAP_CATWALK_C40 + C40_L + RAMP_GAP; // 19.692
export const CLEAR_DOOR = 0.5;
export const PACK_W = GRID_W;
export const PACK_H = STACK_H;
export const DOOR_W = ceilQuarter(PACK_W + 2 * CLEAR_DOOR); // 14
export const DOOR_H = ceilQuarter(PACK_H + CLEAR_DOOR); // 8.5
export const SILL = 0.25;
export const APRON_D = ceilQuarter(C40_L); // 12.25
export const SIDE_CLEAR = (DOOR_W - PACK_W) / 2;
export const TOP_CLEAR = DOOR_H - PACK_H;
export const SIDE_SHELL = (BEAM - DOOR_W) / 2;
export const HEADER = OAH - SILL - DOOR_H;

// ─── Longitudinal bands (deck 0 topology) ────────────────────────────────────
export const Z_BRIDGE_AFT = 13;
export const L_VERT = 6;
export const Z_VERT_FORE = Z_BRIDGE_AFT;
export const Z_VERT_AFT = Z_VERT_FORE + L_VERT; // 19
export const L_CROSS = 4;
export const Z_CROSS_FORE = Z_VERT_AFT;
export const Z_CROSS_AFT = Z_CROSS_FORE + L_CROSS; // 23
/** CAL-INT-DK-001 Rev F: cabin clear F–A 7.2 m; leftover extends INF/GAL/STORE. */
export const CABIN_MODULE_W = 2; // O/A athwartships (= clear 1.92 + wall 0.08)
export const CABIN_CLEAR_W = 1.92;
export const CABIN_WALL_T = 0.08;
export const CABIN_CLEAR_D = 7.2;
export const L_CREW = CABIN_CLEAR_D + CABIN_WALL_T; // 7.28 — module band incl. end walls
export const Z_CREW_FORE = Z_CROSS_AFT;
export const Z_CREW_AFT = Z_CREW_FORE + L_CREW; // 30.28
export const CREW_CABIN_COUNT = 5;
export const Z_MED_FORE = Z_CREW_AFT;
export const Z_MED_AFT = 41; // eng face locked; INF/GAL clear F–A 10.64
export const L_MED = Z_MED_AFT - Z_MED_FORE; // 10.72
export const L_ENG = 8.25;
export const Z_ENG_FORE = Z_MED_AFT;
export const Z_ENG_AFT = Z_ENG_FORE + L_ENG; // 49.25
export const Z_HOLD_FORE = Z_ENG_AFT;

export const LOA_RAW = Z_HOLD_FORE + HOLD_L; // 68.942
export const LOA = ceilQuarter(LOA_RAW); // 69
export const L_MID = LOA - L_FORE - L_AFT; // 48
export const MID_SLACK = LOA - LOA_RAW;

export const C40_AFT = LOA - RAMP_GAP;
export const C40_FORE = C40_AFT - C40_L;
export const CATWALK_FORE = Z_HOLD_FORE + MID_SLACK;
export const CATWALK_AFT = CATWALK_FORE + CATWALK_D;
export const GAP_FORE = CATWALK_AFT;
export const GAP_AFT = GAP_FORE + GAP_CATWALK_C40;

// ─── Athwartships / circulation ──────────────────────────────────────────────
export const CORR_INNER = 5;
export const CORR_W = 3; // spine clear — widened outboard (was 2)
export const CORR_OUTER = CORR_INNER + CORR_W; // 8
export const STACK = CORR_INNER; // |y| ≤ 5 center stack
export const ACCESS_W = 2;
export const SHAFT_W = 3.4;
export const AIR_OUTER = BEAM / 2; // 10 — D3 on shell
export const AIR_W = 1.6;
export const AIR_INNER = AIR_OUTER - AIR_W; // 8.4
export const L_AIR_JOG = 2.5;
export const DOOR_PASS = 1.0;
export const DOOR_CORR = 2.0;
export const DOOR_H_PASS = 2.1; // personnel leaf height clear
export const DOOR_H_CORR = 2.2;

// ─── Decks (floor elevations from keel datum) ────────────────────────────────
export const Z_DK_M1 = 0.5;
export const Z_TANK = 1.0;
export const Z_DK0 = 4.0;
export const Z_DK1 = 8.0;
export const ROOM_H = 3.2;

export const CHAMFER_DIAG = CHAMFER * Math.SQRT2;
export const FLAT_BEAM = BEAM - 2 * CHAMFER;
export const FLAT_OAH = OAH - 2 * CHAMFER;
export const PAD = 0.5; // mesh AABB pad

/** Interpolate outer-hull full height at station z (fore taper). */
export function hullHeightAt(z) {
  if (z <= FORE_STATIONS[0].z) return FORE_STATIONS[0].h;
  if (z >= L_FORE) return OAH;
  for (let i = 0; i < FORE_STATIONS.length - 1; i++) {
    const a = FORE_STATIONS[i];
    const b = FORE_STATIONS[i + 1];
    if (z >= a.z && z <= b.z) {
      const t = (z - a.z) / (b.z - a.z);
      return a.h + t * (b.h - a.h);
    }
  }
  return OAH;
}

export function hullBeamAt(z) {
  if (z <= FORE_STATIONS[0].z) return FORE_STATIONS[0].beam;
  if (z >= L_FORE) return BEAM;
  for (let i = 0; i < FORE_STATIONS.length - 1; i++) {
    const a = FORE_STATIONS[i];
    const b = FORE_STATIONS[i + 1];
    if (z >= a.z && z <= b.z) {
      const t = (z - a.z) / (b.z - a.z);
      return a.beam + t * (b.beam - a.beam);
    }
  }
  return BEAM;
}

export function stationChamfer(halfBeam, halfHeight) {
  const kx = (halfBeam * 2) / BEAM;
  const ky = (halfHeight * 2) / OAH;
  return {
    cx: Math.min(CHAMFER * kx, halfBeam * 0.45),
    cy: Math.min(CHAMFER * ky, halfHeight * 0.45),
  };
}

/** Soft-chamfer octagon at a station — verts in lock frame (y CL→stbd, up from keel). */
export function octagonVerts(halfBeam, halfHeight) {
  const { cx, cy } = stationChamfer(halfBeam, halfHeight);
  const mid = OAH / 2;
  return [
    { y: -halfBeam + cx, up: mid + halfHeight },
    { y: halfBeam - cx, up: mid + halfHeight },
    { y: halfBeam, up: mid + halfHeight - cy },
    { y: halfBeam, up: mid - halfHeight + cy },
    { y: halfBeam - cx, up: mid - halfHeight },
    { y: -halfBeam + cx, up: mid - halfHeight },
    { y: -halfBeam, up: mid - halfHeight + cy },
    { y: -halfBeam, up: mid + halfHeight - cy },
  ];
}

/**
 * Faceted OML loft stations (fore taper + midbody + aft face).
 * Enough for render/manufacture without falling back to AABB boxes.
 */
export function hullLoft() {
  const stations = FORE_STATIONS.map((s) => ({
    id: s.id,
    zFromStem: s.z,
    halfBeam: s.halfBeam,
    halfHeight: s.halfHeight,
    verts: octagonVerts(s.halfBeam, s.halfHeight),
  }));
  for (const m of [
    { id: "MB1", z: L_FORE + L_MID / 2 },
    { id: "MB2", z: L_FORE + L_MID },
    { id: "AFT", z: LOA },
  ]) {
    stations.push({
      id: m.id,
      zFromStem: m.z,
      halfBeam: BEAM / 2,
      halfHeight: OAH / 2,
      verts: octagonVerts(BEAM / 2, OAH / 2),
    });
  }
  return { shell: "OML", material: MATERIAL, t: T_SHELL, stations };
}

/**
 * Plan footprint ring [y, zFromStem] for a clear rectangle clipped to hull beam.
 * CCW when viewed from above (stbd-aft → port-aft → port-fore → stbd-fore walk).
 */
export function planRingRect(y0, y1, z0, z1, samples = 16) {
  const ring = [];
  const clipY = (y, z) => {
    const hb = hullBeamAt(z) / 2 - T_SHELL - 0.02;
    return Math.max(-hb, Math.min(hb, y));
  };
  for (let i = 0; i <= samples; i++) {
    const z = z0 + ((z1 - z0) * i) / samples;
    ring.push([clipY(y1, z), Number(z.toFixed(4))]);
  }
  for (let i = samples; i >= 0; i--) {
    const z = z0 + ((z1 - z0) * i) / samples;
    ring.push([clipY(y0, z), Number(z.toFixed(4))]);
  }
  return ring;
}

function withPlanRing(c) {
  return {
    ...c,
    planRing: planRingRect(c.y0, c.y1, c.z0, c.z1),
  };
}

/** Exterior features in lock coords (for orbit silhouette / render). */
export function exterior() {
  const blisterZ = Z_CROSS_AFT + L_AIR_JOG / 2;
  return {
    nacelles: [
      {
        id: "nacelle-port",
        y: -(BEAM / 2 + 1.2),
        up: OAH * 0.35,
        zFromStem: LOA * 0.62,
        radius: 1.35,
        length: 9,
      },
      {
        id: "nacelle-stbd",
        y: BEAM / 2 + 1.2,
        up: OAH * 0.35,
        zFromStem: LOA * 0.62,
        radius: 1.35,
        length: 9,
      },
    ],
    aftDoor: {
      id: "ext-aft-cargo-door",
      y: 0,
      up: SILL + DOOR_H / 2,
      zFromStem: LOA,
      halfW: DOOR_W / 2,
      halfH: DOOR_H / 2,
      halfD: 0.2,
    },
    airlockBlisters: [
      {
        id: "ext-airlock-blister-port",
        y: -(AIR_OUTER - 0.4),
        up: Z_DK0 + DOOR_H_PASS / 2,
        zFromStem: blisterZ,
        halfY: 0.8,
        halfUp: 1.05,
        halfZ: 1.25,
      },
      {
        id: "ext-airlock-blister-stbd",
        y: AIR_OUTER - 0.4,
        up: Z_DK0 + DOOR_H_PASS / 2,
        zFromStem: blisterZ,
        halfY: 0.8,
        halfUp: 1.05,
        halfZ: 1.25,
      },
    ],
    c40: {
      fore: C40_FORE,
      cols: COLS,
      tiers: TIERS,
      L: C40_L,
      W: C40_W,
      H: C40_H,
      cell: CELL,
    },
  };
}

/**
 * Clear AABB in ship coords: z from stem forward→aft, y CL→stbd+, up from keel.
 * Rooms are clear inside BH faces (T_BH already outside these boxes for midbody stacks).
 */
export function compartments() {
  const mid = { y0: -STACK + T_BH / 2, y1: STACK - T_BH / 2 };
  return [
    {
      id: "BRIDGE",
      deck: 0,
      z0: 2,
      z1: Z_BRIDGE_AFT - T_BH / 2,
      y0: -4,
      y1: 4,
      up0: Z_DK0,
      up1: Z_DK0 + ROOM_H,
      note: "clipped to fore taper in drawings",
    },
    {
      id: "FUEL",
      deck: -1,
      z0: 2,
      z1: Z_BRIDGE_AFT - T_BH / 2,
      y0: -4,
      y1: 4,
      up0: Z_DK_M1,
      up1: Z_DK_M1 + ROOM_H,
      note: "under bridge; clipped to hull",
    },
    {
      id: "LOUNGE",
      deck: 1,
      z0: 2,
      z1: Z_BRIDGE_AFT - T_BH / 2,
      y0: -4,
      y1: 4,
      up0: Z_DK1,
      up1: Z_DK1 + ROOM_H,
      note: "over bridge; clipped to hull",
    },
    {
      id: "STAIRS_P",
      deck: "all",
      z0: Z_VERT_FORE,
      z1: Z_VERT_AFT,
      y0: -ACCESS_W / 2 - SHAFT_W,
      y1: -ACCESS_W / 2,
      up0: Z_DK_M1,
      up1: Z_DK1 + ROOM_H,
      note: "vertical shaft all decks",
    },
    {
      id: "ACCESS",
      deck: "all",
      z0: Z_VERT_FORE,
      z1: Z_VERT_AFT,
      y0: -ACCESS_W / 2,
      y1: ACCESS_W / 2,
      up0: Z_DK_M1,
      up1: Z_DK1 + ROOM_H,
      note: "accessway",
    },
    {
      id: "ELEV_S",
      deck: "all",
      z0: Z_VERT_FORE,
      z1: Z_VERT_AFT,
      y0: ACCESS_W / 2,
      y1: ACCESS_W / 2 + SHAFT_W,
      up0: Z_DK_M1,
      up1: Z_DK1 + ROOM_H,
      note: "elevator shaft all decks",
    },
    {
      id: "CROSSING",
      deck: "all",
      z0: Z_CROSS_FORE + T_BH / 2,
      z1: Z_CROSS_AFT - T_BH / 2,
      y0: -AIR_INNER + T_BH / 2,
      y1: AIR_INNER - T_BH / 2,
      up0: Z_DK0,
      up1: Z_DK0 + ROOM_H,
      note: "athwartships; airlocks outboard DK0 only",
    },
    {
      id: "CORR_P",
      deck: "all",
      z0: Z_CREW_FORE,
      z1: Z_ENG_AFT,
      y0: -CORR_OUTER,
      y1: -CORR_INNER,
      up0: Z_DK0,
      up1: Z_DK0 + ROOM_H,
      note: "port spine corridor clear 3 m; same track −1/+1 (CAL-INT-DK-001)",
    },
    {
      id: "CORR_S",
      deck: "all",
      z0: Z_CREW_FORE,
      z1: Z_ENG_AFT,
      y0: CORR_INNER,
      y1: CORR_OUTER,
      up0: Z_DK0,
      up1: Z_DK0 + ROOM_H,
      note: "starboard spine corridor clear 3 m",
    },
    {
      id: "INFIRMARY",
      deck: 0,
      z0: Z_MED_FORE + CABIN_WALL_T / 2,
      z1: Z_MED_AFT - CABIN_WALL_T / 2,
      y0: -STACK + CABIN_WALL_T / 2,
      y1: -CABIN_WALL_T / 2,
      up0: Z_DK0,
      up1: Z_DK0 + ROOM_H,
      note: "infirmary — clear F–A 10.64 m (DK-001 leftover aft of cabins)",
    },
    {
      id: "GALLEY",
      deck: 0,
      z0: Z_MED_FORE + CABIN_WALL_T / 2,
      z1: Z_MED_AFT - CABIN_WALL_T / 2,
      y0: CABIN_WALL_T / 2,
      y1: STACK - CABIN_WALL_T / 2,
      up0: Z_DK0,
      up1: Z_DK0 + ROOM_H,
      note: "galley — clear F–A 10.64 m",
    },
    {
      id: "ENG",
      deck: "atrium",
      z0: Z_ENG_FORE + T_BH / 2,
      z1: Z_ENG_AFT - T_BH / 2,
      ...mid,
      up0: Z_DK_M1,
      up1: OAH - 0.3,
      note: "full-height atrium",
    },
    {
      id: "HOLD",
      deck: "cargo",
      z0: CATWALK_FORE,
      z1: LOA,
      y0: -BEAM / 2 + 0.5,
      y1: BEAM / 2 - 0.5,
      up0: Z_TANK,
      up1: Z_TANK + 9,
    },
    {
      id: "UTILITY_M1",
      deck: -1,
      z0: Z_CREW_FORE,
      z1: Z_ENG_FORE - T_BH / 2,
      ...mid,
      up0: Z_DK_M1,
      up1: Z_DK_M1 + ROOM_H,
    },
    ...modularCabins(0, "CREW", "crew bunks"),
    ...modularCabins(1, "PAX", "passenger bunks — same module as crew"),
    {
      id: "STORE_P1",
      deck: 1,
      z0: Z_MED_FORE + CABIN_WALL_T / 2,
      z1: Z_MED_AFT - CABIN_WALL_T / 2,
      ...mid,
      up0: Z_DK1,
      up1: Z_DK1 + ROOM_H,
      note: "store — same F–A leftover as INF/GAL (DK-001)",
    },
  ].map(withPlanRing);
}

/** Five C0n modules abreast (P→S), clear 1.92×7.2; O/A 2 m. Same plan DK0 CREW / DK+1 PAX. */
export const CABIN_VEST_D = 0; // unused — doors on fore face
export function modularCabins(deck, prefix, bunkNote, mid = { y0: -STACK, y1: STACK }) {
  const up0 = deck === 0 ? Z_DK0 : Z_DK1;
  const z0 = Z_CREW_FORE + CABIN_WALL_T / 2;
  const z1 = Z_CREW_AFT - CABIN_WALL_T / 2;
  const rooms = [];
  for (let i = 0; i < CREW_CABIN_COUNT; i++) {
    const slot0 = mid.y0 + i * CABIN_MODULE_W;
    const y0 = slot0 + CABIN_WALL_T / 2;
    const y1 = slot0 + CABIN_MODULE_W - CABIN_WALL_T / 2;
    const n = i + 1;
    rooms.push({
      id: `${prefix}_${n}`,
      deck,
      z0,
      z1,
      y0,
      y1,
      up0,
      up1: up0 + ROOM_H,
      note: `C0n ${prefix.toLowerCase()} cell ${n}/${CREW_CABIN_COUNT}: clear ${CABIN_CLEAR_W}×${CABIN_CLEAR_D} m; module ${CABIN_MODULE_W} m with walls; entrance fore; ${bunkNote}`,
    });
  }
  return rooms;
}

/** @deprecated */
export function crewCabinsP1(mid = { y0: -STACK + T_BH / 2, y1: STACK - T_BH / 2 }) {
  return modularCabins(1, "PAX", "passenger bunks — same module as crew", mid);
}

/** Hatch openings = holes in bulkheads (clear opening, not symbols). */
export function hatches() {
  const list = [];
  const add = (h) => list.push(h);

  // L-airlocks port/stbd DK0
  for (const side of ["port", "stbd"]) {
    const s = side === "port" ? -1 : 1;
    const yIn = s * AIR_INNER;
    const yOut = s * AIR_OUTER;
    const yMid = s * ((AIR_INNER + AIR_OUTER) / 2);
    add({
      id: `D1-${side[0].toUpperCase()}`,
      deck: 0,
      clearW: DOOR_PASS,
      clearH: DOOR_H_PASS,
      y: yIn,
      z: (Z_CROSS_FORE + Z_CROSS_AFT) / 2,
      up: Z_DK0 + DOOR_H_PASS / 2,
      normal: side === "port" ? "+Y" : "-Y",
      from: "CROSSING",
      to: `AIRLOCK_A_${side}`,
      faces: "into passageway",
    });
    add({
      id: `D2-${side[0].toUpperCase()}`,
      deck: 0,
      clearW: DOOR_PASS,
      clearH: DOOR_H_PASS,
      y: yMid,
      z: Z_CROSS_AFT,
      up: Z_DK0 + DOOR_H_PASS / 2,
      normal: "+Zaft",
      from: `AIRLOCK_A_${side}`,
      to: `AIRLOCK_B_${side}`,
      faces: "intermediate",
    });
    add({
      id: `D3-${side[0].toUpperCase()}`,
      deck: 0,
      clearW: DOOR_PASS,
      clearH: DOOR_H_PASS,
      y: yOut,
      z: Z_CROSS_AFT + L_AIR_JOG / 2,
      up: Z_DK0 + DOOR_H_PASS / 2,
      normal: side === "port" ? "-Y" : "+Y",
      from: `AIRLOCK_B_${side}`,
      to: "SPACE",
      faces: "outer hull",
    });
  }

  // Modular cabins: fore lobby from crossing + vestibule↔corridor + cabin fore entrance
  // (authoritative hatch list is rebuilt by InternalsLockCabinPatch in C#).
  add({
    id: "CAB-P",
    deck: 0,
    clearW: DOOR_PASS,
    clearH: DOOR_H_PASS,
    y: -STACK / 2,
    z: Z_CREW_FORE,
    up: Z_DK0 + DOOR_H_PASS / 2,
    normal: "-Zfore",
    from: "CROSSING",
    to: "VEST_CREW_1",
    faces: "fore",
  });
  add({
    id: "CAB-S",
    deck: 0,
    clearW: DOOR_PASS,
    clearH: DOOR_H_PASS,
    y: STACK / 2,
    z: Z_CREW_FORE,
    up: Z_DK0 + DOOR_H_PASS / 2,
    normal: "-Zfore",
    from: "CROSSING",
    to: "VEST_CREW_1",
    faces: "fore",
  });

  add({
    id: "INF-P",
    deck: 0,
    clearW: DOOR_PASS,
    clearH: DOOR_H_PASS,
    y: -STACK,
    z: (Z_MED_FORE + Z_MED_AFT) / 2,
    up: Z_DK0 + DOOR_H_PASS / 2,
    normal: "-Y",
    from: "INFIRMARY",
    to: "CORR_P",
    faces: "port",
  });
  add({
    id: "GAL-S",
    deck: 0,
    clearW: DOOR_PASS,
    clearH: DOOR_H_PASS,
    y: STACK,
    z: (Z_MED_FORE + Z_MED_AFT) / 2,
    up: Z_DK0 + DOOR_H_PASS / 2,
    normal: "+Y",
    from: "GALLEY",
    to: "CORR_S",
    faces: "starboard",
  });

  // Eng P/S on decks 0 and −1 only
  for (const deck of [0, -1]) {
    const up = (deck === 0 ? Z_DK0 : Z_DK_M1) + DOOR_H_CORR / 2;
    for (const [tag, y, n] of [
      [`ENG-P-DK${deck === 0 ? "0" : "M1"}`, -STACK, "-Y"],
      [`ENG-S-DK${deck === 0 ? "0" : "M1"}`, STACK, "+Y"],
    ]) {
      add({
        id: tag,
        deck,
        clearW: DOOR_CORR,
        clearH: DOOR_H_CORR,
        y,
        z: (Z_ENG_FORE + Z_ENG_AFT) / 2,
        up,
        normal: n,
        from: y < 0 ? "CORR_P" : "CORR_S",
        to: "ENG",
        faces: y < 0 ? "port" : "starboard",
      });
    }
  }

  // Spine → hold
  for (const deck of [-1, 0, 1]) {
    const upBase = deck === -1 ? Z_DK_M1 : deck === 0 ? Z_DK0 : Z_DK1;
    for (const [tag, y] of [
      [`HOLD-P-DK${deck === -1 ? "M1" : deck === 0 ? "0" : "P1"}`, -(CORR_INNER + CORR_W / 2)],
      [`HOLD-S-DK${deck === -1 ? "M1" : deck === 0 ? "0" : "P1"}`, CORR_INNER + CORR_W / 2],
    ]) {
      add({
        id: tag,
        deck,
        clearW: DOOR_CORR,
        clearH: DOOR_H_CORR,
        y,
        z: Z_ENG_AFT,
        up: upBase + DOOR_H_CORR / 2,
        normal: "+Zaft",
        from: y < 0 ? "CORR_P" : "CORR_S",
        to: "HOLD",
        faces: "aft into cargo",
      });
    }
  }

  return list;
}

export function corridorCenterlines() {
  return [
    {
      id: "CORR_P_CL",
      y: -(CORR_INNER + CORR_W / 2),
      z0: Z_CREW_FORE,
      z1: Z_ENG_AFT,
      decks: [-1, 0, 1],
    },
    {
      id: "CORR_S_CL",
      y: CORR_INNER + CORR_W / 2,
      z0: Z_CREW_FORE,
      z1: Z_ENG_AFT,
      decks: [-1, 0, 1],
    },
    {
      id: "CROSS_CL",
      y0: -AIR_INNER,
      y1: AIR_INNER,
      z: (Z_CROSS_FORE + Z_CROSS_AFT) / 2,
      decks: [-1, 0, 1],
    },
  ];
}

export function airlockVolumes() {
  const out = [];
  for (const side of ["port", "stbd"]) {
    const s = side === "port" ? -1 : 1;
    const y0 = Math.min(s * AIR_INNER, s * AIR_OUTER);
    const y1 = Math.max(s * AIR_INNER, s * AIR_OUTER);
    out.push({
      id: `AIRLOCK_A_${side}`,
      z0: Z_CROSS_FORE,
      z1: Z_CROSS_AFT,
      y0,
      y1,
      up0: Z_DK0,
      up1: Z_DK0 + ROOM_H,
    });
    out.push({
      id: `AIRLOCK_B_${side}`,
      z0: Z_CROSS_AFT,
      z1: Z_CROSS_AFT + L_AIR_JOG,
      y0,
      y1,
      up0: Z_DK0,
      up1: Z_DK0 + ROOM_H,
    });
  }
  return out.map(withPlanRing);
}

/** Run fabrication asserts; throws on failure. */
export function assertLock() {
  const errors = [];
  if (Math.abs(LOA - (L_FORE + L_MID + L_AFT)) > 1e-9) errors.push("LOA ≠ L_fore+L_mid+L_aft");
  if (CORR_W + 1e-9 < 3) errors.push("corridor clear < 3 m (CAL-INT-DK-001)");
  if (Math.abs(CABIN_CLEAR_D - 7.2) > 1e-6) errors.push("cabin clear F–A != 7.2");
  if (Math.abs(CABIN_CLEAR_W - 1.92) > 1e-6) errors.push("cabin clear W != 1.92");
  if (Math.abs(Z_CREW_AFT - 30.28) > 1e-6) errors.push("Z_CREW_AFT != 30.28");
  if (Math.abs(HOLD_L - (CATWALK_D + GAP_CATWALK_C40 + C40_L + RAMP_GAP)) > 1e-9)
    errors.push("hold pack length mismatch");
  if (Math.abs(AIR_OUTER - BEAM / 2) > 1e-9) errors.push("D3 not on outer hull");
  if (AIR_INNER + AIR_W - AIR_OUTER > 1e-9 || AIR_INNER + AIR_W - AIR_OUTER < -1e-9)
    errors.push("airlock width does not reach shell");
  for (const h of hatches()) {
    if (h.id.startsWith("D") || h.id.startsWith("CAB") || h.id.startsWith("INF") || h.id.startsWith("GAL")) {
      if (h.clearW + 1e-9 < DOOR_PASS) errors.push(`${h.id} clearW < DOOR_PASS`);
    }
    if (h.id.startsWith("ENG") || h.id.startsWith("HOLD")) {
      if (h.clearW + 1e-9 < DOOR_CORR) errors.push(`${h.id} clearW < DOOR_CORR`);
    }
    if (h.id.startsWith("D3") && Math.abs(Math.abs(h.y) - AIR_OUTER) > 1e-9)
      errors.push(`${h.id} not on AIR_OUTER`);
  }
  // Eng doors must not exist on +1
  if (hatches().some((h) => h.id.includes("ENG") && h.deck === 1))
    errors.push("eng door on deck +1 forbidden");
  // Midbody compartments inside beam
  for (const c of compartments()) {
    if (c.z0 >= L_FORE && c.z1 <= L_FORE + L_MID) {
      if (c.y0 < -BEAM / 2 - 1e-6 || c.y1 > BEAM / 2 + 1e-6)
        errors.push(`${c.id} outside beam`);
    }
  }
  if (errors.length) throw new Error("calypso-lock asserts failed:\n" + errors.join("\n"));
  return true;
}

export const envelope = {
  LOA,
  BEAM,
  OAH,
  L_FORE,
  L_MID,
  L_AFT,
  CHAMFER,
  T_SHELL,
  MATERIAL,
  PAD,
  note: "Midbody stretch for internals pack (was 65 m outer-only lock)",
};
