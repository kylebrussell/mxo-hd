# Packet Capture Triage Report

- Source kind: `directory`
- Source input: `/Users/kyle/Developer/mxo-hd/research/packet-dumps/community-intake/20260607T032533Z-hdneo-public-movement-spawn-hardline-sanitized-selected/logs`
- Scanned files from: `/Users/kyle/Developer/mxo-hd/research/packet-dumps/community-intake/20260607T032533Z-hdneo-public-movement-spawn-hardline-sanitized-selected/logs`
- Generated UTC: `2026-06-07T03:25:39Z`
- Max sample lines per pattern: `10`

Use this report to decide whether received logs are worth importing into `research/packet-dumps/`. Matches are byte-string leads, not decoded packet claims.

## Vendor And Interaction Leads

### SERVER_VENDOR_OPEN candidate

Signature: `81 0d` (spaced or compact hex)

Matches: 4

```text
mxopackets1_crashover2k_afterwhoruneo_proxy_026_open_doors_and_some_hyper_jump_killed_by_Agent_etc.log.txt:5158:c5 f8 df ec 44 41 20 6b 47 00 00 04 01 01 27 01 6a 81 0d 7c ad d9 43 00 00 00 00 00 c0 b2 c0 00
mxopackets1_crashover2k_afterwhoruneo_proxy_026_open_doors_and_some_hyper_jump_killed_by_Agent_etc.log.txt:71897:a4 38 68 46 80 80 80 80 80 81 0d 01 07 08 00 00 11 00 02 0c ba b9 3a e1 46 00 80 fc c3 79 6c 47
mxopackets1_crashover2k_afterwhoruneo_proxy_026_open_doors_and_some_hyper_jump_killed_by_Agent_etc.log.txt:77485:02 03 29 00 02 08 81 0d ee 46 00 e0 88 44 4c d8 5e 46 21 00 02 0c 1f f5 fa 09 47 00 20 8a c4 fe
mxopackets1_crashover2k_afterwhoruneo_proxy_026_open_doors_and_some_hyper_jump_killed_by_Agent_etc.log.txt:78960:02 03 2d 00 02 0c 81 0d 28 0c 47 00 80 fc c3 97 dd 99 46 21 00 02 0c 82 dc f8 09 47 00 20 8a c4
```

### CLIENT_VENDOR_BUY candidate

Signature: `81 0e` (spaced or compact hex)

Matches: 2

```text
mxopackets2_RajkoPacketsNearTheEnd_PacketsMovementAloneInWhiteHalls.log.txt:3970:02 03 02 00 01 0a 03 81 0e 34 48 00 40 62 c4 e4 0a 46 47 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_026_open_doors_and_some_hyper_jump_killed_by_Agent_etc.log.txt:4510:00 02 0e 00 f9 4f aa c2 c5 f8 df ec 44 39 e8 4a 47 07 00 02 0c ff 98 81 0e c6 f8 df ec 44 3a 60
```

### CLIENT_VENDOR_SELL candidate

Signature: `81 11` (spaced or compact hex)

Matches: 5

```text
mxopackets1_crashover2k_afterwhoruneo_proxy_026_open_doors_and_some_hyper_jump_killed_by_Agent_etc.log.txt:52270:02 03 02 00 03 08 79 81 11 c7 36 d6 d3 45 90 83 15 45 f3 5b 55 05 ff 01 4e 01 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_026_open_doors_and_some_hyper_jump_killed_by_Agent_etc.log.txt:52276:01 22 00 00 00 00 00 00 00 24 00 01 00 04 79 81 11 c7 36 d6 d3 45 90 83 15 45 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_026_open_doors_and_some_hyper_jump_killed_by_Agent_etc.log.txt:108648:82 f4 b9 b0 47 04 01 00 b5 01 81 11 81 f3 19 00 39 00 3c 00 56 9c 2b 00 cd e7 3c 4a 03 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_026_open_doors_and_some_hyper_jump_killed_by_Agent_etc.log.txt:108684:02 04 01 00 b6 01 81 11 81 f3 19 00 39 00 3c 00 33 8f 2b 00 da 32 30 4a 03 00 00 00 00 4a 00 45
mxopackets1_crashover2k_afterwhoruneo_proxy_026_open_doors_and_some_hyper_jump_killed_by_Agent_etc.log.txt:108904:82 06 c4 b0 47 04 01 00 c6 01 81 11 81 f3 19 00 39 00 3c 00 56 9c 2b 00 cd e7 3c 4a 03 00 00 00
```

### CR2 static object interaction

Signature: `80 c8` (spaced or compact hex)

Matches: 43

```text
mxopackets1_crashover2k_afterwhoruneo_proxy_024_chatting_hyperjump_fight.log.txt:18894:28 00 4a 00 32 00 00 00 03 01 08 17 00 00 00 00 80 c8 00 75 00 7e 00 65 00 00 00 00 00 01 17 00
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_005_afterwhoruneo_login_open_door_and_fight.log.txt:4400:02 04 01 00 22 01 08 80 c8 08 00 f0 34 03 00
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_005_afterwhoruneo_login_open_door_and_fight.log.txt:4600:02 04 01 00 25 01 08 80 c8 08 00 f0 34 03 00
mxopackets1_crashover2k_afterwhoruneo_proxy_026_open_doors_and_some_hyper_jump_killed_by_Agent_etc.log.txt:14835:02 04 01 00 82 01 08 80 c8 0b 00 a0 2b 03 00
mxopackets1_crashover2k_afterwhoruneo_proxy_026_open_doors_and_some_hyper_jump_killed_by_Agent_etc.log.txt:14994:02 04 01 00 85 01 08 80 c8 08 00 a0 2b 03 00
mxopackets1_crashover2k_afterwhoruneo_proxy_026_open_doors_and_some_hyper_jump_killed_by_Agent_etc.log.txt:15761:02 03 02 00 01 0a 00 16 e3 99 c6 f8 df ec 44 c4 01 35 47 00 00 04 01 00 8c 01 08 80 c8 b8 1f 80
mxopackets1_crashover2k_afterwhoruneo_proxy_026_open_doors_and_some_hyper_jump_killed_by_Agent_etc.log.txt:16662:02 03 02 00 01 0a 00 8f 21 9a c6 f8 df ec 44 60 99 2a 47 00 00 04 01 00 91 01 08 80 c8 54 3d a0
mxopackets1_crashover2k_afterwhoruneo_proxy_026_open_doors_and_some_hyper_jump_killed_by_Agent_etc.log.txt:17381:02 04 01 00 9d 01 08 80 c8 53 3d a0 2b 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_026_open_doors_and_some_hyper_jump_killed_by_Agent_etc.log.txt:18785:02 04 01 00 a3 01 08 80 c8 05 00 40 07 03 00
mxopackets1_crashover2k_afterwhoruneo_proxy_026_open_doors_and_some_hyper_jump_killed_by_Agent_etc.log.txt:18844:02 04 01 00 a5 01 08 80 c8 02 00 40 07 03 00
```

### CR1 static object interaction

Signature: `80 c3` (spaced or compact hex)

Matches: 93

```text
mxopackets2_RajkoPacketsNearTheEnd_PacketsJumpingAloneInWhiteHallways.log.txt:789:48 8c c0 00 00 00 00 10 6b e8 40 00 00 04 01 01 5f 01 06 80 c3 6a 59 4a 10
mxopackets2_RajkoPacketsNearTheEnd_PacketsJumpingAloneInWhiteHallways.log.txt:1092:00 00 00 00 10 6b e8 40 00 00 04 01 01 60 01 06 80 c3 21 78 4a 10
mxopackets2_RajkoPacketsNearTheEnd_PacketsJumpingAloneInWhiteHallways.log.txt:1469:00 00 00 00 10 6b e8 40 00 00 04 01 01 61 01 06 80 c3 16 97 4a 10
mxopackets2_RajkoPacketsNearTheEnd_PacketsJumpingAloneInWhiteHallways.log.txt:1780:00 00 00 00 10 6b e8 40 00 00 04 01 01 62 01 06 80 c3 f7 b8 4a 10
mxopackets2_RajkoPacketsNearTheEnd_PacketsJumpingAloneInWhiteHallways.log.txt:1858:00 60 f7 d8 06 41 00 00 00 00 00 48 8c c0 00 00 00 00 10 6b e8 40 00 00 04 01 01 62 01 06 80 c3
mxopackets2_RajkoPacketsNearTheEnd_PacketsJumpingAloneInWhiteHallways.log.txt:2081:80 c3 38 d1 4a 10
mxopackets2_RajkoPacketsNearTheEnd_PacketsJumpingAloneInWhiteHallways.log.txt:2451:82 cf 62 09 49 03 02 00 03 01 08 00 80 80 10 6e a3 8a 21 00 00 04 01 01 64 01 06 80 c3 0d e4 4a
mxopackets2_RajkoPacketsNearTheEnd_PacketsJumpingAloneInWhiteHallways.log.txt:2891:00 00 00 f5 6a e8 40 00 00 04 01 01 65 01 06 80 c3 c1 11 4b 10
mxopackets2_RajkoPacketsNearTheEnd_PacketsJumpingAloneInWhiteHallways.log.txt:3193:48 8c c0 00 00 00 00 f5 6a e8 40 00 00 04 01 01 66 01 06 80 c3 5c 28 4b 10
mxopackets1_crashover2k_afterwhoruneo_proxy_003_abs_hyperjump_only_up_and_down.log.txt:19:04 01 03 97 01 06 80 c3 bf 97 be 00
```

### SERVER object/state bundle

Signature: `80 bc` (spaced or compact hex)

Matches: 10696

```text
mxopackets1_crashover2k_afterwhoruneo_proxy_003_loading_area_teleport_to_bathary_row.log.txt:13:82 72 80 03 49 04 01 00 a5 1e 16 80 bc 56 00 80 01 00 0a 95 00 00 06 04 02 00 00 00 01 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_loading_area_teleport_to_bathary_row.log.txt:14:00 16 80 bc 56 00 81 01 00 0a 95 00 00 b4 00 11 00 00 00 01 00 00 00 00 16 80 bc 56 00 82 01 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_loading_area_teleport_to_bathary_row.log.txt:15:0a 95 00 00 b5 00 11 00 00 00 01 00 00 00 00 16 80 bc 56 00 83 01 00 0a 95 00 00 07 04 02 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_loading_area_teleport_to_bathary_row.log.txt:16:00 01 00 00 00 00 16 80 bc 56 00 84 01 00 95 b7 00 00 b4 00 16 00 00 00 01 00 00 00 00 16 80 bc
mxopackets1_crashover2k_afterwhoruneo_proxy_003_loading_area_teleport_to_bathary_row.log.txt:17:56 00 85 01 00 95 b7 00 00 09 04 16 00 00 00 01 00 00 00 00 16 80 bc 56 00 86 01 00 95 b7 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_loading_area_teleport_to_bathary_row.log.txt:18:9d 00 16 00 00 00 01 00 00 00 00 16 80 bc 56 00 87 01 00 95 b7 00 00 b5 00 16 00 00 00 01 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_loading_area_teleport_to_bathary_row.log.txt:19:00 00 16 80 bc 56 00 88 01 00 24 b3 00 00 89 02 02 00 00 00 01 00 00 00 00 16 80 bc 56 00 89 01
mxopackets1_crashover2k_afterwhoruneo_proxy_003_loading_area_teleport_to_bathary_row.log.txt:20:00 24 b3 00 00 1a 04 01 00 00 00 01 00 00 00 00 16 80 bc 56 00 8a 01 00 24 b3 00 00 b4 00 21 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_loading_area_teleport_to_bathary_row.log.txt:21:00 00 01 00 00 00 00 16 80 bc 56 00 8b 01 00 24 b3 00 00 09 04 21 00 00 00 01 00 00 00 00 16 80
mxopackets1_crashover2k_afterwhoruneo_proxy_003_loading_area_teleport_to_bathary_row.log.txt:22:bc 56 00 8c 01 00 24 b3 00 00 15 04 02 00 00 00 01 00 00 00 00 16 80 bc 56 00 8d 01 00 24 b3 00
```

## Selector ff Vendor Body Leads

### Selector ff vendor field +9 family

Signature: `e0 2d 81 0d` (spaced or compact hex)

Matches: 0

### Selector ff vendor body prefix

Signature: `01 43 00 00 00 00 00 00 00 e0 2d 81 0d 22 89 20` (spaced or compact hex)

Matches: 0

### Selector ff continuation prefix 00 41 00 ff

Signature: `00 41 00 ff` (spaced or compact hex)

Matches: 0

### Selector ff continuation prefix 00 01 00 ff

Signature: `00 01 00 ff` (spaced or compact hex)

Matches: 223

```text
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoLoadingAreaThenJackedIntoMaraCThenLeft.log.txt:1807:00 00 00 00 00 00 00 00 00 0b 80 bd 05 11 00 00 00 00 00 00 01 00 ff 01 30 80 f5 bc 7f 00 00 54
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoLoadingAreaThenJackedIntoMaraCThenLeft.log.txt:1950:00 01 00 ff 00 00 00 00 fe af 00 00 00 00 00 00 22 00 00 00 00 00 00 01 00 22 00 02 0e 05 0f 1a
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoLoadingAreaThenJackedIntoMaraCThenLeft.log.txt:2376:07 07 00 91 59 00 00 ff 00 00 00 00 00 20 00 00 04 cc 00 00 00 00 00 00 00 00 01 00 ff aa 04 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoLoadingAreaThenJackedIntoMaraCThenLeft.log.txt:2402:00 00 00 00 20 00 00 04 cc 00 00 00 00 00 00 00 00 01 00 ff aa 04 00 04 fe b4 00 00 20 00 10 01
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoLoadingAreaThenJackedIntoMaraCThenLeft.log.txt:2427:00 00 00 00 20 00 00 04 cc 00 00 00 00 00 00 00 00 01 00 ff aa 04 00 04 fe b4 00 00 20 00 10 01
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoLoadingAreaThenJackedIntoMaraCThenLeft.log.txt:2448:00 00 00 00 00 01 00 ff aa 04 00 04 fe b4 00 00 20 00 10 01 22 03 75 01 00 00 00 01 00 2f 00 01
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoLoadingAreaThenJackedIntoMaraCThenLeft.log.txt:2464:00 bc 7f 00 00 ff ff 7b 22 00 00 d7 30 46 12 27 00 00 00 00 00 00 00 00 01 00 ff 00 00 00 00 f4
mxopackets2_RajkoPacketsNearTheEnd_PacketsLoggedIntoMaraC5PeopleStandingAround.log.txt:1224:ba 00 00 30 06 00 28 23 00 00 00 00 00 00 00 00 01 00 ff 00 00 00 00 32 73 00 00 00 00 00 01 22
mxopackets2_RajkoPacketsNearTheEnd_PacketsLoggedIntoMaraC5PeopleStandingAround.log.txt:1244:00 ff 00 5f ba 00 00 30 06 00 28 23 00 00 00 00 00 00 00 00 01 00 ff 00 00 00 00 32 73 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsLoggedIntoMaraC5PeopleStandingAround.log.txt:1276:ba 00 00 30 06 00 28 23 00 00 00 00 00 00 00 00 01 00 ff 00 00 00 00 32 73 00 00 00 00 00 01 22
```

### Selector ff continuation prefix 00 01 04 ff

Signature: `00 01 04 ff` (spaced or compact hex)

Matches: 9

```text
mxopackets1_crashover2k_afterwhoruneo_proxy_024_chatting_hyperjump_fight.log.txt:9522:02 03 02 00 01 04 ff 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsJumpingAloneInWhiteHallways.log.txt:2796:02 03 02 00 01 04 ff 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_005_afterwhoruneo_login_open_door_and_fight.log.txt:1202:02 03 02 00 01 04 ff 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsJustAnotherMaraCHangout.log.txt:3007:02 03 02 00 01 04 ff 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsHangingOutInMaraCRajkoHaxor.log.txt:25216:02 03 02 00 01 04 ff 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_026_open_doors_and_some_hyper_jump_killed_by_Agent_etc.log.txt:15594:02 03 02 00 01 04 ff 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_026_open_doors_and_some_hyper_jump_killed_by_Agent_etc.log.txt:19182:02 03 02 00 01 04 ff 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_026_open_doors_and_some_hyper_jump_killed_by_Agent_etc.log.txt:52621:02 03 02 00 01 04 ff 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_026_open_doors_and_some_hyper_jump_killed_by_Agent_etc.log.txt:52692:02 03 02 00 01 04 ff 00 00
```

### Selector ff continuation prefix 00 01 10 ff

Signature: `00 01 10 ff` (spaced or compact hex)

Matches: 19

```text
mxopackets2_RajkoPacketsNearTheEnd_PacketsJustAnotherMaraCHangout.log.txt:5531:00 00 00 00 00 01 10 ff 00 00 00 00 00 00 00 00 00 00 08 00 12 00 00 00 00 00 00 01 00 0f 00 01
mxopackets2_RajkoPacketsNearTheEnd_PacketsJustAnotherMaraCHangout.log.txt:5551:25 00 00 00 00 00 00 00 00 01 10 ff 00 00 00 00 00 00 00 00 00 00 08 00 12 00 00 00 00 00 00 01
mxopackets2_RajkoPacketsNearTheEnd_PacketsJustAnotherMaraCHangout.log.txt:5580:28 25 00 00 00 00 00 00 00 00 01 10 ff 00 00 00 00 00 00 00 00 00 00 08 00 12 00 00 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsJustAnotherMaraCHangout.log.txt:5595:00 00 00 00 01 10 ff 00 00 00 00 00 00 00 00 00 00 08 00 12 00 00 00 00 00 00 01 00 13 00 01 2a
mxopackets2_RajkoPacketsNearTheEnd_PacketsJustAnotherMaraCHangout.log.txt:5670:06 00 28 25 00 00 00 00 00 00 00 00 01 10 ff 00 00 00 00 00 00 00 00 00 00 08 00 12 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsJustAnotherMaraCHangout.log.txt:5695:0e 00 00 00 00 00 ff 9b 00 00 00 00 56 06 00 28 25 00 00 00 00 00 00 00 00 01 10 ff 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsJustAnotherMaraCHangout.log.txt:5729:00 00 00 00 ff 9b 00 00 00 00 56 06 00 28 25 00 00 00 00 00 00 00 00 01 10 ff 00 00 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsHangingOutInMaraCRajkoHaxor.log.txt:10812:00 00 00 e8 0b 00 3d 91 00 00 ff ff 00 00 00 00 44 00 00 04 c5 00 00 00 00 00 00 00 00 01 10 ff
mxopackets2_RajkoPacketsNearTheEnd_PacketsHangingOutInMaraCRajkoHaxor.log.txt:10852:00 e8 0b 00 3d 91 00 00 ff ff 00 00 00 00 44 00 00 04 c5 00 00 00 00 00 00 00 00 01 10 ff 5f 01
mxopackets2_RajkoPacketsNearTheEnd_PacketsHangingOutInMaraCRajkoHaxor.log.txt:12910:00 00 00 00 00 00 01 10 ff 5f 01 00 4e cf 16 01 00 00 00 08 00 12 05 57 04 00 00 00 01 00 00 00
```

### Selector ff continuation prefix 00 00 00 ff

Signature: `00 00 00 ff` (spaced or compact hex)

Matches: 4412

```text
mxopackets1_crashover2k_afterwhoruneo_proxy_003_loading_area_teleport_to_bathary_row.log.txt:560:00 00 00 8a 04 00 00 00 00 1d 00 00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_loading_area_teleport_to_bathary_row.log.txt:664:ff 00 00 00 00 00 00 00 00 8a 04 00 00 00 00 1d 00 00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoLoadingAreaThenJackedIntoMaraCThenLeft.log.txt:1947:c0 00 68 00 50 00 00 00 00 01 00 00 00 00 00 00 00 00 ff 00 00 00 00 28 0a 32 00 00 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoLoadingAreaThenJackedIntoMaraCThenLeft.log.txt:1948:00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ff 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoLoadingAreaThenJackedIntoMaraCThenLeft.log.txt:2126:01 cd cc 4c 3e 00 00 00 00 ff 00 00 00 00 28 0a 32 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoLoadingAreaThenJackedIntoMaraCThenLeft.log.txt:2127:00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00 00 28 0a
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoLoadingAreaThenJackedIntoMaraCThenLeft.log.txt:2128:00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ff 00 00 00 00 ef
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoLoadingAreaThenJackedIntoMaraCThenLeft.log.txt:2150:01 cd cc 4c 3e 00 00 00 00 ff 00 00 00 00 28 0a 32 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoLoadingAreaThenJackedIntoMaraCThenLeft.log.txt:2151:00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00 00 28 0a
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoLoadingAreaThenJackedIntoMaraCThenLeft.log.txt:2152:00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ff 00 00 00 00 ef
```

### Vendor-open continuation resource shape

Signature: `00 41 00 ff 00 00 00 00 b0 74` (spaced or compact hex)

Matches: 0

### Vendor-sell continuation resource shape

Signature: `00 00 00 ff 00 00 00 00 1b ee` (spaced or compact hex)

Matches: 0

## Object599 / NPC_BASE Spawn-Profile Leads

### Object599 creation prefix

Signature: `0c 57 02` (spaced or compact hex)

Matches: 340

```text
mxopackets1_crashover2k_afterwhoruneo_proxy_003_loading_area_teleport_to_bathary_row.log.txt:344:02 03 02 00 02 80 80 80 80 80 80 01 00 00 00 01 01 00 0c 57 02 7e cd ab 11 8e 42 61 74 68 61 72
mxopackets1_crashover2k_afterwhoruneo_proxy_003_loading_area_teleport_to_bathary_row.log.txt:348:0c 57 02 7f cd ab 12 8e 42 61 74 68 61 72 79 20 42 6f 79 73 20 52 75 73 74 6c 65 72 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_loading_area_teleport_to_bathary_row.log.txt:351:08 00 00 81 c0 09 00 00 02 07 0f 00 00 01 00 0c 57 02 80 cd ab 11 8e 42 61 74 68 61 72 79 20 42
mxopackets1_crashover2k_afterwhoruneo_proxy_003_loading_area_teleport_to_bathary_row.log.txt:354:40 c0 01 82 03 a8 1a bc 02 b2 09 bc 02 07 08 00 00 81 ca 1e 00 00 02 06 0d 00 00 01 00 0c 57 02
mxopackets1_crashover2k_afterwhoruneo_proxy_003_loading_area_teleport_to_bathary_row.log.txt:393:82 aa 80 03 49 03 01 00 0c 57 02 82 cd ab 11 8e 42 61 74 68 61 72 79 20 42 6f 79 73 20 47 75 6e
mxopackets1_crashover2k_afterwhoruneo_proxy_003_loading_area_teleport_to_bathary_row.log.txt:396:ee 02 b2 09 ee 02 07 08 00 00 81 ca 1e 00 00 02 07 09 00 00 01 00 0c 57 02 83 cd ab 12 8e 42 61
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoLoadingAreaThenJackedIntoMaraCThenLeft.log.txt:1411:82 62 a4 0c 49 03 01 00 0c 57 02 41 cd ab 13 8e 42 6c 61 63 6b 77 6f 6f 64 20 47 6f 6f 66 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsLoggedIntoMaraC5PeopleStandingAround.log.txt:2915:82 43 a2 12 49 03 01 00 0c 57 02 41 cd ab 13 8e 42 6c 61 63 6b 77 6f 6f 64 20 47 6f 6f 66 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_024_chatting_hyperjump_fight.log.txt:13513:02 03 01 00 0c 57 02 3d cd ab 13 ae 43 6f 72 72 75 70 74 65 64 00 00 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_teleports_002_teleport_from_loading_area_to_westview_bathary_row_SE.log.txt:474:00 00 00 00 00 ba d3 40 00 00 00 00 f2 04 35 bf 00 00 00 00 f3 04 35 3f 11 00 00 01 00 0c 57 02
```

### Adjacent Object599 boundary prefix

Signature: `00 0c 57 02` (spaced or compact hex)

Matches: 333

```text
mxopackets1_crashover2k_afterwhoruneo_proxy_003_loading_area_teleport_to_bathary_row.log.txt:344:02 03 02 00 02 80 80 80 80 80 80 01 00 00 00 01 01 00 0c 57 02 7e cd ab 11 8e 42 61 74 68 61 72
mxopackets1_crashover2k_afterwhoruneo_proxy_003_loading_area_teleport_to_bathary_row.log.txt:351:08 00 00 81 c0 09 00 00 02 07 0f 00 00 01 00 0c 57 02 80 cd ab 11 8e 42 61 74 68 61 72 79 20 42
mxopackets1_crashover2k_afterwhoruneo_proxy_003_loading_area_teleport_to_bathary_row.log.txt:354:40 c0 01 82 03 a8 1a bc 02 b2 09 bc 02 07 08 00 00 81 ca 1e 00 00 02 06 0d 00 00 01 00 0c 57 02
mxopackets1_crashover2k_afterwhoruneo_proxy_003_loading_area_teleport_to_bathary_row.log.txt:393:82 aa 80 03 49 03 01 00 0c 57 02 82 cd ab 11 8e 42 61 74 68 61 72 79 20 42 6f 79 73 20 47 75 6e
mxopackets1_crashover2k_afterwhoruneo_proxy_003_loading_area_teleport_to_bathary_row.log.txt:396:ee 02 b2 09 ee 02 07 08 00 00 81 ca 1e 00 00 02 07 09 00 00 01 00 0c 57 02 83 cd ab 12 8e 42 61
mxopackets2_RajkoPacketsNearTheEnd_PacketsLoggedIntoMaraC5PeopleStandingAround.log.txt:2915:82 43 a2 12 49 03 01 00 0c 57 02 41 cd ab 13 8e 42 6c 61 63 6b 77 6f 6f 64 20 47 6f 6f 66 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoLoadingAreaThenJackedIntoMaraCThenLeft.log.txt:1411:82 62 a4 0c 49 03 01 00 0c 57 02 41 cd ab 13 8e 42 6c 61 63 6b 77 6f 6f 64 20 47 6f 6f 66 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_027_just_login_and_spawn_on_rogers_way_ctrl_And_jackout.log.txt:937:02 03 01 00 0c 57 02 a2 cd ab 11 8e 41 2e 53 2e 50 2e 20 4d 61 73 74 65 72 20 53 65 72 67 65 61
mxopackets1_crashover2k_afterwhoruneo_proxy_027_just_login_and_spawn_on_rogers_way_ctrl_And_jackout.log.txt:940:3f 02 07 08 00 00 81 ec 1a 00 00 02 05 45 00 00 01 00 0c 57 02 a3 cd ab 13 8e 41 2e 53 2e 50 2e
mxopackets1_crashover2k_afterwhoruneo_proxy_027_just_login_and_spawn_on_rogers_way_ctrl_And_jackout.log.txt:944:01 00 0c 57 02 a4 cd ab 11 8e 41 2e 53 2e 50 2e 20 54 72 61 69 6e 65 65 00 00 00 00 00 00 00 00
```

### Header-like selector 50

Signature: `01 00 08 50` (spaced or compact hex)

Matches: 23

```text
mxopackets1_crashover2k_afterwhoruneo_proxy_003_loading_area_teleport_to_bathary_row.log.txt:400:07 00 00 01 00 08 50 02 2c 03 30 03 c4 cd ab 02 09 00 00 00 00 80 8a dd c0 00 00 00 00 00 e8 84
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoLoadingAreaThenJackedIntoMaraCThenLeft.log.txt:834:00 00 01 00 08 50 02 ea 01 30 39 da cd ab 01 01 00 00 00 00 00 9a d0 40 00 00 00 00 00 90 80 40
mxopackets2_RajkoPacketsNearTheEnd_PacketsLoggedIntoMaraC5PeopleStandingAround.log.txt:432:02 03 01 00 08 50 02 ea 01 30 39 da cd ab 01 01 00 00 00 00 00 9a d0 40 00 00 00 00 00 90 80 40
mxopackets1_crashover2k_afterwhoruneo_proxy_003_abs_hyperjump_only_up_and_down2.log.txt:1522:01 00 08 50 02 81 08 f0 34 0d cd ab 02 09 00 00 00 c0 94 95 f0 c0 00 00 00 00 00 40 60 40 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_024_chatting_hyperjump_fight.log.txt:3387:5e 56 3d 00 00 00 00 2f a6 7f 3f 0f 00 00 01 00 08 50 02 81 08 f0 34 5a cd ab 02 09 00 00 00 c0
mxopackets1_crashover2k_afterwhoruneo_proxy_024_chatting_hyperjump_fight.log.txt:9162:02 03 01 00 08 50 02 81 08 f0 34 5a cd ab 02 09 00 00 00 c0 94 95 f0 c0 00 00 00 00 00 40 60 40
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_teleports_002_teleport_from_loading_area_to_westview_bathary_row_SE.log.txt:473:02 03 01 00 08 50 02 2c 03 30 03 c4 cd ab 02 09 00 00 00 00 80 8a dd c0 00 00 00 00 00 e8 84 c0
mxopackets1_crashover2k_afterwhoruneo_proxy_027_just_login_and_spawn_on_rogers_way_ctrl_And_jackout.log.txt:1170:02 07 08 00 00 81 f0 1a 00 00 02 05 21 00 00 01 00 08 50 02 f3 0c 60 4f a4 cd ab 02 09 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsQuickInAndOut.log.txt:1001:00 18 68 c5 87 02 c1 00 00 00 59 64 12 88 c0 00 00 00 61 d0 2b f9 c0 05 00 00 01 00 08 50 02 57
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_005_afterwhoruneo_login_open_door_and_fight.log.txt:632:00 00 02 06 1d 00 00 01 00 08 50 02 e8 00 00 0b 29 cd ab 02 09 00 00 00 00 80 58 dd c0 00 00 00
```

### Header-like selector 67

Signature: `01 00 08 67` (spaced or compact hex)

Matches: 0

### Header-like selector 68

Signature: `01 00 08 68` (spaced or compact hex)

Matches: 0

### Header-like selector 69

Signature: `01 00 08 69` (spaced or compact hex)

Matches: 0

### Header-like selector 76

Signature: `01 00 0c 76` (spaced or compact hex)

Matches: 0

### Header-like selector 90

Signature: `01 00 08 90` (spaced or compact hex)

Matches: 0

### Header-like selector a3

Signature: `01 00 08 a3` (spaced or compact hex)

Matches: 22

```text
mxopackets1_crashover2k_afterwhoruneo_proxy_026_open_doors_and_some_hyper_jump_killed_by_Agent_etc.log.txt:11676:01 22 00 00 00 00 00 00 00 01 00 08 a3 01 51 16 a0 2b 74 cd ab 03 88 00 00 00 00 f2 04 35 bf 00
mxopackets1_crashover2k_afterwhoruneo_proxy_026_open_doors_and_some_hyper_jump_killed_by_Agent_etc.log.txt:20370:02 03 01 00 08 a3 01 d4 11 40 07 d0 cd ab 03 88 00 00 00 00 f3 04 35 3f 00 00 00 00 f2 04 35 3f
mxopackets1_crashover2k_afterwhoruneo_proxy_026_open_doors_and_some_hyper_jump_killed_by_Agent_etc.log.txt:20646:02 03 01 00 08 a3 01 e4 11 40 07 d1 cd ab 03 88 00 00 00 00 f3 04 35 3f 00 00 00 00 f2 04 35 3f
mxopackets1_crashover2k_afterwhoruneo_proxy_026_open_doors_and_some_hyper_jump_killed_by_Agent_etc.log.txt:21108:02 03 02 00 02 80 04 1e 01 00 08 a3 01 b1 0c d0 14 d7 cd ab 03 88 00 00 00 00 00 00 80 3f 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_026_open_doors_and_some_hyper_jump_killed_by_Agent_etc.log.txt:21110:ff ff ff 05 00 00 01 00 08 a3 01 88 09 d0 14 b3 cd ab 03 88 00 00 00 00 00 00 80 3f 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_026_open_doors_and_some_hyper_jump_killed_by_Agent_etc.log.txt:21112:ff 0c 00 00 01 00 08 a3 01 b4 0c d0 14 b2 cd ab 03 88 00 00 00 00 00 00 80 3f 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_026_open_doors_and_some_hyper_jump_killed_by_Agent_etc.log.txt:21114:00 00 01 00 08 a3 01 85 09 d0 14 d8 cd ab 03 88 00 00 00 00 00 00 80 3f 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_026_open_doors_and_some_hyper_jump_killed_by_Agent_etc.log.txt:23917:02 03 01 00 08 a3 01 42 03 d0 14 e1 cd ab 03 88 00 00 00 00 00 00 80 3f 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_026_open_doors_and_some_hyper_jump_killed_by_Agent_etc.log.txt:36972:02 03 01 00 08 a3 01 97 25 b0 05 01 cd ab 03 88 00 00 00 00 f3 04 35 3f 00 00 00 00 f3 04 35 3f
mxopackets1_crashover2k_afterwhoruneo_proxy_026_open_doors_and_some_hyper_jump_killed_by_Agent_etc.log.txt:36974:01 00 08 a3 01 a8 17 b0 05 02 cd ab 03 88 00 00 00 00 f3 04 35 bf 00 00 00 00 f3 04 35 3f 22 00
```

### Header-like selector d0

Signature: `01 00 08 d0` (spaced or compact hex)

Matches: 28

```text
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoLoadingAreaThenJackedIntoMaraCThenLeft.log.txt:786:02 03 02 00 02 80 80 80 80 80 10 11 00 00 00 01 00 08 d0 20 ef 01 30 39 de cd ab 08 fd 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoLoadingAreaThenJackedIntoMaraCThenLeft.log.txt:828:2f 22 46 1d 7b 22 00 00 01 11 00 00 00 2f 00 49 00 00 01 00 08 d0 20 f1 01 30 39 c1 cd ab 08 fd
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoLoadingAreaThenJackedIntoMaraCThenLeft.log.txt:1046:1d 0a 00 28 b0 01 10 12 00 89 87 8c 53 22 89 1c 03 11 00 00 00 81 ff 33 00 00 01 00 08 d0 20 ee
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoLoadingAreaThenJackedIntoMaraCThenLeft.log.txt:1149:00 00 81 ff 2d 00 00 01 00 08 d0 20 b0 00 20 4e db cd ab 08 fd 00 00 00 00 c0 05 d2 40 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoLoadingAreaThenJackedIntoMaraCThenLeft.log.txt:1209:00 00 81 ff 27 00 00 01 00 08 d0 20 b1 00 20 4e c2 cd ab 07 fd 00 00 00 00 00 f8 d1 40 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsLoggedIntoMaraC5PeopleStandingAround.log.txt:383:82 52 a0 12 49 03 02 00 02 80 80 80 80 80 10 11 00 00 00 01 00 08 d0 20 ef 01 30 39 de cd ab 08
mxopackets2_RajkoPacketsNearTheEnd_PacketsLoggedIntoMaraC5PeopleStandingAround.log.txt:389:25 00 00 01 00 08 d0 20 f1 01 30 39 c1 cd ab 08 fd 00 00 00 00 80 83 cf 40 00 00 00 00 00 f0 7e
mxopackets2_RajkoPacketsNearTheEnd_PacketsLoggedIntoMaraC5PeopleStandingAround.log.txt:433:00 00 00 00 00 30 a1 40 21 00 00 01 00 08 d0 20 b1 00 20 4e c2 cd ab 07 fd 00 00 00 00 00 f8 d1
mxopackets2_RajkoPacketsNearTheEnd_PacketsLoggedIntoMaraC5PeopleStandingAround.log.txt:584:02 03 01 00 08 d0 20 b0 00 20 4e db cd ab 08 fd 00 00 00 00 c0 05 d2 40 00 00 00 00 00 f0 7e 40
mxopackets2_RajkoPacketsNearTheEnd_PacketsLoggedIntoMaraC5PeopleStandingAround.log.txt:651:01 9e 00 00 00 24 00 09 00 00 01 00 08 d0 20 ee 01 30 39 df cd ab 08 fd 00 00 00 00 c0 5c d5 40
```

## Movement And Object-State Leads

### Mode 06 movement selector 08

Signature: `00 06 08` (spaced or compact hex)

Matches: 30

```text
mxopackets1_crashover2k_afterwhoruneo_proxy_026_open_doors_and_some_hyper_jump_killed_by_Agent_etc.log.txt:27494:02 03 07 00 06 08 ee b5 cc c6 f8 df ec 44 ea 65 d3 46 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_026_open_doors_and_some_hyper_jump_killed_by_Agent_etc.log.txt:63452:02 03 14 00 06 08 66 c7 6b c7 00 60 95 44 9b 99 14 46 80 80 80 80 c0 4c 0a 00 28 01 01 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_026_open_doors_and_some_hyper_jump_killed_by_Agent_etc.log.txt:63462:02 03 14 00 06 08 67 d3 6b c7 00 60 95 44 6d e5 14 46 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_026_open_doors_and_some_hyper_jump_killed_by_Agent_etc.log.txt:63482:02 03 14 00 06 08 75 df 6b c7 00 60 95 44 95 31 15 46 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_026_open_doors_and_some_hyper_jump_killed_by_Agent_etc.log.txt:63502:02 03 14 00 06 08 7d eb 6b c7 00 60 95 44 95 7d 15 46 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_026_open_doors_and_some_hyper_jump_killed_by_Agent_etc.log.txt:63522:82 2b 77 af 47 03 14 00 06 08 87 fd 6b c7 00 60 95 44 88 ef 15 46 ff 00 00 00 10 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_026_open_doors_and_some_hyper_jump_killed_by_Agent_etc.log.txt:63542:02 03 14 00 06 08 95 03 6c c7 00 60 95 44 c6 15 16 46 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_026_open_doors_and_some_hyper_jump_killed_by_Agent_etc.log.txt:63562:02 03 14 00 06 08 9a 15 6c c7 00 60 95 44 99 87 16 46 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_026_open_doors_and_some_hyper_jump_killed_by_Agent_etc.log.txt:63582:02 03 14 00 06 08 a5 1b 6c c7 00 60 95 44 c6 ad 16 46 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_026_open_doors_and_some_hyper_jump_killed_by_Agent_etc.log.txt:63602:02 03 14 00 06 08 b0 2d 6c c7 00 60 95 44 be 1f 17 46 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00
```

### Mode 06 movement selector 0a

Signature: `00 06 0a` (spaced or compact hex)

Matches: 11

```text
mxopackets2_RajkoPacketsNearTheEnd_PacketsJustAnotherMaraCHangout.log.txt:6663:02 03 0d 00 01 02 77 0b 00 06 0a 00 00 bc 7c 46 00 00 be 42 00 50 82 c5 80 80 80 80 80 80 01 43
mxopackets1_crashover2k_afterwhoruneo_proxy_026_open_doors_and_some_hyper_jump_killed_by_Agent_etc.log.txt:32925:02 03 1d 00 06 0a 00 2a 4c ba c6 f8 df ec 44 e9 58 bd 46 80 80 80 80 80 80 01 13 08 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_026_open_doors_and_some_hyper_jump_killed_by_Agent_etc.log.txt:77041:8a c4 06 a5 2b 46 0d 00 06 0a 00 ab 82 09 47 00 80 fc c3 12 c5 81 46 ff 00 00 00 10 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_026_open_doors_and_some_hyper_jump_killed_by_Agent_etc.log.txt:77238:8a c4 61 ad 85 46 0d 00 06 0a 00 ab 82 09 47 00 80 fc c3 12 c5 81 46 ff 00 00 00 10 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_026_open_doors_and_some_hyper_jump_killed_by_Agent_etc.log.txt:79336:b2 ad 2b 46 0d 00 06 0a 00 a2 3e 0a 47 00 80 fc c3 b7 71 80 46 ff 00 00 00 10 00 00 00 00 00 01
mxopackets1_crashover2k_afterwhoruneo_proxy_026_open_doors_and_some_hyper_jump_killed_by_Agent_etc.log.txt:79422:2b 00 02 0e 01 a1 5a 10 0c 47 00 80 fc c3 a1 c3 9b 46 29 00 06 0a 01 73 2d ec 46 00 e0 88 44 0f
mxopackets1_crashover2k_afterwhoruneo_proxy_026_open_doors_and_some_hyper_jump_killed_by_Agent_etc.log.txt:84122:02 03 2b 00 06 0a 01 1c 89 0a 47 00 80 fc c3 30 4b 9a 46 ff 00 00 00 10 00 00 00 00 00 01 ff 00
mxopackets1_crashover2k_afterwhoruneo_proxy_026_open_doors_and_some_hyper_jump_killed_by_Agent_etc.log.txt:84872:82 8d 3e b0 47 03 2d 00 02 08 0d 01 09 47 00 80 fc c3 bd 54 9c 46 27 00 06 0a 01 0d 24 d4 46 00
mxopackets1_crashover2k_afterwhoruneo_proxy_026_open_doors_and_some_hyper_jump_killed_by_Agent_etc.log.txt:96649:02 03 33 00 06 0a 01 c8 0a f0 46 00 e0 ba 44 87 40 2d 46 80 80 80 80 80 01 05 25 00 02 0e 04 e8
mxopackets1_crashover2k_afterwhoruneo_proxy_026_open_doors_and_some_hyper_jump_killed_by_Agent_etc.log.txt:96650:13 ba e7 46 00 e0 ba 44 7f b1 30 46 21 00 06 0a 01 44 b3 0a 47 00 20 8a c4 23 8a 29 46 ff 00 00
```

### Mode 06 movement selector 0c

Signature: `00 06 0c` (spaced or compact hex)

Matches: 345

```text
mxopackets1_crashover2k_afterwhoruneo_proxy_003_loading_area_teleport_to_bathary_row.log.txt:558:02 03 0f 00 06 0c c3 40 ce d6 c6 00 40 30 c4 55 bb af 46 ff 00 00 00 10 00 00 00 00 00 01 ff 00
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_teleports_002_teleport_from_loading_area_to_westview_bathary_row_SE.log.txt:957:02 03 0b 00 06 0c 70 1e 36 d2 c6 00 40 30 c4 43 e3 ab 46 ff 00 00 00 10 00 00 00 00 00 01 ff 00
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_teleports_002_teleport_from_loading_area_to_westview_bathary_row_SE.log.txt:962:00 00 22 00 00 00 00 00 05 00 06 0c 54 cf 9f d6 c6 00 40 30 c4 c6 3e a8 46 ff 00 00 00 10 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_teleports_002_teleport_from_loading_area_to_westview_bathary_row_SE.log.txt:1011:82 d9 d8 00 49 03 0b 00 06 0c 7a 1e 36 d2 c6 00 40 30 c4 43 e3 ab 46 ff 00 00 00 10 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_teleports_002_teleport_from_loading_area_to_westview_bathary_row_SE.log.txt:1039:02 03 0b 00 06 0c 84 1e 36 d2 c6 00 40 30 c4 43 e3 ab 46 ff 00 00 00 10 00 00 00 00 00 01 ff 00
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_teleports_002_teleport_from_loading_area_to_westview_bathary_row_SE.log.txt:1059:02 03 0b 00 06 0c 8e 1e 36 d2 c6 00 40 30 c4 43 e3 ab 46 ff 00 00 00 10 00 00 00 00 00 01 ff 00
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_teleports_002_teleport_from_loading_area_to_westview_bathary_row_SE.log.txt:1119:02 03 0b 00 06 0c a5 1e 36 d2 c6 00 40 30 c4 43 e3 ab 46 ff 00 00 00 10 00 00 00 00 00 01 ff 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsJustAnotherMaraCHangout.log.txt:6171:82 b8 3a 5f 47 03 3f 00 06 0c f8 7e a9 a4 46 00 00 be 42 9f b9 8a c5 ff 00 00 00 10 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsJustAnotherMaraCHangout.log.txt:7106:02 03 3f 00 06 0c 38 ca 16 a5 46 00 00 be 42 af 18 8a c5 ff 00 00 00 10 00 00 00 00 00 01 ff 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsJustAnotherMaraCHangout.log.txt:7260:1d 05 7c 46 00 80 f7 43 ff c6 41 45 0b 00 06 0c 01 00 bc 7c 46 00 00 be 42 00 50 82 c5 ff 00 00
```

### Mode 06 movement selector 0e

Signature: `00 06 0e` (spaced or compact hex)

Matches: 478

```text
mxopackets1_crashover2k_afterwhoruneo_proxy_003_loading_area_teleport_to_bathary_row.log.txt:505:08 8e c0 0d c7 00 40 30 c4 16 44 aa 46 0f 00 06 0e 00 c5 40 ce d6 c6 00 40 30 c4 55 bb af 46 80
mxopackets1_crashover2k_afterwhoruneo_proxy_027_just_login_and_spawn_on_rogers_way_ctrl_And_jackout.log.txt:1320:02 03 01 00 02 1f 00 17 00 06 0e 00 a8 c3 03 84 c7 00 80 fc c3 40 a2 78 c7 80 80 80 80 c0 4c 0a
mxopackets1_crashover2k_afterwhoruneo_proxy_027_just_login_and_spawn_on_rogers_way_ctrl_And_jackout.log.txt:1459:6c 16 8f 83 c7 00 80 fc c3 47 24 63 c7 23 00 06 0e 00 57 ba 6b 81 c7 00 20 8a c4 51 79 4b c7 ff
mxopackets1_crashover2k_afterwhoruneo_proxy_027_just_login_and_spawn_on_rogers_way_ctrl_And_jackout.log.txt:1485:c3 75 57 6c c7 35 00 02 0c e3 a1 18 7d c7 00 20 8a c4 6c 44 6c c7 23 00 06 0e 00 4d ba 6b 81 c7
mxopackets1_crashover2k_afterwhoruneo_proxy_027_just_login_and_spawn_on_rogers_way_ctrl_And_jackout.log.txt:2581:82 3b d5 b4 47 03 3b 00 06 0e 01 c7 64 f6 88 c7 00 80 fc c3 79 b2 72 c7 80 80 80 80 c0 4c 0a 00
mxopackets1_crashover2k_afterwhoruneo_proxy_027_just_login_and_spawn_on_rogers_way_ctrl_And_jackout.log.txt:2596:6d 86 84 c7 00 80 fc c3 97 1b 61 c7 2b 00 06 0e 05 a3 6a 7d 84 c7 00 80 fc c3 61 2d 6e c7 80 80
mxopackets1_crashover2k_afterwhoruneo_proxy_027_just_login_and_spawn_on_rogers_way_ctrl_And_jackout.log.txt:2633:02 03 2f 00 04 80 80 80 80 c0 4c 0a 00 28 01 01 2b 00 06 0e 00 8f 6a 7d 84 c7 00 80 fc c3 61 2d
mxopackets1_crashover2k_afterwhoruneo_proxy_027_just_login_and_spawn_on_rogers_way_ctrl_And_jackout.log.txt:2659:02 03 43 00 02 08 4b 9e 64 c7 00 80 fc c3 0a 12 6d c7 2b 00 06 0e 00 85 6a 7d 84 c7 00 80 fc c3
mxopackets1_crashover2k_afterwhoruneo_proxy_027_just_login_and_spawn_on_rogers_way_ctrl_And_jackout.log.txt:2664:00 00 85 09 00 58 37 08 00 00 1f 07 08 00 00 00 00 00 00 22 00 00 00 00 00 25 00 06 0e 05 9c 28
mxopackets1_crashover2k_afterwhoruneo_proxy_027_just_login_and_spawn_on_rogers_way_ctrl_And_jackout.log.txt:2691:02 03 2d 00 02 0e 04 79 94 d0 88 c7 00 80 fc c3 1d 8c 77 c7 2b 00 06 0e 00 84 6a 7d 84 c7 00 80
```

### Repeated mode 06 suffix lead

Signature: `ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00` (spaced or compact hex)

Matches: 309

```text
mxopackets1_crashover2k_afterwhoruneo_proxy_027_just_login_and_spawn_on_rogers_way_ctrl_And_jackout.log.txt:1486:00 20 8a c4 51 79 4b c7 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_027_just_login_and_spawn_on_rogers_way_ctrl_And_jackout.log.txt:1507:00 06 0c 49 ba 6b 81 c7 00 20 8a c4 51 79 4b c7 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_027_just_login_and_spawn_on_rogers_way_ctrl_And_jackout.log.txt:2610:2d 6e c7 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_027_just_login_and_spawn_on_rogers_way_ctrl_And_jackout.log.txt:2634:6e c7 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_027_just_login_and_spawn_on_rogers_way_ctrl_And_jackout.log.txt:2660:61 2d 6e c7 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_027_just_login_and_spawn_on_rogers_way_ctrl_And_jackout.log.txt:2665:f7 65 c7 00 80 fc c3 3a f0 6c c7 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_027_just_login_and_spawn_on_rogers_way_ctrl_And_jackout.log.txt:2692:fc c3 61 2d 6e c7 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_027_just_login_and_spawn_on_rogers_way_ctrl_And_jackout.log.txt:2697:92 28 f7 65 c7 00 80 fc c3 3a f0 6c c7 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_027_just_login_and_spawn_on_rogers_way_ctrl_And_jackout.log.txt:2725:0c 88 28 f7 65 c7 00 80 fc c3 3a f0 6c c7 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_027_just_login_and_spawn_on_rogers_way_ctrl_And_jackout.log.txt:2816:fc c3 3a f0 6c c7 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00
```

### Movement-state ff 01 64 01 tail

Signature: `ff 01 64 01` (spaced or compact hex)

Matches: 0

### Movement-state ff 01 4e 01 tail

Signature: `ff 01 4e 01` (spaced or compact hex)

Matches: 1024

```text
mxopackets1_crashover2k_afterwhoruneo_proxy_003_abs_hyperjump_only_up_and_down.log.txt:31:02 03 02 00 03 09 08 00 10 7f 84 c7 72 b6 95 44 75 cf 85 46 e8 15 33 20 ff 01 4e 01 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_abs_hyperjump_only_up_and_down.log.txt:53:02 03 02 00 03 09 08 00 0f 7f 84 c7 a0 8a d3 44 74 cf 85 46 65 16 33 20 ff 01 4e 01 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_abs_hyperjump_only_up_and_down.log.txt:75:02 03 02 00 03 08 0d 7f 84 c7 ac d4 07 45 73 cf 85 46 e2 16 33 20 ff 01 4e 01 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_abs_hyperjump_only_up_and_down.log.txt:97:02 03 02 00 03 08 0c 7f 84 c7 53 8a 16 45 73 cf 85 46 5f 17 33 20 ff 01 4e 01 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_abs_hyperjump_only_up_and_down.log.txt:119:02 03 02 00 03 08 0b 7f 84 c7 96 51 33 45 72 cf 85 46 dc 17 33 20 ff 01 4e 01 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_abs_hyperjump_only_up_and_down.log.txt:141:02 03 02 00 03 08 09 7f 84 c7 1e 3e 4f 45 72 cf 85 46 59 18 33 20 ff 01 4e 01 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_abs_hyperjump_only_up_and_down.log.txt:173:02 03 02 00 03 08 06 7f 84 c7 7d 43 82 45 70 cf 85 46 53 19 33 20 ff 01 4e 01 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_abs_hyperjump_only_up_and_down.log.txt:195:82 be e3 03 49 03 02 00 03 08 04 7f 84 c7 a7 f1 8e 45 70 cf 85 46 d0 19 33 20 ff 01 4e 01 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_abs_hyperjump_only_up_and_down.log.txt:217:02 03 02 00 03 08 03 7f 84 c7 74 32 9b 45 6f cf 85 46 4d 1a 33 20 ff 01 4e 01 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_abs_hyperjump_only_up_and_down.log.txt:239:02 03 02 00 03 08 01 7f 84 c7 e3 05 a7 45 6e cf 85 46 ca 1a 33 20 ff 01 4e 01 00 00 00 00 00 00
```

## Ability, Effect, And Combat-State Leads

### SERVER_PLAYER_ATTRIBUTE / ability load

Signature: `80 b2` (spaced or compact hex)

Matches: 712

```text
mxopackets1_crashover2k_afterwhoruneo_proxy_003_loading_area_teleport_to_bathary_row.log.txt:571:00 00 16 80 bc 55 00 e9 01 00 02 00 00 00 cc 00 0c 00 00 00 01 00 00 00 00 08 80 b2 11 00 32 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_loading_area_teleport_to_bathary_row.log.txt:621:00 00 08 80 b2 36 04 00 00 08 02 16 80 bc 45 03 36 04 00 40 00 00 00 36 04 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_loading_area_teleport_to_bathary_row.log.txt:671:00 00 00 08 80 b2 3a 04 00 00 08 02 16 80 bc 45 03 3a 04 00 53 00 00 00 3a 04 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_loading_area_teleport_to_bathary_row.log.txt:678:16 80 bc 55 00 58 02 00 62 00 00 00 06 04 01 00 00 00 00 00 00 00 00 08 80 b2 35 04 00 00 08 02
mxopackets1_crashover2k_afterwhoruneo_proxy_003_loading_area_teleport_to_bathary_row.log.txt:718:00 36 01 00 00 07 04 01 00 00 00 01 00 00 00 00 08 80 b2 3b 04 00 00 08 02 16 80 bc 45 03 3b 04
mxopackets1_crashover2k_afterwhoruneo_proxy_003_loading_area_teleport_to_bathary_row.log.txt:729:08 80 b2 3c 04 00 00 08 02 16 80 bc 45 03 3c 04 00 4e 01 00 00 3c 04 00 00 00 00 00 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsQuickInAndOut.log.txt:8:00 00 00 08 80 b2 4e 00 1e 00 08 02 08 80 b2 52 00 1e 00 08 02 08 80 b2 54 00 0a 00 08 02 08 80
mxopackets2_RajkoPacketsNearTheEnd_PacketsQuickInAndOut.log.txt:9:b2 4f 00 08 00 08 02 08 80 b2 51 00 0b 00 08 02 08 80 b2 11 00 32 00 08 02 16 80 bc 45 03 11 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsQuickInAndOut.log.txt:19:00 00 00 00 08 80 b2 26 04 00 00 08 02 16 80 bc 45 03 26 04 00 89 00 00 00 26 04 00 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsQuickInAndOut.log.txt:20:00 00 00 00 16 80 bc 45 00 0e 00 00 89 00 00 00 16 04 02 00 00 00 00 00 00 00 00 08 80 b2 24 04
```

### SERVER_ABILITY_UNLOAD

Signature: `80 b3` (spaced or compact hex)

Matches: 464

```text
mxopackets1_crashover2k_afterwhoruneo_proxy_003_loading_area_teleport_to_bathary_row.log.txt:128:00 00 04 80 b3 11 00 16 80 bc 45 03 11 00 00 02 00 00 00 11 00 32 00 00 00 01 00 00 00 00 04 80
mxopackets1_crashover2k_afterwhoruneo_proxy_003_loading_area_teleport_to_bathary_row.log.txt:129:b3 35 04 16 80 bc 45 03 35 04 00 62 00 00 00 35 04 00 00 00 00 01 00 00 00 00 04 80 b3 36 04 16
mxopackets1_crashover2k_afterwhoruneo_proxy_003_loading_area_teleport_to_bathary_row.log.txt:130:80 bc 45 03 36 04 00 40 00 00 00 36 04 00 00 00 00 01 00 00 00 00 04 80 b3 3a 04 16 80 bc 45 03
mxopackets1_crashover2k_afterwhoruneo_proxy_003_loading_area_teleport_to_bathary_row.log.txt:131:3a 04 00 53 00 00 00 3a 04 00 00 00 00 01 00 00 00 00 04 80 b3 3b 04 16 80 bc 45 03 3b 04 00 36
mxopackets1_crashover2k_afterwhoruneo_proxy_003_loading_area_teleport_to_bathary_row.log.txt:132:01 00 00 3b 04 00 00 00 00 01 00 00 00 00 04 80 b3 3c 04 16 80 bc 45 03 3c 04 00 4e 01 00 00 3c
mxopackets1_crashover2k_afterwhoruneo_proxy_003_loading_area_teleport_to_bathary_row.log.txt:570:00 01 00 00 00 00 04 80 b3 11 00 16 80 bc 45 03 11 00 00 02 00 00 00 11 00 32 00 00 00 01 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_loading_area_teleport_to_bathary_row.log.txt:619:34 00 00 00 83 02 c8 00 00 00 00 00 00 00 00 04 80 b3 36 04 16 80 bc 45 03 36 04 00 40 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_loading_area_teleport_to_bathary_row.log.txt:631:00 53 00 00 00 19 04 01 00 00 00 01 00 00 00 00 04 80 b3 3a 04
mxopackets1_crashover2k_afterwhoruneo_proxy_003_loading_area_teleport_to_bathary_row.log.txt:676:00 00 06 04 01 00 00 00 01 00 00 00 00 04 80 b3 35 04 16 80 bc 45 03 35 04 00 62 00 00 00 35 04
mxopackets1_crashover2k_afterwhoruneo_proxy_003_loading_area_teleport_to_bathary_row.log.txt:716:02 04 01 00 a4 21 16 80 bc 55 00 5c 02 00 35 01 00 00 42 04 0a 00 00 00 00 00 00 00 00 04 80 b3
```

### Object599 adrenaline booster effect id

Signature: `4c 0a 00 28` (spaced or compact hex)

Matches: 763

```text
mxopackets1_crashover2k_afterwhoruneo_proxy_003_loading_area_teleport_to_bathary_row.log.txt:506:80 80 80 c0 4c 0a 00 28 01 01 00 00 04 02 00 3a 0a 16 80 bc 56 00 e8 01 00 c3 b2 00 00 9d 00 1f
mxopackets1_crashover2k_afterwhoruneo_proxy_003_loading_area_teleport_to_bathary_row.log.txt:562:4c 0a 00 28 ff 01 00 00 00 00 00 00 00 00 00 00 00 9e 05 00 58 37 08 00 00 1f 07 08 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_loading_area_teleport_to_bathary_row.log.txt:563:00 00 22 00 00 00 00 00 09 00 04 80 80 80 80 c0 4c 0a 00 28 01 01 07 00 02 0c f6 bc 29 c1 c6 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_loading_area_teleport_to_bathary_row.log.txt:666:00 00 c0 09 00 00 4c 0a 00 28 ff 01 00 00 00 00 00 00 00 00 00 00 00 9e 05 00 58 37 08 00 00 1f
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_teleports_002_teleport_from_loading_area_to_westview_bathary_row_SE.log.txt:582:02 08 4d d3 d6 c6 00 40 30 c4 9c d3 a8 46 01 00 02 10 00 03 00 04 80 80 80 80 c0 4c 0a 00 28 01
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_teleports_002_teleport_from_loading_area_to_westview_bathary_row_SE.log.txt:849:02 03 0b 00 06 0e 04 5c 1e 36 d2 c6 00 40 30 c4 43 e3 ab 46 80 80 80 80 c0 4c 0a 00 28 01 01 05
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_teleports_002_teleport_from_loading_area_to_westview_bathary_row_SE.log.txt:850:00 06 0e 00 63 cf 9f d6 c6 00 40 30 c4 c6 3e a8 46 80 80 80 80 c0 4c 0a 00 28 01 01 00 00 04 01
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_teleports_002_teleport_from_loading_area_to_westview_bathary_row_SE.log.txt:907:00 4c 0a 00 28 ff 01 00 00 00 00 00 00 00 00 00 00 00 a0 05 00 58 37 08 00 00 1f 07 08 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_teleports_002_teleport_from_loading_area_to_westview_bathary_row_SE.log.txt:912:00 00 00 00 c0 09 00 00 4c 0a 00 28 ff 01 00 00 00 00 00 00 00 00 00 00 00 9e 05 00 58 37 08 00
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_teleports_002_teleport_from_loading_area_to_westview_bathary_row_SE.log.txt:961:4c 0a 00 28 ff 01 00 00 00 00 00 00 00 00 00 00 00 a0 05 00 58 37 08 00 00 1f 07 08 00 00 00 00
```

### Object599 fast healing effect id

Signature: `b5 07 00 28` (spaced or compact hex)

Matches: 23

```text
mxopackets1_crashover2k_afterwhoruneo_proxy_027_just_login_and_spawn_on_rogers_way_ctrl_And_jackout.log.txt:1683:02 03 37 00 04 80 80 80 80 c0 b5 07 00 28 01 01 1f 00 02 0e 00 5a 3b de 6d c7 00 80 fc c3 c5 11
mxopackets1_crashover2k_afterwhoruneo_proxy_027_just_login_and_spawn_on_rogers_way_ctrl_And_jackout.log.txt:1684:5e c7 17 00 04 80 80 80 80 c0 b5 07 00 28 01 02 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_027_just_login_and_spawn_on_rogers_way_ctrl_And_jackout.log.txt:1705:02 03 23 00 04 80 80 80 80 c0 b5 07 00 28 01 02 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_027_just_login_and_spawn_on_rogers_way_ctrl_And_jackout.log.txt:1735:02 03 35 00 04 80 80 80 80 c0 b5 07 00 28 01 02 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_027_just_login_and_spawn_on_rogers_way_ctrl_And_jackout.log.txt:1775:82 25 d1 b4 47 03 05 00 04 80 80 80 80 c0 b5 07 00 28 01 01 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_027_just_login_and_spawn_on_rogers_way_ctrl_And_jackout.log.txt:1846:c7 25 00 04 80 80 80 80 c0 b5 07 00 28 01 01 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_027_just_login_and_spawn_on_rogers_way_ctrl_And_jackout.log.txt:2554:80 c0 b5 07 00 28 01 02 33 00 02 0e 01 f4 65 83 84 c7 00 80 fc c3 c3 5e 61 c7 0b 00 02 0c a0 5d
mxopackets1_crashover2k_afterwhoruneo_proxy_027_just_login_and_spawn_on_rogers_way_ctrl_And_jackout.log.txt:2777:00 00 00 00 22 00 00 00 00 00 11 00 04 80 80 80 80 c0 b5 07 00 28 01 02 0d 00 02 0c a1 7f 5d 86
mxopackets1_crashover2k_afterwhoruneo_proxy_027_just_login_and_spawn_on_rogers_way_ctrl_And_jackout.log.txt:2847:c0 4c 0a 00 28 01 01 11 00 04 80 80 80 80 c0 b5 07 00 28 01 02 0f 00 02 0e 05 d3 e9 41 7c c7 00
mxopackets1_crashover2k_afterwhoruneo_proxy_027_just_login_and_spawn_on_rogers_way_ctrl_And_jackout.log.txt:3416:fc c3 56 08 60 c7 80 80 80 80 c0 b5 07 00 28 01 02 13 00 02 08 98 4b 74 c7 00 80 fc c3 21 26 79
```

### Object599 accelerated spawn effect id

Signature: `66 00 00 04` (spaced or compact hex)

Matches: 211

```text
mxopackets2_RajkoPacketsNearTheEnd_PacketsJustAnotherMaraCHangout.log.txt:1965:00 f0 7e 40 00 00 00 00 eb 7b 7e 40 a6 ff 45 12 fb ff 66 00 00 04 90 01 00 8b 4f 32 5d 03 2d cf
mxopackets2_RajkoPacketsNearTheEnd_PacketsJustAnotherMaraCHangout.log.txt:2675:ff 00 00 00 00 00 00 00 00 00 00 6c 0e 00 b1 90 00 00 ff ff 22 b6 00 00 66 00 00 04 27 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsJustAnotherMaraCHangout.log.txt:2727:00 00 00 00 00 00 6c 0e 00 b1 90 00 00 ff ff 22 b6 00 00 66 00 00 04 27 00 00 00 00 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsJustAnotherMaraCHangout.log.txt:2750:b6 00 00 66 00 00 04 27 00 00 00 00 00 00 00 00 01 00 ff 00 00 00 00 82 08 01 00 00 00 08 00 12
mxopackets2_RajkoPacketsNearTheEnd_PacketsJustAnotherMaraCHangout.log.txt:2781:b1 90 00 00 ff ff 22 b6 00 00 66 00 00 04 27 00 00 00 00 00 00 00 00 01 00 ff 00 00 00 00 82 08
mxopackets2_RajkoPacketsNearTheEnd_PacketsJustAnotherMaraCHangout.log.txt:2803:00 00 ff ff 22 b6 00 00 66 00 00 04 27 00 00 00 00 00 00 00 00 01 00 ff 00 00 00 00 82 08 01 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsJustAnotherMaraCHangout.log.txt:2822:51 3b de 40 ff 00 00 00 00 00 00 00 00 00 00 6c 0e 00 b1 90 00 00 ff ff 22 b6 00 00 66 00 00 04
mxopackets2_RajkoPacketsNearTheEnd_PacketsJustAnotherMaraCHangout.log.txt:2852:00 00 6c 0e 00 b1 90 00 00 ff ff 22 b6 00 00 66 00 00 04 27 00 00 00 00 00 00 00 00 01 00 ff 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsJustAnotherMaraCHangout.log.txt:3062:22 b6 00 00 66 00 00 04 28 00 00 00 00 00 00 00 00 01 00 ff 00 00 00 00 82 08 01 00 00 00 08 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsJustAnotherMaraCHangout.log.txt:3102:90 00 00 ff ff 22 b6 00 00 66 00 00 04 28 00 00 00 00 00 00 00 00 01 00 ff 00 00 00 00 82 08 01
```

## Intake Next Steps

- Preserve the original files separately before editing or importing anything.
- Inspect matches with surrounding lines; embedded hits inside encrypted or unrelated bodies are leads only.
- Record sender, provenance, CR1/CR2 status, action context, character handle, coordinates, known object ids, and known item GoIDs.
- Import only text logs or text-like dumps that contain no credential material, client binaries, key material, or unrelated game assets.
