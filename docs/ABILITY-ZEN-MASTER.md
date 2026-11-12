# Zen Master871 ->872

The shipped GOB parent871 (`ZenMasterAbility`, schema4324) stores `StatModifier/WhenEnabled=0`, `DurationType=2`, `StatModHealthChangedFunc=ApplyAuraEffectOnHealthChange`, MinValue/MaxValue872. `research/client-scripts/tempmods.py:1327` unconditionally calls `subject.AbilityInv.reapplyAbility(tempMod.Value)` without filtering damage, attacks, misses or healing. This recovers the previously missed automatic caller.

Child872 (`ZenMasterEffectAbility`, schema4325) stores WhenEnabled2, DurationType1, CurrentInnerStrength111, Min/Max5, ProcTriggerChance0.15, ActivationFX0x28000a79 and ActivationStringOnSelf0xc6002120. Its stored AbilityID decodes871; that duplicate alone does not establish a wire alias. There is no TargetAbilityGrant. Direct client872 use is refused before costs/cooldowns.

Parent871 requires character level99, above retail50. Normal load prerequisite refusal stays authoritative. Production loadouts are not given871. The `zen-master` NativeQa mode deliberately forces copied CSR110 to99 in a disposable fixture; this is not a retail progression claim.

The permanent listener derives from the owned loaded-ability snapshot. Loading/unloading increments its generation; pending callbacks validate that generation plus world/life. The existing256-callback/once-per-owner-parent synchronous transaction bound applies to nested health effects. Removed/dead/duel-dead listeners do not execute. Integer roll0..99<15 uses System.Random. This probability mapping and bounded delivery policy are ours; retail RNG sequence and fatal-event ordering remain unverified. IS is capped at maximum; the sourced line and FX accompany successful rolls.

Validation: fresh `ability-zen-master` tests damage, external healing, sourced activation, capped IS, direct872 refusal and death cleanup. Units additionally test14/15 chance boundaries, exact+5, zero delta, no parent, unloaded/world-reloaded parent, child-only ownership, nested healing once, dead and duel-dead. Natural health regeneration is a genuine external health event and may itself proc. Native acceptance remains pending Claude.

## Claude-only pinned-head checklist

1. At the exact PR fullSHA, build matching Debug/Release. Read-only `NativeQa --fixture-plan zen-master` must report MXO_DEV_COMMANDS=1 and both callback rolls0. `--build-info <root> <head> zen-master 2,17,871` must match source/snapshot hashes and list owned unloaded ancestors.
2. Only Claude starts a fresh disposable `zen-master` fixture and native client. Confirm copied CSR110 level99,871 loaded,100/200IS. This forced state tests an inaccessible parent, not retail progression. Main data remain unchanged.
3. Login. `?health 90` then `?health 120`: actual health changes produce sourced restore line/FX and5IS up to200; ordinary IS/HP regeneration continues separately. Exact delta is covered by units.
4. Direct872 must refuse without cost or effect. Unload871 and change health again: no871->872 proc. Death/world transition must discard old-generation pending delivery.
5. Separately normal-load871 as a level50 disposable actor: level99 prerequisite refusal, no loaded871/hidden872. Follow established Claude stop/reset/lock-release lifecycle. Record unavailable/skipped checks honestly.
