# Packet Capture Triage Report

- Source kind: `directory`
- Source input: `/Users/kyle/Developer/mxo-hd/research/packet-dumps/community-intake/20260607T013506Z-hdneo-mxopackets1-marketplace-vendor-selected/logs`
- Scanned files from: `/Users/kyle/Developer/mxo-hd/research/packet-dumps/community-intake/20260607T013506Z-hdneo-mxopackets1-marketplace-vendor-selected/logs`
- Generated UTC: `2026-06-07T01:35:18Z`
- Max sample lines per pattern: `10`

Use this report to decide whether received logs are worth importing into `research/packet-dumps/`. Matches are byte-string leads, not decoded packet claims.

## Vendor And Interaction Leads

### SERVER_VENDOR_OPEN candidate

Signature: `81 0d` (spaced or compact hex)

Matches: 0

### CLIENT_VENDOR_BUY candidate

Signature: `81 0e` (spaced or compact hex)

Matches: 0

### CLIENT_VENDOR_SELL candidate

Signature: `81 11` (spaced or compact hex)

Matches: 0

### CR2 static object interaction

Signature: `80 c8` (spaced or compact hex)

Matches: 2

```text
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:11735:02 04 01 00 34 01 08 80 c8 02 00 50 4b 03 00
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:12085:02 03 02 00 01 02 00 00 00 04 01 00 38 01 08 80 c8 81 07 50 4b 03 00
```

### CR1 static object interaction

Signature: `80 c3` (spaced or compact hex)

Matches: 26

```text
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:1235:44 02 80 c3 45 00 00
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:1244:44 02 80 c3 45 00 00
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:1254:bf 2d 44 02 80 c3 45 0f 00 02 08 88 55 e0 47 f0 bf 2d 44 a4 89 d4 45 00 00
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:1263:44 f2 13 cc 45 12 00 02 0c d4 b5 61 d8 47 f0 bf 2d 44 02 80 c3 45 00 00
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:1273:bf 2d 44 02 80 c3 45 00 00
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:1279:bf 2d 44 02 80 c3 45 08 00 02 08 d9 91 e4 47 00 40 2b 44 34 ae f0 45 00 00
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:1285:bf 2d 44 02 80 c3 45 00 00
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:1295:f0 bf 2d 44 02 80 c3 45 08 00 02 2a 0e 40 00 25 54 00 00 10 01 b1 99 e4 47 00 40 2b 44 a4 5c ef
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:1307:a2 b5 61 d8 47 f0 bf 2d 44 02 80 c3 45 08 00 02 2a 0e 40 00 25 54 00 00 10 01 a0 9d e4 47 00 40
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:1318:44 f2 13 cc 45 12 00 02 0c 98 b5 61 d8 47 f0 bf 2d 44 02 80 c3 45 08 00 02 0e 01 71 87 a1 e4 47
```

### SERVER object/state bundle

Signature: `80 bc` (spaced or compact hex)

Matches: 760

```text
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:1749:82 79 d7 25 49 04 01 01 b9 1e 16 80 bc 15 00 44 00 00 f7 03 00 00 08 02 00 00 00 00 01 00 00 00
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:1750:00 16 80 bc 15 00 45 00 00 f7 03 00 00 07 02 ec ff ff ff 01 00 00 00 00 16 80 bc 15 00 46 00 00
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:1751:f7 03 00 00 50 04 00 00 00 00 01 00 00 00 00 16 80 bc 15 00 47 00 00 f7 03 00 00 f4 03 00 00 00
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:1752:00 01 00 00 00 00 16 80 bc 15 00 48 00 00 f7 03 00 00 51 04 f6 ff ff ff 01 00 00 00 00 16 80 bc
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:1753:15 00 49 00 00 f7 03 00 00 52 04 0f 00 00 00 01 00 00 00 00 16 80 bc 56 00 4a 00 00 ef b2 00 00
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:1754:1b 04 02 00 00 00 01 00 00 00 00 16 80 bc 56 00 4b 00 00 ef b2 00 00 b4 00 20 00 00 00 01 00 00
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:1755:00 00 16 80 bc 56 00 4c 00 00 ef b2 00 00 09 04 20 00 00 00 01 00 00 00 00 16 80 bc 56 00 4d 00
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:1756:00 ef b2 00 00 77 04 01 00 00 00 01 00 00 00 00 16 80 bc 56 00 4e 00 00 ef b2 00 00 e6 03 02 00
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:1757:00 00 01 00 00 00 00 16 80 bc 56 00 4f 00 00 ef b2 00 00 b5 00 20 00 00 00 01 00 00 00 00 16 80
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:1758:bc 56 00 50 00 00 70 9a 00 00 23 04 02 00 00 00 01 00 00 00 00 16 80 bc 56 00 51 00 00 70 9a 00
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

Matches: 0

### Selector ff continuation prefix 00 01 04 ff

Signature: `00 01 04 ff` (spaced or compact hex)

Matches: 0

### Selector ff continuation prefix 00 01 10 ff

Signature: `00 01 10 ff` (spaced or compact hex)

Matches: 0

### Selector ff continuation prefix 00 00 00 ff

Signature: `00 00 00 ff` (spaced or compact hex)

Matches: 174

```text
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:680:02 03 01 00 08 a3 01 6b 01 f0 3d be cd ab 03 88 00 00 00 00 ff ff 7f 3f 00 00 00 00 f3 04 35 33
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:814:00 03 ff 00 00 00 00 00 00 00 00 e1 00 00 00 00 00 07 00 00 00 00 00 00 ff 00 00 00 00 00 00 00
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:816:00 00 00 00 00 00 00 00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00 00 00 00 56 03 00 08 37 08 00
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:956:00 00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:957:00 00 00 00 00 01 00 e1 00 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ff 00 00 00 00 00
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:970:00 00 00 00 00 e1 00 00 00 00 00 07 00 00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00 00 00 00 00
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:972:00 00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00 00 00 00 56 03 00 08 37 08 00 00 1f 07 08 00 00
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:987:00 01 00 e1 00 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:1005:00 00 00 00 e1 00 00 00 00 00 07 00 00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:1007:00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00 00 00 00 56 03 00 08 37 08 00 00 1f 07 08 00 00 00
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

Matches: 27

```text
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:2:02 03 01 00 0c 57 02 57 cd ab 12 8e 43 72 6f 73 73 62 6f 6e 65 73 20 42 75 6d 62 6f 6f 00 00 00
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:5:b2 09 e1 00 07 08 00 00 80 02 01 12 00 00 01 00 0c 57 02 61 cd ab 10 8e 43 72 6f 73 73 62 6f 6e
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:274:02 03 01 00 0c 57 02 55 cd ab 10 8e 43 72 6f 73 73 62 6f 6e 65 73 20 42 75 6d 62 6f 6f 00 00 00
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:277:e1 00 07 08 00 00 80 02 01 10 00 00 01 00 0c 57 02 6a cd ab 10 8e 43 72 6f 73 73 62 6f 6e 65 73
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:368:02 03 01 00 0c 57 02 6b cd ab 10 8e 43 72 6f 73 73 62 6f 6e 65 73 20 42 75 6d 62 6f 6f 00 00 00
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:371:e1 00 07 08 00 00 80 02 01 17 00 00 01 00 0c 57 02 6c cd ab 13 8e 43 72 6f 73 73 62 6f 6e 65 73
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:374:bb 40 c0 01 83 01 02 a8 07 51 01 b2 09 51 01 42 21 00 00 80 02 01 15 00 00 01 00 0c 57 02 5d cd
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:469:02 03 01 00 0c 57 02 58 cd ab 13 8e 43 72 6f 73 73 62 6f 6e 65 73 20 53 61 6c 74 00 00 00 00 00
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:472:01 b2 04 c2 01 42 21 00 00 80 02 01 08 00 00 01 00 0c 57 02 c5 cd ab 17 8e 43 72 6f 73 73 62 6f
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:2236:02 03 01 00 0c 57 02 e7 cd ab 13 8e 42 65 6c 6c 20 4d 61 64 6f 6e 6e 61 00 00 00 00 00 00 00 00
```

### Adjacent Object599 boundary prefix

Signature: `00 0c 57 02` (spaced or compact hex)

Matches: 27

```text
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:2:02 03 01 00 0c 57 02 57 cd ab 12 8e 43 72 6f 73 73 62 6f 6e 65 73 20 42 75 6d 62 6f 6f 00 00 00
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:5:b2 09 e1 00 07 08 00 00 80 02 01 12 00 00 01 00 0c 57 02 61 cd ab 10 8e 43 72 6f 73 73 62 6f 6e
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:274:02 03 01 00 0c 57 02 55 cd ab 10 8e 43 72 6f 73 73 62 6f 6e 65 73 20 42 75 6d 62 6f 6f 00 00 00
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:277:e1 00 07 08 00 00 80 02 01 10 00 00 01 00 0c 57 02 6a cd ab 10 8e 43 72 6f 73 73 62 6f 6e 65 73
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:368:02 03 01 00 0c 57 02 6b cd ab 10 8e 43 72 6f 73 73 62 6f 6e 65 73 20 42 75 6d 62 6f 6f 00 00 00
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:371:e1 00 07 08 00 00 80 02 01 17 00 00 01 00 0c 57 02 6c cd ab 13 8e 43 72 6f 73 73 62 6f 6e 65 73
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:374:bb 40 c0 01 83 01 02 a8 07 51 01 b2 09 51 01 42 21 00 00 80 02 01 15 00 00 01 00 0c 57 02 5d cd
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:469:02 03 01 00 0c 57 02 58 cd ab 13 8e 43 72 6f 73 73 62 6f 6e 65 73 20 53 61 6c 74 00 00 00 00 00
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:472:01 b2 04 c2 01 42 21 00 00 80 02 01 08 00 00 01 00 0c 57 02 c5 cd ab 17 8e 43 72 6f 73 73 62 6f
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:2236:02 03 01 00 0c 57 02 e7 cd ab 13 8e 42 65 6c 6c 20 4d 61 64 6f 6e 6e 61 00 00 00 00 00 00 00 00
```

### Header-like selector 50

Signature: `01 00 08 50` (spaced or compact hex)

Matches: 2

```text
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:2246:00 80 02 01 11 00 00 01 00 08 50 02 7d 02 b0 4a 62 cd ab 02 09 00 00 00 00 80 e7 ea 40 00 00 00
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:9118:00 b2 09 e1 00 07 08 00 00 80 02 01 0b 00 00 01 00 08 50 02 13 03 f0 3d c8 cd ab 02 09 00 00 00
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

Matches: 2

```text
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:680:02 03 01 00 08 a3 01 6b 01 f0 3d be cd ab 03 88 00 00 00 00 ff ff 7f 3f 00 00 00 00 f3 04 35 33
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:9074:00 81 fd 7f 3f 19 00 00 01 00 08 a3 01 6b 01 f0 3d be cd ab 03 88 00 00 00 00 ff ff 7f 3f 00 00
```

### Header-like selector d0

Signature: `01 00 08 d0` (spaced or compact hex)

Matches: 7

```text
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:9070:02 03 01 00 08 d0 20 21 03 f0 3d c7 cd ab 08 fd 00 00 00 00 17 e2 fc 40 00 00 00 00 00 b8 85 40
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:9076:ff ff ff 17 00 00 01 00 08 d0 20 23 03 f0 3d c6 cd ab 07 fd 00 00 00 00 90 0d fc 40 00 00 00 00
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:9149:47 f0 bf 2d 44 00 00 96 45 01 00 08 d0 20 94 0a 50 46 cb cd ab 08 fd 00 00 00 00 c0 de fa 40 00
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:9612:02 03 01 00 08 d0 20 22 03 f0 3d de cd ab 08 fd 00 00 00 00 60 1a fc 40 00 00 00 00 00 b8 85 40
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:10168:02 03 01 00 08 d0 20 d2 07 50 4b e0 cd ab 07 fd 00 00 00 00 30 28 fb 40 00 00 00 00 00 b8 85 40
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:10638:02 03 01 00 08 d0 20 b8 07 90 42 e2 cd ab 07 fd 00 00 00 00 b0 0c fb 40 00 00 00 00 00 b8 85 40
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:11706:02 03 01 00 08 d0 20 af 04 50 16 e4 cd ab 07 fd 00 00 00 00 50 db fa 40 00 00 00 00 00 b8 85 40
```

## Movement And Object-State Leads

### Mode 06 movement selector 08

Signature: `00 06 08` (spaced or compact hex)

Matches: 0

### Mode 06 movement selector 0a

Signature: `00 06 0a` (spaced or compact hex)

Matches: 6

```text
crashover2k_afterwhoruneo_proxy_second_pc_002_marketplace_submachine_guns.log.txt:12:54 4a 00 ba 02 00 00 00 00 00 28 fb c6 12 00 e8 03 00 00 00 07 f8 54 4a 00 06 0a 00 00 00 00 00
crashover2k_afterwhoruneo_proxy_second_pc_002_marketplace_submachine_guns.log.txt:14:0c 29 56 4a 00 ba 02 00 00 00 00 00 28 f5 c7 12 00 e8 03 00 00 00 ee 8d 57 4a 00 06 0a 00 00 00
crashover2k_afterwhoruneo_proxy_second_pc_002_marketplace_submachine_guns.log.txt:15:00 00 24 1f c8 12 00 88 13 00 00 00 d7 3a 58 4a 00 06 0a 00 00 00 00 00 24 e7 c8 12 00 88 13 00
crashover2k_afterwhoruneo_proxy_second_pc_002_marketplace_submachine_guns.log.txt:16:00 00 e4 49 5a 4a 00 bb 02 00 00 00 00 00 48 63 ca 12 00 28 23 00 00 00 fe d3 5c 4a 00 06 0a 00
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:3577:02 03 15 00 06 0a 00 0e 52 62 47 00 00 be 42 5e 37 5d 46 ff 00 00 00 10 00 00 00 00 00 01 ff 00
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:9652:02 03 18 00 02 08 40 6f d7 47 f0 bf 2d 44 1e 3a a7 45 04 00 06 0a 00 fc b5 d9 47 f0 bf 2d 44 fa
```

### Mode 06 movement selector 0c

Signature: `00 06 0c` (spaced or compact hex)

Matches: 24

```text
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:713:44 14 61 42 45 10 00 02 2a 00 40 00 00 00 10 01 ad 7f e5 47 00 40 2b 44 27 cb b9 45 0c 00 06 0c
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:1058:ba 45 10 00 06 0c c0 d9 9a e5 47 00 40 2b 44 1c 6f b4 45 ff 00 00 00 10 00 00 00 00 00 01 ff 00
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:1063:00 00 22 00 00 00 00 00 0c 00 06 0c c5 ce 6a e4 47 00 40 2b 44 4f d3 9e 45 ff 00 00 00 10 00 00
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:1081:02 03 10 00 06 0c ca d9 9a e5 47 00 40 2b 44 1c 6f b4 45 ff 00 00 00 10 00 00 00 00 00 01 ff 00
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:1103:02 03 10 00 06 0c d4 d9 9a e5 47 00 40 2b 44 1c 6f b4 45 ff 00 00 00 10 00 00 00 00 00 01 ff 00
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:1125:82 e8 d6 25 49 03 14 00 02 08 ea da d8 47 f0 bf 2d 44 82 5a d0 45 10 00 06 0c de d9 9a e5 47 00
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:1130:00 00 00 00 00 56 03 00 08 37 08 00 00 1f 07 08 00 00 00 00 00 00 22 00 00 00 00 00 0c 00 06 0c
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:2456:02 03 02 00 02 80 04 1e 13 00 06 0c d5 80 d5 5c 47 00 00 be 42 5b 53 59 46 80 80 80 80 80 80 01
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:2572:02 03 13 00 06 0c cb 80 d5 5c 47 00 00 be 42 5b 53 59 46 ff 00 00 00 10 00 00 00 00 00 01 ff 00
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:2593:02 03 13 00 06 0c c1 80 d5 5c 47 00 00 be 42 5b 53 59 46 ff 00 00 00 10 00 00 00 00 00 01 ff 00
```

### Mode 06 movement selector 0e

Signature: `00 06 0e` (spaced or compact hex)

Matches: 32

```text
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:799:44 d3 de bd 45 11 00 01 00 04 48 b0 e1 47 f0 bf 2d 44 ae b4 6b 45 10 00 06 0e 00 9f d9 9a e5 47
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:812:47 f0 bf 2d 44 3e 3b 6f 45 10 00 06 0e 00 9f d9 9a e5 47 00 40 2b 44 1c 6f b4 45 ff 00 00 00 10
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:944:44 ca 80 8e 45 0f 00 06 0e 05 6d d8 05 e0 47 f0 bf 2d 44 58 5b c9 45 80 80 80 80 80 80 01 07 08
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:953:82 d6 d6 25 49 03 12 00 02 0c f7 17 8e d8 47 f0 bf 2d 44 fc ca b6 45 0f 00 06 0e 05 63 d8 05 e0
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:968:db 8f 45 0f 00 06 0e 05 59 d8 05 e0 47 f0 bf 2d 44 58 5b c9 45 ff 00 00 00 10 00 00 00 00 00 01
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:983:02 03 11 00 01 00 04 8a cf e1 47 f0 bf 2d 44 8b cd 90 45 0f 00 06 0e 00 4f d8 05 e0 47 f0 bf 2d
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:1003:02 03 0f 00 06 0e 00 45 d8 05 e0 47 f0 bf 2d 44 58 5b c9 45 ff 00 00 00 10 00 00 00 00 00 01 ff
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:1018:66 91 45 10 00 06 0e 04 ac d9 9a e5 47 00 40 2b 44 1c 6f b4 45 80 80 80 80 80 80 01 07 08 00 00
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:1019:0f 00 06 0e 01 1a 38 09 e0 47 f0 bf 2d 44 30 a4 c9 45 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:1024:00 22 00 00 00 00 00 0c 00 06 0e 04 b1 ce 6a e4 47 00 40 2b 44 4f d3 9e 45 80 80 80 80 80 80 01
```

### Repeated mode 06 suffix lead

Signature: `ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00` (spaced or compact hex)

Matches: 35

```text
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:954:47 f0 bf 2d 44 58 5b c9 45 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:984:44 58 5b c9 45 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:1087:6a e4 47 00 40 2b 44 4f d3 9e 45 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:1109:6a e4 47 00 40 2b 44 4f d3 9e 45 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:1126:40 2b 44 1c 6f b4 45 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:1131:dc ce 6a e4 47 00 40 2b 44 4f d3 9e 45 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:1148:44 1c 6f b4 45 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:2610:53 59 46 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:2643:00 00 10 bb 80 d5 5c 47 00 00 be 42 5b 53 59 46 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:2670:00 00 10 80 d5 5c 47 00 00 be 42 5b 53 59 46 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00
```

### Movement-state ff 01 64 01 tail

Signature: `ff 01 64 01` (spaced or compact hex)

Matches: 1

```text
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:12412:02 03 02 00 03 08 7e ef d9 47 f0 bf 2d 44 07 30 c0 c5 d2 63 7f 28 ff 01 64 01 00 00 00 00 00 00
```

### Movement-state ff 01 4e 01 tail

Signature: `ff 01 4e 01` (spaced or compact hex)

Matches: 0

## Ability, Effect, And Combat-State Leads

### SERVER_PLAYER_ATTRIBUTE / ability load

Signature: `80 b2` (spaced or compact hex)

Matches: 12

```text
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:2211:00 02 00 00 00 cc 00 0c 00 00 00 01 00 00 00 00 08 80 b2 11 00 32 00 08 02 16 80 bc 45 03 11 00
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:2287:03 3d 04 00 43 00 00 00 3d 04 00 00 00 00 01 00 00 00 00 08 80 b2 3d 04 00 00 08 02 16 80 bc 45
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:2312:00 04 01 00 f1 1f 16 80 bc 55 00 2b 01 00 62 00 00 00 06 04 01 00 00 00 00 00 00 00 00 08 80 b2
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:2315:00 4f 01 00 00 3e 04 00 00 00 00 01 00 00 00 00 08 80 b2 3e 04 00 00 08 02 16 80 bc 45 03 3e 04
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:2321:80 b2 3f 04 00 00 08 02 16 80 bc 45 03 3f 04 00 64 01 00 00 3f 04 00 00 00 00 00 00 00 00 00 16
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:2352:00 d5 01 00 00 e3 03 05 00 00 00 01 00 00 00 00 08 80 b2 40 04 00 00 08 02 16 80 bc 45 03 40 04
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:9131:02 00 00 00 cc 00 0c 00 00 00 01 00 00 00 00 08 80 b2 11 00 32 00 08 02 16 80 bc 45 03 11 00 00
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:9169:04 16 80 bc 45 03 3d 04 00 43 00 00 00 3d 04 00 00 00 00 01 00 00 00 00 08 80 b2 3d 04 00 00 08
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:9194:00 08 80 b2 35 04 00 00 08 02 16 80 bc 45 03 35 04 00 62 00 00 00 35 04 00 00 00 00 00 00 00 00
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:9196:45 03 3e 04 00 4f 01 00 00 3e 04 00 00 00 00 01 00 00 00 00 08 80 b2 3e 04 00 00 08 02 16 80 bc
```

### SERVER_ABILITY_UNLOAD

Signature: `80 b3` (spaced or compact hex)

Matches: 24

```text
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:1866:00 00 00 00 04 80 b3 11 00 16 80 bc 45 03 11 00 00 02 00 00 00 11 00 32 00 00 00 01 00 00 00 00
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:1867:04 80 b3 35 04 16 80 bc 45 03 35 04 00 62 00 00 00 35 04 00 00 00 00 01 00 00 00 00 04 80 b3 3d
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:1868:04 16 80 bc 45 03 3d 04 00 43 00 00 00 3d 04 00 00 00 00 01 00 00 00 00 04 80 b3 3e 04 16 80 bc
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:1869:45 03 3e 04 00 4f 01 00 00 3e 04 00 00 00 00 01 00 00 00 00 04 80 b3 3f 04 16 80 bc 45 03 3f 04
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:1870:00 64 01 00 00 3f 04 00 00 00 00 01 00 00 00 00 04 80 b3 40 04 16 80 bc 45 03 40 04 00 d5 01 00
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:2209:00 01 00 00 00 00 16 80 bc 15 00 17 01 00 11 00 00 00 f3 03 46 00 00 00 01 00 00 00 00 04 80 b3
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:2258:00 00 00 00 00 00 04 80 b3 3d 04
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:2293:55 00 cd 00 00 62 00 00 00 06 04 01 00 00 00 01 00 00 00 00 04 80 b3 35 04 16 80 bc 45 03 35 04
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:2314:55 00 2c 01 00 62 00 00 00 15 04 01 00 00 00 00 00 00 00 00 04 80 b3 3e 04 16 80 bc 45 03 3e 04
crashover2k_afterwhoruneo_old_world_packets_actions_Vector_teleport_gotoarchivvendor_etc.log.txt:2319:00 1d 04 0f 00 00 00 00 00 00 00 00 04 80 b3 3f 04 16 80 bc 45 03 3f 04 00 64 01 00 00 3f 04 00
```

### Object599 adrenaline booster effect id

Signature: `4c 0a 00 28` (spaced or compact hex)

Matches: 0

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
- Import only text logs or text-like dumps that contain no passwords, session tokens, private keys, client binaries, or unrelated game assets.
