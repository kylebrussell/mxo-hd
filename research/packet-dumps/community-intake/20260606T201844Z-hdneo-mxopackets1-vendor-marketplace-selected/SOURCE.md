# Packet Capture Source

- Imported at UTC: `20260606T201844Z`
- Dry-run: `0`
- Source kind: `directory`
- Source input: `/tmp/hdneo-mxopackets1-selected`
- Scanned files from: `/tmp/hdneo-mxopackets1-selected`
- Sender: `hdneo/mxopackets1`
- Provenance: `public-github-packet-archive`
- Context: `Selected public vendor, marketplace, inventory, loot, and item movement logs from hdneo/mxopackets1 for packet-format research.`
- Max imported file bytes: `5242880`

## Imported Files

| Original path | Imported path | Bytes | SHA-256 |
| --- | --- | ---: | --- |
| `crashover2k/trinitys/proxy_second_pc/008_buyed_weapons_for_30000_info_per_weapon_all_place_to_first_slot_every_row_4_times_same_weapon.log` | `logs/crashover2k_trinitys_proxy_second_pc_008_buyed_weapons_for_30000_info_per_weapon_all_place_to_first_slot_every_row_4_times_same_weapon.log.txt` | 245592 | `6b5163c9c70503e7b0e5b865bcded28d928b8fb32b017d4b3fb3c3c395b75162` |
| `crashover2k/afterwhoruneo/proxy/003_vendor_prop_vendor.log` | `logs/crashover2k_afterwhoruneo_proxy_003_vendor_prop_vendor.log.txt` | 3784 | `3a26e52c3055a65753740a945be14d0aed889c98c48045c5f20d222511bde916` |
| `crashover2k/afterwhoruneo/proxy/003_vendor_weapon.log` | `logs/crashover2k_afterwhoruneo_proxy_003_vendor_weapon.log.txt` | 1601 | `46b6df97070926eb51fae2de94c5ec25891adbe8c794828fe31896f236d3919e` |
| `crashover2k/afterwhoruneo/proxy_second_pc/002_moove_item_slot1_row1_place4_to_place1.log` | `logs/crashover2k_afterwhoruneo_proxy_second_pc_002_moove_item_slot1_row1_place4_to_place1.log.txt` | 1334 | `f1e2f1c237edd01cf9effaadf9e408ba7bb14121d5b91e15ead365617b5013eb` |
| `crashover2k/afterwhoruneo/proxy_second_pc/002_marketplace_place_swat_disguise_to_sell_for_1000_info.log` | `logs/crashover2k_afterwhoruneo_proxy_second_pc_002_marketplace_place_swat_disguise_to_sell_for_1000_info.log.txt` | 2095 | `9635c952ff64171aa4b6edb15b807b87a8022e3030611f24e2ca7f969b680a0c` |
| `crashover2k/afterwhoruneo/proxy_second_pc/002_marketplace_codes_with_all_subcats.log` | `logs/crashover2k_afterwhoruneo_proxy_second_pc_002_marketplace_codes_with_all_subcats.log.txt` | 47008 | `9d4cba6da65b2ff091c96015cf619e5b5321594c6f54fefd849ae24a2b8e7918` |
| `crashover2k/afterwhoruneo/proxy_second_pc/001lootall_corrupted.log` | `logs/crashover2k_afterwhoruneo_proxy_second_pc_001lootall_corrupted.log.txt` | 4316 | `75fee8c7a7cd1f20df91c0748464c838478027f0048bfa14aae689e9a37b4f05` |
| `crashover2k/afterwhoruneo/proxy_second_pc/002_marketplace_place_lockpick_code_to_sell_for_1000_info.log` | `logs/crashover2k_afterwhoruneo_proxy_second_pc_002_marketplace_place_lockpick_code_to_sell_for_1000_info.log.txt` | 1923 | `e4157cd634b3d6c242992911fc2e91dedc71fa2981ea7010243ae597c93faa50` |
| `crashover2k/afterwhoruneo/proxy_second_pc/001_inventory_items_verification.log` | `logs/crashover2k_afterwhoruneo_proxy_second_pc_001_inventory_items_verification.log.txt` | 2658 | `9ec88b67b55ef041e0dbf9ccc4d371ce15b2879905d5050ea90fc449abc57ede` |
| `crashover2k/afterwhoruneo/proxy_second_pc/002_request_open_marketplace_loadingarea.log` | `logs/crashover2k_afterwhoruneo_proxy_second_pc_002_request_open_marketplace_loadingarea.log.txt` | 630 | `ce4c8bdb3d1333c8a47a7269437ba86aeb05c2d059c429e2253d68d7dfb139ba` |
| `crashover2k/afterwhoruneo/proxy_second_pc/002_buy_shielded_happy_grass_g-met_t-shirt_from_marketplace_for_6000_info.log` | `logs/crashover2k_afterwhoruneo_proxy_second_pc_002_buy_shielded_happy_grass_g-met_t-shirt_from_marketplace_for_6000_info.log.txt` | 2077 | `cf4ff4daa1d0618952af3fe4cf41da89d36d90ce0ab458e50c2e3f23484b10b9` |

## Intake Next Steps

- Review `SOURCE.md`, `SKIPPED.md`, and `TRIAGE.md` before committing imported logs.
- Preserve the original received archive outside the repository and use the SHA-256 hashes in `SOURCE.md` for duplicate/provenance checks.
- Import only text logs or text-like dumps that contain no account secrets, client binaries, private keys, or unrelated game assets.
- After review, run `scripts/packet-research.sh` to regenerate packet summaries.
