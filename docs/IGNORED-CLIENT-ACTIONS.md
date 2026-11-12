# Remaining client actions (P7)

This is an audit of **every row marked ignored on master `10e2cdf`**, rather than a claim that every request has retail parity. Native QA is pending. Client files and captures were inspected read-only. The server's new support service, knowledge articles, compilation policy and GM text reports are explicitly local policies.

## Implemented behavior and authority

* `2a <Afk u8>` updates AFK in the own view (47), existing observer views (35), and subsequent creation records. Tells to an away player still deliver and tell the sender the recipient is away. AFK is session presence, not a database setting. `62240b40` allocates the two-byte request; Object12 maps its attributes.
* `81 4d <ElapsedTime u32>` returns that same unsigned field. Retail echoed it; `62143a6c` allocates six bytes. It is not repurposed as a server timestamp.
* `80 c6 <bag slot u16>` resolves the authenticated player's slot to the existing supported consumable and calls the same inventory effect/timer/removal path as `80 b9`. Dead, duel-defeated and incapacitated players cannot use this alternate sender. Unsupported consumables remain unsupported. The four-byte constructor is `621d35c0`.
* `81 10 <StringID u32> <CloseUI u8>` invalidates the vendor session only for an exact five-byte body with CloseUI=1. CloseUI=0 and malformed shapes do not close it. Opening a vendor records actual data-loaded stock and district; buying requires a current session and a listed item; selling requires a current session. Unknown IDs no longer open an unrelated fallback vendor, and district is part of lookup. World-generation/district changes invalidate the session. Vendor proximity is still the existing interaction policy; this does not claim a reconstructed retail trading radius. Constructor `6210a180` allocates seven bytes.
* `81 17 00000000 00` clears the active collector. `81 41 <CurrentFloor u16>` clears the elevator selection; it neither teleports nor guesses a destination from the floor field. `81 1a` closes the selected loot window without discarding the corpse's remaining loot. Their native sizes are 7 (`62071840`), 4 (`620ae350`), and 2 bytes respectively. A replayed exchange/floor/loot operation must acquire a new selection.
* `60 <ObjectID u32> <archive slot u16> <PersonalInventoryItem u8> <tool slot u8> <FromCrafting u8>` supports materializing an owned uploaded Code Bit or fragment into the bag. `6208375a..62083782` proves that ObjectID is the **decoded goid**, not the packed archive value; the complete record is ten bytes. PersonalInventoryItem=1 is refused. LA requests must actually be in LA; FromCrafting requests must be in-world, alive and able to act, out of combat, with loaded Compile Activate (969, including its parent grant) and Code Compiler 1271 in the supplied tool slot. The archive row must match both slot and goid. One SQL transaction checks current cash/capacity and commits archive charge, bag stack/item and information cost. The answer is `61` result 0 success, 1 failed, 3 full or 4 insufficient information. **Local policy:** a stored fragment materializes deterministically, consumes one archive charge, and uses the existing script's `.12 * VendorPrice` compile cost (plus the existing discount outside LA). Equipment-code and ability-code copying, success odds and the rest of the retail archive economy remain unimplemented. This is distinct from `81 2e` recipe assembly and does not duplicate P8's last-charge notification fix.

## Local petitions and knowledge base

`81 b0` creates, edits or cancels a ticket; `81 b2` lists the owner's most recent 100 tickets; `81 b4` reads an owned ticket. `local_support_tickets` is an InnoDB/utf8mb4 table. The authenticated character owns every operation: request TicketID alone conveys no authority. New IDs come from SQL, cancellation persists, cancelled tickets cannot be edited, parameters bind all text, and at most ten tickets can be open. The process serializes quota/update work. Nonchunked UTF-8 text is limited to 3500 bytes; malformed UTF-8, offsets, booleans, trailing data and chunked submissions are refused with an acknowledgement and explanation. No external service, email or chat recipient is contacted. Administrators review this table locally; automatic replies, assignment, staff comments and a staffed service are not implemented.

`81 b7` supplies one local category and subcategory. `81 b9` searches three bundled local articles, `81 bb` retrieves one by its opaque local ID, and `81 bd` supplies the local-support policy. These are our content, not a copy of the retail knowledge service. There is no browser fetch or external URL execution.

Read-only evidence slices and the DLL hash reproduce with `python research/client-actions/extract_evidence.py <client.dll> research/client-actions/evidence.json`; the script translates PE virtual addresses to file offsets and never loads or executes the client.

The response layouts come from constructors/consumers, not guessed text packets:

| Response | Native evidence | Layout (raw offsets) |
|---|---|---|
| `81 b1` | `62147e00`, `621b13c0` | result u32 at2 (zero success), TicketID at6, Cancel at10 |
| `81 b3` | `62147f40`, `62147790`, `62164430`, `621b2db0` | embedded record at2; id at3, creation at7, modified at11, status at15, category at19, subcategory at23, character string pointer at27 relative to raw2, More at29; id0 clears cache |
| `81 b5` | `62148100`, `621b2ea0` | TicketID at2, result at6, body pointer at10, IsChunked at12, comment count at13 |
| `81 b8` | `6212e4d0`, `621b20a0` | embedded 12-byte `81 ad` category at2; subcategory array pointer at14, More at16; strings in each embedded record use that record's base |
| `81 ba` | `62148540`, `621b1d80` | result at2, title pointer6, ID pointer8, match percent u16 at10, More at12; empty title terminates an empty result |
| `81 bc` | `621486f0`, `621b22f0` | result at2, title pointer6, body pointer8, IsChunked at10 |
| `81 be` | `62148880`, `621b0d00` | result at2, body pointer6, IsChunked at8 |

The ticket status switch at `6209699a`/`62096bf8` maps 0 to string `0200017b` (New), 1 to Unused, 2 to Player, 3 to CSR, 4 to Pending Closed and 5 to `02000180` (Closed). Local cancellation uses **5**, not the tempting but incorrect value 1. Category/subcategory IDs 1/2 are local assignments.

## Restricted GM inspection

`81 57` inspects a current player view using the full view/spawn handle and three boolean switches. `81 59` and `81 5b` query a crew/faction by name. Their SnoopManager field is **u8**, followed by a name pointer u16: the native constructors `621a5122` and `621b7d02` allocate five bytes, not eight. They never treat the client manager field as a privilege.

All three require the authenticated character ID to be an exact member of the server environment allowlist `MXO_GM_CHARACTER_IDS` (comma-separated numeric IDs). Default is nobody. A handle, character name, supplied faction ID, substring match, or dev-command enablement cannot grant GM privileges. Reports contain actual current loaded abilities, bag slots/counts, active buff IDs, stored crew/faction bank/leader/member information, sent privately as local system text. The native structured chunk viewers (`81 5a`/`81 5c`) are not reconstructed: these rows remain **partial**, not full native snoop parity.

## Every remaining ignored row

The following rows remain ignored; adding a switch that accepts and does nothing would not improve parity. P3 PR113 is now integrated: `5c` is partial for light luggables with pickup/drop persistence; heavy attachments remain absent. Actual combined counts are 118 handled / 14 partial / 15 ignored / 19 not sent (166 rows).

| Request | Evidence and remaining reason |
|---|---|
| `80 be` | Inventory/ability manager world-entry sender `621e2cf0`, no fields. Existing captures show no direct reply; following `80 bc` is correlated with normal world-entry effects. An active-effects query is still a guess, so no unsolicited effect replay is invented. |
| `81 01` | Eight captures show `80 fa` and world change, but the native producer and destination-selection/authorization rules are not established. Does not authorize arbitrary LA exit destinations. |
| `81 56` | Thirteen view-manager send sites, no fields. Purpose and response contract remain unknown. |
| `39` | Direction byte; eight captures, seven with value2. Which operation is directed remains unknown; it is not treated as movement/yaw. |
| `81 46` | `62241a50` allocates10 bytes; `6225909d..622590c5` parses two integer words and sends them. The name `/range`, units, which ranges change and authority are not established. No guessed interest-radius or weapon-range control. |
| `81 37` | `62078cd0`/`62078d03` writes the imported tick count into TimeRequested. `62079687` invokes it through a common UI timer; it is not proof of a `/time` slash request. Dispatcher137 goes to `6217e9b0` (no inbound case). Exact clock response remains unknown; neither an echo nor UTC chat spam is claimed as parity. |
| `3b` | Two no-field senders including the slash handler. Purpose not established. |
| `81 55` | No-field slash-handler sender; purpose not established. |
| `81 47` / `81 4a` | Classes exist but no native senders were found. No effect invented. |
| `80 89` | `62038cd6` allocates9 bytes: embedded submessage at2, transaction u8 at3, slot u8 at4, data u32 at5. `621c8e40` accepts successful `80 8a` transaction codes1..6. Which transactions equip/delete/deliver which owned hovercraft object and their economy are not established, and no hovercraft inventory/backend exists here. No arbitrary request data is persisted as a craft upgrade. |
| `80 f2` | FactionID query; `80 f3` has factionID plus array of38-byte `{status u8 at1, constructID u32 at2, fixed name32 at6}` records (`620b4940`, `62161de0`). No faction-construct registry/creation or instance ownership source has been recovered. An empty fabricated registry is not marked handled. |
| `80 f6` | Two u32 fields proven by `620b4a70`. Hack Construct's server rule10024 requires loaded166, real faction, no existing faction hack (FactionFlags bit16), and HackConstructInfoCost. `rshooks.ComputeHackConstructOutcome` returns four times loaded level. The instance/key lifecycle, cost default and capture/ownership rules remain missing. We neither charge nor grant access based on a supplied faction ID. Existing hardline ability166 feedback now correctly distinguishes no ability/no faction from an actual member encountering this unavailable backend; no successful hack is claimed. |
| `80 ef` | `62083ae0`/`62083b00`: four-byte frame with firewall/defence bytes. No ranges, target faction-instance identity, authority model or defence consumer is established. Saving cosmetic bytes without affecting a construct would be a stub, so no faction mutation is added. |
| `62` | Central-storage bag movement is Claude PR110's separate ownership. P7 leaves its implementation untouched. |
| `81 aa` | UserData u32 plus Interrupted byte, sender in the elevator/FX UI (`620ae450`). No proof equates it to `81 91` callback tokens or authoritative ability completion, so it cannot complete arbitrary casts/reward callbacks. |

## Validation and one-pass native checklist (Claude only)

Unit coverage checks exact layouts/offset bases, malformed UTF-8/pointers/chunks, unsigned echo, archive-vs-bag authority and exact GM allowlists. Fresh scenarios `ignored-actions-support`, `ignored-actions-gm`, `ignored-actions-window-close`, and `ignored-actions-presence-trade-compile` exercise real SQL and packets; the support case also starts a fresh server against its disposable database. Run at `--parallel 1` (maximum two simultaneous servers including the persistence check).

No agent launches/drives the client, writes broker requests, acquires its lock, runs a fixture that creates the lock, touches the main DB or character2, or changes system settings. The following is for Claude alone. Native **CSR_Tester110 only**; helpers are headless. Use a fresh output folder and the exact PR head:

```powershell
$root='C:/Users/russe/Developer/mxo-wt/codex-package-p7-client-actions'
$head=(git -C $root rev-parse HEAD).Trim() # compare with the PR's full SHA before starting
$qaRoot=Join-Path $env:TEMP ('mxo-p7-native-'+[guid]::NewGuid().ToString('N'))
dotnet build "$root/tools/NativeQa/NativeQa.csproj" -c Release
dotnet "$root/tools/NativeQa/bin/Release/net8.0/NativeQa.dll" --build-info $root
dotnet "$root/tools/NativeQa/bin/Release/net8.0/NativeQa.dll" $root $qaRoot $head ignored-actions
```

The ignored-actions mode keeps Mara start, provides full500HP, 100000 information, compiler1, pill3, bits4..11 and a single archived Code Bit1 in slot400. Only the copied native110 gets the fixture GM allowlist; this is a server-side fixture grant. `?clientactions` requires **exactly** `MXO_DEV_COMMANDS=1`, and snoop additionally requires that separate allowlist. Do not infer a native pass from helper packets or displayed text alone.

1. Log in as `CSR_AbilityQA` / placeholder password, select only CSR_Tester. Type `/afk` (or `?clientactions afk 1` if the slash toggle is inaccessible). Confirm the AFK indicator; toggle back with `?clientactions afk 0`. The headless observer/late-view checks establish wire propagation; native appearance remains to observe.
2. Open Help/Customer Support. Confirm category **Local server support**, subcategory **General assistance**. Create a short petition `P7 native check`. Confirm saved-ticket feedback, the ticket listed as **New**, and readable text. If Help's controls cannot submit, type `?clientactions petition P7 native check`, then reopen the native list. Record the assigned number. Cancel that owned ticket in Help or `?clientactions cancel <number>`; reopen list and confirm **Closed**. Do not treat a mere chat acknowledgement as a pass of the native list/detail UI.
3. Search knowledge for `stuck`; open **Stuck or defeated** and confirm readable local text. Search a nonsense word and confirm completion with no stale result. Open the policy/about view if available; confirm it explicitly describes local support and does not contact SOE. `?clientactions kb stuck` invokes the same canonical server request but is only a fallback if the search control is inaccessible.
4. Double-click pill3 or use `?clientactions pill 3`. Confirm exactly one owned pill disappears and its existing timer appears. Type `?clientactions vendor` to open the existing Mara tools-vendor fixture through its normal stock/session handler (a gated shortcut because right-click menus are inaccessible). Buy a Lock Pick, close the window with its close control, then type the same shortcut and buy another. Confirm the bag has both and the information cost was applied. Unknown-stock/close/replay authority is covered headlessly; do not claim native spoof/replay testing. Collector, elevator, mission-container, tutorial-wardrobe and corpse close/reopen guards are covered headlessly; this Mara fixture does not claim native coverage of those distant interactions.
5. Exit normally to the Loading Area. In archive storage select the single Code Bit1 from slot400 and press Compile. Confirm the bag Code Bit1 count rises from20 to21 and that archive entry disappears. A second press/reopen must not regenerate it. Other archive equipment/ability sources are explicitly outside this implementation. Return by a known hardline.
6. Type `?clientactions snoop`: confirm a private report of actual loaded abilities, own inventory slots and buff IDs. It is a local text report, not the retail chunk window. With helpers connected, have QAFriend send `?clientactions snoop`; it must get the privilege refusal and no inventory details. Do not grant helpers GM privileges. Existing unknown-purpose requests and construct/hovercraft backend gaps have no native success claim.
7. Close the client; stop the fixture normally by its `stop` file. Wait for `stopped.json` and verify main110 reset to Mara `(23828,495,13984)`, full HP/IS, flag0 and mission986 only, and its lock released. Record exact SHA, native results/limits and the normal cleanup outcome. Never reset/select another main character.

For step6, Claude may create `helpers` and use the existing NativeQa helper format in its README (`actor=QAFriend`, `action=chat`, `text=?clientactions snoop`). This package never executes that native workflow.
