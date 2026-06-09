# Packet Capture Source

- Imported at UTC: `20260608T122812Z`
- Dry-run: `0`
- Source kind: `directory`
- Source input: `/tmp/mxo-hdneo-endgame-mission-sanitized`
- Scanned files from: `/tmp/mxo-hdneo-endgame-mission-sanitized`
- Sender: `hdneo/mxopackets1`
- Provenance: `public hdneo/mxopackets1 archive; sanitized derivative`
- Context: `selected public endgame/mission/duality and high-signal unsorted packet logs from hdneo/mxopackets1; NUL bytes stripped and only GAME/WORLD packet blocks retained to remove auth/margin handshakes before import`
- Max imported file bytes: `9000000`

## Imported Files

| Original path | Imported path | Bytes | SHA-256 |
| --- | --- | ---: | --- |
| `mxopackets1/crashover2k/afterwhoruneo/proxy/023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log` | `logs/mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt` | 5576411 | `1e23d15faeed543f8732a5ac824aa24959d2f518272309124a116a5f9a4188b5` |
| `mxopackets1/crashover2k/_create_char_devoted_make_first_mission.log` | `logs/mxopackets1_crashover2k__create_char_devoted_make_first_mission.log.txt` | 2911976 | `e9e312eb0f3251807fbc072406fddc81a1d756cd07f3fea954f98ccced207dd7` |
| `mxopackets1/crashover2k/proxy_unsorted/Packets1.log` | `logs/mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt` | 1741287 | `feee40889eb9fbc38ae5e0ee54f9b23804bea983d5a111847c331253a541697d` |
| `mxopackets1/medanon/niobe3dualduality.log` | `logs/mxopackets1_medanon_niobe3dualduality.log.txt` | 7752759 | `8f84d48f425fcc322f1277f38f003a91dbe72613221244f161154849a9744851` |

## Intake Next Steps

- Review `SOURCE.md`, `SKIPPED.md`, and `TRIAGE.md` before committing imported logs.
- Preserve the original received archive outside the repository and use the SHA-256 hashes in `SOURCE.md` for duplicate/provenance checks.
- Import only text logs or text-like dumps that contain no account secrets, client binaries, private keys, or unrelated game assets.
- After review, run `scripts/packet-research.sh` to regenerate packet summaries.
