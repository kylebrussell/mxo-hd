# Old1124 effect recovery: primary-source research

**The actual effect of ability 1124 remains MISSING and is not implemented.** This research does not establish retirement, an alias, or a supported activation path. It changes no runtime behavior or audit status.

Research began on PR128 head `3d29b7bf06e8f90cf77d424b28ff40d13e724874`; this standalone report is based on master `75eb7aab2e60bd58bb9fcd34b31a6d661e8bba1a`. The original PR128 worktree was not modified.

## Primary sources and findings

The client is 7.6005. `client.dll` SHA256 is `cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a`; the extracted GOB SHA256 is `3e47f11012f6f219a26c9f5decfa10cbc00726c397937d03c3068254e5424103`.

- **All resources:** decoded all 44,406 raw GOB records, including non-ability records. No decoded field references Old1124's GOID `0x80119000` or the callback `DisruptConnectionTerm`. Searching every GOB byte for the little-endian GOID finds exactly one occurrence, at index offset `0x3274`, outside every record. This also checks opaque fields for that exact GOID, but cannot exclude computed IDs or compact integers.
- **Original bytecode:** parsed all 62 original Python2.3 `.pyc` modules recursively, inspecting every nested code object's names and constants. The only disruption-specific identifiers are the definition `tempmods.DisruptConnectionTerm` and the assignment `ability/spy/misc._DISRUPT_BOOTED_FLAG`. No 1124/Old GOID constant, action implementation, callback registration or caller was found. This verifies the original bytecode rather than relying on decompiler completeness.
- **Orphan callback:** `tempmods.py:128` defines `DisruptConnectionTerm(tempMod, subject)` solely as `subject.CharMvt.removeDissemblingType(kDisruptedConnection)`. It establishes neither the caller nor target, duration, strength, success outcome, or association with 1124. `ability/spy/misc.py:189` assigns `_DISRUPT_BOOTED_FLAG = 1 << 31`; no script reads it. Neither proves session disconnection or target ejection.
- **Native constant:** registrations at `0x6251374e` and `0x62521e20` establish `kDisruptedConnection = 0x20`. This is a movement/dissembling flag, not an ability number. The same flag has sourced `ImpairConnectionInit`/`ImpairConnectionTerm` callbacks at `tempmods.py:813/819`, attached by ability 94's stored StatModifier. Therefore its existence or a consumer cannot establish Old1124's contract.
- **Distinct resources:** the Old record is 235 bytes and has five components: Info, InvItem, Ability, an ability prerequisite and a character prerequisite. Ability 384's record is 268 bytes and includes three StatModifier components. Old1124 contains no hidden modifier component. Its stored AbilityID384 is insufficient to establish a routing alias because native compact-ID mapping resolves 1124 to the separate Old resource.

The native addresses can be inspected without executing the client using the existing local `mxo-combat-inspect.py` utility:

```powershell
python C:/Users/russe/AppData/Local/Temp/mxo-combat-inspect.py xref 0x628c88d4
python C:/Users/russe/AppData/Local/Temp/mxo-combat-inspect.py dis 0x62513710 110
python C:/Users/russe/AppData/Local/Temp/mxo-combat-inspect.py dis 0x62521df0 110
```

## Direction-aware capture check

Used the existing PacketResearch index of 278 files, selecting the 89 files with known `80 b9`/`80 b4` cast-header leads. Joined strict hex-dump lines within packets, retained Client->GAME/Client->WORLD direction and protocol04, then read the little-endian compact ability after each candidate header and normalized it with `%4096`, matching `AbilityCatalog.Canonical`.

Across 11,062 client protocol04 packets, the query finds 1,403 candidate cast headers and **zero normalized 1124 or 1125**. Positive sanity examples are 870 (374), 466 (83), 430 (78), 337 (66), 357 (63), 429 (61), 359 (52), and 267 (49). No indexed selected file was missing.

This is candidate-level triage, not a complete RPC framing decoder. Imported duplicate captures are not deduplicated. These negatives mean no execution example was recovered; they do not prove retail never used the ability. Header leads that span a line boundary or evade the existing index are outside this query's coverage.

A directionless preliminary scan found an apparent 1125 at `ProxyM2_m2_packets.log.txt:388118`. Manual context proves this is WORLD->Client protocol03 movement data (`80 b9 65 c4`), so it is rejected as ability/tool evidence. It must not be used to claim the tool's effect was observed.

## Reproduction

Run the committed read-only query from the repository. It reads existing source files and captures, prints JSON, and neither launches the client nor creates databases. The existing `py23marshal.py` parser is required to read Python2.3 code without executing it; its directory is passed explicitly.

```powershell
python research/abilities/inspect_disrupt_connection.py --client C:/Users/russe/Developer/mxo-client/MxO --pyc-root C:/Users/russe/Developer/mxo-research/client-data/dumps/monolith_pyc --marshal-tools C:/Users/russe/Developer/mxo-research/tools/client-data
```

Expected results include `resource_count=44406`, `old_goid_byte_offsets=["0x3274"]`, `resource_refs=[]`, `original_script_modules=62`, `indexed_capture_files=278`, `cast_lead_files=89`, `missing_capture_files=[]`, `client_protocol04_packets=11062`, `candidate_cast_headers=1403`, and `normalized1124=normalized1125=0`. The source hashes above identify the resource build; the tracked PacketResearch index and corpus are tied to the report's master base.

For alternate versions, read-only file inventory of the supplied `mxo-client` and `mxo-research` trees found one `Monolith.ltp`, one `standard.ltp`, one `gameobjects.pkb`, and no alternate GOB/Monolith build. The download folder contains only `MxO_7.6005.7z`. The `monolith_pyc` and `standard_pyc` directories represent different packs, not alternate versions of the same rules. No older implementation was available in these supplied trees.

## Availability and required missing evidence

PR128's native tool matching analysis establishes that no shipped component10 tool satisfies 1124: all 869 templates were scanned, and tool470 actually requires 1125. This is an eligibility correction only. Ability 384 has its own three passive modifiers; neither its stored ID on the Old resource nor tool470's name justifies aliasing 1124 to 384 or 1125. This evidence does not establish retirement, NPC impossibility or permission to manufacture a grant/tool.

Recovering the actual effect requires primary evidence linking Old1124 to its executor: an earlier server rules build with its action and tempmod construction; an older resource build with matching tool and modifier values; or an identified retail 1124 execution capture and interpretable result state. Target selection, success/deflection behavior, applied state, duration/strength, expiry, and a legitimate activation/grant path remain unknown. A decision to expose an otherwise unreachable legacy action would be separate server policy, not recovered retail behavior.

No runtime or audit changes, headless servers, native QA setup, client actions, shared fixture changes or database operations were performed for this research.
