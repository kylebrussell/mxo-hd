# P1 base and direct-aggregate producer gate (read-only)

Offline research completed before this decoder package. No gameplay or stat-math change. All offsets in this document are hexadecimal; node+0x14 is distinct from decimal14.
Native client SHA256 cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a.

## Closed distinction

I(q) and D(q) have different producers. I is a replicated loaded-attribute/ability value, with a separate CSSlot. D is a client-maintained sum of individual manage-bonus records; it is NOT itself a packet containing an authoritative final GetTotal(q). The server can transmit a modifier directly targeting177 or342, separately from raw modifiers targeting1055/1147. A direct target177 capture is now positive. This does not identify a75-to177 derivation or prove retail server templates/defaults.

## Base I: world RPC80b2, not a recovered Margin message

Native class constructor6213aefc / print621515d0 / vtable6287f98c names Id,Value,CSSlot. Wire layout including header: 80 b2 | Id:u16LE | Value:u16LE | CSSlot:u16LE (8bytes). The field-layout-matched inbound handler621e3b50 reads all three words from payload+2/+4/+6, then calls621e0b90(id,value,slot,0) at621e3b88. That path obtains local avatar629eee08 inventory+b4, and calls625c5ab0 at621e0d98. The setter obtains map inventory+0x110 and writes packed pair Value/CSSlot at node+0x14/+0x16. No link evaluation is performed by this setter. Map helper625c4e00 creates default value1/slot0x208 only when invoked for a missing node; received setter replaces both words. This is not a default177 magnitude claim.

Other I writers:625c2950 assigns word+0x14 for an existing valid template. Status-success upgrade handlers621e8860 (80b8-shaped AbilityID/NewLevel/Status) and621e8c30 (80b6-shaped array/status) call it at621e8948/621e8d88; local operation621e15b9 also calls it.625c5be0 adds a delta to existing I with low-word storage.625e6b50 can create a zero-valued/0x208-slot inventory entry when a newly added modifier requires eligible absent attribute membership (625e6b9b); this is bookkeeping, not server-derived177.

The frozen existing PacketResearch binary decoded1535 base80b2 records in278 corpus files; no selected Id177 or342 was found. A separate whitespace-tolerant raw8byte-signature search found no `08 80 b2 b1 00 ...` or `08 80 b2 56 01 ...` in text/log files. This is bounded corpus/decoder absence, not proof that retail never transmits these base IDs. No specific Margin producer was recovered.

## Direct D: world RPC80bc ManageBonus

Native ctor6213b94d allocates22bytes. Exact layout including header:
- bytes0..1:80bc
- byte2:Type
- bytes3..6:Locator:u32LE
- bytes7..10:SourceID:u32LE
- bytes11..12:TargetID:u16LE
- bytes13..16:Value:i32LE
- byte17:AddRemoveType
- bytes18..21:Duration:f32LE

Inbound field-layout-matched handler621e10a0 obtains local avatar inventory and calls625fd730 at621e1121. The latter adds0x134 (modifier object) and tail-calls625ebb90. That dispatcher explicitly branches on LOW BYTE OF LOCATOR (not Type):0 for individual records,3 for a separate timed/membership path, other values ignored here. For locator-kind0, AddRemove0 builds a record and calls625eb8a0;2 updates existing matching source/target through625e98b0;1 removes matching source/target through625eb720ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢625ead60. Thus removal is source+target based, not a wholesale assignment to a target's total.

625eb8a0 stores the record in the modifier object's+0x4c map and at625eb94b..95e obtains aggregate+0x58 via625e9020, then adds LOW16(record.Value).625e9020 uses u16 target key at node+0x10, creates zero aggregate if missing, and returns node+0x12. Existing-record update625e990a subtracts the previous low-word value and625e9919 adds the new value. Removal625eae22 subtracts the removed low-word value and may erase a zero aggregate. GetDirect625e3d40 reads that node+0x12. This closes the packetÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢recordÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢aggregate path without inventing a separate derived-total RPC.

## Positive retail captures

All bytes below are framed RPC payloads after80bc, decoded from existing corpus with existing PacketResearch enumerator, then independently unpacked at the native offsets above. Line numbers are decoder packet-start lines, not necessarily the physical line of the individual RPC inside a multiline packet.

Capture A: research/packet-dumps/community-intake/20260607T200024Z-hdneo-public-remaining-small-ability-loot-market/logs/mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt
SHA2566744ab2701faedfc07f1ca683e5bd2089c267b90accbd9b1fa83dea4d22a5c5b.
- packet-start37661: `15 00 46 03 00 3f 04 00 00 b1 00 11 00 00 00 00 00 00 00 00`: Type0x15,Locator0x34600,Source1087 Howitzer Upgrade,Target177,Value17,Add0,Duration0. Packet-start16663 has the corresponding Source1087/Target177/Value17 removal (Add1).
- SAME capture packet-start883: `45 00 09 00 00 4b 00 00 00 1f 04 19 00 00 00 01 00 00 00 00`: Source75,Target1055,Value25,Add1. Same packet also has Type0x55 Source75/Target1055/Value25 Add0, locator0x7200. This proves raw75 and direct177 record families occur in the same capture; not that one causes the other.

Capture B: research/packet-dumps/community-intake/20260607T003822Z-hdneo-mxopackets1-combat-ability-selected/logs/actions_combat.log.txt packet-start14214:
`15 00 76 05 00 ee 03 00 00 56 01 6c 00 00 00 00 00 00 00 00`: Source1006 Hacker Style,Target342,Value108,Add0. Distinct raw32 proof: community-intake/20260607T024558Z-hdneo-public-mission-code-rajko-worldstate-sanitized-selected/logs/mxopackets1_crashover2k_afterwhoruneo_proxy_022_loading_area_coder_tree_abilities.log.txt packet-start6334: `45 00 5d 00 00 20 00 00 00 7b 04 09 00 00 00 01 00 00 00 00`, Source32,Target1147,Value9,Add1. No32ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢342 conversion is inferred.

Corpus accounting:278files,66484 ManageBonus samples; selected targets177:13,342:58,1055:540,1147:647. All13 target177 samples are Source1087/Value17/Type0x15 (includes removals/repeated observations).342 source set1006,1084,1087,1293. Counts include repeated captures, retransmits and removals; not unique gameplay events.
Decoder used READONLY: C:/Users/russe/AppData/Local/Temp/mxo-artifacts/bin/PacketResearch/debug/PacketResearch.dll SHA256524bfb13ca1eef1bd20830d850c7bc74e07bb3deab376161cbd364b5e6688c1a. This is an already-built older decoder, not a build from frozen180; no source-provenance claim for that assembly. Its exact framed payload samples are retained in mxo-a1-capture-producer-results.json and script mxo-a1-capture-producers.ps1. Native offsets supply independent semantics, avoiding older heuristic field interpretations.

## Raw schema flags and remaining gate

The schema property table format records name/type/offset/u32flag. The26 selected descriptors in schemas315(177) and112(1055), including qualified and bare link/scale/cap/value aliases, all have rawflag0. Existing parser gob.py:attr_tables merely labels the last u32 `flag`; neither its semantics nor a client/server-only bit contract is recovered. Therefore0 cannot be translated into client-only, server-only, stripped, replicated, or a default-value policy. Declared storage422 versus stored22/30 remains real, but is not proof of a retailserver schema.

The stronger statement is now: retail transmits both raw linked modifiers and direct target-stat modifiers, and the client aggregates the latter through the same80bc path. It is not yet proven that retail transmits precomputed75-derived177 or32-derived342, nor which coefficients/server template produce them. Existing1087/1006 direct records are independent named effects. No passive75/32 closure, row promotion, coefficient/default change, or runtime PR is justified by this gate.

Next useful primary proof would be a causally isolated75 add/remove sequence with every target177 record and base80b2 in the same transaction/window, or a retail server/script producer that explicitly computes that conversion. The current corpus positives alone do not satisfy that causal requirement.

Durable primary excerpts: [native writers](../research/protocol/stat-producers-native.txt). [Complete client link algebra](CLIENT-LINKED-STAT-MATH.md) preserves the separate loader/default/authority findings. Raw corpus paths above refer to existing committed captures; temporary scan files are supplementary, not required to read the positive samples.

## Decoder package and one-pass native check

`BaseAttributeRecord` accepts only exact8byte80b2 or6byte payload; malformed lengths remain raw in `PlayerAttributePayloadSample.NativeFields=null`. Existing `ManageBonusRecord` still accepts exact22byte80bc and preserves every raw field, unknown Type and float bits. `IndividualOperation` is exposed only for locator low-byte0 and operation0/1/2; unsupported kinds/operation bytes remain decoded raw with a null operation. This is classification, not a gameplay accumulator or formula.

Discovered mode: `stat-producers`. Start only through the existing Claude-owned NativeQa workflow. The fixture owns loaded32/75/356/314 at level50; normal356 supplies1087. No helper client, RNG override, GM privilege or fabricated grant is required. Existing strict QA loadout bridge is enabled only for the disposable fixture and routes the character's actual archive row through80ae. This is explicit QA preparation, not a player unlock bypass.

1. On CSR110, confirm Actions contains Howitzer Upgrade1087 and Upgrade Attacks314. No second native character is needed.
2. Enter `?qaunload 75`, then `?qaload 75`. These commands resolve the owned slot/level and call the ordinary loadout handler. Record whether the normal loaded ability state changes without errors. Repeat32 if desired. Headless independently proves raw75Ã¢â€ â€™1055 and32Ã¢â€ â€™1147 bytes; the native ability list alone does not prove hidden stat values.
3. Use Actions Ã¢â€ â€™ Howitzer Upgrade1087 on self. Observe its normal buff icon/state, then activate it again to turn it off. It has no timed expiry; do not wait for or claim one. Headless proves distinct source1087/target177/value17 on/off records and matching locator.
4. Use Actions Ã¢â€ â€™ Upgrade Attacks314 on self. Observe normal casting/buff and20second expiry without another cast. Headless proves source314/target342/value108,Duration20 and matching expiry locator. These are deterministic lifecycle requirements; no random hit/damage outcome is requested.
5. Record native UI PASS/FAIL and exceptions. This diagnostic does not prove retail75Ã¢â€ â€™177/32Ã¢â€ â€™342 conversion, radius gameplay, formula authority or passive parity. Native checks remain PENDING until Claude executes them.

Headless scenario `stat-producers` runs normal owned passive startup, parent356 loadout/grant, activation/timed expiry. Because AddPlayerAsync logs in before scenario-specific archive inserts, it requests the owned parent356 through ordinary80ae after05 and asserts parent(356,50,402) plus grant(1087,0,520) separately. It does not assume initial setup will be replayed by05. It decodes actual received80b2/80bc with the independent research decoders and saves raw RPC hex plus fields before disposal. Finite314 expiry is distinct from Howitzer's indefinite exclusive activation. No hds gameplay implementation is modified.

Validation history remains explicit: the first26.0second attempt skipped earlier32 with a forward-only wait; the second25.1second attempt confirmed32+10/75+25 but incorrectly expected initial setup's1087 grant after post-login SQL insertion; the third26.9second attempt proved normal parent356→grant1087 and all four Howitzer adds, then incorrectly required a second-use off. Each failure remains recorded; the latter two retained complete unique raw/decoded RPC JSON before teardown. These were fixture assumptions, not evidence of a decoder failure or newly implemented gameplay fix. Final lifecycle scope is normal1087 ON, normal75 unload/reload and normal31420second expiry.
