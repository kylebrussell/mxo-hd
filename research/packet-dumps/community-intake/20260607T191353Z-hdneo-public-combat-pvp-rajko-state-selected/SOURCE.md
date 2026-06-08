# Packet Capture Source

- Imported at UTC: `20260607T191353Z`
- Dry-run: `0`
- Source kind: `directory`
- Source input: `/tmp/mxo-hd-next-public-combat-state-sanitized`
- Scanned files from: `/tmp/mxo-hd-next-public-combat-state-sanitized`
- Sender: `public hdneo GitHub archives`
- Provenance: `public GitHub packet logs; sanitized derivative`
- Context: `Selected manageable public mxopackets1 combat/PvP/corrupted traces and mxopackets2 Rajko Mara/Cyph/state traces; auth and margin handshake prefixes removed where present; NUL bytes stripped.`
- Max imported file bytes: `12000000`

## Imported Files

| Original path | Imported path | Bytes | SHA-256 |
| --- | --- | ---: | --- |
| `mxopackets1/crashover2k/008_some_pvp_and_teleports_and_some_doors_open.log` | `logs/mxopackets1_crashover2k_008_some_pvp_and_teleports_and_some_doors_open.log.txt` | 3410139 | `b4652ed5ccb3d03e4a9e801222ab30e2646cf6a7ea2975e836d72b636973cec4` |
| `mxopackets1/crashover2k/afterwhoruneo/proxy/003_pvp_and_tele_to_park_east_north_died_in_pvp.log` | `logs/mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt` | 687329 | `e541d5097d3fbf2674f53de384978138de4928ebd6504bdc1972d69fca74cc6d` |
| `mxopackets1/crashover2k/afterwhoruneo/proxy/003_search_and_loot_corrupted.log` | `logs/mxopackets1_crashover2k_afterwhoruneo_proxy_003_search_and_loot_corrupted.log.txt` | 25859 | `0ee6a047c0d41d13b3413d5ee9258288b1c0b0526d88265cd5e5a3d8a3de66cb` |
| `mxopackets1/crashover2k/afterwhoruneo/proxy/003_abs_logic_blast_3_0_vs_corrupted_success.log` | `logs/mxopackets1_crashover2k_afterwhoruneo_proxy_003_abs_logic_blast_3_0_vs_corrupted_success.log.txt` | 14666 | `9c37ca7f3d1767e4722668d8ee00f6188f37c2a23efbf4c1066a4a21fa574f56` |
| `mxopackets1/crashover2k/afterwhoruneo/proxy/003_abs_evade_Combat.log` | `logs/mxopackets1_crashover2k_afterwhoruneo_proxy_003_abs_evade_Combat.log.txt` | 8091 | `ffbb9b33659db8be27e92f6af0b952b78060577de966e85b51d5eb14730c7c9a` |
| `mxopackets1/crashover2k/afterwhoruneo/proxy_second_pc/002_combat_corrupted_spawn_while_jackout_process.log` | `logs/mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_002_combat_corrupted_spawn_while_jackout_process.log.txt` | 56224 | `1841f35966d867ee294510362623c4856360646d7a626aa730cbcbd22845b719` |
| `mxopackets1/crashover2k/afterwhoruneo/proxy_second_pc/002_combat_zombie_lurch.log` | `logs/mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_002_combat_zombie_lurch.log.txt` | 958184 | `6f2e0b1d4823b917b831d176413df4778970e2e1bf7a8907d1254b7f04869915` |
| `mxopackets2/RajkoPacketsNearTheEnd/PacketsMaraCThenTimeDOut.log` | `logs/mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCThenTimeDOut.log.txt` | 8796908 | `f873ca1ce7e53262bc2cf9ec7a63b081592f7b81fbc5b9cfac06dbae7527197b` |
| `mxopackets2/RajkoPacketsNearTheEnd/PacketsSomethingsGoingWrong.log` | `logs/mxopackets2_RajkoPacketsNearTheEnd_PacketsSomethingsGoingWrong.log.txt` | 104843 | `febfb3a4ee371d42d5cc763b8ae59fe31b5a3194278330760ff45bf1e450379c` |
| `mxopackets2/RajkoPacketsNearTheEnd/PacketsSomethingMaracZombiesDowntown.log` | `logs/mxopackets2_RajkoPacketsNearTheEnd_PacketsSomethingMaracZombiesDowntown.log.txt` | 3007729 | `8acf5f4af51bc0f0d6cc3503443ba45e2e17473facd6b31f574ac4dfedf825bf` |
| `mxopackets2/RajkoPacketsNearTheEnd/PacketsLoadingAreaLoopBecauseRecCrashed.log` | `logs/mxopackets2_RajkoPacketsNearTheEnd_PacketsLoadingAreaLoopBecauseRecCrashed.log.txt` | 177909 | `1fddcc3f099c95bbf5818da6edf0f23a2047a4d0ec149d93401ef83d4884885d` |
| `mxopackets2/RajkoPacketsNearTheEnd/PacketsMaraCCyphs.log` | `logs/mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCCyphs.log.txt` | 7784013 | `6dc59288a7d30ddedfdd21408151123c612e628be7deefb217f2a46ee4de98bb` |

## Intake Next Steps

- Review `SOURCE.md`, `SKIPPED.md`, and `TRIAGE.md` before committing imported logs.
- Preserve the original received archive outside the repository and use the SHA-256 hashes in `SOURCE.md` for duplicate/provenance checks.
- Import only text logs or text-like dumps that contain no account secrets, client binaries, private keys, or unrelated game assets.
- After review, run `scripts/packet-research.sh` to regenerate packet summaries.
