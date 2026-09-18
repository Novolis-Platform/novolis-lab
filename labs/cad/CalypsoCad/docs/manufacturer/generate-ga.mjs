/**
 * Faithful HTML export of calypso-hull-manufacturer.canvas.tsx geometry.
 * Envelope from ../internals/calypso-lock.mjs (LOA mid-stretch for internals).
 * Run: node d:\novolis\novolis-lab\labs\cad\CalypsoCad\docs\manufacturer\generate-ga.mjs
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
  CHAMFER_DIAG,
  FLAT_BEAM,
  FLAT_OAH,
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
  SIDE_CLEAR,
  TOP_CLEAR,
  SIDE_SHELL,
  HEADER,
  APRON_D,
  FORE_STATIONS as FORE_STATIONS_RAW,
} from "../internals/calypso-lock.mjs";

const __dirname = path.dirname(fileURLToPath(import.meta.url));

const ceilDiv = (a, b) => Math.ceil(a / b - 1e-9);
const poly = (pts) => pts.map(([x, y], i) => `${i === 0 ? "M" : "L"} ${x} ${y}`).join(" ") + " Z";

const DRAWING = "CAL-HULL-GA-001";
const REV = "B"; // midbody stretch for internals pack
const STOCK_W = 2;
const STOCK_L = 6;
const PACK_D = C40_L;

const FORE_STATIONS = FORE_STATIONS_RAW.map((s) => ({
  z: s.z,
  halfBeam: s.halfBeam,
  halfHeight: s.halfHeight,
  mark: s.id === "ST0" ? "ST0 stem" : s.id === "ST3" ? "ST3 shoulder" : s.id,
}));
const W_STEM = FORE_STATIONS[0].halfBeam * 2;
const H_STEM = FORE_STATIONS[0].halfHeight * 2;

function stationChamfer(s) {
  const kx = (s.halfBeam * 2) / BEAM;
  const ky = (s.halfHeight * 2) / OAH;
  return {
    cx: Math.min(CHAMFER * kx, s.halfBeam * 0.45),
    cy: Math.min(CHAMFER * ky, s.halfHeight * 0.45),
  };
}

function sectionAtStation(ox, oy, sx, sy, s) {
  const hw = s.halfBeam * sx;
  const hh = s.halfHeight * sy;
  const { cx: cmx, cy: cmy } = stationChamfer(s);
  const cx = cmx * sx;
  const cy = cmy * sy;
  return [
    [ox - hw + cx, oy - hh],
    [ox + hw - cx, oy - hh],
    [ox + hw, oy - hh + cy],
    [ox + hw, oy + hh - cy],
    [ox + hw - cx, oy + hh],
    [ox - hw + cx, oy + hh],
    [ox - hw, oy + hh - cy],
    [ox - hw, oy - hh + cy],
  ];
}

function tilePlates(lenAlong, lenAcross) {
  const along6 = ceilDiv(lenAlong, STOCK_L) * ceilDiv(lenAcross, STOCK_W);
  const along2 = ceilDiv(lenAlong, STOCK_W) * ceilDiv(lenAcross, STOCK_L);
  return Math.min(along6, along2);
}

const BAY_LIST = FORE_STATIONS.slice(0, -1).map((s, i) => {
  const n = FORE_STATIONS[i + 1];
  const flare = n.halfBeam - s.halfBeam;
  const rise = n.halfHeight - s.halfHeight;
  const dz = n.z - s.z;
  const h0 = s.halfHeight * 2;
  const h1 = n.halfHeight * 2;
  return {
    i: i + 1,
    z0: s.z,
    z1: n.z,
    w0: s.halfBeam * 2,
    w1: n.halfBeam * 2,
    h0,
    h1,
    cheekSlant: Math.hypot(dz, flare),
    deckSlant: Math.hypot(dz, rise),
    courses: ceilDiv(Math.max(h0, h1), STOCK_W),
  };
});

const BAYS = BAY_LIST.length;
const FORE_CHEEK = BAY_LIST.reduce((n, b) => n + 2 * b.courses, 0);
const BOM = [
  { id: "P-PORT", face: `Port flat ${L_MID}×${FLAT_OAH}`, sheets: tilePlates(L_MID, FLAT_OAH) },
  { id: "P-STBD", face: `Starboard flat ${L_MID}×${FLAT_OAH}`, sheets: tilePlates(L_MID, FLAT_OAH) },
  { id: "P-DORSAL", face: `Dorsal flat ${L_MID}×${FLAT_BEAM}`, sheets: tilePlates(L_MID, FLAT_BEAM) },
  { id: "P-VENTRAL", face: `Ventral flat ${L_MID}×${FLAT_BEAM}`, sheets: tilePlates(L_MID, FLAT_BEAM) },
  {
    id: "P-CORNER×4",
    face: `Corner ${L_MID}×${CHAMFER_DIAG.toFixed(2)} @45°`,
    sheets: tilePlates(L_MID, CHAMFER_DIAG) * 4,
  },
  { id: "P-STEM", face: `Stem ${W_STEM}×${H_STEM}`, sheets: tilePlates(W_STEM, H_STEM) },
  { id: "P-CHEEK", face: `Fore cheeks (${BAYS} bays×2)`, sheets: FORE_CHEEK },
  { id: "P-FORE-D/V", face: `Fore dorsal/ventral (${BAYS}×2)`, sheets: BAYS * 2 },
  {
    id: "P-AFT",
    face: `Aft ${BEAM}×${OAH} · door cut ${DOOR_W}×${DOOR_H}`,
    sheets: tilePlates(BEAM, OAH),
  },
];
const TOTAL_STOCK = BOM.reduce((n, b) => n + b.sheets, 0);

const C = {
  ink: "#1a1a1a",
  mute: "#444444",
  dim: "#777777",
  accent: "#0b5fff",
  plate: "#e8e8e8",
  seam: "#555555",
  stroke: "#cccccc",
  hatch: "#d0d0d0",
  surface: "#f5f5f5",
};

const PROF = { ox: 52, oy: 40, sx: 8.2, sy: 10 };
const profX = (z) => PROF.ox + z * PROF.sx;
const profY = (up) => PROF.oy + (OAH - up) * PROF.sy;
const PLAN = { ox: 200, oy: 44, sx: 7.4, sy: 7.4 };
const planX = (m) => PLAN.ox + m * PLAN.sx;
const planY = (z) => PLAN.oy + z * PLAN.sy;

function dimH(x1, x2, y, label, above = true) {
  const mid = (x1 + x2) / 2;
  return `<g stroke="${C.accent}" fill="${C.accent}" stroke-width="1" font-size="9">
    <line x1="${x1}" y1="${y}" x2="${x2}" y2="${y}"/>
    <line x1="${x1}" y1="${y - 4}" x2="${x1}" y2="${y + 4}"/>
    <line x1="${x2}" y1="${y - 4}" x2="${x2}" y2="${y + 4}"/>
    <text x="${mid}" y="${above ? y - 5 : y + 13}" text-anchor="middle">${label}</text>
  </g>`;
}
function dimV(y1, y2, x, label, left = true) {
  const mid = (y1 + y2) / 2;
  const tx = left ? x - 6 : x + 8;
  const rot = left ? ` transform="rotate(-90 ${tx} ${mid})"` : "";
  return `<g stroke="${C.accent}" fill="${C.accent}" stroke-width="1" font-size="9">
    <line x1="${x}" y1="${y1}" x2="${x}" y2="${y2}"/>
    <line x1="${x - 4}" y1="${y1}" x2="${x + 4}" y2="${y1}"/>
    <line x1="${x - 4}" y1="${y2}" x2="${x + 4}" y2="${y2}"/>
    <text x="${tx}" y="${mid}" text-anchor="${left ? "end" : "start"}"${rot}>${label}</text>
  </g>`;
}

function titleBlock() {
  return `<svg viewBox="0 0 1040 90" xmlns="http://www.w3.org/2000/svg">
  <rect x="1" y="1" width="1038" height="88" fill="none" stroke="${C.ink}" stroke-width="1.5"/>
  <line x1="720" y1="1" x2="720" y2="89" stroke="${C.ink}"/>
  <line x1="720" y1="45" x2="1039" y2="45" stroke="${C.ink}"/>
  <line x1="880" y1="45" x2="880" y2="89" stroke="${C.ink}"/>
  <text x="16" y="28" font-size="16" fill="${C.ink}">CALYPSO — OUTER HULL GENERAL ARRANGEMENT</text>
  <text x="16" y="50" font-size="11" fill="${C.mute}">Angular bullet fore · soft-chamfer midbody · HILS-C40 aft door · pepakura stock ${STOCK_W}×${STOCK_L} m</text>
  <text x="16" y="72" font-size="10" fill="${C.dim}">Units: meters · Datum: stem tip z=0, centerline, mid-height · Tolerances: ±25 mm envelope unless noted</text>
  <text x="736" y="28" font-size="11" fill="${C.mute}">Drawing</text>
  <text x="736" y="48" font-size="14" fill="${C.ink}">${DRAWING}</text>
  <text x="736" y="72" font-size="11" fill="${C.mute}">Rev ${REV}</text>
  <text x="896" y="62" font-size="11" fill="${C.mute}">Sheet 1 of 1</text>
  <text x="896" y="78" font-size="10" fill="${C.dim}">Manufacturer GA</text>
</svg>`;
}

function profileGA() {
  const midCL = OAH / 2;
  const upper = FORE_STATIONS.map((s) => [profX(s.z), profY(midCL + s.halfHeight)]);
  const lower = [...FORE_STATIONS].reverse().map((s) => [profX(s.z), profY(midCL - s.halfHeight)]);
  const hull = [
    ...upper,
    [profX(L_FORE + L_MID), profY(OAH)],
    [profX(LOA), profY(OAH)],
    [profX(LOA), profY(0)],
    [profX(L_FORE + L_MID), profY(0)],
    ...lower,
  ];
  const doorTop = SILL + DOOR_H;
  const aftX = profX(LOA);
  const stations = FORE_STATIONS.map(
    (s) =>
      `<line x1="${profX(s.z)}" y1="${profY(midCL - s.halfHeight)}" x2="${profX(s.z)}" y2="${profY(midCL + s.halfHeight)}" stroke="${C.seam}" stroke-width="${s.z === 0 || s.z === L_FORE ? 1.6 : 1}"/>`,
  ).join("\n");
  return `<svg viewBox="0 0 1040 320" xmlns="http://www.w3.org/2000/svg">
  <rect x="8" y="8" width="1024" height="304" fill="none" stroke="${C.stroke}"/>
  <text x="20" y="30" font-size="12" fill="${C.ink}">1 · PROFILE (PORT) · BOW LEFT</text>
  <path d="${poly(hull)}" fill="${C.plate}" stroke="${C.ink}" stroke-width="1.8" stroke-linejoin="miter"/>
  ${stations}
  <line x1="${profX(L_FORE + L_MID)}" y1="${profY(0)}" x2="${profX(L_FORE + L_MID)}" y2="${profY(OAH)}" stroke="${C.seam}" stroke-width="1.2"/>
  <rect x="${aftX - 6}" y="${profY(doorTop)}" width="10" height="${DOOR_H * PROF.sy}" fill="${C.accent}" fill-opacity="0.35" stroke="${C.accent}" stroke-width="1.5"/>
  ${dimH(profX(0), profX(L_FORE), profY(OAH) - 16, `L_fore ${fmt(L_FORE)}`)}
  ${dimH(profX(L_FORE), profX(L_FORE + L_MID), profY(OAH) - 16, `L_mid ${fmt(L_MID)}`)}
  ${dimH(profX(L_FORE + L_MID), profX(LOA), profY(OAH) - 16, `L_aft ${fmt(L_AFT)}`)}
  ${dimH(profX(0), profX(LOA), profY(0) + 24, `LOA ${fmt(LOA)}`, false)}
  ${dimV(profY(OAH), profY(0), profX(LOA) + 22, `OAH ${fmt(OAH)}`, false)}
  ${dimV(profY(doorTop), profY(SILL), aftX + 36, `door H ${fmt(DOOR_H)}`, false)}
  ${dimV(profY(SILL), profY(0), aftX + 36, `sill ${fmt(SILL)}`, false)}
  ${dimV(profY(midCL + H_STEM / 2), profY(midCL - H_STEM / 2), profX(0) - 16, `stem ${fmt(H_STEM)}`)}
  <text x="${profX(L_FORE / 2)}" y="${profY(midCL) + 4}" text-anchor="middle" font-size="9" fill="${C.mute}">bullet fore</text>
  <text x="${profX(L_FORE + L_MID / 2)}" y="${profY(OAH / 2)}" text-anchor="middle" font-size="9" fill="${C.accent}">midbody flats + chamfer ${fmt(CHAMFER)}</text>
  <text x="${aftX - 50}" y="${profY(doorTop) - 6}" font-size="9" fill="${C.accent}">C40 door</text>
</svg>`;
}

function planGA() {
  const half = BEAM / 2;
  const stbd = FORE_STATIONS.map((s) => [planX(s.halfBeam), planY(s.z)]);
  const port = [...FORE_STATIONS].reverse().map((s) => [planX(-s.halfBeam), planY(s.z)]);
  const hull = [
    ...stbd,
    [planX(half), planY(L_FORE + L_MID)],
    [planX(half), planY(LOA)],
    [planX(-half), planY(LOA)],
    [planX(-half), planY(L_FORE + L_MID)],
    ...port,
  ];
  const stations = FORE_STATIONS.map(
    (s) => `<g>
    <line x1="${planX(-s.halfBeam)}" y1="${planY(s.z)}" x2="${planX(s.halfBeam)}" y2="${planY(s.z)}" stroke="${C.seam}" stroke-width="${s.z === 0 || s.z === L_FORE ? 1.6 : 1}"/>
    <text x="${planX(s.halfBeam) + 4}" y="${planY(s.z) + 3}" font-size="8" fill="${C.mute}">${s.mark} · z ${nice(s.z)}</text>
    <text x="${planX(0)}" y="${planY(s.z) - 4}" text-anchor="middle" font-size="8" fill="${C.accent}">${nice(s.halfBeam * 2)}×${nice(s.halfHeight * 2)}</text>
  </g>`,
  ).join("\n");
  return `<svg viewBox="0 0 480 640" xmlns="http://www.w3.org/2000/svg">
  <rect x="8" y="8" width="464" height="624" fill="none" stroke="${C.stroke}"/>
  <text x="20" y="30" font-size="12" fill="${C.ink}">2 · PLAN · BOW UP</text>
  <path d="${poly(hull)}" fill="${C.plate}" stroke="${C.ink}" stroke-width="1.8" stroke-linejoin="miter"/>
  ${stations}
  <line x1="${planX(-DOOR_W / 2)}" y1="${planY(LOA)}" x2="${planX(DOOR_W / 2)}" y2="${planY(LOA)}" stroke="${C.accent}" stroke-width="3"/>
  <line x1="${planX(0)}" y1="${planY(0)}" x2="${planX(0)}" y2="${planY(LOA)}" stroke="${C.dim}" stroke-dasharray="5 4"/>
  ${dimH(planX(-half), planX(half), planY(LOA) + 20, `beam ${fmt(BEAM)}`, false)}
  ${dimH(planX(-DOOR_W / 2), planX(DOOR_W / 2), planY(LOA) + 38, `door clear ${fmt(DOOR_W)}`, false)}
  ${dimH(planX(-W_STEM / 2), planX(W_STEM / 2), planY(0) - 12, `stem ${fmt(W_STEM)}`)}
  ${dimV(planY(0), planY(L_FORE), planX(-half) - 18, `L_fore ${fmt(L_FORE)}`)}
  ${dimV(planY(L_FORE), planY(L_FORE + L_MID), planX(half) + 18, `L_mid ${fmt(L_MID)}`, false)}
  ${dimV(planY(L_FORE + L_MID), planY(LOA), planX(-half) - 18, `L_aft ${fmt(L_AFT)}`)}
  ${dimV(planY(0), planY(LOA), 28, `LOA ${fmt(LOA)}`)}
</svg>`;
}

function midbodyGA() {
  const sx = 12,
    sy = 14,
    ox = 200,
    oy = 170;
  const s = FORE_STATIONS[FORE_STATIONS.length - 1];
  const sec = sectionAtStation(ox, oy, sx, sy, s);
  const hw = (BEAM / 2) * sx;
  const hh = (OAH / 2) * sy;
  const cx = CHAMFER * sx;
  const cy = CHAMFER * sy;
  return `<svg viewBox="0 0 400 360" xmlns="http://www.w3.org/2000/svg">
  <rect x="8" y="8" width="384" height="344" fill="none" stroke="${C.stroke}"/>
  <text x="20" y="30" font-size="12" fill="${C.ink}">3 · MIDBODY SECTION @ z≥${L_FORE}</text>
  <path d="${poly(sec)}" fill="${C.plate}" stroke="${C.ink}" stroke-width="1.8" stroke-linejoin="miter"/>
  <line x1="${ox - hw + cx}" y1="${oy - hh}" x2="${ox + hw - cx}" y2="${oy - hh}" stroke="${C.accent}" stroke-width="2"/>
  <line x1="${ox - hw}" y1="${oy - hh + cy}" x2="${ox - hw}" y2="${oy + hh - cy}" stroke="${C.accent}" stroke-width="2"/>
  ${dimH(ox - hw, ox + hw, oy + hh + 22, `beam ${fmt(BEAM)}`, false)}
  ${dimH(ox - hw + cx, ox + hw - cx, oy - hh - 14, `dorsal flat ${fmt(FLAT_BEAM)}`)}
  ${dimV(oy - hh, oy + hh, ox + hw + 18, `OAH ${fmt(OAH)}`, false)}
  ${dimV(oy - hh + cy, oy + hh - cy, ox - hw - 16, `side flat ${fmt(FLAT_OAH)}`)}
  <text x="${ox + hw - cx / 2 + 6}" y="${oy - hh + cy / 2}" font-size="9" fill="${C.mute}">chamfer ${fmt(CHAMFER)}</text>
  <text x="${ox}" y="340" text-anchor="middle" font-size="10" fill="${C.dim}">corner plate width ${fmt(CHAMFER_DIAG)} (√2 × chamfer)</text>
</svg>`;
}

function bowGA() {
  const sx = 11,
    sy = 13,
    ox = 200,
    oy = 190;
  const frames = [...FORE_STATIONS].reverse();
  const paths = frames
    .map((s, idx) => {
      const sec = sectionAtStation(ox, oy, sx, sy, s);
      const hw = s.halfBeam * sx;
      const hh = s.halfHeight * sy;
      const isStem = s.z === 0;
      const isOuter = s.z === L_FORE;
      return `<g>
      <path d="${poly(sec)}" fill="${isStem ? C.plate : "none"}" stroke="${isOuter ? C.dim : C.ink}" stroke-width="${isStem ? 1.8 : 1.2}" ${isOuter ? 'stroke-dasharray="4 3"' : ""}/>
      <text x="${ox + hw + 4}" y="${oy - hh + 8 + idx * 12}" font-size="8" fill="${C.accent}">${nice(s.halfBeam * 2)}×${nice(s.halfHeight * 2)} · ${s.mark}</text>
    </g>`;
    })
    .join("\n");
  return `<svg viewBox="0 0 400 400" xmlns="http://www.w3.org/2000/svg">
  <rect x="8" y="8" width="384" height="384" fill="none" stroke="${C.stroke}"/>
  <text x="20" y="30" font-size="12" fill="${C.ink}">4 · BOW-ON · STATION NEST</text>
  <text x="20" y="46" font-size="9" fill="${C.dim}">Beam and height both taper (angular bullet)</text>
  ${paths}
  ${dimH(ox - (BEAM / 2) * sx, ox + (BEAM / 2) * sx, oy + (OAH / 2) * sy + 20, `shoulder beam ${fmt(BEAM)}`, false)}
  ${dimV(oy - (OAH / 2) * sy, oy + (OAH / 2) * sy, ox + (BEAM / 2) * sx + 16, `OAH ${fmt(OAH)}`, false)}
  ${dimH(ox - (W_STEM / 2) * sx, ox + (W_STEM / 2) * sx, oy - (H_STEM / 2) * sy - 14, `stem ${nice(W_STEM)}×${nice(H_STEM)}`)}
</svg>`;
}

function aftGA() {
  const sx = 16,
    sy = 18,
    ox = 260,
    oy = 220;
  const s = FORE_STATIONS[FORE_STATIONS.length - 1];
  const sec = sectionAtStation(ox, oy, sx, sy, s);
  const doorLeft = ox - (DOOR_W / 2) * sx;
  const doorBot = oy + (OAH / 2) * sy - SILL * sy;
  const doorTop = doorBot - DOOR_H * sy;
  const packLeft = ox - (PACK_W / 2) * sx;
  const packBot = doorBot;
  const packTop = packBot - PACK_H * sy;
  let boxes = "";
  for (let col = 0; col < COLS; col++) {
    for (let tier = 0; tier < TIERS; tier++) {
      const x = packLeft + col * (C40_W + CELL) * sx;
      const y = packBot - (tier + 1) * C40_H * sy;
      boxes += `<rect x="${x}" y="${y}" width="${C40_W * sx}" height="${C40_H * sy}" fill="${C.hatch}" stroke="${C.ink}" stroke-width="1"/>`;
    }
  }
  return `<svg viewBox="0 0 520 480" xmlns="http://www.w3.org/2000/svg">
  <rect x="8" y="8" width="504" height="464" fill="none" stroke="${C.stroke}"/>
  <text x="20" y="30" font-size="12" fill="${C.ink}">5 · AFT · C40 DOOR CLEARANCE</text>
  <text x="20" y="46" font-size="9" fill="${C.dim}">5 wide × 3 high roll-out · ${fmt(CLEAR)} min side + above · factory clear ${fmt(DOOR_W)}×${fmt(DOOR_H)}</text>
  <path d="${poly(sec)}" fill="${C.plate}" stroke="${C.ink}" stroke-width="1.8" stroke-linejoin="miter"/>
  <rect x="${doorLeft}" y="${doorTop}" width="${DOOR_W * sx}" height="${DOOR_H * sy}" fill="${C.surface}" stroke="${C.accent}" stroke-width="2.2"/>
  <rect x="${doorLeft}" y="${doorTop}" width="${SIDE_CLEAR * sx}" height="${DOOR_H * sy}" fill="${C.accent}" fill-opacity="0.12"/>
  <rect x="${doorLeft + (DOOR_W - SIDE_CLEAR) * sx}" y="${doorTop}" width="${SIDE_CLEAR * sx}" height="${DOOR_H * sy}" fill="${C.accent}" fill-opacity="0.12"/>
  <rect x="${packLeft}" y="${doorTop}" width="${PACK_W * sx}" height="${TOP_CLEAR * sy}" fill="${C.accent}" fill-opacity="0.16"/>
  ${boxes}
  ${dimH(ox - (BEAM / 2) * sx, ox + (BEAM / 2) * sx, oy + (OAH / 2) * sy + 24, `beam ${fmt(BEAM)}`, false)}
  ${dimH(doorLeft, doorLeft + DOOR_W * sx, doorTop - 14, `clear W ${fmt(DOOR_W)}`)}
  ${dimH(packLeft, packLeft + PACK_W * sx, packBot + 16, `pack ${fmt(PACK_W)}`, false)}
  ${dimV(oy - (OAH / 2) * sy, oy + (OAH / 2) * sy, ox + (BEAM / 2) * sx + 16, `OAH ${fmt(OAH)}`, false)}
  ${dimV(doorTop, doorBot, doorLeft + DOOR_W * sx + 14, `clear H ${fmt(DOOR_H)}`, false)}
  ${dimV(packTop, packBot, packLeft - 12, `stack ${fmt(PACK_H)}`)}
  ${dimV(doorTop, packTop, ox, `above ${fmt(TOP_CLEAR)}`, false)}
  <text x="${ox}" y="458" text-anchor="middle" font-size="10" fill="${C.dim}">sill ${fmt(SILL)} · side shell each ${fmt(SIDE_SHELL)} · header ${fmt(HEADER)} · apron ≥ ${fmt(APRON_D)}</text>
</svg>`;
}

function table(headers, rows) {
  return `<table><thead><tr>${headers.map((h) => `<th>${h}</th>`).join("")}</tr></thead><tbody>${rows
    .map((r) => `<tr>${r.map((c) => `<td>${c}</td>`).join("")}</tr>`)
    .join("")}</tbody></table>`;
}

const html = `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8"/>
  <title>${DRAWING} Rev ${REV} — Calypso Outer Hull GA</title>
  <style>
    body{margin:0;padding:24px;font:12px/1.45 "Segoe UI",system-ui,sans-serif;color:${C.ink};background:#fff;max-width:1100px}
    h2{font-size:15px;margin:28px 0 10px;border-bottom:1px solid #ccc;padding-bottom:4px}
    h3{font-size:12px;margin:0 0 8px}
    .meta{color:${C.dim};font-size:11px;margin:8px 0 16px}
    .pills{display:flex;flex-wrap:wrap;gap:6px;margin:12px 0}
    .pill{border:1px solid #bbb;padding:3px 8px;border-radius:3px;background:#f5f5f5;font-size:11px}
    .lock{border:1px solid #9ab;background:#f0f6ff;padding:10px 12px;margin:12px 0 18px}
    .stats{display:grid;grid-template-columns:repeat(4,1fr);gap:10px;margin-bottom:18px}
    .stat{border:1px solid #ccc;padding:10px;background:#f5f5f5}
    .stat strong{display:block;font-size:16px}
    .stat span{color:${C.dim};font-size:11px}
    table{width:100%;border-collapse:collapse;font-size:11px;margin:0 0 16px}
    th,td{border:1px solid #ccc;padding:5px 7px;text-align:left}
    th{background:#f0f0f0}
    .view{border:1px solid #ccc;padding:8px;margin:0 0 16px;page-break-inside:avoid}
    .grid2{display:grid;grid-template-columns:1fr 1fr;gap:12px}
    svg{display:block;width:100%;height:auto}
    ol{padding-left:18px;color:${C.mute}}
    @media print{body{padding:12mm;max-width:none}}
  </style>
</head>
<body>
${titleBlock()}
<p class="meta">Generated from canvas geometry (identical math to <code>calypso-hull-manufacturer.canvas.tsx</code>). Do not hand-edit dimensions.</p>
<div class="pills">
  <span class="pill">LOA ${fmt(LOA)}</span>
  <span class="pill">Beam ${fmt(BEAM)}</span>
  <span class="pill">OAH ${fmt(OAH)}</span>
  <span class="pill">Door ${nice(DOOR_W)}×${nice(DOOR_H)}</span>
  <span class="pill">Stem ${nice(W_STEM)}×${nice(H_STEM)}</span>
  <span class="pill">≈${TOTAL_STOCK} sheets @ ${STOCK_W}×${STOCK_L}</span>
</div>
<div class="lock"><strong>Manufacturer design lock.</strong>
 Outer envelope ${fmt(LOA)} × ${fmt(BEAM)} × ${fmt(OAH)}. Fore: angular bullet stations ST0–ST3 over ${fmt(L_FORE)}.
 Midbody: four flats + 45° chamfer ${fmt(CHAMFER)} (diag ${fmt(CHAMFER_DIAG)}).
 Aft door clear ${fmt(DOOR_W)} × ${fmt(DOOR_H)} for 5×3 HILS-C40 (${C40_L}×${C40_W}×${C40_H}) with ≥${fmt(CLEAR)} side and above.
 Roll-out apron depth ≥ ${fmt(APRON_D)}.</div>
<div class="stats">
  <div class="stat"><strong>${fmt(LOA)}</strong><span>LOA</span></div>
  <div class="stat"><strong>${nice(BEAM)} × ${nice(OAH)}</strong><span>Beam × OAH</span></div>
  <div class="stat"><strong>${nice(DOOR_W)} × ${nice(DOOR_H)}</strong><span>Door clear</span></div>
  <div class="stat"><strong>${fmt(APRON_D)}</strong><span>Min apron depth</span></div>
</div>

<h2>Critical dimensions</h2>
${table(
  ["Ref", "Dimension", "Value", "Notes"],
  [
    ["E1", "LOA", fmt(LOA), "Stem tip → aft face"],
    ["E2", "Beam (max)", fmt(BEAM), "Midbody / aft outer"],
    ["E3", "OAH", fmt(OAH), "Keel → crown, midbody"],
    ["E4", "L_fore", fmt(L_FORE), "ST0 → ST3 shoulder"],
    ["E5", "L_mid", fmt(L_MID), "Parallel midbody"],
    ["E6", "L_aft", fmt(L_AFT), "Aft coaming band"],
    ["E7", "Chamfer", fmt(CHAMFER), "Each axis · 45° corners"],
    ["E8", "Flat beam / OAH", `${fmt(FLAT_BEAM)} / ${fmt(FLAT_OAH)}`, "Between chamfers"],
    ["E9", "Stem face", `${fmt(W_STEM)} × ${fmt(H_STEM)}`, "ST0 tip plate"],
    ["D1", "Door clear W×H", `${fmt(DOOR_W)} × ${fmt(DOOR_H)}`, "Factory snap from C40 pack"],
    ["D2", "Sill height", fmt(SILL), "Deck → clear opening"],
    ["D3", "Side clear (each)", fmt(SIDE_CLEAR), `≥ ${fmt(CLEAR)} required`],
    ["D4", "Above-pack clear", fmt(TOP_CLEAR), `≥ ${fmt(CLEAR)} required`],
    ["D5", "Side shell (each)", fmt(SIDE_SHELL), "Beam margin outside door"],
    ["D6", "Header above door", fmt(HEADER), "Lintel → crown"],
    ["D7", "Apron depth", `≥ ${fmt(APRON_D)}`, `One C40 deep (${C40_L} raw)`],
    ["C1", "C40 external", `${C40_L} × ${C40_W} × ${C40_H}`, "HILS-C40 / ISO class"],
    ["C2", "Pack W×H×D", `${nice(PACK_W)} × ${nice(PACK_H)} × ${nice(PACK_D)}`, `5×3×1 · cell ${CELL}`],
  ],
)}

<h2>Station schedule (fore)</h2>
${table(
  ["Mark", "z (m)", "Beam (m)", "Height (m)", "Half-beam", "Half-height"],
  FORE_STATIONS.map((s) => [s.mark, nice(s.z), nice(s.halfBeam * 2), nice(s.halfHeight * 2), nice(s.halfBeam), nice(s.halfHeight)]),
)}

<h2>Fore bay unfold lengths</h2>
${table(
  ["Bay", "z₀→z₁", "beam", "height", "cheek slant", "deck slant", "P+S courses"],
  BAY_LIST.map((b) => [
    String(b.i),
    `${nice(b.z0)} → ${nice(b.z1)}`,
    `${nice(b.w0)} → ${nice(b.w1)}`,
    `${nice(b.h0)} → ${nice(b.h1)}`,
    nice(b.cheekSlant),
    nice(b.deckSlant),
    String(2 * b.courses),
  ]),
)}

<h2>Plate bill (stock ${STOCK_W}×${STOCK_L} m)</h2>
${table(
  ["Id", "Face", "Sheets"],
  [...BOM.map((b) => [b.id, b.face, String(b.sheets)]), ["TOTAL", "All exterior facets (approx)", String(TOTAL_STOCK)]],
)}

<h2>General arrangement views</h2>
<div class="view"><h3>View 1 — Profile</h3>${profileGA()}</div>
<div class="grid2">
  <div class="view"><h3>View 2 — Plan</h3>${planGA()}</div>
  <div>
    <div class="view"><h3>View 3 — Midbody section</h3>${midbodyGA()}</div>
    <div class="view"><h3>View 4 — Bow-on</h3>${bowGA()}</div>
  </div>
</div>
<div class="view"><h3>View 5 — Aft door + C40 pack</h3>${aftGA()}</div>

<h2>Manufacturer notes</h2>
<ol>
  <li>All dimensions meters. Snap values are design authority; ISO C40 lengths stay exact (${C40_L}/${C40_W}/${C40_H}).</li>
  <li>Fore is centerline-symmetric angular bullet — taper beam and height together; do not build a constant-height wedge.</li>
  <li>Midbody is four large flats + four 45° chamfer strips; corner plate width = ${fmt(CHAMFER_DIAG)}.</li>
  <li>Aft face is flat with soft chamfer matching midbody. Door clear opening centered on CL; sill ${fmt(SILL)}; clear ${fmt(DOOR_W)}×${fmt(DOOR_H)} must pass 5×3 C40 with ≥${fmt(CLEAR)} free on sides and above pack.</li>
  <li>Provide roll-out apron / ramp clear depth ≥ ${fmt(APRON_D)} beyond sill (one container deep).</li>
  <li>Exterior plating from ${STOCK_W}×${STOCK_L} m stock, straight cuts. Approx ${TOTAL_STOCK} sheets before scrap/overlap — confirm nesting with fabricator.</li>
  <li>Source of truth: Cursor canvas <code>calypso-hull-manufacturer.canvas.tsx</code>. Regenerate via <code>generate-ga.mjs</code>.</li>
</ol>
</body>
</html>
`;

const outHtml = path.join(__dirname, "CAL-HULL-GA-001.html");
fs.writeFileSync(outHtml, html, "utf8");
console.log(`Wrote ${outHtml}`);
console.log(`Lock: LOA ${LOA} beam ${BEAM} OAH ${OAH} door ${DOOR_W}×${DOOR_H} sheets ${TOTAL_STOCK}`);
