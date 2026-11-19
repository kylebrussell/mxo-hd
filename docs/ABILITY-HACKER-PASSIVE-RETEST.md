# Hacker parent and passive stock retest

Run `ability-parity-hacker-passive-retest` as level-50 CSR110. One 99-memory profile owns and loads all five samples with their required prerequisites. Capture initial Actions and Status before changing it.

| ID | Owned Archive entry | Observe removal on unload and return on reload |
| --- | --- | --- |
| 310 | Arbalest | Granted Arbalest Upgrade (1083), +1 raw Viral Damage Bonus |
| 334 | Ballista | Granted Ballista Upgrade (1084), +5 raw Viral Damage Bonus |
| 335 | Artillerist | Granted Artillerist Upgrade (1086) |
| 390 | Skript Kiddie | Granted Skript Kiddie Upgrade (1079), +2 raw Melee Defense Bonus and +2 raw Ballistic Defense Bonus |
| 398 | Repel Combat | +5 raw Melee Defense Bonus, +5 raw Ballistic Defense Bonus and +15 Powerless Duration Bonus |

For each row in order, use `?qaunload <id>` and then `?qaload <id>`. Wait for the action or modifier records to return before testing the next row. Keep the ordinary load acknowledgements, the parent-sourced add/remove records and the before/after Actions and Status. The supplied commands exercise the normal owned Archive operation through the disposable fixture.

The four already-tested Hacker Upgrade activations from PR #365 need no recasts for this retest. These five rows remain open until the matching stock observations are recorded.
