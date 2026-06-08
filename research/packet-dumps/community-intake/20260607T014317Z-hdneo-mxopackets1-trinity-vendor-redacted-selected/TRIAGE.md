# Packet Capture Triage Report

- Source kind: `directory`
- Source input: `/Users/kyle/Developer/mxo-hd/research/packet-dumps/community-intake/20260607T014317Z-hdneo-mxopackets1-trinity-vendor-redacted-selected/logs`
- Scanned files from: `/Users/kyle/Developer/mxo-hd/research/packet-dumps/community-intake/20260607T014317Z-hdneo-mxopackets1-trinity-vendor-redacted-selected/logs`
- Generated UTC: `2026-06-07T01:43:17Z`
- Max sample lines per pattern: `10`

Use this report to decide whether received logs are worth importing into `research/packet-dumps/`. Matches are byte-string leads, not decoded packet claims.

## Vendor And Interaction Leads

### SERVER_VENDOR_OPEN candidate

Signature: `81 0d` (spaced or compact hex)

Matches: 2

```text
crashover2k_trinitys_proxy_second_pc_009_trinitys_session_weapon_vendor.log.txt:1202:02 04 01 01 12 01 3e 81 0d 7c ad d9 43 00 00 00 00 dd c8 ef c0 00 00 00 00 00 c0 57 40 00 00 00
crashover2k_trinitys_proxy_second_pc_010_trinitys_vendor.log.txt:3559:02 04 01 00 c0 01 3e 81 0d 7c ad d9 43 00 00 00 00 dd c8 ef c0 00 00 00 00 00 c0 57 40 00 00 00
```

### CLIENT_VENDOR_BUY candidate

Signature: `81 0e` (spaced or compact hex)

Matches: 0

### CLIENT_VENDOR_SELL candidate

Signature: `81 11` (spaced or compact hex)

Matches: 5

```text
crashover2k_trinitys_proxy_second_pc_010_trinitys_vendor.log.txt:3843:02 04 01 00 22 01 04 81 11 00 53
crashover2k_trinitys_proxy_second_pc_010_trinitys_vendor.log.txt:3860:02 04 01 00 23 01 04 81 11 00 52
crashover2k_trinitys_proxy_second_pc_010_trinitys_vendor.log.txt:3877:02 04 01 00 24 01 04 81 11 00 51
crashover2k_trinitys_proxy_second_pc_010_trinitys_vendor.log.txt:3894:02 04 01 00 25 01 04 81 11 00 55
crashover2k_trinitys_proxy_second_pc_010_trinitys_vendor.log.txt:3911:02 04 01 00 26 01 04 81 11 00 58
```

### CR2 static object interaction

Signature: `80 c8` (spaced or compact hex)

Matches: 0

### CR1 static object interaction

Signature: `80 c3` (spaced or compact hex)

Matches: 0

### SERVER object/state bundle

Signature: `80 bc` (spaced or compact hex)

Matches: 610

```text
crashover2k_trinitys_proxy_second_pc_009_trinitys_session_weapon_vendor.log.txt:530:80 b2 11 00 11 00 08 02 16 80 bc 45 03 11 00 00 02 00 00 00 11 00 11 00 00 00 00 00 00 00 00 16
crashover2k_trinitys_proxy_second_pc_009_trinitys_session_weapon_vendor.log.txt:531:80 bc 45 00 02 00 00 02 00 00 00 cc 00 04 00 00 00 00 00 00 00 00 16 80 bc 45 00 03 00 00 0b 00
crashover2k_trinitys_proxy_second_pc_009_trinitys_session_weapon_vendor.log.txt:532:00 00 37 02 33 00 00 00 00 00 00 00 00 16 80 bc 45 00 04 00 00 2d 00 00 00 23 04 03 00 00 00 00
crashover2k_trinitys_proxy_second_pc_009_trinitys_session_weapon_vendor.log.txt:533:00 00 00 00 16 80 bc 45 00 05 00 00 2d 00 00 00 20 04 03 00 00 00 00 00 00 00 00 16 80 bc 45 00
crashover2k_trinitys_proxy_second_pc_009_trinitys_session_weapon_vendor.log.txt:534:06 00 00 2d 00 00 00 19 04 01 00 00 00 00 00 00 00 00 16 80 bc 45 00 07 00 00 2d 00 00 00 42 04
crashover2k_trinitys_proxy_second_pc_009_trinitys_session_weapon_vendor.log.txt:535:01 00 00 00 00 00 00 00 00 16 80 bc 45 00 08 00 00 34 00 00 00 83 02 44 00 00 00 00 00 00 00 00
crashover2k_trinitys_proxy_second_pc_009_trinitys_session_weapon_vendor.log.txt:536:16 80 bc 45 00 09 00 00 4c 00 00 00 15 04 04 00 00 00 00 00 00 00 00 16 80 bc 45 00 0a 00 00 4c
crashover2k_trinitys_proxy_second_pc_009_trinitys_session_weapon_vendor.log.txt:537:00 00 00 06 04 04 00 00 00 00 00 00 00 00 16 80 bc 45 00 0b 00 00 4c 00 00 00 19 04 02 00 00 00
crashover2k_trinitys_proxy_second_pc_009_trinitys_session_weapon_vendor.log.txt:538:00 00 00 00 00 16 80 bc 45 00 0c 00 00 53 00 00 00 15 04 01 00 00 00 00 00 00 00 00 16 80 bc 45
crashover2k_trinitys_proxy_second_pc_009_trinitys_session_weapon_vendor.log.txt:539:00 0d 00 00 53 00 00 00 19 04 01 00 00 00 00 00 00 00 00 08 80 b2 3a 04 00 00 08 02 16 80 bc 45
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

Matches: 0

### Selector ff continuation prefix 00 01 04 ff

Signature: `00 01 04 ff` (spaced or compact hex)

Matches: 0

### Selector ff continuation prefix 00 01 10 ff

Signature: `00 01 10 ff` (spaced or compact hex)

Matches: 0

### Selector ff continuation prefix 00 00 00 ff

Signature: `00 00 00 ff` (spaced or compact hex)

Matches: 9

```text
crashover2k_trinitys_proxy_second_pc_009_trinitys_session_weapon_vendor.log.txt:2318:00 00 00 01 ff 00 00 00 00 00 00 00 00 32 32 00 00 00 00 ff 00 00 00 00 00 00 ff 00 00 00 00 00
crashover2k_trinitys_proxy_second_pc_009_trinitys_session_weapon_vendor.log.txt:2320:00 00 00 00 00 00 22 b6 00 00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00 00 00 00 36 00 00 4e 37
crashover2k_trinitys_proxy_second_pc_010_trinitys_vendor.log.txt:180:09 00 00 00 00 00 00 00 00 ff 51 c5 69 0f 00 03 00 0c 00 07 00 a9 00
crashover2k_trinitys_proxy_second_pc_010_trinitys_vendor.log.txt:4545:21 1c 24 0d c9 8a 00 08 00 00 00 03 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ff 28 0a 32 00 00
crashover2k_trinitys_proxy_second_pc_010_trinitys_vendor.log.txt:4548:00 00 00 00 00 00 00 ff 00 00 00 00 01 00 00 00 00 00 00 00 80 3f 11 00 00 00 00 00 00 00 05 00
crashover2k_trinitys_proxy_second_pc_010_trinitys_vendor.log.txt:4572:00 03 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ff 28 0a 32 00 00 00 00 00 00 00 00 00 00 00 00
crashover2k_trinitys_proxy_second_pc_010_trinitys_vendor.log.txt:4573:00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ff 00 00 00 00 00 f7 00 00
crashover2k_trinitys_proxy_second_pc_010_trinitys_vendor.log.txt:4574:00 f7 00 00 00 00 00 28 0a ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ff 00 00
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

Matches: 1

```text
crashover2k_trinitys_proxy_second_pc_009_trinitys_session_weapon_vendor.log.txt:2274:02 03 01 00 0c 57 02 f7 cd ab 11 8e 41 67 65 6e 74 20 57 69 6c 73 6f 6e 00 00 00 00 00 00 00 00
```

### Adjacent Object599 boundary prefix

Signature: `00 0c 57 02` (spaced or compact hex)

Matches: 1

```text
crashover2k_trinitys_proxy_second_pc_009_trinitys_session_weapon_vendor.log.txt:2274:02 03 01 00 0c 57 02 f7 cd ab 11 8e 41 67 65 6e 74 20 57 69 6c 73 6f 6e 00 00 00 00 00 00 00 00
```

### Header-like selector 50

Signature: `01 00 08 50` (spaced or compact hex)

Matches: 2

```text
crashover2k_trinitys_proxy_second_pc_009_trinitys_session_weapon_vendor.log.txt:799:00 00 00 fa c9 40 0f 00 00 01 00 08 50 02 81 08 f0 34 0d cd ab 02 09 00 00 00 c0 94 95 f0 c0 00
crashover2k_trinitys_proxy_second_pc_010_trinitys_vendor.log.txt:2179:40 00 00 00 00 00 32 c9 40 07 00 00 01 00 08 50 02 81 08 f0 34 0d cd ab 02 09 00 00 00 c0 94 95
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

Matches: 0

## Movement And Object-State Leads

### Mode 06 movement selector 08

Signature: `00 06 08` (spaced or compact hex)

Matches: 0

### Mode 06 movement selector 0a

Signature: `00 06 0a` (spaced or compact hex)

Matches: 0

### Mode 06 movement selector 0c

Signature: `00 06 0c` (spaced or compact hex)

Matches: 0

### Mode 06 movement selector 0e

Signature: `00 06 0e` (spaced or compact hex)

Matches: 0

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

Matches: 24

```text
crashover2k_trinitys_proxy_second_pc_009_trinitys_session_weapon_vendor.log.txt:528:cb c0 12 00 00 00 00 0a 80 e4 fc be 4b ee 00 00 00 00 08 80 b2 4e 00 08 00 08 02 08 80 b2 52 00
crashover2k_trinitys_proxy_second_pc_009_trinitys_session_weapon_vendor.log.txt:529:10 00 08 02 08 80 b2 54 00 09 00 08 02 08 80 b2 4f 00 0b 00 08 02 08 80 b2 51 00 0c 00 08 02 08
crashover2k_trinitys_proxy_second_pc_009_trinitys_session_weapon_vendor.log.txt:530:80 b2 11 00 11 00 08 02 16 80 bc 45 03 11 00 00 02 00 00 00 11 00 11 00 00 00 00 00 00 00 00 16
crashover2k_trinitys_proxy_second_pc_009_trinitys_session_weapon_vendor.log.txt:539:00 0d 00 00 53 00 00 00 19 04 01 00 00 00 00 00 00 00 00 08 80 b2 3a 04 00 00 08 02 16 80 bc 45
crashover2k_trinitys_proxy_second_pc_009_trinitys_session_weapon_vendor.log.txt:543:62 00 00 00 06 04 01 00 00 00 00 00 00 00 00 08 80 b2 35 04 00 00 08 02 16 80 bc 45 03 35 04 00
crashover2k_trinitys_proxy_second_pc_009_trinitys_session_weapon_vendor.log.txt:901:00 00 02 00 00 00 cc 00 04 00 00 00 01 00 00 00 00 08 80 b2 11 00 11 00 08 02 16 80 bc 45 03 11
crashover2k_trinitys_proxy_second_pc_009_trinitys_session_weapon_vendor.log.txt:951:00 00 00 00 00 00 00 00 08 80 b2 3a 04 00 00 08 02 16 80 bc 45 03 3a 04 00 53 00 00 00 3a 04 00
crashover2k_trinitys_proxy_second_pc_009_trinitys_session_weapon_vendor.log.txt:958:01 00 00 00 00 16 80 bc 55 00 5f 00 00 62 00 00 00 06 04 01 00 00 00 00 00 00 00 00 08 80 b2 35
crashover2k_trinitys_proxy_second_pc_010_trinitys_vendor.log.txt:526:00 00 00 08 80 b2 4e 00 08 00 08 02 08 80 b2 52 00 10 00 08 02 08 80 b2 54 00 09 00 08 02 08 80
crashover2k_trinitys_proxy_second_pc_010_trinitys_vendor.log.txt:527:b2 4f 00 0b 00 08 02 08 80 b2 51 00 0c 00 08 02 08 80 b2 11 00 11 00 08 02 16 80 bc 45 03 11 00
```

### SERVER_ABILITY_UNLOAD

Signature: `80 b3` (spaced or compact hex)

Matches: 11

```text
crashover2k_trinitys_proxy_second_pc_009_trinitys_session_weapon_vendor.log.txt:948:00 00 01 00 00 00 00 04 80 b3 3a 04 16 80 bc 45 03 3a 04 00 53 00 00 00 3a 04 00 00 00 00 01 00
crashover2k_trinitys_proxy_second_pc_009_trinitys_session_weapon_vendor.log.txt:956:11 00 00 62 00 00 00 06 04 01 00 00 00 01 00 00 00 00 04 80 b3 35 04 16 80 bc 45 03 35 04 00 62
crashover2k_trinitys_proxy_second_pc_010_trinitys_vendor.log.txt:737:00 00 16 80 bc 56 00 4f 00 00 93 6c 00 00 9d 00 10 00 00 00 00 00 00 00 00 04 80 b3 11 00 16 80
crashover2k_trinitys_proxy_second_pc_010_trinitys_vendor.log.txt:755:0d 00 00 53 00 00 00 19 04 01 00 00 00 01 00 00 00 00 04 80 b3 3a 04
crashover2k_trinitys_proxy_second_pc_010_trinitys_vendor.log.txt:794:00 06 04 01 00 00 00 01 00 00 00 00 04 80 b3 35 04 16 80 bc 45 03 35 04 00 62 00 00 00 35 04 00
crashover2k_trinitys_proxy_second_pc_010_trinitys_vendor.log.txt:1958:04 80 b3 11 00 16 80 bc 45 03 11 00 00 02 00 00 00 11 00 11 00 00 00 01 00 00 00 00 04 80 b3 35
crashover2k_trinitys_proxy_second_pc_010_trinitys_vendor.log.txt:1959:04 16 80 bc 45 03 35 04 00 62 00 00 00 35 04 00 00 00 00 01 00 00 00 00 04 80 b3 3a 04 16 80 bc
crashover2k_trinitys_proxy_second_pc_010_trinitys_vendor.log.txt:2240:10 00 00 00 00 00 00 00 00 04 80 b3 11 00 16 80 bc 45 03 11 00 00 02 00 00 00 11 00 11 00 00 00
crashover2k_trinitys_proxy_second_pc_010_trinitys_vendor.log.txt:2295:80 b3 3a 04 16 80 bc 45 03 3a 04 00 53 00 00 00 3a 04 00 00 00 00 01 00 00 00 00 16 80 bc 55 00
crashover2k_trinitys_proxy_second_pc_010_trinitys_vendor.log.txt:2303:04 01 00 00 00 01 00 00 00 00 04 80 b3 35 04 16 80 bc 45 03 35 04 00 62 00 00 00 35 04 00 00 00
```

### Object599 adrenaline booster effect id

Signature: `4c 0a 00 28` (spaced or compact hex)

Matches: 0

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
