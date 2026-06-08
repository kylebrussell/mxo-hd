# Packet Capture Triage Report

- Source kind: `directory`
- Source input: `/Users/kyle/Developer/mxo-hd/research/packet-dumps/community-intake/20260607T205703Z-hdneo-public-vector-supplemental-state-selected/logs`
- Scanned files from: `/Users/kyle/Developer/mxo-hd/research/packet-dumps/community-intake/20260607T205703Z-hdneo-public-vector-supplemental-state-selected/logs`
- Generated UTC: `2026-06-07T20:57:04Z`
- Max sample lines per pattern: `10`

Use this report to decide whether received logs are worth importing into `research/packet-dumps/`. Matches are byte-string leads, not decoded packet claims.

## Vendor And Interaction Leads

### SERVER_VENDOR_OPEN candidate

Signature: `81 0d` (spaced or compact hex)

Matches: 8

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:15290:00 28 81 0d 03 fd 07 00 00 00 00 00 00 00 00 04 01 00 77 01 48 81 9d 13 00 27 00 0c 00 c2 00 0c
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector40.log.txt:5557:82 78 84 9e 48 04 01 01 8e 01 80 de 81 0d 7c ad d9 43 00 00 00 00 80 fe e2 40 00 00 00 00 00 40
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector40.log.txt:6400:02 04 01 01 90 01 7e 81 0d 7c ad d9 43 00 00 00 00 40 a7 e3 40 00 00 00 00 00 40 91 c0 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector40.log.txt:6642:02 04 01 01 92 01 82 fa 81 0d 7c ad d9 43 00 00 00 20 9a c4 e3 40 00 00 00 00 00 3c 91 c0 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector40.log.txt:13858:81 0d 7c ad d9 43 00 00 00 00 40 a7 e3 40 00 00 00 00 00 40 91 c0 00 00 00 00 00 9a c0 c0 20 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector40.log.txt:21658:80 de 81 0d 7c ad d9 43 00 00 00 00 80 fe e2 40 00 00 00 00 00 40 91 c0 00 00 00 00 00 b3 c0 c0
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector31.log.txt:3686:02 03 0d 00 02 08 28 d7 31 c7 00 00 be 42 aa 68 e0 c7 03 00 02 0e 01 47 9a 81 0d c7 00 00 be 42
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_zerooneetc..log.txt:74377:02 04 02 03 25 01 0c 81 9c 98 07 50 4b 26 0e 00 8c 01 00 03 26 01 80 ca 81 0d 7c ad d9 43 00 00
```

### CLIENT_VENDOR_BUY candidate

Signature: `81 0e` (spaced or compact hex)

Matches: 5

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector17.log.txt:2554:02 03 02 00 03 08 00 1c 7c c7 81 0e 69 45 7b 7d 64 c7 b9 42 51 22 ff 01 64 01 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector36.log.txt:8262:2b 44 1a fa e3 45 05 00 02 08 c6 88 d8 47 f0 bf 2d 44 81 0e 98 45 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector16.log.txt:9681:02 03 11 00 02 0c 81 0e fe 34 47 00 20 8a c4 da 10 18 c5 07 00 02 0e 05 18 f2 36 2a 47 00 20 8a
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_zerooneetc..log.txt:60820:02 03 08 00 05 01 c2 03 00 80 80 e0 01 00 00 c0 83 00 00 de 81 0e 03 fd 07 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_zerooneetc..log.txt:78542:64 ec 13 b2 09 ec 13 07 08 00 00 81 0e ba 00 00 02 19 12 00 00 15 00 02 08 77 9b 44 c5 00 00 be
```

### CLIENT_VENDOR_SELL candidate

Signature: `81 11` (spaced or compact hex)

Matches: 6

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_newlogwith_spawned_specialagent.log.txt:184:f1 f8 29 57 ac ea 16 b4 ff d8 ba 25 bd 22 0e a4 de c7 4b 3d 4e 33 21 96 fe 05 0d d3 ea 81 11 87
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_newlogwith_spawned_specialagent.log.txt:12984:aa 45 0d 00 02 2a 00 40 00 00 00 10 01 9a 95 e0 47 f0 bf 2d 44 ae cc ef 45 0b 00 02 0e 04 81 11
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector40.log.txt:101:36 b7 7e 86 4c a1 2b 37 5b 85 81 11 a8 d6 dd 73 99 a1 7a a8 0b c0 db e6 e8 fe cd 0e 2e 1e d4 0c
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector39.log.txt:437:a4 81 11 8f 34 8e 82 92 cc 47 5d ad 1d a8 77 c4 2a fc eb 6a 46 7b e4 6a 11 a8 4c 83 46 3a 7f ce
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Vector3.log.txt:19161:02 03 04 00 02 0e 04 81 11 e5 08 47 00 20 8a c4 80 c5 3a c4 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Vector3.log.txt:34113:8d ee 80 b3 b4 5c 84 50 28 ca 82 51 99 70 0e bd d0 02 71 81 11 61 90 a8 96 53 b4 64 bf 06 cb 67
```

### CR2 static object interaction

Signature: `80 c8` (spaced or compact hex)

Matches: 149

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector31.log.txt:305:83 b0 16 06 ce 9e f9 80 c8 87 20 6b 16 41 da e2 ae ba 9a bf b0 54 8c 68 90 1c bd 71 57 bf e7 fa
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector39.log.txt:6054:02 04 01 00 2c 01 08 80 c8 06 00 e0 03 03 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector39.log.txt:7315:02 04 01 00 2e 01 08 80 c8 21 00 e0 03 03 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector39.log.txt:7739:02 04 01 00 30 01 08 80 c8 22 00 e0 03 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector39.log.txt:12613:02 03 20 00 02 2e 00 40 00 00 00 10 00 80 c8 f5 2d 47 00 f0 1b 45 dc 1c e1 c5 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector39.log.txt:12617:02 03 20 00 02 2e 00 40 00 00 00 10 00 80 c8 f5 2d 47 00 f0 1b 45 dc 1c e1 c5 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector39.log.txt:12621:02 03 20 00 02 2e 00 40 00 00 00 10 00 80 c8 f5 2d 47 00 f0 1b 45 dc 1c e1 c5 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector39.log.txt:12625:02 03 20 00 02 2e 00 40 00 00 00 10 00 80 c8 f5 2d 47 00 f0 1b 45 dc 1c e1 c5 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector39.log.txt:12629:02 03 20 00 02 2e 00 40 00 00 00 10 00 80 c8 f5 2d 47 00 f0 1b 45 dc 1c e1 c5 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector39.log.txt:12633:02 03 20 00 02 2e 00 40 00 00 00 10 00 80 c8 f5 2d 47 00 f0 1b 45 dc 1c e1 c5 00 00 04 01 01 8d
```

### CR1 static object interaction

Signature: `80 c3` (spaced or compact hex)

Matches: 162

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector23.log.txt:7580:00 00 00 22 00 00 00 00 00 00 00 04 01 01 39 01 06 80 c3 d0 26 7a 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector23.log.txt:9063:00 00 00 6d 10 d7 c0 10 e3 00 00 00 04 01 01 3a 01 06 80 c3 e1 52 7a 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector23.log.txt:10539:00 00 04 01 01 3b 01 06 80 c3 82 81 7a 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Vector29.log.txt:1996:00 90 c9 6a 40 00 00 00 20 8a 8f b2 40 10 e3 00 00 00 04 01 01 50 01 06 80 c3 5c 7f 12 01
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Vector29.log.txt:14813:04 01 01 25 01 06 80 c3 78 fc 20 01
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Vector29.log.txt:18300:6c 0e e7 46 00 00 be 42 18 51 af c7 00 00 04 01 01 26 01 06 80 c3 f0 72 21 01
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Vector29.log.txt:19844:01 27 01 06 80 c3 65 9e 21 01
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector31.log.txt:1549:00 00 00 00 00 00 00 00 00 00 00 00 00 10 00 00 22 c5 01 18 01 80 c3 74 0a 00 58 4c 0a 00 28 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector31.log.txt:6172:0c c7 80 c3 f9 42 69 16 ef c7 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector40.log.txt:2228:74 00 00 00 00 00 00 00 00 00 00 00 00 10 00 00 22 d5 01 b1 60 1c 00 c6 01 80 c3 d6 09 00 58 4c
```

### SERVER object/state bundle

Signature: `80 bc` (spaced or compact hex)

Matches: 12508

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:254:80 b2 11 00 02 00 08 02 16 80 bc 45 03 11 00 00 02 00 00 00 11 00 02 00 00 00 00 00 00 00 00 16
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:255:80 bc 45 00 02 00 00 02 00 00 00 cc 00 00 00 00 00 00 00 00 00 00 08 80 b2 ca 03 00 00 08 02 16
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:256:80 bc 45 03 ca 03 00 25 00 00 00 ca 03 00 00 00 00 00 00 00 00 00 16 80 bc 45 00 03 00 00 25 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:257:00 00 89 02 01 00 00 00 00 00 00 00 00 16 80 bc 45 00 04 00 00 2d 00 00 00 23 04 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:258:00 00 00 00 16 80 bc 45 00 05 00 00 2d 00 00 00 20 04 00 00 00 00 00 00 00 00 00 16 80 bc 45 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:259:06 00 00 2d 00 00 00 19 04 00 00 00 00 00 00 00 00 00 16 80 bc 45 00 07 00 00 2d 00 00 00 42 04
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:260:00 00 00 00 00 00 00 00 00 16 80 bc 45 00 08 00 00 71 00 00 00 18 04 0a 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:261:16 80 bc 15 00 09 00 00 f7 03 00 00 08 02 00 00 00 00 00 00 00 00 00 16 80 bc 15 00 0a 00 00 f7
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:262:03 00 00 07 02 ec ff ff ff 00 00 00 00 00 16 80 bc 15 00 0b 00 00 f7 03 00 00 50 04 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:263:00 00 00 00 00 16 80 bc 15 00 0c 00 00 f7 03 00 00 f4 03 00 00 00 00 00 00 00 00 00 16 80 bc 15
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

Matches: 309

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:673:00 00 00 01 00 ff fd 0f 00 04 00 00 00 00 80 00 08 01 12 00 00 00 00 00 00 01 00 1f 00 01 0e 04
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:858:00 00 00 00 01 00 ff fd 0f 00 04 00 00 00 00 00 00 00 00 22 00 00 00 00 00 00 01 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:3737:00 c6 35 00 00 00 00 00 00 00 00 01 00 ff fd 0f 00 04 00 00 00 00 80 00 18 01 12 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:3934:ff 00 00 00 00 07 00 00 c6 35 00 00 00 00 00 00 00 00 01 00 ff fd 0f 00 04 00 00 00 00 80 00 18
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:4079:35 00 00 00 00 00 00 00 00 01 00 ff fd 0f 00 04 00 00 00 00 80 00 18 01 12 00 00 00 00 00 00 01
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:4099:00 00 00 00 00 01 00 ff fd 0f 00 04 00 00 00 00 80 00 18 01 12 00 00 00 00 00 00 01 00 1f 00 02
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:4183:00 00 00 00 01 00 ff fd 0f 00 04 00 00 00 00 80 00 18 01 12 00 00 00 00 00 00 01 00 13 00 02 08
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:4199:00 00 00 00 01 00 ff fd 0f 00 04 00 00 00 00 80 00 18 01 12 00 00 00 00 00 00 01 00 13 00 02 0c
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:4220:00 00 87 0a 00 00 00 00 00 ff ff 00 00 00 00 07 00 00 c6 35 00 00 00 00 00 00 00 00 01 00 ff fd
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:4233:00 00 00 00 01 00 ff fd 0f 00 04 00 00 00 00 80 00 18 01 12 00 00 00 00 00 00 01 00 13 00 02 0e
```

### Selector ff continuation prefix 00 01 04 ff

Signature: `00 01 04 ff` (spaced or compact hex)

Matches: 12

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Vector29.log.txt:20928:02 03 02 00 01 04 ff 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector40.log.txt:5803:02 03 02 00 01 04 ff 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector40.log.txt:8263:02 03 02 00 01 04 ff 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector40.log.txt:13596:02 03 02 00 01 04 ff 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector40.log.txt:14226:02 03 02 00 01 04 ff 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector40.log.txt:22026:02 03 02 00 01 04 ff 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_zerooneetc..log.txt:20923:02 03 02 00 01 04 ff 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_zerooneetc..log.txt:38361:02 03 02 00 01 04 ff 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_zerooneetc..log.txt:47070:02 03 02 00 01 04 ff 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_zerooneetc..log.txt:85481:02 03 02 00 01 04 ff 00 00
```

### Selector ff continuation prefix 00 01 10 ff

Signature: `00 01 10 ff` (spaced or compact hex)

Matches: 78

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:2201:00 00 de 06 00 00 00 00 00 00 00 00 01 10 ff 00 00 00 00 00 00 00 00 02 00 18 00 12 01 d9 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:2228:00 00 00 00 00 ff ff 39 23 00 00 76 00 00 de 06 00 00 00 00 00 00 00 00 01 10 ff 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:2251:00 00 de 06 00 00 00 00 00 00 00 00 01 10 ff 00 00 00 00 00 00 00 00 02 00 18 00 12 01 d9 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:2507:00 00 de 06 00 00 00 00 00 00 00 00 01 10 ff 00 00 00 00 00 00 00 00 02 00 18 00 12 01 d9 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:2518:0c 09 00 00 00 00 00 ff 7f 39 23 00 00 76 00 00 de 06 00 00 00 00 00 00 00 00 01 10 ff 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:2588:00 00 00 00 01 10 ff 00 00 00 00 00 00 00 00 02 00 18 00 12 01 d9 00 00 00 00 01 00 08 00 02 0c
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:2602:00 ff 7f 39 23 00 00 76 00 00 de 06 00 00 00 00 00 00 00 00 01 10 ff 00 00 00 00 00 00 00 00 02
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:2626:06 00 00 00 00 00 00 00 00 01 10 ff 00 00 00 00 00 00 00 00 02 00 18 00 12 01 d9 00 00 00 00 01
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:2649:00 00 00 00 00 00 00 01 10 ff 00 00 00 00 00 00 00 00 02 00 18 00 12 01 d9 00 00 00 00 01 00 13
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:2665:00 00 00 00 00 01 10 ff 00 00 00 00 00 00 00 00 02 00 18 00 12 01 d9 00 00 00 00 01 00 07 00 01
```

### Selector ff continuation prefix 00 00 00 ff

Signature: `00 00 00 ff` (spaced or compact hex)

Matches: 9103

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:672:00 00 00 00 00 00 00 00 00 ea 0a 00 00 00 00 00 ff ff 00 00 00 00 b3 00 00 04 2d 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:855:08 60 00 20 00 40 00 03 20 00 00 00 01 00 00 00 00 00 00 00 00 ff 00 00 00 00 fa 00 03 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:856:00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ff
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:857:00 00 00 00 00 00 00 00 00 00 fa 00 00 00 00 00 00 ff 00 00 00 00 00 b3 00 00 04 04 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:2003:03 00 00 00 c4 96 62 4a 00 ef 02 00 00 00 00 00 28 0e de 18 00 e8 03 00 00 00 ff 96 62 4a 00 59
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:2198:00 00 00 00 00 45 44 26 8e 29 92 84 00 60 07 08 02 40 00 00 03 5c 8f 02 3f 00 00 00 00 ff 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:2200:00 00 00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00 00 ed 08 00 00 00 00 00 ff ff 39 23 00 00 76
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:2212:07 08 02 40 00 00 03 5c 8f 02 3f 00 00 00 00 ff 00 00 00 00 28 0a 32 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:2213:00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ff 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:2214:00 00 00 00 ed 08 00 00 00 00 00 ff ff 39 23 00 00 76 00 00 de 06 00 00 00 00 00 00 00 00 01 10
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

Matches: 896

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:540:02 03 01 00 0c 57 02 25 cd ab 11 8e 42 6c 61 63 6b 77 6f 6f 64 20 47 6f 6f 66 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:564:02 03 01 00 0c 57 02 ca cd ab 15 8e 41 67 65 6e 74 20 4c 6f 6e 67 00 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:2121:02 03 01 00 0c 57 02 5b cd ab 13 ae 53 70 65 63 69 61 6c 20 41 67 65 6e 74 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:2124:b2 04 8c 0a 06 08 00 00 81 22 b6 00 00 02 0d 04 00 00 01 00 0c 57 02 5a cd ab 12 ae 53 70 65 63
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:5290:02 03 01 00 0c 57 02 26 cd ab 13 8e 42 6c 61 63 6b 77 6f 6f 64 20 47 6f 6f 66 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:10417:02 03 01 00 0c 57 02 c2 cd ab 13 ae 53 70 65 63 69 61 6c 20 41 67 65 6e 74 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:10420:b2 04 f6 09 06 08 00 00 81 22 b6 00 00 02 0c 0c 00 00 01 00 0c 57 02 c1 cd ab 12 ae 53 70 65 63
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:10935:02 03 01 00 0c 57 02 26 cd ab 11 8e 42 6c 61 63 6b 77 6f 6f 64 20 47 6f 6f 66 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:12941:02 03 01 00 0c 57 02 ea cd ab 10 8e 53 70 65 63 69 61 6c 20 41 67 65 6e 74 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:12944:00 00 81 22 b6 00 00 02 0d 14 00 00 01 00 0c 57 02 e9 cd ab 12 ae 53 70 65 63 69 61 6c 20 41 67
```

### Adjacent Object599 boundary prefix

Signature: `00 0c 57 02` (spaced or compact hex)

Matches: 876

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:540:02 03 01 00 0c 57 02 25 cd ab 11 8e 42 6c 61 63 6b 77 6f 6f 64 20 47 6f 6f 66 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:564:02 03 01 00 0c 57 02 ca cd ab 15 8e 41 67 65 6e 74 20 4c 6f 6e 67 00 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:2121:02 03 01 00 0c 57 02 5b cd ab 13 ae 53 70 65 63 69 61 6c 20 41 67 65 6e 74 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:2124:b2 04 8c 0a 06 08 00 00 81 22 b6 00 00 02 0d 04 00 00 01 00 0c 57 02 5a cd ab 12 ae 53 70 65 63
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:5290:02 03 01 00 0c 57 02 26 cd ab 13 8e 42 6c 61 63 6b 77 6f 6f 64 20 47 6f 6f 66 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:10417:02 03 01 00 0c 57 02 c2 cd ab 13 ae 53 70 65 63 69 61 6c 20 41 67 65 6e 74 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:10420:b2 04 f6 09 06 08 00 00 81 22 b6 00 00 02 0c 0c 00 00 01 00 0c 57 02 c1 cd ab 12 ae 53 70 65 63
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:10935:02 03 01 00 0c 57 02 26 cd ab 11 8e 42 6c 61 63 6b 77 6f 6f 64 20 47 6f 6f 66 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:12941:02 03 01 00 0c 57 02 ea cd ab 10 8e 53 70 65 63 69 61 6c 20 41 67 65 6e 74 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:12944:00 00 81 22 b6 00 00 02 0d 14 00 00 01 00 0c 57 02 e9 cd ab 12 ae 53 70 65 63 69 61 6c 20 41 67
```

### Header-like selector 50

Signature: `01 00 08 50` (spaced or compact hex)

Matches: 42

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:523:00 00 4c 00 19 00 00 01 00 08 50 02 ea 01 30 39 79 cd ab 01 01 00 00 00 00 00 9a d0 40 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Vector3.log.txt:1236:c0 01 82 03 a8 2f c9 04 b2 09 c9 04 06 08 00 00 81 c4 1e 00 00 02 0b 0c 00 00 01 00 08 50 02 f4
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Vector3.log.txt:29517:01 22 00 00 00 00 00 00 00 01 00 08 50 02 f4 09 e0 03 3c cd ab 02 09 00 00 00 00 00 e6 e4 40 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Vector3.log.txt:35083:28 01 01 01 00 08 50 02 f4 09 e0 03 3c cd ab 02 09 00 00 00 00 00 e6 e4 40 00 00 00 00 00 a4 90
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_actions_loginrogerswayvector002.log.txt:831:00 00 01 00 08 50 02 f3 0c 60 4f 25 cd ab 02 09 00 00 00 00 80 83 ef c0 00 00 00 00 00 ac 90 c0
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector44.log.txt:1087:02 07 08 00 00 81 ed 1a 00 00 02 04 15 00 00 01 00 08 50 02 d3 00 60 11 4d cd ab 02 09 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector44.log.txt:4283:4d d7 f2 c0 13 00 00 01 00 08 50 02 5f 20 e0 17 92 cd ab 02 09 00 00 00 00 00 81 e0 c0 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector44.log.txt:5534:a9 a8 05 3e 00 00 00 00 55 cf 7d 3f 0d 00 00 01 00 08 50 02 b7 01 60 0a 41 cd ab 02 09 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector44.log.txt:6712:01 00 08 50 02 6a 04 90 15 a9 cd ab 02 09 00 00 00 00 80 e6 e6 40 00 00 00 00 00 b0 90 c0 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector36.log.txt:1480:00 00 00 00 00 f2 04 35 3f 00 00 00 00 f3 04 35 3f 1f 00 00 01 00 08 50 02 13 03 f0 3d 26 cd ab
```

### Header-like selector 67

Signature: `01 00 08 67` (spaced or compact hex)

Matches: 10

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Vector3.log.txt:1259:01 22 00 00 00 00 00 00 00 01 00 08 67 95 46 00 80 14 86 cd ab 01 01 00 00 00 00 80 d2 e1 40 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector40.log.txt:5142:02 03 01 00 08 67 95 46 00 80 14 a2 cd ab 01 01 00 00 00 00 80 d2 e1 40 00 00 00 00 00 3c 91 c0
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector40.log.txt:7875:0d 77 01 b2 09 77 01 07 08 00 00 81 b5 02 00 00 02 03 1c 00 00 01 00 08 67 95 46 00 80 14 a2 cd
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector40.log.txt:10263:01 22 00 00 00 00 00 00 00 01 00 08 67 95 46 00 80 14 a2 cd ab 01 01 00 00 00 00 80 d2 e1 40 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector40.log.txt:11831:c0 00 00 00 00 f2 04 35 bf 00 00 00 00 f3 04 35 3f 1b 00 00 01 00 08 67 95 46 00 80 14 a2 cd ab
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector40.log.txt:20869:c2 c0 00 00 00 00 f2 6b dc 3e 00 00 00 00 d4 0f 67 3f 1c 00 00 01 00 08 67 95 46 00 80 14 a2 cd
mxopackets1_crashover2k_afterwhoruneo_Vectornew.log.txt:1244:00 00 c8 26 74 bf 00 00 00 00 1e f6 99 3e 05 00 00 01 00 08 67 95 46 00 80 14 a2 cd ab 01 01 00
mxopackets1_crashover2k_afterwhoruneo_Vectornew.log.txt:3125:00 00 00 20 bb 6c ba c0 00 00 00 00 bf c9 6f 3f 00 00 00 00 61 4e b3 3e 0a 00 00 01 00 08 67 95
mxopackets1_crashover2k_afterwhoruneo_Vectornew.log.txt:4550:6f 3f 00 00 00 00 61 4e b3 3e 07 00 00 01 00 08 67 95 46 00 80 14 a2 cd ab 01 01 00 00 00 00 80
mxopackets1_crashover2k_afterwhoruneo_Vectornew.log.txt:6512:00 00 00 5e 83 6c 3f 07 00 00 01 00 08 67 95 46 00 80 14 a2 cd ab 01 01 00 00 00 00 80 d2 e1 40
```

### Header-like selector 68

Signature: `01 00 08 68` (spaced or compact hex)

Matches: 10

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Vector3.log.txt:1238:00 00 00 00 00 00 80 3f 00 00 00 00 2e bd 3b b3 07 00 00 01 00 08 68 95 44 00 80 14 39 cd ab 02
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Vector3.log.txt:29519:00 00 01 00 08 68 95 44 00 80 14 39 cd ab 02 09 00 00 00 00 80 fe e2 40 00 00 00 00 00 40 91 c0
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Vector3.log.txt:35088:02 07 08 00 00 81 b5 02 00 00 02 03 0b 00 00 01 00 08 68 95 44 00 80 14 39 cd ab 02 09 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector39.log.txt:928:0d 32 02 b2 09 32 02 07 08 00 00 81 b5 02 00 00 02 03 1f 00 00 01 00 08 68 95 44 00 80 14 a1 cd
mxopackets1_crashover2k_afterwhoruneo_Vectornew.log.txt:1237:80 3f 00 00 00 00 2e bd 3b b3 0d 00 00 01 00 08 68 95 44 00 80 14 a1 cd ab 02 09 00 00 00 00 80
mxopackets1_crashover2k_afterwhoruneo_Vectornew.log.txt:4545:00 00 01 00 08 68 95 44 00 80 14 a1 cd ab 02 09 00 00 00 00 80 fe e2 40 00 00 00 00 00 40 91 c0
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector40.log.txt:4858:10 00 00 01 00 08 68 95 44 00 80 14 a1 cd ab 02 09 00 00 00 00 80 fe e2 40 00 00 00 00 00 40 91
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector40.log.txt:10266:00 00 00 00 61 4e b3 3e 10 00 00 01 00 08 68 95 44 00 80 14 a1 cd ab 02 09 00 00 00 00 80 fe e2
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector40.log.txt:11826:01 22 00 00 00 00 00 00 00 01 00 08 68 95 44 00 80 14 a1 cd ab 02 09 00 00 00 00 80 fe e2 40 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector40.log.txt:16001:01 12 00 00 00 00 00 00 00 01 00 08 68 95 44 00 80 14 a1 cd ab 02 09 00 00 00 00 80 fe e2 40 00
```

### Header-like selector 69

Signature: `01 00 08 69` (spaced or compact hex)

Matches: 17

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Vector3.log.txt:1206:00 00 00 20 bb 6c ba c0 00 00 00 00 bf c9 6f 3f 00 00 00 00 61 4e b3 3e 03 00 00 01 00 08 69 95
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Vector3.log.txt:29491:01 22 00 00 00 00 00 00 00 01 00 08 69 95 43 00 80 14 03 cd ab 02 09 00 00 00 00 40 a7 e3 40 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Vector3.log.txt:35093:c0 c0 01 82 03 a8 0d 77 01 b2 09 77 01 07 08 00 00 81 b5 02 00 00 02 03 07 00 00 01 00 08 69 95
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector39.log.txt:933:c0 c0 01 82 01 a8 0d ee 02 b2 04 ee 02 07 08 00 00 81 b5 02 00 00 02 03 1b 00 00 01 00 08 69 95
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector39.log.txt:5782:02 03 01 00 08 69 09 32 04 60 12 3c cd ab 02 05 00 00 00 00 40 4e e2 40 00 00 00 00 00 48 93 40
mxopackets1_crashover2k_afterwhoruneo_Vectornew.log.txt:1233:00 00 00 00 00 d9 c2 c0 00 00 00 00 f2 6b dc 3e 00 00 00 00 d4 0f 67 3f 11 00 00 01 00 08 69 09
mxopackets1_crashover2k_afterwhoruneo_Vectornew.log.txt:3150:00 00 01 00 08 69 95 43 00 80 14 a0 cd ab 02 09 00 00 00 00 40 a7 e3 40 00 00 00 00 00 40 91 c0
mxopackets1_crashover2k_afterwhoruneo_Vectornew.log.txt:4569:01 12 00 00 00 00 00 00 00 01 00 08 69 95 43 00 80 14 a0 cd ab 02 09 00 00 00 00 40 a7 e3 40 00
mxopackets1_crashover2k_afterwhoruneo_Vectornew.log.txt:6510:00 00 f2 6b dc 3e 00 00 00 00 d4 0f 67 3f 09 00 00 01 00 08 69 09 32 04 60 12 3c cd ab 02 05 00
mxopackets1_crashover2k_afterwhoruneo_Vectornew.log.txt:6513:00 00 00 00 00 3c 91 c0 00 00 00 00 00 04 c0 c0 05 00 00 01 00 08 69 95 43 00 80 14 a0 cd ab 02
```

### Header-like selector 76

Signature: `01 00 0c 76` (spaced or compact hex)

Matches: 0

### Header-like selector 90

Signature: `01 00 08 90` (spaced or compact hex)

Matches: 4

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_actions_loginrogerswayvector002.log.txt:818:02 03 01 00 02 14 00 39 00 02 0e 01 e9 e0 42 74 c7 00 80 fc c3 1c ca 78 c7 01 00 08 90 05 fe 04
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector17.log.txt:2357:01 22 00 00 00 00 00 00 00 01 00 08 90 05 fe 04 d0 0f c6 cd ab 02 11 00 00 00 00 e0 fa f0 c0 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector17.log.txt:8485:00 00 81 c7 09 00 00 02 05 35 00 00 01 00 08 90 05 fe 04 d0 0f c6 cd ab 02 11 00 00 00 00 e0 fa
mxopackets1_crashover2k_afterwhoruneo_Vectornew.log.txt:9676:09 a9 03 07 08 00 00 80 02 05 3b 00 00 01 00 08 90 05 fe 04 d0 0f c6 cd ab 02 11 00 00 00 00 e0
```

### Header-like selector a3

Signature: `01 00 08 a3` (spaced or compact hex)

Matches: 29

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Vector3.log.txt:1231:01 22 00 00 00 00 00 00 00 01 00 08 a3 1b 47 00 80 14 42 cd ab 02 09 00 00 00 00 c0 a6 e1 40 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector36.log.txt:1486:01 f9 07 00 00 80 02 01 1b 00 00 01 00 08 a3 01 6b 01 f0 3d a8 cd ab 03 88 00 00 00 00 ff ff 7f
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector39.log.txt:6342:02 03 01 00 08 a3 01 1b 00 e0 03 ae cd ab 02 80 22 00 00 00 00 80 cd e6 40 00 00 00 00 00 e0 6f
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector39.log.txt:6343:c0 00 00 00 00 00 dc be c0 ff ff ff ff 22 00 00 01 00 08 a3 01 1e 00 e0 03 ad cd ab 02 80 22 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector39.log.txt:7396:02 03 01 00 08 a3 01 21 00 e0 03 fa cd ab 02 80 82 00 00 00 00 80 cd e6 40 00 00 00 00 00 7c 90
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector39.log.txt:7918:02 03 02 00 01 0c 67 18 fe 36 47 00 e0 d3 44 4b 5c ec c5 00 c8 48 13 01 00 08 a3 01 0c 00 e0 03
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector39.log.txt:7920:ff ff 12 00 00 01 00 08 a3 01 09 00 e0 03 a0 cd ab 02 80 22 00 00 00 00 80 cd e6 40 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector39.log.txt:7923:a0 40 00 00 00 00 00 20 bc c0 15 00 00 01 00 08 a3 01 0f 00 e0 03 b9 cd ab 02 80 22 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_Vectornew.log.txt:1232:02 03 01 00 08 a3 1b 47 00 80 14 a7 cd ab 02 09 00 00 00 00 c0 a6 e1 40 00 00 00 00 00 40 91 c0
mxopackets1_crashover2k_afterwhoruneo_Vectornew.log.txt:3148:01 12 00 00 00 00 00 00 00 01 00 08 a3 1b 47 00 80 14 a7 cd ab 02 09 00 00 00 00 c0 a6 e1 40 00
```

### Header-like selector d0

Signature: `01 00 08 d0` (spaced or compact hex)

Matches: 69

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:466:02 03 01 00 02 13 00 25 00 01 0c 1b b4 52 84 46 00 80 f7 43 92 59 20 45 01 00 08 d0 20 f1 01 30
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:494:de 90 01 10 89 c7 82 a0 04 46 1d 39 23 00 00 03 11 00 00 00 81 ff 1b 00 00 01 00 08 d0 20 ef 01
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:567:00 b2 02 32 32 fd 07 00 00 81 22 b6 00 00 06 ff 35 08 00 00 0d 00 00 01 00 08 d0 20 b1 00 20 4e
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:571:3a 00 38 0b 00 58 00 00 01 00 0b 00 00 01 00 08 d0 20 ee 01 30 39 59 cd ab 08 fd 00 00 00 00 c0
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:597:ed b9 00 00 02 81 00 00 00 c7 ff 09 00 00 01 00 08 d0 20 b0 00 20 4e 5e cd ab 08 fd 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector31.log.txt:877:81 e5 1a 00 00 02 04 21 00 00 01 00 08 d0 20 df 13 c0 3f e9 cd ab 08 fd 00 00 00 00 00 cd e4 c0
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector31.log.txt:1025:be 42 52 03 f2 c7 01 00 08 d0 20 08 00 b0 31 ed cd ab 08 fd 00 00 00 00 80 75 e4 c0 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector36.log.txt:1476:b2 09 51 01 07 08 00 00 80 02 01 21 00 00 01 00 08 d0 20 94 0a 50 46 b2 cd ab 08 fd 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector36.log.txt:1513:b2 09 51 01 38 21 00 00 80 02 01 19 00 00 01 00 08 d0 20 21 03 f0 3d ae cd ab 08 fd 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector36.log.txt:1590:40 c0 01 83 01 01 a8 07 c2 01 b2 04 c2 01 42 21 00 00 80 02 01 05 00 00 01 00 08 d0 20 23 03 f0
```

## Movement And Object-State Leads

### Mode 06 movement selector 08

Signature: `00 06 08` (spaced or compact hex)

Matches: 99

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:3263:02 03 1b 00 02 80 80 10 67 09 13 00 06 08 29 53 80 46 00 80 93 43 9b 33 93 45 80 80 80 80 20 1f
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:3296:02 03 04 00 06 08 61 0b 76 46 00 00 be 42 0e 75 24 45 80 80 80 80 c0 4c 0a 00 28 81 01 02 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:4057:02 03 25 00 03 0c e7 52 b8 81 46 00 80 f7 43 8f 63 67 45 80 80 10 84 0a 13 00 06 08 00 6e 75 46
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:4234:05 3b aa 50 78 46 00 00 be 42 00 00 16 c3 08 00 06 08 71 2c 76 46 00 00 be 42 c8 22 52 45 ff 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:4655:43 f7 8f 1f 45 08 00 06 08 82 09 76 46 00 00 be 42 42 6c 61 45 ff 00 00 00 10 00 00 00 00 00 01
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:5083:82 e4 87 98 47 03 0e 00 02 0c 00 3e 0c 7d 46 00 80 f7 43 90 29 18 44 08 00 06 08 24 78 7f 46 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:5181:fe 09 75 45 08 00 04 80 80 80 40 a5 09 04 00 06 08 58 a4 7f 46 c0 30 ef 43 7c 03 88 45 ff 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:5197:54 dd 77 45 08 00 06 08 95 8d 7e 46 00 80 f7 43 9f 9c 5f 45 ff 00 00 00 10 00 00 00 00 00 01 ff
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:5266:f7 43 98 88 4b 45 80 80 80 40 a8 09 04 00 06 08 5d f6 7e 46 00 80 f7 43 8a 2c 6b 45 ff 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:5365:0e 00 52 b8 e2 2c 46 00 00 be 42 14 d7 6d 45 04 00 06 08 2c 4b 7e 46 00 80 f7 43 5e e9 46 45 ff
```

### Mode 06 movement selector 0a

Signature: `00 06 0a` (spaced or compact hex)

Matches: 75

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Vector29.log.txt:11866:02 03 11 00 06 0a 05 44 f4 26 47 80 78 d7 42 73 91 a5 c7 80 80 80 80 80 80 01 07 08 00 00 0b 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:5487:1f 45 04 00 06 0a 03 fa 9f 7d 46 00 80 f7 43 ea a5 22 45 ff 00 00 00 10 00 00 00 00 00 01 ff 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:6030:11 00 02 0c 28 33 21 35 46 00 00 be 42 6a d0 7b 45 04 00 06 0a 00 f0 43 7c 46 00 80 f7 43 d0 38
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:7305:82 e8 92 98 47 03 25 00 02 80 80 10 07 0a 13 00 06 0a 00 97 e7 7a 46 00 80 f7 43 da 33 2c 45 ff
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:12709:02 03 13 00 06 0a 00 11 dc 84 46 00 80 f7 43 d1 e0 ee 44 ff 00 00 00 10 00 00 00 00 00 00 ff 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_newlogwith_spawned_specialagent.log.txt:3996:82 26 07 99 47 03 1b 00 06 0a 00 03 5a d6 47 f0 bf 2d 44 98 ad 8e 45 80 80 80 80 80 80 01 39 21
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_newlogwith_spawned_specialagent.log.txt:4456:02 03 1b 00 06 0a 00 6c 56 d7 47 f0 bf 2d 44 7f 13 aa 45 80 80 80 80 80 80 01 45 21 00 00 13 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_newlogwith_spawned_specialagent.log.txt:4466:82 b6 0a 99 47 03 1b 00 06 0a 00 6c 56 d7 47 f0 bf 2d 44 7f 13 aa 45 ff 00 00 00 10 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_newlogwith_spawned_specialagent.log.txt:6627:82 92 21 99 47 03 1b 00 06 0a 00 8a fb de 47 f0 bf 2d 44 30 20 e5 45 80 80 80 80 80 80 01 45 21
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_newlogwith_spawned_specialagent.log.txt:6637:02 03 1b 00 06 0a 00 8a fb de 47 f0 bf 2d 44 30 20 e5 45 80 80 80 80 80 80 01 47 21 00 00 00 00
```

### Mode 06 movement selector 0c

Signature: `00 06 0c` (spaced or compact hex)

Matches: 1237

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector44.log.txt:1608:02 03 11 00 06 0c b2 51 24 08 c7 00 00 be 42 dc 05 af c6 80 80 80 80 c0 4c 0a 00 28 01 01 03 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector44.log.txt:1617:02 03 11 00 06 0c a8 51 24 08 c7 00 00 be 42 dc 05 af c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector44.log.txt:1631:02 03 11 00 06 0c 9e 51 24 08 c7 00 00 be 42 dc 05 af c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector44.log.txt:1673:02 03 11 00 06 0c 87 42 34 08 c7 00 00 be 42 de 70 af c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector44.log.txt:6124:02 03 15 00 06 0c 53 98 c4 94 47 00 60 8b c4 b0 5e 11 45 80 80 80 80 c0 4c 0a 00 28 01 01 0f 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:3062:00 00 00 01 10 ff 00 00 00 00 00 00 00 00 00 00 08 00 12 01 d9 00 00 00 00 01 00 11 00 06 0c 62
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:3083:00 00 00 00 01 10 ff 00 00 00 00 00 00 00 00 00 00 08 00 12 01 d9 00 00 00 00 01 00 11 00 06 0c
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:3104:00 00 00 00 01 10 ff 00 00 00 00 00 00 00 00 00 00 08 00 12 01 d9 00 00 00 00 01 00 11 00 06 0c
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:3121:02 03 11 00 06 0c 80 f0 dd 31 46 00 00 be 42 d7 74 73 45 ff 00 00 00 10 00 00 00 00 00 01 ff 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:3135:02 03 13 00 02 0c 00 00 b0 81 46 00 80 f7 43 a8 2c 7b 45 11 00 06 0c 8b f0 dd 31 46 00 00 be 42
```

### Mode 06 movement selector 0e

Signature: `00 06 0e` (spaced or compact hex)

Matches: 1834

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector39.log.txt:4787:00 00 22 00 00 00 00 00 19 00 06 0e 00 c9 1e 94 3a 47 00 20 8a c4 d0 67 89 c4 ff 00 00 00 10 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector39.log.txt:4833:02 03 21 00 06 0e 00 6a 4c f1 35 47 00 20 8a c4 e9 0f 98 c5 ff 00 00 00 10 00 00 00 00 00 01 ff
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector39.log.txt:4859:02 03 21 00 06 0e 00 60 4c f1 35 47 00 20 8a c4 e9 0f 98 c5 ff 00 00 00 10 00 00 00 00 00 01 ff
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector39.log.txt:4986:02 03 0b 00 02 0e 05 8a 63 8c 37 47 00 20 8a c4 d6 ba 31 c6 07 00 06 0e 04 77 d8 62 29 47 00 20
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector39.log.txt:5027:c6 07 00 06 0e 00 95 d8 62 29 47 00 20 8a c4 70 d5 5a c4 ff 00 00 00 10 00 00 00 00 00 01 ff 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector39.log.txt:5042:02 03 0b 00 02 0e 01 62 be a3 37 47 00 20 8a c4 6d 52 32 c6 07 00 06 0e 00 9f d8 62 29 47 00 20
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector39.log.txt:6265:08 00 00 20 00 06 0e 05 ea 35 f1 31 47 00 20 8a c4 28 f4 07 c6 80 80 80 80 80 80 01 12 08 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector39.log.txt:6266:18 00 06 0e 04 ba f0 63 31 47 00 20 8a c4 5e fd 04 c6 80 80 80 80 80 80 01 12 08 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector39.log.txt:6377:02 03 20 00 06 0e 04 f4 35 f1 31 47 00 20 8a c4 28 f4 07 c6 ff 00 00 00 10 00 00 00 00 00 01 ff
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector39.log.txt:6382:00 00 00 22 00 00 00 00 00 19 00 06 0e 04 de 16 5b 34 47 00 20 8a c4 d0 dd 09 c6 ff 00 00 00 10
```

### Repeated mode 06 suffix lead

Signature: `ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00` (spaced or compact hex)

Matches: 1393

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:2946:31 46 00 00 be 42 d7 74 73 45 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:2962:42 d7 74 73 45 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:3043:58 f0 dd 31 46 00 00 be 42 d7 74 73 45 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:3063:f0 dd 31 46 00 00 be 42 d7 74 73 45 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:3084:6c f0 dd 31 46 00 00 be 42 d7 74 73 45 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:3105:76 f0 dd 31 46 00 00 be 42 d7 74 73 45 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:3136:d7 74 73 45 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:3164:06 0e 01 9f 05 a4 31 46 00 00 be 42 76 61 72 45 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:3321:be 42 7a 18 1b 45 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:3675:12 82 46 00 80 f7 43 b0 49 46 45 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00
```

### Movement-state ff 01 64 01 tail

Signature: `ff 01 64 01` (spaced or compact hex)

Matches: 1702

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Vector3.log.txt:124:82 bd 00 d2 48 03 02 00 03 09 08 00 3e 1b 13 47 27 a3 fb 43 d9 9b c0 c5 13 97 a2 19 ff 01 64 01
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Vector3.log.txt:154:02 03 02 00 03 08 af 12 13 47 1b 6c ac 44 39 3c c4 c5 0d 98 a2 19 ff 01 64 01 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Vector3.log.txt:171:02 03 02 00 03 08 af 12 13 47 cb 59 ea 44 1e 3c c4 c5 8a 98 a2 19 ff 01 64 01 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Vector3.log.txt:187:02 03 02 00 03 08 af 12 13 47 83 3e 13 45 03 3c c4 c5 07 99 a2 19 ff 01 64 01 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Vector3.log.txt:203:02 03 02 00 03 08 af 12 13 47 e7 6a 30 45 e8 3b c4 c5 84 99 a2 19 ff 01 64 01 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Vector3.log.txt:220:02 03 02 00 03 08 b0 12 13 47 0f b2 4c 45 cd 3b c4 c5 01 9a a2 19 ff 01 64 01 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Vector3.log.txt:245:02 03 02 00 03 08 b0 12 13 47 59 48 81 45 97 3b c4 c5 fb 9a a2 19 ff 01 64 01 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Vector3.log.txt:262:82 e1 00 d2 48 03 02 00 03 08 b0 12 13 47 16 14 8e 45 7c 3b c4 c5 78 9b a2 19 ff 01 64 01 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Vector3.log.txt:279:02 03 02 00 03 08 b1 12 13 47 36 6d 9a 45 61 3b c4 c5 f5 9b a2 19 ff 01 64 01 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Vector3.log.txt:295:02 03 02 00 03 08 b1 12 13 47 b9 53 a6 45 46 3b c4 c5 72 9c a2 19 ff 01 64 01 00 00 00 00 00 00
```

### Movement-state ff 01 4e 01 tail

Signature: `ff 01 4e 01` (spaced or compact hex)

Matches: 0

## Ability, Effect, And Combat-State Leads

### SERVER_PLAYER_ATTRIBUTE / ability load

Signature: `80 b2` (spaced or compact hex)

Matches: 419

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:252:cb c0 12 00 00 00 00 0a 80 e4 00 28 6b ee 00 00 00 00 08 80 b2 4e 00 09 00 08 02 08 80 b2 52 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:253:05 00 08 02 08 80 b2 54 00 0b 00 08 02 08 80 b2 4f 00 08 00 08 02 08 80 b2 51 00 08 00 08 02 08
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:254:80 b2 11 00 02 00 08 02 16 80 bc 45 03 11 00 00 02 00 00 00 11 00 02 00 00 00 00 00 00 00 00 16
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:255:80 bc 45 00 02 00 00 02 00 00 00 cc 00 00 00 00 00 00 00 00 00 00 08 80 b2 ca 03 00 00 08 02 16
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:383:00 00 00 00 01 00 00 00 00 08 80 b2 11 00 02 00 08 02 16 80 bc 45 03 11 00 00 02 00 00 00 11 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:386:00 03 00 00 25 00 00 00 89 02 01 00 00 00 01 00 00 00 00 08 80 b2 ca 03 00 00 08 02 16 80 bc 45
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector31.log.txt:554:cb c0 12 00 00 00 00 0a 80 e4 14 61 46 e4 00 00 00 00 08 80 b2 4e 00 08 00 08 02 08 80 b2 52 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector31.log.txt:555:1e 00 08 02 08 80 b2 54 00 0d 00 08 02 08 80 b2 4f 00 08 00 08 02 08 80 b2 51 00 1e 00 08 02 08
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector31.log.txt:556:80 b2 11 00 32 00 08 02 16 80 bc 45 03 11 00 00 02 00 00 00 11 00 32 00 00 00 00 00 00 00 00 16
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector31.log.txt:562:08 80 b2 36 04 00 00 08 02 16 80 bc 45 03 36 04 00 40 00 00 00 36 04 00 00 00 00 00 00 00 00 00
```

### SERVER_ABILITY_UNLOAD

Signature: `80 b3` (spaced or compact hex)

Matches: 335

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:381:02 03 02 00 02 80 80 80 80 80 10 11 00 00 00 00 00 04 01 00 2c 17 04 80 b3 11 00 16 80 bc 45 03
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:385:04 80 b3 ca 03 16 80 bc 45 03 ca 03 00 25 00 00 00 ca 03 00 00 00 00 01 00 00 00 00 16 80 bc 45
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_newlogwith_spawned_specialagent.log.txt:387:55 08 55 42 b8 84 ef 35 76 5d b3 1e 71 3e 07 80 b3 12 aa 9b 15 32 e9 86 80 a1 05 e9 da c8 f2 82
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_newlogwith_spawned_specialagent.log.txt:991:00 00 00 00 00 16 80 bc 56 00 60 00 00 c3 b2 00 00 9d 00 1f 00 00 00 00 00 00 00 00 04 80 b3 11
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_newlogwith_spawned_specialagent.log.txt:1025:00 00 00 00 16 80 bc 45 00 0c 00 00 62 00 00 00 06 04 01 00 00 00 01 00 00 00 00 04 80 b3 35 04
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_newlogwith_spawned_specialagent.log.txt:1029:00 00 00 00 00 16 80 bc 55 00 6c 00 00 62 00 00 00 15 04 01 00 00 00 00 00 00 00 00 04 80 b3 3e
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_newlogwith_spawned_specialagent.log.txt:1045:00 00 04 80 b3 3f 04 16 80 bc 45 03 3f 04 00 64 01 00 00 3f 04 00 00 00 00 01 00 00 00 00 16 80
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_newlogwith_spawned_specialagent.log.txt:1061:03 0f 00 00 00 00 00 00 00 00 04 80 b3 40 04 16 80 bc 45 03 40 04 00 d5 01 00 00 40 04 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_newlogwith_spawned_specialagent.log.txt:2253:f3 03 46 00 00 00 01 00 00 00 00 0b 80 bd 05 11 00 00 00 00 00 00 00 04 80 b3 11 00 16 80 bc 45
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_logins_newlogwith_spawned_specialagent.log.txt:2254:03 11 00 00 02 00 00 00 11 00 32 00 00 00 01 00 00 00 00 04 80 b3 35 04 16 80 bc 45 03 35 04 00
```

### Object599 adrenaline booster effect id

Signature: `4c 0a 00 28` (spaced or compact hex)

Matches: 1406

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:3296:02 03 04 00 06 08 61 0b 76 46 00 00 be 42 0e 75 24 45 80 80 80 80 c0 4c 0a 00 28 81 01 02 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:3324:00 00 01 00 8c 0a ff 00 00 00 00 00 00 00 00 22 b6 00 00 4c 0a 00 28 ff 01 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:4780:00 00 00 00 00 00 00 00 00 00 00 01 00 5a 0a ff 00 00 00 00 00 00 00 00 22 b6 00 00 4c 0a 00 28
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:4848:00 00 00 00 00 00 00 00 00 01 00 5c 0a ff 00 00 00 00 00 00 00 00 22 b6 00 00 4c 0a 00 28 ff 01
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:4947:b6 00 00 4c 0a 00 28 ff 01 00 00 00 00 00 00 00 00 00 00 00 36 00 00 4e 37 08 00 00 1f 06 08 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:4967:00 00 00 00 00 00 00 00 00 01 00 5f 0a ff 00 00 00 00 00 00 00 00 22 b6 00 00 4c 0a 00 28 ff 01
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:5185:00 00 00 00 00 00 22 b6 00 00 4c 0a 00 28 ff 01 00 00 00 00 00 00 00 00 00 00 00 36 00 00 4e 37
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:5270:00 00 00 00 00 22 b6 00 00 4c 0a 00 28 ff 01 00 00 00 00 00 00 00 00 00 00 00 36 00 00 4e 37 08
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:5369:00 00 00 00 00 00 00 00 22 b6 00 00 4c 0a 00 28 ff 01 00 00 00 00 00 00 00 00 00 00 00 36 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_Syntax.log.txt:5491:4c 0a 00 28 ff 01 00 00 00 00 00 00 00 00 00 00 00 36 00 00 4e 37 08 00 00 1f 06 08 00 00 00 00
```

### Object599 fast healing effect id

Signature: `b5 07 00 28` (spaced or compact hex)

Matches: 188

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector31.log.txt:1705:07 08 00 00 00 00 00 00 22 00 00 00 00 00 0f 00 04 80 80 80 80 c0 b5 07 00 28 01 02 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector31.log.txt:1752:00 1f 07 08 00 00 00 00 00 00 22 00 00 00 00 00 11 00 04 80 80 80 80 c0 b5 07 00 28 01 01 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector31.log.txt:2128:02 03 0b 00 06 0e 04 9d cc 80 32 c7 00 00 be 42 27 e3 e0 c7 80 80 80 80 c0 b5 07 00 28 01 03 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector31.log.txt:2141:b5 07 00 28 ff 03 00 00 00 00 00 00 00 00 00 00 00 1b 0a 00 58 37 08 00 00 1f 07 08 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector31.log.txt:2161:00 00 00 00 00 00 00 00 00 00 00 01 00 a9 01 ff 00 00 00 00 00 00 00 00 e5 1a 00 00 b5 07 00 28
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector31.log.txt:2183:b5 07 00 28 ff 03 00 00 00 00 00 00 00 00 00 00 00 1b 0a 00 58 37 08 00 00 1f 07 08 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector31.log.txt:2202:e5 1a 00 00 b5 07 00 28 ff 03 00 00 00 00 00 00 00 00 00 00 00 1b 0a 00 58 37 08 00 00 1f 07 08
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector31.log.txt:2222:00 00 00 01 00 a9 01 ff 00 00 00 00 00 00 00 00 e5 1a 00 00 b5 07 00 28 ff 03 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector31.log.txt:2238:e5 1a 00 00 b5 07 00 28 ff 03 00 00 00 00 00 00 00 00 00 00 00 1b 0a 00 58 37 08 00 00 1f 07 08
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector31.log.txt:2251:00 00 00 01 00 a9 01 ff 00 00 00 00 00 00 00 00 e5 1a 00 00 b5 07 00 28 ff 03 00 00 00 00 00 00
```

### Object599 accelerated spawn effect id

Signature: `66 00 00 04` (spaced or compact hex)

Matches: 1

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_zerooneetc..log.txt:26128:02 03 02 00 01 06 00 66 00 00 04 01 00 42 03 12 80 c9 14 00 90 0b 00 b8 08 47 00 00 20 41 00 b8
```

## Intake Next Steps

- Preserve the original files separately before editing or importing anything.
- Inspect matches with surrounding lines; embedded hits inside encrypted or unrelated bodies are leads only.
- Record sender, provenance, CR1/CR2 status, action context, character handle, coordinates, known object ids, and known item GoIDs.
- Import only text logs or text-like dumps that contain no passwords, session tokens, private keys, client binaries, or unrelated game assets.
