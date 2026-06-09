# Packet Capture Triage Report

- Source kind: `directory`
- Source input: `/Users/kyle/Developer/mxo-hd/research/packet-dumps/community-intake/20260608T065306Z-hdneo-public-luizoe-remaining-trade-sanitized-selected/logs`
- Scanned files from: `/Users/kyle/Developer/mxo-hd/research/packet-dumps/community-intake/20260608T065306Z-hdneo-public-luizoe-remaining-trade-sanitized-selected/logs`
- Generated UTC: `2026-06-08T06:53:08Z`
- Max sample lines per pattern: `10`

Use this report to decide whether received logs are worth importing into `research/packet-dumps/`. Matches are byte-string leads, not decoded packet claims.

## Vendor And Interaction Leads

### SERVER_VENDOR_OPEN candidate

Signature: `81 0d` (spaced or compact hex)

Matches: 0

### CLIENT_VENDOR_BUY candidate

Signature: `81 0e` (spaced or compact hex)

Matches: 1

```text
mxopackets1_luizoe_recursion2.log.txt:5926:02 03 02 00 03 28 04 c0 00 80 85 00 00 74 a4 c7 00 00 be 42 00 86 88 c7 81 0e a0 06 ff 01 01 01 
```

### CLIENT_VENDOR_SELL candidate

Signature: `81 11` (spaced or compact hex)

Matches: 1

```text
mxopackets1_luizoe_recursion2.log.txt:6150:c0 d0 0c 01 83 14 01 a8 4f ec 00 b2 04 d2 0f 06 08 00 00 81 11 ba 00 00 02 13 0d 00 00 01 00 0c 
```

### CR2 static object interaction

Signature: `80 c8` (spaced or compact hex)

Matches: 3

```text
mxopackets1_luizoe_recursion2.log.txt:3504:02 03 02 00 01 0e 00 3c b4 3a aa c6 f8 df ec 44 5c 8d 90 c6 00 00 04 01 00 4f 01 08 80 c8 a0 21 
mxopackets1_luizoe_any_things.log.txt:3082:02 03 02 00 01 0e 00 86 d2 8f b2 c7 00 e0 a1 44 dc 90 52 c6 00 00 04 01 00 6b 01 08 80 c8 a9 36 
mxopackets1_luizoe_margins_Syntax.log.txt:7140:02 03 02 00 01 0e 00 0c 10 cd de 45 00 b0 79 45 da e9 76 c6 00 00 04 01 00 14 01 08 80 c8 49 00 
```

### CR1 static object interaction

Signature: `80 c3` (spaced or compact hex)

Matches: 23

```text
mxopackets1_luizoe_recursion2.log.txt:1593:00 00 00 00 00 00 00 00 00 00 00 00 00 10 00 00 22 d5 01 6e c1 1b 00 c6 01 80 c3 ea 08 00 58 4c 
mxopackets1_luizoe_recursion2.log.txt:4475:00 22 d5 01 35 c1 1b 00 c6 01 80 c3 ea 08 00 58 4c 0a 00 28 00 00 00 60 b0 c5 d1 c0 00 00 00 00 
mxopackets1_luizoe_recursion2.log.txt:5934:01 80 c3 c4 06 00 58 4c 0a 00 28 00 00 00 68 d1 8a f5 c0 00 00 00 00 60 a0 57 40 00 00 00 34 d8 
mxopackets1_luizoe_recursion2.log.txt:5988:00 00 10 00 00 22 d5 06 28 8f 1b 00 c6 01 80 c3 bf 06 00 58 4c 0a 00 28 00 00 00 28 f7 90 f5 c0 
mxopackets1_luizoe_recursion2.log.txt:6042:00 00 00 00 00 00 00 00 00 00 10 00 00 22 d5 06 27 8f 1b 00 c6 01 80 c3 bf 06 00 58 4c 0a 00 28 
mxopackets1_luizoe_recursion2.log.txt:6046:90 1b 00 c6 01 80 c3 c3 06 00 58 4c 0a 00 28 00 00 00 c4 dd 8c f5 c0 00 00 00 00 00 c0 57 40 00 
mxopackets1_luizoe_recursion2.log.txt:6258:22 d5 06 21 8f 1b 00 c6 01 80 c3 c0 06 00 58 4c 0a 00 28 00 00 00 50 52 97 f5 c0 00 00 00 00 50 
mxopackets1_luizoe_recursion2.log.txt:8009:00 00 00 10 00 00 22 d5 01 61 54 1b 00 c6 01 80 c3 4d 0b 00 58 4c 0a 00 28 00 00 00 98 07 f7 d6 
mxopackets1_luizoe_any_things.log.txt:2060:00 3c 94 40 00 00 00 20 e1 38 c9 c0 00 00 04 01 00 bf 01 06 80 c3 9d 0e 26 00 
mxopackets1_luizoe_any_things.log.txt:2409:b5 c7 a0 a7 a2 44 ea 0e 06 c6 00 00 04 01 00 c0 01 06 80 c3 7e 1b 26 00 
```

### SERVER object/state bundle

Signature: `80 bc` (spaced or compact hex)

Matches: 1915

```text
mxopackets1_luizoe_any_things.log.txt:11:b2 4f 00 08 00 08 02 08 80 b2 51 00 08 00 08 02 08 80 b2 11 00 32 00 08 02 16 80 bc 45 03 11 00 
mxopackets1_luizoe_any_things.log.txt:12:00 02 00 00 00 11 00 32 00 00 00 00 00 00 00 00 16 80 bc 45 00 02 00 00 02 00 00 00 cc 00 0c 00 
mxopackets1_luizoe_any_things.log.txt:13:00 00 00 00 00 00 00 16 80 bc 45 00 03 00 00 0b 00 00 00 37 02 96 00 00 00 00 00 00 00 00 08 80 
mxopackets1_luizoe_any_things.log.txt:14:b2 ca 03 00 00 08 02 16 80 bc 45 03 ca 03 00 25 00 00 00 ca 03 00 00 00 00 00 00 00 00 00 16 80 
mxopackets1_luizoe_any_things.log.txt:15:bc 45 00 04 00 00 25 00 00 00 89 02 0f 00 00 00 00 00 00 00 00 16 80 bc 45 00 05 00 00 71 00 00 
mxopackets1_luizoe_any_things.log.txt:16:00 18 04 0d 00 00 00 00 00 00 00 00 16 80 bc 45 00 06 00 00 74 00 00 00 74 04 07 00 00 00 00 00 
mxopackets1_luizoe_any_things.log.txt:17:00 00 00 16 80 bc 45 00 07 00 00 74 00 00 00 78 04 07 00 00 00 00 00 00 00 00 16 80 bc 45 00 08 
mxopackets1_luizoe_any_things.log.txt:18:00 00 88 00 00 00 e5 03 05 00 00 00 00 00 00 00 00 16 80 bc 45 00 09 00 00 88 00 00 00 76 04 05 
mxopackets1_luizoe_any_things.log.txt:19:00 00 00 00 00 00 00 00 08 80 b2 26 04 00 00 08 02 16 80 bc 45 03 26 04 00 89 00 00 00 26 04 00 
mxopackets1_luizoe_any_things.log.txt:20:00 00 00 00 00 00 00 00 16 80 bc 45 00 0a 00 00 89 00 00 00 16 04 02 00 00 00 00 00 00 00 00 08 
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

Matches: 4

```text
mxopackets1_luizoe_recursion2.log.txt:7282:00 28 8c 00 00 00 00 00 00 00 00 01 00 ff 00 00 00 00 00 00 00 00 00 80 10 00 22 02 87 00 00 00 
mxopackets1_luizoe_recursion2.log.txt:7302:00 28 8c 00 00 00 00 00 00 00 00 01 00 ff 00 00 00 00 00 00 00 00 00 80 10 00 22 02 87 00 00 00 
mxopackets1_luizoe_recursion2.log.txt:7324:00 28 8c 00 00 00 00 00 00 00 00 01 00 ff 00 00 00 00 00 00 00 00 00 80 10 00 22 02 87 00 00 00 
mxopackets1_luizoe_recursion2.log.txt:7368:00 28 8c 00 00 00 00 00 00 00 00 01 00 ff 00 00 00 00 00 00 00 00 00 80 10 00 22 02 87 00 00 00 
```

### Selector ff continuation prefix 00 01 04 ff

Signature: `00 01 04 ff` (spaced or compact hex)

Matches: 3

```text
mxopackets1_luizoe_any_things.log.txt:4388:02 03 02 00 01 04 ff 00 00 
mxopackets1_luizoe_recursion2.log.txt:4119:02 03 02 00 01 04 ff 00 00 
mxopackets1_luizoe_margins_Syntax.log.txt:14166:02 03 02 00 01 04 ff 00 00 
```

### Selector ff continuation prefix 00 01 10 ff

Signature: `00 01 10 ff` (spaced or compact hex)

Matches: 0

### Selector ff continuation prefix 00 00 00 ff

Signature: `00 00 00 ff` (spaced or compact hex)

Matches: 883

```text
mxopackets1_luizoe_recursion2.log.txt:988:2a d4 c0 00 00 00 00 ff 9b 9d 40 00 00 00 00 00 c0 d2 c0 0b 00 63 22 90 01 00 88 c0 1c 02 00 10 
mxopackets1_luizoe_recursion2.log.txt:1005:2a d4 c0 00 00 00 00 ff 9b 9d 40 00 00 00 00 00 c0 d2 c0 0b 00 63 22 90 01 00 88 c0 1c 02 00 10 
mxopackets1_luizoe_recursion2.log.txt:1022:00 00 00 80 2a d4 c0 00 00 00 00 ff 9b 9d 40 00 00 00 00 00 c0 d2 c0 0b 00 63 22 90 01 00 88 c0 
mxopackets1_luizoe_recursion2.log.txt:1039:2a d4 c0 00 00 00 00 ff 9b 9d 40 00 00 00 00 00 c0 d2 c0 0b 00 63 22 90 01 00 88 c0 1c 02 00 10 
mxopackets1_luizoe_recursion2.log.txt:1056:00 00 00 80 2a d4 c0 00 00 00 00 ff 9b 9d 40 00 00 00 00 00 c0 d2 c0 0b 00 63 22 90 01 00 88 c0 
mxopackets1_luizoe_recursion2.log.txt:1073:2a d4 c0 00 00 00 00 ff 9b 9d 40 00 00 00 00 00 c0 d2 c0 0b 00 63 22 90 01 00 88 c0 1c 02 00 10 
mxopackets1_luizoe_recursion2.log.txt:1106:2a d4 c0 00 00 00 00 ff 9b 9d 40 00 00 00 00 00 c0 d2 c0 0b 00 63 22 90 01 00 88 c0 1c 02 00 10 
mxopackets1_luizoe_recursion2.log.txt:1123:2a d4 c0 00 00 00 00 ff 9b 9d 40 00 00 00 00 00 c0 d2 c0 0b 00 63 22 90 01 00 88 c0 1c 02 00 10 
mxopackets1_luizoe_recursion2.log.txt:1186:00 00 00 80 2a d4 c0 00 00 00 00 ff 9b 9d 40 00 00 00 00 00 c0 d2 c0 0b 00 63 22 90 01 00 88 c0 
mxopackets1_luizoe_recursion2.log.txt:1594:0a 00 28 00 00 00 50 ed 07 d9 c0 00 00 00 00 ff 9b 9d 40 00 00 00 cd b2 a2 d2 c0 c0 01 82 01 a8 
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

Matches: 38

```text
mxopackets1_luizoe_recursion2.log.txt:1592:02 03 01 00 0c 57 02 14 cd ab 13 8e 53 75 69 74 20 53 75 70 65 72 76 69 73 6f 72 00 00 00 00 00 
mxopackets1_luizoe_recursion2.log.txt:4470:82 09 08 d9 47 03 01 00 0c 57 02 3f cd ab 10 8e 53 75 69 74 20 57 6f 72 6b 65 72 00 00 00 00 00 
mxopackets1_luizoe_recursion2.log.txt:4473:04 b2 09 d5 04 07 08 00 00 81 c1 1e 00 00 02 07 0d 00 00 01 00 0c 57 02 40 cd ab 13 8e 53 75 69 
mxopackets1_luizoe_recursion2.log.txt:5932:00 00 22 00 00 00 00 00 00 00 01 00 0c 57 02 c1 cd ab 12 8e 50 69 74 20 56 69 70 65 72 20 48 61 
mxopackets1_luizoe_recursion2.log.txt:5935:88 ef c0 c0 01 82 01 a8 1f 72 06 b2 04 72 06 12 08 00 00 80 02 07 21 00 00 01 00 0c 57 02 43 cd 
mxopackets1_luizoe_recursion2.log.txt:5986:00 3f 00 00 00 00 22 00 00 00 00 00 00 00 01 00 02 06 00 1f 00 01 01 01 00 0c 57 02 b7 cd ab 12 
mxopackets1_luizoe_recursion2.log.txt:5990:80 02 07 1b 00 00 01 00 0c 57 02 bc cd ab 16 ae 50 69 74 20 56 69 70 65 72 20 53 6e 61 6b 65 00 
mxopackets1_luizoe_recursion2.log.txt:6041:00 0c 57 02 b9 cd ab 12 8e 50 69 74 20 56 69 70 65 72 20 46 69 6e 67 65 72 6c 69 6e 67 00 00 00 
mxopackets1_luizoe_recursion2.log.txt:6044:b2 09 39 03 12 08 00 00 80 02 07 17 00 00 01 00 0c 57 02 f5 cd ab 12 8e 50 69 74 20 56 69 70 65 
mxopackets1_luizoe_recursion2.log.txt:6142:02 03 02 00 03 08 2f 90 a4 c7 00 00 be 42 00 86 88 c7 17 10 a0 06 80 04 1e 01 00 0c 57 02 16 cd 
```

### Adjacent Object599 boundary prefix

Signature: `00 0c 57 02` (spaced or compact hex)

Matches: 37

```text
mxopackets1_luizoe_recursion2.log.txt:1592:02 03 01 00 0c 57 02 14 cd ab 13 8e 53 75 69 74 20 53 75 70 65 72 76 69 73 6f 72 00 00 00 00 00 
mxopackets1_luizoe_recursion2.log.txt:4470:82 09 08 d9 47 03 01 00 0c 57 02 3f cd ab 10 8e 53 75 69 74 20 57 6f 72 6b 65 72 00 00 00 00 00 
mxopackets1_luizoe_recursion2.log.txt:4473:04 b2 09 d5 04 07 08 00 00 81 c1 1e 00 00 02 07 0d 00 00 01 00 0c 57 02 40 cd ab 13 8e 53 75 69 
mxopackets1_luizoe_recursion2.log.txt:5932:00 00 22 00 00 00 00 00 00 00 01 00 0c 57 02 c1 cd ab 12 8e 50 69 74 20 56 69 70 65 72 20 48 61 
mxopackets1_luizoe_recursion2.log.txt:5935:88 ef c0 c0 01 82 01 a8 1f 72 06 b2 04 72 06 12 08 00 00 80 02 07 21 00 00 01 00 0c 57 02 43 cd 
mxopackets1_luizoe_recursion2.log.txt:5986:00 3f 00 00 00 00 22 00 00 00 00 00 00 00 01 00 02 06 00 1f 00 01 01 01 00 0c 57 02 b7 cd ab 12 
mxopackets1_luizoe_recursion2.log.txt:5990:80 02 07 1b 00 00 01 00 0c 57 02 bc cd ab 16 ae 50 69 74 20 56 69 70 65 72 20 53 6e 61 6b 65 00 
mxopackets1_luizoe_recursion2.log.txt:6041:00 0c 57 02 b9 cd ab 12 8e 50 69 74 20 56 69 70 65 72 20 46 69 6e 67 65 72 6c 69 6e 67 00 00 00 
mxopackets1_luizoe_recursion2.log.txt:6044:b2 09 39 03 12 08 00 00 80 02 07 17 00 00 01 00 0c 57 02 f5 cd ab 12 8e 50 69 74 20 56 69 70 65 
mxopackets1_luizoe_recursion2.log.txt:6142:02 03 02 00 03 08 2f 90 a4 c7 00 00 be 42 00 86 88 c7 17 10 a0 06 80 04 1e 01 00 0c 57 02 16 cd 
```

### Header-like selector 50

Signature: `01 00 08 50` (spaced or compact hex)

Matches: 5

```text
mxopackets1_luizoe_recursion2.log.txt:1546:ff d3 9c 40 00 00 00 40 f5 28 d1 c0 09 a1 0f 00 00 0b 00 00 01 00 08 50 02 8e 21 50 09 9b cd ab 
mxopackets1_luizoe_recursion2.log.txt:5904:ba 13 01 00 08 50 02 2c 00 d0 20 6c cd ab 02 09 00 00 00 00 40 88 f4 c0 00 00 00 00 00 40 60 40 
mxopackets1_luizoe_recursion2.log.txt:8062:07 08 00 00 81 da 1e 00 00 02 0a 09 00 00 01 00 08 50 02 59 23 10 2a c9 cd ab 01 01 00 00 00 00 
mxopackets1_luizoe_any_things.log.txt:3124:02 03 01 00 08 50 02 a9 36 10 01 0a cd ab 01 01 00 00 00 00 c0 56 f6 c0 00 00 00 00 00 c4 94 40 
mxopackets1_luizoe_any_things.log.txt:4153:02 03 01 00 08 50 02 8e 21 50 09 9b cd ab 02 09 00 00 00 00 80 11 d4 c0 00 00 00 00 00 28 9e 40 
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

Matches: 1

```text
mxopackets1_luizoe_any_things.log.txt:810:00 f4 ca c0 0d 00 00 01 00 0c 76 a7 ef cd ab 03 0d 00 00 00 00 40 8f f7 c0 00 00 00 00 00 2c 95 
```

### Header-like selector 90

Signature: `01 00 08 90` (spaced or compact hex)

Matches: 0

### Header-like selector a3

Signature: `01 00 08 a3` (spaced or compact hex)

Matches: 0

### Header-like selector d0

Signature: `01 00 08 d0` (spaced or compact hex)

Matches: 3

```text
mxopackets1_luizoe_any_things.log.txt:4212:00 00 40 d2 9c 9d 40 00 00 00 00 80 2b d8 c0 05 00 00 01 00 08 d0 20 49 35 10 1c 63 cd ab 08 fd 
mxopackets1_luizoe_recursion2.log.txt:1541:c0 0f 00 00 01 00 08 d0 20 49 35 10 1c 63 cd ab 08 fd 00 00 00 00 00 51 d8 c0 00 00 00 00 00 9c 
mxopackets1_luizoe_recursion2.log.txt:5119:02 03 01 00 08 d0 20 49 35 10 1c 63 cd ab 08 fd 00 00 00 00 00 51 d8 c0 00 00 00 00 00 9c 9d 40 
```

## Movement And Object-State Leads

### Mode 06 movement selector 08

Signature: `00 06 08` (spaced or compact hex)

Matches: 0

### Mode 06 movement selector 0a

Signature: `00 06 0a` (spaced or compact hex)

Matches: 0

### Mode 06 movement selector 0c

Signature: `00 06 0c` (spaced or compact hex)

Matches: 5

```text
mxopackets1_luizoe_margins_Syntax.log.txt:8305:02 03 07 00 06 0c 38 00 a8 85 45 00 80 f7 43 00 9a 83 c6 ff 00 00 00 10 00 00 01 01 00 01 ff 00 
mxopackets1_luizoe_margins_Syntax.log.txt:10188:02 03 07 00 06 0c 37 5a d7 85 45 00 80 f7 43 e4 c1 84 c6 ff 00 00 00 10 00 00 01 01 00 01 ff 00 
mxopackets1_luizoe_margins_Syntax.log.txt:15114:82 88 54 da 47 03 02 00 02 80 04 1e 08 00 06 0c 7e 86 7e b2 45 00 20 8a c4 02 7e 1b c6 80 80 80 
mxopackets1_luizoe_margins_Syntax.log.txt:15242:02 03 08 00 06 0c 7d 86 7e b2 45 00 20 8a c4 02 7e 1b c6 ff 00 02 00 10 00 00 01 00 00 01 ff 00 
mxopackets1_luizoe_margins_Syntax.log.txt:25882:02 03 0b 00 06 0c de c4 15 4e 46 00 40 30 c4 59 a5 6e 47 ff 00 00 00 10 00 00 00 00 00 01 ff 00 
```

### Mode 06 movement selector 0e

Signature: `00 06 0e` (spaced or compact hex)

Matches: 29

```text
mxopackets1_luizoe_margins_Syntax.log.txt:886:02 03 05 00 06 0e 04 69 bc 76 0f 46 00 80 93 43 44 d1 7f c6 80 80 80 80 80 80 01 0a 08 00 00 03 
mxopackets1_luizoe_margins_Syntax.log.txt:898:02 03 05 00 06 0e 00 74 bc 76 0f 46 00 80 93 43 44 d1 7f c6 ff 00 00 00 10 00 00 01 01 00 01 ff 
mxopackets1_luizoe_margins_Syntax.log.txt:918:02 03 05 00 06 0e 00 7b bc 76 0f 46 00 80 93 43 44 d1 7f c6 ff 00 00 00 10 00 00 01 01 00 01 ff 
mxopackets1_luizoe_margins_Syntax.log.txt:3806:02 03 05 00 06 0e 01 81 5c 75 0f 46 00 80 93 43 a1 04 80 c6 80 80 80 80 80 80 01 fe 07 00 00 00 
mxopackets1_luizoe_margins_Syntax.log.txt:3818:02 03 05 00 06 0e 01 85 58 6a 0f 46 00 80 93 43 48 31 80 c6 ff 00 00 00 10 00 00 01 01 00 01 ff 
mxopackets1_luizoe_margins_Syntax.log.txt:4003:02 03 05 00 06 0e 00 84 f4 d5 0e 46 00 80 93 43 30 59 82 c6 ff 00 00 00 10 00 00 01 01 00 01 ff 
mxopackets1_luizoe_margins_Syntax.log.txt:4023:02 03 02 00 02 80 04 00 05 00 06 0e 00 84 f4 d5 0e 46 00 80 93 43 30 59 82 c6 ff 00 00 00 10 00 
mxopackets1_luizoe_margins_Syntax.log.txt:4045:82 d1 c4 d9 47 03 05 00 06 0e 00 84 f4 d5 0e 46 00 80 93 43 30 59 82 c6 ff 00 00 00 10 00 00 01 
mxopackets1_luizoe_margins_Syntax.log.txt:4065:02 03 05 00 06 0e 00 84 f4 d5 0e 46 00 80 93 43 30 59 82 c6 ff 00 00 00 10 00 00 01 01 00 01 ff 
mxopackets1_luizoe_margins_Syntax.log.txt:4085:02 03 05 00 06 0e 00 84 f4 d5 0e 46 00 80 93 43 30 59 82 c6 ff 00 00 00 10 00 00 01 01 00 01 ff 
```

### Repeated mode 06 suffix lead

Signature: `ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00` (spaced or compact hex)

Matches: 0

### Movement-state ff 01 64 01 tail

Signature: `ff 01 64 01` (spaced or compact hex)

Matches: 0

### Movement-state ff 01 4e 01 tail

Signature: `ff 01 4e 01` (spaced or compact hex)

Matches: 0

## Ability, Effect, And Combat-State Leads

### SERVER_PLAYER_ATTRIBUTE / ability load

Signature: `80 b2` (spaced or compact hex)

Matches: 81

```text
mxopackets1_luizoe_recursion2.log.txt:7:08 80 b2 4e 00 1e 00 08 02 08 80 b2 52 00 0d 00 08 02 08 80 b2 54 00 1e 00 08 02 08 80 b2 4f 00 
mxopackets1_luizoe_recursion2.log.txt:8:08 00 08 02 08 80 b2 51 00 08 00 08 02 08 80 b2 11 00 32 00 08 02 16 80 bc 45 03 11 00 00 02 00 
mxopackets1_luizoe_recursion2.log.txt:10:00 00 00 00 16 80 bc 45 00 03 00 00 0b 00 00 00 37 02 96 00 00 00 00 00 00 00 00 08 80 b2 ca 03 
mxopackets1_luizoe_recursion2.log.txt:16:00 00 00 00 00 08 80 b2 26 04 00 00 08 02 16 80 bc 45 03 26 04 00 89 00 00 00 26 04 00 00 00 00 
mxopackets1_luizoe_recursion2.log.txt:17:00 00 00 00 00 16 80 bc 45 00 0a 00 00 89 00 00 00 16 04 02 00 00 00 00 00 00 00 00 08 80 b2 24 
mxopackets1_luizoe_recursion2.log.txt:22:97 00 00 00 17 04 05 00 00 00 00 00 00 00 00 08 80 b2 57 04 32 00 08 02 16 80 bc 45 03 57 04 00 
mxopackets1_luizoe_recursion2.log.txt:24:00 00 00 00 00 00 08 80 b2 25 04 00 00 08 02 16 80 bc 45 03 25 04 00 9a 00 00 00 25 04 00 00 00 
mxopackets1_luizoe_recursion2.log.txt:51:02 04 01 00 24 1e 16 80 bc 45 00 12 00 00 01 01 00 00 76 04 01 00 00 00 00 00 00 00 00 08 80 b2 
mxopackets1_luizoe_recursion2.log.txt:56:00 14 01 00 00 c3 02 32 00 00 00 00 00 00 00 00 08 80 b2 c9 03 00 00 08 02 16 80 bc 45 03 c9 03 
mxopackets1_luizoe_recursion2.log.txt:127:80 b2 11 00 32 00 08 02 16 80 bc 45 03 11 00 00 02 00 00 00 11 00 32 00 00 00 00 00 00 00 00 16 
```

### SERVER_ABILITY_UNLOAD

Signature: `80 b3` (spaced or compact hex)

Matches: 101

```text
mxopackets1_luizoe_recursion2.log.txt:125:00 b5 00 14 00 00 00 00 00 00 00 00 04 80 b3 11 00 16 80 bc 45 03 11 00 00 02 00 00 00 11 00 32 
mxopackets1_luizoe_recursion2.log.txt:130:00 00 00 00 04 80 b3 ca 03 16 80 bc 45 03 ca 03 00 25 00 00 00 ca 03 00 00 00 00 01 00 00 00 00 
mxopackets1_luizoe_recursion2.log.txt:171:00 04 80 b3 26 04 16 80 bc 45 03 26 04 00 89 00 00 00 26 04 00 00 00 00 01 00 00 00 00 16 80 bc 
mxopackets1_luizoe_recursion2.log.txt:174:16 04 02 00 00 00 00 00 00 00 00 04 80 b3 24 04 16 80 bc 45 03 24 04 00 8f 00 00 00 24 04 00 00 
mxopackets1_luizoe_recursion2.log.txt:180:00 00 97 00 00 00 17 04 05 00 00 00 01 00 00 00 00 04 80 b3 57 04 16 80 bc 45 03 57 04 00 97 00 
mxopackets1_luizoe_recursion2.log.txt:186:04 80 b3 25 04 16 80 bc 45 03 25 04 00 9a 00 00 00 25 04 00 00 00 00 01 00 00 00 00 16 80 bc 55 
mxopackets1_luizoe_recursion2.log.txt:190:00 04 80 b3 27 04 16 80 bc 45 03 27 04 00 01 01 00 00 27 04 00 00 00 00 01 00 00 00 00 16 80 bc 
mxopackets1_luizoe_recursion2.log.txt:231:00 00 00 00 00 00 04 80 b3 c9 03 16 80 bc 45 03 c9 03 00 15 01 00 00 c9 03 00 00 00 00 01 00 00 
mxopackets1_luizoe_recursion2.log.txt:856:00 00 04 02 00 00 8d 02 32 00 00 00 01 00 00 00 00 04 80 b3 11 00 16 80 bc 45 03 11 00 00 02 00 
mxopackets1_luizoe_recursion2.log.txt:857:00 00 11 00 32 00 00 00 01 00 00 00 00 04 80 b3 c9 03 16 80 bc 45 03 c9 03 00 15 01 00 00 c9 03 
```

### Object599 adrenaline booster effect id

Signature: `4c 0a 00 28` (spaced or compact hex)

Matches: 14

```text
mxopackets1_luizoe_recursion2.log.txt:4475:00 22 d5 01 35 c1 1b 00 c6 01 80 c3 ea 08 00 58 4c 0a 00 28 00 00 00 60 b0 c5 d1 c0 00 00 00 00 
mxopackets1_luizoe_recursion2.log.txt:5934:01 80 c3 c4 06 00 58 4c 0a 00 28 00 00 00 68 d1 8a f5 c0 00 00 00 00 60 a0 57 40 00 00 00 34 d8 
mxopackets1_luizoe_recursion2.log.txt:5988:00 00 10 00 00 22 d5 06 28 8f 1b 00 c6 01 80 c3 bf 06 00 58 4c 0a 00 28 00 00 00 28 f7 90 f5 c0 
mxopackets1_luizoe_recursion2.log.txt:5992:01 a0 1e c3 c2 06 00 58 4c 0a 00 28 00 00 20 b2 16 70 f5 c0 00 00 00 00 50 dd 58 40 00 00 00 a4 
mxopackets1_luizoe_recursion2.log.txt:6042:00 00 00 00 00 00 00 00 00 00 10 00 00 22 d5 06 27 8f 1b 00 c6 01 80 c3 bf 06 00 58 4c 0a 00 28 
mxopackets1_luizoe_recursion2.log.txt:6046:90 1b 00 c6 01 80 c3 c3 06 00 58 4c 0a 00 28 00 00 00 c4 dd 8c f5 c0 00 00 00 00 00 c0 57 40 00 
mxopackets1_luizoe_recursion2.log.txt:6258:22 d5 06 21 8f 1b 00 c6 01 80 c3 c0 06 00 58 4c 0a 00 28 00 00 00 50 52 97 f5 c0 00 00 00 00 50 
mxopackets1_luizoe_recursion2.log.txt:8009:00 00 00 10 00 00 22 d5 01 61 54 1b 00 c6 01 80 c3 4d 0b 00 58 4c 0a 00 28 00 00 00 98 07 f7 d6 
mxopackets1_luizoe_recursion2.log.txt:8481:82 bb 24 d9 47 03 02 00 02 80 80 80 40 ed 13 11 00 04 80 80 80 80 c0 4c 0a 00 28 01 01 00 00 
mxopackets1_luizoe_any_things.log.txt:874:00 00 10 00 00 22 d5 01 0b 47 1b 00 c6 01 80 d3 37 0a 00 58 4c 0a 00 28 0a 00 00 00 e4 5e b0 f6 
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
