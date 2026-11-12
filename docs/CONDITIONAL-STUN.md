# Dazed-dependent throw stuns

Subduing Throw 347 and Paralyzing Throw 1135 now execute the shipped `ApplyStunEffectIfDazed` callback for NPC and player targets. Subduing Throw previously replayed a captured four-second stun on every target, bypassing the condition; Paralyzing Throw omitted the callback entirely. Both remain partial abilities: this change does not resolve their generic damage model or claim native visual acceptance.

## Source contract

The catalog's `Stun` (347) and `StunIfDazed` (1135) modifiers have `WhenEnabled=2`, fixed values 4 and 8, `DurationType=0`, `Duration=.01`, and **StatModTermFunc** `ApplyStunEffectIfDazed`. There is no level value function or scale. The callback is a term callback, not an immediate hit proc.

[`tempmods.py:2368`](../research/client-scripts/tempmods.py#L2368) tests the subject's current `ConditionStateFlags & ST_DAZED` and calls [`ApplyStunWithTimer:1659`](../research/client-scripts/tempmods.py#L1659). That helper refuses an existing `ControlImmunityTimerAbility -> ISDrainTargetAbility` pair. Otherwise it directly adds a Stun state for `float(tempMod.Value)` and a separate control-immunity modifier for the same value. It does not remove Dazed, replace sibling root/debuff modifiers, or give this stun a health-break callback. These direct script durations are kept as four/eight seconds; direct script calls are not reinterpreted as level-scaled catalog duration functions.

The marker now enters the normal tempmod holder and resolves at term. The shared target condition reader includes both ordinary tactic flags and timed Dazed state. Its successful callback appends the stun to the original parent's other targets, and adds the independent 734/681 timer. Thus Paralyzing Throw keeps its root/debuffs, and breaking/removing its parent does not remove the independent timer. Explicit removal, refresh and health-break termination can run the callback; death/world cleanup clears markers without creating new states. No stun occurs on an undazed target, after Dazed expires, during control immunity, or on a dead subject.

The existing server tick is .25 seconds, so a .01-second marker resolves on the first due tick. The Dazed test occurs then, and the stun gets its full duration from that execution time. Exact native scheduler timing and early-removal callback presentation remain unverified.

## Checks and native checklist

`ConditionalControlTests` covers both values, condition arrival after application, condition expiry before term, existing immunity, death, root preservation, independent immunity, explicit termination and actual NPC tactic flags/attack gating. Existing timed-effect and stealth-terminal tests verify the shared lifecycle still behaves as before. The headless `conditional-dazed-stun` scenario uses normal player casts: an undazed Subduing Throw produces no stun, then a fixture-owned NPC Daze 884 establishes Dazed and the next throw gives the four-second stun/timer and blocks the victim's actions. The fixture grant of 884 is test setup, not a claim that players may normally load that NPC ability.

Claude only: use the working radial/item menus and the exact PR commit. Establish Dazed through a Speed-tactic combat condition while the target has no control timer. Land Subduing Throw: expect four seconds of stun and four seconds of independent immunity. Repeat against an undazed target: damage/other procs may land, but no stun/timer. For Paralyzing Throw in its required Spy Style/interlock context, expect eight seconds only while Dazed; the original root/debuff siblings must remain. Confirm the target cannot act during stun, the on/off modifier records match, damage can break the existing root as before without deleting the independent timer, and death/re-entry leaves no pending marker. Record packet bytes, screenshots and full build SHA. Validation: 31 focused unit tests pass; the fresh-database headless scenario passes in 37.3 seconds with parallelism one. AbilityAudit and build_audit reproduce all three generated outputs exactly after UTF-8/CRLF normalization.

Native results are pending; no client, live fixture or production database was changed by this implementation.
