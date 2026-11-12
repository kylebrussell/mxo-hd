# Large01: bounded partial construct support

Map12 uses the shipped resource/worlds/final_world/constructs/large/large01/large01.metr. This adds only validated static keys and existing navigation through the small03 foundation's loader/exporter. It does not claim complete retail population, rotated interior interaction or Loading Area UI return.

## Resource and transform gate

All153 METR sectors have packed nav resources; the existing nav exporter yields153 sectors/10 prefab meshes,3,056,554 uncompressed bytes. Repeated decompressed export SHA256 is996b9825c515a07db94eb854282a6b7a5993742b917d3a028f4b8726685f673d. Gzip headers contain generation time, so compare decompressed bytes.

There are33 SIDI prefab placements:17 quarter-turn0,5 turn1,8 turn2,3 turn3. Nonzero placements occur in sectors129/136/141. Their complete static transform remains unresolved: composing a Y-axis quaternion repaired8 city crosscheck entries but did not resolve conflicting proxy positions/GOIDs. Mathematical rotation alone is insufficient evidence. No rotated placement is used in this static export.

The existing exporter is generalized with --world12; strict default still rejects unproven rotations before writing output. The explicit --omit-unproven-rotations policy excludes **every variant** of any native key touched by a nonzero rotation, plus every conflicting key. It never takes a first variant to settle ambiguity. The audit retains1025 raw references,1001 keys and459 source-file SHA256 values:

| Native key accounting | Count |
|---|---:|
|Nonzero-placement keys|46|
|Ambiguous keys|22|
|Overlap|20|
|Union omitted|48|
|Exported unrotated/unambiguous keys|953|

Raw references contain42 exterior and983 interior entries. The raw audit's nonzero transformed positions are explicitly **unproven diagnostics**, never exported placements. The exported identity/position/quaternion/Y+145 convention is unchanged from validated rotation0 small03/city records. The +145 is server CSV compatibility, not a new native pivot claim. Original small03 CSV and audit reproduce byte-for-byte.

Reproduce from the repository root using existing read-only client-data tools:

    python research/world-data/static_objects_small03.py <client-data-tools> --world 12 --omit-unproven-rotations
    python research/world-data/navmesh.py <client-data-tools> data/sector_properties.csv data/navmesh 12

Omitting the omission flag must fail. No default unsafe export or second loader/parser is introduced.

## Hardline, doors and captured population

The sole cooked hardline is unnamed HID1, sector50/static7, GOID592. Exported booth position is(13000,-1252,-7500); HDL arrival is(13000,-1302,-7400), yaw0. Only map12 is added to the existing validated hardline coverage/list path; no other construct is enabled by this change. METR NETS has zero CNET networks, so no subway is invented.

The unrotated native darkwood door GOID365 sector136/static1 is at(12900,145,15600); the nearby standing/nav point(12900,95,15500) locates in its interior. Other omitted interior keys remain unavailable.

Existing Instance.csv has78 map12 NPC rows:24 are inside the shipped sector footprint and54 outside. These captured rows are unchanged. The sample Chopper Runner(7713,-705,53769) lies600 units below the shipped nav standing height(-105) at the same X/Z. The fixture stands on the real floor and checks existing NPC wire visibility; it does **not** establish correct native NPC elevation or relocate captured spawns. Population completeness and elevation remain partial.

## Loading Area UI gate remains unresolved

Claude verified small03's native load/walk/booth-to-LA path, but the LA list omitted small03 and Jack-in fell back to Downtown. A headless direct80ff17/1 did not establish native UI selection. The same distinction applies to large01:6c12/6d12 can enumerate HID1, and direct80ff12/1 can exercise transport, but that is not proof the UI offers it.

Bounded primary reading: client laviewhardlineselection.cpp tree builder620374a0 enumerates a runtime construct vector at629ea21c..629ea220, calls621c3c40, then skips a construct if byte[record]==0 at6203756c/6203756f. This is a runtime availability gate, not proof of a hardcoded city-only filter. The server's general GoToLoadingArea saves a nearest-hardline fallback and sends8107;80fa MakeActive currently appears only in the sourced tutorial exit path. These facts do not establish that adding80fa fixes the tree. Reproduce this bounded gate with research/world-data/inspect_la_construct_gate.py --client <client.dll> [--python-deps <existing dependencies>]. Next bounded query: recover the6d availability update and80fa active-selection handlers, and their construct metadata/name dependencies. No guessed reply/dropdown is added.

## Claude-only one-pass

Use discovered large01 profile: no dev/GM/forced rolls. It enables the existing captured NPC scope without modifying spawns. Enter at shipped12/1 and check terrain, booth, ground/nav movement and world geometry. Walk to sector136 near(12900,15500), test the actual darkwood door radial and report missing interior interactions separately. Inspect existing captured NPC presentation where reachable; record elevation problems rather than moving them.

Use the actual booth radial Exit to Loading Area and accept. Inspect whether large01 is offered by the native hardline tree, and attempt normal UI return only if offered. If absent, record the unresolved gate; do not label a direct injected80ff as a UI PASS. Native geometry and overall UI acceptance remain Claude-only.
