# Packet Capture Request

This brief is for MxO preservation contacts who may have old logs or can run a client against Hardline Dreams. The current decoder has enough structure to target the missing packet families precisely; raw packet logs with action context are more useful than screenshots or summaries.

Use `docs/PACKET-CAPTURE-OUTREACH.md` for ready-to-send forum, Discord, and direct-message text.
Use `docs/PACKET-RESOURCE-LEADS.md` to choose which community thread, Discord route, source archive, or tester group to approach first.

## Highest-Value Captures

1. Vendor open, buy, and sell sessions.
   - Record the character handle, cash before and after, vendor/static object id if known, item GoID, inventory slot, item stack count, quoted price, and whether the item is normal, ability code, or recycle info.
   - Include both static object interactions (`80 c8` / CR1 `80 c3` object type `0` or `3`) and human-NPC/vendor-path interactions if available (object type `2`).
   - Current corpus has selected decoded top-level `CLIENT_VENDOR_BUY`, `CLIENT_VENDOR_SELL`, and `SERVER_VENDOR_OPEN` anchors, but before/after cash and inventory state is still missing.
   - Warboy vendor-byte candidates now sit inside selector-`ff` object-state bodies with position triples near decoded player/world coordinates, so before/after location, target, and nearby vendor/static-object context matter as much as the byte signatures.
2. NPC talk, secure-trade, loot, inventory move, marketplace list/buy/cancel, hardline, and door actions.
   - Record before/after object state where possible.
   - These actions are needed to validate `80 bc` interaction tracker candidates such as `BuyActiveTracker`, `TalkActiveTracker`, `SecureTradeFlag`, `GiverActiveTracker`, `TakerActiveTracker`, and `PutActiveTracker`.
3. Combat and ability sessions.
   - Preserve object serialization, ability state, FX changes, equipment changes, health/inner-strength changes, and before/after attributes.
   - The public RaGEZONE/Hardline Dreams lead mentions combat packet logs; those are useful if they include the surrounding object state, not only isolated combat lines.
4. Movement and object-state sessions around nested movement windows.
   - Prioritize captures around `00 06 <movement selector>` payloads and repeated suffix `ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00`.
   - Current generated evidence queues 397 mode `06` object-state movement windows as boundary targets; none has a bounded wrapper example yet. The top checked-in family starts its tail mostly at unresolved-selector offsets `+17`/`+16`/`+18`/`+19`/`+26` and has after-suffix candidate cuts at `+25`/`+24`/`+26`/`+27`/`+34`. Preserve before/after object state and enough surrounding bytes to prove whether those candidate cuts are true body boundaries.
5. Static object and door sessions.
   - Capture multiple interactions with known world coordinates and static object ids.
   - Current protocol 03 static object leads cover selectors `3c`, `9e`, `9f`, and `a0`; more before/after captures are needed to assign final post-quaternion tail semantics.
6. Protocol 03 Object599 / `NPC_BASE` spawn/profile tails.
   - Prioritize captures around NPC spawn packets where a parsed `NPC_BASE` creation is immediately followed by header-like object-view tails with `view 1`, update counts `08` or `0c`, and first selectors `50`, `67`, `68`, `69`, `76`, `90`, `a3`, or `d0`.
   - Preserve at least 600 bytes after the first `0c 57 02 <spawn counter> cd ab <attribute count> 8e <CharacterName>` block so the unknown tail body is not truncated before the next object boundary.
   - The current corpus shows embedded `cd ab` anchors at selector-body offset `+6` for selectors `50`, `67`, `68`, `69`, `90`, `a3`, and `d0`, and at `+2` for selector `76` and offset-7 selector `80`. Record zone, coordinates, nearby NPC names, and whether the capture happens right after hardline/teleport, because most examples come from teleport-spawn windows.
   - Useful existing sample lines to compare against are `teleport/kaede_SW.txt:237` (`50`), `teleport/juron_NE.txt:348` (`67`), `teleport/saikung_central2.txt:221` (`68`), `teleport/shinjuku_westcentral.txt:260` (`69`), `teleport/kaede_central.txt:451` (`76`), `teleport/murasaki_southwest.txt:464` (`90`), `teleport/murasaki_central.txt:231` (`a3`), and `teleport/teleporttosaikung.txt:409` (`d0`).

## Community Routing

- `docs/PACKET-RESOURCE-LEADS.md` tracks prioritized public leads, evidence, packet value, and the next ask for each route.
- Current public MxOEmu status still lists exploration, doors, clothing vendors, item trading, role-play, animations/effects, and movement abilities as available, while leveling and combat are not implemented. Ask MxOEmu contacts for vendor/trade/movement/effect/door captures and old sniffer logs.
- Use the RaGEZONE/Hardline Dreams lead for combat packet logs, especially if they include object serialization before and after ability use. Isolated combat lines are lower value unless they include surrounding protocol 03 object state.
- The MxOEmu forum remains the best public place to ask for old source/packet-log holders; include exact selector names and sample lines from this brief so contacts can search their archives without needing to understand the whole decoder.

## If Running This Server

Start the server with packet logging enabled from the working directory where you want the text logs written:

```bash
MXO_PACKET_LOG=1 MXO_RPC_LOG=1 MXO_CLIENT_VIEW_LOG=1 MXO_DEBUG_LOG=1 \
dotnet run --project "hds/Hardline Dreams MxO server.csproj"
```

After the session, bundle logs from the repo root:

```bash
scripts/collect-packet-capture.sh \
  --context "vendor open, buy, sell with known cash and item ids" \
  --character "handle" \
  --protocol "CR2" \
  --location "district / hardline / coordinates / nearby NPC or static object" \
  --actions "exact action sequence, including failed attempts" \
  --objects "known object ids, static ids, vendor ids, or NPC names" \
  --items "item GoIDs, slots, stack counts, quoted prices" \
  --cash "cash before -> after" \
  --inventory "slot/count before -> after" \
  --state "health, ability, buff, FX, equipment, team, crew, or faction state" \
  --missing "anything unknown or not captured"
```

The script looks for `PacketLog.txt`, `UnknownRPC.txt`, `UnknownClient03Request.txt`, `DebugLog.txt`, and `ServerLog.txt` in the repo root and common build output directories, then writes a timestamped archive under `data/experiment_artifacts/packet-captures/`. The archive includes `capture-context.txt` with the structured context fields, `capture-file-manifest.md` with bytes and SHA-256 hashes, `skipped-files.md` for possible secret-marker hits, and `TRIAGE.md` when logs are found.

Before importing received logs, triage extracted folders or archives for current research signatures:

```bash
scripts/triage-packet-capture.sh --source /path/to/extracted/logs-or-capture.zip --out data/experiment_artifacts/packet-captures/triage.md
```

The triage report lists vendor, selector-`ff` vendor-body, interaction, Object599 / `NPC_BASE`, movement, ability, and effect signature counts plus first matching lines. It accepts directories plus the same archive types as import, and matches both spaced and compact hex forms. The selector-`ff` section includes Warboy-derived body leads such as `e0 2d 81 0d`, `01 43 ... e0 2d 81 0d 22 89 20`, `00 41 00 ff`, and `00 01 04 ff`. Matches are leads only; inspect surrounding bytes and context before treating them as decoded packets.

After manual review, preview the import without staging files in the repository:

```bash
scripts/import-packet-capture.sh \
  --source /path/to/extracted/logs-or-capture.zip \
  --label contact-or-thread-name \
  --sender "who sent it" \
  --provenance "old live-server, MxOEmu, Hardline Dreams, or unknown" \
  --context "short action notes" \
  --dry-run
```

Then stage safe text logs into the tracked research corpus:

```bash
scripts/import-packet-capture.sh \
  --source /path/to/extracted/logs-or-capture.zip \
  --label contact-or-thread-name \
  --sender "who sent it" \
  --provenance "old live-server, MxOEmu, Hardline Dreams, or unknown" \
  --context "short action notes"
```

The import script accepts directories plus `.zip`, `.tar`, `.tar.gz`, `.tgz`, `.tar.bz2`, `.tbz2`, `.tar.xz`, and `.txz` archives. It copies only text-like files under `research/packet-dumps/community-intake/`, normalizes ignored `.log` inputs to tracked `.txt` files, writes Markdown-safe `SOURCE.md` provenance with source-archive and imported-file SHA-256 hashes where available, flags duplicate hashes from earlier intake manifests as review leads, writes `SKIPPED.md`, rejects unsafe archive paths, and runs triage on the staged logs. Review those files before committing received data.

## Minimum Context To Include

- Character handle and server/client build used.
- District, hardline, coordinates, and nearby static/NPC object if known.
- Exact action sequence, including failed attempts.
- Before/after values for cash, inventory slot, item count, health, buffs, ability state, team/crew/faction state, and location.
- Any known object ids or item GoIDs from client tooling, local data files, or logs.
- Whether the capture is CR1 or CR2.

## Files To Send

- `PacketLog.txt`
- `UnknownRPC.txt`
- `UnknownClient03Request.txt`
- `DebugLog.txt`
- `ServerLog.txt`
- The `capture-context.txt` manifest generated by `scripts/collect-packet-capture.sh`
- The `capture-file-manifest.md`, `skipped-files.md`, and `TRIAGE.md` files generated by `scripts/collect-packet-capture.sh`
- Any old packet dumps or sniffer logs, even if they are manually formatted or partially decoded

Do not include account passwords, session tokens, private keys, or client binaries. Text packet logs and contextual notes are enough for this research workflow.
