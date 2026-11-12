# Data mining: Actions filtering and Tools activation

Read-only follow-up to PR113, 2026-11-12. Client: MxO 7.6005
`client.dll`, SHA-256
`cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a`.
This is a static consumer trace plus an existing native capture, not a new native run.

## Why the Data Miner folder is absent

The Actions rebuild (`0x6205ade0`) calls `0x62059ba0` at `0x6205ae8c`.
That collector walks the inventory manager's ability map and admits candidates
through `0x62518e60`. The alternative live-actor collector
`0x620593b0` -> `0x62059000` uses the same predicate.

`0x62518e60` is **not an IsDiscipline test**. After resolving the ability template,
it reads `RequiresTool` (string `0x628c8adc`) at `0x62518eb3`. A nonzero getter
status or nonzero property value branches to the false return at `0x62518ec3`:

```text
62518eb3 call 0x621ce010
62518eb8 test al, al
62518eba jne  0x62518ec3
62518ebc mov  al, byte ptr [ebp + 0xb]
62518ebf test al, al
62518ec1 je   0x62518ecb
62518ec3 pop  edi
62518ec4 xor  al, al
```

The remaining predicate calls `0x62515bb0`, which looks up `CastValidTarget`
(string `0x628c8a8c`). Accepted abilities are then grouped by
`0x6205a0f0`: it calls `0x62519560` for the discipline and `0x62059ea0`
to find/create that folder. `0x62519560` uses `IsDiscipline` or
`DisciplineBelongsTo`; this grouping happens **after** the tool filter.

The reproducible GOB catalog stores `Ability/RequiresTool=1` for215,621 and622.
They therefore cannot create entries/folders through this Abilities collector.
537 is a passive discipline;538–541 are prerequisite/passive tiers. Their stored
rows have no `CastValidTarget` or `CastWhenUsed` fields. Omitted engine defaults
are not reconstructed; this observation is not an invented passive cast route.

For the focused data-mining preset, no Data Miner folder in Actions > Abilities
is expected. Owning537 without loading it is useful ownership metadata, but
does not override this filter. Loading the discipline's stats, adding arbitrary
prerequisites or editing the client templates to manufacture a folder is not
justified by this consumer.

## Actual activation route

Use **Actions > Tools**, then double-click the locator item in the personal bag.
Tools item insertion at `0x620598f5` uses the Actions object's member `+0x84`;
the weapon list uses `+0x80`, and the Abilities tree uses `+0x7c`.

The Actions left-double-click handler `0x6205a620` recognizes the event at
`0x6205a686`. Its Tools branch at `0x6205a794` matches source widget `+0x84`,
obtains the selected bag slot, and rejects sentinel slot `0x6b`. It calls
`0x621cd0a0`, dispatching to `0x621ed310` or `0x621ed630`.
The tool-use path `0x621ed630` validates the slot with `0x621d84d0`, obtains
the item's ability with `0x621d0970`, then calls common activation
`0x621ecf10` at `0x621ed68d`.

`0x621d0970` tries `ActivatedAbility` (string `0x6288c05c`) first, otherwise
`ReqAbilityID` (string `0x62871018`), and decodes `(value >> 10) & 0xfff`.
The shipped locator export `data/utility_objects.json` contains:

| Item | Stored ReqAbilityID | Decoded ability |
|---|---:|---:|
|422 handheld|2147703808|215|
|423 goggles|2147703808|215|
|38209 basic goggles|2147703808|215|
|38208 advanced goggles|2148120576|622|

The shipped basic locators do not select621. Distinct server handling of621,
its activation/off records and215/621 cleanup are covered by existing unit and
fresh-server tests. A native621 input route is not established by this trace.

## Preserved native evidence and limits

Claude reported accepted locator use on the corrected4cf2de4 preset, while
noting that the Data Miner folder was still absent. The preserved
`mxo-headless/9986c372efa5/PacketLog.txt` lines800–803, CSR_Tester110,
2026-11-12 01:30:08 ET, contains this client request (outer framing omitted):

```text
80 b9 d7 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
00 00 00 00 0c f5 1a 00 c8 73 91 3d 08 01 00 00 00 00 00
```

The record's ability is215, not621. The corresponding DebugLog lines541/557–558
record `[ABILITY] 215 (LocateDataNodeAbility) on view 0`,
`[DATA-MINING] search char=110 ability=215 tool=422 outcome=on`, and handheld
range30m/Detect Luggables. This log proves the215/handheld422 server route;
it does not independently prove a423 cast or the visual appearance of node FX,
white glow, drop or mission-map presentation. The earlier native pickup evidence
is separately preserved in `docs/DATA-MINING.md`.

## Reproducing the static evidence

Use Python with `pefile` and `capstone` installed. This reads the binary without
loading or running it; change only the client path if necessary:

```python
import hashlib
from pathlib import Path
import capstone, pefile
p = Path("C:/Users/russe/Developer/mxo-client/MxO/client.dll")
assert hashlib.sha256(p.read_bytes()).hexdigest() == "cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a"
pe = pefile.PE(str(p))
b, data = pe.OPTIONAL_HEADER.ImageBase, pe.get_memory_mapped_image()
md = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_32)
for a, n in [(0x62518e60, 0x80), (0x62515bb0, 0x69),
             (0x6205a0f0, 0x22), (0x620598f2, 0x16),
             (0x6205a794, 0x6a), (0x621d0970, 0x75),
             (0x621ed630, 0x80)]:
    for i in md.disasm(data[a-b:a-b+n], a):
        print(hex(i.address), i.mnemonic, i.op_str)
for a in [0x628c8adc, 0x628c8a8c, 0x6288c05c, 0x62871018]:
    print(hex(a), data[a-b:a-b+80].split(b"\0")[0].decode())
```

The existing read-only `research/world-data/utility_objects.py` reproduces the
locator properties from the GOB. No runtime, fixture statistics or catalog data
change is needed for this documentation correction.
