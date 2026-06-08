# Packet Capture Source

- Imported at UTC: `20260607T014317Z`
- Dry-run: `0`
- Source kind: `directory`
- Source input: `/tmp/mxo-hdneo-trinity-vendor-redacted`
- Scanned files from: `/tmp/mxo-hdneo-trinity-vendor-redacted`
- Sender: `hdneo/mxopackets1`
- Provenance: `public GitHub Matrix Online packet archive; sanitized derivative`
- Context: `redacted public Trinity vendor-session logs from hdneo/mxopackets1; password-bearing auth lines were removed and a single NUL byte was stripped before import; original non-text/source files remain outside Git`
- Max imported file bytes: `5242880`

## Imported Files

| Original path | Imported path | Bytes | SHA-256 |
| --- | --- | ---: | --- |
| `crashover2k/trinitys/proxy_second_pc/010_trinitys_vendor.log` | `logs/crashover2k_trinitys_proxy_second_pc_010_trinitys_vendor.log.txt` | 216836 | `7af8c6d102c6ab971b53766bdac2771bb44c4f3bcbe70b255fd7d82ef2b39095` |
| `crashover2k/trinitys/proxy_second_pc/009_trinitys_session_weapon_vendor.log` | `logs/crashover2k_trinitys_proxy_second_pc_009_trinitys_session_weapon_vendor.log.txt` | 143958 | `e7fb751993ea9f13038173e3d6b0c8c2aa51dc54f39ec2a8cf527be75418a6f3` |

## Intake Next Steps

- Review `SOURCE.md`, `SKIPPED.md`, and `TRIAGE.md` before committing imported logs.
- Preserve the original received archive outside the repository and use the SHA-256 hashes in `SOURCE.md` for duplicate/provenance checks.
- Import only text logs or text-like dumps that contain no account secrets, client binaries, private keys, or unrelated game assets.
- After review, run `scripts/packet-research.sh` to regenerate packet summaries.
