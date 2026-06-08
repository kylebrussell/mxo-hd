# Packet Capture Source

- Imported at UTC: `20260607T212233Z`
- Dry-run: `0`
- Source kind: `directory`
- Source input: `/tmp/mxo-hdneo-public-session-spawn-movement-sanitized-selected`
- Scanned files from: `/tmp/mxo-hdneo-public-session-spawn-movement-sanitized-selected`
- Sender: `public hdneo GitHub archives`
- Provenance: `public GitHub packet logs sanitized derivative`
- Context: `Selected public mxopackets1 Luizoe recursion, unsorted proxy, create/session, idle, Vector4, and Unknown traces after stripping NUL bytes and removing auth/margin handshake blocks through MS_EstablishUDPSessionReply; staged source had no auth/password markers and adds vendor-window, static-interaction, Object599/profile, object-state, movement, selector-ff, and session/create evidence.`
- Max imported file bytes: `6000000`

## Imported Files

| Original path | Imported path | Bytes | SHA-256 |
| --- | --- | ---: | --- |
| `mxopackets1_crashover2k_afterwhoruneo_proxy_003_login_session.log` | `logs/mxopackets1_crashover2k_afterwhoruneo_proxy_003_login_session.log.txt` | 1199436 | `d550ade0d388db91d05fefb54ef242047efeb75465d665dd3e8ff45f3d808733` |
| `mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_Neuer_Ordner_Unknown.log2` | `logs/mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_Neuer_Ordner_Unknown.log2.txt` | 2724462 | `026a0d2340531e5f55049481efce6261a44ce6957f02abde92d8edb049d2207c` |
| `mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log` | `logs/mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt` | 5672171 | `0a5c8901d13e0931962ffeff44f0ac25b2703f3a2c7408204f7e0a51c6e3de27` |
| `mxopackets1_crashover2k_009_session_complete.log` | `logs/mxopackets1_crashover2k_009_session_complete.log.txt` | 2050369 | `0b353b53de8eb34f41de9b160fe6adcba8ae7238c698a46de5343af7ff2b5746` |
| `mxopackets1_crashover2k_011_idle_park_east.log` | `logs/mxopackets1_crashover2k_011_idle_park_east.log.txt` | 249725 | `0a265b52c4c99817cc600581a40199cc091832ee642c3e6bcb693a284c8ae7fd` |
| `mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector4.log` | `logs/mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector4.log.txt` | 440535 | `a2f5681dcd82f45d63a61e202a739209cc10a0fa0da4a750adff35ae00056f46` |
| `mxopackets1_crashover2k__create_char_devoted_make_first_mission.log` | `logs/mxopackets1_crashover2k__create_char_devoted_make_first_mission.log.txt` | 2977171 | `b7e3390a5e0f5c45600fd8c65d259fb61299110cf241140b279837cfe9480ca6` |
| `mxopackets1_crashover2k_afterwhoruneo_proxy_unsorted_afterwhoruneologunsorted.log` | `logs/mxopackets1_crashover2k_afterwhoruneo_proxy_unsorted_afterwhoruneologunsorted.log.txt` | 1278799 | `3618491435263fd21ad12dba82a0736c00fa5539380605f66b9d9bad033d9c80` |
| `mxopackets1_crashover2k_proxy_unsorted_Packets1.log` | `logs/mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt` | 1767808 | `481dba5f67460e0ea2ae5f2f5da628e90f99dde093f4cdf57d52d19b5fb6810a` |
| `mxopackets1_luizoe_recursion1.log` | `logs/mxopackets1_luizoe_recursion1.log.txt` | 2360287 | `44d5d4efe96dc8759c0e7ac605eaaed44100fac9069e191237a6f25d6a81b078` |

## Intake Next Steps

- Review `SOURCE.md`, `SKIPPED.md`, and `TRIAGE.md` before committing imported logs.
- Preserve the original received archive outside the repository and use the SHA-256 hashes in `SOURCE.md` for duplicate/provenance checks.
- Import only text logs or text-like dumps that contain no account secrets, client binaries, private keys, or unrelated game assets.
- After review, run `scripts/packet-research.sh` to regenerate packet summaries.
