# Cosmetic passive audit

The 29 rows previously marked cosmetic now divide into 14 partial and 15 cosmetic. Thirteen already reached gameplay through existing features. Sixth Sense 149 is the new consumer.

| Existing consumer | Reviewed abilities | Headless evidence |
|---|---|---|
| Typed ability health damage and periodic resistance | Calm Mind/Calm Body 5, Empty Mind 8, Transmit Code 593 | `typed-ability-resistance`, `transmit-code-resistance` |
| Control duration and damage on current target state | Deflect Code 23, Punishing Blows 16 | `deflect-code-duration`, `punishing-blows` |
| Free-cast viral accuracy, PvP entry, interlock and free-fire | Run Program 32, Gaussian Blur 49, Combat Insight 113, Multi-Fighting 140, Sharpshooter 148, Desperation 1149 | `viral-accuracy-bonus`, `combat-insight-engagement`, `multifighting-interlock`, `sharpshooter-freefire`, `desperation-initiative` |
| Ordinary item compile timing | Minor Repairs 24, Code Crafter 515 | `ordinary-ic-compile` |

The audit retains the existing mechanics and their stated evidence limits. The [CSV](ABILITY-PARITY.csv) records the remaining case-specific gaps. Hacker's numeric contest is unreachable after the shipped early return. Uncanny Senses' declared dependency never becomes a detection test in the shipped callback. Code Shaper 35 and 697 target friendly simulacra, whose incoming viral contest is absent. Resist Infection 75 requires a recovered relationship between bonus1055 and direct radius177. Code Crafter's compile timer consumes its timing stat. Its distinct Code Writing Discount is read in `WriteCodeTest`, whose output and cost path remains open.

## Sixth Sense

The [shipped callback](../research/client-scripts/ability/spy/misc.py#L80) scans living character movement objects within 1,500 world units. It reads Sixth Sense Detection 646, draws once from 0–99, skips friends and uses a strict greater-than comparison. The stored level function 2 has scale 4. Original Python 2.3 bytecode confirms the comparison to zero in the defender branch. A zero Stealth target has threshold 50. A nonzero Stealth target has threshold zero. The callback's radius takes precedence over the generic template's 8 m field.

A loaded 149 uses the normal cast and 10 IS charge. Group allies are omitted using the server's mission-team, crew and faction membership. The server's PvP eligibility marks enemies, and other in-range players are neutral. NPCs require a current authorized view. Existing Stealth and view rules continue to govern target selection. Scanned players receive source scan and result notices. The caster receives the sourced FX and ten-second [minimap position record](DATA-MINING.md). This uses the same minimap entry type as map and locator pings. The native binding from `sendPlayerPositions` remains unresolved.

The first native sample printed 453 detections with the malformed header `80 c5 01 00 0a`. The stock constructor `0x6213c1f0` calls `0x62129590`, which reads the array offset from +2, then reads the count at that offset. Offset 1 made it interpret `c5 01` as the count. The shared encoder now places the count at +5. The native retest displayed one detection and one red marker beside the player's arrow in the Mara map panel. The marker was gone by the 12-second observation. The map's default zoom limited visual measurement of the offset. Headless checks verified the 8 m offset and 10 IS charge. A distinct native scan visual remains unverified. [Native retest](https://github.com/kylebrussell/mxo-hd/pull/308#issuecomment-6991689247)

## Native sample

Use NativeQa mode `sixth-sense` at the PR head. The disposable bootstrap checks both live helper positions, both PvP flags, ambient aggro, the one-marker scan, the near helper's discovery and the far helper's exclusion. It writes `sixth-sense-bootstrap.json`. No caster commands or forced roll are required in the real client.

1. Jack in as CSR110 and stay at the seeded point `17000 495 2300`. Press M to open the Mara map panel and activate Sixth Sense from Actions or the hotbar.
2. Check that the client reports one detection and shows one red dot 8 m east for 10 seconds, along with the scan visual. Record the actual output and `80 c5 05 00 0a 01 00 00 08 00 02`.
3. Check 10 IS spent and normal native fixture shutdown.
