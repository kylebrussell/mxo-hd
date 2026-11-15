# Protocol family decoders

This research/tooling change changes no server packets or gameplay effects. Existing PacketResearch framing, the conservative protocol03 parser and HeadlessClient object definitions retain ownership of boundaries. There is no second heuristic spawn parser.

## Native80bc fields

Client SHA256 cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a: constructor6213b94d allocates0x16 bytes and writes opcode0xbc80. Printer62151b00 reads these widths and labels (also indexed in generated/client-rpc-classes.txt). Offsets include both opcode bytes.

| Field | Offset | Bytes | Decode |
|---|---:|---:|---|
|Type|2|1|unsigned byte|
|Locator|3|4|little-endian unsigned32|
|SourceID|7|4|little-endian unsigned32|
|TargetID|11|2|little-endian unsigned16|
|Value|13|4|raw bits; explicit signed/float views|
|AddRemoveType|17|1|unsigned byte, unknown values preserved|
|Duration|18|4|raw binary32 bits; explicit float view|

Reproduce with `python research/protocol/inspect_manage_bonus.py --client <client.dll> [--python-deps <research dependencies>]` (pefile/capstone). The SHA gate prevents applying these addresses to another binary.

The previous two-word grouping overlaps native fields. Captured Overclock `15 00 42 01 00 ...` has native Type0x15 and Locator0x00014200. Compatibility Family0x0015/RecordId0x0142 are projections, not extra fields. Likewise `45 03 11 00 00 ...` has Type0x45 and Locator0x00001103;0x0345 is a compatibility family, not a native16-bit Type. SourceID and TargetID are not universally an ability and stat: trade/login/state variants preserve their raw identities.

ManageBonusRecord accepts exactly22 bytes including80bc (or exactly20 through the explicit payload method). Unknown types and IEEE NaN bits survive; float views are excluded from JSON so arbitrary bits do not fail serialization. Existing DetectManageBonusPayloads attaches NativeFields only at the exact framed length. Other lengths retain original bytes with null NativeFields. No signature search inside arbitrary payloads is added.

## Protocol03 boundary contract

Existing FindCreations uses recognized creation starts and existing ordered per-GoID attribute definitions. Attribute count/masks are now transactional: unknown schemas, invalid offsets, truncated values, extra active fields, missing continuation and continuation after the final field fail without consuming bytes or publishing partial fields. Empty count consumes only its count byte. Widths come from existing definitions, not guessed printable strings.

This strengthens the headless schema decoder; it does not recover omitted retail variable selector bodies or make all retail spawn/profile layouts complete. PacketResearch's existing partial records remain partial. Retail objects absent from server definitions remain unknown.

## Bounded corpus

Eight existing sanitized create/spawn/session logs are hash-pinned in research/protocol/family-corpus-manifest.json. Run:

    dotnet run --project tools/PacketResearch -c Release -- --repo . --packet-dumps research/packet-dumps/community-intake/20260608T120313Z-hdneo-public-create-spawn-session-sanitized-selected/logs --families-json <scratch-report.json>
    python research/protocol/verify_family_corpus.py --report <scratch-report.json> --repo .

The CLI reuses existing framed detectors, emits per-file totals/family examples and bypasses unrelated large report rollups. Counts are detected records, not unique actions or every possible record in all archives. No new captures or copies of original logs are committed.

| Detected records | Total | Decoded/complete | Opaque/partial |
|---|---:|---:|---:|
|80bc|940|940|0|
|03 existing parser|11214|9057|2157|

Structurally complete does not mean semantically understood. The unchanged03 partial remainder is explicit. The verifier checks eight source hashes, per-file counts and conservation equations.

## Claude-only native one-pass

Use fresh discovered protocol-family profile. It loads6 Consistent Technique and normal ancestors, with no dev/GM/fixed rolls because these observations have no random outcome. Jack in normally; confirm nearby QA helper names/appearances. Open Actions and activate Consistent Technique on self. Observe the buff icon, normal20-second expiry and no corrupted helper profile/disconnect. Preserve normal server packets for typed80bc comparison. Do not inject raw packets or hidden commands. The fresh headless canary checks real observer03 creation and a normal cast's on/off80bc locator/value/duration. Native acceptance remains pending and Claude-only.

## Recorded validation

Full4,166 unit tests passed, including native field ownership, every truncated22-byte prefix, trailing bytes, unknown types/NaN serialization, opaque-length preservation and transactional creation-mask failures. Python12 audit policy/link tests passed. Debug/Release builds and actual read-only protocol-family/unknown plans plus provenance passed.

The normal wire canary passed40.4seconds, exit0, peak one disposable server:38 observed80bc records decoded, normal6 on/off retained the same Locator00012400/value235/duration20, and a real observer decoded FamilyActor03 name and position. No native-client PASS is claimed. The8-file corpus hash/count verifier passed on a fresh report; the source probe verified constructor widths and all native printer label references.
