# Loading Area hardline registration

This is an explicit OURS registration extension, not a recovered retail assignment of construct numbers. Native Margin09 registers filenames in advertised order; the hardline loader uses their one-based ordinal to match real HDL/HID records. Previously09 advertised only three cities while cached hardlines incorrectly used internal map IDs12-17.

| Logical | Internal map | Universe METR | World |
|---|---|---|---|
|1|1|12|Slums|
|2|2|7|Downtown|
|3|3|169|International|
|4|12|379|large01|
|5|13|384|large02|
|6|14|377|medium01|
|7|15|382|medium02|
|8|16|383|medium03|
|9|17|381|small03|

The original09 opaque header and cities remain unchanged. At offset13 its u16 absolute array offset remains15; the u16 count is followed by Universeu16 IDs. Cached08 hardlines are five-byte logicalu8/HIDu32LE records.6c/6d,80ff and both818e construct fields use logical IDs, with full uint validation before conversion. Database rows and HardlineNetwork keep internal map IDs.06 world/instance fields, PvP11-18, tutorial800, LA901 and the existing city1 tutorial80fa packet are unchanged. The native-retest repair below adds the sourced incremental80fa discovery reply for ordinary Synchronize.

`HardlineUploadRequired=false` remains the existing OURS default. When true, cached/status/teleport destinations require uploaded, source-valid hardlines. No normal player receives new discovery rows. The discovery fixture inserts explicit QA-only rows after proving refusal. Its harness-only XML override copies the ordinary source configuration into a new GUID working directory and edits only that ordinary file, never source/output hardlinks. It verifies source configuration bytes remain unchanged.

## Primary evidence

Client SHA256 `cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a`. Shipped `resource/worlds/universe.btb` SHA256 `e4676c0a31178a4beb167c08ffa96cbacc62cb35b3af66d201ee9a41560116fa`,581 METR records parsed to exact EOF.

* Native6253bf80 decodes09: opcode@0, statusu32@1, opaqueu32@5 and9, offsetu16@13; zero offset means absent array.
* Successful09 calls6252ad20 at62542d17 to erase the filename vector, so relog does not append to the previous registration.62542d40 maps incoming Universe IDs through622a9e40 and appends via mediator+190/6252af10.
*621c6d90 loads actual Hdln resources by filename ordinal and HID, setting physical eligibility at621c7085.620374a0 filters that byte;6d/621c42c0 changes the separate known byte.
*621c3da0 serializes five-byte cache records;621c7a30 accepts logical IDs1-10.
*12 tracked retail MS_ConnectReply packets independently decoded without errors all advertise `[12,7,169]`, including Trinity second_pc_010 vendor-selected packet180 and Rajko near-end Mara hardline packet165. They support the preserved cities only; the six additions are OURS policy.

No primary evidence establishes06 as a hardline ordinal, so it remains unchanged. District data remains partial with its existing static/rotation/population omissions.

## Validation and Claude-only native one-pass

Units cover09 opaque bytes/order, bounds/non-aliasing, cache widths, discovery refusal, safe source heading192 and the separate PvP namespace. Headless `la-hardline-registration` and `la-hardline-discovery` independently decode actual09/cache and exercise ordinary source booth, Loading Area, logical8/HID2, exact shipped internal16 arrival, malformed/replayed refusals and direct818e logical9 to internal17. Wire success is not native UI proof.

Discovered mode `la-hardline-registration` starts CSR110 at medium03/2 source arrival37700,495,31700 with byte heading192, no dev/GM/RNG override and no QA discovery grants (existing default policy).

1. Rightclick the nearby actual592 booth and choose Exit to Loading Area; confirm modal and transition.
2. Inspect native construct labels, Actions/menu intact; select medium03/HID2 and confirm exact booth/world reload.
3. Travel from a city booth to a construct and back; check ordinary direct hardline-map travel.
4. Check city, archive-book and tutorial routes retain their existing worlds. Record native label/HDL failures separately from headless wire results.

## Local validation

Frozen runtime/fixture pin `0323bc31ee99b3e7c417966976e3f8c3b3e6ef65` (master `05bccebff0a2888907aeee98dc488e7590b4f8b7` imported; own protocol code byte-identical to `480927b16817eb80cff57d6034a7ac88d0503c9d`). Release hds SHA256 `434E2E3EC7954748A281B25B65443B3672B5D50AD81579A8175976F5A3F1B11E`; Debug `CBD812E97F694F1CE95EF80ED5D455DCC14D2792836265F64864666143BDE1DF`.

Both actual headless scenarios passed18.3s each,36.6s total, exit0, peakone server; both disposable servers/databases disposed. Success raw logs were saved before teardown as Temp/mxo-la-registration-success.log (28081 bytes) and mxo-la-discovery-success.log (30463 bytes); runner mxo-la-fresh.log. Source WorldConfig.xml.dist SHA256 stayed `5975b584fe57fce10b730aae3aec3fb2efec161bd14e7f1979acbec3d3da3b24`.

Full4328 unit tests passed; after pure upstream170/173 imports,20 focused registration/world-fixture tests passed. Both configurations built and actual read-only fixture plans/provenance passed;12 Python audit/link/policy tests passed. Historical parity Markdown, coverage JSON and current CSV remain unchanged. No ability source changed. Original production empty08 omission is preserved; the independent wire decoder treats an absent08 block as empty. No native empty-versus-absent reset semantics are claimed.

Native UI/rendering remains pending Claude's one-pass. No client, native setup, main database or settings were changed.


## Native failure and Synchronize repair

Claude's22:09 native test at711ca7f FAILED: the booth showed only Synchronize and action8 received no reply. The4029799 transport integration did not repair that failure; earlier READY statements are superseded. Native retest is pending.

The missing production path now handles ordinary80c7/80c8 action8 on a sourced GO592 booth: resolve its nearest real HDL entry, persist internal map/HID discovery, break shadow, then send80fa with the Margin09 filename ordinal/HID/MakeActive1. Already-known rows still receive the reply for an idempotent retry. Unknown registrations/HIDs refuse; stale dynamic spawn handles refuse before all booth actions. Synchronize does not upload bag items, advance mission hardline objectives or reset the world. No fixture-known-row grant is used.

Primary evidence: [native discovery handler](../research/protocol/hardline-sync-native.txt), shipped serverrules/clientrules HardlineSync10040 and the literal Rajko02:38:41 request/reply. The handler sets the known byte, and client rules require it for Loading Area exit. This is distinct from the initial cached08 producer; native UI success is still unproven.

New headless la-hardline-sync uses discovery-required configuration: initial empty cache, stale handle refusal, normal Synchronize, exact80fa080000000200000001, one persisted row after retry, no upload/reset, ordinary33s logout cleanup/relog, exact five-byte Margin08 cache, then ordinary booth->LA901->logical8/HID2->exact HDL arrival. It retains actual raw RPCs/cache/DLL hash before teardown. Prior default/discovery and combined169-fence wire passes remain prior evidence.

Claude-only one-pass: use la-hardline-registration/CSR110. If the actual booth is unknown, double-click or radial Synchronize; confirm the full radial now appears and record80fa. Exit to Loading Area, select medium03/logical8/HID2, jack in, confirm37700,495,31700. Relog and check that the booth remains known. Then check ordinary city travel. No bypass, dev/RNG command or guessed activation packet is required.
