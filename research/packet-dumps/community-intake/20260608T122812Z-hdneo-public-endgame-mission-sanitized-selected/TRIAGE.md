# Packet Capture Triage Report

- Source kind: `directory`
- Source input: `/Users/kyle/Developer/mxo-hd/research/packet-dumps/community-intake/20260608T122812Z-hdneo-public-endgame-mission-sanitized-selected/logs`
- Scanned files from: `/Users/kyle/Developer/mxo-hd/research/packet-dumps/community-intake/20260608T122812Z-hdneo-public-endgame-mission-sanitized-selected/logs`
- Generated UTC: `2026-06-08T12:28:13Z`
- Max sample lines per pattern: `10`

Use this report to decide whether received logs are worth importing into `research/packet-dumps/`. Matches are byte-string leads, not decoded packet claims.

## Vendor And Interaction Leads

### SERVER_VENDOR_OPEN candidate

Signature: `81 0d` (spaced or compact hex)

Matches: 106

```text
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:13335:ff 00 00 00 00 fe 5d 01 00 00 00 18 01 12 00 00 00 00 00 00 01 00 23 00 02 80 80 10 81 0d 0d 00 
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:61212:00 00 16 80 bc 56 00 81 0d 00 c3 b2 00 00 b4 00 1f 00 00 00 01 00 00 00 00 16 80 bc 56 00 82 0d 
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:63112:02 03 01 00 02 13 00 13 00 02 0c 36 81 0d 20 47 00 80 fc c3 96 5c bd c6 01 00 08 a3 01 2b 01 30 
mxopackets1_medanon_niobe3dualduality.log.txt:6594:02 03 01 00 02 1a 00 0d 00 02 2e 00 40 00 00 00 10 00 6a 71 81 0d c8 f0 bf 2d 44 a0 fd d4 46 00 
mxopackets1_medanon_niobe3dualduality.log.txt:6601:02 03 01 00 02 1a 00 0d 00 02 2e 00 40 00 00 00 10 00 6a 71 81 0d c8 f0 bf 2d 44 a0 fd d4 46 00 
mxopackets1_medanon_niobe3dualduality.log.txt:6608:02 03 01 00 02 1a 00 0d 00 02 2e 00 40 00 00 00 10 00 6a 71 81 0d c8 f0 bf 2d 44 a0 fd d4 46 00 
mxopackets1_medanon_niobe3dualduality.log.txt:6615:02 03 01 00 02 1a 00 0d 00 02 2e 00 40 00 00 00 10 00 6a 71 81 0d c8 f0 bf 2d 44 a0 fd d4 46 00 
mxopackets1_medanon_niobe3dualduality.log.txt:6622:02 03 01 00 02 1a 00 0d 00 02 2e 00 40 00 00 00 10 00 6a 71 81 0d c8 f0 bf 2d 44 a0 fd d4 46 00 
mxopackets1_medanon_niobe3dualduality.log.txt:6629:02 03 01 00 02 1a 00 0d 00 02 2e 00 40 00 00 00 10 00 6a 71 81 0d c8 f0 bf 2d 44 a0 fd d4 46 00 
mxopackets1_medanon_niobe3dualduality.log.txt:6657:02 03 01 00 02 1a 00 0d 00 02 2e 00 40 00 00 00 10 00 6a 71 81 0d c8 f0 bf 2d 44 a0 fd d4 46 00 
```

### CLIENT_VENDOR_BUY candidate

Signature: `81 0e` (spaced or compact hex)

Matches: 156

```text
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:4622:ac 00 00 00 00 08 01 12 00 00 00 00 00 00 01 00 45 00 01 0e 05 0d 81 0e 84 46 00 80 f7 43 0b 6d 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:4643:46 00 80 f7 43 35 54 9a 44 45 00 01 0e 00 fb 81 0e 84 46 00 80 f7 43 0b 6d 0a 45 41 00 01 0a 00 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:4675:57 65 00 01 0c 35 f7 69 85 46 00 80 f7 43 43 72 9d 44 45 00 01 0e 00 f2 81 0e 84 46 00 80 f7 43 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:4726:46 00 80 f7 43 be b2 ad 44 45 00 01 2e 30 c0 00 98 d9 00 00 f2 81 0e 84 46 00 80 f7 43 0b 6d 0a 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:4964:45 45 00 01 0e 04 fa 81 0e 84 46 00 80 f7 43 0b 6d 0a 45 33 00 03 0a 03 82 45 85 46 00 80 f7 43 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:4995:68 ca 17 45 49 00 01 0e 04 58 eb 4d 7b 46 00 80 f7 43 7f 86 2f 45 45 00 01 0e 04 03 81 0e 84 46 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:5020:86 2f 45 45 00 01 0c 0c 81 0e 84 46 00 80 f7 43 0b 6d 0a 45 3b 00 01 02 00 33 00 03 08 dc ff 85 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:5049:00 01 0e 00 1f 81 0e 84 46 00 80 f7 43 0b 6d 0a 45 33 00 03 08 ed 54 86 46 00 80 f7 43 ec 81 49 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:11681:02 03 59 00 01 0e 05 fd 9e f3 85 46 00 80 f7 43 b7 cf 2a 45 45 00 01 0e 00 17 81 0e 84 46 00 80 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:11702:2a 45 53 00 02 80 80 10 b4 0d 45 00 01 0e 00 10 81 0e 84 46 00 80 f7 43 0b 6d 0a 45 21 00 02 80 
```

### CLIENT_VENDOR_SELL candidate

Signature: `81 11` (spaced or compact hex)

Matches: 4

```text
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:16668:00 00 00 01 00 03 00 01 0e 3e 5b de 21 83 46 00 80 f7 43 3f 81 11 45 00 00 
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:79077:16 80 bc 15 00 81 11 00 11 00 00 00 ab 00 0c 00 00 00 00 00 00 00 00 16 80 bc 15 00 82 11 00 11 
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:79196:00 00 16 80 bc 55 00 80 11 00 36 02 00 00 37 02 0a 00 00 00 00 00 00 00 00 16 80 bc 15 00 81 11 
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:82204:00 00 00 16 80 bc 15 00 81 11 00 11 00 00 00 ab 00 0c 00 00 00 01 00 00 00 00 16 80 bc 15 00 82 
```

### CR2 static object interaction

Signature: `80 c8` (spaced or compact hex)

Matches: 16

```text
mxopackets1_crashover2k__create_char_devoted_make_first_mission.log.txt:34568:02 04 01 00 58 01 08 80 c8 01 00 20 10 03 00 
mxopackets1_crashover2k__create_char_devoted_make_first_mission.log.txt:35849:02 04 01 00 5d 01 08 80 c8 e4 00 20 10 03 00 
mxopackets1_crashover2k__create_char_devoted_make_first_mission.log.txt:36717:02 04 01 00 60 01 08 80 c8 db 00 20 10 03 00 
mxopackets1_crashover2k__create_char_devoted_make_first_mission.log.txt:37283:02 04 01 00 66 01 08 80 c8 05 01 20 10 03 00 
mxopackets1_crashover2k__create_char_devoted_make_first_mission.log.txt:38409:02 04 01 00 6a 01 08 80 c8 04 01 20 10 00 00 
mxopackets1_crashover2k__create_char_devoted_make_first_mission.log.txt:39707:02 04 01 00 71 01 08 80 c8 1d 00 20 10 03 00 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:9404:82 0e 80 c8 47 03 83 00 01 08 c9 5d 7d 46 00 80 f7 43 b7 f9 5b 45 65 00 01 0e 01 70 d7 d5 82 46 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:9628:82 9e 80 c8 47 03 65 00 01 0a 00 a7 24 83 46 00 80 f7 43 96 74 ff 44 59 00 01 0e 01 4e 8c 13 85 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:19394:80 c8 ce 03 10 04 00 00 08 03 0d 00 01 0e 00 22 56 be 67 46 00 a0 87 44 8c b1 1a 45 0b 00 01 0a 
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:68679:00 72 dd 47 00 80 fb c3 00 bc c0 c7 12 80 c9 4d 07 90 5a 80 c8 d9 47 00 00 f9 c3 00 34 bf c7 02 
```

### CR1 static object interaction

Signature: `80 c3` (spaced or compact hex)

Matches: 111

```text
mxopackets1_crashover2k__create_char_devoted_make_first_mission.log.txt:3254:7c 47 0b 00 02 0e 01 80 c3 05 44 46 00 40 30 c4 cd fb 75 47 00 00 
mxopackets1_crashover2k__create_char_devoted_make_first_mission.log.txt:3273:c4 74 14 7a 47 0d 00 02 0c de 7f 11 5f 46 00 40 30 c4 12 d4 7c 47 0b 00 02 0e 01 80 c3 05 44 46 
mxopackets1_crashover2k__create_char_devoted_make_first_mission.log.txt:8615:c4 8a 2b 70 47 09 00 02 0e 04 2e 7d b4 0c 46 00 40 30 c4 80 c3 78 47 00 00 
mxopackets1_crashover2k__create_char_devoted_make_first_mission.log.txt:8628:c4 80 c3 78 47 06 00 02 0c 16 e7 ce f4 45 00 40 30 c4 57 c5 58 47 00 00 
mxopackets1_crashover2k__create_char_devoted_make_first_mission.log.txt:8642:40 30 c4 80 c3 78 47 00 00 
mxopackets1_crashover2k__create_char_devoted_make_first_mission.log.txt:8656:40 30 c4 80 c3 78 47 03 00 02 0e 05 dc 58 7b 57 46 00 40 30 c4 19 e1 71 47 00 00 
mxopackets1_crashover2k__create_char_devoted_make_first_mission.log.txt:8670:0c 46 00 40 30 c4 80 c3 78 47 03 00 02 0e 05 d2 58 7b 57 46 00 40 30 c4 19 e1 71 47 00 00 
mxopackets1_crashover2k__create_char_devoted_make_first_mission.log.txt:8684:46 00 40 30 c4 1a 52 72 47 09 00 02 0c 61 7d b4 0c 46 00 40 30 c4 80 c3 78 47 06 00 02 08 66 53 
mxopackets1_crashover2k__create_char_devoted_make_first_mission.log.txt:8700:46 00 40 30 c4 80 c3 78 47 03 00 02 0e 01 be 83 21 57 46 00 40 30 c4 5f e1 71 47 00 00 
mxopackets1_crashover2k__create_char_devoted_make_first_mission.log.txt:8714:02 0e 01 22 ab 25 51 46 00 40 30 c4 6e 65 72 47 09 00 02 0c 75 7d b4 0c 46 00 40 30 c4 80 c3 78 
```

### SERVER object/state bundle

Signature: `80 bc` (spaced or compact hex)

Matches: 14239

```text
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:39:08 00 08 02 08 80 b2 51 00 1e 00 08 02 08 80 b2 11 00 32 00 08 02 16 80 bc 45 03 11 00 00 02 00 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:40:00 00 11 00 32 00 00 00 00 00 00 00 00 16 80 bc 45 00 02 00 00 02 00 00 00 cc 00 0c 00 00 00 00 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:41:00 00 00 00 16 80 bc 45 00 03 00 00 0b 00 00 00 37 02 96 00 00 00 00 00 00 00 00 16 80 bc 45 00 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:42:04 00 00 2d 00 00 00 23 04 0a 00 00 00 00 00 00 00 00 16 80 bc 45 00 05 00 00 2d 00 00 00 20 04 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:43:0a 00 00 00 00 00 00 00 00 16 80 bc 45 00 06 00 00 2d 00 00 00 19 04 05 00 00 00 00 00 00 00 00 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:44:16 80 bc 45 00 07 00 00 2d 00 00 00 42 04 05 00 00 00 00 00 00 00 00 16 80 bc 45 00 08 00 00 34 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:45:00 00 00 83 02 c8 00 00 00 00 00 00 00 00 08 80 b2 36 04 00 00 08 02 16 80 bc 45 03 36 04 00 40 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:46:00 00 00 36 04 00 00 00 00 00 00 00 00 00 16 80 bc 45 00 09 00 00 40 00 00 00 7b 04 01 00 00 00 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:47:00 00 00 00 00 16 80 bc 45 00 0a 00 00 49 00 00 00 e3 03 05 00 00 00 00 00 00 00 00 16 80 bc 45 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:48:00 0b 00 00 49 00 00 00 7b 04 0e 00 00 00 00 00 00 00 00 16 80 bc 45 00 0c 00 00 4c 00 00 00 15 
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

Matches: 2

```text
mxopackets1_medanon_niobe3dualduality.log.txt:154332:64 00 00 00 00 00 00 00 00 41 00 ff 00 00 00 00 93 60 01 00 00 00 08 00 12 03 31 00 00 00 00 01 
mxopackets1_medanon_niobe3dualduality.log.txt:154354:ff 00 00 00 00 78 0a 00 28 64 00 00 00 00 00 00 00 00 41 00 ff 00 00 00 00 93 60 01 00 00 00 08 
```

### Selector ff continuation prefix 00 01 00 ff

Signature: `00 01 00 ff` (spaced or compact hex)

Matches: 673

```text
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:2780:00 b0 12 00 69 75 00 00 ff ff 00 00 00 00 79 0a 00 28 32 00 00 00 00 00 00 00 00 01 00 ff 00 00 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:2920:00 00 00 00 79 0a 00 28 32 00 00 00 00 00 00 00 00 01 00 ff 00 00 00 00 ef ac 00 00 00 00 08 01 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:2995:00 00 00 00 00 01 00 ff 00 00 00 00 ef ac 00 00 00 00 08 01 12 00 00 00 00 00 00 01 00 00 00 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:3024:00 00 b0 12 00 69 75 00 00 ff ff 00 00 00 00 79 0a 00 28 32 00 00 00 00 00 00 00 00 01 00 ff 00 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:3090:00 55 2c 00 00 ff 4c 00 00 00 00 e6 00 00 28 49 00 00 00 00 00 00 00 00 01 00 ff 00 00 00 00 f9 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:3096:00 00 00 00 00 01 00 ff 36 00 00 4e 65 bf 00 00 00 00 08 01 12 09 40 04 00 00 00 01 00 33 00 07 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:3129:e2 00 00 28 71 00 00 00 00 00 00 00 00 01 00 ff 36 00 00 4e 65 bf 00 00 00 00 08 01 12 09 40 04 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:3155:00 01 00 ff 00 00 00 00 ef ac 00 00 00 00 08 01 12 00 00 00 00 00 00 01 00 65 00 03 2a 37 c0 00 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:3160:00 01 00 ff 00 00 00 00 f9 34 01 00 00 00 08 01 12 00 00 00 00 00 00 01 00 00 00 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:3184:ff 00 00 00 00 79 0a 00 28 32 00 00 00 00 00 00 00 00 01 00 ff 00 00 00 00 ef ac 00 00 00 00 08 
```

### Selector ff continuation prefix 00 01 04 ff

Signature: `00 01 04 ff` (spaced or compact hex)

Matches: 5

```text
mxopackets1_crashover2k__create_char_devoted_make_first_mission.log.txt:47483:02 03 02 00 01 04 ff 00 00 
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:9089:02 03 02 00 01 04 ff 00 00 
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:51593:02 03 02 00 01 04 ff 00 00 
mxopackets1_medanon_niobe3dualduality.log.txt:42452:02 03 02 00 01 04 ff 00 00 
mxopackets1_medanon_niobe3dualduality.log.txt:63622:02 03 02 00 01 04 ff 00 00 
```

### Selector ff continuation prefix 00 01 10 ff

Signature: `00 01 10 ff` (spaced or compact hex)

Matches: 315

```text
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:2809:00 00 00 00 ff 1f 00 00 00 00 30 04 00 28 15 00 00 00 00 00 00 00 00 01 10 ff 00 00 00 00 00 00 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:3891:00 00 44 00 00 04 bb 00 00 00 00 00 00 00 00 01 10 ff 36 00 00 4e ee 5e 01 00 00 00 08 01 12 20 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:4021:00 01 10 ff 36 00 00 4e ee 5e 01 00 00 00 08 01 12 20 5b 01 00 00 00 01 00 00 00 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:4051:e8 00 00 00 00 44 00 00 04 bb 00 00 00 00 00 00 00 00 01 10 ff 36 00 00 4e ee 5e 01 00 00 00 08 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:4084:11 ba 00 00 ff e8 00 00 00 00 44 00 00 04 bb 00 00 00 00 00 00 00 00 01 10 ff 36 00 00 4e ee 5e 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:4940:00 00 04 bc 00 00 00 00 00 00 00 00 01 10 ff 36 00 00 4e ee 5e 01 00 00 00 08 01 12 20 5b 01 00 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:4968:00 00 ff e8 00 00 00 00 44 00 00 04 bc 00 00 00 00 00 00 00 00 01 10 ff 36 00 00 4e ee 5e 01 00 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:5000:04 bc 00 00 00 00 00 00 00 00 01 10 ff 36 00 00 4e ee 5e 01 00 00 00 08 01 12 20 5b 01 00 00 00 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:5024:ba 13 00 11 ba 00 00 ff e8 00 00 00 00 44 00 00 04 bc 00 00 00 00 00 00 00 00 01 10 ff 36 00 00 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:5053:e8 00 00 00 00 44 00 00 04 bc 00 00 00 00 00 00 00 00 01 10 ff 36 00 00 4e ee 5e 01 00 00 00 08 
```

### Selector ff continuation prefix 00 00 00 ff

Signature: `00 00 00 ff` (spaced or compact hex)

Matches: 4357

```text
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:2312:91 06 00 04 a1 01 15 c1 ec 0e 00 04 00 00 00 ff ff 58 cf 40 00 00 00 00 00 f0 7e 40 00 00 00 20 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:2778:40 00 00 01 9a 99 19 3e 00 00 00 00 ff 00 00 00 00 ba 13 63 00 00 87 43 00 00 a7 2e 13 06 00 00 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:2807:66 66 e6 3e 00 00 00 00 ff 00 00 00 00 65 14 63 00 00 7a 44 02 00 ee 7e 1d 06 00 00 00 60 62 0d 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:2809:00 00 00 00 ff 1f 00 00 00 00 30 04 00 28 15 00 00 00 00 00 00 00 00 01 10 ff 00 00 00 00 00 00 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:2992:a2 8e 29 92 c0 74 52 00 38 00 40 00 00 01 9a 99 19 3e 00 00 00 00 ff 00 00 00 00 ba 13 63 00 00 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:3022:00 40 00 00 01 9a 99 19 3e 00 00 00 00 ff 00 00 00 00 ba 13 63 00 00 87 43 00 00 a7 2e 13 06 00 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:3088:03 9a 99 19 3f 00 00 00 00 ff 00 00 00 00 ba 13 63 00 00 87 43 00 00 73 13 19 06 00 00 00 c0 65 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:3097:00 27 00 40 00 00 00 10 05 00 05 d2 ff 00 00 00 10 00 00 00 00 00 00 ff 00 00 00 00 00 00 00 00 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:3099:00 00 01 00 00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:3152:c0 74 52 00 38 00 40 00 00 01 9a 99 19 3e 00 00 00 00 ff 00 00 00 00 ba 13 63 00 00 87 43 00 00 
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

Matches: 749

```text
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:1358:01 00 c9 69 6c 0b 06 e9 00 1c 02 85 00 00 00 e0 ff 87 00 00 01 00 0c 57 02 95 cd ab 19 8e 4f 76 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:1407:06 77 1c 03 65 00 00 00 81 ff 83 00 00 01 00 02 06 00 81 00 01 01 01 00 0c 57 02 7c cd ab 17 8e 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:1511:01 00 0c 57 02 7b cd ab 17 8e 4f 76 65 72 72 69 64 65 20 46 75 6e 63 74 69 6f 6e 00 00 00 00 00 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:1559:0c 1c 01 65 00 00 00 34 00 77 00 00 01 00 02 06 00 75 00 01 01 01 00 0c 57 02 81 cd ab 17 8e 4f 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:1726:00 de 90 01 10 89 9c 99 12 06 3d 1c 02 85 00 00 00 83 ff 6f 00 00 01 00 0c 57 02 83 cd ab 18 8e 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:1776:01 01 01 00 0c 57 02 6a cd ab 15 8e 4f 76 65 72 72 69 64 65 20 46 75 6e 63 74 69 6f 6e 00 00 00 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:1824:00 28 92 01 01 00 ab 45 14 12 06 31 df 0c 1c 02 65 00 00 00 81 ff 6b 00 00 01 00 0c 57 02 94 cd 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:1874:01 01 00 0c 57 02 d4 cd ab 14 8e 53 61 74 69 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:1927:91 1e 8d 03 ee 03 00 00 81 ff 65 00 00 01 00 0c 57 02 cc cd ab 13 8e 53 61 74 69 00 00 00 00 00 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:1931:01 00 0c 57 02 bc cd ab 11 8e 42 6c 61 63 6b 77 6f 6f 64 20 47 6f 6f 66 00 00 00 00 00 00 00 00 
```

### Adjacent Object599 boundary prefix

Signature: `00 0c 57 02` (spaced or compact hex)

Matches: 733

```text
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:1358:01 00 c9 69 6c 0b 06 e9 00 1c 02 85 00 00 00 e0 ff 87 00 00 01 00 0c 57 02 95 cd ab 19 8e 4f 76 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:1407:06 77 1c 03 65 00 00 00 81 ff 83 00 00 01 00 02 06 00 81 00 01 01 01 00 0c 57 02 7c cd ab 17 8e 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:1511:01 00 0c 57 02 7b cd ab 17 8e 4f 76 65 72 72 69 64 65 20 46 75 6e 63 74 69 6f 6e 00 00 00 00 00 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:1559:0c 1c 01 65 00 00 00 34 00 77 00 00 01 00 02 06 00 75 00 01 01 01 00 0c 57 02 81 cd ab 17 8e 4f 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:1726:00 de 90 01 10 89 9c 99 12 06 3d 1c 02 85 00 00 00 83 ff 6f 00 00 01 00 0c 57 02 83 cd ab 18 8e 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:1776:01 01 01 00 0c 57 02 6a cd ab 15 8e 4f 76 65 72 72 69 64 65 20 46 75 6e 63 74 69 6f 6e 00 00 00 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:1824:00 28 92 01 01 00 ab 45 14 12 06 31 df 0c 1c 02 65 00 00 00 81 ff 6b 00 00 01 00 0c 57 02 94 cd 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:1874:01 01 00 0c 57 02 d4 cd ab 14 8e 53 61 74 69 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:1927:91 1e 8d 03 ee 03 00 00 81 ff 65 00 00 01 00 0c 57 02 cc cd ab 13 8e 53 61 74 69 00 00 00 00 00 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:1931:01 00 0c 57 02 bc cd ab 11 8e 42 6c 61 63 6b 77 6f 6f 64 20 47 6f 6f 66 00 00 00 00 00 00 00 00 
```

### Header-like selector 50

Signature: `01 00 08 50` (spaced or compact hex)

Matches: 49

```text
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:1984:07 08 00 00 81 c0 09 00 00 02 06 09 00 00 01 00 08 50 02 f9 18 60 02 86 cd ab 01 01 00 00 00 00 
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:3357:02 03 01 00 08 50 02 1f 0b 30 28 73 cd ab 01 01 00 00 00 00 60 30 ff c0 00 00 00 00 00 40 60 40 
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:10443:b2 09 33 04 07 08 00 00 81 d5 1e 00 00 02 0a 15 00 00 01 00 08 50 02 6c 27 30 1e 02 cd ab 02 09 
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:13237:05 07 08 00 00 81 cf 1e 00 00 02 09 15 00 00 01 00 08 50 02 ad 27 00 0c 2b cd ab 01 01 00 00 00 
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:15096:c0 01 83 01 02 a8 24 91 05 b2 09 91 05 07 08 00 00 81 cf 1e 00 00 02 09 0f 00 00 01 00 08 50 02 
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:17143:01 02 a8 26 dc 05 b2 09 dc 05 07 08 00 00 81 cf 1e 00 00 02 09 0d 00 00 01 00 08 50 02 12 00 40 
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:19775:bd 00 00 00 00 49 d2 7e 3f 07 00 00 01 00 08 50 02 19 00 30 0a 01 cd ab 01 01 00 00 00 00 a0 de 
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:21526:02 03 01 00 08 50 02 91 26 90 2d e0 cd ab 02 09 00 00 00 00 20 d1 f8 c0 00 00 00 00 00 c8 94 40 
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:24243:00 f3 04 35 3f 13 00 00 01 00 08 50 02 f9 03 a0 2d e9 cd ab 02 09 00 00 00 00 40 87 f0 40 00 00 
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:26474:01 b2 09 2c 01 07 08 00 00 80 02 01 17 00 00 01 00 08 50 02 7d 02 b0 4a ce cd ab 02 09 00 00 00 
```

### Header-like selector 67

Signature: `01 00 08 67` (spaced or compact hex)

Matches: 6

```text
mxopackets1_medanon_niobe3dualduality.log.txt:31478:00 00 00 00 00 00 00 01 00 08 67 95 46 00 80 14 36 cd ab 01 01 00 00 00 00 80 d2 e1 40 00 00 00 
mxopackets1_medanon_niobe3dualduality.log.txt:63363:02 03 01 00 08 67 95 46 00 80 14 36 cd ab 01 01 00 00 00 00 80 d2 e1 40 00 00 00 00 00 3c 91 c0 
mxopackets1_medanon_niobe3dualduality.log.txt:90264:82 40 cf 04 48 03 01 00 08 67 95 46 00 80 14 36 cd ab 01 01 00 00 00 00 80 d2 e1 40 00 00 00 00 
mxopackets1_medanon_niobe3dualduality.log.txt:141610:02 03 01 00 08 67 95 46 00 80 14 36 cd ab 01 01 00 00 00 00 80 d2 e1 40 00 00 00 00 00 3c 91 c0 
mxopackets1_medanon_niobe3dualduality.log.txt:144754:00 00 b7 e7 bc c0 00 00 00 00 c7 1d 3f bf 00 00 00 00 29 53 2a 3f 6b 00 00 01 00 08 67 95 46 00 
mxopackets1_medanon_niobe3dualduality.log.txt:149748:02 03 01 00 08 67 95 46 00 80 14 36 cd ab 01 01 00 00 00 00 80 d2 e1 40 00 00 00 00 00 3c 91 c0 
```

### Header-like selector 68

Signature: `01 00 08 68` (spaced or compact hex)

Matches: 5

```text
mxopackets1_medanon_niobe3dualduality.log.txt:30704:b6 c5 01 00 02 14 00 0f 00 02 0e 00 e4 50 40 38 47 00 20 8a c4 56 32 32 c6 01 00 08 68 95 44 00 
mxopackets1_medanon_niobe3dualduality.log.txt:62553:00 00 00 00 e4 7e 00 bf 00 00 00 00 65 6a 5d 3f 4b 00 00 01 00 08 68 95 44 00 80 14 3c cd ab 02 
mxopackets1_medanon_niobe3dualduality.log.txt:86003:01 00 08 68 95 44 00 80 14 3c cd ab 02 09 00 00 00 00 80 fe e2 40 00 00 00 00 00 40 91 c0 00 00 
mxopackets1_medanon_niobe3dualduality.log.txt:142010:00 0c 92 c0 00 00 00 00 9b a6 b6 c0 02 a1 0f 00 00 09 00 00 01 00 08 68 95 44 00 80 14 3c cd ab 
mxopackets1_medanon_niobe3dualduality.log.txt:144551:b6 00 00 02 0d 5f 00 00 01 00 08 68 95 44 00 80 14 3c cd ab 02 09 00 00 00 00 80 fe e2 40 00 00 
```

### Header-like selector 69

Signature: `01 00 08 69` (spaced or compact hex)

Matches: 11

```text
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:82846:ff ff ff 23 00 00 01 00 08 69 09 38 08 90 55 bc cd ab 02 05 00 00 00 00 60 2a f6 40 00 00 00 00 
mxopackets1_medanon_niobe3dualduality.log.txt:1315:02 03 01 00 08 69 09 50 0f d0 08 b8 cd ab 02 05 00 00 00 00 30 2d 01 c1 00 00 00 00 00 1a b2 40 
mxopackets1_medanon_niobe3dualduality.log.txt:5059:02 03 01 00 08 69 09 50 0f d0 08 b8 cd ab 02 05 00 00 00 00 30 2d 01 c1 00 00 00 00 00 1a b2 40 
mxopackets1_medanon_niobe3dualduality.log.txt:6043:02 03 01 00 08 69 09 50 0f d0 08 b8 cd ab 02 05 00 00 00 00 30 2d 01 c1 00 00 00 00 00 1a b2 40 
mxopackets1_medanon_niobe3dualduality.log.txt:6290:02 03 01 00 08 69 09 50 0f d0 08 b8 cd ab 02 05 00 00 00 00 30 2d 01 c1 00 00 00 00 00 1a b2 40 
mxopackets1_medanon_niobe3dualduality.log.txt:27517:09 bc 02 07 08 00 00 81 ca 1e 00 00 02 06 0f 00 00 01 00 08 69 09 50 0f d0 08 b8 cd ab 02 05 00 
mxopackets1_medanon_niobe3dualduality.log.txt:30394:01 a8 34 00 00 b2 04 8c 0a fd 07 00 00 81 22 b6 00 00 06 0d 35 08 00 00 3b 00 00 01 00 08 69 95 
mxopackets1_medanon_niobe3dualduality.log.txt:62651:fd 07 00 00 81 22 b6 00 00 06 0d 35 08 00 00 3d 00 00 01 00 08 69 95 43 00 80 14 3d cd ab 02 09 
mxopackets1_medanon_niobe3dualduality.log.txt:85774:d2 0f fd 07 00 00 81 0d ba 00 00 02 13 43 00 00 01 00 08 69 95 43 00 80 14 3d cd ab 02 09 00 00 
mxopackets1_medanon_niobe3dualduality.log.txt:141997:82 87 dd 05 48 03 01 00 08 69 95 43 00 80 14 3d cd ab 02 09 00 00 00 00 40 a7 e3 40 00 00 00 00 
```

### Header-like selector 76

Signature: `01 00 0c 76` (spaced or compact hex)

Matches: 0

### Header-like selector 90

Signature: `01 00 08 90` (spaced or compact hex)

Matches: 1

```text
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:13186:82 02 a8 24 91 05 b2 09 91 05 07 08 00 00 81 cf 1e 00 00 02 09 1d 00 00 01 00 08 90 05 bc 0f 40 
```

### Header-like selector a3

Signature: `01 00 08 a3` (spaced or compact hex)

Matches: 8

```text
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:63112:02 03 01 00 02 13 00 13 00 02 0c 36 81 0d 20 47 00 80 fc c3 96 5c bd c6 01 00 08 a3 01 2b 01 30 
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:74151:01 82 02 a8 07 51 01 b2 09 51 01 07 08 00 00 80 02 01 13 00 00 01 00 08 a3 01 6b 01 f0 3d c7 cd 
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:82844:00 f3 04 35 3f 25 00 00 01 00 08 a3 01 a1 03 90 55 55 cd ab 03 88 00 00 00 00 ff ff 7f bf 00 00 
mxopackets1_medanon_niobe3dualduality.log.txt:31670:01 00 08 a3 1b 47 00 80 14 3a cd ab 02 09 00 00 00 00 c0 a6 e1 40 00 00 00 00 00 40 91 c0 00 00 
mxopackets1_medanon_niobe3dualduality.log.txt:63366:3e 12 00 00 01 00 08 a3 1b 47 00 80 14 3a cd ab 02 09 00 00 00 00 c0 a6 e1 40 00 00 00 00 00 40 
mxopackets1_medanon_niobe3dualduality.log.txt:90388:00 00 00 20 bb 6c ba c0 00 00 00 00 bf c9 6f 3f 00 00 00 00 61 4e b3 3e 10 00 00 01 00 08 a3 1b 
mxopackets1_medanon_niobe3dualduality.log.txt:142007:00 00 c7 1d 3f bf 00 00 00 00 29 53 2a 3f 2b 00 00 01 00 08 a3 1b 47 00 80 14 3a cd ab 02 09 00 
mxopackets1_medanon_niobe3dualduality.log.txt:144712:cf bc c0 00 00 00 00 50 3a b7 be 00 00 00 00 b9 0b 6f 3f 69 00 00 01 00 08 a3 1b 47 00 80 14 3a 
```

### Header-like selector d0

Signature: `01 00 08 d0` (spaced or compact hex)

Matches: 93

```text
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:1973:02 03 01 00 08 d0 20 0e 19 60 02 bf cd ab 08 fd 00 00 00 00 70 7a 05 c1 00 00 00 00 00 c0 57 40 
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:24136:02 03 01 00 08 d0 20 06 00 c0 08 25 cd ab 08 fd 00 00 00 00 60 44 f0 40 00 00 00 00 00 40 58 c0 
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:24140:00 f3 04 35 3f 23 00 00 01 00 08 d0 20 46 05 a0 37 22 cd ab 08 fd 00 00 00 00 c0 2f f0 40 00 00 
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:24144:00 80 3f 00 00 00 00 2e bd 3b b3 21 00 00 01 00 08 d0 20 09 00 c0 08 2b cd ab 08 fd 00 00 00 00 
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:24185:82 cd 08 a0 47 03 01 00 08 d0 20 03 04 a0 2d 24 cd ab 07 fd 00 00 00 00 f0 e5 f0 40 00 00 00 00 
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:24192:80 02 01 1b 00 00 01 00 08 d0 20 07 00 c0 08 26 cd ab 07 fd 00 00 00 00 a0 4f ef 40 00 00 00 00 
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:24239:02 03 01 00 08 d0 20 05 04 a0 2d 28 cd ab 08 fd 00 00 00 00 70 61 f0 40 00 00 00 00 00 40 59 c0 
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:24248:01 07 08 00 00 80 02 01 0d 00 00 01 00 08 d0 20 08 00 c0 08 27 cd ab 07 fd 00 00 00 00 c0 e9 ee 
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:24298:02 03 01 00 08 d0 20 9f 02 e0 19 2c cd ab 07 fd 00 00 00 00 20 39 f0 40 00 00 00 00 00 80 58 c0 
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:24305:05 00 00 01 00 08 d0 20 04 04 a0 2d 21 cd ab 07 fd 00 00 00 00 60 8a f0 40 00 00 00 00 00 80 58 
```

## Movement And Object-State Leads

### Mode 06 movement selector 08

Signature: `00 06 08` (spaced or compact hex)

Matches: 16

```text
mxopackets1_crashover2k__create_char_devoted_make_first_mission.log.txt:32181:02 03 2c 00 06 08 2c 83 d1 45 00 40 30 c4 15 23 a4 46 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:33865:00 00 00 00 00 00 00 00 16 80 bc 15 00 06 08 00 11 00 00 00 0c 02 37 00 00 00 00 00 00 00 00 16 
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:33902:00 05 08 00 11 00 00 00 f1 03 33 00 00 00 01 00 00 00 00 16 80 bc 15 00 06 08 00 11 00 00 00 0c 
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:34561:0f 00 00 00 00 00 00 00 00 16 80 bc 11 00 06 08 00 00 00 00 00 cb 02 00 00 00 00 00 00 00 00 00 
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:36693:00 f7 03 00 00 52 04 0f 00 00 00 01 00 00 00 00 16 80 bc 11 00 06 08 00 00 00 00 00 cb 02 00 00 
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:40430:02 03 01 00 02 a7 00 11 00 06 08 26 26 cb 47 00 80 fc c3 8e 35 16 c8 ff 00 00 00 10 00 00 00 00 
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:44611:00 00 00 00 00 0f 00 06 08 b3 6f b1 47 00 00 be 42 d8 b7 a3 c7 ff 00 00 00 10 00 00 00 00 00 01 
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:57085:02 03 21 00 06 08 fa ce 7f 46 00 80 f7 43 d3 ed 90 c7 80 80 80 80 c0 4c 0a 00 28 01 01 1d 00 06 
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:67975:82 90 0a a1 47 03 14 00 06 08 17 aa ff 47 00 80 fc c3 1b 12 d3 c7 ff 00 00 00 10 00 00 00 00 00 
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:94086:02 03 0b 00 06 08 bd 5e 9b 47 f0 bf 2d 44 9c 9a 84 c7 80 80 80 80 c0 4c 0a 00 28 01 01 09 00 04 
```

### Mode 06 movement selector 0a

Signature: `00 06 0a` (spaced or compact hex)

Matches: 19

```text
mxopackets1_crashover2k__create_char_devoted_make_first_mission.log.txt:4186:02 03 2d 00 02 0c 01 08 4f 2b 46 00 40 30 c4 49 1e 78 47 21 00 06 0a 00 f7 e1 80 46 00 40 30 c4 
mxopackets1_crashover2k__create_char_devoted_make_first_mission.log.txt:4200:02 03 2d 00 02 0c f7 08 4f 2b 46 00 40 30 c4 49 1e 78 47 21 00 06 0a 00 f7 e1 80 46 00 40 30 c4 
mxopackets1_crashover2k__create_char_devoted_make_first_mission.log.txt:7557:02 03 2b 00 06 0a 00 7c 9f 4e 46 00 40 30 c4 6a bd 71 47 80 80 80 80 80 80 01 39 21 00 00 21 00 
mxopackets1_crashover2k__create_char_devoted_make_first_mission.log.txt:13666:00 00 22 00 00 00 00 00 2b 00 06 0a 00 ce a8 4e 46 00 40 30 c4 3e bf 71 47 80 80 80 80 80 80 01 
mxopackets1_crashover2k__create_char_devoted_make_first_mission.log.txt:13813:82 ad 90 6a 47 03 2f 00 06 0a 01 a0 eb 0b 46 00 40 30 c4 e9 f2 6a 47 ff 00 00 00 10 00 00 00 00 
mxopackets1_crashover2k__create_char_devoted_make_first_mission.log.txt:35048:02 03 29 00 06 0a 00 c3 ab 97 45 00 40 30 c4 f3 b5 98 46 80 80 80 80 80 80 01 47 21 00 00 27 00 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:221:6e 65 00 6b 00 00 06 0a 00 00 05 0a 00 00 04 0a 00 00 01 0a 00 00 00 0a 00 00 ff 00 03 c6 72 00 
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:27460:02 03 0b 00 06 0a 00 c9 4f 62 47 00 00 be 42 9d 1a 5d 46 80 80 80 80 80 80 01 37 21 00 00 00 00 
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:27470:02 03 0b 00 06 0a 00 c9 4f 62 47 00 00 be 42 9d 1a 5d 46 ff 00 00 00 10 00 00 00 00 00 01 ff 00 
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:43197:80 bc 56 00 06 0a 00 2c b3 00 00 b5 00 21 00 00 00 00 00 00 00 00 16 80 bc 56 00 dc 09 00 c3 b2 
```

### Mode 06 movement selector 0c

Signature: `00 06 0c` (spaced or compact hex)

Matches: 799

```text
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:3396:02 03 83 00 01 0c 3a 08 23 85 46 00 80 f7 43 d6 e2 e5 43 43 00 06 0c 0c eb e6 2d 46 00 00 be 42 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:3425:82 46 00 80 f7 43 7d e3 5f 45 43 00 06 0c 01 eb e6 2d 46 00 00 be 42 47 7a 6b 45 ff 00 00 00 10 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:3455:0c 07 00 28 f7 4d 00 01 0f 00 00 00 95 b7 60 82 46 00 80 f7 43 7d e3 5f 45 43 00 06 0c f7 eb e6 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:3493:15 08 23 85 46 00 80 f7 43 d6 e2 e5 43 53 00 02 80 80 90 d4 04 0c 72 0a 00 28 f8 43 00 06 0c ed 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:3524:02 03 83 00 01 0e 00 11 08 23 85 46 00 80 f7 43 d6 e2 e5 43 6b 00 02 80 80 80 08 50 43 00 06 0c 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:6409:00 00 00 ff 00 00 00 00 ac f3 00 00 00 00 08 01 12 00 00 00 00 00 00 01 00 04 00 06 0c 52 3a c0 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:7175:00 80 00 08 01 12 00 00 00 00 00 00 01 00 04 00 06 0c 4d 3a c0 2b 46 00 00 be 42 3f d6 87 45 80 
mxopackets1_crashover2k__create_char_devoted_make_first_mission.log.txt:5506:8e 7b 47 21 00 06 0c ff 99 e9 80 46 00 40 30 c4 43 19 6d 47 ff 00 00 00 10 00 00 00 00 00 01 ff 
mxopackets1_crashover2k__create_char_devoted_make_first_mission.log.txt:7637:02 03 2b 00 06 0c c7 7c 9f 4e 46 00 40 30 c4 6a bd 71 47 ff 00 00 00 10 00 00 00 00 00 01 ff 00 
mxopackets1_crashover2k__create_char_devoted_make_first_mission.log.txt:7659:02 03 2b 00 06 0c d1 7c 9f 4e 46 00 40 30 c4 6a bd 71 47 ff 00 00 00 10 00 00 00 00 00 01 ff 00 
```

### Mode 06 movement selector 0e

Signature: `00 06 0e` (spaced or compact hex)

Matches: 1057

```text
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:3291:43 c5 12 22 45 80 80 10 ea 12 43 00 06 0e 05 29 eb e6 2d 46 00 00 be 42 47 7a 6b 45 ff 00 00 00 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:3360:60 82 46 00 80 f7 43 7d e3 5f 45 43 00 06 0e 05 14 eb e6 2d 46 00 00 be 42 47 7a 6b 45 ff 00 00 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:3553:02 03 83 00 01 0e 03 1e d0 5f 85 46 00 80 f7 43 dc a6 f6 43 77 00 02 80 80 10 07 13 43 00 06 0e 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:3628:00 02 80 80 10 5c 04 53 00 02 80 80 10 fc 04 4d 00 01 02 05 43 00 06 0e 01 cd d1 0a 2d 46 00 00 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:7494:53 f0 80 46 00 80 f7 43 45 9e 07 45 39 00 02 80 80 10 0e 11 04 00 06 0e 00 52 3a c0 2b 46 00 00 
mxopackets1_crashover2k__create_char_devoted_make_first_mission.log.txt:5463:82 9a 4f 6a 47 03 2b 00 02 0e 05 b7 1f bb 68 46 00 40 30 c4 a7 8e 7b 47 21 00 06 0e 01 0f f7 e1 
mxopackets1_crashover2k__create_char_devoted_make_first_mission.log.txt:5485:02 03 2b 00 02 0e 05 ad 1f bb 68 46 00 40 30 c4 a7 8e 7b 47 21 00 06 0e 01 05 26 ea 80 46 00 40 
mxopackets1_crashover2k__create_char_devoted_make_first_mission.log.txt:7573:82 6e 62 6a 47 03 2f 00 02 08 4a b9 27 46 00 40 30 c4 4e f2 71 47 2b 00 06 0e 04 a9 7c 9f 4e 46 
mxopackets1_crashover2k__create_char_devoted_make_first_mission.log.txt:7589:02 03 2b 00 06 0e 04 b3 7c 9f 4e 46 00 40 30 c4 6a bd 71 47 ff 00 00 00 10 00 00 00 00 00 01 ff 
mxopackets1_crashover2k__create_char_devoted_make_first_mission.log.txt:7611:02 03 2f 00 02 2a 00 40 00 00 00 10 01 83 64 27 46 00 40 30 c4 f5 19 72 47 2b 00 06 0e 04 bd 7c 
```

### Repeated mode 06 suffix lead

Signature: `ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00` (spaced or compact hex)

Matches: 667

```text
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:10677:00 e0 a1 44 85 53 c2 47 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:10731:a1 44 85 53 c2 47 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:11194:47 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:17718:dd b7 c9 46 f0 bf 2d 44 f6 09 f0 46 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:18133:08 aa 46 f0 bf 2d 44 29 1e d4 46 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:18217:44 29 1e d4 46 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:18259:44 50 dc d3 46 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:18279:4f af d3 46 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:26645:27 69 5e 46 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:26675:82 4f 16 a0 47 03 0b 00 06 02 00 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 
```

### Movement-state ff 01 64 01 tail

Signature: `ff 01 64 01` (spaced or compact hex)

Matches: 1

```text
mxopackets1_medanon_niobe3dualduality.log.txt:101093:01 d9 3c c4 ff 01 64 01 00 00 00 00 00 00 43 44 a2 ce 08 42 71 6d 68 00 18 01 c0 00 00 03 0a d7 
```

### Movement-state ff 01 4e 01 tail

Signature: `ff 01 4e 01` (spaced or compact hex)

Matches: 71

```text
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:4701:84 46 00 80 f7 43 54 99 44 45 ff 01 4e 01 00 00 00 00 00 00 4f b0 a6 86 e8 52 00 72 c0 00 03 80 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:4782:00 03 06 04 d4 ff 01 4e 01 00 00 00 00 00 00 4f b0 a6 86 e8 52 00 72 c0 00 03 80 c0 00 00 03 00 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:4814:cb 2f 45 2f 00 03 0e 04 e3 90 aa 84 46 00 80 f7 43 54 99 44 45 ff 01 4e 01 00 00 00 00 00 00 4f 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:4844:00 2f 00 03 0e 04 ed 90 aa 84 46 00 80 f7 43 54 99 44 45 ff 01 4e 01 00 00 00 00 00 00 4f b0 a6 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:4872:f7 43 54 99 44 45 ff 01 4e 01 00 00 00 00 00 00 4f b0 a6 86 e8 52 00 72 c0 00 03 80 c0 00 00 03 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:4908:aa 84 46 00 80 f7 43 54 99 44 45 ff 01 4e 01 00 00 00 00 00 00 4f b0 a6 86 e8 52 00 72 c0 00 03 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:5055:45 ff 01 4e 01 00 00 00 00 00 00 4f b0 a6 86 e8 52 00 72 c0 00 03 80 c0 00 00 03 00 00 00 00 00 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:5122:20 5b 01 00 00 00 01 00 2f 00 03 02 04 ff 01 4e 01 00 00 00 00 00 00 4f b0 a6 86 e8 52 00 72 c0 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:5159:84 46 00 80 f7 43 54 99 44 45 ff 01 4e 01 00 00 00 00 00 00 4f b0 a6 86 e8 52 00 72 c0 00 03 80 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:5200:8a 46 00 80 f7 43 60 89 4b 45 2f 00 03 0e 00 07 90 aa 84 46 00 80 f7 43 54 99 44 45 ff 01 4e 01 
```

## Ability, Effect, And Combat-State Leads

### SERVER_PLAYER_ATTRIBUTE / ability load

Signature: `80 b2` (spaced or compact hex)

Matches: 160

```text
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:38:08 80 b2 4e 00 0a 00 08 02 08 80 b2 52 00 1e 00 08 02 08 80 b2 54 00 1e 00 08 02 08 80 b2 4f 00 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:39:08 00 08 02 08 80 b2 51 00 1e 00 08 02 08 80 b2 11 00 32 00 08 02 16 80 bc 45 03 11 00 00 02 00 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:45:00 00 00 83 02 c8 00 00 00 00 00 00 00 00 08 80 b2 36 04 00 00 08 02 16 80 bc 45 03 36 04 00 40 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:52:00 00 00 00 00 00 08 80 b2 3a 04 00 00 08 02 16 80 bc 45 03 3a 04 00 53 00 00 00 3a 04 00 00 00 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:56:00 00 08 80 b2 35 04 00 00 08 02 16 80 bc 45 03 35 04 00 62 00 00 00 35 04 00 00 00 00 00 00 00 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:87:00 00 08 80 b2 3b 04 00 00 08 02 16 80 bc 45 03 3b 04 00 36 01 00 00 3b 04 00 00 00 00 00 00 00 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:92:00 7c 04 0a 00 00 00 00 00 00 00 00 08 80 b2 3c 04 00 00 08 02 16 80 bc 45 03 3c 04 00 4e 01 00 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:94:00 00 00 16 80 bc 45 00 20 00 00 86 01 00 00 03 04 02 00 00 00 00 00 00 00 00 08 80 b2 37 04 00 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:383:00 ac 0b 16 80 bc 45 00 02 00 00 02 00 00 00 cc 00 0c 00 00 00 01 00 00 00 00 08 80 b2 11 00 32 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:426:00 7b 04 01 00 00 00 01 00 00 00 00 08 80 b2 36 04 00 00 08 02 16 80 bc 45 03 36 04 00 40 00 00 
```

### SERVER_ABILITY_UNLOAD

Signature: `80 b3` (spaced or compact hex)

Matches: 199

```text
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:382:00 00 00 00 04 80 b3 11 00 16 80 bc 45 03 11 00 00 02 00 00 00 11 00 32 00 00 00 01 00 00 00 00 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:424:00 00 16 80 bc 55 00 76 00 00 34 00 00 00 83 02 c8 00 00 00 00 00 00 00 00 04 80 b3 36 04 16 80 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:436:16 80 bc 45 00 10 00 00 53 00 00 00 19 04 01 00 00 00 01 00 00 00 00 04 80 b3 3a 04 16 80 bc 45 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:474:00 00 16 80 bc 45 00 14 00 00 62 00 00 00 06 04 01 00 00 00 01 00 00 00 00 04 80 b3 35 04 16 80 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:483:00 00 00 00 04 80 b3 3b 04 16 80 bc 45 03 3b 04 00 36 01 00 00 3b 04 00 00 00 00 01 00 00 00 00 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:524:00 04 80 b3 3c 04 16 80 bc 45 03 3c 04 00 4e 01 00 00 3c 04 00 00 00 00 01 00 00 00 00 16 80 bc 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:528:00 00 04 80 b3 37 04 16 80 bc 45 03 37 04 00 86 01 00 00 37 04 00 00 00 00 01 00 00 00 00 16 80 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:1029:bc 11 00 9a 00 00 00 00 00 00 cb 02 00 00 00 00 01 00 00 00 00 04 80 b3 11 00 16 80 bc 45 03 11 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:1030:00 00 02 00 00 00 11 00 32 00 00 00 01 00 00 00 00 04 80 b3 35 04 16 80 bc 45 03 35 04 00 62 00 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:1031:00 00 35 04 00 00 00 00 01 00 00 00 00 04 80 b3 36 04 16 80 bc 45 03 36 04 00 40 00 00 00 36 04 
```

### Object599 adrenaline booster effect id

Signature: `4c 0a 00 28` (spaced or compact hex)

Matches: 1321

```text
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:2038:00 00 00 00 00 00 00 00 00 10 00 00 22 95 0f 13 91 06 00 04 a1 01 27 c3 ec 0e 00 04 4c 0a 00 28 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:7024:00 00 00 00 00 00 01 00 23 00 02 80 80 80 0c 4c 0a 00 28 87 13 00 01 0a 82 0f ab 6c 46 00 a0 87 
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:7053:f3 00 00 00 00 08 01 12 00 00 00 00 00 00 01 00 23 00 02 80 80 90 81 13 0c 4c 0a 00 28 87 00 00 
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:1979:c3 3f 0a 00 58 4c 0a 00 28 00 00 00 1f fa 7a 06 c1 00 00 00 00 00 c0 57 40 00 00 00 ac f4 26 f0 
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:1982:00 00 00 00 00 00 10 00 00 22 d5 01 2d 9d 1b 00 c6 01 80 c3 3f 0a 00 58 4c 0a 00 28 00 00 80 b9 
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:2149:02 03 03 00 04 80 80 80 80 c0 4c 0a 00 28 01 01 00 00 
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:2448:02 03 05 00 06 0c ea 3c cb 32 c8 00 00 be 42 40 cb 80 c7 80 80 80 80 c0 4c 0a 00 28 01 01 00 00 
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:2462:4c 0a 00 28 ff 01 00 00 00 00 00 00 00 00 00 00 00 3e 0a 00 58 37 08 00 00 1f 07 08 00 00 00 00 
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:2482:00 00 00 ca 1e 00 00 4c 0a 00 28 ff 01 00 00 00 00 00 00 00 00 00 00 00 3e 0a 00 58 37 08 00 00 
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:2502:4c 0a 00 28 ff 01 00 00 00 00 00 00 00 00 00 00 00 3e 0a 00 58 37 08 00 00 1f 07 08 00 00 00 00 
```

### Object599 fast healing effect id

Signature: `b5 07 00 28` (spaced or compact hex)

Matches: 0

### Object599 accelerated spawn effect id

Signature: `66 00 00 04` (spaced or compact hex)

Matches: 3

```text
mxopackets1_crashover2k_proxy_unsorted_Packets1.log.txt:2366:00 22 95 3a 6f 01 03 00 04 a1 01 0e c3 5f 01 00 4e 66 00 00 04 00 00 00 50 6e 7b d4 40 00 00 00 
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:3603:02 03 03 00 04 80 80 80 80 c0 66 00 00 04 01 01 00 00 
mxopackets1_crashover2k_afterwhoruneo_proxy_023_teleports_Starting_sout_vauxton_until_end_dt_And_complete_richland.log.txt:3770:82 50 5b 9f 47 03 03 00 04 80 80 80 80 c0 66 00 00 04 01 03 00 00 
```

## Intake Next Steps

- Preserve the original files separately before editing or importing anything.
- Inspect matches with surrounding lines; embedded hits inside encrypted or unrelated bodies are leads only.
- Record sender, provenance, CR1/CR2 status, action context, character handle, coordinates, known object ids, and known item GoIDs.
- Import only reviewed text logs or text-like dumps that clear repository safety review; leave binaries and unrelated game assets out of Git.
