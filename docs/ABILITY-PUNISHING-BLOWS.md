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

No native UI result or fresh-server result is claimed by this source checkpoint.
