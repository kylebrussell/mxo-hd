# Combat Insight113 and Gaussian Blur49 — OURS entry scores

This is a labelled **OURS approximation**, not retail parity. A new player interlock against current Evade Combat217 compares deterministic force and evasion scores. Combat Insight's existing loaded stat1048 scales force once; Gaussian Blur's existing direct520+25 increases evasion. Neither is added to accuracy or character HP damage. The retail entry predicate remains unresolved. See the shield meter evidence below for what follows a resisted request.

```
baseForce = max(0,100 + current tactic ForceCombat + direct519)
force = floor(baseForce * (100 + max(0,active1048)) /100)
baseEvade = max(0,100 + current tactic EvadeInterlock + direct520)
evade = floor(baseEvade * max(0,100 + active1049) /100) +10
admit when force >= evade
```

The100 baseline and10 active-evasion margin are explicit OURS normalization. This is not a recovered1048→519 attribute link. At level50, ordinary113 contributes10: Power versus Speed+217 changes100<110 refusal to110==110 admission. Normal49 then raises the target to135, so110 refuses; ordinary Grab with113 gives220 and admits. Current tactics retain their authored direct519/520 values.

The starter Evade fixture exercises Execute Program’s +5 Evade Interlock Bonus (1049) inside this same rule. Against a live shield it raises the baseline 110 to 115 and blocks Force 110. Unloading Execute Program restores equality. With the shield off, a loaded bonus has no effect on entry.

For player-versus-player entry, the check runs after legality, range, ranged-request suppression, same-fight targeting and join-capacity checks, before a new fight mutates combat state. It applies to either client ForcedInterlock flag value. Positive520 alone cannot recreate the gate when217 is off or1125 suppresses it. The target-owned 217 generation and its world and life checks keep resisted requests tied to one shield.

The existing4d forced-entry refusal shape is reused, with no recast timer and explicit OURS score text. Exact localized reason14 wording is a native check, not a newly proven retail semantic.

## Shield meter after a resisted request

Retail view 2 attribute 30 is a byte. The retained Vector_zerooneetc capture shows 255 → 166 → 77 → 0 on reason 14 replies. Vector45 shows 255 → 153 → 51 → 0 and, in another fight, 255 → 127 → 0. The zero records include a 217 switch-off. In each sequence the attempt that broke the shield was still resisted. See Vector_zerooneetc packet starts 62241, 64611, 68213 and 68548, and Vector45 packet starts 10011, 10098, 36622, 36810, 36979 and 38605 under `research/packet-dumps/community-intake`.

The resistance branch publishes the remaining meter to the defender and observers, including observers entering view later. It applies [the shipped shield damage function](../research/client-scripts/combat_calculations.py#L233) to the two levels and active Force Combat Bonus stat 1048. The rounded damage-to-byte conversion assumes a 100-point scale and reproduces the three observed sequences for 35, 40 and 50 damage. The raw pool and exact bonus input were inferred from the captures. Protection ends on depletion or IS shortfall. Disable Evasion suppresses the meter and restores the same remaining value when it expires.

## NPC forced interlock

The original shield sequences include NPC attackers. Vector_zerooneetc binds Corrupted (level 47) to view 0x0a, spawn 0x75. That view's attempts toward self 2/0x5e at lines 64611, 68213 and 68548 arrive with reason 14 and the defender's shield update. Vector45 identifies Zombie Lurch (level 52) at view 0x0d for lines 10011 and 10098 and a later set of Zombie Lurch and Zombie Rot views for lines 36810 and 36979. [Corrupted creation](../research/packet-dumps/community-intake/20260607T205703Z-hdneo-public-vector-supplemental-state-selected/logs/mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_zerooneetc..log.txt#L56524) · [Corrupted attempt](../research/packet-dumps/community-intake/20260607T205703Z-hdneo-public-vector-supplemental-state-selected/logs/mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector_zerooneetc..log.txt#L64611) · [Zombie Lurch creation](../research/packet-dumps/community-intake/20260607T203107Z-hdneo-public-vector-state-static-selected/logs/mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector45.log.txt#L9677) · [Zombie Lurch attempt](../research/packet-dumps/community-intake/20260607T203107Z-hdneo-public-vector-state-static-selected/logs/mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector45.log.txt#L10011)

The NPC path verifies the live attacker and its spawn, then uses the same player-owned shield, defender 4d reason 14 and remaining meter. It uses the actual NPC level for both shipped resource formulas. The entry score uses its modeled timed 519 and 1048 totals with a neutral pre-entry tactic. This is a server policy for the contest input. An NPC takes a resisted attempt from its existing action clock, with the five-second aggro retry interval. This covers direct retaliation and chase arrival as well as the visibility scheduler.

Use NativeQa mode `ability-parity-starter-evade-npc-entry` for the stock check. On the open Richland stand, activate 217 before approaching the level-11 unarmed QA Corrupted. At level 19, expect cost 7 with each paid attempt and meter 255→194→133. The third attempt finds 5 IS, removes 217/624, starts the 30 s timer and still refuses the entry. The next ordinary NPC attack opens close combat.

## Fixture and qualification scope

Discovered case/mode: `combat-insight-engagement`. The normal wire case owns2/17/49/77/113/217 before Margin. It normally unloads113 for the baseline, normally reloads the same owned row, and observes actual1048 add/remove records and durable load flags. The target casts217 and49 normally. Ordinary42 selects Power/Grab; ordinary40 requests entry. Paired normal44 withdrawal ends admitted fights. No buff/state injection, flag/dev command or fixed roll is used. The fixture uses the existing hostile-server level50 entry profile; this setting and seeded levels/loadout are aids.

The retained headless case predates full prerequisite seeding. Its 100/110 threshold remains a historical server-policy comparison. The current NativeQa mode loads every ordinary prerequisite. Execute Program adds +5 Evade Interlock Bonus to the helper, so the live baseline is 115.

## Native checklist

Use the console plan for `combat-insight-engagement`. With Power, 113 changes force from 100 to 110 and the equal-level shield damage from 35 to 38. Both attempts are resisted at evade 115. Grab raises force to 220 and starts an ordinary round without a shield debit. After paired withdrawal, return to Power and have the helper cast Gaussian Blur while Evade remains active. Evade reaches 141, and the last resisted request depletes the shield. Blur remains present and the next Power entry starts an ordinary fight with the shield gone. The fixture uses seeded profiles, ordinary helper self-casts, and disposable Archive load/unload for 113. The original entry formula remains unresolved.

## Qualified results

Runtime source `38934ac` and test-only child `71f73fa882f68f47f6ab17f0f9fbde951059d86f` passed61 focused tests and the full5927-test Release suite, plus two Python controls for old/new49 audit classification. Debug/Release builds, supported read-only plans/build-info and exact case/mode discovery passed. Generated Temp review rows49 and113 are `partial` with the explicit OURS entry label. The conditional212 audit bridge correctly stays `total only` in this source because the separate Punishing Blows257 consumer is absent. No generated snapshot is committed.

One normal wire run passed35.4s/held exit0 at71f73fa8. Mandatory expected source equalled actual launch source SHA256 `C7A0F5701CEE781B3620405C10FAD306F8F880ED8327681702A2FFE8938E861A`, driver `0CC7DF1B47E9A2A19C1FBA296CD30660D735266C2B5428C3821DBC9DBDF057A6`. It normally unloaded/reloaded the same owned113 row and observed1048 removal/add10. The actual four outcomes were100<110 refused,110==110 admitted,110<135 refused after normal49+25/600s, then220>=135 admitted with ordinary Grab. Both admitted fights ended by paired normal44. Parsers were zero for94/67 received RPCs. Normal217/49 casts use this server's existing nonspecial-style policy; native-player Hacker-style autoswitch was not demonstrated.

Raw `mxo-combat-insight-mxotest_run_1_50c90e2913.json` SHA256 `0A9D92DA8FC282CD85C03E573D029029166A3E1E6854189D9E63FBB9BED6BBE0` retains all measurements, wire, owned rows and source association. Held driver43204/server39792 used `mxotest_run_1_50c90e2913`; independent cleanup confirmed COUNT0/all seven recorded creation identities gone. Monitoring is sampled rather than a claim to observe every short-lived process.

The separately authorized actual-mode bootstrap also passed: one own-mode custom hook, normal helper217 cast, source equality, no shared-noop substitution. Runner49632/server55828/schema `mxotest_bootstrap_86bcbc0fc9` were disposed and independently COUNT0/all eleven recorded identities gone. Its summary SHA256 is `6AE50FBF78DC83528AE1B3D3A494DECE9222CA9E4D013B6F4B1D69B89BCEFFE4`. This is fixture-startup evidence, not native UI or a second gameplay run.

The initial focus60 pass/1 fail remains retained: the final accuracy control had incorrectly expected zero after also loading Awakened2, whose existing522+50 was legitimate. The test-only correction holds Awakened constant and asserts113 leaves accuracy unchanged; all four score assertions had already passed. No runtime was changed to satisfy it. Final CI qualifies the published descendant. No native result or retail contest parity is claimed.
