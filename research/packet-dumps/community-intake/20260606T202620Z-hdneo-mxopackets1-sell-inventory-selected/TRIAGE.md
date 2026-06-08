# Packet Capture Triage Report

- Source kind: `directory`
- Source input: `/Users/kyle/Developer/mxo-hd/research/packet-dumps/community-intake/20260606T202620Z-hdneo-mxopackets1-sell-inventory-selected/logs`
- Scanned files from: `/Users/kyle/Developer/mxo-hd/research/packet-dumps/community-intake/20260606T202620Z-hdneo-mxopackets1-sell-inventory-selected/logs`
- Generated UTC: `2026-06-06T20:26:22Z`
- Max sample lines per pattern: `10`

Use this report to decide whether received logs are worth importing into `research/packet-dumps/`. Matches are byte-string leads, not decoded packet claims.

## Vendor And Interaction Leads

### SERVER_VENDOR_OPEN candidate

Signature: `81 0d` (spaced or compact hex)

Matches: 6

```text
crashover2k_afterwhoruneo_old_world_packets_second_pc_others_use_item_vendor_sai_kung.log.txt:119:02 04 01 06 c1 01 66 81 0d 7c ad d9 43 00 00 00 00 c0 a6 e1 40 00 00 00 00 00 40 91 c0 00 00 00
crashover2k_afterwhoruneo_old_world_packets_second_pc_others_useroperativevendor_saikungcentral.log.txt:22:02 04 01 03 93 01 80 d2 81 0d 7c ad d9 43 00 00 00 00 80 d2 e1 40 00 00 00 00 00 3c 91 c0 00 00
crashover2k_afterwhoruneo_old_world_packets_second_pc_others_useroperativevendor_saikungcentral.log.txt:39:02 04 01 03 93 01 80 d2 81 0d 7c ad d9 43 00 00 00 00 80 d2 e1 40 00 00 00 00 00 3c 91 c0 00 00
crashover2k_afterwhoruneo_old_world_packets_second_pc_others_useroperativevendor_saikungcentral.log.txt:56:82 b8 32 96 47 04 01 03 93 01 80 d2 81 0d 7c ad d9 43 00 00 00 00 80 d2 e1 40 00 00 00 00 00 3c
crashover2k_afterwhoruneo_old_world_packets_second_pc_others_use_hacker_vendor_elf_at_saikung.log.txt:14:82 12 c4 99 47 04 02 06 ba 01 0c 81 9c 44 00 80 14 24 06 00 50 01 00 06 bb 01 80 de 81 0d 7c ad
crashover2k_afterwhoruneo_old_world_packets_second_pc_others_use_clothing_vendor_at_saikung.log.txt:98:02 04 01 06 bd 01 82 fa 81 0d 7c ad d9 43 00 00 00 20 9a c4 e3 40 00 00 00 00 00 3c 91 c0 00 00
```

### CLIENT_VENDOR_BUY candidate

Signature: `81 0e` (spaced or compact hex)

Matches: 2

```text
crashover2k_afterwhoruneo_old_world_packets_second_pc_others_buyawakend2.log.txt:2:02 04 01 00 56 01 07 81 0e 00 08 00 80 00
crashover2k_afterwhoruneo_old_world_packets_second_pc_others_buyawakend.log.txt:2:02 04 01 00 54 01 07 81 0e 00 08 00 80 00
```

### CLIENT_VENDOR_SELL candidate

Signature: `81 11` (spaced or compact hex)

Matches: 2

```text
crashover2k_afterwhoruneo_old_world_packets_second_pc_others_sellawakened.log.txt:2:02 04 01 00 57 01 04 81 11 00 58
crashover2k_afterwhoruneo_old_world_packets_second_pc_others_sell_item_FM13sk_saikungoperative.log.txt:22:02 04 01 00 53 01 04 81 11 00 58
```

### CR2 static object interaction

Signature: `80 c8` (spaced or compact hex)

Matches: 3

```text
crashover2k_afterwhoruneo_old_world_packets_second_pc_others_useroperativevendor_saikungcentral.log.txt:10:02 04 01 00 50 01 08 80 c8 46 00 80 14 02 00
crashover2k_afterwhoruneo_old_world_packets_second_pc_others_use_hacker_vendor_elf_at_saikung.log.txt:10:02 04 01 00 96 01 08 80 c8 44 00 80 14 02 00
crashover2k_afterwhoruneo_old_world_packets_second_pc_others_use_clothing_vendor_at_saikung.log.txt:73:02 04 01 00 99 01 08 80 c8 42 00 80 14 02 00
```

### CR1 static object interaction

Signature: `80 c3` (spaced or compact hex)

Matches: 0

### SERVER object/state bundle

Signature: `80 bc` (spaced or compact hex)

Matches: 10

```text
crashover2k_afterwhoruneo_old_world_packets_second_pc_others_equipanewitem_smithshirt.log.txt:7:00 63 00 00 16 80 bc 56 00 47 00 00 8a b2 00 00 23 04 02 00 00 00 01 00 00 00 00 16 80 bc 56 00
crashover2k_afterwhoruneo_old_world_packets_second_pc_others_equipanewitem_smithshirt.log.txt:8:48 00 00 8a b2 00 00 21 04 01 00 00 00 01 00 00 00 00 16 80 bc 56 00 49 00 00 8a b2 00 00 b4 00
crashover2k_afterwhoruneo_old_world_packets_second_pc_others_equipanewitem_smithshirt.log.txt:9:1d 00 00 00 01 00 00 00 00 16 80 bc 56 00 4a 00 00 8a b2 00 00 09 04 1d 00 00 00 01 00 00 00 00
crashover2k_afterwhoruneo_old_world_packets_second_pc_others_equipanewitem_smithshirt.log.txt:10:16 80 bc 56 00 4b 00 00 8a b2 00 00 9d 00 1d 00 00 00 01 00 00 00 00 16 80 bc 56 00 4c 00 00 8a
crashover2k_afterwhoruneo_old_world_packets_second_pc_others_equipanewitem_smithshirt.log.txt:11:b2 00 00 06 04 01 00 00 00 01 00 00 00 00 16 80 bc 56 00 43 01 00 95 b7 00 00 b4 00 16 00 00 00
crashover2k_afterwhoruneo_old_world_packets_second_pc_others_equipanewitem_smithshirt.log.txt:12:00 00 00 00 00 16 80 bc 56 00 44 01 00 95 b7 00 00 09 04 16 00 00 00 00 00 00 00 00 16 80 bc 56
crashover2k_afterwhoruneo_old_world_packets_second_pc_others_equipanewitem_smithshirt.log.txt:13:00 45 01 00 95 b7 00 00 9d 00 16 00 00 00 00 00 00 00 00 16 80 bc 56 00 46 01 00 95 b7 00 00 b5
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

Matches: 2

```text
crashover2k_afterwhoruneo_proxy_second_pc_001uploadcodebits.log.txt:322:00 00 00 00 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 02
crashover2k_afterwhoruneo_proxy_second_pc_001uploadcodebits.log.txt:323:ff 00 00 00 00 00 00 00 00 9e 07 00 00 00 00 32 00 00 00 00 00 00 ff 00 00 00 00 00 00 00 00 00
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
crashover2k_afterwhoruneo_old_world_packets_second_pc_others_use_item_vendor_sai_kung.log.txt:106:02 03 01 00 0c 57 02 3f cd ab 15 8e 42 72 6f 74 68 65 72 73 20 6f 66 20 44 65 73 74 69 6e 79 20
```

### Adjacent Object599 boundary prefix

Signature: `00 0c 57 02` (spaced or compact hex)

Matches: 1

```text
crashover2k_afterwhoruneo_old_world_packets_second_pc_others_use_item_vendor_sai_kung.log.txt:106:02 03 01 00 0c 57 02 3f cd ab 15 8e 42 72 6f 74 68 65 72 73 20 6f 66 20 44 65 73 74 69 6e 79 20
```

### Header-like selector 50

Signature: `01 00 08 50` (spaced or compact hex)

Matches: 0

### Header-like selector 67

Signature: `01 00 08 67` (spaced or compact hex)

Matches: 3

```text
crashover2k_afterwhoruneo_old_world_packets_second_pc_others_useroperativevendor_saikungcentral.log.txt:34:02 03 01 00 08 67 95 46 00 80 14 86 cd ab 01 01 00 00 00 00 80 d2 e1 40 00 00 00 00 00 3c 91 c0
crashover2k_afterwhoruneo_old_world_packets_second_pc_others_useroperativevendor_saikungcentral.log.txt:51:02 03 01 00 08 67 95 46 00 80 14 86 cd ab 01 01 00 00 00 00 80 d2 e1 40 00 00 00 00 00 3c 91 c0
crashover2k_afterwhoruneo_old_world_packets_second_pc_others_useroperativevendor_saikungcentral.log.txt:76:02 03 01 00 08 67 95 46 00 80 14 86 cd ab 01 01 00 00 00 00 80 d2 e1 40 00 00 00 00 00 3c 91 c0
```

### Header-like selector 68

Signature: `01 00 08 68` (spaced or compact hex)

Matches: 1

```text
crashover2k_afterwhoruneo_old_world_packets_second_pc_others_use_hacker_vendor_elf_at_saikung.log.txt:31:02 03 01 00 08 68 95 44 00 80 14 39 cd ab 02 09 00 00 00 00 80 fe e2 40 00 00 00 00 00 40 91 c0
```

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

Matches: 0

### SERVER_ABILITY_UNLOAD

Signature: `80 b3` (spaced or compact hex)

Matches: 0

### Object599 adrenaline booster effect id

Signature: `4c 0a 00 28` (spaced or compact hex)

Matches: 4

```text
crashover2k_afterwhoruneo_old_world_packets_second_pc_others_use_clothing_vendor_at_saikung.log.txt:77:02 03 0d 00 04 80 80 80 80 c0 4c 0a 00 28 01 01 09 00 04 80 80 80 80 c0 4c 0a 00 28 01 01 06 00
crashover2k_afterwhoruneo_old_world_packets_second_pc_others_use_clothing_vendor_at_saikung.log.txt:78:04 80 80 80 80 c0 4c 0a 00 28 01 01 00 00
crashover2k_afterwhoruneo_old_world_packets_second_pc_others_use_clothing_vendor_at_saikung.log.txt:86:82 e0 d7 99 47 03 07 00 04 80 80 80 80 c0 4c 0a 00 28 01 01 00 00
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
