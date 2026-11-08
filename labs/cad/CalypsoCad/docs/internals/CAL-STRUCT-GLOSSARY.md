# Calypso — Structural and CAD Terminology

Companion text glossary for Calypso CAD structure (ring frames, knuckle paths, sandwich decks, semantic pipeline). Place SVG diagram pack beside this file as `CAL-STRUCT-GLOSSARY.html` when available.

Coordinate convention: zFromStem = 0 at bow, z = 69 m at stern. All dimensions in metres unless noted.
## Diagram 1: A ring frame versus a pressure bulkhead
```text
RING FRAME (mostly open)       PRESSURE BULKHEAD (mostly solid)
  +------------------+            +##################+
  | +--------------+ |            |##################|
  | |              | |            |######    ########|
  | |     VOID     | |            |######HATCH########|
  | +--------------+ |            |######    ########|
  +------------------+            +##################+
```
## Diagram 2: Longitudinal seam path
```text
BOW                                         STERN
(3D diagonal during taper)                 (straight)
      /------------------+------------------+
     /                   ^                  ^
    /             welded splice       frame / bulkhead
   /
Whole-length logical path != one physically continuous 69 m part.
```
## Diagram 3: Cellular deck
```text
TOP FACE SHEET     ============================
CORE WEBS          /\/\/\/\/\/\/\/\/\/\/\
BOTTOM FACE SHEET  ============================
GIRDER SUPPORT                 [====]
```
## Diagram 4: Semantic generation pipeline
```text
HULL LOFT -> ASSEMBLY SPECIFICATION -> PARTS/SOLIDS
                                        |
                                        +-> GEOMETRIC VALIDATION
                                        +-> FABRICATION PROFILES
                                        +-> RENDERS / DRAWINGS / BOM
```
## Reference geometry

Geometry that constrains parts but is not necessarily a physical part.

| Term | Kind | Meaning |
|---|---|---|
| Bow / stem | Reference | Forward end of the ship. Calypso defines zFromStem = 0 m here. |
| Stern / aft | Reference | Rear end of the ship, toward the cargo doors. Calypso has LOA = 69 m. |
| Station | Reference | Longitudinal position at which a section, frame, bulkhead, or manufacturing event is defined. |
| Frame pitch / bay | Structural | Distance between consecutive transverse structural stations; a bay is the region between them. Maximum target is roughly 4 m for major frames. |
| Hull loft | Reference | The complete 3D hull surface defined by corresponding transverse sections at different stations. |
| OML (outer mold line) | Reference | Exterior reference surface of the finished pressure hull. |
| IML (inner mold line) | Reference | Inner boundary of the structural hull or pressure shell, allowing for material thickness and chosen reference offsets. |
| Transverse section | Reference | Cross-section perpendicular to the bow-stern axis at a given station. |
| Hull facet | Reference | One of the broad planar or nearly planar surfaces making up Calypso's polygonal hull. |
| Hull knuckle | Reference | Line where adjacent hull facets meet at an angle; distinct from an arbitrary welded plate seam on a flat facet. |
| Chamfer | Reference | Beveled polygonal corner between otherwise flat hull faces. |
| Loft station | Reference | Station specifying a hull shape for interpolation; not automatically a structural frame location. |
| Clearance envelope | Constraint | Reserved 3D empty volume for cargo, personnel, machinery removal or access; not a steel object. |

## Transverse structure

Members crossing or enclosing the ship at a particular stem-to-stern station.

| Term | Kind | Meaning |
|---|---|---|
| Ring frame | Assembly | Complete transverse structural assembly around the inner hull perimeter, largely open in the middle. |
| Annulus / annular web | Part | The actual steel plate region between an outer polygon and an inner polygonal opening; need not be circular. |
| Polygonal annulus | Geometry | A ring with faceted outer and inner boundaries, as in Calypso's octagonal sections. |
| Web | Part | The flat plate portion of a beam, frame or girder that carries shear and supports flanges. |
| Flange | Part | Reinforcing strip or plate running along a web edge; increases stiffness and resistance to bending. |
| Deep-web frame | Assembly | Larger transverse frame with a deeper web and stronger connection details for higher loads. |
| Deck chord / deck beam | Part | Transverse member supporting or connecting structural decks at a frame station. |
| Heavy bulkhead | Assembly | Substantially continuous transverse steel assembly; may act as a diaphragm and/or pressure barrier. |
| Pressure bulkhead | Assembly | Bulkhead with sealed periphery and closures, capable of separating atmospheric compartments. |
| Diaphragm bulkhead | Assembly | Heavy transverse plate/assembly resisting distortion or transferring loads without necessarily sealing atmosphere. |
| Collision bulkhead | Assembly | Forward pressure/damage-control barrier limiting the impact of a bow breach. |
| Hatch collar / coaming | Part | Reinforced perimeter around an access opening; a coaming can also be a raised opening boundary. |
| Lightening hole | Feature | Intentional hole in a web to reduce mass, with strength and buckling effects accounted for. |
| Passage opening | Feature | Actual cutout through the member, kept clear for people or equipment; not a drawn transparent rectangle. |

## Longitudinal and hull structure

Members running toward bow and stern, and the shell they support.

| Term | Kind | Meaning |
|---|---|---|
| Shell plating | Assembly | Continuous, welded steel skin. Both pressure boundary and part of the load-bearing hull. |
| Shell panel | Part | Individual fabricated portion of shell plating, with its own perimeter and attachment details. |
| Plate seam | Feature | Welded boundary between two shell panels. A seam can lie on a flat facet or coincide with a knuckle. |
| Knuckle stringer | Assembly | Longitudinal stiffener following a hull knuckle in 3D, including bow taper. |
| Hull stringer | Part | Longitudinal stiffening member welded to or attached along a hull panel or facet. |
| Seam path | Reference | Continuous geometric curve along the hull used as the reference for a knuckle or seam-related member. |
| Stringer segment | Part | Manufacturable portion of a continuous logical stringer assembly; must join its neighbors. |
| Longitudinal girder | Assembly | Substantial fore-aft beam that carries deck, hull or cargo loads through the ship. |
| Keel / centerline girder | Assembly | Primary member near the ship centerline, often straight despite tapering hull sides. |
| Thrust girder | Assembly | Structural member directing engine-mount loads into the ship's primary load paths. |
| Stiffener | Part | Smaller structural member used to prevent local plate bending or buckling between major supports. |
| Longitudinal load path | Engineering | Chain of structural members and joints through which axial thrust and other forces are transmitted. |
| Structural continuity | Engineering | Intended transfer of force through connected members and joints; not necessarily one unbroken piece of steel. |

## Decks and functional spaces

Horizontal structure and volumes that determine where steel can or cannot go.

| Term | Kind | Meaning |
|---|---|---|
| Structural deck | Assembly | Load-bearing horizontal assembly connected to the ship's frames and longitudinal members. |
| Sandwich deck panel | Assembly | Lightweight stiff panel built from two structural faces separated by an internal core. |
| Face sheet | Part | Top or bottom continuous plate in a structural sandwich. |
| Cellular core / web ribs | Part | Internal connected web network transferring shear and supporting the sandwich face sheets. |
| Panel perimeter closure | Part | Border members closing and reinforcing the sandwich panel's edge. |
| Deck-edge girder | Part | Structural support along the deck boundary, tying the deck into nearby hull framing. |
| Deck opening | Feature | Actual missing deck material for stairs, machinery, hatchways, atria, or service trunks. |
| Full-height cargo hold | Constraint | Volume spanning multiple nominal decks with no intermediate deck obstructing its cargo envelope. |
| Engineering atrium | Constraint | Multi-level machinery void that cuts through selected decks while retaining planned circulation. |

## Fabrication, joints and connections

How separate steel parts become an assembly.

| Term | Kind | Meaning |
|---|---|---|
| Butt joint / splice | Joint | Two aligned parts joined end-to-end; common when a long stringer is assembled from shorter lengths. |
| T-joint | Joint | End or edge of one member joined to the face of another, such as a web to a face sheet. |
| Fillet weld | Joint | Weld deposited along the interior corner of two intersecting metal surfaces. |
| Full-penetration weld | Joint | Weld designed to fuse the complete joint thickness as specified; commonly relevant to pressure-boundary butt seams. |
| Mitered joint | Joint | Cut and prepared joint accommodating an angle change in a beam or stringer. |
| Bracket / gusset | Part | Reinforcing plate connecting members and distributing concentrated load. |
| Penetration | Feature | Designed crossing of a pressure boundary by pipe, cable, duct or structural system, requiring appropriate sealing/details. |
| Attachment interface | Engineering | Specified mating geometry and connection requirement between two parts. |
| Manufacturing segment | Part | Part divided for practical cutting, transport or welding; division must not break assembled continuity. |

## CAD generation and verification

Words that describe the software model rather than the ship itself.

| Term | Kind | Meaning |
|---|---|---|
| Semantic system / category | CAD | Classification such as Hull, Transverse, Longitudinal, Deck, Closures or Interior. |
| Assembly | CAD | Engineering object comprising one or more parts and connections; e.g. FRAME-F16 or KNUCKLE-3. |
| Part | CAD | Individually identified manufactured component within an assembly. |
| Geometry authority | CAD | Single reference representation from which dependent shapes are derived; e.g. the hull loft. |
| Polygon offset | Geometry | New profile at a specified normal distance from a 2D polygon, used for inner hull or web boundaries. |
| Polygon with holes | Geometry | Material region bounded externally and by one or more internal void boundaries. |
| Extrusion | Geometry | Creates a 3D solid from a planar profile and thickness/direction. |
| Loft | Geometry | Generates an intervening surface or solid between station profiles. |
| Sweep | Geometry | Moves a structural cross-section along a 3D path to generate a beam or stringer. |
| Triangulation | Geometry | Decomposes a polygonal face into triangles for mesh representation; triangle edges are not weld seams. |
| Watertight mesh | Validation | Closed mesh surface with appropriate edge connectivity. Necessary for robust solids, but not proof of structural adequacy. |
| Connection graph | Validation | Model of members/assemblies linked by specified joints; useful for detecting disconnected load paths. |
| BOM (bill of materials) | Output | Manufacturing schedule of parts, quantities, materials and calculated mass. |
| Drawing projection | Output | 2D technical view derived from the actual 3D model, not a separately invented approximation. |
| LOD (level of detail) | Rendering | Different display complexity for full-ship, structural-inspection and fabrication views of the same assembly. |

## Engineering note
A valid mesh or a successful structure audit does not prove stress, buckling, fatigue, weld, or pressure adequacy. Knuckle paths follow facet intersections; arbitrary flat-plate weld seams are different.
