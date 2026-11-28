# Remaining client actions (P7)

This is an audit of **every row marked ignored on master `10e2cdf`**, rather than a claim that every request has retail parity. Native QA is pending. Client files and captures were inspected read-only. The server's new support service, knowledge articles, compilation policy and GM text reports are explicitly local policies.

## Implemented behavior and authority

* `2a <Afk u8>` updates AFK in the own view (47), existing observer views (35), and subsequent creation records. Tells to an away player still deliver and tell the sender the recipient is away. AFK is session presence, not a database setting. `62240b40` allocates the two-byte request; Object12 maps its attributes.
* `81 4d <ElapsedTime u32>` returns that same unsigned field. Retail echoed it; `62143a6c` allocates six bytes. It is not repurposed as a server timestamp.
* `80 c6 <bag slot u16>` resolves the authenticated player's slot to the existing supported consumable and calls the same inventory effect/timer/removal path as `80 b9`. Dead, duel-defeated and incapacitated players cannot use this alternate sender. Unsupported consumables remain unsupported. The four-byte constructor is `621d35c0`.
* `81 10 <StringID u32> <CloseUI u8>` invalidates the vendor session only for an exact five-byte body with CloseUI=1. CloseUI=0 and malformed shapes do not close it. Opening a vendor records actual data-loaded stock and district; buying requires a current session and a listed item; selling requires a current session. Unknown IDs no longer open an unrelated fallback vendor, and district is part of lookup. World-generation/district changes invalidate the session. Vendor proximity is still the existing interaction policy; this does not claim a reconstructed retail trading radius. Constructor `6210a180` allocates seven bytes.
* `81 17 00000000 00` clears the active collector. `81 41 <CurrentFloor u16>` clears the elevator selection; it neither teleports nor guesses a destination from the floor field. `81 1a` closes the selected loot window without discarding the corpse's remaining loot. Their native sizes are 7 (`62071840`), 4 (`620ae350`), and 2 bytes respectively. A replayed exchange/floor/loot operation must acquire a new selection.
* `60 <ObjectID u32> <archive slot u16> <PersonalInventoryItem u8> <tool slot u8> <FromCrafting u8>` supports materializing an owned uploaded Code Bit or fragment into the bag. `6208375a..62083782` proves that ObjectID is the **decoded goid**, not the packed archive value; the complete record is ten bytes. PersonalInventoryItem=1 is refused. LA requests must actually be in LA; FromCrafting requests must be in-world, alive and able to act, out of combat, with an active [Compile session](COMPILE-ITEMS.md), loaded Compile Activate (969, including its parent grant) and Code Compiler 1271 in the session's supplied tool slot. The archive row must match both slot and goid. One SQL transaction checks current cash/capacity and commits archive charge, bag stack/item and information cost. The answer is `61` result 0 success, 1 failed, 3 full or 4 insufficient information. **Local policy:** a stored fragment materializes deterministically, consumes one archive charge, and uses the existing script's `.12 * VendorPrice` compile cost (plus the existing discount outside LA). Equipment-code and ability-code copying, success odds and the rest of the retail archive economy remain unimplemented. This is distinct from `81 2e` recipe assembly and does not duplicate P8's last-charge notification fix.

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

## Round 2 priorities

| Impact | Action and evidence | Result |
|---|---|---|
| High when blocked | Post-tutorial jack-in (`81 01`). The Norajob creation trace has the bare request followed by `80 fa 01 00 00 00 99 00 00 00 01` and `81 07`. The Morpheuzz and first-mission traces name the same hardline. | Accept the request once, from the Loading Area after a server-recognized tutorial finish or skip. Keep the existing `05` compatibility route with the same admission. |
| Medium | Loading Area archive Compile (`60`). The stock archive sender and `61` results are documented in [Client actions](CLIENT-ACTIONS.md#item-requests-in-detail). Ordinary stored codes already have an in-world executor and charge format. | **OURS:** extend the existing direct LA policy to supported ordinary item codes. Use the shipped base cost, one charge, and the same atomic archive/bag/Info update. The in-world tool, contest and timing rules still apply when FromCrafting=1. |
| Medium | Decompile Items (`81 31`). Owned item/slot, contest, recipe notification and result are already verified. Separate SQL writes could persist the recipe and consume a replacement item if the source changed. | Match the owned source under a SQL lock. One transaction consumes the source and persists the recipe. A failed commit emits a failed result, with no item loss or recipe notice. |

The other ignored actions have lower demonstrated impact or an unresolved response
contract. World-entry `80 be` has no direct captured response. `81 aa` has an
interruption flag and user datum, with no demonstrated mapping to a server-owned
completion token. `81 46` still has no established ranges or units and no sender
was found for `81 47` or `81 4a`. Constructs, hovercraft, clock, target
damage, `81 56` and `39` remain deferred. Central storage `62` is served.

`client-action-followups` starts on the verified Mara Central hardline 152
stand (17000,495,2300). With a stock-written disposable UI template, hotbar
page 2 has Decompile Items in slot 1 and the Item Decompiler tool in slot 2.
Open Inventory, use the page 2 shortcut and click the pill in bag slot 5.
Use the nearby booth's Loading Area action for the archive check. The fixture
bootstrap replays the inventory request, the booth radial and the captured
zero-Item archive request. It verifies both flagged rows in the actual Margin
known-abilities block. Run `client-action-tutorial` as a fresh fixture to
log directly into the Tutorial and check Skip Tutorial independently.
Headless scenarios also cover the tutorial request and replay, archive charge
depletion, and a rejected recipe commit followed by a successful decompile.

## Round 2 follow-up ranking

The ranking weighs lost items or Info first, followed by blocked player actions
and missing presentation. It includes implemented handlers that still answered
some requests incorrectly.

| Rank | Player impact | Stock action | Evidence and disposition |
|---|---|---|---|
| 1 | High | Vendor Buy `81 0e` | The ordinary purchase used to debit Info even when inventory placement failed. The vendor charge and item update now commit together. Full bags get the existing vendor refusal response. |
| 2 | High | Marketplace List `81 2b` | The stock Lock Pick code trace uses container 0, slot 396. A bag lookup ignores that source. The captured `5f 8c 01 00` and `81 2c` now follow an atomic archive removal and posting. |
| 3 | High | Marketplace Buy or De-list `81 26` | Checkout used to write the listing, bag, and wallets separately and could only deliver to the bag. Now it commits delivery, sale state and payment together. An ordinary item code returns through a known-abilities `5e` record. |
| 3a | High | Secure Trade Accept `81 85` | Both accepted offers now [settle inventory and Info in one transaction](SECURE-TRADE.md#atomic-settlement). A failed final write closes the trade and keeps both participants' assets. |
| 3b | High | Bag Merge and Split `67`/`68` | [Personal bag moves](CLIENT-ACTIONS.md#atomic-personal-bag-moves) now commit both slots together and send a visible refusal on rollback. |
| 3c | High | Central Storage `62`, `67` and `68` | [Archive moves](CENTRAL-STORAGE.md#atomic-moves) now commit both slot writes before publishing an update. A failed write keeps the source item. |
| 3d | High | Bag Move or Swap `65` | [Bag slot swaps](CLIENT-ACTIONS.md#atomic-bag-slot-swaps) now commit both actual row moves. Failure restores the cursor with the existing same-slot acknowledgement. |
| 3e | High | Weapon Equip or Unequip `63`/`64` | [Weapon equipment](CLIENT-ACTIONS.md#atomic-weapon-equip-and-unequip) now commits the displacement and the equip together. A failed write or unavailable bag slot keeps the held weapon. |
| 3f | High | Clothing Equip or Unequip `63`/`64` | [Clothing equipment](CLIENT-ACTIONS.md#atomic-clothing-and-appearance-changes) now commits the inventory moves and appearance together. A failed write or unavailable bag slot keeps the outfit and both item rows. |
| 3g | High | Ordinary corpse Take All `81 1f` | [Corpse loot](CLIENT-ACTIONS.md#atomic-ordinary-corpse-take-all) now commits Info and fitting items together. A failed write keeps the corpse contents available for retry. |
| 3h | High | Crew and faction bank `78` | [Bank transfers](CLIENT-ACTIONS.md#atomic-crew-and-faction-bank-transfers) now commit group and personal balances together before publishing the ledger delta. |
| 3i | High | Faction Recruit `7a` | [Faction founding](GAMEPLAY-BREADTH-PEERS.md#atomic-faction-founding) commits the faction, starting crew rank and member affiliations together. A failed founding keeps the name available and sends no invitation. |
| 3j | High | Faction invitation acceptance `6f 01` | [Faction admission](GAMEPLAY-BREADTH-PEERS.md#atomic-faction-admission) checks the original crew and faction and commits the rank and affiliations together. |
| 3k | High | Faction roster Remove `73` | [Faction ejection](GAMEPLAY-BREADTH-PEERS.md#atomic-faction-ejection) commits the crew's removal and its member affiliations together. A refused write keeps the roster available for retry. |
| 3l | High | Faction Disband `77 01` | [Faction disband](GAMEPLAY-BREADTH-PEERS.md#atomic-faction-disband) commits the archive marker, crew resets and member affiliations together. A refused write keeps the roster and treasury available. |
| 3m | High | Crew roster Remove `72 02` | [Crew member removal](GAMEPLAY-BREADTH-PEERS.md#atomic-crew-member-removal) commits the roster deletion and the character's crew and faction resets together. A refused write keeps the member in both groups. |
| 3n | High | Crew roster Make Captain `75 02` | [Crew captain transfer](GAMEPLAY-BREADTH-PEERS.md#atomic-crew-captain-transfer) commits the crew roles and any faction-leader change together. A refused transfer preserves both groups' leadership and releases the roster input lock. |
| 3o | High | Crew Create `80 84` plus invite acceptance `6f 02` | [Crew founding](GAMEPLAY-BREADTH-PEERS.md#atomic-crew-founding) commits the crew, roster and affiliations together. The stock failure result releases the window and preserves the requested name for retry. |
| 4 | Medium | Mail Send and Take Attachments `81 e5` / `81 fc` | Each sends or claims attachments in one SQL transaction. Database failures and full bags return a refusal while preserving the transfer for retry. See [native mail transaction check](CLIENT-ACTIONS.md#native-mail-transaction-check). |
| 5 | Medium | Archive Compile `60`, Decompile Items `81 31` | [Owned ordinary item and equipment codes](COMPILE-ITEMS.md#owned-equipment-codes) compile using their stored charges. Decompile learns new recipes and recovers fragments from known recipes using the shipped contest and [the current yield policy](CLIENT-COMBAT-ACTIONS.md#decompile-fragment-recovery). The ability-code archive economy remains open. |
| 6 | Low | Drop Luggable `5c`; Use Pill `80 c6` | [Light luggables](DATA-MINING.md#archive-travel) support flagged drops at Archive boundaries. [Carried Datamine taps](DATA-MINING.md#datamine-taps-and-master-shadow) can end Master Shadow, [pulse their shipped effect](DATA-MINING.md#carried-tap-effect), and [stay out of Secure Trade and mail](DATA-MINING.md#non-tradeable-taps). [Restorative pills](CLIENT-ACTIONS.md#restorative-pill-consumption) commit the dose before starting reuse or effects, including [Health Boost](HEALTH-BOOST-PILLS.md#authority-and-lifecycle) and [Anti-Stun](ANTI-STUN-PILLS.md). Supported pill families, including [RSI modification pills](CLIENT-ACTIONS.md#rsi-modification-pills), [Quicksilver Pill](CLIENT-ACTIONS.md#quicksilver-pill), [Tactic Boosts](CLIENT-ACTIONS.md#tactic-boost-consumables), the [IS Booster](CLIENT-ACTIONS.md#inner-strength-booster), [Blue Marbles](CLIENT-ACTIONS.md#blue-marble-consumables), [Health Inhalers](CLIENT-ACTIONS.md#health-inhalers), [Optimizer and Overdrive items](CLIENT-ACTIONS.md#optimizer-and-overdrive-consumables), [SSR Gum](CLIENT-ACTIONS.md#ssr-gum), [Andvaranaut Coupler](CLIENT-ACTIONS.md#andvaranaut-coupler), [Smith's Rage Pill](CLIENT-ACTIONS.md#smiths-rage-pill), and [event health regeneration items](CLIENT-ACTIONS.md#event-health-regeneration-items), work. Heavy attachments need carrying-state evidence, and additional consumables need verified effect rules. |
| 6a | Medium | Manual luggable Drop `5c` and Take | [Manual transfer failures](DATA-MINING.md#manual-transfer-refusal) keep both the item and its original location. Drop also clears the pending inventory cursor. |
| 7 | Low | FX completion `81 aa`, world-entry `80 be`, range `81 46` | Senders are known. No reply or authoritative state transition is established. `81 47` and `81 4a` still lack a reachable sender. |
| 8 | Low | Local support `81 b*`, GM inspection `81 57/59/5b` | Local support and private GM text reports work. External ticket service and structured GM viewers remain open. |

The marketplace uses the low seven bits of the captured class-46 descriptor
for the charge count (1–127). Listing accepts both flagged storage rows and
older unflagged Write Code rows whose category matches a shipped recipe.
Checkout stores `CodeStorage.BaseValue(goid)` and `charges - 1`, then sends the
flagged storage add. Marketplace delivery uses slots 384–519 by server
policy, with the actual u16 slot in the storage add. The `81 27` buy result
echoes the requested byte slot and keeps the marketplace descriptor.

Use `vendor-purchase-full-bag` at Mara Central to attempt a Lock Pick purchase
at 5,000 Info, recycle one shirt for 340, then buy for 1,000. Use
`marketplace-archive-code` from the same booth to list CSR's code for 1,200,
then buy QAFriend's pre-posted code for 1,000. Compile the bought code in the
archive. Expect `61 00`, a Lock Pick in the bag, and 880 Info after the
120 Info compile. De-list and re-list CSR's code to check the return path. The
fixtures print the stands and steps.

The vendor refusal's `81 10` carries a zero StringID, which bypasses the
stock popup. Ordinary vendor purchases now also put the refusal in visible
chat (`2e c7`). The inventory-full status used by mail and the marketplace
has no verified mapping to the vendor's popup StringID.

## Original ignored-row audit

The following rows remain ignored; adding a switch that accepts and does nothing would not improve parity. P3 PR113 is now integrated: `5c` is partial for light luggables with pickup/drop persistence; heavy attachments remain absent. Actual combined counts are 118 handled / 14 partial / 15 ignored / 19 not sent (166 rows). Since then `81 55` (/stuck) is handled by a local Loading Area policy (CLIENT-ACTIONS.md).

| Request | Evidence and remaining reason |
|---|---|
| `80 be` | Inventory/ability manager world-entry sender `621e2cf0`, no fields. Existing captures show no direct reply; following `80 bc` is correlated with normal world-entry effects. An active-effects query is still a guess, so no unsolicited effect replay is invented. |
| `81 01` | Handled for the server-owned tutorial exit. The request supplies no destination and has no effect during ordinary Loading Area visits. |
| `81 56` | Thirteen view-manager send sites, no fields. Purpose and response contract remain unknown. |
| `39` | Direction byte; eight captures, seven with value2. Which operation is directed remains unknown; it is not treated as movement/yaw. |
| `81 46` | `62241a50` allocates10 bytes; `6225909d..622590c5` parses two integer words and sends them. The name `/range`, units, which ranges change and authority are not established. No guessed interest-radius or weapon-range control. |
| `81 37` | `62078cd0`/`62078d03` writes the imported tick count into TimeRequested. `62079687` invokes it through a common UI timer; it is not proof of a `/time` slash request. Dispatcher137 goes to `6217e9b0` (no inbound case). Exact clock response remains unknown; neither an echo nor UTC chat spam is claimed as parity. |
| `3b` | Two no-field senders; C1 now identifies `/targetdamage` (string600000d3, dispatcher62258267/leaf62259838). Response and reporting semantics remain unknown; see CLIENT-ACTIONS-FOLLOWTHROUGH.md. |
| `81 47` / `81 4a` | Classes exist but no native senders were found. No effect invented. |
| `80 89` | `62038cd6` allocates9 bytes: embedded submessage at2, transaction u8 at3, slot u8 at4, data u32 at5. `621c8e40` accepts successful `80 8a` transaction codes1..6. Which transactions equip/delete/deliver which owned hovercraft object and their economy are not established, and no hovercraft inventory/backend exists here. No arbitrary request data is persisted as a craft upgrade. |
| `80 f2` | FactionID query; `80 f3` has factionID plus array of38-byte `{status u8 at1, constructID u32 at2, fixed name32 at6}` records (`620b4940`, `62161de0`). No faction-construct registry/creation or instance ownership source has been recovered. An empty fabricated registry is not marked handled. |
| `80 f6` | The stock constructor at `620b4a70` writes the requested faction and construct as two u32 values. The selection now uses the same refusal path as the hardline action. The server reads the player's loaded 166 and current faction and sends an unavailable notice to eligible members. Instance and key lifecycles, the Info price, faction war state and capture/ownership remain open. The refusal leaves both resources unchanged. [Hack Construct Key](UTILITY-ABILITIES.md#hack-construct-key-166) |
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
$plan = dotnet "$root/tools/NativeQa/bin/Release/net8.0/NativeQa.dll" --fixture-plan ignored-actions | ConvertFrom-Json
if ($plan.Environment.MXO_DEV_COMMANDS -ne '1' -or $plan.Environment.MXO_GM_CHARACTER_IDS -ne '110') { throw 'Wrong ignored-actions fixture environment' }
dotnet "$root/tools/NativeQa/bin/Release/net8.0/NativeQa.dll" $root $qaRoot $head ignored-actions
```

The ignored-actions fixture uses the same `NativeQaFixturePlan.EnvironmentFor(mode)` for the read-only plan and actual server launch. The plan exits before directory, client-lock or database work; its CLI regressions run from empty test directories and cover all eight explicit QA modes (remaining, ignored-actions, coder-pets, styles, data-mining, death-presentation, actions, health-callbacks), plus default/unknown disabled. The earlier remaining-only setting was a fixture regression, not an intended restriction on the other documented QA helpers. This fixes the ce534a5 native fixture failure: `/afk` displayed its native AFK tag, but every helper was correctly blocked because dev commands had been configured off. No support/compilation native pass is claimed from that run.

The ignored-actions mode keeps Mara start, provides full500HP, 100000 information, compiler1, pill3, bits4..11 and a single archived Code Bit1 in slot400. Only the copied native110 gets the fixture GM allowlist; this is a server-side fixture grant. `?clientactions` requires **exactly** `MXO_DEV_COMMANDS=1`, and snoop additionally requires that separate allowlist. Do not infer a native pass from helper packets or displayed text alone.

1. Log in as `CSR_AbilityQA` / placeholder password, select only CSR_Tester. Type `/afk` (or `?clientactions afk 1` if the slash toggle is inaccessible). Confirm the AFK indicator; toggle back with `?clientactions afk 0`. The headless observer/late-view checks establish wire propagation; native appearance remains to observe.
2. Open Help/Customer Support. Confirm category **Local server support**, subcategory **General assistance**. Create a short petition `P7 native check`. Confirm saved-ticket feedback, the ticket listed as **New**, and readable text. If Help's controls cannot submit, type `?clientactions petition P7 native check`, then reopen the native list. Record the assigned number. Cancel that owned ticket in Help or `?clientactions cancel <number>`; reopen list and confirm **Closed**. Do not treat a mere chat acknowledgement as a pass of the native list/detail UI.
3. Search knowledge for `stuck`; open **Stuck or defeated** and confirm readable local text. Search a nonsense word and confirm completion with no stale result. Open the policy/about view if available; confirm it explicitly describes local support and does not contact SOE. `?clientactions kb stuck` invokes the same canonical server request but is only a fallback if the search control is inaccessible.
4. Double-click pill3 or use `?clientactions pill 3`. Confirm exactly one owned pill disappears and its existing timer appears. Type `?clientactions vendor` to open the existing Mara tools-vendor fixture through its normal stock/session handler (a gated shortcut because right-click menus are inaccessible). Buy a Lock Pick, close the window with its close control, then type the same shortcut and buy another. Confirm the bag has both and the information cost was applied. Unknown-stock/close/replay authority is covered headlessly; do not claim native spoof/replay testing. Collector, elevator, mission-container, tutorial-wardrobe and corpse close/reopen guards are covered headlessly; this Mara fixture does not claim native coverage of those distant interactions.
5. Exit normally to the Loading Area. In archive storage select the single Code Bit1 from slot400 and press Compile. Confirm the bag Code Bit1 count rises from20 to21 and that archive entry disappears. A second press/reopen must not regenerate it. Other archive equipment/ability sources are explicitly outside this implementation. Return by a known hardline.
6. Type `?clientactions snoop`: confirm a private report of actual loaded abilities, own inventory slots and buff IDs. It is a local text report, not the retail chunk window. With helpers connected, have QAFriend send `?clientactions snoop`; it must get the privilege refusal and no inventory details. Do not grant helpers GM privileges. Existing unknown-purpose requests and construct/hovercraft backend gaps have no native success claim.
7. Close the client; stop the fixture normally by its `stop` file. Wait for `stopped.json` and verify main110 reset to Mara `(23828,495,13984)`, full HP/IS, flag0 and mission986 only, and its lock released. Record exact SHA, native results/limits and the normal cleanup outcome. Never reset/select another main character.

For step6, Claude may create `helpers` and use the existing NativeQa helper format in its README (`actor=QAFriend`, `action=chat`, `text=?clientactions snoop`). This package never executes that native workflow.
