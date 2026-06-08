# Packet Capture Triage Report

- Source kind: `directory`
- Source input: `/Users/kyle/Developer/mxo-hd/research/packet-dumps/community-intake/20260607T191353Z-hdneo-public-combat-pvp-rajko-state-selected/logs`
- Scanned files from: `/Users/kyle/Developer/mxo-hd/research/packet-dumps/community-intake/20260607T191353Z-hdneo-public-combat-pvp-rajko-state-selected/logs`
- Generated UTC: `2026-06-07T19:13:53Z`
- Max sample lines per pattern: `10`

Use this report to decide whether received logs are worth importing into `research/packet-dumps/`. Matches are byte-string leads, not decoded packet claims.

## Vendor And Interaction Leads

### SERVER_VENDOR_OPEN candidate

Signature: `81 0d` (spaced or compact hex)

Matches: 20

```text
mxopackets1_crashover2k_008_some_pvp_and_teleports_and_some_doors_open.log.txt:33075:02 04 01 01 b6 01 4a 81 0d 7c ad d9 43 00 00 00 00 a0 b7 f6 c0 00 00 00 00 00 3c 94 40 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCCyphs.log.txt:8280:6d 45 67 00 02 a0 9a 99 19 3e 80 10 81 0d 31 00 03 27 17 c0 00 69 55 01 0c 00 00 03 a0 cd cc 4c
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCCyphs.log.txt:8330:67 00 02 a0 9a 99 19 3e 80 90 81 0d 08 15 31 00 03 27 17 c0 00 69 55 01 0c 00 00 03 a0 cd cc 4c
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCThenTimeDOut.log.txt:2346:9a 9f 07 00 b0 74 00 00 e0 2d 81 0d 22 89 20 00 0b 0c 11 79 80 00 00 8a 01 fd 00 00 00 bf 4b 5b
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCThenTimeDOut.log.txt:20296:b1 6d a6 40 ba 13 fd 43 00 00 00 fb ff 9a 9f 07 00 b0 74 00 00 e0 2d 81 0d 22 89 20 00 0b 0c 11
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCThenTimeDOut.log.txt:25153:37 08 00 00 1f 07 08 00 00 00 00 00 00 22 00 00 00 00 00 00 00 04 01 01 dd 01 83 8e 81 0d 7c ad
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCThenTimeDOut.log.txt:28313:ba 13 fd 43 00 00 00 fb ff 9a 9f 07 00 b0 74 00 00 e0 2d 81 0d 22 89 20 00 0b 0c 11 79 80 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCThenTimeDOut.log.txt:35901:10 7d 45 ff 01 43 00 00 00 00 00 00 00 e0 2d 81 0d 22 89 20 00 0b 0c 11 79 80 00 00 01 9a 99 19
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCThenTimeDOut.log.txt:56339:89 7b 45 80 81 00 00 00 00 80 c0 fe 02 e0 f9 b9 00 00 f2 00 00 28 81 0d 03 06 08 00 00 00 00 10
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCThenTimeDOut.log.txt:56508:81 00 00 00 00 80 c0 00 03 e0 f9 b9 00 00 f2 00 00 28 81 0d 03 06 08 00 00 00 00 10 00 c4 00 02
```

### CLIENT_VENDOR_BUY candidate

Signature: `81 0e` (spaced or compact hex)

Matches: 14

```text
mxopackets2_RajkoPacketsNearTheEnd_PacketsSomethingMaracZombiesDowntown.log.txt:36115:02 03 02 00 01 0c af a7 5c 02 c8 50 92 74 44 51 81 0e c7 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCThenTimeDOut.log.txt:9158:80 08 81 0e 00 01 0e 00 f4 1d 69 88 46 00 80 f7 43 73 2b 88 45 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCThenTimeDOut.log.txt:75157:01 0d cd cc cc 3e 01 ac 02 36 00 00 b0 f0 0a fd 07 00 00 81 0e ba 00 00 02 0d f3 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCThenTimeDOut.log.txt:75449:a3 bf ab 40 d4 02 0d 01 9a 01 0d cd cc cc 3e a8 35 00 00 b0 be 0a fd 07 00 00 81 0e ba 00 00 06
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCThenTimeDOut.log.txt:83958:f7 43 4e 41 68 45 e5 00 04 80 80 80 c0 7b 07 c0 11 0a 00 28 81 0e 02 00 00 10 00 e3 00 06 0c e0
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCThenTimeDOut.log.txt:86199:02 36 00 00 b0 f0 0a fd 07 00 00 81 0e ba 00 00 02 0d f6 00 00 01 00 02 0b 00 34 00 04 80 80 80
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCThenTimeDOut.log.txt:86897:14 00 04 80 80 80 e0 01 00 00 c0 30 04 00 28 81 0e 02 04 00 11 00 11 00 04 80 80 80 c0 5b 04 c0
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCThenTimeDOut.log.txt:87128:9a 01 0d cd cc cc 3e a8 35 00 00 b0 be 0a fd 07 00 00 81 0e ba 00 00 06 0d 35 08 00 00 48 01 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCThenTimeDOut.log.txt:87951:00 00 00 0d 00 e8 80 80 81 00 00 00 00 c0 1b 04 e0 23 ba 00 00 30 04 00 28 81 0e 02 00 00 10 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCThenTimeDOut.log.txt:89860:46 00 80 f7 43 04 44 63 45 80 80 80 c0 d6 00 c0 03 08 00 28 81 0e 02 00 00 10 00 a2 00 03 1d 0c
```

### CLIENT_VENDOR_SELL candidate

Signature: `81 11` (spaced or compact hex)

Matches: 13

```text
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCCyphs.log.txt:24661:8d 8b a7 82 46 00 80 f7 43 c0 63 46 44 9b 00 01 0e 03 fd 81 11 81 46 00 80 f7 43 76 f5 56 44 99
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCCyphs.log.txt:45490:43 81 11 82 44 44 00 01 28 bd c0 00 70 a6 02 97 f0 88 46 00 80 f7 43 63 aa d5 45 32 00 02 0c 1e
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCCyphs.log.txt:55313:00 80 f7 43 17 c4 0b 45 0b 00 02 80 80 10 81 11 09 00 02 80 80 10 38 14 04 00 03 02 65 80 80 10
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCThenTimeDOut.log.txt:75085:4c 3e ac 02 36 00 00 b0 f0 0a fd 07 00 00 81 11 ba 00 00 02 0d bc 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCThenTimeDOut.log.txt:75342:3f ac 01 36 00 00 b0 f0 0a fd 07 00 00 81 11 ba 00 00 02 0d 71 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCThenTimeDOut.log.txt:88345:0a fd 07 00 00 81 11 ba 00 00 02 0d 26 01 00 01 00 0c 57 02 7a cd ab 1b 8e 52 68 61 6d 69 65 6c
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCThenTimeDOut.log.txt:89897:03 08 00 28 81 11 03 fd 07 00 00 00 00 00 00 51 00 01 01 33 00 06 2a 0c 81 00 98 56 00 a0 00 ac
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCThenTimeDOut.log.txt:90103:0a fd 07 00 00 81 11 ba 00 00 02 0d 4d 01 00 01 00 0c 57 02 ff cd ab 17 8e 42 61 67 6c 69 73 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCThenTimeDOut.log.txt:92216:e0 01 00 00 c0 30 04 00 28 81 11 03 fd 07 00 00 00 00 00 00 a2 00 03 03 00 00 74 80 80 80 80 04
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCThenTimeDOut.log.txt:92634:43 5d 50 69 45 c2 03 00 80 80 e0 01 00 00 80 81 11 03 fd 07 00 00 00 00 00 00 0b 01 06 2a 0e 01
```

### CR2 static object interaction

Signature: `80 c8` (spaced or compact hex)

Matches: 14

```text
mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt:5183:02 03 02 00 02 80 84 4f 80 c8 00 00 3a 09 b0 37 06 00 28 07 80 01 04 00 10 01 06 00 01 00 00 80
mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt:8689:02 03 02 00 02 80 88 00 00 80 bf 80 c8 00 00 35 06 80 80 01 20 00 10 01 1b 00 01 00 01 35 06 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt:8749:82 92 0b 04 49 03 02 00 02 80 8c 1a 00 00 80 bf 80 c8 00 00 35 06 80 80 01 20 00 10 01 0f 00 03
mxopackets1_crashover2k_008_some_pvp_and_teleports_and_some_doors_open.log.txt:10067:02 04 01 00 30 01 08 80 c8 04 00 a0 1b 04 00
mxopackets1_crashover2k_008_some_pvp_and_teleports_and_some_doors_open.log.txt:20064:02 04 01 00 4c 01 08 80 c8 b4 0e a0 26 04 00
mxopackets1_crashover2k_008_some_pvp_and_teleports_and_some_doors_open.log.txt:20842:02 03 0a 00 02 08 5c 21 99 c7 04 f0 4d 45 b8 5e 0d c6 09 00 02 0c 80 c8 46 9b c7 04 f0 4d 45 bf
mxopackets1_crashover2k_008_some_pvp_and_teleports_and_some_doors_open.log.txt:23743:02 04 01 00 69 01 08 80 c8 06 00 a0 26 03 00
mxopackets1_crashover2k_008_some_pvp_and_teleports_and_some_doors_open.log.txt:31910:02 04 01 00 86 01 08 80 c8 a9 36 10 01 04 00
mxopackets1_crashover2k_008_some_pvp_and_teleports_and_some_doors_open.log.txt:33048:02 04 01 00 8c 01 08 80 c8 ea 36 10 01 02 00
mxopackets1_crashover2k_008_some_pvp_and_teleports_and_some_doors_open.log.txt:60460:02 03 02 00 01 0a 00 22 e3 82 45 00 e0 a1 44 19 01 bd 47 00 00 04 01 00 40 01 08 80 c8 6c 27 30
```

### CR1 static object interaction

Signature: `80 c3` (spaced or compact hex)

Matches: 61

```text
mxopackets2_RajkoPacketsNearTheEnd_PacketsSomethingMaracZombiesDowntown.log.txt:29936:00 22 d5 01 03 6c 1b 00 c6 01 80 c3 b3 06 00 58 4c 0a 00 28 00 00 00 86 87 51 d9 40 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSomethingMaracZombiesDowntown.log.txt:29939:00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 10 00 00 22 d5 01 09 6b 1b 00 c6 01 80 c3 b1
mxopackets2_RajkoPacketsNearTheEnd_PacketsSomethingMaracZombiesDowntown.log.txt:29978:00 00 00 00 00 00 00 00 00 00 00 00 00 10 00 00 22 d5 01 1a 6c 1b 00 c6 01 80 c3 b4 06 00 58 4c
mxopackets2_RajkoPacketsNearTheEnd_PacketsSomethingMaracZombiesDowntown.log.txt:31939:00 00 00 00 00 00 00 00 00 00 00 00 00 10 00 00 22 d5 01 c2 cc 1b 00 c6 01 80 c3 95 0a 00 58 4c
mxopackets2_RajkoPacketsNearTheEnd_PacketsSomethingMaracZombiesDowntown.log.txt:40144:cf 01 06 80 c3 98 23 96 12
mxopackets2_RajkoPacketsNearTheEnd_PacketsSomethingMaracZombiesDowntown.log.txt:48163:01 06 80 c3 2b 1e 97 12
mxopackets2_RajkoPacketsNearTheEnd_PacketsSomethingMaracZombiesDowntown.log.txt:51845:00 00 e0 bb c0 f7 c0 10 e3 00 00 00 04 01 01 d8 01 06 80 c3 70 81 97 12
mxopackets2_RajkoPacketsNearTheEnd_PacketsSomethingMaracZombiesDowntown.log.txt:60013:6e 67 65 72 00 00 00 00 00 00 00 00 00 00 00 00 00 10 00 00 22 d5 02 9d d8 1b 00 c6 01 80 c3 6a
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_002_combat_zombie_lurch.log.txt:2010:00 00 00 00 00 00 00 00 00 00 00 00 00 10 00 00 22 d5 01 c8 f7 1a 00 c6 01 80 c3 9b 00 00 4e 4c
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_002_combat_zombie_lurch.log.txt:2977:69 63 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 10 00 00 22 d5 01 14 fd 1a 00 c6 01 80 c3 9b
```

### SERVER object/state bundle

Signature: `80 bc` (spaced or compact hex)

Matches: 7493

```text
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_002_combat_corrupted_spawn_while_jackout_process.log.txt:143:82 31 a5 01 49 04 01 01 44 0d 16 80 bc 15 00 f4 21 00 f7 03 00 00 08 02 00 00 00 00 01 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_002_combat_corrupted_spawn_while_jackout_process.log.txt:144:00 16 80 bc 15 00 f5 21 00 f7 03 00 00 07 02 ec ff ff ff 01 00 00 00 00 16 80 bc 15 00 f6 21 00
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_002_combat_corrupted_spawn_while_jackout_process.log.txt:145:f7 03 00 00 50 04 00 00 00 00 01 00 00 00 00 16 80 bc 15 00 f7 21 00 f7 03 00 00 f4 03 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_002_combat_corrupted_spawn_while_jackout_process.log.txt:146:00 01 00 00 00 00 16 80 bc 15 00 f8 21 00 f7 03 00 00 51 04 f6 ff ff ff 01 00 00 00 00 16 80 bc
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_002_combat_corrupted_spawn_while_jackout_process.log.txt:147:15 00 f9 21 00 f7 03 00 00 52 04 0f 00 00 00 01 00 00 00 00 16 80 bc 15 00 a5 22 00 f9 03 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_002_combat_corrupted_spawn_while_jackout_process.log.txt:148:08 02 ec ff ff ff 00 00 00 00 00 16 80 bc 15 00 a6 22 00 f9 03 00 00 07 02 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_002_combat_corrupted_spawn_while_jackout_process.log.txt:149:00 00 16 80 bc 15 00 a7 22 00 f9 03 00 00 50 04 14 00 00 00 00 00 00 00 00 16 80 bc 15 00 a8 22
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_002_combat_corrupted_spawn_while_jackout_process.log.txt:150:00 f9 03 00 00 f4 03 01 00 00 00 00 00 00 00 00 16 80 bc 15 00 a9 22 00 f9 03 00 00 51 04 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_002_combat_corrupted_spawn_while_jackout_process.log.txt:151:00 00 00 00 00 00 00 16 80 bc 15 00 aa 22 00 f9 03 00 00 52 04 f6 ff ff ff 00 00 00 00 00 2f 47
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_002_combat_corrupted_spawn_while_jackout_process.log.txt:731:00 00 04 01 01 53 01 16 80 bc 09 00 46 22 00 ca 00 00 00 ca 00 e7 ff ff ff 02 00 80 90 43
```

## Selector ff Vendor Body Leads

### Selector ff vendor field +9 family

Signature: `e0 2d 81 0d` (spaced or compact hex)

Matches: 5

```text
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCThenTimeDOut.log.txt:2346:9a 9f 07 00 b0 74 00 00 e0 2d 81 0d 22 89 20 00 0b 0c 11 79 80 00 00 8a 01 fd 00 00 00 bf 4b 5b
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCThenTimeDOut.log.txt:20296:b1 6d a6 40 ba 13 fd 43 00 00 00 fb ff 9a 9f 07 00 b0 74 00 00 e0 2d 81 0d 22 89 20 00 0b 0c 11
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCThenTimeDOut.log.txt:28313:ba 13 fd 43 00 00 00 fb ff 9a 9f 07 00 b0 74 00 00 e0 2d 81 0d 22 89 20 00 0b 0c 11 79 80 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCThenTimeDOut.log.txt:35901:10 7d 45 ff 01 43 00 00 00 00 00 00 00 e0 2d 81 0d 22 89 20 00 0b 0c 11 79 80 00 00 01 9a 99 19
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCThenTimeDOut.log.txt:89474:17 ab 40 00 00 fd 43 00 00 00 fb ff 9a 9f 07 00 b0 74 00 00 e0 2d 81 0d 22 89 20 00 0b 0c 11 79
```

### Selector ff vendor body prefix

Signature: `01 43 00 00 00 00 00 00 00 e0 2d 81 0d 22 89 20` (spaced or compact hex)

Matches: 1

```text
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCThenTimeDOut.log.txt:35901:10 7d 45 ff 01 43 00 00 00 00 00 00 00 e0 2d 81 0d 22 89 20 00 0b 0c 11 79 80 00 00 01 9a 99 19
```

### Selector ff continuation prefix 00 41 00 ff

Signature: `00 41 00 ff` (spaced or compact hex)

Matches: 42

```text
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCCyphs.log.txt:16377:00 d5 14 00 ed 58 00 00 ff ff 00 00 00 00 21 0a 00 28 02 00 00 00 00 00 00 00 00 41 00 ff 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCCyphs.log.txt:16409:00 21 0a 00 28 02 00 00 00 00 00 00 00 00 41 00 ff 00 00 00 00 ef ab 00 00 00 00 08 00 12 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCCyphs.log.txt:16440:00 00 21 0a 00 28 02 00 00 00 00 00 00 00 00 41 00 ff 00 00 00 00 ef ab 00 00 00 00 08 00 12 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCCyphs.log.txt:16511:00 21 0a 00 28 02 00 00 00 00 00 00 00 00 41 00 ff 00 00 00 00 ef ab 00 00 00 00 08 00 12 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCThenTimeDOut.log.txt:4608:00 45 3b 00 00 ff ff 00 00 00 00 18 00 00 04 29 00 00 00 00 00 00 00 00 41 00 ff 73 03 00 08 c0
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCThenTimeDOut.log.txt:4680:00 00 00 00 00 00 00 41 00 ff 73 03 00 08 c0 65 00 00 80 00 08 00 12 07 d9 00 00 00 00 01 00 59
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCThenTimeDOut.log.txt:6924:04 90 00 00 00 00 00 00 00 00 41 00 ff b8 0d 00 04 d2 1b 01 00 80 00 08 00 12 00 00 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCThenTimeDOut.log.txt:7019:00 00 00 00 41 00 ff b8 0d 00 04 d2 1b 01 00 80 00 08 00 12 00 00 00 00 00 00 01 00 85 00 03 0e
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCThenTimeDOut.log.txt:7057:90 00 00 00 00 00 00 00 00 41 00 ff b8 0d 00 04 d2 1b 01 00 80 00 08 00 12 00 00 00 00 00 00 01
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCThenTimeDOut.log.txt:7122:04 90 00 00 00 00 00 00 00 00 41 00 ff b8 0d 00 04 d2 1b 01 00 80 00 08 00 12 00 00 00 00 00 00
```

### Selector ff continuation prefix 00 01 00 ff

Signature: `00 01 00 ff` (spaced or compact hex)

Matches: 2765

```text
mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt:1158:ff ff 2f a8 00 00 3f 00 00 04 65 00 00 00 00 00 00 00 00 01 00 ff 0f 00 00 dc 78 5e 01 00 80 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt:1188:65 00 00 00 00 00 00 00 00 01 00 ff 0f 00 00 dc 78 5e 01 00 80 00 18 01 11 05 6c 05 00 00 00 01
mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt:1218:00 00 00 00 00 00 00 00 01 00 ff 0f 00 00 dc 78 5e 01 00 80 00 18 01 11 05 6c 05 00 00 00 01 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt:1248:65 00 00 00 00 00 00 00 00 01 00 ff 0f 00 00 dc 78 5e 01 00 80 00 18 01 11 05 6c 05 00 00 00 01
mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt:1278:00 00 00 00 00 00 00 00 01 00 ff 0f 00 00 dc 78 5e 01 00 80 00 18 01 11 05 6c 05 00 00 00 01 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt:1330:3f 00 00 04 65 00 00 00 00 00 00 00 00 01 00 ff 0f 00 00 dc 78 5e 01 00 80 00 18 01 11 05 6c 05
mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt:2077:00 00 00 00 01 00 ff 00 00 00 00 fe 5d 01 00 00 00 00 01 22 00 00 00 00 00 00 01 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt:2105:21 0a 00 28 0d 00 00 00 00 00 00 00 00 01 00 ff 00 00 00 00 fe 5d 01 00 00 00 00 01 22 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt:2135:00 00 ff 00 00 00 00 00 21 0a 00 28 0d 00 00 00 00 00 00 00 00 01 00 ff 00 00 00 00 fe 5d 01 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt:2162:28 0d 00 00 00 00 00 00 00 00 01 00 ff 00 00 00 00 fe 5d 01 00 00 00 00 01 22 00 00 00 00 00 00
```

### Selector ff continuation prefix 00 01 04 ff

Signature: `00 01 04 ff` (spaced or compact hex)

Matches: 75

```text
mxopackets2_RajkoPacketsNearTheEnd_PacketsSomethingMaracZombiesDowntown.log.txt:7019:02 03 02 00 01 04 ff 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCCyphs.log.txt:3867:00 00 00 00 00 00 00 01 04 ff 00 00 00 00 fe 2f 00 00 00 00 10 01 22 01 27 02 00 00 00 01 00 01
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCCyphs.log.txt:3907:00 01 04 ff 00 00 00 00 fe 2f 00 00 00 00 10 01 22 01 27 02 00 00 00 01 00 01 00 02 13 00 2b 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCCyphs.log.txt:4625:00 73 1e 00 00 ff 00 00 00 00 00 44 00 00 04 8c 00 00 00 00 00 00 00 00 01 04 ff 00 00 00 00 fe
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCCyphs.log.txt:5122:8d 00 00 00 00 00 00 00 00 01 04 ff 00 00 00 00 fe 2f 00 00 00 00 10 01 22 01 27 02 00 00 00 01
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCCyphs.log.txt:5313:00 00 00 a8 0f 00 73 1e 00 00 ff 00 00 00 00 00 30 04 00 28 8d 00 00 00 00 00 00 00 00 01 04 ff
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCCyphs.log.txt:5750:0f 00 73 1e 00 00 ff 00 00 00 00 00 44 00 00 04 8f 00 00 00 00 00 00 00 00 01 04 ff 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCCyphs.log.txt:5809:00 04 8f 00 00 00 00 00 00 00 00 01 04 ff 00 00 00 00 fe 2f 00 00 02 00 10 01 22 01 27 02 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCCyphs.log.txt:6328:00 00 30 04 00 28 90 00 00 00 00 00 00 00 00 01 04 ff 00 00 00 00 fe 2f 00 00 02 00 10 01 22 01
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCCyphs.log.txt:6857:00 01 04 ff 00 00 00 00 fe 2f 00 00 02 00 10 01 22 01 27 02 00 00 00 01 00 00 00
```

### Selector ff continuation prefix 00 01 10 ff

Signature: `00 01 10 ff` (spaced or compact hex)

Matches: 1537

```text
mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt:1109:00 35 7e 00 00 ff a6 22 b6 00 00 eb 09 00 28 ce 00 00 00 00 00 00 00 00 01 10 ff 00 00 00 00 9d
mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt:1183:00 00 00 00 00 01 10 ff 00 00 00 00 9d 45 01 00 00 00 98 01 12 01 57 04 00 00 00 01 00 03 00 03
mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt:1213:00 00 00 00 01 10 ff 00 00 00 00 9d 45 01 00 00 00 98 01 12 01 57 04 00 00 00 01 00 03 00 03 2a
mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt:1243:00 00 00 00 00 01 10 ff 00 00 00 00 9d 45 01 00 00 00 98 01 12 01 57 04 00 00 00 01 00 03 00 03
mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt:1273:00 00 00 00 00 01 10 ff 00 00 00 00 9d 45 01 00 00 00 98 01 12 01 57 04 00 00 00 01 00 03 00 03
mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt:1305:28 ce 00 00 00 00 00 00 00 00 01 10 ff 00 00 00 00 9d 45 01 00 00 00 98 01 12 01 57 04 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt:1350:00 00 00 00 00 01 10 ff 00 00 00 00 9d 45 01 00 00 00 98 01 12 01 57 04 00 00 00 01 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt:1395:05 00 35 7e 00 00 ff a6 22 b6 00 00 eb 09 00 28 ce 00 00 00 00 00 00 00 00 01 10 ff 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt:1426:00 00 00 00 00 01 10 ff 00 00 00 00 9d 45 01 00 00 00 18 01 12 01 57 04 00 00 00 01 00 04 00 01
mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt:1441:00 00 00 00 01 10 ff 00 00 00 00 9d 45 01 00 00 00 18 01 12 01 57 04 00 00 00 01 00 00 00
```

### Selector ff continuation prefix 00 00 00 ff

Signature: `00 00 00 ff` (spaced or compact hex)

Matches: 10767

```text
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_002_combat_corrupted_spawn_while_jackout_process.log.txt:575:00 00 00 40 ec 88 b8 c0 00 00 00 00 ff 9b 9d 40 00 00 00 38 6b 47 d0 40 c0 01 83 01 03 a8 1f 39
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_002_combat_corrupted_spawn_while_jackout_process.log.txt:710:00 00 23 1a e9 88 80 b0 04 00 02 8e 00 00 00 01 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ff 28
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_002_combat_corrupted_spawn_while_jackout_process.log.txt:713:00 00 12 31 46 12 03 00 00 00 00 ff 00 00 00 00 01 00 76 0c 00 58 19 04 36 3f 11 00 00 00 4b b1
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_002_combat_corrupted_spawn_while_jackout_process.log.txt:1004:00 00 00 00 d5 04 00 00 00 00 1f 00 00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_002_combat_corrupted_spawn_while_jackout_process.log.txt:1024:00 00 00 00 d5 04 00 00 00 00 1f 00 00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_002_combat_corrupted_spawn_while_jackout_process.log.txt:1044:00 00 00 d5 04 00 00 00 00 1f 00 00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_002_combat_corrupted_spawn_while_jackout_process.log.txt:1064:00 00 4e 00 00 00 0c 00 0b bd c0 00 00 00 00 ff 9b 9d 40 00 00 00 58 b7 65 d1 40 d0 0c 01 82 02
mxopackets1_crashover2k_afterwhoruneo_proxy_003_search_and_loot_corrupted.log.txt:96:00 00 23 1a e9 88 80 b0 04 00 02 8e 00 00 00 01 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ff da
mxopackets1_crashover2k_afterwhoruneo_proxy_003_search_and_loot_corrupted.log.txt:98:00 00 00 00 00 ff 00 00 00 00 00 f7 00 00 00 c1 00 00 00 00 00 39 04 ff 00 31 5e 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_search_and_loot_corrupted.log.txt:99:00 00 37 06 00 28 04 00 00 00 00 ff 00 00 00 00 01 00 00 00 00 00 00 00 80 3f ee 03 00 00 4b b1
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

Matches: 725

```text
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_002_combat_corrupted_spawn_while_jackout_process.log.txt:573:02 03 01 00 0c 57 02 5f cd ab 14 8e 42 75 69 6c 64 69 6e 67 20 53 65 63 75 72 69 74 79 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_002_combat_corrupted_spawn_while_jackout_process.log.txt:1062:02 03 01 00 0c 57 02 61 cd ab 14 ae 43 6f 72 72 75 70 74 65 64 00 00 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt:765:0a 00 d0 84 46 00 80 f7 43 00 c0 0f 45 01 00 0c 57 02 73 cd ab 11 8e 42 6c 61 63 6b 77 6f 6f 64
mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt:768:40 c0 01 8a 01 15 a8 04 2c 01 b2 04 2c 01 07 08 00 00 80 02 01 15 00 00 01 00 0c 57 02 c9 cd ab
mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt:4430:02 03 01 00 0c 57 02 72 cd ab 10 8e 42 6c 61 63 6b 77 6f 6f 64 20 47 6f 6f 66 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt:6331:02 03 01 00 0c 57 02 72 cd ab 10 8e 42 6c 61 63 6b 77 6f 6f 64 20 47 6f 6f 66 00 00 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSomethingMaracZombiesDowntown.log.txt:3518:00 00 01 00 0c 57 02 55 cd ab 11 8e 42 6c 61 63 6b 77 6f 6f 64 20 41 63 65 00 00 00 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSomethingMaracZombiesDowntown.log.txt:3645:02 03 02 00 02 80 80 80 50 c4 00 8f 0a 01 00 0c 57 02 56 cd ab 11 8e 42 6c 61 63 6b 77 6f 6f 64
mxopackets2_RajkoPacketsNearTheEnd_PacketsSomethingMaracZombiesDowntown.log.txt:3648:c0 c0 01 8a 03 0c a8 05 af 00 b2 09 af 00 12 08 00 00 80 02 01 06 00 00 01 00 0c 57 02 4c cd ab
mxopackets2_RajkoPacketsNearTheEnd_PacketsSomethingMaracZombiesDowntown.log.txt:3652:1a 00 00 01 00 0c 57 02 49 cd ab 11 8e 42 6c 61 63 6b 77 6f 6f 64 20 47 6f 6f 66 00 00 00 00 00
```

### Adjacent Object599 boundary prefix

Signature: `00 0c 57 02` (spaced or compact hex)

Matches: 708

```text
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_002_combat_corrupted_spawn_while_jackout_process.log.txt:573:02 03 01 00 0c 57 02 5f cd ab 14 8e 42 75 69 6c 64 69 6e 67 20 53 65 63 75 72 69 74 79 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_002_combat_corrupted_spawn_while_jackout_process.log.txt:1062:02 03 01 00 0c 57 02 61 cd ab 14 ae 43 6f 72 72 75 70 74 65 64 00 00 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt:765:0a 00 d0 84 46 00 80 f7 43 00 c0 0f 45 01 00 0c 57 02 73 cd ab 11 8e 42 6c 61 63 6b 77 6f 6f 64
mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt:768:40 c0 01 8a 01 15 a8 04 2c 01 b2 04 2c 01 07 08 00 00 80 02 01 15 00 00 01 00 0c 57 02 c9 cd ab
mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt:4430:02 03 01 00 0c 57 02 72 cd ab 10 8e 42 6c 61 63 6b 77 6f 6f 64 20 47 6f 6f 66 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt:6331:02 03 01 00 0c 57 02 72 cd ab 10 8e 42 6c 61 63 6b 77 6f 6f 64 20 47 6f 6f 66 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_002_combat_zombie_lurch.log.txt:1525:02 03 01 00 0c 57 02 21 cd ab 15 8e 44 69 73 63 69 70 6c 65 73 20 49 6e 69 74 69 61 74 65 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_002_combat_zombie_lurch.log.txt:2009:02 03 01 00 0c 57 02 1a cd ab 13 8e 44 69 73 63 69 70 6c 65 73 20 49 6e 69 74 69 61 74 65 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_002_combat_zombie_lurch.log.txt:2976:82 22 f4 00 49 03 01 00 0c 57 02 1e cd ab 14 8e 44 69 73 63 69 70 6c 65 73 20 45 63 73 74 61 74
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_002_combat_zombie_lurch.log.txt:6081:02 03 01 00 0c 57 02 1f cd ab 13 8e 44 69 73 63 69 70 6c 65 73 20 49 6e 69 74 69 61 74 65 00 00
```

### Header-like selector 50

Signature: `01 00 08 50` (spaced or compact hex)

Matches: 18

```text
mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt:777:f2 04 35 bf 00 00 00 00 f3 04 35 3f 0b 00 00 01 00 08 50 02 ea 01 30 39 0d cd ab 01 01 00 00 00
mxopackets1_crashover2k_008_some_pvp_and_teleports_and_some_doors_open.log.txt:10096:02 03 01 00 08 50 02 04 00 a0 1b cb cd ab 01 01 00 00 00 00 a0 aa f4 c0 00 00 00 00 00 40 60 40
mxopackets1_crashover2k_008_some_pvp_and_teleports_and_some_doors_open.log.txt:20175:02 03 01 00 08 50 02 b4 0e a0 26 4c cd ab 01 01 00 00 00 00 20 f5 f2 c0 00 00 00 00 00 c8 94 40
mxopackets1_crashover2k_008_some_pvp_and_teleports_and_some_doors_open.log.txt:31961:02 03 01 00 08 50 02 a9 36 10 01 31 cd ab 01 01 00 00 00 00 c0 56 f6 c0 00 00 00 00 00 c4 94 40
mxopackets1_crashover2k_008_some_pvp_and_teleports_and_some_doors_open.log.txt:34964:00 01 00 08 50 02 ea 01 30 39 23 cd ab 01 01 00 00 00 00 00 9a d0 40 00 00 00 00 00 90 80 40 00
mxopackets1_crashover2k_008_some_pvp_and_teleports_and_some_doors_open.log.txt:60610:82 92 ca f1 45 03 01 00 01 01 00 35 00 01 00 08 50 02 6c 27 30 1e 02 cd ab 02 09 00 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSomethingMaracZombiesDowntown.log.txt:444:7b 40 00 00 00 00 00 d0 a0 40 01 07 00 00 01 00 08 50 02 ea 01 30 39 da cd ab 01 01 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSomethingMaracZombiesDowntown.log.txt:29980:a8 1f d5 04 b2 09 d5 04 12 08 00 00 81 c1 09 00 00 02 07 05 00 00 01 00 08 50 02 a1 27 20 03 93
mxopackets2_RajkoPacketsNearTheEnd_PacketsSomethingMaracZombiesDowntown.log.txt:31945:05 00 00 01 00 08 50 02 a3 06 80 26 35 cd ab 02 09 00 00 00 00 70 4d 00 c1 00 00 00 00 00 c8 94
mxopackets2_RajkoPacketsNearTheEnd_PacketsSomethingMaracZombiesDowntown.log.txt:38607:02 03 01 00 08 50 02 7c 16 60 29 8c cd ab 02 09 00 00 00 00 e0 26 02 c1 00 00 00 00 00 d0 86 40
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
mxopackets2_RajkoPacketsNearTheEnd_PacketsSomethingMaracZombiesDowntown.log.txt:45432:f3 c0 c0 01 82 03 a8 1a bc 02 b2 09 bc 02 07 08 00 00 81 ca 1e 00 00 02 06 05 00 00 01 00 08 69
```

### Header-like selector 76

Signature: `01 00 0c 76` (spaced or compact hex)

Matches: 3

```text
mxopackets2_RajkoPacketsNearTheEnd_PacketsSomethingMaracZombiesDowntown.log.txt:48284:00 00 00 00 22 00 00 00 00 00 00 00 01 00 0c 76 a7 f1 cd ab 03 0d 00 00 00 00 20 78 02 c1 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSomethingMaracZombiesDowntown.log.txt:53335:00 00 00 00 40 83 f9 c0 0e 00 00 01 00 0c 76 a7 f1 cd ab 03 0d 00 00 00 00 20 78 02 c1 00 00 00
mxopackets1_crashover2k_008_some_pvp_and_teleports_and_some_doors_open.log.txt:31447:01 22 00 00 00 00 00 00 00 01 00 0c 76 a7 2c cd ab 03 0d 00 00 00 00 40 8f f7 c0 00 00 00 00 00
```

### Header-like selector 90

Signature: `01 00 08 90` (spaced or compact hex)

Matches: 0

### Header-like selector a3

Signature: `01 00 08 a3` (spaced or compact hex)

Matches: 0

### Header-like selector d0

Signature: `01 00 08 d0` (spaced or compact hex)

Matches: 89

```text
mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt:715:02 03 01 00 08 d0 20 f1 01 30 39 04 cd ab 08 fd 00 00 00 00 80 83 cf 40 00 00 00 00 00 f0 7e 40
mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt:719:00 f3 04 35 3f 21 00 00 01 00 08 d0 20 b1 00 20 4e fe cd ab 07 fd 00 00 00 00 00 f8 d1 40 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt:773:00 00 00 00 69 40 11 00 00 01 00 08 d0 20 ef 01 30 39 05 cd ab 08 fd 00 00 00 00 40 5a d5 40 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSomethingMaracZombiesDowntown.log.txt:331:5f ba 00 00 01 9e 00 00 00 24 00 23 00 00 01 00 08 d0 20 ee 01 30 39 df cd ab 08 fd 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSomethingMaracZombiesDowntown.log.txt:372:02 03 01 00 08 d0 20 f1 01 30 39 c1 cd ab 08 fd 00 00 00 00 80 83 cf 40 00 00 00 00 00 f0 7e 40
mxopackets2_RajkoPacketsNearTheEnd_PacketsSomethingMaracZombiesDowntown.log.txt:376:00 f3 04 35 3f 1f 00 00 01 00 08 d0 20 b1 00 20 4e c2 cd ab 07 fd 00 00 00 00 00 f8 d1 40 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSomethingMaracZombiesDowntown.log.txt:433:02 03 01 00 02 0a 00 23 00 02 80 80 10 8c 0a 01 00 08 d0 20 ef 01 30 39 de cd ab 08 fd 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSomethingMaracZombiesDowntown.log.txt:437:03 00 00 00 00 00 f2 04 35 bf 00 00 00 00 f3 04 35 3f 19 00 00 01 00 08 d0 20 b0 00 20 4e db cd
mxopackets2_RajkoPacketsNearTheEnd_PacketsSomethingMaracZombiesDowntown.log.txt:1345:02 03 02 00 02 80 04 1e 01 00 08 d0 20 af 00 20 4e e5 cd ab 08 fd 00 00 00 00 c0 a8 d4 40 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSomethingMaracZombiesDowntown.log.txt:4231:02 03 02 00 02 80 80 80 10 de 00 01 00 08 d0 20 af 00 20 4e e5 cd ab 08 fd 00 00 00 00 c0 a8 d4
```

## Movement And Object-State Leads

### Mode 06 movement selector 08

Signature: `00 06 08` (spaced or compact hex)

Matches: 364

```text
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_002_combat_zombie_lurch.log.txt:245:cf c5 07 00 06 08 25 80 99 c7 00 00 be 42 c5 bd d4 c5 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_002_combat_zombie_lurch.log.txt:317:cf c5 07 00 06 08 25 80 99 c7 00 00 be 42 c5 bd d4 c5 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_002_combat_zombie_lurch.log.txt:349:81 c5 08 00 02 08 c9 ca 9b c7 00 00 be 42 5f 25 cf c5 07 00 06 08 25 80 99 c7 00 00 be 42 c5 bd
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_002_combat_zombie_lurch.log.txt:381:cf c5 07 00 06 08 25 80 99 c7 00 00 be 42 c5 bd d4 c5 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_002_combat_zombie_lurch.log.txt:413:cf c5 07 00 06 08 25 80 99 c7 00 00 be 42 c5 bd d4 c5 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_002_combat_zombie_lurch.log.txt:445:cf c5 07 00 06 08 25 80 99 c7 00 00 be 42 c5 bd d4 c5 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_002_combat_zombie_lurch.log.txt:469:cf c5 07 00 06 08 25 80 99 c7 00 00 be 42 c5 bd d4 c5 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_002_combat_zombie_lurch.log.txt:501:be 42 5f 25 cf c5 07 00 06 08 25 80 99 c7 00 00 be 42 c5 bd d4 c5 ff 00 00 00 10 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_002_combat_zombie_lurch.log.txt:569:9b c7 00 00 be 42 5f 25 cf c5 80 80 80 80 80 80 01 12 08 00 00 07 00 06 08 25 80 99 c7 00 00 be
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_002_combat_zombie_lurch.log.txt:596:00 00 22 00 00 00 00 00 07 00 06 08 25 80 99 c7 00 00 be 42 c5 bd d4 c5 ff 00 00 00 10 00 00 00
```

### Mode 06 movement selector 0a

Signature: `00 06 0a` (spaced or compact hex)

Matches: 130

```text
mxopackets2_RajkoPacketsNearTheEnd_PacketsSomethingMaracZombiesDowntown.log.txt:45219:00 1f 12 08 00 00 00 00 00 00 22 00 00 00 00 00 0a 00 06 0a 01 a7 55 17 c8 00 00 be 42 32 da 97
mxopackets1_crashover2k_008_some_pvp_and_teleports_and_some_doors_open.log.txt:17202:02 03 11 00 06 0a 00 28 fd 97 c7 00 e0 a1 44 84 94 31 c4 ff 00 00 00 10 00 00 00 00 00 01 ff 00
mxopackets1_crashover2k_008_some_pvp_and_teleports_and_some_doors_open.log.txt:20119:01 07 08 00 00 0c 00 06 0a 01 67 72 9b c7 00 e0 a1 44 4e 1c 34 c5 ff 00 00 00 10 00 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCCyphs.log.txt:50675:8d 45 11 00 06 0a 00 00 02 85 46 00 80 f7 43 07 d9 97 45 ff 00 00 00 10 00 00 00 00 00 01 ff 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCCyphs.log.txt:52064:02 03 c9 00 06 0a 03 7e 47 88 46 00 80 f7 43 ac 06 36 45 ff 00 00 00 10 00 00 00 00 00 01 ff 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCCyphs.log.txt:53528:02 03 02 00 02 80 80 80 50 48 01 bb 14 11 00 04 80 80 80 40 38 0d c9 00 06 0a 00 d6 c1 8a 46 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCCyphs.log.txt:53770:02 03 02 00 02 80 80 80 10 4a 01 11 00 04 80 80 80 40 3c 0d c9 00 06 0a 00 d6 c1 8a 46 00 80 f7
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCCyphs.log.txt:54876:02 03 c9 00 06 0a 00 00 6e 8c 46 00 80 f7 43 fb 87 83 45 ff 00 00 00 10 00 00 00 00 00 01 ff 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCCyphs.log.txt:62471:43 04 97 85 44 21 00 01 0e 00 80 00 b2 89 46 00 80 f7 43 1f 2c 6e 45 0c 00 06 0a 03 8d 6f 8e 46
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCCyphs.log.txt:66046:80 f7 43 79 43 d6 45 77 00 02 80 80 10 df 03 72 00 02 80 80 10 b8 13 41 00 06 0a 00 31 a7 8e 46
```

### Mode 06 movement selector 0c

Signature: `00 06 0c` (spaced or compact hex)

Matches: 840

```text
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_002_combat_corrupted_spawn_while_jackout_process.log.txt:992:82 bb a6 01 49 03 03 00 06 0c 77 6e 3f 0a c6 f8 df ec 44 14 b0 69 46 80 80 80 80 80 01 02 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_002_combat_corrupted_spawn_while_jackout_process.log.txt:1042:02 03 03 00 06 0c 8f 6e 3f 0a c6 f8 df ec 44 14 b0 69 46 ff 00 00 00 10 00 00 00 00 00 01 ff 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt:4278:02 03 15 00 06 0c 66 34 ad 37 46 00 00 be 42 52 86 7a 45 ff 00 00 00 10 00 00 00 00 00 01 ff 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt:4310:02 03 02 00 02 80 04 4d 1b 00 01 00 04 ff 85 82 46 00 80 f7 43 5c 84 0f 45 15 00 06 0c 70 34 ad
mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt:4336:02 03 15 00 06 0c 7a 34 ad 37 46 00 00 be 42 52 86 7a 45 ff 00 00 00 10 00 00 00 00 00 01 ff 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt:4356:02 03 02 00 02 80 84 4d 80 40 23 09 1b 00 01 00 01 23 09 15 00 06 0c 84 34 ad 37 46 00 00 be 42
mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt:4376:02 03 15 00 06 0c 8e 34 ad 37 46 00 00 be 42 52 86 7a 45 ff 00 00 00 10 00 00 00 00 00 01 ff 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt:4396:82 d1 08 04 49 03 15 00 06 0c 98 34 ad 37 46 00 00 be 42 52 86 7a 45 ff 00 00 00 10 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt:8238:02 03 15 00 06 0c 75 34 ad 37 46 00 00 be 42 52 86 7a 45 ff 00 00 00 10 00 00 00 00 00 01 ff 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt:8258:02 03 15 00 06 0c 7f 34 ad 37 46 00 00 be 42 52 86 7a 45 ff 00 00 00 10 00 00 00 00 00 01 ff 00
```

### Mode 06 movement selector 0e

Signature: `00 06 0e` (spaced or compact hex)

Matches: 1034

```text
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_002_combat_corrupted_spawn_while_jackout_process.log.txt:1002:02 03 03 00 06 0e 00 81 6e 3f 0a c6 f8 df ec 44 14 b0 69 46 ff 00 00 00 10 00 00 00 00 00 01 ff
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_002_combat_corrupted_spawn_while_jackout_process.log.txt:1022:02 03 03 00 06 0e 00 8b 6e 3f 0a c6 f8 df ec 44 14 b0 69 46 ff 00 00 00 10 00 00 00 00 00 01 ff
mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt:2455:82 93 07 04 49 03 15 00 06 0e 00 52 34 ad 37 46 00 00 be 42 52 86 7a 45 80 80 80 80 80 80 01 12
mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt:4242:02 03 15 00 06 0e 04 52 34 ad 37 46 00 00 be 42 52 86 7a 45 80 80 80 80 80 80 01 07 08 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt:4254:02 03 15 00 06 0e 04 5c 34 ad 37 46 00 00 be 42 52 86 7a 45 ff 00 00 00 10 00 00 00 00 00 01 ff
mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt:4418:02 03 15 00 06 0e 00 4d 34 ad 37 46 00 00 be 42 52 86 7a 45 80 80 80 80 80 80 01 12 08 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt:8181:02 03 02 00 02 80 80 80 50 7e 00 4b 08 1b 00 01 80 01 7e 00 4b 08 15 00 06 0e 04 57 34 ad 37 46
mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt:8198:82 4a 0b 04 49 03 15 00 06 0e 04 61 34 ad 37 46 00 00 be 42 52 86 7a 45 ff 00 00 00 10 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt:8218:02 03 15 00 06 0e 04 6b 34 ad 37 46 00 00 be 42 52 86 7a 45 ff 00 00 00 10 00 00 00 00 00 01 ff
mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt:8318:02 03 15 00 06 0e 00 9d 34 ad 37 46 00 00 be 42 52 86 7a 45 ff 00 00 00 10 00 00 00 00 00 01 ff
```

### Repeated mode 06 suffix lead

Signature: `ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00` (spaced or compact hex)

Matches: 992

```text
mxopackets1_crashover2k_afterwhoruneo_proxy_003_search_and_loot_corrupted.log.txt:203:01 22 00 00 00 00 00 00 00 12 00 06 02 00 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt:2478:37 46 00 00 be 42 52 86 7a 45 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt:4311:37 46 00 00 be 42 52 86 7a 45 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt:4357:52 86 7a 45 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt:8361:00 be 42 52 86 7a 45 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt:10205:00 00 be 42 52 86 7a 45 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSomethingMaracZombiesDowntown.log.txt:30245:c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSomethingMaracZombiesDowntown.log.txt:32799:44 06 09 0f c7 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSomethingMaracZombiesDowntown.log.txt:32819:0e 04 a7 d7 70 fd c7 00 e0 a1 44 06 09 0f c7 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsSomethingMaracZombiesDowntown.log.txt:32957:70 fd c7 00 e0 a1 44 06 09 0f c7 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00
```

### Movement-state ff 01 64 01 tail

Signature: `ff 01 64 01` (spaced or compact hex)

Matches: 9

```text
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCCyphs.log.txt:8471:e3 67 44 ff 01 64 01 00 00 00 00 00 00 00 40 56 4c f8 40 e9 29 41 00 8b c6 00 00 00 03 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCCyphs.log.txt:69144:b7 45 ff 01 64 01 00 00 00 00 00 00 00 40 56 4c f8 40 e9 29 41 00 8b c6 00 00 00 03 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCCyphs.log.txt:70166:00 80 f7 43 41 99 bc 45 29 00 03 0c 2e 76 c4 aa 46 00 80 f7 43 36 d2 d1 45 ff 01 64 01 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCThenTimeDOut.log.txt:78197:9c 05 00 28 01 01 16 01 03 02 00 ff 01 64 01 00 00 00 00 00 00 58 24 55 86 2a 64 d6 86 ca 03 18
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCThenTimeDOut.log.txt:79156:03 00 08 37 08 00 00 1f fd 07 00 00 00 00 00 00 22 00 00 00 00 00 16 01 03 06 00 40 ff 01 64 01
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCThenTimeDOut.log.txt:79339:46 00 e0 88 44 5e df 6a 45 ff 01 64 01 00 00 00 00 00 00 58 24 55 86 2a 64 d6 86 ca 03 18 87 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCThenTimeDOut.log.txt:96679:02 61 ff 01 64 01 00 00 00 00 00 00 58 24 42 46 2a 50 d6 26 d0 07 18 87 00 00 00 03 9a 99 19 3f
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCThenTimeDOut.log.txt:97065:50 45 ff 01 64 01 00 00 00 00 00 00 58 24 42 46 2a 50 d6 26 d0 07 18 87 00 00 00 03 00 00 80 bf
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCThenTimeDOut.log.txt:104385:08 00 00 46 ff 01 64 01 00 00 00 00 00 00 58 24 42 46 2a 50 d6 26 d0 07 18 87 00 00 00 03 9a 99
```

### Movement-state ff 01 4e 01 tail

Signature: `ff 01 4e 01` (spaced or compact hex)

Matches: 287

```text
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_002_combat_corrupted_spawn_while_jackout_process.log.txt:709:ff 01 4e 01 00 00 00 00 00 00 00 00 00 00 00 08 41 6e 64 65 72 73 6f 6e 06 54 68 6f 6d 61 73 ff
mxopackets1_crashover2k_afterwhoruneo_proxy_003_search_and_loot_corrupted.log.txt:95:ff 01 4e 01 00 00 00 00 00 00 00 00 00 00 00 08 41 6e 64 65 72 73 6f 6e 06 54 68 6f 6d 61 73 ff
mxopackets1_crashover2k_afterwhoruneo_proxy_003_search_and_loot_corrupted.log.txt:116:02 03 02 00 03 08 ae 88 84 c7 00 00 be 42 a9 c0 7d 46 82 80 2b 20 ff 01 4e 01 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_search_and_loot_corrupted.log.txt:174:ff 01 4e 01 00 00 00 00 00 00 00 00 00 00 00 08 41 6e 64 65 72 73 6f 6e 06 54 68 6f 6d 61 73 ff
mxopackets1_crashover2k_afterwhoruneo_proxy_003_search_and_loot_corrupted.log.txt:197:02 03 02 00 03 08 ae 88 84 c7 00 00 be 42 a9 c0 7d 46 76 82 2b 20 ff 01 4e 01 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt:6153:ff 01 4e 01 00 00 00 00 00 00 00 00 00 00 00 08 41 6e 64 65 72 73 6f 6e 06 54 68 6f 6d 61 73 ff
mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt:10519:20 ff 01 4e 01 00 00 00 00 00 00 00 00 00 00 00 08 41 6e 64 65 72 73 6f 6e 06 54 68 6f 6d 61 73
mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt:10553:44 77 1e 3d 20 ff 01 4e 01 00 00 00 00 00 00 00 00 00 00 00 08 41 6e 64 65 72 73 6f 6e 06 54 68
mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt:10586:02 03 02 00 03 0c cb 21 15 85 46 00 80 f7 43 73 5b a9 44 f4 1e 3d 20 ff 01 4e 01 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt:10615:02 03 02 00 03 0c cb 21 15 85 46 00 80 f7 43 73 5b a9 44 70 1f 3d 20 ff 01 4e 01 00 00 00 00 00
```

## Ability, Effect, And Combat-State Leads

### SERVER_PLAYER_ATTRIBUTE / ability load

Signature: `80 b2` (spaced or compact hex)

Matches: 284

```text
mxopackets2_RajkoPacketsNearTheEnd_PacketsLoadingAreaLoopBecauseRecCrashed.log.txt:7:08 80 b2 4e 00 1e 00 08 02 08 80 b2 52 00 1e 00 08 02 08 80 b2 54 00 12 00 08 02 08 80 b2 4f 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsLoadingAreaLoopBecauseRecCrashed.log.txt:8:1e 00 08 02 08 80 b2 51 00 1e 00 08 02 08 80 b2 11 00 32 00 08 02 16 80 bc 45 03 11 00 00 02 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsLoadingAreaLoopBecauseRecCrashed.log.txt:15:00 00 00 00 00 16 80 bc 45 00 0a 00 00 88 00 00 00 76 04 05 00 00 00 00 00 00 00 00 08 80 b2 26
mxopackets2_RajkoPacketsNearTheEnd_PacketsLoadingAreaLoopBecauseRecCrashed.log.txt:17:00 0b 00 00 89 00 00 00 16 04 02 00 00 00 00 00 00 00 00 08 80 b2 24 04 00 00 08 02 16 80 bc 45
mxopackets2_RajkoPacketsNearTheEnd_PacketsLoadingAreaLoopBecauseRecCrashed.log.txt:20:00 08 80 b2 25 04 00 00 08 02 16 80 bc 45 03 25 04 00 9a 00 00 00 25 04 00 00 00 00 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsLoadingAreaLoopBecauseRecCrashed.log.txt:22:01 01 00 00 76 04 01 00 00 00 00 00 00 00 00 08 80 b2 27 04 00 00 08 02 16 80 bc 45 03 27 04 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsLoadingAreaLoopBecauseRecCrashed.log.txt:52:45 00 14 00 00 aa 01 00 00 e5 03 05 00 00 00 00 00 00 00 00 08 80 b2 28 04 00 00 08 02 16 80 bc
mxopackets2_RajkoPacketsNearTheEnd_PacketsLoadingAreaLoopBecauseRecCrashed.log.txt:148:02 04 03 00 5e 0b 16 80 bc 56 00 4c 00 00 0d b9 00 00 b5 00 29 00 00 00 00 00 00 00 00 08 80 b2
mxopackets2_RajkoPacketsNearTheEnd_PacketsLoadingAreaLoopBecauseRecCrashed.log.txt:348:00 00 01 00 00 00 00 08 80 b2 d4 03 00 00 08 02 16 80 bc 46 03 d4 03 00 11 a7 00 00 d4 03 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsLoadingAreaLoopBecauseRecCrashed.log.txt:350:00 00 00 16 80 bc 45 00 02 00 00 02 00 00 00 cc 00 0c 00 00 00 01 00 00 00 00 08 80 b2 11 00 32
```

### SERVER_ABILITY_UNLOAD

Signature: `80 b3` (spaced or compact hex)

Matches: 307

```text
mxopackets2_RajkoPacketsNearTheEnd_PacketsLoadingAreaLoopBecauseRecCrashed.log.txt:347:b5 00 29 00 00 00 00 00 00 00 00 04 80 b3 d4 03 16 80 bc 46 03 d4 03 00 11 a7 00 00 d4 03 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsLoadingAreaLoopBecauseRecCrashed.log.txt:349:00 00 00 00 00 00 00 04 80 b3 11 00 16 80 bc 45 03 11 00 00 02 00 00 00 11 00 32 00 00 00 01 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsLoadingAreaLoopBecauseRecCrashed.log.txt:364:00 04 80 b3 26 04 16 80 bc 45 03 26 04 00 89 00 00 00 26 04 00 00 00 00 01 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsLoadingAreaLoopBecauseRecCrashed.log.txt:395:00 16 80 bc 55 00 8a 00 00 89 00 00 00 16 04 02 00 00 00 00 00 00 00 00 04 80 b3 24 04 16 80 bc
mxopackets2_RajkoPacketsNearTheEnd_PacketsLoadingAreaLoopBecauseRecCrashed.log.txt:399:00 00 16 80 bc 45 00 0d 00 00 9a 00 00 00 16 04 02 00 00 00 01 00 00 00 00 04 80 b3 25 04 16 80
mxopackets2_RajkoPacketsNearTheEnd_PacketsLoadingAreaLoopBecauseRecCrashed.log.txt:403:00 01 00 00 00 00 16 80 bc 45 00 0f 00 00 01 01 00 00 76 04 01 00 00 00 01 00 00 00 00 04 80 b3
mxopackets2_RajkoPacketsNearTheEnd_PacketsLoadingAreaLoopBecauseRecCrashed.log.txt:413:80 b3 28 04 16 80 bc 45 03 28 04 00 aa 01 00 00 28 04 00 00 00 00 01 00 00 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsLoadingAreaLoopBecauseRecCrashed.log.txt:677:00 00 00 68 03 00 00 01 00 00 00 00 04 80 b3 11 00 16 80 bc 45 03 11 00 00 02 00 00 00 11 00 32
mxopackets2_RajkoPacketsNearTheEnd_PacketsLoadingAreaLoopBecauseRecCrashed.log.txt:678:00 00 00 01 00 00 00 00 04 80 b3 d4 03 16 80 bc 46 03 d4 03 00 11 a7 00 00 d4 03 00 00 00 00 01
mxopackets2_RajkoPacketsNearTheEnd_PacketsLoadingAreaLoopBecauseRecCrashed.log.txt:679:00 00 00 00 04 80 b3 24 04 16 80 bc 45 03 24 04 00 8f 00 00 00 24 04 00 00 00 00 01 00 00 00 00
```

### Object599 adrenaline booster effect id

Signature: `4c 0a 00 28` (spaced or compact hex)

Matches: 1246

```text
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_002_combat_corrupted_spawn_while_jackout_process.log.txt:574:00 00 00 00 00 00 00 00 00 00 00 00 00 10 00 00 22 c5 01 c4 01 80 d3 7a 05 00 58 4c 0a 00 28 0a
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_002_combat_corrupted_spawn_while_jackout_process.log.txt:1006:00 4c 0a 00 28 ff 02 00 00 00 00 00 00 00 00 00 00 00 e8 08 00 58 37 08 00 00 1f 07 08 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_002_combat_corrupted_spawn_while_jackout_process.log.txt:1026:00 4c 0a 00 28 ff 02 00 00 00 00 00 00 00 00 00 00 00 e8 08 00 58 37 08 00 00 1f 07 08 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_002_combat_corrupted_spawn_while_jackout_process.log.txt:1046:4c 0a 00 28 ff 02 00 00 00 00 00 00 00 00 00 00 00 e8 08 00 58 37 08 00 00 1f 07 08 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_002_combat_zombie_lurch.log.txt:478:00 00 ec 1a 00 00 4c 0a 00 28 ff 01 00 00 00 00 00 00 00 00 00 00 00 9b 00 00 4e 37 08 00 00 1f
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_002_combat_zombie_lurch.log.txt:595:4c 0a 00 28 ff 01 00 00 00 00 00 00 00 00 00 00 00 9b 00 00 4e 37 08 00 00 1f 12 08 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_002_combat_zombie_lurch.log.txt:626:8a 02 ff 00 00 00 00 00 00 00 00 ec 1a 00 00 4c 0a 00 28 ff 01 00 00 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_002_combat_zombie_lurch.log.txt:656:8a 02 ff 00 00 00 00 00 00 00 00 ec 1a 00 00 4c 0a 00 28 ff 01 00 00 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_002_combat_zombie_lurch.log.txt:686:8a 02 ff 00 00 00 00 00 00 00 00 ec 1a 00 00 4c 0a 00 28 ff 01 00 00 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_second_pc_002_combat_zombie_lurch.log.txt:716:8a 02 ff 00 00 00 00 00 00 00 00 ec 1a 00 00 4c 0a 00 28 ff 01 00 00 00 00 00 00 00 00 00 00 00
```

### Object599 fast healing effect id

Signature: `b5 07 00 28` (spaced or compact hex)

Matches: 0

### Object599 accelerated spawn effect id

Signature: `66 00 00 04` (spaced or compact hex)

Matches: 675

```text
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCThenTimeDOut.log.txt:425:40 e3 ff 63 22 66 00 00 04 b0 01 00 14 00 c9 75 4c 51 07 28 00 1c 03 f0 03 00 00 eb ff a1 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCThenTimeDOut.log.txt:428:d4 04 00 58 66 00 00 04 01 c1 00 00 00 c0 6e 26 d0 40 00 00 00 00 00 f0 7e 40 00 00 00 a0 13 d5
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCThenTimeDOut.log.txt:479:ca 61 d0 40 00 00 00 00 00 f0 7e 40 00 00 00 80 0b 99 a9 40 7f 00 63 12 66 00 00 04 b0 01 00 04
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCThenTimeDOut.log.txt:692:40 00 00 00 80 de 1a a9 40 81 ff 63 12 fb ff 66 00 00 04 90 41 00 89 82 4c 64 07 13 1c 03 ee 03
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCThenTimeDOut.log.txt:739:00 00 00 00 f0 7e 40 00 00 00 20 fe c1 a8 40 05 00 63 12 fb ff 66 00 00 04 90 01 00 c9 43 30 4c
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCThenTimeDOut.log.txt:902:40 81 ff 63 22 fb ff 66 00 00 04 90 01 00 c9 97 92 40 07 95 00 1c 01 11 00 00 00 55 00 8d 00 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCThenTimeDOut.log.txt:955:d1 40 00 00 00 00 00 f0 7e 40 00 00 00 00 00 70 ac 40 81 ff 63 12 fb ff 66 00 00 04 90 01 00 8b
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCThenTimeDOut.log.txt:1063:e1 c0 d0 40 00 00 00 00 00 f0 7e 40 00 00 00 00 eb 2d a2 40 fb ff 63 12 fb ff 66 00 00 04 90 01
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCThenTimeDOut.log.txt:1126:f0 7e 40 00 00 00 20 54 cf a8 40 81 ff 63 22 fb ff 66 00 00 04 80 89 08 b6 5e 07 18 1c 01 11 00
mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCThenTimeDOut.log.txt:1334:aa 40 0b 00 ff 22 64 00 66 00 00 04 80 89 c4 29 be 05 c7 1d 6a 9e 00 00 07 81 00 00 00 08 00 77
```

## Intake Next Steps

- Preserve the original files separately before editing or importing anything.
- Inspect matches with surrounding lines; embedded hits inside encrypted or unrelated bodies are leads only.
- Record sender, provenance, CR1/CR2 status, action context, character handle, coordinates, known object ids, and known item GoIDs.
- Import only text logs or text-like dumps that contain no passwords, session tokens, private keys, client binaries, or unrelated game assets.
