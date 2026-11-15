# NPC-only measured elevator evidence (pure checkpoint)

This package does **not** change mission selection, admission, checkpoints, NPC placement, door publication, movement or restore. There are no runtime callers. It is the audited evidence/resolver prerequisite for a separately reviewed NPC-only policy. It does not claim that Tabor can now be completed ordinarily.

The committed primary profiles cover explicit world1 sectors550 (Tabor) and1349 (retail control) only. Every original placement ordinal is retained; unavailable prefab/INAV evidence is explicit and never becomes a route. The existing `data/mission_elevators/1` tree and all v1/default consumers remain unchanged. World2 export and sector842/Bornmann policy remain separate backlogs.

## Reproduction and authority

From the repository root, with the read-only original7.6005 client-data tools:

```
python research/world-data/mission_interior_evidence.py <client-data-tools> --world 1 --sectors 550 1349 --output <owned-output>
```

The exporter reads original counted SIDI placement/GO arrays, original prefab INAVs and the direct SCIN parent. It retains source RID/offset/hash, original ordinal/region, raw74-byte records, transformed bounds/native model midpoint and per-GO quaternion; counted26-byte portal tables retain raw records and table bounds. It does not rewrite/select gameplay sources or emit coverage snapshots. The manifest pins original CSV/resource/transform evidence and exact compressed payload hashes. The runtime-independent loader additionally pins the committed normalized manifest; a coherently replaced dataset is not trusted merely because its internal hashes match.

The source portal record is LE`u16 region,u16 polygon,u8 edge,u16 targetRegion,u16 targetPolygon,u8 targetEdge,4*f32 endpointXZ`. A cross-region edge is usable only with one exact reverse record, both referenced INAV edge endpoint pairs and equal endpoint floor elevations. Exterior/unavailable region evidence is retained but never traversed. Local INAV neighbor polygon indices are preserved; `regionTag=ffff` alone is not a wall (`neighborPolygon=ffff` is absent adjacency). The resolver does not broaden `NavMesh.OpenBetween` or any global `Contains` policy.

## Literal primary controls

Tabor raw floor door22602817 at40300/6145/25800 is independently native40000/6145/25500. Exactly one unchanged ordinary entry calculation selects region125/poly30 at39810.81081081081/6095/25564.864864864863; the resulting context selects the door's yaw. It does not look for a more convenient region. The original reciprocal opening125/0/2↔43/39/1 spansX38470..38530/Z25800 atfloor6000. Authored graph reachability from that root to the actual raw Ophiuchi marker226005f5 standing38490/6095/26980 (43/poly245) is26hops. Exact planned marker identity is preserved separately from its transformed original source; no NPC is moved to the native marker.

Required wooden door2260056b uses the actual selected raw GO346. Its own original native model root is38500/6145/25800, quaternion`00000000f30435b300000000ffff7f3f`; the paired343 record independently agrees here. An unsupported/missing required door refuses the route proof. A topology proof plus supported pose is **not** a native walking/collision/door-open certificate.

Retail-positive control: Recursion capture2009-07-05 19:57:25 RPC321 `80C84B0150540300`; next-second bound GO346 with RelatedStatic5450014b, Model.Position59400/-55/11000, quaternion`00000000f304353f00000000f204353f`, CurrentState0834. Direct original SIDI GO346/region105 reproduces the pose exactly. PairedGO343/region117 has a DIFFERENT yaw, so the selected GO's pose must never be borrowed from another type. The focused test includes the literal creation bytes and independent existing wire decoder. The paired model resources/skeletons keep the group/frame root fixed while the leaf rotates in opposing directions; no leaf/joint offset is added to groupPosition.

Client primary anchors: HandleCreateObject622d9510; bound static lookup622be5e0→62303380; existing-static attributes applied via62304300 at622d9c0a. Native static factory6231ae73..aeff computes f32(boundsMin+boundsMax)*.5, bundled as Model.Position at6231af25..56. Original DLL SHA`cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a`, GOB SHA`3e47f11012f6f219a26c9f5decfa10cbc00726c397937d03c3068254e5424103`.

## Checkpoint and remaining work

This index has no mission, SQL, timer, transport publication or packet mutation entry point. Result arrays own their data. Future integration requires a NEW tagged NPC witness/pending policy, exact accepted-owner/phase/world/life/transport binding and a reviewed old-state refusal boundary; v1 must not be reinterpreted and old pending/fixed raw state must not be migrated or rerolled. The Stage/Accept/Advance/restore/overlay map will be reviewed before those files change.

Before any ordinary-completion claim, Claude must verify actual Tabor metal elevator and wooden Door/Open hover/pick, native group pose/leaf motion and ordinary passage/exit, followed by normal Give/one receipt/next offer on the separately reviewed producer. The retained old ?missionfloor/carry/Give result remains historical partial evidence. Missing world2 prefab8a000000 is a synthetic ordinal-preservation negative in tests, not a claim that Tabor's source is missing. No native or fresh-server run is part of this checkpoint.
