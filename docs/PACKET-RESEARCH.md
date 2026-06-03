# Packet Research

This project now has a repeatable packet-format research workflow instead of relying on manual notes.

## Source Inventory

| Source | Location | Use | Status |
| --- | --- | --- | --- |
| Local Hardline Dreams headers | `hds/servertype/*/NetworkProtocolHeaders.cs` | Current CR1/CR2 RPC request/response names and values | Tracked |
| Local packet logging | `hds/utils/output/Output.cs` | Runtime logs for new unknown RPC/client packets when `MXO_PACKET_LOG=1` | Tracked |
| Community packet dumps | `research/packet-dumps/mxopackets1` | 83 text and text-like `.log*` dumps imported from `Vibrantic/mxo-resurgence` | Tracked |
| MxOEmu FX list | `research/community-data/fxlisthex.txt` | 2,356 public effect IDs for matching four-byte payload fields to symbolic FX names | Tracked |
| MxOEmu public data host | `https://files.mxoemu.info/` | Public object, inventory, ability, FX, `goprops`, Python/resource data, and Discord exports; `goprops.7z` generated tracked vendor prices | Partially tracked |
| Rajko MxOEmu | `https://github.com/rajkosto/mxoemu` | External RPC dispatch map and sequence/encryption handling reference | External clone |
| TasteeWheat MxOEmu fork | `https://github.com/TasteeWheat/mxoemu1` | External door/hardline fork with hardcoded protocol 03 object/spawn packet constants and raw `HandleCommand` debug lines | External clone |
| MxOEmu forum thread | `https://mxoemu.info/forum/showthread.php?tid=2801` | Lead for source and packet-log requests from preservation community | External lead |
| MxOEmu captured-packet replay thread | `https://www.mxoemu.info/forum/showthread.php?pid=1059&tid=104` | Lead that HD_Morpheus had a Python server for replaying captured world-server packets | External lead |
| MxO client protocol forum | `https://mxoemu.info/forum/showthread.php?tid=7` | Auth/protocol research context, mostly margin/auth rather than world object payloads | External lead |
| RaGEZONE Hardline Dreams thread | `https://forum.ragezone.com/threads/the-matrix-online-server-emulator.1226514/?amp=1` | 2024 public lead that Hardline Dreams has combat packet logs, but object serialization/client research remains the hard part | External lead |

The packet dump import intentionally includes text and text-like dumps only. Binary images, asset files, and client/key material were left out. The imported `.log*` files contain encrypted margin sections as well as decrypted game packet sections, so embedded header hits from those files must be treated as leads until the surrounding protocol context is decoded.

## Workflow

Generate the current evidence summary:

```bash
scripts/packet-research.sh
```

The script writes:

- `docs/generated/packet-research-summary.md`
- `docs/generated/packet-research-summary.json`

If a Rajko MxOEmu clone is somewhere other than `/tmp/mxo-research/rajkosto-mxoemu`, set:

```bash
RAJKO_MXOEMU_ROOT=/path/to/mxoemu scripts/packet-research.sh
```

If the TasteeWheat MxOEmu fork is somewhere other than `/tmp/mxo-research/tasteewheat-mxoemu1`, set:

```bash
TASTEEWHEAT_MXOEMU_ROOT=/path/to/mxoemu1 scripts/packet-research.sh
```

## Current Findings

- Rajko's public `PlayerObject.cpp` dispatch map confirms 21 client RPC handlers against the local CR2/CR1 enum values.
- Exact command-byte matches include static/dynamic object interaction, jump, region loaded, hardline teleport, object selected, whereami, who, jackout, ready-for-world-change, and player background/detail RPCs.
- The imported packet dumps contain 41,398 packet-like lines across 79 parsed files.
- Protocol 04 parsing now assembles marked multi-line packet bodies, then walks every top-level RPC block instead of only the first apparent header.
- The generated report cross-references 5,201 local gameobject attribute definitions and 2,356 public FX IDs from `https://files.mxoemu.info/fxlisthex.txt`.
- Top-level protocol 04 hits include confirmed samples for static object interaction, region loaded, hardline teleport, target/object selected, whereami, jump, ability handler, loot accept, jackout, `SERVER_PLAYER_ATTRIBUTE` / `SERVER_ABILITY_LOAD`, and `SERVER_MANAGE_BONUS`.
- The MxOEmu public data host exposes `goprops.7z`; its expanded object properties include per-item `VendorPrice` and `NonSellable` fields. `scripts/import-mxoemu-community-data.sh` now generates `data/vendor_prices.csv` from that source, and the runtime vendor price lookup uses it with a default fallback.
- The same public data host exposes `pythonMXO.zip`; `resource/Python/Monolith.ltp/rshooks.py` defines `DEFAULT_VENDOR_PRICE = 1000`, normal vendor buyback as `VendorPrice / 10`, recycle info as `VendorPrice / 20`, and special ability-code buyback handling. Runtime normal-item sell pricing now follows the decoded one-tenth buyback rule while leaving ability-code exceptions as future work.
- Protocol 04 object-interaction payload parsing now decodes the local handler shape: four-byte object id, derived sector, and one-byte object type. The current imported corpus has 10 top-level interaction payloads, all CR2 static interactions currently labeled by local code as doors (`80 c8`, object type `3`) across three object ids. It has no parsed static human-NPC/vendor-path interactions (`object type 2`), so vendor-open capture is still missing rather than merely unclassified.
- External source/log mining now parses raw `HandleCommand:` debug lines. TasteeWheat's `Reality.log` contributes two raw static interaction commands (`80 c8 02 00 20 4e 03 00`) for object id `1310720002`, sector `20000`, object type `3`. These are not full packet captures, but they independently confirm the object-interaction command payload shape outside the imported packet dump corpus.
- The generated report now joins decoded object-interaction ids against tracked `data/vendor_items.csv`. Two decoded object ids also appear as vendor inventory static ids: `888143880` from six packet-dump interactions, and `1310720002` from the two TasteeWheat raw command examples. Because HD_Neo's vendor list was parsed from incomplete old logs, this is a vendor-related lead and a warning that the local object type label `3 = door` should not be treated as final protocol semantics without a vendor-open capture.
- The public MxO Discord export has useful source leads but no replacement for missing packet captures. Notable leads include Rajko requesting collector RPC packets `8113`, `8114`, and `8115`; Rajko noting no packet logs for transferring from code storage back to inventory; HD_Neo linking `vendor_items.csv` generated by parsing old logs and warning that many vendors are missing because only opened vendors could be parsed; and later notes that some static vendors are overridden in logs while event/info vendors may be non-static.
- The imported dumps do not currently show top-level local vendor buy (`81 0e`), vendor sell (`81 11`), or vendor-open (`81 0d`) headers.
- One embedded `81 11` candidate appears in `Vector.log22`, but it is inside an `MRGN->Client` packet body rather than a top-level `Client->GAME` protocol 04 block. Treat it as random/encrypted-body noise until proven otherwise.
- Three embedded `81 0d` candidates appear in Shirabaka West and `Vector.log22` payloads. Two Shirabaka West samples are now confirmed as the second two-byte field in top-level `80 bc` server payloads, and the `Vector.log22` sample is inside an MRGN packet body. Treat these as payload-field evidence, not proof of a vendor-open packet.
- The generated report now groups top-level `80 bc` payloads. The repeated structure is consistently 20 payload bytes after the `80 bc` header, with field families such as `56 00 <field> 00 c3...` and `45 03 <field> ...`, so the next step is semantic mapping to object attributes or bonus state.
- The generated report now groups `80 bc` internal layout by leading field. The largest families are `56 00` with 4,044 payloads, `15 00` with 2,782 payloads, `55 00` with 2,647 payloads, and `45 03` with 625 payloads. The `45 03` family is structurally distinct: all 625 payloads repeat field 1 at payload byte offset 9, which matches the linked `80 b2`/`80 bc` state identifier evidence. The small `11 00` family repeats field 2 at payload byte offset 11 in all 54 payloads.
- The generated report now ties manual `80 bc` constants back to corpus layout families. The strongest provenance-backed family is `45 03`: four hardcoded examples collapse to one corpus-backed field group with 105 hits. The `45 00` family has 18 hardcoded examples with seven corpus-backed field groups and 28 total field-group hits; `15 00` has 13 hardcoded examples with one corpus-backed field group and 23 hits. The hardcoded `55 00` and `56 00` examples have local provenance but no current field-group corpus hit.
- `80 bc` field 1 values now produce candidate matches for interaction-oriented attributes such as `BuyActiveTracker`, `TalkActiveTracker`, `GiverActiveTracker`, `TakerActiveTracker`, `PutActiveTracker`, `SecureTradeFlag`, and `CurrentState`. These are priority capture targets, not decoded names yet.
- The generated report now mines 59 hardcoded command examples from Rajko and local queued RPC constants, including 37 `80 bc` startup/world-state examples. The strongest field-group overlap is `45 03 / 11 00 / 00 02`, which appears 105 times in community dumps even though exact payload values differ. One local `80 bc` payload (`15 00 / 45 00 / 00 f7`) has 20 exact corpus hits and 23 field-group hits, making it a high-confidence manual constant.
- The generated report now groups top-level `80 b2` payloads, which the local protocol map aliases as both `SERVER_PLAYER_ATTRIBUTE` and `SERVER_ABILITY_LOAD`. The current corpus has 106 parsed `80 b2` payloads; local hardcoded payload `35 04 00 00 08 02` exactly matches 19 corpus packets.
- The generated report now groups protocol 04 server-state header sequences. It finds 484 state-related protocol 04 packets containing `80 b2` and/or `80 bc`, including 51 packets that carry both families in the same top-level packet. Repeated sequence shapes show `80 bc` traveling in large state bundles, often interleaved with `80 b2`, `80 b3`, and `80 bd`.
- Cross-family state links now connect `80 b2` field 0 to `80 bc` field 1 within the same top-level packet. The strongest shared fields are `3d 04` in 20 packets and `35 04`, `3e 04`, `3f 04` in 19 packets each, which is concrete evidence that the short `80 b2` form and long `80 bc` form share an attribute or state identifier.
- The linked short/long forms now expose a repeatable field-value invariant. Across 210 same-packet `80 b2`/`80 bc` links, 208 repeat the linked field at `80 bc` payload byte offset 9 and repeat the `80 b2` field 1 value at `80 bc` payload byte offset 11. The two exceptions are duplicate `55 00 / 35 04` payloads in `ikeburo_northeast.txt:372`, which should be treated as explicit outliers until more captures explain them.
- FX matches inside `80 bc` payload windows are low-frequency and may be coincidental, but repeated hits now have symbolic names and source lines in the generated report.
- Protocol 03 parsing now segments top-level object packets beyond the first signature. The current corpus yields 5,062 parsed object messages, 1,472 fully segmented messages, and 5,463 known selector segments. Known selector payload sizes now cover one-byte state markers (`00`), empty markers (`02`), position (`08`), position plus flags (`0a`, `0c`, `0e`), rotation (`04`, `06`), delete/animation (`01`), single emote payloads (`28`), and secondary movement wrappers.
- Treating selector `00` as a one-byte state marker promotes common `0e ... 00 00` object-update tails from partial to fully segmented, raising complete protocol 03 segmentation from 326 to 990 messages in the current corpus.
- Secondary movement wrappers are now decoded when the second segment in a two-update object packet starts with `00 02`, embeds a known movement selector (`08`, `0a`, `0c`, or `0e`), and ends with `00 00`. The current generated report finds 570 such wrapper segments and raises complete protocol 03 segmentation from 990 to 1,472 messages.
- The generated report now mines 34 hardcoded protocol 03 examples from local code and external MxOEmu sources, including TasteeWheat's door/hardline fork. These examples are provenance hints for spawn/self-view, static object, and movement layouts; live capture evidence is still required before treating them as decoded client behavior.
- Variable-length protocol 03 families remain intentionally partial. The generated report now calls out unresolved spawn/profile, appearance/attribute, effect/emote bundle, static object/door, and unknown selector tails instead of pretending those payload sizes are decoded.

## Immediate Research Targets

1. Capture or acquire vendor buy/sell sessions with a known character cash value, inventory slot, item GoID, vendor GoID, static object id, object interaction payload, and before/after inventory state.
2. Validate the `80 bc` field 1 candidates by pairing NPC talk, vendor open, buy, sell, secure-trade, inventory, and loot actions with before/after object state.
3. Re-run local clients with `MXO_PACKET_LOG=1` and preserve `PacketLog.txt`, `UnknownRPC.txt`, and `UnknownClient03Request.txt` after vendor, marketplace, inventory, loot, and door actions.
4. Ask MxO preservation contacts specifically for packet logs containing static human-NPC/vendor interactions (`80 c8` or CR1 `80 c3` with object type `2` or `3`), vendor windows, sell transactions, marketplace listings, inventory drag/drop, and hardline workflows.
5. Validate whether vendor-related static interactions can use object type `3`, given the `data/vendor_items.csv` correlations for object ids `888143880` and `1310720002`.
6. Implement the ability-code branch of `rshooks.py` vendor buyback once the ability ID constants are mapped cleanly into the tracked price table.
7. Semantically decode `80 bc` server payload fields by mapping the grouped field families to local object attributes, bonus/state classes, and before/after gameplay actions.
8. Follow up on the RaGEZONE/Hardline Dreams combat-log lead, prioritizing logs that include object serialization, ability state, FX, and before/after attribute context.
9. Resolve variable-length protocol 03 selectors by pairing the partial spawn/profile, appearance/attribute, effect/emote, and static object/door samples with local object definitions and more capture context.
10. Diff the local packet handlers against Rajko's `GameClient.cpp`, `MessageTypes.cpp`, and `PlayerObject.cpp` behavior before changing protocol parsing semantics.

## Notes

Protocol 04 request headers are encoded differently from the local enum values for two-byte commands. For example, local `CLIENT_OBJECTINTERACTION_STATIC = 0xc8` appears on the wire as `80 c8`, while `CLIENT_TELEPORT_HL = 0x18e` appears as `81 8e`. The packet research tool normalizes those forms before comparison.
