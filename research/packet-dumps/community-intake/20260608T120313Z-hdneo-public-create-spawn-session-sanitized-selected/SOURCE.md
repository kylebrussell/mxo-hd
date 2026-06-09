# Packet Capture Source

- Imported at UTC: `20260608T120313Z`
- Dry-run: `0`
- Source kind: `directory`
- Source input: `/tmp/mxo-hdneo-create-spawn-session-selected`
- Scanned files from: `/tmp/mxo-hdneo-create-spawn-session-selected`
- Sender: `hdneo/mxopackets1`
- Provenance: `public hdneo/mxopackets1 archive; sanitized derivative`
- Context: `selected public create-character, login/session, jackin, Morpheus, and Special Agent spawn logs from hdneo/mxopackets1; NUL bytes stripped and only GAME/WORLD packet blocks retained to remove auth/margin handshakes before import`
- Max imported file bytes: `5242880`

## Imported Files

| Original path | Imported path | Bytes | SHA-256 |
| --- | --- | ---: | --- |
| `mxopackets1/crashover2k/morpheuzz_vector/_create_morpheuzz_char.log` | `logs/mxopackets1_crashover2k_morpheuzz_vector__create_morpheuzz_char.log.txt` | 247558 | `91f0eadfec2858d74c443f4e898a8e351b639e225aafbf8da104395d19508009` |
| `mxopackets1/crashover2k/002_create_mxoemu_character2.log` | `logs/mxopackets1_crashover2k_002_create_mxoemu_character2.log.txt` | 1615405 | `7db24f56dd99a75d17ae158f943f80a8af5e35857aecf748c3b4d93266944d6f` |
| `mxopackets1/crashover2k/_create_char_mxoemu_and_make_complete_tutorial+enteringtheworld_uriah.log` | `logs/mxopackets1_crashover2k__create_char_mxoemu_and_make_complete_tutorial_enteringtheworld_uriah.log.txt` | 912040 | `1359f683e0d4583e16b115e9e1a78389ae6d7a4b680290433e2b82ded8683d6d` |
| `mxopackets1/crashover2k/afterwhoruneo/morpheuzzz/001_login_and_died_mara_teleport_to_archan_open_onedoor.log` | `logs/mxopackets1_crashover2k_afterwhoruneo_morpheuzzz_001_login_and_died_mara_teleport_to_archan_open_onedoor.log.txt` | 894453 | `706ff1cb00fa766c27d5cef2a3f1ada73b161520acaac6081a4045cc774379ce` |
| `mxopackets1/crashover2k/afterwhoruneo/old_world_packets_second_pc/others/special_agent_spawn.log` | `logs/mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_special_agent_spawn.log.txt` | 213807 | `8c082f31cbfbd4389c6c47df682dc73846756279796a8339a338ac7872752706` |
| `mxopackets1/crashover2k/afterwhoruneo/old_world_packets_second_pc/logins/loggedinsession001.log` | `logs/mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt` | 973808 | `3e8fcaf15cb1a8967d0b97c608267938384b3a870fbb5a456c06ef81c9af1879` |
| `mxopackets1/crashover2k/mxoemu/001_jackin_coordinates_jackout.log` | `logs/mxopackets1_crashover2k_mxoemu_001_jackin_coordinates_jackout.log.txt` | 649222 | `c0506a6d933f1b70607009eb6a9edb4fae196d706468942547ec7fd4714c5133` |
| `mxopackets1/norajob_acc_logs/002_norajob_create_female_char_mxoemu2.log` | `logs/mxopackets1_norajob_acc_logs_002_norajob_create_female_char_mxoemu2.log.txt` | 107058 | `15a69932b35fb9bb67a9dbcc1aab5790c5b1737b6f661f4621d026350c72c287` |

## Intake Next Steps

- Review `SOURCE.md`, `SKIPPED.md`, and `TRIAGE.md` before committing imported logs.
- Preserve the original received archive outside the repository and use the SHA-256 hashes in `SOURCE.md` for duplicate/provenance checks.
- Import only text logs or text-like dumps that contain no account secrets, client binaries, private keys, or unrelated game assets.
- After review, run `scripts/packet-research.sh` to regenerate packet summaries.
