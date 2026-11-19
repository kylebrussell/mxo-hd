# Martial Artist passive and grant stock retest

Run `ability-parity-martial-passive-retest` on level-50 CSR110. The owned and loaded profile uses 59 of 100 memory. Capture the initial Status and Actions lists.

| ID | Archive entry | Source-specific changes on unload and reload |
| --- | --- | --- |
| 136 | Martial Arts Expertise | Raw Melee Accuracy Bonus +5 and Melee Damage Bonus +5 |
| 137 | Martial Arts Initiate | Initiative Bonus +2 and granted Martial Arts Initiate Upgrade (1062) |
| 257 | Kung Fu Master | Initiative Bonus +1, Melee Damage Bonus +1 and granted Kung Fu Master Upgrade (1063) |
| 258 | Kung Fu Proficiency | Max Health Bonus +5%, Melee Accuracy Bonus +7 and Melee Damage Bonus +3 |
| 264 | Karate Expertise | Max Health Bonus +5%, Melee Accuracy Bonus +7 and Melee Damage Bonus +3 |
| 269 | Aikido Prowess | Max Health Bonus +15%, Melee Accuracy Bonus +5 and Melee Damage Bonus +3 |
| 431 | Kung Fu Perfection | Melee Damage Bonus +1, Melee Defense Bonus +10, Stun Duration Bonus +10 and Viral Defense Bonus +6 |

For each row, enter `?qaunload <id>` and then `?qaload <id>`. Let each reload complete before testing the next row. The disposable commands submit the ordinary owned Archive operations. Save both loadout acknowledgements, each source-specific modifier removal and add, and the Status changes. Where a stat is aggregated or omitted, keep its raw PacketLog record. For 137 and 257, also capture the granted action disappearing and returning in its parent section of Actions.

After both children return, select **Martial Passive Target** and activate each granted entry from the hotbar in order. Martial Arts Initiate Upgrade adds +70 max HP, +4 melee accuracy, +2 melee damage and +4 melee defense to the caster. Kung Fu Master Upgrade removes those sourced records and applies +90 max HP, +6 melee accuracy, +2 melee damage and +4 melee defense to the caster. The children are absent from the owned Archive. Preserve the two actual outgoing requests and the replacement records.

The seven parent and passive rows remain stock candidates. This fixture leaves the Zen Master admission question outside its scope.
