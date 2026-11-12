# Medium01: bounded partial construct support

Map 14 uses the shipped resource/worlds/final_world/constructs/medium/medium01/medium01.metr. This package adds the shared exporter's validated 39 static keys, 144 native nav sectors and existing shipped hardline14/1. It preserves all captured NPC rows and the source's incomplete population/interaction limits. It is not complete retail population or Loading Area UI parity.

## Resource and object gate

The METR SHA256 is55d9843329ce67f67715ce4ba0faff33366a4412e58816f7c4c6dd09b2c0e38b. All 144 METR sectors have matching packed.nav resources; the existing MXNV3 nav export has 144 sectors, 0 prefabs and 785,872 uncompressed bytes (decompressed SHA256 0a11ff6978d1186e9639cb9527835ec398e7d07c364f90f755980bff868c2d3d). There are **no SIDI prefab placements**, so no unproven placement rotations. All 39 raw static references are exterior, all 39 native keys are unique and no key is ambiguous or excluded. Object-local quaternions remain their actual stored values; absence of prefab rotations does not mean every object's orientation is zero.

All 39 GOIDs were classified, rather than checking a short numeric door-ID range:

| GOID | Count | Primary classification |
| --- | ---: | --- |
|38339|38|PropagandaFloorSpawner, GOB schema 212, zero components and zero stored bytes. No Usable or door component; absent from object_usables and runtime GameObjectDefinitions. These are markers, not implemented propaganda-spawning gameplay.|
|592|1|Hardline_PhoneBooth, schema 3632: Info/Hardline/Model and six native hardline Usables (Exit3, Upload4, Loading Area5, Enter Construct6, Hack7, Synchronize8).|

Thus **no door is exported in this map dataset**; no absent door, subway or marker-generated object is invented. This does not prove that an external script could never spawn another entity. The booth is native sector140/static1 (key0x08c00001), exported CSV position(39350,-903,27000), stored local quaternion yawapproximately-90degrees. The shared +145Y CSV convention is retained; no new pivot/rotation rule is introduced. The raw GOB check used SHA3e47f11012f6f219a26c9f5decfa10cbc00726c397937d03c3068254e5424103.

The existing parameterized exporter is extended to --world 14; no second parser/loader is added. Strict default remains strict and successfully exports14 because there are no unsupported placements. Small03 and large01 behavior is unchanged, including large01's required explicit omission option.

    python research/world-data/static_objects_small03.py <client-data-tools> --world 14
    python research/world-data/navmesh.py <client-data-tools> data/sector_properties.csv data/navmesh 14

Repeated static CSV/source audit must match byte-for-byte; nav comparison uses decompressed bytes because gzip headers include generation time. The audit records 432 source-file SHA256 values and all 39 raw references.

## Hardline and navigation boundaries

The sole shipped hardline is unnamed HID 1. Existing HDL arrival is(39250,-953,27000), yaw-1.5708. Native nav decoding locates sector140/region0/poly7 there, floorY-1050 and standingY-955: the recorded arrival is2units above that standing value and remains within existing navigation tolerance. The existing HDL arrival is preserved exactly.

The old hardline CSV's booth metadata for14 has no cooked GOID/address and uses HDL-base fallback coordinates(39350,-1048,27000). The new source-validated static booth CSV position is distinct. This package does not silently enrich or rewrite that fallback row. Nearest/list coverage is enabled only for14, keeping the other construct gates unchanged.

The METR NETS container has zero payload/children; the existing subway parser returns no CNET networks. A literal CNET byte sequence elsewhere in the whole file is not a network child. No subway is added.

## Captured NPC limitations

Instance.csv contains 12 map 14 rows, all inside METR sector X/Z bounds. **Zero has a containing shipped NAV polygon at its exact recorded X/Z.** There is no valid floor at those points from which to fabricate a vertical correction/gap. These rows remain unchanged; they are not relocated, deduplicated or treated as proof of walkable placement.

For example, Demon Army Andras(41538,495,39933) has no containing polygon. One nearest validated polygon-centroid sample is(31991.67,-705,33973.33), over 11,253 units away horizontally. Hariel(53907,495,41869) has a sampled nearest centroid(39455,-955,26935), over 20,781 units away. Centroid distances are diagnostic samples, not proofs of globally closest polygon-edge distance. They do not justify a nearby grounded actor/NPC visibility fixture. The package tests source row preservation and footprint and lack of exact-position navigation; it makes **no fresh or native NPC visibility/elevation PASS claim**. A possible world-ID/instance-layout correspondence is a future source gate, not permission to move captures.

## Loading Area UI remains unresolved

Small03's actual native booth-to-LA path worked, but its construct was absent from the native tree and Jack-in fell back to Downtown. Large01 documents the same runtime availability gate. Nothing here recovers medium01 tree availability, metadata/name dependencies or MakeActive behavior. A direct headless80ff14/1 is diagnostic transport evidence only, not proof the real UI offers it. No guessed dropdown/reply/packet is added. Root's subsequent bounded client read found6d consumer621c4240 changes record byte1 while LA eligibility reads byte0;80fa consumer621c7a30 checks IDs greater than10 at621c7b53–621c7b5f. Their complete identity/availability mapping remains a separate gate; this package adds no80fa remedy.

## Claude-only one-pass native check

Use discovered `medium01` at the exact reviewed head. It starts copied CSR110 at shipped14/1,39250/-953/27000, with abilities2/17, no dev/GM/forced rolls. The existing captured NPC scope is enabled without changing spawns.

1. Verify actual world load/terrain and grounded movement around the hardline; record any geometry or2-unit-height disagreement. Confirm the visible592booth is the sector140/static1 object, without inventing marker gameplay.
2. Right-click the actual booth, choose Exit to Loading Area and accept. Inspect whether medium01 is offered by the native tree. Attempt normal UI return only if offered. If absent or wrong-world fallback occurs, record the unresolved gate; do not substitute an injected80ff as UI proof.
3. Record missing doors/subway/population separately. Do not teleport onto unsupported captured NPC coordinates or relocate an NPC to make a visibility check pass. Finish through Claude's normal exclusive QA reset/release workflow.

## Automated validation

Full Release suite: 4,203 passed; final postmerge: 25 focused world/hardline/fixture tests passed. Both NativeQa configurations built and the read-only discovered mode confirms no dev/GM/forced-roll authority. The shared exporter reproduces14/17/12 CSV and source audits byte-for-byte; large01 without its explicit omission option still refuses before output. Repeated14navigation matches after decompression. An initial unit assertion incorrectly expected MXNV2; the existing exporter uses MXNV3 for every non-city construct. Only that test expectation changed; no format/parser workaround was added.

One normal-protocol fresh case passed in 20.5 seconds, exit 0, parallel 1. It observed592sector140/static1, real booth-to-LA,6d14/1, direct80ff14/1, new06/new self-spawn and exact HDL database position39250/-953/27000, without parser problems. Runtime navigation logs name sector140/region0/polygon7 at entry and return. The owned full success log was saved before teardown and preserved at Temp/mxo-medium01-proof-20261112/fresh-success-debug.log; permanent stdout/exit is fresh-summary.txt there. No captured NPC visibility or native UI selection is claimed from this case.

The dependency is PR157 stabilized051c13b4353471287dd53ced546756db6fb0cc2b; base stays master and157must land first. Current master0248942d is integrated. Own registry entry is beside legacy mission-arrival-navmesh, distinct from157/159 anchors; no shared README appendix is added. Native geometry/booth/menu acceptance remains pending Claude.
