# Large02: bounded partial construct support

Map13 uses the shipped resource/worlds/final_world/constructs/large/large02/large02.metr. This extends the existing Small03/Large01 loader and parameterized exporter, not a new parser. It now adds2,052 validated native static keys,225 available navigation sectors and the shipped hardline13/1. It remains **PARTIAL**.

## Source and omitted keys

The world has226 METR sectors and34 prefab placements:20 quarter-turn0,13 turn1 and1 turn2. Reviewed native transforms are now available through the explicit native-prefab option; see WORLD-LARGE02-PREFAB-ROTATIONS.md. The following counts describe the historical omission mode. The exporter strict default refuses them before writing output. Explicit --omit-unproven-rotations removes every variant of any native key touched by a nonzero placement, plus every ambiguous key; it never settles a conflict by taking the first variant.

| Native key accounting | Count |
|---|---:|
|Raw references|2164|
|Unique keys|2096|
|Nonzero-placement keys|442|
|Ambiguous keys|44|
|Overlap|9|
|Union omitted|477|
|Exported unrotated/unambiguous keys|1619|

The audit retains all raw references and678 SID/SIDI/SIDG source hashes. Raw sources are41 exterior and2123 interior references. Nonzero transformed audit positions remain unproven diagnostics, never exported. The established native IDs, unrotated positions/quaternions and server CSV Y+145 convention are inherited unchanged. Original Large01 CSV/audit reproduce byte-for-byte. The new reviewed rollout restores433 keys while preserving all44 conflicting omissions; the historical diagnostics above are superseded only by that opt-in path.

## Missing navigation is not a NoNav claim

Sector47 is filename large02_137, designer label Facade, bounds X29000..40000/Z0..10000. Its METR **Enable Navigation Mesh is1**, yet the packed index and loose resource directory contain no corresponding .nav asset. SID/SIDI/SIDG exist and yield no static references there. This is an explicitly unavailable resource, not a proven NoNav flag or an invitation to fabricate polygons. NavMesh.Locate remains false in that sector.

The existing nav exporter packs only225 available sectors and31 prefab meshes:420,200 decompressed bytes. Repeated export matches decompressed SHA256816c4f6feb78886d33fbca73b1df6c767d514fe023b3489f865de846aecdb6e8. Gzip headers carry generation time, so compare decompressed content.

Reproduce from the repository root with the existing read-only client-data tools:

    python research/world-data/static_objects_small03.py <client-data-tools> --world 13 --omit-unproven-rotations
    python research/world-data/navmesh.py <client-data-tools> data/sector_properties.csv data/navmesh 13

Omitting the explicit omission option must fail. No missing sector polygon is synthesized.

## Hardline, door and population scope

The sole cooked hardline is unnamed HID1: sector207/static3, GOID592. Exported booth position is(-7600,145,-2000), with HDL arrival(-7500,95,-2000) and source yaw pi/2. Only map13 is added to the validated hardline path; map14 remains unchanged. There are zero CNET subway networks and no cooked transport groups, so no subway or transport population is invented.

The nearby verified unrotated door is sector27/static11, GOID359 (greenmetal), at(12200,145,-4100). Standing point(12300,95,-4100) has interior navigation. Its native ID and existing door interaction are reused.

There are no map13 ambient/Instance captured NPC rows. The sole other captured map13 row is Encounter.csv Special Agent, level52, at(16875,495,4024). Existing NpcSpawnScope classifies it as a target/encounter arrival and excludes it from ambient loading. This point lies inside the sector27 footprint. That distinction is preserved; it is not made a permanent street spawn or relocated. Population completeness is not established, and the wire fixture makes no ambient NPC claim.

## Claude-only native one-pass

Use discovered large02 profile, with no dev/GM/forced rolls. Enter at shipped13/1; check terrain, booth and ordinary movement/nav. Walk toward increasing X from -7500 to12200, then decreasing Z from -2000 to-4100 (about218metres in world units) to test the actual greenmetal door radial at27/11. Check the visible building entrance before entering; report inaccessible or omitted interior interaction separately. No chat teleport is required or claimed as a UI interaction. Stay west of unavailable sector47 at X29000..40000/Z0..10000.

Use the booth's actual Exit to Loading Area radial and accept. Inspect the native hardline tree and attempt normal UI return only if Large02 is offered. Shared native UI return remains **unresolved**: Small03 loaded/walked/exited to LA, but its LA list omitted the construct and defaulted to Downtown. A headless direct80ff13/1 verifies the existing wire path, not native menu selection. FriendlyNameID0 and the client runtime availability gate documented in WORLD-LARGE01.md do not establish a safe reply/fix; no guessed80fa or dropdown is added here.

Native geometry, door reachability and UI acceptance remain Claude-only and pending.


## Recorded automated validation

Full4,177 unit tests and24 focused world/hardline tests passed;12 Python audit-policy/link tests passed. Debug/Release builds and actual read-only large02/unknown plans plus provenance passed. Large02 CSV/audit and inherited Large01 CSV/audit reproduce byte-for-byte, repeated nav matches after decompression, and default large02 export without the explicit omission option fails before writing output.

The normal wire fixture passed16.5seconds, exit0, peak one disposable server: shipped592 booth radial to LA,6d13/1, direct80ff return, a new06 and new self-spawn at exact HDL coordinates, and native359 door interaction at27/11. Server nav logs show207/6/15 at arrival and return,27/101/572 at the door. The fixture introduces no ambient NPC and makes no native LA menu-selection claim. Successful raw diagnostics were saved before teardown. Native geometry/UI acceptance remains pending.
