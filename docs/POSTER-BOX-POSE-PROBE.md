> Historical diagnostic. Its serialization overlay and discovered mode/case registrations are retired by the shared open-floor resolver. The retained native/source results do not validate the new placement policy.

# Poster Hunt Box pose/picking diagnostic

**Diagnostic only.** This package compares two serialized poses for one existing Box 548 proxy. It does not implement a default placement fix or prove a retail Box pivot, model, picking or reach rule.

## Exact source and literal geometry

Under the existing layout seed 201202, normal Poster Hunt 201202 phase 0/object 1 selects source 1415, key `0x0d1003dc` (CSV `dc03100d`), sector 209/static ID 988. Current legacy pose is `(23940,2245,15050)`, quaternion `00000000f204353f00000000f304353f`.

The counted source record is independently retained in `research/world-data/poster-box-pose-evidence.json`: `slums_13.sidi`, RID 838865252, flags 4, SHA256 `da3a2ca249546a4a379ecf53ea6df30119c63924e712c7a1a762e51c235f17bb`. Placement 36 contains 36 records; record 1 at offset 234783 is GO1415/static988. Parent pivot `(200,200,580)`, quarter-turn 2, sector corner `(22000,0,10000)` and local position `(60,100,750)` give:

```
X = 22000 + 200*10 - 60  = 23940
Y =          200*10 +100 = 2100
Z = 10000 + 580*10 -750  = 15050
```

Independent Hamilton composition preserves the exact float32 quaternion `00000000f304353f00000000f20435bf`, without normalization. This matches the audited Computer anchor row, used only as reference geometry. **No GO3 is spawned or substituted.** A Box at a Computer marker still has an unproven asset/pivot relationship; the proposed source pose is a diagnostic candidate, not a retail Box placement claim.

## Scope and guards

New opt-in variable `MXO_POSTER_BOX_POSE_PROBE` accepts only:

| Value | Serialized Box 1 pose | Quaternion |
| --- | --- | --- |
| `control` | `(23940,2245,15050)` | Original legacy bytes |
| `source-native` | `(23940,2100,15050)` | Exact source-native bytes |

The guard requires dev 1, the **actual configured** valid `mxotest_` database, actual live CSR_Tester character 110 with UDP/on-world readiness, district 1 and no LA/world-change/death/duel, current owned active mission/progress 201202 phase 0, object 1 of kind Box, concrete 548 and exact unchanged source key/type/pose/quaternion. Unknown values, other actors, DBs, missions, phases, objects, custom source poses or future changed base poses return the existing pose object unchanged.

Only the final spawn Position/Orientation serialization is derived. Source CSV, saved spot, prototype, other objects, default components, Hidden, bounds, state, mission key, UseCount, free-object identity, interaction authority, object-relative goals, persistence and rewards are untouched. The hook does not hide a static marker or move an authoritative interaction point. It adds no guessed height offset or range workaround.

## Grounded approach

Existing compiled NAV Project and Locate validate `(24080,2095,15080)`, sector 209/region 54/polygon 87, floor 2000. Projection from the recorded normal elevator landing's component finds it; direct self-projection retains it. An independent geometric shared-edge route from the normal landing to this polygon spans 33 polygons. Temp evidence is `mxo-poster-box-pose-approach.json/.ps1` and `mxo-poster-box-pose-nav-path.json/.py`.

Its model-centre distance is 143.265 units. Centre distance is not distance to oriented bounds and does not establish native usable reach. The NAV evidence does not prove native walking, collision, camera or LOS.

## Headless diagnostic plan

Cases `poster-box-pose-control` and `poster-box-pose-native` use **actual CSR 110** seeded only inside each owned disposable Harness DB, following the existing PR194 pattern. They use normal Agent Gray list/info/accept, unchanged selected source and existing elevator/test-movement helpers. They assert actual Box 548 XYZ/quaternion, unchanged poses of the other two proxies, unchanged mission key/UseCount and non-pose creation fields matching unmodified sibling boxes.

Before/after saved object records, source poses, prototypes and states must match exactly. Goals/items, inventory, XP, information and completion receipt remain unchanged. No Open, Take, completion or payout is sent or required. Raw RPC/03, decoded Box fields, checkpoints and actual selected DLL/configuration/hash are saved uniquely before teardown on success or failure; a proof-write failure cannot silently pass. StillNpcs and test movement are not native survival or walkability proof.

## Claude-only native comparison — pending

Use separate fresh modes `poster-box-pose-control` and `poster-box-pose-native`. Both are actual level/XP 50, Machines, Mara, ordinary support abilities, exact earlier-only Poster Hunt receipts, existing seed 201202, no GM or forced RNG. Selected 201202 is not granted.

1. Call Agent Gray > Critical > Poster Hunt, accept normally and use the ordinary elevator.
2. Approach the target near `(24080,2095,15080)` on floor 2000. The geometric path is not a recorded native walking witness.
3. Compare the actual visible Box, hover, targeting and radial Open between the control and source-native fixtures. Record C2S337 and C2S80c7 if an action is selected. A tooltip alone is not selection proof.
4. No full mission completion is required. If ordinary Open completes goal 1, that is the existing action path, not diagnostic progress injection. Do not infer a default placement fix or change Hidden, model, bounds, reach or offsets from a failed comparison.

The current Box picking problem remains unresolved until this visual/action comparison is observed. Native QA/setup/client lock and merge are Claude-only.

## Local checks before fresh review

**68 scoped tests passed**: strict authority/source/variant guards, default-pose identity, source/state immutability, future normal-pose protection, independent literal quaternion/position, actual NAV grounding, mode scoping, existing Poster Hunt content and existing Computer-anchor regression units. Both actual Debug/Release NativeQa builds, all four read-only diagnostic plans and assembly provenance passed. Python snapshot policy: nine passed. No broad full-suite repeat was run for this QA-only hook, as agreed.

Checked hds hashes: Debug `a8fa30b2e9d8ee515505ceb243532bf253d3fbe8f83e6f0bcd62c31236f48658`; Release `b6cd33102f328f67cfb52c8f17efc07ba0886c664dda7ec93e659c1ec69619ad`. No fresh or native diagnostic result is claimed yet.

## Fresh serialization results

Frozen source `1e6c35c3a4e1990a3aef454bd40f32973a00e303`, two serial cases with `--parallel 1`:

| Case | Duration | Actual first Box 548 position | Actual quaternion |
| --- | ---: | --- | --- |
| Control | 22.7 s | `(23940,2245,15050)` | `00000000F204353F00000000F304353F` |
| Source-native | 22.4 s | `(23940,2100,15050)` | `00000000F304353F00000000F20435BF` |

Both passed with exit 0 and peak one server. All owned servers disposed; slot 1 was released. The worktree and binaries were frozen throughout. Actual selected configuration was **Debug**, hds SHA256 `a8fa30b2e9d8ee515505ceb243532bf253d3fbe8f83e6f0bcd62c31236f48658`; this is not a Release fresh result.

Each case used actual CSR 110, normal Poster Hunt acceptance and the exact naturally selected source key. Saved object arrays were byte-equivalent before/after serialization, the other two Box poses were unchanged, and no goal, item, inventory, XP, information or completion receipt changed. Non-pose creation fields matched unchanged siblings within each case and also matched byte-for-byte across cases: `UseCount = 01`, `MissionKey = FF2F0002`. Both retained 48 actual RPCs and nine view lists with zero parser problems. No Open or completion action was sent.

Required proof was saved before teardown:

| File | SHA256 |
| --- | --- |
| `Temp/mxo-poster-box-pose-control-mxotest_run_1_2f9aaa8204.json` | `5a679b6a61be2a5f5db9ff14e6c461d394ab0ff7cf4fc79b68dc1a9095dd0171` |
| Its `.log` | `9be4bd83da65013852646f68d002f57f802a74648dc95e646850d295b5876b36` |
| `Temp/mxo-poster-box-pose-source-native-mxotest_run_1_52e38f68a3.json` | `a282d0601185c49cbf408db92e1bf33185577f359a8e86250c8a84456a699aef` |
| Its `.log` | `d99b76c8a35beb27b97f124a47b07d3876cf7beb3f982997ce46b1704d5c4a01` |

The complete file/assembly/creation-field manifest is `Temp/mxo-poster-box-pose-final-evidence.json`. Permanent stdout and explicit exit records use the `mxo-poster-box-pose-*` Temp prefix.

These passes establish the isolated wire comparison and invariance only. **The draft diagnostic is not a native model/picking pass, default placement fix or merge-ready gameplay change.** Claude's subsequent visual, hover/target and radial-action comparison is still required.
