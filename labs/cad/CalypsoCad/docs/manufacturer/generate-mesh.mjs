/**
 * Mesh blueprint export — envelope from ../internals/calypso-lock.mjs
 * Origo (0,0,0): ventral-aft-port, slightly outside the hull AABB.
 *
 * Run: node d:\novolis\novolis-lab\labs\cad\CalypsoCad\docs\manufacturer\generate-mesh.mjs
 */
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import {
  nice,
  fmt,
  LOA,
  BEAM,
  OAH,
  L_FORE,
  L_AFT,
  L_MID,
  CHAMFER,
  C40_L,
  C40_W,
  C40_H,
  CELL,
  COLS,
  TIERS,
  CLEAR_DOOR as CLEAR,
  PACK_W,
  PACK_H,
  DOOR_W,
  DOOR_H,
  SILL,
  PAD,
  FORE_STATIONS as FORE_STATIONS_RAW,
} from "../internals/calypso-lock.mjs";

const __dirname = path.dirname(fileURLToPath(import.meta.url));

const r3 = (m) => Math.round(m * 1000) / 1000;

// ─── Hull lock (shared with internals) ───────────────────────────────────────
const DRAWING = "CAL-HULL-MESH-001";
const REV = "B";

const FORE_STATIONS = FORE_STATIONS_RAW.map((s) => ({
  id: s.id,
  zFromStem: s.z,
  halfBeam: s.halfBeam,
  halfHeight: s.halfHeight,
  mark: s.mark,
}));

/** Extra parallel rings (same section as ST3). */
const MID_RINGS = [
  { id: "MB0", zFromStem: L_FORE, mark: "midbody fore (=ST3)" },
  { id: "MB1", zFromStem: L_FORE + L_MID / 2, mark: "midbody mid" },
  { id: "MB2", zFromStem: L_FORE + L_MID, mark: "midbody aft / aft-band fore" },
  { id: "AFT", zFromStem: LOA, mark: "aft face outer" },
];

const VERT_NAMES = [
  "D-P", // 0 dorsal port flat corner
  "D-S", // 1 dorsal stbd
  "S-U", // 2 stbd upper
  "S-L", // 3 stbd lower
  "V-S", // 4 ventral stbd
  "V-P", // 5 ventral port
  "P-L", // 6 port lower
  "P-U", // 7 port upper
];

function stationChamfer(halfBeam, halfHeight) {
  const kx = (halfBeam * 2) / BEAM;
  const ky = (halfHeight * 2) / OAH;
  return {
    cx: Math.min(CHAMFER * kx, halfBeam * 0.45),
    cy: Math.min(CHAMFER * ky, halfHeight * 0.45),
  };
}

/** Local Y (stbd+), Z (up+) relative to ship CL / mid-height. */
function octagonLocal(halfBeam, halfHeight) {
  const { cx, cy } = stationChamfer(halfBeam, halfHeight);
  return [
    { name: VERT_NAMES[0], y: -halfBeam + cx, z: +halfHeight },
    { name: VERT_NAMES[1], y: +halfBeam - cx, z: +halfHeight },
    { name: VERT_NAMES[2], y: +halfBeam, z: +halfHeight - cy },
    { name: VERT_NAMES[3], y: +halfBeam, z: -halfHeight + cy },
    { name: VERT_NAMES[4], y: +halfBeam - cx, z: -halfHeight },
    { name: VERT_NAMES[5], y: -halfBeam + cx, z: -halfHeight },
    { name: VERT_NAMES[6], y: -halfBeam, z: -halfHeight + cy },
    { name: VERT_NAMES[7], y: -halfBeam, z: +halfHeight - cy },
  ];
}

/** Stem zFromStem → mesh X (forward from origo). */
function xFromStem(zFromStem) {
  return r3(PAD + (LOA - zFromStem));
}

function worldFromLocal(zFromStem, yLocal, zLocal) {
  return {
    x: xFromStem(zFromStem),
    y: r3(PAD + BEAM / 2 + yLocal),
    z: r3(PAD + OAH / 2 + zLocal),
  };
}

const points = [];
const pointIndex = new Map();

function addPoint(id, x, y, z, group, note = "") {
  if (pointIndex.has(id)) return pointIndex.get(id);
  const p = { id, x: r3(x), y: r3(y), z: r3(z), group, note };
  pointIndex.set(id, points.length);
  points.push(p);
  return points.length - 1;
}

// Origo
addPoint("O", 0, 0, 0, "origo", "ventral-aft-port, outside hull AABB");

// Envelope AABB corners (outer box before chamfer cut) for reference
const aabb = [
  ["AABB-AFT-P-V", PAD, PAD, PAD, "aft port ventral"],
  ["AABB-AFT-S-V", PAD, PAD + BEAM, PAD, "aft stbd ventral"],
  ["AABB-AFT-P-D", PAD, PAD, PAD + OAH, "aft port dorsal"],
  ["AABB-AFT-S-D", PAD, PAD + BEAM, PAD + OAH, "aft stbd dorsal"],
  ["AABB-BOW-P-V", PAD + LOA, PAD, PAD, "bow port ventral"],
  ["AABB-BOW-S-V", PAD + LOA, PAD + BEAM, PAD, "bow stbd ventral"],
  ["AABB-BOW-P-D", PAD + LOA, PAD, PAD + OAH, "bow port dorsal"],
  ["AABB-BOW-S-D", PAD + LOA, PAD + BEAM, PAD + OAH, "bow stbd dorsal"],
];
for (const [id, x, y, z, note] of aabb) {
  addPoint(id, x, y, z, "aabb", note);
}

function addRing(ringId, zFromStem, halfBeam, halfHeight, group, mark) {
  const locals = octagonLocal(halfBeam, halfHeight);
  const ids = [];
  for (const v of locals) {
    const w = worldFromLocal(zFromStem, v.y, v.z);
    const id = `${ringId}.${v.name}`;
    addPoint(id, w.x, w.y, w.z, group, `${mark} · ${v.name}`);
    ids.push(id);
  }
  return ids;
}

const rings = [];

for (const s of FORE_STATIONS) {
  const ids = addRing(s.id, s.zFromStem, s.halfBeam, s.halfHeight, "fore", s.mark);
  rings.push({ id: s.id, zFromStem: s.zFromStem, x: xFromStem(s.zFromStem), vertIds: ids, group: "fore" });
}

for (const m of MID_RINGS) {
  if (m.id === "MB0") continue; // same verts as ST3 — reuse connectivity, skip duplicate points
  const ids = addRing(m.id, m.zFromStem, BEAM / 2, OAH / 2, m.id === "AFT" ? "aft" : "midbody", m.mark);
  rings.push({ id: m.id, zFromStem: m.zFromStem, x: xFromStem(m.zFromStem), vertIds: ids, group: m.id === "AFT" ? "aft" : "midbody" });
}

// ST3 already added; insert logical ring pointing at ST3 verts for midbody loft
const st3 = rings.find((r) => r.id === "ST3");
rings.splice(4, 0, {
  id: "MB0",
  zFromStem: L_FORE,
  x: st3.x,
  vertIds: st3.vertIds,
  group: "midbody",
  aliasOf: "ST3",
});

// Door clear opening on aft face (X = PAD)
const doorX = PAD;
const doorY0 = PAD + BEAM / 2 - DOOR_W / 2;
const doorY1 = PAD + BEAM / 2 + DOOR_W / 2;
const doorZ0 = PAD + SILL;
const doorZ1 = PAD + SILL + DOOR_H;
const doorPts = [
  ["DOOR.P-V", doorX, doorY0, doorZ0, "door port sill"],
  ["DOOR.S-V", doorX, doorY1, doorZ0, "door stbd sill"],
  ["DOOR.S-D", doorX, doorY1, doorZ1, "door stbd lintel"],
  ["DOOR.P-D", doorX, doorY0, doorZ1, "door port lintel"],
];
for (const [id, x, y, z, note] of doorPts) {
  addPoint(id, x, y, z, "door", note);
}

// Pack footprint corners on aft plane (for clearance check)
const packY0 = PAD + BEAM / 2 - PACK_W / 2;
const packY1 = PAD + BEAM / 2 + PACK_W / 2;
const packZ0 = doorZ0;
const packZ1 = doorZ0 + PACK_H;
for (const [id, y, z, note] of [
  ["PACK.P-V", packY0, packZ0, "C40 pack port sill"],
  ["PACK.S-V", packY1, packZ0, "C40 pack stbd sill"],
  ["PACK.S-D", packY1, packZ1, "C40 pack stbd top"],
  ["PACK.P-D", packY0, packZ1, "C40 pack port top"],
]) {
  addPoint(id, doorX, y, z, "pack", note);
}

// Edges: ring loops + longitudinal between consecutive rings
const edges = [];
function addEdge(a, b, group) {
  edges.push({ a, b, group });
}

for (const ring of rings) {
  if (ring.aliasOf) continue; // don't double-loop ST3/MB0
  for (let i = 0; i < 8; i++) {
    addEdge(ring.vertIds[i], ring.vertIds[(i + 1) % 8], `ring:${ring.id}`);
  }
}

const loftOrder = ["ST0", "ST1", "ST2", "ST3", "MB1", "MB2", "AFT"];
for (let r = 0; r < loftOrder.length - 1; r++) {
  const a = rings.find((x) => x.id === loftOrder[r]);
  const b = rings.find((x) => x.id === loftOrder[r + 1]);
  for (let i = 0; i < 8; i++) {
    addEdge(a.vertIds[i], b.vertIds[i], `loft:${a.id}->${b.id}`);
  }
}

// Door loop
addEdge("DOOR.P-V", "DOOR.S-V", "door");
addEdge("DOOR.S-V", "DOOR.S-D", "door");
addEdge("DOOR.S-D", "DOOR.P-D", "door");
addEdge("DOOR.P-D", "DOOR.P-V", "door");

// Quad faces between loft rings (for mesh builders)
const faces = [];
for (let r = 0; r < loftOrder.length - 1; r++) {
  const a = rings.find((x) => x.id === loftOrder[r]);
  const b = rings.find((x) => x.id === loftOrder[r + 1]);
  for (let i = 0; i < 8; i++) {
    const j = (i + 1) % 8;
    faces.push({
      id: `F.${a.id}.${VERT_NAMES[i]}`,
      verts: [a.vertIds[i], a.vertIds[j], b.vertIds[j], b.vertIds[i]],
      group: `loft:${a.id}->${b.id}`,
    });
  }
}

// Stem cap (ST0 loop as face) + aft outer (AFT loop) — door is a hole note
faces.push({ id: "F.STEM", verts: rings.find((x) => x.id === "ST0").vertIds.slice(), group: "cap:stem" });
faces.push({
  id: "F.AFT_OUTER",
  verts: rings.find((x) => x.id === "AFT").vertIds.slice(),
  group: "cap:aft",
  hole: ["DOOR.P-V", "DOOR.S-V", "DOOR.S-D", "DOOR.P-D"],
});

const json = {
  drawing: DRAWING,
  rev: REV,
  units: "meters",
  coordinateSystem: {
    origo: {
      id: "O",
      xyz: [0, 0, 0],
      description:
        "Ventral-aft-port of outer AABB, offset PAD outside so all hull points are positive",
    },
    pad: PAD,
    axes: {
      X: "+forward (toward stem)",
      Y: "+starboard",
      Z: "+up (dorsal)",
    },
    fromStemConvention:
      "GA canvas uses zFromStem=0 at stem; mesh X = PAD + (LOA - zFromStem)",
  },
  envelope: { LOA, BEAM, OAH, L_FORE, L_MID, L_AFT, CHAMFER, PAD },
  door: { DOOR_W, DOOR_H, SILL, x: doorX },
  rings: rings.map((r) => ({
    id: r.id,
    zFromStem: r.zFromStem,
    x: r.x,
    group: r.group,
    aliasOf: r.aliasOf ?? null,
    verts: r.vertIds,
  })),
  points,
  edges,
  faces,
};

fs.writeFileSync(path.join(__dirname, "CAL-HULL-MESH-001.json"), JSON.stringify(json, null, 2), "utf8");

// ─── SVG blueprint helpers ───────────────────────────────────────────────────
const ink = "#1a1a1a";
const mute = "#555";
const dim = "#888";
const accent = "#0b5fff";
const plate = "#e8e8e8";
const portC = "#c23b22";
const stbdC = "#0b5fff";
const ventC = "#2a7a3a";

function projectPlot(opts) {
  const { title, mapXY, width = 720, height = 420, showOrigo = true, showLabels = true } = opts;
  const hullPts = points.filter((p) => ["fore", "midbody", "aft", "door", "pack"].includes(p.group));
  const mapped = hullPts.map((p) => {
    const [u, v] = mapXY(p);
    return { ...p, u, v };
  });
  const origoUV = mapXY({ x: 0, y: 0, z: 0 });
  let minU = Infinity,
    maxU = -Infinity,
    minV = Infinity,
    maxV = -Infinity;
  for (const p of mapped) {
    minU = Math.min(minU, p.u);
    maxU = Math.max(maxU, p.u);
    minV = Math.min(minV, p.v);
    maxV = Math.max(maxV, p.v);
  }
  minU = Math.min(minU, origoUV[0]);
  maxU = Math.max(maxU, origoUV[0]);
  minV = Math.min(minV, origoUV[1]);
  maxV = Math.max(maxV, origoUV[1]);
  const padPx = 48;
  const spanU = Math.max(maxU - minU, 1e-6);
  const spanV = Math.max(maxV - minV, 1e-6);
  const sx = (width - padPx * 2) / spanU;
  const sy = (height - padPx * 2) / spanV;
  const s = Math.min(sx, sy);
  const ox = padPx - minU * s;
  const oy = height - padPx + minV * s; // flip V so +v draws up if v increases "up" on page
  // For plan: v = x (forward) should go up on page → we want larger x higher on SVG = smaller svgY
  // map: svgX = ox + u*s, svgY = oy - v*s  with oy = pad + maxV*s
  const ox2 = padPx - minU * s + ((width - padPx * 2 - spanU * s) / 2);
  const oy2 = padPx + maxV * s + ((height - padPx * 2 - spanV * s) / 2);
  const toSvg = (u, v) => [ox2 + u * s, oy2 - v * s];

  // Draw loft edges that are in this projection
  const edgeSet = loftOrder;
  let edgePaths = "";
  for (let r = 0; r < edgeSet.length; r++) {
    const ring = rings.find((x) => x.id === edgeSet[r]);
    const pts2 = ring.vertIds.map((id) => {
      const p = points[pointIndex.get(id)];
      const [u, v] = mapXY(p);
      return toSvg(u, v);
    });
    edgePaths += `<polygon points="${pts2.map(([x, y]) => `${x},${y}`).join(" ")}" fill="none" stroke="${ink}" stroke-width="1.2"/>`;
  }
  // longitudinal sample: D-P and V-P lines
  for (const corner of ["D-P", "D-S", "V-P", "V-S"]) {
    const pathPts = loftOrder.map((rid) => {
      const ring = rings.find((x) => x.id === rid);
      const id = ring.vertIds.find((vid) => vid.endsWith(`.${corner}`));
      const p = points[pointIndex.get(id)];
      return toSvg(...mapXY(p));
    });
    edgePaths += `<polyline points="${pathPts.map(([x, y]) => `${x},${y}`).join(" ")}" fill="none" stroke="${mute}" stroke-width="1" stroke-dasharray="4 3"/>`;
  }

  let dots = "";
  for (const p of mapped) {
    const [sx_, sy_] = toSvg(p.u, p.v);
    const col =
      p.group === "door" ? accent : p.group === "pack" ? ventC : p.group === "fore" ? portC : ink;
    dots += `<circle cx="${sx_}" cy="${sy_}" r="2.4" fill="${col}"/>`;
  }
  const [oxS, oyS] = toSvg(origoUV[0], origoUV[1]);
  if (showOrigo) {
    dots += `<circle cx="${oxS}" cy="${oyS}" r="5" fill="none" stroke="${accent}" stroke-width="2"/>
      <line x1="${oxS - 8}" y1="${oyS}" x2="${oxS + 8}" y2="${oyS}" stroke="${accent}" stroke-width="1.5"/>
      <line x1="${oxS}" y1="${oyS - 8}" x2="${oxS}" y2="${oyS + 8}" stroke="${accent}" stroke-width="1.5"/>
      <text x="${oxS + 10}" y="${oyS - 8}" font-size="11" fill="${accent}">O (0,0,0)</text>`;
  }

  // Label ring X or key corners
  let labels = "";
  if (showLabels) {
    for (const rid of ["ST0", "ST3", "AFT"]) {
      const ring = rings.find((x) => x.id === rid);
      const p = points[pointIndex.get(ring.vertIds[0])];
      const [lx, ly] = toSvg(...mapXY(p));
      labels += `<text x="${lx + 4}" y="${ly - 4}" font-size="9" fill="${mute}">${rid}</text>`;
    }
  }

  return `<svg viewBox="0 0 ${width} ${height}" xmlns="http://www.w3.org/2000/svg">
  <rect x="8" y="8" width="${width - 16}" height="${height - 16}" fill="none" stroke="#ccc"/>
  <text x="20" y="28" font-size="13" fill="${ink}">${title}</text>
  <rect x="16" y="16" width="${width - 32}" height="${height - 32}" fill="${plate}" opacity="0.35"/>
  ${edgePaths}
  ${dots}
  ${labels}
</svg>`;
}

const planSvg = projectPlot({
  title: "PLAN (XY) · +X forward up-page · +Y stbd right · origo aft-port",
  mapXY: (p) => [p.y, p.x],
  width: 520,
  height: 720,
});

const profileSvg = projectPlot({
  title: "PROFILE (XZ) · +X forward right · +Z up · looking to port (stbd hidden)",
  mapXY: (p) => [p.x, p.z],
  width: 900,
  height: 360,
});

const aftSvg = projectPlot({
  title: "AFT-ON (YZ) @ X≈PAD · +Y stbd right · +Z up · looking forward",
  mapXY: (p) => [p.y, p.z],
  width: 520,
  height: 420,
  // only aft-ish points still plotted via full set — OK for nest
});

function table(headers, rows) {
  return `<table><thead><tr>${headers.map((h) => `<th>${h}</th>`).join("")}</tr></thead><tbody>${rows
    .map((r) => `<tr>${r.map((c) => `<td>${c}</td>`).join("")}</tr>`)
    .join("")}</tbody></table>`;
}

const pointRows = points.map((p) => [
  p.id,
  p.x.toFixed(3),
  p.y.toFixed(3),
  p.z.toFixed(3),
  p.group,
  p.note,
]);

const html = `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8"/>
  <title>${DRAWING} Rev ${REV} — Mesh point blueprints</title>
  <style>
    body{margin:0;padding:24px;font:12px/1.45 "Segoe UI",system-ui,sans-serif;color:${ink};background:#fff;max-width:1100px}
    h1{font-size:18px;margin:0 0 6px}
    h2{font-size:15px;margin:26px 0 10px;border-bottom:1px solid #ccc;padding-bottom:4px}
    h3{font-size:12px;margin:0 0 8px}
    .meta{color:${dim};font-size:11px}
    .lock{border:1px solid #9ab;background:#f0f6ff;padding:10px 12px;margin:12px 0}
    .axes{display:grid;grid-template-columns:1fr 1fr;gap:12px;margin:12px 0}
    .box{border:1px solid #ccc;padding:10px;background:#fafafa}
    .pills{display:flex;flex-wrap:wrap;gap:6px;margin:10px 0}
    .pill{border:1px solid #bbb;padding:3px 8px;background:#f5f5f5;font-size:11px}
    table{width:100%;border-collapse:collapse;font-size:10.5px;margin:0 0 14px}
    th,td{border:1px solid #ccc;padding:4px 6px;text-align:left;font-variant-numeric:tabular-nums}
    th{background:#f0f0f0;position:sticky;top:0}
    .view{border:1px solid #ccc;padding:8px;margin:0 0 14px;page-break-inside:avoid}
    .grid2{display:grid;grid-template-columns:1fr 1fr;gap:12px}
    svg{display:block;width:100%;height:auto}
    code{font-size:11px}
    @media print{body{padding:10mm;max-width:none}}
  </style>
</head>
<body>
  <h1>${DRAWING} Rev ${REV} — Detailed mesh blueprints</h1>
  <p class="meta">Same hull math as <code>CAL-HULL-GA-001</code> / <code>calypso-hull-manufacturer.canvas.tsx</code>.
  Machine file: <code>CAL-HULL-MESH-001.json</code>. Regenerate: <code>node generate-mesh.mjs</code>.</p>

  <div class="pills">
    <span class="pill">LOA ${fmt(LOA)}</span>
    <span class="pill">Beam ${fmt(BEAM)}</span>
    <span class="pill">OAH ${fmt(OAH)}</span>
    <span class="pill">PAD ${fmt(PAD)}</span>
    <span class="pill">${points.length} points</span>
    <span class="pill">${edges.length} edges</span>
    <span class="pill">${faces.length} faces</span>
  </div>

  <div class="lock">
    <strong>Origo (0,0,0)</strong> = ventral · aft · port of the outer AABB, offset
    <strong>PAD = ${fmt(PAD)}</strong> behind the aft face, below the keel, and to port of the port shell
    so every hull mesh point has <code>X,Y,Z ≥ ${PAD}</code>.
    <br/>Axes: <strong>+X</strong> forward (stem) · <strong>+Y</strong> starboard · <strong>+Z</strong> up (dorsal).
    <br/>GA stem station <code>zFromStem=0</code> maps to mesh <code>X = PAD + LOA</code>; aft face at <code>X = PAD</code>.
  </div>

  <div class="axes">
    <div class="box">
      <h3>AABB in mesh frame</h3>
      <p>Hull outer box (before chamfer):</p>
      <ul>
        <li>X ∈ [${PAD}, ${PAD + LOA}] = [${nice(PAD)}, ${nice(PAD + LOA)}]</li>
        <li>Y ∈ [${PAD}, ${PAD + BEAM}] = [${nice(PAD)}, ${nice(PAD + BEAM)}]</li>
        <li>Z ∈ [${PAD}, ${PAD + OAH}] = [${nice(PAD)}, ${nice(PAD + OAH)}]</li>
      </ul>
      <p class="meta">Aft-port-ventral corner of AABB = (${nice(PAD)}, ${nice(PAD)}, ${nice(PAD)}) — not the origo.</p>
    </div>
    <div class="box">
      <h3>Octagon vertex order (each ring)</h3>
      <ol>
        <li><code>D-P</code> dorsal port flat corner</li>
        <li><code>D-S</code> dorsal stbd</li>
        <li><code>S-U</code> stbd upper</li>
        <li><code>S-L</code> stbd lower</li>
        <li><code>V-S</code> ventral stbd</li>
        <li><code>V-P</code> ventral port</li>
        <li><code>P-L</code> port lower</li>
        <li><code>P-U</code> port upper</li>
      </ol>
      <p class="meta">Chamfer ${fmt(CHAMFER)} clamped per station (same as GA).</p>
    </div>
  </div>

  <h2>Ring schedule (loft spine)</h2>
  ${table(
    ["Ring", "zFromStem (GA)", "mesh X", "section B×H", "group"],
    [
      ...FORE_STATIONS.map((s) => [
        s.id,
        nice(s.zFromStem),
        nice(xFromStem(s.zFromStem)),
        `${nice(s.halfBeam * 2)}×${nice(s.halfHeight * 2)}`,
        "fore",
      ]),
      ["MB1", nice(L_FORE + L_MID / 2), nice(xFromStem(L_FORE + L_MID / 2)), `${BEAM}×${OAH}`, "midbody"],
      ["MB2", nice(L_FORE + L_MID), nice(xFromStem(L_FORE + L_MID)), `${BEAM}×${OAH}`, "midbody"],
      ["AFT", nice(LOA), nice(xFromStem(LOA)), `${BEAM}×${OAH}`, "aft + door"],
    ],
  )}

  <h2>Blueprint views (points plotted)</h2>
  <div class="view"><h3>1 · Plan</h3>${planSvg}</div>
  <div class="view"><h3>2 · Profile</h3>${profileSvg}</div>
  <div class="view"><h3>3 · Aft-on / section nest</h3>${aftSvg}</div>

  <h2>Door + pack on aft plane (X = ${nice(doorX)})</h2>
  ${table(
    ["Id", "X", "Y", "Z", "Notes"],
    [...doorPts, ...[
      ["PACK.P-V", doorX, packY0, packZ0, "C40 pack port sill"],
      ["PACK.S-V", doorX, packY1, packZ0, "C40 pack stbd sill"],
      ["PACK.S-D", doorX, packY1, packZ1, "C40 pack stbd top"],
      ["PACK.P-D", doorX, packY0, packZ1, "C40 pack port top"],
    ]].map(([id, x, y, z, note]) => [id, nice(x), nice(y), nice(z), note]),
  )}

  <h2>All mesh points</h2>
  <p class="meta">${points.length} vertices. Use JSON for import. Coordinates meters, 3 dp.</p>
  ${table(["Id", "X", "Y", "Z", "Group", "Notes"], pointRows)}

  <h2>Loft connectivity</h2>
  <p>Longitudinal loft order: <code>${loftOrder.join(" → ")}</code>. Each step: 8 quads (one per octagon edge). Stem cap = ST0 loop. Aft cap = AFT loop with door hole.</p>
  <p class="meta">${faces.length} faces · ${edges.length} edges — full lists in <code>CAL-HULL-MESH-001.json</code>.</p>

  <h2>Manufacturer notes</h2>
  <ol>
    <li>Build the outer shell by lofting octagon rings ST0→ST1→ST2→ST3→MB1→MB2→AFT with the listed quads.</li>
    <li>Cut the aft door opening using DOOR.* rectangle on the AFT plane; keep ≥0.50 m clear around the C40 pack (PACK.*).</li>
    <li>Origo is intentionally outside the ship — do not place structure at (0,0,0).</li>
    <li>Keep PAD synchronized if you change the GA envelope; regenerate this file from <code>generate-mesh.mjs</code>.</li>
  </ol>
</body>
</html>
`;

fs.writeFileSync(path.join(__dirname, "CAL-HULL-MESH-001.html"), html, "utf8");
console.log(`Wrote CAL-HULL-MESH-001.html + .json`);
console.log(`points=${points.length} edges=${edges.length} faces=${faces.length} PAD=${PAD}`);
console.log(`AABB aft-port-ventral=(${PAD},${PAD},${PAD})  stem X=${PAD + LOA}`);
