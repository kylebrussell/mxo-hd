# Packet Capture Source

- Imported at UTC: `20260607T022135Z`
- Dry-run: `0`
- Source kind: `directory`
- Source input: `/tmp/hdneo-two-person-sanitized`
- Scanned files from: `/tmp/hdneo-two-person-sanitized`
- Sender: `hdneo/mxopackets1`
- Provenance: `public GitHub Matrix Online packet archive; sanitized derivative`
- Context: `sanitized public two-person/player-interaction logs selected from hdneo/mxopackets1 for trade-adjacent, static interaction, vendor-window, object-state, mission/crew, and player-interaction packet research; NUL bytes were stripped and auth/margin handshake blocks through MS_EstablishUDPSessionReply were removed before import; original source files remain outside Git`
- Max imported file bytes: `12000000`

## Imported Files

| Original path | Imported path | Bytes | SHA-256 |
| --- | --- | ---: | --- |
| `mxopackets1/crashover2k/afterwhoruneo/proxy/020_afterwhoruneo_some_2_person_actions_with_mxoemu_and_mission_and_crew_things.log` | `logs/mxopackets1_crashover2k_afterwhoruneo_proxy_020_afterwhoruneo_some_2_person_actions_with_mxoemu_and_mission_and_crew_things.log.txt` | 8209033 | `7f25dfb94d46c39a80ecb24a8260b57c5c09b8738f3f0018a7329d4d573a41f3` |
| `mxopackets1/crashover2k/020_mxoemu_2_person_actions_with_afterwhoruneo.log` | `logs/mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt` | 6690317 | `925dc17948c23b2e845bfb4b104baf8cf1d1ba2cc76ef522b0cff429d302fc31` |

## Intake Next Steps

- Review `SOURCE.md`, `SKIPPED.md`, and `TRIAGE.md` before committing imported logs.
- Preserve the original received archive outside the repository and use the SHA-256 hashes in `SOURCE.md` for duplicate/provenance checks.
- Import only text logs or text-like dumps that contain no account secrets, client binaries, private keys, or unrelated game assets.
- After review, run `scripts/packet-research.sh` to regenerate packet summaries.
