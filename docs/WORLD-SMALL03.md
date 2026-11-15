# Small03: partial construct support

Map17 uses the shipped `resource/worlds/final_world/constructs/small/small03/small03.metr`. This package adds its static objects and navigation export to the existing world loader, and enables its shipped hardline1. It does not establish complete retail population or interaction parity.

## Resource inventory (2026-11-12)

All19 worlds in `research/world-data/metr_sectors.py` have loose METR/cooked COD/SSR resources. Packed navigation and existing exports are different inventories:

| Map | World | Sectors | Packed nav sectors | Prior exported nav | HDL rows |
|---:|---|---:|---:|:---:|---:|
|0|tutorial|36|36|yes|0|
|1|Slums, including Richland and Westview|1493|1490|yes|55|
|2|Downtown|792|792|yes|65|
|3|International|391|391|yes|23|
|4|archive01|393|392|yes|0|
|5|archive02|109|109|yes|0|
|6|Ashencourte|86|86|yes|0|
|7|Datamine|59|59|yes|0|
|8|Sakura|49|49|yes|0|
|9|Sati|31|31|yes|0|
|10|Widowsmoor|86|86|yes|0|
|11|Yuki|49|49|yes|0|
|12|large01|153|153|no|1|
|13|large02|226|225|no|1|
|14|medium01|144|144|no|1|
|15|medium02|100|100|no|1|
|16|medium03|60|60|no|1|
|17|small03|30|30|added here|1|
|901|Loading Area|1|1|no|0|

Only the three cities previously loaded static CSVs. Archives4..11 already have their separate archive transport/nav behavior; their lack of HDL rows does not imply missing transport. Constructs12..17 each have one cooked hardline; their METR NETS chunks contain **zero CNET subway networks**. Cooked transport group/space counts for12..17 are respectively2/8,0/0,0/0,0/0,5/59,1/6. No subway or population is synthesized here. The mapped `zion_caves.metr` for18 is absent in this client installation; no availability is claimed.

Existing `npc_spawns/Instance.csv` raw rows by map:0:224,4:28,6:6,7:18,8:22,10:17,11:179,12:78,14:12,15:54,16:58,17:119,18:65. These are captured server data, not proof of a complete shipped population. Of small03's119 labelled rows,68 lie inside a shipped sector X/Z footprint and51 do not. They remain unchanged; the wire fixture checks an existing in-bounds SSR Security Guard. No missing world is populated by moving those rows.

## Static identity and omissions

`static_objects_small03.py` reads the existing resource index without writing client files. OSID has ten length/absolute-offset buffers, checked through EOF. Its fourth buffer contains fixed74-byte GO records. SIDI's first buffer holds declared prefab placements and their inline fixed74-byte GO records; placement counts, increasing offsets and record bounds are checked. SIDG supplies the separate designer layer. A record's first two integers are GOID and final native staticID; no prefab ID offset is added.

All small03 interior placements have quarter-turn0. The exporter rejects nonzero placement rotations. It joins METR sector indices to their actual filenames: sector5 is `small03_23`, not `small03_5`. World X/Z is METR corner plus native local position and prefab pivot in tens. Exported Y retains the existing city CSV convention of native local Y plus pivot Y plus145. This +145 is a **server compatibility convention**, not a claim about native pivots.

The city crosscheck used Slums sectors0 and405:4,322 record references match existing CSV native IDs, GOIDs, positions and quaternions, including3 designer records.20 references with quarter-turn1/3 differ under the simplistic local quaternion transform; legacy CSV contains composed and proxy placements. That transform remains unresolved and is deliberately unavailable in this bounded exporter. Small03 has no such placements.

The raw audit retains370 references:130 exterior OSID,239 interior SIDI and1 designer SIDG. These identify361 keys. Six duplicate keys are exact; three have conflicting native representations. **All variants** of these three keys are omitted, leaving358 exported objects:

| Sector/staticID | Native alternatives | Reason unavailable |
|---|---|---|
|13/13|365 /580|Same location, different GOID; precedence unknown|
|13/15|419 /417|Shared elevator boundary, different door GOID; precedence unknown|
|13/16|348 /364|Same location, different GOID and opposite local quaternion; precedence unknown|

No first-reference policy is used to resolve these conflicts. `small03-static-audit.json` retains every raw reference, exclusions and SHA256 of all90 source SID/SIDI/SIDG files. Other district exporters remain separate work.

The unique hardline GOID592 is sector5/static13 (`0x0050000d`), source booth(17700,-598,34200), cooked name **358 Evertt Estates**. The HDL/cooked arrival is(17700,-503,34100), yaw -pi. The one transport group in sector13 contains six shipped6952 panels; all six are exported. Existing elevator runtime is reused.

## Reproduction and native checklist

From the repository root, with the existing read-only client-data tools directory:

```
python research/world-data/static_objects_small03.py <client-data-tools>
python research/world-data/navmesh.py <client-data-tools> data/sector_properties.csv data/navmesh 17
```

The nav export contains30 sectors and10 interior prefabs (82,907 uncompressed bytes). Compare decompressed bytes when reproducing the existing gzip exporter, whose header stores generation time.

`NativeQa --fixture-plan small03` is read-only. The discovered `small03` mode seeds only the disposable fixture at the shipped hardline arrival; no development commands or random controls are enabled. Claude alone runs native setup/QA.

1. Enter small03 at358 Evertt Estates. Check terrain, booth view, position and movement around the hardline; no unsupported subway should appear.
2. Open the actual booth radial menu, choose Exit to Loading Area, accept, then return using the available native Loading Area menu. Record whether the unnamed shipped hardline is offered by the client; the wire17/1 path is tested, client menu presentation remains native QA.
3. Walk to the existing building/elevator at sector13 around(21300,39200). Test the unambiguous door static7 on the1600-unit floor and the six native panels through the actual menus. Keys13/13,13/15,13/16 are explicitly unavailable.
4. Check existing captured NPC visibility, for example SSR Security Guard near(26829,5695,22900). Captured location validity beyond the footprint check remains partial; no population completeness claim.
5. Check outdoor and indoor nav/own-view movement, hardline exit again, and Actions/menu responsiveness. Report presentation or reachability gaps separately; do not replace native interaction with chat commands.
