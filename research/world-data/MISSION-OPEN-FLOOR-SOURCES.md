# Authored static bounds for mission open-floor placement

The runtime sector packs contain source-backed **authored spatial bounds**. They
are separate from structural NAV and the three proxy models' measured animated
envelopes. They do not certify physics, GPU arithmetic, avatar clearance or native
picking. Source statics are read-only inputs.

The pinned client DLL is
`cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a`;
the GOB is
`3e47f11012f6f219a26c9f5decfa10cbc00726c397937d03c3068254e5424103`.

| Domain | Source authority | World composition |
| --- | --- | --- |
| GO Model6 | Property slots1/2/3 are Position/Orientation/HalfExtents;3899-layout parse and binder625ce485..4bc. Initial625cbf4f and update625cc0e8 call626600d0 then62099000;624993d0 consumes the same registered min/max. | Canonical counted SID/SIDGO/SIDI instance pose; union full quaternion-corner bounds and native floor/ceil registration. All conflicting variants remain obstacles. |
| ROOT-OSID PROP | OSID115 buffer6,624c0a90→6246ae50; actual spatial getters624a5d50/624a5dc0 are consumed through SCIN624dc0c9. | Union raw authored bounds with native packed bounds; add actual SCIN translation. Do not rotate the already-placed raw AABB or add prop XYZ again. |
| IPI inline PROP |62476030 reads placement+70/+78 at624762b4..6247631d; the intervening link array has26-byte records. Same6246ae50 PROP decoder. | Typed IPI628bf088+4c→62469530, rotates via+44, adds signed pivot×10 and reorders bounds;62469150..179 adds actual SCIN translation. |
| EPI inline PROP/GOIN | ROOT-SID buffer1,624bfd80→624b93b0. GOIN reads72+2 bytes at6246af00. Inline PROP count+ec/array+f4 is read at624b9622..96be. | Actual EPI628c1a28+10→624b4a30;6249f093..ba passes SCIN translation to624b7ce0,624b81ba..8208 combines the parent,624b869c sends it to PROP rendering. Union direct-parent and composed-world-quaternion Model6 profiles. |
| ROOT-SID buffer8 |624c0b70→624c0130 reads key, centre, half extents and opaque blob;624bf010 selects that exact record. | Actual6249ecae..6249eda5 computes centre±half and adds SCIN+10 before culling/rendering. The key is not asserted to be an asset RID. Zero extents remain explicit. |

Packed PROP min/max triples use native truncation toward zero:
`i16(trunc(f32((rawMin−9)*f32(.1))))*10` and the corresponding `rawMax+9`.
The conservative union avoids inward truncation. No guessed model height or
server `+145` offset belongs in these native obstacle poses.

The33 secondary IMAP resources referenced by selected sectors127/209 were
independently parsed through exact EOF. Their INMP101 schema is a bitmap plus
grouped four-i16 map records (reader624c9b30; bitmap upload624c98d0). They contain
no independent3D model/pose list. EPI's four-u16 locator-link arrays likewise are
not child placement poses. Primary IPRF/EPRF structural meshes remain distinct
from persistent object bounds; structural walkability comes from NAV.

Model6 absence is domain-specific, not a claim that FX, flock or NPC actors do
not render. Valid fixed Model6 extents remain conservative obstacles even for
conditional/DescriptionID0 templates. Invalid bounds are unknown, never empty.

## Packaging and reproduction

`mission_open_floor_gob_catalog.py` reads the configured client GOB resource and
emits exact Model6/component/span evidence. It accepts `--goids` as a JSON file
or comma-separated list. The627-template source inventory reproduces449
positive authored profiles and178 Model6-absent profiles.

`mission_open_floor_world_bounds.py` independently exports canonical GO,
ROOT-OSID and inline IPI source geometry. `mission_open_floor_aux_bounds.py`
exports EPI and buffer8 geometry. Both accept `--tools`, `--catalog`, `--sectors`
and `--output`. `--sectors` selects **source origins**; use `all` for cross-sector
source traversal. Neither exporter treats a local-origin subset as a complete
overlap query. SIDI's remaining non-obstacle tail is retained by hash, not
claimed semantically decoded.

The initial audited profile-export arrays are named and SHA256-pinned under
`inputCorpora` in `data/mission_open_floor/1/manifest.json`.
`mission_open_floor_scene_pack.py --inputs <profile-export-directory>
--sectors data/sector_properties.csv --output data/mission_open_floor/1`
deterministically packages those exports. The portable source exporters provide
an independent geometry reproduction; their richer evidence schema is distinct
from these initial compact profile-export arrays.

The packer assigns every obstacle to **every** sector rectangle its AABB
intersects, including remote-source overhangs and all heights. Its1000-unit grid
is only an exact-search accelerator. It preserves all conflicting source
variants and collapses only identical normalized records. Per-row evidence and
ordered profile versions support scene fingerprints; the global manifest hash
is not a mission's saved-pose fingerprint.

The1493 lazy JSON-gzip packs contain2,893,246 sector memberships. Missing declared
SIDI556/SIDGO1139 explicitly make those two scenes incomplete. The runtime must
refuse a targeted placement there rather than use an unsafe legacy pose.
No corpus for other worlds is asserted by this package.

The final127/209 packs have5,338/3,115 obstacles. Those counts include all five
source domains and cross-sector overhangs; they are not radius or floor cuts.
Each pack's raw-source qualification, format, coverage rectangle, profile
versions and SHA256 are reviewable in its manifest. Generated server coverage
and ability-parity snapshots are not part of this data.

`anchors_1` is a separate source-only index:535 lazy CSV-gzip files contain
1,013,197 canonical native/legacy-associated keys. It changes no chooser,
eligibility or concrete prototype. `mission_open_floor_anchors.py --tools
<resource-tools> --output <directory>` reproduces it directly from the client's
counted GO records and both repository Slums CSVs. Native/legacy conflicts,
missing-layer sectors, type mismatches and exterior rows are omitted. Every
accepted row retains the exact legacy XYZ/full quaternion plus native XYZ/full
quaternion, identity and source-record provenance. It includes all65,404 earlier
Computer anchors byte-for-byte in these fields and covers other spawners and
furniture fallback sources. Selected unsupported orientations still refuse;
unrelated rows do not invalidate a whole sector.

`source_models_1.json` independently supplies positive Model6 presence/absence
for627 source templates. A missing obstacle never becomes evidence of absence.
The packer's `--models-only --source-model-catalog <catalog> --output
data/mission_open_floor/1` regenerates this lookup without rebuilding geometry.

Run source/parser/pack regressions with:

```text
python -m unittest discover -s research/world-data -p "test_mission_open_floor*.py" -v
```

Native walking, hover, radial use and same-world redraw remain separate tests.
