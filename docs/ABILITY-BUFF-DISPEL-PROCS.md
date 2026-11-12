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
