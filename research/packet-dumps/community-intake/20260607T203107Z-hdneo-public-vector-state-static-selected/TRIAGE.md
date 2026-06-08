# Packet Capture Triage Report

- Source kind: `directory`
- Source input: `/Users/kyle/Developer/mxo-hd/research/packet-dumps/community-intake/20260607T203107Z-hdneo-public-vector-state-static-selected/logs`
- Scanned files from: `/Users/kyle/Developer/mxo-hd/research/packet-dumps/community-intake/20260607T203107Z-hdneo-public-vector-state-static-selected/logs`
- Generated UTC: `2026-06-07T20:31:08Z`
- Max sample lines per pattern: `10`

Use this report to decide whether received logs are worth importing into `research/packet-dumps/`. Matches are byte-string leads, not decoded packet claims.

## Vendor And Interaction Leads

### SERVER_VENDOR_OPEN candidate

Signature: `81 0d` (spaced or compact hex)

Matches: 20

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector12.log.txt:26160:05 01 c2 03 00 80 80 e0 01 00 00 80 81 0d 03 fd 07 00 00 00 00 00 00 0b 00 02 0e 00 ef 94 45 cf
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector49_with_unknown_objects.log.txt:154:c8 ae b8 d2 cb 0b f7 47 ec af c5 aa 13 cf 34 39 1b 88 2f b4 32 81 0d 62 a6 f9 ce d8 51 6c b9 f3
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector49_with_unknown_objects.log.txt:730:c8 ae b8 d2 cb 0b f7 47 ec af c5 aa 13 cf 34 39 1b 88 2f b4 32 81 0d 62 a6 f9 ce d8 51 6c b9 f3
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector49_with_unknown_objects.log.txt:1340:c8 ae b8 d2 cb 0b f7 47 ec af c5 aa 13 cf 34 39 1b 88 2f b4 32 81 0d 62 a6 f9 ce d8 51 6c b9 f3
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector49_with_unknown_objects.log.txt:6553:c8 ae b8 d2 cb 0b f7 47 ec af c5 aa 13 cf 34 39 1b 88 2f b4 32 81 0d 62 a6 f9 ce d8 51 6c b9 f3
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_Neuer_Ordner_Kopie_von_Unknown.log.txt:11764:c8 ae b8 d2 cb 0b f7 47 ec af c5 aa 13 cf 34 39 1b 88 2f b4 32 81 0d 62 a6 f9 ce d8 51 6c b9 f3
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_Neuer_Ordner_Kopie_von_Unknown.log.txt:15802:0c d1 71 e9 84 c7 00 00 be 42 f5 22 55 c7 01 00 02 13 00 13 00 02 0c d1 81 0d 86 c7 00 00 be 42
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_Neuer_Ordner_Kopie_von_Unknown.log.txt:15836:20 8a c4 b1 75 6b c7 19 00 02 0e 01 bf 4d f7 84 c7 00 00 be 42 71 21 55 c7 13 00 02 0c bf 81 0d
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_teleport_unsorted_tels_allteleportetc001.log.txt:1152:de 81 0d 7c ad d9 43 00 00 00 00 80 fe e2 40 00 00 00 00 00 40 91 c0 00 00 00 00 00 b3 c0 c0 20
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector14_walking_to_courty.log.txt:412:27 0f 5d 70 44 ff 16 a6 10 94 bd 86 37 34 72 d1 06 80 cb 7c 07 1a 26 06 af ae d9 81 0d 53 7d 05
```

### CLIENT_VENDOR_BUY candidate

Signature: `81 0e` (spaced or compact hex)

Matches: 6

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:1745:02 03 02 00 02 80 84 2a 80 50 b9 00 28 0a 0e 00 05 01 c2 04 00 80 80 e0 01 00 00 80 81 0e 03 fd
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:4262:02 03 0b 00 06 0d 00 00 9f 41 6f 3a c7 00 d8 bc 40 74 38 f9 c6 80 80 80 c0 44 02 80 81 0e 02 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26.log.txt:15224:02 04 01 00 5a 01 07 81 0e 13 02 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_teleport_unsorted_tels_allteleportetc001.log.txt:8363:02 03 02 00 03 08 66 f6 5c c7 04 81 0e 46 26 2f 95 46 75 63 98 1f ff 01 64 01 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector14_walking_to_courty.log.txt:332:08 fe 99 c7 65 44 7c 13 05 ea 4b 05 03 1b 66 38 ef 0b b3 e1 81 0e b2 92 a2 40 68 ba aa 13 72 3f
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector28.log.txt:37191:02 03 13 00 02 80 80 80 0c 25 00 00 04 81 0e 00 01 08 6c eb 80 46 00 80 f7 43 0d d4 29 45 00 00
```

### CLIENT_VENDOR_SELL candidate

Signature: `81 11` (spaced or compact hex)

Matches: 4

```text
mxopackets1_crashover2k_007_lost_connection.log.txt:13218:02 03 02 00 01 0e 3e df ef 5b 98 c7 00 00 be 42 81 11 66 46 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector45.log.txt:9125:34 c0 65 c7 06 00 02 0c 81 11 0e 41 47 00 20 8a c4 43 df 3b c7 04 00 02 0c 03 4c bf 42 47 00 20
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector28.log.txt:2:81 3b 01 03 00 36 01 76 14 76 48 b1 54 9f 03 3a 46 fe 9c 81 11 b4 9a 42 b0 3c 9e da d4 2d 50 54
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector28.log.txt:15033:46 00 80 f7 43 0a 1c 39 44 80 80 90 06 07 0c 30 04 00 28 81 11 00 03 2a b3 01 0c c0 81 00 15 00
```

### CR2 static object interaction

Signature: `80 c8` (spaced or compact hex)

Matches: 350

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:577:c0 57 40 00 00 80 c8 e5 c2 de c0 0d 00 00 00 00 00 00 00 01 02 01 00 00 00 00 00 70 db 3f 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:582:80 c8 e5 c2 de c0 0a 00 02 0e 00 17 4d e6 51 c7 00 00 be 42 06 ab c9 c6 03 00 01 80 01 c7 00 1d
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector12.log.txt:41337:00 80 5c 22 c2 80 c8 62 c2 00 00 00 00 80 a9 1e 42 00 00 07 00 30 68 00 00 a4 00 29 07 51 0e 02
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector6.log.txt:11266:02 04 01 00 2f 01 08 80 c8 02 00 e0 03 03 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector6.log.txt:11643:02 04 01 00 31 01 08 80 c8 3f 00 e0 03 03 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector6.log.txt:11921:02 04 01 00 33 01 08 80 c8 3e 00 e0 03 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector6.log.txt:12088:02 04 01 00 36 01 08 80 c8 24 00 e0 03 03 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector6.log.txt:12748:02 04 01 00 3c 01 08 80 c8 23 00 e0 03 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector49_with_unknown_objects.log.txt:482:a8 f1 e9 32 68 a9 74 7f 80 c8 26 ed af 1d c5 23 cc 7c f9 c5 6c 28 30 a6 60 9c 37 ef 41 5b e8 3c
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector49_with_unknown_objects.log.txt:1058:a8 f1 e9 32 68 a9 74 7f 80 c8 26 ed af 1d c5 23 cc 7c f9 c5 6c 28 30 a6 60 9c 37 ef 41 5b e8 3c
```

### CR1 static object interaction

Signature: `80 c3` (spaced or compact hex)

Matches: 237

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:3065:00 00 00 00 00 00 00 00 00 00 00 00 00 10 00 00 22 d5 14 5c 38 1c 00 c6 01 80 c3 f5 09 00 58 4c
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:5275:00 00 00 00 00 00 00 00 00 00 00 00 00 10 00 00 22 d5 14 66 38 1c 00 c6 01 80 c3 f5 09 00 58 4c
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:15594:c6 01 80 c3 f4 09 00 58 4c 0a 00 28 00 00 00 c0 5b e4 e9 c0 00 00 00 00 00 c0 57 40 00 00 00 b8
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:19575:00 00 00 00 00 00 00 00 00 00 00 00 00 10 00 00 22 d5 18 bf 37 1c 00 c6 01 80 c3 f4 09 00 58 4c
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:24159:19 e4 38 1c 00 c6 01 80 c3 f5 09 00 58 4c 0a 00 28 00 00 00 00 0c 2f ea c0 00 00 00 00 00 c0 57
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector12.log.txt:1809:00 00 40 55 40 00 00 00 a0 a5 ab f6 c0 10 c6 00 00 00 04 01 01 b5 01 06 80 c3 57 53 05 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector12.log.txt:8546:80 93 43 70 80 c3 c7 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector12.log.txt:9627:25 c5 6b b8 47 80 c4 3e 43 a5 b4 bd c7 08 00 02 08 6e 41 b8 47 00 80 93 43 02 80 c3 c7 07 00 02
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector12.log.txt:10852:00 00 00 00 00 00 00 00 00 00 00 00 00 10 00 00 22 d5 01 ca 89 1c 00 c6 01 80 c3 bd 01 00 58 4c
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector12.log.txt:10856:10 00 00 22 d5 01 99 87 1c 00 c6 01 80 c3 b8 01 00 58 4c 0a 00 28 00 00 00 7c d8 a9 f9 40 00 00
```

### SERVER object/state bundle

Signature: `80 bc` (spaced or compact hex)

Matches: 15180

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:695:0c 1d 02 16 80 bc 01 00 29 07 00 cd 00 00 00 c8 00 00 00 00 00 00 00 00 20 41 16 80 bc 05 00 2a
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:1796:e7 c0 00 00 00 00 00 c0 57 40 00 00 e0 73 15 f5 de c0 00 00 04 01 0c 47 02 16 80 bc 01 00 29 07
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:1797:00 cd 00 00 00 c8 00 00 00 00 00 01 00 00 20 41 16 80 bc 05 00 2a 07 00 c8 00 00 00 75 00 f6 ff
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:5485:00 00 01 00 00 00 01 01 03 00 00 00 00 00 80 00 00 00 00 00 00 00 00 0c aa 0b 16 80 bc 15 00 21
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:5486:07 00 ee 03 00 00 f1 03 34 00 00 00 01 00 00 00 00 16 80 bc 15 00 22 07 00 ee 03 00 00 56 01 6c
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:5487:00 00 00 01 00 00 00 00 16 80 bc 15 00 23 07 00 ee 03 00 00 ed 03 4c 00 00 00 01 00 00 00 00 16
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:5488:80 bc 15 00 24 07 00 ee 03 00 00 ae 00 1c 00 00 00 01 00 00 00 00 16 80 bc 15 00 25 07 00 ee 03
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:5489:00 00 f3 03 46 00 00 00 01 00 00 00 00 16 80 bc 15 00 26 07 00 ee 03 00 00 ac 00 2e 00 00 00 01
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:5490:00 00 00 00 16 80 bc 15 00 27 07 00 ee 03 00 00 b7 00 4c 00 00 00 01 00 00 00 00 16 80 bc 15 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:5492:bc 15 00 2b 07 00 11 00 00 00 ab 00 0d 00 00 00 00 00 00 00 00 16 80 bc 15 00 2c 07 00 11 00 00
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

Matches: 304

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:15840:00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 01 00 ff 01 ff 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:15854:00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 01 00 ff 01 ff 00 00 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:15868:00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 01 00 ff 01 ff 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:15882:00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 01 00 ff 01 ff 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector2.log.txt:11470:00 00 00 00 01 00 ff 00 00 00 00 51 e2 00 00 00 00 00 00 22 00 00 00 00 00 00 01 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector28.log.txt:3237:08 09 00 28 46 00 00 00 00 00 00 00 00 01 00 ff 00 00 00 00 4d 2c 01 00 00 00 18 01 12 03 f0 01
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector28.log.txt:3278:49 33 00 00 ff ff 1a 95 00 00 08 09 00 28 46 00 00 00 00 00 00 00 00 01 00 ff 00 00 00 00 4d 2c
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector28.log.txt:3354:00 28 0a 00 00 00 00 00 ff ff 00 00 00 00 1d 0a 00 28 12 00 00 00 00 00 00 00 00 01 00 ff 06 01
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector28.log.txt:3853:1a 95 00 00 08 09 00 28 46 00 00 00 00 00 00 00 00 01 00 ff 00 00 00 00 4d 2c 01 00 00 00 18 01
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector28.log.txt:3888:95 00 00 08 09 00 28 46 00 00 00 00 00 00 00 00 01 00 ff 00 00 00 00 4d 2c 01 00 00 00 18 01 12
```

### Selector ff continuation prefix 00 01 04 ff

Signature: `00 01 04 ff` (spaced or compact hex)

Matches: 7

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26.log.txt:14528:02 03 02 00 01 04 ff 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector6.log.txt:11336:02 03 02 00 01 04 ff 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector6.log.txt:11378:02 03 02 00 01 04 ff 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_teleport_unsorted_tels_allteleportetc001.log.txt:135:02 03 02 00 01 04 ff 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_teleport_unsorted_tels_allteleportetc001.log.txt:1749:02 03 02 00 01 04 ff 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector45.log.txt:37239:02 03 02 00 01 04 ff 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector14_walking_to_courty.log.txt:31961:02 03 02 00 01 04 ff 00 00
```

### Selector ff continuation prefix 00 01 10 ff

Signature: `00 01 10 ff` (spaced or compact hex)

Matches: 1039

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector2.log.txt:4420:06 00 28 05 00 00 00 00 00 00 00 00 01 10 ff 00 00 00 00 51 e2 00 00 00 00 08 01 12 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector2.log.txt:7142:05 00 00 00 00 00 00 00 00 01 10 ff 00 00 00 00 51 e2 00 00 00 00 08 01 12 00 00 00 00 00 00 01
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector2.log.txt:7172:06 00 28 05 00 00 00 00 00 00 00 00 01 10 ff 00 00 00 00 51 e2 00 00 00 00 08 01 12 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector2.log.txt:7889:00 28 05 00 00 00 00 00 00 00 00 01 10 ff 00 00 00 00 51 e2 00 00 00 00 08 01 12 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector2.log.txt:9438:00 00 00 00 56 06 00 28 05 00 00 00 00 00 00 00 00 01 10 ff 00 00 00 00 51 e2 00 00 00 00 08 01
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector2.log.txt:9459:00 28 05 00 00 00 00 00 00 00 00 01 10 ff 00 00 00 00 51 e2 00 00 00 00 08 01 12 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector2.log.txt:10728:00 28 05 00 00 00 00 00 00 00 00 01 10 ff 00 00 00 00 51 e2 00 00 00 00 08 01 12 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26.log.txt:32891:d0 8e 56 76 c5 f8 df ec 44 2a 84 41 47 05 00 07 00 27 00 40 00 00 00 10 00 00 01 10 ff 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector28.log.txt:1972:09 00 28 5f 00 00 00 00 00 00 00 00 01 10 ff 00 00 00 00 ed 39 00 00 00 00 18 01 12 03 4e 04 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector28.log.txt:2633:00 44 00 00 04 a6 00 00 00 00 00 00 00 00 01 10 ff e6 02 00 58 00 00 00 00 80 00 18 01 11 0f 18
```

### Selector ff continuation prefix 00 00 00 ff

Signature: `00 00 00 ff` (spaced or compact hex)

Matches: 12297

```text
mxopackets1_crashover2k_007_lost_connection.log.txt:3913:80 b0 04 00 02 8e 00 00 00 01 1e 00 00 00 00 00 00 00 00 00 00 00 00 00 ff de 0d 45 00 f0 20 46
mxopackets1_crashover2k_007_lost_connection.log.txt:3916:19 00 00 00 00 ff 00 00 00 00 01 00 00 00 00 00 00 00 80 3f ee 03 00 00 4b b1 00 00 75 02 00 00
mxopackets1_crashover2k_007_lost_connection.log.txt:3935:02 8e 00 00 00 01 1e 00 00 00 00 00 00 00 00 00 00 00 00 00 ff de 0d 45 00 f0 20 46 14 00 db 31
mxopackets1_crashover2k_007_lost_connection.log.txt:3967:02 8e 00 00 00 01 1e 00 00 00 00 00 00 00 00 00 00 00 00 00 ff de 0d 45 00 f0 20 46 14 00 db 31
mxopackets1_crashover2k_007_lost_connection.log.txt:4470:00 00 00 00 00 00 02 ff 00 00 00 00 00 00 00 00 66 0a 00 00 00 00 45 00 00 00 00 00 00 ff 00 00
mxopackets1_crashover2k_007_lost_connection.log.txt:4495:00 00 00 00 02 ff 00 00 00 00 00 00 00 00 66 0a 00 00 00 00 45 00 00 00 00 00 00 ff 00 00 00 00
mxopackets1_crashover2k_007_lost_connection.log.txt:5729:ff 00 00 00 00 00 00 00 00 a3 02 00 00 00 00 19 00 00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_007_lost_connection.log.txt:6113:00 00 00 00 00 00 02 ff 00 00 00 00 00 00 00 00 66 0a 00 00 00 00 45 00 00 00 00 00 00 ff 00 00
mxopackets1_crashover2k_007_lost_connection.log.txt:6137:00 45 00 00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_007_lost_connection.log.txt:6159:04 00 02 8e 00 00 00 01 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ff de 0d 45 0c 35 63 43 04 00
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

Matches: 1028

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:3064:02 03 01 00 0c 57 02 9d cd ab 14 8e 53 69 6c 76 65 72 20 44 72 61 67 6f 6e 20 48 6f 72 6e 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:5274:02 03 01 00 0c 57 02 9d cd ab 13 8e 53 69 6c 76 65 72 20 44 72 61 67 6f 6e 20 48 6f 72 6e 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:6790:02 03 01 00 0c 57 02 9d cd ab 15 8e 53 69 6c 76 65 72 20 44 72 61 67 6f 6e 20 48 6f 72 6e 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:13275:82 74 7e 26 49 03 01 00 0c 57 02 b9 cd ab 12 8e 5a 6f 6d 62 69 65 20 4c 75 72 63 68 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:13278:4c be a8 34 46 05 b2 09 46 05 06 08 00 00 82 86 31 46 12 02 0d 12 00 00 01 00 0c 57 02 ba cd ab
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:13282:82 86 31 46 12 02 0d 04 00 00 01 00 0c 57 02 bb cd ab 12 8e 5a 6f 6d 62 69 65 20 52 6f 74 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:14061:02 03 01 00 0c 57 02 9d cd ab 15 8e 53 69 6c 76 65 72 20 44 72 61 67 6f 6e 20 48 6f 72 6e 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:15592:02 03 02 00 02 80 80 80 10 03 00 01 00 0c 57 02 9e cd ab 14 8e 53 69 6c 76 65 72 20 44 72 61 67
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:19574:02 03 01 00 0c 57 02 9e cd ab 14 8e 53 69 6c 76 65 72 20 44 72 61 67 6f 6e 20 53 63 61 6c 65 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:24157:82 88 87 26 49 03 02 00 02 80 84 62 80 08 00 00 01 00 0c 57 02 9d cd ab 13 8e 53 69 6c 76 65 72
```

### Adjacent Object599 boundary prefix

Signature: `00 0c 57 02` (spaced or compact hex)

Matches: 998

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:3064:02 03 01 00 0c 57 02 9d cd ab 14 8e 53 69 6c 76 65 72 20 44 72 61 67 6f 6e 20 48 6f 72 6e 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:5274:02 03 01 00 0c 57 02 9d cd ab 13 8e 53 69 6c 76 65 72 20 44 72 61 67 6f 6e 20 48 6f 72 6e 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:6790:02 03 01 00 0c 57 02 9d cd ab 15 8e 53 69 6c 76 65 72 20 44 72 61 67 6f 6e 20 48 6f 72 6e 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:13275:82 74 7e 26 49 03 01 00 0c 57 02 b9 cd ab 12 8e 5a 6f 6d 62 69 65 20 4c 75 72 63 68 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:13278:4c be a8 34 46 05 b2 09 46 05 06 08 00 00 82 86 31 46 12 02 0d 12 00 00 01 00 0c 57 02 ba cd ab
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:13282:82 86 31 46 12 02 0d 04 00 00 01 00 0c 57 02 bb cd ab 12 8e 5a 6f 6d 62 69 65 20 52 6f 74 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:14061:02 03 01 00 0c 57 02 9d cd ab 15 8e 53 69 6c 76 65 72 20 44 72 61 67 6f 6e 20 48 6f 72 6e 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:15592:02 03 02 00 02 80 80 80 10 03 00 01 00 0c 57 02 9e cd ab 14 8e 53 69 6c 76 65 72 20 44 72 61 67
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:19574:02 03 01 00 0c 57 02 9e cd ab 14 8e 53 69 6c 76 65 72 20 44 72 61 67 6f 6e 20 53 63 61 6c 65 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:24157:82 88 87 26 49 03 02 00 02 80 84 62 80 08 00 00 01 00 0c 57 02 9d cd ab 13 8e 53 69 6c 76 65 72
```

### Header-like selector 50

Signature: `01 00 08 50` (spaced or compact hex)

Matches: 44

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector6.log.txt:2101:c0 c0 0b 00 00 01 00 08 50 02 f4 09 e0 03 3c cd ab 02 09 00 00 00 00 00 e6 e4 40 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector6.log.txt:3766:35 3f 11 00 00 01 00 08 50 02 f4 09 e0 03 3c cd ab 02 09 00 00 00 00 00 e6 e4 40 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector6.log.txt:4072:02 03 01 00 02 14 00 0f 00 02 0e 01 4c ea e3 32 47 00 f0 1b 45 36 f2 e1 c5 01 00 08 50 02 f4 09
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector6.log.txt:9020:02 03 01 00 08 50 02 f4 09 e0 03 3c cd ab 02 09 00 00 00 00 00 e6 e4 40 00 00 00 00 00 a4 90 c0
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector6.log.txt:9655:02 03 01 00 08 50 02 f4 09 e0 03 3c cd ab 02 09 00 00 00 00 00 e6 e4 40 00 00 00 00 00 a4 90 c0
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector6.log.txt:12820:00 31 e5 40 00 00 00 00 00 e0 6f c0 00 00 00 00 00 20 ac c0 ff ff ff ff 16 00 00 01 00 08 50 02
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector6.log.txt:17231:01 12 00 00 00 00 00 00 00 01 00 08 50 02 ab 09 c0 15 50 cd ab 01 01 00 00 00 00 00 aa de 40 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector2.log.txt:514:00 00 81 b5 02 00 00 02 03 15 00 00 01 00 08 50 02 f4 09 e0 03 3c cd ab 02 09 00 00 00 00 00 e6
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector14_walking_to_courty.log.txt:1270:2c 01 07 08 00 00 80 02 02 07 00 00 01 00 08 50 02 0f 06 a0 0c ad cd ab 01 01 00 00 00 00 80 19
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector14_walking_to_courty.log.txt:3574:3f 00 00 00 01 22 00 00 00 00 00 00 00 01 00 08 50 02 0f 06 a0 0c ad cd ab 01 01 00 00 00 00 80
```

### Header-like selector 67

Signature: `01 00 08 67` (spaced or compact hex)

Matches: 7

```text
mxopackets1_crashover2k_007_lost_connection.log.txt:1205:82 04 b4 c8 45 03 02 00 02 80 04 1e 01 00 08 67 01 12 00 70 2f bb cd ab 02 80 41 00 00 00 00 80
mxopackets1_crashover2k_007_lost_connection.log.txt:1892:02 03 01 00 08 67 01 13 00 70 2f bc cd ab 02 80 41 00 00 00 00 80 b6 ee c0 00 00 00 00 00 20 62
mxopackets1_crashover2k_007_lost_connection.log.txt:2557:02 03 02 00 02 80 04 1e 01 00 08 67 01 12 00 70 2f c2 cd ab 02 80 41 00 00 00 00 80 1a ef c0 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector2.log.txt:15114:01 22 00 00 00 00 00 00 00 01 00 08 67 95 46 00 80 14 86 cd ab 01 01 00 00 00 00 80 d2 e1 40 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector6.log.txt:2099:c0 00 00 00 00 00 9a c0 c0 00 00 00 00 f2 04 35 bf 00 00 00 00 f3 04 35 3f 0d 00 00 01 00 08 67
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector6.log.txt:10164:00 00 00 00 00 9a c0 c0 00 00 00 00 f2 04 35 bf 00 00 00 00 f3 04 35 3f 12 00 00 01 00 08 67 95
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_teleport_unsorted_tels_allteleportetc001.log.txt:340:02 03 01 00 08 67 95 46 00 80 14 a2 cd ab 01 01 00 00 00 00 80 d2 e1 40 00 00 00 00 00 3c 91 c0
```

### Header-like selector 68

Signature: `01 00 08 68` (spaced or compact hex)

Matches: 6

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector2.log.txt:487:00 00 02 03 1b 00 00 01 00 08 68 95 44 00 80 14 39 cd ab 02 09 00 00 00 00 80 fe e2 40 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_teleport_unsorted_tels_allteleportetc001.log.txt:16812:0d 32 02 b2 09 32 02 07 08 00 00 81 b5 02 00 00 02 03 0d 00 00 01 00 08 68 95 44 00 80 14 a1 cd
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector6.log.txt:2137:00 00 00 17 ef c3 3e 05 00 00 01 00 08 68 95 44 00 80 14 39 cd ab 02 09 00 00 00 00 80 fe e2 40
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector6.log.txt:3764:3f 00 00 00 00 17 ef c3 3e 10 00 00 01 00 08 68 95 44 00 80 14 39 cd ab 02 09 00 00 00 00 80 fe
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector6.log.txt:4037:02 03 01 00 02 14 00 0b 00 02 0e 01 26 96 01 30 47 00 f0 1b 45 f0 a6 df c5 01 00 08 68 95 44 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector6.log.txt:10272:02 03 01 00 08 68 95 44 00 80 14 39 cd ab 02 09 00 00 00 00 80 fe e2 40 00 00 00 00 00 40 91 c0
```

### Header-like selector 69

Signature: `01 00 08 69` (spaced or compact hex)

Matches: 14

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:3784:02 03 01 00 08 69 09 d0 09 40 02 19 cd ab 02 05 00 00 00 00 c0 52 ea c0 00 00 00 00 00 88 99 40
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:8696:02 03 01 00 08 69 09 d0 09 40 02 19 cd ab 02 05 00 00 00 00 c0 52 ea c0 00 00 00 00 00 88 99 40
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:19084:02 03 01 00 08 69 09 d0 09 40 02 19 cd ab 02 05 00 00 00 00 c0 52 ea c0 00 00 00 00 00 88 99 40
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:23565:02 03 01 00 08 69 09 d0 09 40 02 19 cd ab 02 05 00 00 00 00 c0 52 ea c0 00 00 00 00 00 88 99 40
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector2.log.txt:519:82 01 a8 0d ee 02 b2 04 ee 02 07 08 00 00 81 b5 02 00 00 02 03 11 00 00 01 00 08 69 95 43 00 80
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_teleport_unsorted_tels_allteleportetc001.log.txt:916:02 03 01 00 08 69 09 32 04 60 12 3c cd ab 02 05 00 00 00 00 40 4e e2 40 00 00 00 00 00 48 93 40
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_teleport_unsorted_tels_allteleportetc001.log.txt:16804:00 00 81 b5 02 00 00 02 03 15 00 00 01 00 08 69 95 43 00 80 14 a0 cd ab 02 09 00 00 00 00 40 a7
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26.log.txt:7015:02 03 01 00 08 69 09 b5 04 50 16 d3 cd ab 01 01 00 00 00 00 30 1a fa 40 00 00 00 00 00 d0 83 40
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector6.log.txt:2098:0f 00 00 01 00 08 69 95 43 00 80 14 03 cd ab 02 09 00 00 00 00 40 a7 e3 40 00 00 00 00 00 40 91
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector6.log.txt:3742:00 00 00 00 00 20 ac c0 ff ff ff ff 04 00 00 01 00 08 69 95 43 00 80 14 03 cd ab 02 09 00 00 00
```

### Header-like selector 76

Signature: `01 00 0c 76` (spaced or compact hex)

Matches: 0

### Header-like selector 90

Signature: `01 00 08 90` (spaced or compact hex)

Matches: 9

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector49_with_unknown_objects.log.txt:7654:02 03 01 00 02 12 00 35 00 02 08 cf 47 86 c7 00 00 be 42 64 2e 53 c7 01 00 08 90 05 fe 04 d0 0f
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector49_with_unknown_objects.log.txt:8582:02 03 01 00 02 12 00 35 00 02 08 cf 47 86 c7 00 00 be 42 64 2e 53 c7 01 00 08 90 05 fe 04 d0 0f
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector49_with_unknown_objects.log.txt:9519:02 03 01 00 02 12 00 35 00 02 08 cf 47 86 c7 00 00 be 42 64 2e 53 c7 01 00 08 90 05 fe 04 d0 0f
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector49_with_unknown_objects.log.txt:10316:02 03 01 00 02 12 00 35 00 02 08 cf 47 86 c7 00 00 be 42 64 2e 53 c7 01 00 08 90 05 fe 04 d0 0f
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector49_with_unknown_objects.log.txt:11523:02 03 01 00 02 12 00 35 00 02 08 cf 47 86 c7 00 00 be 42 64 2e 53 c7 01 00 08 90 05 fe 04 d0 0f
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_teleport_unsorted_tels_allteleportetc001.log.txt:3826:80 3f 00 00 00 00 2e bd 3b b3 3b 00 00 01 00 08 90 05 fe 04 d0 0f c6 cd ab 02 11 00 00 00 00 e0
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector14_walking_to_courty.log.txt:18864:00 00 02 07 1d 00 00 01 00 08 90 05 ab 0a a0 1c af cd ab 02 11 00 00 00 00 40 17 e2 c0 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_Neuer_Ordner_Kopie_von_Unknown.log.txt:13330:58 02 b2 09 58 02 07 08 00 00 81 f0 1a 00 00 02 05 2d 00 00 01 00 08 90 05 fe 04 d0 0f c6 cd ab
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26.log.txt:38077:09 b0 04 07 08 00 00 81 c1 09 00 00 02 07 0d 00 00 01 00 08 90 05 ab 0a a0 1c 15 cd ab 02 11 00
```

### Header-like selector a3

Signature: `01 00 08 a3` (spaced or compact hex)

Matches: 12

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26.log.txt:632:00 00 00 00 00 48 87 40 00 00 00 00 00 00 a9 40 ff ff ff ff 0f 00 00 01 00 08 a3 01 6b 01 f0 3d
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26.log.txt:19872:02 03 01 00 08 a3 01 6b 01 f0 3d 3a cd ab 03 88 00 00 00 00 ff ff 7f 3f 00 00 00 00 f3 04 35 33
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:8494:02 03 01 00 08 a3 01 3f 00 40 02 6c cd ab 02 80 22 00 00 00 00 00 83 e8 c0 00 00 00 00 00 08 81
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:23193:02 03 02 00 02 80 80 80 10 96 00 01 00 08 a3 01 3f 00 40 02 6c cd ab 02 80 22 00 00 00 00 00 83
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector6.log.txt:11044:02 03 01 00 08 a3 1b 47 00 80 14 42 cd ab 02 09 00 00 00 00 c0 a6 e1 40 00 00 00 00 00 40 91 c0
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector6.log.txt:11675:02 03 01 00 08 a3 01 3f 00 e0 03 92 cd ab 03 88 00 00 00 00 f3 04 35 bf 00 00 00 00 f3 04 35 3f
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector6.log.txt:12112:02 03 01 00 08 a3 01 24 00 e0 03 95 cd ab 03 88 00 00 00 00 f3 04 35 bf 00 00 00 00 f3 04 35 3f
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector6.log.txt:12815:01 00 08 a3 01 3f 00 e0 03 92 cd ab 03 88 00 00 00 00 f3 04 35 bf 00 00 00 00 f3 04 35 3f 82 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector6.log.txt:17368:07 08 00 00 81 e5 1a 00 00 02 04 22 00 00 01 00 08 a3 01 e0 00 c0 15 30 cd ab 02 80 22 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_teleport_unsorted_tels_allteleportetc001.log.txt:422:00 00 00 20 bb 6c ba c0 00 00 00 00 bf c9 6f 3f 00 00 00 00 61 4e b3 3e 04 00 00 01 00 08 a3 1b
```

### Header-like selector d0

Signature: `01 00 08 d0` (spaced or compact hex)

Matches: 225

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector12.log.txt:9373:01 12 00 00 00 00 00 00 00 01 00 08 d0 20 a5 07 a0 1d 7f cd ab 08 fd 00 00 00 00 30 ee f8 40 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector12.log.txt:16425:02 03 01 00 08 d0 20 34 06 a0 0c ac cd ab 08 fd 00 00 00 00 f0 49 fb 40 00 00 00 00 00 f0 7e c0
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector12.log.txt:20008:02 03 01 00 08 d0 20 37 06 a0 0c 96 cd ab 08 fd 00 00 00 00 10 e4 fa 40 00 00 00 00 00 60 7f c0
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector12.log.txt:22680:02 03 01 00 08 d0 20 37 06 a0 0c 96 cd ab 08 fd 00 00 00 00 10 e4 fa 40 00 00 00 00 00 60 7f c0
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector6.log.txt:2133:ff 07 00 00 01 00 08 d0 20 fc 09 e0 03 10 cd ab 08 fd 00 00 00 00 40 5d e7 40 00 00 00 00 00 44
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector6.log.txt:3760:1b 45 c8 e0 e7 c5 01 00 08 d0 20 fc 09 e0 03 10 cd ab 08 fd 00 00 00 00 40 5d e7 40 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector6.log.txt:4059:02 03 01 00 08 d0 20 fc 09 e0 03 10 cd ab 08 fd 00 00 00 00 40 5d e7 40 00 00 00 00 00 44 91 c0
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector6.log.txt:4733:02 03 01 00 08 d0 20 17 00 c0 0a 4d cd ab 08 fd 00 00 00 00 80 5d e8 40 00 00 00 00 00 40 91 c0
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector6.log.txt:11304:01 00 08 d0 20 fc 09 e0 03 10 cd ab 08 fd 00 00 00 00 40 5d e7 40 00 00 00 00 00 44 91 c0 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector6.log.txt:13228:82 d1 d3 3b 48 03 01 00 01 03 00 12 00 1a 00 1b 00 01 00 08 d0 20 fc 09 e0 03 10 cd ab 08 fd 00
```

## Movement And Object-State Leads

### Mode 06 movement selector 08

Signature: `00 06 08` (spaced or compact hex)

Matches: 334

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:926:27 00 40 00 00 00 10 0c 00 00 f2 80 80 80 c0 a8 03 c0 0d 0a 00 28 01 03 0b 00 06 08 68 ef 3c c7
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:1080:02 03 0c 00 06 27 00 40 00 00 00 10 0c 00 03 6d 80 80 80 40 66 03 0b 00 06 08 68 ef 3c c7 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:1682:02 03 0c 00 06 08 03 9b 3c c7 00 00 be 42 ac d2 fb c6 80 80 80 40 e2 02 0b 00 06 08 c7 65 3c c7
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:1707:82 70 73 26 49 03 0c 00 06 08 03 9b 3c c7 00 00 be 42 ac d2 fb c6 80 80 80 40 e4 02 0b 00 06 08
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:1770:82 82 73 26 49 03 0c 00 06 08 03 9b 3c c7 00 00 be 42 ac d2 fb c6 ff 00 00 00 10 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:1775:00 00 00 10 00 22 00 00 00 00 00 0b 00 06 08 c7 65 3c c7 00 00 be 42 80 de fa c6 ff 00 00 00 10
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:1910:02 03 0c 00 06 08 03 9b 3c c7 00 00 be 42 ac d2 fb c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:1915:00 22 00 00 00 00 00 0b 00 06 08 c7 65 3c c7 00 00 be 42 80 de fa c6 ff 00 00 00 10 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:2005:02 03 0c 00 06 08 03 9b 3c c7 00 00 be 42 ac d2 fb c6 80 80 80 40 a8 02 0b 00 06 08 c7 65 3c c7
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:2083:02 03 0e 00 02 1d 0d 00 0f 05 01 0a 00 00 00 01 7d eb 3d c7 00 00 be 42 a1 34 f8 c6 0c 00 06 08
```

### Mode 06 movement selector 0a

Signature: `00 06 0a` (spaced or compact hex)

Matches: 34

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:6076:02 03 02 00 02 80 84 28 80 50 47 00 06 0a 0c 00 04 80 80 80 40 2d 00 0b 00 04 80 80 80 40 0b 01
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:6077:03 00 01 80 01 47 00 06 0a 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:7784:02 03 02 00 02 80 80 80 50 57 00 06 0a 0b 00 04 80 80 80 40 3e 01 03 00 01 80 01 57 00 06 0a 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:25349:02 03 02 00 02 80 80 80 50 6f 00 06 0a 12 00 02 0e 05 24 e9 29 3c c7 00 00 be 42 3f 87 f6 c6 03
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:25350:00 01 80 01 6f 00 06 0a 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26.log.txt:356:00 00 01 00 00 00 00 16 80 bc 55 00 06 0a 00 2d 00 00 00 42 04 05 00 00 00 01 00 00 00 00 16 80
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26.log.txt:33634:00 06 0a 2c 5b e9 e7 c5 f8 df ec 44 d1 3f 37 47 80 80 80 80 c0 9c 05 00 28 81 02 01 06 08 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26.log.txt:33649:02 03 17 00 06 0a 2c 5b e9 e7 c5 f8 df ec 44 d1 3f 37 47 ff 00 00 00 10 00 00 00 00 00 01 ff 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26.log.txt:34777:08 00 00 1f 06 08 00 00 00 00 10 00 22 00 00 00 00 00 17 00 06 0a 00 4e 08 e4 c5 f8 df ec 44 26
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26.log.txt:34807:02 03 18 00 06 0a 00 c6 e3 d4 c5 f8 df ec 44 00 c1 36 47 ff 00 00 00 10 00 00 00 00 00 01 ff 00
```

### Mode 06 movement selector 0c

Signature: `00 06 0c` (spaced or compact hex)

Matches: 3355

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:356:02 03 0a 00 06 0c 77 66 b3 51 c7 00 00 be 42 47 9a c8 c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:606:02 03 0a 00 06 0c 62 4d e6 51 c7 00 00 be 42 06 ab c9 c6 80 80 80 80 80 80 01 12 08 00 00 09 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:607:06 0c 5d 34 0c 52 c7 00 00 be 42 c9 01 d2 c6 80 80 80 80 80 80 01 12 08 00 00 06 00 06 0c 61 4a
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:752:02 03 0c 00 02 08 40 37 3d c7 00 00 be 42 f1 65 f9 c6 0a 00 06 0c 3a 4d e6 51 c7 00 00 be 42 06
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:757:00 f5 09 00 58 37 08 00 00 1f 07 08 00 00 00 00 00 00 22 00 00 00 00 00 09 00 06 0c 35 34 0c 52
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:774:02 03 0a 00 06 0c 30 4d e6 51 c7 00 00 be 42 06 ab c9 c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:779:00 00 22 00 00 00 00 00 09 00 06 0c 2b 34 0c 52 c7 00 00 be 42 c9 01 d2 c6 ff 00 00 00 10 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:802:00 00 00 00 09 00 06 0c 21 34 0c 52 c7 00 00 be 42 c9 01 d2 c6 ff 00 00 00 10 00 00 00 00 00 01
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:824:00 00 00 00 22 00 00 00 00 00 09 00 06 0c 17 34 0c 52 c7 00 00 be 42 c9 01 d2 c6 ff 00 00 00 10
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:842:02 03 0a 00 06 0c 1a 4d e6 51 c7 00 00 be 42 06 ab c9 c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00
```

### Mode 06 movement selector 0e

Signature: `00 06 0e` (spaced or compact hex)

Matches: 4141

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:290:02 03 0a 00 06 0e 04 59 66 b3 51 c7 00 00 be 42 47 9a c8 c6 ff 00 00 00 10 00 00 00 00 00 01 ff
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:295:00 00 00 22 00 00 00 00 00 09 00 06 0e 05 53 34 0c 52 c7 00 00 be 42 c9 01 d2 c6 ff 00 00 00 10
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:312:02 03 0e 00 04 80 80 80 40 f6 02 0a 00 06 0e 04 63 66 b3 51 c7 00 00 be 42 47 9a c8 c6 ff 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:317:08 00 00 1f 07 08 00 00 00 00 00 00 22 00 00 00 00 00 09 00 06 0e 05 49 34 0c 52 c7 00 00 be 42
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:334:82 a4 72 26 49 03 0c 00 02 08 40 37 3d c7 00 00 be 42 f1 65 f9 c6 0a 00 06 0e 04 6d 66 b3 51 c7
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:361:00 00 22 00 00 00 00 00 09 00 06 0e 00 35 34 0c 52 c7 00 00 be 42 c9 01 d2 c6 ff 00 00 00 10 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:378:02 03 0b 00 02 08 68 ef 3c c7 00 00 be 42 c4 f4 f8 c6 0a 00 06 0e 01 90 ad b3 51 c7 00 00 be 42
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:383:00 00 f5 09 00 58 37 08 00 00 1f 07 08 00 00 00 00 00 00 22 00 00 00 00 00 09 00 06 0e 00 2b 34
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:400:02 03 09 00 06 0e 00 27 34 0c 52 c7 00 00 be 42 c9 01 d2 c6 ff 00 00 00 10 00 00 00 00 00 01 ff
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:683:02 03 02 00 02 80 84 32 80 c0 fc 09 80 80 01 04 00 10 01 0e 00 04 80 80 80 40 b4 02 0a 00 06 0e
```

### Repeated mode 06 suffix lead

Signature: `ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00` (spaced or compact hex)

Matches: 2333

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:318:c9 01 d2 c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:335:00 00 be 42 47 9a c8 c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:340:0e 05 3f 34 0c 52 c7 00 00 be 42 c9 01 d2 c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:379:cf b0 c8 c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:384:0c 52 c7 00 00 be 42 c9 01 d2 c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:475:00 be 42 c4 f4 f8 c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:684:05 58 4d e6 51 c7 00 00 be 42 06 ab c9 c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:714:c9 01 d2 c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:753:ab c9 c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:758:c7 00 00 be 42 c9 01 d2 c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00
```

### Movement-state ff 01 64 01 tail

Signature: `ff 01 64 01` (spaced or compact hex)

Matches: 1226

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector12.log.txt:1664:02 03 02 00 03 28 01 c0 00 98 d9 00 33 bb 76 47 00 00 be 42 e6 22 b3 c7 f7 45 3e 1b ff 01 64 01
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector12.log.txt:1680:02 03 02 00 03 28 01 c0 00 98 d9 00 33 bb 76 47 00 00 be 42 e6 22 b3 c7 74 46 3e 1b ff 01 64 01
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector12.log.txt:1696:82 47 2d df 48 03 02 00 03 08 33 bb 76 47 00 00 be 42 e6 22 b3 c7 f1 46 3e 1b ff 01 64 01 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector12.log.txt:1817:82 f2 2d df 48 03 02 00 03 09 08 00 18 fc 76 47 dc 33 5b 43 26 35 b3 c7 d1 5b 3e 1b ff 01 64 01
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector12.log.txt:1829:02 03 02 00 03 09 08 00 c4 2c 77 47 d5 79 99 43 d5 42 b3 c7 4e 5c 3e 1b ff 01 64 01 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector12.log.txt:1859:02 03 02 00 03 08 56 9e 77 47 63 d8 f6 43 c5 62 b3 c7 48 5d 3e 1b ff 01 64 01 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector12.log.txt:1879:02 03 02 00 03 08 3b df 77 47 00 45 13 44 05 75 b3 c7 c5 5d 3e 1b ff 01 64 01 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector12.log.txt:1895:02 03 02 00 03 08 21 20 78 47 e5 0e 29 44 44 87 b3 c7 42 5e 3e 1b ff 01 64 01 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector12.log.txt:1911:02 03 02 00 03 08 06 61 78 47 e4 c9 3c 44 84 99 b3 c7 bf 5e 3e 1b ff 01 64 01 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector12.log.txt:1931:02 03 02 00 03 08 b2 91 78 47 5a 3c 4a 44 34 a7 b3 c7 4c 5f 3e 1b ff 01 64 01 00 00 00 00 00 00
```

### Movement-state ff 01 4e 01 tail

Signature: `ff 01 4e 01` (spaced or compact hex)

Matches: 149

```text
mxopackets1_crashover2k_007_lost_connection.log.txt:3911:82 70 92 ca 45 03 02 00 03 08 f4 0a 65 c7 00 00 be 42 3e 53 ad 46 80 e9 62 00 ff 01 4e 01 00 00
mxopackets1_crashover2k_007_lost_connection.log.txt:3933:02 03 02 00 03 08 f4 0a 65 c7 00 00 be 42 34 64 ad 46 a0 e9 62 00 ff 01 4e 01 00 00 00 00 00 00
mxopackets1_crashover2k_007_lost_connection.log.txt:3965:02 03 02 00 03 08 f4 0a 65 c7 00 00 be 42 78 96 ad 46 1c ea 62 00 ff 01 4e 01 00 00 00 00 00 00
mxopackets1_crashover2k_007_lost_connection.log.txt:6157:02 03 02 00 03 09 08 00 8f ed 86 c7 4e cf 79 41 1f 3e bd 46 c4 4b 63 00 ff 01 4e 01 00 00 00 00
mxopackets1_crashover2k_007_lost_connection.log.txt:6181:02 03 02 00 03 08 1e f0 86 c7 06 e4 9e 42 82 2f bd 46 32 4c 63 00 ff 01 4e 01 00 00 00 00 00 00
mxopackets1_crashover2k_007_lost_connection.log.txt:6213:82 b6 5d cb 45 03 02 00 03 08 0e f3 86 c7 0d d5 fa 42 bf 1e bd 46 c2 4c 63 00 ff 01 4e 01 00 00
mxopackets1_crashover2k_007_lost_connection.log.txt:6235:02 03 02 00 03 08 a4 f5 86 c7 ec cd 0d 43 00 10 bd 46 1b 4d 63 00 ff 01 4e 01 00 00 00 00 00 00
mxopackets1_crashover2k_007_lost_connection.log.txt:6257:02 03 02 00 03 08 ee f8 86 c7 e0 13 02 43 3a fd bc 46 a8 4d 63 00 ff 01 4e 01 00 00 00 00 00 00
mxopackets1_crashover2k_007_lost_connection.log.txt:6279:02 03 02 00 03 08 dd fb 86 c7 6e 89 b1 42 77 ec bc 46 25 4e 63 00 ff 01 4e 01 00 00 00 00 00 00
mxopackets1_crashover2k_007_lost_connection.log.txt:6359:02 03 02 00 03 09 0c 00 1d 02 87 c7 80 ee bf c2 cd c8 bc 46 19 50 63 00 ff 01 4e 01 00 00 00 00
```

## Ability, Effect, And Combat-State Leads

### SERVER_PLAYER_ATTRIBUTE / ability load

Signature: `80 b2` (spaced or compact hex)

Matches: 864

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:64:82 54 72 26 49 04 01 0c 08 02 04 80 b3 d5 04 08 80 b2 d5 04 00 00 08 02
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:367:c6 00 00 04 01 0c 0d 02 04 80 b3 d5 04 08 80 b2 d5 04 00 00 08 02
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:891:02 04 01 0c 1f 02 04 80 b3 d5 04 08 80 b2 d5 04 00 00 08 02
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:1530:c6 00 00 04 01 0c 34 02 04 80 b3 d5 04 08 80 b2 d5 04 00 00 08 02
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:1949:50 a1 cf c6 00 00 04 01 0c 49 02 04 80 b3 d5 04 08 80 b2 d5 04 00 00 08 02
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:2396:00 04 01 0c 4e 02 04 80 b3 d5 04 08 80 b2 d5 04 00 00 08 02
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:2772:00 00 00 22 00 00 00 00 00 00 00 04 01 0c 59 02 04 80 b3 d5 04 08 80 b2 d5 04 00 00 08 02
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:3000:02 04 01 0c 5b 02 04 80 b3 d5 04 08 80 b2 d5 04 00 00 08 02
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:3291:03 03 00 01 00 01 28 0a 00 00 04 01 0c 66 02 04 80 b3 d5 04 08 80 b2 d5 04 00 00 08 02
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:3512:80 b2 d5 04 00 00 08 02
```

### SERVER_ABILITY_UNLOAD

Signature: `80 b3` (spaced or compact hex)

Matches: 793

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:64:82 54 72 26 49 04 01 0c 08 02 04 80 b3 d5 04 08 80 b2 d5 04 00 00 08 02
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:367:c6 00 00 04 01 0c 0d 02 04 80 b3 d5 04 08 80 b2 d5 04 00 00 08 02
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:891:02 04 01 0c 1f 02 04 80 b3 d5 04 08 80 b2 d5 04 00 00 08 02
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:1530:c6 00 00 04 01 0c 34 02 04 80 b3 d5 04 08 80 b2 d5 04 00 00 08 02
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:1949:50 a1 cf c6 00 00 04 01 0c 49 02 04 80 b3 d5 04 08 80 b2 d5 04 00 00 08 02
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:2396:00 04 01 0c 4e 02 04 80 b3 d5 04 08 80 b2 d5 04 00 00 08 02
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:2772:00 00 00 22 00 00 00 00 00 00 00 04 01 0c 59 02 04 80 b3 d5 04 08 80 b2 d5 04 00 00 08 02
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:3000:02 04 01 0c 5b 02 04 80 b3 d5 04 08 80 b2 d5 04 00 00 08 02
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:3291:03 03 00 01 00 01 28 0a 00 00 04 01 0c 66 02 04 80 b3 d5 04 08 80 b2 d5 04 00 00 08 02
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:3511:be 42 1f 13 fa c6 80 80 80 40 4a 03 03 00 01 80 00 b8 00 00 00 04 01 0c 6d 02 04 80 b3 d5 04 08
```

### Object599 adrenaline booster effect id

Signature: `4c 0a 00 28` (spaced or compact hex)

Matches: 5373

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:294:00 4c 0a 00 28 ff 13 00 00 00 00 00 00 00 00 00 00 00 f5 09 00 58 37 08 00 00 1f 07 08 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:299:00 00 00 00 ed 1a 00 00 4c 0a 00 28 ff 13 00 00 00 00 00 00 00 00 00 00 00 f4 09 00 58 37 08 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:316:00 00 00 00 00 00 ed 1a 00 00 4c 0a 00 28 ff 13 00 00 00 00 00 00 00 00 00 00 00 f5 09 00 58 37
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:321:01 00 c2 01 ff 00 00 00 00 00 00 00 00 ed 1a 00 00 4c 0a 00 28 ff 13 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:338:00 00 00 00 01 00 a3 02 ff 00 00 00 00 00 00 00 00 ed 1a 00 00 4c 0a 00 28 ff 13 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:343:00 00 00 00 00 00 00 00 00 00 00 01 00 c2 01 ff 00 00 00 00 00 00 00 00 ed 1a 00 00 4c 0a 00 28
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:360:4c 0a 00 28 ff 13 00 00 00 00 00 00 00 00 00 00 00 f5 09 00 58 37 08 00 00 1f 07 08 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:365:00 00 00 ed 1a 00 00 4c 0a 00 28 ff 13 00 00 00 00 00 00 00 00 00 00 00 f4 09 00 58 37 08 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:382:01 00 a3 02 ff 00 00 00 00 00 00 00 00 ed 1a 00 00 4c 0a 00 28 ff 13 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector35.log.txt:387:00 00 00 00 00 00 00 01 00 c2 01 ff 00 00 00 00 00 00 00 00 ed 1a 00 00 4c 0a 00 28 ff 13 00 00
```

### Object599 fast healing effect id

Signature: `b5 07 00 28` (spaced or compact hex)

Matches: 158

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_teleport_unsorted_tels_allteleportetc001.log.txt:4710:c3 af ce 6f c7 04 00 04 80 80 80 80 c0 b5 07 00 28 01 01 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_teleport_unsorted_tels_allteleportetc001.log.txt:4809:02 03 2b 00 04 80 80 80 80 c0 b5 07 00 28 01 02 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_teleport_unsorted_tels_allteleportetc001.log.txt:4834:02 03 1b 00 04 80 80 80 80 c0 b5 07 00 28 01 02 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector14_walking_to_courty.log.txt:8589:02 0c cf d4 15 95 c7 00 00 be 42 b9 cf 10 c7 23 00 04 80 80 80 80 c0 b5 07 00 28 01 01 22 00 04
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector14_walking_to_courty.log.txt:9409:0c 09 94 c7 00 00 be 42 ec 87 14 c7 18 00 04 80 80 80 80 c0 b5 07 00 28 01 02 16 00 02 0c b7 72
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector14_walking_to_courty.log.txt:9976:02 03 07 00 04 80 80 80 80 c0 b5 07 00 28 01 02 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector14_walking_to_courty.log.txt:10258:82 91 e8 ef 48 03 0a 00 04 80 80 80 80 c0 b5 07 00 28 01 02 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector14_walking_to_courty.log.txt:10525:02 03 1f 00 04 80 80 80 80 c0 b5 07 00 28 01 02 1c 00 02 0e 04 46 a6 fd a2 c7 00 80 fc c3 0a b5
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector14_walking_to_courty.log.txt:10656:d3 82 09 00 58 b5 07 00 28 0a 00 00 20 e8 1f e6 f6 c0 00 00 00 00 00 90 7f c0 00 00 00 14 31 1c
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector14_walking_to_courty.log.txt:11227:02 03 06 00 04 80 80 80 80 c0 b5 07 00 28 01 01 00 00
```

### Object599 accelerated spawn effect id

Signature: `66 00 00 04` (spaced or compact hex)

Matches: 65

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26.log.txt:7806:71 00 00 04 66 00 00 04 22 81 13 6a 28 ae 6b 11 78 de 11 a4 35 00 0d 60 1c a6 94 70 00 00 04 70
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26.log.txt:7807:00 00 04 70 00 00 04 66 00 00 04 22 81 13 6a 28 ae 6b 11 78 de 11 a4 35 00 0d 60 1c a6 94 6f 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26.log.txt:7808:00 04 6f 00 00 04 6f 00 00 04 66 00 00 04 22 81 13 6a 28 ae 6b 11 78 de 11 a4 35 00 0d 60 1c a6
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26.log.txt:7809:94 6e 00 00 04 6e 00 00 04 6e 00 00 04 66 00 00 04 22 81 13 6a 28 ae 6b 11 78 de 11 a4 35 00 0d
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26.log.txt:7810:60 1c a6 94 6d 00 00 04 6d 00 00 04 6d 00 00 04 66 00 00 04 22 81 13 6a 28 ae 6b 11 78 de 11 a4
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26.log.txt:7811:35 00 0d 60 1c a6 94 6c 00 00 04 6c 00 00 04 6c 00 00 04 66 00 00 04 22 81 13 6a 28 ae 6b 11 78
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26.log.txt:7812:de 11 a4 35 00 0d 60 1c a6 94 6b 00 00 04 6b 00 00 04 6b 00 00 04 66 00 00 04
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26.log.txt:7824:04 6a 00 00 04 6a 00 00 04 66 00 00 04 22 81 13 6a 28 ae 6b 11 78 de 11 a4 35 00 0d 60 1c a6 94
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26.log.txt:7827:00 0d 60 1c a6 94 2a 03 00 04 2a 03 00 04 2a 03 00 04 66 00 00 04 22 81 13 6a 28 ae 6b 11 78 de
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26.log.txt:7828:11 a4 35 00 0d 60 1c a6 94 1e 03 00 04 1e 03 00 04 1e 03 00 04 66 00 00 04 22 81 13 6a 28 ae 6b
```

## Intake Next Steps

- Preserve the original files separately before editing or importing anything.
- Inspect matches with surrounding lines; embedded hits inside encrypted or unrelated bodies are leads only.
- Record sender, provenance, CR1/CR2 status, action context, character handle, coordinates, known object ids, and known item GoIDs.
- Import only text logs or text-like dumps that contain no passwords, session tokens, private keys, client binaries, or unrelated game assets.
