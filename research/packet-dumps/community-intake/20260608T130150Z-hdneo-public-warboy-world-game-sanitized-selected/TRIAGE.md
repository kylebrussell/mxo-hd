# Packet Capture Triage Report

- Source kind: `directory`
- Source input: `/Users/kyle/Developer/mxo-hd/research/packet-dumps/community-intake/20260608T130150Z-hdneo-public-warboy-world-game-sanitized-selected/logs`
- Scanned files from: `/Users/kyle/Developer/mxo-hd/research/packet-dumps/community-intake/20260608T130150Z-hdneo-public-warboy-world-game-sanitized-selected/logs`
- Generated UTC: `2026-06-08T13:01:51Z`
- Max sample lines per pattern: `10`

Use this report to decide whether received logs are worth importing into `research/packet-dumps/`. Matches are byte-string leads, not decoded packet claims.

## Vendor And Interaction Leads

### SERVER_VENDOR_OPEN candidate

Signature: `81 0d` (spaced or compact hex)

Matches: 22

```text
ProxyM2_m2_packets6.log.txt:66444:02 03 0f 00 02 80 80 80 08 81 0d 00 03 2a 27 c0 00 80 85 00 01 fb 49 a4 47 00 00 be 42 f5 a4 59 
ProxyM2_m2_packets.log.txt:5758:e0 2d 81 0d 22 89 20 00 0b 0c 11 79 80 00 00 8a 01 bd 00 00 00 20 94 a0 d1 40 00 00 00 80 00 be 
ProxyM2_m2_packets.log.txt:16449:00 00 40 f2 5d 99 40 ba 13 fd 43 00 00 00 fb ff 9a 9f 07 00 b0 74 00 00 e0 2d 81 0d 22 89 20 00 
ProxyM2_m2_packets.log.txt:20640:1e e3 44 ff 01 43 00 00 00 00 00 00 00 e0 2d 81 0d 22 89 20 00 0b 0c 11 79 80 00 00 01 00 00 00 
ProxyM2_m2_packets.log.txt:20677:89 46 00 80 f7 43 f2 1e e3 44 ff 01 43 00 00 00 00 00 00 00 e0 2d 81 0d 22 89 20 00 0b 0c 11 79 
ProxyM2_m2_packets.log.txt:20729:2d 81 0d 22 89 20 00 0b 0c 11 79 80 00 00 01 00 00 00 00 1b 01 00 02 ff 00 00 00 00 ba 13 63 00 
ProxyM2_m2_packets.log.txt:20752:43 f2 1e e3 44 ff 01 43 00 00 00 00 00 00 00 e0 2d 81 0d 22 89 20 00 0b 0c 11 79 80 00 00 01 00 
ProxyM2_m2_packets.log.txt:39541:8b 46 00 80 f7 43 84 8a b1 45 27 00 01 02 04 22 00 01 0c 81 0d 88 80 46 00 80 f7 43 86 c2 6a 45 
ProxyM2_m2_packets.log.txt:45900:00 00 40 de 63 9c 40 ba 13 fd 43 00 00 00 fb ff 9a 9f 07 00 b0 74 00 00 e0 2d 81 0d 22 89 20 00 
ProxyM2_m2_packets.log.txt:93344:02 04 01 03 81 0d 16 80 bc 15 00 88 02 00 f5 03 00 00 08 02 3c 00 00 00 01 00 00 00 00 16 80 bc 
```

### CLIENT_VENDOR_BUY candidate

Signature: `81 0e` (spaced or compact hex)

Matches: 5

```text
ProxyM2_m2_packets.log.txt:207310:02 03 83 00 02 80 80 10 81 0e 7f 00 01 0e ab 2b 41 21 e2 c6 00 72 8d c6 4e 82 00 c5 00 00 
ProxyM2_m2_packets.log.txt:243796:02 03 02 00 02 80 80 80 40 81 0e 83 00 03 08 6e ad df c6 00 72 8d c6 c8 88 b3 c4 80 80 10 ad 04 
ProxyM2_m2_packets.log.txt:259601:02 03 7f 00 02 80 80 10 81 0e 7c 00 01 0e 05 7b 52 92 e6 c6 00 72 8d c6 20 36 02 c4 48 00 01 0c 
ProxyM2_m2_packets.log.txt:272999:02 03 7f 00 02 80 80 10 81 0e 79 00 01 0a 05 c2 2a e6 c6 00 72 8d c6 10 a4 99 c4 77 00 01 02 04 
ProxyM2_m2_packets.log.txt:323419:00 3c 81 0e e3 c6 00 72 8d c6 80 84 98 42 4d 00 01 0a 00 a4 fd e2 c6 00 72 8d c6 c0 a8 70 c3 3d 
```

### CLIENT_VENDOR_SELL candidate

Signature: `81 11` (spaced or compact hex)

Matches: 5

```text
ProxyM2_m2_packets.log.txt:31847:d9 83 46 00 80 f7 43 81 11 18 45 00 00 
ProxyM2_m2_packets.log.txt:37689:fb ff 90 01 00 81 11 76 86 07 1d 34 a8 00 00 03 9e 00 00 00 81 ff 24 00 00 00 00 
ProxyM2_m2_packets.log.txt:307176:8d c6 46 31 2c c5 80 80 10 81 11 00 00 
ProxyM2_m2_packets.log.txt:335246:02 03 7c 00 01 0e 03 4f c7 e9 ec c6 00 72 8d c6 a8 73 c9 c4 4b 00 01 02 05 18 00 02 0c 81 11 c4 
ProxyM2_m2_packets.log.txt:375614:02 03 28 00 02 80 80 10 81 11 00 00 
```

### CR2 static object interaction

Signature: `80 c8` (spaced or compact hex)

Matches: 24

```text
ProxyM2_m2_packets_5.log.txt:9140:02 03 02 00 01 02 00 00 00 04 01 00 d4 01 08 80 c8 09 07 90 10 03 00 
ProxyM2_m2_packets_5.log.txt:26688:6e 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 80 bf 80 c8 
ProxyM1_m1_packets4.log.txt:13865:11 0a 08 00 4e c0 00 00 8a 01 dd 00 00 00 b6 4d 5a d2 40 1a 92 ce 8e 10 ed a1 40 00 00 00 80 c8 
ProxyM1_m1_packets4.log.txt:18851:80 10 00 38 47 c7 00 80 13 c4 00 94 8e c7 00 89 01 03 80 fc 02 00 8e 01 08 80 c8 88 06 e0 4b 03 
ProxyM1_m1_packets4.log.txt:18947:80 10 00 38 47 c7 00 80 13 c4 00 94 8e c7 00 89 01 03 80 fc 02 00 8e 01 08 80 c8 88 06 e0 4b 03 
ProxyM1_m1_packets4.log.txt:19036:80 10 00 38 47 c7 00 80 13 c4 00 94 8e c7 00 89 01 03 80 fc 02 00 8e 01 08 80 c8 88 06 e0 4b 03 
ProxyM2_m2_packets6.log.txt:6257:03 2e e1 40 1a 00 00 00 00 7b cc 8b 8b 46 00 80 f7 43 e6 d3 57 45 a0 00 00 40 3f 80 80 c8 1a 03 
ProxyM2_m2_packets6.log.txt:7919:46 00 80 f7 43 38 77 b6 44 c7 00 01 0a 00 8b aa 7c 46 00 80 f7 43 38 a1 5e 45 99 00 03 0c 80 c8 
ProxyM2_m2_packets.log.txt:2762:02 03 0a 00 02 0e 04 45 80 c8 d9 44 00 00 be 42 28 fc 8a 46 00 00 
ProxyM2_m2_packets.log.txt:2777:82 26 11 f6 47 03 0a 00 02 0e 04 54 80 c8 d9 44 00 00 be 42 28 fc 8a 46 07 00 02 0c 9f 90 4b 87 
```

### CR1 static object interaction

Signature: `80 c3` (spaced or compact hex)

Matches: 125

```text
ProxyM1_m1_packets4.log.txt:5386:08 00 00 00 00 00 00 22 00 00 00 00 00 00 00 04 01 01 4f 01 06 80 c3 66 4d df 0b 
ProxyM1_m1_packets4.log.txt:7459:01 01 52 01 06 80 c3 a9 76 df 0b 
ProxyM1_m1_packets4.log.txt:9218:53 01 06 80 c3 dc 9f df 0b 
ProxyM1_m1_packets4.log.txt:10940:00 00 00 fa 3f 84 40 10 76 01 00 00 04 01 01 54 01 06 80 c3 7e c6 df 0b 
ProxyM2_m2_packets_5.log.txt:1380:00 c6 01 80 c3 e7 08 00 58 4c 0a 00 28 00 00 00 a8 d5 8f df c0 00 00 00 00 00 1c 91 40 00 00 00 
ProxyM2_m2_packets_5.log.txt:3175:00 00 00 00 00 00 00 00 00 00 00 00 00 10 00 00 22 d5 08 dc c0 1b 00 c6 01 80 c3 e8 08 00 58 4c 
ProxyM2_m2_packets_5.log.txt:3253:00 00 00 00 00 00 00 00 00 00 00 00 00 10 00 00 22 d5 08 fe c0 1b 00 c6 01 80 c3 e7 08 00 58 4c 
ProxyM2_m2_packets_5.log.txt:7851:00 00 00 00 00 00 00 00 00 00 00 00 00 10 00 00 22 d5 08 32 c0 1b 00 c6 01 80 c3 e7 08 00 58 4c 
ProxyM2_m2_packets_5.log.txt:7855:10 00 00 22 d5 08 bd c0 1b 00 c6 01 80 c3 e8 08 00 58 4c 0a 00 28 00 00 00 70 a8 92 dc c0 00 00 
ProxyM2_m2_packets_5.log.txt:8906:00 00 00 00 00 00 00 00 00 00 00 00 00 10 00 00 22 d5 07 44 c0 1b 00 c6 01 80 c3 e7 08 00 58 4c 
```

### SERVER object/state bundle

Signature: `80 bc` (spaced or compact hex)

Matches: 6124

```text
ProxyM2_m2_packets_f_5.log.txt:70:00 04 03 01 66 01 16 80 bc 11 00 6b 01 00 f8 00 00 00 63 00 00 00 00 00 01 c2 f5 a8 41 01 67 01 
ProxyM2_m2_packets_f_5.log.txt:71:16 80 bc 1d 00 6d 01 00 63 00 00 00 00 00 00 00 00 00 01 00 00 00 00 01 68 01 0b 81 6e 00 00 00 
ProxyM2_m2_packets_f_5.log.txt:84:01 01 69 01 16 80 bc 11 00 70 01 00 f8 00 00 00 63 00 00 00 00 00 00 c2 f5 a8 41 
ProxyM2_m2_packets_f_5.log.txt:187:03 01 6a 01 16 80 bc 1d 00 71 01 00 63 00 00 00 00 00 00 00 00 00 00 00 00 00 00 01 6b 01 16 80 
ProxyM2_m2_packets_f_5.log.txt:188:bc 1d 00 71 01 00 63 00 00 00 00 00 00 00 00 00 01 00 00 00 00 01 6c 01 16 80 bc 1d 00 72 01 00 
ProxyM1_m1_packets.log.txt:39:1e 00 08 02 08 80 b2 51 00 12 00 08 02 08 80 b2 11 00 32 00 08 02 16 80 bc 45 03 11 00 00 02 00 
ProxyM1_m1_packets.log.txt:40:00 00 11 00 32 00 00 00 00 00 00 00 00 16 80 bc 45 00 02 00 00 02 00 00 00 cc 00 0c 00 00 00 00 
ProxyM1_m1_packets.log.txt:41:00 00 00 00 16 80 bc 45 00 03 00 00 0b 00 00 00 37 02 96 00 00 00 00 00 00 00 00 16 80 bc 45 00 
ProxyM1_m1_packets.log.txt:42:04 00 00 71 00 00 00 18 04 13 00 00 00 00 00 00 00 00 16 80 bc 45 00 05 00 00 74 00 00 00 74 04 
ProxyM1_m1_packets.log.txt:43:07 00 00 00 00 00 00 00 00 16 80 bc 45 00 06 00 00 74 00 00 00 78 04 07 00 00 00 00 00 00 00 00 
```

## Selector ff Vendor Body Leads

### Selector ff vendor field +9 family

Signature: `e0 2d 81 0d` (spaced or compact hex)

Matches: 16

```text
ProxyM2_m2_packets.log.txt:5758:e0 2d 81 0d 22 89 20 00 0b 0c 11 79 80 00 00 8a 01 bd 00 00 00 20 94 a0 d1 40 00 00 00 80 00 be 
ProxyM2_m2_packets.log.txt:16449:00 00 40 f2 5d 99 40 ba 13 fd 43 00 00 00 fb ff 9a 9f 07 00 b0 74 00 00 e0 2d 81 0d 22 89 20 00 
ProxyM2_m2_packets.log.txt:20640:1e e3 44 ff 01 43 00 00 00 00 00 00 00 e0 2d 81 0d 22 89 20 00 0b 0c 11 79 80 00 00 01 00 00 00 
ProxyM2_m2_packets.log.txt:20677:89 46 00 80 f7 43 f2 1e e3 44 ff 01 43 00 00 00 00 00 00 00 e0 2d 81 0d 22 89 20 00 0b 0c 11 79 
ProxyM2_m2_packets.log.txt:20752:43 f2 1e e3 44 ff 01 43 00 00 00 00 00 00 00 e0 2d 81 0d 22 89 20 00 0b 0c 11 79 80 00 00 01 00 
ProxyM2_m2_packets.log.txt:45900:00 00 40 de 63 9c 40 ba 13 fd 43 00 00 00 fb ff 9a 9f 07 00 b0 74 00 00 e0 2d 81 0d 22 89 20 00 
ProxyM2_m2_packets.log.txt:172962:0a fb ff 9a 9f 07 00 b0 74 00 00 e0 2d 81 0d 22 89 20 00 0b 0c 11 79 80 00 00 8a 01 fd 00 00 00 
ProxyM2_m2_packets.log.txt:192637:00 00 00 00 00 00 00 e0 2d 81 0d 22 89 20 00 0b 0c 11 79 80 00 00 01 00 00 00 00 1b 01 00 02 ff 
ProxyM2_m2_packets.log.txt:192655:e0 2d 81 0d 22 89 20 00 0b 0c 11 79 80 00 00 01 00 00 00 00 1b 01 00 02 ff 00 00 00 00 ba 13 63 
ProxyM2_m2_packets.log.txt:192684:00 00 00 00 00 e0 2d 81 0d 22 89 20 00 0b 0c 11 79 80 00 00 01 00 00 00 00 1b 01 00 02 ff 00 00 
```

### Selector ff vendor body prefix

Signature: `01 43 00 00 00 00 00 00 00 e0 2d 81 0d 22 89 20` (spaced or compact hex)

Matches: 5

```text
ProxyM2_m2_packets.log.txt:20640:1e e3 44 ff 01 43 00 00 00 00 00 00 00 e0 2d 81 0d 22 89 20 00 0b 0c 11 79 80 00 00 01 00 00 00 
ProxyM2_m2_packets.log.txt:20677:89 46 00 80 f7 43 f2 1e e3 44 ff 01 43 00 00 00 00 00 00 00 e0 2d 81 0d 22 89 20 00 0b 0c 11 79 
ProxyM2_m2_packets.log.txt:20752:43 f2 1e e3 44 ff 01 43 00 00 00 00 00 00 00 e0 2d 81 0d 22 89 20 00 0b 0c 11 79 80 00 00 01 00 
ProxyM2_m2_packets.log.txt:193210:c4 ff 01 43 00 00 00 00 00 00 00 e0 2d 81 0d 22 89 20 00 0b 0c 11 79 80 00 00 01 00 00 00 00 1b 
ProxyM2_m2_packets.log.txt:193234:00 12 88 f8 c6 00 e0 88 44 e0 4f a7 c4 ff 01 43 00 00 00 00 00 00 00 e0 2d 81 0d 22 89 20 00 0b 
```

### Selector ff continuation prefix 00 41 00 ff

Signature: `00 41 00 ff` (spaced or compact hex)

Matches: 33

```text
ProxyM2_m2_packets6.log.txt:19960:04 1c 00 00 00 00 00 00 00 00 41 00 ff 00 00 00 00 3f 24 01 00 00 00 08 00 12 01 4a 01 00 00 00 
ProxyM2_m2_packets6.log.txt:20061:00 00 00 00 00 00 00 41 00 ff 00 00 00 00 3f 24 01 00 00 00 08 00 12 01 4a 01 00 00 00 01 00 00 
ProxyM2_m2_packets6.log.txt:20109:00 00 00 00 66 00 00 04 1c 00 00 00 00 00 00 00 00 41 00 ff 00 00 00 00 3f 24 01 00 00 00 08 00 
ProxyM2_m2_packets6.log.txt:20177:00 00 00 66 00 00 04 1c 00 00 00 00 00 00 00 00 41 00 ff 00 00 00 00 3f 24 01 00 00 00 08 00 12 
ProxyM2_m2_packets6.log.txt:20208:00 41 00 ff 00 00 00 00 3f 24 01 00 00 00 08 00 12 01 4a 01 00 00 00 01 00 00 00 
ProxyM2_m2_packets6.log.txt:20251:00 00 00 41 00 ff 00 00 00 00 3f 24 01 00 00 00 08 00 12 01 4a 01 00 00 00 01 00 00 00 
ProxyM2_m2_packets6.log.txt:20320:00 00 00 00 00 41 00 ff 00 00 00 00 3f 24 01 00 00 00 08 00 12 01 4a 01 00 00 00 01 00 00 00 
ProxyM2_m2_packets.log.txt:59259:00 00 00 00 00 00 41 00 ff 00 00 00 00 cb 29 00 00 00 00 18 00 12 02 a5 01 00 00 00 01 00 80 00 
ProxyM2_m2_packets.log.txt:59284:00 00 00 00 00 00 00 41 00 ff 00 00 00 00 cb 29 00 00 00 00 18 00 12 02 a5 01 00 00 00 01 00 00 
ProxyM2_m2_packets.log.txt:59304:04 00 00 00 00 00 00 00 00 41 00 ff 00 00 00 00 cb 29 00 00 00 00 18 00 12 02 a5 01 00 00 00 01 
```

### Selector ff continuation prefix 00 01 00 ff

Signature: `00 01 00 ff` (spaced or compact hex)

Matches: 2608

```text
ProxyM2_m2_packets_5.log.txt:4649:00 00 00 00 00 00 00 00 01 00 ff aa 04 00 04 d9 2f 00 00 02 00 10 00 22 01 7e 00 00 00 00 01 00 
ProxyM2_m2_packets_5.log.txt:4724:00 a8 13 00 01 1c 00 00 ff 00 00 00 00 00 d7 30 46 12 1e 00 00 00 00 00 00 00 00 01 00 ff aa 04 
ProxyM2_m2_packets_5.log.txt:4787:00 00 00 00 00 00 01 00 ff aa 04 00 04 d9 2f 00 00 02 00 10 00 22 01 7e 00 00 00 00 01 00 00 00 
ProxyM2_m2_packets_5.log.txt:23852:00 00 00 00 00 00 00 01 00 ff e6 05 00 28 2f 1b 01 00 00 00 00 00 22 02 f6 01 00 00 00 01 00 05 
ProxyM2_m2_packets_5.log.txt:23949:ff 00 00 00 00 1d 0a 00 28 5d 00 00 00 00 00 00 00 00 01 00 ff f8 0a 00 04 ef 23 01 00 00 00 08 
ProxyM2_m2_packets_5.log.txt:23982:00 00 00 00 01 00 ff f8 0a 00 04 ef 23 01 00 00 00 08 00 12 02 4e 04 00 00 00 01 00 18 00 02 0c 
ProxyM2_m2_packets_5.log.txt:26363:00 66 00 00 04 bf 00 00 00 00 00 00 00 00 01 00 ff 00 00 00 00 00 00 00 00 80 00 98 00 12 00 00 
ProxyM2_m2_packets_5.log.txt:29492:00 d6 1e 00 00 66 00 00 04 38 00 00 00 00 00 00 00 00 01 00 ff 51 0b 00 58 00 00 00 00 00 20 80 
ProxyM2_m2_packets_5.log.txt:29497:8d 00 00 ff ff 00 00 00 00 66 00 00 04 fc 00 00 00 00 00 00 00 00 01 00 ff 20 05 00 04 23 09 01 
ProxyM2_m2_packets_5.log.txt:29523:00 d6 1e 00 00 66 00 00 04 38 00 00 00 00 00 00 00 00 01 00 ff 51 0b 00 58 00 00 00 00 00 20 80 
```

### Selector ff continuation prefix 00 01 04 ff

Signature: `00 01 04 ff` (spaced or compact hex)

Matches: 70

```text
ProxyM2_m2_packets6.log.txt:13023:00 19 31 46 12 27 00 00 00 00 00 00 00 00 01 04 ff 00 00 00 00 00 00 00 00 00 00 00 00 22 00 00 
ProxyM2_m2_packets6.log.txt:13060:00 00 ba 13 00 00 00 00 00 ff 00 00 00 00 00 19 31 46 12 27 00 00 00 00 00 00 00 00 01 04 ff 00 
ProxyM2_m2_packets6.log.txt:13509:00 00 00 00 00 00 00 00 01 04 ff 00 00 00 00 00 00 00 00 00 00 00 00 22 00 00 00 00 00 00 01 00 
ProxyM2_m2_packets6.log.txt:13542:31 46 12 27 00 00 00 00 00 00 00 00 01 04 ff 00 00 00 00 00 00 00 00 00 00 00 00 22 00 00 00 00 
ProxyM2_m2_packets6.log.txt:15093:00 00 00 01 04 ff 00 00 00 00 00 00 00 00 00 00 00 00 22 00 00 00 00 00 00 01 00 25 01 03 28 af 
ProxyM2_m2_packets6.log.txt:15156:00 00 00 00 00 00 00 00 01 04 ff 00 00 00 00 00 00 00 00 00 00 00 00 22 00 00 00 00 00 00 01 00 
ProxyM2_m2_packets6.log.txt:15741:00 00 04 2a 00 00 00 00 00 00 00 00 01 04 ff 00 00 00 00 00 00 00 00 00 00 00 00 22 00 00 00 00 
ProxyM2_m2_packets6.log.txt:15815:00 00 00 00 00 01 04 ff 00 00 00 00 00 00 00 00 00 00 00 00 22 00 00 00 00 00 00 01 00 00 00 
ProxyM2_m2_packets6.log.txt:15848:00 00 ff 00 00 00 00 00 66 00 00 04 2a 00 00 00 00 00 00 00 00 01 04 ff 00 00 00 00 00 00 00 00 
ProxyM2_m2_packets6.log.txt:15882:ba 13 00 00 00 00 00 ff 00 00 00 00 00 66 00 00 04 2a 00 00 00 00 00 00 00 00 01 04 ff 00 00 00 
```

### Selector ff continuation prefix 00 01 10 ff

Signature: `00 01 10 ff` (spaced or compact hex)

Matches: 757

```text
ProxyM2_m2_packets_5.log.txt:19255:00 20 00 00 04 c7 00 00 00 00 00 00 00 00 01 10 ff e6 05 00 28 a2 fc 00 00 00 00 18 00 11 00 00 
ProxyM2_m2_packets_5.log.txt:19511:00 00 00 01 10 ff e6 05 00 28 a2 fc 00 00 00 00 18 00 11 00 00 00 00 00 00 01 00 12 00 06 0e 03 
ProxyM2_m2_packets_5.log.txt:19563:00 00 20 00 00 04 c8 00 00 00 00 00 00 00 00 01 10 ff e6 05 00 28 a2 fc 00 00 00 00 18 00 11 00 
ProxyM2_m2_packets_5.log.txt:20136:00 20 00 00 04 c8 00 00 00 00 00 00 00 00 01 10 ff e6 05 00 28 a2 fc 00 00 00 00 18 00 11 00 00 
ProxyM2_m2_packets_5.log.txt:20560:00 20 00 00 04 c9 00 00 00 00 00 00 00 00 01 10 ff e6 05 00 28 a2 fc 00 00 00 00 18 00 11 00 00 
ProxyM2_m2_packets_5.log.txt:20802:00 00 00 01 10 ff e6 05 00 28 9c e1 00 00 00 00 00 00 22 00 00 00 00 00 00 01 00 00 00 
ProxyM2_m2_packets_5.log.txt:20840:00 00 00 01 10 ff e6 05 00 28 9c e1 00 00 00 00 00 00 22 00 00 00 00 00 00 01 00 0e 00 01 0c 59 
ProxyM2_m2_packets_5.log.txt:20860:00 00 00 01 10 ff e6 05 00 28 9c e1 00 00 00 00 00 00 22 00 00 00 00 00 00 01 00 0e 00 01 0c 5c 
ProxyM2_m2_packets_5.log.txt:21344:00 01 10 ff e6 05 00 28 a2 fc 00 00 02 00 18 00 11 00 00 00 00 00 00 01 00 00 00 
ProxyM2_m2_packets_5.log.txt:21898:00 00 00 00 01 10 ff e6 05 00 28 a2 fc 00 00 02 00 18 00 12 00 00 00 00 00 00 01 00 00 00 04 01 
```

### Selector ff continuation prefix 00 00 00 ff

Signature: `00 00 00 ff` (spaced or compact hex)

Matches: 11869

```text
ProxyM2_m2_packets_f_5.log.txt:5:42 0a 5e 76 a5 24 11 70 04 00 00 00 01 00 00 00 80 bf 00 00 00 00 00 00 00 00 00 ff ba 13 63 00 
ProxyM2_m2_packets_f_5.log.txt:8:0a 00 28 05 00 00 00 00 ff 00 00 00 00 00 10 00 00 00 00 00 00 80 3f 11 00 00 00 42 9e 00 00 2f 
ProxyM2_m2_packets_f_5.log.txt:26:42 0a 5e 76 a5 24 11 70 04 00 00 00 01 00 00 00 80 bf 00 00 00 00 00 00 00 00 00 ff ba 13 63 00 
ProxyM2_m2_packets_f_5.log.txt:29:0a 00 28 05 00 00 00 00 ff 00 00 00 00 00 10 00 00 00 00 00 00 80 3f 11 00 00 00 42 9e 00 00 2f 
ProxyM2_m2_packets_f_5.log.txt:50:bf 00 00 00 00 00 00 00 00 00 ff ba 13 63 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
ProxyM2_m2_packets_f_5.log.txt:51:00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ff 00 00 00 00 00 8a 01 00 00 8a 01 00 00 00 
ProxyM2_m2_packets_f_5.log.txt:52:00 b2 13 ff 00 ee 0f 00 00 00 00 00 00 00 79 0a 00 28 05 00 00 00 00 ff 00 00 00 00 00 10 00 00 
ProxyM2_m2_packets_f_5.log.txt:95:00 00 00 00 00 00 00 00 ff ba 13 63 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
ProxyM2_m2_packets_f_5.log.txt:96:00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ff 00 00 00 00 00 8a 01 00 00 8a 01 00 00 00 00 b7 
ProxyM2_m2_packets_f_5.log.txt:97:13 ff 00 ee 0f 00 00 00 00 00 00 00 79 0a 00 28 05 00 00 00 00 ff 00 00 00 00 00 10 00 00 00 00 
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

Matches: 582

```text
ProxyM1_m1_packets.log.txt:1666:02 03 02 00 02 80 80 80 80 80 10 11 00 00 00 01 00 0c 57 02 78 cd ab 13 8e 42 65 6c 6c 20 4a 61 
ProxyM1_m1_packets.log.txt:1736:02 03 01 00 0c 57 02 79 cd ab 11 8e 42 65 6c 6c 20 4d 61 64 6f 6e 6e 61 00 00 00 00 00 00 00 00 
ProxyM1_m1_packets.log.txt:1739:09 2c 01 07 08 00 00 80 02 01 0f 00 00 01 00 0c 57 02 a6 cd ab 18 8e 42 65 6c 6c 20 4d 61 64 6f 
ProxyM1_m1_packets.log.txt:1743:00 00 06 01 35 08 00 00 0d 00 00 01 00 0c 57 02 7a cd ab 11 8e 42 65 6c 6c 20 4d 61 64 6f 6e 6e 
ProxyM1_m1_packets.log.txt:1746:01 8a 02 15 a8 06 2c 01 b2 09 2c 01 07 08 00 00 80 02 01 0b 00 00 01 00 0c 57 02 7b cd ab 13 8e 
ProxyM1_m1_packets.log.txt:1785:02 03 01 00 0c 57 02 7c cd ab 11 8e 42 65 6c 6c 20 4d 61 64 6f 6e 6e 61 00 00 00 00 00 00 00 00 
ProxyM1_m1_packets.log.txt:1789:a0 19 46 01 00 0c 57 02 7d cd ab 11 8e 42 65 6c 6c 20 4d 61 64 6f 6e 6e 61 00 00 00 00 00 00 00 
ProxyM1_m1_packets.log.txt:1792:b2 09 2c 01 07 08 00 00 80 02 01 07 00 00 01 00 0c 57 02 7e cd ab 11 8e 42 65 6c 6c 20 4d 61 64 
ProxyM2_m2_packets6.log.txt:846:02 03 01 00 0c 57 02 58 cd ab 17 8e 43 79 70 68 65 72 69 74 65 20 53 4d 47 20 53 70 65 63 69 61 
ProxyM2_m2_packets6.log.txt:897:1c 02 84 00 00 00 e4 ff 37 01 00 01 00 0c 57 02 77 cd ab 17 8e 43 79 70 68 65 72 69 74 65 20 53 
```

### Adjacent Object599 boundary prefix

Signature: `00 0c 57 02` (spaced or compact hex)

Matches: 566

```text
ProxyM1_m1_packets.log.txt:1666:02 03 02 00 02 80 80 80 80 80 10 11 00 00 00 01 00 0c 57 02 78 cd ab 13 8e 42 65 6c 6c 20 4a 61 
ProxyM1_m1_packets.log.txt:1736:02 03 01 00 0c 57 02 79 cd ab 11 8e 42 65 6c 6c 20 4d 61 64 6f 6e 6e 61 00 00 00 00 00 00 00 00 
ProxyM1_m1_packets.log.txt:1739:09 2c 01 07 08 00 00 80 02 01 0f 00 00 01 00 0c 57 02 a6 cd ab 18 8e 42 65 6c 6c 20 4d 61 64 6f 
ProxyM1_m1_packets.log.txt:1743:00 00 06 01 35 08 00 00 0d 00 00 01 00 0c 57 02 7a cd ab 11 8e 42 65 6c 6c 20 4d 61 64 6f 6e 6e 
ProxyM1_m1_packets.log.txt:1746:01 8a 02 15 a8 06 2c 01 b2 09 2c 01 07 08 00 00 80 02 01 0b 00 00 01 00 0c 57 02 7b cd ab 13 8e 
ProxyM1_m1_packets.log.txt:1785:02 03 01 00 0c 57 02 7c cd ab 11 8e 42 65 6c 6c 20 4d 61 64 6f 6e 6e 61 00 00 00 00 00 00 00 00 
ProxyM1_m1_packets.log.txt:1789:a0 19 46 01 00 0c 57 02 7d cd ab 11 8e 42 65 6c 6c 20 4d 61 64 6f 6e 6e 61 00 00 00 00 00 00 00 
ProxyM1_m1_packets.log.txt:1792:b2 09 2c 01 07 08 00 00 80 02 01 07 00 00 01 00 0c 57 02 7e cd ab 11 8e 42 65 6c 6c 20 4d 61 64 
ProxyM1_m1_packets4.log.txt:3355:d4 c0 ff ff ff ff 17 00 00 01 00 0c 57 02 ce cd ab 11 8e 42 6c 61 63 6b 77 6f 6f 64 20 47 6f 6f 
ProxyM1_m1_packets4.log.txt:3358:03 17 a8 05 af 00 b2 09 af 00 07 08 00 00 80 02 01 15 00 00 01 00 0c 57 02 cf cd ab 12 8e 42 6c 
```

### Header-like selector 50

Signature: `01 00 08 50` (spaced or compact hex)

Matches: 21

```text
ProxyM1_m1_packets.log.txt:1721:82 ca bc e0 47 03 01 00 08 50 02 7d 02 b0 4a 48 cd ab 02 09 00 00 00 00 80 e7 ea 40 00 00 00 00 
ProxyM1_m1_packets4.log.txt:3365:af 00 12 08 00 00 80 02 01 0d 00 00 01 00 08 50 02 e7 02 30 19 2a cd ab 02 09 00 00 00 00 40 11 
ProxyM2_m2_packets_5.log.txt:23757:00 00 81 ff 08 00 00 01 00 08 50 02 5b 03 40 0a e8 cd ab 01 01 00 00 00 00 00 57 d7 c0 00 00 00 
ProxyM2_m2_packets_5.log.txt:26190:00 00 f0 c1 d8 a5 40 ad 61 4d 09 37 01 00 01 00 08 50 02 ea 01 30 39 ef cd ab 01 01 00 00 00 00 
ProxyM2_m2_packets6.log.txt:815:02 03 01 00 08 50 02 ea 01 30 39 ef cd ab 01 01 00 00 00 00 00 9a d0 40 00 00 00 00 00 90 80 40 
ProxyM2_m2_packets6.log.txt:25551:08 00 00 81 d4 1e 00 00 02 09 27 00 00 01 00 08 50 02 eb 1f f0 2e f3 cd ab 02 09 00 00 00 00 00 
ProxyM2_m2_packets6.log.txt:56417:00 00 01 00 08 50 02 eb 1f f0 2e f3 cd ab 02 09 00 00 00 00 00 82 c4 c0 00 00 00 00 00 28 9e 40 
ProxyM2_m2_packets6.log.txt:68865:02 03 01 00 08 50 02 34 00 90 31 99 cd ab 02 09 00 00 00 00 c0 62 f4 40 00 00 00 00 00 40 60 40 
ProxyM2_m2_packets6.log.txt:72587:82 ef 2d 19 48 03 01 00 08 50 02 34 00 90 31 99 cd ab 02 09 00 00 00 00 c0 62 f4 40 00 00 00 00 
ProxyM2_m2_packets6.log.txt:74246:00 b2 09 e1 00 fd 07 00 00 81 b4 02 00 00 02 01 03 00 00 01 00 08 50 02 34 00 90 31 99 cd ab 02 
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
ProxyM2_m2_packets.log.txt:42539:22 00 00 00 00 00 00 00 01 00 08 69 09 3f 0b f0 02 35 cd ab 02 05 00 00 00 00 00 3f c1 40 00 00 
```

### Header-like selector 76

Signature: `01 00 0c 76` (spaced or compact hex)

Matches: 0

### Header-like selector 90

Signature: `01 00 08 90` (spaced or compact hex)

Matches: 2

```text
ProxyM2_m2_packets6.log.txt:25655:02 03 01 00 02 13 00 19 00 02 0c ed 52 73 21 c6 f8 df ec 44 17 fd 60 47 01 00 08 90 05 c4 14 e0 
ProxyM2_m2_packets6.log.txt:56549:00 00 81 d9 1e 00 00 02 09 2c 00 00 01 00 08 90 05 c4 14 e0 11 2e cd ab 01 01 00 00 00 00 00 7b 
```

### Header-like selector a3

Signature: `01 00 08 a3` (spaced or compact hex)

Matches: 7

```text
ProxyM1_m1_packets4.log.txt:3353:02 03 02 00 02 80 80 80 40 df 13 01 00 08 a3 01 2b 01 30 19 b4 cd ab 03 88 00 00 00 00 00 00 80 
ProxyM1_m1_packets4.log.txt:7397:00 00 e1 ff 0c 00 00 01 00 08 a3 01 2b 01 30 19 b4 cd ab 03 88 00 00 00 00 00 00 80 3f 00 00 00 
ProxyM2_m2_packets6.log.txt:81561:47 00 80 fc c3 a7 fa bd c6 01 00 08 a3 01 2b 01 30 19 b4 cd ab 03 88 00 00 00 00 00 00 80 3f 00 
ProxyM2_m2_packets.log.txt:54787:02 03 01 00 08 a3 01 2b 01 30 19 b4 cd ab 03 88 00 00 00 00 00 00 80 3f 00 00 00 00 00 00 00 00 
ProxyM2_m2_packets.log.txt:92287:02 03 02 00 02 80 84 50 80 50 79 01 f4 06 01 00 08 a3 01 2b 01 30 19 b4 cd ab 03 88 00 00 00 00 
ProxyM2_m2_packets.log.txt:125799:02 03 01 00 08 a3 01 2b 01 30 19 b4 cd ab 03 88 00 00 00 00 00 00 80 3f 00 00 00 00 00 00 00 00 
ProxyM2_m2_packets.log.txt:125818:82 78 2c f8 47 03 01 00 01 01 00 90 00 02 00 02 80 04 33 01 00 08 a3 01 2b 01 30 19 b4 cd ab 03 
```

### Header-like selector d0

Signature: `01 00 08 d0` (spaced or compact hex)

Matches: 45

```text
ProxyM1_m1_packets4.log.txt:12518:12 a8 05 06 01 b2 09 06 01 07 08 00 00 80 02 01 37 00 00 01 00 08 d0 20 ee 01 30 39 c7 cd ab 08 
ProxyM1_m1_packets4.log.txt:12553:15 a8 04 e1 00 b2 09 e1 00 12 08 00 00 80 02 01 33 00 00 01 00 08 d0 20 ef 01 30 39 c8 cd ab 08 
ProxyM1_m1_packets4.log.txt:13774:02 03 01 00 08 d0 20 af 00 20 4e e1 cd ab 08 fd 00 00 00 00 c0 a8 d4 40 00 00 00 00 00 f0 7e 40 
ProxyM1_m1_packets4.log.txt:13985:01 00 04 00 89 98 75 6d 07 58 1c 02 85 00 00 00 f3 ff 3f 00 00 01 00 08 d0 20 b1 00 20 4e c5 cd 
ProxyM2_m2_packets_5.log.txt:26579:90 01 00 ab ff 54 2e 09 42 c7 0c 1c 02 85 00 00 00 ff ff 4f 01 00 01 00 08 d0 20 f1 01 30 39 ea 
ProxyM2_m2_packets6.log.txt:7322:00 00 f4 90 40 00 00 00 40 6d 3e b4 40 57 a9 4c 09 18 00 00 01 00 08 d0 20 b1 00 20 4e ee cd ab 
ProxyM2_m2_packets6.log.txt:16247:00 8b 24 83 50 09 1e 1d 1e 03 01 84 00 00 00 6d 00 45 00 00 01 00 08 d0 20 b1 00 20 4e ee cd ab 
ProxyM2_m2_packets6.log.txt:19419:02 03 01 00 08 d0 20 b0 00 20 4e f4 cd ab 08 fd 00 00 00 00 c0 05 d2 40 00 00 00 00 00 f0 7e 40 
ProxyM2_m2_packets6.log.txt:87015:00 00 00 00 00 00 69 40 1a 00 00 01 00 08 d0 20 ee 01 30 39 f1 cd ab 08 fd 00 00 00 00 c0 5c d5 
ProxyM2_m2_packets6.log.txt:88443:02 03 01 00 08 d0 20 f1 01 30 39 ea cd ab 08 fd 00 00 00 00 80 83 cf 40 00 00 00 00 00 f0 7e 40 
```

## Movement And Object-State Leads

### Mode 06 movement selector 08

Signature: `00 06 08` (spaced or compact hex)

Matches: 108

```text
ProxyM2_m2_packets_5.log.txt:19589:02 03 12 00 06 08 3c 32 f3 c6 00 e0 88 44 a0 c0 0f c5 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 
ProxyM2_m2_packets_5.log.txt:19739:02 03 12 00 06 08 4c 83 f1 c6 00 e0 88 44 a8 ad 0b c5 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 
ProxyM2_m2_packets_5.log.txt:19779:02 03 12 00 06 08 4c 83 f1 c6 00 e0 88 44 a8 ad 0b c5 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 
ProxyM2_m2_packets_5.log.txt:19854:c5 12 00 06 08 4c 83 f1 c6 00 e0 88 44 a8 ad 0b c5 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 
ProxyM2_m2_packets_5.log.txt:19998:02 03 12 00 06 08 4c 83 f1 c6 00 e0 88 44 a8 ad 0b c5 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 
ProxyM2_m2_packets_5.log.txt:20038:02 03 12 00 06 08 4c 83 f1 c6 00 e0 88 44 a8 ad 0b c5 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 
ProxyM2_m2_packets_5.log.txt:20690:82 f3 66 18 48 03 1b 00 01 02 00 16 00 02 80 80 90 b8 13 80 04 02 00 18 00 12 00 06 08 4c 83 f1 
ProxyM2_m2_packets_5.log.txt:28699:40 1f 03 01 00 02 18 00 3b 00 06 08 fa 09 8a 46 00 80 f7 43 6d 03 6c 45 80 80 80 40 2a 0f 01 00 
ProxyM2_m2_packets6.log.txt:26246:02 03 2d 00 01 02 00 19 00 06 08 52 73 21 c6 f8 df ec 44 17 fd 60 47 80 80 80 40 54 06 0f 00 01 
ProxyM2_m2_packets6.log.txt:26772:47 80 80 80 80 80 80 01 07 08 00 00 19 00 06 08 3e ee 1c c6 f8 df ec 44 b9 f5 5f 47 80 80 80 40 
```

### Mode 06 movement selector 0a

Signature: `00 06 0a` (spaced or compact hex)

Matches: 15

```text
ProxyM1_m1_packets4.log.txt:12851:22 00 00 00 00 00 00 00 01 00 0c 0c 00 ae cd ab 2d 9f 25 00 06 0a 00 00 0d 44 65 73 63 61 69 6e 
ProxyM2_m2_packets_5.log.txt:4983:02 03 19 00 06 0a 00 a4 b5 bc c6 68 29 eb 44 00 20 8a c4 ff 00 00 00 10 00 00 00 00 00 01 ff 00 
ProxyM2_m2_packets_5.log.txt:5338:02 03 17 00 06 0a 00 e9 ba bd c6 50 af e5 44 06 e5 07 c5 ff 00 00 00 10 00 00 00 00 00 01 ff 00 
ProxyM2_m2_packets_5.log.txt:20966:02 03 12 00 06 0a 00 4c 83 f1 c6 00 e0 88 44 a8 ad 0b c5 80 80 80 40 1d 00 00 00 
ProxyM2_m2_packets6.log.txt:38528:00 00 04 dd 16 00 03 01 0c 15 80 80 80 82 10 95 00 00 0c 00 00 18 00 11 14 00 06 0a 00 82 50 a0 
ProxyM2_m2_packets.log.txt:4964:00 00 00 7d 00 28 00 00 01 00 0c 0c 00 e9 cd ab 23 9b 25 00 06 0a 00 00 44 65 73 63 61 69 6e 00 
ProxyM2_m2_packets.log.txt:15598:e9 cd ab 23 9b 25 00 06 0a 00 00 44 65 73 63 61 69 6e 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
ProxyM2_m2_packets.log.txt:18311:e9 cd ab 23 9b 25 00 06 0a 00 00 44 65 73 63 61 69 6e 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
ProxyM2_m2_packets.log.txt:46632:1b 00 00 01 00 0c 0c 00 e9 cd ab 23 9b 25 00 06 0a 00 00 44 65 73 63 61 69 6e 00 00 00 00 00 00 
ProxyM2_m2_packets.log.txt:141680:95 07 00 00 00 00 00 00 00 00 06 0a 02 13 80 ba 66 03 00 00 6b 05 00 00 00 00 00 00 00 00 00 00 
```

### Mode 06 movement selector 0c

Signature: `00 06 0c` (spaced or compact hex)

Matches: 1137

```text
ProxyM1_m1_packets.log.txt:2411:82 bb be e0 47 03 13 00 06 0c f6 36 3c 57 47 00 00 be 42 10 8a 5a 46 80 80 80 80 80 80 01 11 08 
ProxyM1_m1_packets4.log.txt:3790:02 03 02 00 02 80 80 80 40 03 14 13 00 06 0c 75 5d ea 10 47 00 00 be 42 cc 82 98 c6 ff 00 00 00 
ProxyM1_m1_packets4.log.txt:3849:02 03 13 00 06 0c 6f 5d ea 10 47 00 00 be 42 cc 82 98 c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00 
ProxyM1_m1_packets4.log.txt:3884:02 03 13 00 06 0c 65 5d ea 10 47 00 00 be 42 cc 82 98 c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00 
ProxyM1_m1_packets4.log.txt:3919:02 03 13 00 06 0c 5b 5d ea 10 47 00 00 be 42 cc 82 98 c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00 
ProxyM1_m1_packets4.log.txt:3924:00 00 22 00 00 00 00 00 0d 00 06 0c 2b 04 fa 14 47 00 00 be 42 00 bc 9b c6 ff 00 00 00 10 00 00 
ProxyM1_m1_packets4.log.txt:3944:02 03 13 00 06 0c 51 5d ea 10 47 00 00 be 42 cc 82 98 c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00 
ProxyM1_m1_packets4.log.txt:3949:00 00 22 00 00 00 00 00 0d 00 06 0c 21 04 fa 14 47 00 00 be 42 00 bc 9b c6 ff 00 00 00 10 00 00 
ProxyM1_m1_packets4.log.txt:3974:82 f3 25 f5 47 03 13 00 06 0c 47 5d ea 10 47 00 00 be 42 cc 82 98 c6 ff 00 00 00 10 00 00 00 00 
ProxyM1_m1_packets4.log.txt:3979:00 00 00 00 00 00 22 00 00 00 00 00 0d 00 06 0c 17 04 fa 14 47 00 00 be 42 00 bc 9b c6 ff 00 00 
```

### Mode 06 movement selector 0e

Signature: `00 06 0e` (spaced or compact hex)

Matches: 1254

```text
ProxyM1_m1_packets4.log.txt:3795:00 00 1f 07 08 00 00 00 00 00 00 22 00 00 00 00 00 0d 00 06 0e 05 45 04 fa 14 47 00 00 be 42 00 
ProxyM1_m1_packets4.log.txt:3854:00 00 22 00 00 00 00 00 0d 00 06 0e 05 3f 04 fa 14 47 00 00 be 42 00 bc 9b c6 ff 00 00 00 10 00 
ProxyM1_m1_packets4.log.txt:3889:00 00 22 00 00 00 00 00 0d 00 06 0e 05 35 04 fa 14 47 00 00 be 42 00 bc 9b c6 ff 00 00 00 10 00 
ProxyM1_m1_packets4.log.txt:4084:02 03 02 00 02 80 80 80 40 1e 14 13 00 06 0e 00 26 5d ea 10 47 00 00 be 42 cc 82 98 c6 ff 00 00 
ProxyM1_m1_packets4.log.txt:4089:08 00 00 1f 07 08 00 00 00 00 00 00 22 00 00 00 00 00 0d 00 06 0e 00 f3 04 fa 14 47 00 00 be 42 
ProxyM1_m1_packets4.log.txt:4114:02 03 13 00 06 0e 00 1b 5d ea 10 47 00 00 be 42 cc 82 98 c6 ff 00 00 00 10 00 00 00 00 00 01 ff 
ProxyM1_m1_packets4.log.txt:4119:00 00 00 22 00 00 00 00 00 0d 00 06 0e 00 eb 04 fa 14 47 00 00 be 42 00 bc 9b c6 ff 00 00 00 10 
ProxyM1_m1_packets4.log.txt:4566:02 03 02 00 02 80 04 1e 11 00 01 02 77 0d 00 06 0e 05 73 6c 3e 12 47 00 00 be 42 9e 42 9b c6 80 
ProxyM1_m1_packets4.log.txt:4588:02 03 0d 00 06 0e 05 73 6c 3e 12 47 00 00 be 42 9e 42 9b c6 ff 00 00 00 10 00 00 00 00 00 01 ff 
ProxyM1_m1_packets4.log.txt:5206:02 03 15 00 06 0e 05 63 6a 24 12 47 00 00 be 42 95 71 91 c6 ff 00 00 00 10 00 00 00 00 00 01 ff 
```

### Repeated mode 06 suffix lead

Signature: `ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00` (spaced or compact hex)

Matches: 943

```text
ProxyM1_m1_packets4.log.txt:3796:bc 9b c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
ProxyM1_m1_packets4.log.txt:4090:00 bc 9b c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
ProxyM1_m1_packets4.log.txt:4177:42 00 bc 9b c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
ProxyM1_m1_packets4.log.txt:4246:00 ef 72 2b 47 00 80 fc c3 8b 04 c6 c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 
ProxyM1_m1_packets4.log.txt:4641:00 10 00 ef 72 2b 47 00 80 fc c3 8b 04 c6 c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 
ProxyM1_m1_packets4.log.txt:4807:02 03 09 00 02 02 01 07 00 06 02 00 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 
ProxyM1_m1_packets4.log.txt:5510:e7 cb 12 47 00 00 be 42 80 9a 9a c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 
ProxyM1_m1_packets4.log.txt:5537:00 00 be 42 80 9a 9a c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 
ProxyM1_m1_packets4.log.txt:5756:be 42 80 9a 9a c6 ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
ProxyM1_m1_packets4.log.txt:5779:ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 
```

### Movement-state ff 01 64 01 tail

Signature: `ff 01 64 01` (spaced or compact hex)

Matches: 0

### Movement-state ff 01 4e 01 tail

Signature: `ff 01 4e 01` (spaced or compact hex)

Matches: 54

```text
ProxyM2_m2_packets6.log.txt:4793:3f 80 46 00 80 f7 43 fb 17 02 45 ff 01 4e 01 00 00 00 00 00 00 52 18 20 06 20 12 d0 82 40 18 a8 
ProxyM2_m2_packets6.log.txt:4864:00 00 37 01 01 0a 3e 84 ca 87 46 00 80 f7 43 04 84 bc 44 0f 01 03 02 05 ff 01 4e 01 00 00 00 00 
ProxyM2_m2_packets6.log.txt:4901:45 0f 01 03 0e 03 12 e9 c1 80 46 00 80 f7 43 35 b0 0a 45 ff 01 4e 01 00 00 00 00 00 00 52 18 20 
ProxyM2_m2_packets6.log.txt:4943:0e 00 12 09 8d 80 46 00 80 f7 43 db 31 07 45 ff 01 4e 01 00 00 00 00 00 00 52 18 20 06 20 12 d0 
ProxyM2_m2_packets6.log.txt:8155:02 03 0f 01 03 0a 00 86 a6 82 46 00 80 f7 43 dc 89 7c 44 ff 01 4e 01 00 00 00 00 00 00 52 18 20 
ProxyM2_m2_packets6.log.txt:8187:02 03 37 01 01 02 05 0f 01 03 0a 00 86 a6 82 46 00 80 f7 43 dc 89 7c 44 ff 01 4e 01 00 00 00 00 
ProxyM2_m2_packets6.log.txt:8221:02 03 0f 01 03 0a 00 86 a6 82 46 00 80 f7 43 dc 89 7c 44 ff 01 4e 01 00 00 00 00 00 00 52 18 20 
ProxyM2_m2_packets6.log.txt:8254:43 56 f4 50 45 ff 01 4e 01 00 00 00 00 00 00 46 34 56 82 08 24 e5 8b 61 13 4a c0 c0 00 00 01 9a 
ProxyM2_m2_packets6.log.txt:8292:46 00 80 f7 43 67 0c 66 45 ff 01 4e 01 00 00 00 00 00 00 46 34 56 82 08 24 e5 8b 61 13 4a c0 c0 
ProxyM2_m2_packets6.log.txt:8347:43 04 7a 6b 45 ff 01 4e 01 00 00 00 00 00 00 46 34 56 82 08 24 e5 8b 61 13 4a c0 c0 00 00 01 9a 
```

## Ability, Effect, And Combat-State Leads

### SERVER_PLAYER_ATTRIBUTE / ability load

Signature: `80 b2` (spaced or compact hex)

Matches: 208

```text
ProxyM1_m1_packets.log.txt:38:08 80 b2 4e 00 1e 00 08 02 08 80 b2 52 00 1e 00 08 02 08 80 b2 54 00 1e 00 08 02 08 80 b2 4f 00 
ProxyM1_m1_packets.log.txt:39:1e 00 08 02 08 80 b2 51 00 12 00 08 02 08 80 b2 11 00 32 00 08 02 16 80 bc 45 03 11 00 00 02 00 
ProxyM1_m1_packets.log.txt:45:00 00 00 76 04 05 00 00 00 00 00 00 00 00 08 80 b2 26 04 00 00 08 02 16 80 bc 45 03 26 04 00 89 
ProxyM1_m1_packets.log.txt:47:00 00 00 00 00 08 80 b2 24 04 00 00 08 02 16 80 bc 45 03 24 04 00 8f 00 00 00 24 04 00 00 00 00 
ProxyM1_m1_packets.log.txt:49:00 0b 00 00 9a 00 00 00 16 04 02 00 00 00 00 00 00 00 00 08 80 b2 25 04 00 00 08 02 16 80 bc 45 
ProxyM1_m1_packets.log.txt:52:00 08 80 b2 27 04 00 00 08 02 16 80 bc 45 03 27 04 00 01 01 00 00 27 04 00 00 00 00 00 00 00 00 
ProxyM1_m1_packets.log.txt:88:00 aa 01 00 00 e5 03 05 00 00 00 00 00 00 00 00 08 80 b2 28 04 00 00 08 02 16 80 bc 45 03 28 04 
ProxyM1_m1_packets.log.txt:261:02 00 00 00 cc 00 0c 00 00 00 01 00 00 00 00 08 80 b2 11 00 32 00 08 02 
ProxyM1_m1_packets.log.txt:302:00 00 00 00 16 80 bc 45 00 09 00 00 89 00 00 00 16 04 02 00 00 00 01 00 00 00 00 08 80 b2 26 04 
ProxyM1_m1_packets.log.txt:306:00 00 00 01 00 00 00 00 08 80 b2 24 04 00 00 08 02 16 80 bc 45 03 24 04 00 8f 00 00 00 24 04 00 
```

### SERVER_ABILITY_UNLOAD

Signature: `80 b3` (spaced or compact hex)

Matches: 186

```text
ProxyM1_m1_packets.log.txt:259:00 00 00 00 00 16 80 bc 56 00 55 00 00 98 b8 00 00 b5 00 29 00 00 00 00 00 00 00 00 04 80 b3 11 
ProxyM1_m1_packets.log.txt:301:00 00 00 00 00 00 00 00 04 80 b3 26 04 16 80 bc 45 03 26 04 00 89 00 00 00 26 04 00 00 00 00 01 
ProxyM1_m1_packets.log.txt:304:bc 55 00 5d 00 00 89 00 00 00 16 04 02 00 00 00 00 00 00 00 00 04 80 b3 24 04 16 80 bc 45 03 24 
ProxyM1_m1_packets.log.txt:308:80 bc 45 00 0b 00 00 9a 00 00 00 16 04 02 00 00 00 01 00 00 00 00 04 80 b3 25 04 16 80 bc 45 03 
ProxyM1_m1_packets.log.txt:312:16 80 bc 45 00 0d 00 00 01 01 00 00 76 04 01 00 00 00 01 00 00 00 00 04 80 b3 27 04 
ProxyM1_m1_packets.log.txt:360:16 80 bc 45 00 18 00 00 aa 01 00 00 e5 03 05 00 00 00 01 00 00 00 00 04 80 b3 28 04 16 80 bc 45 
ProxyM1_m1_packets.log.txt:1444:5d 00 77 00 00 67 03 00 00 00 00 68 03 00 00 01 00 00 00 00 04 80 b3 11 00 16 80 bc 45 03 11 00 
ProxyM1_m1_packets.log.txt:1445:00 02 00 00 00 11 00 32 00 00 00 01 00 00 00 00 04 80 b3 24 04 02 4b 0a 16 80 bc 45 03 24 04 00 
ProxyM1_m1_packets.log.txt:1446:8f 00 00 00 24 04 00 00 00 00 01 00 00 00 00 04 80 b3 25 04 16 80 bc 45 03 25 04 00 9a 00 00 00 
ProxyM1_m1_packets.log.txt:1447:25 04 00 00 00 00 01 00 00 00 00 04 80 b3 26 04 16 80 bc 45 03 26 04 00 89 00 00 00 26 04 00 00 
```

### Object599 adrenaline booster effect id

Signature: `4c 0a 00 28` (spaced or compact hex)

Matches: 747

```text
ProxyM2_m2_packets_5.log.txt:1380:00 c6 01 80 c3 e7 08 00 58 4c 0a 00 28 00 00 00 a8 d5 8f df c0 00 00 00 00 00 1c 91 40 00 00 00 
ProxyM2_m2_packets_5.log.txt:2970:00 00 00 00 00 00 c1 1e 00 00 4c 0a 00 28 ff 06 00 00 00 00 00 00 00 00 00 00 00 e7 08 00 58 37 
ProxyM2_m2_packets_5.log.txt:3048:00 c1 1e 00 00 4c 0a 00 28 ff 06 00 00 00 00 00 00 00 00 00 00 00 e7 08 00 58 37 08 00 00 1f 12 
ProxyM2_m2_packets_5.log.txt:6761:b3 44 0a 6d 2f c5 80 80 80 80 c0 4c 0a 00 28 81 01 02 00 00 10 00 00 00 
ProxyM2_m2_packets_5.log.txt:6784:00 01 00 5a 0a ff 00 00 00 00 00 00 00 00 22 b6 00 00 4c 0a 00 28 ff 01 00 00 00 00 00 00 00 00 
ProxyM2_m2_packets_5.log.txt:6825:00 22 b6 00 00 4c 0a 00 28 ff 01 00 00 00 00 00 00 00 00 00 00 00 9b 00 00 90 37 08 00 00 1f 06 
ProxyM2_m2_packets_5.log.txt:7855:10 00 00 22 d5 08 bd c0 1b 00 c6 01 80 c3 e8 08 00 58 4c 0a 00 28 00 00 00 70 a8 92 dc c0 00 00 
ProxyM2_m2_packets_5.log.txt:7955:00 00 00 00 00 00 00 01 00 d5 04 ff 00 00 00 00 00 00 00 00 c1 1e 00 00 4c 0a 00 28 ff 08 00 00 
ProxyM2_m2_packets_5.log.txt:8168:00 4c 0a 00 28 ff 08 00 00 00 00 00 00 00 00 00 00 00 e7 08 00 58 37 08 00 00 1f 07 08 00 00 00 
ProxyM2_m2_packets_5.log.txt:8188:00 4c 0a 00 28 ff 08 00 00 00 00 00 00 00 00 00 00 00 e7 08 00 58 37 08 00 00 1f 07 08 00 00 00 
```

### Object599 fast healing effect id

Signature: `b5 07 00 28` (spaced or compact hex)

Matches: 0

### Object599 accelerated spawn effect id

Signature: `66 00 00 04` (spaced or compact hex)

Matches: 1134

```text
ProxyM1_m1_packets4.log.txt:13785:40 db 40 b0 bf 60 22 a1 40 00 00 00 40 9d 77 9e c0 ca ff 63 22 fb ff 66 00 00 04 b4 01 01 00 02 
ProxyM1_m1_packets4.log.txt:13866:42 9e c0 d6 ff 63 22 66 00 00 04 b4 01 01 00 02 00 a9 73 d1 5e 07 f0 08 1d d6 1e 00 00 03 11 00 
ProxyM1_m1_packets4.log.txt:13897:00 39 93 b4 d2 40 24 e9 99 88 cb ed a1 40 00 00 00 80 3a 26 9f c0 f7 ff 63 22 fb ff 66 00 00 04 
ProxyM2_m2_packets_5.log.txt:10414:00 00 ff 9b 9d 40 00 00 00 00 84 4e 82 c0 81 ff 63 22 fb ff 66 00 00 04 98 00 57 01 02 01 00 8b 
ProxyM2_m2_packets_5.log.txt:14004:00 1c 91 40 00 00 00 00 86 51 a3 c0 81 ff 63 22 fb ff 66 00 00 04 90 01 00 89 94 75 0d 09 41 1d 
ProxyM2_m2_packets_5.log.txt:15825:66 00 00 04 a0 02 00 89 32 b3 41 09 37 1d 22 b6 00 00 03 81 00 00 00 81 ff 1b 00 00 00 00 
ProxyM2_m2_packets_5.log.txt:26188:11 05 9f 40 81 ff 63 22 fb ff 66 00 00 04 b0 01 00 04 00 a9 8c 97 26 09 e3 0c 1c 01 ee 03 00 00 
ProxyM2_m2_packets_5.log.txt:26245:00 a0 35 d6 9f 40 81 ff 63 22 66 00 00 04 80 89 c6 52 39 09 b1 1d 0e 23 00 00 01 11 00 00 00 62 
ProxyM2_m2_packets_5.log.txt:26253:00 00 00 40 91 c1 ab 40 eb ff 63 12 fb ff 66 00 00 04 98 ff ff ff ff 01 00 a9 7a ad 2a 09 74 0c 
ProxyM2_m2_packets_5.log.txt:26313:40 00 00 00 00 00 f0 7e 40 00 00 00 e0 27 6b a7 40 81 ff 63 22 fb ff 66 00 00 04 98 ff ff ff ff 
```

## Intake Next Steps

- Preserve the original files separately before editing or importing anything.
- Inspect matches with surrounding lines; embedded hits inside encrypted or unrelated bodies are leads only.
- Record sender, provenance, CR1/CR2 status, action context, character handle, coordinates, known object ids, and known item GoIDs.
- Import only reviewed text logs or text-like dumps that clear repository safety review; leave binaries and unrelated game assets out of Git.
