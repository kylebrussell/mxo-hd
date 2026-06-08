# Packet Capture Source

- Imported at UTC: `20260607T030922Z`
- Dry-run: `0`
- Source kind: `directory`
- Source input: `/tmp/mxo-hd-next-intake`
- Scanned files from: `/tmp/mxo-hd-next-intake`
- Sender: `public hdneo/mxopackets1`
- Provenance: `public standalone hdneo/mxopackets1 Luizoe old_sniffer logs`
- Context: `Focused public Luizoe old_sniffer recursion/trade slice selected for vendor-sell, Object599, static/dynamic object interaction, mode 06 movement, friend-status, and ability evidence.`
- Max imported file bytes: `7000000`

## Imported Files

| Original path | Imported path | Bytes | SHA-256 |
| --- | --- | ---: | --- |
| `mxopackets1/luizoe/old_sniffer/Recursion Char.log` | `logs/mxopackets1_luizoe_old_sniffer_Recursion_Char.log.txt` | 2704317 | `b78976f9feab201df54b0348e22ff7d4277318c21f339ca9b9672742e2660182` |
| `mxopackets1/luizoe/old_sniffer/Recursion.log` | `logs/mxopackets1_luizoe_old_sniffer_Recursion.log.txt` | 5502045 | `4c0ffb25cc263ab714921314e6d7311df17643231c5eb4e20660f91a8e3441dd` |
| `mxopackets1/luizoe/old_sniffer/Unknown.log` | `logs/mxopackets1_luizoe_old_sniffer_Unknown.log.txt` | 6510088 | `334adb726fa4a8cab6d1226d9e3372d39d0e065269cabcc2a1d8aba9f05b020f` |
| `mxopackets1/luizoe/old_sniffer/Syntax.log` | `logs/mxopackets1_luizoe_old_sniffer_Syntax.log.txt` | 4225 | `4327a2ea0eb40277f5669d349d87aaa4683ec58e7b12a44bb1ad82a5c6c8004c` |
| `mxopackets1/luizoe/old_sniffer/Cpy of recursion.log` | `logs/mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt` | 2549727 | `9a720e7fbb4abbdb45426adb54e3c2b7e54495efbff49a063990d5193ed29003` |

## Intake Next Steps

- Review `SOURCE.md`, `SKIPPED.md`, and `TRIAGE.md` before committing imported logs.
- Preserve the original received archive outside the repository and use the SHA-256 hashes in `SOURCE.md` for duplicate/provenance checks.
- Import only text logs or text-like dumps that contain no account secrets, client binaries, private keys, or unrelated game assets.
- After review, run `scripts/packet-research.sh` to regenerate packet summaries.
