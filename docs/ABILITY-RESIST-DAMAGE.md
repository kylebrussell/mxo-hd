# Resist Damage74: friendly radius consumer

This partial implementation connects the existing loaded stat127 to friendly area targeting. SweepBonus1052 remains a recorded total without a proven consumer; no sweep chance is changed. Native presentation is unverified.

## Primary evidence

Read-only client.dll SHA256 `cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a`; reproduce with `python research/abilities/inspect_resist_damage_radius.py <client.dll>`.

Native62515e40 reads `Ability/StatusCategory`;62515ef0 compares all four bytes to UBF0 through625361e0. This is an exact status-category predicate, not the inventory category. Caster method625c5500 calls base-radius getter62519100, then reads stat127 through625c4e90 with arguments1,1,0. That stat reader returns its stored signed total plus modifiers through625c47a0. The radius caller sign-extends AX, divides100 and stores binary32 at625c558d. It returns base radius multiplied by1 plus that fraction. No new linked attribute or negative clamp is inferred. UDB0/stat177 is a separate branch and is excluded here.

Base62519100 reads byte RadiusOfEffect and multiplies100.0 at628706a8. Native UI621ed033 uses the caster method when a character is present and divides100 for metres. The simpler tooltip621e2435 uses the base getter, so the Affects string alone would not prove a caster bonus. Existing server selectors accept metres: the shared helper applies the same dimensionless percentage without another factor100.

74's stored level function2 and binary32 scale0.481 produce stat127+24 at learned50; its second modifier produces stat1052+5. The latter's relationship to TempMod.Value is unresolved. NegativeConditionSweepPulse compares its roll to TempMod.Value, which does not by itself establish an additive1052 bonus.

## Scope and checks

Only existing support group targeting and health-callback friendly944 use this consumer. Other categories retain their old radius; no hostile177 implementation, aura expansion or sibling-specific ability effect is added. Existing target eligibility, team membership, vertical tolerance, cast authority and cooldowns remain in place.

Unit checks cover loaded74, alias, unloaded restoration, exact friendly selector, hostile bypass and6/8/9m boundaries. The fresh `resist-damage-radius` scenario casts ordinary Group Repairs3.0/371 before and after loading owned74 at50: base7m reaches6m, excludes8m; expanded8.68m reaches8m and excludes9m. It executes the exact shared native-fixture SQL builder on its disposable actors and checks coordinates/vitals. No RNG override is used.

## Claude-only native checklist

After CI, use the normal NativeQa CLI on the exact pinned PR head with mode `resist-damage`, a new QA directory and matching Debug build. Codex has not run native setup. Mode loads2,17,74,371,77, starts CSR_Tester at17000,2300; level50/IS2000. QAFriend is6m east, QAFar8m east, QAOther9m east, QAEnemy10m east, all atY495/Z2300, alignment1 and no PVP flag. Helpers have100/5000 HP. Dev commands are enabled solely for the existing health reset; no GM, fixed roll or health hold.

Wait for the actual user to enter world, then create the existing `helpers` trigger. For QAFriend/QAFar/QAOther send a separate `requests/*.json` file such as `{"actor":"QAFriend","action":"invite","target":"CSR_Tester"}` and accept each real team invite in the native client. Verify team membership; coordinates alone do not make a teammate. QAEnemy stays outside the team.

Before EACH baseline/loaded case, send existing helper chat requests for all three teammates, e.g. `{"actor":"QAFriend","action":"chat","text":"?health 100"}`. Wait for responses and actual health records; natural regeneration otherwise hides healing. No health hold is introduced. Unload74 through the ordinary ability organizer, verify its passive off record, then cast371 normally:6m teammate heals,8m/9m do not. Wait normal recast, load owned74 at50, verify stat127+24 record, reset helper health again and cast371 normally:8m now heals,9m does not. Record actual cast/effect/health records, not merely request Ok. Optionally unload again to verify restoration. Observe radius UI separately; native presentation remains unverified until observed.
