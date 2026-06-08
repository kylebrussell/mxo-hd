# Packet Capture Triage Report

- Source kind: `directory`
- Source input: `/Users/kyle/Developer/mxo-hd/research/packet-dumps/community-intake/20260607T015831Z-hdneo-public-inventory-lastday-rajko-nearend-selected/logs`
- Scanned files from: `/Users/kyle/Developer/mxo-hd/research/packet-dumps/community-intake/20260607T015831Z-hdneo-public-inventory-lastday-rajko-nearend-selected/logs`
- Generated UTC: `2026-06-07T01:58:32Z`
- Max sample lines per pattern: `10`

Use this report to decide whether received logs are worth importing into `research/packet-dumps/`. Matches are byte-string leads, not decoded packet claims.

## Vendor And Interaction Leads

### SERVER_VENDOR_OPEN candidate

Signature: `81 0d` (spaced or compact hex)

Matches: 4

```text
mxopackets1_crashover2k_trinitys_proxy_second_pc_011_trinity_session.log.txt:1367:01 82 02 a8 32 9e 07 b2 09 9e 07 06 08 00 00 81 0d ba 00 00 02 0c 0e 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsMovingAroundDowntown.log.txt:6668:02 03 12 00 02 0c e8 3a 81 0d c6 f8 df ec 44 ef 14 66 47 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsMovingAroundDowntown.log.txt:19673:fb 2b 8d 47 00 00 04 01 01 72 01 26 81 0d 5c c1 00 44 00 00 00 e0 a3 b4 90 c0 00 00 00 80 c2 86
mxopackets1_entilsar_enti_lastday2.log.txt:132494:1f 38 21 00 00 00 00 00 00 22 00 00 00 00 00 0f 00 02 80 80 10 81 0d 0b 00 02 80 80 90 b9 0c 0c
```

### CLIENT_VENDOR_BUY candidate

Signature: `81 0e` (spaced or compact hex)

Matches: 29

```text
mxopackets2_RajkoPacketsNearTheEnd_PacketsMovingAroundDowntown.log.txt:20171:02 04 01 00 56 01 07 81 0e 01 a8 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsMovingAroundDowntown.log.txt:20215:02 04 01 00 57 01 07 81 0e 01 a8 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsMovingAroundDowntown.log.txt:20237:02 04 01 00 58 01 07 81 0e 01 a8 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsMovingAroundDowntown.log.txt:20255:02 04 01 00 59 01 07 81 0e 01 a8 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsMovingAroundDowntown.log.txt:20272:02 04 01 00 5a 01 07 81 0e 01 a8 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsMovingAroundDowntown.log.txt:20294:02 04 01 00 5b 01 07 81 0e 01 a8 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsMovingAroundDowntown.log.txt:20311:02 04 01 00 5c 01 07 81 0e 01 a8 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsMovingAroundDowntown.log.txt:20330:02 04 01 00 5d 01 07 81 0e 01 a8 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsMovingAroundDowntown.log.txt:20352:02 04 01 00 5e 01 07 81 0e 01 a8 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsMovingAroundDowntown.log.txt:20371:02 04 01 00 5f 01 07 81 0e 01 a8 00 00 00
```

### CLIENT_VENDOR_SELL candidate

Signature: `81 11` (spaced or compact hex)

Matches: 7

```text
mxopackets2_RajkoPacketsNearTheEnd_PacketsMovingAroundDowntown.log.txt:5880:02 03 05 00 02 2e 00 40 00 00 00 10 01 47 58 81 11 c6 f8 df ec 44 96 81 5b 47 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:34217:02 03 02 00 01 0e 05 86 02 76 e0 c6 00 c8 a5 c5 76 81 11 c5 00 00
mxopackets1_entilsar_enti_lastday2.log.txt:85063:80 f7 43 92 d5 08 45 80 80 90 81 11 8c 11 0a 00 28 06 04 00 00 18 00 00 00
mxopackets1_entilsar_enti_lastday2.log.txt:86224:02 03 02 00 03 08 7b 8f 8c 46 26 d5 ee 45 b3 49 2f 45 5b 81 11 07 ff 01 01 01 00 00 00 00 00 00
mxopackets1_entilsar_enti_lastday2.log.txt:86254:02 03 02 00 03 08 e7 9c 8c 46 81 fa ed 45 b3 49 2f 45 c7 81 11 07 ff 01 01 01 00 00 00 00 00 00
mxopackets1_entilsar_enti_lastday2.log.txt:140777:be 42 c2 44 57 46 1f 00 01 00 04 b6 81 11 46 00 00 be 42 53 c0 3d 46 1d 00 01 08 b6 81 11 46 00
```

### CR2 static object interaction

Signature: `80 c8` (spaced or compact hex)

Matches: 30

```text
mxopackets2_RajkoPacketsNearTheEnd_PacketsWhiteHallwaysWarboyTeleportDoors.log.txt:8321:02 04 01 00 19 01 08 80 c8 15 00 50 30 03 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsWhiteHallwaysWarboyTeleportDoors.log.txt:12594:02 04 01 00 29 01 08 80 c8 20 00 50 30 03 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsWhiteHallwaysWarboyTeleportDoors.log.txt:13329:02 04 01 00 2d 01 08 80 c8 15 00 50 30 03 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsChillinginMaraNoticingSpawns.log.txt:34289:02 03 02 00 01 0e 00 85 01 ea 82 46 00 80 f7 43 6a 24 0b 45 00 00 04 01 00 ab 01 08 80 c8 ec 01
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:17968:02 04 01 00 9d 01 08 80 c8 09 07 90 10 03 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:18207:02 04 01 00 9e 01 08 80 c8 30 07 90 10 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:18364:02 04 01 00 a1 01 08 80 c8 fa 06 90 10 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:18620:02 04 01 00 a3 01 08 80 c8 e5 06 90 10 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:18731:02 04 01 00 a6 01 08 80 c8 e8 06 90 10 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:18843:02 04 01 00 a9 01 08 80 c8 eb 06 90 10 00 00
```

### CR1 static object interaction

Signature: `80 c3` (spaced or compact hex)

Matches: 66

```text
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedInMaraCThenExitedViaHardline.log.txt:372:06 80 bc 00 01 0c 04 80 bd 00 01 10 04 80 c1 00 01 2c 02 80 c2 00 32 08 04 80 c3 00 01 14 04 80
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedInMaraCThenExitedViaHardline.log.txt:426:32 08 04 80 c3 00 01 14 04 80 c4 00 01 04 04 80 c5 00 01 18 04 80 c6 00 01 b4 06 80 c7 00 8d 52
mxopackets2_RajkoPacketsNearTheEnd_PacketsWhiteHallwaysWarboyTeleportDoors.log.txt:370:06 80 bc 00 01 0c 04 80 bd 00 01 10 04 80 c1 00 01 2c 02 80 c2 00 32 08 04 80 c3 00 01 14 04 80
mxopackets2_RajkoPacketsNearTheEnd_PacketsWhiteHallwaysWarboyTeleportDoors.log.txt:424:32 08 04 80 c3 00 01 14 04 80 c4 00 01 04 04 80 c5 00 01 18 04 80 c6 00 01 b4 06 80 c7 00 8c 52
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:425:01 2c 02 80 c2 00 32 08 04 80 c3 00 01 14 04 80 c4 00 01 04 04 80 c5 00 01 18 04 80 c6 00 01 b4
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:480:32 08 04 80 c3 00 01 14 04 80 c4 00 01 04 04 80 c5 00 01 18 04 80 c6 00 01 b4 06 80 c7 00 93 52
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:11176:00 10 00 00 22 c5 01 42 01 80 c3 6f 05 00 58 4c 0a 00 28 00 00 00 38 81 9a b8 c0 00 00 00 00 ff
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:12757:1b 00 c6 01 80 c3 e5 08 00 58 4c 0a 00 28 00 00 00 60 cf e2 c3 c0 00 00 00 00 ff 9b 9d 40 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:14181:00 00 00 00 00 00 00 00 00 00 00 00 00 10 00 00 22 d5 02 22 bf 1b 00 c6 01 80 c3 e6 08 00 58 4c
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:14193:00 00 00 00 10 00 00 22 d5 02 23 c0 1b 00 c6 01 80 c3 e9 08 00 58 4c 0a 00 28 00 00 00 f0 08 ed
```

### SERVER object/state bundle

Signature: `80 bc` (spaced or compact hex)

Matches: 7150

```text
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedInMaraCThenExitedViaHardline.log.txt:372:06 80 bc 00 01 0c 04 80 bd 00 01 10 04 80 c1 00 01 2c 02 80 c2 00 32 08 04 80 c3 00 01 14 04 80
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedInMaraCThenExitedViaHardline.log.txt:535:80 b2 11 00 32 00 08 02 16 80 bc 45 03 11 00 00 02 00 00 00 11 00 32 00 00 00 00 00 00 00 00 16
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedInMaraCThenExitedViaHardline.log.txt:536:80 bc 45 00 02 00 00 02 00 00 00 cc 00 0c 00 00 00 00 00 00 00 00 16 80 bc 45 00 03 00 00 0b 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedInMaraCThenExitedViaHardline.log.txt:537:00 00 37 02 96 00 00 00 00 00 00 00 00 16 80 bc 45 00 04 00 00 2d 00 00 00 23 04 0a 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedInMaraCThenExitedViaHardline.log.txt:538:00 00 00 00 16 80 bc 45 00 05 00 00 2d 00 00 00 20 04 0a 00 00 00 00 00 00 00 00 16 80 bc 45 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedInMaraCThenExitedViaHardline.log.txt:539:06 00 00 2d 00 00 00 19 04 05 00 00 00 00 00 00 00 00 16 80 bc 45 00 07 00 00 2d 00 00 00 42 04
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedInMaraCThenExitedViaHardline.log.txt:540:05 00 00 00 00 00 00 00 00 16 80 bc 45 00 08 00 00 34 00 00 00 83 02 c8 00 00 00 00 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedInMaraCThenExitedViaHardline.log.txt:541:16 80 bc 45 00 09 00 00 71 00 00 00 18 04 0a 00 00 00 00 00 00 00 00 16 80 bc 45 00 0a 00 00 74
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedInMaraCThenExitedViaHardline.log.txt:542:00 00 00 74 04 07 00 00 00 00 00 00 00 00 16 80 bc 45 00 0b 00 00 74 00 00 00 78 04 07 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedInMaraCThenExitedViaHardline.log.txt:543:00 00 00 00 00 16 80 bc 45 00 0c 00 00 88 00 00 00 e5 03 05 00 00 00 00 00 00 00 00 16 80 bc 45
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

Matches: 18

```text
mxopackets1_entilsar_enti_lastday2.log.txt:73260:00 00 00 00 00 41 00 ff 00 00 00 00 93 60 01 00 00 00 08 00 12 01 32 00 00 00 00 01 00 1c 00 01
mxopackets1_entilsar_enti_lastday2.log.txt:73315:b6 00 00 1d 0a 00 28 28 00 00 00 00 00 00 00 00 41 00 ff 00 00 00 00 93 60 01 00 00 00 08 00 11
mxopackets1_entilsar_enti_lastday2.log.txt:73342:00 00 00 00 00 00 00 41 00 ff 00 00 00 00 93 60 01 00 00 00 08 00 11 01 32 00 00 00 00 01 00 1c
mxopackets1_entilsar_enti_lastday2.log.txt:73480:00 00 00 00 00 00 00 41 00 ff 00 00 00 00 93 60 01 00 00 00 08 00 12 01 32 00 00 00 00 01 00 1c
mxopackets1_entilsar_enti_lastday2.log.txt:73557:00 00 00 00 00 00 00 00 41 00 ff 00 00 00 00 93 60 01 00 00 00 18 00 11 01 32 00 00 00 00 01 00
mxopackets1_entilsar_enti_lastday2.log.txt:73609:1d 0a 00 28 28 00 00 00 00 00 00 00 00 41 00 ff 00 00 00 00 93 60 01 00 00 00 18 00 12 01 32 00
mxopackets1_entilsar_enti_lastday2.log.txt:73658:b6 00 00 1d 0a 00 28 28 00 00 00 00 00 00 00 00 41 00 ff 00 00 00 00 93 60 01 00 00 00 18 00 12
mxopackets1_entilsar_enti_lastday2.log.txt:74045:00 00 00 00 00 00 00 41 00 ff 00 00 00 00 93 60 01 00 00 00 18 00 12 01 32 00 00 00 00 01 00 00
mxopackets1_entilsar_enti_lastday2.log.txt:74492:28 00 00 00 00 00 00 00 00 41 00 ff 00 00 00 00 93 60 01 00 00 00 18 01 11 01 32 00 00 00 00 01
mxopackets1_entilsar_enti_lastday2.log.txt:75097:ff 22 b6 00 00 1d 0a 00 28 28 00 00 00 00 00 00 00 00 41 00 ff 00 00 00 00 93 60 01 00 00 00 18
```

### Selector ff continuation prefix 00 01 00 ff

Signature: `00 01 00 ff` (spaced or compact hex)

Matches: 2372

```text
mxopackets2_RajkoPacketsNearTheEnd_PacketsWhiteHallwaysWarboyTeleportDoors.log.txt:4321:45 00 00 00 00 00 00 00 00 01 00 ff 00 00 00 00 42 9e 00 00 00 00 20 00 22 02 ff 01 00 00 00 01
mxopackets2_RajkoPacketsNearTheEnd_PacketsWhiteHallwaysWarboyTeleportDoors.log.txt:4341:45 00 00 00 00 00 00 00 00 01 00 ff 00 00 00 00 42 9e 00 00 00 00 20 00 22 02 ff 01 00 00 00 01
mxopackets2_RajkoPacketsNearTheEnd_PacketsWhiteHallwaysWarboyTeleportDoors.log.txt:4443:56 06 00 28 45 00 00 00 00 00 00 00 00 01 00 ff 00 00 00 00 42 9e 00 00 00 00 00 00 22 02 ff 01
mxopackets2_RajkoPacketsNearTheEnd_PacketsWhiteHallwaysWarboyTeleportDoors.log.txt:4463:45 00 00 00 00 00 00 00 00 01 00 ff 00 00 00 00 42 9e 00 00 00 00 00 00 22 02 ff 01 00 00 00 01
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedInMaraCThenExitedViaHardline.log.txt:2075:00 ff 00 c1 09 00 00 12 31 46 12 20 00 00 00 00 00 00 00 00 01 00 ff 76 0c 00 58 3b 4a 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedInMaraCThenExitedViaHardline.log.txt:2103:00 00 00 00 00 00 00 00 01 00 ff 76 0c 00 58 3b 4a 00 00 00 00 00 00 22 01 f2 00 00 00 00 01 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedInMaraCThenExitedViaHardline.log.txt:2132:00 00 00 00 00 00 00 00 01 00 ff 76 0c 00 58 3b 4a 00 00 00 00 00 00 22 01 f2 00 00 00 00 01 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedInMaraCThenExitedViaHardline.log.txt:2161:00 00 00 00 00 00 00 00 01 00 ff 76 0c 00 58 3b 4a 00 00 00 00 00 00 22 01 f2 00 00 00 00 01 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedInMaraCThenExitedViaHardline.log.txt:2187:00 00 00 00 00 01 00 ff 76 0c 00 58 3b 4a 00 00 00 00 00 00 22 01 f2 00 00 00 00 01 00 51 00 01
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedInMaraCThenExitedViaHardline.log.txt:2333:00 00 ff 00 00 00 00 00 ff 06 00 28 ed 00 00 00 00 00 00 00 00 01 00 ff aa 04 00 04 fe b4 00 00
```

### Selector ff continuation prefix 00 01 04 ff

Signature: `00 01 04 ff` (spaced or compact hex)

Matches: 42

```text
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:23431:02 03 02 00 01 04 ff 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsMovingAroundDowntown.log.txt:32070:02 03 02 00 01 04 ff 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsMovingAroundDowntown.log.txt:32557:02 03 02 00 01 04 ff 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsChillinginMaraNoticingSpawns.log.txt:6525:02 03 02 00 01 04 ff 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsChillinginMaraNoticingSpawns.log.txt:19308:02 03 02 00 01 04 ff 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsChillinginMaraNoticingSpawns.log.txt:32596:02 03 02 00 01 04 ff 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsChillinginMaraNoticingSpawns.log.txt:36457:02 03 02 00 01 04 ff 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsChillinginMaraNoticingSpawns.log.txt:40607:02 03 02 00 01 04 ff 00 00
mxopackets1_entilsar_enti_lastday2.log.txt:4379:02 03 02 00 01 04 ff 00 00
mxopackets1_entilsar_enti_lastday2.log.txt:71394:0c 00 00 00 00 00 ff 00 cb 1e 00 00 19 31 46 12 09 00 00 00 00 00 00 00 00 01 04 ff 00 00 00 00
```

### Selector ff continuation prefix 00 01 10 ff

Signature: `00 01 10 ff` (spaced or compact hex)

Matches: 1154

```text
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:4217:00 01 10 ff 95 03 00 08 3b 4a 00 00 00 00 10 01 21 01 d9 00 00 00 00 01 00 49 00 01 02 3d 13 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:4240:ba 13 00 00 00 00 00 ff 00 c9 1e 00 00 56 06 00 28 87 00 00 00 00 00 00 00 00 01 10 ff 95 03 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:4269:00 00 00 00 01 10 ff 95 03 00 08 3b 4a 00 00 00 00 10 01 21 01 d9 00 00 00 00 01 00 49 00 01 0a
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:4867:00 00 01 10 ff 95 03 00 08 3b 4a 00 00 00 00 10 01 21 01 d9 00 00 00 00 01 00 49 00 01 0e 03 6f
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:4886:13 00 00 00 00 00 ff 00 00 00 00 00 d3 08 00 28 88 00 00 00 00 00 00 00 00 01 10 ff 95 03 00 08
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:5407:00 00 00 00 01 10 ff 00 00 00 00 dc ec 00 00 00 00 18 01 12 00 00 00 00 00 00 01 00 65 00 01 2a
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:5990:00 00 00 00 01 10 ff 00 00 00 00 3b 19 01 00 00 00 08 00 12 00 00 00 00 00 00 01 00 5b 00 03 2a
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:6017:18 00 00 04 07 00 00 00 00 00 00 00 00 01 10 ff 00 00 00 00 3b 19 01 00 00 00 08 00 12 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:6022:c9 1e 00 00 20 07 00 28 8a 00 00 00 00 00 00 00 00 01 10 ff 95 03 00 08 3b 4a 00 00 00 00 10 01
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:6058:7a 14 00 00 00 00 00 ff ff 00 00 00 00 18 00 00 04 07 00 00 00 00 00 00 00 00 01 10 ff 00 00 00
```

### Selector ff continuation prefix 00 00 00 ff

Signature: `00 00 00 ff` (spaced or compact hex)

Matches: 7644

```text
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedInMaraCThenExitedViaHardline.log.txt:1769:00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 24 37 c1 40 00 00 00 00 ff 9b 9d 40 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedInMaraCThenExitedViaHardline.log.txt:2073:3d 00 00 00 00 ff 00 00 00 00 a7 0a 32 00 f0 20 46 14 00 7b c6 56 22 00 00 00 80 62 9a ea c0 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedInMaraCThenExitedViaHardline.log.txt:2100:00 44 24 52 82 30 08 13 5e 5e 06 01 80 c0 00 00 01 cd cc 4c 3d 00 00 00 00 ff 00 00 00 00 a7 0a
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedInMaraCThenExitedViaHardline.log.txt:2102:d2 fa c0 ff 00 00 00 00 00 00 00 00 00 00 e1 05 00 00 00 00 00 ff 00 c1 09 00 00 12 31 46 12 20
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedInMaraCThenExitedViaHardline.log.txt:2129:00 44 24 52 82 30 08 13 5e 5e 06 01 80 c0 00 00 01 cd cc 4c 3d 00 00 00 00 ff 00 00 00 00 a7 0a
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedInMaraCThenExitedViaHardline.log.txt:2131:d2 fa c0 ff 00 00 00 00 00 00 00 00 00 00 e1 05 00 00 00 00 00 ff 00 c1 09 00 00 12 31 46 12 20
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedInMaraCThenExitedViaHardline.log.txt:2158:00 44 24 52 82 30 08 13 5e 5e 06 01 80 c0 00 00 01 cd cc 4c 3d 00 00 00 00 ff 00 00 00 00 a7 0a
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedInMaraCThenExitedViaHardline.log.txt:2160:d2 fa c0 ff 00 00 00 00 00 00 00 00 00 00 e1 05 00 00 00 00 00 ff 00 c1 09 00 00 12 31 46 12 20
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedInMaraCThenExitedViaHardline.log.txt:2184:52 82 30 08 13 5e 5e 06 01 80 c0 00 00 01 cd cc 4c 3d 00 00 00 00 ff 00 00 00 00 a7 0a 32 00 f0
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedInMaraCThenExitedViaHardline.log.txt:2186:ff 00 00 00 00 00 00 00 00 00 00 e1 05 00 00 00 00 00 ff 00 c1 09 00 00 12 31 46 12 20 00 00 00
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

Matches: 257

```text
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedInMaraCThenExitedViaHardline.log.txt:1282:11 00 00 00 81 ff 37 00 00 01 00 0c 57 02 41 cd ab 13 8e 42 6c 61 63 6b 77 6f 6f 64 20 47 6f 6f
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:2482:00 00 c6 b0 01 10 14 00 ab 08 f8 00 09 37 2b 0c 1c 03 58 04 00 00 a7 ff 8b 00 00 01 00 0c 57 02
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:2714:00 00 00 00 60 68 40 0d 00 00 01 00 0c 57 02 3f cd ab 10 8e 42 6c 61 63 6b 77 6f 6f 64 20 47 6f
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:11168:02 03 01 00 0c 57 02 75 cd ab 10 8e 42 75 69 6c 64 69 6e 67 20 53 65 63 75 72 69 74 79 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:11171:00 00 81 cb 1e 00 00 02 07 09 00 00 01 00 0c 57 02 76 cd ab 10 8e 42 75 69 6c 64 69 6e 67 20 53
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:11174:a8 1f 39 03 b2 09 39 03 07 08 00 00 81 cb 1e 00 00 02 07 07 00 00 01 00 0c 57 02 77 cd ab 13 8e
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:12755:02 03 02 00 02 80 80 80 50 4f 01 5d 14 01 00 0c 57 02 7f cd ab 13 8e 53 75 69 74 20 4f 66 66 69
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:13269:02 03 02 00 02 80 80 80 10 76 01 01 00 0c 57 02 86 cd ab 11 8e 53 75 69 74 20 57 6f 72 6b 65 72
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:14180:02 03 01 00 0c 57 02 5e cd ab 13 8e 53 75 69 74 20 4f 66 66 69 63 65 20 47 69 72 6c 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:14191:46 12 b0 01 10 14 00 c9 85 58 06 09 84 00 1c 02 85 00 00 00 81 ff 05 00 00 01 00 0c 57 02 4f cd
```

### Adjacent Object599 boundary prefix

Signature: `00 0c 57 02` (spaced or compact hex)

Matches: 251

```text
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedInMaraCThenExitedViaHardline.log.txt:1282:11 00 00 00 81 ff 37 00 00 01 00 0c 57 02 41 cd ab 13 8e 42 6c 61 63 6b 77 6f 6f 64 20 47 6f 6f
mxopackets1_crashover2k_trinitys_proxy_second_pc_011_trinity_session.log.txt:1364:82 a3 0d 13 49 03 01 00 0c 57 02 42 cd ab 13 ae 43 6f 72 72 75 70 74 65 64 00 00 00 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsMovingAroundDowntown.log.txt:2003:82 82 53 07 48 03 01 00 0c 57 02 11 cd ab 13 8e 41 73 73 61 73 73 69 6e 20 42 6c 61 64 65 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsMovingAroundDowntown.log.txt:2013:00 00 00 00 00 80 3f 00 00 00 00 2e bd 3b b3 07 00 00 01 00 0c 57 02 12 cd ab 13 8e 41 73 73 61
mxopackets2_RajkoPacketsNearTheEnd_PacketsMovingAroundDowntown.log.txt:2563:82 cc 53 07 48 03 01 00 0c 57 02 15 cd ab 12 8e 41 73 73 61 73 73 69 6e 20 42 6c 61 64 65 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsMovingAroundDowntown.log.txt:2566:24 b6 03 b2 09 b6 03 07 08 00 00 81 d4 1e 00 00 02 09 12 00 00 01 00 0c 57 02 16 cd ab 11 8e 41
mxopackets2_RajkoPacketsNearTheEnd_PacketsMovingAroundDowntown.log.txt:5583:02 03 01 00 0c 57 02 2c cd ab 13 8e 41 73 73 61 73 73 69 6e 20 42 6c 61 64 65 00 00 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsMovingAroundDowntown.log.txt:5586:24 b6 03 b2 09 b6 03 07 08 00 00 81 d4 1e 00 00 02 09 04 00 00 01 00 0c 57 02 2d cd ab 13 8e 41
mxopackets2_RajkoPacketsNearTheEnd_PacketsMovingAroundDowntown.log.txt:8172:02 03 01 00 0c 57 02 2c cd ab 15 8e 41 73 73 61 73 73 69 6e 20 42 6c 61 64 65 00 00 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsMovingAroundDowntown.log.txt:8959:82 9e 64 07 48 03 01 00 0c 57 02 46 cd ab 12 8e 41 73 73 61 73 73 69 6e 20 42 6c 61 64 65 00 00
```

### Header-like selector 50

Signature: `01 00 08 50` (spaced or compact hex)

Matches: 13

```text
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedInMaraCThenExitedViaHardline.log.txt:865:00 c0 d8 d0 40 00 00 00 00 00 d0 7b 40 00 00 00 00 00 d0 a0 40 01 39 00 00 01 00 08 50 02 ea 01
mxopackets1_crashover2k_trinitys_proxy_second_pc_011_trinity_session.log.txt:1086:01 00 08 50 02 81 08 f0 34 0d cd ab 02 09 00 00 00 c0 94 95 f0 c0 00 00 00 00 00 40 60 40 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:2486:80 02 01 43 00 00 01 00 08 50 02 ea 01 30 39 ef cd ab 01 01 00 00 00 00 00 9a d0 40 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:11178:00 02 07 05 00 00 01 00 08 50 02 de 22 c0 1c c1 cd ab 02 09 00 00 00 00 00 c0 bc c0 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsMovingAroundDowntown.log.txt:2011:00 80 cf 06 be c0 00 00 c0 e9 66 6b a1 40 00 00 00 8a 3d aa ed 40 09 00 00 01 00 08 50 02 eb 1f
mxopackets2_RajkoPacketsNearTheEnd_PacketsMovingAroundDowntown.log.txt:14205:02 03 01 00 08 50 02 bc 1a b0 02 e3 cd ab 02 09 00 00 00 00 00 e2 bd c0 00 00 00 00 00 28 9e 40
mxopackets2_RajkoPacketsNearTheEnd_PacketsMovingAroundDowntown.log.txt:28107:02 03 01 00 08 50 02 eb 1f f0 2e f3 cd ab 02 09 00 00 00 00 00 82 c4 c0 00 00 00 00 00 28 9e 40
mxopackets2_RajkoPacketsNearTheEnd_PacketsMovingAroundDowntown.log.txt:35365:c0 01 83 05 03 a8 24 b6 03 b2 09 b6 03 07 08 00 00 81 c7 1e 00 00 02 09 0c 00 00 01 00 08 50 02
mxopackets2_RajkoPacketsNearTheEnd_PacketsMovingAroundDowntown.log.txt:38415:00 00 00 00 22 00 00 00 00 00 00 00 01 00 08 50 02 9e 06 20 1e f2 cd ab 02 09 00 00 00 00 40 ae
mxopackets1_entilsar_enti_lastday2.log.txt:6935:00 80 69 f6 c0 06 00 00 01 00 08 50 02 70 06 70 13 ef cd ab 02 09 00 00 00 00 e0 dd ee 40 00 00
```

### Header-like selector 67

Signature: `01 00 08 67` (spaced or compact hex)

Matches: 0

### Header-like selector 68

Signature: `01 00 08 68` (spaced or compact hex)

Matches: 0

### Header-like selector 69

Signature: `01 00 08 69` (spaced or compact hex)

Matches: 2

```text
mxopackets1_entilsar_enti_lastday2.log.txt:3091:02 03 01 00 08 69 99 d5 0d 30 2e 3d cd ab 02 09 00 00 00 00 40 bc ec 40 00 00 00 00 c0 ab d1 40
mxopackets1_entilsar_enti_lastday2.log.txt:3092:00 00 00 00 e0 27 f6 c0 00 00 00 00 f3 04 35 bf 00 00 00 00 f3 04 35 3f 07 00 00 01 00 08 69 99
```

### Header-like selector 76

Signature: `01 00 0c 76` (spaced or compact hex)

Matches: 0

### Header-like selector 90

Signature: `01 00 08 90` (spaced or compact hex)

Matches: 4

```text
mxopackets2_RajkoPacketsNearTheEnd_PacketsMovingAroundDowntown.log.txt:2017:00 00 01 00 08 90 05 c4 14 e0 11 2e cd ab 01 01 00 00 00 00 00 7b c1 c0 00 00 00 a0 a8 b9 b0 40
mxopackets2_RajkoPacketsNearTheEnd_PacketsMovingAroundDowntown.log.txt:9550:02 03 01 00 08 90 05 c4 14 e0 11 2e cd ab 01 01 00 00 00 00 00 7b c1 c0 00 00 00 a0 a8 b9 b0 40
mxopackets2_RajkoPacketsNearTheEnd_PacketsMovingAroundDowntown.log.txt:27197:02 03 01 00 08 90 05 c4 14 e0 11 2e cd ab 01 01 00 00 00 00 00 7b c1 c0 00 00 00 a0 a8 b9 b0 40
mxopackets2_RajkoPacketsNearTheEnd_PacketsMovingAroundDowntown.log.txt:32919:02 03 01 00 08 90 05 84 2a 70 1b dc cd ab 01 01 00 00 00 00 00 7e dd c0 00 00 00 80 a2 52 9d 40
```

### Header-like selector a3

Signature: `01 00 08 a3` (spaced or compact hex)

Matches: 9

```text
mxopackets2_RajkoPacketsNearTheEnd_PacketsMovingAroundDowntown.log.txt:21781:02 03 01 00 08 a3 01 f6 03 50 29 3b cd ab 02 80 22 00 00 00 00 00 cc b0 40 00 00 00 00 00 52 a2
mxopackets2_RajkoPacketsNearTheEnd_PacketsMovingAroundDowntown.log.txt:37011:00 00 00 00 22 00 00 00 00 00 00 00 01 00 08 a3 01 6c 06 20 1e d7 cd ab 03 88 00 00 00 00 f3 04
mxopackets2_RajkoPacketsNearTheEnd_PacketsMovingAroundDowntown.log.txt:37013:c5 f0 40 ff ff ff ff 1c 00 00 01 00 08 a3 01 6b 06 20 1e d8 cd ab 03 88 00 00 00 00 f3 04 35 bf
mxopackets2_RajkoPacketsNearTheEnd_PacketsMovingAroundDowntown.log.txt:38604:7a f0 40 1b 00 00 01 00 08 a3 01 35 06 20 1e 4e cd ab 03 88 00 00 00 00 f3 04 35 bf 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsMovingAroundDowntown.log.txt:38606:ff 0c 00 00 01 00 08 a3 01 b5 05 20 1e 87 cd ab 03 88 00 00 00 00 f3 04 35 bf 00 00 00 00 f3 04
mxopackets2_RajkoPacketsNearTheEnd_PacketsMovingAroundDowntown.log.txt:39519:01 00 08 a3 01 c0 04 20 1e 85 cd ab 03 88 00 00 00 00 f3 04 35 bf 00 00 00 00 f3 04 35 3f 22 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsMovingAroundDowntown.log.txt:39553:8d f0 40 1c 00 00 01 00 08 a3 01 55 04 20 1e 88 cd ab 03 88 00 00 00 00 f3 04 35 bf 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsMovingAroundDowntown.log.txt:39708:00 00 00 00 22 00 00 00 00 00 00 00 01 00 08 a3 01 6b 06 20 1e d8 cd ab 03 88 00 00 00 00 f3 04
mxopackets2_RajkoPacketsNearTheEnd_PacketsMovingAroundDowntown.log.txt:39710:c5 f0 40 ff ff ff ff 1b 00 00 01 00 08 a3 01 6c 06 20 1e d7 cd ab 03 88 00 00 00 00 f3 04 35 bf
```

### Header-like selector d0

Signature: `01 00 08 d0` (spaced or compact hex)

Matches: 30

```text
mxopackets1_crashover2k_trinitys_proxy_second_pc_011_trinity_session.log.txt:5501:02 03 02 00 02 80 80 81 28 0a 40 c6 01 01 00 08 d0 20 ad 03 70 2f 0d cd ab 08 fd 00 00 00 00 80
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedInMaraCThenExitedViaHardline.log.txt:963:12 44 22 17 0c 1c 01 84 00 00 00 7f 00 51 00 00 01 00 08 d0 20 ee 01 30 39 df cd ab 08 fd 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedInMaraCThenExitedViaHardline.log.txt:1012:00 00 01 00 08 d0 20 b0 00 20 4e db cd ab 08 fd 00 00 00 00 c0 05 d2 40 00 00 00 00 00 f0 7e 40
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedInMaraCThenExitedViaHardline.log.txt:1131:00 00 01 11 00 00 00 2f 00 45 00 00 01 00 08 d0 20 f1 01 30 39 c1 cd ab 08 fd 00 00 00 00 80 83
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedInMaraCThenExitedViaHardline.log.txt:1285:8b 01 03 15 a8 04 96 00 b2 09 96 00 07 08 00 00 80 02 01 29 00 00 01 00 08 d0 20 b1 00 20 4e c2
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:1837:00 00 00 00 00 f0 7e 40 00 00 00 60 dc 14 98 40 53 8c 07 09 a5 00 00 01 00 08 d0 20 b0 00 20 4e
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:1886:00 01 00 08 d0 20 ee 01 30 39 f1 cd ab 08 fd 00 00 00 00 c0 5c d5 40 00 00 00 00 00 20 7f 40 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:1934:00 28 90 01 00 89 b2 f8 04 09 8f 1d 22 b6 00 00 03 81 00 00 00 9e ff a7 00 00 01 00 08 d0 20 f1
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:2153:28 90 01 00 89 12 58 ee 08 3c 1c 02 85 00 00 00 c3 ff 9b 00 00 01 00 08 d0 20 b1 00 20 4e ee cd
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:2586:76 00 63 22 66 00 00 04 90 01 00 89 fa 15 f0 08 14 1c 02 85 00 00 00 f3 ff 7f 00 00 01 00 08 d0
```

## Movement And Object-State Leads

### Mode 06 movement selector 08

Signature: `00 06 08` (spaced or compact hex)

Matches: 13

```text
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:26233:00 06 08 6c f4 e0 c6 00 c8 a5 c5 be 0e 32 c5 80 80 80 80 80 01 66 4c 00 01 2c 57 40 11 00 00 10
mxopackets1_entilsar_enti_lastday2.log.txt:48206:02 03 0c 00 06 08 59 e6 76 47 00 00 be 42 00 d3 b3 c7 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00
mxopackets1_entilsar_enti_lastday2.log.txt:48286:02 03 0c 00 06 08 59 e6 76 47 00 00 be 42 00 d3 b3 c7 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00
mxopackets1_entilsar_enti_lastday2.log.txt:48358:02 03 0c 00 06 08 59 e6 76 47 00 00 be 42 00 d3 b3 c7 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00
mxopackets1_entilsar_enti_lastday2.log.txt:48408:82 72 97 e5 47 03 0d 00 01 02 04 0c 00 06 08 59 e6 76 47 00 00 be 42 00 d3 b3 c7 ff 00 00 00 10
mxopackets1_entilsar_enti_lastday2.log.txt:48722:02 03 0c 00 06 08 34 22 77 47 00 00 be 42 00 d3 b3 c7 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00
mxopackets1_entilsar_enti_lastday2.log.txt:91607:01 0f 00 0f 00 a8 29 21 8a 46 00 80 f7 43 cd 6a e2 44 15 00 06 08 92 57 86 46 00 80 f7 43 1c ee
mxopackets1_entilsar_enti_lastday2.log.txt:92861:52 a7 46 00 80 f7 43 2c c5 ae 45 15 00 06 08 92 57 86 46 00 80 f7 43 1c ee 07 45 ff 00 00 00 10
mxopackets1_entilsar_enti_lastday2.log.txt:133071:02 03 2f 00 01 0c c1 c5 8d f0 45 00 00 be 42 a0 78 57 46 2d 00 06 08 f0 ff 0b 45 00 00 be 42 19
mxopackets1_entilsar_enti_lastday2.log.txt:133525:02 03 2f 00 01 0c 0a c5 8d f0 45 00 00 be 42 a0 78 57 46 2d 00 06 08 f8 38 11 45 00 00 be 42 87
```

### Mode 06 movement selector 0a

Signature: `00 06 0a` (spaced or compact hex)

Matches: 13

```text
mxopackets2_RajkoPacketsNearTheEnd_PacketsWhiteHallwaysWarboyTeleportDoors.log.txt:297:10 00 00 00 00 bb 9a 06 00 00 00 07 00 0a 10 00 10 00 ba 02 00 00 06 0a 00 00 3d 20 00 00 89 a9
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedInMaraCThenExitedViaHardline.log.txt:299:10 00 00 00 00 bb 9a 06 00 00 00 07 00 0a 10 00 10 00 ba 02 00 00 06 0a 00 00 3d 20 00 00 89 a9
mxopackets2_RajkoPacketsNearTheEnd_PacketsMovingAroundDowntown.log.txt:333:10 00 00 00 00 bb 9a 06 00 00 00 07 00 0a 10 00 a4 00 ba 02 00 00 c9 09 00 00 06 0a 00 00 e0 15
mxopackets2_RajkoPacketsNearTheEnd_PacketsMovingAroundDowntown.log.txt:22604:00 00 00 22 00 00 00 00 00 14 00 06 0a 01 f5 88 16 45 00 a0 eb 44 cc eb 82 47 ff 00 00 00 10 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsMovingAroundDowntown.log.txt:23613:02 03 1a 00 06 0a 01 43 88 b0 45 00 a0 eb 44 cf bc 84 47 ff 00 00 00 10 00 00 00 00 00 01 ff 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsMovingAroundDowntown.log.txt:25438:02 03 11 00 06 0a 01 da ea a3 c5 f8 df ec 44 da b2 8a 47 ff 00 00 00 10 00 00 00 00 00 01 ff 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:335:10 00 00 00 00 bb 9a 06 00 00 00 07 00 0a 10 00 a4 00 ba 02 00 00 c9 09 00 00 06 0a 00 00 e0 15
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:12165:02 03 09 00 06 0a 01 ee 04 c9 c5 f8 df ec 44 7a 3a 80 46 ff 00 00 00 10 00 00 00 00 00 01 ff 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:48989:c6 00 48 99 c5 00 80 98 44 6a 00 06 0a 00 6c f4 e0 c6 00 c8 a5 c5 be 0e 32 c5 ff 00 00 00 10 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:49366:0e 01 15 5a ce e3 c6 00 c8 a5 c5 00 76 5c c3 ab 00 02 80 80 80 0c 44 00 00 04 41 6a 00 06 0a 00
```

### Mode 06 movement selector 0c

Signature: `00 06 0c` (spaced or compact hex)

Matches: 446

```text
mxopackets1_crashover2k_trinitys_proxy_second_pc_011_trinity_session.log.txt:7387:02 03 01 00 01 01 00 0a 00 0e 00 06 0c b7 4d 89 7c c7 00 00 be 42 41 34 91 46 ff 00 00 00 10 00
mxopackets1_crashover2k_trinitys_proxy_second_pc_011_trinity_session.log.txt:7407:82 65 17 13 49 03 0e 00 06 0c c1 4d 89 7c c7 00 00 be 42 41 34 91 46 ff 00 00 00 10 00 00 00 00
mxopackets1_crashover2k_trinitys_proxy_second_pc_011_trinity_session.log.txt:7427:02 03 0e 00 06 0c cb 4d 89 7c c7 00 00 be 42 41 34 91 46 80 80 80 40 84 06 00 00
mxopackets1_crashover2k_trinitys_proxy_second_pc_011_trinity_session.log.txt:7437:02 03 0e 00 06 0c d5 4d 89 7c c7 00 00 be 42 41 34 91 46 ff 00 00 00 10 00 00 00 00 00 01 ff 00
mxopackets1_crashover2k_trinitys_proxy_second_pc_011_trinity_session.log.txt:7457:02 03 0e 00 06 0c df 4d 89 7c c7 00 00 be 42 41 34 91 46 ff 00 00 00 10 00 00 00 00 00 01 ff 00
mxopackets1_crashover2k_trinitys_proxy_second_pc_011_trinity_session.log.txt:7522:02 03 0e 00 06 0c f4 36 9e 7c c7 00 00 be 42 2e 9c 91 46 ff 00 00 00 10 00 00 00 00 00 01 ff 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsMovingAroundDowntown.log.txt:3039:02 03 02 00 02 80 80 81 bb 14 10 4e 01 0c 00 06 0c 9c 57 57 0f c6 f8 df ec 44 68 17 6a 47 80 80
mxopackets2_RajkoPacketsNearTheEnd_PacketsMovingAroundDowntown.log.txt:3059:02 03 12 00 06 0c af 1a 2a 0d c6 f8 df ec 44 f5 3b 66 47 80 80 80 80 80 80 01 12 08 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsMovingAroundDowntown.log.txt:3234:02 03 12 00 06 0c 89 1a 2a 0d c6 f8 df ec 44 f5 3b 66 47 ff 00 00 00 10 00 00 00 00 00 01 ff 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsMovingAroundDowntown.log.txt:3282:02 03 12 00 06 0c 7f 1a 2a 0d c6 f8 df ec 44 f5 3b 66 47 ff 00 00 00 10 00 00 00 00 00 01 ff 00
```

### Mode 06 movement selector 0e

Signature: `00 06 0e` (spaced or compact hex)

Matches: 701

```text
mxopackets1_crashover2k_trinitys_proxy_second_pc_011_trinity_session.log.txt:7367:02 03 0e 00 06 0e 04 ad 4d 89 7c c7 00 00 be 42 41 34 91 46 ff 00 00 00 10 00 00 00 00 00 01 ff
mxopackets1_crashover2k_trinitys_proxy_second_pc_011_trinity_session.log.txt:7477:02 03 0e 00 06 0e 01 e9 52 8f 7c c7 00 00 be 42 44 47 91 46 ff 00 00 00 10 00 00 00 00 00 01 ff
mxopackets1_crashover2k_trinitys_proxy_second_pc_011_trinity_session.log.txt:7497:02 03 0e 00 06 0e 01 f3 a8 97 7c c7 00 00 be 42 f5 70 91 46 ff 00 00 00 10 00 00 00 00 00 01 ff
mxopackets2_RajkoPacketsNearTheEnd_PacketsMovingAroundDowntown.log.txt:2793:02 03 0c 00 06 0e 04 97 57 57 0f c6 f8 df ec 44 68 17 6a 47 80 80 80 80 80 80 01 12 08 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsMovingAroundDowntown.log.txt:2909:02 03 0c 00 06 0e 05 8e 57 57 0f c6 f8 df ec 44 68 17 6a 47 ff 00 00 00 10 00 00 00 00 00 01 ff
mxopackets2_RajkoPacketsNearTheEnd_PacketsMovingAroundDowntown.log.txt:2924:02 03 12 00 02 2e 00 40 00 00 00 10 00 72 1a 2a 0d c6 f8 df ec 44 f5 3b 66 47 0c 00 06 0e 00 84
mxopackets2_RajkoPacketsNearTheEnd_PacketsMovingAroundDowntown.log.txt:2948:82 a1 54 07 48 03 0c 00 06 0e 00 7c 57 57 0f c6 f8 df ec 44 68 17 6a 47 ff 00 00 00 10 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsMovingAroundDowntown.log.txt:2963:02 03 0c 00 06 0e 00 77 57 57 0f c6 f8 df ec 44 68 17 6a 47 ff 00 00 00 10 00 00 00 00 00 01 ff
mxopackets2_RajkoPacketsNearTheEnd_PacketsMovingAroundDowntown.log.txt:3000:02 03 02 00 02 88 05 8c 1e 66 66 e6 3e 80 90 62 01 b0 56 06 00 28 01 02 01 10 11 00 06 0e 04 d4
mxopackets2_RajkoPacketsNearTheEnd_PacketsMovingAroundDowntown.log.txt:3174:02 03 12 00 06 0e 05 a5 1a 2a 0d c6 f8 df ec 44 f5 3b 66 47 ff 00 00 00 10 00 00 00 00 00 01 ff
```

### Repeated mode 06 suffix lead

Signature: `ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00` (spaced or compact hex)

Matches: 486

```text
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:12826:1b a2 60 46 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:17371:00 00 10 24 eb e6 c6 00 e0 88 44 45 1a 94 c5 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:40471:80 e8 c6 00 48 99 c5 10 98 48 44 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:43106:99 c5 90 3e cb 44 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:48712:27 31 00 6c f4 e0 c6 00 c8 a5 c5 be 0e 32 c5 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:48753:31 00 00 6c f4 e0 c6 00 c8 a5 c5 be 0e 32 c5 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:48813:e0 c6 00 c8 a5 c5 be 0e 32 c5 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:48857:00 00 6c f4 e0 c6 00 c8 a5 c5 be 0e 32 c5 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:48904:ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:49197:6c f4 e0 c6 00 c8 a5 c5 be 0e 32 c5 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00
```

### Movement-state ff 01 64 01 tail

Signature: `ff 01 64 01` (spaced or compact hex)

Matches: 9

```text
mxopackets1_entilsar_enti_lastday2.log.txt:75408:00 03 28 91 c0 00 98 d9 00 96 22 85 46 00 80 f7 43 00 30 0e 45 ff 01 64 01 00 00 00 00 00 00 00
mxopackets1_entilsar_enti_lastday2.log.txt:75442:45 ff 01 64 01 00 00 00 00 00 00 00 00 23 1a e9 90 80 b0 04 02 02 8e 00 00 00 01 00 00 00 00 b8
mxopackets1_entilsar_enti_lastday2.log.txt:91014:39 45 ff 01 64 01 00 00 00 00 00 00 00 00 23 1a e9 90 80 b0 04 02 02 8e 00 00 00 01 00 00 00 00
mxopackets1_entilsar_enti_lastday2.log.txt:91354:11 37 00 03 02 00 ff 01 64 01 00 00 00 00 00 00 00 00 23 1a e9 90 80 b0 04 02 02 8e 00 00 00 01
mxopackets1_entilsar_enti_lastday2.log.txt:91923:95 67 01 15 00 3c 00 d3 a5 85 46 00 80 f7 43 18 69 39 45 ff 01 64 01 00 00 00 00 00 00 00 00 23
mxopackets1_entilsar_enti_lastday2.log.txt:91956:67 01 15 00 3c 00 d3 a5 85 46 00 80 f7 43 18 69 39 45 ff 01 64 01 00 00 00 00 00 00 00 00 23 1a
mxopackets1_entilsar_enti_lastday2.log.txt:98292:95 57 39 45 37 00 03 27 98 81 00 95 67 01 15 00 3c 00 0d 00 00 7b ff 01 64 01 00 00 00 00 00 00
mxopackets1_entilsar_enti_lastday2.log.txt:136974:02 03 31 00 03 03 08 00 00 ff 01 64 01 00 00 00 00 00 00 00 00 23 1a e9 90 80 b0 04 02 02 8e 00
mxopackets1_entilsar_enti_lastday2.log.txt:148557:02 03 2a 00 02 ff 01 64 01 00 00 00 00 00 00 00 00 23 1a e9 90 80 b0 04 02 02 8e 00 00 00 01 00
```

### Movement-state ff 01 4e 01 tail

Signature: `ff 01 4e 01` (spaced or compact hex)

Matches: 280

```text
mxopackets2_RajkoPacketsNearTheEnd_PacketsChillinginMaraNoticingSpawns.log.txt:17118:02 03 4f 00 03 2b 00 40 00 00 00 10 08 00 03 4c 4d 80 46 86 db 2b 44 71 6a 46 45 ff 01 4e 01 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsChillinginMaraNoticingSpawns.log.txt:17138:02 03 55 00 02 80 80 10 76 06 4f 00 03 03 08 00 00 ff 01 4e 01 00 00 00 00 00 00 f0 48 60 04 85
mxopackets1_entilsar_enti_lastday2.log.txt:61264:be 42 b2 ca b3 c7 ff 01 4e 01 00 00 00 00 00 00 00 00 23 1a e9 90 80 b0 04 02 02 8e 00 00 00 01
mxopackets1_entilsar_enti_lastday2.log.txt:61284:00 98 21 bd 77 47 00 00 be 42 b2 ca b3 c7 ff 01 4e 01 00 00 00 00 00 00 00 00 23 1a e9 90 80 b0
mxopackets1_entilsar_enti_lastday2.log.txt:61303:02 03 05 00 03 28 8b 81 00 98 49 01 08 00 04 00 21 bd 77 47 00 00 be 42 b2 ca b3 c7 ff 01 4e 01
mxopackets1_entilsar_enti_lastday2.log.txt:65267:82 62 17 e7 47 03 05 00 03 27 8f 81 00 98 4a 01 08 00 04 00 00 00 00 d4 ff 01 4e 01 00 00 00 00
mxopackets1_entilsar_enti_lastday2.log.txt:71888:21 00 03 0c 86 6b f0 81 46 00 80 f7 43 03 fe 32 45 ff 01 4e 01 00 00 00 00 00 00 45 44 a2 5a d8
mxopackets1_entilsar_enti_lastday2.log.txt:71912:80 f7 43 03 fe 32 45 ff 01 4e 01 00 00 00 00 00 00 45 44 a2 5a d8 52 80 70 10 07 5a 80 00 00 00
mxopackets1_entilsar_enti_lastday2.log.txt:71945:43 03 fe 32 45 ff 01 4e 01 00 00 00 00 00 00 45 44 a2 5a d8 52 80 70 10 07 5a 80 00 00 00 01 00
mxopackets1_entilsar_enti_lastday2.log.txt:71981:00 00 00 00 00 00 01 00 21 00 03 0c 89 6b f0 81 46 00 80 f7 43 03 fe 32 45 ff 01 4e 01 00 00 00
```

## Ability, Effect, And Combat-State Leads

### SERVER_PLAYER_ATTRIBUTE / ability load

Signature: `80 b2` (spaced or compact hex)

Matches: 267

```text
mxopackets1_crashover2k_trinitys_proxy_second_pc_012_trinitys_inventory_session_slot1_row2_empty.log.txt:522:00 00 00 08 80 b2 4e 00 08 00 08 02 08 80 b2 52 00 10 00 08 02 08 80 b2 54 00 09 00 08 02 08 80
mxopackets1_crashover2k_trinitys_proxy_second_pc_012_trinitys_inventory_session_slot1_row2_empty.log.txt:523:b2 4f 00 0b 00 08 02 08 80 b2 51 00 0c 00 08 02 08 80 b2 11 00 11 00 08 02 16 80 bc 45 03 11 00
mxopackets1_crashover2k_trinitys_proxy_second_pc_012_trinitys_inventory_session_slot1_row2_empty.log.txt:533:00 00 00 00 08 80 b2 3a 04 00 00 08 02 16 80 bc 45 03 3a 04 00 53 00 00 00 3a 04 00 00 00 00 00
mxopackets1_crashover2k_trinitys_proxy_second_pc_012_trinitys_inventory_session_slot1_row2_empty.log.txt:537:08 80 b2 35 04 00 00 08 02 16 80 bc 45 03 35 04 00 62 00 00 00 35 04 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_trinitys_proxy_second_pc_012_trinitys_inventory_session_slot1_row2_empty.log.txt:719:00 08 80 b2 11 00 11 00 08 02 16 80 bc 45 03 11 00 00 02 00 00 00 11 00 11 00 00 00 00 00 00 00
mxopackets1_crashover2k_trinitys_proxy_second_pc_012_trinitys_inventory_session_slot1_row2_empty.log.txt:770:00 00 00 00 00 00 00 00 08 80 b2 3a 04 00 00 08 02 16 80 bc 45 03 3a 04 00 53 00 00 00 3a 04 00
mxopackets1_crashover2k_trinitys_proxy_second_pc_012_trinitys_inventory_session_slot1_row2_empty.log.txt:808:06 04 01 00 00 00 00 00 00 00 00 08 80 b2 35 04 00 00 08 02 16 80 bc 45 03 35 04 00 62 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsWhiteHallwaysWarboyTeleportDoors.log.txt:531:cb c0 12 00 00 00 00 0a 80 e4 b0 9f b7 df 00 00 00 00 08 80 b2 4e 00 1e 00 08 02 08 80 b2 52 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsWhiteHallwaysWarboyTeleportDoors.log.txt:532:1e 00 08 02 08 80 b2 54 00 0a 00 08 02 08 80 b2 4f 00 08 00 08 02 08 80 b2 51 00 0b 00 08 02 08
mxopackets2_RajkoPacketsNearTheEnd_PacketsWhiteHallwaysWarboyTeleportDoors.log.txt:533:80 b2 11 00 32 00 08 02 16 80 bc 45 03 11 00 00 02 00 00 00 11 00 32 00 00 00 00 00 00 00 00 16
```

### SERVER_ABILITY_UNLOAD

Signature: `80 b3` (spaced or compact hex)

Matches: 231

```text
mxopackets2_RajkoPacketsNearTheEnd_PacketsWhiteHallwaysWarboyTeleportDoors.log.txt:801:00 04 80 b3 11 00 16 80 bc 45 03 11 00 00 02 00 00 00 11 00 32 00 00 00 01 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsWhiteHallwaysWarboyTeleportDoors.log.txt:850:00 00 88 00 00 00 76 04 05 00 00 00 00 00 00 00 00 04 80 b3 26 04 16 80 bc 45 03 26 04 00 89 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsWhiteHallwaysWarboyTeleportDoors.log.txt:853:00 00 00 00 16 80 bc 55 00 6f 00 00 89 00 00 00 16 04 02 00 00 00 00 00 00 00 00 04 80 b3 24 04
mxopackets2_RajkoPacketsNearTheEnd_PacketsWhiteHallwaysWarboyTeleportDoors.log.txt:888:00 00 04 80 b3 25 04 16 80 bc 45 03 25 04 00 9a 00 00 00 25 04 00 00 00 00 01 00 00 00 00 16 80
mxopackets2_RajkoPacketsNearTheEnd_PacketsWhiteHallwaysWarboyTeleportDoors.log.txt:895:80 b3 27 04 16 80 bc 45 03 27 04 00 01 01 00 00 27 04 00 00 00 00 01 00 00 00 00 16 80 bc 55 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsWhiteHallwaysWarboyTeleportDoors.log.txt:938:02 04 02 00 df 09 16 80 bc 45 00 1b 00 00 aa 01 00 00 e5 03 05 00 00 00 01 00 00 00 00 04 80 b3
mxopackets2_RajkoPacketsNearTheEnd_PacketsWhiteHallwaysWarboyTeleportDoors.log.txt:5564:bd 05 11 00 00 00 00 00 00 00 04 80 b3 11 00 16 80 bc 45 03 11 00 00 02 00 00 00 11 00 32 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsWhiteHallwaysWarboyTeleportDoors.log.txt:5565:00 01 00 00 00 00 04 80 b3 24 04 16 80 bc 45 03 24 04 00 8f 00 00 00 24 04 00 00 00 00 01 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsWhiteHallwaysWarboyTeleportDoors.log.txt:5566:00 00 04 80 b3 25 04 16 80 bc 45 03 25 04 00 9a 00 00 00 25 04 00 00 00 00 01 00 00 00 00 04 80
mxopackets2_RajkoPacketsNearTheEnd_PacketsWhiteHallwaysWarboyTeleportDoors.log.txt:5567:b3 26 04 16 80 bc 45 03 26 04 00 89 00 00 00 26 04 00 00 00 00 01 00 00 00 00 04 80 b3 27 04 16
```

### Object599 adrenaline booster effect id

Signature: `4c 0a 00 28` (spaced or compact hex)

Matches: 780

```text
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:11176:00 10 00 00 22 c5 01 42 01 80 c3 6f 05 00 58 4c 0a 00 28 00 00 00 38 81 9a b8 c0 00 00 00 00 ff
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:11927:02 03 09 00 06 0e 04 68 31 44 d0 c5 f8 df ec 44 22 0f 84 46 80 80 80 80 c0 4c 0a 00 28 01 01 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:11948:00 00 00 cb 1e 00 00 4c 0a 00 28 ff 01 00 00 00 00 00 00 00 00 00 00 00 6c 05 00 58 37 08 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:11968:00 00 cb 1e 00 00 4c 0a 00 28 ff 01 00 00 00 00 00 00 00 00 00 00 00 6c 05 00 58 37 08 00 00 1f
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:11988:00 00 cb 1e 00 00 4c 0a 00 28 ff 01 00 00 00 00 00 00 00 00 00 00 00 6c 05 00 58 37 08 00 00 1f
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:12008:00 00 00 00 00 00 00 cb 1e 00 00 4c 0a 00 28 ff 01 00 00 00 00 00 00 00 00 00 00 00 6c 05 00 58
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:12028:00 00 00 cb 1e 00 00 4c 0a 00 28 ff 01 00 00 00 00 00 00 00 00 00 00 00 6c 05 00 58 37 08 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:12029:1f 07 08 00 00 00 00 00 00 22 00 00 00 00 00 07 00 04 80 80 80 80 c0 4c 0a 00 28 01 01 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:12048:00 00 cb 1e 00 00 4c 0a 00 28 ff 01 00 00 00 00 00 00 00 00 00 00 00 6c 05 00 58 37 08 00 00 1f
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:12068:00 00 cb 1e 00 00 4c 0a 00 28 ff 01 00 00 00 00 00 00 00 00 00 00 00 6c 05 00 58 37 08 00 00 1f
```

### Object599 fast healing effect id

Signature: `b5 07 00 28` (spaced or compact hex)

Matches: 17

```text
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:52243:0a 03 3e 68 e6 c6 00 48 99 c5 c0 59 89 44 11 00 02 80 80 80 0c b5 07 00 28 e8 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsChillinginMaraNoticingSpawns.log.txt:31289:02 03 39 00 02 80 80 80 0c b5 07 00 28 c9 0b 00 01 06 00 39 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsChillinginMaraNoticingSpawns.log.txt:31302:00 ed 58 00 00 ff 9b 00 00 00 00 b5 07 00 28 c9 00 00 00 00 00 00 00 00 01 00 ff f9 0a 00 04 d4
mxopackets2_RajkoPacketsNearTheEnd_PacketsChillinginMaraNoticingSpawns.log.txt:31321:00 b5 07 00 28 c9 00 00 00 00 00 00 00 00 01 00 ff f9 0a 00 04 d4 22 00 00 00 00 08 00 12 02 18
mxopackets2_RajkoPacketsNearTheEnd_PacketsChillinginMaraNoticingSpawns.log.txt:31360:a0 90 b0 c0 ff 00 00 00 00 00 00 00 00 00 00 81 0c 00 ed 58 00 00 ff 9b 00 00 00 00 b5 07 00 28
mxopackets2_RajkoPacketsNearTheEnd_PacketsChillinginMaraNoticingSpawns.log.txt:64016:02 03 39 00 02 80 80 80 0c b5 07 00 28 d8 23 00 02 80 80 10 1c 03 1d 00 01 0e 05 5f 32 5d 64 46
mxopackets1_entilsar_enti_lastday2.log.txt:82740:0c 70 3b 45 27 00 01 00 04 b3 26 86 46 00 80 f7 43 0c 70 3b 45 26 00 02 80 80 80 0c b5 07 00 28
mxopackets1_entilsar_enti_lastday2.log.txt:109417:26 01 ba 40 00 00 00 00 00 c0 57 40 00 00 00 00 80 f1 ca 40 81 ff 63 22 fb ff b5 07 00 28 90 01
mxopackets1_entilsar_enti_lastday2.log.txt:125353:12 fb ff b5 07 00 28 b0 01 00 14 00 8b 54 c1 13 07 2d c5 1e 3d 03 ee 03 00 00 81 ff 07 00 00 01
mxopackets1_entilsar_enti_lastday2.log.txt:127254:ff ff 00 00 00 00 b5 07 00 28 c3 00 00 00 00 00 00 00 00 01 00 ff 75 0c 00 58 9c b7 00 00 00 00
```

### Object599 accelerated spawn effect id

Signature: `66 00 00 04` (spaced or compact hex)

Matches: 73

```text
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:1835:00 00 00 00 f0 7e 40 00 00 00 c0 f9 c0 a6 40 fd ff 63 12 fb ff 66 00 00 04 90 01 00 a9 44 dd a9
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:1997:00 80 65 e1 a6 40 f5 ff 63 22 66 00 00 04 80 89 d8 8e e3 08 df 1c 01 11 00 00 00 2b 00 7d 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:2051:f0 7e 40 00 00 00 20 23 24 a7 40 81 ff 63 22 fb ff 66 00 00 04 80 89 4c 09 e9 08 8f 1d 22 b6 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:2093:2c 14 9a 40 81 ff 63 22 fb ff 66 00 00 04 90 01 00 89 5a c5 c7 08 36 1c 03 11 00 00 00 81 ff 9f
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:2218:00 00 00 a0 0c 06 a4 40 81 ff 63 12 fb ff 66 00 00 04 90 01 00 a9 82 79 e2 08 f7 13 1c 03 11 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:2319:7e 40 00 00 00 20 ea 0d af 40 81 ff 63 22 fb ff 66 00 00 04 90 01 00 a9 18 2a 7a 08 7c 14 1c 03
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:2586:76 00 63 22 66 00 00 04 90 01 00 89 fa 15 f0 08 14 1c 02 85 00 00 00 f3 ff 7f 00 00 01 00 08 d0
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:2636:6c 9c a9 40 fb ff 63 22 fb ff 66 00 00 04 90 01 00 a9 f8 9b bb 08 f7 13 1d 30 a8 00 00 02 81 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:2712:40 81 ff 63 22 fb ff 66 00 00 04 b0 01 00 12 00 89 c9 ba f7 08 17 1c 01 85 00 00 00 77 00 6f 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt:2785:f0 7e 40 00 00 00 80 ee 05 a4 40 81 ff 63 12 66 00 00 04 80 8b 07 71 c3 08 1e 27 1e 82 01 65 00
```

## Intake Next Steps

- Preserve the original files separately before editing or importing anything.
- Inspect matches with surrounding lines; embedded hits inside encrypted or unrelated bodies are leads only.
- Record sender, provenance, CR1/CR2 status, action context, character handle, coordinates, known object ids, and known item GoIDs.
- Import only text logs or text-like dumps that contain no passwords, session tokens, private keys, client binaries, or unrelated game assets.
