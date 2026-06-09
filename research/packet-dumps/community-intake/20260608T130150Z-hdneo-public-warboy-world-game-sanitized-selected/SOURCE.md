# Packet Capture Source

- Imported at UTC: `20260608T130150Z`
- Dry-run: `0`
- Source kind: `directory`
- Source input: `/tmp/mxo-warboy-selected-sanitized.OFEnRM`
- Scanned files from: `/tmp/mxo-warboy-selected-sanitized.OFEnRM`
- Sender: `hdneo/mxopackets2-warboy`
- Provenance: `public hdneo mxopackets2 warboy.zip; sanitized derivative`
- Context: `Selected Warboy WORLD/GAME-only sanitized derivative; retained high-signal selector-ff vendor/body, continuation-prefix, static-object, and Object599 evidence while dropping AUTH/MARGIN handshake blocks and known session/key marker lines.`
- Max imported file bytes: `30000000`

## Imported Files

| Original path | Imported path | Bytes | SHA-256 |
| --- | --- | ---: | --- |
| `ProxyM2/m2_packets_f_5.log` | `logs/ProxyM2_m2_packets_f_5.log.txt` | 20068 | `45187a68b914bba14f917ecc3b8a552918b1bb0869bd7e761387626f28ae2c33` |
| `ProxyM2/m2_packets.log` | `logs/ProxyM2_m2_packets.log.txt` | 25324763 | `52bdc5a7dd26be8fe37c8573ff43ceb047027a96fadf9455d63f198c048ac496` |
| `ProxyM2/m2_packets_5.log` | `logs/ProxyM2_m2_packets_5.log.txt` | 1809118 | `4328fffb660cf5f5c287528eb323f5215469298fada6668fbc173026d2df19bf` |
| `ProxyM2/m2_packets6.log` | `logs/ProxyM2_m2_packets6.log.txt` | 6306207 | `59dfede2ece60a0730f00d094ca5c0b90b70544639b77b7db0dd373a0f43b8e5` |
| `ProxyM1/m1_packets4.log` | `logs/ProxyM1_m1_packets4.log.txt` | 1203837 | `7b0ba4e56f704f763b3ac395efc95d8015e8ae14ea2d5b9333c011b06042a645` |
| `ProxyM1/m1_packets.log` | `logs/ProxyM1_m1_packets.log.txt` | 221753 | `43f3e4c9a183073a2b66ed6b8b2357a2ea2f3f1bd245f9a2ace02fc58acb22d8` |

## Intake Next Steps

- Review `SOURCE.md`, `SKIPPED.md`, and `TRIAGE.md` before committing imported logs.
- Preserve the original received archive outside the repository and use the SHA-256 hashes in `SOURCE.md` for duplicate/provenance checks.
- Import only text logs or text-like dumps that contain no account secrets, client binaries, private keys, or unrelated game assets.
- After review, run `scripts/packet-research.sh` to regenerate packet summaries.
