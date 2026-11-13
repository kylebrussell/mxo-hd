# Safe 552 floor diagnostic

This standalone QA-only experiment tests the actual free Safe asset, hover and radial picking. It does not repair PR197's mission anchor, change any source/catalog/model/bounds/range/rule, or register a mission/loot/animation/reward executor. Normal gameplay and default fixture modes have no authority to create it.

## Primary source and failed native boundary

[Source evidence](../research/world-data/safe-floor-source.json) preserves the full GO 552/1410 components/defaults, GOB hash, all four mesh hashes, skeleton/model resources and NAV witnesses. GO 552 is class 46, with Model type 6, Info type 12, Container type 11, OpenUse type 5 and StateMachine type 36 among its full components. Description resource 28000654 is `resource/GameObjects/Containers/large_safe/large_safe.moa`, SHA256 2c86628f698b840d97b813895b3b1a3eea48613270ece9ea9639bdc1eb522604. The actual skeleton is 2800064e, SHA256 b8361d43f14a032924eb0b602ecd2ab1d3cb5eebc5f9878b810767bdeb818e56. Its root 0 / parent −1 has identity matrices, so no root translation is invented.

The four indexed mesh resources have blank filename aliases; no names are fabricated. Each has exact 63-byte header + vertexCount×32 + triangleCount×6 accounting. Root-bone base 28000653 has 48 vertices / 28 triangles and rest AABB[-39.400635,-50,-34.000004]..[39.400635,50,34.084763]. Other meshes are bones 1/2/3; their hashes and measured bounds are retained. Closed BASE geometry is not proof of full animated-door sweep or native collision/picking.

Raw default CurrentState 835 enables Open 3 and disables Pick Lock/Unlock. OpenUse ClientRule/ExecuteRule1010, Defaultable 1, MaxDistance 100. Shipped clientrules.ContainerOpen returns success for UseCount nonzero and no AI, or matching mission key. No missing-state/range patch follows from the failure. Existing native Model type 6 init625d09c0 and attachment625cbcc0 copy the complete template and attach the default description; received position is translated raw by the proven type11 render path. That path does not establish actual resource-load success in a failed native session.

Claude's PR197 failure at70a748c9 showed two filing cabinets and no separately recognizable Safe. Retained PacketLog `mxo-headless/c9673bf80d17/PacketLog.txt` lines686 and1013 positively contains free0c GO 552 at 29100/2001/14900 before/after relog, with orientation, Position, UseCount 1 and MissionKey, without a wrong explicit model/state/bounds override. This proves delivery identity, not asset draw. The screenshot does not identify the obstructing static GOID. At that source pose the measured base would extend to Y 1951 against floor 2000, but this is not authority for a production offset or source mapping repair.

## Diagnostic geometry and authority

The probe is fixed at 25100/2050/14100 with an explicitly documented identity quaternion baseline. Y 2050 places the measured closed rest minimum−50 on source floor 2000. It preserves the real GO 552 template. It sends default Closed 835, UseCount 1 and MissionKey 0; it does not override HalfExtents, model resource or usability rules. The old mission source quaternion remains untouched.

NAV209/54/12 is rectangle[25000,14000]..[25200,14200], floor 2000. The entire measured closed base footprint and standing25100/2095/14020 lie inside it. The standing-to-centre distance is sqrt(80²+45²)=91.79, under100 even with centre distance; no range enlargement is needed. This is a point/base NAV witness only: it does not certify scene/static/avatar clearance, animated sweep, a normal elevator route or native availability.

The guard requires exact dev=1, explicit MXO_SAFE_FLOOR_QA=1, a fully validated actual disposable mxotest_ database, current CSR110 reference, alive/ready Slums world, no death/shutdown/pending transition/Loading Area and no buffered mission output. A unique private entity/view lease captures data/view-manager/world/life/transport/view/spawn identities. Encoding and Flush occur outside the transport gate; only scalar checks/allocation/direct enqueue occur inside. Cleanup targets only that exact private free view. Reset cleanup is epoch-qualified; an old reset cannot remove a newer lease. No source static is hidden, bound, moved or deleted. No MissionProgress object, owned item or completion receipt is inserted by the helper.

## Claude-only one-pass

1. Select discovered mode `safe-floor`. This is an explicit QA starting position in the control room, not proof of ordinary traversal. Inspect the space first and retain a baseline screenshot. If obstructed, report it instead of moving the probe by guess.
2. Type `?safefloor on`. Inspect the actual Safe model at 25100/2050/14100; turn/look down normally. The supplied standing point25100/2095/14020 is within the unchanged100 range. Check hover, selection and radial **Open**.
3. Choose ordinary Open and retain actual `80c7 <view/spawn> 03 00` plus `[OBJECT-USE]` identity. This diagnostic has no container executor; do not expect loot, mission ticks, opening animation or rewards, and do not claim them from a request log.
4. Type `?safefloor off`; confirm only the diagnostic Safe disappears. Repeat on/off once and retain distinct view/spawn logs and screenshots. Reset/session cleanup removes the lease.

A visible/selectable result isolates the asset from PR197's mission placement/occlusion question. Failure bounds the free552 presentation/picking/defaults question. Neither result repairs the mission source or proves a retail chooser. Native observations remain pending Claude.

Reproduce the asset-only measurements without writing client files:

```powershell
python research/world-data/inspect_safe_floor_asset.py --tools C:/Users/russe/Developer/mxo-research/tools/client-data --output C:/Users/russe/AppData/Local/Temp/safe-floor-asset.json
```

The script pins the MOA/skeleton, checks each exact mesh size and preserves blank aliases. The committed NAV witness includes input hashes and exact floor polygons; it does not infer visual clearance.

## Local qualification before fresh review

The current full suite passed 4,800 tests; final focused Safe/Box-probe/protocol checks passed 52. Both actual Debug/Release builds, read-only safe-floor/poster-box-floor/default plans and provenance passed. The read-only asset reproducer passed against the pinned client resources. No fresh or native run has been performed for this package yet. The initial standalone creation test failed because its test wrapper omitted the master-view prefix; framing was corrected without changing production bytes. Initial compilation caught a namespace and existing Loading Area accessor mismatch; both were corrected before these passing checks.
