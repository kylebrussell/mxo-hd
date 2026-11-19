# Martial hotbar target conditions

The [PR321 stock retest](https://github.com/kylebrussell/mxo-hd/pull/321#issuecomment-7001527488) saw these moves disabled with their ancestors loaded and styles active. Each move's shipped [ability catalog](../data/ability_catalog.json) entry requires a condition on the target.

| Move | Active style | Required mask | Target condition | Tactic that can apply it |
| --- | --- | --- | --- | --- |
| Head Butt (197) | Self Defense (17) | `0x02` | Dazed | Speed |
| Tomo Nage (412) | Aikido (101) | `0x04` | Off Balance | Grab |
| Piston Kicks (262) | Kung-Fu (133) | `0x08` | Staggered | Power |

In stock 7.6005 `client.dll`, `0x62517630` reads `CastRequiredStates` into the output mask. Its eligibility caller at `0x625c1da0` treats a zero mask as success for this check. For a nonzero mask, it reads the target snapshot and at `0x625c1dff` through `0x625c1e03` checks `(flags & required) == required`. The failure branch records the missing bits. The constant registration at `0x6269ef26` through `0x6269efb2` binds `ST_DAZED`, `ST_OFFBALANCE` and `ST_STAGGERED` to 2, 4 and 8. Same-style controls Cheap Shot (198) and Punch Reversal (407) have no `CastRequiredStates` entry.

The [NPC view update](../hds/world/Structures/Mob.cs) already sends these flags when [landed rounds apply a tactic condition](../hds/world/Structures/CombatInterlock.cs) or [a timed effect applies the same state](../hds/world/Structures/TimedEffects.cs). The disposable modes reuse the existing selected-target commands for controlled retries. The `martial-hotbar-conditions` headless scenario checks the exact selected NPC flag bytes on application and their subsequent clearance for each command.

## Stock retest

1. Use `style-choreography` for Head Butt and `ability-sweep-martial-artist` for Piston Kicks and Tomo Nage. Interlock the isolated Melee Dummy, select the named style, and check the slot with adequate IS and clear personal and family reuse. Punch Reversal provides an Aikido control before applying Off Balance.
2. Use the indicated tactic until the dummy shows the required condition. For a controlled retry with the dummy selected in the active interlock, use `?combatdepth daze 30`, `?combatdepth offbalance 30`, or `?combatdepth stagger 30` for the matching move. Keep the same target and style. Confirm the slot enables and an ordinary click sends the full `80 b9` request.
3. Record the landed move IDs 284, 751 and 8085. Repeat with the state allowed to expire before clicking, with reuse clear, to check that the slot disables again. A combat miss is separate from hotbar eligibility.
