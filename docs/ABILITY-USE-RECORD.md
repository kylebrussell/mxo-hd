# Full native ability-use record

This tools-only decoder accepts exactly one full43-byte `80 b9` RPC or its41-byte body. It preserves every scalar and all24 ground-location bytes; it does not change server parsing, effects or admission. Existing abbreviated six-byte headless helpers are deliberately refused by this research decoder.

## Primary layout

Fixed client.dll SHA256 `cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a`. Reproduce the read-only literal probe:

```text
python research/protocol/inspect_ability_use_record.py <client.dll>
```

The inline producer621e5100 installs vtable6288c1e4; allocation621e5112 and serializer621d343f independently select43 bytes. Printer621d7a70 supplies the following names. Body offsets exclude the two-byte opcode.

| Packet offset | Body offset | Width | Native printer name |
|---|---|---|---|
|2|0|u16|Id|
|4|2|u32|Target|
|8|6|u32|ObjectTargetCreate|
|12|10|24 raw bytes / three immutable u64 words|TargetGroundLocation|
|36|34|u8|TargetInventorySlot|
|37|35|u8|InvSlotOfToolUsed|
|38|36|u8|FromPill|
|39|37|u32|TargetMiniTeamMemberID|

These are printer names, not universal field-purpose proof. In particular body34 is not labeled as a tactic by this class, and FromPill does not mean generic activation: native208 ordinary device activation used0, while an existing compiler close path also uses0. Unknown byte values, sentinel targets and all raw ground words (including nonfinite binary64 patterns) roundtrip unchanged. No coordinate interpretation, required-zero restriction or normalization is used. Producer621e5221..5249 copies six dwords unchanged; printer621d7b19..7b59 loads three qwords. The last eight copied bytes remain semantically opaque, including the native208 nonzero example.

`research/protocol/ability-use-native-device.json` pins an actual43-byte native208 request, source manifest commit/blob, fixed DLL and original packet-log SHA. It is device activation evidence, not a heal capture or an imported runtime feature. The decoded values are Id949, Target0, ObjectTargetCreate0, inventory-slot byte1, tool-slot106 and FromPill0. The source manifest's earlier state-byte terminology remains historical evidence, not a new semantic assertion.

## Usable standalone CLI

```text
dotnet tools/PacketResearch/bin/Debug/net8.0/PacketResearch.dll --ability-use-hex <86 hex digits>
dotnet tools/PacketResearch/bin/Debug/net8.0/PacketResearch.dll --ability-use-body-hex <82 hex digits>
```

The command emits typed raw fields plus full/body hexadecimal roundtrip output and exits before corpus loading. Wrong opcode, wrong length, bad hex or simultaneous full/body options exit2. Do not scan arbitrary packet bytes for this signature. This package accounts for one positively framed native record and independent synthetic boundary cases; it makes no broad corpus-count claim and adds no speculative parser branch to the large packet report.

## Canary and Claude one pass

Discovered `ability-wire-record` profile loads2/17/77 plus the sourced77 prerequisite closure45/1006. Folder52 is owned/unloaded by the existing generic fixture ancestor logic. Stored level50 matches EXP-derived50; health100/1000 and IS1000 support the ordinary heal. Existing dev health preparation is enabled, with no GM or RNG override. No developer ability-load bridge is required.

Claude: enter disposable CSR110 normally, inspect ability77 in Actions, select yourself and cast77 once. Retain the actual full client43-byte80b9 request, ordinary cast/cooldown reply and health update. Decode that bounded request with the CLI and compare exact raw roundtrip. Native UI and that capture remain pending; the pinned native208 request is not relabeled as77.

The headless `ability-wire-record` case uses actual current self creation view/spawn to construct a full43-byte request through the research encoder. Ground words are explicitly zero as fixture input, not a claim about every native request. It uses existing guarded health preparation, then ordinary77 dispatch and existing cast/heal replies. Fail-closed preteardown evidence includes sent raw request, current self identity, all receivedRPC/03/problems, full output/debug and actual source DLL/configuration/hash, including failures. Success would establish this ordinary accepted request path, not that the server consumes every field.

## Candidate validation

37 focused decoder/profile checks pass: pinned native literal, independent byte-ramp offsets, immutable input behavior, all wrong lengths/opcodes, nonfinite/sentinel/unknown-bit roundtrip and sourced77 prerequisites. The fixed-DLL source probe passes. Actual CLI full/body decoding roundtrips the native literal; short harness and bad-hex inputs refuse with exit2. Both actual Debug/Release builds and discovered plans/provenance pass. Debug8807B96C9D7D988B6EB27DC9B3CB1A74E090B6FAF73E93EB530479E99EB901F2; Release2E0479658F3CACAFDA6090A0CCD92876B9718BD4BD2FB1BECEA8D25970F10D82. Production hds/data bytes match actual master0d333c5c. No generated parity/coverage snapshots change. Fresh execution passed as recorded below.

Before the full request, the canary captures a fresh view-list cursor. Its health witness reads only later view lists and records the cursor and values in required evidence; historical entry/regeneration records cannot satisfy it. Actual Heal77 debug, cast and cooldown replies remain required. CLI output includes hexadecimal ground-word strings alongside authoritative full RpcHex, avoiding precision loss in JavaScript numeric readers.

## Fresh canary proof

Frozen1e8544c3fb5f69ba428093964bb8aa9197c7a56d passed the single ability-wire-record case in17.7 seconds (17.8-second total summary), exit0, parallel1, normal owned-server disposal. Required preteardown proof Temp/mxo-ability-wire-mxotest_run_1_80bb6dcfcd.json SHA256991E7D2D4451A5D6945BE77874607F09A5429847CBAD1BD4C9A07FF28D00A8D0 has Passed true, Step complete, Failure null. Actual selected server was this worktree's Debug hds.dll, hash8807B96C9D7D988B6EB27DC9B3CB1A74E090B6FAF73E93EB530479E99EB901F2; it is not relabeled as the separately built Release binary.

Live self creation supplied view2/spawn1. The actual sent full request was `80B94D0002000100000000000000000000000000000000000000000000000000000000006B6B0000000000`. Health view-list cursor2 was captured immediately before sending; later values were120 and129. Ordinary cast/cooldown replies and the actual `Heal 77 -> WireHealer for 9` executor log were observed. Raw evidence retains39 receivedRPCs/7 view lists with zero protocol problems, full stdout/debug and source identity. This proves the existing ordinary accepted77 path and research roundtrip, not that every field is consumed, that the zeros reproduce native uninitialized bytes, or native UI behavior. Claude's one pass remains pending.


Root CLI follow-up: explicit full/body flags now enter the standalone decoder even when their value is missing, and require exactly one value. Missing values, conflicting flags and repeated values exit2 before corpus analysis. Nine actual CLI checks passed, including literal full/body roundtrip, omitted/conflicting/repeated inputs, bad hex and abbreviated records. Only the entry-point argument guard changed after the recorded fresh canary; decoder, headless scenario, mode and hds/data remain unchanged, so its original source/binary proof is retained.
