# Medium02: bounded partial construct support

Map15 uses resource/worlds/final_world/constructs/medium/medium02/medium02.metr. Current support exports997 proven native static keys, all100 shipped navigation sectors and the shipped hardline15/1 through the existing exporter/loader. It is **PARTIAL**, with26 conflicting native keys (all variants) omitted and no captured NPC gameplay/visibility validation. The original historical omission path exports426; the explicit reviewed native-transform extension restores571. See [rotation evidence and native checklist](WORLD-MEDIUM02-PREFAB-ROTATIONS.md).

## Resources and omissions

All100 METR sectors have packed nav resources and SID/SIDI/SIDG layers. There are25 prefab placements:12 quarter-turn0,12 turn1 and1 turn2. The explicit `--native-prefab-rotations` path now uses the source-proven direct-sector IPI transform; nested/EPI parents remain unsupported. The existing exporter adds --world15; its strict default still refuses nonzero rotations before writing output. Explicit --omit-unproven-rotations removes every variant of a key touched by a rotated placement or ambiguity, never resolving conflicts by first reference.

| Native key accounting | Count |
|---|---:|
|Raw references|1061|
|Unique native keys|1023|
|Nonzero-placement keys|584|
|Ambiguous keys|26|
|Overlap|13|
|Union omitted|597|
|Exported unrotated/unambiguous keys|426|

Raw references comprise38 exterior and1023 interior entries. The audit retains every alternative and300 source hashes. Nonzero transformed positions are explicitly unproven diagnostics, never exported. Native IDs, unrotated positions/quaternions and the inherited CSV Y+145 compatibility convention remain unchanged; this does not establish a new retail pivot or complete interior interaction parity.

The existing nav exporter produces100 sectors/21 prefabs,256,512 decompressed bytes. Repeated decompressed SHA256 is c8c2988430b91380f6b0bed5e5a25f7f7054aa3016c1e5dbe6aacfbbb1d6116b. Gzip timestamps differ, so compare decompressed bytes. Original Large01 and Small03 CSV/audit exports reproduce byte-for-byte.

Reproduce from the repository root with existing read-only client-data tools:

    python research/world-data/static_objects_small03.py <client-data-tools> --world 15 --native-prefab-rotations
    python research/world-data/navmesh.py <client-data-tools> data/sector_properties.csv data/navmesh 15

Omitting both explicit transform/omission flags must fail before output. Historical `--omit-unproven-rotations` still reproduces426 keys and597 omissions. No parser fork, fabricated polygons or invented subway is added. The native METR contains zero CNET networks (METR SHA15092777adbc18284dea2f2806db9f213166a25f6eafc3449684f382c9e0f631).

## Grounded fixture and supported door

The sole cooked hardline is unnamed HID1, sector56/static7, GOID592. Exported booth position is(-1000,145,2400); HDL arrival is(-1000,95,2500), yaw0. Both the source nav and existing C# lookup ground that arrival in sector56.

The nearest selected safe ground-floor door is native GOID414, sector76/static210, at(-5700,145,15000). Standing point(-5700,95,14900) has an interior polygon at floor0. This is a supported normal path, not a fallback:

- GameObjectDefinitions.Object414 is AttributeClass364; the existing ObjectInteractionHandler explicitly supports that schema.
- Native object_usables.csv414 defines OpenUsage/use3, DoorOpenGeneric4004.
- Native object_states.csv414 defaults to Closed835 offering3; normal success moves to Open834, then Closing838 after20seconds and Closed835 after another second.
- The fixture sends ordinary static80c8 Open3, observes creation CurrentState834, then waits for the natural Closed835 update. No door state, timer or result override is added.

## Captured population remains unvalidated

Existing Instance.csv has54 map15 rows:23 inside the METR footprint and31 outside. **All23 in-footprint captured positions fail the existing C# NavMesh.Locate lookup.** Sati(16971,495,3868), for example, lies inside sector57's footprint but has no nav polygon at that X/Z. The reason may include capture context or geometry/version differences; it is not established here.

Rows remain untouched. No NPC is moved to a convenient floor, synthesized or claimed as a gameplay/visibility PASS. The diagnostic profile and fresh fixture cover hardline/nav/door only; they do not establish retail population completeness.

## Loading Area source gate

Direct headless80ff15/1 exercises the server transport path, not the native LA tree. The shared source investigation found that6d changes the runtime record's status byte1 rather than its eligibility byte0. The80fa handler621c7a30 rejects nonzero construct IDs above10 (cmp at621c7b5c, rejection at621c7b5f) in client SHA cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a. Injecting80fa15 is therefore not an established fix. Map/construct metadata and native tree availability remain a separate source gate; no guessed reply or dropdown is introduced.

## Claude-only one-pass

Use discovered medium02 profile: no dev, GM or forced RNG. Enter at15/1, inspect terrain/booth and ordinary movement/nav. Walk toward X-5700/Z15000 (about172metres by increasing Z then decreasing X), inspect the actual building entrance, and Open the graymetal door at76/210 through its normal radial/default use. Observe normal20-second open/closing/closed behavior; report geometry or omitted interior paths separately. The grounded test point is just south of it at Z14900. No chat teleport or injected state is claimed as UI proof.

Return to the booth, use the actual Exit to Loading Area radial and accept. Inspect the native hardline tree and attempt ordinary UI return only if offered. If absent, record that source gate; a direct injected80ff is not native menu proof. No NPC pass is requested or claimed. Geometry and UI acceptance remain Claude-only and pending.

## Recorded automated validation

Full4,234 tests passed on the final master155/158 combined base, including25 focused world/hardline tests and the exact414 schema/use/state contract. Twelve Python audit policy/link tests passed. Debug/Release builds and actual read-only named/unknown mode plans plus provenance passed. Exports reproduce as above, and strict default refuses unproven rotations.

The fresh normal-protocol fixture passed38.2seconds, exit0, peak one disposable server: shipped592 booth radial toLA,6d15/1, direct80ff return with new06/new self at exact HDL coordinates, normal414 Open/state834 and natural timedClosed835. Successful raw diagnostics were saved before teardown. Native LA menu selection, geometry and NPC validation remain explicitly unclaimed.

Master155/158 was merged after the fresh run; only upstream economy/team-mission code changed. This tested world loader/hardline/door path is unchanged. The full suite, both builds and read-only provenance were refreshed on that final base.
