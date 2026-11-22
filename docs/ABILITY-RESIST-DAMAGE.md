# Resist Damage and Sweep Bonus

Stat 127 applies to friendly area targeting. Stat 1052 adds chance points to ordinary timed sweep casts.

## Sweep Bonus

The shipped `NegativeConditionSweepPulse` callback draws `random.randrange(100)` for each eligible debuff and removes the debuff when `roll < TempMod.Value`. The ordinary templates give Negative Condition Sweep 1.0/2.0 (320/424) base chances of 18%/35%, and Sweep Party 1.0/2.0 (379/487) base chances of 20%/30%. The catalog defines stat 1052 as a separate bonus produced by Repair and Upgrade (71), Dedication (370), and other passives. Its stored attribute link concerns Perception. The archive leaves the original calculation connecting 1052 to the callback value open. [Shipped pulse callback](../research/client-scripts/tempmods.py#L182)

The server adds the caster's signed Sweep Bonus once as percentage points at application or refresh, clamps the pulse threshold to 0–100, and publishes the original effect value and timer. Existing pulses keep their threshold after either actor changes loadout. Items, the separate instant removal callback, and class or event sweeps retain their existing behavior.

The `ability-parity-starter-hacker-sweep-bonus` fixture uses +1 from rank-19 Repair and Upgrade and +3 from rank-30 Dedication, with fixed +10 and +5 from prerequisite sources 70 and 74. Rank 30 satisfies the acquired Corpsman prerequisites. With both tested passives, just 370, just 71, and neither, the stock 320 cast uses thresholds 37, 36, 34, and 33 against a fixed draw of 33. The last case leaves an actual helper Slow active until natural expiry. The isolated bootstrap verifies both outdoor NAV stands, legal Archives, the four 1052 records, unloading the two tested passives independently, and the retained stock helper Slow frame. The direct executable tests verify the pulse result. The first complete stock 320 request is a native capture item.

Native PR676 verifies Repair and Upgrade's heal 8→9 and both source records. PR755 verifies Dedication's healing, two defense, and Sweep Bonus records, as well as the Patcher healing change. The direct tests also isolate each passive's healing and combat defense consumers. [Repair and Upgrade result](https://github.com/kylebrussell/mxo-hd/pull/676#issuecomment-7065776933) [Dedication result](https://github.com/kylebrussell/mxo-hd/pull/755#issuecomment-7081754805)

## Primary evidence

Read-only client.dll SHA256 `cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a`; reproduce with `python research/abilities/inspect_resist_damage_radius.py <client.dll>`.

Native62515e40 reads `Ability/StatusCategory`;62515ef0 compares all four bytes to UBF0 through625361e0. This is an exact status-category predicate, not the inventory category. Caster method625c5500 calls base-radius getter62519100, then reads stat127 through625c4e90 with arguments1,1,0. That stat reader returns its stored signed total plus modifiers through625c47a0. The radius caller sign-extends AX, divides100 and stores binary32 at625c558d. It returns base radius multiplied by1 plus that fraction. No new linked attribute or negative clamp is inferred. UDB0/stat177 is a separate branch and is excluded here.

Base62519100 reads byte RadiusOfEffect and multiplies100.0 at628706a8. Native UI621ed033 uses the caster method when a character is present and divides100 for metres. The simpler tooltip621e2435 uses the base getter, so the Affects string alone would not prove a caster bonus. Existing server selectors accept metres: the shared helper applies the same dimensionless percentage without another factor100.

74's stored level function 2 and binary32 scale 0.481 produce stat 127 +24 at learned rank 50. Its second modifier produces stat 1052 +5.

## Scope and checks

Only existing support group targeting and health-callback friendly944 use this consumer. Other categories retain their old radius; no hostile177 implementation, aura expansion or sibling-specific ability effect is added. Existing target eligibility, team membership, vertical tolerance, cast authority and cooldowns remain in place.

Unit checks cover loaded74, alias, unloaded restoration, exact friendly selector, hostile bypass and6/8/9m boundaries. The fresh `resist-damage-radius` scenario casts ordinary Group Repairs3.0/371 before and after loading owned74 at50: base7m reaches6m, excludes8m; expanded8.68m reaches8m and excludes9m. It executes the exact shared native-fixture SQL builder on its disposable actors and checks coordinates/vitals. No RNG override is used.

## Claude-only native checklist

After CI, use the normal NativeQa CLI on the exact pinned PR head with mode `resist-damage`, a new QA directory and matching Debug build. Codex has not run native setup. Mode loads2,17,74,371,77, starts CSR_Tester at17000,2300; level50/IS2000. QAFriend is6m east, QAFar8m east, QAOther9m east, QAEnemy10m east, all atY495/Z2300, alignment1 and no PVP flag. Helpers have100/5000 HP. Dev commands are enabled solely for the existing health reset; no GM, fixed roll or health hold.

Wait for the actual user to enter world, then create the existing `helpers` trigger. For QAFriend/QAFar/QAOther send a separate `requests/*.json` file such as `{"actor":"QAFriend","action":"invite","target":"CSR_Tester"}` and accept each real team invite in the native client. Verify team membership; coordinates alone do not make a teammate. QAEnemy stays outside the team.

Before EACH baseline/loaded case, send existing helper chat requests for all three teammates, e.g. `{"actor":"QAFriend","action":"chat","text":"?health 100"}`. Wait for responses and actual health records; natural regeneration otherwise hides healing. No health hold is introduced. Unload74 through the ordinary ability organizer, verify its passive off record, then cast371 normally:6m teammate heals,8m/9m do not. Wait normal recast, load owned74 at50, verify stat127+24 record, reset helper health again and cast371 normally:8m now heals,9m does not. Record actual cast/effect/health records, not merely request Ok. Optionally unload again to verify restoration. Observe radius UI separately; native presentation remains unverified until observed.
