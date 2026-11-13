# Medium03 (map16): heavily partial construct

This package adds a bounded subset of the shipped Medium03 resources. **4,491 primary-proven native keys are exported; all variants of 134 ambiguous keys remain omitted.** It does not establish a complete playable district. Existing NPC captures, elevator destinations, ambiguous statics and native Loading Area return remain unresolved.

## Source and omission boundary

The client Medium03 METR has 60 sectors. Their 180 SID/SIDI/SIDG resources pass the existing ten-OSID-buffer, EOF, placement-count and record-bound checks. The decoder retains4,762 references/4,625 native keys in [the static audit](../research/world-data/medium03-static-audit.json). It retains all variants of the 134 conflicting keys as omissions. The explicit world16 native-transform opt-in restores3,209 previously omitted keys, using primary native IPI transforms rather than nav/appearance guesses. No first/last variant policy is used.

The native IPI constructor/reset clears the EPI-parent pointer; the direct sector-SIDI parser supplies its SID parent. All173 placements here belong to that validated direct SCIN sector path. The native matrix uses signed pivot times10, exact quarter turns and parent-times-local quaternion composition with float32 stores. The server's +145Y convention is kept separate. Unknown actual nested/EPI contexts and invalid flags are refused. Resource aliases may be blank despite indexed/readable packed resources; their IDs, pack offsets and hashes remain recorded rather than guessed names. The original4,762 raw diagnostic tuples and all134 conflict groups are unchanged. Source hashes now cover180 static inputs, the METR parent proof and23 prefab resources.

See [the transform and diagnostic evidence](WORLD-MEDIUM03-PREFAB-ROTATIONS.md). The historical omission mode still reproduces1,282 rows and preserves its original unproven diagnostic meaning; the default remains strict.

[The resource crosscheck](../research/world-data/medium03-resources.json) records the 60 NAV inputs, METR, HDL and cooked COD hashes and hardline schema. All 60 navigation sectors are packed using the existing exporter; the static transform is established independently by native IPI code, not inferred from navigation. METR NETS has zero CNET networks; no subway is added.

Reproduce from the repository root (read-only client access):

```text
python research/world-data/static_objects_small03.py <client-data-tools> --world 16 --native-prefab-rotations
python research/world-data/navmesh.py <client-data-tools> data/sector_properties.csv data/navmesh 16
python research/world-data/medium03_resources.py <client-data-tools> <MxO-directory>
```

Existing12/17 exports remain unchanged. Without an explicit flag,16 still refuses before writing output; --omit-unproven-rotations retains the historical1,282-row export. --native-prefab-rotations is reviewed for16 only and rejects every other world.

## Exact hardline and grounded door

HDL schema HID 2 (NextID 3) places the hardline at(37800,400,31700) with offset(-100,95,~0), yielding arrival(37700,495,31700), yaw−1.5708. Cooked sector 52 independently identifies GUID 1/RTID 592, name **467 Reitman Road**, sector-local position(7800,400,1700), forward−X and offset100. This is native static sector 52/ID 1 (`01004003` little-endian), not HID 1. The CSV booth position is(37800,545,31700); its centre has no nav polygon because it occupies the booth footprint. The exact shipped arrival is walkable NAV sector 52/region7/polygon4/floor400, also at adjacent Z31600/31800. It is100 units in front of the booth.

Safe native darkwood door GOID 365, sector 33/static 1079 (`37041002`), is at(27100,1745,41200). Its approach(27100,1695,41100) is on interior region99/floor 1600. The second safe365 is static1087. The newly restored source580 door33/1380 has a separate [direct diagnostic fixture](WORLD-MEDIUM03-PREFAB-ROTATIONS.md); native rendering remains pending. No guessed417 door is added: e.g. sector 33/static20 is still omitted because its native key has conflicting variants.

There are59 primary-proven 6952 elevator-panel statics. Cooked data contains 5 TransportGroups / 59 TransportSpaces, but this package has **no proven panel-key/destination association** and adds no elevator mapping. Static presence must not be reported as working elevator travel.

All 58 existing map16 Instance.csv NPC rows lie outside the shipped sector X/Z footprints. They remain byte-for-byte unchanged. No NPC is relocated, fabricated or claimed visible/usable. Missing population is an explicit limitation.

## Native Loading Area return is unavailable

The existing native80fa handler rejects map IDs above 10. This package does not change that handler, construct list metadata or client UI. The existing wire6c/6d route can enumerate 16/2 and direct80ff16/2 can return to it; neither establishes a native selectable Loading Area entry. Native return is **unresolved**, not a UI PASS or a chat-command substitute.

## Claude-only one-pass checklist

Use discovered `medium03` profile (no dev, GM or forced randomness) at the shipped16/2 arrival. Confirm visible ground/booth and walk along the entrance pavement; check collision/nav alignment without crossing omitted interiors and report missing interactions honestly. Inspect Actions and the actual hardline radial. Walk to the darkwood door near(27100,1695,41100), test its normal radial/open presentation, and record actual geometry/door behavior. Panels may render but working travel is not claimed; NPC visibility is not a pass criterion.

Use the actual booth radial Exit to Loading Area and confirm. Inspect the native list and record that map16 return remains unavailable if absent. Do not inject80ff or use chat as proof of UI return. Claude alone owns native setup, the client, UI validation and merges.

## Automated validation

Release full suite: 4,233 tests PASS. Focused world tests: 9 PASS; final Medium03 checks: 3 PASS. Python audit/link/policy: 12 PASS. Both NativeQa configurations built and read-only medium03 plans passed with dev/GM/forced rolls disabled. Current ignored audit artifacts reproduce exactly with CRLF; tracked parity snapshots/current CSV and captured NPC data are unchanged. The 12/17 static exports reproduce byte-for-byte (17 uses its original strict/default form),16 CSV/audit reproduce exactly, repeated NAV 16 matches after decompression, and strict 16 without the omission flag refuses before writing output.

The real-wire fixture passed 20.3 seconds/exit0/one disposable server, with successful raw server/debug output saved before normal disposal. It ran against frozen 687aa860525887c41b9135903863ac9bde70aff5; merging master 1068a0d9 afterward changed no files. The later master 0c719c44 Large02 import unions 13/16 in the shared loader/network/exporter; 30 focused world/hardline tests PASS and all 12/13/16/17 CSV/audit outputs reproduce exactly. Own 16 data/fixture source remain unchanged; no additional fresh run is needed for those upstream-only imports. The fixture covers shipped 592/HDL16/2, actual booth-radial Loading Area exit,6d16/2, explicit direct-wire return with new world/self creation, exact durable arrival, safe 365 door view and omitted 417 refusal. It does not prove native geometry/UI, elevator destinations, NPC population or complete district parity.


### Native fixture heading repair

Native login exposed a fixture error: the SQL stored raw HDL yaw−1.5708 in the character rotation column, which the login reader converts to one byte. The source uses256 yaw steps per turn, so the shipped−X heading is192 (`round(yaw * 128 / pi) & 255`). The medium03 fixture now seeds byte192; raw HDL/audit radians remain unchanged. A regression derives192 from the actual HDL row and validates discovered fixture rotation literals as integral0–255 values. The repaired headless case stores the same byte before initial UDP login and asserts the actual decoded one-byte `Yaw Interval` in its self03 creation, instead of its earlier default0 seed. Shared legacy-data login hardening belongs to PR161 and is not duplicated here.

The heading fixture passed20.6 seconds/exit0 with exact byte192, source pin5343b68ec63abf0cb820f57cf26b4afa384c6d37 and Release hds07FE59E23C55291BFDEEE0ADF4D0E5E9D4E95339F5723A051BA34234FD533EE2; normal disposal completed and raw success was retained. A preceding19.8-second attempt logged in successfully but its test used the C# field name `YawInterval` rather than the decoder display name `Yaw Interval`; that test-only failure is retained. The exact shared CharacterSeed.Rotation/SQL seam comes from PR161 commit2b7d5b4fde683cd45035c99bcfbbce9086dc27be; no DB hardening was imported. Master5cc17819/Medium02 was merged afterward, preserving its loader/exporter/network and updating only its now-obsolete uncovered16 assertion to still-unvalidated14.

After the5cc master union,49 focused world/archive/hardline/QA-loadout tests PASS; both NativeQa builds and read-only192 plans/provenance PASS. All4/12/13/15/16/17 CSV/audit outputs reproduce exactly, master DataLoader bytes are preserved plus one16 loader line, and current ignored three-artifact CRLF roundtrip/policy PASS. No fresh repeat was needed for those upstream-only imports.
