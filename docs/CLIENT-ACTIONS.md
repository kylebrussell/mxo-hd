# Client actions

Every request the 7.6005 client can send to the world server, what it is, and what the server does with it. Use it to pick the next gap to close.

## How this was built

- **The client's requests.** `docs/generated/client-rpc-classes.txt` lists the RPC classes in `client.dll`. A class is a client request when the client builds it and passes it to its send function (0x6214c700: 187 of its 231 call sites were traced back to the class they send), or when the server-to-client dispatcher (0x62175570, jump table 0x6217e9c0) has no case for its opcode. 266 opcodes have a dispatcher case. Some opcodes are used in both directions, with different classes.
- **What triggers them.** The sending function's callers: the UI source file named in their asserts (`viewmailinbox.cpp`, `viewplayer.cpp`, ...), the UI event codes they compare, and the slash command strings (`stringtable_client.sta` in `packmaps/zmisc_1.pkb`, UTF-16, each text after its id).
- **Retail's answers.** Protocol 04 records parsed out of every file in `research/packet-dumps` (76,921 records): for each client request, what the server sent next.
- **Status codes.** Their names come from the client's registrations (0x622a6480, 649 codes): `(success << 31) | (category << 24) | id`, for example `0x8e000084` = `LTOBJSRV_SOECHAT_SUCCESS`.
- **Today's sessions.** `world-pop/UnknownRPC.txt` (2026-11-11). Its header is the opcode as a little-endian u16, so `4801` is `81 48` and `be00` is `80 be`.

Opcodes are as on the wire: one byte below `80`, otherwise `80 xx` or `81 xx`. Offsets in layouts count from the record's first byte.

**Evidence:** `D` = client.dll (class, sender or handler), `C n` = sent n times by clients in the retail captures, `U n` = n times in today's UnknownRPC.txt, `S` = client string. **Server today:** *handled* (does what retail did, as far as the evidence goes), *partial* (handled for some cases only), *stub* (accepted, nothing useful done), *ignored* (falls to `default:`), *wrong* (does something else). **Impact** is a judgement of how much a player misses. Anything marked "guess" is not established.

## Summary

| | High | Medium | Low | Total |
|---|---:|---:|---:|---:|
| Handled | 49 | 42 | 12 | 103 |
| Partial | 0 | 0 | 1 | 1 |
| Ignored | 0 | 6 | 36 | 42 |
| Wrong | 0 | 0 | 1 | 1 |
| Not sent by this client | | | | 19 |
| **Total** | 49 | 48 | 50 | 166 |

Counts are rows of the tables below (`81 47` and `81 4a` share a row). The first audit found 15 handled rows that had been ignored: email (11 rows, including the long-string records `81 ab`/`81 ac`), the ignore list (2), and the world-entry `81 48` and `0c`. Since then the subway (`81 3b`, `81 3c`, `81 3d`) and stack splitting and merging (`68`, `67`) are handled, and `81 1b`/`81 1d` turned out never to be sent (see "Looting single items"). The chat room requests (16 rows, two of them new: `81 c0` create was missing from the class list, and `81 c5` is also sent by `/roomallow`) turned out never to be sent: the client doesn't register the room commands (see "Chat rooms"); the server handles them anyway. Handled since then: booting and promoting in teams and crews, first mates, faction ranks, removing a crew from a faction, the crew creation window (`80 84` turned out not to be a faction invite) and the team seekers list. Paired emotes, stances (`34` enters one, `33` leaves it), giving a mission item by dragging it (`66`) and callbacks for any prompt or FX token (`81 90`, `81 91`) followed; `5b` is never sent, `60` is compiling (Codex's area) and `5c` drops a luggable item.

The biggest gaps left, by impact:

1. **Combat style at world entry** (`3e`, U 232): Codex's area (combat), not touched here.
2. **Compiling code** (`60`, `81 2e`, `81 31`): Codex's area (crafting).
3. **Stance sites** (stance 1, benches): entered through `80 c8`; not done.

## Login and world entry

| Request | Name / purpose | Evidence | Server today | Impact |
|---|---|---|---|---|
| `05` | Spawn ready: the client is ready for its own view | C 274 | handled | high |
| `0c 01` | LoadingComplete, sent from `OnEnterWorld` with `80 be`, `3e` and `81 48` | D, C 181, U 212 | handled: retail answered nothing, now so does the server | low |
| `80 be` | No fields. Sent at world entry by the inventory/ability manager (0x629ea4e8, 0x621e2cf0) after it re-sends its slots. Retail answered nothing directly; the `80 bc` effect list followed in 102 of 181 cases, which is also sent at every world entry anyway. Guess: asks for the active effects | D, C 181, U 212 | ignored | low (guess) |
| `3e` | Slot, Ability, IsSlot, IgnoreStyleUpdate. At world entry `6b 00 10 00 00 00 00` (147 times), `6b 85 00 ..` (37); also sent when picking a special move (caller has `CombatSpecialMove`). Guess: the combat style / special move in use | D, C 187, U 232 | handled: validated selection / own attribute39; a style becomes the active one (stats, special-move set, interlock exchanges). Producers: the toolbar dropdown slot's list, the toolbar window's construction (world entry), bag-slot moves. Using a style from the Actions window arrives as `80 b9 <style>` and switches the same way; see [COMBAT-STYLES.md](COMBAT-STYLES.md) | high |
| `81 48` | No fields, from `viewplayer.cpp`. Retail answered `81 49` (FeatureArray, CharacterID, SubscriptionFeatures), mid-session too (luizoe_margins_Syntax packets 1296/1298) | D, C 184, U 237 | handled: structured bounded `81 49` (PR99 fixes native crash) | low |
| `81 08` | Ready for the world change (Status) | C 209 | handled | high |
| `80 c9` | Region loaded (ObjID, ObjPos) | C 10438 | handled | high |
| `81 00` | LA picker selects tutorial construct800..899 (client.dll62171620); normal picks use80ff. Retail resets and loads tutorial_v2 | D, C 8, U 3 | handled only in LA; pending-reset spawn guard; [evidence](CLIENT-COMBAT-ACTIONS.md#deathreconstruction-follow-up-2026-11-11) | high |
| `81 01` | No fields. Retail answered `80 fa` (ConstructID, HardlineID, MakeActive) then `81 07`: a world change. Guess: jack in from the Loading Area by another route | C 8 | ignored | low (guess) |
| `81 56` | No fields; 13 send sites in `viewmgr.cpp` | D, C 1 | ignored | low (purpose unknown) |
| `81 4d` | ElapsedTime u32; retail echoed `81 4d` | C 1 | ignored | low |

## Movement, travel and the world

| Request | Name / purpose | Evidence | Server today | Impact |
|---|---|---|---|---|
| `80 c2` | Jump start | C 112 | handled | high |
| `80 c4` | Jump cancel / hyperjump target (Destination, Sector, Prefab, NavPoly) | D, C 48 | handled | high |
| `80 c7` | Interact with a dynamic object (Handle, ComponentOffset) | D, C 92 | handled | high |
| `80 c8` | Interact with a static object (ObjID, ComponentOffset) | D, C 107 | handled | high |
| `6c` | Hardline status for a district | D, C 348 | handled | high |
| `81 8e` | Hardline teleport (From/To hardline and construct) | D, C 119 | handled | high |
| `80 fc` | Leave the world (ExitType) | D, C 66 | handled | high |
| `80 fe` | Exit finish: quit or go to the Loading Area | D, C 82 | handled | high |
| `80 ff` | Leave the Loading Area by a hardline | D, C 48 | handled | high |
| `81 4c` | Respawn after death | D, C 23 | handled | high |
| `81 40` | Elevator floor | D, C 29 | handled | medium |
| `81 41` | Elevator CurrentFloor: the client closes the panel when it decides the player left it (docs/MISSIONS.md) | D, U 7 | ignored; harmless, the client closes the panel itself | low |
| `81 3b` | No fields: the subway map's Cancel, or its countdown reached 0 (0x620fc490; the map closes) | D | handled (new): ends the ride | medium |
| `81 3c` | WorldID, NetworkID, ConnectorID: the station picked on the subway map (0x620fc520) | D | handled (new): the player moves to the station and the world reloads (`81 07`) | high |
| `81 3d` | No fields: the map is open (0x620fc150, sent from its set-up) | D | handled (new): the `81 3e` countdown starts | medium |
| `39` | Direction (`02` 7 times) | D, C 8 | ignored | low |
| `81 46` | Range, Range2, from the slash command handler. Guess: `/range` | D | ignored | low (guess) |
| `81 37` | TimeRequested, from `viewcommon.cpp`. Guess: the clock / `/time` | D | ignored | low (guess) |

## Chat, emotes and presence

| Request | Name / purpose | Evidence | Server today | Impact |
|---|---|---|---|---|
| `28` | Chat line (ChatType, bShout, Text, RoomName, ChatHandle). ChatType 18 would be a room line (`/room`, which the client doesn't register) | D, C 1052 | handled; room lines (new) go to everyone in the room, as `2e` type 18 | high |
| `29` | Tell (TargetName, Text, bEchoOnly, ChatHandle) | D, C 81 | handled | high |
| `30` | Emote (EmoteID, TargetID): every emote; an interactive one (`/hug` and 16 more) asks the target | D, C 24 | handled: the player's own view, and now other players' views (new); a paired one sends `31` to the target (new) | medium |
| `32` | Answer to a two-person emote request (Result, EmoteID, Partner, WaitTime, IsRequester); the server's `31` asks | D, C 8 | handled (new): `32` to the requester, then its accepted animation | medium |
| `33` | Leave a stance (the current Stance): `/stand`, or moving out of one. Retail's `19` (249 times) was a stance the server imposed and never released | D, C 302 | handled (new): stance 0 to the own view and everyone else's | medium |
| `34` | Enter a stance (Stance): `/sit` `0a`, `/dance` `11`..`15` | D, C 21 | handled (new): the stance to the own view and everyone else's, sitting with ConditionStateFlags 0x00200000 | medium |
| `35` | Mood (Demeanor) | D, C 8 | handled | medium |
| `2a` | `/afk` (Afk u8; `01` once) | D, S, C 1, U 1 | ignored | low |
| `3b` | No fields; two senders, one in the slash command handler (0x62256760). Purpose not determined | D | ignored | low |
| `81 55` | No fields, from the slash command handler. Purpose not determined | D | ignored | low |
| `81 47`, `81 4a` | No fields; no sender found | D | ignored | low |

## Chat rooms (custom channels)

All from the slash commands `/roomcreate` .. `/roomtopic` (strings 0x02000160..0x02000175), and **the 7.6005 client never sends any of them**. Its command handler (0x62256760) looks the typed word up in the chat manager's command map (+0xf0, 0x621c0900), then in the macro map (0x629ebe48 +0x40), and prints "Unrecognized command.  Please try again." (0x2e000002) when neither has it. The map is filled once from a fixed list of 116 string ids (0x62254020, looped up to 0x74 at 0x6225466e): `/orgnames` .. `/afk`, with no room command among them. No other code inserts into the map (the only other caller of the insert, 0x62253c20, is never called), and the ids 0x02000160..0x02000175 appear nowhere in client.dll except the handler's own switch, so its room cases are dead code. Nothing the server sends changes that: no feature flag (`81 49`, client-scripts/gameFeatures.py) or setting is read on the way. Checked in the real client (CSR_Tester, 75f540b): `/roomcreate`, `/roomenter` and `/room` all print "Unrecognized command" and send nothing.

The server keeps its room code (the records are client.dll's, the rules ours: see "Chat rooms in detail"). It is reachable only through SocialBot and the headless client, and would serve a client build that registers the commands.

| Request | Name / purpose | Evidence | Server today | Impact |
|---|---|---|---|---|
| `81 c0` | Create (RoomName, Topic, Password, IsPrivate .. IsGameLocal, MaxOccupancy): `/roomcreate room[:password] [topic]`. Missing from `client-rpc-classes.txt` (vtable 0x6288e7d0, sender 0x622461f0) | D, S | not sent by this client: the command isn't registered (see below). The server handles it (handled (new): `81 c1`) | none |
| `81 c2` | Enter (RoomName, Password): `/roomenter room[:password]` | D, S | not sent by this client: the command isn't registered (see below). The server handles it (handled (new): `81 c3` to the room; a private room's knock is `81 c5` to its moderators) | none |
| `81 c5` | Let a waiting player in (PlayerName, RoomName, Allow, never set): `/roomallow room player`. The same class as the server's "%s is requesting entry" | D, S | not sent by this client: the command isn't registered (see below). The server handles it (handled (new): `81 c6`, then `81 c3`) | none |
| `81 c7` | Leave (RoomName): `/roomleave` | D, S | not sent by this client: the command isn't registered (see below). The server handles it (handled (new): `81 c8`) | none |
| `81 c9` | Destroy (RoomName): `/roomdestroy` | D, S | not sent by this client: the command isn't registered (see below). The server handles it (handled (new): `81 ca`) | none |
| `81 cd` | Invite (PlayerNameToInvite, RoomName): `/roominvite room player` | D, S | not sent by this client: the command isn't registered (see below). The server handles it (handled (new): `81 ce`) | none |
| `81 cf` | Uninvite | D, S | not sent by this client: the command isn't registered (see below). The server handles it (handled (new): `81 d0`) | none |
| `81 d1` | Kick | D, S | not sent by this client: the command isn't registered (see below). The server handles it (handled (new): `81 d2`) | none |
| `81 d3` | Ban | D, S | not sent by this client: the command isn't registered (see below). The server handles it (handled (new): `81 d4`) | none |
| `81 d5` | Unban | D, S | not sent by this client: the command isn't registered (see below). The server handles it (handled (new): `81 d6`) | none |
| `81 d7` | Make moderator (IsTemporaryModerator, sent as 0) | D, S | not sent by this client: the command isn't registered (see below). The server handles it (handled (new): `81 d8`) | none |
| `81 d9` | Remove moderator (IsTemporaryModerator before RoomName) | D, S | not sent by this client: the command isn't registered (see below). The server handles it (handled (new): `81 da`) | none |
| `81 db` | Set owner | D, S | not sent by this client: the command isn't registered (see below). The server handles it (handled (new): `81 dc`) | none |
| `81 dd` | Give voice | D, S | not sent by this client: the command isn't registered (see below). The server handles it (handled (new): `81 de` (kept, but no room can be moderated: see below)) | none |
| `81 df` | Remove voice | D, S | not sent by this client: the command isn't registered (see below). The server handles it (handled (new): `81 e0`) | none |
| `81 e1` | Find rooms (StartingNodeName, SearchFilter): `/roomfind [node] [filter]`, both words as typed. `/rooms` sends nothing | D, S | not sent by this client: the command isn't registered (see below). The server handles it (handled (new): `81 e2`, then `81 e3` per room) | none |

## Friends, ignore list, who and character info

| Request | Name / purpose | Evidence | Server today | Impact |
|---|---|---|---|---|
| `80 d6` | Add friend (BuddyName) | D, C 29 | handled | medium |
| `80 da` | Remove friend | D | handled | medium |
| `80 e0` | Ignore a player (IgnoreName) | D, S | handled (new): `80 e1` | medium |
| `80 e2` | Stop ignoring | D | handled (new): `80 e3` | medium |
| `81 52` | `/who` (Filter) | D, C 9 | handled | medium |
| `81 54` | `/whereami` | C 58 | handled | low |
| `81 92` | Player details (CharacterID, CharacterName) | D, C 7 | handled | medium |
| `81 94` | Get background (`vieweditbackground.cpp`) | D, C 1 | handled | low |
| `81 96` | Set background (ChunkString, IsChunked) | D | handled | low |
| `81 57` | Inspect a target (TargetHandle, GetAbilities, GetInventory, GetTempMods). Guess: the GM `/snooploadout` | D, S | ignored | low |
| `81 59` | Snoop a crew (SnoopManager, CrewName): GM `/snoopcrew` | D, S | ignored | low |
| `81 5b` | Snoop a faction: GM `/snoopfaction` | D, S | ignored | low |

## Email

The phone's Email window, `/mail`, and mail the server sends (retail forwarded loot to it when the bag was full: "New items forwarded to email because your inventory is full."). Retail: `mxopackets1_crashover2k_afterwhoruneo_proxy_026`, packets 14653-14716 (list, read two, reply to System, close, list again).

| Request | Name / purpose | Evidence | Server today | Impact |
|---|---|---|---|---|
| `81 ea` | Get headers (Category, MaxHeaders, StartTime, DescendingOrder): opening the inbox | D, C 3 | handled (new): `81 eb`, `81 ef`, `81 ed` per message | high |
| `81 f0` | Read a message (PersistentMessageID) | D, C 3 | handled (new): `81 f3` | high |
| `81 e5` | Send (SenderName, Category, CategoryLimit, EnforceInboxLimit, ChunkStrings, AreChunked) | D, C 1 | handled (new): `81 e6`; the recipient gets `81 ed` with Notification | high |
| `81 ab` | Long string header (TotalLength, ChunkSize, IsWide, IsMulti, Index): a chunk of the next request follows | D, C 1 | handled (new), for mail | high |
| `81 ac` | Long string chunk | D, C 1 | handled (new), for mail | high |
| `81 f4` | Set one message's status (Delete = 4, Undelete = 3) | D | handled (new): `81 f5` | medium |
| `81 f6` | Change every message of one status (on closing: 4 -> 5, 1 -> 2) | D, C 4 | handled (new): `81 f7` | medium |
| `81 fc` | Take attachments (PersistentMessageID, Items) | D | handled (new): `81 fd` | high |
| `81 fe` | Delete a message: the client sends it for mail from someone it ignores (0x621f9549) | D | handled (new) | low |
| `81 f8` | Move to a folder (PlayerName, PersistentMessageIDs, NewFolder) | D | handled (new): `81 f9` | low |
| `81 fa` | Delete mail older than a time (PlayerName, Category, TimeOlderThan) | D | handled (new): `81 fb` | low |

## Teams, crews and factions

| Request | Name / purpose | Evidence | Server today | Impact |
|---|---|---|---|---|
| `6e` | Invite to crew or faction (GroupType, PlayerName, GroupName) | D, C 3 | handled | high |
| `80 8c` | Invite to a mission team (InviteeName, TeamName, LootSharing) | D, C 2 | handled | high |
| `6f` | Answer an invite (GroupType, PlayerName, Status) | D, C 9 | handled | high |
| `71` | Leave a group (GroupType) | D, C 2 | handled | high |
| `80 90` | Team loot mode | D, C 4 | handled | medium |
| `7a` | Create a faction by inviting (InviteeName, FactionName) | D | handled | medium |
| `80 f4` | Faction info (FactionID) | D, C 372 | handled | medium |
| `77` | Disband (GroupType), from `viewflashtraffic.cpp` | D, C 1 | handled | medium |
| `78` | Deposit to / withdraw from the group bank (GroupType, Amount, Deposit), `viewfactionstatus.cpp` | D, C 1 | handled | medium |
| `72` | GroupType, PlayerID: boot a team member (team window Boot, 0x621c0c40) or remove a crew member (crew right-click "Remove"), each after a confirm dialog | D | handled (new): `72` to every member, the removed one included | medium |
| `75` | GroupType, PlayerID: make a member team leader (Promote, 0x621c0700) or crew captain ("Make Captain") | D | handled (new): `75` to every member; `7e` to the crew's faction | medium |
| `73` | CrewID: remove a crew from the faction (faction members window "Remove", for the leading crew's captain) | D | handled (new): `73` to the faction, `74` if refused | low |
| `80 80` | Promote / demote a crew in the faction (MemberID is a crew id, bPromote): faction members window | D | handled (new): `7f` to the faction, `80 81` if refused | medium |
| `80 87` | Set the first mate (FirstMateID; 0 demotes): crew right-click "Make First Mate" / "Demote" | D | handled (new): `80 87` to the crew | low |
| `80 84` | Create a crew by inviting someone (InviteeName, CrewName, OrganizationID): the crew creation window, not a faction invite | D | handled (new): `80 88` to the invitee, `80 85` to the creator | medium |
| `80 89` | Hovercraft action (submessage; the server's `80 8a`/`80 8b` carry TransactionCode, HovercraftSlot) | D | ignored | low |
| `80 91` | Join / leave the team seekers list (RemoveFromSeekerList, ExtraDisciplines, OrgsToWorkWith): the team window's Seek | D | handled (new); the client answers itself | medium |
| `80 92` | Search the seekers list (MinLevel, MaxLevel, MyFactionOnly, DesiredDisciplines, OrgsToWorkWith): Find Team Members. Retail answered neither capture (40 s) | D, C 2 | handled (new): `80 93` | medium |
| `80 f2` | FactionID. Guess: the faction's constructs (answer `80 f3`) | D | ignored | low |
| `80 f6` | Hack a construct (FactionID, ConstructID), `viewhackconstructs.cpp` | D | ignored | low |
| `80 ef` | Construct firewall / defence settings | D | ignored | low |

## Missions and tutorial

| Request | Name / purpose | Evidence | Server today | Impact |
|---|---|---|---|---|
| `80 94` | Mission list from a contact (SessionID, Organization) | D, C 8 | handled | high |
| `80 98` | Mission details (SessionID, PreviewID, Size, Difficulty, AutoAccept) | D, C 8 | handled | high |
| `80 9b` | Accept (SessionID, Accepted) | D, C 8 | handled | high |
| `80 a6` | Abort | D, C 11 | handled | high |
| `81 91` | An FX token coming back (EventID): the token of a server `81 42`, when the FX ends | D, C 12 | handled (new): any feature's callback (`ClientCallbacks`), else the tutorial | medium |
| `81 90` | Answer to a modal (Message, LeftChosen): the Message of a server `81 8f`, echoed | D, C 8 | handled (new): any feature's callback (`ClientCallbacks`), else the tutorial | medium |
| `81 04` | A panel the server showed was used (ViewID, ResponseToken) | D, C 62 | handled (the tutorial is its only user) | low |
| `81 8d` | Shortcut added to the bar | D, C 20 | handled (tutorial) | low |
| `59` | Use an item (Slot): mission tickets | D, C 8 | handled | medium |

## Inventory, vendors, loot and the marketplace

| Request | Name / purpose | Evidence | Server today | Impact |
|---|---|---|---|---|
| `63` | Equip (SrcSlot, DestSlot) | D, C 40 | handled | high |
| `64` | Unequip (Slot, DestSlot) | D, C 18 | handled | high |
| `65` | Move or swap bag slots (SlotA, SlotB, IsCentralStorageItem) | D, C 34 | handled | high |
| `5d` | Recycle (Slot, PersonalInventoryItem, RecycleItem) | D, C 7 | handled | medium |
| `68` | Split a stack (SourceSlot u8, DestSlot u8, AmountToSplitOff u32, IsCentralStorageItem u8): the split dialog's amount dropped on a slot or a bag page | D | handled (new): `69` for the source, `5e` (or `69`) for the destination | medium |
| `67` | Merge stacks (Slot1 u16, Slot2 u16, IsCentralStorageItem u8): a stack dropped on a stack of the same item | D | handled (new): `69` for the destination, `69` or `5f` for the source | medium |
| `62` | SourceSlot, DestinationSlot, PersonalInventoryItem: between the bag and central storage (the drop handler sends it for central-storage drags) | D | ignored: the server has no central storage | low |
| `5b` | ItemHandle: pick up an item. Its sender (0x621e2140) is never called in this build | D | not sent by this client | none |
| `5c` | Slot u16, PersonalInventoryItem: drop a luggable item into the world (a dragged bag item released over the world, 0x621e1d10). The client refuses anything else: "You can only drop luggable objects in the world." The other sender is never called | D | partial: shared light-luggable drop/pickup and persistence; heavy attachments absent (DATA-MINING.md) | low |
| `60` | Compile stored code into an item (ObjectID, Slot u16, PersonalInventoryItem 0 = a code storage slot, InvSlotOfToolUsed, FromCrafting): the Compile window's button (`CfCo`, 0x620836b0) and the Loading Area storage window. Not "use an item on an object". The answer is `61 <ResultCode>` | D | ignored; code compiling is Codex's area | medium |
| `66` | Slot u8, TargetID: a mission item dragged onto the NPC whose key it carries (0x621eb080). The client prints "You hand over the %s." itself | D | handled (new): the mission's Give goal, as the NPC menu's Give | medium |
| `80 c6` | PillInvSlot: use a pill from the bag (callers in the inventory and hotbar menus) | D | ignored (pills work through `80 b9`) | low |
| `81 0e` | Vendor buy (ItemTypeID, BuyBackLastItemSold) | D, C 40 | handled | high |
| `81 11` | Vendor sell (Appraise, Slot) | D, C 5 | handled | high |
| `81 10` | StringID, CloseUI, sent as `00 00 00 00 01`. Guess: closing the vendor window | C 9 | ignored | low |
| `81 16` | Collector exchange (ItemSetIndex, Prizes) | D | handled | medium |
| `81 17` | `00 00 00 00 00`. Guess: closing the collector | C 1 | ignored | low |
| `81 1f` | Loot: take all, the container window's only take button (event `CTAl`, 0x62085060) | D, C 39 | handled | high |
| `81 1b` | Loot one item (ListID u32, ObjectID u32, InstanceData u32; size 0x0e). The class exists but client.dll never sends it: nothing calls its constructors (0x621d36b0, 0x621d3770) or refers to them | D | not sent by this client | none |
| `81 1d` | No fields (take the information; the server's `81 1e` InfoAmount answers). Its sender (0x621d4ff0) is never called, jumped to or pointed at | D | not sent by this client | none |
| `81 1a` | No fields, `viewcontainer.cpp`: closing the loot window. Retail answered nothing | D, C 40, U 33 | ignored; nothing to do | low |
| `81 21` | Slot u8: a bag item dropped onto the open container window (the drop handler's target 15, 0x620a045b, from the window's `MLUp`); the server's `81 22` (ObjectID, InstanceData, Status) answers. Guess: putting an item into a mission container | D | handled: nearby open mission Put container; `81 22` result; generic storage remains missing | low |
| `81 29` | Marketplace: my listings / next page (MarketplaceID, SearchForward) | D, C 7 | handled | medium |
| `81 24` | Marketplace search | D, C 84 | handled | medium |
| `81 26` | Marketplace buy | D, C 14 | handled | medium |
| `81 2b` | Marketplace: list an item | D, C 2 | handled | medium |
| `81 82` | Secure trade request | D | handled (docs/SECURE-TRADE.md) | high |
| `81 85` | Secure trade action | D | handled | high |

## Abilities, crafting and combat (Codex's area: not changed here)

| Request | Name / purpose | Evidence | Server today | Impact |
|---|---|---|---|---|
| `80 b9` | Use an ability | C 942 | handled | high |
| `80 ae` | Load / unload abilities (ObjID, UnEquipping, Equipping) | D, C 314 | handled | high |
| `80 b7` | Upgrade an ability (AbilityID, CSSlot, NumLevels) | D, C 139 | handled | high |
| `80 b5` | Raise attributes | D, C 6 | handled | high |
| `80 bb` | AbilityId (`28 04`, `0a 00`, `0c 00`): dismiss a removable beneficial effect | D, C 7 | handled: removable beneficial effects; hostile/protected effects refused | medium |
| `40` | Close combat (TargetID, ForcedInterlock, WeaponToUseSlot) | D, C 75 | handled | high |
| `41` | Ranged attack (TargetID) | D, C 21 | handled | high |
| `42` | Tactic (TacticalValue, TacticalTotalValue, TacticType) | D, C 98 | handled | high |
| `43` | Martial art (MartialArt) | D | handled: never sent by 7.6005 (producer `6219f0a0` and constructor unreferenced; `81 6e` ForcedMartialArt unread); retained for logging, no reply. See [COMBAT-STYLES.md](COMBAT-STYLES.md) | high |
| `44` | Withdraw (WantToWithdraw) | D, C 18 | handled | high |
| `3d` | Draw / stand down (IsAutoAttacking) | D, C 39 | handled | high |
| `3f` | Free Fire weapon | D, C 206 | handled | high |
| `50` | `/duel` | D, C 6 | handled | medium |
| `52` | Duel answer | D, C 15 | handled | medium |
| `81 9e` | `/pvp` | D | handled | medium |
| `81 51` | Target | D, C 972 | handled | high |
| `81 2e` | Compile code (SourceCode, CodeFragmentInfoArray, InvSlotForToolBeingUsed) | D | handled: validated compiler request and script contest; [limits](CLIENT-COMBAT-ACTIONS.md) | medium |
| `81 31` | Inventory-target ability (AbilityID, SlotID, InvSlotOfToolUsed, IsRecipeKnown) | D | partial: Decompile276 learns/persists recipes; fragment recovery and other inventory abilities remain | medium |
| `81 aa` | UserData, Interrupted: an FX or cast ending (guess) | D | ignored | low |

## Help and petitions

None of these are in the captures and the server has no help desk.

| Request | Name / purpose | Evidence | Server today | Impact |
|---|---|---|---|---|
| `81 b0` | Petition: create or cancel (TicketID, ChunkString, IsChunked, CancelTicket) | D | ignored | low |
| `81 b2` | No fields. Guess: list my petitions | D | ignored | low |
| `81 b4` | TicketID. Guess: get one petition | D | ignored | low |
| `81 b7` | No fields | D | ignored | low |
| `81 b9` | Knowledge base search (SearchString) | D | ignored | low |
| `81 bb` | Knowledge base article (Idstring) | D | ignored | low |
| `81 bd` | No fields | D | ignored | low |

## GM and developer

| Request | Name / purpose | Evidence | Server today | Impact |
|---|---|---|---|---|
| `81 7a` | Simulacrum order (ID u8, Command u8): Stay 0, Deactivate 1, Follow 3; window ID 0xfe; `/ordersimulacra` ([CODER-PETS](CODER-PETS.md)) | D, S | handled | medium |
| `81 7b` | Simulacrum order with a target (ID u8, Command u8, TargetObject u32): Attack 2 | D | handled | medium |
| `81 7c` | Simulacrum order with a value (ID u8, Command u8, Value u8): command 5 mode Defensive 0 / Aggressive 1 / Passive 2 | D | handled | medium |

## Email in detail

### Records

All layouts are client.dll's; the values are retail's. Strings are `<u16 length with the NUL> text 00` at a u16 offset. An array is `<u16 count>` then its elements; a string array's element is `00 <u16 offset from the element's start>` (0 = none), with the strings after the elements. Arrays of structures also start each element with a `00` byte (`81 fc`: `00 <ClassData u32> <InstanceData u32>`).

| Record | Layout |
|---|---|
| `81 ea` (C) | +2 Category, +4 MaxHeaders u32, +8 StartTime u32, +0c DescendingOrder u8. Retail: `"" 50 0x7ffffffe 1` |
| `81 eb` (S) | InboxLimit u32. Retail: 300 |
| `81 ef` (S) | +2 PlayerName (`SOE+MXO+<server>+<handle>`), +4 PersistentMessageCount, +8 Status |
| `81 ed` (S) | +2 Sender, +4 Folder, +6 Category (`PLAYER`, `SYSTEM`), +8 PersistentMessageID, +0c SentTime (unix), +10 Status, +14 More, +15 Notification, +16 ChunkString (the subject), +18 IsChunked |
| `81 ec` (S) | Status: the headers could not be fetched |
| `81 f0` (C) | PersistentMessageID |
| `81 f3` (S) | the `81 ed` fields to +14 More, then +15 ChunkStrings, +17 AreChunked. Retail's chunks: subject, body, the out-of-band XML |
| `81 f2` (S) | Status: the message could not be read (retail sent nothing on success) |
| `81 ab` (C) | TotalLength u32, ChunkSize u32, IsWide, IsMulti, Index |
| `81 ac` (C) | +2 Chunk (byte array) |
| `81 e5` (C) | +2 SenderName (0), +4 Category, +6 CategoryLimit, +0a EnforceInboxLimit, +0b ChunkStrings, +0d AreChunked. Chunks: recipients, subject, body, out-of-band XML (chunked, so sent first in `81 ab`/`81 ac`) |
| `81 e6` (S) | PersistentMessageID, Status |
| `81 f4` (C) / `81 f5` (S) | PersistentMessageID, NewStatus / Status |
| `81 f6` (C) / `81 f7` (S) | OldStatus, NewStatus / Status |
| `81 f8` (C) / `81 f9` (S) | PlayerName, PersistentMessageIDs (u32 array), NewFolder / Status |
| `81 fa` (C) / `81 fb` (S) | PlayerName, Category, TimeOlderThan / +2 PlayerName, +4 NumDeleted, +8 Status (the client then asks for the headers again) |
| `81 fc` (C) / `81 fd` (S) | PersistentMessageID, Items / Status |
| `81 fe` (C) | PersistentMessageID |

**Addresses.** Senders and recipients are `SOE+MXO+<world>+<handle>`, as retail's (`SOE+MXO+Vector-Hostile+Galishnikov`). client.dll's address check (0x6224e810) compares the parts with its SOEPrefix ("SOE"), GamePrefix ("MXO") and WorldPrefix settings. WorldPrefix is set at world entry (0x621707e0) to the name of the world the player logged in to, which the client only learns from the auth server's world list, that is the `worlds` table. Any other world part is another server's: the client shows `World.Handle` and refuses attachments to it ("You can't send attachments to a recipient on another server."). The server used WorldConfig's serverName ("Syntax") while the world list said "Recursion", so every address showed as `Syntax.<handle>`. `ChatAddress` now takes the name from the `worlds` table, for mail, tells, the friend list and the ignore list.

**Message status:** 1 new (the `81 ed` handler lights the new-mail indicator), 2 seen in the list, 3 read, 4 deleted (shown with Undelete), 5 gone. The inbox's Delete button sends `81 f4 <id> 4`, or 3 to undelete (0x620cb690). On closing the inbox the client sends `81 f6 4 5` and `81 f6 1 2`.

**Status codes** (`LTOBJSRV_SOECHAT_*`): `0x8e000084` SUCCESS; `0x0e000041` DESTAVATARDOESNTEXIST (retail's answer to a send to `soe+mxo+vector-hostile+system`, followed by a "Message Undeliverable" mail); `0x0e000052` PMSGNOTFOUND; `0x0e000081` INBOXLIMITEXCEEDED; `0x0e000068` SUCCESSBADDATA; `0x0e000077` NOTHINGTODO. The client prints "Mail sent successfully." or "Failed to send mail. Reason: %s." and the like for each answer.

**Out-of-band XML.** The client writes, on sending (0x620c8b40): `<MXO_Mail_OOB>`, `<Faction id="n"/>`, `<RecipientList><Recipient Name=".."/></RecipientList>`, `<Info Slot="amount"/>` (the amount really is in an attribute called Slot), `<ItemList><Item FromPI="True" Slot="n"/></ItemList>`. Retail's own reply had only the Faction element. On reading (0x621f5400), it takes the children of `ItemList` with `ClassData` and `InstanceData`, read with `atoi`. The compose window refuses, before sending: more than one recipient with attachments, items while dead or away from a hardline, more than MailMaxAttachments items, more information than the player has, singletons, luggables, mission items, and non-tradeable items to anyone but oneself.

### What the server does (ours, not retail's)

- Mail lives in table `mail` (made on first use; also in `SQL/reality_hd.sql`). Status 5 deletes the row.
- A send is checked whole before anything moves. An unknown recipient fails it with DESTAVATARDOESNTEXIST and a "Message Undeliverable" mail from System, as retail did; its text is ours. A full inbox (300) fails it with INBOXLIMITEXCEEDED.
- Attached items leave the sender's bag on sending and are listed in the message's ItemList. `81 fc` puts them into free bag slots; without room it answers `LTPS_SPPERSONALINVENTORYFULL` (`0x0c0002ca`) and keeps them.
- Attached information leaves the sender and reaches the recipient when the mail is sent. The received message has no place for an amount, so retail must have done something like this; how it showed it is not known.
- Reading a new or seen message marks it read. The new-mail notice is `81 ed` with Notification 1 to an online recipient. Nothing is said at login about mail that arrived while offline (no evidence of what retail did).
- No postage: the client has MailInfoCost and MailItemCost settings, whose values and use were not traced.
- Not done: mail to several recipients with attachments (the client refuses it anyway), folders other than storing the name, and items forwarded to mail when the bag is full (`MailService.SystemMessage` and `Deliver` are there for it).

### Dev helpers (`MXO_DEV_COMMANDS=1`)

- `?mailbot [subject]`: a mail from MailBot with a Code Bit 1 attached arrives at once.
- Mail sent to `MailBot` is answered with "RE: <subject>", the text quoted, and the attachments (and information) given back.

## Ignore list in detail

`80 e0` / `80 e2`: `<name offset u16>` and the name, like the friend list's `80 d6`. The answers `80 e1` / `80 e3` are `08 00 <Status u32> <name>`. client.dll (0x62255bb0) adds the name and prints "Ignoring %s." for a success code and "Failed to ignore %s." otherwise. The server answers SUCCESS (`0x8e000084`), DESTAVATARDOESNTEXIST for a handle nobody has, IGNORENOTFOUND (`0x0e000058`) for removing someone not on the list. At world entry it sends each stored name with `LTOBJSRV_CONTACT_LIST_SETUP_PLAYER_IGNORED` (`0x8e00003d`); that this is what retail sent at login is a guess (the handler compares against that code, and the friend list uses its neighbours 3b/3c the same way). The client does the filtering: chat, mail to and from ignored players.

## Subway in detail

No retail capture has a subway ride, so the records are client.dll's and the rest is ours. The stations are the client's own (`data/subway_network.csv`, docs/WORLD-DATA.md "Subway network").

| Record | Layout and what the client does |
|---|---|
| `81 3a` (S) | `<Position: 3 × f64>` (class 0x621429c0). Opens the map (window 0x47, 0x620fd9e0): the client looks in its own `.metr` for the line and connector within 2000 units of the position; with none the map stays shut |
| `81 3d` (C) | No body, sent as the map opens (0x620fc150) |
| `81 3e` (S) | `<Seconds u8>` (0x620fccd0): the departure countdown, shown as m:ss. The client starts at 60 and only takes a lower value. At 0 it shows "The subway is now leaving the station... sorry, but you missed it!" and sends `81 3b` |
| `81 3b` (C) | No body: Cancel, or the countdown ran out (0x620fc490). The map closes |
| `81 3c` (C) | `<WorldID u16> <NetworkID u16> <ConnectorID u16>` (0x620fc520, size 8). WorldID is the `universe.btb` id of the district whose map is shown (12, 7, 169). Clicking a link connector ("Downtown", "Richland", "International District") switches the map to that district's line of the same name, and NetworkID then indexes that district's lines (0x620fd31c). The client won't let the player pick their own station, and it closes the map |

**What the server does (ours):**

- Using a subway car (`80 c7 <view> <spawn> 00 02 00` on a 6568 view) opens the map on the nearest station's connector, if it is within 4000 units of the player; `81 3a` carries the connector's position, so the client always finds the line. The old answer sent the player's own position and `81 3e 0a 00`.
- `81 3d` starts a countdown: `81 3e` every second from 60 (the client's own starting value; retail's timing is not known). At 0 the client says the train left.
- `81 3c` is ridden if a map is open (up to 65 s after it opened), the connector exists and is a station (on a named line, not a link to another district) and isn't the one the player is at. The player is moved onto the connector at standing height (its nav mesh floor + 95), the position and district are saved, and the world reloads (`81 07`, as a hardline teleport does), which brings up the loading screen the client's subway strings ("Loading World...") belong to. Rides across districts work the same way.
- No cost, level or organisation check: none was found in the map's code.
- Dev helper `?subway` (`MXO_DEV_COMMANDS=1`): opens the map at the district's nearest station wherever the player stands.

**The cars.** The server shows each subway car (goid 6568) as a view and runs a door and departure cycle on it (`Subway.cs`, older than this work). Its states went to every player in range as system chat ("Subway ID 1 STATE NOW DRIVE_OUT"); they now go to the debug log. The car's view was keyed by "district followed by static id", so Moriah Projects' car was entity 18, which is also an NPC's id: the NPC pass deleted the car's view as an out-of-range NPC and the subway pass made it again, twice a second (21 creations in 12 s headlessly). Car ids now have their own range.

**The stall at Tabor Park (PR 97 test, not solved).** The tester's client logged in on Tabor Park's Red Line platform and then sent nothing more. The server's packet log shows the client's last packet arriving 2:51:04, right after the server sent the player's own view, the own-view position record (sector 43, region 81, polygon 50: the station prefab's floor, the same polygon as Moriah Projects, where a ride arrived fine) and the usual login RPCs. These were the same bytes as a lobby login except for the position. The subway car's view and its states went out only afterwards (2:51:06 and 2:51:28). A headless login on the same spot keeps going, and `?subway` and a ride work after it (`subway-platform-logins-keep-car-views`). What the client did is not known. If it happens again, a packet log (`MXO_PACKET_LOG=1`) of the attempt will show whether the client stops after the same records.


## Stacks in detail

No retail capture has a split or a merge; the layouts are client.dll's and the rules ours. All come from the bag's drag-and-drop handler (0x620a0000):

- `68 <SourceSlot u8> <DestSlot u8> <AmountToSplitOff u32> <IsCentralStorageItem u8>` (class 0x6209b8e0, size 8; DestSlot defaults to `6b`, none). The amount from the split dialog dropped on a slot (DestSlot is that slot) or on a bag page (DestSlot is the page's first free slot, `6b` when it has none).
- `67 <Slot1 u16> <Slot2 u16> <IsCentralStorageItem u8>` (0x6209b7c0, size 6). A stack dropped on a stack of the same item (0x620a0504), or on a bag page that holds one with room (0x620a01e6: the client looks for one with fewer than 20).
- `62` is sent for central-storage drags only.

**The server:** a split into an empty slot (or the first free one for `6b`) makes a new stack there (`5e` with the amount, the source's new count in `69`); onto the same item it adds if the stack stays within ItemStacks.MaxStack (64); splitting the whole stack moves it. A merge moves as much as fits (`69` for both, `5f` for an emptied source). Anything else (another item, a full stack, more than the stack holds, central storage) is refused with "You can't do that: ..." and the source's count sent again. These answers are the records the server already used for stacks (`69 01 <slot u32> <(count - 1) * 4 u32>`, which retail sent for a used cookie).

## Looting single items

The 7.6005 client can't take one item from a corpse or container. Its container window (`viewcontainer.cpp`) handles three events: `CTAl` (Take All: `81 1f`), `CCls` (close: `81 1a`) and `MLUp` (a bag item dropped onto the window: `81 21 <slot>`). The single-item request `81 1b` and the take-the-information request `81 1d` are compiled in, but nothing in client.dll constructs or sends them: their constructors and senders have no calls, jumps, or pointers in code or data. So there is nothing to implement for them, and the server keeps Take All as the only way to loot.

## Chat rooms in detail

The 7.6005 client doesn't register the room commands, so it never sends these requests (see "Chat rooms" above); what follows is what its compiled-in senders and handlers would do, and what the server does with requests from SocialBot or the headless client. No capture has a room. The records are client.dll's. Who gets which record, and the rules, are ours.

**Addresses.** Before sending, the client rewrites room arguments (0x6224ff40) and player arguments (0x6224f880) into chat addresses. Dots become `+`, and `SOE+MXO+<WorldPrefix>+` goes in front, so `/roomenter my.room` sends `SOE+MXO+Recursion+my+room`. For display, the client strips its own world's prefix again (0x6224f020 for rooms, 0x6224e810 for players). The server keeps the name after the prefix (`my+room`), compares names without case, and echoes full addresses back.

**Requests.** Strings are `<u16 offset from the record's first byte>` fields, as everywhere else. A string the sender never set keeps offset 0.

| Request | Layout | Command |
|---|---|---|
| `81 c0` (0x11) | +2 RoomName, +4 Topic, +6 Password, +8 IsPrivate, +9 IsModerated, +a IsPersistent, +b IsWorldLocal, +c IsGameLocal, +d MaxOccupancy u32 | `/roomcreate room[:password] [topic]`. IsPrivate is 1 only with a password; the other flags and MaxOccupancy are always 0 |
| `81 c2` (6) | +2 RoomName, +4 Password | `/roomenter room[:password]` |
| `81 c7`, `81 c9` (4) | +2 RoomName | `/roomleave`, `/roomdestroy` |
| `81 cd` .. `81 df` (6) | +2 player, +4 RoomName | `/roominvite`, `/roomuninvite`, `/roomkick`, `/roomban`, `/roomunban`, `/roomvoice`, `/roomunvoice` |
| `81 d7` (7) | +2 player, +4 RoomName, +6 IsTemporaryModerator (0) | `/roommod` |
| `81 d9` (7) | +2 player, +4 IsTemporaryModerator (0), +5 RoomName | `/roomunmod` |
| `81 db` (0xa) | +2 player, +4 RoomName, +6 Status (0) | `/roomsetowner` |
| `81 c5` (7) | +2 PlayerName, +4 RoomName, +6 Allow (0) | `/roomallow` (its usage line says `/roomallowenter`) |
| `81 e1` (6) | +2 StartingNodeName, +4 SearchFilter | `/roomfind [node] [filter]`, both words as typed |
| `28` (8) | +1 ChatType 18, +2 bShout 0, +3 Text (up to 255 characters), +5 RoomName, +7 ChatHandle | `/room room text` (0x6216aff0) |

Five commands send nothing. `/rooms` is accepted and dropped. `/roomsize`, `/roomprivate`, `/roompassword` and `/roomtopic` fall to the client's "not a command" path. The only check the client makes is for missing arguments ("usage: /roomX room player").

**Answers.** Each handler prints one System chat line and keeps no state. A status without the success bit is a failure ("Failed to ... Reason: %s."). Where a record names a player, a name left out (offset 0) means "you". `81 c3` without a name prints "You entered chat room %s."; with a name it prints "%s entered chat room %s.".

| Record | Layout | Prints |
|---|---|---|
| `81 c1` | +2 RoomName, +4 Status | "Created chat room %s." |
| `81 c3`, `81 c8`, `81 ca` | +2 PlayerName, +4 RoomName, +6 Status | entered / left / destroyed ("Destroy chat room successful." without a name) |
| `81 c5` | +2 PlayerName, +4 RoomName, +6 Allow | "%s is requesting entry into chat room %s." |
| `81 c6` | +2 PlayerName, +4 RoomName, +6 Status | "You allow entry into chat room %s." |
| `81 c4` | +2 RoomName, +4 Status | "You were denied entry into chat room %s." Unused, since no command denies entry |
| `81 ce` .. `81 e0` | +2 target, +4 actor, +6 RoomName, +8 Status | "%s kicks you ..." (no target), "You kick %s ..." (no actor), "%s kicks %s ..." (both) |
| `81 d8`, `81 da` | +2 target, +4 actor, +6 RoomName, +8 IsTemporaryModerator, +9 Status | the same three forms, with "temporary " when the flag is set |
| `81 dc` | +2 PlayerNameGainingOwnership, +4 RoomName, +6 Status | "%s gains ownership of chat room %s." |
| `81 e2` | +2 NumRoomsFound u32, +6 Status | "Found %i matching rooms" |
| `81 e3` (0x17) | +2 RoomName, +4 RoomTopic, +6 private, +7 moderated, +8 persistent, +9 world-local, +a game-local, +b CurrentOccupancy u32, +f MaxOccupancy u32, +13 RoomIndex u32 | "  Room %s (%i occupants)", then the topic and the flags. MaxOccupancy and RoomIndex are not shown |
| `81 bf` | +2 ChatHandle, +3 Status | "Failed to send room message.  Reason: %s." |
| `2e` | ChatType 18, SenderName (+7), RoomName (+9), Text (+b) | "[room] Name: text", in every tab that shows type 8 (the default tab does, 0x62249930). The client does not print its own line |

**Status codes** (`LTOBJSRV_SOECHAT_*`, category 0x0e):

| Code | Name |
|---|---|
| `0x8e000084` | SUCCESS |
| `41` | DESTAVATARDOESNTEXIST |
| `42` | ADDRESSDOESNTEXIST (no such room) |
| `48` | ROOM_DESTNOTINROOM |
| `49` | ROOM_BANNEDAVATAR |
| `4c` | ROOM_NOTINROOM |
| `4d` | ROOM_NOPRIVILEGES |
| `55` | ROOM_ALREADYEXISTS |
| `5b`, `5c`, `5d` | ROOM_DESTAVATARNOTMODERATOR, NOTINVITED, NOTBANNED |
| `5e`, `5f`, `60` | ROOM_DUPLICATEBAN, DUPLICATEMODERATOR, DUPLICATEINVITE |
| `61` | ROOM_ALREADYINROOM |
| `6e` | INVALIDNODENAME |
| `7e` | ROOM_DESTAVATARNOTENTERING |
| `80` | ROOM_WAITINGFORENTRY |

There is no wrong-password code.

The text after "Reason:" is not settled. It comes from a map in the chat manager (+0x100), and nothing in client.dll seems to fill that map, so it is probably empty ("Reason: .").

### What the server does (ours, not retail's)

**Rooms.**
- Rooms live in memory (`ChatRooms`), not the database. A room goes when it is destroyed, or when its last occupant leaves. It also goes when its owner leaves the world if nobody ever entered it.
- Creating a room doesn't enter it. The creator becomes the owner and enters with `/roomenter`.
- Names are 1 to 64 characters. Topics are cut at 255.

**Private rooms** (`/roomcreate room:password`).
- These let in the owner, moderators, invited players and anyone with the password.
- Anyone else is told ROOM_WAITINGFORENTRY and put on the room's waiting list. The owner and moderators get `81 c5`.
- `/roomallow` lets a waiting player in: `81 c6` goes to the moderator, and `81 c3` to the newcomer and the room.

**Who is told.**
- Entering, leaving and destroying go to the player ("You ...") and to everyone in the room ("%s ...").
- Kick, ban, unban, invite, uninvite, voice and moderator changes go to the actor, to the player they were done to, and to the room.
- Leaving the world takes a player out of every room, and the others there get "%s left chat room %s.".

**Who may do what.**
- Moderators and the owner may invite, uninvite, kick, ban, unban, voice and allow.
- Only the owner makes and unmakes moderators, gives the room away and destroys it.
- A ban takes the player out of the room and keeps them out.
- Naming a handle nobody has gives DESTAVATARDOESNTEXIST.

**Room lines.** The speaker must be in the room; otherwise they get ROOM_NOTINROOM in `81 bf`. A line goes to everyone in the room, the speaker included. The client never sets IsModerated, so no room is moderated and voice changes nothing yet.

**`/roomfind`.** The filter is matched inside room names; with only one word, that word is the filter. `81 e2` with the count comes first, then one `81 e3` per room, sorted by name.

### Dev helper

`?socialbot` (`MXO_DEV_COMMANDS=1`) puts SocialBot, a player played by the server, next to you. `?socialbot off` sends it away.

For rooms:
- `?socialbot room <name> [password]` makes it try to enter the room and say "Hello from SocialBot.".
- `?socialbot say <room> <text>` makes it speak.
- `?socialbot leave <room>` makes it leave.

It can be invited, kicked, banned, and made a moderator or the owner, like anyone else.

## Teams, crews and factions in detail

These requests come only from window buttons and right-click menus, each followed by a confirm dialog. The only group slash commands are `/invite` and `/disband`. GroupType is 1 for a faction, 2 for a crew and 3 for a team.

Retail traffic has only `72 03 <own id>` at login, `72 02` to the remaining member after a `71 02`, two `80 92` searches with no answer, and `76`. Everything else below comes from client.dll.

### Records

**Client to server:**

| Record | Layout | What sends it |
|---|---|---|
| `72` (6) | +1 GroupType, +2 PlayerID u32 | Team window Boot (`MTBt`, confirm `MTBC`), or the mini team list (`MTBo`). Crew right-click "Remove" (`CrRm`), offered only to the captain |
| `75` (6) | +1 GroupType, +2 PlayerID u32 | Team Promote (`MTPr`), or crew right-click "Make Captain" (`CNCp`) |
| `80 87` (6) | +2 FirstMateID u32 | Crew right-click "Make First Mate" (`CrFM`), or "Demote" on the first mate (`CrDm`), which sends 0 |
| `80 80` (7) | +2 MemberID u32 (a crew id), +6 bPromote | Faction members window "Promote" (`FMPr`) and "Demote" (`FMDe`). Offered only to a captain whose crew ranks higher than the target |
| `73` (5) | +1 CrewID u32 | Faction members window "Remove" (`FMRm`). Offered only to the captain of a rank-1 crew |
| `80 84` | +2 InviteeName offset, +4 CrewName offset (7), +6 OrganizationID, then the crew name, then the invitee | The crew creation window (`viewcrewcreation.cpp`). It prints "Crew create invitation sent." |
| `80 91` | +2 RemoveFromSeekerList, +3 ExtraDisciplines offset, +5 OrgsToWorkWith offset | Team window Seek (`MTSk`). The client prints "You flag yourself as looking for team." itself |
| `80 92` | +2 MinLevel, +3 MaxLevel, +4 MyFactionOnly, +5 DesiredDisciplines offset (u32 ability ids), +7 OrgsToWorkWith offset (u8) | Find Team Members, or its advanced form |

Arrays are `<u16 count>` followed by the elements, at the offset given.

**Server to client:**

| Record | Layout | The client |
|---|---|---|
| `72` | +1 GroupType, +2 PlayerID | If the id is its own: "You have been booted from your team." (team-worded even for a crew), then it drops the group. For anyone else: "%s has left the mission team. / the crew. / the faction." |
| `75` | +1 GroupType, +2 id | "You are now the leader ..." or "%s is now the leader of your mission team."; for a crew, "... the captain of your crew." |
| `80 87` | +2 FirstMateID | "%s is now the first mate of your crew."; no text for 0 |
| `7f` (6) | +1 MemberID (crew id), +5 NewRank u8 | Sets the crew's rank: "Faction member %s is now Rank %i." |
| `80 81` (11) | +2 MemberID, +6 bPromote, +7 Status | Error popup only: 0x0d00000b "Promotee not found.", 0x0d000010 "Promoter not found...", 0x0d000014 "Promoter is not higher rank.", 0x0d000015 "...illegal rank configuration." |
| `7e` (42) | +1 CrewID, +5 NewCaptainID, +9 name (32 bytes), +0x29 online | Updates the captain of one crew in the faction's crew list |
| `73` / `74` | +1 CrewID / +1 CrewID, +5 Status | `73`: if it is its own crew, the faction is cleared; otherwise "%s has left the faction.". `74`: "Failed to eject faction." |
| `80 85` | +2 CrewID, +6 name offset (0x0d), +8 OrganizationID, +9 Status, then the name | Status 0: the creator is captain of a one-member crew, "Crew created.". Otherwise "Crew could not be created. ...": 0x0d00001e declined, 1f a request in progress, 20 already a member, 21 invalid name |
| `80 93` | +2 Members offset (4), then `<u16 count>` and 63-byte entries: 00, char id u32, handle (32 bytes), level, a flag, best discipline (ability id u32), five u32 | One row per player. A count of 0 shows "No matching players could be found!" |

**Ranks.** A crew has a captain, one first mate and plain members. `80 86` has no per-member rank: it carries the captain and first mate ids in its header. In a faction, each crew has a rank from 1 (leading) to 5, the last byte of each crew in `7c` mode 2. Retail lists show ranks 1 to 4, with up to three crews at rank 1.

### What the server does (ours, not retail's)

**Teams.**
- Only the leader may boot or promote. Anyone else gets `6f 03` with MTNOTTEAMLEADER, as for a disband.
- A boot sends `72 03 <id>` to every member, the booted one included, and MissionTeamID 0 to the booted one.
- A new leader is `75 03 <id>` to every member.

**Crews.**
- Only the captain may remove a member, name a captain or name a first mate.
- A removal sends `72 02 <id>` to every online member, the removed one included, and CrewID and FactionID 0 to the removed one.
- A new captain is `75 02 <id>` to every member and is stored. If the new captain was first mate, the crew has no first mate.
- If the old captain was the faction's master, the new one becomes master. The faction's other crews get `7e`.
- A first mate is `80 87 <id>` to every online member.

**Faction ranks.**
- The founder's crew starts at rank 1 and crews that join start at 5. Crews from before ranks were kept read as 1 for the master's crew and 5 for the rest.
- A captain may move another crew up or down one step if their own crew ranks higher (a smaller number), within 1 to 5. That can make several crews rank 1, as retail had.
- Success is `7f` to every online member of the faction. A refusal is `80 81`, with promoter not higher (0x0d000014) or illegal ranks (0x0d000015).
- The captain of a rank-1 crew may remove another crew. That crew's members lose the faction, and everyone in the faction gets `73`. A refusal is `74` with 0x0d000014.

**Creating a crew from its window (`80 84`).**
- The invitee gets the usual `80 88` invite. The crew is created only when they accept.
- On acceptance the creator gets `80 85` with the new crew id and status 0, then the member list. A decline gives `80 85` with 0x0d00001e.
- The creator must have no crew, the name must be new and 1 to 31 characters, and the invitee must be online and without a crew. Otherwise `80 85` comes back at once with the matching code.
- The organisation the window picked is stored with the crew.

**Looking for team.**
- `80 91` puts the player on the list or takes them off. Leaving the world takes them off too.
- `80 92` answers with the other seekers in the level range, in the player's faction if asked, with a wanted discipline and an organisation from the lists if given, sorted by level.
- A range of 0..0 means any level, and the two ends are swapped if given the wrong way round. Retail's own search sent 64..50.
- Best discipline is the player's TitleAbility. The flag byte is 0.

### Dev helper

SocialBot (`?socialbot`, see "Chat rooms in detail") accepts any team, crew or faction invite a second after getting it.

| Command | What SocialBot does |
|---|---|
| `?socialbot leader` | Hands the team, or a crew it captains, back to you |
| `?socialbot seek` | Puts itself on the looking-for-team list |

A bot has no character row, so its crew membership is kept in memory only. If it is captain when dismissed, or when you leave, the crew goes back to you.

A bot can't captain a crew of its own, so faction ranks and removing a crew need a second player. The headless scenario `faction-ranks-and-removing-a-crew` plays that part.

## Emotes and stances in detail

Retail sent no RPC back for a stance or a plain emote. Everything is in the view updates: the player's own view (view 2), and every other player's view of them.

### The animation block

A view update's state part is `01 <flags>`, with one field per flag, in the order 0x20, 0x01, 0x02, 0x04, 0x08:

| Flag | Field |
|---|---|
| 0x20 | The animation block, below |
| 0x01 | A u16 |
| 0x02 | The action byte |
| 0x04 | The yaw |
| 0x08 | The position, three f32 |

The block is `<counter> <kind> <stance> <25 <action u16> | 00> 00 10`. It is followed by the partner u32 for kinds 01 and 02, and kind 02 then adds four f32: 0, 0, 140, pi. Those four floats are the same in every capture.

| Kind | Meaning | Retail example |
|---|---|---|
| `40` | A plain emote or a stance | /clap `40 00 25 04 00 00 10`; sitting `40 0a 00 00 10` |
| `01` | A paired emote's request (the 'Beckon' action) | `01 00 25 d5 00 00 10 17 00 96 00` |
| `02` | The accepted paired animation | `02 00 25 d6 00 00 10 <partner> <16 bytes>` |

No client.dll reader for the block was found, so these are retail templates.

**Own view and other views.**
- The own view uses `28` (block, position), followed by a u32 sim time in ms.
- Other views use `2a` (block, a `00` action byte, position) and have no time.
- A sit or stand also carries ConditionStateFlags bit 0x00200000. In the own view it goes in the sub-view `03` attribute part as attribute 42 (`80 80 80 80 80 80 01 <u32>`); in other views as attribute 30 (`80 80 80 80 04 <u32>`).

Retail examples:

| Capture | Bytes |
|---|---|
| WhiteHallwaysWarboyTeleportDoors | `04 00 03 2a 0b 40 0a 00 00 10 00 <pos> 80 80 80 80 04 00 00 20 00` |
| entilsar, own view | `02 00 03 28 12 40 0a 00 00 10 <pos> <time> 80 80 80 80 80 80 01 00 00 28 01` |

**The counter** is one per character and the same in every view. The client echoes it back as the first field of its own update. In retail `01 28 08 <pos>` followed the server's `01 28 08 40 13 ...`, and `22 01 98`, `22 02 98`, `22 03 74` count up.

That fixes the field order in client updates, which the server had wrong (action first): it is the counter, then the action, then the yaw. The echo is no longer forwarded to other players, whose clients would read it as the start of an animation block.

### Paired emotes

The 17 interactive emotes are in the client's Python `emotes` module: `addInteractiveEmote`, in `research/client-scripts/emotes.py`.

Each one gets three action ids from `createActionID` in turn: request, accepted, refused. /shakehands is 189/190/191, and the list runs up to /bangbang at 237/238/239. /manhug is 213/214, which matches retail's `d5`/`d6`.

The ids are per viewer: `<u16 view as that client sees the player> <spawn id> 00`, and a client's own character is view 2.

| Record | Layout | Notes |
|---|---|---|
| `30` (C, 9) | EmoteID u32, TargetID u32 | Every emote. Before sending, the client checks the player is standing and has a target |
| `31` (S, 9) | EmoteID, RequesterID | Handler 0x62252c50. The client must already see the requester. It answers by itself with Result 7 if it ignores them, 4 if a request is already pending, and 2 under its auto-decline check. Otherwise it shows the yes/no dialog "Interactive Emote" |
| `32` (both, 15) | Result u8, EmoteID, Partner, WaitTime f32, IsRequester u8 | The target's client sends Result 1 (accept) or 2 (refuse), with WaitTime 0 and IsRequester 0. The requester's client prints only when IsRequester is 1 |

`32` Result values, printed for the requester:

| Result | Message |
|---|---|
| 1 | "%s agrees to a hug." |
| 0, 2 | The refusal text |
| 3 | "You cannot reach %s..." |
| 4 | "%s cannot perform an interactive emote with you." |
| 5 | "...not a player" |
| 6 | "You must wait..." |
| 7 | "%s is ignoring you" |
| 8 | "...with an opponent." |

**Retail, both sides of a /manhug (two-person capture):**
1. The requester sends `30 f5 0d 00 58 17 00 96 00`.
2. Its own view gets the request animation. The target gets `31 f5 0d 00 58 0f 00 51 00` and sees the requester beckon.
3. The target sends `32 01 ... 00`.
4. Seven seconds after the request, the requester gets `32 01 f5 0d 00 58 17 00 96 00 00 00 00 00 01` and the accepted animation, in its own view and the target's.
5. The target never gets a `32`, and its own view gets nothing.

**What the server does.**

The rules are retail's flow; the checks are ours:
- A plain emote now plays in other players' views too, not only the player's own.
- A paired request is refused at once with `32`, IsRequester 1, in these cases:
  - Result 5 when the target is no player.
  - Result 3 when the target is more than 1500 units away (the client's limit is 1200).
  - Result 4 when the target isn't standing, or either player already has a request open.
- On acceptance, only the requester's view animates, as retail.
- A refusal or a client-side answer (4, 7) is passed to the requester.
- With no answer in 20 s (the client's EmoteQueryDurationMS), the requester gets Result 0, which prints the refusal text. That timeout is ours: the client never sends one.
- No refused animation is played; no capture has a refusal.

### Stances

`docs/CLIENT-ACTIONS.md` used to have these the wrong way round.

**`34 <stance>` enters a stance.**
- /sit sends `34 0a`.
- /dance and /dance1–5 send `34 11`–`34 15`.

**`33 <current stance>` leaves one.** The client sends it for /stand, for /sit while sitting, and once when the player moves out of a locked stance. It keeps resending `33` until it sees stance 0: in the shutdown capture (ProxyM2_m2_packets6) the server imposed stance `19` and never released it, and the client sent `33 19` 1,906 times.

**Other values.**
- Stance 1 is a stance-site object such as a bench, entered by using it (`80 c8`).
- Free-moving stances are 0, 9, 0x0b, 0x0c and 0x1a (0x6266b7e0).

**The server.**
- `34` stores the stance and sends it to the own view and everyone else's. Sitting sets ConditionStateFlags 0x00200000 and sends it with the stance.
- `33` sends stance 0 and clears the bit.
- Not done: a player who comes into view later sees the sitter standing, because the view creation doesn't carry the stance; and the stance-site objects.

### Dev helper

SocialBot (`?socialbot`) accepts any paired emote a second after it is asked.

| Command | What SocialBot does |
|---|---|
| `?socialbot hug [emote id hex]` | Asks you for a paired emote (a hug by default) |
| `?socialbot sit`, `?socialbot dance`, `?socialbot stand` | Takes that stance, so you see another player's stance |

## Item requests in detail

None of `5b`, `5c`, `60`, `61`, `66` or `80 c6` appears in the 339 captures, so all of this comes from client.dll.

**`66 <Slot u8> <TargetID u32>`: giving a mission item by dragging it.**
- The trigger is a bag item dropped on a world object (drop handler destination 0). That goes to the trade sender 0x621eb790, which takes this path (0x621eb080) instead of `81 82` when the target lists interface 0x31.
- Before sending, the client checks two things. The target must also have interface 0x25 and the component 0x1000035. The bag entry's second dword must have bit 31 set and its low bits must equal the NPC's key. That dword is the descriptor `BuildMissionItemAdd` writes.
- If the checks pass, the client prints "You hand over the %s." and sends `66`, leaving the slot alone. If they fail, it prints "The NPC will not accept your trade request." and sends nothing.
- TargetID is `<view u16> <spawn id> 00`, as for trades.

**What the server does with `66`.** It runs the NPC menu's Give (`80 c7 ... 0e`) for that NPC: the mission's Give goal takes the item (`5f`) and completes (`80 a0`). For the menu's Give, retail sent `5f 07 00 01`, `81 22`, then the goal.

If no Give goal wants the item from that NPC, the server says "They don't want that." That text is ours: the client has already said it handed the item over, and the item is still in the bag.

**`60`: compile.**
- Layout: `60 <ObjectID u32> <Slot u16> <PersonalInventoryItem u8> <InvSlotOfToolUsed u8> <FromCrafting u8>`, 10 bytes.
- It comes from the Compile window's button, which an `80 bc` for ability 969 opens, and from the Loading Area storage window's compile. PersonalInventoryItem 0 means the slot is a code storage slot.
- The answer is `61 <ResultCode u8>`. The client acts on it only while the Compile window is open:

| ResultCode | The client shows |
|---|---|
| 0 | placed in the inventory |
| 1 | failed |
| 2 | ends the session |
| 3 | inventory full |
| 4 | not enough information |
| 5 | singleton |

- This is code compiling, Codex's area, so it is left alone here.

**`5c <Slot u16> <PersonalInventoryItem u8>`: dropping a luggable into the world.**
- It is sent when a dragged bag item is released over the world.
- The client refuses anything that isn't luggable before sending: "You can only drop luggable objects in the world."
- Offline, the client spawns the object itself. Online, it waits for the server. Shared light-luggable pickup/drop and persistence now execute; see [DATA-MINING.md](DATA-MINING.md). Heavy carrying remains absent.

**`5b <ItemHandle>`: picking up an item.** It is never sent. **`80 c6 <PillInvSlot u16>`** is sent only when the item check returns `0x95000014` (consumable grants an ability). Pills work through `80 b9`.

## Prompts and FX tokens

The client hands two things back unchanged:
- The Message of a two-button prompt. The server sends `81 8f <image> <string id> 1c 00 <Message> <left> <right> ...`, and the client answers `81 90 <Message> <LeftChosen>`.
- The token of an FX. The server sends `81 42 <fx> <token> <user data>`, and when the FX ends the client sends `81 91 <token>`.

Only the tutorial used either before. `ClientCallbacks` (ours) lets any feature attach a callback: `ShowModal(client, image, string, left, right, onAnswer)` and `PlayFx(client, fx, onEnd)`.

Their Messages and tokens start at 0x10000, above the tutorial's script numbers. An answer or token nobody registered goes on to the tutorial, as before. A callback runs once. Each player keeps the newest 32 callbacks of each kind, because a prompt can be closed without an answer.

Dev helpers (`MXO_DEV_COMMANDS=1`):
- `?ask [string id hex]` opens a Yes/No prompt and says "You answered Yes." or "You answered No.".
- `?fxdone <fx id hex>` plays an FX and says "The FX ended." when the client reports it.

## Check in the real client

Start the server with `MXO_DEV_COMMANDS=1`. One client is enough.

### Email: reading and the notice

1. In the world, type `?mailbot Hello there`.
2. Expect in chat a line naming MailBot as the sender, a sound, and the phone's new-mail indicator.
3. Phone > Email. Expect the inbox to list "Hello there" from MailBot. The server does not send `81 ec`, so "Mail headers retrieved successfully." should not appear (retail did not send it either).
4. Open the message. Expect the body text and one attachment, a Code Bit 1.
5. Take the attachment. Expect the Code Bit in the bag, "Attachments retrieved successfully.", and the attachment gone when the message is opened again.
6. Close Email, open it again: the message is no longer marked new.

### Email: sending, with an attachment

1. Note your information. Put an item in the bag.
2. Phone > Email > Compose. To: `MailBot`. Subject `Ping`, some body text. Attach the item and 100 information.
3. Send. Expect "Mail sent successfully.", the item gone from the bag and the information down by 100.
4. Expect a new-mail notice at once: "RE: Ping" from MailBot. Open it: your text quoted and your item attached. Take it back. The information comes back with the reply.
5. Compose to a name nobody has. Expect "Failed to send mail. Reason: ..." and a "Message Undeliverable" mail from System.
6. If a second account is at hand: send to that character while it is offline, log it in, open Email: the message is there with status new.

### Email: deleting

1. In the inbox, select a message and press Delete. Expect it shown as deleted (Undelete offered). Press Undelete, then Delete again.
2. Close Email and open it again. Expect the deleted message gone.

### Ignore list

1. `/ignore <the handle of any character on the server>`. Expect "Ignoring <name>." and the name in the buddy list's ignore tab.
2. `/ignore Nobodyhere`. Expect "Failed to ignore Nobodyhere." (or the client's own message for it).
3. Log out and back in. Expect the name still in the ignore tab, with no "Ignoring" line at login (if the line does show, the login status code is wrong: see "Ignore list in detail").
4. Remove the name from the ignore tab. Expect it gone, and gone after another relog.

### World entry

1. Log in and look at the server log: `81 48`, `0c` no longer appear in UnknownRPC.txt (`80 be` and `3e` still do).
2. Nothing should change on screen; if anything does after `81 49` (a feature switched on or off), note it here: what the two features (200, 201) do is not known.

### Subway

1. In Richland, go to the Red Line platform at Tabor Park (its connector is at 68400, -800, 47500, underground; `?sector` shows where you are) and use the subway car there.
2. Expect the subway map with the Red Line, Tabor Park marked as "you are here", and a departure countdown starting at 1:00 and going down each second.
3. Pick Moriah Projects. Expect the map to close, a loading screen, and arrival on the Moriah Projects platform, standing on the floor (not in it or above it), facing anywhere.
4. Use the car there, and let the countdown run out. Expect "The subway is now leaving the station... sorry, but you missed it!" and the map closing.
5. Use the car again and press Cancel. Expect the map to close and nothing else.
6. From a Yellow Line station (Richland - Kedemoth, or `?subway` near 56600, -705, -91500), click "Downtown" on the map. Expect the map to switch to Downtown's Yellow Line. Pick South Vauxton. Expect the Downtown load and arrival on its platform.
7. Anywhere in a city, `?subway` opens the nearest station's map; a ride from it works as above.
8. Note anything the map shows that the server leaves empty (a level range, a count, "ON: ..."), and whether the countdown or the arrival looks wrong.

### Stacks

A split starts from the item's right-click menu, not a modifier key. client.dll's bag view (0x6203f5a0) answers a right click (`MRCl`) on a bag item with a pop-up menu. That menu has "Split Quantity..." (string 0xb2000034) only when the client's own check 0x621cd790 passes: the slot is in the bag (below 0x60), the item's class has IsStackable, and the slot holds at least 2. Choosing it (`CISQ`) opens "Choose Amount" (Total, Amount; 0x6203d270). OK (`CICQ`) puts that many on the cursor as a split drag (drag type 4). Dropping it on a slot, or on empty bag space, sends `68`. A plain left drag, with or without Shift, is a move (`65`). The path needs no modifier key: a right click, a menu choice, typing the amount and two left clicks. Whether the client reads the right button through window messages (rather than polling it) was not checked.

1. Get a stack of a few Code Bit 1 into the bag (`?additem 7332` several times; each one joins the stack).
2. Right-click the stack. Expect a pop-up menu with "Split Quantity...". If the menu has no such entry, the client doesn't think the slot holds two or more: note the count shown on the icon.
3. Choose "Split Quantity...". Expect "Choose Amount" with the stack's total. Enter 2 and press OK. Expect 2 Code Bits on the cursor.
4. Click an empty bag slot. Expect a new stack of 2 there and the source 2 fewer. The server log shows `[INVENTORY] Split 2 of slot ... to slot ...`. Relog: both stacks keep their counts.
5. Split again, and this time click empty bag space instead of a slot. Expect the new stack in the first free slot.
6. Merge: drag one of the two stacks (plain left drag) and drop it on the other stack's slot. That is the only way to get `67`: the drop handler (0x620a0504) sends it when the destination slot holds the same item. Expect one stack with the total and the other slot empty; the log shows `[INVENTORY] Merged slot ... into ...`.
7. Drag a stack onto a different item. Expect the client's usual swap (`65`), not a merge.
8. Note anything the client shows that the server didn't confirm, such as a count that jumps back or a stack that reappears.

### Chat rooms

The client doesn't register the room commands, so there is nothing to click through.

1. `/roomcreate Lounge`. Expect "Unrecognized command.  Please try again." and no `81 c0` in the server log. Anything else means this client build does register them: note it.
2. The server side (create, enter, kick, ban, private rooms, find, room lines) is covered by the headless scenarios `chat-rooms-create-talk-moderate-and-find` and `socialbot-in-a-chat-room`.

### Teams

1. `?socialbot`, then `/invite SocialBot`. Expect SocialBot to join your mission team a second later and appear in the team window.
2. In the team window, click SocialBot, then Promote, then Yes in the confirm dialog. Expect "SocialBot is now the leader of your mission team.".
3. `?socialbot leader`. Expect "You are now the leader ..." (the client's own wording).
4. Click SocialBot, then Boot, then Yes. Expect "SocialBot has left the mission team." and SocialBot gone from the team window.
5. Looking for team:
   1. `?socialbot seek`. Expect "SocialBot is looking for a team.".
   2. Open Find Team Members and search. Expect a row for SocialBot, at your level, with your discipline.
   3. Click Seek in the team window. Expect "You flag yourself as looking for team.". Click it again: "You are no longer flagged ...".
6. Note whether the boot and promote buttons need anything but left clicks, and anything the windows show that the server left empty.

### Crews

The crew window's Remove, Make Captain and Make First Mate are right-click menu entries. The headless scenario `crew-create-first-mate-captain-and-remove` covers them.

1. With no crew, open the crew creation window. Type the name `Botcrew` and the invitee `SocialBot`, then create. Expect "Crew create invitation sent.". A second later expect "Crew created." and a crew window listing you as captain and SocialBot.
2. If right-click menus work: right-click SocialBot and choose Make First Mate. Expect "SocialBot is now the first mate of your crew." and " - FM" after its name.
3. Make Captain on SocialBot. Expect "SocialBot is now the captain of your crew.". Then `?socialbot leader`: "You are now the captain ...".
4. Remove on SocialBot. Expect "SocialBot has left the crew.".
5. `?socialbot off` while SocialBot is captain. Expect the crew to be yours after a relog.

### Emotes and stances

1. `/sit`. Expect your character to sit. `/stand`: it stands. `/sit`, then walk forward (W): it stands up and walks, and the server log shows `[EMOTE] ... stance 00` once, not a stream of them.
2. `/dance`. Expect a dance; walking stops it.
3. `?socialbot`, then `?socialbot sit`. Expect SocialBot to sit where it stands. `?socialbot dance`: it dances. `?socialbot stand`: it stands.
4. Click SocialBot to target it, then `/hug`. Expect your character to beckon, and a second later "SocialBot agrees to a hug." with the hug playing between you.
5. `?socialbot hug`. Expect the "Interactive Emote" dialog asking whether you accept a hug from SocialBot. Click Yes: SocialBot plays the hug with you. Do it again and click No: nothing plays.
6. `/clap` with SocialBot nearby. Your own clap should look as before.
7. `/hug` with no target: the client's own message. `/hug` on an NPC: "...not a player".
8. Note whether the hug lines the two characters up, or plays with them apart. The four floats after the partner (0, 0, 140, pi) are copied from retail, and their meaning (offset and facing) is a guess.

### Giving a mission item by dragging it

1. Take a mission with a Give goal, for example Tyndall's "Computer Crackdown" (the data disk to Alcor), and play it until the disk is in your bag.
2. With Alcor in view, left-drag the disk from the bag onto Alcor and release.
3. Expect "You hand over the Data CD." (the client's own line), the disk gone from the bag, and the goal "Give the data disk to Alcor" done.
4. Drag any other bag item onto an NPC. Expect "The NPC will not accept your trade request." from the client, and nothing from the server.
5. If the hand-over line appears but the goal doesn't complete, note it, and note whether "They don't want that." followed.

### Prompts and FX callbacks

1. `?ask`. Expect a Yes/No prompt ("Spawn another opponent for practice?"). Click Yes: "You answered Yes.". `?ask` again and click No: "You answered No.".
2. `?fxdone 1`. Expect an effect on your character, then "The FX ended." when it finishes. If no line appears, try another FX id from `data/fxlistreal.txt` and note which ones report their end.
3. Play the tutorial's pill choice and its practice prompt: they must still work (they use their own Messages).
