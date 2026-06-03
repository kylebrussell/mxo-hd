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
- [x] Decode protocol 04 object-interaction payload fields used by the local static/dynamic interaction handlers.
  - Current generated report finds 10 top-level object-interaction payloads, all CR2 static door interactions (`80 c8`, object type `3`) across three object ids. No parsed human-NPC/vendor-path interaction (`object type 2`) appears in the imported corpus.
- [x] Mine raw external `HandleCommand` debug logs for object-interaction command examples.
  - TasteeWheat's `Reality.log` contributes two raw CR2 static interaction commands (`80 c8 02 00 20 4e 03 00`), confirming the same payload shape outside the top-level packet dump corpus.
- [x] Correlate decoded object-interaction ids with tracked vendor inventory rows.
  - `data/vendor_items.csv` has 44 parser-derived vendor inventory rows from HD_Neo's old log parser. The generated packet report now flags two matching object ids: `888143880` from packet dumps and `1310720002` from TasteeWheat's raw command examples.
- [ ] Acquire or capture vendor buy/sell packet dumps.
  - Current imported dumps do not show top-level `CLIENT_VENDOR_BUY`, `CLIENT_VENDOR_SELL`, or `SERVER_VENDOR_OPEN`.
  - Current imported dumps also do not show a parsed static human-NPC/vendor-path interaction payload (`80 c8` or CR1 `80 c3` with object type `2`).
  - Current object-id/vendor-list correlations suggest some vendor-related static interactions may appear with object type `3`, so future capture requests should include static interaction payloads with object type `2` or `3`.
  - Current imported dumps have one embedded `CLIENT_VENDOR_SELL` candidate inside an `MRGN->Client` packet body, which is not useful evidence yet.
  - Current imported dumps have three embedded `SERVER_VENDOR_OPEN` candidates; two are confirmed as `80 bc` payload fields and one is inside an MRGN packet body, so they are not a decoded vendor window exchange.
- [ ] Implement full ability-code vendor buyback semantics.
  - Public `rshooks.py` adds level-based ability-code pricing and a full-price exception list for specific abilities; normal item buyback is implemented, but these ability exceptions still need constant mapping.
- [x] Group repeated `80 bc` server payload structures and validate whether embedded `81 0d` values are headers or payload fields.
- [x] Mine Rajko and local hardcoded queued command examples and compare `80 bc` startup/world-state payloads against the imported corpus.
  - Current generated report finds 59 hardcoded command examples, including 37 `80 bc` examples. The strongest corpus field-group overlap is `45 03 / 11 00 / 00 02` with 105 imported dump hits, and one local `15 00 / 45 00 / 00 f7` payload has 20 exact corpus hits.
- [x] Group top-level `80 b2` server payloads and compare them against hardcoded player attribute / ability load constants.
  - Current generated report finds 106 top-level `80 b2` payloads and 13 hardcoded `80 b2` examples. Local hardcoded payload `35 04 00 00 08 02` has 19 exact corpus hits.
- [x] Group protocol 04 server-state header sequences to identify repeated state bundles.
  - Current generated report finds 484 protocol 04 packets containing `80 b2` and/or `80 bc`; 51 of those packets contain both families in the same top-level packet.
- [x] Link `80 b2` field 0 to `80 bc` field 1 inside the same protocol 04 state bundles.
  - Current generated report finds six shared-field links. The strongest are `3d 04` in 20 packets and `35 04`, `3e 04`, `3f 04` in 19 packets each.
- [x] Validate linked short/long field-value consistency between `80 b2` and `80 bc`.
  - Current generated report finds 210 same-packet field links; 208 repeat the linked field at `80 bc` payload byte offset 9 and repeat the `80 b2` field 1 value at `80 bc` payload byte offset 11. The two exceptions are duplicate `55 00 / 35 04` payloads in `ikeburo_northeast.txt:372`.
- [x] Group `80 bc` internal field layout by leading field.
  - Current generated report separates 20-byte `80 bc` payloads into leading-field families: `56 00` (4,044), `15 00` (2,782), `55 00` (2,647), `45 03` (625), and smaller families. All 625 `45 03` payloads repeat field 1 at payload byte offset 9; all 54 `11 00` payloads repeat field 2 at payload byte offset 11.
- [x] Tie hardcoded `80 bc` constants back to corpus layout families.
  - Current generated report shows `45 03` has four hardcoded examples and one corpus-backed field group with 105 hits; `45 00` has 18 hardcoded examples, seven corpus-backed field groups, and 28 total field-group hits; `15 00` has 13 hardcoded examples and one corpus-backed field group with 23 hits.
- [ ] Validate `80 bc` field 1 interaction tracker candidates against paired NPC, vendor, inventory, trade, and loot captures.
  - Current candidate labels include `BuyActiveTracker`, `TalkActiveTracker`, `GiverActiveTracker`, `TakerActiveTracker`, `PutActiveTracker`, `SecureTradeFlag`, and `CurrentState`.
- [ ] Semantically decode `80 bc` field families and map them to object attributes, bonus state, or gameplay actions.
- [x] Extend packet tooling to classify protocol 03 object/state packet families, not only protocol headers.
  - Current generated report parses 5,062 protocol 03 object messages, fully segments 1,472 messages, and identifies 5,463 known selector segments across movement, object state, spawn/self-view, emote/effect, delete-view, and static object/door families.
- [x] Extend protocol 03 tooling from first object-view signature classification into conservative object-message segmentation and known per-selector payload sizing.
- [x] Treat protocol 03 selector `00` as a one-byte state marker.
  - This counted 696 selector `00` segments and raised full protocol 03 segmentation from 326 to 990 messages before the secondary movement wrapper pass.
- [x] Decode secondary movement wrappers in two-update protocol 03 object packets.
  - Current generated report counts 570 secondary movement wrapper segments and raises full protocol 03 segmentation from 990 to 1,472 messages.
- [x] Mine external hardcoded protocol 03 object-message examples from MxOEmu forks.
  - Current generated report finds 34 hardcoded protocol 03 examples from local code, Rajko MxOEmu, and the TasteeWheat door/hardline fork.
- [ ] Resolve variable-length protocol 03 spawn/profile, appearance/attribute, effect/emote, static object/door, and unknown selector payloads.
- [ ] Follow up on public Hardline Dreams/RaGEZONE lead for combat packet logs and request logs with object serialization, ability state, FX, and before/after attribute context.
- [ ] Use new packet evidence to implement vendor selling.
