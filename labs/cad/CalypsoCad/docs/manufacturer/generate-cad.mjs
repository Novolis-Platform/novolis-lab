/**
 * CAD package — outer/inner mold lines, 316L plate, no bend (faceted weldment).
 * Envelope from ../internals/calypso-lock.mjs (LOA mid-stretch for internals).
 *
 * Run: node d:\novolis\novolis-lab\labs\cad\CalypsoCad\docs\manufacturer\generate-cad.mjs
 */
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import {
  nice,
  fmt,
  ceilQuarter,
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
  T_SHELL,
  APRON_D,
  FORE_STATIONS as FORE_STATIONS_RAW,
} from "../internals/calypso-lock.mjs";

const __dirname = path.dirname(fileURLToPath(import.meta.url));

const r3 = (m) => Math.round(m * 1000) / 1000;
const r6 = (m) => Math.round(m * 1e6) / 1e6;

// ─── Envelope (shared lock) ──────────────────────────────────────────────────
const DRAWING = "CAL-HULL-CAD-001";
const REV = "B";

/**
 * Plate process: flat sheet only — NO press brake / roll / brake-form.
 * Faceted pepakura outer: cut → fit → butt/lap weld → grind.
 */
const MATERIAL = {
  designation: "AISI 316L",
  uns: "S31603",
  en: "1.4404 / X2CrNiMo17-12-2",
  product: "Annealed flat plate / sheet (cut to facet nets)",
  form: "flat only — no bending, no roll-forming",
  density_kg_m3: 8000,
  E_GPa: 193,
  nu: 0.29,
  Sy_MPa: 170, // typical annealed 0.2% proof (design; confirm mill cert)
  Su_MPa: 485, // typical UTS annealed
  CTE_1e6_K: 16.0,
  melt_C: 1375,
  weld: {
    process: "GTAW / GMAW per ECSS-Q-ST-70-39 class welding (or NASA-STD-5006 analogue)",
    filler: "ER316L / ISO 14343 19 12 3 L",
    condition: "as-welded (L-grade; no PWHT required for sensitization control)",
    notes: "Avoid unstabilized 316 (C>0.03%). Control ferrite in deposit to limit hot cracking.",
  },
  space: {
    spacematdb: "AISI 316L — space experience: Good",
    vacuum: "No atmospheric corrosion in vacuum; oxide does not self-heal if abraded",
    coldWeld: "Risk of cold welding metal-to-metal in vacuum — separate faying surfaces / coatings at mechanisms",
    whyNot301: "301 is for cold-worked formed sheet; this hull is explicitly not bent",
    whyNotCarbon: "Carbon/HSLA needs coatings and has poor vacuum/outgas + corrosion story for long dwell",
    whyNotTi: "Ti-6Al-4V better specific strength but costly/slow for large flat pepakura sheet weldments",
    why316Lvs304L:
      "Both L-grades weld as-welded; 316L Mo improves ground-assembly / hangar pitting margin and is the usual structural plate pick in SPACEMATDB-class lists",
  },
  stock: {
    sheet_m: [2.0, 6.0],
    thickness_m: T_SHELL, // 8 mm — outer skin
    thickness_mm: T_SHELL * 1000,
    tolerance_mm: "±0.3 plate thickness (EN 10029/ASTM A480 class; confirm PO)",
  },
};

/** Outer skin thickness (OML → IML). */
const T = MATERIAL.stock.thickness_m;

const FORE_STATIONS = FORE_STATIONS_RAW.map((s) => ({
  id: s.id,
  zFromStem: s.z,
  halfBeam: s.halfBeam,
  halfHeight: s.halfHeight,
  mark: s.mark,
}));

const LOFT_EXTRA = [
  { id: "MB1", zFromStem: L_FORE + L_MID / 2, halfBeam: BEAM / 2, halfHeight: OAH / 2 },
  { id: "MB2", zFromStem: L_FORE + L_MID, halfBeam: BEAM / 2, halfHeight: OAH / 2 },
  { id: "AFT", zFromStem: LOA, halfBeam: BEAM / 2, halfHeight: OAH / 2 },
];

const VERT_NAMES = ["D-P", "D-S", "S-U", "S-L", "V-S", "V-P", "P-L", "P-U"];

function stationChamfer(halfBeam, halfHeight, chamfer = CHAMFER) {
  const kx = (halfBeam * 2) / BEAM;
  const ky = (halfHeight * 2) / OAH;
  return {
    cx: Math.min(chamfer * kx, halfBeam * 0.45),
    cy: Math.min(chamfer * ky, halfHeight * 0.45),
  };
}

/** Section locals relative to CL / mid-height. */
function octagonLocal(halfBeam, halfHeight, chamfer = CHAMFER) {
  const { cx, cy } = stationChamfer(halfBeam, halfHeight, chamfer);
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

/**
 * Inward offset for faceted shell: shrink half-extents by t and chamfer by t.
 * Valid while halfBeam > t and halfHeight > t (stem still OK at 1.75 > 0.008).
 */
function innerSection(halfBeam, halfHeight) {
  return {
    halfBeam: halfBeam - T,
    halfHeight: halfHeight - T,
    chamfer: Math.max(0, CHAMFER - T),
  };
}

function xOmlFromStem(zFromStem) {
  return r3(PAD + (LOA - zFromStem));
}
/** IML longitudinal: aft face moves forward (+X) by T; stem moves aft (−X) by T. */
function xImlFromStem(zFromStem) {
  if (zFromStem <= 0) return r3(PAD + LOA - T); // stem inner
  if (zFromStem >= LOA) return r3(PAD + T); // aft inner
  return xOmlFromStem(zFromStem); // parallel midbody rings — same X (thickness is in YZ)
}

function worldYZ(yLocal, zLocal) {
  return {
    y: r3(PAD + BEAM / 2 + yLocal),
    z: r3(PAD + OAH / 2 + zLocal),
  };
}

const points = [];
const idx = new Map();
function addPoint(id, x, y, z, group, note = "") {
  if (idx.has(id)) return idx.get(id);
  const i = points.length;
  idx.set(id, i);
  points.push({ id, x: r3(x), y: r3(y), z: r3(z), group, note });
  return i;
}

addPoint("O", 0, 0, 0, "origo", "ventral-aft-port outside AABB");

const loftDefs = [
  ...FORE_STATIONS.map((s) => ({ ...s })),
  ...LOFT_EXTRA,
];

function addShellRing(shell, def) {
  const isIml = shell === "IML";
  const sec = isIml ? innerSection(def.halfBeam, def.halfHeight) : null;
  const hb = isIml ? sec.halfBeam : def.halfBeam;
  const hh = isIml ? sec.halfHeight : def.halfHeight;
  const ch = isIml ? sec.chamfer : CHAMFER;
  const x = isIml ? xImlFromStem(def.zFromStem) : xOmlFromStem(def.zFromStem);
  const locals = octagonLocal(hb, hh, ch);
  const vertIds = [];
  for (const v of locals) {
    const yz = worldYZ(v.y, v.z);
    const id = `${shell}.${def.id}.${v.name}`;
    addPoint(id, x, yz.y, yz.z, shell === "OML" ? "oml" : "iml", `${def.mark ?? def.id} ${shell}`);
    vertIds.push(id);
  }
  return { id: def.id, shell, x, zFromStem: def.zFromStem, vertIds, halfBeam: hb, halfHeight: hh };
}

const omlRings = loftDefs.map((d) => addShellRing("OML", d));
const imlRings = loftDefs.map((d) => addShellRing("IML", d));

// Door on OML aft plane; IML door recess
const doorXOml = PAD;
const doorXIml = PAD + T;
function addDoor(shell, x) {
  const y0 = PAD + BEAM / 2 - DOOR_W / 2;
  const y1 = PAD + BEAM / 2 + DOOR_W / 2;
  // Clear opening measured to finished opening — sill/header from OML keel
  const z0 = PAD + SILL;
  const z1 = PAD + SILL + DOOR_H;
  const ids = [
    [`${shell}.DOOR.P-V`, x, y0, z0],
    [`${shell}.DOOR.S-V`, x, y1, z0],
    [`${shell}.DOOR.S-D`, x, y1, z1],
    [`${shell}.DOOR.P-D`, x, y0, z1],
  ];
  for (const [id, X, y, z] of ids) {
    addPoint(id, X, y, z, "door", `${shell} door clear`);
  }
  return ids.map((a) => a[0]);
}
const doorOml = addDoor("OML", doorXOml);
const doorIml = addDoor("IML", doorXIml);

// Edges + faces for OML loft (primary CAD skin)
const loftOrder = loftDefs.map((d) => d.id);
const edges = [];
const faces = [];
function ringById(rings, id) {
  return rings.find((r) => r.id === id);
}
for (const ring of omlRings) {
  for (let i = 0; i < 8; i++) {
    edges.push({ a: ring.vertIds[i], b: ring.vertIds[(i + 1) % 8], group: `oml-ring:${ring.id}` });
  }
}
for (let r = 0; r < loftOrder.length - 1; r++) {
  const a = ringById(omlRings, loftOrder[r]);
  const b = ringById(omlRings, loftOrder[r + 1]);
  for (let i = 0; i < 8; i++) {
    const j = (i + 1) % 8;
    edges.push({ a: a.vertIds[i], b: b.vertIds[i], group: `oml-loft:${a.id}->${b.id}` });
    faces.push({
      id: `OML.F.${a.id}.${VERT_NAMES[i]}`,
      shell: "OML",
      verts: [a.vertIds[i], a.vertIds[j], b.vertIds[j], b.vertIds[i]],
      group: `loft:${a.id}->${b.id}`,
    });
  }
}
// Thickness ribs: OML↔IML at each ring vertex (for solid visualization)
for (const o of omlRings) {
  const inn = ringById(imlRings, o.id);
  for (let i = 0; i < 8; i++) {
    edges.push({ a: o.vertIds[i], b: inn.vertIds[i], group: `thickness:${o.id}` });
  }
}

function triArea(a, b, c) {
  const ab = [b.x - a.x, b.y - a.y, b.z - a.z];
  const ac = [c.x - a.x, c.y - a.y, c.z - a.z];
  const cx = ab[1] * ac[2] - ab[2] * ac[1];
  const cy = ab[2] * ac[0] - ab[0] * ac[2];
  const cz = ab[0] * ac[1] - ab[1] * ac[0];
  return 0.5 * Math.hypot(cx, cy, cz);
}
function faceArea(vertIds) {
  const pts = vertIds.map((id) => points[idx.get(id)]);
  if (pts.length === 4) return triArea(pts[0], pts[1], pts[2]) + triArea(pts[0], pts[2], pts[3]);
  let a = 0;
  for (let i = 1; i < pts.length - 1; i++) a += triArea(pts[0], pts[i], pts[i + 1]);
  return a;
}

let skinArea = 0;
for (const f of faces) skinArea += faceArea(f.verts);
skinArea = r3(skinArea);
const skinVolume = r6(skinArea * T);
const skinMass_kg = r3(skinVolume * MATERIAL.density_kg_m3);

// OBJ: OML solid-ish as outer shell + IML (two objects)
function writeObj() {
  const omlPts = points.filter((p) => p.group === "oml");
  const lines = [
    `# ${DRAWING} Rev ${REV}`,
    `# AISI 316L t=${T} m flat plate — no bend`,
    `# units: meters`,
    `mtllib ${DRAWING}.mtl`,
    `o Calypso_OML`,
    `usemtl Steel316L`,
  ];
  const remap = new Map();
  omlPts.forEach((p, i) => {
    remap.set(p.id, i + 1);
    lines.push(`v ${p.x} ${p.y} ${p.z}`);
  });
  for (const f of faces) {
    const ids = f.verts.map((id) => remap.get(id));
    if (ids.every(Boolean)) lines.push(`f ${ids.join(" ")}`);
  }
  // IML as second object
  lines.push(`o Calypso_IML`);
  const imlPts = points.filter((p) => p.group === "iml");
  const remapI = new Map();
  const vBase = omlPts.length;
  imlPts.forEach((p, i) => {
    remapI.set(p.id, vBase + i + 1);
    lines.push(`v ${p.x} ${p.y} ${p.z}`);
  });
  for (let r = 0; r < loftOrder.length - 1; r++) {
    const a = ringById(imlRings, loftOrder[r]);
    const b = ringById(imlRings, loftOrder[r + 1]);
    for (let i = 0; i < 8; i++) {
      const j = (i + 1) % 8;
      const ids = [a.vertIds[i], a.vertIds[j], b.vertIds[j], b.vertIds[i]].map((id) => remapI.get(id));
      lines.push(`f ${ids.join(" ")}`);
    }
  }
  return lines.join("\n") + "\n";
}

const mtl = `# ${DRAWING}
newmtl Steel316L
Ka 0.15 0.15 0.16
Kd 0.55 0.56 0.58
Ks 0.35 0.35 0.35
Ns 40
d 1.0
illum 2
# density ${MATERIAL.density_kg_m3} kg/m3 · t=${T} m
`;

const json = {
  drawing: DRAWING,
  rev: REV,
  units: "meters",
  process: {
    construction: "faceted flat-plate weldment (pepakura)",
    bending: false,
    stock_sheet_m: MATERIAL.stock.sheet_m,
    thickness_m: T,
  },
  material: MATERIAL,
  coordinateSystem: {
    origo: [0, 0, 0],
    pad: PAD,
    axes: { X: "+forward", Y: "+starboard", Z: "+up" },
    moldLines: {
      OML: "outer mold line — exterior of 316L skin",
      IML: `inner mold line — OML inset by t=${T} m along section (half-extents − t; aft/stem faces ±t in X)`,
    },
  },
  envelope: { LOA, BEAM, OAH, L_FORE, L_MID, L_AFT, CHAMFER, PAD, T },
  door: { DOOR_W, DOOR_H, SILL, clearIsTo: "finished opening (through OML)" },
  massProperties: {
    outerSkinArea_m2: skinArea,
    thickness_m: T,
    volume_m3: skinVolume,
    mass_kg: skinMass_kg,
    mass_t: r3(skinMass_kg / 1000),
    note: "OML loft facet area × t × density only — excludes frames, door gear, apron, coatings, fasteners",
    arealDensity_kg_m2: r3(T * MATERIAL.density_kg_m3),
  },
  rings: { OML: omlRings, IML: imlRings },
  points,
  edges,
  faces,
  doorVerts: { OML: doorOml, IML: doorIml },
};

fs.writeFileSync(path.join(__dirname, `${DRAWING}.json`), JSON.stringify(json, null, 2));
fs.writeFileSync(path.join(__dirname, `${DRAWING}.obj`), writeObj());
fs.writeFileSync(path.join(__dirname, `${DRAWING}.mtl`), mtl);

// ─── HTML CAD package ────────────────────────────────────────────────────────
const ink = "#1a1a1a";
const mute = "#555";
const accent = "#0b5fff";

function table(headers, rows) {
  return `<table><thead><tr>${headers.map((h) => `<th>${h}</th>`).join("")}</tr></thead><tbody>${rows
    .map((r) => `<tr>${r.map((c) => `<td>${c}</td>`).join("")}</tr>`)
    .join("")}</tbody></table>`;
}

function sectionSvg() {
  // Midbody OML vs IML at ST3 — YZ plot
  const W = 480,
    H = 440;
  const sx = 14,
    sy = 16;
  const ox = 240,
    oy = 220;
  const o = ringById(omlRings, "ST3");
  const inn = ringById(imlRings, "ST3");
  const poly = (ring, stroke, fill, dash) => {
    const pts = ring.vertIds
      .map((id) => {
        const p = points[idx.get(id)];
        return `${ox + (p.y - (PAD + BEAM / 2)) * sx},${oy - (p.z - (PAD + OAH / 2)) * sy}`;
      })
      .join(" ");
    return `<polygon points="${pts}" fill="${fill}" stroke="${stroke}" stroke-width="1.6" ${dash ? 'stroke-dasharray="5 3"' : ""}/>`;
  };
  // thickness callout on dorsal
  const oD = points[idx.get("OML.ST3.D-P")];
  const iD = points[idx.get("IML.ST3.D-P")];
  const x1 = ox + (oD.y - (PAD + BEAM / 2)) * sx;
  const y1 = oy - (oD.z - (PAD + OAH / 2)) * sy;
  const x2 = ox + (iD.y - (PAD + BEAM / 2)) * sx;
  const y2 = oy - (iD.z - (PAD + OAH / 2)) * sy;
  return `<svg viewBox="0 0 ${W} ${H}" xmlns="http://www.w3.org/2000/svg">
  <rect x="8" y="8" width="${W - 16}" height="${H - 16}" fill="none" stroke="#ccc"/>
  <text x="20" y="28" font-size="13" fill="${ink}">MIDBODY SECTION · OML vs IML · t = ${T * 1000} mm</text>
  ${poly(o, ink, "#e8e8e8", false)}
  ${poly(inn, accent, "none", true)}
  <line x1="${x1}" y1="${y1}" x2="${x2}" y2="${y2}" stroke="${accent}" stroke-width="2"/>
  <text x="${(x1 + x2) / 2 + 8}" y="${(y1 + y2) / 2}" font-size="10" fill="${accent}">t ${T * 1000} mm</text>
  <text x="20" y="${H - 36}" font-size="10" fill="${mute}">Solid = OML (exterior) · Dashed = IML (interior of skin)</text>
  <text x="20" y="${H - 20}" font-size="10" fill="${mute}">316L flat plate · chamfer ${CHAMFER} m · no bend</text>
</svg>`;
}

const omlPointRows = points
  .filter((p) => p.group === "oml" || p.group === "iml" || p.id === "O" || p.group === "door")
  .map((p) => [p.id, p.x.toFixed(3), p.y.toFixed(3), p.z.toFixed(3), p.group, p.note]);

const html = `<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8"/>
<title>${DRAWING} Rev ${REV} — CAD / material package</title>
<style>
body{margin:0;padding:24px;font:12px/1.45 "Segoe UI",system-ui,sans-serif;color:${ink};max-width:1100px;background:#fff}
h1{font-size:18px;margin:0 0 6px}
h2{font-size:15px;margin:26px 0 10px;border-bottom:1px solid #ccc;padding-bottom:4px}
h3{font-size:12px;margin:12px 0 6px}
.meta{color:#777;font-size:11px}
.lock{border:1px solid #9ab;background:#f0f6ff;padding:10px 12px;margin:12px 0}
.warn{border:1px solid #c80;background:#fff8e8;padding:10px 12px;margin:12px 0}
.grid2{display:grid;grid-template-columns:1fr 1fr;gap:12px}
.box{border:1px solid #ccc;padding:10px;background:#fafafa}
.pills{display:flex;flex-wrap:wrap;gap:6px;margin:10px 0}
.pill{border:1px solid #bbb;padding:3px 8px;background:#f5f5f5;font-size:11px}
table{width:100%;border-collapse:collapse;font-size:10.5px;margin:0 0 14px}
th,td{border:1px solid #ccc;padding:4px 6px;text-align:left;font-variant-numeric:tabular-nums}
th{background:#f0f0f0}
.view{border:1px solid #ccc;padding:8px;margin:0 0 14px}
svg{display:block;width:100%;height:auto}
code{font-size:11px}
</style>
</head>
<body>
<h1>${DRAWING} Rev ${REV} — CAD material & mold-line package</h1>
<p class="meta">Companion to <code>CAL-HULL-GA-001</code> / <code>CAL-HULL-MESH-001</code>.
Files: <code>${DRAWING}.html</code> · <code>.json</code> · <code>.obj</code> · <code>.mtl</code>.
Regenerate: <code>node generate-cad.mjs</code>.</p>

<div class="pills">
  <span class="pill">${MATERIAL.designation}</span>
  <span class="pill">t = ${T * 1000} mm</span>
  <span class="pill">flat only — no bend</span>
  <span class="pill">stock ${MATERIAL.stock.sheet_m[0]}×${MATERIAL.stock.sheet_m[1]} m</span>
  <span class="pill">skin ≈ ${nice(skinMass_kg / 1000)} t</span>
</div>

<div class="lock">
  <strong>Process lock:</strong> Outer hull is a <em>faceted flat-plate weldment</em> (pepakura).
  Plates are shear/plasma/laser cut from annealed sheet and joined — <strong>not brake-formed, not rolled, not stretch-formed</strong>.
  OML = exterior; IML = interior of skin (OML inset by <strong>t = ${T * 1000}&nbsp;mm</strong>).
</div>

<h2>1 · Material selection (space + flat sheet)</h2>
<div class="grid2">
  <div class="box">
    <h3>Selected: ${MATERIAL.designation}</h3>
    <ul>
      <li>UNS ${MATERIAL.uns} · EN ${MATERIAL.en}</li>
      <li>Product: ${MATERIAL.product}</li>
      <li>Density ρ = ${MATERIAL.density_kg_m3} kg/m³</li>
      <li>E ≈ ${MATERIAL.E_GPa} GPa · ν ≈ ${MATERIAL.nu}</li>
      <li>Sy ≈ ${MATERIAL.Sy_MPa} MPa · Su ≈ ${MATERIAL.Su_MPa} MPa (annealed typical — use mill cert)</li>
      <li>CTE ≈ ${MATERIAL.CTE_1e6_K}×10⁻⁶ /K</li>
    </ul>
  </div>
  <div class="box">
    <h3>Why this alloy</h3>
    <ul>
      <li><strong>L-grade:</strong> as-welded construction without sensitization PWHT (ECSS guidance for low-C austenitics).</li>
      <li><strong>Flat sheet:</strong> ${MATERIAL.space.whyNot301}</li>
      <li><strong>Space listing:</strong> ${MATERIAL.space.spacematdb} (SPACEMATDB).</li>
      <li><strong>316L vs 304L:</strong> ${MATERIAL.space.why316Lvs304L}</li>
      <li><strong>Not carbon steel:</strong> ${MATERIAL.space.whyNotCarbon}</li>
      <li><strong>Not Ti for this build:</strong> ${MATERIAL.space.whyNotTi}</li>
    </ul>
  </div>
</div>

<div class="warn">
  <strong>Vacuum notes:</strong> ${MATERIAL.space.vacuum}
  ${MATERIAL.space.coldWeld}
  Filler: <code>${MATERIAL.weld.filler}</code> · ${MATERIAL.weld.condition}.
  Qualify to <code>${MATERIAL.weld.process}</code>.
</div>

<h2>2 · Thickness & space mass maths</h2>
${table(
  ["Parameter", "Value", "Notes"],
  [
    ["Skin thickness t", `${T * 1000} mm (${T} m)`, "Continuous outer facets; primary MMOD/handling skin"],
    ["Stock sheet", `${MATERIAL.stock.sheet_m[0]} × ${MATERIAL.stock.sheet_m[1]} m × ${T * 1000} mm`, "Same plan-form as GA BOM"],
    ["Areal density ρ·t", `${r3(T * MATERIAL.density_kg_m3)} kg/m²`, "Every m² of facet costs this mass"],
    ["OML loft area (approx)", `${skinArea} m²`, "Sum of loft quads"],
    ["Skin volume", `${skinVolume} m³`, "area × t"],
    ["Skin mass", `${skinMass_kg} kg (${nice(skinMass_kg / 1000)} t)`, "Excludes frames, door, apron, fasteners"],
    ["Δv leverage", "mass-critical", "Prefer frames + thinner skin later if propulsion budget dominates"],
  ],
)}
<p class="meta">8&nbsp;mm is a CAD lock for a welded freighter-scale outer: thick enough for facet weldment handling and modest debris, thin enough that structure should still migrate into frames in a later revision. Do not treat Sy/Su above as flight allowables without certs and factors.</p>

<h2>3 · Mold lines (CAD)</h2>
<div class="grid2">
  <div class="box">
    <h3>OML — outer mold line</h3>
    <p>Exterior of the ${MATERIAL.designation} skin. Matches GA envelope stations (stem → aft).</p>
    <p>Aft face at <code>X = ${nice(PAD)}</code>. Stem tip plane at <code>X = ${nice(PAD + LOA)}</code>.</p>
  </div>
  <div class="box">
    <h3>IML — inner mold line</h3>
    <p>Interior of skin: section <code>halfBeam′ = halfBeam − t</code>, <code>halfHeight′ = halfHeight − t</code>, chamfer′ = chamfer − t.</p>
    <p>Aft IML at <code>X = ${nice(PAD + T)}</code>; stem IML at <code>X = ${nice(PAD + LOA - T)}</code>.</p>
  </div>
</div>
<div class="view">${sectionSvg()}</div>

<h2>4 · Welding / joints (no bend)</h2>
${table(
  ["Item", "Spec"],
  [
    ["Joint types", "Butt (preferred on flats) / single-sided lap at facet edges if fit-up requires"],
    ["Edge prep", "Square or slight V for 8 mm; full penetration on structural seams"],
    ["Filler", MATERIAL.weld.filler],
    ["PWHT", "None for sensitization (L-grade); stress-relief only if drawing calls"],
    ["Inspection", "VT + PT/MT on structural seams; sample RT/UT per class"],
    ["Forbidden", "Press-brake bends, rolled cylinders, heat-and-beat fairing"],
  ],
)}

<h2>5 · Door clear (finished opening)</h2>
${table(
  ["Item", "Value"],
  [
    ["Clear W × H", `${fmt(DOOR_W)} × ${fmt(DOOR_H)}`],
    ["Sill", fmt(SILL)],
    ["OML aft X", nice(doorXOml)],
    ["IML coaming X", nice(doorXIml)],
    ["Pack check", `5×3 C40 + ≥${fmt(CLEAR)} side/above`],
  ],
)}

<h2>6 · Deliverables for CAD / DCC</h2>
<ul>
  <li><code>${DRAWING}.obj</code> + <code>.mtl</code> — OML and IML meshes (meters, Y-up in file as Z-up ship → import with axis mapping as needed)</li>
  <li><code>${DRAWING}.json</code> — full points, edges, faces, material, mass</li>
  <li>Import OBJ into Blender/FreeCAD/Rhino; use JSON for parametric rebuild</li>
</ul>

<h2>7 · Point list (OML / IML / door / origo)</h2>
${table(["Id", "X", "Y", "Z", "Group", "Notes"], omlPointRows)}

<h2>8 · Still out of scope (next CAD revs)</h2>
<ol>
  <li>Internal frames / stringers (recommended once skin mass is accepted)</li>
  <li>Door coaming extrusion, seals, actuators</li>
  <li>Apron/ramp solid ≥ ${fmt(ceilQuarter(C40_L))}</li>
  <li>Unfold nested DXF for ${MATERIAL.stock.sheet_m[0]}×${MATERIAL.stock.sheet_m[1]} stock</li>
  <li>Flight allowables, KDFs, GD&T, STEP AP242 with PMI</li>
</ol>

<p class="meta">Alloy rationale references: SPACEMATDB AISI 316L / 304L; ECSS-Q-ST-70-71 (low-C austenitics, welding); ECSS-Q-ST-70-39 (flight welding QA).</p>
</body>
</html>
`;

fs.writeFileSync(path.join(__dirname, `${DRAWING}.html`), html);
console.log(`Wrote ${DRAWING}.html/.json/.obj/.mtl`);
console.log(
  `316L t=${T * 1000}mm area=${skinArea}m2 mass=${skinMass_kg}kg (${(skinMass_kg / 1000).toFixed(2)}t) points=${points.length}`,
);
