# Data nodes, luggables and mission map pings

This is **partial reconstruction**, using the 7.6005 `client.dll`, `gameobjects.gob` and the shipped Python. Claude native QA at c37727c reached a successful on-wire luggable pickup but failed the locator check because the focused loadout omitted215. The corrected 4cf2de4 preset was accepted in Claude's subsequent locator check. The missing Data Miner folder in Actions > Abilities is expected: this client excludes tool abilities from that tab. Use Actions > Tools; see the [client filter and activation evidence](../research/abilities/data-mining-actions.md). This does not establish a native pass for every FX, drop or mission-map presentation. `DataMining`, `DataMiningPackets` and `WorldLuggablesDb` implement the runtime. `research/world-data/utility_objects.py` reproduces `data/utility_objects.json` from the read-only client. GOB SHA-256: `3e47f11012f6f219a26c9f5decfa10cbc00726c397937d03c3068254e5424103`.

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

## Locate Data Node 215, Find Data Node 621 and tiers

215 and 621 are distinct templates and keep distinct wire activation IDs. 215 stores CastingScript176, cap5 and cost5 IS; 621 stores MasteryAbility1 and MasteryBaseAbility=215, cap50 and cost0. Both execute the base LocateDataNode callback, using a search state that retains the actual cast ID. Starting one while another is active ends the active search; cleanup never sends an off record under an invented alias. 422/423/38209 explicitly require **loaded215**; advanced38208 explicitly requires622. Mastery621 does not silently satisfy the native tool's215 requirement. These properties now also appear in the reproducible export's four `locators` rows.


`ability/spy/misc.py LocateDataNode_DirectObject` and `tempmods.py LocateDataNodeTerm/VisLocDataNode` establish the tool, range, mode and update interval. Handheld 422 searches 30 m for **10 seconds and then deactivates** (the source tempmod Duration); goggles 423 / 38209 search 5 m every second; advanced goggles 38208 search 15 m. The start/stop lines and tool-required line are preserved. Carried bag tools count; equipment and storage slots do not. The sourced toggle/exclusive flags use the existing `80 bd 05` activation record; toggle off, removed tool, death, unloaded active search ability/tool prerequisite and district change clear the search's markers and activation state. Ordinary movement removes/rebuilds markers on the next pass.

DataNode 606's `FX/FXID` is `0x28000462`. Located nodes create this effect through `80 cd`; nearest node uses ID 0, then the handheld's `80 cb` attenuation. No synthetic payout, access difficulty, tapping economy or DataNodeContainer 47080 is substituted for a located node.

538 **Data Node Goggles** and 539 **Data Tap** are prerequisite tiers in the GOB. No shipped Python callback reads them; there is no separate cast, damage or stat to invent. They retain the existing loadout/dependency metadata. 540 is the flag passed to goggles' native `visualLocateDataNode`; 541 requests the nearest luggable when using the handheld.

**Our presentation choices:** 540 highlights nearby ground light luggables and other players carrying them within 5 m with the shipped `FX_OBJECT_LUGGABLE_AURA` (`0x280004f9`). The exact retail white-character-glow path is not identified. 541 sends a yellow `80 c5` ping at the nearest same-floor ground luggable for the 10-second handheld activation. Its native waypoint record remains unidentified, so it cannot point beyond the signed-offset range (about 127 m). Search uses a 250-unit floor tolerance; native occlusion is not reconstructed. Node FX and these presentations need native verification.

## Shared light luggables

`serverrules.py TestLightLuggablePickup` (10004) establishes level, singleton and PickupExclusive checks. `utility_objects.json` stores each pickup usable's actual **component index**, reach, size and requirements; e.g. 596 pickup is use **2**, Bomb Pet 47014 is **6**, Quicksilver 47100 is **11**. CurrentState is the GOB's unused state 2111 (`0x83f`). A real view/spawn handle, living player, matching district, same floor and the stored 100-unit three-dimensional reach are required. The shared object disappears for all observers only after the inventory insertion commits. Every utility c7 logs char/view/received and expected spawn/GOID/use, decision and DB outcome; success includes ground ID and bag slot. `?utilitystatus` (dev + mxotest_ only) reports loaded215/621, active search ID and carried light-luggable bag slots. A full bag leaves it available for retry. Successful pickup also runs the script's `BreakExclusiveAbility`: implemented stealth and Find Data Node searches end, after the inventory/ground transfer commits. Failed or forged pickups leave those states intact.

`5c <slot u16> 01` drops a light luggable from a personal bag slot. Client sender `0x621e1d10` checks the item with `0x625d97b0`, writes personal flag 1 at `0x621e1d79` and slot at `0x621e1d87`, then sends it. `5b` remains unused. Equipment/storage slots, nonluggables, stacks, bad flags and malformed bodies are rejected. The server projects the drop to a navmesh floor within 150 units of the player and persists that position. The client offline implementation offsets the object in front of the player; our at-feet floor placement is a deliberate choice.

`world_luggables` is an InnoDB table in the selected server database. A drop transaction removes exactly one bag row and inserts a ground row; a pickup locks that ground row, finds a free slot, inserts the bag item and deletes the ground row in one transaction. Runtime serializes its transfers and visibility. Replayed requests cannot duplicate the item. A fresh server reloads ground objects. Carried items use normal inventory persistence. This covers one server per database, not cross-process shared-world synchronization.

Still absent: heavy-luggable attachments/Hyper-Strength carrying, faction/crew binding and ownership, Unbind/UniqueID, luggable stat buffs/forced PvP, automatic drop on death or construct change, timed recovery/respawn and quest-specific luggable mechanics. Heavy carrying is explicitly refused. No full retail parity is claimed for these items.

## Placements and missions

The three shipped district cooked COD files (`slums_barrens_full`, `downtown/dt_world`, `international/it`) contain Hardline, RichWorldGOs, TransportGroup and TransportSpace records, but no DataNode/Luggable class or RichWorldGO reference to their GOIDs. Existing static CSVs do not supply their placements. GOB has a LuggableSpawner 46627 definition, with no usable ambient coordinates here. This package **does not invent retail ambient spawn coordinates**. Persistent explicit placements and dropped items work; `?utilityobject` supplies reviewable QA placements. Sourced ambient generation remains a gap.

Download Mission Map preserves the `rshooks.py ComputeDownloadMissionMapOutcome` roll and six cumulative access bits. The successful roll now has visible minimap output, refreshed every 2 s for 3 s while the ability remains loaded and a mission is active. Mission-object positions and hostiles come from the current generated phase, on its sourced furniture/NPC spawners. Known tracked locked static doors inside the mission site can be marked.

**Our bit interpretation:** bit 1 reveals mission objects (yellow), bit 2 living phase hostiles (red), bit 4 known locked doors (yellow). The Python hook does not identify what any bit unlocks in native mission code; the higher bits do not add guessed features. Ping offsets outside i8 range are skipped, never wrapped. This does not reveal unknown interiors, map geometry, unspawned enemies, NPC conversation objectives or an entire team map. Native floorplan-reveal code and exact flag semantics remain gaps.

## Native failure and captured pickup evidence

Claude's [native report](https://github.com/kylebrussell/mxo-hd/pull/113#issuecomment-6815877811) at c37727c (2026-11-12 00:51 ET) found missing Actions entries and the client's own "You need the Locate Data Node ability" refusal. The old preset owned2/17 plus the leaves, but omitted215 and the Hacker52/Data Miner537 folder ownership. Margin `0x07` serializes every owned row as slot u16 plus GOID+level u32; `0x06` serializes only loaded rows. The focused fixture now loads215 alongside621 and adds missing DisciplineBelongsTo ancestors **owned but unloaded**, retaining caps and avoiding unwanted parent stats. The same general helper supports other focused presets. The ancestor ownership fixes the known/loadout metadata; it does not force a folder into Actions > Abilities. The subsequent binary trace proves that215/621 are filtered there because RequiresTool=1. A Data Miner folder is not expected for this focused preset; passive44 and prerequisite538–541 are not clickable cast actions.

The same session's preserved packet log proves that the disappearing596 was an actual pickup, despite the old missing diagnostics. Source: `Temp/mxo-headless/b575bfcd0ca5/PacketLog.txt` lines1133–1145, CSR_Tester110, 00:51:30. Request and replies:

```
80 c7 05 00 03 00 02 00       # view5/spawn3/use2
03 01 00 01 01 00 05 00 00 00 # view5 deletion (object-list section)
5e 02 00 54 02 00 00 00 00 00 00 01 # goid596 into personal bag slot2
```

This establishes the native request and on-wire inventory transfer, not a visual bag-icon or drop/locator/map pass. No menu index or object type was guessed from the disappearing model.

## Validation and native checklist

Unit tests pin byte layouts, signed-offset boundaries, payload limits, floor/district/range, level, singleton, exclusive-item and heavy rejection. Fresh-server cases: `data-mining-base-and-margin`, `data-mining-luggable-persistence`, `data-mining-full-bag`, `data-mining-locator`, `data-mining-mission-map` (`locked-safe.xml`). Headless validation proves these server contracts, not client rendering.

**Claude only:** the `data-mining` preset offers 44, 538, 539, 540, 541 and 621, with handheld422 and distinct loaded215 in the disposable bag. Use CSR_Tester110 as the only native character, within the existing native lock/cleanup workflow. The fixture itself acquires its lock; agents do not invoke it. The existing QA credential is cloned. `?utilityobject` and `?spawndatanode` require `MXO_DEV_COMMANDS=1`; `?utilitylocator` additionally requires an `mxotest_` database.

```powershell
$root = 'C:/path/to/current/checkout' # use the checkout under review
$qa = Join-Path $env:TEMP ('mxo-data-mining-' + [guid]::NewGuid().ToString('N'))
$head = (git -C $root rev-parse HEAD).Trim()
dotnet build "$root/tools/NativeQa/NativeQa.csproj" -c Release
dotnet "$root/tools/NativeQa/bin/Release/net8.0/NativeQa.dll" $root $qa $head data-mining
```

Wait for `state.json` Ready and `Im'running :D` before logging in as CSR_AbilityQA / CSR_Tester. Verify `Configuration` is `Release`, `SourceAssembly` points at this checkout's Release server, and `SourceAssemblySha256` equals `SnapshotAssemblySha256`. The harness snapshots the server configuration it was built with. Use the current full PR head in the test report.

1. Inspect `state.json`: loaded Abilities must include215 and621; OwnedUnloadedDisciplineAncestors must include52/537. Before any fixture invocation, the read-only command `dotnet "$root/tools/NativeQa/bin/Release/net8.0/NativeQa.dll" --build-info $root "2,17,44,215,538,539,540,541,621"` must report those same ancestors and matching source hash. After login, `?utilitystatus` must report loaded215=5, loaded621=50, search=0. In Actions > Tools, inspect the handheld locator. Do not expect215 or621 in Actions > Abilities, or a Data Miner folder for this preset. 44 is passive;538–541 are prerequisite/passive tiers, not cast entries.
2. `?utilitylocator 423`, `?utilityobject 606`, `?utilityobject 596`. Double-click the goggles in Actions > Tools. The old client-side215 prerequisite refusal must be gone. Confirm the actual request/active search ID from `[DATA-MINING] search ... ability=...` and `?utilitystatus`; these shipped goggles request215, not621. Expect the searching line, native activation and node FX. With goggles, walk >5m away and return, allowing1s each; node/luggable aura should disappear/reappear. Record the appearance; exact retail white glow remains unverified.
3. Double-click the same locator in Actions > Tools to toggle the active215 search off; confirm status and its off record. Activate it again, then `?utilitylocator none` must clear search/markers/activation. The locator item is removed from Tools. The shipped basic/handheld tools select215 through their ReqAbilityID; none of these tools selects621. Distinct621 activation and215/621 cross-toggle cleanup remain covered by the fresh-server locator case, not by an invented native Data Miner button. No native621 input route is claimed here.
4. `?utilitylocator 422`, double-click the handheld in Actions > Tools to activate215. Expect node FX, attenuation and the nearest luggable yellow ping. Wait10s: handheld search/FX must stop automatically. This tests source expiry, not repeated indefinite polling.
5. `?utilitylocator 423`, activate a search, walk within1m of596 and double-click the object (GOB default usable2). Confirm `[DATA-MINING] pickup ... goid=596 use=2 outcome=success db=committed ... bagSlot=N`, `5e` and `?utilitystatus` carried596@N; search must stop. Drag the bag item onto the world and confirm the matching committed drop log plus5f, then pick it up again. A full bag must leave the object available for retry. If it disappears, distinguish deletion+5e success from any logged denial rather than inferring success from appearance alone.
6. After a drop, log out/reconnect with the same fixture running; it must remain pickable once. A full-server restart is already covered headlessly; NativeQa disposes its DB when stopped.
7. With passive44 loaded, accept a mission via the phone Missions/Contacts list, select Flood, reach its objective floor and use `?missionobject 1` for a mission with object1. Note the real `[UTILITY] Download Mission Map` roll/flags. For a deterministic presentation check run `?utilitymap 7` (dev + mxotest_ + active mission + loaded44 only); this is explicitly a fixture override, not evidence of a successful retail roll. Inspect yellow objects/red enemies, and note that the floorplan itself is not unlocked. Abort/finish: pings must age out within3s.
8. Record full SHA, source/snapshot hashes, exact owned/loaded lists, actual Tools inputs and215 records (621 remains headless coverage), pickup/drop logs and native FX/map observations. Close the client and create the fixture stop file (`New-Item -ItemType File -Path (Join-Path $qa 'stop')`); inspect stopped.json for CSR_Tester110 at Mara `(23828,495,13984)`, full health, pvpflag0 and mission986 only, and verify lock release. Claude alone performs these native steps; no new pass is claimed here.
