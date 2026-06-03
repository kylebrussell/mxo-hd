# Community Data Resources

This directory contains small public preservation-community data resources that are useful for packet-format research.

## FX lists

- Source: `https://files.mxoemu.info/fxlisthex.txt`
- Lead: MxOEmu forum thread `https://mxoemu.info/forum/showthread.php?tid=2801`
- Last-Modified header observed: `Sun, 30 Jan 2011 03:17:49 GMT`
- Imported purpose: map four-byte effect IDs found in packet payloads to symbolic FX names.

`research/community-data/fxlisthex.txt` and `data/fxlisthex.txt` are plain-text lists of little-endian and big-endian FX identifiers plus symbolic names. The checked-in `data/fxlistreal.txt` uses one big-endian identifier plus an FX file/name pair. The packet research tool loads all three, reverses `fxlistreal.txt` ids for little-endian packet matching, and treats them as supporting evidence only; packet interpretation still needs local packet framing and payload context.

## MxOEmu public data host

- Source index: `https://files.mxoemu.info/`
- Useful resources observed: `gameobjects.csv`, `abilities.txt`, `invitems.txt`, `invitems_command.txt`, `fxlistreal.txt`, `goprops.7z`, `fullgoprops.7z`, and `pythonMXO.zip`.
- Imported runtime artifact: `data/vendor_prices.csv`
- Imported research artifact: `research/community-data/invitems_command.txt`
- Regeneration command: `scripts/import-mxoemu-community-data.sh`

`data/vendor_prices.csv` is generated from `goprops.7z`, which expands to a much larger `goprops.txt` containing per-object `VendorPrice` and `NonSellable` properties. The compact CSV is tracked so runtime vendor pricing can use original decoded price data without adding the 51 MB expanded source text to the repository.

`research/community-data/invitems_command.txt` is the public MxOEmu `&gib` item command list. The packet research tool uses it to resolve vendor inventory item ids and Object599 `EquippedItemID` values to command symbols and display names.

## External leads not imported

- RaGEZONE thread: `https://forum.ragezone.com/threads/the-matrix-online-server-emulator.1226514/?amp=1`
- Observed lead: a Hardline Dreams contributor publicly stated in 2024 that they have combat packet logs, while noting that object serialization and client research remain major blockers.
- Import status: no packet files were published in the thread, so nothing was imported. Treat this as a contact/request lead for combat, ability, FX, and object-state captures.
- `pythonMXO.zip`: public MxOEmu client Python/resource archive. It includes `serverrules.py` and `clientrules.py` vendor/collector hooks, but no packet capture payloads. It remains an external semantic-reference lead rather than a tracked runtime dependency.
