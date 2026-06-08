# Packet Capture Source

- Imported at UTC: `20260607T200024Z`
- Dry-run: `0`
- Source kind: `directory`
- Source input: `/tmp/mxo-hd-next-small-public-intake`
- Scanned files from: `/tmp/mxo-hd-next-small-public-intake`
- Sender: `public hdneo GitHub archives`
- Provenance: `public GitHub packet logs`
- Context: `Remaining small non-duplicate hdneo mxopackets1 ability loadout, evade-combat, loot, and marketplace-lockpicks text logs; images and large mxopackets2 endgame files left out for separate external triage/storage policy.`
- Max imported file bytes: `8000000`

## Imported Files

| Original path | Imported path | Bytes | SHA-256 |
| --- | --- | ---: | --- |
| `mxopackets1/crashover2k/afterwhoruneo/old_world_packets/Vector_abilityloadoutchange.log` | `logs/mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt` | 5666337 | `1cf87e850b15a1b7893b9587b298e54d65d6eaccbece1046849b46e5901f0bc6` |
| `mxopackets1/crashover2k/afterwhoruneo/proxy/003_abs_evade_Combat2.log` | `logs/mxopackets1_crashover2k_afterwhoruneo_proxy_003_abs_evade_Combat2.log.txt` | 6557 | `ffe1bae3290731a083ac06a741fcfa9cc4297c9783a1f26c220415139afdf18a` |
| `mxopackets1/crashover2k/afterwhoruneo/proxy_second_pc/002_marketplace_lockpicks.log` | `logs/mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_002_marketplace_lockpicks.log.txt` | 894 | `173058070288d3f276f1c763fa68b44d66bf2478fb96b61ff016f3f6819b002e` |
| `mxopackets1/crashover2k/afterwhoruneo/proxy_second_pc/001executeevadecombat.log` | `logs/mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_001executeevadecombat.log.txt` | 17894 | `9112f120875c9aca043c88cc86f534efb0adb006710044659601ae1d38373030` |
| `mxopackets1/crashover2k/afterwhoruneo/old_world_packets_second_pc/others/loot_but_inventory_full_corrupted_got_2000_info.log` | `logs/mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_loot_but_inventory_full_corrupted_got_2000_info.log.txt` | 5743 | `835c32b9b774b063f625662b220e0a68bc7078eb307c122386ba4572185e7092` |
| `mxopackets1/crashover2k/afterwhoruneo/old_world_packets_second_pc/others/loot_fm11sk_codebyte1_from_corrupted.log` | `logs/mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_loot_fm11sk_codebyte1_from_corrupted.log.txt` | 5363 | `757b5e3c9ef310c0485d0667ccefec82dae0b4dd6e1b3fa46d305db448d45960` |
| `mxopackets1/crashover2k/afterwhoruneo/old_world_packets_second_pc/abilities/loot_but_inventory_full_corrupted.log` | `logs/mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_abilities_loot_but_inventory_full_corrupted.log.txt` | 5743 | `835c32b9b774b063f625662b220e0a68bc7078eb307c122386ba4572185e7092` |

## Intake Next Steps

- Review `SOURCE.md`, `SKIPPED.md`, and `TRIAGE.md` before committing imported logs.
- Preserve the original received archive outside the repository and use the SHA-256 hashes in `SOURCE.md` for duplicate/provenance checks.
- Import only text logs or text-like dumps that contain no account secrets, client binaries, private keys, or unrelated game assets.
- After review, run `scripts/packet-research.sh` to regenerate packet summaries.
