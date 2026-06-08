# Packet Capture Source

- Imported at UTC: `20260607T203107Z`
- Dry-run: `0`
- Source kind: `directory`
- Source input: `/tmp/mxo-hdneo-vector-state-static-selected`
- Scanned files from: `/tmp/mxo-hdneo-vector-state-static-selected`
- Sender: `public hdneo GitHub archives`
- Provenance: `public GitHub packet logs; NUL-stripped derivative where needed`
- Context: `Selected public mxopackets1 Vector old-world, teleport, unknown-object, and disconnect traces for vendor-window, CR1/CR2 static interaction, Object599/profile, object-state, ability/effect, and selector-ff movement-boundary research; NUL bytes stripped from Vector35 only; no auth/password markers were present in the staged source.`
- Max imported file bytes: `6000000`

## Imported Files

| Original path | Imported path | Bytes | SHA-256 |
| --- | --- | ---: | --- |
| `mxopackets1/crashover2k/007_lost_connection.log` | `logs/mxopackets1_crashover2k_007_lost_connection.log.txt` | 698737 | `60e456f0bcde170f44cf1db7f838b418d6d85d9914e9c17f6941e05fcd5d37c8` |
| `mxopackets1/crashover2k/afterwhoruneo/old_world_packets/Vector6.log` | `logs/mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector6.log.txt` | 1374077 | `43e0273cb5139e6260e39ced641e134a4bdb15892a03e2dfb48175a777808032` |
| `mxopackets1/crashover2k/afterwhoruneo/old_world_packets/Vector2.log` | `logs/mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector2.log.txt` | 1191959 | `b91a9ceef26ce1654925aa854df5bb047cb13d788cfd90c516da4fbc7fecbd16` |
| `mxopackets1/crashover2k/afterwhoruneo/old_world_packets/Vector12.log` | `logs/mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector12.log.txt` | 2624855 | `107451102199657fc1e90755bd6b753ea0d6f3dbfddba2baca6462a6a287e3a4` |
| `mxopackets1/crashover2k/afterwhoruneo/old_world_packets/Vector28.log` | `logs/mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector28.log.txt` | 4187474 | `491f666ce611df677f10bf447568c308ef15783121853e41246625f9030cd2af` |
| `mxopackets1/crashover2k/afterwhoruneo/old_world_packets/Vector14_walking_to_courty.log` | `logs/mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector14_walking_to_courty.log.txt` | 2338947 | `56a7a624260ebe1fd46d4e560ce211af9d838ca47f4a88bf98d8d9da6c1bc627` |
| `mxopackets1/crashover2k/afterwhoruneo/old_world_packets/Vector26.log` | `logs/mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26.log.txt` | 2847980 | `ddcc5857ab9e5f45b1cb192a01a9673126ab0710336a9a624d6c849abe981da9` |
| `mxopackets1/crashover2k/afterwhoruneo/old_world_packets/Vector35.log` | `logs/mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt` | 1629838 | `a13d739672ef711b4a23ef3e06b6bd7afa907ba6842472b52d1481942e6608be` |
| `mxopackets1/crashover2k/afterwhoruneo/old_world_packets/Vector45.log` | `logs/mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector45.log.txt` | 3317739 | `f793589a89779373164baf954aefcbdf7211c8a8ac55d846d27b58905c400b9b` |
| `mxopackets1/crashover2k/afterwhoruneo/old_world_packets/Vector49_with_unknown_objects.log` | `logs/mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector49_with_unknown_objects.log.txt` | 856219 | `96b1cc7024a2cc85c4973608b1fb7094072cd14c730e9498a6d251ac7226be3f` |
| `mxopackets1/crashover2k/afterwhoruneo/old_world_packets_second_pc/teleport/unsorted_tels/allteleportetc001.log` | `logs/mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_teleport_unsorted_tels_allteleportetc001.log.txt` | 1049612 | `f735738a624dd9c5ff478a109524df931567fe3cb90346504baa3a4af80d45a3` |
| `mxopackets1/crashover2k/afterwhoruneo/old_world_packets_second_pc/Neuer Ordner/Kopie von Unknown.log` | `logs/mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_Neuer_Ordner_Kopie_von_Unknown.log.txt` | 1811184 | `a9a621f5bb4cd9209dfa8753fc567f120e6db2ab18573e164d28845684e125c5` |

## Intake Next Steps

- Review `SOURCE.md`, `SKIPPED.md`, and `TRIAGE.md` before committing imported logs.
- Preserve the original received archive outside the repository and use the SHA-256 hashes in `SOURCE.md` for duplicate/provenance checks.
- Import only text logs or text-like dumps that contain no account secrets, client binaries, private keys, or unrelated game assets.
- After review, run `scripts/packet-research.sh` to regenerate packet summaries.
