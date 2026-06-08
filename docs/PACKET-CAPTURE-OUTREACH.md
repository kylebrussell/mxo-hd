# Packet Capture Outreach Templates

Use these templates when asking MxO preservation contacts for packet logs. Link them back to `docs/PACKET-CAPTURE-REQUEST.md` for the full technical checklist, and use `docs/PACKET-RESOURCE-LEADS.md` to decide which public thread, Discord route, source archive, or tester group to approach first.

## Short Forum Or Discord Request

Subject: Looking for MxO packet logs for preservation research

Hi, I am working through Hardline Dreams packet-format research and trying to preserve/decode old Matrix Online packet behavior from real logs.

The most useful files are raw text packet logs, old sniffer dumps, `PacketLog.txt`, `UnknownRPC.txt`, `UnknownClient03Request.txt`, `DebugLog.txt`, or `ServerLog.txt`. Screenshots and summaries are much less useful than the raw packet text plus a short note about what action was happening.

Highest-value actions right now:

- vendor open, buy, sell, recycle-info, and item trade sessions
- NPC talk, loot, inventory move, marketplace, hardline, and door interactions
- movement/object-state captures around hyperjump, hypersprint, hardline arrival, and teleport-spawn windows
- combat or ability logs that include before/after object state, not just isolated combat lines
- old world/NPC spawn logs containing Object599 / `NPC_BASE` profile tails

Please do not send account passwords, session tokens, private keys, or client binaries. Text logs and action notes are enough.

## Object599 Spawn/Profile Tail Request

I am specifically looking for old MxO world packet logs where an NPC spawn/profile packet contains:

- `0c 57 02 <spawn counter> cd ab <attribute count> 8e <CharacterName>`
- at least 600 bytes preserved after that first Object599 / `NPC_BASE` creation block
- nearby header-like object-view tails with `view 1`, update count `08` or `0c`, and first selector `50`, `67`, `68`, `69`, `76`, `90`, `a3`, or `d0`

Helpful archive search strings:

```text
0c 57 02
00 0c 57 02
01 00 08 50
01 00 08 67
01 00 08 68
01 00 08 69
01 00 08 90
01 00 08 a3
01 00 08 d0
01 00 0c 76
```

If you find matches, the surrounding lines matter. Please include a few hundred bytes before the match and at least 600 bytes after it if possible, plus the district/hardline/coordinates/NPC names if known.

Existing corpus examples we are trying to extend:

- `teleport/kaede_SW.txt:237` for selector `50`
- `teleport/juron_NE.txt:348` for selector `67`
- `teleport/saikung_central2.txt:221` for selector `68`
- `teleport/shinjuku_westcentral.txt:260` for selector `69`
- `teleport/kaede_central.txt:451` for selector `76`
- `teleport/murasaki_southwest.txt:464` for selector `90`
- `teleport/murasaki_central.txt:231` for selector `a3`
- `teleport/teleporttosaikung.txt:409` for selector `d0`

## Hardline Dreams Combat Log Request

Hi, I saw public references that Hardline Dreams has or had combat packet logs. I am trying to use those for packet-format preservation, especially protocol 03 object serialization and ability/effect state.

Useful combat logs should include:

- packet text before, during, and after combat or ability use
- character/NPC names or object ids if known
- ability name, loadout/equipment changes, buffs, FX, health, inner-strength, and combat mode before/after
- surrounding object-state packets, not only the single combat action line

Even partial logs are useful if they include raw bytes and action context. Please avoid passwords, tokens, private keys, or client binaries.

## Local Capture Request For Testers

If you can run a client against Hardline Dreams, please start the server with:

```bash
MXO_PACKET_LOG=1 MXO_RPC_LOG=1 MXO_CLIENT_VIEW_LOG=1 MXO_DEBUG_LOG=1 \
dotnet run --project "hds/Hardline Dreams MxO server.csproj"
```

Then perform one short action sequence per capture:

- vendor open only
- vendor buy one known item
- vendor sell one known normal item
- trade one known item
- door/static object interaction
- hardline teleport and arrival
- hyperjump/hypersprint movement burst
- one combat or ability action if your server state supports it

After the session, run:

```bash
scripts/collect-packet-capture.sh \
  --context "short action notes" \
  --character "handle" \
  --protocol "CR2" \
  --location "district / hardline / coordinates" \
  --actions "exact action sequence" \
  --objects "known object ids or nearby NPC/static object" \
  --items "known item GoIDs, slots, counts, prices" \
  --cash "cash before -> after" \
  --inventory "slot/count before -> after" \
  --state "health, ability, buff, FX, equipment, team, crew, faction state" \
  --missing "anything unknown or not captured"
```

Send the generated archive from `data/experiment_artifacts/packet-captures/`. It includes the logs, `capture-context.txt`, file hashes, skipped-file evidence for possible secret markers, and an initial triage report when logs are present.

## Intake Checklist

When someone sends logs, record:

- who sent it and where it came from
- the lead route from `docs/PACKET-RESOURCE-LEADS.md`, if applicable
- whether it is old live-server, MxOEmu, Hardline Dreams, or unknown
- CR1 or CR2 if known
- exact action sequence
- character handle, district, hardline, coordinates, nearby NPC/static object, and known item GoIDs
- files received and whether any sensitive material had to be removed

Then add the files under `research/packet-dumps/` only if they are text logs or text-like dumps and contain no account secrets, client binaries, private keys, or unrelated game assets.

Before import, run this against an extracted folder or a `.zip`, `.tar`, `.tar.gz`, `.tgz`, `.tar.bz2`, `.tbz2`, `.tar.xz`, or `.txz` archive:

```bash
scripts/triage-packet-capture.sh --source /path/to/extracted/logs-or-capture.zip --out data/experiment_artifacts/packet-captures/triage.md
```

Use the triage report to find the strongest vendor, selector-`ff` vendor-body, Object599 / `NPC_BASE`, movement, ability, and effect leads. The scan matches both spaced and compact hex forms and includes Warboy-derived selector-`ff` body signatures such as `e0 2d 81 0d`, `00 41 00 ff`, and `00 01 04 ff`; inspect surrounding bytes before making parser changes.

After review, preview safe text logs with provenance without staging them. The source can be an extracted folder or a `.zip`, `.tar`, `.tar.gz`, `.tgz`, `.tar.bz2`, `.tbz2`, `.tar.xz`, or `.txz` archive:

```bash
scripts/import-packet-capture.sh \
  --source /path/to/extracted/logs-or-capture.zip \
  --label contact-or-thread-name \
  --sender "who sent it" \
  --provenance "old live-server, MxOEmu, Hardline Dreams, or unknown" \
  --context "short action notes" \
  --dry-run
```

Then rerun the same command without `--dry-run` to stage the reviewed corpus. Review the generated `SOURCE.md`, `SKIPPED.md`, and `TRIAGE.md` before committing anything from the staged import. Use the source-archive and imported-file SHA-256 hashes in `SOURCE.md` to detect duplicate or repacked submissions later; duplicate hash rows are review leads, not automatic rejection.
