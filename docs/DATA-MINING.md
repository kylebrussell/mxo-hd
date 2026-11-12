# Data nodes, luggables and mission map pings

This is **partial reconstruction**, using the 7.6005 `client.dll`, `gameobjects.gob` and the shipped Python. There is no native-client pass yet. `DataMining`, `DataMiningPackets` and `WorldLuggablesDb` implement the runtime. `research/world-data/utility_objects.py` reproduces `data/utility_objects.json` from the read-only client. GOB SHA-256: `3e47f11012f6f219a26c9f5decfa10cbc00726c397937d03c3068254e5424103`.

## Native records

All integers below are little-endian. Offsets include the opcode. RPC framing already provides the record length.

| Record | Layout and client evidence |
|---|---|
| `80 ca` | Six bytes: RezID u32 at +2. Decoder `0x6213c340`, printer `0x62152290`; dispatcher `0x62179dba` calls `0x621c9b40`, which is **RET 4**. No visible effect in this client. We do not send it. |
| `80 cb` | Four bytes: AngleToDataNode u8 at +2, PercentOfRangeToDataNode u8 at +3. Printer `0x621522e0`, consumer `0x621e02d0`: looks up **marker ID 0**, adjusts opacity (angle >90: .25, otherwise 1), and scale (`percent / 100 + constant`). It is not an arrow-direction record. Create ID 0 first. |
| `80 cc` | Two bytes, no fields. Dispatcher `0x62179f03` calls `0x621c9b50`: **RET 4**. We do not send it. |
| `80 cd` | Twenty bytes: ID u16 +2, RezID u32 +4, position **three float32** at +8/+12/+16. Printer `0x621523b0`; consumer `0x621e0f70` adds an FX instance to a map keyed by ID, only if absent. Position is promoted to doubles, RezID becomes the FX resource. A changed/moving marker needs removal before replacement. |
| `80 ce` | Four bytes: ID u16 +2. Consumer `0x621e0240` deletes that FX and keyed entry. |
| `80 cf` | Two bytes, no fields. Consumer `0x621db3b0` deletes all keyed FX. Runtime uses individual `ce` removals for its own IDs. |
| `80 c5` | Header: count u16 +2, DisplaySeconds u8 +4. Then count **four-byte** nested records: unused byte, DeltaX i8, DeltaZ i8, Relationship u8. Constructor `0x6213c1f0`, printer `0x621520d0`, dispatcher `0x621772df`. It clears minimap entry type 15 (`0x620ccc20`) and adds each ping with its duration (`0x620ccbf0`). `0x62016c02` reads signed offsets, multiplies by double **100** at `0x6286db58`, and adds the player's origin. Relationship 1 = green `ff00ff00`, 2 = red `ffff0000`, otherwise yellow `ff00ffff`. This is a minimap ping, not a floorplan-unlock record. |

Examples pinned by unit tests:

```
80 cd 34 12 62 04 00 28 00 00 c8 42 00 00 48 43 00 00 96 c3
# ID 0x1234, DataNode FX 0x28000462 at (100,200,-300)
80 ce 34 12
80 cb 5a 64
80 c5 01 00 0a 00 fe 03 02
# one red ping, (-2,+3) metres, 10 seconds
```

## Find Data Node and tiers

`ability/spy/misc.py LocateDataNode_DirectObject` and `tempmods.py LocateDataNodeTerm/VisLocDataNode` establish the tool, range, mode and update interval. Handheld 422 searches 30 m for 10 s per pass; goggles 423 / 38209 search 5 m every second; advanced goggles 38208 search 15 m. The start/stop lines and tool-required line are preserved. Carried bag tools count; equipment and storage slots do not. The sourced toggle/exclusive flags use the existing `80 bd 05` activation record; toggle off, removed tool, death, unloaded 621 and district change clear the search's markers and activation state. Ordinary movement removes/rebuilds markers on the next pass.

DataNode 606's `FX/FXID` is `0x28000462`. Located nodes create this effect through `80 cd`; nearest node uses ID 0, then the handheld's `80 cb` attenuation. No synthetic payout, access difficulty, tapping economy or DataNodeContainer 47080 is substituted for a located node.

538 **Data Node Goggles** and 539 **Data Tap** are prerequisite tiers in the GOB. No shipped Python callback reads them; there is no separate cast, damage or stat to invent. They retain the existing loadout/dependency metadata. 540 is the flag passed to goggles' native `visualLocateDataNode`; 541 requests the nearest luggable when using the handheld.

**Our presentation choices:** 540 highlights nearby ground light luggables and other players carrying them within 5 m with the shipped `FX_OBJECT_LUGGABLE_AURA` (`0x280004f9`). The exact retail white-character-glow path is not identified. 541 sends a yellow `80 c5` ping at the nearest same-floor ground luggable every 10 s. Its native waypoint record remains unidentified, so it cannot point beyond the signed-offset range (about 127 m). Search uses a 250-unit floor tolerance; native occlusion is not reconstructed. Node FX and these presentations need native verification.

## Shared light luggables

`serverrules.py TestLightLuggablePickup` (10004) establishes level, singleton and PickupExclusive checks. `utility_objects.json` stores each pickup usable's actual **component index**, reach, size and requirements; e.g. 596 pickup is use **2**, Bomb Pet 47014 is **6**, Quicksilver 47100 is **11**. CurrentState is the GOB's unused state 2111 (`0x83f`). A real view/spawn handle, living player, matching district, same floor and the stored 100-unit three-dimensional reach are required. The shared object disappears for all observers only after the inventory insertion commits. A full bag leaves it available for retry. Successful pickup also runs the script's `BreakExclusiveAbility`: implemented stealth and Find Data Node searches end, after the inventory/ground transfer commits. Failed or forged pickups leave those states intact.

`5c <slot u16> 01` drops a light luggable from a personal bag slot. Client sender `0x621e1d10` checks the item with `0x625d97b0`, writes personal flag 1 at `0x621e1d79` and slot at `0x621e1d87`, then sends it. `5b` remains unused. Equipment/storage slots, nonluggables, stacks, bad flags and malformed bodies are rejected. The server projects the drop to a navmesh floor within 150 units of the player and persists that position. The client offline implementation offsets the object in front of the player; our at-feet floor placement is a deliberate choice.

`world_luggables` is an InnoDB table in the selected server database. A drop transaction removes exactly one bag row and inserts a ground row; a pickup locks that ground row, finds a free slot, inserts the bag item and deletes the ground row in one transaction. Runtime serializes its transfers and visibility. Replayed requests cannot duplicate the item. A fresh server reloads ground objects. Carried items use normal inventory persistence. This covers one server per database, not cross-process shared-world synchronization.

Still absent: heavy-luggable attachments/Hyper-Strength carrying, faction/crew binding and ownership, Unbind/UniqueID, luggable stat buffs/forced PvP, automatic drop on death or construct change, timed recovery/respawn and quest-specific luggable mechanics. Heavy carrying is explicitly refused. No full retail parity is claimed for these items.

## Placements and missions

The three shipped district cooked COD files (`slums_barrens_full`, `downtown/dt_world`, `international/it`) contain Hardline, RichWorldGOs, TransportGroup and TransportSpace records, but no DataNode/Luggable class or RichWorldGO reference to their GOIDs. Existing static CSVs do not supply their placements. GOB has a LuggableSpawner 46627 definition, with no usable ambient coordinates here. This package **does not invent retail ambient spawn coordinates**. Persistent explicit placements and dropped items work; `?utilityobject` supplies reviewable QA placements. Sourced ambient generation remains a gap.

Download Mission Map preserves the `rshooks.py ComputeDownloadMissionMapOutcome` roll and six cumulative access bits. The successful roll now has visible minimap output, refreshed every 2 s for 3 s while the ability remains loaded and a mission is active. Mission-object positions and hostiles come from the current generated phase, on its sourced furniture/NPC spawners. Known tracked locked static doors inside the mission site can be marked.

**Our bit interpretation:** bit 1 reveals mission objects (yellow), bit 2 living phase hostiles (red), bit 4 known locked doors (yellow). The Python hook does not identify what any bit unlocks in native mission code; the higher bits do not add guessed features. Ping offsets outside i8 range are skipped, never wrapped. This does not reveal unknown interiors, map geometry, unspawned enemies, NPC conversation objectives or an entire team map. Native floorplan-reveal code and exact flag semantics remain gaps.

## Validation and native checklist

Unit tests pin byte layouts, signed-offset boundaries, payload limits, floor/district/range, level, singleton, exclusive-item and heavy rejection. Fresh-server cases: `data-mining-luggable-persistence`, `data-mining-full-bag`, `data-mining-locator`, `data-mining-mission-map` (`locked-safe.xml`). Headless validation proves these server contracts, not client rendering.

**Claude only:** the `data-mining` preset offers 44, 538, 539, 540, 541 and 621, with handheld 422 in the disposable bag. Use CSR_Tester110 as the only native character, within the existing native lock/cleanup workflow. The fixture itself acquires its lock; agents do not invoke it. The existing QA credential is cloned. `?utilityobject` and `?spawndatanode` require `MXO_DEV_COMMANDS=1`; `?utilitylocator` additionally requires an `mxotest_` database.

```powershell
$root = 'C:/Users/russe/Developer/mxo-wt/codex-package-p3-data-nodes'
$qa = Join-Path $env:TEMP ('mxo-pr113-' + [guid]::NewGuid().ToString('N'))
$head = (git -C $root rev-parse HEAD).Trim()
dotnet build "$root/tools/NativeQa/NativeQa.csproj" -c Release
dotnet "$root/tools/NativeQa/bin/Release/net8.0/NativeQa.dll" $root $qa $head data-mining
```

Wait for `state.json` Ready and `Im'running :D` before logging in as CSR_AbilityQA / CSR_Tester. Use the current full PR head in the test report.

1. `?utilitylocator none`, double-click **Find Data Node** in Actions: expect "A tool is required to activate this ability." Do not cast prerequisite-only tiers 538/539.
2. `?utilitylocator 422`, `?utilityobject 606`, `?utilityobject 596`. Double-click Find Data Node; expect the searching line and node FX at the fixture. Inspect the minimap for a yellow luggable ping. Move several metres, wait 10 s, and verify the updated attenuation/position. Double-click again and verify node FX disappears.
3. `?utilitylocator 423`. Activate again: node FX within 5 m and luggable aura. Walk >5 m away and back, allowing 1 s each; FX must disappear/reappear. `?utilitylocator none` must clear the FX. Record whether the effect resembles retail goggles; do not mark the guessed white-glow presentation retail-complete.
4. Equip the locator with `?utilitylocator 423` and activate Find Data Node. Walk within 1 m of fixture 596 and **double-click it** (usable defaultable in GOB). Expect the object to disappear, one item to enter the bag, and the exclusive search/FX to turn off on successful pickup. Drag that bag item onto the world: expect it to leave the bag and respawn at the player's floor. Pick it up again. Repeat with a full bag; object must remain until space is freed. Check another observer if available.
5. After a drop, log out and reconnect while the same fixture stays running. The ground object must remain and be pickable once. Fresh-server restart persistence is covered headlessly; the NativeQa fixture disposes its database when stopped.
6. With Download Mission Map loaded, obtain a mission normally (use the phone Missions/Contacts list to select Flood), accept it, reach the objective floor, then `?missionobject 1` for a mission with object 1. Inspect yellow object pings; inspect red enemy pings in a combat mission. Log the access flags and note that the floorplan itself is not unlocked. Abort or finish the mission: pings should age out within 3 s.
7. Record build SHA, node/goggles appearance, luggable pickup/drop/menu behavior, minimap colors/timing and any missing/incorrect native input or rendering. No native pass is assumed from headless records.

8. Close the native client and create the fixture's stop file (`New-Item -ItemType File -Path (Join-Path $qa 'stop')`). Wait for normal exit. Inspect `stopped.json` for CSR_Tester110 at Mara `(23828,495,13984)`, full health, pvpflag0 and mission986 only, and confirm the lock was released.
