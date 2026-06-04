# Project TODO

This checklist tracks the current modernization push from "builds locally" to "fresh clone is runnable and verifiable."

## 1. Bootstrap and Runtime
- [x] Make `Config.xml.dist` match the parser, or make the parser support the checked-in config shape.
- [x] Add a tracked `WorldConfig.xml.dist` and make local setup copy it to `WorldConfig.xml`.
- [x] Make data-file loading work on macOS/Linux and Windows.
- [x] Copy required config/data files to build output where needed.
- [x] Document the actual one-command local setup path.
- [x] Verify a fresh clone can reach the server-ready log with Docker MySQL.
  - Verified locally with Homebrew `docker` + `docker-compose` + Colima on Apple Silicon macOS.
  - `scripts/smoke-local.sh` reached `Smoke test passed: server reached ready state.`

## 2. Automated Tests
- [x] Add a real test project to the solution.
- [x] Cover config parsing for old and new config shapes.
- [x] Cover path-stable data loading helpers.
- [x] Cover `SpatialGrid`, `ViewManager`, and combat calculator edge cases.
- [x] Keep startup health checks separate from automated tests.

## 3. CI
- [x] Add GitHub Actions for restore, build, and test.
- [x] Run CI on pull requests and pushes to `master`.
- [x] Cache NuGet packages.

## 4. Validation Harness
- [x] Add a smoke script for config, database, build, and launch readiness.
- [x] Add a load-test harness for 50/100/200 simulated clients.
- [ ] Record CPU, tick duration, queue depth, and packet flush latency.
- [ ] Verify player, mob, static object, and subway view spawn/despawn behavior.
  - The current harness sends lightweight UDP client load; full view validation still needs a running server and client/protocol fixtures.

## 5. First Gameplay Economy Loop
- [x] Make vendor buys deduct cash.
- [x] Reject vendor buys when cash is insufficient.
- [x] Send updated cash to the client after a vendor transaction.
- [x] Implement a basic vendor sell path or document the packet format still needed.
- [x] Add tests around pricing and cash updates where possible.

## 6. Packet Research
- [x] Inventory public MxO preservation packet/protocol resources.
- [x] Import useful text packet dumps into `research/packet-dumps`.
- [x] Record source metadata for imported packet dumps.
- [x] Add a repeatable packet research tool for RPC map comparison and dump summarization.
- [x] Generate `docs/generated/packet-research-summary.md` and `.json`.
- [x] Import the public MxOEmu FX symbol list and cross-reference packet payload windows.
- [x] Import decoded MxOEmu `goprops` vendor price data and use it for runtime vendor pricing.
  - `data/vendor_prices.csv` has 41,788 price rows generated from public `goprops.7z`; `VendorPricing` now uses decoded `VendorPrice` and `NonSellable` values with the previous default as fallback.
- [x] Decode normal vendor buyback pricing from public MxOEmu Python resources.
  - `pythonMXO.zip` contains `resource/Python/Monolith.ltp/rshooks.py`; normal buyback is `VendorPrice / 10`, recycle info is `VendorPrice / 20`, and the default vendor price is `1000`. Runtime normal-item sell pricing now follows the one-tenth rule.
- [x] Implement full ability-code vendor buyback semantics.
  - Runtime sell pricing now follows the high-bit ability-code branch from public `rshooks.py`, including base ability-code lookup, level-based premiums, and the mapped full-price exception list for specific abilities.
- [x] Decode protocol 04 object-interaction payload fields used by the local static/dynamic interaction handlers.
  - Current generated report finds 10 top-level object-interaction payloads, all CR2 static door interactions (`80 c8`, object type `3`) across three object ids. No parsed human-NPC/vendor-path interaction (`object type 2`) appears in the imported corpus.
- [x] Mine raw external `HandleCommand` debug logs for object-interaction command examples.
  - TasteeWheat's `Reality.log` contributes two raw CR2 static interaction commands (`80 c8 02 00 20 4e 03 00`), confirming the same payload shape outside the top-level packet dump corpus.
- [x] Correlate decoded object-interaction ids with tracked vendor inventory rows.
  - `data/vendor_items.csv` has 44 parser-derived vendor inventory rows from HD_Neo's old log parser. The generated packet report now flags two matching object ids: `888143880` from packet dumps and `1310720002` from TasteeWheat's raw command examples.
- [x] Correlate decoded object-interaction ids with tracked static object rows.
  - The generated packet report now resolves all four decoded static interaction ids to eight `data/staticObjects_*.csv` rows using the same little-endian `mxoId` interpretation as the runtime loader. Object ids `888143880` and `1310720002` both have static table rows, door-like type symbols, and vendor inventory rows, preserving them as high-priority vendor-open capture targets rather than final vendor semantics.
- [ ] Acquire or capture vendor buy/sell packet dumps.
  - Current imported dumps do not show top-level `CLIENT_VENDOR_BUY`, `CLIENT_VENDOR_SELL`, or `SERVER_VENDOR_OPEN`.
  - Current imported dumps also do not show a parsed static human-NPC/vendor-path interaction payload (`80 c8` or CR1 `80 c3` with object type `2`).
  - Current object-id/vendor-list/static-object correlations suggest some vendor-related static interactions may appear with object type `3`, so future capture requests should include static interaction payloads with object type `2` or `3`.
  - Current imported dumps have one embedded `CLIENT_VENDOR_SELL` candidate inside an `MRGN->Client` packet body, which is not useful evidence yet.
  - Current imported dumps have three embedded `SERVER_VENDOR_OPEN` candidates; two are confirmed as `80 bc` payload fields and one is inside an MRGN packet body, so they are not a decoded vendor window exchange.
- [ ] Implement recycle-info pricing if a decoded packet path needs it.
  - Public `rshooks.py` defines recycle info as `VendorPrice / 20`, but no current decoded gameplay handler uses that path.
- [x] Group repeated `80 bc` server payload structures and validate whether embedded `81 0d` values are headers or payload fields.
- [x] Mine Rajko and local hardcoded queued command examples and compare `80 bc` startup/world-state payloads against the imported corpus.
  - Current generated report finds 59 hardcoded command examples, including 37 `80 bc` examples. The strongest corpus field-group overlap is `45 03 / 11 00 / 00 02` with 105 imported dump hits, and one local `15 00 / 45 00 / 00 f7` payload has 20 exact corpus hits.
- [x] Group top-level `80 b2` server payloads and compare them against hardcoded player attribute / ability load constants.
  - Current generated report finds 106 top-level `80 b2` payloads and 13 hardcoded `80 b2` examples. Local hardcoded payload `35 04 00 00 08 02` has 19 exact corpus hits.
- [x] Group top-level `80 b3` ability-unload payloads and same-packet `80 bc` links.
  - Current generated report finds 408 top-level `80 b3` / `SERVER_ABILITY_UNLOAD` payloads. All are 2 payload bytes. Six dominant fields (`3d 04`, `35 04`, `3e 04`, `3f 04`, `11 00`, and `40 04`) account for 406 samples, are mostly teleport/state-bundle traffic, and have same-packet `80 bc` field-1 matches.
- [x] Group top-level `80 d7` friend-list status payloads and decode handle text.
  - Current generated report finds 594 top-level `80 d7` / `SERVER_FRIENDLIST_STATUS` payloads. They share leading fields `08 00`, status `3c 00` or rare `3b 00`, and `00 8e`, followed by a two-byte little-endian text length and ASCII `SOE+MXO+Vector-Hostile+...` handle text.
- [x] Group protocol 04 server-state header sequences to identify repeated state bundles.
  - Current generated report finds 484 protocol 04 packets containing `80 b2` and/or `80 bc`; 51 of those packets contain both families in the same top-level packet.
- [x] Group top-level `80 bd` coder-attribute payloads and sequence context.
  - Current generated report finds 185 top-level `80 bd` / `SERVER_CODER_ATTRIBUTE_UNKNOWN` payloads. All are 9 payload bytes. The dominant shape is `05 11 00 00 00 00 00 00 00` with 179 hits, mostly in teleport captures and repeated `80 bc` state bundles. Two rare `saikungnorthwestbroken.txt` shapes appear in a distinct sequence with `80 c1`, making them special-case state-bundle leads.
- [x] Link `80 b2` field 0 to `80 bc` field 1 inside the same protocol 04 state bundles.
  - Current generated report finds six shared-field links. The strongest are `3d 04` in 20 packets and `35 04`, `3e 04`, `3f 04` in 19 packets each.
- [x] Validate linked short/long field-value consistency between `80 b2` and `80 bc`.
  - Current generated report finds 210 same-packet field links; 208 repeat the linked field at `80 bc` payload byte offset 9 and repeat the `80 b2` field 1 value at `80 bc` payload byte offset 11. The two exceptions are duplicate `55 00 / 35 04` payloads in `ikeburo_northeast.txt:372`.
- [x] Cross-reference linked `80 b2`/`80 bc` state fields with tracked gameobject symbols.
  - Current generated report loads 44,406 `data/gameobjects.csv` entries and maps repeated shared fields to clothing GoIDs: `3d 04` / 1085 to `Mclothing_Pants_A8_C1`, `35 04` / 1077 to `Mclothing_Pants_A6_C8`, and `3e 04` through `40 04` to adjacent `Mclothing_Pants_A8_C2` through `Mclothing_Pants_A8_C4`.
- [x] Group `80 bc` internal field layout by leading field.
  - Current generated report separates 20-byte `80 bc` payloads into leading-field families: `56 00` (4,044), `15 00` (2,782), `55 00` (2,647), `45 03` (625), and smaller families. All 625 `45 03` payloads repeat field 1 at payload byte offset 9; all 54 `11 00` payloads repeat field 2 at payload byte offset 11.
- [x] Tie hardcoded `80 bc` constants back to corpus layout families.
  - Current generated report shows `45 03` has four hardcoded examples and one corpus-backed field group with 105 hits; `45 00` has 18 hardcoded examples, seven corpus-backed field groups, and 28 total field-group hits; `15 00` has 13 hardcoded examples and one corpus-backed field group with 23 hits.
- [x] Add layout-strength evidence for `80 bc` interaction attribute candidates.
  - Current generated report combines interaction-looking field 1 candidates with the byte-offset layout. Field 1 `11 00` / 17 as `SecureTradeFlag` is the strongest current lead: 108 payloads match that candidate and 105 repeat field 1 at payload byte offset 9, mostly in the `45 03` family. Candidate `TalkActiveTracker` and `BuyActiveTracker` rows remain capture targets but do not currently show the byte-9 repeat pattern.
- [ ] Validate `80 bc` field 1 interaction tracker candidates against paired NPC, vendor, inventory, trade, and loot captures.
  - Current candidate labels include `BuyActiveTracker`, `TalkActiveTracker`, `GiverActiveTracker`, `TakerActiveTracker`, `PutActiveTracker`, `SecureTradeFlag`, and `CurrentState`.
- [ ] Semantically decode `80 bc` field families and map them to object attributes, bonus state, or gameplay actions.
  - Current generated report separates `80 bc` 20-byte payloads into fixed-offset schema shapes. Highest-count targets are `55 00` / bytes `01 00 00` / byte-11 `05 00` / byte-15 `01 00` with 520 records, `56 00` / `b3 00 00` / `21 00` / `01 00` with 435 records, and `15 00` / `00 00 00` / `34 00` / `01 00` with 349 records. Their capture scopes are mostly teleport traffic (507/520, 426/435, and 342/349 respectively), and their top packet sequences are large `80 bc` state bundles such as `80 bc x30` or runs interleaved with `80 bd`, `80 b2`, and `80 b3`, so treat them as world-state/teleport-state targets rather than strong vendor leads. The `45 03` family remains the strongest state-link target because all 625 payloads repeat field 1 at byte offset 9.
- [x] Extend packet tooling to classify protocol 03 object/state packet families, not only protocol headers.
  - Current generated report parses 8,456 protocol 03 object messages, fully segments 6,770 messages, and identifies 14,471 known selector segments across movement, object state, spawn/profile, emote/effect, compact terminal selector `28` markers, selector `80` self-view attributes, selector `09` two-byte-prefixed movement positions, primary/secondary/adjacent movement wrappers, selector `9b` state blocks, delete-view, and static object/door families.
- [x] Extend protocol 03 tooling from first object-view signature classification into conservative object-message segmentation and known per-selector payload sizing.
- [x] Treat protocol 03 selector `00` as a one-byte state marker.
  - This counted 696 selector `00` segments and raised full protocol 03 segmentation from 326 to 990 messages before the secondary movement wrapper pass.
- [x] Decode secondary movement wrappers in two-update protocol 03 object packets.
  - Current generated report counts 1,367 terminated secondary movement wrapper segments, including rows exposed after adjacent object-view parsing.
- [x] Decode primary movement wrappers in two-update protocol 03 object packets.
  - Current generated report counts 167 bounded primary movement wrappers whose first selector is `12`, `13`, or `14` and whose payload starts `00 <id> 00 <mode> <movement selector>`. Selector `12` uses 17-byte payloads, selector `13` uses 18-byte payloads, and selector `14` uses 19-byte payloads.
- [x] Decode adjacent movement wrappers at protocol 03 object-view boundaries.
  - Current generated report counts 3,348 adjacent movement wrapper segments. These second segments start `00 02 <movement selector>`, carry plausible position data, omit the terminated-wrapper `00 00`, and are immediately followed by another plausible object-view header.
- [x] Decode protocol 03 selector `09` movement-state position prefixes.
  - Current generated report counts 25 update-count-3, first-selector-`09` movement bundles whose first segment is a 14-byte two-byte prefix plus xyz position.
- [x] Group protocol 03 movement-state post-position tail lead families.
  - Current generated report keeps update-count-3 movement-state tails after first selectors `08` and `09` unresolved when variable tail bytes collide with generic selectors. It groups 283 tails into lead families, led by first-selector `08` with `ff 01 64 01` (172 records), first-selector `08` with `ff 01 4e 01` (67), first-selector `08` with `80 80` (19), first-selector `09` with `ff 01 64 01` (18), first-selector `09` with `80 80` (4), and first-selector `09` with `ff 01 4e 01` (3). The tail selector byte plus the next three payload bytes now decode as a little-endian tag; adjacent captures commonly step by 124-126 or 250-251, making it a timestamp/sequence lead.
- [x] Decode one-update protocol 03 selector `9b` state blocks.
  - Current generated report counts 22 one-update selector `9b` records with fixed 10-byte payload `01 08 80 fd 00 00 00 00 00 00`. Longer selector `9b` contexts remain unresolved.
- [x] Extract embedded movement-window leads from protocol 03 selector payloads.
  - Current generated report finds 486 embedded movement windows across 41 outer selectors after adjacent wrappers and movement-state tails are bounded. The extractor now recognizes `00 <mode> <movement selector>` windows instead of only the `00 02` form. Marker mode `02` accounts for 355 windows, including 164 bounded primary wrappers and 191 unresolved payload leads. Newly surfaced mode `06` accounts for 115 windows, all unresolved, in object-state groups such as outer selectors `03`, `25`, `1f`, `26`, and `30`; these carry plausible movement xyz prefixes but still need body-length evidence. The dominant mode `06` suffix lead is `ff 00 00 00 10 00 00 00`, appearing after 52 `06/0c` windows and 40 `06/0e` windows, and both groups continue with `00 00 01 ff 00 00 00 00`.
- [x] Extract string leads from variable-length protocol 03 object-view packets.
  - Current generated report extracts printable strings from unresolved variable blocks. The strongest family initially appeared as update count `12`, selector `57`; creation-aware parsing now identifies it as Object599 / `NPC_BASE` dynamic creation records with repeated names such as `A.S.P. Major`, `Gold Blood Champion`, and `Sisters of Fate Student`.
- [x] Correlate protocol 03 variable-block strings with tracked mob/NPC rows.
  - Current generated report loads 14,642 world entity rows from `data/mob_parsed_untouched.csv` and `data/NPC_COLLECTION.xml`, then exact-matches protocol 03 string leads to known entity names. Top matches include `A.S.P. Major`, `Gold Blood Champion`, `Sisters of Fate Student`, and `Black Tiger Pupil` with level, zone, RSI samples, and bounded prefix/suffix byte context.
- [x] Parse Object12 / `PlayerCharacter` dynamic creation records.
  - Current generated report directly extracts all 176 protocol 03 update count `12`, selector `0c` player spawn/profile records as GoID 12 / `PlayerCharacter` creation packets. The Object12 fixed-width creation table decodes `RealLastName`, `RealFirstName`, `CharacterName`, `Level`, `Health`, `MaxHealth`, `RSIDescription`, `CombatantMode`, and 24-byte position values. The corpus has 175 `AfterWhoruNeo` records and one `platinumfox` record.
- [x] Aggregate Object599 / `NPC_BASE` dynamic creation record-prefix candidates.
  - Current generated report directly extracts records matching the observed `0c 57 02 <spawn counter> cd ab <attribute count> 8e <name>` prefix shape. It maps `57 02` to tracked GoID 599 / `NPC_BASE`, records spawn counters, creation attribute counts, first masks, and entity-row counts for repeated names.
  - Spawn counter bytes are reused across different names, as expected for packet-local creation state. The byte after `cd ab` is now treated as creation attribute count rather than a name/entity id candidate.
- [x] Identify Object599 / `NPC_BASE` `CharacterName` slot size.
  - All 433 directly extracted Object599 / `NPC_BASE` creation records in the current corpus have a 32-byte fixed `CharacterName` attribute after first creation mask `8e`.
- [x] Identify Object599 / `NPC_BASE` attributes after `CharacterName`.
  - All 433 directly extracted Object599 / `NPC_BASE` creation records in the current corpus share `TitleAbility = 00 10 00 00` and `CombatantMode = 22`, matching the local mob creation defaults.
- [x] Parse Object599 / `NPC_BASE` creation masks into fixed-width attributes.
  - The current generated report walks the creation mask stream with local fixed-width Object599 attribute definitions. All 433 directly extracted records have matching declared and parsed attribute counts, covering 14 through 21 attributes and grouping raw values for core fields such as `CharacterName`, `TitleAbility`, `CombatantMode`, `StopFollowActiveTracker`, `Description`, `Position`, `IsEnemy`, `Level`, `Health`, `MaxHealth`, and `CurrentState`.
- [x] Correlate typed Object599 / `NPC_BASE` creation attributes with tracked mob rows.
  - Among 423 creation records with same-name mob rows, decoded `Level`, `Health` plus `MaxHealth`, `NPCRank`, `DebuffState`, and `CurrentState` all match 423/423, while `Description`/RSI-like ids match 417/423. Decoded `Position` values exact-match 0/423 old mob rows, but nearest same-name row distances put 328/423 within 1k units, 373/423 within 2.5k, and 405/423 within 5k.
- [x] Resolve Object599 / `NPC_BASE` equipment and effect symbol leads.
  - Current generated report decodes `EquippedItemID` as a little-endian gameobject id and maps all 313 observed records across 12 values to imported gameobject symbols plus MxOEmu `&gib` command names from 41,696 imported item rows. `EffectID` appears in 97 records across three values, and all three now resolve through imported FX definitions: adrenaline booster loop, fast healing, and accelerated Oligarch spawn.
- [x] Extract protocol 03 selector `28` effect/emote prefix leads.
  - Current generated report finds 97 selector `28` payloads, including 14 first-selector records and additional non-first occurrences exposed after bounded selector `80` self-view attribute and adjacent movement-wrapper decoding. Nineteen non-first records now parse as compact terminal marker `01 <01|02|03> 00 00`; 78 expose a 12-byte little-endian float position prefix, and 42 contain imported FX ID windows.
- [x] Extract protocol 03 selector `2a`/`2e` position-like prefix leads.
  - Current generated report finds 138 selector `2a`/`2e` variable tails and extracts 130 repeated lead families with plausible little-endian xyz triples. The strongest groups are `2e` prefix `00 40 00 00 00 10` at position offset 8 with 33 records, and `2a` prefix `00 40 00 00 00 10` at position offset 7 with 16 records.
- [x] Decode protocol 03 selector `80` self-view attribute mask/value blocks.
  - Current generated report parses 164 selector `80` payload starts with the local Object12 self-view attribute table, yielding 173 fixed-width attributes. The largest group is 68 `Health` updates; the previous `4c 0a 00 28` FX-looking window is now decoded as `Health = 0a4c`, a zero mask terminator, and then the next selector `28`. Only one selector `80` tail remains outside this mask/value layout.
- [x] Extract protocol 03 static object/door spawn prefix leads.
  - Current generated report finds seven selector `9e`/`a0` static object leads with a stable `cd ab` separator. Selector `a0` carries static object id `888143880` across six records, protocol object type `3`, six instance bytes, two matching static rows, one vendor inventory row, and quaternion sample `0,-0.707,0,0.707`. Selector `9e` carries static object id `888143885` in one record, protocol object type `3`, instance byte `ea`, two matching static rows, no vendor inventory row, and quaternion sample `0,0.707,0,-0.707`.
- [x] Mine external hardcoded protocol 03 object-message examples from MxOEmu forks.
  - Current generated report finds 34 hardcoded protocol 03 examples from local code, Rajko MxOEmu, and the TasteeWheat door/hardline fork.
- [ ] Resolve variable-length protocol 03 spawn/profile, Object599 / `NPC_BASE`, appearance/attribute, effect/emote, static object/door, and unknown payload layouts.
  - Object12 / `PlayerCharacter` and Object599 / `NPC_BASE` creation parsing now names fixed-width attributes and decodes core identity, health, RSI/effect, state, and position fields. Selector `80` now decodes Object12 self-view attribute mask/value blocks, while selector `2a`/`2e` and `28` parsing extracts stable prefix, compact terminal markers, position, and FX-window leads. Static interaction ids now resolve to static object rows and type symbols, selector `9e`/`a0` static object records now expose a stable object-id prefix, and movement wrapper parsing now bounds selector `08`/`09` movement-state tail leads, selector `12`/`13`/`14` primary wrappers, terminated secondary wrappers, and adjacent boundary wrappers. The unresolved work is remaining creation/profile tails, effect/equipment behavior validation, movement-state post-position tail body lengths, selector `2a`, `2e`, and longer `28` variable tails, the one residual selector `80` tail, static object view payload bodies, full unknown selector body lengths, and before/after gameplay captures. For nested movement, the next capture request should preserve before/after object state around `00 06 <movement selector>` payloads and the repeated `ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00` boundary lead, because the current corpus exposes 115 repeated mode `06` object-state movement windows without any bounded-wrapper examples.
- [ ] Follow up on public Hardline Dreams/RaGEZONE lead for combat packet logs and request logs with object serialization, ability state, FX, and before/after attribute context.
- [ ] Use new packet evidence to implement vendor selling.
