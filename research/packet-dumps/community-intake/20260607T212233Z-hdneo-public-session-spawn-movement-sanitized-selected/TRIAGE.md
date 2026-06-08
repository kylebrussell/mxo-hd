# Packet Capture Triage Report

- Source kind: `directory`
- Source input: `/Users/kyle/Developer/mxo-hd/research/packet-dumps/community-intake/20260607T212233Z-hdneo-public-session-spawn-movement-sanitized-selected/logs`
- Scanned files from: `/Users/kyle/Developer/mxo-hd/research/packet-dumps/community-intake/20260607T212233Z-hdneo-public-session-spawn-movement-sanitized-selected/logs`
- Generated UTC: `2026-06-07T21:22:35Z`
- Max sample lines per pattern: `10`

Use this report to decide whether received logs are worth importing into `research/packet-dumps/`. Matches are byte-string leads, not decoded packet claims.

## Vendor And Interaction Leads

### SERVER_VENDOR_OPEN candidate

Signature: `81 0d` (spaced or compact hex)

Matches: 6

```text
mxopackets1_luizoe_recursion1.log.txt:26055:02 03 02 00 02 80 80 80 50 4c 00 81 0d 00 00
mxopackets1_crashover2k_009_session_complete.log.txt:7341:02 03 18 00 02 0e 00 56 aa 8c c4 c5 f8 df ec 44 c5 55 4b 47 00 00 04 01 01 0e 01 81 0a 81 0d 7c
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:13324:ff 00 00 00 00 fe 5d 01 00 00 00 18 01 12 00 00 00 00 00 00 01 00 23 00 02 80 80 10 81 0d 0d 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_login_session.log.txt:12415:42 81 0d 66 45 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:61201:00 00 16 80 bc 56 00 81 0d 00 c3 b2 00 00 b4 00 1f 00 00 00 01 00 00 00 00 16 80 bc 56 00 82 0d
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:63101:02 03 01 00 02 13 00 13 00 02 0c 36 81 0d 20 47 00 80 fc c3 96 5c bd c6 01 00 08 a3 01 2b 01 30
```

### CLIENT_VENDOR_BUY candidate

Signature: `81 0e` (spaced or compact hex)

Matches: 42

```text
mxopackets1_crashover2k_009_session_complete.log.txt:38281:02 03 09 00 02 0e 01 fe 4d 81 0e c6 f8 df ec 44 4a ed 68 47 08 00 02 0e 00 1a 76 c4 9a c5 f8 df
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:4611:ac 00 00 00 00 08 01 12 00 00 00 00 00 00 01 00 45 00 01 0e 05 0d 81 0e 84 46 00 80 f7 43 0b 6d
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:4632:46 00 80 f7 43 35 54 9a 44 45 00 01 0e 00 fb 81 0e 84 46 00 80 f7 43 0b 6d 0a 45 41 00 01 0a 00
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:4664:57 65 00 01 0c 35 f7 69 85 46 00 80 f7 43 43 72 9d 44 45 00 01 0e 00 f2 81 0e 84 46 00 80 f7 43
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:4715:46 00 80 f7 43 be b2 ad 44 45 00 01 2e 30 c0 00 98 d9 00 00 f2 81 0e 84 46 00 80 f7 43 0b 6d 0a
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:4953:45 45 00 01 0e 04 fa 81 0e 84 46 00 80 f7 43 0b 6d 0a 45 33 00 03 0a 03 82 45 85 46 00 80 f7 43
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:4984:68 ca 17 45 49 00 01 0e 04 58 eb 4d 7b 46 00 80 f7 43 7f 86 2f 45 45 00 01 0e 04 03 81 0e 84 46
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:5009:86 2f 45 45 00 01 0c 0c 81 0e 84 46 00 80 f7 43 0b 6d 0a 45 3b 00 01 02 00 33 00 03 08 dc ff 85
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:5038:00 01 0e 00 1f 81 0e 84 46 00 80 f7 43 0b 6d 0a 45 33 00 03 08 ed 54 86 46 00 80 f7 43 ec 81 49
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:11670:02 03 59 00 01 0e 05 fd 9e f3 85 46 00 80 f7 43 b7 cf 2a 45 45 00 01 0e 00 17 81 0e 84 46 00 80
```

### CLIENT_VENDOR_SELL candidate

Signature: `81 11` (spaced or compact hex)

Matches: 7

```text
mxopackets1_luizoe_recursion1.log.txt:18924:02 03 18 00 06 0e 01 54 13 81 11 c6 f8 df ec 44 12 3f 54 47 ff 00 00 00 10 00 00 00 00 00 01 ff
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:16657:00 00 00 01 00 03 00 01 0e 3e 5b de 21 83 46 00 80 f7 43 3f 81 11 45 00 00
mxopackets1_crashover2k_011_idle_park_east.log.txt:1007:55 00 81 11 00 5d 01 00 00 20 04 0f 00 00 00 00 00 00 00 00 16 80 bc 55 00 25 11 00 d7 01 00 00
mxopackets1_crashover2k_011_idle_park_east.log.txt:1106:bc 55 00 81 11 00 5d 01 00 00 20 04 0f 00 00 00 00 00 00 00 00 16 80 bc 55 00 82 11 00 d7 01 00
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:79066:16 80 bc 15 00 81 11 00 11 00 00 00 ab 00 0c 00 00 00 00 00 00 00 00 16 80 bc 15 00 82 11 00 11
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:79185:00 00 16 80 bc 55 00 80 11 00 36 02 00 00 37 02 0a 00 00 00 00 00 00 00 00 16 80 bc 15 00 81 11
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:82193:00 00 00 16 80 bc 15 00 81 11 00 11 00 00 00 ab 00 0c 00 00 00 01 00 00 00 00 16 80 bc 15 00 82
```

### CR2 static object interaction

Signature: `80 c8` (spaced or compact hex)

Matches: 14

```text
mxopackets1_crashover2k_009_session_complete.log.txt:7309:02 04 01 00 49 01 08 80 c8 be 1f 80 11 02 00
mxopackets1_luizoe_recursion1.log.txt:23467:02 03 02 00 02 80 84 5a 80 c8 03 00 bc 0d 80 80 01 00 00 10 00 08 00 02 0c a2 c2 f2 bf c5 f8 df
mxopackets1_crashover2k_011_idle_park_east.log.txt:3685:82 23 05 2a 46 03 07 00 02 08 b2 76 7d c7 f0 bf 2d 44 80 c8 40 c6 00 00
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:9393:82 0e 80 c8 47 03 83 00 01 08 c9 5d 7d 46 00 80 f7 43 b7 f9 5b 45 65 00 01 0e 01 70 d7 d5 82 46
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:9617:82 9e 80 c8 47 03 65 00 01 0a 00 a7 24 83 46 00 80 f7 43 96 74 ff 44 59 00 01 0e 01 4e 8c 13 85
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:19383:80 c8 ce 03 10 04 00 00 08 03 0d 00 01 0e 00 22 56 be 67 46 00 a0 87 44 8c b1 1a 45 0b 00 01 0a
mxopackets1_crashover2k__create_char_devoted_make_first_mission.log.txt:34586:02 04 01 00 58 01 08 80 c8 01 00 20 10 03 00
mxopackets1_crashover2k__create_char_devoted_make_first_mission.log.txt:35867:02 04 01 00 5d 01 08 80 c8 e4 00 20 10 03 00
mxopackets1_crashover2k__create_char_devoted_make_first_mission.log.txt:36735:02 04 01 00 60 01 08 80 c8 db 00 20 10 03 00
mxopackets1_crashover2k__create_char_devoted_make_first_mission.log.txt:37301:02 04 01 00 66 01 08 80 c8 05 01 20 10 03 00
```

### CR1 static object interaction

Signature: `80 c3` (spaced or compact hex)

Matches: 122

```text
mxopackets1_crashover2k_009_session_complete.log.txt:5019:00 00 00 00 00 00 00 00 00 00 00 00 00 10 00 00 22 d5 01 9e 2f 1b 00 c6 01 80 c3 5b 0b 00 58 4c
mxopackets1_crashover2k_009_session_complete.log.txt:5849:00 00 00 00 00 00 00 00 00 00 00 00 00 10 00 00 22 d5 01 4b 2f 1b 00 c6 01 80 c3 5a 0b 00 58 4c
mxopackets1_crashover2k_009_session_complete.log.txt:6506:00 00 00 00 00 00 00 00 00 00 00 00 00 10 00 00 22 d5 01 3c 2f 1b 00 c6 01 80 c3 5b 0b 00 58 4c
mxopackets1_crashover2k_009_session_complete.log.txt:10043:00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 10 00 00 22 d5 02 c9 2f 1b 00 c6 01 80 c3 59
mxopackets1_crashover2k_009_session_complete.log.txt:11439:00 00 00 00 00 00 00 00 00 00 00 00 00 10 00 00 22 d5 01 fa 30 1b 00 c6 01 80 c3 58 0b 00 58 4c
mxopackets1_crashover2k_009_session_complete.log.txt:12993:00 00 00 00 00 00 10 00 00 22 d5 03 06 2f 1b 00 c6 01 80 c3 59 0b 00 58 4c 0a 00 28 00 00 00 80
mxopackets1_crashover2k_009_session_complete.log.txt:12997:24 2f 1b 00 c6 01 80 c3 5a 0b 00 58 4c 0a 00 28 00 00 00 e0 68 1d b3 c0 00 00 00 00 ff 9b 9d 40
mxopackets1_crashover2k_009_session_complete.log.txt:13000:00 00 00 00 00 00 00 00 00 00 00 00 00 10 00 00 22 d5 03 91 2f 1b 00 c6 01 80 c3 5b 0b 00 58 4c
mxopackets1_crashover2k_009_session_complete.log.txt:14238:00 00 00 00 00 00 00 00 00 00 00 00 00 10 00 00 22 d5 03 e9 2f 1b 00 c6 01 80 c3 59 0b 00 58 4c
mxopackets1_crashover2k_009_session_complete.log.txt:16827:00 00 00 00 00 00 00 10 00 00 22 d5 03 a8 2f 1b 00 c6 01 80 c3 5b 0b 00 58 4c 0a 00 28 00 00 00
```

### SERVER object/state bundle

Signature: `80 bc` (spaced or compact hex)

Matches: 16249

```text
mxopackets1_crashover2k_009_session_complete.log.txt:29:b2 4f 00 08 00 08 02 08 80 b2 51 00 1e 00 08 02 08 80 b2 11 00 32 00 08 02 16 80 bc 45 03 11 00
mxopackets1_crashover2k_009_session_complete.log.txt:30:00 02 00 00 00 11 00 32 00 00 00 00 00 00 00 00 16 80 bc 45 00 02 00 00 02 00 00 00 cc 00 0c 00
mxopackets1_crashover2k_009_session_complete.log.txt:31:00 00 00 00 00 00 00 16 80 bc 45 00 03 00 00 0b 00 00 00 37 02 96 00 00 00 00 00 00 00 00 16 80
mxopackets1_crashover2k_009_session_complete.log.txt:32:bc 45 00 04 00 00 2d 00 00 00 23 04 0a 00 00 00 00 00 00 00 00 16 80 bc 45 00 05 00 00 2d 00 00
mxopackets1_crashover2k_009_session_complete.log.txt:33:00 20 04 0a 00 00 00 00 00 00 00 00 16 80 bc 45 00 06 00 00 2d 00 00 00 19 04 05 00 00 00 00 00
mxopackets1_crashover2k_009_session_complete.log.txt:34:00 00 00 16 80 bc 45 00 07 00 00 2d 00 00 00 42 04 05 00 00 00 00 00 00 00 00 16 80 bc 45 00 08
mxopackets1_crashover2k_009_session_complete.log.txt:35:00 00 34 00 00 00 83 02 c8 00 00 00 00 00 00 00 00 08 80 b2 36 04 00 00 08 02 16 80 bc 45 03 36
mxopackets1_crashover2k_009_session_complete.log.txt:36:04 00 40 00 00 00 36 04 00 00 00 00 00 00 00 00 00 16 80 bc 45 00 09 00 00 40 00 00 00 7b 04 01
mxopackets1_crashover2k_009_session_complete.log.txt:37:00 00 00 00 00 00 00 00 16 80 bc 45 00 0a 00 00 49 00 00 00 e3 03 05 00 00 00 00 00 00 00 00 16
mxopackets1_crashover2k_009_session_complete.log.txt:38:80 bc 45 00 0b 00 00 49 00 00 00 7b 04 0e 00 00 00 00 00 00 00 00 16 80 bc 45 00 0c 00 00 4c 00
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

Matches: 426

```text
mxopackets1_crashover2k__create_char_devoted_make_first_mission.log.txt:10540:00 00 00 00 00 01 00 ff 95 03 00 08 d4 65 01 00 00 00 00 00 22 00 00 00 00 00 00 01 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_Neuer_Ordner_Unknown.log2.txt:2529:10 00 00 01 00 ff 00 00 00 10 00 00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_Neuer_Ordner_Unknown.log2.txt:36022:00 00 ff 00 00 00 00 00 19 31 46 12 03 00 00 00 00 00 00 00 00 01 00 ff 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_Neuer_Ordner_Unknown.log2.txt:36058:00 00 00 ff 00 00 00 00 00 19 31 46 12 03 00 00 00 00 00 00 00 00 01 00 ff 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_Neuer_Ordner_Unknown.log2.txt:36074:00 00 89 01 00 00 00 00 00 ff 00 00 00 00 00 19 31 46 12 03 00 00 00 00 00 00 00 00 01 00 ff 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_Neuer_Ordner_Unknown.log2.txt:36091:12 03 00 00 00 00 00 00 00 00 01 00 ff 00 00 00 00 00 00 00 00 00 00 00 00 22 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_Neuer_Ordner_Unknown.log2.txt:36108:00 00 19 31 46 12 03 00 00 00 00 00 00 00 00 01 00 ff 00 00 00 00 00 00 00 00 00 00 00 00 22 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_Neuer_Ordner_Unknown.log2.txt:36352:00 00 00 00 00 00 00 00 01 00 ff 00 00 00 00 00 00 00 00 00 00 00 00 22 00 00 00 00 00 00 01 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_Neuer_Ordner_Unknown.log2.txt:36685:00 00 01 00 ff 00 00 00 00 00 00 00 00 00 00 00 00 22 00 00 00 00 00 00 01 00 0b 00 02 0e 05 9d
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_Neuer_Ordner_Unknown.log2.txt:36705:00 00 00 00 00 00 01 00 ff 00 00 00 00 00 00 00 00 00 00 00 00 22 00 00 00 00 00 00 01 00 0b 00
```

### Selector ff continuation prefix 00 01 04 ff

Signature: `00 01 04 ff` (spaced or compact hex)

Matches: 5

```text
mxopackets1_crashover2k_009_session_complete.log.txt:14257:02 03 02 00 01 04 ff 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector4.log.txt:2835:02 03 02 00 01 04 ff 00 00
mxopackets1_crashover2k__create_char_devoted_make_first_mission.log.txt:47501:02 03 02 00 01 04 ff 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:9078:02 03 02 00 01 04 ff 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:51582:02 03 02 00 01 04 ff 00 00
```

### Selector ff continuation prefix 00 01 10 ff

Signature: `00 01 10 ff` (spaced or compact hex)

Matches: 293

```text
mxopackets1_crashover2k_afterwhoruneo_proxy_003_login_session.log.txt:2997:00 3a 0a 00 40 7e 00 00 ff 8f 00 00 00 00 56 06 00 28 09 00 00 00 00 00 00 00 00 01 10 ff 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_login_session.log.txt:3024:00 00 3a 0a 00 40 7e 00 00 ff 8f 00 00 00 00 56 06 00 28 09 00 00 00 00 00 00 00 00 01 10 ff 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_login_session.log.txt:3206:00 40 7e 00 00 ff ff 00 00 00 00 56 06 00 28 7f 00 00 00 00 00 00 00 00 01 10 ff 63 01 00 90 51
mxopackets1_crashover2k_afterwhoruneo_proxy_003_login_session.log.txt:3217:00 00 00 00 56 06 00 28 09 00 00 00 00 00 00 00 00 01 10 ff 00 00 00 00 60 38 00 00 00 00 18 01
mxopackets1_crashover2k_afterwhoruneo_proxy_003_login_session.log.txt:3252:40 7e 00 00 ff 8f 00 00 00 00 56 06 00 28 09 00 00 00 00 00 00 00 00 01 10 ff 00 00 00 00 60 38
mxopackets1_crashover2k_afterwhoruneo_proxy_003_login_session.log.txt:3272:00 00 80 08 00 40 7e 00 00 ff 8f 00 00 00 00 56 06 00 28 09 00 00 00 00 00 00 00 00 01 10 ff 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_login_session.log.txt:3291:7e 00 00 ff 8f 00 00 00 00 56 06 00 28 09 00 00 00 00 00 00 00 00 01 10 ff 00 00 00 00 60 38 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_login_session.log.txt:3316:00 00 00 00 01 10 ff 00 00 00 00 60 38 00 00 00 00 18 01 12 0a 87 00 00 00 00 01 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_login_session.log.txt:3351:8b 08 00 40 7e 00 00 ff 8f 00 00 00 00 56 06 00 28 09 00 00 00 00 00 00 00 00 01 10 ff 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_login_session.log.txt:3376:7e 00 00 ff 8f 00 00 00 00 56 06 00 28 09 00 00 00 00 00 00 00 00 01 10 ff 00 00 00 00 60 38 00
```

### Selector ff continuation prefix 00 00 00 ff

Signature: `00 00 00 ff` (spaced or compact hex)

Matches: 6548

```text
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:2301:91 06 00 04 a1 01 15 c1 ec 0e 00 04 00 00 00 ff ff 58 cf 40 00 00 00 00 00 f0 7e 40 00 00 00 20
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:2767:40 00 00 01 9a 99 19 3e 00 00 00 00 ff 00 00 00 00 ba 13 63 00 00 87 43 00 00 a7 2e 13 06 00 00
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:2796:66 66 e6 3e 00 00 00 00 ff 00 00 00 00 65 14 63 00 00 7a 44 02 00 ee 7e 1d 06 00 00 00 60 62 0d
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:2798:00 00 00 00 ff 1f 00 00 00 00 30 04 00 28 15 00 00 00 00 00 00 00 00 01 10 ff 00 00 00 00 00 00
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:2981:a2 8e 29 92 c0 74 52 00 38 00 40 00 00 01 9a 99 19 3e 00 00 00 00 ff 00 00 00 00 ba 13 63 00 00
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:3011:00 40 00 00 01 9a 99 19 3e 00 00 00 00 ff 00 00 00 00 ba 13 63 00 00 87 43 00 00 a7 2e 13 06 00
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:3077:03 9a 99 19 3f 00 00 00 00 ff 00 00 00 00 ba 13 63 00 00 87 43 00 00 73 13 19 06 00 00 00 c0 65
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:3086:00 27 00 40 00 00 00 10 05 00 05 d2 ff 00 00 00 10 00 00 00 00 00 00 ff 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:3088:00 00 01 00 00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:3141:c0 74 52 00 38 00 40 00 00 01 9a 99 19 3e 00 00 00 00 ff 00 00 00 00 ba 13 63 00 00 87 43 00 00
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

Matches: 1037

```text
mxopackets1_crashover2k_009_session_complete.log.txt:1529:02 03 02 00 02 80 80 80 80 80 10 11 00 00 00 01 00 0c 57 02 a8 cd ab 11 8e 41 73 73 61 73 73 69
mxopackets1_crashover2k_009_session_complete.log.txt:1536:00 81 d4 1e 00 00 02 09 17 00 00 01 00 0c 57 02 aa cd ab 13 8e 41 73 73 61 73 73 69 6e 20 42 6c
mxopackets1_crashover2k_009_session_complete.log.txt:1539:c0 01 83 01 03 a8 24 b6 03 b2 09 b6 03 07 08 00 00 81 d4 1e 00 00 02 09 15 00 00 01 00 0c 57 02
mxopackets1_crashover2k_009_session_complete.log.txt:1581:f3 6b dc 3e 11 00 00 01 00 0c 57 02 ac cd ab 13 8e 41 73 73 61 73 73 69 6e 20 42 6c 61 64 65 00
mxopackets1_crashover2k_009_session_complete.log.txt:1584:03 a8 25 cf 03 b2 09 cf 03 07 08 00 00 81 d4 1e 00 00 02 09 0f 00 00 01 00 0c 57 02 ad cd ab 13
mxopackets1_crashover2k_009_session_complete.log.txt:1588:00 00 02 09 0d 00 00 01 00 0c 57 02 ae cd ab 13 8e 41 73 73 61 73 73 69 6e 20 42 6c 61 64 65 00
mxopackets1_crashover2k_009_session_complete.log.txt:1637:02 03 01 00 02 13 00 0f 00 02 0c 23 68 a5 a8 c5 f8 df ec 44 f1 5a 75 47 01 00 0c 57 02 af cd ab
mxopackets1_crashover2k_009_session_complete.log.txt:1641:1e 00 00 02 09 09 00 00 01 00 0c 57 02 b0 cd ab 13 8e 41 73 73 61 73 73 69 6e 20 42 6c 61 64 65
mxopackets1_crashover2k_009_session_complete.log.txt:1644:01 03 a8 24 b6 03 b2 09 b6 03 07 08 00 00 81 d4 1e 00 00 02 09 07 00 00 01 00 0c 57 02 b1 cd ab
mxopackets1_crashover2k_009_session_complete.log.txt:1648:1e 00 00 02 09 05 00 00 01 00 0c 57 02 b2 cd ab 13 8e 41 73 73 61 73 73 69 6e 20 42 6c 61 64 65
```

### Adjacent Object599 boundary prefix

Signature: `00 0c 57 02` (spaced or compact hex)

Matches: 1016

```text
mxopackets1_crashover2k_009_session_complete.log.txt:1529:02 03 02 00 02 80 80 80 80 80 10 11 00 00 00 01 00 0c 57 02 a8 cd ab 11 8e 41 73 73 61 73 73 69
mxopackets1_crashover2k_009_session_complete.log.txt:1536:00 81 d4 1e 00 00 02 09 17 00 00 01 00 0c 57 02 aa cd ab 13 8e 41 73 73 61 73 73 69 6e 20 42 6c
mxopackets1_crashover2k_009_session_complete.log.txt:1539:c0 01 83 01 03 a8 24 b6 03 b2 09 b6 03 07 08 00 00 81 d4 1e 00 00 02 09 15 00 00 01 00 0c 57 02
mxopackets1_crashover2k_009_session_complete.log.txt:1581:f3 6b dc 3e 11 00 00 01 00 0c 57 02 ac cd ab 13 8e 41 73 73 61 73 73 69 6e 20 42 6c 61 64 65 00
mxopackets1_crashover2k_009_session_complete.log.txt:1584:03 a8 25 cf 03 b2 09 cf 03 07 08 00 00 81 d4 1e 00 00 02 09 0f 00 00 01 00 0c 57 02 ad cd ab 13
mxopackets1_crashover2k_009_session_complete.log.txt:1588:00 00 02 09 0d 00 00 01 00 0c 57 02 ae cd ab 13 8e 41 73 73 61 73 73 69 6e 20 42 6c 61 64 65 00
mxopackets1_crashover2k_009_session_complete.log.txt:1637:02 03 01 00 02 13 00 0f 00 02 0c 23 68 a5 a8 c5 f8 df ec 44 f1 5a 75 47 01 00 0c 57 02 af cd ab
mxopackets1_crashover2k_009_session_complete.log.txt:1641:1e 00 00 02 09 09 00 00 01 00 0c 57 02 b0 cd ab 13 8e 41 73 73 61 73 73 69 6e 20 42 6c 61 64 65
mxopackets1_crashover2k_009_session_complete.log.txt:1644:01 03 a8 24 b6 03 b2 09 b6 03 07 08 00 00 81 d4 1e 00 00 02 09 07 00 00 01 00 0c 57 02 b1 cd ab
mxopackets1_crashover2k_009_session_complete.log.txt:1648:1e 00 00 02 09 05 00 00 01 00 0c 57 02 b2 cd ab 13 8e 41 73 73 61 73 73 69 6e 20 42 6c 61 64 65
```

### Header-like selector 50

Signature: `01 00 08 50` (spaced or compact hex)

Matches: 49

```text
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:1454:01 a8 35 00 00 b2 04 be 0a fd 07 00 00 81 35 ba 00 00 06 27 35 08 00 00 79 00 00 01 00 08 50 02
mxopackets1_crashover2k__create_char_devoted_make_first_mission.log.txt:22648:03 87 13 1c 03 58 04 00 00 fb ff 2c 00 00 01 00 08 50 02 f5 06 50 48 80 cd ab 01 01 00 00 00 00
mxopackets1_crashover2k__create_char_devoted_make_first_mission.log.txt:26937:00 00 00 00 80 a1 ca 40 00 00 00 00 f2 04 35 bf 00 00 00 00 f3 04 35 3f 25 00 00 01 00 08 50 02
mxopackets1_luizoe_recursion1.log.txt:981:09 b6 03 07 08 00 00 81 d4 1e 00 00 02 09 05 00 00 01 00 08 50 02 eb 1f f0 2e f3 cd ab 02 09 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_login_session.log.txt:1485:00 01 00 08 50 02 ea 01 30 39 0d cd ab 01 01 00 00 00 00 00 9a d0 40 00 00 00 00 00 90 80 40 00
mxopackets1_crashover2k_afterwhoruneo_proxy_unsorted_afterwhoruneologunsorted.log.txt:1202:82 53 1f b4 47 03 02 00 02 80 80 80 80 80 10 11 00 00 00 01 00 08 50 02 f3 0c 60 4f a4 cd ab 02
mxopackets1_crashover2k_afterwhoruneo_proxy_unsorted_afterwhoruneologunsorted.log.txt:14208:01 12 00 00 00 00 00 00 00 01 00 08 50 02 f3 0c 60 4f a4 cd ab 02 09 00 00 00 00 80 83 ef c0 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_Neuer_Ordner_Unknown.log2.txt:1146:04 b2 04 b0 04 07 08 00 00 81 c7 09 00 00 02 05 17 00 00 01 00 08 50 02 f3 0c 60 4f 25 cd ab 02
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_Neuer_Ordner_Unknown.log2.txt:1265:09 71 02 07 08 00 00 81 ec 1a 00 00 02 05 20 00 00 01 00 08 50 02 f3 0c 60 4f 25 cd ab 02 09 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_Neuer_Ordner_Unknown.log2.txt:34009:05 02 a8 16 84 03 b2 09 84 03 07 08 00 00 81 c7 09 00 00 02 05 18 00 00 01 00 08 50 02 f3 0c 60
```

### Header-like selector 67

Signature: `01 00 08 67` (spaced or compact hex)

Matches: 0

### Header-like selector 68

Signature: `01 00 08 68` (spaced or compact hex)

Matches: 0

### Header-like selector 69

Signature: `01 00 08 69` (spaced or compact hex)

Matches: 1

```text
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:82835:ff ff ff 23 00 00 01 00 08 69 09 38 08 90 55 bc cd ab 02 05 00 00 00 00 60 2a f6 40 00 00 00 00
```

### Header-like selector 76

Signature: `01 00 0c 76` (spaced or compact hex)

Matches: 0

### Header-like selector 90

Signature: `01 00 08 90` (spaced or compact hex)

Matches: 3

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_Neuer_Ordner_Unknown.log2.txt:40639:09 00 00 02 05 2a 00 00 01 00 08 90 05 fe 04 d0 0f c6 cd ab 02 11 00 00 00 00 e0 fa f0 c0 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_Neuer_Ordner_Unknown.log2.txt:45153:09 71 02 07 08 00 00 81 ec 1a 00 00 02 05 2b 00 00 01 00 08 90 05 fe 04 d0 0f c6 cd ab 02 11 00
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:13175:82 02 a8 24 91 05 b2 09 91 05 07 08 00 00 81 cf 1e 00 00 02 09 1d 00 00 01 00 08 90 05 bc 0f 40
```

### Header-like selector a3

Signature: `01 00 08 a3` (spaced or compact hex)

Matches: 3

```text
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:63101:02 03 01 00 02 13 00 13 00 02 0c 36 81 0d 20 47 00 80 fc c3 96 5c bd c6 01 00 08 a3 01 2b 01 30
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:74140:01 82 02 a8 07 51 01 b2 09 51 01 07 08 00 00 80 02 01 13 00 00 01 00 08 a3 01 6b 01 f0 3d c7 cd
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:82833:00 f3 04 35 3f 25 00 00 01 00 08 a3 01 a1 03 90 55 55 cd ab 03 88 00 00 00 00 ff ff 7f bf 00 00
```

### Header-like selector d0

Signature: `01 00 08 d0` (spaced or compact hex)

Matches: 87

```text
mxopackets1_crashover2k_afterwhoruneo_proxy_003_login_session.log.txt:1480:12 fb ff 56 06 00 28 90 01 10 89 3d 8d 0c 20 c1 1c 01 ee 03 00 00 79 00 29 00 00 01 00 08 d0 20
mxopackets1_crashover2k_afterwhoruneo_proxy_003_login_session.log.txt:1530:00 00 01 00 08 d0 20 f1 01 30 39 04 cd ab 08 fd 00 00 00 00 80 83 cf 40 00 00 00 00 00 f0 7e 40
mxopackets1_crashover2k_afterwhoruneo_proxy_003_login_session.log.txt:1577:00 30 4b 00 fa 07 00 00 1d 00 00 01 00 08 d0 20 b1 00 20 4e fe cd ab 07 fd 00 00 00 00 00 f8 d1
mxopackets1_crashover2k_afterwhoruneo_proxy_003_login_session.log.txt:1581:00 01 00 08 d0 20 ef 01 30 39 05 cd ab 08 fd 00 00 00 00 40 5a d5 40 00 00 00 00 00 f0 7e 40 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_login_session.log.txt:1628:22 00 00 01 81 00 00 00 50 00 1b 00 00 01 00 08 d0 20 ee 01 30 39 07 cd ab 08 fd 00 00 00 00 c0
mxopackets1_crashover2k_afterwhoruneo_proxy_003_login_session.log.txt:3120:02 03 01 00 08 d0 20 af 00 20 4e ff cd ab 08 fd 00 00 00 00 c0 a8 d4 40 00 00 00 00 00 f0 7e 40
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:1606:00 28 80 89 b6 1f ed 05 64 0d 6a 9e 00 00 07 81 00 00 00 73 00 00 01 00 08 d0 20 b0 00 20 4e 21
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:1662:00 01 00 08 d0 20 f1 01 30 39 1a cd ab 08 fd 00 00 00 00 80 83 cf 40 00 00 00 00 00 f0 7e 40 00
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:2191:1d d1 1e 00 00 01 81 00 00 00 0e 00 4d 00 00 01 00 08 d0 20 ef 01 30 39 1e cd ab 08 fd 00 00 00
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:2241:81 00 00 00 81 ff 49 00 00 01 00 08 d0 20 ee 01 30 39 1f cd ab 08 fd 00 00 00 00 c0 5c d5 40 00
```

## Movement And Object-State Leads

### Mode 06 movement selector 08

Signature: `00 06 08` (spaced or compact hex)

Matches: 37

```text
mxopackets1_crashover2k_009_session_complete.log.txt:8296:02 03 19 00 06 27 00 40 00 00 00 10 00 00 01 c7 80 80 80 80 80 01 02 18 00 06 08 15 0f c6 c5 f8
mxopackets1_crashover2k_009_session_complete.log.txt:13398:02 03 19 00 06 08 c5 be 0f c6 f8 df ec 44 66 d0 5c 47 80 80 80 80 80 01 03 15 00 02 0e 01 4c fc
mxopackets1_crashover2k_009_session_complete.log.txt:22375:17 00 02 0e 05 90 6a 62 c5 c5 f8 df ec 44 08 f6 45 47 16 00 06 08 37 33 99 c5 f8 df ec 44 6f 5e
mxopackets1_crashover2k_009_session_complete.log.txt:30145:02 03 1d 00 06 08 13 c1 08 c6 f8 df ec 44 69 5b 42 47 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00
mxopackets1_crashover2k_009_session_complete.log.txt:38750:02 03 15 00 06 08 35 4b a6 c5 f8 df ec 44 67 c1 6f 47 80 80 80 80 80 01 02 08 00 02 0e 05 0c 76
mxopackets1_crashover2k__create_char_devoted_make_first_mission.log.txt:32199:02 03 2c 00 06 08 2c 83 d1 45 00 40 30 c4 15 23 a4 46 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00
mxopackets1_crashover2k_011_idle_park_east.log.txt:1673:02 03 09 00 06 08 0e cc 7f c7 f0 bf 2d 44 86 92 38 c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00
mxopackets1_luizoe_recursion1.log.txt:10566:0e 00 a3 f0 ee 9c c5 f8 df ec 44 22 ca 44 47 0b 00 06 08 8d ad 75 c5 f8 df ec 44 24 46 23 47 80
mxopackets1_luizoe_recursion1.log.txt:28666:02 03 1a 00 06 08 85 19 b5 c5 f8 df ec 44 1a 8e 44 47 80 80 80 80 80 01 04 0e 00 02 0e 01 c1 3d
mxopackets1_luizoe_recursion1.log.txt:28719:02 03 02 00 02 80 80 80 50 44 00 aa 0d 1c 00 06 08 c9 81 15 c6 f8 df ec 44 2a 89 43 47 ff 00 00
```

### Mode 06 movement selector 0a

Signature: `00 06 0a` (spaced or compact hex)

Matches: 22

```text
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:210:6e 65 00 6b 00 00 06 0a 00 00 05 0a 00 00 04 0a 00 00 01 0a 00 00 00 0a 00 00 ff 00 03 c6 72 00
mxopackets1_crashover2k__create_char_devoted_make_first_mission.log.txt:4204:02 03 2d 00 02 0c 01 08 4f 2b 46 00 40 30 c4 49 1e 78 47 21 00 06 0a 00 f7 e1 80 46 00 40 30 c4
mxopackets1_crashover2k__create_char_devoted_make_first_mission.log.txt:4218:02 03 2d 00 02 0c f7 08 4f 2b 46 00 40 30 c4 49 1e 78 47 21 00 06 0a 00 f7 e1 80 46 00 40 30 c4
mxopackets1_crashover2k__create_char_devoted_make_first_mission.log.txt:7575:02 03 2b 00 06 0a 00 7c 9f 4e 46 00 40 30 c4 6a bd 71 47 80 80 80 80 80 80 01 39 21 00 00 21 00
mxopackets1_crashover2k__create_char_devoted_make_first_mission.log.txt:13684:00 00 22 00 00 00 00 00 2b 00 06 0a 00 ce a8 4e 46 00 40 30 c4 3e bf 71 47 80 80 80 80 80 80 01
mxopackets1_crashover2k__create_char_devoted_make_first_mission.log.txt:13831:82 ad 90 6a 47 03 2f 00 06 0a 01 a0 eb 0b 46 00 40 30 c4 e9 f2 6a 47 ff 00 00 00 10 00 00 00 00
mxopackets1_crashover2k__create_char_devoted_make_first_mission.log.txt:35066:02 03 29 00 06 0a 00 c3 ab 97 45 00 40 30 c4 f3 b5 98 46 80 80 80 80 80 80 01 47 21 00 00 27 00
mxopackets1_luizoe_recursion1.log.txt:12159:02 03 1a 00 06 0a 01 74 47 a8 c5 f8 df ec 44 46 f6 43 47 ff 00 00 00 10 00 00 00 00 00 01 ff 00
mxopackets1_luizoe_recursion1.log.txt:22550:a4 41 30 d0 85 42 00 00 00 00 80 a2 a4 c1 00 00 04 00 18 34 00 00 a2 00 06 0a 12 08 02 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_unsorted_afterwhoruneologunsorted.log.txt:226:00 00 06 0a 00 00 05 0a 00 00 04 0a 00 00 01 0a 00 00 00 0a 00 00 ff 09 00 2f d8 01 00 65 6e 74
```

### Mode 06 movement selector 0c

Signature: `00 06 0c` (spaced or compact hex)

Matches: 1367

```text
mxopackets1_crashover2k_009_session_complete.log.txt:2231:02 03 15 00 02 0e 05 8b 20 cd 0d c6 f8 df ec 44 4d c0 67 47 0f 00 06 0c 48 df 89 a1 c5 f8 df ec
mxopackets1_crashover2k_009_session_complete.log.txt:2306:80 80 80 80 01 12 08 00 00 15 00 06 0c aa 20 cd 0d c6 f8 df ec 44 4d c0 67 47 80 80 80 80 80 80
mxopackets1_crashover2k_009_session_complete.log.txt:2307:01 12 08 00 00 13 00 06 0c e1 32 08 08 c6 f8 df ec 44 6a 10 5d 47 80 80 80 80 80 80 01 12 08 00
mxopackets1_crashover2k_009_session_complete.log.txt:2437:02 03 19 00 06 0c 23 43 91 0a c6 f8 df ec 44 37 b6 5c 47 ff 00 00 00 10 00 00 00 00 00 01 ff 00
mxopackets1_crashover2k_009_session_complete.log.txt:2442:00 00 22 00 00 00 00 00 15 00 06 0c 91 20 cd 0d c6 f8 df ec 44 4d c0 67 47 ff 00 00 00 10 00 00
mxopackets1_crashover2k_009_session_complete.log.txt:2469:02 03 19 00 06 0c 2d 43 91 0a c6 f8 df ec 44 37 b6 5c 47 ff 00 00 00 10 00 00 00 00 00 01 ff 00
mxopackets1_crashover2k_009_session_complete.log.txt:2474:00 00 22 00 00 00 00 00 15 00 06 0c 87 20 cd 0d c6 f8 df ec 44 4d c0 67 47 ff 00 00 00 10 00 00
mxopackets1_crashover2k_009_session_complete.log.txt:2501:02 03 19 00 06 0c 37 43 91 0a c6 f8 df ec 44 37 b6 5c 47 ff 00 00 00 10 00 00 00 00 00 01 ff 00
mxopackets1_crashover2k_009_session_complete.log.txt:2538:02 03 19 00 06 0c 41 43 91 0a c6 f8 df ec 44 37 b6 5c 47 ff 00 00 00 10 00 00 00 00 00 01 ff 00
mxopackets1_crashover2k_009_session_complete.log.txt:2575:00 00 00 22 00 00 00 00 00 15 00 06 0c 6d 20 cd 0d c6 f8 df ec 44 4d c0 67 47 ff 00 00 00 10 00
```

### Mode 06 movement selector 0e

Signature: `00 06 0e` (spaced or compact hex)

Matches: 1859

```text
mxopackets1_crashover2k_009_session_complete.log.txt:2195:02 03 0f 00 06 0e 00 3b df 89 a1 c5 f8 df ec 44 3c 24 76 47 80 80 80 80 c0 4c 0a 00 28 01 01 0b
mxopackets1_crashover2k_009_session_complete.log.txt:2209:02 03 0f 00 06 0e 00 45 df 89 a1 c5 f8 df ec 44 3c 24 76 47 ff 00 00 00 10 00 00 00 00 00 01 ff
mxopackets1_crashover2k_009_session_complete.log.txt:2305:82 31 1e fc 45 03 02 00 02 80 04 1e 19 00 06 0e 05 05 43 91 0a c6 f8 df ec 44 37 b6 5c 47 80 80
mxopackets1_crashover2k_009_session_complete.log.txt:2308:00 0b 00 06 0e 04 a0 7e 52 14 c6 f8 df ec 44 71 cc 65 47 80 80 80 80 80 80 01 12 08 00 00 03 00
mxopackets1_crashover2k_009_session_complete.log.txt:2373:82 31 27 fc 45 03 19 00 06 0e 04 0f 43 91 0a c6 f8 df ec 44 37 b6 5c 47 ff 00 00 00 10 00 00 00
mxopackets1_crashover2k_009_session_complete.log.txt:2378:08 00 00 00 00 00 00 22 00 00 00 00 00 15 00 06 0e 05 a5 20 cd 0d c6 f8 df ec 44 4d c0 67 47 80
mxopackets1_crashover2k_009_session_complete.log.txt:2379:80 80 80 80 80 01 07 08 00 00 13 00 06 0e 04 eb 32 08 08 c6 f8 df ec 44 6a 10 5d 47 ff 00 00 00
mxopackets1_crashover2k_009_session_complete.log.txt:2405:02 03 19 00 06 0e 04 19 43 91 0a c6 f8 df ec 44 37 b6 5c 47 ff 00 00 00 10 00 00 00 00 00 01 ff
mxopackets1_crashover2k_009_session_complete.log.txt:2410:00 00 00 22 00 00 00 00 00 15 00 06 0e 05 9b 20 cd 0d c6 f8 df ec 44 4d c0 67 47 ff 00 00 00 10
mxopackets1_crashover2k_009_session_complete.log.txt:2506:00 00 22 00 00 00 00 00 15 00 06 0e 00 7d 20 cd 0d c6 f8 df ec 44 4d c0 67 47 ff 00 00 00 10 00
```

### Repeated mode 06 suffix lead

Signature: `ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00` (spaced or compact hex)

Matches: 993

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector4.log.txt:1803:c7 00 80 fc c3 b3 9a 85 c7 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector4.log.txt:2031:00 80 fc c3 30 c7 71 c7 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector4.log.txt:2037:ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector4.log.txt:2061:be 63 84 c7 00 80 fc c3 e6 81 6e c7 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector4.log.txt:2081:00 80 fc c3 30 c7 71 c7 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector4.log.txt:2125:00 80 fc c3 68 8f 71 c7 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector4.log.txt:2534:c7 00 80 fc c3 3e 78 7d c7 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector4.log.txt:2551:80 c7 00 80 fc c3 3e 78 7d c7 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector4.log.txt:2677:eb 80 c7 00 80 fc c3 7d 64 7b c7 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector4.log.txt:2844:3c 8b 71 c7 00 80 fc c3 0b 92 72 c7 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00
```

### Movement-state ff 01 64 01 tail

Signature: `ff 01 64 01` (spaced or compact hex)

Matches: 299

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector4.log.txt:203:02 03 02 00 03 09 08 00 00 1c 7c c7 61 a4 7a c4 da 52 64 c7 d5 f1 6d 00 ff 01 64 01 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector4.log.txt:220:02 03 02 00 03 08 00 1c 7c c7 38 ad 6d c4 da b6 64 c7 52 f2 6d 00 ff 01 64 01 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector4.log.txt:236:02 03 02 00 03 08 00 1c 7c c7 09 24 61 c4 da 1a 65 c7 cf f2 6d 00 ff 01 64 01 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector4.log.txt:252:02 03 02 00 03 08 00 1c 7c c7 d3 08 55 c4 da 7e 65 c7 4c f3 6d 00 ff 01 64 01 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector4.log.txt:268:02 03 02 00 03 08 00 1c 7c c7 98 5b 49 c4 da e2 65 c7 c9 f3 6d 00 ff 01 64 01 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector4.log.txt:285:02 03 02 00 03 08 00 1c 7c c7 bb b3 3c c4 a7 53 66 c7 75 f4 6d 00 ff 01 64 01 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector4.log.txt:327:82 eb 31 e1 45 03 02 00 03 08 00 1c 7c c7 07 b0 27 c4 da 1a 67 c7 5f f5 6d 00 ff 01 64 01 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector4.log.txt:347:02 03 02 00 03 08 00 1c 7c c7 e6 c7 1d c4 da 7e 67 c7 ec f5 6d 00 ff 01 64 01 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector4.log.txt:365:02 03 02 00 03 08 00 1c 7c c7 c8 3a 14 c4 a7 e3 67 c7 78 f6 6d 00 ff 01 64 01 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector4.log.txt:383:02 03 02 00 03 08 00 1c 7c c7 b4 51 0c c4 da 3a 68 c7 c7 f6 6d 00 ff 01 64 01 00 00 00 00 00 00
```

### Movement-state ff 01 4e 01 tail

Signature: `ff 01 4e 01` (spaced or compact hex)

Matches: 290

```text
mxopackets1_crashover2k_009_session_complete.log.txt:30587:02 03 02 00 03 09 08 00 70 87 08 c6 e4 c8 1d 45 3c b5 4f 47 a4 0e 7f 00 ff 01 4e 01 00 00 00 00
mxopackets1_crashover2k_009_session_complete.log.txt:30611:02 03 02 00 03 08 9f 7d 08 c6 bb d3 43 45 35 19 50 47 21 0f 7f 00 ff 01 4e 01 00 00 00 00 00 00
mxopackets1_crashover2k_009_session_complete.log.txt:30641:02 03 02 00 03 08 ce 73 08 c6 08 a2 68 45 2d 7d 50 47 9e 0f 7f 00 ff 01 4e 01 00 00 00 00 00 00
mxopackets1_crashover2k_009_session_complete.log.txt:30663:02 03 02 00 03 08 fd 69 08 c6 e7 19 86 45 25 e1 50 47 1b 10 7f 00 ff 01 4e 01 00 00 00 00 00 00
mxopackets1_crashover2k_009_session_complete.log.txt:30685:02 03 02 00 03 08 2c 60 08 c6 85 44 97 45 1d 45 51 47 98 10 7f 00 ff 01 4e 01 00 00 00 00 00 00
mxopackets1_crashover2k_009_session_complete.log.txt:30711:02 03 02 00 03 08 5b 56 08 c6 df d0 a7 45 16 a9 51 47 15 11 7f 00 ff 01 4e 01 00 00 00 00 00 00
mxopackets1_crashover2k_009_session_complete.log.txt:30739:02 03 02 00 03 08 8a 4c 08 c6 f5 be b7 45 0e 0d 52 47 92 11 7f 00 ff 01 4e 01 00 00 00 00 00 00
mxopackets1_crashover2k_009_session_complete.log.txt:30787:82 48 1f 02 46 03 02 00 03 08 e7 38 08 c6 53 c0 d5 45 ff d4 52 47 8c 12 7f 00 ff 01 4e 01 00 00
mxopackets1_crashover2k_009_session_complete.log.txt:30817:02 03 02 00 03 08 16 2f 08 c6 9c d3 e3 45 f7 38 53 47 09 13 7f 00 ff 01 4e 01 00 00 00 00 00 00
mxopackets1_crashover2k_009_session_complete.log.txt:30853:02 03 02 00 03 08 45 25 08 c6 a1 48 f1 45 ef 9c 53 47 86 13 7f 00 ff 01 4e 01 00 00 00 00 00 00
```

## Ability, Effect, And Combat-State Leads

### SERVER_PLAYER_ATTRIBUTE / ability load

Signature: `80 b2` (spaced or compact hex)

Matches: 250

```text
mxopackets1_crashover2k_011_idle_park_east.log.txt:694:01 00 5f 1e 16 80 bc 55 00 05 11 00 02 00 00 00 cc 00 0c 00 00 00 01 00 00 00 00 08 80 b2 11 00
mxopackets1_crashover2k_011_idle_park_east.log.txt:713:00 00 00 01 00 00 00 00 08 80 b2 36 04 00 00 08 02 16 80 bc 45 03 36 04 00 40 00 00 00 36 04 00
mxopackets1_crashover2k_011_idle_park_east.log.txt:759:04 01 00 00 00 00 00 00 00 00 08 80 b2 3a 04 00 00 08 02 16 80 bc 45 03 3a 04 00 53 00 00 00 3a
mxopackets1_crashover2k_011_idle_park_east.log.txt:805:01 00 00 07 04 01 00 00 00 01 00 00 00 00 08 80 b2 3b 04 00 00 08 02 16 80 bc 45 03 3b 04 00 36
mxopackets1_crashover2k_afterwhoruneo_proxy_unsorted_afterwhoruneologunsorted.log.txt:28:00 00 00 08 80 b2 4e 00 0a 00 08 02 08 80 b2 52 00 1e 00 08 02 08 80 b2 54 00 1e 00 08 02 08 80
mxopackets1_crashover2k_afterwhoruneo_proxy_unsorted_afterwhoruneologunsorted.log.txt:29:b2 4f 00 08 00 08 02 08 80 b2 51 00 1e 00 08 02 08 80 b2 11 00 32 00 08 02 16 80 bc 45 03 11 00
mxopackets1_crashover2k_afterwhoruneo_proxy_unsorted_afterwhoruneologunsorted.log.txt:35:00 00 34 00 00 00 83 02 c8 00 00 00 00 00 00 00 00 08 80 b2 36 04 00 00 08 02 16 80 bc 45 03 36
mxopackets1_crashover2k_afterwhoruneo_proxy_unsorted_afterwhoruneologunsorted.log.txt:42:01 00 00 00 00 00 00 00 00 08 80 b2 3a 04 00 00 08 02 16 80 bc 45 03 3a 04 00 53 00 00 00 3a 04
mxopackets1_crashover2k_afterwhoruneo_proxy_unsorted_afterwhoruneologunsorted.log.txt:78:02 04 01 00 21 1f 08 80 b2 35 04 00 00 08 02 16 80 bc 45 03 35 04 00 62 00 00 00 35 04 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_unsorted_afterwhoruneologunsorted.log.txt:82:00 00 08 80 b2 3b 04 00 00 08 02 16 80 bc 45 03 3b 04 00 36 01 00 00 3b 04 00 00 00 00 00 00 00
```

### SERVER_ABILITY_UNLOAD

Signature: `80 b3` (spaced or compact hex)

Matches: 254

```text
mxopackets1_crashover2k_009_session_complete.log.txt:370:00 00 00 00 00 00 00 00 04 80 b3 11 00 16 80 bc 45 03 11 00 00 02 00 00 00 11 00 32 00 00 00 01
mxopackets1_crashover2k_009_session_complete.log.txt:412:02 04 01 00 af 1f 16 80 bc 55 00 6e 00 00 34 00 00 00 83 02 c8 00 00 00 00 00 00 00 00 04 80 b3
mxopackets1_crashover2k_009_session_complete.log.txt:424:00 00 00 00 16 80 bc 45 00 10 00 00 53 00 00 00 19 04 01 00 00 00 01 00 00 00 00 04 80 b3 3a 04
mxopackets1_crashover2k_009_session_complete.log.txt:462:02 04 02 00 ce 1a 16 80 bc 45 00 14 00 00 62 00 00 00 06 04 01 00 00 00 01 00 00 00 00 04 80 b3
mxopackets1_crashover2k_009_session_complete.log.txt:471:00 00 00 00 00 00 00 00 04 80 b3 3b 04 16 80 bc 45 03 3b 04 00 36 01 00 00 3b 04 00 00 00 00 01
mxopackets1_crashover2k_009_session_complete.log.txt:481:00 00 00 00 16 80 bc 55 00 84 00 00 48 01 00 00 7c 04 0a 00 00 00 00 00 00 00 00 04 80 b3 3c 04
mxopackets1_crashover2k_009_session_complete.log.txt:1285:11 00 8c 00 00 00 00 00 00 cb 02 00 00 00 00 01 00 00 00 00 04 80 b3 11 00 16 80 bc 45 03 11 00
mxopackets1_crashover2k_009_session_complete.log.txt:1286:00 02 00 00 00 11 00 32 00 00 00 01 00 00 00 00 04 80 b3 35 04 16 80 bc 45 03 35 04 00 62 00 00
mxopackets1_crashover2k_009_session_complete.log.txt:1287:00 35 04 00 00 00 00 01 00 00 00 00 04 80 b3 36 04 16 80 bc 45 03 36 04 00 40 00 00 00 36 04 00
mxopackets1_crashover2k_009_session_complete.log.txt:1288:00 00 00 01 00 00 00 00 04 80 b3 3a 04 16 80 bc 45 03 3a 04 00 53 00 00 00 3a 04 00 00 00 00 01
```

### Object599 adrenaline booster effect id

Signature: `4c 0a 00 28` (spaced or compact hex)

Matches: 2160

```text
mxopackets1_crashover2k_009_session_complete.log.txt:2195:02 03 0f 00 06 0e 00 3b df 89 a1 c5 f8 df ec 44 3c 24 76 47 80 80 80 80 c0 4c 0a 00 28 01 01 0b
mxopackets1_crashover2k_009_session_complete.log.txt:2213:00 4c 0a 00 28 ff 01 00 00 00 00 00 00 00 00 00 00 00 5b 0b 00 58 37 08 00 00 1f 07 08 00 00 00
mxopackets1_crashover2k_009_session_complete.log.txt:2235:00 01 00 cf 03 ff 00 00 00 00 00 00 00 00 d4 1e 00 00 4c 0a 00 28 ff 01 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_009_session_complete.log.txt:2285:02 03 07 00 04 80 80 80 80 c0 4c 0a 00 28 01 01 00 00
mxopackets1_crashover2k_009_session_complete.log.txt:2708:02 03 05 00 04 80 80 80 80 c0 4c 0a 00 28 01 01 00 00
mxopackets1_crashover2k_009_session_complete.log.txt:2718:02 03 15 00 02 0e 05 68 20 cd 0d c6 f8 df ec 44 4d c0 67 47 03 00 04 80 80 80 80 c0 4c 0a 00 28
mxopackets1_crashover2k_009_session_complete.log.txt:2731:4c 0a 00 28 01 01 00 00
mxopackets1_crashover2k_009_session_complete.log.txt:2908:4c 0a 00 28 01 01 00 00
mxopackets1_crashover2k_009_session_complete.log.txt:2919:02 03 15 00 02 0e 04 0f 41 c0 0d c6 f8 df ec 44 43 a0 68 47 13 00 04 80 80 80 80 c0 4c 0a 00 28
mxopackets1_crashover2k_009_session_complete.log.txt:2941:02 03 19 00 04 80 80 80 80 c0 4c 0a 00 28 01 01 15 00 02 0c 23 41 c0 0d c6 f8 df ec 44 43 a0 68
```

### Object599 fast healing effect id

Signature: `b5 07 00 28` (spaced or compact hex)

Matches: 269

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector4.log.txt:845:80 80 c0 b5 07 00 28 01 03 1a 00 02 0c b3 58 05 69 c7 00 80 fc c3 56 55 5c c7 10 00 02 0c a0 4e
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector4.log.txt:1066:00 00 00 01 00 b0 04 ff 00 00 00 00 00 00 00 00 c7 09 00 00 b5 07 00 28 ff 02 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector4.log.txt:1929:00 80 fc c3 3a 7e 80 c7 06 00 04 80 80 80 80 c0 b5 07 00 28 01 02 05 00 02 0c f3 06 4e 84 c7 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector4.log.txt:1985:02 03 28 00 06 08 75 8e 86 c7 00 80 fc c3 5d ab 7d c7 80 80 80 80 c0 b5 07 00 28 01 01 19 00 02
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector4.log.txt:3995:04 80 80 80 80 c0 b5 07 00 28 01 02 07 00 02 0e 01 de 8d 79 83 c7 00 80 fc c3 40 78 6b c7 06 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector4.log.txt:3999:00 00 00 00 00 00 00 00 00 01 00 71 02 ff 00 00 00 00 00 00 00 00 ec 1a 00 00 b5 07 00 28 ff 02
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector4.log.txt:4018:b2 be 5d 73 c7 00 80 fc c3 56 57 81 c7 80 80 80 80 c0 b5 07 00 28 01 02 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector4.log.txt:4797:00 00 00 00 00 00 00 00 c7 09 00 00 b5 07 00 28 ff 01 00 00 00 00 00 00 00 00 00 00 00 84 09 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector4.log.txt:6673:02 03 23 00 04 80 80 80 80 c0 b5 07 00 28 01 01 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector4.log.txt:6681:02 03 03 00 04 80 80 80 80 c0 b5 07 00 28 01 02 00 00
```

### Object599 accelerated spawn effect id

Signature: `66 00 00 04` (spaced or compact hex)

Matches: 3

```text
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:2355:00 22 95 3a 6f 01 03 00 04 a1 01 0e c3 5f 01 00 4e 66 00 00 04 00 00 00 50 6e 7b d4 40 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:3592:02 03 03 00 04 80 80 80 80 c0 66 00 00 04 01 01 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:3759:82 50 5b 9f 47 03 03 00 04 80 80 80 80 c0 66 00 00 04 01 03 00 00
```

## Intake Next Steps

- Preserve the original files separately before editing or importing anything.
- Inspect matches with surrounding lines; embedded hits inside encrypted or unrelated bodies are leads only.
- Record sender, provenance, CR1/CR2 status, action context, character handle, coordinates, known object ids, and known item GoIDs.
- Import only text logs or text-like dumps that contain no passwords, session tokens, private keys, client binaries, or unrelated game assets.
