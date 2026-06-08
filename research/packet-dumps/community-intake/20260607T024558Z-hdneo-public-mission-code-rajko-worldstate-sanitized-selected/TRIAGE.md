# Packet Capture Triage Report

- Source kind: `directory`
- Source input: `/Users/kyle/Developer/mxo-hd/research/packet-dumps/community-intake/20260607T024558Z-hdneo-public-mission-code-rajko-worldstate-sanitized-selected/logs`
- Scanned files from: `/Users/kyle/Developer/mxo-hd/research/packet-dumps/community-intake/20260607T024558Z-hdneo-public-mission-code-rajko-worldstate-sanitized-selected/logs`
- Generated UTC: `2026-06-07T02:46:01Z`
- Max sample lines per pattern: `10`

Use this report to decide whether received logs are worth importing into `research/packet-dumps/`. Matches are byte-string leads, not decoded packet claims.

## Vendor And Interaction Leads

### SERVER_VENDOR_OPEN candidate

Signature: `81 0d` (spaced or compact hex)

Matches: 6

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26_manythings_4bill_upgradeabilitie_buyzionkey_start_keymission_gotowhite.log.txt:39055:de 81 0d 7c ad d9 43 00 00 00 00 80 db ec c0 00 00 00 00 00 c0 57 40 00 00 00 00 80 ad d3 40 20
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26_manythings_4bill_upgradeabilitie_buyzionkey_start_keymission_gotowhite.log.txt:40174:81 0d 7c ad d9 43 00 00 00 00 dd c8 ef c0 00 00 00 00 00 c0 57 40 00 00 00 e0 45 a8 d2 40 20 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26_manythings_4bill_upgradeabilitie_buyzionkey_start_keymission_gotowhite.log.txt:40354:02 04 01 01 03 01 81 e2 81 0d 44 b3 d7 43 00 00 00 00 80 7e ef c0 00 00 00 80 0a bc 57 40 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26_manythings_4bill_upgradeabilitie_buyzionkey_start_keymission_gotowhite.log.txt:42420:01 80 e6 81 0d 7c ad d9 43 00 00 00 00 00 8f fb 40 00 00 00 00 00 b8 85 40 00 00 00 00 00 c0 77
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26_manythings_4bill_upgradeabilitie_buyzionkey_start_keymission_gotowhite.log.txt:43740:02 04 01 00 f6 01 80 ca 81 0d 7c ad d9 43 00 00 00 00 60 34 fb 40 00 00 00 00 00 b8 85 40 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsZombiesPortingEtc.log.txt:2486:02 03 02 00 01 0e 82 26 dd 81 0d 47 00 20 8a c4 f6 5a f3 c6 00 00
```

### CLIENT_VENDOR_BUY candidate

Signature: `81 0e` (spaced or compact hex)

Matches: 3

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26_manythings_4bill_upgradeabilitie_buyzionkey_start_keymission_gotowhite.log.txt:43772:02 04 01 00 3e 01 07 81 0e 5d b5 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsZombiesPortingEtc.log.txt:14871:02 03 02 00 01 0e 03 6b 57 81 0e 47 00 20 8a c4 cd 4a f8 c6 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsAgentsAndIntPortingSyncHardline.log.txt:44200:02 03 02 00 01 0e 82 f9 37 81 0e 47 00 20 8a c4 0b 3e f1 c6 00 00
```

### CLIENT_VENDOR_SELL candidate

Signature: `81 11` (spaced or compact hex)

Matches: 0

### CR2 static object interaction

Signature: `80 c8` (spaced or compact hex)

Matches: 14

```text
mxopackets2_RajkoPacketsNearTheEnd_PacketsDoorsAloneInWhiteHalls.log.txt:1442:02 04 01 00 15 01 08 80 c8 19 00 50 30 03 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsDoorsAloneInWhiteHalls.log.txt:2246:02 04 01 00 1d 01 08 80 c8 03 00 50 30 03 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsDoorsAloneInWhiteHalls.log.txt:2413:02 04 01 00 21 01 08 80 c8 03 00 50 30 03 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsDoorsAloneInWhiteHalls.log.txt:3050:02 04 01 00 26 01 08 80 c8 19 00 50 30 03 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26_manythings_4bill_upgradeabilitie_buyzionkey_start_keymission_gotowhite.log.txt:285:37 b3 d5 28 95 22 de d5 05 32 92 9e 90 02 1e bb fb 4b 4a 56 e0 dd 58 ec 4c d3 80 c8 dd 71 c4 3d
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26_manythings_4bill_upgradeabilitie_buyzionkey_start_keymission_gotowhite.log.txt:43063:02 03 02 00 01 08 d7 1a db 47 f0 bf 2d 44 9c 8e 8d c5 00 00 04 01 00 34 01 08 80 c8 02 00 50 4b
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26_manythings_4bill_upgradeabilitie_buyzionkey_start_keymission_gotowhite.log.txt:43296:02 03 02 00 01 08 1e d5 db 47 f0 bf 2d 44 02 7b bd c5 00 00 04 01 00 39 01 08 80 c8 81 07 50 4b
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26_manythings_4bill_upgradeabilitie_buyzionkey_start_keymission_gotowhite.log.txt:44363:02 04 01 00 42 01 08 80 c8 04 00 50 4b 03 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26_manythings_4bill_upgradeabilitie_buyzionkey_start_keymission_gotowhite.log.txt:47585:02 04 01 00 4a 01 08 80 c8 13 00 f0 3d 03 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26_manythings_4bill_upgradeabilitie_buyzionkey_start_keymission_gotowhite.log.txt:48269:02 04 01 00 4c 01 08 80 c8 91 01 f0 3d 03 00
```

### CR1 static object interaction

Signature: `80 c3` (spaced or compact hex)

Matches: 150

```text
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:4457:69 6e 79 20 46 72 61 74 65 72 00 00 00 00 00 00 00 10 00 00 22 d5 01 b0 ff 1b 00 c6 01 80 c3 f9
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:4461:00 00 00 00 10 00 00 22 d5 01 a5 ff 1b 00 c6 01 80 c3 f8 09 00 58 4c 0a 00 28 00 00 00 f0 a6 c0
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:4465:00 c6 01 80 c3 f8 09 00 58 4c 0a 00 28 00 00 00 e0 fe d7 ce 40 00 00 00 00 00 c0 57 40 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:6388:69 6e 79 20 46 72 61 74 65 72 00 00 00 00 00 00 00 10 00 00 22 d5 02 81 ff 1b 00 c6 01 80 c3 f9
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:6392:00 00 00 00 10 00 00 22 d5 02 ce ff 1b 00 c6 01 80 c3 f8 09 00 58 4c 0a 00 28 00 00 00 00 77 e9
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:6396:00 c6 01 80 c3 f8 09 00 58 4c 0a 00 28 00 00 00 50 7c ad ce 40 00 00 00 00 00 c0 57 40 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:6588:01 4d 55 32 a2 46 00 80 98 c3 7c dd e1 c5 00 00 04 01 01 52 01 06 80 c3 e4 81 d2 10
mxopackets2_RajkoPacketsNearTheEnd_PacketsDoorsAloneInWhiteHalls.log.txt:1100:48 8c c0 00 00 00 a0 bf c7 e8 40 00 00 04 01 01 5e 01 06 80 c3 1e da 51 10
mxopackets2_RajkoPacketsNearTheEnd_PacketsTUtorialLoginTsts.log.txt:3840:08 00 00 1f f9 07 00 00 00 00 00 00 22 00 00 00 00 00 00 00 04 01 00 62 01 06 80 c3 67 13 7a 0d
mxopackets2_RajkoPacketsNearTheEnd_PacketsTUtorialLoginTsts.log.txt:4174:47 00 00 04 01 00 63 01 06 80 c3 18 18 7a 0d
```

### SERVER object/state bundle

Signature: `80 bc` (spaced or compact hex)

Matches: 8661

```text
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:12:11 00 32 00 08 02 16 80 bc 45 03 11 00 00 02 00 00 00 11 00 32 00 00 00 00 00 00 00 00 16 80 bc
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:13:45 00 02 00 00 02 00 00 00 cc 00 0c 00 00 00 00 00 00 00 00 16 80 bc 45 00 03 00 00 0b 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:14:37 02 96 00 00 00 00 00 00 00 00 16 80 bc 45 00 04 00 00 2d 00 00 00 23 04 0a 00 00 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:15:00 00 16 80 bc 45 00 05 00 00 2d 00 00 00 20 04 0a 00 00 00 00 00 00 00 00 16 80 bc 45 00 06 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:16:00 2d 00 00 00 19 04 05 00 00 00 00 00 00 00 00 16 80 bc 45 00 07 00 00 2d 00 00 00 42 04 05 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:17:00 00 00 00 00 00 00 16 80 bc 45 00 08 00 00 34 00 00 00 83 02 c8 00 00 00 00 00 00 00 00 16 80
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:18:bc 45 00 09 00 00 71 00 00 00 18 04 0a 00 00 00 00 00 00 00 00 16 80 bc 45 00 0a 00 00 74 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:19:00 74 04 07 00 00 00 00 00 00 00 00 16 80 bc 45 00 0b 00 00 74 00 00 00 78 04 07 00 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:20:00 00 00 16 80 bc 45 00 0c 00 00 88 00 00 00 e5 03 05 00 00 00 00 00 00 00 00 16 80 bc 45 00 0d
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:21:00 00 88 00 00 00 76 04 05 00 00 00 00 00 00 00 00 08 80 b2 26 04 00 00 08 02 16 80 bc 45 03 26
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

Matches: 127

```text
mxopackets2_RajkoPacketsNearTheEnd_PacketsAgentsAndIntPortingSyncHardline.log.txt:25117:00 28 06 00 00 00 00 00 00 00 00 01 00 ff 00 00 00 00 42 9e 00 00 00 00 10 00 22 01 73 01 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsAgentsAndIntPortingSyncHardline.log.txt:25151:00 00 00 00 00 01 00 ff 00 00 00 00 42 9e 00 00 00 00 10 00 22 01 73 01 00 00 00 01 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsAgentsAndIntPortingSyncHardline.log.txt:25281:00 00 00 00 00 01 00 ff 00 00 00 00 42 9e 00 00 00 00 10 00 22 01 73 01 00 00 00 01 00 0d 00 01
mxopackets2_RajkoPacketsNearTheEnd_PacketsAgentsAndIntPortingSyncHardline.log.txt:25311:00 00 00 00 01 00 ff 00 00 00 00 42 9e 00 00 00 00 10 00 22 01 73 01 00 00 00 01 00 0d 00 01 08
mxopackets2_RajkoPacketsNearTheEnd_PacketsAgentsAndIntPortingSyncHardline.log.txt:25363:00 00 00 00 00 01 00 ff 00 00 00 00 42 9e 00 00 08 00 10 00 22 01 73 01 00 00 00 01 00 0d 00 01
mxopackets2_RajkoPacketsNearTheEnd_PacketsAgentsAndIntPortingSyncHardline.log.txt:25393:06 00 00 00 00 00 00 00 00 01 00 ff 00 00 00 00 42 9e 00 00 08 00 10 00 22 01 73 01 00 00 00 01
mxopackets2_RajkoPacketsNearTheEnd_PacketsAgentsAndIntPortingSyncHardline.log.txt:25423:00 30 09 00 28 06 00 00 00 00 00 00 00 00 01 00 ff 00 00 00 00 42 9e 00 00 08 00 10 00 22 01 73
mxopackets2_RajkoPacketsNearTheEnd_PacketsAgentsAndIntPortingSyncHardline.log.txt:25455:00 00 00 00 01 00 ff 00 00 00 00 42 9e 00 00 08 00 10 00 22 01 73 01 00 00 00 01 00 0d 00 01 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsAgentsAndIntPortingSyncHardline.log.txt:25499:00 00 00 00 01 00 ff 00 00 00 00 42 9e 00 00 08 00 10 00 22 01 73 01 00 00 00 01 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsAgentsAndIntPortingSyncHardline.log.txt:25517:00 00 00 00 01 00 ff 00 00 00 00 42 9e 00 00 08 00 10 00 22 01 73 01 00 00 00 01 00 00 00
```

### Selector ff continuation prefix 00 01 04 ff

Signature: `00 01 04 ff` (spaced or compact hex)

Matches: 5

```text
mxopackets2_RajkoPacketsNearTheEnd_PacketsAgentsAndIntPortingSyncHardline.log.txt:4880:02 03 02 00 01 04 ff 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsAgentsAndIntPortingSyncHardline.log.txt:6299:02 03 02 00 01 04 ff 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsAgentsAndIntPortingSyncHardline.log.txt:32117:02 03 02 00 01 04 ff 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26_manythings_4bill_upgradeabilitie_buyzionkey_start_keymission_gotowhite.log.txt:12771:02 03 02 00 01 04 ff 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsZombiesPortingEtc.log.txt:2895:02 03 02 00 01 04 ff 00 00
```

### Selector ff continuation prefix 00 01 10 ff

Signature: `00 01 10 ff` (spaced or compact hex)

Matches: 136

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26_manythings_4bill_upgradeabilitie_buyzionkey_start_keymission_gotowhite.log.txt:16194:37 00 00 ff 0e 1a 95 00 00 56 06 00 28 08 00 00 00 00 00 00 00 00 01 10 ff 00 00 00 00 a6 45 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26_manythings_4bill_upgradeabilitie_buyzionkey_start_keymission_gotowhite.log.txt:16222:56 06 00 28 08 00 00 00 00 00 00 00 00 01 10 ff 00 00 00 00 a6 45 00 00 00 00 18 01 12 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26_manythings_4bill_upgradeabilitie_buyzionkey_start_keymission_gotowhite.log.txt:16236:00 00 00 00 01 10 ff 00 00 00 00 a6 45 00 00 00 00 18 01 12 00 00 00 00 00 00 01 00 08 00 01 0a
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26_manythings_4bill_upgradeabilitie_buyzionkey_start_keymission_gotowhite.log.txt:16267:00 00 01 10 ff dd 06 00 58 1b 45 00 00 80 00 08 01 12 32 f4 00 00 00 00 01 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26_manythings_4bill_upgradeabilitie_buyzionkey_start_keymission_gotowhite.log.txt:16283:00 00 ff ff 1a 95 00 00 56 06 00 28 ae 00 00 00 00 00 00 00 00 01 10 ff dd 06 00 58 1b 45 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26_manythings_4bill_upgradeabilitie_buyzionkey_start_keymission_gotowhite.log.txt:16293:00 00 ff ff 1a 95 00 00 56 06 00 28 ae 00 00 00 00 00 00 00 00 01 10 ff dd 06 00 58 1b 45 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26_manythings_4bill_upgradeabilitie_buyzionkey_start_keymission_gotowhite.log.txt:16312:00 00 56 06 00 28 ae 00 00 00 00 00 00 00 00 01 10 ff dd 06 00 58 1b 45 00 00 80 00 08 01 12 32
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26_manythings_4bill_upgradeabilitie_buyzionkey_start_keymission_gotowhite.log.txt:16379:00 00 00 00 00 01 10 ff dd 06 00 58 1b 45 00 00 80 00 08 01 12 32 f4 00 00 00 00 01 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26_manythings_4bill_upgradeabilitie_buyzionkey_start_keymission_gotowhite.log.txt:16409:00 00 00 00 00 00 01 10 ff dd 06 00 58 1b 45 00 00 80 00 08 01 12 32 f4 00 00 00 00 01 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26_manythings_4bill_upgradeabilitie_buyzionkey_start_keymission_gotowhite.log.txt:16449:00 ff ff 1a 95 00 00 56 06 00 28 ae 00 00 00 00 00 00 00 00 01 10 ff dd 06 00 58 1b 45 00 00 80
```

### Selector ff continuation prefix 00 00 00 ff

Signature: `00 00 00 ff` (spaced or compact hex)

Matches: 5122

```text
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:1149:00 00 00 00 db 01 00 00 00 00 11 00 00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:1175:00 00 00 00 db 01 00 00 00 00 11 00 00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:1201:00 00 00 00 db 01 00 00 00 00 11 00 00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:1643:00 00 00 00 ee 02 00 00 00 00 12 00 00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:1645:00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00 00 00 00 fb 09 00 58 37 08 00 00 1f 07 08 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:1663:00 00 00 00 ee 02 00 00 00 00 12 00 00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:1665:00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00 00 00 00 fb 09 00 58 37 08 00 00 1f 07 08 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:1688:00 00 00 ee 02 00 00 00 00 12 00 00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:1690:00 00 00 00 ff 00 00 00 00 00 00 00 00 00 00 00 00 fb 09 00 58 37 08 00 00 1f 07 08 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:1713:00 00 00 00 00 00 00 00 ee 02 00 00 00 00 12 00 00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00 00
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

Matches: 341

```text
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:459:02 03 01 00 0c 57 02 f4 cd ab 13 8e 42 72 6f 74 68 65 72 73 20 6f 66 20 44 65 73 74 69 6e 79 20
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:483:82 0f 8a 0b 49 03 01 00 0c 57 02 f3 cd ab 13 8e 42 72 6f 74 68 65 72 73 20 6f 66 20 44 65 73 74
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:507:02 03 01 00 0c 57 02 f5 cd ab 12 8e 42 72 6f 74 68 65 72 73 20 6f 66 20 44 65 73 74 69 6e 79 20
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:531:02 03 01 00 02 14 00 09 00 02 0e 04 29 0f 13 76 46 00 00 be 42 f5 04 4a c6 01 00 0c 57 02 82 cd
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:557:02 03 01 00 0c 57 02 f6 cd ab 11 8e 42 72 6f 74 68 65 72 73 20 6f 66 20 44 65 73 74 69 6e 79 20
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:588:02 03 01 00 0c 57 02 84 cd ab 13 8e 42 72 6f 74 68 65 72 73 20 6f 66 20 44 65 73 74 69 6e 79 20
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:609:02 03 01 00 0c 57 02 83 cd ab 12 8e 42 72 6f 74 68 65 72 73 20 6f 66 20 44 65 73 74 69 6e 79 20
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:827:02 03 01 00 0c 57 02 86 cd ab 12 8e 42 72 6f 74 68 65 72 73 20 6f 66 20 44 65 73 74 69 6e 79 20
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:830:04 e8 03 07 08 00 00 81 ef 1a 00 00 02 04 06 00 00 01 00 0c 57 02 85 cd ab 12 8e 42 72 6f 74 68
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:4456:82 71 8d 0b 49 03 01 00 0c 57 02 f4 cd ab 13 8e 42 72 6f 74 68 65 72 73 20 6f 66 20 44 65 73 74
```

### Adjacent Object599 boundary prefix

Signature: `00 0c 57 02` (spaced or compact hex)

Matches: 337

```text
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:459:02 03 01 00 0c 57 02 f4 cd ab 13 8e 42 72 6f 74 68 65 72 73 20 6f 66 20 44 65 73 74 69 6e 79 20
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:483:82 0f 8a 0b 49 03 01 00 0c 57 02 f3 cd ab 13 8e 42 72 6f 74 68 65 72 73 20 6f 66 20 44 65 73 74
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:507:02 03 01 00 0c 57 02 f5 cd ab 12 8e 42 72 6f 74 68 65 72 73 20 6f 66 20 44 65 73 74 69 6e 79 20
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:531:02 03 01 00 02 14 00 09 00 02 0e 04 29 0f 13 76 46 00 00 be 42 f5 04 4a c6 01 00 0c 57 02 82 cd
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:557:02 03 01 00 0c 57 02 f6 cd ab 11 8e 42 72 6f 74 68 65 72 73 20 6f 66 20 44 65 73 74 69 6e 79 20
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:588:02 03 01 00 0c 57 02 84 cd ab 13 8e 42 72 6f 74 68 65 72 73 20 6f 66 20 44 65 73 74 69 6e 79 20
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:609:02 03 01 00 0c 57 02 83 cd ab 12 8e 42 72 6f 74 68 65 72 73 20 6f 66 20 44 65 73 74 69 6e 79 20
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:827:02 03 01 00 0c 57 02 86 cd ab 12 8e 42 72 6f 74 68 65 72 73 20 6f 66 20 44 65 73 74 69 6e 79 20
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:830:04 e8 03 07 08 00 00 81 ef 1a 00 00 02 04 06 00 00 01 00 0c 57 02 85 cd ab 12 8e 42 72 6f 74 68
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:4456:82 71 8d 0b 49 03 01 00 0c 57 02 f4 cd ab 13 8e 42 72 6f 74 68 65 72 73 20 6f 66 20 44 65 73 74
```

### Header-like selector 50

Signature: `01 00 08 50` (spaced or compact hex)

Matches: 20

```text
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:510:09 db 01 07 08 00 00 81 ef 1a 00 00 02 04 09 00 00 01 00 08 50 02 12 15 80 07 39 cd ab 02 09 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26_manythings_4bill_upgradeabilitie_buyzionkey_start_keymission_gotowhite.log.txt:817:01 00 08 50 02 81 08 f0 34 0d cd ab 02 09 00 00 00 c0 94 95 f0 c0 00 00 00 00 00 40 60 40 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26_manythings_4bill_upgradeabilitie_buyzionkey_start_keymission_gotowhite.log.txt:12461:d2 40 00 00 00 00 7f 0d 72 3f 00 00 00 00 df b0 a6 3e 0d 00 00 01 00 08 50 02 81 08 f0 34 0d cd
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26_manythings_4bill_upgradeabilitie_buyzionkey_start_keymission_gotowhite.log.txt:15241:00 1b 00 00 01 00 08 50 02 ea 01 30 39 e7 cd ab 01 01 00 00 00 00 00 9a d0 40 00 00 00 00 00 90
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26_manythings_4bill_upgradeabilitie_buyzionkey_start_keymission_gotowhite.log.txt:24478:01 a2 01 02 a8 31 00 00 b2 04 f6 09 fd 07 00 00 81 22 b6 00 00 02 0c 11 00 00 01 00 08 50 02 ea
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26_manythings_4bill_upgradeabilitie_buyzionkey_start_keymission_gotowhite.log.txt:27262:01 00 08 50 02 a0 0a a0 1c 22 cd ab 02 09 00 00 00 00 40 02 e3 c0 00 00 00 00 00 40 60 40 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26_manythings_4bill_upgradeabilitie_buyzionkey_start_keymission_gotowhite.log.txt:34569:3f 00 00 10 01 22 00 00 00 00 00 00 00 01 00 08 50 02 a0 0a a0 1c 22 cd ab 02 09 00 00 00 00 40
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26_manythings_4bill_upgradeabilitie_buyzionkey_start_keymission_gotowhite.log.txt:39719:02 03 01 00 08 50 02 81 08 f0 34 0d cd ab 02 09 00 00 00 c0 94 95 f0 c0 00 00 00 00 00 40 60 40
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26_manythings_4bill_upgradeabilitie_buyzionkey_start_keymission_gotowhite.log.txt:41265:fb 40 00 00 00 00 00 90 75 40 00 00 00 00 00 40 af 40 ff ff ff ff 09 00 00 01 00 08 50 02 13 03
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26_manythings_4bill_upgradeabilitie_buyzionkey_start_keymission_gotowhite.log.txt:45968:83 40 00 00 00 00 62 c0 b2 40 0c 00 00 01 00 08 50 02 13 03 f0 3d 3b cd ab 02 09 00 00 00 00 20
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
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26_manythings_4bill_upgradeabilitie_buyzionkey_start_keymission_gotowhite.log.txt:37553:01 22 00 00 00 00 00 00 00 01 00 08 69 09 b5 05 70 06 f4 cd ab 02 05 00 00 00 00 40 98 ed c0 00
```

### Header-like selector 76

Signature: `01 00 0c 76` (spaced or compact hex)

Matches: 0

### Header-like selector 90

Signature: `01 00 08 90` (spaced or compact hex)

Matches: 2

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26_manythings_4bill_upgradeabilitie_buyzionkey_start_keymission_gotowhite.log.txt:27253:c0 09 00 00 02 07 21 00 00 01 00 08 90 05 ab 0a a0 1c 15 cd ab 02 11 00 00 00 00 40 17 e2 c0 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26_manythings_4bill_upgradeabilitie_buyzionkey_start_keymission_gotowhite.log.txt:34571:04 35 3f 2d 00 00 01 00 08 90 05 ab 0a a0 1c 15 cd ab 02 11 00 00 00 00 40 17 e2 c0 00 00 00 80
```

### Header-like selector a3

Signature: `01 00 08 a3` (spaced or compact hex)

Matches: 2

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26_manythings_4bill_upgradeabilitie_buyzionkey_start_keymission_gotowhite.log.txt:41263:3d 71 b3 40 c0 01 83 01 02 a8 07 51 01 b2 09 51 01 07 08 00 00 80 02 01 0b 00 00 01 00 08 a3 01
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26_manythings_4bill_upgradeabilitie_buyzionkey_start_keymission_gotowhite.log.txt:48015:02 03 01 00 08 a3 01 6b 01 f0 3d 3a cd ab 03 88 00 00 00 00 ff ff 7f 3f 00 00 00 00 f3 04 35 33
```

### Header-like selector d0

Signature: `01 00 08 d0` (spaced or compact hex)

Matches: 30

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26_manythings_4bill_upgradeabilitie_buyzionkey_start_keymission_gotowhite.log.txt:15173:02 03 01 00 08 d0 20 ee 01 30 39 35 cd ab 08 fd 00 00 00 00 c0 5c d5 40 00 00 00 00 00 20 7f 40
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26_manythings_4bill_upgradeabilitie_buyzionkey_start_keymission_gotowhite.log.txt:15177:00 f3 04 35 3f 31 00 00 01 00 08 d0 20 b0 00 20 4e 37 cd ab 08 fd 00 00 00 00 c0 05 d2 40 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26_manythings_4bill_upgradeabilitie_buyzionkey_start_keymission_gotowhite.log.txt:15199:82 41 52 77 47 03 01 00 08 d0 20 b1 00 20 4e 31 cd ab 07 fd 00 00 00 00 00 f8 d1 40 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26_manythings_4bill_upgradeabilitie_buyzionkey_start_keymission_gotowhite.log.txt:15259:02 03 01 00 08 d0 20 ef 01 30 39 34 cd ab 08 fd 00 00 00 00 40 5a d5 40 00 00 00 00 00 f0 7e 40
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26_manythings_4bill_upgradeabilitie_buyzionkey_start_keymission_gotowhite.log.txt:15287:02 03 01 00 02 06 00 13 00 01 01 01 00 08 d0 20 f1 01 30 39 29 cd ab 08 fd 00 00 00 00 80 83 cf
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26_manythings_4bill_upgradeabilitie_buyzionkey_start_keymission_gotowhite.log.txt:24365:0f 00 00 2f 00 00 01 00 08 d0 20 f1 01 30 39 29 cd ab 08 fd 00 00 00 00 80 83 cf 40 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26_manythings_4bill_upgradeabilitie_buyzionkey_start_keymission_gotowhite.log.txt:24401:af 40 1a a1 0f 00 00 23 00 00 01 00 08 d0 20 ef 01 30 39 34 cd ab 08 fd 00 00 00 00 40 5a d5 40
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26_manythings_4bill_upgradeabilitie_buyzionkey_start_keymission_gotowhite.log.txt:24465:02 03 01 00 08 d0 20 b1 00 20 4e 31 cd ab 07 fd 00 00 00 00 00 f8 d1 40 00 00 00 00 00 f0 7e 40
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26_manythings_4bill_upgradeabilitie_buyzionkey_start_keymission_gotowhite.log.txt:24502:02 03 01 00 02 06 00 11 00 01 01 01 00 08 d0 20 b0 00 20 4e 37 cd ab 08 fd 00 00 00 00 c0 05 d2
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26_manythings_4bill_upgradeabilitie_buyzionkey_start_keymission_gotowhite.log.txt:24511:00 00 00 00 00 d0 7b 40 00 00 00 20 43 5a a6 40 0a a1 0f 00 00 05 00 00 01 00 08 d0 20 ee 01 30
```

## Movement And Object-State Leads

### Mode 06 movement selector 08

Signature: `00 06 08` (spaced or compact hex)

Matches: 27

```text
mxopackets2_RajkoPacketsNearTheEnd_PacketsAgentsAndIntPortingSyncHardline.log.txt:35168:da e4 46 0b 00 06 08 c9 85 89 c7 00 00 be 42 18 61 0f 47 ff 00 00 00 10 00 00 00 00 00 01 ff 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsAgentsAndIntPortingSyncHardline.log.txt:42215:02 03 0d 00 06 08 dd 99 0e 47 00 20 8a c4 fd e9 f2 c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsAgentsAndIntPortingSyncHardline.log.txt:42661:02 03 1d 00 02 80 80 10 32 05 13 00 01 80 01 1a 00 32 05 0d 00 06 08 cb 60 0e 47 00 20 8a c4 28
mxopackets2_RajkoPacketsNearTheEnd_PacketsAgentsAndIntPortingSyncHardline.log.txt:42745:11 00 04 80 80 80 80 80 80 01 07 08 00 00 0f 00 04 80 80 80 80 80 80 01 07 08 00 00 0d 00 06 08
mxopackets2_RajkoPacketsNearTheEnd_PacketsAgentsAndIntPortingSyncHardline.log.txt:43059:82 11 2e 0a 49 03 0d 00 06 08 cb 60 0e 47 00 20 8a c4 28 c1 f2 c6 ff 00 00 00 10 00 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsAgentsAndIntPortingSyncHardline.log.txt:43446:47 00 20 8a c4 41 44 04 c7 0d 00 06 08 cb 60 0e 47 00 20 8a c4 28 c1 f2 c6 ff 00 00 00 10 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsAgentsAndIntPortingSyncHardline.log.txt:43604:02 03 1d 00 02 80 80 10 3a 05 13 00 01 80 01 21 00 3a 05 0d 00 06 08 cb 60 0e 47 00 20 8a c4 28
mxopackets2_RajkoPacketsNearTheEnd_PacketsAgentsAndIntPortingSyncHardline.log.txt:44030:02 03 0d 00 06 08 cb 60 0e 47 00 20 8a c4 28 c1 f2 c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsAgentsAndIntPortingSyncHardline.log.txt:46629:00 e0 05 07 c1 0f 47 00 20 8a c4 03 0a f8 c6 0d 00 06 08 aa e2 0e 47 00 20 8a c4 77 98 f3 c6 ff
mxopackets2_RajkoPacketsNearTheEnd_PacketsAgentsAndIntPortingSyncHardline.log.txt:46835:01 1e 00 ee 05 0d 00 06 08 aa e2 0e 47 00 20 8a c4 77 98 f3 c6 ff 00 00 00 10 00 00 00 00 00 01
```

### Mode 06 movement selector 0a

Signature: `00 06 0a` (spaced or compact hex)

Matches: 6

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_actions_missionstart.log.txt:816:02 03 12 00 02 0c b1 e1 b8 d7 47 f0 bf 2d 44 37 52 ba 45 08 00 06 0a 00 71 15 e5 47 00 40 2b 44
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26_manythings_4bill_upgradeabilitie_buyzionkey_start_keymission_gotowhite.log.txt:42087:00 00 22 00 00 00 00 00 03 00 06 0a 00 18 85 d8 47 f0 bf 2d 44 a6 84 e4 45 80 80 80 80 80 80 01
mxopackets2_RajkoPacketsNearTheEnd_PacketsAgentsAndIntPortingSyncHardline.log.txt:25719:02 03 03 00 06 0a 00 bf c7 0d 47 00 20 8a c4 51 d0 f6 c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsZombiesPortingEtc.log.txt:6876:02 03 04 00 06 0a 00 9a 5b 0a 47 00 20 8a c4 c9 a1 f5 c6 80 80 80 40 96 04 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsZombiesPortingEtc.log.txt:8339:02 03 02 00 02 80 08 66 66 e6 3e 0c 00 06 0a 00 b8 c8 0c 47 00 20 8a c4 00 a2 f8 c6 ff 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsZombiesPortingEtc.log.txt:65419:02 03 0c 00 06 0a 00 be 1f 34 47 00 20 8a c4 8c 6c 62 c7 ff 00 00 00 10 00 00 00 00 00 01 ff 00
```

### Mode 06 movement selector 0c

Signature: `00 06 0c` (spaced or compact hex)

Matches: 1463

```text
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:1129:02 03 0d 00 06 0c 75 55 05 72 46 00 00 be 42 43 85 58 c6 80 80 80 80 c0 4c 0a 00 28 01 01 0b 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:1526:02 03 02 00 02 80 04 1e 04 00 06 0c ae 28 b4 a1 46 00 80 98 c3 f2 8a 1b c6 80 80 80 80 80 80 01
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:1686:02 03 04 00 06 0c 8b 28 b4 a1 46 00 80 98 c3 f2 8a 1b c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:4338:00 06 0c fd be 52 a0 46 00 80 98 c3 e2 eb 29 c6 80 80 80 80 80 80 01 12 08 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:4409:82 67 8d 0b 49 03 07 00 06 0c 88 ca 05 a2 46 00 80 98 c3 96 72 e1 c5 80 80 80 80 80 80 01 12 08
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:4799:80 01 07 08 00 00 04 00 06 0c eb be 52 a0 46 00 80 98 c3 e2 eb 29 c6 80 80 80 80 80 80 01 07 08
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:4863:c3 72 c2 52 c6 08 00 06 0c 07 db 58 a3 46 00 80 98 c3 c6 97 2a c6 ff 00 00 00 10 00 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:4961:02 03 0b 00 02 0c 26 bc 12 84 46 00 80 98 c3 ba f5 09 c6 08 00 06 0c 24 d2 64 a3 46 00 80 98 c3
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:5103:02 03 07 00 06 0c e0 ca 05 a2 46 00 80 98 c3 96 72 e1 c5 ff 00 00 00 10 00 00 00 00 00 01 ff 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:5215:08 00 00 00 00 00 00 22 00 00 00 00 00 07 00 06 0c c0 ca 05 a2 46 00 80 98 c3 96 72 e1 c5 80 80
```

### Mode 06 movement selector 0e

Signature: `00 06 0e` (spaced or compact hex)

Matches: 2499

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_actions_missionstart.log.txt:606:02 03 08 00 06 0e 04 a7 5d 40 e5 47 00 40 2b 44 50 4c b2 45 ff 00 00 00 10 00 00 00 00 00 01 ff
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_actions_missionstart.log.txt:692:02 03 0c 00 02 0c f6 3a ac e4 47 00 40 2b 44 3e 34 a9 45 08 00 06 0e 01 e3 5d 40 e5 47 00 40 2b
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_actions_missionstart.log.txt:706:02 03 0c 00 02 0e 00 ec 3a ac e4 47 00 40 2b 44 3e 34 a9 45 08 00 06 0e 01 ed b8 3a e5 47 00 40
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:1147:02 03 0d 00 06 0e 04 7e 42 1c 72 46 00 00 be 42 40 dc 58 c6 ff 00 00 00 10 00 00 00 00 00 01 ff
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:1173:02 03 0d 00 06 0e 00 83 42 1c 72 46 00 00 be 42 40 dc 58 c6 ff 00 00 00 10 00 00 00 00 00 01 ff
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:1199:02 03 0d 00 06 0e 00 8c 42 1c 72 46 00 00 be 42 40 dc 58 c6 ff 00 00 00 10 00 00 00 00 00 01 ff
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:1624:02 03 04 00 06 0e 05 a9 28 b4 a1 46 00 80 98 c3 f2 8a 1b c6 80 80 80 80 80 80 01 07 08 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:1641:02 03 04 00 06 0e 05 9f 28 b4 a1 46 00 80 98 c3 f2 8a 1b c6 ff 00 00 00 10 00 00 00 00 00 01 ff
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:1661:02 03 04 00 06 0e 05 95 28 b4 a1 46 00 80 98 c3 f2 8a 1b c6 ff 00 00 00 10 00 00 00 00 00 01 ff
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:1711:82 b3 8a 0b 49 03 04 00 06 0e 00 80 28 b4 a1 46 00 80 98 c3 f2 8a 1b c6 ff 00 00 00 10 00 00 00
```

### Repeated mode 06 suffix lead

Signature: `ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00` (spaced or compact hex)

Matches: 988

```text
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:4380:ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:4962:fc 71 2a c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:5082:a2 46 00 80 98 c3 96 72 e1 c5 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:5819:12 94 a6 46 00 80 98 c3 0e 30 25 c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:5856:c3 0e 30 25 c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:5893:46 00 80 98 c3 0e 30 25 c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:5898:06 0c 85 ca 05 a2 46 00 80 98 c3 96 72 e1 c5 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:5957:94 a6 46 00 80 98 c3 0e 30 25 c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:5984:94 a6 46 00 80 98 c3 0e 30 25 c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:6164:c3 75 dc 5c c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
```

### Movement-state ff 01 64 01 tail

Signature: `ff 01 64 01` (spaced or compact hex)

Matches: 333

```text
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26_manythings_4bill_upgradeabilitie_buyzionkey_start_keymission_gotowhite.log.txt:9705:ff 01 64 01 00 00 00 00 00 00 00 00 00 00 00 08 41 6e 64 65 72 73 6f 6e 06 54 68 6f 6d 61 73 ff
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26_manythings_4bill_upgradeabilitie_buyzionkey_start_keymission_gotowhite.log.txt:9722:ff 01 64 01 00 00 00 00 00 00 00 00 00 00 00 08 41 6e 64 65 72 73 6f 6e 06 54 68 6f 6d 61 73 ff
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26_manythings_4bill_upgradeabilitie_buyzionkey_start_keymission_gotowhite.log.txt:9738:02 03 02 00 03 08 0d 78 84 c7 00 00 be 42 9a c7 80 46 64 51 c0 03 ff 01 64 01 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26_manythings_4bill_upgradeabilitie_buyzionkey_start_keymission_gotowhite.log.txt:9849:ff 01 64 01 00 00 00 00 00 00 00 00 00 00 00 08 41 6e 64 65 72 73 6f 6e 06 54 68 6f 6d 61 73 ff
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26_manythings_4bill_upgradeabilitie_buyzionkey_start_keymission_gotowhite.log.txt:9866:ff 01 64 01 00 00 00 00 00 00 00 00 00 00 00 08 41 6e 64 65 72 73 6f 6e 06 54 68 6f 6d 61 73 ff
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26_manythings_4bill_upgradeabilitie_buyzionkey_start_keymission_gotowhite.log.txt:9885:02 03 02 00 03 08 0d 78 84 c7 00 00 be 42 9a c7 80 46 f4 60 c0 03 ff 01 64 01 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26_manythings_4bill_upgradeabilitie_buyzionkey_start_keymission_gotowhite.log.txt:9902:02 03 02 00 03 08 0d 78 84 c7 00 00 be 42 9a c7 80 46 71 61 c0 03 ff 01 64 01 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26_manythings_4bill_upgradeabilitie_buyzionkey_start_keymission_gotowhite.log.txt:11672:49 5f c1 03 ff 01 64 01 00 00 00 00 00 00 00 00 00 00 00 08 41 6e 64 65 72 73 6f 6e 06 54 68 6f
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26_manythings_4bill_upgradeabilitie_buyzionkey_start_keymission_gotowhite.log.txt:11690:ff 01 64 01 00 00 00 00 00 00 00 00 00 00 00 08 41 6e 64 65 72 73 6f 6e 06 54 68 6f 6d 61 73 ff
mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26_manythings_4bill_upgradeabilitie_buyzionkey_start_keymission_gotowhite.log.txt:11710:02 03 02 00 03 0c 36 a1 4d 83 c7 00 00 be 42 9c e1 83 46 43 60 c1 03 ff 01 64 01 00 00 00 00 00
```

### Movement-state ff 01 4e 01 tail

Signature: `ff 01 4e 01` (spaced or compact hex)

Matches: 0

## Ability, Effect, And Combat-State Leads

### SERVER_PLAYER_ATTRIBUTE / ability load

Signature: `80 b2` (spaced or compact hex)

Matches: 479

```text
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:10:12 00 00 00 00 0a 80 e4 80 94 b8 df 00 00 00 00 08 80 b2 4e 00 1e 00 08 02 08 80 b2 52 00 1e 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:11:08 02 08 80 b2 54 00 0a 00 08 02 08 80 b2 4f 00 08 00 08 02 08 80 b2 51 00 0b 00 08 02 08 80 b2
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:21:00 00 88 00 00 00 76 04 05 00 00 00 00 00 00 00 00 08 80 b2 26 04 00 00 08 02 16 80 bc 45 03 26
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:23:00 00 00 00 00 00 00 00 08 80 b2 24 04 00 00 08 02 16 80 bc 45 03 24 04 00 8f 00 00 00 24 04 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:25:80 bc 45 00 10 00 00 9a 00 00 00 16 04 02 00 00 00 00 00 00 00 00 08 80 b2 25 04 00 00 08 02 16
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:29:14 00 00 01 01 00 00 76 04 01 00 00 00 00 00 00 00 00 08 80 b2 27 04 00 00 08 02 16 80 bc 45 03
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:35:00 1b 00 00 aa 01 00 00 e5 03 05 00 00 00 00 00 00 00 00 08 80 b2 28 04 00 00 08 02 16 80 bc 45
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:703:00 08 80 b2 11 00 32 00 08 02 16 80 bc 45 03 11 00 00 02 00 00 00 11 00 32 00 00 00 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:763:0e 00 00 89 00 00 00 16 04 02 00 00 00 01 00 00 00 00 08 80 b2 26 04 00 00 08 02 16 80 bc 45 03
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:766:01 00 00 00 00 16 80 bc 45 00 0f 00 00 8f 00 00 00 0e 02 32 00 00 00 01 00 00 00 00 08 80 b2 24
```

### SERVER_ABILITY_UNLOAD

Signature: `80 b3` (spaced or compact hex)

Matches: 515

```text
mxopackets2_RajkoPacketsNearTheEnd_PacketsDoorsAloneInWhiteHalls.log.txt:284:00 04 80 b3 11 00 16 80 bc 45 03 11 00 00 02 00 00 00 11 00 32 00 00 00 01 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsDoorsAloneInWhiteHalls.log.txt:333:00 00 88 00 00 00 76 04 05 00 00 00 00 00 00 00 00 04 80 b3 26 04 16 80 bc 45 03 26 04 00 89 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsDoorsAloneInWhiteHalls.log.txt:336:00 00 00 00 16 80 bc 55 00 6f 00 00 89 00 00 00 16 04 02 00 00 00 00 00 00 00 00 04 80 b3 24 04
mxopackets2_RajkoPacketsNearTheEnd_PacketsDoorsAloneInWhiteHalls.log.txt:371:00 00 04 80 b3 25 04 16 80 bc 45 03 25 04 00 9a 00 00 00 25 04 00 00 00 00 01 00 00 00 00 16 80
mxopackets2_RajkoPacketsNearTheEnd_PacketsDoorsAloneInWhiteHalls.log.txt:378:80 b3 27 04 16 80 bc 45 03 27 04 00 01 01 00 00 27 04 00 00 00 00 01 00 00 00 00 16 80 bc 55 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsDoorsAloneInWhiteHalls.log.txt:421:02 04 02 00 e0 09 16 80 bc 45 00 1b 00 00 aa 01 00 00 e5 03 05 00 00 00 01 00 00 00 00 04 80 b3
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:701:b2 00 00 06 04 01 00 00 00 00 00 00 00 00 04 80 b3 11 00 16 80 bc 45 03 11 00 00 02 00 00 00 11
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:762:80 b3 26 04 16 80 bc 45 03 26 04 00 89 00 00 00 26 04 00 00 00 00 01 00 00 00 00 16 80 bc 45 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:765:02 00 00 00 00 00 00 00 00 04 80 b3 24 04 16 80 bc 45 03 24 04 00 8f 00 00 00 24 04 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:769:04 02 00 00 00 01 00 00 00 00 04 80 b3 25 04 16 80 bc 45 03 25 04 00 9a 00 00 00 25 04 00 00 00
```

### Object599 adrenaline booster effect id

Signature: `4c 0a 00 28` (spaced or compact hex)

Matches: 2725

```text
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:1129:02 03 0d 00 06 0c 75 55 05 72 46 00 00 be 42 43 85 58 c6 80 80 80 80 c0 4c 0a 00 28 01 01 0b 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:1132:c0 4c 0a 00 28 01 01 04 00 02 2e 00 40 00 00 00 10 01 0a 60 21 a1 46 00 80 98 c3 b2 5f 20 c6 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:1151:00 4c 0a 00 28 ff 01 00 00 00 00 00 00 00 00 00 00 00 f9 09 00 58 37 08 00 00 1f 07 08 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:1177:00 4c 0a 00 28 ff 01 00 00 00 00 00 00 00 00 00 00 00 f9 09 00 58 37 08 00 00 1f 07 08 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:1203:00 4c 0a 00 28 ff 01 00 00 00 00 00 00 00 00 00 00 00 f9 09 00 58 37 08 00 00 1f 07 08 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:1316:02 03 0b 00 02 0c 40 ea e9 80 46 00 80 98 c3 bc f2 fd c5 07 00 04 80 80 80 80 c0 4c 0a 00 28 01
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:1408:02 03 08 00 04 80 80 80 80 c0 4c 0a 00 28 01 01 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:1432:02 03 0a 00 04 80 80 80 80 c0 4c 0a 00 28 01 01 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:1442:82 77 8a 0b 49 03 06 00 04 80 80 80 80 c0 4c 0a 00 28 01 01 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSpawnedNearAkasakaandTagged.log.txt:1599:02 03 02 00 02 80 04 00 0b 00 04 80 80 80 80 c0 4c 0a 00 28 01 01 00 00
```

### Object599 fast healing effect id

Signature: `b5 07 00 28` (spaced or compact hex)

Matches: 0

### Object599 accelerated spawn effect id

Signature: `66 00 00 04` (spaced or compact hex)

Matches: 41

```text
mxopackets2_RajkoPacketsNearTheEnd_PacketsAgentsAndIntPortingSyncHardline.log.txt:8148:00 00 00 00 10 00 00 22 d5 04 70 01 03 00 04 01 80 c3 36 00 00 4e 66 00 00 04 00 00 00 86 5e 70
mxopackets2_RajkoPacketsNearTheEnd_PacketsAgentsAndIntPortingSyncHardline.log.txt:9055:00 00 00 00 00 00 00 01 00 98 08 ff 00 00 00 00 00 00 00 00 22 b6 00 00 66 00 00 04 ff 07 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsAgentsAndIntPortingSyncHardline.log.txt:9081:00 00 66 00 00 04 ff 07 00 00 00 00 00 00 00 00 00 00 00 36 00 00 4e 37 08 00 00 1f 07 08 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsAgentsAndIntPortingSyncHardline.log.txt:9105:b6 00 00 66 00 00 04 ff 07 00 00 00 00 00 00 00 00 00 00 00 36 00 00 4e 37 08 00 00 1f 07 08 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsAgentsAndIntPortingSyncHardline.log.txt:9129:b6 00 00 66 00 00 04 ff 07 00 00 00 00 00 00 00 00 00 00 00 36 00 00 4e 37 08 00 00 1f 07 08 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsAgentsAndIntPortingSyncHardline.log.txt:9152:00 00 00 00 00 00 01 00 98 08 ff 00 00 00 00 00 00 00 00 22 b6 00 00 66 00 00 04 ff 07 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsAgentsAndIntPortingSyncHardline.log.txt:9177:00 00 00 00 01 00 98 08 ff 00 00 00 00 00 00 00 00 22 b6 00 00 66 00 00 04 ff 07 00 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsAgentsAndIntPortingSyncHardline.log.txt:9204:98 08 ff 00 00 00 00 00 00 00 00 22 b6 00 00 66 00 00 04 ff 07 00 00 00 00 00 00 00 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsAgentsAndIntPortingSyncHardline.log.txt:13959:00 66 00 00 04 ff 18 00 00 00 00 00 00 00 00 00 00 00 36 00 00 4e 37 08 00 00 1f 07 08 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsAgentsAndIntPortingSyncHardline.log.txt:13981:00 22 b6 00 00 66 00 00 04 ff 18 00 00 00 00 00 00 00 00 00 00 00 36 00 00 4e 37 08 00 00 1f 07
```

## Intake Next Steps

- Preserve the original files separately before editing or importing anything.
- Inspect matches with surrounding lines; embedded hits inside encrypted or unrelated bodies are leads only.
- Record sender, provenance, CR1/CR2 status, action context, character handle, coordinates, known object ids, and known item GoIDs.
- Import only text logs or text-like dumps that contain no passwords, session tokens, private keys, client binaries, or unrelated game assets.
