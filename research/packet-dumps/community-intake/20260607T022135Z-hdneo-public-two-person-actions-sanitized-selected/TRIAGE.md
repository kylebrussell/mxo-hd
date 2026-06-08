# Packet Capture Triage Report

- Source kind: `directory`
- Source input: `/Users/kyle/Developer/mxo-hd/research/packet-dumps/community-intake/20260607T022135Z-hdneo-public-two-person-actions-sanitized-selected/logs`
- Scanned files from: `/Users/kyle/Developer/mxo-hd/research/packet-dumps/community-intake/20260607T022135Z-hdneo-public-two-person-actions-sanitized-selected/logs`
- Generated UTC: `2026-06-07T02:21:36Z`
- Max sample lines per pattern: `10`

Use this report to decide whether received logs are worth importing into `research/packet-dumps/`. Matches are byte-string leads, not decoded packet claims.

## Vendor And Interaction Leads

### SERVER_VENDOR_OPEN candidate

Signature: `81 0d` (spaced or compact hex)

Matches: 5

```text
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:12395:1e ed 40 c0 01 83 2c 03 a8 42 a4 06 b2 09 a4 06 06 08 00 00 81 0d ba 00 00 02 10 0c 00 00 2d 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:47692:82 32 03 8f 47 03 18 00 02 0c 59 33 3b ac 46 00 40 30 c4 81 0d 66 47 15 00 06 0c e1 2c 37 ae 46
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:76283:82 03 a8 42 00 00 b2 09 a4 06 fd 07 00 00 81 0d ba 00 00 02 10 1a 00 00 27 00 02 0e 01 ce cc 9d
mxopackets1_crashover2k_afterwhoruneo_proxy_020_afterwhoruneo_some_2_person_actions_with_mxoemu_and_mission_and_crew_things.log.txt:36228:1e ed 40 c0 01 83 2c 03 a8 42 a4 06 b2 09 a4 06 06 08 00 00 81 0d ba 00 00 02 10 05 00 00 25 00
mxopackets1_crashover2k_afterwhoruneo_proxy_020_afterwhoruneo_some_2_person_actions_with_mxoemu_and_mission_and_crew_things.log.txt:102798:09 a4 06 fd 07 00 00 81 0d ba 00 00 02 10 29 00 00 01 00 0c 57 02 a9 cd ab 16 8e 43 6f 72 72 75
```

### CLIENT_VENDOR_BUY candidate

Signature: `81 0e` (spaced or compact hex)

Matches: 1

```text
mxopackets1_crashover2k_afterwhoruneo_proxy_020_afterwhoruneo_some_2_person_actions_with_mxoemu_and_mission_and_crew_things.log.txt:13066:02 03 25 00 02 08 05 9b e9 45 00 40 30 c4 2e 1f 57 47 19 00 02 0c 39 eb 81 0e 46 00 40 30 c4 d1
```

### CLIENT_VENDOR_SELL candidate

Signature: `81 11` (spaced or compact hex)

Matches: 5

```text
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:109103:02 03 27 00 02 08 81 11 58 46 00 40 30 c4 33 ab 6f 47 12 00 02 0c 1d 3d 56 60 46 00 40 30 c4 76
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:111137:02 03 29 00 02 0e 01 6d 84 47 f3 45 00 40 30 c4 81 11 5a 47 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_020_afterwhoruneo_some_2_person_actions_with_mxoemu_and_mission_and_crew_things.log.txt:26025:02 0e 01 42 f2 81 11 46 00 40 30 c4 8d 3a 6b 47 13 00 02 0e 04 ed d3 59 58 46 00 40 30 c4 cc cc
mxopackets1_crashover2k_afterwhoruneo_proxy_020_afterwhoruneo_some_2_person_actions_with_mxoemu_and_mission_and_crew_things.log.txt:136939:02 03 23 00 02 08 81 11 58 46 00 40 30 c4 33 ab 6f 47 07 00 02 0c 1d 3d 56 60 46 00 40 30 c4 76
mxopackets1_crashover2k_afterwhoruneo_proxy_020_afterwhoruneo_some_2_person_actions_with_mxoemu_and_mission_and_crew_things.log.txt:139048:02 03 1c 00 02 0e 01 6d 84 47 f3 45 00 40 30 c4 81 11 5a 47 00 00
```

### CR2 static object interaction

Signature: `80 c8` (spaced or compact hex)

Matches: 4

```text
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:1310:02 03 02 00 01 04 11 00 00 04 01 00 27 01 08 80 c8 1d 00 20 10 03 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:2226:02 04 01 00 29 01 08 80 c8 f7 00 20 10 00 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:2453:02 04 01 00 2c 01 08 80 c8 05 01 20 10 03 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:2577:02 03 02 00 01 08 48 95 64 45 00 00 be 42 4f 39 49 46 00 00 04 01 00 2e 01 08 80 c8 03 00 20 10
```

### CR1 static object interaction

Signature: `80 c3` (spaced or compact hex)

Matches: 1

```text
mxopackets1_crashover2k_afterwhoruneo_proxy_020_afterwhoruneo_some_2_person_actions_with_mxoemu_and_mission_and_crew_things.log.txt:90361:19 00 02 0e 04 0b da c0 ac 46 00 40 30 c4 62 a7 5c 47 13 00 02 0e 01 80 c3 23 5f 46 00 40 30 c4
```

### SERVER object/state bundle

Signature: `80 bc` (spaced or compact hex)

Matches: 1814

```text
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:12:80 b2 11 00 01 00 08 02 16 80 bc 45 03 11 00 00 02 00 00 00 11 00 01 00 00 00 00 00 00 00 00 16
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:13:80 bc 45 00 02 00 00 02 00 00 00 cc 00 00 00 00 00 00 00 00 00 00 16 80 bc 15 00 03 00 00 f7 03
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:14:00 00 08 02 00 00 00 00 00 00 00 00 00 16 80 bc 15 00 04 00 00 f7 03 00 00 07 02 ec ff ff ff 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:15:00 00 00 00 16 80 bc 15 00 05 00 00 f7 03 00 00 50 04 00 00 00 00 00 00 00 00 00 16 80 bc 15 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:16:06 00 00 f7 03 00 00 f4 03 00 00 00 00 00 00 00 00 00 16 80 bc 15 00 07 00 00 f7 03 00 00 51 04
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:17:f6 ff ff ff 00 00 00 00 00 16 80 bc 15 00 08 00 00 f7 03 00 00 52 04 0f 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:56:01 00 08 02 16 80 bc 45 03 11 00 00 02 00 00 00 11 00 01 00 00 00 00 00 00 00 00 16 80 bc 45 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:57:02 00 00 02 00 00 00 cc 00 00 00 00 00 00 00 00 00 00 16 80 bc 15 00 03 00 00 f7 03 00 00 08 02
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:58:00 00 00 00 00 00 00 00 00 16 80 bc 15 00 04 00 00 f7 03 00 00 07 02 ec ff ff ff 00 00 00 00 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:59:16 80 bc 15 00 05 00 00 f7 03 00 00 50 04 00 00 00 00 00 00 00 00 00 16 80 bc 15 00 06 00 00 f7
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

Matches: 2

```text
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:6053:00 01 00 ff 00 00 00 00 c5 e1 00 00 00 00 00 00 22 00 00 00 00 00 00 01 00 04 00 02 0c 12 20 06
mxopackets1_crashover2k_afterwhoruneo_proxy_020_afterwhoruneo_some_2_person_actions_with_mxoemu_and_mission_and_crew_things.log.txt:14962:de 0d 00 49 33 00 00 ff 00 00 00 00 00 21 0a 00 28 07 00 00 00 00 00 00 00 00 01 00 ff 9a 0b 00
```

### Selector ff continuation prefix 00 01 04 ff

Signature: `00 01 04 ff` (spaced or compact hex)

Matches: 0

### Selector ff continuation prefix 00 01 10 ff

Signature: `00 01 10 ff` (spaced or compact hex)

Matches: 29

```text
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:3445:00 00 00 00 01 10 ff 9a 0b 00 58 4b 62 01 00 00 00 08 01 12 00 00 00 00 00 00 01 00 04 00 02 0e
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:3465:00 00 00 00 00 01 10 ff 9a 0b 00 58 4b 62 01 00 00 00 08 01 12 00 00 00 00 00 00 01 00 0c 00 06
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:3490:00 00 00 00 00 01 10 ff 9a 0b 00 58 4b 62 01 00 00 00 08 01 12 00 00 00 00 00 00 01 00 00 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:3508:08 00 00 00 00 00 00 00 00 01 10 ff 9a 0b 00 58 4b 62 01 00 00 00 08 01 12 00 00 00 00 00 00 01
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:3528:00 00 00 00 01 10 ff 9a 0b 00 58 4b 62 01 00 00 00 08 01 12 00 00 00 00 00 00 01 00 00 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:3546:00 00 00 00 01 10 ff 9a 0b 00 58 4b 62 01 00 00 00 08 01 12 00 00 00 00 00 00 01 00 00 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:3564:00 00 00 00 01 10 ff 9a 0b 00 58 4b 62 01 00 00 00 08 01 12 00 00 00 00 00 00 01 00 00 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:3583:00 00 00 de 0d 00 49 33 00 00 ff ff 00 00 00 00 56 06 00 28 08 00 00 00 00 00 00 00 00 01 10 ff
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:3606:00 00 00 00 00 01 10 ff 9a 0b 00 58 4b 62 01 00 00 00 08 01 12 00 00 00 00 00 00 01 00 00 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:3986:00 00 00 00 00 01 10 ff 9a 0b 00 58 4b 62 01 00 00 00 08 01 12 00 00 00 00 00 00 01 00 00 00
```

### Selector ff continuation prefix 00 00 00 ff

Signature: `00 00 00 ff` (spaced or compact hex)

Matches: 8342

```text
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:2263:10 2e cd ab 03 88 00 00 00 00 ff ff 7f 3f 00 00 00 00 f3 04 35 33 22 00 00 00 00 00 70 97 40 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:2433:02 03 09 00 07 00 27 00 40 00 00 00 10 00 15 00 80 ff 00 00 00 10 00 00 00 00 00 00 ff 00 00 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:2435:00 96 00 00 00 00 00 04 00 00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:3442:86 28 64 10 82 c1 02 00 00 c0 00 00 01 29 5c 0f 3f 00 00 00 00 ff 00 00 00 00 de 0d 45 00 00 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:3443:00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ff
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:3462:16 86 28 64 10 82 c1 02 00 00 c0 00 00 01 29 5c 0f 3f 00 00 00 00 ff 00 00 00 00 de 0d 45 00 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:3487:16 86 28 64 10 82 c1 02 00 00 c0 00 00 01 29 5c 0f 3f 00 00 00 00 ff 00 00 00 00 de 0d 45 00 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:3505:00 00 16 08 16 86 28 64 10 82 c1 02 00 00 c0 00 00 01 29 5c 0f 3f 00 00 00 00 ff 00 00 00 00 de
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:3507:00 00 00 00 ff 00 00 00 00 00 00 00 00 00 00 de 0d 00 49 33 00 00 ff ff 00 00 00 00 56 06 00 28
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:3525:86 28 64 10 82 c1 02 00 00 c0 00 00 01 29 5c 0f 3f 00 00 00 00 ff 00 00 00 00 de 0d 45 00 00 00
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

Matches: 317

```text
mxopackets1_crashover2k_afterwhoruneo_proxy_020_afterwhoruneo_some_2_person_actions_with_mxoemu_and_mission_and_crew_things.log.txt:680:02 03 02 00 02 80 80 80 80 80 90 11 00 00 00 01 00 00 00 01 01 00 0c 57 02 99 cd ab 13 8e 43 72
mxopackets1_crashover2k_afterwhoruneo_proxy_020_afterwhoruneo_some_2_person_actions_with_mxoemu_and_mission_and_crew_things.log.txt:689:08 00 00 81 d3 1e 00 00 02 07 25 00 00 01 00 0c 57 02 9b cd ab 11 8e 43 72 6f 77 20 42 61 72 73
mxopackets1_crashover2k_afterwhoruneo_proxy_020_afterwhoruneo_some_2_person_actions_with_mxoemu_and_mission_and_crew_things.log.txt:735:0c 24 37 9b 27 c7 00 00 be 42 48 48 2e c5 01 00 0c 57 02 9c cd ab 11 8e 43 72 6f 77 20 42 61 72
mxopackets1_crashover2k_afterwhoruneo_proxy_020_afterwhoruneo_some_2_person_actions_with_mxoemu_and_mission_and_crew_things.log.txt:742:c1 1e 00 00 02 07 1f 00 00 01 00 0c 57 02 9e cd ab 11 8e 43 72 6f 77 20 42 61 72 73 20 42 72 61
mxopackets1_crashover2k_afterwhoruneo_proxy_020_afterwhoruneo_some_2_person_actions_with_mxoemu_and_mission_and_crew_things.log.txt:745:02 a8 1e b0 04 b2 09 b0 04 07 08 00 00 81 c1 09 00 00 02 07 1d 00 00 01 00 0c 57 02 9f cd ab 11
mxopackets1_crashover2k_afterwhoruneo_proxy_020_afterwhoruneo_some_2_person_actions_with_mxoemu_and_mission_and_crew_things.log.txt:785:be 42 00 8b d9 43 01 00 0c 57 02 a0 cd ab 13 8e 43 72 6f 77 20 42 61 72 73 20 48 61 74 63 68 6c
mxopackets1_crashover2k_afterwhoruneo_proxy_020_afterwhoruneo_some_2_person_actions_with_mxoemu_and_mission_and_crew_things.log.txt:788:a8 1e 20 03 b2 09 20 03 07 08 00 00 81 c1 1e 00 00 02 07 19 00 00 01 00 0c 57 02 a1 cd ab 11 8e
mxopackets1_crashover2k_afterwhoruneo_proxy_020_afterwhoruneo_some_2_person_actions_with_mxoemu_and_mission_and_crew_things.log.txt:792:07 17 00 00 01 00 0c 57 02 a2 cd ab 13 8e 43 72 6f 77 20 42 61 72 73 20 42 72 61 67 67 61 72 74
mxopackets1_crashover2k_afterwhoruneo_proxy_020_afterwhoruneo_some_2_person_actions_with_mxoemu_and_mission_and_crew_things.log.txt:843:02 0e 00 a2 90 4c 08 c7 00 00 be 42 00 8b d9 43 01 00 0c 57 02 a3 cd ab 13 8e 43 72 6f 77 20 42
mxopackets1_crashover2k_afterwhoruneo_proxy_020_afterwhoruneo_some_2_person_actions_with_mxoemu_and_mission_and_crew_things.log.txt:847:01 00 0c 57 02 a4 cd ab 13 8e 43 72 6f 77 20 42 61 72 73 20 48 61 74 63 68 6c 69 6e 67 00 00 00
```

### Adjacent Object599 boundary prefix

Signature: `00 0c 57 02` (spaced or compact hex)

Matches: 316

```text
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:1235:01 00 0c 57 02 ad cd ab 11 8e 42 6c 61 63 6b 77 6f 6f 64 20 47 6f 6f 66 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:1375:02 03 01 00 0c 57 02 b5 cd ab 12 8e 42 6c 61 63 6b 77 6f 6f 64 20 4d 75 73 68 66 61 6b 65 00 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:1492:02 03 01 00 0c 57 02 af cd ab 0f 8e 42 6c 61 63 6b 77 6f 6f 64 20 4d 75 73 68 66 61 6b 65 00 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:2731:02 03 01 00 01 05 00 03 00 05 00 07 00 08 00 0b 00 01 00 0c 57 02 af cd ab 0f 8e 42 6c 61 63 6b
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:4379:02 03 01 00 0c 57 02 b2 cd ab 0f 8e 43 68 6f 70 70 65 72 20 53 6b 69 64 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:4668:02 03 01 00 0c 57 02 bb cd ab 12 8e 42 6c 61 63 6b 77 6f 6f 64 20 47 6f 6f 66 00 00 00 00 00 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:5763:02 03 01 00 0c 57 02 8e cd ab 13 8e 42 6c 61 63 6b 77 6f 6f 64 20 47 6f 6f 66 00 00 00 00 00 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:6158:02 03 01 00 0c 57 02 b3 cd ab 12 8e 43 68 6f 70 70 65 72 20 52 75 6e 6e 65 72 00 00 00 00 00 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:6645:82 d5 61 8e 47 03 01 00 0c 57 02 91 cd ab 13 8e 42 6c 61 63 6b 77 6f 6f 64 20 47 6f 6f 66 00 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:6648:10 a8 04 96 00 b2 09 96 00 38 21 00 00 80 02 01 0d 00 00 01 00 0c 57 02 92 cd ab 11 8e 42 6c 61
```

### Header-like selector 50

Signature: `01 00 08 50` (spaced or compact hex)

Matches: 2

```text
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:2690:01 00 08 50 02 88 06 e0 4b 9a cd ab 02 09 00 00 00 00 00 dc be 40 00 00 00 00 00 40 60 40 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_020_afterwhoruneo_some_2_person_actions_with_mxoemu_and_mission_and_crew_things.log.txt:684:07 29 00 00 01 00 08 50 02 a0 0a a0 1c e1 cd ab 02 09 00 00 00 00 40 02 e3 c0 00 00 00 00 00 40
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

Matches: 1

```text
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:2262:40 00 00 00 00 00 88 8d 40 00 00 00 00 00 64 c9 40 ff ff ff ff 0f 00 00 01 00 08 a3 01 0e 01 20
```

### Header-like selector d0

Signature: `01 00 08 d0` (spaced or compact hex)

Matches: 0

## Movement And Object-State Leads

### Mode 06 movement selector 08

Signature: `00 06 08` (spaced or compact hex)

Matches: 9

```text
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:4996:02 03 18 00 01 0c 37 5c c9 48 46 00 a0 87 44 4b f9 80 46 0d 00 06 08 6c 07 1c 45 00 f0 1b 45 4c
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:22116:64 84 4f 46 00 40 30 c4 9e a6 72 47 0a 00 06 08 65 bf 41 46 00 40 30 c4 00 e9 68 47 80 80 80 c0
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:22608:c4 47 6b 69 47 0b 00 02 0c 15 21 64 54 46 00 40 30 c4 ea e6 74 47 0a 00 06 08 65 bf 41 46 00 40
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:42368:01 4b 00 b2 09 4b 00 06 08 00 00 01 b4 02 00 00 0d 00 00 01 00 0c 57 02 22 cd ab 0f 8e 43 6f 72
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:42371:00 f8 b3 20 ec 40 c0 01 83 2c 01 a8 01 96 00 32 04 96 00 06 08 00 00 27 00 00 1f 00 02 0c 41 20
mxopackets1_crashover2k_afterwhoruneo_proxy_020_afterwhoruneo_some_2_person_actions_with_mxoemu_and_mission_and_crew_things.log.txt:1583:07 08 00 00 00 00 00 00 22 00 00 00 00 00 0f 00 06 08 76 0b 26 c7 00 00 be 42 72 c9 2e c5 ff 00
mxopackets1_crashover2k_afterwhoruneo_proxy_020_afterwhoruneo_some_2_person_actions_with_mxoemu_and_mission_and_crew_things.log.txt:47921:40 30 c4 47 6b 69 47 04 00 06 08 65 bf 41 46 00 40 30 c4 00 e9 68 47 ff 00 00 00 10 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_020_afterwhoruneo_some_2_person_actions_with_mxoemu_and_mission_and_crew_things.log.txt:68636:01 4b 00 b2 09 4b 00 06 08 00 00 01 b4 02 00 00 25 00 00 01 00 0c 57 02 22 cd ab 0f 8e 43 6f 72
mxopackets1_crashover2k_afterwhoruneo_proxy_020_afterwhoruneo_some_2_person_actions_with_mxoemu_and_mission_and_crew_things.log.txt:68639:00 f8 b3 20 ec 40 c0 01 83 2c 01 a8 01 96 00 32 04 96 00 06 08 00 00 04 00 00 21 00 02 02 00 1c
```

### Mode 06 movement selector 0a

Signature: `00 06 0a` (spaced or compact hex)

Matches: 110

```text
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:7677:02 03 18 00 06 0a 00 80 92 2c 46 00 f0 66 45 c8 57 07 46 80 80 80 80 80 80 01 40 21 00 00 00 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:7689:02 03 18 00 06 0a 00 80 92 2c 46 00 f0 66 45 c8 57 07 46 80 80 80 80 80 80 01 f9 07 00 00 00 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:8928:c4 32 20 6d 47 09 00 06 0a 00 ce 0b 86 46 00 40 30 c4 40 3a 6c 47 80 80 80 80 80 80 01 39 21 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:11427:02 03 2b 00 02 0e 00 69 58 39 eb 45 00 40 30 c4 dd 29 59 47 25 00 06 0a 00 07 0b 6e 46 00 40 30
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:12311:82 0b 82 8e 47 03 0b 00 02 0c a6 b6 b2 5b 46 00 40 30 c4 b3 3a 74 47 09 00 06 0a 00 32 7e 26 46
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:12324:ce 2b 74 47 09 00 06 0a 00 32 7e 26 46 00 40 30 c4 19 70 7f 47 80 80 80 80 80 80 01 47 21 00 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:13155:47 0b 00 06 0a 00 b4 a9 4e 46 00 40 30 c4 3f bf 71 47 80 80 80 80 80 80 01 39 21 00 00 00 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:25187:c8 8c 7b 47 09 00 06 0a 00 0c 17 6e 46 00 40 30 c4 3a 2f 7c 47 80 80 80 80 80 80 01 39 21 00 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:26086:47 0f 00 02 80 80 10 10 06 0b 00 06 0a 01 03 aa 67 46 00 40 30 c4 de 9a 78 47 80 80 80 80 80 80
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:26682:00 02 0c 25 73 b9 61 46 00 40 30 c4 cc a2 7e 47 0b 00 06 0a 00 8c 3f 61 46 00 40 30 c4 cc ed 75
```

### Mode 06 movement selector 0c

Signature: `00 06 0c` (spaced or compact hex)

Matches: 2126

```text
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:3770:02 03 18 00 01 02 00 04 00 06 0c 64 1c d2 07 45 00 00 be 42 d2 52 81 46 80 80 80 80 80 80 01 07
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:3782:02 03 04 00 06 0c 5a 1c d2 07 45 00 00 be 42 d2 52 81 46 ff 00 00 00 10 00 00 00 00 00 01 ff 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:3802:82 42 3e 8e 47 03 04 00 06 0c 50 1c d2 07 45 00 00 be 42 d2 52 81 46 ff 00 00 00 10 00 00 00 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:3827:02 03 04 00 06 0c 46 1c d2 07 45 00 00 be 42 d2 52 81 46 ff 00 00 00 10 00 00 00 00 00 01 ff 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:3847:02 03 04 00 06 0c 3c 1c d2 07 45 00 00 be 42 d2 52 81 46 ff 00 00 00 10 00 00 00 00 00 01 ff 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:3867:02 03 04 00 06 0c 32 1c d2 07 45 00 00 be 42 d2 52 81 46 ff 00 00 00 10 00 00 00 00 00 01 ff 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:3932:02 03 04 00 06 0c 15 70 5a 08 45 00 00 be 42 85 61 81 46 ff 00 00 00 10 00 00 00 00 00 01 ff 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:4566:02 03 0f 00 06 0c 44 c9 a2 9d 45 00 40 30 c4 d8 7e 98 46 ff 00 00 00 10 00 00 00 00 00 01 ff 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:4628:02 03 0f 00 06 0c 2e c9 a2 9d 45 00 40 30 c4 d8 7e 98 46 ff 00 00 00 10 00 00 00 00 00 01 ff 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:6374:02 03 1c 00 06 0c 9e a3 85 97 45 00 40 30 c4 2a ae 98 46 ff 00 00 00 10 00 00 00 00 00 01 ff 00
```

### Mode 06 movement selector 0e

Signature: `00 06 0e` (spaced or compact hex)

Matches: 2490

```text
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:3667:02 03 18 00 01 0c 00 30 ad bf 45 00 00 be 42 60 a5 69 46 04 00 06 0e 05 69 1c d2 07 45 00 00 be
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:3887:02 03 04 00 06 0e 01 28 1c d2 07 45 00 00 be 42 d2 52 81 46 ff 00 00 00 10 00 00 00 00 00 01 ff
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:3907:02 03 04 00 06 0e 01 1e 70 5a 08 45 00 00 be 42 85 61 81 46 ff 00 00 00 10 00 00 00 00 00 01 ff
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:4262:02 03 04 00 06 0e 00 46 74 7e 14 45 00 00 be 42 cf 00 84 46 80 80 80 80 80 80 01 12 08 00 00 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:4274:02 03 18 00 01 0c 4f e2 70 c8 45 00 c0 5f 44 5d 9d 7b 46 04 00 06 0e 00 46 74 7e 14 45 00 00 be
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:4432:82 c4 40 8e 47 03 04 00 06 0e 01 59 0c 11 15 45 00 00 be 42 e7 f3 83 46 ff 00 00 00 10 00 00 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:4452:02 03 0f 00 02 2c 00 40 00 00 00 10 63 c9 a2 9d 45 00 40 30 c4 d8 7e 98 46 04 00 06 0e 01 5a 0c
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:4526:02 03 0f 00 06 0e 05 59 c9 a2 9d 45 00 40 30 c4 d8 7e 98 46 ff 00 00 00 10 00 00 00 00 00 01 ff
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:4546:82 54 41 8e 47 03 0f 00 06 0e 05 4e c9 a2 9d 45 00 40 30 c4 d8 7e 98 46 ff 00 00 00 10 00 00 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:4586:02 03 01 00 01 01 00 15 00 18 00 01 0e 3e 40 ac 1a 15 46 88 28 83 44 d6 ca 7e 46 0f 00 06 0e 00
```

### Repeated mode 06 suffix lead

Signature: `ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00` (spaced or compact hex)

Matches: 1611

```text
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:4275:42 cf 00 84 46 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:4310:45 00 00 be 42 cf 00 84 46 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:4453:11 15 45 00 00 be 42 e7 f3 83 46 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:4587:3a c9 a2 9d 45 00 40 30 c4 d8 7e 98 46 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:4609:30 c4 d8 7e 98 46 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:8954:30 c4 40 3a 6c 47 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:8975:c4 40 3a 6c 47 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:9039:74 3a 17 86 46 00 40 30 c4 24 19 6c 47 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:12367:30 c4 19 70 7f 47 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:12398:7f 47 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
```

### Movement-state ff 01 64 01 tail

Signature: `ff 01 64 01` (spaced or compact hex)

Matches: 1

```text
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:6049:02 03 1c 00 03 27 03 c0 00 98 d9 00 00 00 00 32 ff 01 64 01 00 00 00 00 00 00 d0 95 03 94 81 b5
```

### Movement-state ff 01 4e 01 tail

Signature: `ff 01 4e 01` (spaced or compact hex)

Matches: 126

```text
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:20782:02 03 0f 00 03 1e 00 10 0f 03 0d 00 00 00 25 b1 8a 39 46 00 40 30 c4 93 d3 68 47 ff 01 4e 01 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:21678:47 ff 01 4e 01 00 00 00 00 00 00 00 00 23 1a e9 90 80 b0 04 02 02 8e 00 00 00 01 00 00 00 00 69
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:21700:0a 00 a9 00 b1 8a 39 46 00 40 30 c4 93 d3 68 47 ff 01 4e 01 00 00 00 00 00 00 00 00 23 1a e9 90
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:22142:ff 01 4e 01 00 00 00 00 00 00 00 00 23 1a e9 90 80 b0 04 02 02 8e 00 00 00 01 00 00 00 00 69 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:23760:02 03 2d 00 02 08 ec e0 06 46 00 40 30 c4 a3 f2 54 47 0f 00 03 02 00 ff 01 4e 01 00 00 00 00 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:24055:68 47 ff 01 4e 01 00 00 00 00 00 00 00 00 23 1a e9 90 80 b0 04 02 02 8e 00 00 00 01 00 00 00 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:24203:00 00 00 00 00 22 00 00 00 00 00 0f 00 03 0c 57 6e b2 3c 46 00 40 30 c4 15 09 68 47 ff 01 4e 01
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:24231:46 00 40 30 c4 91 79 6f 47 0f 00 03 0c 51 59 bd 3d 46 00 40 30 c4 88 e8 67 47 ff 01 4e 01 00 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:24256:70 6f 47 0f 00 03 0c 43 07 e7 3e 46 00 40 30 c4 93 dc 67 47 ff 01 4e 01 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:24287:03 0e 3e 43 55 2f 3f 46 00 40 30 c4 db e7 67 47 ff 01 4e 01 00 00 00 00 00 00 00 00 23 1a e9 90
```

## Ability, Effect, And Combat-State Leads

### SERVER_PLAYER_ATTRIBUTE / ability load

Signature: `80 b2` (spaced or compact hex)

Matches: 65

```text
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:10:0f 00 00 00 00 00 00 0a 80 e4 9e 57 00 00 00 00 00 00 08 80 b2 4e 00 08 00 08 02 08 80 b2 52 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:11:0b 00 08 02 08 80 b2 54 00 05 00 08 02 08 80 b2 4f 00 08 00 08 02 08 80 b2 51 00 08 00 08 02 08
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:12:80 b2 11 00 01 00 08 02 16 80 bc 45 03 11 00 00 02 00 00 00 11 00 01 00 00 00 00 00 00 00 00 16
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:54:00 00 00 0a 80 e4 9e 57 00 00 00 00 00 00 08 80 b2 4e 00 08 00 08 02 08 80 b2 52 00 0b 00 08 02
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:55:08 80 b2 54 00 05 00 08 02 08 80 b2 4f 00 08 00 08 02 08 80 b2 51 00 08 00 08 02 08 80 b2 11 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:98:00 00 00 0a 80 e4 9e 57 00 00 00 00 00 00 08 80 b2 4e 00 08 00 08 02 08 80 b2 52 00 0b 00 08 02
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:99:08 80 b2 54 00 05 00 08 02 08 80 b2 4f 00 08 00 08 02 08 80 b2 51 00 08 00 08 02 08 80 b2 11 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:164:02 04 01 00 03 09 08 80 b2 4e 00 08 00 08 02 08 80 b2 52 00 0b 00 08 02 08 80 b2 54 00 05 00 08
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:165:02 08 80 b2 4f 00 08 00 08 02 08 80 b2 51 00 08 00 08 02 08 80 b2 11 00 01 00 08 02 16 80 bc 45
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:1171:00 00 00 00 01 00 00 00 00 08 80 b2 11 00 01 00 08 02 16 80 bc 45 03 11 00 00 02 00 00 00 11 00
```

### SERVER_ABILITY_UNLOAD

Signature: `80 b3` (spaced or compact hex)

Matches: 37

```text
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:1169:02 03 02 00 02 80 80 80 80 80 10 11 00 00 00 00 00 04 01 00 1d 18 04 80 b3 11 00 16 80 bc 45 03
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:8099:05 11 00 00 00 00 00 00 00 04 80 b3 11 00 16 80 bc 45 03 11 00 00 02 00 00 00 11 00 01 00 00 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:8331:15 00 3b 00 00 11 00 00 00 f3 03 00 00 00 00 01 00 00 00 00 04 80 b3 11 00 16 80 bc 45 03 11 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:48878:83 80 b3 46 00 40 30 c4 bd cd 63 47 15 00 02 02 00 03 00 02 0c 97 55 94 ac 46 00 40 30 c4 80 7c
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:52321:00 00 00 00 c2 a0 40 00 00 00 00 80 b3 e2 40 01 00 00 00 a0 0a 30 49 23 80 9e 01 00 00 00 00 00
mxopackets1_crashover2k_020_mxoemu_2_person_actions_with_afterwhoruneo.log.txt:52322:6a e8 40 00 00 00 00 00 c2 a0 40 00 00 00 00 80 b3 e2 40 01 00 00 00 f8 0a 30 49 23 80 a0 00 06
mxopackets1_crashover2k_afterwhoruneo_proxy_020_afterwhoruneo_some_2_person_actions_with_mxoemu_and_mission_and_crew_things.log.txt:1020:00 00 9d 00 1f 00 00 00 00 00 00 00 00 04 80 b3 11 00 16 80 bc 45 03 11 00 00 02 00 00 00 11 00
mxopackets1_crashover2k_afterwhoruneo_proxy_020_afterwhoruneo_some_2_person_actions_with_mxoemu_and_mission_and_crew_things.log.txt:1064:76 00 00 34 00 00 00 83 02 c8 00 00 00 00 00 00 00 00 04 80 b3 36 04 16 80 bc 45 03 36 04 00 40
mxopackets1_crashover2k_afterwhoruneo_proxy_020_afterwhoruneo_some_2_person_actions_with_mxoemu_and_mission_and_crew_things.log.txt:1076:00 53 00 00 00 19 04 01 00 00 00 01 00 00 00 00 04 80 b3 3a 04 16 80 bc 45 03 3a 04 00 53 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_020_afterwhoruneo_some_2_person_actions_with_mxoemu_and_mission_and_crew_things.log.txt:1118:00 00 00 04 80 b3 35 04 16 80 bc 45 03 35 04 00 62 00 00 00 35 04 00 00 00 00 01 00 00 00 00 16
```

### Object599 adrenaline booster effect id

Signature: `4c 0a 00 28` (spaced or compact hex)

Matches: 34

```text
mxopackets1_crashover2k_afterwhoruneo_proxy_020_afterwhoruneo_some_2_person_actions_with_mxoemu_and_mission_and_crew_things.log.txt:1314:02 03 21 00 04 80 80 80 80 c0 4c 0a 00 28 01 01 1b 00 04 80 80 80 80 c0 4c 0a 00 28 01 01 11 00
mxopackets1_crashover2k_afterwhoruneo_proxy_020_afterwhoruneo_some_2_person_actions_with_mxoemu_and_mission_and_crew_things.log.txt:1564:02 03 29 00 06 27 00 40 00 00 00 10 00 00 00 d0 80 80 80 80 c0 4c 0a 00 28 01 01 25 00 06 2e 01
mxopackets1_crashover2k_afterwhoruneo_proxy_020_afterwhoruneo_some_2_person_actions_with_mxoemu_and_mission_and_crew_things.log.txt:1566:04 80 80 80 80 c0 4c 0a 00 28 01 01 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_020_afterwhoruneo_some_2_person_actions_with_mxoemu_and_mission_and_crew_things.log.txt:1587:00 00 00 00 00 00 00 ca 1e 00 00 4c 0a 00 28 ff 01 00 00 00 00 00 00 00 00 00 00 00 14 0a 00 58
mxopackets1_crashover2k_afterwhoruneo_proxy_020_afterwhoruneo_some_2_person_actions_with_mxoemu_and_mission_and_crew_things.log.txt:1617:00 01 00 07 03 ff 00 00 00 00 00 00 00 00 ca 1e 00 00 4c 0a 00 28 ff 01 00 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_020_afterwhoruneo_some_2_person_actions_with_mxoemu_and_mission_and_crew_things.log.txt:1677:00 00 01 00 07 03 ff 00 00 00 00 00 00 00 00 ca 1e 00 00 4c 0a 00 28 ff 01 00 00 00 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_020_afterwhoruneo_some_2_person_actions_with_mxoemu_and_mission_and_crew_things.log.txt:1702:00 4c 0a 00 28 ff 01 00 00 00 00 00 00 00 00 00 00 00 14 0a 00 58 37 08 00 00 1f 07 08 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_020_afterwhoruneo_some_2_person_actions_with_mxoemu_and_mission_and_crew_things.log.txt:1722:4c 0a 00 28 ff 01 00 00 00 00 00 00 00 00 00 00 00 14 0a 00 58 37 08 00 00 1f 07 08 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_020_afterwhoruneo_some_2_person_actions_with_mxoemu_and_mission_and_crew_things.log.txt:1744:4c 0a 00 28 ff 01 00 00 00 00 00 00 00 00 00 00 00 14 0a 00 58 37 08 00 00 1f 07 08 00 00 00 00
mxopackets1_crashover2k_afterwhoruneo_proxy_020_afterwhoruneo_some_2_person_actions_with_mxoemu_and_mission_and_crew_things.log.txt:1749:00 00 00 c0 09 00 00 4c 0a 00 28 ff 01 00 00 00 00 00 00 00 00 00 00 00 16 0a 00 58 37 08 00 00
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
