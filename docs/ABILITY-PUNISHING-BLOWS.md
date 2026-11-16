# Punishing Blows16 — OURS approximation

The existing active `DamageAgainstVulnerability` stat212 is a percent bonus against a living target currently Dazed, Stunned or Off Balance. Daze/Off Balance combat flags also qualify. Roots, Staggered, ordinary healthy targets and dead targets do not. Each hit uses the current state; there is no cached vulnerability flag. Damage is `floor(raw * (100 + max(0, active212)) / 100)`, saturated to the packet range. Ability16 already supplies15; this consumer adds no extra base15.

This is deliberately **OURS**, not retail parity. Original bytecode uses an exploited-vulnerability counter whose lifecycle is unrecovered. The modern control-state interpretation is an explicit playability choice. The existing activation, ownership/load checks, cost, cooldown, toggle and1IS/s drain are unchanged.

The consumer runs before damage presentation in ordinary NPC/PvP gunfire and interlock exchanges, typed direct ability damage and DOT ticks, hostile aura health pulses and supported health-callback damage children. Existing caller eligibility and asynchronous owner/world/life fences remain authoritative. Fixed neutral EMP Blast damage and developer damage probes are excluded. NPC attackers do not acquire a new ability/stat producer. No combat RNG is overridden.

## Validation and aids

The `punishing-blows` normal-wire case seeds level50 loaded abilities2/17/16 and an ordinary rifle692. Two opposing disposable actors use ordinary PvP firing and normal ability16 activation/toggle. It checks each sampled pre-bonus number against independent integer arithmetic and the received8168 damage field. Target state is prepared by the existing strict disposable `?combatdepth offbalance` control; the dedicated mode enables that existing feature flag. No target ability is granted and no hit/damage roll is fixed. Unit controls cover Daze, Stun, Off Balance, expiry/removal, root/Staggered exclusions, dead/world-changing targets and the actual16 drain shortfall.

## One-pass native checklist

Use discovered mode `punishing-blows`, CSR110 only in the disposable fixture. Keep the original character and ordinary UI input throughout.

1. Equip the seeded rifle using inventory, flag PvP normally, select visible QAEnemy and fire normally with16 off. Record received/logged damage.
2. Stop firing. Activate Punishing Blows16 through Actions. Verify its normal stat212+15 record and ordinary1IS/s drain. Fire at the healthy target: no vulnerability bonus.
3. Stop firing. With the exact current target selected, use `?combatdepth offbalance 30`, then fire normally. Compare each captured raw/final pair: final is exactly the integer15% result; verify the same damage number in the client log. This state preparation is an aid, not a retail ability cast.
4. Stop firing and allow the state to expire naturally. Fire again: no bonus. Toggle16 off normally; confirm its removal and that drain stops. No cooldown reset.

## Qualified results

Runtime source `df9be2778bdf6be84ab5f2b248a745514424c744` passed110 focused tests and the full5931-test Release suite. Debug and Release builds and exact case/mode discovery passed. The QA-only child `8a664b93ae0eec9ba0317d13693038607644bfb3` changes only the scenario's natural-expiry barrier; its110-test focus passed and its runtime/data trees are unchanged. Final CI qualifies the published head.

The normal `punishing-blows` run passed63.4s with held driver exit0 against actual Debug source SHA256 `D350202F51AF78430D504CA7900BFDA6F88464AD17DC96BECC3FB982127F897D`, Release driver `EDCF396FA15F1578A86056DD8BD0BD88AAE27CC39A97755D2BF8948D57D0BCC3`. Received8168 numbers were87→87 with16 off/healthy,87→87 with16 active/healthy,87→100 with16 active/OffBalance,87→87 after actual natural885 removal while16 remained active, and87→87 against newly prepared OffBalance after normal16 deactivation. Both parsers had zero problems. This live case proves ordinary PvP rifle behavior; NPC/interlock/ability paths are source and unit qualified, not separately live demonstrated here. Archive unload was not exercised live.

Raw `mxo-punishing-blows-mxotest_run_1_b1d4b00d0a.json` SHA256 `881520015EC9E665CD26AD1AE41449F87D57FC807BEA82684DA9C01CA61E4508` retains the wire, measurements and source association. Driver58324/server33316 used schema `mxotest_run_1_b1d4b00d0a`; independent cleanup confirmed COUNT0 and all six recorded creation identities gone. Descendant monitoring is sampled, not a claim to observe every short-lived process.

The explicit actual-mode bootstrap passed separately at runtime-identical `df9be277`: one custom helper hook, own mode seed/auth, actual D350 source, no shared baseline substitution. Runner51520/server27408/schema `mxotest_bootstrap_7494870906` were disposed; independent COUNT0/all ten recorded identities gone. This is fixture-startup evidence, not native UI or a second gameplay run. Its hook/loadout source was unchanged by the QA child.

Retained failures: initial synthetic liveness fixtures108 pass/2 fail (fixed by initializing their real liveness timestamp); first wire run35.8s failed because its immediate second16 request hit the existing0.5s duplicate guard, after three correct wire measurements. The child waits for actual target state expiry rather than altering that guard. An incorrect `--plan` CLI invocation also entered setup and failed before seeding/server startup; the transient lock was absent and the native-ability disposable schema family count was zero afterward. No client or main-character reset path was reached. Correct read-only plans use `--fixture-plan`.

No native UI result or retail-counter parity is claimed.
