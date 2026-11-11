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
| Handled | 48 | 26 | 10 | 84 |
| Partial | 0 | 2 | 0 | 2 |
| Ignored | 2 | 27 | 48 | 77 |
| Wrong | 0 | 0 | 1 | 1 |
| **Total** | 50 | 55 | 59 | 164 |

Counts are rows of the tables below (`81 47` and `81 4a` share a row). Before this work 15 of the handled rows were ignored: email (11 rows, including the long-string records `81 ab`/`81 ac`), the ignore list (2), and the world-entry `81 48` and `0c`.

The biggest gaps left, by impact:

1. **Subway travel** (`81 3b`, `81 3c`, `81 3d`). The server opens the subway map, but choosing a station does nothing. No capture of a ride exists.
2. **Animation stances** (`33`, `34`, sent 302 and 21 times in the captures): probably sitting, leaning and the like. Retail answered with nothing over RPC, so the effect, if any, is in other players' views (protocol 03), not decoded.
3. **Stack splitting and merging** (`68`, `67`, `62`): the bag's split dialog and dragging stacks together.
4. **Chat rooms** (`81 c2` to `81 e1`): `/roomcreate`, `/roomenter`, the channel list.
5. **Paired emotes** (`32`, the answer to a two-person emote such as `/hug`).
6. **Combat style at world entry** (`3e`, U 232): Codex's area (combat), not touched here.

## Login and world entry

| Request | Name / purpose | Evidence | Server today | Impact |
|---|---|---|---|---|
| `05` | Spawn ready: the client is ready for its own view | C 274 | handled | high |
| `0c 01` | LoadingComplete, sent from `OnEnterWorld` with `80 be`, `3e` and `81 48` | D, C 181, U 212 | handled: retail answered nothing, now so does the server | low |
| `80 be` | No fields. Sent at world entry by the inventory/ability manager (0x629ea4e8, 0x621e2cf0) after it re-sends its slots. Retail answered nothing directly; the `80 bc` effect list followed in 102 of 181 cases, which is also sent at every world entry anyway. Guess: asks for the active effects | D, C 181, U 212 | ignored | low (guess) |
| `3e` | Slot, Ability, IsSlot, IgnoreStyleUpdate. At world entry `6b 00 10 00 00 00 00` (147 times), `6b 85 00 ..` (37); also sent when picking a special move (caller has `CombatSpecialMove`). Guess: the combat style / special move in use | D, C 187, U 232 | ignored; combat is Codex's area | medium |
| `81 48` | No fields, from `viewplayer.cpp`. Retail answered `81 49` (FeatureArray, CharacterID, SubscriptionFeatures), mid-session too (luizoe_margins_Syntax packets 1296/1298) | D, C 184, U 237 | handled: retail's `81 49` bytes | low |
| `81 08` | Ready for the world change (Status) | C 209 | handled | high |
| `80 c9` | Region loaded (ObjID, ObjPos) | C 10438 | handled | high |
| `81 00` | No fields, from `clientshell.cpp` near `tutorial.flg`. Retail answered `80 b3` and `3a` | D, C 8, U 3 | ignored | low (purpose unknown) |
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
| `81 3b` | No fields, from `viewsubwaymap.cpp` (map open, cancel or back; not determined which) | D | ignored | high |
| `81 3c` | WorldID, NetworkID, ConnectorID: the station picked on the subway map | D | ignored: the map opens (`81 3e`) but nothing happens on a pick | high |
| `81 3d` | No fields, subway map (close) | D | ignored | medium |
| `39` | Direction (`02` 7 times) | D, C 8 | ignored | low |
| `81 46` | Range, Range2, from the slash command handler. Guess: `/range` | D | ignored | low (guess) |
| `81 37` | TimeRequested, from `viewcommon.cpp`. Guess: the clock / `/time` | D | ignored | low (guess) |

## Chat, emotes and presence

| Request | Name / purpose | Evidence | Server today | Impact |
|---|---|---|---|---|
| `28` | Chat line (ChatType, bShout, Text, RoomName, ChatHandle) | D, C 1052 | handled | high |
| `29` | Tell (TargetName, Text, bEchoOnly, ChatHandle) | D, C 81 | handled | high |
| `30` | Emote (EmoteID, TargetID) | D, C 24 | handled | medium |
| `32` | Answer to a two-person emote request (Result, EmoteID, Partner, WaitTime, IsRequester); the server's `31` asks | D, C 8 | ignored | medium |
| `33` | Animation stance start (Stance: `19` 249 times, `0a` 33). Guess: sitting, leaning and the like. Retail sent no RPC back | D, C 302 | ignored | medium |
| `34` | Animation stance stop (Stance) | D, C 21 | ignored | medium |
| `35` | Mood (Demeanor) | D, C 8 | handled | medium |
| `2a` | `/afk` (Afk u8; `01` once) | D, S, C 1, U 1 | ignored | low |
| `3b` | No fields; two senders, one in the slash command handler (0x62256760). Purpose not determined | D | ignored | low |
| `81 55` | No fields, from the slash command handler. Purpose not determined | D | ignored | low |
| `81 47`, `81 4a` | No fields; no sender found | D | ignored | low |

## Chat rooms (custom channels)

All from the slash commands `/roomcreate` .. `/roomtopic` (strings 0x02000160..0x02000175). The server has no rooms; none are in the captures. The pairing of request to command is read from the field names; which of `81 c7` and `81 c9` is leave and which destroy is a guess.

| Request | Name / purpose | Evidence | Server today | Impact |
|---|---|---|---|---|
| `81 c2` | Enter a room (RoomName, Password) | D, S | ignored | medium |
| `81 c7` | RoomName: leave (guess) | D, S | ignored | medium |
| `81 c9` | RoomName: create or destroy (guess) | D, S | ignored | medium |
| `81 cd` | Invite (PlayerNameToInvite, RoomName) | D, S | ignored | low |
| `81 cf` | Uninvite | D, S | ignored | low |
| `81 d1` | Kick | D, S | ignored | low |
| `81 d3` | Ban | D, S | ignored | low |
| `81 d5` | Unban | D, S | ignored | low |
| `81 d7` | Make moderator (IsTemporaryModerator) | D, S | ignored | low |
| `81 d9` | Remove moderator | D, S | ignored | low |
| `81 db` | Set owner | D, S | ignored | low |
| `81 dd` | Give voice | D, S | ignored | low |
| `81 df` | Remove voice | D, S | ignored | low |
| `81 e1` | Find rooms (StartingNodeName, SearchFilter): `/rooms`, `/roomfind` | D, S | ignored | medium |

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
| `72` | GroupType, PlayerID. Guess: remove a member | D | ignored | medium |
| `75` | GroupType, PlayerID. Guess: make leader | D | ignored | medium |
| `73` | CrewID. Guess: leave or request a crew's info | D | ignored | low |
| `80 80` | Promote / demote a crew member (MemberID, bPromote) | D | ignored | medium |
| `80 87` | Set the first mate (FirstMateID) | D | ignored | low |
| `80 84` | Invite a crew into a faction (InviteeName, CrewName, OrganizationID) | D | ignored | medium |
| `80 89` | Hovercraft action (submessage; the server's `80 8a`/`80 8b` carry TransactionCode, HovercraftSlot) | D | ignored | low |
| `80 91` | Join / leave the team seekers list (RemoveFromSeekerList, ExtraDisciplines, OrgsToWorkWith) | D | ignored | medium |
| `80 92` | Search the seekers list (MinLevel, MaxLevel, MyFactionOnly, DesiredDisciplines, OrgsToWorkWith) | D, C 2 | ignored | medium |
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
| `81 91` | An FX token coming back (EventID) | D, C 12 | partial: the tutorial only | medium |
| `81 90` | Answer to a modal (Message, LeftChosen) | D, C 8 | partial: the tutorial only | medium |
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
| `68` | Split a stack (SourceSlot, DestSlot, AmountToSplitOff, IsCentralStorageItem) | D | ignored | medium |
| `67` | Slot1, Slot2, IsCentralStorageItem. Guess: merge stacks | D | ignored | medium |
| `62` | SourceSlot, DestinationSlot, PersonalInventoryItem. Guess: to or from central storage | D | ignored | medium |
| `5b` | ItemHandle | D | ignored | low (purpose unknown) |
| `5c` | Slot, PersonalInventoryItem; two senders, one in the item menu | D | ignored | medium (purpose unknown) |
| `60` | Use an item on an object (ObjectID, Slot, PersonalInventoryItem, InvSlotOfToolUsed, FromCrafting) | D | ignored | medium |
| `66` | Slot, TargetID: the give-to-NPC path SECURE-TRADE.md found (0x621eb080) | D | ignored | medium |
| `80 c6` | PillInvSlot: use a pill from the bag (callers in the inventory and hotbar menus) | D | ignored (pills work through `80 b9`) | low |
| `81 0e` | Vendor buy (ItemTypeID, BuyBackLastItemSold) | D, C 40 | handled | high |
| `81 11` | Vendor sell (Appraise, Slot) | D, C 5 | handled | high |
| `81 10` | StringID, CloseUI, sent as `00 00 00 00 01`. Guess: closing the vendor window | C 9 | ignored | low |
| `81 16` | Collector exchange (ItemSetIndex, Prizes) | D | handled | medium |
| `81 17` | `00 00 00 00 00`. Guess: closing the collector | C 1 | ignored | low |
| `81 1f` | Loot: take all | D, C 39 | handled | high |
| `81 1b` | Loot one item (ListID, ObjectID, InstanceData); the server's `81 1c` answers | D | ignored | medium |
| `81 1d` | No fields; the server's `81 1e` (InfoAmount) answers. Guess: take the information | D | ignored | medium |
| `81 1a` | No fields, `viewcontainer.cpp`: closing the loot window. Retail answered nothing | D, C 40, U 33 | ignored; nothing to do | low |
| `81 21` | Slot, from item menus (the server's `81 22`: ObjectID, InstanceData, Status) | D | wrong: treated as "open the marketplace" and answered with `81 2a` | low |
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
| `80 bb` | AbilityId (`28 04`, `0a 00`, `0c 00`). Guess: cancel an effect on oneself | D, C 7 | ignored | medium |
| `40` | Close combat (TargetID, ForcedInterlock, WeaponToUseSlot) | D, C 75 | handled | high |
| `41` | Ranged attack (TargetID) | D, C 21 | handled | high |
| `42` | Tactic (TacticalValue, TacticalTotalValue, TacticType) | D, C 98 | handled | high |
| `43` | Martial art (MartialArt) | D | ignored | medium |
| `44` | Withdraw (WantToWithdraw) | D, C 18 | handled | high |
| `3d` | Draw / stand down (IsAutoAttacking) | D, C 39 | handled | high |
| `3f` | Free Fire weapon | D, C 206 | handled | high |
| `50` | `/duel` | D, C 6 | handled | medium |
| `52` | Duel answer | D, C 15 | handled | medium |
| `81 9e` | `/pvp` | D | handled | medium |
| `81 51` | Target | D, C 972 | handled | high |
| `81 2e` | Compile code (SourceCode, CodeFragmentInfoArray, InvSlotForToolBeingUsed) | D | ignored | medium |
| `81 31` | Craft from a recipe (AbilityID, SlotID, InvSlotOfToolUsed, IsRecipeKnown) | D | ignored | medium |
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
| `81 7a` | Simulacrum order (ID, Command): `/ordersimulacra` | D, S | ignored | low |
| `81 7b` | Simulacrum order with a target (ID, Command, TargetObject) | D | ignored | low |
| `81 7c` | Simulacrum order with a value (ID, Command, Value) | D | ignored | low |

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
