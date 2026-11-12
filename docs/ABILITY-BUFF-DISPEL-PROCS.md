# Buff dispel callback probabilities

Machinegun Fist Combo 261's shipped StatModifier stores `WhenEnabled=2`, `StatModInitFunc=RemoveRandomBuff` and `ProcTriggerChance=0.2` in `data/ability_catalog.json`. The callback itself is `research/client-scripts/tempmods.py:342`: it chooses one eligible buff parent, removes its records and deactivates its toggle. The prior server landing callback ran unconditionally, so every landed 261 removed an upgrade and every NPC hit emitted the no-upgrade failure line.

`AbilityHostile` now checks each positive dispel callback's explicit probability before invoking it. The check uses the existing `TimedEffects.Apply` probability convention (`NextDouble() < chance`), with absent chance defaulting to 1. Both PvP removal and the current NPC no-buff failure output are qualified. Delete/Cancel Upgrade callbacks without an explicit probability keep their unconditional behavior. UpgradeSweepPulse's separate per-candidate Value% roll is preserved.

This does not establish retail RNG order, seed or exact cross-modifier correlation. It adds no meaning for unknown ProcTriggerFlags. NPCs still have no positive buff executor here; their removal branch continues to report failure only when the callback triggers. General linked-parent/item/equipping provenance is not reconstructed by this change; the existing player candidate/removal model remains the boundary.

Validation uses deterministic rolls below/at/above 0.2, default chance callbacks, untouched sibling buff/debuff, aura deactivation and later application. Fresh `buff-dispel-proc-pvp` and `buff-dispel-proc-npc` use actual 80 b9 requests with bounded retries and no production RNG override. Existing hostile-fields scenarios cover guaranteed removal and hostile field behavior.

## Claude-only native checklist (pending)

- Use the exact PR head and read-only NativeQa build-info provenance before any native fixture work.
- From real Actions/radial menus, use Machinegun Fist Combo with the required Kung-Fu stance against a PvP-legal player with removable upgrades. Observe successful landed attacks that retain upgrades and eventual triggered removal. Verify the removed record/toggle disappears while sibling effects remain.
- Confirm ordinary Delete/Cancel Upgrade still removes eligible upgrades and reports no upgrades when empty. Exercise a toggled aura and verify it stays off until explicitly activated again.
- Against an NPC, observe261 hits without spurious no-upgrade lines on every hit; a triggered callback may report no upgrade under the documented NPC policy.
- Native visuals, exact proc rate and retail RNG sequence are unverified. Codex has not operated the real client or created a native fixture.

## Deterministic Claude-only native fixture (2026-11-12)

The discovered `buff-dispel` and `buff-dispel-miss` modes use the generic mode API, with loaded2/17/133/261/49/327/42/420 and automatically owned unloaded discipline ancestors. Both seed copied110 and helpers at10000HP/2000IS with legal opposing organizations/PvP. Bootstrap logs in helpers, then sends real ability RPCs to QAEnemy for Gaussian Blur49, Avoidance327 and Combat Aura42. Ready is withheld until both upgrade on-records, the aura toggle on-record and both695 child on-records arrive. `buff-dispel-ready.json` stores those five actual acknowledgements; generic state metadata names the target, IDs, record count and requested proc roll. It returns an empty legacy shield acknowledgement, so it does not claim StealthShield readiness.

`buff-dispel` requests callback roll0; `buff-dispel-miss` requests99. These use the existing `MXO_CALLBACK_QA_ROLL100` fixture path only when dev commands equal exactly1 and the server database begins exactly `mxotest_`, with integer0..99. Invalid/absent values retain the original Random object and NextDouble production path; explicit caller RNG remains authoritative. The fixed random implements NextDouble as roll/100 only inside this guarded fixture. No hit contest, damage roll, production probability or global RNG sequence is overridden. Forced rolls establish fixture behavior, not a retail probability sample.

Claude's deterministic pass:

1. Build the exact pushed head in the intended configuration. Read-only `--fixture-plan buff-dispel` must show dev1, roll100=0, no GM/health-hold, actor/helper abilities and metadata. `--build-info <root> 2,17,133,261,49,327,42,420` records the actual source SHA and discipline ancestors. Only Claude starts NativeQa/client. Wait for state Ready and inspect the five acknowledgements in `buff-dispel-ready.json`.
2. Select QAEnemy in the actual native UI. Use loaded Machinegun Fist Combo261 with Kung-Fu133 through Actions/radial controls. Observe a normal landed damaging hit: Gaussian Blur49 is removed, while Avoidance327 and Combat Aura42 remain. Ordinary combat can still miss; forced callback probability does not force the hit contest.
3. In the same roll0 fixture, use loaded Delete Upgrade2 (420) on QAEnemy through the UI. Its sourced20% sweep is deterministically successful with roll0, removing remaining eligible parents and deactivating42. Verify its off record/toggle and no695 child reapplication after another pulse interval. This avoids the random candidate-last policy and any90s upgrade cooldown retry; no new aura-removal policy is introduced.
4. End that fixture through the established Claude lifecycle. Start a fresh `buff-dispel-miss` profile, whose ordinary bootstrap restores all upgrades/aura. A normal landed damaging261 hit now preserves49,327 and42 because callback roll99 fails the stored0.2 test. Native Ready metadata reports99; both fixture profiles are separate disposable sessions, not a runtime RNG command.

Codex exercises the shared actual bootstrap method in ordinary ephemeral-port HeadlessClient scenarios, never NativeQa setup or its fixed ports. Native observation remains pending Claude; these acknowledgements and forced cases alone are not native PASS.
