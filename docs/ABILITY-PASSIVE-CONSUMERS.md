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

A loaded 149 uses the normal cast and 10 IS charge. Group allies are omitted using the server's mission-team, crew and faction membership. The server's PvP eligibility marks enemies, and other in-range players are neutral. NPCs require a current authorized view. The scan does not change Stealth or expose hidden characters as a selectable world view. Scanned players receive source scan and result notices. The caster sees the sourced FX and ten-second [minimap position record](DATA-MINING.md). The native binding from `sendPlayerPositions` and stock client display at this call site remain pending. This uses the same minimap entry type as map and locator pings.

## Native sample

Use NativeQa mode `sixth-sense` at the PR head. The disposable bootstrap checks both live helper positions, both PvP flags, ambient aggro, the one-marker scan, the near helper's discovery and the far helper's exclusion. It writes `sixth-sense-bootstrap.json`. No caster commands or forced roll are required in the real client.

1. Jack in as CSR110 and stay at the seeded point `17000 495 2300`. Open the minimap and activate Sixth Sense once from Actions.
2. Look for one red dot 8 m east for 10 seconds and a scan visual. QAFar at 16 m should have no dot. Record the actual output and `80 c5 01 00 0a 00 08 00 02`.
3. Check 10 IS spent and normal native fixture shutdown.
