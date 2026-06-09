# Packet Capture Triage Report

- Source kind: `directory`
- Source input: `/Users/kyle/Developer/mxo-hd/research/packet-dumps/community-intake/20260608T120313Z-hdneo-public-create-spawn-session-sanitized-selected/logs`
- Scanned files from: `/Users/kyle/Developer/mxo-hd/research/packet-dumps/community-intake/20260608T120313Z-hdneo-public-create-spawn-session-sanitized-selected/logs`
- Generated UTC: `2026-06-08T12:03:16Z`
- Max sample lines per pattern: `10`

Use this report to decide whether received logs are worth importing into `research/packet-dumps/`. Matches are byte-string leads, not decoded packet claims.

## Vendor And Interaction Leads

### SERVER_VENDOR_OPEN candidate

Signature: `81 0d` (spaced or compact hex)

Matches: 2

```text
mxopackets1_crashover2k_afterwhoruneo_morpheuzzz_001_login_and_died_mara_teleport_to_archan_open_onedoor.log.txt:13522:02 03 15 00 02 0c 39 95 40 63 47 00 00 d2 c2 25 78 12 46 05 00 02 0c 81 0d 05 61 47 00 00 d2 c2 
mxopackets1_crashover2k_002_create_mxoemu_character2.log.txt:39997:c4 9d 30 70 47 11 00 02 0e 00 81 0d 86 66 46 00 40 30 c4 dc 22 79 47 00 00 
```

### CLIENT_VENDOR_BUY candidate

Signature: `81 0e` (spaced or compact hex)

Matches: 0

### CLIENT_VENDOR_SELL candidate

Signature: `81 11` (spaced or compact hex)

Matches: 2

```text
mxopackets1_crashover2k_mxoemu_001_jackin_coordinates_jackout.log.txt:2210:69 30 73 47 0b 00 02 0e 01 42 92 81 11 46 00 40 30 c4 7c 3a 6b 47 00 00 
mxopackets1_crashover2k_002_create_mxoemu_character2.log.txt:33271:02 03 1d 00 02 08 f5 50 5a 46 00 40 30 c4 81 11 7a 47 11 00 02 0e 00 84 96 a5 6c 46 00 40 30 c4 
```

### CR2 static object interaction

Signature: `80 c8` (spaced or compact hex)

Matches: 5

```text
mxopackets1_crashover2k_afterwhoruneo_morpheuzzz_001_login_and_died_mara_teleport_to_archan_open_onedoor.log.txt:12536:02 04 01 00 37 01 08 80 c8 0f 00 b0 4a 03 00 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_special_agent_spawn.log.txt:408:82 18 a0 ab 47 03 02 00 02 80 84 50 80 c8 00 00 de 08 80 10 ee 03 00 00 10 00 03 00 00 00 08 0b 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_special_agent_spawn.log.txt:3042:02 03 02 00 02 80 88 00 00 80 bf 80 c8 00 00 23 02 80 80 01 20 00 10 01 00 00 04 04 01 d3 01 16 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_special_agent_spawn.log.txt:3078:02 03 02 00 02 80 8c 28 00 00 80 bf 80 c8 00 00 23 02 80 80 01 20 00 10 01 00 00 
mxopackets1_crashover2k_002_create_mxoemu_character2.log.txt:28876:02 04 01 00 c5 01 08 80 c8 4d 00 10 00 06 00 
```

### CR1 static object interaction

Signature: `80 c3` (spaced or compact hex)

Matches: 9

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:11803:60 50 80 c3 00 00 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:20027:74 00 00 00 00 00 00 00 00 00 00 00 00 10 00 00 22 d5 01 03 60 1c 00 c6 01 80 c3 d9 09 00 58 4c 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:20033:61 1c 00 c6 01 80 c3 d7 09 00 58 4c 0a 00 28 00 00 00 e4 6c 13 e6 40 00 00 00 00 00 44 91 c0 00 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:20064:69 6f 6e 00 00 00 00 00 00 00 00 00 00 00 00 00 00 10 00 00 22 d5 01 b2 61 1c 00 c6 01 80 c3 d7 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:20068:00 00 00 00 10 00 00 22 d5 01 87 60 1c 00 c6 01 80 c3 d7 09 00 58 4c 0a 00 28 00 00 00 c0 7e 32 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:20101:00 00 00 00 00 00 00 00 00 00 00 00 00 10 00 00 22 d5 01 f9 5f 1c 00 c6 01 80 c3 d8 09 00 58 4c 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:20105:10 00 00 22 d5 01 a4 60 1c 00 c6 01 80 c3 d7 09 00 58 4c 0a 00 28 00 00 00 64 3a f7 e6 40 00 00 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:20144:00 00 00 00 00 00 00 00 00 00 00 00 10 00 00 22 d5 01 77 5f 1c 00 c6 01 80 c3 d8 09 00 58 4c 0a 
mxopackets1_crashover2k__create_char_mxoemu_and_make_complete_tutorial_enteringtheworld_uriah.log.txt:4684:44 91 c0 00 00 00 c0 1c 79 c9 c0 00 00 04 01 00 b9 01 06 80 c3 92 2a de 00 
```

### SERVER object/state bundle

Signature: `80 bc` (spaced or compact hex)

Matches: 2275

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_special_agent_spawn.log.txt:59:02 03 02 00 03 01 0c 00 80 80 80 c0 60 09 80 80 01 00 00 10 01 00 00 04 01 01 25 03 16 80 bc 1d 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_special_agent_spawn.log.txt:60:00 25 01 00 9e 02 00 00 72 00 b0 ff ff ff 00 00 00 00 00 16 80 bc 15 00 26 01 00 9e 02 00 00 70 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_special_agent_spawn.log.txt:382:00 00 00 01 00 00 00 00 01 04 00 00 00 00 00 80 00 00 00 00 00 00 00 00 01 2f 13 16 80 bc 15 00 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_special_agent_spawn.log.txt:383:1c 01 00 11 00 00 00 ab 00 0d 00 00 00 01 00 00 00 00 16 80 bc 15 00 1d 01 00 11 00 00 00 0a 02 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_special_agent_spawn.log.txt:384:4d 00 00 00 01 00 00 00 00 16 80 bc 15 00 1e 01 00 11 00 00 00 f2 03 34 00 00 00 01 00 00 00 00 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_special_agent_spawn.log.txt:385:16 80 bc 15 00 1f 01 00 11 00 00 00 ed 03 4d 00 00 00 01 00 00 00 00 16 80 bc 15 00 20 01 00 11 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_special_agent_spawn.log.txt:386:00 00 00 9f 00 19 00 00 00 01 00 00 00 00 16 80 bc 15 00 21 01 00 11 00 00 00 f1 03 34 00 00 00 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_special_agent_spawn.log.txt:387:01 00 00 00 00 16 80 bc 15 00 22 01 00 11 00 00 00 0c 02 34 00 00 00 01 00 00 00 00 16 80 bc 15 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_special_agent_spawn.log.txt:388:00 23 01 00 11 00 00 00 b7 00 4c 00 00 00 01 00 00 00 00 16 80 bc 15 00 24 01 00 11 00 00 00 f3 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_special_agent_spawn.log.txt:389:03 46 00 00 00 01 00 00 00 00 0b 80 bd 05 11 00 00 00 00 00 00 00 16 80 bc 15 00 27 01 00 ee 03 
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

Matches: 30

```text
mxopackets1_crashover2k_afterwhoruneo_morpheuzzz_001_login_and_died_mara_teleport_to_archan_open_onedoor.log.txt:3437:00 00 00 00 00 2d 01 00 28 29 00 00 00 00 00 00 00 00 01 00 ff 00 00 00 00 ef ac 00 00 00 00 00 
mxopackets1_crashover2k_afterwhoruneo_morpheuzzz_001_login_and_died_mara_teleport_to_archan_open_onedoor.log.txt:3461:00 00 00 01 00 ff 00 00 00 00 ef ac 00 00 00 00 00 01 22 00 00 00 00 00 00 01 00 00 00 
mxopackets1_crashover2k_afterwhoruneo_morpheuzzz_001_login_and_died_mara_teleport_to_archan_open_onedoor.log.txt:3479:00 ff 00 00 00 00 00 2d 01 00 28 29 00 00 00 00 00 00 00 00 01 00 ff 00 00 00 00 ef ac 00 00 00 
mxopackets1_crashover2k_afterwhoruneo_morpheuzzz_001_login_and_died_mara_teleport_to_archan_open_onedoor.log.txt:3556:00 00 00 00 01 00 ff 00 00 00 00 ef ac 00 00 00 00 00 01 22 00 00 00 00 00 00 01 00 00 00 04 01 
mxopackets1_crashover2k_afterwhoruneo_morpheuzzz_001_login_and_died_mara_teleport_to_archan_open_onedoor.log.txt:3668:00 00 00 00 01 00 ff 00 00 00 00 ef ac 00 00 00 00 00 01 22 00 00 00 00 00 00 01 00 00 00 
mxopackets1_crashover2k_afterwhoruneo_morpheuzzz_001_login_and_died_mara_teleport_to_archan_open_onedoor.log.txt:3686:00 00 00 00 01 00 ff 00 00 00 00 ef ac 00 00 00 00 00 01 22 00 00 00 00 00 00 01 00 00 00 
mxopackets1_crashover2k_afterwhoruneo_morpheuzzz_001_login_and_died_mara_teleport_to_archan_open_onedoor.log.txt:3704:00 00 00 00 00 00 00 01 00 ff 00 00 00 00 ef ac 00 00 00 00 00 01 22 00 00 00 00 00 00 01 00 00 
mxopackets1_crashover2k_afterwhoruneo_morpheuzzz_001_login_and_died_mara_teleport_to_archan_open_onedoor.log.txt:3780:29 00 00 00 00 00 00 00 00 01 00 ff 00 00 00 00 ef ac 00 00 00 00 00 01 22 00 00 00 00 00 00 01 
mxopackets1_crashover2k_afterwhoruneo_morpheuzzz_001_login_and_died_mara_teleport_to_archan_open_onedoor.log.txt:4040:00 00 00 01 00 ff 00 00 00 00 ef ac 00 00 00 00 00 01 22 00 00 00 00 00 00 01 00 00 00 
mxopackets1_crashover2k_afterwhoruneo_morpheuzzz_001_login_and_died_mara_teleport_to_archan_open_onedoor.log.txt:4058:00 00 00 01 00 ff 00 00 00 00 ef ac 00 00 00 00 00 01 22 00 00 00 00 00 00 01 00 00 00 
```

### Selector ff continuation prefix 00 01 04 ff

Signature: `00 01 04 ff` (spaced or compact hex)

Matches: 5

```text
mxopackets1_crashover2k__create_char_mxoemu_and_make_complete_tutorial_enteringtheworld_uriah.log.txt:5015:02 03 02 00 01 04 ff 00 00 
mxopackets1_crashover2k__create_char_mxoemu_and_make_complete_tutorial_enteringtheworld_uriah.log.txt:5287:02 03 02 00 01 04 ff 00 00 
mxopackets1_crashover2k__create_char_mxoemu_and_make_complete_tutorial_enteringtheworld_uriah.log.txt:5995:02 03 02 00 01 04 ff 00 00 
mxopackets1_crashover2k__create_char_mxoemu_and_make_complete_tutorial_enteringtheworld_uriah.log.txt:10271:02 03 02 00 01 04 ff 00 00 
mxopackets1_crashover2k_002_create_mxoemu_character2.log.txt:11033:02 03 02 00 01 04 ff 00 00 
```

### Selector ff continuation prefix 00 01 10 ff

Signature: `00 01 10 ff` (spaced or compact hex)

Matches: 1

```text
mxopackets1_crashover2k_afterwhoruneo_morpheuzzz_001_login_and_died_mara_teleport_to_archan_open_onedoor.log.txt:5300:00 00 00 00 00 01 10 ff 00 00 00 00 bf 61 01 00 00 00 08 01 12 00 00 00 00 00 00 01 00 00 00 
```

### Selector ff continuation prefix 00 00 00 ff

Signature: `00 00 00 ff` (spaced or compact hex)

Matches: 833

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:779:00 00 00 00 32 02 00 00 00 00 0d 00 00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:809:00 00 00 00 00 00 00 00 02 ff 00 00 00 00 00 00 00 00 32 02 00 00 00 00 0d 00 00 00 00 00 00 ff 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:841:00 00 00 32 02 00 00 00 00 0d 00 00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:846:ff 00 00 00 00 00 00 00 00 32 02 00 00 00 00 0d 00 00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:870:00 00 00 32 02 00 00 00 00 0d 00 00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:875:ff 00 00 00 00 00 00 00 00 32 02 00 00 00 00 0d 00 00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:891:00 00 00 32 02 00 00 00 00 0d 00 00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:896:ff 00 00 00 00 00 00 00 00 32 02 00 00 00 00 0d 00 00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:913:00 00 00 00 00 00 02 ff 00 00 00 00 00 00 00 00 32 02 00 00 00 00 0d 00 00 00 00 00 00 ff 00 00 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:919:00 00 00 00 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
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

Matches: 143

```text
mxopackets1_norajob_acc_logs_002_norajob_create_female_char_mxoemu2.log.txt:578:82 7f 9a ff 48 03 01 00 0c 18 0a 76 cd ab 01 01 01 00 00 00 05 00 00 01 00 0c 57 02 77 cd ab 10 
mxopackets1_norajob_acc_logs_002_norajob_create_female_char_mxoemu2.log.txt:1327:02 03 01 00 0c 57 02 48 cd ab 11 8e 43 68 6f 70 70 65 72 20 52 75 6e 6e 65 72 00 00 00 00 00 00 
mxopackets1_norajob_acc_logs_002_norajob_create_female_char_mxoemu2.log.txt:1330:32 09 4b 00 07 08 00 00 23 00 00 01 00 0c 57 02 49 cd ab 12 8e 43 68 6f 70 70 65 72 20 4a 75 6d 
mxopackets1_norajob_acc_logs_002_norajob_create_female_char_mxoemu2.log.txt:1333:c0 01 8b 01 02 16 a8 01 70 00 32 09 70 00 07 08 00 00 21 00 00 01 00 0c 57 02 3e cd ab 10 8e 43 
mxopackets1_norajob_acc_logs_002_norajob_create_female_char_mxoemu2.log.txt:1365:82 10 a4 ff 48 03 01 00 0c 57 02 4b cd ab 12 8e 43 68 6f 70 70 65 72 20 52 75 6e 6e 65 72 00 00 
mxopackets1_norajob_acc_logs_002_norajob_create_female_char_mxoemu2.log.txt:1368:15 a8 01 4b 00 32 09 4b 00 07 08 00 00 1b 00 00 01 00 0c 57 02 4c cd ab 10 8e 43 68 6f 70 70 65 
mxopackets1_norajob_acc_logs_002_norajob_create_female_char_mxoemu2.log.txt:1371:23 97 ed 40 c0 01 8a 03 0d a8 01 4b 00 32 09 4b 00 07 08 00 00 19 00 00 01 00 0c 57 02 40 cd ab 
mxopackets1_norajob_acc_logs_002_norajob_create_female_char_mxoemu2.log.txt:1375:01 00 0c 57 02 4d cd ab 10 8e 43 68 6f 70 70 65 72 20 4a 75 6d 70 65 72 00 00 00 00 00 00 00 00 
mxopackets1_norajob_acc_logs_002_norajob_create_female_char_mxoemu2.log.txt:1406:00 e6 1a 6e 08 46 00 40 30 c4 35 f3 51 47 01 00 0c 57 02 42 cd ab 10 8e 43 68 6f 70 70 65 72 20 
mxopackets1_norajob_acc_logs_002_norajob_create_female_char_mxoemu2.log.txt:1409:ea 40 c0 01 8a 02 0c a8 01 70 00 32 09 70 00 07 08 00 00 13 00 00 01 00 0c 57 02 4e cd ab 0f 8e 
```

### Adjacent Object599 boundary prefix

Signature: `00 0c 57 02` (spaced or compact hex)

Matches: 142

```text
mxopackets1_crashover2k_morpheuzz_vector__create_morpheuzz_char.log.txt:387:02 03 01 00 0c 57 02 2d cd ab 10 8e 41 6d 6d 69 74 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
mxopackets1_crashover2k_morpheuzz_vector__create_morpheuzz_char.log.txt:1403:02 03 01 00 0c 57 02 c2 cd ab 10 8e 43 68 6f 70 70 65 72 20 52 75 6e 6e 65 72 00 00 00 00 00 00 
mxopackets1_crashover2k_morpheuzz_vector__create_morpheuzz_char.log.txt:1406:09 4b 00 07 08 00 00 2b 00 00 01 00 0c 57 02 c3 cd ab 10 8e 43 68 6f 70 70 65 72 20 52 75 6e 6e 
mxopackets1_crashover2k_morpheuzz_vector__create_morpheuzz_char.log.txt:1409:8a 03 15 a8 01 4b 00 32 09 4b 00 07 08 00 00 29 00 00 01 00 0c 57 02 c4 cd ab 10 8e 43 68 6f 70 
mxopackets1_crashover2k_morpheuzz_vector__create_morpheuzz_char.log.txt:1412:00 be a6 25 ef 40 c0 01 8a 02 15 a8 01 70 00 32 09 70 00 07 08 00 00 27 00 00 01 00 0c 57 02 c5 
mxopackets1_crashover2k_morpheuzz_vector__create_morpheuzz_char.log.txt:1441:02 03 01 00 0c 57 02 bf cd ab 10 8e 43 68 6f 70 70 65 72 20 52 75 6e 6e 65 72 00 00 00 00 00 00 
mxopackets1_crashover2k_morpheuzz_vector__create_morpheuzz_char.log.txt:1446:1f 00 00 01 00 0c 57 02 c6 cd ab 11 8e 43 68 6f 70 70 65 72 20 52 75 6e 6e 65 72 00 00 00 00 00 
mxopackets1_crashover2k_morpheuzz_vector__create_morpheuzz_char.log.txt:1449:00 32 09 4b 00 07 08 00 00 1d 00 00 01 00 0c 57 02 c0 cd ab 12 8e 43 68 6f 70 70 65 72 20 52 75 
mxopackets1_crashover2k_morpheuzz_vector__create_morpheuzz_char.log.txt:1452:40 c0 01 8b 01 03 15 a8 01 4b 00 32 09 4b 00 07 08 00 00 1b 00 00 01 00 0c 57 02 c7 cd ab 11 8e 
mxopackets1_crashover2k_morpheuzz_vector__create_morpheuzz_char.log.txt:1483:9c c9 75 47 01 00 02 12 00 19 00 02 08 b9 22 81 46 00 40 30 c4 bf 49 70 47 01 00 0c 57 02 c8 cd 
```

### Header-like selector 50

Signature: `01 00 08 50` (spaced or compact hex)

Matches: 4

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:410:c0 01 83 05 01 a8 0d ee 02 b2 04 ee 02 07 08 00 00 81 b5 02 00 00 02 03 0f 00 00 01 00 08 50 02 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:20146:77 01 b2 09 77 01 07 08 00 00 81 b5 02 00 00 02 03 05 00 00 01 00 08 50 02 f4 09 e0 03 b3 cd ab 
mxopackets1_crashover2k_afterwhoruneo_morpheuzzz_001_login_and_died_mara_teleport_to_archan_open_onedoor.log.txt:768:ff d7 30 46 12 b0 01 10 04 00 89 ec 01 3a 23 a4 1c 03 ee 03 00 00 81 ff 21 00 00 01 00 08 50 02 
mxopackets1_crashover2k_afterwhoruneo_morpheuzzz_001_login_and_died_mara_teleport_to_archan_open_onedoor.log.txt:6111:07 00 00 80 06 01 35 08 00 00 0f 00 00 01 00 08 50 02 7d 02 b0 4a 2a cd ab 02 09 00 00 00 00 80 
```

### Header-like selector 67

Signature: `01 00 08 67` (spaced or compact hex)

Matches: 0

### Header-like selector 68

Signature: `01 00 08 68` (spaced or compact hex)

Matches: 2

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:341:00 00 02 03 1d 00 00 01 00 08 68 95 44 00 80 14 a1 cd ab 02 09 00 00 00 00 80 fe e2 40 00 00 00 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:20141:00 17 ef c3 3e 09 00 00 01 00 08 68 95 44 00 80 14 a1 cd ab 02 09 00 00 00 00 80 fe e2 40 00 00 
```

### Header-like selector 69

Signature: `01 00 08 69` (spaced or compact hex)

Matches: 2

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:332:77 01 07 08 00 00 81 b5 02 00 00 02 03 23 00 00 01 00 08 69 95 43 00 80 14 a0 cd ab 02 09 00 00 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:20029:0d 32 02 b2 09 32 02 07 08 00 00 81 b5 02 00 00 02 03 23 00 00 01 00 08 69 95 43 00 80 14 a0 cd 
```

### Header-like selector 76

Signature: `01 00 0c 76` (spaced or compact hex)

Matches: 0

### Header-like selector 90

Signature: `01 00 08 90` (spaced or compact hex)

Matches: 0

### Header-like selector a3

Signature: `01 00 08 a3` (spaced or compact hex)

Matches: 0

### Header-like selector d0

Signature: `01 00 08 d0` (spaced or compact hex)

Matches: 8

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:20137:02 03 01 00 08 d0 20 fc 09 e0 03 10 cd ab 08 fd 00 00 00 00 40 5d e7 40 00 00 00 00 00 44 91 c0 
mxopackets1_crashover2k_afterwhoruneo_morpheuzzz_001_login_and_died_mara_teleport_to_archan_open_onedoor.log.txt:756:02 03 01 00 08 d0 20 af 00 20 4e ff cd ab 08 fd 00 00 00 00 c0 a8 d4 40 00 00 00 00 00 f0 7e 40 
mxopackets1_crashover2k_afterwhoruneo_morpheuzzz_001_login_and_died_mara_teleport_to_archan_open_onedoor.log.txt:812:23 87 48 0c 1d da b9 00 00 03 81 00 00 00 81 ff 1f 00 00 01 00 08 d0 20 b0 00 20 4e fd cd ab 08 
mxopackets1_crashover2k_afterwhoruneo_morpheuzzz_001_login_and_died_mara_teleport_to_archan_open_onedoor.log.txt:816:00 58 00 00 03 00 00 00 00 00 00 00 80 3f 00 00 00 00 2e bd 3b b3 1b 00 00 01 00 08 d0 19 d4 04 
mxopackets1_crashover2k_afterwhoruneo_morpheuzzz_001_login_and_died_mara_teleport_to_archan_open_onedoor.log.txt:854:02 03 01 00 08 d0 20 b1 00 20 4e fe cd ab 07 fd 00 00 00 00 00 f8 d1 40 00 00 00 00 00 f0 7e 40 
mxopackets1_crashover2k_afterwhoruneo_morpheuzzz_001_login_and_died_mara_teleport_to_archan_open_onedoor.log.txt:857:00 00 00 00 00 00 00 00 02 00 00 3a 00 38 0b 00 58 00 00 01 00 19 00 00 01 00 08 d0 20 0e 00 c0 
mxopackets1_crashover2k_afterwhoruneo_morpheuzzz_001_login_and_died_mara_teleport_to_archan_open_onedoor.log.txt:913:02 03 01 00 08 d0 20 f1 01 30 39 04 cd ab 08 fd 00 00 00 00 80 83 cf 40 00 00 00 00 00 f0 7e 40 
mxopackets1_crashover2k_afterwhoruneo_morpheuzzz_001_login_and_died_mara_teleport_to_archan_open_onedoor.log.txt:919:00 01 00 08 d0 20 b4 00 20 4e fc cd ab 08 fd 00 00 00 00 40 1e d0 40 00 00 00 00 00 20 7f 40 00 
```

## Movement And Object-State Leads

### Mode 06 movement selector 08

Signature: `00 06 08` (spaced or compact hex)

Matches: 5

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:7921:00 00 00 22 00 00 00 00 00 0f 00 04 80 80 80 80 80 01 03 0a 00 06 08 36 5f 38 47 00 20 8a c4 80 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:7945:02 03 03 00 06 08 8a 18 2a 47 00 20 8a c4 70 14 24 c4 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 
mxopackets1_crashover2k_mxoemu_001_jackin_coordinates_jackout.log.txt:8729:02 03 1f 00 06 08 4d 05 fe 45 00 40 30 c4 69 fd 77 47 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_special_agent_spawn.log.txt:1375:02 03 12 00 04 80 80 80 80 c0 11 0a 00 28 01 05 0e 00 06 08 26 a0 83 c7 00 00 be 42 54 fb 83 46 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_special_agent_spawn.log.txt:3004:02 03 0e 00 06 08 d5 03 84 c7 00 00 be 42 df d9 83 46 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 
```

### Mode 06 movement selector 0a

Signature: `00 06 0a` (spaced or compact hex)

Matches: 21

```text
mxopackets1_crashover2k__create_char_mxoemu_and_make_complete_tutorial_enteringtheworld_uriah.log.txt:17431:02 03 09 00 06 0a 00 02 dc 80 46 00 40 30 c4 90 e8 6c 47 80 80 80 80 80 80 01 45 21 00 00 05 00 
mxopackets1_crashover2k__create_char_mxoemu_and_make_complete_tutorial_enteringtheworld_uriah.log.txt:17444:00 00 10 01 2b 64 8f ef 45 00 40 30 c4 3c 21 77 47 09 00 06 0a 00 02 dc 80 46 00 40 30 c4 90 e8 
mxopackets1_crashover2k__create_char_mxoemu_and_make_complete_tutorial_enteringtheworld_uriah.log.txt:17937:82 e1 44 fd 48 03 0c 00 06 0a 00 21 9d ef 45 00 40 30 c4 bc d7 52 47 ff 00 00 00 10 00 00 00 00 
mxopackets1_crashover2k__create_char_mxoemu_and_make_complete_tutorial_enteringtheworld_uriah.log.txt:17957:02 03 0c 00 06 0a 00 21 9d ef 45 00 40 30 c4 bc d7 52 47 ff 00 00 00 10 00 00 00 00 00 01 ff 00 
mxopackets1_crashover2k__create_char_mxoemu_and_make_complete_tutorial_enteringtheworld_uriah.log.txt:19161:02 03 17 00 06 0a 00 a3 83 26 46 00 40 30 c4 49 73 7f 47 80 80 80 80 80 80 01 45 21 00 00 0d 00 
mxopackets1_crashover2k__create_char_mxoemu_and_make_complete_tutorial_enteringtheworld_uriah.log.txt:19173:82 e5 46 fd 48 03 17 00 06 0a 00 a3 83 26 46 00 40 30 c4 49 73 7f 47 80 80 80 80 80 80 01 47 21 
mxopackets1_crashover2k_mxoemu_001_jackin_coordinates_jackout.log.txt:3623:02 03 13 00 02 0c 0e e5 31 24 46 00 40 30 c4 9a 2a 7b 47 04 00 06 0a 00 47 02 86 46 00 40 30 c4 
mxopackets1_crashover2k_mxoemu_001_jackin_coordinates_jackout.log.txt:4190:82 14 76 0d 49 03 1f 00 06 0a 00 77 26 fd 45 00 40 30 c4 a7 cc 77 47 80 80 80 80 80 80 01 39 21 
mxopackets1_crashover2k_mxoemu_001_jackin_coordinates_jackout.log.txt:8654:02 03 1f 00 06 0a 00 2c 27 fd 45 00 40 30 c4 65 cc 77 47 80 80 80 80 80 80 01 39 21 00 00 11 00 
mxopackets1_crashover2k_mxoemu_001_jackin_coordinates_jackout.log.txt:11251:02 03 13 00 02 0c a9 5d 06 21 46 00 40 30 c4 9a 8c 7b 47 0c 00 06 0a 00 e2 fd 85 46 00 40 30 c4 
```

### Mode 06 movement selector 0c

Signature: `00 06 0c` (spaced or compact hex)

Matches: 163

```text
mxopackets1_crashover2k_morpheuzz_vector__create_morpheuzz_char.log.txt:3791:02 03 17 00 02 0e 01 9c db 38 8a 46 00 40 30 c4 91 d3 73 47 15 00 06 0c fe e0 eb 80 46 00 40 30 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:812:d9 09 00 58 37 08 00 00 1f 07 08 00 00 00 00 00 00 22 00 00 00 00 00 03 00 06 0c 26 9a 77 27 47 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:839:02 03 09 00 06 0c 55 a8 ef 33 47 00 20 8a c4 8c 90 7c c5 ff 00 00 00 10 00 00 00 00 00 01 ff 00 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:844:00 00 22 00 00 00 00 00 03 00 06 0c 30 9a 77 27 47 00 20 8a c4 00 65 72 c3 ff 00 00 00 10 00 00 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:868:02 03 09 00 06 0c 4b a8 ef 33 47 00 20 8a c4 8c 90 7c c5 ff 00 00 00 10 00 00 00 00 00 01 ff 00 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:873:00 00 22 00 00 00 00 00 03 00 06 0c 3a 9a 77 27 47 00 20 8a c4 00 65 72 c3 ff 00 00 00 10 00 00 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:889:02 03 09 00 06 0c 41 a8 ef 33 47 00 20 8a c4 8c 90 7c c5 ff 00 00 00 10 00 00 00 00 00 01 ff 00 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:894:00 00 22 00 00 00 00 00 03 00 06 0c 44 9a 77 27 47 00 20 8a c4 00 65 72 c3 ff 00 00 00 10 00 00 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:911:02 0c 83 16 da 37 47 00 20 8a c4 e8 b3 ea c4 09 00 06 0c 37 a8 ef 33 47 00 20 8a c4 8c 90 7c c5 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:932:02 03 09 00 06 0c 2d a8 ef 33 47 00 20 8a c4 8c 90 7c c5 ff 00 00 00 10 00 00 00 00 00 01 ff 00 
```

### Mode 06 movement selector 0e

Signature: `00 06 0e` (spaced or compact hex)

Matches: 201

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:777:02 03 09 00 06 0e 05 69 a8 ef 33 47 00 20 8a c4 8c 90 7c c5 ff 00 00 00 10 00 00 00 00 00 01 ff 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:807:02 03 0f 00 04 80 80 80 80 c0 4c 0a 00 28 01 01 09 00 06 0e 05 5f a8 ef 33 47 00 20 8a c4 8c 90 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:916:00 58 37 08 00 00 1f 07 08 00 00 00 00 00 00 22 00 00 00 00 00 03 00 06 0e 00 4e 9a 77 27 47 00 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:937:00 00 22 00 00 00 00 00 03 00 06 0e 00 54 9a 77 27 47 00 20 8a c4 00 65 72 c3 ff 00 00 00 10 00 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:997:02 03 13 00 02 0c 82 0d d3 37 47 00 20 8a c4 c4 16 f9 c4 09 00 06 0e 00 04 a8 ef 33 47 00 20 8a 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:1012:80 80 c0 4c 0a 00 28 01 01 09 00 06 0e 00 fa a8 ef 33 47 00 20 8a c4 8c 90 7c c5 ff 00 00 00 10 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:1027:02 03 13 00 06 0e 04 8c ac d1 37 47 00 20 8a c4 1c e5 fb c4 ff 00 00 00 10 00 00 00 00 00 01 ff 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:1048:02 03 19 00 02 0e 05 d2 12 54 35 47 00 20 8a c4 20 d8 25 c4 13 00 06 0e 00 96 ac d1 37 47 00 20 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:1062:02 03 19 00 02 0e 05 c8 12 54 35 47 00 20 8a c4 20 d8 25 c4 13 00 06 0e 00 a0 ac d1 37 47 00 20 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:7896:02 03 15 00 06 0e 00 64 65 90 36 47 00 20 8a c4 b0 09 21 c5 ff 00 00 00 10 00 00 00 00 00 01 ff 
```

### Repeated mode 06 suffix lead

Signature: `ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00` (spaced or compact hex)

Matches: 66

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:808:7c c5 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:912:ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:917:20 8a c4 00 65 72 c3 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:998:c4 8c 90 7c c5 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:1049:8a c4 1c e5 fb c4 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:1063:8a c4 1c e5 fb c4 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:1077:1c e5 fb c4 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:7922:ab 2f c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:15127:c4 5a d5 28 c5 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:15159:5a d5 28 c5 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
```

### Movement-state ff 01 64 01 tail

Signature: `ff 01 64 01` (spaced or compact hex)

Matches: 31

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:17132:ff 01 64 01 00 00 00 00 00 00 00 00 00 00 00 08 41 6e 64 65 72 73 6f 6e 06 54 68 6f 6d 61 73 ff 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:17143:02 03 02 00 03 28 02 c0 00 74 00 10 00 30 27 47 00 20 8a c4 00 30 b1 c5 e9 f9 52 1f ff 01 64 01 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:17160:02 03 02 00 03 28 02 c0 00 74 00 10 00 30 27 47 00 20 8a c4 00 30 b1 c5 66 fa 52 1f ff 01 64 01 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:17178:02 03 02 00 03 08 00 30 27 47 00 20 8a c4 00 30 b1 c5 e3 fa 52 1f ff 01 64 01 00 00 00 00 00 00 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:17190:02 03 02 00 03 08 00 30 27 47 00 20 8a c4 00 30 b1 c5 60 fb 52 1f ff 01 64 01 00 00 00 00 00 00 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:17206:02 03 02 00 03 08 00 30 27 47 00 20 8a c4 00 30 b1 c5 dd fb 52 1f ff 01 64 01 00 00 00 00 00 00 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:17228:02 03 02 00 03 28 02 c0 00 74 00 10 00 30 27 47 00 20 8a c4 00 30 b1 c5 d7 fc 52 1f ff 01 64 01 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:17246:02 03 02 00 03 08 00 30 27 47 00 20 8a c4 00 30 b1 c5 54 fd 52 1f ff 01 64 01 00 00 00 00 00 00 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:17263:82 ca 4d 00 49 03 02 00 03 08 00 30 27 47 00 20 8a c4 00 30 b1 c5 d1 fd 52 1f ff 01 64 01 00 00 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:17279:02 03 02 00 03 28 02 c0 00 74 00 10 00 30 27 47 00 20 8a c4 00 30 b1 c5 cb fe 52 1f ff 01 64 01 
```

### Movement-state ff 01 4e 01 tail

Signature: `ff 01 4e 01` (spaced or compact hex)

Matches: 1

```text
mxopackets1_crashover2k_afterwhoruneo_morpheuzzz_001_login_and_died_mara_teleport_to_archan_open_onedoor.log.txt:5296:02 03 21 00 03 09 00 0e 50 50 88 46 00 80 f7 43 7e 23 95 45 ff 01 4e 01 00 00 00 00 00 00 45 20 
```

## Ability, Effect, And Combat-State Leads

### SERVER_PLAYER_ATTRIBUTE / ability load

Signature: `80 b2` (spaced or compact hex)

Matches: 80

```text
mxopackets1_norajob_acc_logs_002_norajob_create_female_char_mxoemu2.log.txt:57:00 00 16 80 bc 45 00 02 00 00 02 00 00 00 cc 00 00 00 00 00 01 00 00 00 00 08 80 b2 11 00 01 00 
mxopackets1_norajob_acc_logs_002_norajob_create_female_char_mxoemu2.log.txt:368:00 00 00 00 00 01 00 00 00 00 08 80 b2 11 00 01 00 08 02 16 80 bc 45 03 11 00 00 02 00 00 00 11 
mxopackets1_norajob_acc_logs_002_norajob_create_female_char_mxoemu2.log.txt:428:73 65 20 62 75 74 74 6f 6e 00 08 80 b2 bb 01 01 00 08 02 16 80 bc 15 00 12 00 00 11 00 00 00 ab 
mxopackets1_norajob_acc_logs_002_norajob_create_female_char_mxoemu2.log.txt:474:03 00 00 00 00 00 00 00 00 00 0b 80 bd 05 11 00 00 00 00 00 00 01 08 80 b2 cc 01 01 00 08 02 16 
mxopackets1_norajob_acc_logs_002_norajob_create_female_char_mxoemu2.log.txt:861:02 04 01 00 06 09 16 80 bc 55 00 3e 00 00 02 00 00 00 cc 00 00 00 00 00 01 00 00 00 00 08 80 b2 
mxopackets1_crashover2k_morpheuzz_vector__create_morpheuzz_char.log.txt:39:00 00 00 08 80 b2 4e 00 08 00 08 02 08 80 b2 52 00 05 00 08 02 08 80 b2 54 00 0b 00 08 02 08 80 
mxopackets1_crashover2k_morpheuzz_vector__create_morpheuzz_char.log.txt:40:b2 4f 00 08 00 08 02 08 80 b2 51 00 08 00 08 02 08 80 b2 11 00 01 00 08 02 16 80 bc 45 03 11 00 
mxopackets1_crashover2k_morpheuzz_vector__create_morpheuzz_char.log.txt:90:00 00 16 80 bc 45 00 02 00 00 02 00 00 00 cc 00 00 00 00 00 01 00 00 00 00 08 80 b2 11 00 01 00 
mxopackets1_crashover2k_morpheuzz_vector__create_morpheuzz_char.log.txt:353:00 00 00 00 16 80 bc 55 00 10 00 00 02 00 00 00 cc 00 00 00 00 00 01 00 00 00 00 08 80 b2 11 00 
mxopackets1_crashover2k_morpheuzz_vector__create_morpheuzz_char.log.txt:485:74 6f 6e 00 08 80 b2 bb 01 01 00 08 02 
```

### SERVER_ABILITY_UNLOAD

Signature: `80 b3` (spaced or compact hex)

Matches: 54

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:214:01 00 00 00 00 04 80 b3 11 00 16 80 bc 45 03 11 00 00 02 00 00 00 11 00 32 00 00 00 01 00 00 00 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:215:00 04 80 b3 35 04 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:230:02 04 01 01 81 0a 16 80 bc 45 03 35 04 00 62 00 00 00 35 04 00 00 00 00 01 00 00 00 00 04 80 b3 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:231:3d 04 16 80 bc 45 03 3d 04 00 43 00 00 00 3d 04 00 00 00 00 01 00 00 00 00 04 80 b3 3e 04 16 80 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:232:bc 45 03 3e 04 00 4f 01 00 00 3e 04 00 00 00 00 01 00 00 00 00 04 80 b3 3f 04 16 80 bc 45 03 3f 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:233:04 00 64 01 00 00 3f 04 00 00 00 00 01 00 00 00 00 04 80 b3 40 04 16 80 bc 45 03 40 04 00 d5 01 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:527:00 f3 03 41 00 00 00 01 00 00 00 00 04 80 b3 11 00 16 80 bc 45 03 11 00 00 02 00 00 00 11 00 32 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:560:00 01 00 00 00 00 16 80 bc 55 00 c4 02 00 34 00 00 00 83 02 c8 00 00 00 00 00 00 00 00 04 80 b3 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:567:00 00 00 00 86 09 16 80 bc 55 00 71 02 00 62 00 00 00 06 04 01 00 00 00 01 00 00 00 00 04 80 b3 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:593:04 80 b3 3f 04 16 80 bc 45 03 3f 04 00 64 01 00 00 3f 04 00 00 00 00 01 00 00 00 00 16 80 bc 55 
```

### Object599 adrenaline booster effect id

Signature: `4c 0a 00 28` (spaced or compact hex)

Matches: 78

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:63:02 03 06 00 04 80 80 80 80 c0 4c 0a 00 28 01 01 03 00 02 0e 00 a5 48 9f 0b c6 00 00 be 42 c6 ef 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:88:02 03 0e 00 04 80 80 80 80 c0 4c 0a 00 28 01 01 0b 00 02 0c 33 5d 38 8d c6 80 6e 17 43 c0 6d 3d 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:733:02 03 23 00 04 80 80 80 80 c0 4c 0a 00 28 01 01 1b 00 04 80 80 80 80 c0 4c 0a 00 28 01 01 19 00 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:734:02 08 5c 38 36 47 00 20 8a c4 c0 ae 54 c4 17 00 04 80 80 80 80 c0 4c 0a 00 28 01 01 15 00 04 80 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:735:80 80 80 c0 4c 0a 00 28 01 01 13 00 02 08 fc e0 37 47 00 20 8a c4 e8 da d9 c4 11 00 04 80 80 80 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:736:80 c0 4c 0a 00 28 01 01 09 00 04 80 80 80 80 c0 4c 0a 00 28 01 01 03 00 02 0c 12 9a 77 27 47 00 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:781:00 4c 0a 00 28 ff 01 00 00 00 00 00 00 00 00 00 00 00 d9 09 00 58 37 08 00 00 1f 07 08 00 00 00 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:807:02 03 0f 00 04 80 80 80 80 c0 4c 0a 00 28 01 01 09 00 06 0e 05 5f a8 ef 33 47 00 20 8a c4 8c 90 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:811:32 02 ff 00 00 00 00 00 00 00 00 b5 02 00 00 4c 0a 00 28 ff 01 00 00 00 00 00 00 00 00 00 00 00 
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_loggedinsession001.log.txt:813:00 20 8a c4 00 65 72 c3 80 80 80 80 c0 4c 0a 00 28 01 01 00 00 04 01 01 0f 11 16 80 bc 55 00 d8 
```

### Object599 fast healing effect id

Signature: `b5 07 00 28` (spaced or compact hex)

Matches: 0

### Object599 accelerated spawn effect id

Signature: `66 00 00 04` (spaced or compact hex)

Matches: 0

## Intake Next Steps

- Preserve the original files separately before editing or importing anything.
- Inspect matches with surrounding lines; embedded hits inside encrypted or unrelated bodies are leads only.
- Record sender, provenance, CR1/CR2 status, action context, character handle, coordinates, known object ids, and known item GoIDs.
- Import only reviewed text logs or text-like dumps that clear repository safety review; leave binaries and unrelated game assets out of Git.
