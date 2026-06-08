# Packet Capture Source

- Imported at UTC: `20260607T015831Z`
- Dry-run: `0`
- Source kind: `directory`
- Source input: `/tmp/mxo-next-public-candidates-sanitized`
- Scanned files from: `/tmp/mxo-next-public-candidates-sanitized`
- Sender: `hdneo/mxopackets1 and hdneo/mxopackets2`
- Provenance: `public GitHub Matrix Online packet archives; sanitized derivatives where needed`
- Context: `sanitized public inventory, item-move, manageable last-day, and Rajko near-end logs selected from hdneo/mxopackets1 and hdneo/mxopackets2 for cash/inventory, static interaction, object-state, and endgame packet research; password-bearing auth lines were removed and NUL bytes stripped before import; original source files remain outside Git`
- Max imported file bytes: `12000000`

## Imported Files

| Original path | Imported path | Bytes | SHA-256 |
| --- | --- | ---: | --- |
| `mxopackets1/entilsar/enti_lastday2.log` | `logs/mxopackets1_entilsar_enti_lastday2.log.txt` | 8803529 | `9f6deb102c7b4566b2526f7d3fff618da86074c263e3ed89ad22c600d8c4cf55` |
| `mxopackets1/crashover2k/trinitys/proxy_second_pc/012_trinitys_inventory_session_slot1_row2_empty.log` | `logs/mxopackets1_crashover2k_trinitys_proxy_second_pc_012_trinitys_inventory_session_slot1_row2_empty.log.txt` | 71838 | `0204af4775e895d9475409bc329f0955bea24974a31522170f91c63d4aa544d9` |
| `mxopackets1/crashover2k/trinitys/proxy_second_pc/011_trinity_session.log` | `logs/mxopackets1_crashover2k_trinitys_proxy_second_pc_011_trinity_session.log.txt` | 390544 | `3a108345a29bfb45f56f8ac8b1dc74c50fb98bad7fafe9d8abdaa0507cf7e83f` |
| `mxopackets1/crashover2k/trinitys/proxy_second_pc/012_server_shut_down_announce_and_shutdown.log` | `logs/mxopackets1_crashover2k_trinitys_proxy_second_pc_012_server_shut_down_announce_and_shutdown.log.txt` | 80113 | `a66cd653245cccfe8e58e3df11aba0a4bb20c0072264d8065b3ee0388a0cd89a` |
| `mxopackets1/crashover2k/afterwhoruneo/proxy_second_pc/002_moove_data_scramlber_0.4_from_slot2_row1_place1_to_slot1_row1_place1.log` | `logs/mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_002_moove_data_scramlber_0.4_from_slot2_row1_place1_to_slot1_row1_place1.log.txt` | 443 | `3e6bcc992ad6875fe47612523de5ace196350d27fa46871dde264146c82b829e` |
| `mxopackets1/crashover2k/afterwhoruneo/proxy_second_pc/002_inventory_items_verification.log` | `logs/mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_002_inventory_items_verification.log.txt` | 2658 | `9ec88b67b55ef041e0dbf9ccc4d371ce15b2879905d5050ea90fc449abc57ede` |
| `mxopackets2/RajkoPacketsNearTheEnd/PacketsGotIntoHelAgain.log` | `logs/mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt` | 3498868 | `3f6be82234b40cac8a3e0a27916e174f46934821dab8c8f4d55667d73bdc64af` |
| `mxopackets2/RajkoPacketsNearTheEnd/PacketsSpawnedInMaraCThenExitedViaHardline.log` | `logs/mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedInMaraCThenExitedViaHardline.log.txt` | 201217 | `ade620568e0a4f74daf28c5e5164373e2fd30439a92f52a457e9b9e98237b47d` |
| `mxopackets2/RajkoPacketsNearTheEnd/PacketsMovingAroundDowntown.log` | `logs/mxopackets2_RajkoPacketsNearTheEnd_PacketsMovingAroundDowntown.log.txt` | 2560699 | `f9aaf3c1b73e794bcb432e6307f8898be991a11bc012d204a15edcc1fa5b2066` |
| `mxopackets2/RajkoPacketsNearTheEnd/PacketsWhiteHallwaysWarboyTeleportDoors.log` | `logs/mxopackets2_RajkoPacketsNearTheEnd_PacketsWhiteHallwaysWarboyTeleportDoors.log.txt` | 592985 | `56f57096997e09349c86901c4a51ea1d2b494f68a5e116dc64eb91efa357cda8` |
| `mxopackets2/RajkoPacketsNearTheEnd/PacketsChillinginMaraNoticingSpawns.log` | `logs/mxopackets2_RajkoPacketsNearTheEnd_PacketsChillinginMaraNoticingSpawns.log.txt` | 4181785 | `34155e885d6c62911a7b27f74d80aa98e36eafd5b8b8fa00b650d52b5acbbd9d` |

## Duplicate Hash Leads

These rows matched SHA-256 hashes already present in prior intake manifests. They are review leads, not automatic rejection: a repeated hash can mean an intentional resend, a partial repost, or a renamed/repacked archive.

| Kind | Original path | Imported path | SHA-256 | Previous manifest |
| --- | --- | --- | --- | --- |
| imported file | `mxopackets1/crashover2k/afterwhoruneo/proxy_second_pc/002_inventory_items_verification.log` | `logs/mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_002_inventory_items_verification.log.txt` | `9ec88b67b55ef041e0dbf9ccc4d371ce15b2879905d5050ea90fc449abc57ede` | `research/packet-dumps/community-intake/20260606T201844Z-hdneo-mxopackets1-vendor-marketplace-selected/SOURCE.md:25` |

## Intake Next Steps

- Review `SOURCE.md`, `SKIPPED.md`, and `TRIAGE.md` before committing imported logs.
- Preserve the original received archive outside the repository and use the SHA-256 hashes in `SOURCE.md` for duplicate/provenance checks.
- Import only text logs or text-like dumps that contain no account secrets, client binaries, private keys, or unrelated game assets.
- After review, run `scripts/packet-research.sh` to regenerate packet summaries.
