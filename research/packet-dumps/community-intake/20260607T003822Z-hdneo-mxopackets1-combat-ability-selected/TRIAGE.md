# Packet Capture Triage Report

- Source kind: `directory`
- Source input: `/Users/kyle/Developer/mxo-hd/research/packet-dumps/community-intake/20260607T003822Z-hdneo-mxopackets1-combat-ability-selected/logs`
- Scanned files from: `/Users/kyle/Developer/mxo-hd/research/packet-dumps/community-intake/20260607T003822Z-hdneo-mxopackets1-combat-ability-selected/logs`
- Generated UTC: `2026-06-07T00:38:24Z`
- Max sample lines per pattern: `10`

Use this report to decide whether received logs are worth importing into `research/packet-dumps/`. Matches are byte-string leads, not decoded packet claims.

## Vendor And Interaction Leads

### SERVER_VENDOR_OPEN candidate

Signature: `81 0d` (spaced or compact hex)

Matches: 1

```text
actions_combat_marketplace_abilitychangecoder.log.txt:5722:04 00 06 0f 00 00 00 23 af c2 3f c7 00 00 be 42 12 da fa c6 80 80 80 c0 98 04 80 81 0d 02 00 00
```

### CLIENT_VENDOR_BUY candidate

Signature: `81 0e` (spaced or compact hex)

Matches: 2

```text
actions_combat_marketplace_abilitychangecoder.log.txt:1473:02 03 02 00 02 80 80 80 40 b2 09 0e 00 05 01 c2 04 00 80 80 e0 01 00 00 c0 0d 0a 00 28 81 0e 03
actions_combat_marketplace_abilitychangecoder.log.txt:44898:82 dc 55 26 49 03 10 00 06 01 0c 00 80 80 80 80 c0 62 01 00 28 81 0e 02 00 00 10 00 0f 00 04 80
```

### CLIENT_VENDOR_SELL candidate

Signature: `81 11` (spaced or compact hex)

Matches: 1

```text
abilities_use_code_nuke_30_at_corrupted.log.txt:237:e0 01 00 00 c0 83 00 00 de 81 11 03 fd 07 00 00 00 00 00 00 00 00 04 02 06 35 02 3d 81 67 17 00
```

### CR2 static object interaction

Signature: `80 c8` (spaced or compact hex)

Matches: 2

```text
actions_combat_marketplace_abilitychangecoder.log.txt:7133:02 03 02 00 02 80 84 53 80 c8 00 00 e3 09 80 10 ee 03 00 00 04 00 06 0e 00 24 b7 36 40 c7 00 00
actions_combat_marketplace_abilitychangecoder.log.txt:47154:00 00 00 22 00 00 00 00 00 07 00 06 0c 80 c8 e4 4e c7 00 00 be 42 b2 12 c9 c6 ff 00 00 00 10 00
```

### CR1 static object interaction

Signature: `80 c3` (spaced or compact hex)

Matches: 22

```text
actions_combat.log.txt:2579:80 c3 c6 50 75 00
actions_combat.log.txt:2711:01 80 00 e3 00 00 00 04 01 01 3f 01 06 80 c3 e8 5a 75 00
actions_combat.log.txt:3676:08 de 32 c6 00 00 04 01 01 40 01 06 80 c3 0f 79 75 00
actions_combat.log.txt:5096:61 76 40 00 00 00 a0 cd a2 b1 40 10 e3 00 1b 00 01 80 00 e3 00 00 00 04 01 01 43 01 06 80 c3 a5
actions_combat.log.txt:12657:00 00 00 00 00 00 00 00 00 00 00 00 00 10 00 00 22 d5 01 2c 38 1c 00 c6 01 80 c3 f5 09 00 58 4c
actions_combat_marketplace_abilitychangecoder.log.txt:7697:38 1c 00 c6 01 80 c3 f5 09 00 58 4c 0a 00 28 00 00 00 0c a0 1b ea c0 00 00 00 00 00 c0 57 40 00
actions_combat_marketplace_abilitychangecoder.log.txt:41120:00 00 00 00 00 00 00 00 00 00 00 00 00 10 00 00 22 d5 0b 5a 37 1c 00 c6 01 80 c3 f4 09 00 58 4c
actions_combat_marketplace_abilitychangecoder.log.txt:41124:00 10 00 00 22 d5 0b 5e 37 1c 00 c6 01 80 c3 f4 09 00 58 4c 0a 00 28 00 00 00 10 bf 40 ea c0 00
actions_combat_marketplace_abilitychangecoder.log.txt:42647:00 00 00 00 00 00 00 00 00 00 00 00 00 10 00 00 22 d5 0b 72 37 1c 00 c6 01 80 c3 f4 09 00 58 4c
actions_combat_marketplace_abilitychangecoder.log.txt:54518:61 6c 65 00 00 00 00 00 00 00 00 00 00 00 00 00 00 10 00 00 22 d5 0e 5a 37 1c 00 c6 01 80 c3 f4
```

### SERVER object/state bundle

Signature: `80 bc` (spaced or compact hex)

Matches: 2429

```text
actions_Vector_unload_and_load_ability.log.txt:6:02 04 01 00 99 02 04 80 b3 74 02 16 80 bc 55 00 2a 01 00 74 02 00 00 34 02 1e 00 00 00 01 00 00
actions_Vector_unload_and_load_ability.log.txt:27:82 66 c7 f3 48 04 01 00 9b 02 08 80 b2 74 02 32 00 89 01 16 80 bc 45 00 2b 01 00 74 02 00 00 34
abilities_use_code_nuke_30_at_corrupted.log.txt:86:6a 10 47 00 20 8a c4 3a a8 d8 c5 00 00 04 01 06 26 05 16 80 bc 01 00 e5 01 00 cb 00 00 00 c7 00
abilities_use_code_nuke_30_at_corrupted.log.txt:87:00 00 00 00 01 00 00 20 41 16 80 bc 05 00 e6 01 00 c7 00 00 00 ab 00 fc ff ff ff 01 00 00 00 00
abilities_use_code_nuke_30_at_corrupted.log.txt:88:16 80 bc 05 00 e7 01 00 c7 00 00 00 ac 00 fc ff ff ff 01 00 00 00 00 16 80 bc 05 00 e8 01 00 c7
abilities_use_code_nuke_30_at_corrupted.log.txt:89:00 00 00 0a 04 fc ff ff ff 01 00 00 00 00 16 80 bc 05 00 e9 01 00 c7 00 00 00 9f 00 fc ff ff ff
abilities_usecombathacking.log.txt:33:00 95 43 01 03 cd 04 06 80 c1 16 00 c8 00 16 80 bc 15 00 3f 01 00 58 01 00 00 f2 03 16 00 00 00
abilities_usecombathacking.log.txt:34:00 00 00 00 42 16 80 bc 15 00 40 01 00 58 01 00 00 75 00 18 00 00 00 00 00 00 00 42 16 80 bc 15
actions_Vector_loadandunloadhyperspeed.log.txt:15:02 04 01 00 9d 03 04 80 b3 0c 00 04 80 b3 0b 00 16 80 bc 55 00 11 01 00 0b 00 00 00 37 02 96 00
actions_Vector_loadandunloadhyperspeed.log.txt:28:82 1c cf f3 48 04 01 00 a0 02 08 80 b2 0b 00 32 00 16 00 16 80 bc 45 00 2c 01 00 0b 00 00 00 37
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

Matches: 12

```text
actions_combat_marketplace_abilitychangecoder.log.txt:19991:00 00 00 00 01 00 ff 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 0d 0a 00 28 ff 24 00 00 00 00 00
actions_combat_marketplace_abilitychangecoder.log.txt:20063:00 01 00 ff 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 0d 0a 00 28 ff 24 00 00 00 00 00 00 00 00
actions_combat_marketplace_abilitychangecoder.log.txt:20077:00 00 00 00 00 00 00 00 00 00 01 00 ff 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 0d 0a 00 28 ff
actions_combat_marketplace_abilitychangecoder.log.txt:31133:00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 01 00 ff 06 ff 00 00 00 00 00 00 00 00 00 00
actions_combat_marketplace_abilitychangecoder.log.txt:31138:00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 01 00 ff 06 ff 00 00 00
actions_combat_marketplace_abilitychangecoder.log.txt:31155:00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 01 00 ff 06 ff 00 00 00 00
actions_combat_marketplace_abilitychangecoder.log.txt:31161:00 00 00 00 00 00 01 00 ff 06 ff 00 00 00 00 00 00 00 00 00 00 00 00 30 04 00 28 ff 0b 00 00 00
actions_combat_marketplace_abilitychangecoder.log.txt:33429:00 00 01 00 ff 06 ff 00 00 00 00 00 00 00 00 00 00 00 00 30 04 00 28 ff 07 00 00 00 00 00 00 00
actions_combat_marketplace_abilitychangecoder.log.txt:33451:00 00 00 00 00 00 00 00 01 00 ff 06 ff 00 00 00 00 00 00 00 00 00 00 00 00 30 04 00 28 ff 07 00
actions_combat_marketplace_abilitychangecoder.log.txt:33474:00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 01 00 ff 06 ff 00 00 00 00 00 00 00 00 00 00 00
```

### Selector ff continuation prefix 00 01 04 ff

Signature: `00 01 04 ff` (spaced or compact hex)

Matches: 2

```text
actions_combat.log.txt:2468:02 03 02 00 01 04 ff 00 00
actions_combat_marketplace_abilitychangecoder.log.txt:62476:02 03 02 00 01 04 ff 00 00
```

### Selector ff continuation prefix 00 01 10 ff

Signature: `00 01 10 ff` (spaced or compact hex)

Matches: 0

### Selector ff continuation prefix 00 00 00 ff

Signature: `00 00 00 ff` (spaced or compact hex)

Matches: 2822

```text
abilities_use_destroyer_at_corrupted.log.txt:36:00 00 00 03 ff 00 00 00 00 00 00 00 00 c9 04 00 00 00 00 2f 00 00 00 00 00 00 ff 00 00 00 00 00
abilities_use_destroyer_at_corrupted.log.txt:79:00 00 00 03 ff 00 00 00 00 00 00 00 00 c9 04 00 00 00 00 2f 00 00 00 00 00 00 ff 00 00 00 00 00
abilities_use_destroyer_at_corrupted.log.txt:236:02 8e 00 00 00 01 00 66 66 e6 3e 00 2a 00 00 02 00 00 00 00 ff 28 0a 32 01 e4 89 46 14 00 6f 46
abilities_use_destroyer_at_corrupted.log.txt:321:00 00 00 03 ff 00 00 00 00 00 00 00 00 c9 04 00 00 00 00 2f 00 00 00 00 00 00 ff 00 00 00 00 00
abilities_use_destroyer_at_corrupted.log.txt:390:00 00 00 03 ff 00 00 00 00 00 00 00 00 c9 04 00 00 00 00 2f 00 00 00 00 00 00 ff 00 00 00 00 00
abilities_use_destroyer_at_corrupted.log.txt:404:00 00 00 03 ff 00 00 00 00 00 00 00 00 c9 04 00 00 00 00 2f 00 00 00 00 00 00 ff 00 00 00 00 00
abilities_use_plague_zone_2_0_at_corrupted.log.txt:87:02 8e 00 00 00 01 00 66 66 e6 3e 00 2a 00 00 02 00 00 00 00 ff 28 0a 32 01 e4 89 46 14 00 6f 46
abilities_use_plague_zone_2_0_at_corrupted.log.txt:150:00 00 00 00 00 c9 04 00 00 00 00 2f 00 00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00 00 00 00 00
abilities_use_plague_zone_2_0_at_corrupted.log.txt:167:00 00 00 03 ff 00 00 00 00 00 00 00 00 c9 04 00 00 00 00 2f 00 00 00 00 00 00 ff 00 00 00 00 00
actions_combat.log.txt:1675:00 00 00 00 00 00 00 5e 01 00 00 00 00 05 00 00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00 00 00
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

Matches: 84

```text
actions_combat.log.txt:445:00 00 80 3f 00 00 00 00 2e bd 3b b3 17 00 00 01 00 0c 57 02 3c cd ab 13 8e 42 6c 61 63 6b 77 6f
actions_combat.log.txt:478:01 00 0c 57 02 3e cd ab 13 8e 42 6c 61 63 6b 77 6f 6f 64 20 47 6f 6f 66 00 00 00 00 00 00 00 00
actions_combat.log.txt:481:09 af 00 07 08 00 00 80 02 01 11 00 00 01 00 0c 57 02 3f cd ab 13 8e 42 6c 61 63 6b 77 6f 6f 64
actions_combat.log.txt:484:cb c0 c0 01 8b 01 03 15 a8 05 af 00 b2 09 af 00 07 08 00 00 80 02 01 0f 00 00 01 00 0c 57 02 40
actions_combat.log.txt:514:28 5b a6 46 00 00 be 42 dc ab 1e c6 01 00 0c 57 02 41 cd ab 11 8e 42 6c 61 63 6b 77 6f 6f 64 20
actions_combat.log.txt:517:c0 01 83 05 02 a8 05 06 01 b2 09 06 01 07 08 00 00 80 02 01 09 00 00 01 00 0c 57 02 42 cd ab 13
actions_combat.log.txt:521:01 07 00 00 01 00 0c 57 02 43 cd ab 11 8e 42 6c 61 63 6b 77 6f 6f 64 20 4d 75 73 68 66 61 6b 65
actions_combat.log.txt:524:01 b2 09 06 01 07 08 00 00 80 02 01 05 00 00 01 00 0c 57 02 44 cd ab 13 8e 42 6c 61 63 6b 77 6f
actions_combat.log.txt:1351:02 03 02 00 02 80 04 00 01 00 0c 57 02 45 cd ab 11 8e 42 6c 61 63 6b 77 6f 6f 64 20 47 6f 6f 66
actions_combat.log.txt:2172:02 03 01 00 0c 57 02 46 cd ab 13 8e 42 6c 61 63 6b 77 6f 6f 64 20 4d 75 73 68 66 61 6b 65 00 00
```

### Adjacent Object599 boundary prefix

Signature: `00 0c 57 02` (spaced or compact hex)

Matches: 79

```text
actions_combat.log.txt:445:00 00 80 3f 00 00 00 00 2e bd 3b b3 17 00 00 01 00 0c 57 02 3c cd ab 13 8e 42 6c 61 63 6b 77 6f
actions_combat.log.txt:478:01 00 0c 57 02 3e cd ab 13 8e 42 6c 61 63 6b 77 6f 6f 64 20 47 6f 6f 66 00 00 00 00 00 00 00 00
actions_combat.log.txt:481:09 af 00 07 08 00 00 80 02 01 11 00 00 01 00 0c 57 02 3f cd ab 13 8e 42 6c 61 63 6b 77 6f 6f 64
actions_combat.log.txt:484:cb c0 c0 01 8b 01 03 15 a8 05 af 00 b2 09 af 00 07 08 00 00 80 02 01 0f 00 00 01 00 0c 57 02 40
actions_combat.log.txt:514:28 5b a6 46 00 00 be 42 dc ab 1e c6 01 00 0c 57 02 41 cd ab 11 8e 42 6c 61 63 6b 77 6f 6f 64 20
actions_combat.log.txt:517:c0 01 83 05 02 a8 05 06 01 b2 09 06 01 07 08 00 00 80 02 01 09 00 00 01 00 0c 57 02 42 cd ab 13
actions_combat.log.txt:521:01 07 00 00 01 00 0c 57 02 43 cd ab 11 8e 42 6c 61 63 6b 77 6f 6f 64 20 4d 75 73 68 66 61 6b 65
actions_combat.log.txt:524:01 b2 09 06 01 07 08 00 00 80 02 01 05 00 00 01 00 0c 57 02 44 cd ab 13 8e 42 6c 61 63 6b 77 6f
actions_combat.log.txt:1351:02 03 02 00 02 80 04 00 01 00 0c 57 02 45 cd ab 11 8e 42 6c 61 63 6b 77 6f 6f 64 20 47 6f 6f 66
actions_combat.log.txt:2172:02 03 01 00 0c 57 02 46 cd ab 13 8e 42 6c 61 63 6b 77 6f 6f 64 20 4d 75 73 68 66 61 6b 65 00 00
```

### Header-like selector 50

Signature: `01 00 08 50` (spaced or compact hex)

Matches: 4

```text
actions_combat.log.txt:4766:01 00 00 00 00 c0 98 d0 40 00 00 00 00 00 f0 7e 40 00 00 00 00 00 60 68 40 0c 00 00 01 00 08 50
actions_combat.log.txt:7187:0c c6 48 54 d6 c6 00 40 30 c4 96 94 b1 46 01 00 08 50 02 2c 03 30 03 3d cd ab 02 09 00 00 00 00
actions_combat.log.txt:8884:03 07 08 00 00 81 d4 1e 00 00 02 09 1b 00 00 01 00 08 50 02 eb 1f f0 2e 04 cd ab 02 09 00 00 00
actions_combat.log.txt:11158:b3 1c e2 c0 0f 00 00 01 00 08 50 02 bb 09 40 02 ee cd ab 01 01 00 00 00 00 00 bb e7 c0 00 00 00
```

### Header-like selector 67

Signature: `01 00 08 67` (spaced or compact hex)

Matches: 0

### Header-like selector 68

Signature: `01 00 08 68` (spaced or compact hex)

Matches: 0

### Header-like selector 69

Signature: `01 00 08 69` (spaced or compact hex)

Matches: 8

```text
actions_combat.log.txt:11163:01 00 08 69 09 d0 09 40 02 19 cd ab 02 05 00 00 00 00 c0 52 ea c0 00 00 00 00 00 88 99 40 00 00
actions_combat.log.txt:12389:82 5b 2c 26 49 03 01 00 08 69 09 d0 09 40 02 19 cd ab 02 05 00 00 00 00 c0 52 ea c0 00 00 00 00
actions_combat_marketplace_abilitychangecoder.log.txt:86:02 03 01 00 08 69 09 d0 09 40 02 19 cd ab 02 05 00 00 00 00 c0 52 ea c0 00 00 00 00 00 88 99 40
actions_combat_marketplace_abilitychangecoder.log.txt:5912:02 03 01 00 08 69 09 d0 09 40 02 19 cd ab 02 05 00 00 00 00 c0 52 ea c0 00 00 00 00 00 88 99 40
actions_combat_marketplace_abilitychangecoder.log.txt:61133:02 03 02 00 02 80 84 00 80 10 db 00 01 00 08 69 09 d0 09 40 02 19 cd ab 02 05 00 00 00 00 c0 52
actions_combat_marketplace_abilitychangecoder.log.txt:61857:00 00 00 00 08 81 40 00 00 00 00 00 04 e0 c0 ff ff ff ff 05 00 00 01 00 08 69 09 d0 09 40 02 19
actions_combat_marketplace_abilitychangecoder.log.txt:63224:02 03 01 00 08 69 09 d0 09 40 02 19 cd ab 02 05 00 00 00 00 c0 52 ea c0 00 00 00 00 00 88 99 40
actions_combat_marketplace_abilitychangecoder.log.txt:63246:82 99 67 26 49 03 01 00 08 69 09 d0 09 40 02 19 cd ab 02 05 00 00 00 00 c0 52 ea c0 00 00 00 00
```

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
actions_combat_marketplace_abilitychangecoder.log.txt:5913:00 00 00 00 80 81 e2 c0 00 00 00 00 16 ef c3 3e 00 00 00 00 5e 83 6c 3f 10 00 00 01 00 08 a3 01
actions_combat_marketplace_abilitychangecoder.log.txt:61856:02 03 02 00 02 80 04 00 01 00 08 a3 01 3f 00 40 02 6c cd ab 02 80 22 00 00 00 00 00 83 e8 c0 00
```

### Header-like selector d0

Signature: `01 00 08 d0` (spaced or compact hex)

Matches: 5

```text
actions_combat.log.txt:3929:01 22 00 00 00 00 00 00 00 01 00 08 d0 20 ee 01 30 39 12 cd ab 08 fd 00 00 00 00 c0 5c d5 40 00
actions_combat.log.txt:4812:01 22 00 00 00 00 00 00 00 01 00 08 d0 20 f1 01 30 39 0d cd ab 08 fd 00 00 00 00 80 83 cf 40 00
actions_combat.log.txt:4932:01 22 00 00 00 00 00 00 00 01 00 08 d0 20 ef 01 30 39 13 cd ab 08 fd 00 00 00 00 40 5a d5 40 00
actions_combat.log.txt:5046:01 b2 09 06 01 07 08 00 00 80 02 01 13 00 00 01 00 08 d0 20 b1 00 20 4e 10 cd ab 07 fd 00 00 00
actions_combat.log.txt:5985:01 22 00 00 00 00 00 00 00 01 00 08 d0 20 b0 00 20 4e 11 cd ab 08 fd 00 00 00 00 c0 05 d2 40 00
```

## Movement And Object-State Leads

### Mode 06 movement selector 08

Signature: `00 06 08` (spaced or compact hex)

Matches: 173

```text
abilities_use_destroyer_at_corrupted.log.txt:287:02 03 06 00 06 08 ac d3 0f 47 00 20 8a c4 62 da e6 c5 80 80 80 40 12 02 00 00
abilities_use_destroyer_at_corrupted.log.txt:341:02 03 06 00 06 08 ac d3 0f 47 00 20 8a c4 62 da e6 c5 80 80 80 40 15 02 00 00
abilities_use_destroyer_at_corrupted.log.txt:380:02 03 06 00 06 08 ac d3 0f 47 00 20 8a c4 62 da e6 c5 80 80 80 40 16 02 00 00
abilities_use_plague_zone_2_0_at_corrupted.log.txt:133:02 03 02 00 02 80 80 80 10 cc 00 11 00 01 80 00 cc 00 06 00 06 08 ac d3 0f 47 00 20 8a c4 62 da
actions_combat.log.txt:8257:02 03 11 00 06 08 00 28 be c6 00 40 30 c4 8c 79 9a 46 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00
actions_combat.log.txt:16473:02 03 0e 00 06 08 6a 7d 3f c7 00 00 be 42 19 43 f6 c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00
actions_combat.log.txt:16982:02 03 02 00 02 80 80 80 50 a9 00 00 0a 10 00 06 08 77 ee 3e c7 00 00 be 42 00 ba f4 c6 80 80 80
actions_combat.log.txt:17042:02 03 0e 00 06 08 6a 7d 3f c7 00 00 be 42 19 43 f6 c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00
actions_combat.log.txt:17056:02 03 02 00 02 80 80 80 40 04 0a 10 00 06 08 77 ee 3e c7 00 00 be 42 00 ba f4 c6 80 80 80 c0 7d
actions_combat.log.txt:17119:02 03 02 00 02 80 04 16 0e 00 06 08 6a 7d 3f c7 00 00 be 42 19 43 f6 c6 ff 00 00 00 10 00 00 00
```

### Mode 06 movement selector 0a

Signature: `00 06 0a` (spaced or compact hex)

Matches: 5

```text
actions_combat.log.txt:18701:00 00 0b 00 06 0a 01 7e 5a 51 c7 00 00 be 42 70 a4 d1 c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00
actions_combat_marketplace_abilitychangecoder.log.txt:16117:82 0b 3f 26 49 03 0b 00 06 0a 05 df cf 50 c7 40 4d 3e 43 e6 2d d5 c6 80 80 80 80 80 80 01 07 08
actions_combat_marketplace_abilitychangecoder.log.txt:38470:82 c0 50 26 49 03 10 00 04 80 80 80 40 82 07 0f 00 04 80 80 80 40 6a 05 07 00 06 0a 01 aa 5b 50
actions_combat_marketplace_abilitychangecoder.log.txt:51353:02 03 10 00 06 0a 00 00 80 3b c7 00 00 be 42 7f 6b f4 c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00
actions_combat_marketplace_abilitychangecoder.log.txt:51448:02 03 10 00 06 0a 00 00 80 3b c7 00 00 be 42 7f 6b f4 c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00
```

### Mode 06 movement selector 0c

Signature: `00 06 0c` (spaced or compact hex)

Matches: 1196

```text
actions_combat.log.txt:1744:82 37 24 26 49 03 1b 00 01 08 04 30 92 2c bb 46 00 00 be 42 19 b3 17 c6 15 00 06 0c 3d 0c 99 a8
actions_combat.log.txt:3552:02 03 1a 00 06 0c e2 12 d5 98 46 00 00 be 42 a6 00 03 c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00
actions_combat.log.txt:3557:00 00 22 00 00 00 00 00 10 00 06 0c de 5f ea 95 46 00 00 be 42 68 3b 0c c6 ff 00 00 00 10 00 00
actions_combat.log.txt:6543:02 03 09 00 06 0c 99 9c 9d 30 46 00 00 be 42 ec 5d 6f 45 ff 00 00 00 10 00 00 00 00 00 01 ff 00
actions_combat.log.txt:6557:82 f3 26 26 49 03 09 00 06 0c a3 9c 9d 30 46 00 00 be 42 ec 5d 6f 45 ff 00 00 00 10 00 00 00 00
actions_combat.log.txt:6571:02 03 09 00 06 0c ad 9c 9d 30 46 00 00 be 42 ec 5d 6f 45 ff 00 00 00 10 00 00 00 00 00 01 ff 00
actions_combat.log.txt:6585:02 03 09 00 06 0c b7 9c 9d 30 46 00 00 be 42 ec 5d 6f 45 ff 00 00 00 10 00 00 00 00 00 01 ff 00
actions_combat.log.txt:6599:02 03 09 00 06 0c c1 9c 9d 30 46 00 00 be 42 ec 5d 6f 45 ff 00 00 00 10 00 00 00 00 00 01 ff 00
actions_combat.log.txt:6617:02 03 09 00 06 0c d5 9c 9d 30 46 00 00 be 42 ec 5d 6f 45 ff 00 00 00 10 00 00 00 00 00 01 ff 00
actions_combat.log.txt:6631:02 03 09 00 06 0c df 9c 9d 30 46 00 00 be 42 ec 5d 6f 45 ff 00 00 00 10 00 00 00 00 00 01 ff 00
```

### Mode 06 movement selector 0e

Signature: `00 06 0e` (spaced or compact hex)

Matches: 1560

```text
actions_combat.log.txt:1659:91 93 46 00 00 be 42 66 fd f3 c5 0b 00 06 0e 05 4a 73 2a a5 46 00 00 be 42 b9 9c 20 c6 80 80 80
actions_combat.log.txt:1673:42 66 fd f3 c5 0b 00 06 0e 05 4a 73 2a a5 46 00 00 be 42 b9 9c 20 c6 ff 00 00 00 10 00 00 00 00
actions_combat.log.txt:1685:42 66 fd f3 c5 0b 00 06 0e 05 4a 73 2a a5 46 00 00 be 42 b9 9c 20 c6 ff 00 00 00 10 00 00 00 00
actions_combat.log.txt:1792:42 66 fd f3 c5 0b 00 06 0e 01 4f 57 3f a5 46 00 00 be 42 d7 ac 20 c6 80 80 80 80 80 80 01 07 08
actions_combat.log.txt:1920:02 03 1b 00 01 00 04 9f 88 c3 46 00 00 be 42 f4 50 19 c6 15 00 06 0e 05 35 0c 99 a8 46 00 00 be
actions_combat.log.txt:1937:82 5b 24 26 49 03 1b 00 01 00 04 ef 50 c5 46 00 00 be 42 c4 7d 19 c6 15 00 06 0e 00 2b 0c 99 a8
actions_combat.log.txt:1951:02 03 1b 00 01 00 04 e8 e6 c5 46 00 00 be 42 74 8c 19 c6 15 00 06 0e 00 21 0c 99 a8 46 00 00 be
actions_combat.log.txt:1969:02 03 1b 00 01 00 04 f4 7c c6 46 00 00 be 42 27 9b 19 c6 15 00 06 0e 00 1f 0c 99 a8 46 00 00 be
actions_combat.log.txt:3488:00 00 01 22 00 00 00 00 00 00 00 01 00 02 1f 00 10 00 06 0e 05 fb b3 21 96 46 00 00 be 42 71 c7
actions_combat.log.txt:3489:0c c6 80 80 80 80 80 80 01 07 08 00 00 01 00 02 1f 00 1a 00 06 0e 05 fb f1 12 99 46 00 00 be 42
```

### Repeated mode 06 suffix lead

Signature: `ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00` (spaced or compact hex)

Matches: 706

```text
actions_combat.log.txt:1921:42 14 e1 19 c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
actions_combat.log.txt:1938:46 00 00 be 42 14 e1 19 c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00
actions_combat.log.txt:1952:42 14 e1 19 c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
actions_combat.log.txt:1970:42 14 e1 19 c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
actions_combat.log.txt:6528:9d 30 46 00 00 be 42 ec 5d 6f 45 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00
actions_combat.log.txt:9362:27 1b c6 f8 df ec 44 fe 49 78 47 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00
actions_combat.log.txt:9430:44 fe 49 78 47 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
actions_combat.log.txt:9446:06 0e 00 ff 53 27 1b c6 f8 df ec 44 fe 49 78 47 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00
actions_combat.log.txt:9798:c6 5e 47 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
actions_combat.log.txt:9820:c6 5e 47 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
```

### Movement-state ff 01 64 01 tail

Signature: `ff 01 64 01` (spaced or compact hex)

Matches: 163

```text
abilities_use_destroyer_at_corrupted.log.txt:234:02 03 02 00 03 08 72 df 0f 47 00 20 8a c4 49 a0 fa c5 71 3e a9 04 ff 01 64 01 00 00 00 00 00 00
abilities_use_plague_zone_2_0_at_corrupted.log.txt:85:02 03 02 00 03 08 72 df 0f 47 00 20 8a c4 49 a0 fa c5 b3 ca a9 04 ff 01 64 01 00 00 00 00 00 00
actions_combat.log.txt:2719:02 03 02 00 03 09 08 00 66 23 da 46 97 d6 51 44 61 3f 19 c6 50 06 90 28 ff 01 64 01 00 00 00 00
actions_combat.log.txt:2736:02 03 02 00 03 08 8d 99 d9 46 e1 ab d2 44 75 10 18 c6 cd 06 90 28 ff 01 64 01 00 00 00 00 00 00
actions_combat.log.txt:2753:02 03 02 00 03 08 b3 0f d9 46 eb c2 1b 45 89 e1 16 c6 4a 07 90 28 ff 01 64 01 00 00 00 00 00 00
actions_combat.log.txt:2770:02 03 02 00 03 08 da 85 d8 46 93 bc 4b 45 9c b2 15 c6 c7 07 90 28 ff 01 64 01 00 00 00 00 00 00
actions_combat.log.txt:2787:02 03 02 00 03 08 00 fc d7 46 eb 42 79 45 b0 83 14 c6 44 08 90 28 ff 01 64 01 00 00 00 00 00 00
actions_combat.log.txt:2804:02 03 02 00 03 08 27 72 d7 46 f9 2a 92 45 c4 54 13 c6 c1 08 90 28 ff 01 64 01 00 00 00 00 00 00
actions_combat.log.txt:2825:02 03 02 00 03 08 4e e8 d6 46 d5 7a a6 45 d8 25 12 c6 3e 09 90 28 ff 01 64 01 00 00 00 00 00 00
actions_combat.log.txt:2853:02 03 02 00 03 08 87 19 d6 46 82 a6 c2 45 75 5f 10 c6 38 0a 90 28 ff 01 64 01 00 00 00 00 00 00
```

### Movement-state ff 01 4e 01 tail

Signature: `ff 01 4e 01` (spaced or compact hex)

Matches: 0

## Ability, Effect, And Combat-State Leads

### SERVER_PLAYER_ATTRIBUTE / ability load

Signature: `80 b2` (spaced or compact hex)

Matches: 271

```text
actions_Vector_unload_and_load_ability.log.txt:27:82 66 c7 f3 48 04 01 00 9b 02 08 80 b2 74 02 32 00 89 01 16 80 bc 45 00 2b 01 00 74 02 00 00 34
actions_Vector_loadhyperspeed.log.txt:6:02 04 01 00 b0 01 08 80 b2 0c 00 32 00 30 00
actions_Vector_loadandunloadhyperspeed.log.txt:28:82 1c cf f3 48 04 01 00 a0 02 08 80 b2 0b 00 32 00 16 00 16 80 bc 45 00 2c 01 00 0b 00 00 00 37
actions_Vector_loadandunloadhyperspeed.log.txt:89:02 04 01 00 a2 01 08 80 b2 c6 00 00 00 08 00
actions_Vector_loadandunloadhyperspeed.log.txt:113:02 04 01 00 a4 01 08 80 b2 0c 00 32 00 30 00
actions_Vector_loadandunloadhyperspeed.log.txt:237:82 ce da f3 48 04 01 00 a6 01 08 80 b2 0c 00 32 00 30 00
actions_Vector_loadandunloadhyperspeed.log.txt:261:02 04 01 00 a8 01 08 80 b2 0c 00 32 00 30 00
actions_Vector_loadandunloadhyperspeed.log.txt:285:02 04 01 00 aa 01 08 80 b2 0c 00 32 00 30 00
actions_Vector_loadandunloadhyperspeed.log.txt:309:02 04 01 00 ac 01 08 80 b2 0c 00 32 00 30 00
actions_Vector_loadandunloadhyperspeed.log.txt:333:02 04 01 00 ae 01 08 80 b2 0c 00 32 00 30 00
```

### SERVER_ABILITY_UNLOAD

Signature: `80 b3` (spaced or compact hex)

Matches: 308

```text
actions_Vector_unload_and_load_ability.log.txt:6:02 04 01 00 99 02 04 80 b3 74 02 16 80 bc 55 00 2a 01 00 74 02 00 00 34 02 1e 00 00 00 01 00 00
actions_Vector_unloadhyperspeed.log.txt:26:02 04 01 00 af 01 04 80 b3 0c 00
actions_Vector_loadandunloadhyperspeed.log.txt:15:02 04 01 00 9d 03 04 80 b3 0c 00 04 80 b3 0b 00 16 80 bc 55 00 11 01 00 0b 00 00 00 37 02 96 00
actions_Vector_loadandunloadhyperspeed.log.txt:101:82 0f d3 f3 48 04 01 00 a3 01 04 80 b3 c6 00
actions_Vector_loadandunloadhyperspeed.log.txt:225:02 04 01 00 a5 01 04 80 b3 0c 00
actions_Vector_loadandunloadhyperspeed.log.txt:249:02 04 01 00 a7 01 04 80 b3 0c 00
actions_Vector_loadandunloadhyperspeed.log.txt:273:82 06 db f3 48 04 01 00 a9 01 04 80 b3 0c 00
actions_Vector_loadandunloadhyperspeed.log.txt:297:02 04 01 00 ab 01 04 80 b3 0c 00
actions_Vector_loadandunloadhyperspeed.log.txt:321:82 3a db f3 48 04 01 00 ad 01 04 80 b3 0c 00
actions_combat.log.txt:154:00 16 80 bc 15 00 ab 02 00 11 00 00 00 f3 03 46 00 00 00 01 00 00 00 00 04 80 b3 11 00 16 80 bc
```

### Object599 adrenaline booster effect id

Signature: `4c 0a 00 28` (spaced or compact hex)

Matches: 1929

```text
actions_combat.log.txt:7685:02 03 10 00 02 0c de 61 5a 0d c7 00 40 30 c4 16 0b a9 46 0f 00 04 80 80 80 80 c0 4c 0a 00 28 01
actions_combat.log.txt:7686:01 0b 00 06 27 00 40 00 00 00 10 00 00 00 23 80 80 80 80 c0 4c 0a 00 28 01 01 0a 00 02 0c 6a 24
actions_combat.log.txt:8245:02 03 11 00 04 80 80 80 80 c0 4c 0a 00 28 01 01 0d 00 04 80 80 80 80 c0 4c 0a 00 28 01 01 00 00
actions_combat.log.txt:8309:82 09 29 26 49 03 10 00 04 80 80 80 80 c0 4c 0a 00 28 01 01 00 00
actions_combat.log.txt:8317:02 03 0a 00 04 80 80 80 80 c0 4c 0a 00 28 01 01 00 00
actions_combat.log.txt:9365:00 00 00 00 00 00 00 01 00 b6 03 ff 00 00 00 00 00 00 00 00 d4 1e 00 00 4c 0a 00 28 ff 01 00 00
actions_combat.log.txt:9383:00 00 00 d4 1e 00 00 4c 0a 00 28 ff 01 00 00 00 00 00 00 00 00 00 00 00 5a 0b 00 58 37 08 00 00
actions_combat.log.txt:9401:00 00 00 d4 1e 00 00 4c 0a 00 28 ff 01 00 00 00 00 00 00 00 00 00 00 00 5a 0b 00 58 37 08 00 00
actions_combat.log.txt:9418:d4 1e 00 00 4c 0a 00 28 ff 01 00 00 00 00 00 00 00 00 00 00 00 5a 0b 00 58 37 08 00 00 1f 07 08
actions_combat.log.txt:9433:00 01 00 b6 03 ff 00 00 00 00 00 00 00 00 d4 1e 00 00 4c 0a 00 28 ff 01 00 00 00 00 00 00 00 00
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
- Import only text logs or text-like dumps that contain no passwords, session tokens, private keys, client binaries, or unrelated game assets.
