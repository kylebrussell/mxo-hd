# Packet Capture Triage Report

- Source kind: `archive`
- Source input: `https://raw.githubusercontent.com/Vibrantic/mxo-resurgence/master/packets_and_assets/mxopackets2-master/mxopackets2-master/warboy.zip`
- Source archive SHA-256: `3dde9509d4f1b97e09938315dc591acfde2e32c5f4f1fb3153ce738f816f6341`
- Scanned files from: `temporary extraction of public mxopackets2 warboy.zip`
- Generated UTC: `2026-06-06T04:34:10Z`
- Max sample lines per pattern: `5`

Use this report to decide whether received logs are worth importing into `research/packet-dumps/`. Matches are byte-string leads, not decoded packet claims.

## Direction-Fixed Packet Research Follow-Up

After this byte triage, a scratch `tools/PacketResearch` run over the committed corpus plus an external extraction of this archive treated Warboy `Client->WORLD` / `WORLD->Client` frame labels as normal game client/server directions. That produced 1,712,918 packet-like lines across 95 files, including 607,278 protocol 03 object views, 100 framed protocol 04 interaction payloads, 1,187 additional protocol 04 server payload shape samples, and 282 static object correlations after including selector-`ff` `u32 +9` candidates in the static-object candidate load.

The latest full-archive 2026-06-08 refresh reran `scripts/run-warboy-packet-research.sh` against the then-current checked-in corpus plus the same ignored external extraction, writing scratch outputs under `data/experiment_artifacts/packet-corpora/warboy/reports/`. That refreshed report covers 272 packet-like files and 2,681,011 packet-like lines, including 857,696 protocol 03 object views, 429 framed protocol 04 interaction payloads, and 2,466 additional protocol 04 server payload shape samples. Treat those refreshed aggregate counts as authoritative for that full external-archive scratch pass; the older counts above are retained as provenance for the initial direction-fixed follow-up.

A bounded six-file WORLD/GAME-only sanitized derivative from the same public Warboy material is now checked in under `research/packet-dumps/community-intake/20260608T130150Z-hdneo-public-warboy-world-game-sanitized-selected`. That derivative retains high-signal selector-`ff`, static-object, Object599, and mode `06` evidence while dropping AUTH/MARGIN blocks and known session/key marker lines; the full 365 MB extraction remains external.

The refreshed selector-`ff` repeat-list queue now preserves 86 vendor-header windows in the capped sample export: 50 `SERVER_VENDOR_OPEN`, 29 `CLIENT_VENDOR_SELL`, and seven `CLIENT_VENDOR_BUY`. The vendor-region split is 28 `field +9 u32`, 30 `continuation body`, 24 `pre-position body`, and four `repeat entry 1` windows. `Protocol03SelectorFfRepeatListVendorBoundaryPrioritySummaries` now has five current priority groups: deep-body boundary candidates with 28 targets / 29 windows, mid-body boundary candidates with 13 targets / 15 windows, one short-span boundary candidate, modeled field overlaps with five targets / 32 windows, and near-position overlaps with nine targets / nine windows.

The refreshed body-window probe table now has 50 continuation/pre-position rows after excluding modeled packed-field and repeat-entry overlaps. Parser actions suppress 24 rows / 26 windows as packed item/gameobject field collisions, suppress seven rows / eight windows unless new capture context corroborates them, and suppress 19 rows / 20 windows as high-half header-byte collisions; no active body-length-test rows remain from the vendor-looking byte windows. The lower-level offset tables now expose 26 continuation-body rows, 20 pre-position-body rows, and 513 continuation-marker rows covering 29,085 modeled repeat-list candidates and all 86 vendor windows.

The framed protocol 04 interaction payloads split into:

| Header | Kind | Object type | Local type label | Count | Distinct object ids |
| --- | --- | ---: | --- | ---: | ---: |
| `80 c7` | dynamic | 3 | unknown dynamic | 33 | 20 |
| `80 c8` | static | 3 | door | 30 | 8 |
| `80 c8` | static | 0 | environment | 24 | 3 |
| `80 c7` | dynamic | 4 | unknown dynamic | 10 | 4 |
| `80 c7` | dynamic | 2 | subway | 3 | 3 |

The scratch report found three Warboy static interaction object ids that also appear in tracked `data/vendor_items.csv` rows:

| Static object id | Packet interactions | Local type label | Static table lead | Vendor row |
| ---: | ---: | --- | --- | --- |
| 277874480 | 14 | environment | `ElevatorPanel` in sector `2/265` | `data/vendor_items.csv:2` |
| 277874441 | 11 | door | `Door Hell Club Elevator` in sector `2/265` | `data/vendor_items.csv:3` |
| 959447528 | 2 | environment | `Stance Site Invis Bench` in sector `1/915` | `data/vendor_items.csv:8` |

These are stronger vendor-path leads than raw byte signatures because they join framed static interactions to static object rows and vendor inventory rows. They are still not decoded vendor window or buy/sell exchanges: no Warboy top-level `CLIENT_VENDOR_BUY`, `CLIENT_VENDOR_SELL`, or `SERVER_VENDOR_OPEN` RPC has been proven yet, and the vendor header byte candidates remain mostly embedded in protocol 03 object/state frames or unknown packet bodies.

The follow-up report also shows the strongest embedded `SERVER_VENDOR_OPEN` byte window belongs to unresolved protocol 03 selector-`ff` body data, not a clean nested RPC boundary. The repeated selector-`ff` family carries `e0 2d 81 0d` at payload offset `+9` and a conservative 24-byte little-endian xyz triple at payload offset `+51`. The largest coordinate cluster is `17595.543,495,2447.504`, with nearest decoded-position hints at player `Se7` (85.4 units) and world row `Zion Duelist` (102.2 units); the alternate cluster is `17615.635,495,1816.967`, with nearest player hints `ricky303` (43.7 units) and `Tsurai` (96.9 units). The structured post-position grouping shows all 27,716 Warboy position leads with suffix bytes have top groups shaped as post-position `+0 = ff`, ten zero bytes, and a little-endian `u16` at first nonzero offset `+11`. The repeat-stride scan shows 25,393 of those suffixes expose at least two `ff` markers at a consistent stride, with stride `18` dominating the visible top groups, and 25,245 repeat tails expose a local field inside entry 2. The largest first-`0c` repeat groups keep `ba 13 / 5050` as the first record field, then split into second-entry zero-run 5 / local field `+6 = 66 00 / 102` across 1,133 records and a broader zero-run 0 / local field `+1 = ff 00 / 255` family across 708 records. The repeat-entry transition table pairs all 25,393 repeat-stride tails, and the matrix adds 50,786 observations across the first two consecutive entries, showing entry 1 preserves the first-record local field while entry 2 carries the `66 00`, `ff 00`, and smaller variant buckets. The continuation table shows every paired tail continues at offset `36`; visible prefixes such as `00 41 00 ff`, `00 00 00 ff`, `00 01 00 ff`, and `00 01 04 ff` point to a two-entry list followed by a separate continuation body. The continuation-field table confirms all 25,393 continuation bodies with at least four bytes have local byte `+3 = ff` and exposes follow-up u16 windows at `+4`, `+6`, `+8`, `+10`, `+12`, and `+14`, including `00 41 00 ff` groups with `b0 74 / 29872` at `+8` and `08 00 / 8` at `+14`, `00 00 00 ff` groups that often stay zero until `+14 = 08 00 / 8`, and `00 01 00 ff` / `00 01 04 ff` variants with nonzero mid-body fields. The marker-tail table treats local `+3 = ff` as a candidate marker and finds post-marker u16 fields in all 25,393 bodies after full-continuation parsing, split into immediate `+4`, four-zero `+8`, and ten-zero `+14` field shapes. The prefix-family table collapses those marker bodies into 532 global families; the largest are `00 01 00 ff` immediate `+4 = f0 0f / 4080` (912 bodies), `00 01 00 ff` immediate `+4 = fd 0f / 4093` variants (714 and 692 bodies), `00 01 04 ff` four-zero `+8 = f2 22 / 8946` (666 bodies), `00 01 10 ff` four-zero `+8 = 12 c9 / 51474` (598 bodies), and `00 01 04 ff` immediate `+4 = f7 0f / 4087` (548 bodies). The repeat-list candidate table confirms all 25,393 marker continuations have exactly two stride-18 entries before continuation offset `36` and expose post-marker u16 fields; top combined shapes include `ba 13 -> ff 00 -> 00 01 00 ff/+4 f0 0f` (912), `ba 13 -> 66 00 -> 00 01 04 ff/+8 f2 22` (666), `01 00 -> ff 33 -> 00 01 00 ff/+4 fd 0f` (644), `7a 14 -> 66 00 -> 00 01 04 ff/+4 f7 0f` (444), and `ba 13 -> 66 00 -> 00 00 00 ff/+8 3f 24` (436). The global field-value summary adds the original role-level view: entry 1 is led by `ba 13 / 5050` and `01 00 / 1`, entry 2 by `ff 00 / 255` and `66 00 / 102`, and the continuation post-marker by `fd 0f / 4093`, `f2 22 / 8946`, `3f 24 / 9279`, and `f0 0f / 4080`. The JSON now exposes those role-level families as 2,621 `Protocol03SelectorFfRepeatListFieldSummaries` groups for downstream tooling, split into 1,857 entry-1, 245 primary entry-2, 298 effective entry-2, and 221 post-marker groups, with imported resource references on 2,422 groups. The top entry-1 reference families are clothing item/game-object IDs with animation-symbol collisions, led by `ba 13 / 5050` (`Fclothing_Shirt_A13_C7`, "Orange/Yellow Sif Leather Fighting Dress", and `NPCDJAtTurnTable_Idle4`) and `7a 14 / 5242` (`Fclothing_Shirt_A19_C26`, "Deep Magenta Knight Leather V-Neck Top", and `AD_ADLb_Aggro`). The new effective entry-2 role keeps `66 00 / 102` visible as both 5,064 zero-padded primary observations and 4,679 secondary-sourced marker-adjacent observations, while preserving primary `ff 00 / 255` as marker evidence. Post-marker leaders split between animation refs (`fd 0f / 4093`, `f0 0f / 4080`) and clothing item/game-object refs (`f2 22 / 8946`, `3f 24 / 9279`). Treat this as a selector-body anchor and candidate-label set for object-state boundary work, not a vendor RPC decode.

The same scratch JSON now exports 3,567 `Protocol03SelectorFfRepeatListShapeSummaries`, each grouping the full entry-1 -> entry-2 -> continuation -> post-marker shape and carrying at least one imported resource reference. The top resource-labeled shapes are `ba 13 / 5050 -> ff 00 / 255 -> 00 01 00 ff -> f0 0f / 4080` (912), `ba 13 / 5050 -> 66 00 / 102 -> 00 01 04 ff -> f2 22 / 8946` (666), `01 00 / 1 -> ff 33 / 13311 -> 00 01 00 ff -> fd 0f / 4093` (644), `7a 14 / 5242 -> 66 00 / 102 -> 00 01 04 ff -> f7 0f / 4087` (444), and `ba 13 / 5050 -> 66 00 / 102 -> 00 00 00 ff -> 3f 24 / 9279` (436). The normalized `Protocol03SelectorFfRepeatListEffectiveShapeSummaries` export has 3,662 Warboy groups and keeps primary/secondary source counts visible. Its leading rows are `ba 13 / 5050 -> 66 00 / 102 -> 00 01 00 ff -> f0 0f / 4080` (878, all secondary-sourced from primary `ff 00`), `ba 13 / 5050 -> 66 00 / 102 -> 00 01 04 ff -> f2 22 / 8946` (666 primary-sourced), `7a 14 / 5242 -> 66 00 / 102 -> 00 01 04 ff -> f7 0f / 4087` (444 primary-sourced), and `ba 13 / 5050 -> 66 00 / 102 -> 00 01 00 ff -> 3f 24 / 9279` (438 mixed: 346 primary, 92 secondary). The new field-layout export separates Warboy entry 2 into 11,997 zero-padded u16 candidates, 10,754 marker-adjacent u16 candidates, 2,494 immediate u16 candidates, and 148 no-field candidates, which keeps the `ff 00` family visible as a marker-adjacent window instead of mixing it with padded fields. The layout-prefix roll-up adds 99 Warboy groups; the largest is long-zero entry 1 / marker-adjacent entry 2 / `00 01 00 ff` / immediate post-marker with 3,308 candidates across 235 exact shapes. The secondary-slot export shows 15,047 Warboy entry-2 records have a nonzero local `+6` field; top pairs are `ff 00 -> 66 00` (3,179), `ff 22 -> 66 00` (843), `ff 00 -> 5a 0a` (710), and `ff 00 -> 44 00` (686). The effective-field export now uses that secondary slot for marker-adjacent entry-2 layouts when present: Warboy has 10,754 secondary-sourced effective entry-2 fields, making `66 00 / 102` the top normalized entry-2 value with 9,743 observations, including 4,679 marker-adjacent observations. Treat these as stable selector-body labels for boundary tests, not decoded field semantics.

The new `Protocol03SelectorFfRepeatListEffectiveShapeHeaderSummaries` export joins those normalized shapes to known local RPC-header byte windows. Warboy has 892 effective-shape groups with known-header evidence covering 2,843 candidate instances, and 81 candidate instances across those groups have vendor-header windows. The top vendor-correlated shape is `ba 13 / 5050 -> 21 0a / 2593 -> 00 41 00 ff -> b0 74 / 29872`: all 12 candidates carry `SERVER_VENDOR_OPEN` at selector-payload `+11` / object `+29` inside the four-byte window `e0 2d 81 0d`. Another priority shape is `ba 13 / 5050 -> 66 00 / 102 -> 00 00 00 ff -> 1b ee / 60955`, with all nine candidates carrying vendor windows led by `CLIENT_VENDOR_SELL` at `81 11`. The companion `Protocol03SelectorFfRepeatListHeaderNameSummaries` export inverts those rows by local RPC header name: Warboy has 89 header-name rollups and three vendor rollups, with `SERVER_VENDOR_OPEN` at 49 candidates / 49 windows / 15 shapes, `CLIENT_VENDOR_SELL` at 27 / 27 / 11, and `CLIENT_VENDOR_BUY` at 5 / 5 / 5. These rows connect the stride/list body model to the vendor byte evidence, but they remain byte-window boundary targets rather than proof of nested vendor RPCs.

The companion `Protocol03SelectorFfRepeatListHeaderSamples` export keeps bounded byte slices for parser work. The Warboy scratch JSON is capped at 120 samples and includes all 81 vendor de-duplicated windows: 49 `SERVER_VENDOR_OPEN`, 27 `CLIENT_VENDOR_SELL`, and five `CLIENT_VENDOR_BUY`. Each sample includes the source file/line, payload and object offsets, `HeaderRegion`, header context bytes, repeat-list context bytes, normalized shape fields, and decoded position anchors. Region classification splits those vendor samples into 28 `field +9 u32`, 27 `continuation body`, 22 `pre-position body`, and four `repeat entry 1` hits.

The new `Protocol03SelectorFfRepeatListHeaderRegionSummaries` export collapses those byte samples into queryable header/region groups. Warboy has 207 total header-region rows and 10 vendor rows, preserving the 81 vendor windows as four `SERVER_VENDOR_OPEN` region rows, three `CLIENT_VENDOR_SELL` rows, and three `CLIENT_VENDOR_BUY` rows. The region rows now also attach weighted resource-reference labels for entry 1, effective entry 2, and post-marker fields: all 10 vendor rows have entry-1 and entry-2 refs, and 9 have post-marker refs. The largest single vendor bucket is `SERVER_VENDOR_OPEN` in `field +9 u32` with 28 candidate windows, entry-2 ref `21 0a / 2593`, and post-marker refs to `Shield_F_Coat_A4_C5_5` / "Shielded Green Farrel Cinch Jacket Highcollar".

The new `Protocol03SelectorFfRepeatListContinuationMarkerSummaries` export makes the continuation marker/body split machine-readable and cross-links each marker family to known-header byte windows. Warboy has 405 marker rows covering all 25,393 modeled two-entry selector-`ff` candidates and all 3,662 effective shapes; every row uses marker byte `ff`. Prefix-before-marker `00 01 00` dominates with 15,228 candidates, followed by `00 01 10` (3,842), `00 00 00` (2,909), and `00 01 04` (1,441). Known-header evidence appears in 232 marker rows covering 2,843 candidates; vendor-header evidence appears in 22 rows and covers all 81 vendor windows. The marker rows now also keep weighted item, game-object, animation, and ability reference labels for entry 1, effective entry 2, and post-marker fields: 384 Warboy rows have entry-1 refs, 386 have effective entry-2 refs, and 339 have post-marker refs. The export now includes vendor-only resource labels too: 21 of the 22 vendor-bearing rows have vendor-only entry-1 refs, 21 have vendor-only effective entry-2 refs, and 16 have vendor-only post-marker refs. The strongest vendor-bearing row is `00 41 00 ff` with immediate post-marker `b0 74`: it has 25 candidates, 19 `SERVER_VENDOR_OPEN` windows, vendor-only effective entry-2 ref `21 0a / 2593`, and vendor-only post-marker refs to `Shield_F_Coat_A4_C5_5` / "Shielded Green Farrel Cinch Jacket Highcollar". `00 01 00 ff -> b0 74` has 101 candidates, 16 vendor windows, vendor-only entry-1 refs led by `NPCVendorArchStandOutdoor_GatherRound` / `Fclothing_Shirt_A12_C13`, and the same post-marker refs; `00 00 00 ff -> 1b ee` has nine `CLIENT_VENDOR_SELL` windows with vendor-only entry refs `ba 13 / 5050 -> 66 00 / 102`; `00 01 00 ff -> fd 0f` has six vendor windows and the `Aggro_StandMovementDisruption_WalkF` post-marker animation ref. These labels are search/debug anchors, not final field semantics.

## Vendor And Interaction Leads

### SERVER_VENDOR_OPEN candidate

Signature: `81 0d` (spaced or compact hex)

Matches: 207

```text
warboy/ProxyM2/m2_packets6.log:67372:02 03 0f 00 02 80 80 80 08 81 0d 00 03 2a 27 c0 00 80 85 00 01 fb 49 a4 47 00 00 be 42 f5 a4 59
warboy/ProxyM2/m2_packets.log:6731:e0 2d 81 0d 22 89 20 00 0b 0c 11 79 80 00 00 8a 01 bd 00 00 00 20 94 a0 d1 40 00 00 00 80 00 be
warboy/ProxyM2/m2_packets.log:17422:00 00 40 f2 5d 99 40 ba 13 fd 43 00 00 00 fb ff 9a 9f 07 00 b0 74 00 00 e0 2d 81 0d 22 89 20 00
warboy/ProxyM2/m2_packets.log:21613:1e e3 44 ff 01 43 00 00 00 00 00 00 00 e0 2d 81 0d 22 89 20 00 0b 0c 11 79 80 00 00 01 00 00 00
warboy/ProxyM2/m2_packets.log:21650:89 46 00 80 f7 43 f2 1e e3 44 ff 01 43 00 00 00 00 00 00 00 e0 2d 81 0d 22 89 20 00 0b 0c 11 79
```

### CLIENT_VENDOR_BUY candidate

Signature: `81 0e` (spaced or compact hex)

Matches: 105

```text
warboy/ProxyM2/m2_packets_5.log:120:11 af 10 b6 f9 d2 d1 97 b8 c3 a7 ef f9 8a 59 8e 0a 8d d9 8f cb 24 64 a8 c9 81 0e b1 ac 70 c0 6d
warboy/ProxyM2/m2_packets4.log:32173:81 0e 08 a9 8e 00 01 2e 4c 40 00 00 00 10 00 a5 fa c8 ea c6 00 48 99 c5 a8 4a d2 44 6c 00 01 2a
warboy/ProxyM2/m2_packets4.log:66991:80 4a 88 42 90 00 01 0c 7a 60 37 d7 c6 01 0b 9d c5 a0 81 0e 44 84 00 01 0c 7a 6d 54 ee c6 2d 5c
warboy/ProxyM2/m2_packets4.log:69386:40 81 0e c3 66 00 01 0e 04 26 45 97 ee c6 8c ea 99 c5 30 25 51 44 3a 00 01 0c 5f d2 3d e5 c6 00
warboy/ProxyM2/m2_packets4.log:201387:00 00 5d 1c 01 00 00 00 08 00 12 00 00 00 00 00 00 01 00 62 00 01 0c 81 0e 8f e8 c6 00 48 99 c5
```

### CLIENT_VENDOR_SELL candidate

Signature: `81 11` (spaced or compact hex)

Matches: 112

```text
warboy/ProxyM2/m2_packets4.log:15755:02 03 2f 00 01 0e 00 41 81 11 e5 c6 00 48 99 c5 12 2b b0 c5 0a 00 02 0e 00 ff aa 70 e8 c6 00 48
warboy/ProxyM2/m2_packets4.log:15777:02 03 2f 00 01 2e 4c c0 00 21 eb 03 00 41 81 11 e5 c6 00 48 99 c5 12 2b b0 c5 0a 00 02 0e 00 ff
warboy/ProxyM2/m2_packets4.log:15789:02 03 2f 00 01 2a 4c c0 00 21 eb 03 00 81 11 e5 c6 00 48 99 c5 12 2b b0 c5 0a 00 02 2a 01 40 00
warboy/ProxyM2/m2_packets4.log:15801:02 03 2f 00 01 2a 4c c0 00 21 eb 03 00 81 11 e5 c6 00 48 99 c5 12 2b b0 c5 0a 00 02 2a 01 40 00
warboy/ProxyM2/m2_packets.log:32820:d9 83 46 00 80 f7 43 81 11 18 45 00 00
```

### CR2 static object interaction

Signature: `80 c8` (spaced or compact hex)

Matches: 687

```text
warboy/ProxyM1/m1_packets.log:751:04 80 c8 00 01 44 05 80 c9 00 06 dc 11 84 ca 00 01 0c 01 80 cb 00 01 b0 06 80 cc 00 01 c4 03 80
warboy/ProxyM1/m1_packets4.log:751:04 80 c8 00 01 44 05 80 c9 00 06 dc 11 84 ca 00 01 0c 01 80 cb 00 01 b0 06 80 cc 00 01 c4 03 80
warboy/ProxyM1/m1_packets4.log:14791:11 0a 08 00 4e c0 00 00 8a 01 dd 00 00 00 b6 4d 5a d2 40 1a 92 ce 8e 10 ed a1 40 00 00 00 80 c8
warboy/ProxyM1/m1_packets4.log:19777:80 10 00 38 47 c7 00 80 13 c4 00 94 8e c7 00 89 01 03 80 fc 02 00 8e 01 08 80 c8 88 06 e0 4b 03
warboy/ProxyM1/m1_packets4.log:19873:80 10 00 38 47 c7 00 80 13 c4 00 94 8e c7 00 89 01 03 80 fc 02 00 8e 01 08 80 c8 88 06 e0 4b 03
```

### CR1 static object interaction

Signature: `80 c3` (spaced or compact hex)

Matches: 1187

```text
warboy/ProxyM1/m1_packets.log:697:ba 01 32 10 02 80 bb 01 32 20 04 80 bc 01 01 28 04 80 c3 01 00 24 00 80 f4 01 00 3c 00 80 00 02
warboy/ProxyM1/m1_packets.log:750:32 04 02 80 c3 00 01 fc 05 80 c4 00 32 94 01 80 c5 00 01 34 02 80 c6 00 01 3c 05 80 c7 00 32 34
warboy/ProxyM1/m1_packets4.log:697:ba 01 32 10 02 80 bb 01 32 20 04 80 bc 01 01 28 04 80 c3 01 00 24 00 80 f4 01 00 3c 00 80 00 02
warboy/ProxyM1/m1_packets4.log:750:32 04 02 80 c3 00 01 fc 05 80 c4 00 32 94 01 80 c5 00 01 34 02 80 c6 00 01 3c 05 80 c7 00 32 34
warboy/ProxyM1/m1_packets4.log:6312:08 00 00 00 00 00 00 22 00 00 00 00 00 00 00 04 01 01 4f 01 06 80 c3 66 4d df 0b
```

### SERVER object/state bundle

Signature: `80 bc` (spaced or compact hex)

Matches: 31339

```text
warboy/ProxyM1/m1_packets.log:697:ba 01 32 10 02 80 bb 01 32 20 04 80 bc 01 01 28 04 80 c3 01 00 24 00 80 f4 01 00 3c 00 80 00 02
warboy/ProxyM1/m1_packets.log:748:05 80 b8 00 01 40 04 80 b9 00 92 da 4e 84 ba 00 20 f4 01 80 bb 00 32 38 02 80 bc 00 32 d8 03 80
warboy/ProxyM1/m1_packets.log:796:07 80 b8 01 01 50 08 80 b9 01 32 4c 08 80 ba 01 32 10 02 80 bb 01 32 20 04 80 bc 01 01 28 04 80
warboy/ProxyM1/m1_packets.log:960:1e 00 08 02 08 80 b2 51 00 12 00 08 02 08 80 b2 11 00 32 00 08 02 16 80 bc 45 03 11 00 00 02 00
warboy/ProxyM1/m1_packets.log:961:00 00 11 00 32 00 00 00 00 00 00 00 00 16 80 bc 45 00 02 00 00 02 00 00 00 cc 00 0c 00 00 00 00
```

## Selector ff Vendor Body Leads

### Selector ff vendor field +9 family

Signature: `e0 2d 81 0d` (spaced or compact hex)

Matches: 80

```text
warboy/ProxyM2/m2_packets.log:6731:e0 2d 81 0d 22 89 20 00 0b 0c 11 79 80 00 00 8a 01 bd 00 00 00 20 94 a0 d1 40 00 00 00 80 00 be
warboy/ProxyM2/m2_packets.log:17422:00 00 40 f2 5d 99 40 ba 13 fd 43 00 00 00 fb ff 9a 9f 07 00 b0 74 00 00 e0 2d 81 0d 22 89 20 00
warboy/ProxyM2/m2_packets.log:21613:1e e3 44 ff 01 43 00 00 00 00 00 00 00 e0 2d 81 0d 22 89 20 00 0b 0c 11 79 80 00 00 01 00 00 00
warboy/ProxyM2/m2_packets.log:21650:89 46 00 80 f7 43 f2 1e e3 44 ff 01 43 00 00 00 00 00 00 00 e0 2d 81 0d 22 89 20 00 0b 0c 11 79
warboy/ProxyM2/m2_packets.log:21725:43 f2 1e e3 44 ff 01 43 00 00 00 00 00 00 00 e0 2d 81 0d 22 89 20 00 0b 0c 11 79 80 00 00 01 00
```

### Selector ff vendor body prefix

Signature: `01 43 00 00 00 00 00 00 00 e0 2d 81 0d 22 89 20` (spaced or compact hex)

Matches: 42

```text
warboy/ProxyM2/m2_packets.log:21613:1e e3 44 ff 01 43 00 00 00 00 00 00 00 e0 2d 81 0d 22 89 20 00 0b 0c 11 79 80 00 00 01 00 00 00
warboy/ProxyM2/m2_packets.log:21650:89 46 00 80 f7 43 f2 1e e3 44 ff 01 43 00 00 00 00 00 00 00 e0 2d 81 0d 22 89 20 00 0b 0c 11 79
warboy/ProxyM2/m2_packets.log:21725:43 f2 1e e3 44 ff 01 43 00 00 00 00 00 00 00 e0 2d 81 0d 22 89 20 00 0b 0c 11 79 80 00 00 01 00
warboy/ProxyM2/m2_packets.log:194183:c4 ff 01 43 00 00 00 00 00 00 00 e0 2d 81 0d 22 89 20 00 0b 0c 11 79 80 00 00 01 00 00 00 00 1b
warboy/ProxyM2/m2_packets.log:194207:00 12 88 f8 c6 00 e0 88 44 e0 4f a7 c4 ff 01 43 00 00 00 00 00 00 00 e0 2d 81 0d 22 89 20 00 0b
```

### Selector ff continuation prefix 00 41 00 ff

Signature: `00 41 00 ff` (spaced or compact hex)

Matches: 377

```text
warboy/ProxyM2/m2_packets6.log:20888:04 1c 00 00 00 00 00 00 00 00 41 00 ff 00 00 00 00 3f 24 01 00 00 00 08 00 12 01 4a 01 00 00 00
warboy/ProxyM2/m2_packets6.log:20989:00 00 00 00 00 00 00 41 00 ff 00 00 00 00 3f 24 01 00 00 00 08 00 12 01 4a 01 00 00 00 01 00 00
warboy/ProxyM2/m2_packets6.log:21037:00 00 00 00 66 00 00 04 1c 00 00 00 00 00 00 00 00 41 00 ff 00 00 00 00 3f 24 01 00 00 00 08 00
warboy/ProxyM2/m2_packets6.log:21105:00 00 00 66 00 00 04 1c 00 00 00 00 00 00 00 00 41 00 ff 00 00 00 00 3f 24 01 00 00 00 08 00 12
warboy/ProxyM2/m2_packets6.log:21136:00 41 00 ff 00 00 00 00 3f 24 01 00 00 00 08 00 12 01 4a 01 00 00 00 01 00 00 00
```

### Selector ff continuation prefix 00 01 00 ff

Signature: `00 01 00 ff` (spaced or compact hex)

Matches: 45946

```text
warboy/ProxyM1/m1_packets4.log:6079:00 01 00 ff b8 02 00 04 47 1c 01 00 00 00 00 00 22 02 7e 00 00 00 00 01 00 00 00
warboy/ProxyM2/m2_packets_5.log:5577:00 00 00 00 00 00 00 00 01 00 ff aa 04 00 04 d9 2f 00 00 02 00 10 00 22 01 7e 00 00 00 00 01 00
warboy/ProxyM2/m2_packets_5.log:5652:00 a8 13 00 01 1c 00 00 ff 00 00 00 00 00 d7 30 46 12 1e 00 00 00 00 00 00 00 00 01 00 ff aa 04
warboy/ProxyM2/m2_packets_5.log:5715:00 00 00 00 00 00 01 00 ff aa 04 00 04 d9 2f 00 00 02 00 10 00 22 01 7e 00 00 00 00 01 00 00 00
warboy/ProxyM2/m2_packets_5.log:24780:00 00 00 00 00 00 00 01 00 ff e6 05 00 28 2f 1b 01 00 00 00 00 00 22 02 f6 01 00 00 00 01 00 05
```

### Selector ff continuation prefix 00 01 04 ff

Signature: `00 01 04 ff` (spaced or compact hex)

Matches: 2127

```text
warboy/ProxyM2/m2_packets6.log:13951:00 19 31 46 12 27 00 00 00 00 00 00 00 00 01 04 ff 00 00 00 00 00 00 00 00 00 00 00 00 22 00 00
warboy/ProxyM2/m2_packets6.log:13988:00 00 ba 13 00 00 00 00 00 ff 00 00 00 00 00 19 31 46 12 27 00 00 00 00 00 00 00 00 01 04 ff 00
warboy/ProxyM2/m2_packets6.log:14437:00 00 00 00 00 00 00 00 01 04 ff 00 00 00 00 00 00 00 00 00 00 00 00 22 00 00 00 00 00 00 01 00
warboy/ProxyM2/m2_packets6.log:14470:31 46 12 27 00 00 00 00 00 00 00 00 01 04 ff 00 00 00 00 00 00 00 00 00 00 00 00 22 00 00 00 00
warboy/ProxyM2/m2_packets6.log:16021:00 00 00 01 04 ff 00 00 00 00 00 00 00 00 00 00 00 00 22 00 00 00 00 00 00 01 00 25 01 03 28 af
```

### Selector ff continuation prefix 00 01 10 ff

Signature: `00 01 10 ff` (spaced or compact hex)

Matches: 12141

```text
warboy/ProxyM2/m2_packets_5.log:20183:00 20 00 00 04 c7 00 00 00 00 00 00 00 00 01 10 ff e6 05 00 28 a2 fc 00 00 00 00 18 00 11 00 00
warboy/ProxyM2/m2_packets_5.log:20439:00 00 00 01 10 ff e6 05 00 28 a2 fc 00 00 00 00 18 00 11 00 00 00 00 00 00 01 00 12 00 06 0e 03
warboy/ProxyM2/m2_packets_5.log:20491:00 00 20 00 00 04 c8 00 00 00 00 00 00 00 00 01 10 ff e6 05 00 28 a2 fc 00 00 00 00 18 00 11 00
warboy/ProxyM2/m2_packets_5.log:21064:00 20 00 00 04 c8 00 00 00 00 00 00 00 00 01 10 ff e6 05 00 28 a2 fc 00 00 00 00 18 00 11 00 00
warboy/ProxyM2/m2_packets_5.log:21488:00 20 00 00 04 c9 00 00 00 00 00 00 00 00 01 10 ff e6 05 00 28 a2 fc 00 00 00 00 18 00 11 00 00
```

### Selector ff continuation prefix 00 00 00 ff

Signature: `00 00 00 ff` (spaced or compact hex)

Matches: 123627

```text
warboy/ProxyM1/m1_packets.log:3281:00 03 ff 00 00 00 00 00 00 00 00 c8 00 00 00 00 00 06 00 00 00 00 00 00 ff 00 00 00 00 00 00 00
warboy/ProxyM1/m1_packets.log:3283:00 00 00 00 00 00 00 00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00 00 00 00 ac 01 00 58 37 08 00
warboy/ProxyM1/m1_packets.log:3357:00 00 03 ff 00 00 00 00 00 00 00 00 c8 00 00 00 00 00 06 00 00 00 00 00 00 ff 00 00 00 00 00 00
warboy/ProxyM1/m1_packets.log:3359:00 00 00 00 00 00 00 00 00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00 00 00 00 ac 01 00 58 37 08
warboy/ProxyM1/m1_packets4.log:4718:00 00 03 ff 00 00 00 00 00 00 00 00 af 00 00 00 00 00 05 00 00 00 00 00 00 ff 00 00 00 00 00 00
```

### Vendor-open continuation resource shape

Signature: `00 41 00 ff 00 00 00 00 b0 74` (spaced or compact hex)

Matches: 54

```text
warboy/ProxyM1/m1_packets3.log:428589:00 00 00 41 00 ff 00 00 00 00 b0 74 00 00 00 00 08 00 12 02 39 00 00 00 00 01 00 22 00 01 2b 7d
warboy/ProxyM1/m1_packets3.log:428638:0a 00 28 05 00 00 00 00 00 00 00 00 41 00 ff 00 00 00 00 b0 74 00 00 00 00 08 00 12 02 39 00 00
warboy/ProxyM1/m1_packets3.log:428762:00 00 00 00 41 00 ff 00 00 00 00 b0 74 00 00 00 00 08 00 12 02 39 00 00 00 00 01 00 00 00
warboy/ProxyM1/m1_packets3.log:428782:00 00 ff ff 00 00 00 00 21 0a 00 28 05 00 00 00 00 00 00 00 00 41 00 ff 00 00 00 00 b0 74 00 00
warboy/ProxyM1/m1_packets3.log:428824:ff 00 00 00 00 21 0a 00 28 05 00 00 00 00 00 00 00 00 41 00 ff 00 00 00 00 b0 74 00 00 00 00 08
```

### Vendor-sell continuation resource shape

Signature: `00 00 00 ff 00 00 00 00 1b ee` (spaced or compact hex)

Matches: 13

```text
warboy/ProxyM1/m1_packets3.log:466839:00 00 00 00 00 66 00 00 04 0f 00 00 00 00 00 00 00 00 00 00 ff 00 00 00 00 1b ee 00 00 00 00 00
warboy/ProxyM1/m1_packets3.log:468352:04 12 00 00 00 00 00 00 00 00 00 00 ff 00 00 00 00 1b ee 00 00 00 00 00 00 22 00 00 00 00 00 00
warboy/ProxyM1/m1_packets3.log:468407:12 00 00 00 00 00 00 00 00 00 00 ff 00 00 00 00 1b ee 00 00 00 00 00 00 22 00 00 00 00 00 00 01
warboy/ProxyM1/m1_packets3.log:491443:00 00 00 00 00 ff 00 00 00 00 1b ee 00 00 00 00 00 00 22 00 00 00 00 00 00 01 00 18 00 01 08 65
warboy/ProxyM1/m1_packets3.log:493192:3d 00 00 00 00 00 00 00 00 00 00 ff 00 00 00 00 1b ee 00 00 00 00 00 00 22 00 00 00 00 00 00 01
```

## Object599 / NPC_BASE Spawn-Profile Leads

### Object599 creation prefix

Signature: `0c 57 02` (spaced or compact hex)

Matches: 3321

```text
warboy/ProxyM1/m1_packets.log:2588:02 03 02 00 02 80 80 80 80 80 10 11 00 00 00 01 00 0c 57 02 78 cd ab 13 8e 42 65 6c 6c 20 4a 61
warboy/ProxyM1/m1_packets.log:2658:02 03 01 00 0c 57 02 79 cd ab 11 8e 42 65 6c 6c 20 4d 61 64 6f 6e 6e 61 00 00 00 00 00 00 00 00
warboy/ProxyM1/m1_packets.log:2661:09 2c 01 07 08 00 00 80 02 01 0f 00 00 01 00 0c 57 02 a6 cd ab 18 8e 42 65 6c 6c 20 4d 61 64 6f
warboy/ProxyM1/m1_packets.log:2665:00 00 06 01 35 08 00 00 0d 00 00 01 00 0c 57 02 7a cd ab 11 8e 42 65 6c 6c 20 4d 61 64 6f 6e 6e
warboy/ProxyM1/m1_packets.log:2668:01 8a 02 15 a8 06 2c 01 b2 09 2c 01 07 08 00 00 80 02 01 0b 00 00 01 00 0c 57 02 7b cd ab 13 8e
```

### Adjacent Object599 boundary prefix

Signature: `00 0c 57 02` (spaced or compact hex)

Matches: 3251

```text
warboy/ProxyM1/m1_packets.log:2588:02 03 02 00 02 80 80 80 80 80 10 11 00 00 00 01 00 0c 57 02 78 cd ab 13 8e 42 65 6c 6c 20 4a 61
warboy/ProxyM1/m1_packets.log:2658:02 03 01 00 0c 57 02 79 cd ab 11 8e 42 65 6c 6c 20 4d 61 64 6f 6e 6e 61 00 00 00 00 00 00 00 00
warboy/ProxyM1/m1_packets.log:2661:09 2c 01 07 08 00 00 80 02 01 0f 00 00 01 00 0c 57 02 a6 cd ab 18 8e 42 65 6c 6c 20 4d 61 64 6f
warboy/ProxyM1/m1_packets.log:2665:00 00 06 01 35 08 00 00 0d 00 00 01 00 0c 57 02 7a cd ab 11 8e 42 65 6c 6c 20 4d 61 64 6f 6e 6e
warboy/ProxyM1/m1_packets.log:2668:01 8a 02 15 a8 06 2c 01 b2 09 2c 01 07 08 00 00 80 02 01 0b 00 00 01 00 0c 57 02 7b cd ab 13 8e
```

### Header-like selector 50

Signature: `01 00 08 50` (spaced or compact hex)

Matches: 77

```text
warboy/ProxyM1/m1_packets.log:2643:82 ca bc e0 47 03 01 00 08 50 02 7d 02 b0 4a 48 cd ab 02 09 00 00 00 00 80 e7 ea 40 00 00 00 00
warboy/ProxyM1/m1_packets4.log:4291:af 00 12 08 00 00 80 02 01 0d 00 00 01 00 08 50 02 e7 02 30 19 2a cd ab 02 09 00 00 00 00 40 11
warboy/ProxyM2/m2_packets6.log:1743:02 03 01 00 08 50 02 ea 01 30 39 ef cd ab 01 01 00 00 00 00 00 9a d0 40 00 00 00 00 00 90 80 40
warboy/ProxyM2/m2_packets6.log:26479:08 00 00 81 d4 1e 00 00 02 09 27 00 00 01 00 08 50 02 eb 1f f0 2e f3 cd ab 02 09 00 00 00 00 00
warboy/ProxyM2/m2_packets6.log:57345:00 00 01 00 08 50 02 eb 1f f0 2e f3 cd ab 02 09 00 00 00 00 00 82 c4 c0 00 00 00 00 00 28 9e 40
```

### Header-like selector 67

Signature: `01 00 08 67` (spaced or compact hex)

Matches: 5

```text
warboy/ProxyM1/m1_packets3.log:48995:02 03 01 00 08 67 95 46 00 80 14 c5 cd ab 01 01 00 00 00 00 80 d2 e1 40 00 00 00 00 00 3c 91 c0
warboy/ProxyM1/m1_packets3.log:54578:02 03 01 00 08 67 95 46 00 80 14 c5 cd ab 01 01 00 00 00 00 80 d2 e1 40 00 00 00 00 00 3c 91 c0
warboy/ProxyM1/m1_packets3.log:66319:02 03 01 00 08 67 01 26 00 60 12 74 cd ab 02 80 11 00 00 00 00 80 f8 e3 40 00 00 00 00 00 88 8d
warboy/ProxyM1/m1_packets3.log:66455:82 79 c4 ec 47 03 02 00 02 80 04 2d 01 00 08 67 95 46 00 80 14 c5 cd ab 01 01 00 00 00 00 80 d2
warboy/ProxyM1/m1_packets3.log:66840:00 01 00 08 67 01 27 00 60 12 d8 cd ab 02 80 11 00 00 00 00 80 f8 e3 40 00 00 00 00 00 88 8d 40
```

### Header-like selector 68

Signature: `01 00 08 68` (spaced or compact hex)

Matches: 3

```text
warboy/ProxyM1/m1_packets3.log:48699:01 00 08 68 95 44 00 80 14 ad cd ab 02 09 00 00 00 00 80 fe e2 40 00 00 00 00 00 40 91 c0 00 00
warboy/ProxyM1/m1_packets3.log:55186:01 00 08 68 95 44 00 80 14 ad cd ab 02 09 00 00 00 00 80 fe e2 40 00 00 00 00 00 40 91 c0 00 00
warboy/ProxyM1/m1_packets3.log:66186:00 00 00 00 d9 c2 c0 00 00 00 00 f2 6b dc 3e 00 00 00 00 d4 0f 67 3f 0e 00 00 01 00 08 68 95 44
```

### Header-like selector 69

Signature: `01 00 08 69` (spaced or compact hex)

Matches: 7

```text
warboy/ProxyM1/m1_packets3.log:17052:01 8b 25 01 16 a8 04 2c 01 b2 04 2c 01 38 21 00 00 80 02 01 1d 00 00 01 00 08 69 09 3f 0b f0 02
warboy/ProxyM1/m1_packets3.log:48627:b6 00 00 06 0c 35 08 00 00 23 00 00 01 00 08 69 95 43 00 80 14 7a cd ab 02 09 00 00 00 00 40 a7
warboy/ProxyM1/m1_packets3.log:55180:c0 00 00 00 00 c8 26 74 bf 00 00 00 00 1e f6 99 3e 22 00 00 01 00 08 69 95 43 00 80 14 7a cd ab
warboy/ProxyM1/m1_packets3.log:55748:02 03 01 00 08 69 09 32 04 60 12 79 cd ab 02 05 00 00 00 00 40 4e e2 40 00 00 00 00 00 48 93 40
warboy/ProxyM1/m1_packets3.log:64794:02 03 01 00 08 69 09 32 04 60 12 79 cd ab 02 05 00 00 00 00 40 4e e2 40 00 00 00 00 00 48 93 40
```

### Header-like selector 76

Signature: `01 00 0c 76` (spaced or compact hex)

Matches: 0

### Header-like selector 90

Signature: `01 00 08 90` (spaced or compact hex)

Matches: 2

```text
warboy/ProxyM2/m2_packets6.log:26583:02 03 01 00 02 13 00 19 00 02 0c ed 52 73 21 c6 f8 df ec 44 17 fd 60 47 01 00 08 90 05 c4 14 e0
warboy/ProxyM2/m2_packets6.log:57477:00 00 81 d9 1e 00 00 02 09 2c 00 00 01 00 08 90 05 c4 14 e0 11 2e cd ab 01 01 00 00 00 00 00 7b
```

### Header-like selector a3

Signature: `01 00 08 a3` (spaced or compact hex)

Matches: 14

```text
warboy/ProxyM1/m1_packets4.log:4279:02 03 02 00 02 80 80 80 40 df 13 01 00 08 a3 01 2b 01 30 19 b4 cd ab 03 88 00 00 00 00 00 00 80
warboy/ProxyM1/m1_packets4.log:8323:00 00 e1 ff 0c 00 00 01 00 08 a3 01 2b 01 30 19 b4 cd ab 03 88 00 00 00 00 00 00 80 3f 00 00 00
warboy/ProxyM2/m2_packets6.log:82489:47 00 80 fc c3 a7 fa bd c6 01 00 08 a3 01 2b 01 30 19 b4 cd ab 03 88 00 00 00 00 00 00 80 3f 00
warboy/ProxyM2/m2_packets.log:55760:02 03 01 00 08 a3 01 2b 01 30 19 b4 cd ab 03 88 00 00 00 00 00 00 80 3f 00 00 00 00 00 00 00 00
warboy/ProxyM2/m2_packets.log:93260:02 03 02 00 02 80 84 50 80 50 79 01 f4 06 01 00 08 a3 01 2b 01 30 19 b4 cd ab 03 88 00 00 00 00
```

### Header-like selector d0

Signature: `01 00 08 d0` (spaced or compact hex)

Matches: 363

```text
warboy/ProxyM1/m1_packets4.log:13444:12 a8 05 06 01 b2 09 06 01 07 08 00 00 80 02 01 37 00 00 01 00 08 d0 20 ee 01 30 39 c7 cd ab 08
warboy/ProxyM1/m1_packets4.log:13479:15 a8 04 e1 00 b2 09 e1 00 12 08 00 00 80 02 01 33 00 00 01 00 08 d0 20 ef 01 30 39 c8 cd ab 08
warboy/ProxyM1/m1_packets4.log:14700:02 03 01 00 08 d0 20 af 00 20 4e e1 cd ab 08 fd 00 00 00 00 c0 a8 d4 40 00 00 00 00 00 f0 7e 40
warboy/ProxyM1/m1_packets4.log:14911:01 00 04 00 89 98 75 6d 07 58 1c 02 85 00 00 00 f3 ff 3f 00 00 01 00 08 d0 20 b1 00 20 4e c5 cd
warboy/ProxyM2/m2_packets6.log:8250:00 00 f4 90 40 00 00 00 40 6d 3e b4 40 57 a9 4c 09 18 00 00 01 00 08 d0 20 b1 00 20 4e ee cd ab
```

## Movement And Object-State Leads

### Mode 06 movement selector 08

Signature: `00 06 08` (spaced or compact hex)

Matches: 1128

```text
warboy/ProxyM2/m2_packets_5.log:20517:02 03 12 00 06 08 3c 32 f3 c6 00 e0 88 44 a0 c0 0f c5 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00
warboy/ProxyM2/m2_packets_5.log:20667:02 03 12 00 06 08 4c 83 f1 c6 00 e0 88 44 a8 ad 0b c5 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00
warboy/ProxyM2/m2_packets_5.log:20707:02 03 12 00 06 08 4c 83 f1 c6 00 e0 88 44 a8 ad 0b c5 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00
warboy/ProxyM2/m2_packets_5.log:20782:c5 12 00 06 08 4c 83 f1 c6 00 e0 88 44 a8 ad 0b c5 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00
warboy/ProxyM2/m2_packets_5.log:20926:02 03 12 00 06 08 4c 83 f1 c6 00 e0 88 44 a8 ad 0b c5 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00
```

### Mode 06 movement selector 0a

Signature: `00 06 0a` (spaced or compact hex)

Matches: 346

```text
warboy/ProxyM1/m1_packets4.log:13777:22 00 00 00 00 00 00 00 01 00 0c 0c 00 ae cd ab 2d 9f 25 00 06 0a 00 00 0d 44 65 73 63 61 69 6e
warboy/ProxyM2/m2_packets6.log:39456:00 00 04 dd 16 00 03 01 0c 15 80 80 80 82 10 95 00 00 0c 00 00 18 00 11 14 00 06 0a 00 82 50 a0
warboy/ProxyM2/m2_packets_5.log:5911:02 03 19 00 06 0a 00 a4 b5 bc c6 68 29 eb 44 00 20 8a c4 ff 00 00 00 10 00 00 00 00 00 01 ff 00
warboy/ProxyM2/m2_packets_5.log:6266:02 03 17 00 06 0a 00 e9 ba bd c6 50 af e5 44 06 e5 07 c5 ff 00 00 00 10 00 00 00 00 00 01 ff 00
warboy/ProxyM2/m2_packets_5.log:21894:02 03 12 00 06 0a 00 4c 83 f1 c6 00 e0 88 44 a8 ad 0b c5 80 80 80 40 1d 00 00 00
```

### Mode 06 movement selector 0c

Signature: `00 06 0c` (spaced or compact hex)

Matches: 5888

```text
warboy/ProxyM1/m1_packets.log:3333:82 bb be e0 47 03 13 00 06 0c f6 36 3c 57 47 00 00 be 42 10 8a 5a 46 80 80 80 80 80 80 01 11 08
warboy/ProxyM1/m1_packets4.log:4716:02 03 02 00 02 80 80 80 40 03 14 13 00 06 0c 75 5d ea 10 47 00 00 be 42 cc 82 98 c6 ff 00 00 00
warboy/ProxyM1/m1_packets4.log:4775:02 03 13 00 06 0c 6f 5d ea 10 47 00 00 be 42 cc 82 98 c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00
warboy/ProxyM1/m1_packets4.log:4810:02 03 13 00 06 0c 65 5d ea 10 47 00 00 be 42 cc 82 98 c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00
warboy/ProxyM1/m1_packets4.log:4845:02 03 13 00 06 0c 5b 5d ea 10 47 00 00 be 42 cc 82 98 c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00
```

### Mode 06 movement selector 0e

Signature: `00 06 0e` (spaced or compact hex)

Matches: 7217

```text
warboy/ProxyM1/m1_packets4.log:4721:00 00 1f 07 08 00 00 00 00 00 00 22 00 00 00 00 00 0d 00 06 0e 05 45 04 fa 14 47 00 00 be 42 00
warboy/ProxyM1/m1_packets4.log:4780:00 00 22 00 00 00 00 00 0d 00 06 0e 05 3f 04 fa 14 47 00 00 be 42 00 bc 9b c6 ff 00 00 00 10 00
warboy/ProxyM1/m1_packets4.log:4815:00 00 22 00 00 00 00 00 0d 00 06 0e 05 35 04 fa 14 47 00 00 be 42 00 bc 9b c6 ff 00 00 00 10 00
warboy/ProxyM1/m1_packets4.log:5010:02 03 02 00 02 80 80 80 40 1e 14 13 00 06 0e 00 26 5d ea 10 47 00 00 be 42 cc 82 98 c6 ff 00 00
warboy/ProxyM1/m1_packets4.log:5015:08 00 00 1f 07 08 00 00 00 00 00 00 22 00 00 00 00 00 0d 00 06 0e 00 f3 04 fa 14 47 00 00 be 42
```

### Repeated mode 06 suffix lead

Signature: `ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00` (spaced or compact hex)

Matches: 5509

```text
warboy/ProxyM1/m1_packets4.log:4722:bc 9b c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
warboy/ProxyM1/m1_packets4.log:5016:00 bc 9b c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
warboy/ProxyM1/m1_packets4.log:5103:42 00 bc 9b c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
warboy/ProxyM1/m1_packets4.log:5172:00 ef 72 2b 47 00 80 fc c3 8b 04 c6 c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00
warboy/ProxyM1/m1_packets4.log:5567:00 10 00 ef 72 2b 47 00 80 fc c3 8b 04 c6 c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00
```

### Movement-state ff 01 64 01 tail

Signature: `ff 01 64 01` (spaced or compact hex)

Matches: 48

```text
warboy/ProxyM1/m1_packets3.log:642767:00 e0 88 44 86 78 50 45 ff 01 64 01 00 00 00 00 00 00 58 24 42 46 2a 50 d6 26 d0 07 18 87 00 00
warboy/ProxyM1/m1_packets3.log:649237:00 00 10 01 05 00 03 07 08 00 00 46 ff 01 64 01 00 00 00 00 00 00 58 24 42 46 2a 50 d6 26 d0 07
warboy/ProxyM1/m1_packets2.log:31599:43 2f e5 39 44 80 80 80 8c d9 07 00 28 56 08 22 1e 00 03 03 0c 00 00 ff 01 64 01 00 00 00 00 00
warboy/ProxyM1/m1_packets2.log:33872:6d bf 44 57 00 03 28 01 c0 00 98 d9 00 cf 70 83 46 00 80 f7 43 2a 95 0a 45 ff 01 64 01 00 00 00
warboy/ProxyM1/m1_packets2.log:39610:1e 00 03 27 44 81 00 95 67 01 51 00 e2 00 05 00 00 80 ff 01 64 01 00 00 00 00 00 00 57 b4 82 86
```

### Movement-state ff 01 4e 01 tail

Signature: `ff 01 4e 01` (spaced or compact hex)

Matches: 1608

```text
warboy/ProxyM2/m2_packets4.log:56252:00 c8 a5 c5 f0 6b 33 c4 32 00 03 0e 03 1a 33 47 e3 c6 00 c8 a5 c5 b0 47 54 c4 ff 01 4e 01 00 00
warboy/ProxyM2/m2_packets4.log:70909:c8 a5 c5 f0 6b 33 c4 32 00 03 0e 04 bb e0 7c e2 c6 00 c8 a5 c5 b0 fd 8d c4 ff 01 4e 01 00 00 00
warboy/ProxyM2/m2_packets4.log:72226:b0 e8 c6 00 48 99 c5 78 d2 8d 44 ff 01 4e 01 00 00 00 00 00 00 52 34 50 06 08 24 e5 94 00 13 4a
warboy/ProxyM2/m2_packets4.log:73768:c8 a5 c5 f0 6b 33 c4 32 00 03 0e 00 1b 7a e6 ec c6 00 48 99 c5 c0 90 b8 44 ff 01 4e 01 00 00 00
warboy/ProxyM2/m2_packets4.log:82216:10 7c e6 ec c6 00 48 99 c5 70 91 b8 44 ff 01 4e 01 00 00 00 00 00 00 46 34 50 02 08 24 e5 8b 60
```

## Ability, Effect, And Combat-State Leads

### SERVER_PLAYER_ATTRIBUTE / ability load

Signature: `80 b2` (spaced or compact hex)

Matches: 1058

```text
warboy/ProxyM1/m1_packets.log:746:ad 00 01 3c 04 80 ae 00 01 10 05 80 af 00 06 df 11 84 b0 00 01 e0 03 80 b1 00 01 04 06 80 b2 00
warboy/ProxyM1/m1_packets.log:959:08 80 b2 4e 00 1e 00 08 02 08 80 b2 52 00 1e 00 08 02 08 80 b2 54 00 1e 00 08 02 08 80 b2 4f 00
warboy/ProxyM1/m1_packets.log:960:1e 00 08 02 08 80 b2 51 00 12 00 08 02 08 80 b2 11 00 32 00 08 02 16 80 bc 45 03 11 00 00 02 00
warboy/ProxyM1/m1_packets.log:966:00 00 00 76 04 05 00 00 00 00 00 00 00 00 08 80 b2 26 04 00 00 08 02 16 80 bc 45 03 26 04 00 89
warboy/ProxyM1/m1_packets.log:968:00 00 00 00 00 08 80 b2 24 04 00 00 08 02 16 80 bc 45 03 24 04 00 8f 00 00 00 24 04 00 00 00 00
```

### SERVER_ABILITY_UNLOAD

Signature: `80 b3` (spaced or compact hex)

Matches: 1036

```text
warboy/ProxyM1/m1_packets.log:507:00 00 7e b3 00 00 7f b3 00 00 80 b3 00 00 81 b3 00 00 82 b3 00 00 83 b3 00 00 f8 b7 00 00 03 b8
warboy/ProxyM1/m1_packets.log:747:01 94 06 80 b3 00 1e 68 05 80 b4 00 05 db 11 84 b5 00 01 f4 00 80 b6 00 32 34 07 80 b7 00 01 40
warboy/ProxyM1/m1_packets.log:795:01 d0 03 80 b3 01 01 18 04 80 b4 01 01 84 05 80 b5 01 01 50 07 80 b6 01 01 f4 03 80 b7 01 32 c8
warboy/ProxyM1/m1_packets.log:1181:00 00 00 00 00 16 80 bc 56 00 55 00 00 98 b8 00 00 b5 00 29 00 00 00 00 00 00 00 00 04 80 b3 11
warboy/ProxyM1/m1_packets.log:1223:00 00 00 00 00 00 00 00 04 80 b3 26 04 16 80 bc 45 03 26 04 00 89 00 00 00 26 04 00 00 00 00 01
```

### Object599 adrenaline booster effect id

Signature: `4c 0a 00 28` (spaced or compact hex)

Matches: 6657

```text
warboy/ProxyM2/m2_packets_5.log:2308:00 c6 01 80 c3 e7 08 00 58 4c 0a 00 28 00 00 00 a8 d5 8f df c0 00 00 00 00 00 1c 91 40 00 00 00
warboy/ProxyM2/m2_packets_5.log:3898:00 00 00 00 00 00 c1 1e 00 00 4c 0a 00 28 ff 06 00 00 00 00 00 00 00 00 00 00 00 e7 08 00 58 37
warboy/ProxyM2/m2_packets_5.log:3976:00 c1 1e 00 00 4c 0a 00 28 ff 06 00 00 00 00 00 00 00 00 00 00 00 e7 08 00 58 37 08 00 00 1f 12
warboy/ProxyM2/m2_packets_5.log:7689:b3 44 0a 6d 2f c5 80 80 80 80 c0 4c 0a 00 28 81 01 02 00 00 10 00 00 00
warboy/ProxyM2/m2_packets_5.log:7712:00 01 00 5a 0a ff 00 00 00 00 00 00 00 00 22 b6 00 00 4c 0a 00 28 ff 01 00 00 00 00 00 00 00 00
```

### Object599 fast healing effect id

Signature: `b5 07 00 28` (spaced or compact hex)

Matches: 10

```text
warboy/ProxyM1/m1_packets2.log:107436:b5 07 00 28 d4 5b 00 03 2b 66 01 0c 37 81 00 43 00 f9 00 00 00 00 c8 b7 a3 46 00 80 f7 43 8a 6a
warboy/ProxyM1/m1_packets2.log:186890:41 17 21 45 33 00 02 80 80 80 0c b5 07 00 28 54 21 00 01 0c ea 36 07 a6 46 00 80 f7 43 ca f0 b2
warboy/ProxyM1/m1_packets2.log:528251:22 00 00 00 00 08 01 12 01 56 00 00 00 00 01 00 27 00 02 80 80 80 0c b5 07 00 28 eb 20 00 01 0c
warboy/ProxyM1/m1_packets2.log:531424:94 1f 8f 46 00 80 f7 43 f1 31 d0 45 80 80 80 0c b5 07 00 28 08 27 00 01 0a 00 79 d3 90 46 00 80
warboy/ProxyM1/m1_packets2.log:531453:00 00 00 00 00 00 00 00 00 00 b0 13 00 58 89 00 00 ff ff 00 00 00 00 b5 07 00 28 08 00 00 00 00
```

### Object599 accelerated spawn effect id

Signature: `66 00 00 04` (spaced or compact hex)

Matches: 28591

```text
warboy/ProxyM1/m1_packets4.log:14711:40 db 40 b0 bf 60 22 a1 40 00 00 00 40 9d 77 9e c0 ca ff 63 22 fb ff 66 00 00 04 b4 01 01 00 02
warboy/ProxyM1/m1_packets4.log:14792:42 9e c0 d6 ff 63 22 66 00 00 04 b4 01 01 00 02 00 a9 73 d1 5e 07 f0 08 1d d6 1e 00 00 03 11 00
warboy/ProxyM1/m1_packets4.log:14823:00 39 93 b4 d2 40 24 e9 99 88 cb ed a1 40 00 00 00 80 3a 26 9f c0 f7 ff 63 22 fb ff 66 00 00 04
warboy/ProxyM2/m2_packets_5.log:11342:00 00 ff 9b 9d 40 00 00 00 00 84 4e 82 c0 81 ff 63 22 fb ff 66 00 00 04 98 00 57 01 02 01 00 8b
warboy/ProxyM2/m2_packets_5.log:14932:00 1c 91 40 00 00 00 00 86 51 a3 c0 81 ff 63 22 fb ff 66 00 00 04 90 01 00 89 94 75 0d 09 41 1d
```

## Intake Next Steps

- Preserve the original files separately before editing or importing anything.
- Inspect matches with surrounding lines; embedded hits inside encrypted or unrelated bodies are leads only.
- Record sender, provenance, CR1/CR2 status, action context, character handle, coordinates, known object ids, and known item GoIDs.
- Import only text logs or text-like dumps that contain no passwords, session tokens, private keys, client binaries, or unrelated game assets.
