# Exact3072 computer pose diagnostic

DIAGNOSTIC ONLY. This adds no default placement fix and makes no retail-coordinate, native picking or mission-completion claim. Three isolated profiles compare the one failed Retrieval3072/Object1, dynamicGO3, original sourcekey7611b015. No ambiguous clones and no arbitrary pose command.

| Profile / env enum | Serialized Position | Quaternion |
|---|---|---|
| mission-object-pose-control / control |92740,4445,13535|original00000000f20435bf00000000f304353f|
| mission-object-pose-native-y / native-y |92740,4300,13535|same original|
| mission-object-pose-full-native / full-native |91865,4300,12660|identity0000000000000000000000000000803f|

The former4325 HalfY guess is deliberately absent: the renderer's rawcopy evidence does not justify an automatic HalfY adjustment. The FullNative tuple is the independent source-composed diagnostic candidate, not a universal coordinate policy. Native visual/picking results are required before any general anchor rollout.

## Guard and unchanged authority

MXO_MISSION_OBJECT_POSE_PROBE accepts only those three exact enum strings. It also requires dev exactly1, ACTUAL Store.config database with a valid mxotest_ name, live authenticated UDP/inworld CSR_Tester char110, district1/nonLA/nontransition/nondead, exact current mission/progress3072/phase0/nonfailed, exact ownedObject1/Computer/GO3 and original source identity. An envDB name cannot spoof a foreign actual database. The source is sector347, key7611b015 (native0x15b01176), static0x1176/sourcebytes76110000, sourceGO1415/type8705, original92740/4445/13535 and originalquat. The raw row is data/staticObjects_slums_1.csv277937.

Only the two serialized Position/Orientation fields in SpawnObject use the derived pose. State.spot/sourceCSV/index/checkpoint/site/waypoint/RelatedStatic/0c/entity/MissionKey/CurrentState/UseCount/Usable/model/HalfExtents/NPC/reach/inventory/reward/economy remain unchanged. Missing/invalid guards yield byte-identical default source serialization. If upstream placement no longer selects the exact source, the diagnostic must not force it: absence of the positive MISSION-POSE-PROBE log is a refused comparison.

FullNative shifts X/Z about12.37m while authoritative state.spot intentionally stays original. A native80c7 can therefore receive Move closer under existing reach rules. That is EXPECTED diagnostic behavior, not a test failure; no Take/payout requirement or interaction fix is claimed. Existing MoveObjectToPlayer is not used because it mutates source placement/orientation.

## Claude-only one-pass for each profile

Use the existing exclusive NativeQa workflow, one profile/variant at a time. CSR110 starts at real Chessman7 approach76015/485/15845, heading128, actuallevel10/XP1282500, ordinary2/17,2000HP/1000IS. No3072 progress, disk, completion or receipt is pregranted. Existing layout-onlyseed3072 reproduces the site; GM/generic callback/combat/drop forcing remain off.

1. Right-click The Chessman, Get Mission, choose Retrieval and accept normally. Follow the mission/elevator to the target floor. Verify the positive diagnostic log names char110/3072/phase0/Object1/source7611b015 and the requested variant. STOP this comparison if another source is selected.
2. Compare visible computer placement/floor relation, hover, left/right/doubleclick selection and radial actions. Preserve identical camera/standing conditions where possible; record337 selection clears versus actual80c7. No inferred picking success from a headless request.
3. For FullNative, grounded source approach91865/4295/12740 is inside sector347/region184/poly203 floor4200 and about0.8m from the candidate object. Independent geometricNAV routes exist from the observed room/elevator; this is not a native collision/walking guarantee. Walk normally if possible; record obstruction separately. Do not teleport through a wall or change bounds to force selection.
4. A visible/targetable FullNative object may still refuse use as too far from the OLD authoritative source. Record that distinction; no forcedTake/serverComplete/reward claim.
5. Record actual03 position/quat, positive probe log, client visuals and picking RPCs per variant. Abort/logout/reset/release through normal Claude workflow before starting the next profile. Results may be allfail. No default placement fix is authorized by this diagnostic PR.

## Headless proof and status

Each owned disposable case seeds an actual CSR110 from that same disposable database's ordinary28/2 templates, then uses normal Chessman offer/accept/elevator. It asserts actual03 tuple/quat, computerTake state/MissionKey, unchanged checkpointSourceSpot/quaternion and unchanged cash/XP/items. No Take/completion is needed. Unique DB/variant rawRPC03/DLL/config/checkpoint evidence is saved before teardown even on failure. Native rendering/picking remains pending.

Full4575 tests PASS66s;55focused tests/Python12 policy PASS. Both actual NativeQa builds/three read-only plans/provenance PASS. Root reviewed the exact derived-only hook and guards. Fresh source `8dd23350429f5a7bd659104ef2bb6b419a1a53ce`: Control24.4s, NativeY22.0s, FullNative21.5s PASS (67.9s total), three driverEXIT0/parallel1/peak1, normaldisposal. All actualCSR110: exact03position/quaternion/currentstate/MissionKey, original savedSpot92740/4445/13535/sourcequat and cash/XP/items unchanged. Actual selected Debug DLL SHA256 `da0f1f65a585349ffff4d6294be2d52808c6da5687e0e36c6b65a7d9e076ff0b`; Release `dc4b45b3c6daf48c956cd14fbbcba0371e3a77ea74773eb3ac612475b88abe3b`. Unique raw/RPC03/DLL/checkpoint evidence saved before teardown in everycase. InitialControl17.5s/EXIT1 fixture array-reference comparison failure retained separately; corrected to SequenceEqual plus earlier/best-effort final checkpoint capture, with runtime/modes byte-identical89d98ddc. No full repeat for that fixture-only correction. Claude native visual/picking remains PENDING; no Take/render/placement-fix claim. No generated ability snapshots are changed.
