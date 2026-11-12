# Medium03 (map16): heavily partial construct

This package adds a bounded subset of the shipped Medium03 resources. **1,282 safe native keys are exported; all variants of 3,343 keys are omitted.** It does not establish a complete playable district. Existing NPC captures, elevator destinations, rotated interior statics and native Loading Area return remain unresolved.

## Source and omission boundary

The client Medium03 METR has60 sectors. Their180 SID/SIDI/SIDG resources pass the existing ten-OSID-buffer, EOF, placement-count and record-bound checks. The decoder retains4,762 references/4,625 native keys in [the static audit](../research/world-data/medium03-static-audit.json). It excludes3,321 keys touched by nonzero prefab placement rotations and134 conflicting keys (112 overlap), a union of3,343. No first-variant rule or guessed quaternion composition is used. All1,282 remaining keys have rotation0 and an unambiguous identity/position/quaternion.

Positions retain the established city-CSV convention: METR sector corner + local position, with +145Y. This is a server convention, not proof of a native object pivot. Raw transformed positions on omitted rotated records are diagnostic only. The strict default still refuses nonzero rotations. Source hashes cover all180 static inputs.

[The resource crosscheck](../research/world-data/medium03-resources.json) records the60 NAV inputs, METR, HDL and cooked COD hashes and hardline schema. All60 navigation sectors are packed using the existing exporter; navigation quarter-turn handling does not establish static-object transform semantics. METR NETS has zero CNET networks; no subway is added.

Reproduce from the repository root (read-only client access):

```text
python research/world-data/static_objects_small03.py <client-data-tools> --world 16 --omit-unproven-rotations
python research/world-data/navmesh.py <client-data-tools> data/sector_properties.csv data/navmesh 16
python research/world-data/medium03_resources.py <client-data-tools> <MxO-directory>
```

Existing12/17 exports must reproduce; without the explicit omission flag,16 must refuse before writing output.

## Exact hardline and grounded door

HDL schema HID2 (NextID3) places the hardline at(37800,400,31700) with offset(-100,95,~0), yielding arrival(37700,495,31700), yaw−1.5708. Cooked sector52 independently identifies GUID1/RTID592, name **467 Reitman Road**, sector-local position(7800,400,1700), forward−X and offset100. This is native static sector52/ID1 (`01004003` little-endian), not HID1. The CSV booth position is(37800,545,31700); its centre has no nav polygon because it occupies the booth footprint. The exact shipped arrival is walkable NAV sector52/region7/polygon4/floor400, also at adjacent Z31600/31800. It is100 units in front of the booth.

Safe native darkwood door GOID365, sector33/static1079 (`37041002`), is at(27100,1745,41200). Its approach(27100,1695,41100) is on interior region99/floor1600. The second safe365 is static1087. No guessed417 door is added: e.g. sector33/static20 is omitted because its placement rotation is unproven.

There are18 safe6952 elevator-panel statics. Cooked data contains5 TransportGroups/59 TransportSpaces, but this package has **no proven panel-key/destination association** and adds no elevator mapping. Static presence must not be reported as working elevator travel.

All58 existing map16 Instance.csv NPC rows lie outside the shipped sector X/Z footprints. They remain byte-for-byte unchanged. No NPC is relocated, fabricated or claimed visible/usable. Missing population is an explicit limitation.

## Native Loading Area return is unavailable

The existing native80fa handler rejects map IDs above10. This package does not change that handler, construct list metadata or client UI. The existing wire6c/6d route can enumerate16/2 and direct80ff16/2 can return to it; neither establishes a native selectable Loading Area entry. Native return is **unresolved**, not a UI PASS or a chat-command substitute.

## Claude-only one-pass checklist

Use discovered `medium03` profile (no dev, GM or forced randomness) at the shipped16/2 arrival. Confirm visible ground/booth and walk along the entrance pavement; check collision/nav alignment without crossing omitted interiors and report missing interactions honestly. Inspect Actions and the actual hardline radial. Walk to the darkwood door near(27100,1695,41100), test its normal radial/open presentation, and record actual geometry/door behavior. Panels may render but working travel is not claimed; NPC visibility is not a pass criterion.

Use the actual booth radial Exit to Loading Area and confirm. Inspect the native list and record that map16 return remains unavailable if absent. Do not inject80ff or use chat as proof of UI return. Claude alone owns native setup, the client, UI validation and merges.

## Automated validation

Pending final unit/build/fresh results. The real-wire fixture covers shipped592/HDL16/2, actual booth-radial Loading Area exit,6d16/2, explicit direct-wire return with new world/self creation, exact durable arrival, safe365 door view and omitted417 refusal. It does not prove native geometry/UI, elevator destinations, NPC population or complete district parity.
