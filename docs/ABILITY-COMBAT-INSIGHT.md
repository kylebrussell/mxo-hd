# Combat Insight113 and Gaussian Blur49 — OURS entry scores

This is a labelled **OURS approximation**, not retail parity. A new player interlock against current Evade Combat217 compares deterministic force and evasion scores. Combat Insight's existing loaded stat1048 scales force once; Gaussian Blur's existing direct520+25 increases evasion. Neither is added to accuracy, health damage or shield HP. The retail forced-entry caller/probability and shield-health baseline remain unrecovered.

```
baseForce = max(0,100 + current tactic ForceCombat + direct519)
force = floor(baseForce * (100 + max(0,active1048)) /100)
baseEvade = max(0,100 + current tactic EvadeInterlock + direct520)
evade = floor(baseEvade * max(0,100 + active1049) /100) +10
admit when force >= evade
```

The100 baseline and10 active-evasion margin are explicit OURS normalization. This is not a recovered1048→519 attribute link. At level50, ordinary113 contributes10: Power versus Speed+217 changes100<110 refusal to110==110 admission. Normal49 then raises the target to135, so110 refuses; ordinary Grab with113 gives220 and admits. Current tactics retain their authored direct519/520 values.

The starter Evade fixture exercises Execute Program’s +5 Evade Interlock Bonus (1049) inside this same rule. Against a live shield it raises the baseline 110 to 115 and blocks Force 110. Unloading Execute Program restores equality. With the shield off, a loaded bonus has no effect on entry.

The check runs after existing PvP legality, range, ranged-request suppression, same-fight targeting and join-capacity checks, before a new fight mutates combat state. It applies to either client ForcedInterlock flag value. Non-evasion targets, existing interlocks/joins and ranged fire retain their behavior. Positive520 alone cannot recreate the gate when217 is off or1125 suppresses it. The existing target-owned217 generation/world/life state is reused; no new meter, sticky flag, timer, cost or RNG draw is introduced. Existing admitted-round RNG is unchanged. Player-to-NPC and NPC-initiated entry are outside this first consumer because those actors lack the same217 ownership capability.

The existing4d forced-entry refusal shape is reused, with no recast timer and explicit OURS score text. Exact localized reason14 wording is a native check, not a newly proven retail semantic.

## Fixture and qualification scope

Discovered case/mode: `combat-insight-engagement`. The normal wire case owns2/17/49/77/113/217 before Margin. It normally unloads113 for the baseline, normally reloads the same owned row, and observes actual1048 add/remove records and durable load flags. The target casts217 and49 normally. Ordinary42 selects Power/Grab; ordinary40 requests entry. Paired normal44 withdrawal ends admitted fights. No buff/state injection, flag/dev command or fixed roll is used. The fixture uses the existing hostile-server level50 entry profile; this setting and seeded levels/loadout are aids.

The headless memory request uses the normal80ae handler; the native control must use Hardline/Loading Area UI. The runtime only changes new player engagement against current217. No general evasion, PvE, shield-damage or retail-chance proof is claimed. Audit labels are conditional on the actual consumer's presence and remain `partial`, never complete parity. No generated coverage snapshot is committed.

## One-pass native checklist

1. Use mode `combat-insight-engagement`, CSR110 only. QAEnemy has normally cast217 during fixture startup; verify its visible shield/state. Native113 begins owned but unloaded. Choose Power, select QAEnemy and request ordinary interlock: expect force100/evade110 refusal and no combat round. Capture the client's refusal wording.
2. Use ordinary Hardline/Loading Area memory UI to load owned Combat Insight113. Return normally to the same helper; verify sourced113→1048+10. With Power, request entry:110==110 admits. Inspect normal combat presentation. Withdraw, and issue helper request `{"actor":"QAEnemy","action":"stop"}` so both ordinary withdrawal intents end the fight.
3. Request the helper's ordinary self-cast: `{"actor":"QAEnemy","action":"cast","target":"QAEnemy","ability":49}`. Verify its actual49→520+25/600s record and normal cast/cost. Keep217 on. Power+113 should now refuse110<135.
4. Select Grab normally. The next request should admit220>=135. End with paired ordinary withdrawal again. Optionally unload113 at a Hardline and confirm its sourced removal. No cooldown reset or RNG control.

## Qualified results

Runtime source `38934ac` and test-only child `71f73fa882f68f47f6ab17f0f9fbde951059d86f` passed61 focused tests and the full5927-test Release suite, plus two Python controls for old/new49 audit classification. Debug/Release builds, supported read-only plans/build-info and exact case/mode discovery passed. Generated Temp review rows49 and113 are `partial` with the explicit OURS entry label. The conditional212 audit bridge correctly stays `total only` in this source because the separate Punishing Blows257 consumer is absent. No generated snapshot is committed.

One normal wire run passed35.4s/held exit0 at71f73fa8. Mandatory expected source equalled actual launch source SHA256 `C7A0F5701CEE781B3620405C10FAD306F8F880ED8327681702A2FFE8938E861A`, driver `0CC7DF1B47E9A2A19C1FBA296CD30660D735266C2B5428C3821DBC9DBDF057A6`. It normally unloaded/reloaded the same owned113 row and observed1048 removal/add10. The actual four outcomes were100<110 refused,110==110 admitted,110<135 refused after normal49+25/600s, then220>=135 admitted with ordinary Grab. Both admitted fights ended by paired normal44. Parsers were zero for94/67 received RPCs. Normal217/49 casts use this server's existing nonspecial-style policy; native-player Hacker-style autoswitch was not demonstrated.

Raw `mxo-combat-insight-mxotest_run_1_50c90e2913.json` SHA256 `0A9D92DA8FC282CD85C03E573D029029166A3E1E6854189D9E63FBB9BED6BBE0` retains all measurements, wire, owned rows and source association. Held driver43204/server39792 used `mxotest_run_1_50c90e2913`; independent cleanup confirmed COUNT0/all seven recorded creation identities gone. Monitoring is sampled rather than a claim to observe every short-lived process.

The separately authorized actual-mode bootstrap also passed: one own-mode custom hook, normal helper217 cast, source equality, no shared-noop substitution. Runner49632/server55828/schema `mxotest_bootstrap_86bcbc0fc9` were disposed and independently COUNT0/all eleven recorded identities gone. Its summary SHA256 is `6AE50FBF78DC83528AE1B3D3A494DECE9222CA9E4D013B6F4B1D69B89BCEFFE4`. This is fixture-startup evidence, not native UI or a second gameplay run.

The initial focus60 pass/1 fail remains retained: the final accuracy control had incorrectly expected zero after also loading Awakened2, whose existing522+50 was legitimate. The test-only correction holds Awakened constant and asserts113 leaves accuracy unchanged; all four score assertions had already passed. No runtime was changed to satisfy it. Final CI qualifies the published descendant. No native result or retail contest parity is claimed.
