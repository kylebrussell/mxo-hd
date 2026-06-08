# Community Data Resources

This directory contains small public preservation-community data resources that are useful for packet-format research.

See `docs/PACKET-RESOURCE-LEADS.md` for the broader outreach matrix covering packet-log holders, replay-log leads, live tester routes, and external semantic references.

## FX lists

- Source: `https://files.mxoemu.info/fxlisthex.txt`
- Lead: MxOEmu forum thread `https://mxoemu.info/forum/showthread.php?tid=2801`
- Last-Modified header observed: `Sun, 30 Jan 2011 03:17:49 GMT`
- Imported purpose: map four-byte effect IDs found in packet payloads to symbolic FX names.

`research/community-data/fxlisthex.txt` and `data/fxlisthex.txt` are plain-text lists of little-endian and big-endian FX identifiers plus symbolic names. The checked-in `data/fxlistreal.txt` uses one big-endian identifier plus an FX file/name pair. The packet research tool loads all three, reverses `fxlistreal.txt` ids for little-endian packet matching, and treats them as supporting evidence only; packet interpretation still needs local packet framing and payload context.

## MxOEmu public data host

- Source index: `https://files.mxoemu.info/`
- Useful resources observed: `gameobjects.csv`, `abilities.txt`, `invitems.txt`, `invitems_command.txt`, `fxlistreal.txt`, `goprops.7z`, `fullgoprops.7z`, and `pythonMXO.zip`.
- Import manifest: `research/community-data/MANIFEST.md`
- Imported runtime artifact: `data/vendor_prices.csv`
- Imported research artifacts: `research/community-data/abilities.txt`, `fxlisthex.txt`, `fxlistreal.txt`, `gameobjects-public.csv`, `invitems.txt`, and `invitems_command.txt`
- Regeneration command: `scripts/import-mxoemu-community-data.sh`

`data/vendor_prices.csv` is generated from `goprops.7z`, which expands to a much larger `goprops.txt` containing per-object `VendorPrice` and `NonSellable` properties. The compact CSV is tracked so runtime vendor pricing can use original decoded price data without adding the 51 MB expanded source text to the repository.

`research/community-data/invitems_command.txt` is the public MxOEmu `&gib` item command list. The packet research tool uses it to resolve vendor inventory item ids and Object599 `EquippedItemID` values to command symbols and display names.

`research/community-data/abilities.txt` is the public compact ability GoID, codename, and display-name map. The packet research tool loads it alongside `data/abilityIDs.csv` for ability-name correlation. Current PlayerCharacter creation records expose small title/combat-exclusive values such as `356`, `17`, and `4096`, not direct public high-bit ability GoIDs, so those fields remain raw leads rather than named abilities.

`research/community-data/gameobjects-public.csv` is normalized from the public `gameobjects.csv` `name: ,id` shape into parser-compatible `name,id,` rows. It is kept as source-reference data because the tracked runtime `data/gameobjects.csv` has the same line count but includes additional third-column values on some RSI customization rows.

## Public Discord export lead mining

- Source index: `https://files.mxoemu.info/MxoDiscordBackup/`
- Channel scan: `research/community-data/discord-backup-channel-scan.md`
- Emulation source JSON: `https://files.mxoemu.info/MxoDiscordBackup/The%20Matrix%20Community%20-%20The%20Matrix%20Online%20-%20emulation%20%5B137010908307259393%5D.json`
- Emulation source HTML: `https://files.mxoemu.info/MxoDiscordBackup/The%20Matrix%20Community%20-%20The%20Matrix%20Online%20-%20emulation%20%5B137010908307259393%5D.html`
- Emulation derived report: `research/community-data/discord-emulation-packet-leads.md`
- Mara Central source JSON: `https://files.mxoemu.info/MxoDiscordBackup/The%20Matrix%20Community%20-%20The%20Matrix%20Online%20-%20mara-central%20%5B137006126100250624%5D.json`
- Mara Central source HTML: `https://files.mxoemu.info/MxoDiscordBackup/The%20Matrix%20Community%20-%20The%20Matrix%20Online%20-%20mara-central%20%5B137006126100250624%5D.html`
- Mara Central derived report: `research/community-data/discord-mara-central-packet-leads.md`
- Regeneration command: `scripts/mine-discord-resource-leads.sh --source /path/to/emulation.json --json-url <source-json-url> --html-url <source-html-url> --out research/community-data/discord-emulation-packet-leads.md`

The Discord reports are acquisition and outreach artifacts, not packet corpora. They record source metadata, SHA-256 hashes, term counts, message anchors, and paraphrased packet-resource findings for collector RPC, vendor coverage, code-storage, Neo packet-log archives, public GitHub packet-corpus provenance, Rajkosto sniffer provenance, NPC spawn/log coverage limits, combat-log material, packet-capture/sniffer mentions, mission-log torrents, tutorial packet-log parser leads, and replay/source leads. Message bodies are intentionally omitted; use the public HTML anchors for context review before contacting anyone or importing received files.

## External leads not imported

- Lead matrix: `docs/PACKET-RESOURCE-LEADS.md`
- RaGEZONE thread: `https://forum.ragezone.com/threads/the-matrix-online-server-emulator.1226514/?amp=1`
- Observed lead: a Hardline Dreams contributor publicly stated in 2024 that they have combat packet logs, while noting that object serialization and client research remain major blockers.
- Import status: no packet files were published in the thread, so nothing was imported. Treat this as a contact/request lead for combat, ability, FX, and object-state captures.
- `pythonMXO.zip`: public MxOEmu client Python/resource archive. It includes `serverrules.py` and `clientrules.py` vendor/collector hooks, but no packet capture payloads. It remains an external semantic-reference lead rather than a tracked runtime dependency.
