# Packet Capture Triage Report

- Source kind: `directory`
- Source input: `/Users/kyle/Developer/mxo-hd/research/packet-dumps/community-intake/20260607T030922Z-hdneo-public-luizoe-old-sniffer-selected/logs`
- Scanned files from: `/Users/kyle/Developer/mxo-hd/research/packet-dumps/community-intake/20260607T030922Z-hdneo-public-luizoe-old-sniffer-selected/logs`
- Generated UTC: `2026-06-07T03:09:23Z`
- Max sample lines per pattern: `10`

Use this report to decide whether received logs are worth importing into `research/packet-dumps/`. Matches are byte-string leads, not decoded packet claims.

## Vendor And Interaction Leads

### SERVER_VENDOR_OPEN candidate

Signature: `81 0d` (spaced or compact hex)

Matches: 1

```text
mxopackets1_luizoe_old_sniffer_Unknown.log.txt:66584:04 82 80 9b 46 00 80 f7 43 81 0d 3c 45 0f 00 02 80 80 10 39 07 00 00
```

### CLIENT_VENDOR_BUY candidate

Signature: `81 0e` (spaced or compact hex)

Matches: 2

```text
mxopackets1_luizoe_old_sniffer_Unknown.log.txt:50240:02 03 20 00 01 00 04 72 81 0e 46 00 c0 5f 44 c9 a7 59 46 11 00 02 0c 74 44 92 00 46 00 c0 5f 44
mxopackets1_luizoe_old_sniffer_Unknown.log.txt:102506:02 03 0c 00 02 0e 01 e7 6f c7 f8 45 00 00 be 42 05 81 0e c6 0b 00 06 0c cc 4e 7e 0b 46 00 00 be
```

### CLIENT_VENDOR_SELL candidate

Signature: `81 11` (spaced or compact hex)

Matches: 22

```text
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:10406:81 11 81 47 05 00 02 08 28 6e 94 45 00 40 30 c4 ee a9 65 47 00 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:10415:c4 81 11 81 47 03 00 02 0c 22 4b 88 2a 46 00 40 30 c4 41 4e 6b 47 00 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:10428:f5 47 46 00 40 30 c4 81 11 81 47 03 00 02 0c 23 b7 cb 2a 46 00 40 30 c4 28 5d 6b 47 00 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:10442:46 00 40 30 c4 81 11 81 47 00 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:10451:91 45 00 40 30 c4 2b 71 6b 47 06 00 02 0c 6f 9c f5 47 46 00 40 30 c4 81 11 81 47 03 00 02 2e 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:10467:47 46 00 40 30 c4 81 11 81 47 00 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:10480:5b 9c f5 47 46 00 40 30 c4 81 11 81 47 00 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:10490:00 40 30 c4 3f 50 6b 47 06 00 02 0c 50 9c f5 47 46 00 40 30 c4 81 11 81 47 05 00 02 0c f2 28 6e
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:10508:02 03 06 00 02 0c 3c 9c f5 47 46 00 40 30 c4 81 11 81 47 00 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:10517:81 11 81 47 03 00 02 0a 00 a5 ef 2c 46 00 40 30 c4 39 d6 6b 47 00 00
```

### CR2 static object interaction

Signature: `80 c8` (spaced or compact hex)

Matches: 68

```text
mxopackets1_luizoe_old_sniffer_Recursion_Char.log.txt:37922:02 04 01 00 9b 01 08 80 c8 0f 00 10 0d 03 00
mxopackets1_luizoe_old_sniffer_Recursion_Char.log.txt:38322:02 04 01 00 9d 01 08 80 c8 a7 04 10 0d 03 00
mxopackets1_luizoe_old_sniffer_Recursion_Char.log.txt:38788:02 04 01 00 9f 01 08 80 c8 a6 04 10 0d 00 00
mxopackets1_luizoe_old_sniffer_Recursion_Char.log.txt:40778:02 04 01 00 a4 01 08 80 c8 a1 04 10 0d 00 00
mxopackets1_luizoe_old_sniffer_Recursion_Char.log.txt:41032:02 04 01 00 a9 01 08 80 c8 a1 04 10 0d 00 00
mxopackets1_luizoe_old_sniffer_Recursion_Char.log.txt:51022:02 04 01 00 da 01 08 80 c8 a1 04 10 0d 00 00
mxopackets1_luizoe_old_sniffer_Recursion.log.txt:35723:02 04 01 00 87 01 08 80 c8 02 00 50 19 03 00
mxopackets1_luizoe_old_sniffer_Recursion.log.txt:35833:02 04 01 00 88 01 08 80 c8 02 00 50 19 03 00
mxopackets1_luizoe_old_sniffer_Recursion.log.txt:36298:02 04 01 00 8c 01 08 80 c8 79 03 50 19 03 00
mxopackets1_luizoe_old_sniffer_Recursion.log.txt:36573:02 04 01 00 8e 01 08 80 c8 b8 03 50 19 00 00
```

### CR1 static object interaction

Signature: `80 c3` (spaced or compact hex)

Matches: 67

```text
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:4579:00 00 04 01 00 62 01 06 80 c3 9c c0 5d 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:25411:c9 db 01 ef 45 00 40 30 c4 c0 3a 55 47 00 00 04 01 00 fa 01 06 80 c3 3d dd 60 00
mxopackets1_luizoe_old_sniffer_Recursion_Char.log.txt:981:00 00 00 00 00 00 06 80 c3 06 7d 68 00
mxopackets1_luizoe_old_sniffer_Recursion_Char.log.txt:8878:43 43 54 46 20 17 17 c4 87 52 24 47 00 00 04 01 00 d7 01 06 80 c3 43 dc 6a 00
mxopackets1_luizoe_old_sniffer_Recursion_Char.log.txt:10115:20 06 13 c4 52 09 06 47 00 00 04 01 00 d8 01 06 80 c3 0c 14 6b 00
mxopackets1_luizoe_old_sniffer_Recursion_Char.log.txt:16798:80 c3 f6 7e 6c 00
mxopackets1_luizoe_old_sniffer_Recursion_Char.log.txt:21903:00 00 00 22 00 00 00 00 00 00 00 04 01 01 b9 01 06 80 c3 64 57 6d 00
mxopackets1_luizoe_old_sniffer_Recursion_Char.log.txt:24775:00 01 00 04 38 3f ae 46 3f 97 2b c4 ff 23 90 46 00 00 04 01 01 be 01 06 80 c3 08 b2 6d 00
mxopackets1_luizoe_old_sniffer_Recursion_Char.log.txt:27928:63 9a 46 00 00 04 01 01 bf 01 06 80 c3 7d 1e 6e 00
mxopackets1_luizoe_old_sniffer_Recursion_Char.log.txt:28347:9b 46 00 40 30 c4 00 cb 9d 46 00 00 04 01 01 c0 01 06 80 c3 b8 29 6e 00
```

### SERVER object/state bundle

Signature: `80 bc` (spaced or compact hex)

Matches: 2444

```text
mxopackets1_luizoe_old_sniffer_Syntax.log.txt:18:65 02 03 00 60 00 29 e6 0e 3b 4e 1f 9d ee 15 93 08 f8 db b0 3e ed 50 0d 12 e4 01 80 bc 83 9e 2b
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:208:51 00 05 00 08 02 08 80 b2 11 00 01 00 08 02 16 80 bc 45 03 11 00 00 02 00 00 00 11 00 01 00 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:209:00 00 00 00 00 00 16 80 bc 45 00 02 00 00 02 00 00 00 cc 00 00 00 00 00 00 00 00 00 00 16 80 bc
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:210:15 00 03 00 00 f7 03 00 00 08 02 00 00 00 00 00 00 00 00 00 16 80 bc 15 00 04 00 00 f7 03 00 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:211:07 02 ec ff ff ff 00 00 00 00 00 16 80 bc 15 00 05 00 00 f7 03 00 00 50 04 00 00 00 00 00 00 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:212:00 00 16 80 bc 15 00 06 00 00 f7 03 00 00 f4 03 00 00 00 00 00 00 00 00 00 16 80 bc 15 00 07 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:213:00 f7 03 00 00 51 04 f6 ff ff ff 00 00 00 00 00 16 80 bc 15 00 08 00 00 f7 03 00 00 52 04 0f 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:256:02 04 01 00 1d 0d 04 80 b3 11 00 16 80 bc 45 03 11 00 00 02 00 00 00 11 00 01 00 00 00 01 00 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:257:00 00 16 80 bc 45 00 02 00 00 02 00 00 00 cc 00 00 00 00 00 01 00 00 00 00 08 80 b2 11 00 01 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:258:08 02 16 80 bc 45 03 11 00 00 02 00 00 00 11 00 01 00 00 00 00 00 00 00 00 16 80 bc 55 00 09 00
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

Matches: 272

```text
mxopackets1_luizoe_old_sniffer_Recursion.log.txt:97321:f3 09 00 28 74 00 00 00 00 00 00 00 00 01 00 ff 76 0c 00 58 00 00 00 00 00 00 00 00 22 02 87 00
mxopackets1_luizoe_old_sniffer_Recursion.log.txt:97343:f3 09 00 28 74 00 00 00 00 00 00 00 00 01 00 ff 76 0c 00 58 00 00 00 00 00 00 00 00 22 02 87 00
mxopackets1_luizoe_old_sniffer_Recursion.log.txt:97360:00 00 00 00 01 00 ff 76 0c 00 58 00 00 00 00 00 00 00 00 22 02 87 00 00 00 00 01 00 12 00 02 0c
mxopackets1_luizoe_old_sniffer_Recursion.log.txt:97382:00 28 74 00 00 00 00 00 00 00 00 01 00 ff 76 0c 00 58 00 00 00 00 00 00 00 00 22 02 87 00 00 00
mxopackets1_luizoe_old_sniffer_Recursion.log.txt:97404:00 28 74 00 00 00 00 00 00 00 00 01 00 ff 76 0c 00 58 00 00 00 00 00 00 00 00 22 02 87 00 00 00
mxopackets1_luizoe_old_sniffer_Recursion.log.txt:99565:00 00 00 00 00 01 00 ff 51 0b 00 58 00 00 00 00 00 00 00 00 22 02 87 00 00 00 00 01 00 40 00 06
mxopackets1_luizoe_old_sniffer_Recursion.log.txt:99591:00 00 00 00 00 01 00 ff 51 0b 00 58 00 00 00 00 00 00 00 00 22 02 87 00 00 00 00 01 00 40 00 06
mxopackets1_luizoe_old_sniffer_Recursion.log.txt:99613:75 00 00 00 00 00 00 00 00 01 00 ff 51 0b 00 58 00 00 00 00 00 00 00 00 22 02 87 00 00 00 00 01
mxopackets1_luizoe_old_sniffer_Recursion.log.txt:99635:00 00 00 00 00 01 00 ff 51 0b 00 58 00 00 00 00 00 00 00 00 22 02 87 00 00 00 00 01 00 00 00
mxopackets1_luizoe_old_sniffer_Recursion.log.txt:99648:00 00 00 00 01 00 ff 51 0b 00 58 00 00 00 00 00 00 00 00 22 02 87 00 00 00 00 01 00 3c 00 02 0e
```

### Selector ff continuation prefix 00 01 04 ff

Signature: `00 01 04 ff` (spaced or compact hex)

Matches: 34

```text
mxopackets1_luizoe_old_sniffer_Recursion_Char.log.txt:26121:02 03 02 00 01 04 ff 00 00
mxopackets1_luizoe_old_sniffer_Recursion_Char.log.txt:26154:02 03 02 00 01 04 ff 00 00
mxopackets1_luizoe_old_sniffer_Recursion_Char.log.txt:27458:02 03 02 00 01 04 ff 00 00
mxopackets1_luizoe_old_sniffer_Recursion_Char.log.txt:35917:02 03 02 00 01 04 ff 00 00
mxopackets1_luizoe_old_sniffer_Recursion_Char.log.txt:36018:02 03 02 00 01 04 ff 00 00
mxopackets1_luizoe_old_sniffer_Recursion_Char.log.txt:39713:02 03 02 00 01 04 ff 00 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:4499:02 03 02 00 01 04 ff 00 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:5212:02 03 02 00 01 04 ff 00 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:28031:02 03 02 00 01 04 ff 00 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:38182:02 03 02 00 01 04 ff 00 00
```

### Selector ff continuation prefix 00 01 10 ff

Signature: `00 01 10 ff` (spaced or compact hex)

Matches: 131

```text
mxopackets1_luizoe_old_sniffer_Recursion.log.txt:52390:ff 00 00 00 00 c8 06 00 28 7e 00 00 00 00 00 00 00 00 01 10 ff bf 00 00 4e c6 19 01 00 00 00 08
mxopackets1_luizoe_old_sniffer_Recursion.log.txt:52442:00 00 00 00 c8 06 00 28 7e 00 00 00 00 00 00 00 00 01 10 ff bf 00 00 4e c6 19 01 00 00 00 08 00
mxopackets1_luizoe_old_sniffer_Recursion.log.txt:52480:00 00 00 c8 06 00 28 7e 00 00 00 00 00 00 00 00 01 10 ff bf 00 00 4e c6 19 01 00 00 00 08 00 12
mxopackets1_luizoe_old_sniffer_Recursion.log.txt:52518:00 00 b8 0a 00 91 59 00 00 ff ff 00 00 00 00 c8 06 00 28 7f 00 00 00 00 00 00 00 00 01 10 ff bf
mxopackets1_luizoe_old_sniffer_Recursion.log.txt:52580:00 00 00 00 00 00 00 00 01 10 ff bf 00 00 4e c6 19 01 00 00 00 08 00 12 06 a5 00 00 00 00 01 00
mxopackets1_luizoe_old_sniffer_Recursion.log.txt:52600:00 00 00 00 00 01 10 ff bf 00 00 4e c6 19 01 00 00 00 08 00 12 06 a5 00 00 00 00 01 00 00 00
mxopackets1_luizoe_old_sniffer_Recursion.log.txt:52621:00 00 00 00 01 10 ff bf 00 00 4e c6 19 01 00 00 00 08 00 12 06 a5 00 00 00 00 01 00 00 00
mxopackets1_luizoe_old_sniffer_Recursion.log.txt:52634:00 00 00 00 00 00 00 00 01 10 ff bf 00 00 4e c6 19 01 00 00 00 08 00 12 06 a5 00 00 00 00 01 00
mxopackets1_luizoe_old_sniffer_Recursion.log.txt:52728:28 81 00 00 00 00 00 00 00 00 01 10 ff bf 00 00 4e c6 19 01 00 00 00 08 00 12 06 a5 00 00 00 00
mxopackets1_luizoe_old_sniffer_Recursion.log.txt:52742:00 00 00 00 01 10 ff bf 00 00 4e c6 19 01 00 00 00 08 00 12 06 a5 00 00 00 00 01 00 00 00
```

### Selector ff continuation prefix 00 00 00 ff

Signature: `00 00 00 ff` (spaced or compact hex)

Matches: 13738

```text
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:847:cc 64 5d 25 00 99 0e c0 00 00 01 00 00 00 00 00 ff b9 00 00 03 00 4c 01 03 ff 96 00 01 00 00 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:850:00 00 00 00 00 00 ff 00 00 00 00 01 00 00 00 00 00 00 00 80 3f 11 00 00 00 00 00 00 00 00 00 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:884:0e c0 00 00 01 00 00 00 00 00 ff b9 00 00 03 00 4c 01 03 ff 96 00 01 00 00 00 00 00 00 00 00 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:885:00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ff 00 00 00 00 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:921:0e c0 00 00 01 00 00 00 00 00 ff b9 00 00 03 00 4c 01 03 ff 96 00 01 00 00 00 00 00 00 00 00 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:922:00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ff 00 00 00 00 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:964:80 10 42 08 cc 64 5d 25 00 99 0e c0 00 00 01 00 00 00 00 00 ff b9 00 00 03 00 4c 01 03 ff 96 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:966:00 00 00 00 ff 00 00 00 00 00 64 00 00 00 64 00 00 00 00 00 96 00 ff 00 00 00 00 00 00 00 00 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:967:00 00 00 00 00 00 00 00 00 00 ff 00 00 00 00 01 00 00 00 00 00 00 00 80 3f 11 00 00 00 00 00 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:982:0e c0 00 00 01 00 00 00 00 00 ff b9 00 00 03 00 4c 01 03 ff 96 00 01 00 00 00 00 00 00 00 00 00
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

Matches: 580

```text
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:949:02 03 01 00 0c 57 02 86 cd ab 12 8e 41 6d 6d 69 74 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:3988:02 03 01 00 0c 57 02 ce cd ab 11 8e 43 68 6f 70 70 65 72 20 52 75 6e 6e 65 72 00 00 00 00 00 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:3991:32 09 4b 00 07 08 00 00 21 00 00 01 00 0c 57 02 cc cd ab 12 8e 43 68 6f 70 70 65 72 20 52 75 6e
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:3994:c0 01 8b 01 03 15 a8 01 4b 00 32 09 4b 00 f9 07 00 00 1f 00 00 01 00 0c 57 02 cf cd ab 11 8e 43
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:3998:0c 57 02 d0 cd ab 11 8e 43 68 6f 70 70 65 72 20 4a 75 6d 70 65 72 00 00 00 00 00 00 00 00 00 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:4027:00 39 80 47 01 00 0c 57 02 d1 cd ab 10 8e 43 68 6f 70 70 65 72 20 52 75 6e 6e 65 72 00 00 00 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:4030:00 32 09 4b 00 07 08 00 00 19 00 00 01 00 0c 57 02 d2 cd ab 11 8e 43 68 6f 70 70 65 72 20 4a 75
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:4033:c0 01 8b 05 02 0c a8 01 70 00 32 09 70 00 f9 07 00 00 17 00 00 01 00 0c 57 02 c8 cd ab 12 8e 43
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:4037:00 0c 57 02 cb cd ab 12 8e 43 68 6f 70 70 65 72 20 52 75 6e 6e 65 72 00 00 00 00 00 00 00 00 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:4065:17 00 02 0c e7 3d 9d 46 46 00 40 30 c4 4d a8 78 47 01 00 0c 57 02 d3 cd ab 10 8e 43 68 6f 70 70
```

### Adjacent Object599 boundary prefix

Signature: `00 0c 57 02` (spaced or compact hex)

Matches: 568

```text
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:949:02 03 01 00 0c 57 02 86 cd ab 12 8e 41 6d 6d 69 74 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:3988:02 03 01 00 0c 57 02 ce cd ab 11 8e 43 68 6f 70 70 65 72 20 52 75 6e 6e 65 72 00 00 00 00 00 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:3991:32 09 4b 00 07 08 00 00 21 00 00 01 00 0c 57 02 cc cd ab 12 8e 43 68 6f 70 70 65 72 20 52 75 6e
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:3994:c0 01 8b 01 03 15 a8 01 4b 00 32 09 4b 00 f9 07 00 00 1f 00 00 01 00 0c 57 02 cf cd ab 11 8e 43
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:4027:00 39 80 47 01 00 0c 57 02 d1 cd ab 10 8e 43 68 6f 70 70 65 72 20 52 75 6e 6e 65 72 00 00 00 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:4030:00 32 09 4b 00 07 08 00 00 19 00 00 01 00 0c 57 02 d2 cd ab 11 8e 43 68 6f 70 70 65 72 20 4a 75
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:4033:c0 01 8b 05 02 0c a8 01 70 00 32 09 70 00 f9 07 00 00 17 00 00 01 00 0c 57 02 c8 cd ab 12 8e 43
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:4037:00 0c 57 02 cb cd ab 12 8e 43 68 6f 70 70 65 72 20 52 75 6e 6e 65 72 00 00 00 00 00 00 00 00 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:4065:17 00 02 0c e7 3d 9d 46 46 00 40 30 c4 4d a8 78 47 01 00 0c 57 02 d3 cd ab 10 8e 43 68 6f 70 70
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:4068:30 7b 68 ee 40 c0 01 8a 02 12 a8 01 70 00 32 09 70 00 07 08 00 00 0f 00 00 01 00 0c 57 02 d4 cd
```

### Header-like selector 50

Signature: `01 00 08 50` (spaced or compact hex)

Matches: 10

```text
mxopackets1_luizoe_old_sniffer_Recursion_Char.log.txt:31408:00 00 00 00 80 6f ca 40 00 00 00 00 f2 04 35 bf 00 00 00 00 f3 04 35 3f 14 00 00 01 00 08 50 02
mxopackets1_luizoe_old_sniffer_Unknown.log.txt:22241:c0 00 00 00 00 00 90 87 c0 00 00 00 00 00 f5 de 40 1a 00 00 01 00 08 50 02 f5 06 50 48 9b cd ab
mxopackets1_luizoe_old_sniffer_Unknown.log.txt:38531:02 03 01 00 08 50 02 88 06 e0 4b 41 cd ab 02 09 00 00 00 00 00 dc be 40 00 00 00 00 00 40 60 40
mxopackets1_luizoe_old_sniffer_Unknown.log.txt:54984:00 00 00 22 00 00 00 00 00 00 00 01 00 08 50 02 ea 01 30 39 3b cd ab 01 01 00 00 00 00 00 9a d0
mxopackets1_luizoe_old_sniffer_Unknown.log.txt:82813:02 03 01 00 08 50 02 ea 01 30 39 3b cd ab 01 01 00 00 00 00 00 9a d0 40 00 00 00 00 00 90 80 40
mxopackets1_luizoe_old_sniffer_Unknown.log.txt:106987:fc 16 7e 00 1c 03 84 00 00 00 81 ff 24 00 00 01 00 08 50 02 ea 01 30 39 3b cd ab 01 01 00 00 00
mxopackets1_luizoe_old_sniffer_Recursion.log.txt:31116:35 3f 05 00 00 01 00 08 50 02 ff 1a 30 49 4a cd ab 02 09 00 00 00 00 40 00 ea 40 00 00 00 00 00
mxopackets1_luizoe_old_sniffer_Recursion.log.txt:57468:00 80 02 01 21 00 00 01 00 08 50 02 7d 02 b0 4a 12 cd ab 02 09 00 00 00 00 80 e7 ea 40 00 00 00
mxopackets1_luizoe_old_sniffer_Recursion.log.txt:74132:01 b8 40 c0 01 8a 03 0c a8 06 c8 00 b2 09 c8 00 07 08 00 00 80 02 01 3c 00 00 01 00 08 50 02 7d
mxopackets1_luizoe_old_sniffer_Recursion.log.txt:97088:09 96 00 07 08 00 00 80 02 01 1c 00 00 01 00 08 50 02 7d 02 b0 4a 12 cd ab 02 09 00 00 00 00 80
```

### Header-like selector 67

Signature: `01 00 08 67` (spaced or compact hex)

Matches: 9

```text
mxopackets1_luizoe_old_sniffer_Recursion_Char.log.txt:37947:02 03 02 00 01 08 fe 95 b9 46 00 80 f7 43 00 10 4d 46 94 48 55 11 01 00 08 67 01 0f 00 10 0d ef
mxopackets1_luizoe_old_sniffer_Unknown.log.txt:42440:02 03 01 00 08 67 01 08 00 e0 4b 47 cd ab 03 84 00 00 00 00 00 00 80 3f 00 00 00 00 00 00 00 00
mxopackets1_luizoe_old_sniffer_Unknown.log.txt:51341:02 03 01 00 08 67 01 08 00 e0 4b 59 cd ab 03 84 00 00 00 00 00 00 80 3f 00 00 00 00 00 00 00 00
mxopackets1_luizoe_old_sniffer_Unknown.log.txt:91760:02 03 01 00 08 67 01 0d 00 80 14 c5 cd ab 03 84 00 00 00 00 00 00 80 3f 00 00 00 00 00 00 00 00
mxopackets1_luizoe_old_sniffer_Unknown.log.txt:95586:02 03 02 00 01 08 ff dd 26 46 00 00 be 42 84 83 18 c6 11 80 76 11 01 00 08 67 01 0d 00 80 14 da
mxopackets1_luizoe_old_sniffer_Recursion.log.txt:67822:a8 06 2c 01 b2 09 2c 01 f9 07 00 00 80 02 01 04 00 00 01 00 08 67 01 03 00 50 54 2f cd ab 03 84
mxopackets1_luizoe_old_sniffer_Recursion.log.txt:74167:08 00 00 37 00 00 01 00 08 67 01 01 00 50 54 4b cd ab 03 84 00 00 00 00 00 00 80 3f 00 00 00 00
mxopackets1_luizoe_old_sniffer_Recursion.log.txt:88212:02 03 01 00 08 67 01 10 00 30 02 a2 cd ab 03 84 00 00 00 00 f3 04 35 bf 00 00 00 00 f3 04 35 3f
mxopackets1_luizoe_old_sniffer_Recursion.log.txt:94984:02 03 01 00 08 67 01 10 00 30 02 c6 cd ab 03 84 00 00 00 00 f3 04 35 bf 00 00 00 00 f3 04 35 3f
```

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
mxopackets1_luizoe_old_sniffer_Recursion.log.txt:38657:02 03 01 00 08 a3 01 d4 03 50 19 96 cd ab 03 88 00 00 00 00 f2 04 35 bf 00 00 00 00 f3 04 35 3f
mxopackets1_luizoe_old_sniffer_Recursion.log.txt:38659:01 00 08 a3 01 d1 03 50 19 95 cd ab 03 88 00 00 00 00 f2 04 35 bf 00 00 00 00 f3 04 35 3f 22 00
mxopackets1_luizoe_old_sniffer_Recursion.log.txt:38662:00 00 58 eb 40 00 00 00 00 00 22 aa 40 00 00 00 00 00 fb dd 40 ff ff ff ff 12 00 00 01 00 08 a3
mxopackets1_luizoe_old_sniffer_Recursion.log.txt:41826:02 03 02 00 02 80 80 80 50 62 00 c2 00 01 00 08 a3 01 d4 03 50 19 96 cd ab 03 88 00 00 00 00 f2
mxopackets1_luizoe_old_sniffer_Recursion.log.txt:41828:00 fb dd 40 ff ff ff ff 10 00 00 01 00 08 a3 01 ce 03 50 19 93 cd ab 03 88 00 00 00 00 f2 04 35
mxopackets1_luizoe_old_sniffer_Recursion.log.txt:41832:18 00 00 01 00 08 a3 01 d1 03 50 19 95 cd ab 03 88 00 00 00 00 f2 04 35 bf 00 00 00 00 f3 04 35
mxopackets1_luizoe_old_sniffer_Recursion.log.txt:41835:00 00 00 00 00 dc de 40 0b 00 00 01 00 08 a3 01 d7 03 50 19 aa cd ab 03 88 00 00 00 00 f2 04 35
mxopackets1_luizoe_old_sniffer_Recursion.log.txt:43951:02 03 01 00 08 a3 01 e0 03 50 19 19 cd ab 03 88 00 00 00 00 f2 04 35 bf 00 00 00 00 f3 04 35 3f
mxopackets1_luizoe_old_sniffer_Recursion.log.txt:48279:02 03 01 00 08 a3 01 65 0f 60 53 6f cd ab 02 80 82 00 00 00 00 80 b4 e6 40 00 00 00 00 00 20 62
mxopackets1_luizoe_old_sniffer_Recursion.log.txt:48580:00 c2 a0 40 00 00 00 00 00 4a d5 40 29 00 00 01 00 08 a3 01 53 00 60 53 c9 cd ab 03 88 00 00 00
```

### Header-like selector d0

Signature: `01 00 08 d0` (spaced or compact hex)

Matches: 62

```text
mxopackets1_luizoe_old_sniffer_Recursion_Char.log.txt:24005:02 03 01 00 08 d0 20 b4 00 20 4e 1d cd ab 08 fd 00 00 00 00 40 1e d0 40 00 00 00 00 00 20 7f 40
mxopackets1_luizoe_old_sniffer_Recursion_Char.log.txt:24465:02 03 01 00 08 d0 20 d2 04 10 0d 1f cd ab 08 fd 00 00 00 00 40 5f da 40 00 00 00 00 00 20 7f 40
mxopackets1_luizoe_old_sniffer_Recursion_Char.log.txt:29051:82 02 f1 8d 48 03 01 00 08 d0 20 b4 00 20 4e 1d cd ab 08 fd 00 00 00 00 40 1e d0 40 00 00 00 00
mxopackets1_luizoe_old_sniffer_Recursion_Char.log.txt:33684:02 03 01 00 08 d0 20 b4 00 20 4e 1d cd ab 08 fd 00 00 00 00 40 1e d0 40 00 00 00 00 00 20 7f 40
mxopackets1_luizoe_old_sniffer_Recursion_Char.log.txt:35049:02 03 01 00 08 d0 20 af 00 20 4e 2c cd ab 08 fd 00 00 00 00 c0 a8 d4 40 00 00 00 00 00 f0 7e 40
mxopackets1_luizoe_old_sniffer_Recursion_Char.log.txt:35645:00 00 22 00 00 00 00 00 00 00 01 00 08 d0 19 d4 04 10 0d e3 cd ab 01 01 00 00 00 00 c0 00 d7 40
mxopackets1_luizoe_old_sniffer_Recursion_Char.log.txt:36817:00 00 22 00 00 00 00 00 00 00 01 00 08 d0 20 0e 00 c0 1f 39 cd ab 08 fd 00 00 00 00 c0 6e d7 40
mxopackets1_luizoe_old_sniffer_Recursion_Char.log.txt:36821:00 f2 04 35 3f 00 00 00 00 f3 04 35 3f 0b 00 00 01 00 08 d0 20 af 00 20 4e 2c cd ab 08 fd 00 00
mxopackets1_luizoe_old_sniffer_Recursion_Char.log.txt:37200:82 f4 fc 8d 48 03 01 00 08 d0 20 d2 04 10 0d 1f cd ab 08 fd 00 00 00 00 40 5f da 40 00 00 00 00
mxopackets1_luizoe_old_sniffer_Recursion_Char.log.txt:37460:02 03 01 00 08 d0 20 b0 00 20 4e 2a cd ab 08 fd 00 00 00 00 c0 05 d2 40 00 00 00 00 00 f0 7e 40
```

## Movement And Object-State Leads

### Mode 06 movement selector 08

Signature: `00 06 08` (spaced or compact hex)

Matches: 361

```text
mxopackets1_luizoe_old_sniffer_Recursion_Char.log.txt:1372:02 03 11 00 06 08 e0 16 b2 45 00 40 30 c4 9d d5 2c 47 40 00 09 00 06 08 47 c9 ae 45 00 40 30 c4
mxopackets1_luizoe_old_sniffer_Recursion_Char.log.txt:40246:02 03 0d 00 02 0e 00 06 08 fa b1 46 00 e0 a1 44 17 01 51 46 05 00 01 00 04 7b db b8 46 00 e0 a1
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:8357:d0 1d b5 e1 45 00 40 30 c4 0c a3 82 47 05 00 06 08 2c 2b ba 45 00 40 30 c4 a3 c9 67 47 ff 00 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:9707:02 03 02 00 02 80 80 80 40 c8 00 18 00 06 08 d4 da a2 45 00 40 30 c4 a6 6a 6c 47 ff 00 00 00 10
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:9728:02 03 18 00 06 08 d4 da a2 45 00 40 30 c4 a6 6a 6c 47 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:9744:02 03 02 00 02 80 84 22 80 40 c8 00 18 00 06 08 d4 da a2 45 00 40 30 c4 a6 6a 6c 47 ff 00 00 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:9765:02 03 18 00 06 08 d4 da a2 45 00 40 30 c4 a6 6a 6c 47 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:9785:82 56 6f 8d 48 03 18 00 06 08 d4 da a2 45 00 40 30 c4 a6 6a 6c 47 ff 00 00 00 10 00 00 00 00 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:9805:02 03 1f 00 02 0a 00 58 94 33 46 00 40 30 c4 fb 15 79 47 18 00 06 08 d4 da a2 45 00 40 30 c4 a6
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:11421:97 e7 6a 47 0c 00 02 0c 46 79 a9 ed 45 00 40 30 c4 af 8d 82 47 05 00 06 08 42 5c 8e 45 00 40 30
```

### Mode 06 movement selector 0a

Signature: `00 06 0a` (spaced or compact hex)

Matches: 72

```text
mxopackets1_luizoe_old_sniffer_Recursion_Char.log.txt:17356:02 03 01 00 02 1e 00 15 00 06 0a 00 7e 4b 98 46 00 40 30 c4 79 16 99 46 80 80 80 80 80 80 01 43
mxopackets1_luizoe_old_sniffer_Recursion_Char.log.txt:28442:22 00 00 00 00 00 00 00 1b 00 06 0a 00 8a 49 98 46 00 40 30 c4 44 1c 99 46 80 80 80 80 80 80 01
mxopackets1_luizoe_old_sniffer_Recursion_Char.log.txt:36195:82 d4 fb 8d 48 03 1b 00 06 0a 00 f0 e8 9f 46 00 40 30 c4 5f 6b 96 46 80 80 80 80 80 80 01 43 21
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:13657:02 03 02 00 03 21 00 40 00 00 00 10 0c 00 80 80 80 40 c6 00 13 00 06 0a 00 70 7e 0b 46 00 40 30
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:13670:02 03 1d 00 02 08 60 d8 cc c3 00 40 30 c4 3b de 80 47 13 00 06 0a 00 70 7e 0b 46 00 40 30 c4 b6
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:13692:00 00 00 3f 00 00 10 00 22 00 00 00 00 00 00 00 13 00 06 0a 00 70 7e 0b 46 00 40 30 c4 b6 85 73
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:13777:02 03 1d 00 02 08 00 0e da c3 00 40 30 c4 49 37 81 47 13 00 06 0a 01 4a 54 0b 46 00 40 30 c4 f0
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:16141:00 22 00 00 00 00 00 0a 00 06 0a 05 c9 7a ec 45 00 40 30 c4 1c 68 58 47 ff 00 00 00 10 00 00 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:28769:02 03 20 00 06 0a 00 29 cd 46 46 00 40 30 c4 57 af 78 47 80 80 80 80 80 80 01 39 21 00 00 00 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:36823:01 07 08 00 00 1d 00 06 0a 01 00 9e f5 45 00 40 30 c4 d6 bd 58 47 80 80 80 80 80 80 01 07 08 00
```

### Mode 06 movement selector 0c

Signature: `00 06 0c` (spaced or compact hex)

Matches: 1671

```text
mxopackets1_luizoe_old_sniffer_Recursion_Char.log.txt:917:02 03 10 00 02 0c 84 72 34 1a 46 00 40 30 c4 04 ac 2c 47 08 00 06 0c ab dc ae 3b 46 00 40 30 c4
mxopackets1_luizoe_old_sniffer_Recursion_Char.log.txt:995:02 03 04 00 06 0c 97 d4 41 f8 45 00 40 30 c4 7f c4 31 47 ff 00 00 00 10 00 00 00 00 00 01 ff 00
mxopackets1_luizoe_old_sniffer_Recursion_Char.log.txt:1010:40 30 c4 b4 e8 29 47 04 00 06 0c a1 d4 41 f8 45 00 40 30 c4 7f c4 31 47 ff 00 00 00 10 00 00 00
mxopackets1_luizoe_old_sniffer_Recursion_Char.log.txt:1031:b4 e8 29 47 04 00 06 0c ab d4 41 f8 45 00 40 30 c4 7f c4 31 47 ff 00 00 00 10 00 00 00 00 00 01
mxopackets1_luizoe_old_sniffer_Recursion_Char.log.txt:1052:00 40 30 c4 33 87 2d 47 04 00 06 0c b4 d4 41 f8 45 00 40 30 c4 7f c4 31 47 ff 00 00 00 10 00 00
mxopackets1_luizoe_old_sniffer_Recursion_Char.log.txt:1072:30 c4 c8 41 34 47 09 00 02 0e 00 c5 47 c9 ae 45 00 40 30 c4 b4 e8 29 47 04 00 06 0c c4 d4 41 f8
mxopackets1_luizoe_old_sniffer_Recursion_Char.log.txt:1093:c4 c8 41 34 47 09 00 02 0e 00 cf 47 c9 ae 45 00 40 30 c4 b4 e8 29 47 04 00 06 0c cf d4 41 f8 45
mxopackets1_luizoe_old_sniffer_Recursion_Char.log.txt:1936:02 03 04 00 06 0c 51 b5 e6 e9 45 00 40 30 c4 7e fd 34 47 80 80 80 80 80 80 01 12 08 00 00 00 00
mxopackets1_luizoe_old_sniffer_Recursion_Char.log.txt:1945:00 06 0c 56 5f 70 06 46 00 40 30 c4 e9 29 35 47 80 80 80 80 80 80 01 12 08 00 00 0a 00 06 0c 54
mxopackets1_luizoe_old_sniffer_Recursion_Char.log.txt:2013:00 06 0e 05 4c 5f 70 06 46 00 40 30 c4 e9 29 35 47 80 80 80 80 80 80 01 07 08 00 00 0a 00 06 0c
```

### Mode 06 movement selector 0e

Signature: `00 06 0e` (spaced or compact hex)

Matches: 4238

```text
mxopackets1_luizoe_old_sniffer_Recursion_Char.log.txt:770:02 03 10 00 06 0e 00 94 72 34 1a 46 00 40 30 c4 04 ac 2c 47 80 80 80 80 80 80 01 12 08 00 00 0d
mxopackets1_luizoe_old_sniffer_Recursion_Char.log.txt:779:02 03 11 00 02 08 c4 b1 a7 45 00 40 30 c4 d5 4b 2c 47 10 00 06 0e 00 94 72 34 1a 46 00 40 30 c4
mxopackets1_luizoe_old_sniffer_Recursion_Char.log.txt:785:02 46 00 40 30 c4 e9 6c 36 47 0d 00 02 0e 01 1c 7e 58 ba 45 00 40 30 c4 2b e2 2a 47 04 00 06 0e
mxopackets1_luizoe_old_sniffer_Recursion_Char.log.txt:801:02 03 11 00 02 08 2a 58 a8 45 00 40 30 c4 72 54 2c 47 10 00 06 0e 00 94 72 34 1a 46 00 40 30 c4
mxopackets1_luizoe_old_sniffer_Recursion_Char.log.txt:808:02 46 00 40 30 c4 21 4a 35 47 04 00 06 0e 00 6c d4 41 f8 45 00 40 30 c4 7f c4 31 47 ff 00 00 00
mxopackets1_luizoe_old_sniffer_Recursion_Char.log.txt:943:f0 50 2a 47 04 00 06 0e 04 7c d4 41 f8 45 00 40 30 c4 7f c4 31 47 ff 00 00 00 10 00 00 00 00 00
mxopackets1_luizoe_old_sniffer_Recursion_Char.log.txt:959:3f 9f 34 47 09 00 02 0c a2 60 78 b2 45 00 40 30 c4 f0 50 2a 47 04 00 06 0e 04 81 d4 41 f8 45 00
mxopackets1_luizoe_old_sniffer_Recursion_Char.log.txt:974:02 03 12 00 02 0c a6 53 cf 7f 45 00 40 30 c4 ce a6 2c 47 04 00 06 0e 04 8c d4 41 f8 45 00 40 30
mxopackets1_luizoe_old_sniffer_Recursion_Char.log.txt:1118:c4 c8 41 34 47 09 00 02 0e 00 d1 47 c9 ae 45 00 40 30 c4 b4 e8 29 47 04 00 06 0e 01 da d4 41 f8
mxopackets1_luizoe_old_sniffer_Recursion_Char.log.txt:1138:c8 41 34 47 04 00 06 0e 01 e4 09 c7 f7 45 00 40 30 c4 07 d5 31 47 ff 00 00 00 10 00 00 00 00 00
```

### Repeated mode 06 suffix lead

Signature: `ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00` (spaced or compact hex)

Matches: 2012

```text
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:4762:cb 3c 8e 48 46 00 40 30 c4 6d c5 6e 47 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:4862:06 0e 01 f2 3c 8e 48 46 00 40 30 c4 6d c5 6e 47 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:4904:84 48 46 00 40 30 c4 0e f2 6e 47 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:4955:84 48 46 00 40 30 c4 0e f2 6e 47 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:4983:5a 84 48 46 00 40 30 c4 0e f2 6e 47 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:5018:30 c4 fa 9b 74 47 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:5049:77 72 47 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:5819:c4 0e f2 6e 47 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:5945:fa 9b 74 47 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:5996:46 00 40 30 c4 fa 9b 74 47 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00
```

### Movement-state ff 01 64 01 tail

Signature: `ff 01 64 01` (spaced or compact hex)

Matches: 0

### Movement-state ff 01 4e 01 tail

Signature: `ff 01 4e 01` (spaced or compact hex)

Matches: 0

## Ability, Effect, And Combat-State Leads

### SERVER_PLAYER_ATTRIBUTE / ability load

Signature: `80 b2` (spaced or compact hex)

Matches: 75

```text
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:206:6f 78 00 0a 80 e5 00 00 00 00 00 00 00 00 0a 80 e4 e8 03 00 00 00 00 00 00 08 80 b2 4e 00 08 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:207:08 02 08 80 b2 52 00 0b 00 08 02 08 80 b2 54 00 08 00 08 02 08 80 b2 4f 00 08 00 08 02 08 80 b2
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:208:51 00 05 00 08 02 08 80 b2 11 00 01 00 08 02 16 80 bc 45 03 11 00 00 02 00 00 00 11 00 01 00 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:257:00 00 16 80 bc 45 00 02 00 00 02 00 00 00 cc 00 00 00 00 00 01 00 00 00 00 08 80 b2 11 00 01 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:810:00 00 00 00 16 80 bc 55 00 10 00 00 02 00 00 00 cc 00 00 00 00 00 01 00 00 00 00 08 80 b2 11 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:860:62 75 74 74 6f 6e 00 08 80 b2 bb 01 01 00 08 02 16 80 bc 15 00 12 00 00 11 00 00 00 ab 00 02 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:895:bd 05 11 00 00 00 00 00 00 01 08 80 b2 cc 01 01 00 08 02 16 80 bc 4d 00 24 00 00 cc 01 00 00 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:3710:02 04 01 00 05 07 16 80 bc 55 00 3e 00 00 02 00 00 00 cc 00 00 00 00 00 01 00 00 00 00 08 80 b2
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:3947:02 04 01 00 25 20 08 80 b2 11 00 01 00 08 02 16 80 bc 45 03 11 00 00 02 00 00 00 11 00 01 00 00
mxopackets1_luizoe_old_sniffer_Recursion_Char.log.txt:240:4f 0b 00 00 00 00 00 00 08 80 b2 4e 00 08 00 08 02 08 80 b2 52 00 0b 00 08 02 08 80 b2 54 00 08
```

### SERVER_ABILITY_UNLOAD

Signature: `80 b3` (spaced or compact hex)

Matches: 36

```text
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:256:02 04 01 00 1d 0d 04 80 b3 11 00 16 80 bc 45 03 11 00 00 02 00 00 00 11 00 01 00 00 00 01 00 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:283:00 00 01 00 00 00 00 04 80 b3 11 00 16 80 bc 45 03 11 00 00 02 00 00 00 11 00 01 00 00 00 01 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:809:00 00 00 04 02 00 0e 18 04 80 b3 11 00 16 80 bc 45 03 11 00 00 02 00 00 00 11 00 01 00 00 00 01
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:3579:82 7b 61 8d 48 04 01 00 79 21 04 80 b3 11 00 16 80 bc 15 00 26 00 00 11 00 00 00 ab 00 02 00 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:3599:15 00 37 00 00 11 00 00 00 f3 03 00 00 00 00 01 00 00 00 00 04 80 b3 bb 01 04 80 b3 cc 01 16 80
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:3783:00 01 00 00 00 00 04 80 b3 11 00 16 80 bc 45 03 11 00 00 02 00 00 00 11 00 01 00 00 00 01 00 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:3934:11 00 00 00 f3 03 00 00 00 00 01 00 00 00 00 04 80 b3 11 00 16 80 bc 45 03 11 00 00 02 00 00 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:22915:00 00 9e f7 cb ec 40 02 00 00 00 00 00 00 00 01 01 02 00 00 00 00 c0 e2 55 42 00 00 00 00 80 b3
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:22916:34 42 c0 e2 55 c2 00 00 00 00 80 b3 34 c2 00 00 02 00 c8 0e 00 00 4e 01 a0 0f ec 10 02 9b 59 00
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:23324:cb ec 40 03 00 00 00 00 00 00 00 01 01 02 00 00 00 00 c0 e2 55 42 00 00 00 00 80 b3 34 42 c0 e2
```

### Object599 adrenaline booster effect id

Signature: `4c 0a 00 28` (spaced or compact hex)

Matches: 0

### Object599 fast healing effect id

Signature: `b5 07 00 28` (spaced or compact hex)

Matches: 9

```text
mxopackets1_luizoe_old_sniffer_Unknown.log.txt:70673:0a 0c b5 07 00 28 b8 14 00 01 0e 01 82 9e c8 84 46 00 80 f7 43 7d 18 48 45 0c 00 01 0c e8 10 d8
mxopackets1_luizoe_old_sniffer_Unknown.log.txt:70687:ff 00 00 00 00 00 b5 07 00 28 b8 00 00 00 00 00 00 00 00 01 01 ff 00 00 00 00 dd 37 00 00 00 00
mxopackets1_luizoe_old_sniffer_Unknown.log.txt:70732:00 00 00 00 00 00 00 4f 0a 00 12 4e 00 00 ff 00 00 00 00 00 b5 07 00 28 b8 00 00 00 00 00 00 00
mxopackets1_luizoe_old_sniffer_Unknown.log.txt:72658:02 03 26 00 01 0e 04 48 1b b1 80 46 00 80 f7 43 68 b1 17 45 15 00 02 80 80 80 0c b5 07 00 28 97
mxopackets1_luizoe_old_sniffer_Unknown.log.txt:115859:82 73 83 bc 48 03 1f 00 02 80 80 80 0c b5 07 00 28 03 18 00 01 0c e5 88 7c 8a 46 00 80 f7 43 97
mxopackets1_luizoe_old_sniffer_Unknown.log.txt:115926:00 46 77 00 00 ff ff 00 00 00 00 b5 07 00 28 03 00 00 00 00 00 00 00 00 00 00 ff a1 05 00 58 bf
mxopackets1_luizoe_old_sniffer_Unknown.log.txt:116004:b5 07 00 28 03 00 00 00 00 00 00 00 00 00 00 ff a1 05 00 58 bf 0a 01 00 00 00 08 00 12 00 00 00
mxopackets1_luizoe_old_sniffer_Unknown.log.txt:116019:00 00 00 00 ff 00 00 00 00 00 00 00 00 00 00 28 0a 00 46 77 00 00 ff ff 00 00 00 00 b5 07 00 28
mxopackets1_luizoe_old_sniffer_Unknown.log.txt:116060:00 00 00 00 ff 00 00 00 00 00 00 00 00 00 00 28 0a 00 46 77 00 00 ff ff 00 00 00 00 b5 07 00 28
```

### Object599 accelerated spawn effect id

Signature: `66 00 00 04` (spaced or compact hex)

Matches: 3

```text
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:5527:02 03 02 00 01 04 66 00 00 04 01 00 1d 04 12 80 c9 07 00 b0 47 00 d4 ad 47 00 00 20 41 00 f0 d2
mxopackets1_luizoe_old_sniffer_Cpy_of_recursion.log.txt:25743:02 03 02 00 01 04 66 00 00 04 01 00 3c 01 06 81 51 18 00 24 00
mxopackets1_luizoe_old_sniffer_Recursion.log.txt:31315:02 03 02 00 01 06 03 66 00 00 04 01 00 70 01 12 80 c9 66 1b b0 2a 00 7d 66 47 00 80 f7 43 00 0f
```

## Intake Next Steps

- Preserve the original files separately before editing or importing anything.
- Inspect matches with surrounding lines; embedded hits inside encrypted or unrelated bodies are leads only.
- Record sender, provenance, CR1/CR2 status, action context, character handle, coordinates, known object ids, and known item GoIDs.
- Import only text logs or text-like dumps that contain no passwords, session tokens, private keys, client binaries, or unrelated game assets.
