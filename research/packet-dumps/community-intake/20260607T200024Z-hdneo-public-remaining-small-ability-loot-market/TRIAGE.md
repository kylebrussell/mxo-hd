# Packet Capture Triage Report

- Source kind: `directory`
- Source input: `/Users/kyle/Developer/mxo-hd/research/packet-dumps/community-intake/20260607T200024Z-hdneo-public-remaining-small-ability-loot-market/logs`
- Scanned files from: `/Users/kyle/Developer/mxo-hd/research/packet-dumps/community-intake/20260607T200024Z-hdneo-public-remaining-small-ability-loot-market/logs`
- Generated UTC: `2026-06-07T20:00:26Z`
- Max sample lines per pattern: `10`

Use this report to decide whether received logs are worth importing into `research/packet-dumps/`. Matches are byte-string leads, not decoded packet claims.

## Vendor And Interaction Leads

### SERVER_VENDOR_OPEN candidate

Signature: `81 0d` (spaced or compact hex)

Matches: 4

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:16937:05 01 a8 64 ec 13 b2 09 ec 13 07 08 00 00 81 0d ba 00 00 02 19 0d 00 00 01 00 0c 57 02 ec cd ab
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:41903:02 04 01 02 81 0d 34 81 14 20 7c a7 0e d2 73 de 11 a1 47 00 0d 60 1c a6 94 16 4f b6 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:63739:02 03 02 00 02 80 84 4f 80 50 08 00 0a 0a 11 00 02 0e 03 31 10 65 14 c8 00 40 30 c4 81 0d cb c7
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:83290:01 0c 81 9c f0 01 30 39 24 06 00 50 01 00 01 3d 01 32 81 0d 7c ad d9 43 00 00 00 00 c0 98 d0 40
```

### CLIENT_VENDOR_BUY candidate

Signature: `81 0e` (spaced or compact hex)

Matches: 2

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:48437:80 c0 62 01 00 28 81 0e 02 00 00 10 00 0b 00 06 0e 00 fe 99 4d 0e c8 00 40 30 c4 c4 c2 c5 c7 ff
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:57979:02 03 02 00 01 0c 81 0e fe 14 c8 00 c0 32 c4 af db cf c7 00 00
```

### CLIENT_VENDOR_SELL candidate

Signature: `81 11` (spaced or compact hex)

Matches: 5

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:26302:02 0c 84 88 88 eb c4 00 f0 7f 45 f2 d1 60 c6 03 00 02 08 94 81 11 c5 00 f0 7f 45 5a 09 70 c6 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:83473:02 04 01 00 3a 01 04 81 11 00 57
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:83784:02 04 01 00 3b 01 04 81 11 00 45
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:84071:02 04 01 00 3c 01 04 81 11 00 46
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:84303:02 04 01 00 3d 01 04 81 11 00 4e
```

### CR2 static object interaction

Signature: `80 c8` (spaced or compact hex)

Matches: 5

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:393:08 03 f2 8f 8d 79 40 3e 84 dc c1 c5 e1 02 71 7a 05 44 80 c8 75 68 7d 23 66 75 26 b1 43 66 bf d1
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:15910:02 03 02 00 01 02 00 00 00 04 01 00 41 01 08 80 c8 02 00 50 4b 03 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:16159:02 04 01 00 47 01 08 80 c8 81 07 50 4b 03 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:54608:02 03 11 00 06 0e 05 a4 56 ed 13 c8 00 40 30 c4 80 c8 cc c7 ff 00 00 00 10 00 00 00 00 00 01 ff
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:54630:02 03 02 00 02 80 04 0b 11 00 06 0e 05 7c 56 ed 13 c8 00 40 30 c4 80 c8 cc c7 ff 00 00 00 10 00
```

### CR1 static object interaction

Signature: `80 c3` (spaced or compact hex)

Matches: 54

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_loot_fm11sk_codebyte1_from_corrupted.log.txt:72:00 00 00 00 00 00 00 00 00 00 00 00 00 10 00 00 22 d5 0b 5e 61 1c 00 c6 01 80 c3 d7 09 00 58 4c
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:15834:02 03 02 00 01 0c 90 15 fe da 47 f0 bf 2d 44 80 c3 7f c5 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:19039:cc 31 c7 03 00 02 0c ec 6f af 4b c5 00 00 be 42 b8 bc 32 c7 00 00 04 01 01 4c 01 06 80 c3 32 1c
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:19201:72 65 72 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 10 00 00 22 c5 02 1c 01 80 c3 8e 08
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:20635:19 c0 18 65 c5 00 f0 02 45 d6 46 0d c7 00 00 04 01 01 4d 01 06 80 c3 36 52 0a 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:23220:00 00 00 00 00 00 00 00 00 00 00 00 00 10 00 00 22 c5 01 75 01 80 c3 8f 08 00 58 4c 0a 00 28 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:23997:02 0c 7b ef 01 81 c5 00 f0 02 45 d7 3d 0a c7 00 00 04 01 01 4e 01 06 80 c3 22 fc 0a 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:24834:00 00 60 3d a5 d2 c0 10 e3 00 00 00 04 01 01 4f 01 06 80 c3 5c 1c 0b 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:26170:50 01 06 80 c3 60 45 0b 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:28726:00 04 01 01 51 01 06 80 c3 ce a0 0b 00
```

### SERVER object/state bundle

Signature: `80 bc` (spaced or compact hex)

Matches: 3043

```text
mxopackets1_crashover2k_afterwhoruneo_proxy_003_abs_evade_Combat2.log.txt:82:80 bd 05 d9 00 00 00 00 00 00 01 06 80 c1 01 00 c8 00 16 80 bc 19 00 37 04 00 d9 00 00 00 70 02
mxopackets1_crashover2k_afterwhoruneo_proxy_003_abs_evade_Combat2.log.txt:83:00 00 00 00 00 00 00 00 00 16 80 bc 1d 00 38 04 00 70 02 00 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_001executeevadecombat.log.txt:87:00 00 00 00 00 00 01 06 80 c1 01 00 c8 00 16 80 bc 19 00 96 00 00 d9 00 00 00 70 02 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_001executeevadecombat.log.txt:88:00 00 00 00 00 16 80 bc 1d 00 97 00 00 70 02 00 00 00 00 00 00 00 00 00 00 00 00 00 01 82 01 41
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_001executeevadecombat.log.txt:218:00 be 42 fe d6 65 46 00 00 04 01 01 83 03 16 80 bc 1d 00 98 00 00 9e 02 00 00 72 00 b0 ff ff ff
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_001executeevadecombat.log.txt:219:00 00 00 00 00 16 80 bc 15 00 99 00 00 9e 02 00 00 70 00 b0 ff ff ff 00 00 00 00 00 39 81 68 2d
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:620:08 00 08 02 08 80 b2 51 00 1e 00 08 02 08 80 b2 11 00 32 00 08 02 16 80 bc 45 03 11 00 00 02 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:621:00 00 11 00 32 00 00 00 00 00 00 00 00 16 80 bc 45 00 02 00 00 02 00 00 00 cc 00 0c 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:622:00 00 00 00 16 80 bc 45 00 03 00 00 0b 00 00 00 37 02 96 00 00 00 00 00 00 00 00 16 80 bc 45 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:623:04 00 00 2d 00 00 00 23 04 0a 00 00 00 00 00 00 00 00 16 80 bc 45 00 05 00 00 2d 00 00 00 20 04
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

Matches: 7

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:47960:00 00 00 00 00 00 00 00 00 00 00 00 00 00 01 00 ff 02 ff 00 00 00 00 00 00 00 00 00 00 00 00 30
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:48584:00 00 00 00 00 00 00 00 00 00 00 00 00 00 01 00 ff 03 ff 00 00 00 00 00 00 00 00 00 00 00 00 9c
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:98331:00 00 28 0a 00 69 75 00 00 ff ff 00 00 00 00 52 c7 de 12 06 00 00 00 00 00 00 00 00 01 00 ff 71
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:98345:12 06 00 00 00 00 00 00 00 00 01 00 ff 71 05 00 04 8e b6 00 00 00 00 08 00 12 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:98359:12 06 00 00 00 00 00 00 00 00 01 00 ff 71 05 00 04 8e b6 00 00 00 00 08 00 12 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:98381:12 06 00 00 00 00 00 00 00 00 01 00 ff 71 05 00 04 8e b6 00 00 00 00 08 00 12 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:98403:12 06 00 00 00 00 00 00 00 00 01 00 ff 71 05 00 04 8e b6 00 00 00 00 08 00 12 00 00 00 00 00 00
```

### Selector ff continuation prefix 00 01 04 ff

Signature: `00 01 04 ff` (spaced or compact hex)

Matches: 3

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:75936:02 03 02 00 01 04 ff 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:78932:01 12 03 67 01 00 00 00 00 03 00 07 00 27 00 40 00 00 00 10 00 00 01 04 ff 00 00 00 10 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:85267:02 03 02 00 01 04 ff 00 00
```

### Selector ff continuation prefix 00 01 10 ff

Signature: `00 01 10 ff` (spaced or compact hex)

Matches: 40

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:2004:00 00 01 10 ff 00 00 00 00 93 55 01 00 00 80 18 01 11 3b f0 01 00 00 00 01 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:2030:00 00 00 01 10 ff 00 00 00 00 93 55 01 00 00 80 18 01 11 3b f0 01 00 00 00 01 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:2220:49 33 00 00 ff 7b 00 00 00 00 30 04 00 28 04 00 00 00 00 00 00 00 00 01 10 ff 00 00 00 00 93 55
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:2238:00 00 00 00 01 10 ff 00 00 00 00 93 55 01 00 80 00 18 01 12 16 9f 01 00 00 00 01 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:2409:00 00 01 10 ff 00 00 00 00 93 55 01 00 80 80 18 01 12 16 9f 01 00 00 00 01 00 15 00 02 2e 00 40
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:2424:00 49 33 00 00 ff 7b 00 00 00 00 30 04 00 28 04 00 00 00 00 00 00 00 00 01 10 ff 00 00 00 00 93
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:2438:00 ff 7b 00 00 00 00 30 04 00 28 04 00 00 00 00 00 00 00 00 01 10 ff 00 00 00 00 93 55 01 00 80
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:2581:00 00 00 00 01 10 ff 00 00 00 00 93 55 01 00 80 00 18 01 12 17 9f 01 00 00 00 01 00 0d 00 02 80
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:2595:00 00 00 00 01 10 ff 00 00 00 00 93 55 01 00 80 00 18 01 12 17 9f 01 00 00 00 01 00 17 00 04 80
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:2609:00 ff 7b 00 00 00 00 30 04 00 28 04 00 00 00 00 00 00 00 00 01 10 ff 00 00 00 00 93 55 01 00 80
```

### Selector ff continuation prefix 00 00 00 ff

Signature: `00 00 00 ff` (spaced or compact hex)

Matches: 3037

```text
mxopackets1_crashover2k_afterwhoruneo_proxy_003_abs_evade_Combat2.log.txt:24:00 00 23 1a e9 88 80 b0 04 00 02 8e 00 00 00 01 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ff 28
mxopackets1_crashover2k_afterwhoruneo_proxy_003_abs_evade_Combat2.log.txt:27:00 00 1d 0a 00 28 06 00 00 00 00 ff 00 00 00 00 01 00 00 00 00 00 00 00 80 3f ee 03 00 00 4b b1
mxopackets1_crashover2k_afterwhoruneo_proxy_003_abs_evade_Combat2.log.txt:46:02 8e 00 00 00 01 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ff 28 0a 32 00 f0 20 46 14 00 d1 ab
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_001executeevadecombat.log.txt:34:00 00 23 1a e9 88 80 b0 04 00 02 8e 00 00 00 01 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ff 28
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_001executeevadecombat.log.txt:36:00 00 00 00 00 ff 00 00 00 00 00 f7 00 00 00 f7 00 00 00 00 00 28 0a ff 00 31 5e 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_001executeevadecombat.log.txt:37:00 00 1d 0a 00 28 01 00 00 00 00 ff 00 00 00 00 00 00 00 00 00 00 00 00 80 3f 11 00 00 00 4b b1
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_001executeevadecombat.log.txt:61:02 8e 00 00 00 01 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ff 28 0a 32 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_001executeevadecombat.log.txt:62:00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ff 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_001executeevadecombat.log.txt:247:23 1a e9 88 80 b0 04 00 02 8e 00 00 00 01 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ff 28 0a 32
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_001executeevadecombat.log.txt:249:00 00 00 ff 00 00 00 00 00 f7 00 00 00 e5 00 00 00 00 00 e1 09 ff 00 31 5e 00 00 ff 00 00 00 00
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

Matches: 140

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_loot_fm11sk_codebyte1_from_corrupted.log.txt:71:02 03 01 00 0c 57 02 33 cd ab 13 8e 47 6f 6c 64 20 42 6c 6f 6f 64 20 43 68 61 6d 70 69 6f 6e 00
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_001executeevadecombat.log.txt:171:02 03 01 00 0c 57 02 0a cd ab 13 ae 43 6f 72 72 75 70 74 65 64 00 00 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:1467:02 03 01 00 0c 57 02 e8 cd ab 12 8e 43 72 6f 73 73 62 6f 6e 65 73 20 54 61 72 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:1474:00 00 00 00 00 f2 04 35 3f 00 00 00 00 f3 04 35 3f 27 00 00 01 00 0c 57 02 b5 cd ab 1a af ff 43
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:1504:02 03 01 00 02 07 00 29 00 02 02 00 01 00 0c 57 02 e6 cd ab 11 8e 43 72 6f 73 73 62 6f 6e 65 73
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:1541:02 03 01 00 0c 57 02 c7 cd ab 18 ae 43 6f 72 72 75 70 74 65 64 00 00 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:1548:08 00 00 80 02 01 1b 00 00 01 00 0c 57 02 e2 cd ab 12 8e 43 72 6f 73 73 62 6f 6e 65 73 20 54 61
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:1551:83 01 02 a8 07 51 01 b2 09 51 01 07 08 00 00 80 02 01 19 00 00 01 00 0c 57 02 c8 cd ab 17 ae 43
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:1580:02 0e 04 d4 57 cb da 47 f0 bf 2d 44 52 dc ed 45 01 00 0c 57 02 e4 cd ab 12 8e 43 72 6f 73 73 62
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:1587:00 00 80 02 01 11 00 00 01 00 0c 57 02 e7 cd ab 11 8e 43 72 6f 73 73 62 6f 6e 65 73 20 42 75 6d
```

### Adjacent Object599 boundary prefix

Signature: `00 0c 57 02` (spaced or compact hex)

Matches: 137

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_loot_fm11sk_codebyte1_from_corrupted.log.txt:71:02 03 01 00 0c 57 02 33 cd ab 13 8e 47 6f 6c 64 20 42 6c 6f 6f 64 20 43 68 61 6d 70 69 6f 6e 00
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_001executeevadecombat.log.txt:171:02 03 01 00 0c 57 02 0a cd ab 13 ae 43 6f 72 72 75 70 74 65 64 00 00 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:1467:02 03 01 00 0c 57 02 e8 cd ab 12 8e 43 72 6f 73 73 62 6f 6e 65 73 20 54 61 72 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:1474:00 00 00 00 00 f2 04 35 3f 00 00 00 00 f3 04 35 3f 27 00 00 01 00 0c 57 02 b5 cd ab 1a af ff 43
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:1504:02 03 01 00 02 07 00 29 00 02 02 00 01 00 0c 57 02 e6 cd ab 11 8e 43 72 6f 73 73 62 6f 6e 65 73
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:1541:02 03 01 00 0c 57 02 c7 cd ab 18 ae 43 6f 72 72 75 70 74 65 64 00 00 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:1548:08 00 00 80 02 01 1b 00 00 01 00 0c 57 02 e2 cd ab 12 8e 43 72 6f 73 73 62 6f 6e 65 73 20 54 61
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:1551:83 01 02 a8 07 51 01 b2 09 51 01 07 08 00 00 80 02 01 19 00 00 01 00 0c 57 02 c8 cd ab 17 ae 43
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:1580:02 0e 04 d4 57 cb da 47 f0 bf 2d 44 52 dc ed 45 01 00 0c 57 02 e4 cd ab 12 8e 43 72 6f 73 73 62
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:1587:00 00 80 02 01 11 00 00 01 00 0c 57 02 e7 cd ab 11 8e 43 72 6f 73 73 62 6f 6e 65 73 20 42 75 6d
```

### Header-like selector 50

Signature: `01 00 08 50` (spaced or compact hex)

Matches: 4

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:1590:03 a8 07 e1 00 b2 09 e1 00 07 08 00 00 80 02 01 0f 00 00 01 00 08 50 02 13 03 f0 3d 29 cd ab 02
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:74590:b2 09 91 05 07 08 00 00 81 c2 1e 00 00 02 09 09 00 00 01 00 08 50 02 38 2f 70 25 51 cd ab 02 09
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:80216:02 03 01 00 08 50 02 38 2f 70 25 51 cd ab 02 09 00 00 00 00 80 d3 f5 c0 00 00 00 00 00 c8 94 40
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:81350:00 06 ff 35 08 00 00 17 00 00 01 00 08 50 02 ea 01 30 39 61 cd ab 01 01 00 00 00 00 00 9a d0 40
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

Matches: 0

### Header-like selector d0

Signature: `01 00 08 d0` (spaced or compact hex)

Matches: 13

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:1470:b2 09 51 01 38 21 00 00 80 02 01 29 00 00 01 00 08 d0 20 94 0a 50 46 be cd ab 08 fd 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:1478:a8 07 51 01 b2 09 51 01 42 21 00 00 80 02 01 25 00 00 01 00 08 d0 20 23 03 f0 3d ba cd ab 07 fd
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:1652:02 03 01 00 02 13 00 09 00 02 0c 80 e8 78 e5 47 00 40 2b 44 54 8c f6 45 01 00 08 d0 20 21 03 f0
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:14922:04 00 00 01 00 08 d0 20 22 03 f0 3d bd cd ab 08 fd 00 00 00 00 60 1a fc 40 00 00 00 00 00 b8 85
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:15114:82 97 c3 40 48 03 01 00 08 d0 20 d2 07 50 4b c3 cd ab 07 fd 00 00 00 00 30 28 fb 40 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:15499:02 03 01 00 08 d0 20 b8 07 90 42 c4 cd ab 07 fd 00 00 00 00 b0 0c fb 40 00 00 00 00 00 b8 85 40
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:37263:02 03 01 00 08 d0 20 1d 00 30 10 f8 cd ab 08 fd 00 00 00 00 00 d9 02 c1 00 00 00 00 00 58 86 c0
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:81342:02 03 02 00 02 80 80 80 80 80 84 76 0c 00 58 01 00 00 08 00 01 00 08 d0 20 ef 01 30 39 1c cd ab
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:81378:0c a8 04 96 00 b2 09 96 00 07 08 00 00 80 02 01 11 00 00 01 00 08 d0 20 b0 00 20 4e 1f cd ab 08
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:81382:00 58 00 00 03 00 00 00 00 00 00 00 80 3f 00 00 00 00 2e bd 3b b3 0f 00 00 01 00 08 d0 20 ee 01
```

## Movement And Object-State Leads

### Mode 06 movement selector 08

Signature: `00 06 08` (spaced or compact hex)

Matches: 189

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:22910:02 03 13 00 02 0c 54 fa dc 84 c5 00 f0 02 45 da 7d 0f c7 11 00 06 08 0f 60 12 45 00 f0 02 45 bf
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:31811:02 03 1b 00 02 0e 04 1e 27 3c 22 c6 00 b8 dd 45 f7 fa e9 c5 19 00 06 08 a7 bb ed c5 00 b8 dd 45
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:33257:02 03 02 00 02 80 80 80 10 72 00 19 00 06 08 a7 bb ed c5 00 b8 dd 45 59 48 d5 c5 80 80 80 80 c0
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:33258:11 0a 00 28 01 04 13 00 06 08 c4 df ef c5 00 b8 dd 45 0f 3f d3 c5 80 80 80 80 c0 9c 05 00 28 01
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:33259:05 05 00 06 08 ab b2 e9 c5 00 b8 dd 45 f1 92 d7 c5 80 80 80 80 c0 11 0a 00 28 01 04 00 00 04 02
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:33480:82 8f 0d 41 48 03 19 00 06 08 a7 bb ed c5 00 b8 dd 45 59 48 d5 c5 80 80 80 40 df 07 05 00 06 27
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:33513:02 03 19 00 06 08 a7 bb ed c5 00 b8 dd 45 59 48 d5 c5 80 80 80 40 e2 07 13 00 02 08 c4 df ef c5
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:33514:00 b8 dd 45 0f 3f d3 c5 05 00 06 08 ab b2 e9 c5 00 b8 dd 45 f1 92 d7 c5 80 80 80 40 e2 07 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:33761:02 03 1d 00 02 0c 40 48 dc 1e c6 00 b8 dd 45 a8 7d e4 c5 19 00 06 08 a7 bb ed c5 00 b8 dd 45 59
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:33762:48 d5 c5 80 80 80 40 ac 05 13 00 02 08 94 8d ec c5 00 b8 dd 45 57 f5 d4 c5 05 00 06 08 ab b2 e9
```

### Mode 06 movement selector 0a

Signature: `00 06 0a` (spaced or compact hex)

Matches: 21

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:8447:02 03 29 00 02 02 00 25 00 06 0a 00 c1 54 d7 47 f0 bf 2d 44 09 e4 a9 45 80 80 80 80 80 80 01 45
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:8456:82 0c ae 40 48 03 25 00 06 0a 00 c1 54 d7 47 f0 bf 2d 44 09 e4 a9 45 80 80 80 80 80 80 01 47 21
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:10013:02 03 29 00 06 0a 00 09 5d e4 47 00 40 2b 44 dc 39 f6 45 80 80 80 80 80 80 01 42 21 00 00 25 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:13286:02 03 29 00 02 0c 1d a9 47 e4 47 00 40 2b 44 f4 13 f6 45 25 00 06 0a 00 dd c5 e3 47 00 40 2b 44
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:13296:02 03 29 00 02 0c 13 a9 47 e4 47 00 40 2b 44 f4 13 f6 45 25 00 06 0a 00 dd c5 e3 47 00 40 2b 44
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:13372:02 03 29 00 02 0c d7 a9 47 e4 47 00 40 2b 44 f4 13 f6 45 25 00 06 0a 00 9f ba e3 47 00 40 2b 44
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:33619:00 00 00 00 00 90 08 00 58 37 08 00 00 1f 06 08 00 00 00 00 10 00 22 00 00 00 00 00 05 00 06 0a
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:35777:65 d3 e6 c5 13 00 06 0a 00 94 8d ec c5 00 b8 dd 45 57 f5 d4 c5 80 80 80 40 be 0b 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:46845:02 03 02 00 02 80 84 3c 80 50 64 00 06 0a 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:48069:00 00 22 00 00 00 00 00 08 00 06 0a 00 d2 17 14 c8 00 40 30 c4 82 5c cd c7 80 80 80 40 d7 03 04
```

### Mode 06 movement selector 0c

Signature: `00 06 0c` (spaced or compact hex)

Matches: 807

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:1726:02 03 25 00 02 0e 01 4e e5 6f d9 47 f0 bf 2d 44 cc ca d6 45 21 00 06 0c 60 c2 b2 e3 47 00 40 2b
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:1762:82 d7 99 40 48 03 25 00 02 0e 01 39 fb 7a d9 47 f0 bf 2d 44 3c ae d6 45 21 00 06 0c 56 c2 b2 e3
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:1799:02 03 21 00 06 0c 4c c2 b2 e3 47 00 40 2b 44 ba 6d cf 45 ff 00 00 00 10 00 00 00 00 00 01 ff 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:1836:02 03 25 00 02 0c 3a 25 91 d9 47 f0 bf 2d 44 c4 eb d6 45 21 00 06 0c 42 c2 b2 e3 47 00 40 2b 44
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:1923:02 03 21 00 06 0c 25 c2 b2 e3 47 00 40 2b 44 ba 6d cf 45 ff 00 00 00 10 00 00 00 00 00 01 ff 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:2055:98 75 34 45 1b 00 06 0c 34 e5 b5 d8 47 f0 bf 2d 44 4b bd bd 45 80 80 80 80 80 80 01 12 08 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:2056:19 00 06 0c 5e 57 cb da 47 f0 bf 2d 44 52 dc ed 45 80 80 80 80 80 80 01 12 08 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:2064:02 03 29 00 06 0c a4 a6 40 e5 47 00 40 2b 44 c8 54 b2 45 80 80 80 80 80 80 01 11 08 00 00 17 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:2065:06 0e 00 22 3a 75 e1 47 f0 bf 2d 44 1c 07 7f 45 80 80 80 80 80 80 01 12 08 00 00 11 00 06 0c 3b
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:2066:72 b5 d6 47 f0 bf 2d 44 36 1e 62 45 80 80 80 80 80 80 01 12 08 00 00 03 00 06 0c 7d 20 7c e1 47
```

### Mode 06 movement selector 0e

Signature: `00 06 0e` (spaced or compact hex)

Matches: 947

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:1660:00 00 80 02 01 03 00 00 25 00 02 0e 05 62 e5 6f d9 47 f0 bf 2d 44 cc ca d6 45 21 00 06 0e 05 74
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:1689:02 03 25 00 02 0e 05 58 e5 6f d9 47 f0 bf 2d 44 cc ca d6 45 21 00 06 0e 05 6a c2 b2 e3 47 00 40
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:1873:02 03 25 00 02 0c 3a 47 9c d9 47 f0 bf 2d 44 70 08 d7 45 21 00 06 0e 00 38 c2 b2 e3 47 00 40 2b
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:1907:02 03 21 00 06 0e 00 2e c2 b2 e3 47 00 40 2b 44 ba 6d cf 45 ff 00 00 00 10 00 00 00 00 00 01 ff
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:2076:82 67 9a 40 48 03 17 00 06 0e 00 22 3a 75 e1 47 f0 bf 2d 44 1c 07 7f 45 ff 00 00 00 10 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:2098:02 03 21 00 06 0e 04 76 c2 b2 e3 47 00 40 2b 44 ba 6d cf 45 80 80 80 80 80 80 01 12 08 00 00 1d
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:3440:01 00 80 00 08 01 12 17 9f 01 00 00 00 01 00 1b 00 06 0e 05 46 e5 b5 d8 47 f0 bf 2d 44 4b bd bd
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:3458:c8 00 00 00 00 59 80 80 10 0e 0a 1b 00 06 0e 05 3c e5 b5 d8 47 f0 bf 2d 44 4b bd bd 45 ff 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:3463:08 00 00 1f 07 08 00 00 00 00 00 00 22 00 00 00 00 00 19 00 06 0e 05 4b 57 cb da 47 f0 bf 2d 44
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:3484:00 01 10 ff 00 00 00 00 93 55 01 00 80 00 08 01 12 17 9f 01 00 00 00 01 00 1b 00 06 0e 05 32 e5
```

### Repeated mode 06 suffix lead

Signature: `ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00` (spaced or compact hex)

Matches: 541

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:1690:2b 44 ba 6d cf 45 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:1727:44 ba 6d cf 45 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:1763:47 00 40 2b 44 ba 6d cf 45 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:1837:ba 6d cf 45 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:1874:44 ba 6d cf 45 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:2267:e8 78 e5 47 00 40 2b 44 54 8c f6 45 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:3441:45 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:3464:52 dc ed 45 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:3485:b5 d8 47 f0 bf 2d 44 4b bd bd 45 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:3550:52 dc ed 45 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
```

### Movement-state ff 01 64 01 tail

Signature: `ff 01 64 01` (spaced or compact hex)

Matches: 356

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:2947:02 03 02 00 03 28 01 c0 00 98 d9 00 00 e1 e1 47 f0 bf 2d 44 a3 2b 91 45 0e 9d c1 0b ff 01 64 01
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:2968:02 03 02 00 03 28 01 c0 00 98 d9 00 00 e1 e1 47 f0 bf 2d 44 a3 2b 91 45 8a 9d c1 0b ff 01 64 01
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:2985:02 03 02 00 03 08 00 e1 e1 47 f0 bf 2d 44 a3 2b 91 45 07 9e c1 0b ff 01 64 01 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:3001:82 9a 9d 40 48 03 02 00 03 08 00 e1 e1 47 f0 bf 2d 44 a3 2b 91 45 84 9e c1 0b ff 01 64 01 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:19049:02 03 02 00 03 09 08 00 56 d7 79 c5 25 57 2f 44 6a 48 33 c7 2d 00 c6 0b ff 01 64 01 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:19067:02 03 02 00 03 08 21 aa 79 c5 95 b0 a9 44 40 d5 32 c7 9a 00 c6 0b ff 01 64 01 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:19084:02 03 02 00 03 08 f2 81 79 c5 a8 9b f0 44 e1 6e 32 c7 17 01 c6 0b ff 01 64 01 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:19101:02 03 02 00 03 08 c2 59 79 c5 df d3 1a 45 83 08 32 c7 85 01 c6 0b ff 01 64 01 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:19118:02 03 02 00 03 08 93 31 79 c5 6a 6a 3c 45 24 a2 31 c7 02 02 c6 0b ff 01 64 01 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:19135:02 03 02 00 03 08 5d 04 79 c5 81 15 61 45 fa 2e 31 c7 8e 02 c6 0b ff 01 64 01 00 00 00 00 00 00
```

### Movement-state ff 01 4e 01 tail

Signature: `ff 01 4e 01` (spaced or compact hex)

Matches: 5

```text
mxopackets1_crashover2k_afterwhoruneo_proxy_003_abs_evade_Combat2.log.txt:23:ff 01 4e 01 00 00 00 00 00 00 00 00 00 00 00 08 41 6e 64 65 72 73 6f 6e 06 54 68 6f 6d 61 73 ff
mxopackets1_crashover2k_afterwhoruneo_proxy_003_abs_evade_Combat2.log.txt:44:02 03 02 00 03 08 38 57 84 c7 00 00 be 42 74 b3 80 46 6a d6 36 20 ff 01 4e 01 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_001executeevadecombat.log.txt:33:ff 01 4e 01 00 00 00 00 00 00 00 00 00 00 00 08 41 6e 64 65 72 73 6f 6e 06 54 68 6f 6d 61 73 ff
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_001executeevadecombat.log.txt:59:02 03 02 00 03 08 66 f4 81 c7 00 00 be 42 00 40 61 46 66 4c 12 1f ff 01 4e 01 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_001executeevadecombat.log.txt:269:02 03 02 00 03 09 0c 00 66 f4 81 c7 00 00 be 42 00 40 61 46 9a 6d 12 1f ff 01 4e 01 00 00 00 00
```

## Ability, Effect, And Combat-State Leads

### SERVER_PLAYER_ATTRIBUTE / ability load

Signature: `80 b2` (spaced or compact hex)

Matches: 346

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:619:08 80 b2 4e 00 08 00 08 02 08 80 b2 52 00 1e 00 08 02 08 80 b2 54 00 0d 00 08 02 08 80 b2 4f 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:620:08 00 08 02 08 80 b2 51 00 1e 00 08 02 08 80 b2 11 00 32 00 08 02 16 80 bc 45 03 11 00 00 02 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:626:00 00 00 83 02 c8 00 00 00 00 00 00 00 00 08 80 b2 3d 04 00 00 08 02 16 80 bc 45 03 3d 04 00 43
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:630:04 01 00 00 00 00 00 00 00 00 08 80 b2 35 04 00 00 08 02 16 80 bc 45 03 35 04 00 62 00 00 00 35
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:632:00 08 80 b2 3e 04 00 00 08 02 16 80 bc 45 03 3e 04 00 4f 01 00 00 3e 04 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:634:5a 01 00 00 1d 04 0f 00 00 00 00 00 00 00 00 08 80 b2 3f 04 00 00 08 02 16 80 bc 45 03 3f 04 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:858:00 02 00 00 00 cc 00 0c 00 00 00 01 00 00 00 00 08 80 b2 11 00 32 00 08 02 16 80 bc 45 03 11 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:869:16 80 bc 45 03 3d 04 00 43 00 00 00 3d 04 00 00 00 00 01 00 00 00 00 08 80 b2 3d 04 00 00 08 02
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:892:00 00 00 00 00 00 00 00 08 80 b2 35 04 00 00 08 02 16 80 bc 45 03 35 04 00 62 00 00 00 35 04 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:894:80 b3 3e 04 16 80 bc 45 03 3e 04 00 4f 01 00 00 3e 04 00 00 00 00 01 00 00 00 00 08 80 b2 3e 04
```

### SERVER_ABILITY_UNLOAD

Signature: `80 b3` (spaced or compact hex)

Matches: 387

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:856:00 00 00 00 00 00 16 80 bc 56 00 6a 00 00 c3 b2 00 00 9d 00 1f 00 00 00 00 00 00 00 00 04 80 b3
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:868:00 00 00 00 16 80 bc 55 00 71 00 00 34 00 00 00 83 02 c8 00 00 00 00 00 00 00 00 04 80 b3 3d 04
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:894:80 b3 3e 04 16 80 bc 45 03 3e 04 00 4f 01 00 00 3e 04 00 00 00 00 01 00 00 00 00 08 80 b2 3e 04
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:898:16 80 bc 55 00 78 00 00 5a 01 00 00 1d 04 0f 00 00 00 00 00 00 00 00 04 80 b3 3f 04 16 80 bc 45
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:929:55 00 82 00 00 cd 01 00 00 e3 03 0f 00 00 00 00 00 00 00 00 04 80 b3 40 04 16 80 bc 45 03 40 04
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:1255:00 00 00 00 cb 02 00 00 00 00 01 00 00 00 00 04 80 b3 11 00 16 80 bc 45 03 11 00 00 02 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:1256:11 00 32 00 00 00 01 00 00 00 00 04 80 b3 35 04 16 80 bc 45 03 35 04 00 62 00 00 00 35 04 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:1257:00 00 01 00 00 00 00 04 80 b3 3d 04 16 80 bc 45 03 3d 04 00 43 00 00 00 3d 04 00 00 00 00 01 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:1258:00 00 00 04 80 b3 3e 04 16 80 bc 45 03 3e 04 00 4f 01 00 00 3e 04 00 00 00 00 01 00 00 00 00 04
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:1259:80 b3 3f 04 16 80 bc 45 03 3f 04 00 64 01 00 00 3f 04 00 00 00 00 01 00 00 00 00 04 80 b3 40 04
```

### Object599 adrenaline booster effect id

Signature: `4c 0a 00 28` (spaced or compact hex)

Matches: 724

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:1553:10 00 00 22 00 40 00 00 d5 03 19 30 23 00 c6 01 a0 02 d3 14 00 00 4e 4c 0a 00 28 0a 00 00 00 3c
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:2080:00 f0 1a 00 00 4c 0a 00 28 ff 03 00 00 00 00 00 00 00 00 00 00 00 14 00 00 4e 37 08 00 00 1f 12
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:17612:42 e6 be 32 c7 80 80 80 80 c0 4c 0a 00 28 01 01 05 00 02 0e 04 6c 94 4f 99 c5 00 00 be 42 64 c7
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:17625:4c 0a 00 28 ff 01 00 00 00 00 00 00 00 00 00 00 00 7c 01 00 58 37 08 00 00 1f 07 08 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:17641:4c 0a 00 28 ff 01 00 00 00 00 00 00 00 00 00 00 00 7c 01 00 58 37 08 00 00 1f 07 08 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:17657:4c 0a 00 28 ff 01 00 00 00 00 00 00 00 00 00 00 00 7c 01 00 58 37 08 00 00 1f 07 08 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:17674:4c 0a 00 28 ff 01 00 00 00 00 00 00 00 00 00 00 00 7c 01 00 58 37 08 00 00 1f 07 08 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:17696:4c 0a 00 28 ff 01 00 00 00 00 00 00 00 00 00 00 00 7c 01 00 58 37 08 00 00 1f 07 08 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:17718:4c 0a 00 28 ff 01 00 00 00 00 00 00 00 00 00 00 00 7c 01 00 58 37 08 00 00 1f 07 08 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:17740:4c 0a 00 28 ff 01 00 00 00 00 00 00 00 00 00 00 00 7c 01 00 58 37 08 00 00 1f 07 08 00 00 00 00
```

### Object599 fast healing effect id

Signature: `b5 07 00 28` (spaced or compact hex)

Matches: 0

### Object599 accelerated spawn effect id

Signature: `66 00 00 04` (spaced or compact hex)

Matches: 17

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:41408:04 71 00 00 04 71 00 00 04 66 00 00 04 22 81 13 20 7c a7 0e d2 73 de 11 a1 47 00 0d 60 1c a6 94
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:41409:70 00 00 04 70 00 00 04 70 00 00 04 66 00 00 04 22 81 13 20 7c a7 0e d2 73 de 11 a1 47 00 0d 60
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:41410:1c a6 94 6f 00 00 04 6f 00 00 04 6f 00 00 04 66 00 00 04 22 81 13 20 7c a7 0e d2 73 de 11 a1 47
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:41411:00 0d 60 1c a6 94 6e 00 00 04 6e 00 00 04 6e 00 00 04 66 00 00 04 22 81 13 20 7c a7 0e d2 73 de
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:41412:11 a1 47 00 0d 60 1c a6 94 6d 00 00 04 6d 00 00 04 6d 00 00 04 66 00 00 04 22 81 13 20 7c a7 0e
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:41413:d2 73 de 11 a1 47 00 0d 60 1c a6 94 6c 00 00 04 6c 00 00 04 6c 00 00 04 66 00 00 04
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:41432:00 00 04 66 00 00 04 22 81 13 20 7c a7 0e d2 73 de 11 a1 47 00 0d 60 1c a6 94 6a 00 00 04 6a 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:41433:00 04 6a 00 00 04 66 00 00 04 22 81 13 20 7c a7 0e d2 73 de 11 a1 47 00 0d 60 1c a6 94 69 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:41436:1c a6 94 2a 03 00 04 2a 03 00 04 2a 03 00 04 66 00 00 04 22 81 13 20 7c a7 0e d2 73 de 11 a1 47
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_abilityloadoutchange.log.txt:41437:00 0d 60 1c a6 94 1e 03 00 04 1e 03 00 04 1e 03 00 04 66 00 00 04 22 81 13 20 7c a7 0e d2 73 de
```

## Intake Next Steps

- Preserve the original files separately before editing or importing anything.
- Inspect matches with surrounding lines; embedded hits inside encrypted or unrelated bodies are leads only.
- Record sender, provenance, CR1/CR2 status, action context, character handle, coordinates, known object ids, and known item GoIDs.
- Import only text logs or text-like dumps that contain no passwords, session tokens, private keys, client binaries, or unrelated game assets.
