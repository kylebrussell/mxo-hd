# Packet Capture Triage Report

- Source kind: `directory`
- Source input: `/Users/kyle/Developer/mxo-hd/research/packet-dumps/community-intake/20260606T201844Z-hdneo-mxopackets1-vendor-marketplace-selected/logs`
- Scanned files from: `/Users/kyle/Developer/mxo-hd/research/packet-dumps/community-intake/20260606T201844Z-hdneo-mxopackets1-vendor-marketplace-selected/logs`
- Generated UTC: `2026-06-06T20:18:44Z`
- Max sample lines per pattern: `10`

Use this report to decide whether received logs are worth importing into `research/packet-dumps/`. Matches are byte-string leads, not decoded packet claims.

## Vendor And Interaction Leads

### SERVER_VENDOR_OPEN candidate

Signature: `81 0d` (spaced or compact hex)

Matches: 3

```text
crashover2k_afterwhoruneo_proxy_003_vendor_prop_vendor.log.txt:18:82 80 fb 03 49 04 02 04 98 01 0c 81 9c 9a 08 f0 34 06 05 00 08 01 00 04 99 01 81 e2 81 0d 44 b3
crashover2k_afterwhoruneo_proxy_003_vendor_weapon.log.txt:23:02 04 02 04 9c 01 0c 81 9c 9b 08 f0 34 24 06 00 50 01 00 04 9d 01 3e 81 0d 7c ad d9 43 00 00 00
crashover2k_trinitys_proxy_second_pc_008_buyed_weapons_for_30000_info_per_weapon_all_place_to_first_slot_every_row_4_times_same_weapon.log.txt:4820:02 04 01 01 29 01 3e 81 0d 7c ad d9 43 00 00 00 00 dd c8 ef c0 00 00 00 00 00 c0 57 40 00 00 00
```

### CLIENT_VENDOR_BUY candidate

Signature: `81 0e` (spaced or compact hex)

Matches: 16

```text
crashover2k_trinitys_proxy_second_pc_008_buyed_weapons_for_30000_info_per_weapon_all_place_to_first_slot_every_row_4_times_same_weapon.log.txt:5004:02 04 01 00 4a 01 07 81 0e c1 09 00 00 00
crashover2k_trinitys_proxy_second_pc_008_buyed_weapons_for_30000_info_per_weapon_all_place_to_first_slot_every_row_4_times_same_weapon.log.txt:5033:02 04 01 00 4b 01 07 81 0e c1 09 00 00 00
crashover2k_trinitys_proxy_second_pc_008_buyed_weapons_for_30000_info_per_weapon_all_place_to_first_slot_every_row_4_times_same_weapon.log.txt:5070:02 04 01 00 4c 01 07 81 0e c1 09 00 00 00
crashover2k_trinitys_proxy_second_pc_008_buyed_weapons_for_30000_info_per_weapon_all_place_to_first_slot_every_row_4_times_same_weapon.log.txt:5148:02 04 01 00 4d 01 07 81 0e c1 09 00 00 00
crashover2k_trinitys_proxy_second_pc_008_buyed_weapons_for_30000_info_per_weapon_all_place_to_first_slot_every_row_4_times_same_weapon.log.txt:5287:02 04 01 00 4e 01 07 81 0e c1 1e 00 00 00
crashover2k_trinitys_proxy_second_pc_008_buyed_weapons_for_30000_info_per_weapon_all_place_to_first_slot_every_row_4_times_same_weapon.log.txt:5321:02 04 01 00 50 01 07 81 0e c1 1e 00 00 00
crashover2k_trinitys_proxy_second_pc_008_buyed_weapons_for_30000_info_per_weapon_all_place_to_first_slot_every_row_4_times_same_weapon.log.txt:5338:02 04 01 00 51 01 07 81 0e c1 1e 00 00 00
crashover2k_trinitys_proxy_second_pc_008_buyed_weapons_for_30000_info_per_weapon_all_place_to_first_slot_every_row_4_times_same_weapon.log.txt:5367:02 04 01 00 52 01 07 81 0e c1 1e 00 00 00
crashover2k_trinitys_proxy_second_pc_008_buyed_weapons_for_30000_info_per_weapon_all_place_to_first_slot_every_row_4_times_same_weapon.log.txt:5414:02 04 01 00 53 01 07 81 0e c6 1e 00 00 00
crashover2k_trinitys_proxy_second_pc_008_buyed_weapons_for_30000_info_per_weapon_all_place_to_first_slot_every_row_4_times_same_weapon.log.txt:5441:02 04 01 00 54 01 07 81 0e c6 1e 00 00 00
```

### CLIENT_VENDOR_SELL candidate

Signature: `81 11` (spaced or compact hex)

Matches: 0

### CR2 static object interaction

Signature: `80 c8` (spaced or compact hex)

Matches: 0

### CR1 static object interaction

Signature: `80 c3` (spaced or compact hex)

Matches: 1

```text
crashover2k_trinitys_proxy_second_pc_008_buyed_weapons_for_30000_info_per_weapon_all_place_to_first_slot_every_row_4_times_same_weapon.log.txt:3878:00 c0 57 40 00 00 00 80 20 70 d2 40 0a 00 04 80 80 80 40 ec 04 00 00 04 01 01 1d 01 06 80 c3 60
```

### SERVER object/state bundle

Signature: `80 bc` (spaced or compact hex)

Matches: 0

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

Matches: 30

```text
crashover2k_trinitys_proxy_second_pc_008_buyed_weapons_for_30000_info_per_weapon_all_place_to_first_slot_every_row_4_times_same_weapon.log.txt:1840:88 00 00 00 00 00 00 00 00 01 00 ff 00 00 00 00 ef ac 00 00 02 00 10 01 22 00 00 00 00 00 00 01
crashover2k_trinitys_proxy_second_pc_008_buyed_weapons_for_30000_info_per_weapon_all_place_to_first_slot_every_row_4_times_same_weapon.log.txt:1860:88 00 00 00 00 00 00 00 00 01 00 ff 00 00 00 00 ef ac 00 00 02 00 10 01 22 00 00 00 00 00 00 01
crashover2k_trinitys_proxy_second_pc_008_buyed_weapons_for_30000_info_per_weapon_all_place_to_first_slot_every_row_4_times_same_weapon.log.txt:2029:00 00 00 00 00 00 00 00 01 00 ff 00 00 00 00 ef ac 00 00 02 00 10 01 22 00 00 00 00 00 00 01 00
crashover2k_trinitys_proxy_second_pc_008_buyed_weapons_for_30000_info_per_weapon_all_place_to_first_slot_every_row_4_times_same_weapon.log.txt:2054:00 00 00 00 01 00 ff 00 00 00 00 ef ac 00 00 02 00 10 01 22 00 00 00 00 00 00 01 00 00 00
crashover2k_trinitys_proxy_second_pc_008_buyed_weapons_for_30000_info_per_weapon_all_place_to_first_slot_every_row_4_times_same_weapon.log.txt:2072:00 00 00 00 01 00 ff 00 00 00 00 ef ac 00 00 02 00 10 01 22 00 00 00 00 00 00 01 00 00 00
crashover2k_trinitys_proxy_second_pc_008_buyed_weapons_for_30000_info_per_weapon_all_place_to_first_slot_every_row_4_times_same_weapon.log.txt:2102:00 00 00 00 00 01 00 ff 00 00 00 00 ef ac 00 00 02 00 10 01 22 00 00 00 00 00 00 01 00 0a 00 02
crashover2k_trinitys_proxy_second_pc_008_buyed_weapons_for_30000_info_per_weapon_all_place_to_first_slot_every_row_4_times_same_weapon.log.txt:2122:00 00 00 00 01 00 ff 00 00 00 00 ef ac 00 00 02 00 10 01 22 00 00 00 00 00 00 01 00 0a 00 02 0e
crashover2k_trinitys_proxy_second_pc_008_buyed_weapons_for_30000_info_per_weapon_all_place_to_first_slot_every_row_4_times_same_weapon.log.txt:2142:00 00 00 00 01 00 ff 00 00 00 00 ef ac 00 00 02 00 10 01 22 00 00 00 00 00 00 01 00 0a 00 02 0c
crashover2k_trinitys_proxy_second_pc_008_buyed_weapons_for_30000_info_per_weapon_all_place_to_first_slot_every_row_4_times_same_weapon.log.txt:2174:00 00 00 00 01 00 ff 00 00 00 00 ef ac 00 00 02 00 10 01 22 00 00 00 00 00 00 01 00 0a 00 02 0c
crashover2k_trinitys_proxy_second_pc_008_buyed_weapons_for_30000_info_per_weapon_all_place_to_first_slot_every_row_4_times_same_weapon.log.txt:2214:00 00 00 00 01 00 ff 00 00 00 00 ef ac 00 00 02 00 10 01 22 00 00 00 00 00 00 01 00 00 00
```

### Selector ff continuation prefix 00 01 04 ff

Signature: `00 01 04 ff` (spaced or compact hex)

Matches: 0

### Selector ff continuation prefix 00 01 10 ff

Signature: `00 01 10 ff` (spaced or compact hex)

Matches: 0

### Selector ff continuation prefix 00 00 00 ff

Signature: `00 00 00 ff` (spaced or compact hex)

Matches: 76

```text
crashover2k_trinitys_proxy_second_pc_008_buyed_weapons_for_30000_info_per_weapon_all_place_to_first_slot_every_row_4_times_same_weapon.log.txt:206:21 1c 24 0d c9 8a 00 08 00 00 00 03 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ff 28 0a 32 00 00
crashover2k_trinitys_proxy_second_pc_008_buyed_weapons_for_30000_info_per_weapon_all_place_to_first_slot_every_row_4_times_same_weapon.log.txt:209:00 00 00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00 00 00 00 80 3f 11 00 00 00 00 00 00 00 05 00
crashover2k_trinitys_proxy_second_pc_008_buyed_weapons_for_30000_info_per_weapon_all_place_to_first_slot_every_row_4_times_same_weapon.log.txt:228:00 03 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ff 28 0a 32 00 00 00 00 00 00 00 00 00 00 00 00
crashover2k_trinitys_proxy_second_pc_008_buyed_weapons_for_30000_info_per_weapon_all_place_to_first_slot_every_row_4_times_same_weapon.log.txt:229:00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ff 00 00 00 00 00 f7 00 00
crashover2k_trinitys_proxy_second_pc_008_buyed_weapons_for_30000_info_per_weapon_all_place_to_first_slot_every_row_4_times_same_weapon.log.txt:230:00 f7 00 00 00 00 00 28 0a ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ff 00 00
crashover2k_trinitys_proxy_second_pc_008_buyed_weapons_for_30000_info_per_weapon_all_place_to_first_slot_every_row_4_times_same_weapon.log.txt:1200:00 9e 07 ff 00 00 00 00 00 00 00 00 3d b9 00 00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00 00 00
crashover2k_trinitys_proxy_second_pc_008_buyed_weapons_for_30000_info_per_weapon_all_place_to_first_slot_every_row_4_times_same_weapon.log.txt:1218:00 00 00 02 ff 00 00 00 00 00 00 00 00 9e 07 00 00 00 00 32 00 00 00 00 00 00 ff 00 00 00 00 00
crashover2k_trinitys_proxy_second_pc_008_buyed_weapons_for_30000_info_per_weapon_all_place_to_first_slot_every_row_4_times_same_weapon.log.txt:1220:00 00 00 00 00 00 3d b9 00 00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00 00 00 00 14 00 00 4e 37
crashover2k_trinitys_proxy_second_pc_008_buyed_weapons_for_30000_info_per_weapon_all_place_to_first_slot_every_row_4_times_same_weapon.log.txt:1238:00 00 00 00 9e 07 00 00 00 00 32 00 00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00
crashover2k_trinitys_proxy_second_pc_008_buyed_weapons_for_30000_info_per_weapon_all_place_to_first_slot_every_row_4_times_same_weapon.log.txt:1240:00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00 00 00 00 14 00 00 4e 37 08 00 00 1f 06 08 00 00 00
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
crashover2k_trinitys_proxy_second_pc_008_buyed_weapons_for_30000_info_per_weapon_all_place_to_first_slot_every_row_4_times_same_weapon.log.txt:1165:02 03 01 00 0c 57 02 9c cd ab 11 8e 43 6f 72 72 75 70 74 65 64 00 00 00 00 00 00 00 00 00 00 00
```

### Adjacent Object599 boundary prefix

Signature: `00 0c 57 02` (spaced or compact hex)

Matches: 1

```text
crashover2k_trinitys_proxy_second_pc_008_buyed_weapons_for_30000_info_per_weapon_all_place_to_first_slot_every_row_4_times_same_weapon.log.txt:1165:02 03 01 00 0c 57 02 9c cd ab 11 8e 43 6f 72 72 75 70 74 65 64 00 00 00 00 00 00 00 00 00 00 00
```

### Header-like selector 50

Signature: `01 00 08 50` (spaced or compact hex)

Matches: 0

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

Matches: 8

```text
crashover2k_afterwhoruneo_proxy_second_pc_002_marketplace_codes_with_all_subcats.log.txt:12:54 4a 00 ba 02 00 00 00 00 00 28 fb c6 12 00 e8 03 00 00 00 07 f8 54 4a 00 06 0a 00 00 00 00 00
crashover2k_afterwhoruneo_proxy_second_pc_002_marketplace_codes_with_all_subcats.log.txt:14:0c 29 56 4a 00 ba 02 00 00 00 00 00 28 f5 c7 12 00 e8 03 00 00 00 ee 8d 57 4a 00 06 0a 00 00 00
crashover2k_afterwhoruneo_proxy_second_pc_002_marketplace_codes_with_all_subcats.log.txt:15:00 00 24 1f c8 12 00 88 13 00 00 00 d7 3a 58 4a 00 06 0a 00 00 00 00 00 24 e7 c8 12 00 88 13 00
crashover2k_afterwhoruneo_proxy_second_pc_002_marketplace_codes_with_all_subcats.log.txt:16:00 00 e4 49 5a 4a 00 bb 02 00 00 00 00 00 48 63 ca 12 00 28 23 00 00 00 fe d3 5c 4a 00 06 0a 00
crashover2k_afterwhoruneo_proxy_second_pc_002_marketplace_codes_with_all_subcats.log.txt:85:02 00 00 00 00 00 28 fb c6 12 00 e8 03 00 00 00 07 f8 54 4a 00 06 0a 00 00 00 00 00 44 2d c7 12
crashover2k_afterwhoruneo_proxy_second_pc_002_marketplace_codes_with_all_subcats.log.txt:87:00 ba 02 00 00 00 00 00 28 f5 c7 12 00 e8 03 00 00 00 ee 8d 57 4a 00 06 0a 00 00 00 00 00 24 1f
crashover2k_afterwhoruneo_proxy_second_pc_002_marketplace_codes_with_all_subcats.log.txt:88:c8 12 00 88 13 00 00 00 d7 3a 58 4a 00 06 0a 00 00 00 00 00 24 e7 c8 12 00 88 13 00 00 00 e4 49
crashover2k_afterwhoruneo_proxy_second_pc_002_marketplace_codes_with_all_subcats.log.txt:89:5a 4a 00 bb 02 00 00 00 00 00 48 63 ca 12 00 28 23 00 00 00 fe d3 5c 4a 00 06 0a 00 00 00 00 00
```

### Mode 06 movement selector 0c

Signature: `00 06 0c` (spaced or compact hex)

Matches: 0

### Mode 06 movement selector 0e

Signature: `00 06 0e` (spaced or compact hex)

Matches: 3

```text
crashover2k_trinitys_proxy_second_pc_008_buyed_weapons_for_30000_info_per_weapon_all_place_to_first_slot_every_row_4_times_same_weapon.log.txt:1236:02 03 0a 00 06 0e 04 14 80 cd 83 c7 00 00 be 42 d6 34 7b 46 ff 00 00 00 10 00 00 00 00 00 01 ff
crashover2k_trinitys_proxy_second_pc_008_buyed_weapons_for_30000_info_per_weapon_all_place_to_first_slot_every_row_4_times_same_weapon.log.txt:1256:02 03 0a 00 06 0e 03 3c 80 cd 83 c7 00 00 be 42 d6 34 7b 46 ff 00 00 00 10 00 00 00 00 00 01 ff
crashover2k_trinitys_proxy_second_pc_008_buyed_weapons_for_30000_info_per_weapon_all_place_to_first_slot_every_row_4_times_same_weapon.log.txt:1271:02 03 0a 00 06 0e 03 48 c0 a8 83 c7 00 00 be 42 9d f6 7a 46 ff 00 00 00 10 00 00 00 00 00 01 ff
```

### Repeated mode 06 suffix lead

Signature: `ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00` (spaced or compact hex)

Matches: 1

```text
crashover2k_trinitys_proxy_second_pc_008_buyed_weapons_for_30000_info_per_weapon_all_place_to_first_slot_every_row_4_times_same_weapon.log.txt:1197:34 7b 46 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
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

Matches: 0

### SERVER_ABILITY_UNLOAD

Signature: `80 b3` (spaced or compact hex)

Matches: 0

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
