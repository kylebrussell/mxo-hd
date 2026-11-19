# Timed buff replacement and native values

The [stock session on PR333](https://github.com/kylebrussell/mxo-hd/pull/333#issuecomment-7007872279) ran head `55f1b05b`. Packet offsets below refer to `mxo-headless/0940f9af648d/PacketLog.txt`, filtered to the CSR's own server messages. The original capture stays with native QA.

Combat Training raised Melee Accuracy by 64. When Weapons Boost landed, the server sent a removal for Combat Training's original modifier record `0x13e`, followed by the Weapons Boost add in the same packet (lines 7747–7750). Melee Accuracy returned to baseline and Ballistic Accuracy rose by 64. Weapons Boost expired naturally at 300 seconds (line 10815).

Bolster Health and Upgrade Health shared one active modifier. Each replacement sent the previous record off before the next add. The server published the resulting maximum through the own-view Max Health attribute:

| Ability | Record | Max HP | Capture lines |
| --- | --- | ---: | --- |
| Bolster Health 1.0 (39) | `0x144` | 6,250 (+50) | 9837–9844 |
| Bolster Health 2.0 (322) | `0x145` | 6,400 (+200) | 10056–10064 |
| Bolster Health 3.0 (419) | `0x146` | 6,700 (+500) | 10342–10350 |
| Upgrade Health (417) | `0x147` | 6,700 (+500) | 10540–10543 |

Upgrade Health replaced Bolster Health 3.0 at the same maximum, so the server had no new own-view maximum to publish. The prior own-view maximum and both modifier records show the retained 6,700 maximum. The capture shows later casts replacing earlier ones in family 3 even when the incoming catalog priority is lower. It does not establish priority ordering for other buff families.

Firearms Skill showed +25 Ballistic Damage for 20 seconds. Its baseline cast debited 20 IS (line 6738). With Efficiency, the same cast debited 17 IS (lines 8226–8257) and Repair RSI debited 9 IS (lines 8410–8452). Efficiency's +15% cost reduction and both of Enhanced Dodge's +47 defenses returned to baseline at expiry.

The server previously suppressed its debug message when it replaced a buff. The replacement now logs both ability IDs. Packet order, cooldowns and modifier behavior follow the existing paths. The replacement tests replay the stock ability sequence and verify the active server stats, original record IDs and final expiry.
