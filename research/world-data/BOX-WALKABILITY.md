# Offline Box 548 geometry feasibility

This experiment consumes the shipped `data/navmesh/1.bin.gz` and sector properties read-only. It does not participate in mission selection, admission, staging, saved checkpoints, interaction authority, rewards or the server. It makes no gameplay or native-parity claim.

Run from the repository root:

```powershell
python -m unittest discover -s research/world-data -p 'test_box_walkability*.py' -v
python research/world-data/box_nav_audit.py --repo . --cases research/world-data/box_walkability_cases.json --output C:/Users/russe/AppData/Local/Temp/mxo-box-walkability-offline-results.json
```

Use an owned output path. The command does not write the NAV pack, catalog or historical audit snapshots.

## Inputs and policy

`plan(tuple[Surface], Root | None, RawPose)` accepts immutable world polygons, an explicit root polygon/standing point/evidence string, and the original position/quaternion bytes. It validates finite data, convexity, root identity, containment and standing height itself. A missing or remote root is refused; it never selects a nearest floor or silently drops the root.

The reader accepts MXNV versions 2 and 3, checks the embedded district, counts, prefab references, quarter-turn rotations, truncation and trailing data. Its transform matches the existing NAV exporter: signed placement coordinates in tens, quarter-turn world transform and floor planes. It records compressed/decompressed NAV and sector-properties SHA256 values. Only the explicitly requested sector set bounds the experiment. No connectivity outside that set is claimed.

The planner traverses only exact equal-height flat convex polygons connected by a positive-length shared edge. Ramps, different heights, corner-only contacts and uncertain/nonconvex polygons are not traversed. It does not use production Project, its provisional outputs, inset fallback or fail-open sight helper. Numerical tolerance is `1e-7` world units (edge-scaled for route/base clipping); there is no search radius or grid.

For each reached polygon, convex half-plane erosion computes every centre that fits the entire rotated **closed base** footprint in that single polygon. It selects the nearest point to raw XZ, breaking ties by stable polygon ID and coordinates. No point or corner outside the polygon is admitted through production EdgeSlack. A zero-area feasible region is conservatively rejected.

The measured base MGA (`0x2800086d`, SHA256 `10ed8bc5b72e6b13d3b92c65e6e98944a9bc90287a0829615cb7892837d90af2`) has AABB `[-50,-40,-70]..[50,40,70]`. Its MOA (`0x28000872`, SHA256 `76e59536d70505c4a22799fa1b211f8ec32f922a06278dd9ade72cece04f211a`) uses an identity root. Yaw-only quaternion input is required. The calculation normalizes only the near-unit f32 yaw for mathematics; output preserves the original quaternion bytes. Pitch/roll and non-unit/invalid inputs refuse. Centre Y is the actual surface plus 40; an avatar standing point is surface plus 95. Source marker heights and site median NPC-spawner heights are not floors.

The approach is the actual final shared-edge ingress midpoint into the destination polygon, or the actual root when the destination is the root polygon. Every route point and segment must avoid the entire closed rest base, including edge/corner/tangent contact; a root inside/on the base rejects that candidate. The old final segment to a root-projected approach could cross the Box itself and is no longer emitted. Candidates with blocked ingress routes are conservatively rejected without moving their nearest feasible centre, trying alternate routes or repacking. Shared-edge midpoint witnesses connect the explicit root to that polygon. These prove only geometric point/segment feasibility. They do **not** certify avatar radius, animation flap sweep, source/static/SCIN decoration clearance, collision/LOS, portals, actual native walking, use range or picking. Approach distances in XZ and 3D are reported without filtering; `useRangeValidated=false` is explicit. No sourced interaction consumer is invoked by this experiment. Feasible-polygon counts are not simultaneous Box allocation capacity; only one Box is planned.

## Four named cases

The cases preserve the selected source and layout seed; they do not invoke or change selection. The two Zion inputs come from the peer's normal offline Choose/PlaceObjects inventory at seed 101208. The actor-entry integration remains unimplemented.

| Case | Raw centre | Conditional candidate centre | Root limitation |
|---|---|---|---|
| 101209 phase 1, source 1444/key `22b00254` | 31950 / 1349 / -6600 | approximately 31730 / 1240 / -6850 | Explicit peer-validated standing approach 31850 / 1295 / -6620, polygon 555/45/94. Normal elevator-to-root traversal is not proved. |
| 101209 phase 2, source 1415/key `4be0055b` | 6715 / 1022 / 15005 | 6855 / 840 / 15070 | **Explicit analytic caller input**, not a normal player entry: NAV polygon 1214/65/6 below the selected NPC XZ 6900/15150. NAV standing Y895 differs from source NPC Y896. The original failed Project's provisional point is never used. |
| Native 209 open-floor control | 25100 / 2040 / 14100 | unchanged | Existing normal landing context 25579.31146596289 / 2095 / 13959.481277120463, polygon 209/63/3. Native 209 proved the final asset/Open, not this complete route. |
| Native 203 failed source 1415/key `0d1003dc` | 23940 / 2100 / 15050 | 24250 / 2040 / 15470 | Same explicit landing context as 209. This geometric candidate has no new visual-empty-space certification. |

Every case also reports `withoutExplicitRoot: NoPlan`. The phase 2 result establishes conditional geometry feasibility around a supplied standing context; it does not establish normal mission admission, reachability from its elevator, multi-object capacity or a safe production replacement. The unchanged open-floor control is a useful regression, not proof that every room is clear.

## Limits and next decision

All source spots, quaternions, mission objects, prototypes, checkpoints, use rules and pending Safe/Computer behavior remain unchanged. No native mode or production helper was added. Before a visual probe or production package, review the actual normal entry/root bridge, scene occupancy and approach on the client. Any later production integration needs a separately reviewed admission/capacity and saved-pose policy; this experiment provides none of those guarantees.

## Actual live218 inputs

`box_walkability_live218_cases.json` adds four retained live checkpoint inputs, reconstructed **offline** with explicit normal-entry roots. They are not the two named offline seed choices above. The normal `MissionSites.Build` source blob is `9c7cedde8c1fb670a4d466ece74e11b507751343`; entry arithmetic is `MissionStaging` blob `0c718a67549323210e59b76d81c88a6eda485644`. Existing compiled `hds.dll` SHA `F5A14557C90D08C0BE99419ED5823CFFC0137ED037DD61CAD38A60ED6DCC9E64` performed Build/PlaceNear/Locate. No new server was run. Each root is standing surface+95. Saved elevator identity **and XYZ**, then exact selected source membership, uniquely identify the site. Two elevator identities have multiple catalog records; the other record has no matching selected source. This qualification is retained rather than treating door ID alone as unique.

| Retained run / phase | Source / raw position | Derived normal entry / polygon | Full-base candidate | Ingress approach |
|---|---|---|---|---|
| `51e509462f` / 1 | 1412 `0d10029d`; 24050/3471/14950 | 25550.263656786137/3295/13932.585925048399; 209/60/3 | 24110/3240/15120 | 24200/3295/15125 |
| `51e509462f` / 2 | 1412 `45e003c5`; 39965/605/14460 | 40970.71067811865/495/15470.710678118654; 1118/52/8 | 39929.999995785314/440/14460 | 39900/495/14400 |
| `cae96c70b5` / 1 | 1415 `22b00184`; 29055/625/-7485 | 30278.80849421055/495/-6998.874131254861; 555/41/3 | 30110/440/-7130 | 30130/495/-7000 |
| `cae96c70b5` / 2 | 1415 `12a0028a`; 35150/1445/-20055 | 35970.71067811865/1295/-20529.289321881344; 298/63/8 | 35150/1240/-20270 | 35130/1295/-20400 |

The first run's whole scenario FAILED85.2s at a later reward assertion; the second PASSED74.6s after an unrelated fixture correction. Their SHA256 values are respectively `faeed15493b3f94120cbaf0984413494d874ee36898b8e6f1d070505d58f59bc` and `41a8779eea3ede61d5976669cf239781df2fab69c5210db91784921908956ab8`. Both are real retained source choices; a fixed QA seed did not make them identical. Each case preserves raw quaternion bytes, source key, selected site bounds, current elevator and proof hashes. The planner itself does not select sites or impose those bounds; regression tests independently verify that these four bases fit their recorded existing site bounds.

Reproduce separately:

```powershell
python research/world-data/box_nav_audit.py --repo . --cases research/world-data/box_walkability_live218_cases.json --output C:/Users/russe/AppData/Local/Temp/mxo-box-walkability-live218-fixed.json
```

The former projected approach's final segment crosses the newly placed base in three of these four contexts. Those exact failed segments are retained as regression assertions, checked by an independent edge-intersection/even-odd oracle that imports no planner collision helpers. Pre-correction outputs remain in Temp `mxo-box-live218-geometry-unbounded.json`, `mxo-box-live218-geometry-bounded.json` and `mxo-box-live218-summary.json`; they are historical failed route witnesses, not corrected walking proof. Literal root-inside, edge/corner contact and portal-touch tests cover conservative refusal.

This correction supplies no production admission, persisted-pose, multi-object packing or scene occupancy policy. It does not certify avatar radius, portals, collision with other scene props/NPCs, flap sweep, line of sight, interaction range, picking or native walking. The actual-root reconstructions are input evidence, not live mission re-staging or native verification.
