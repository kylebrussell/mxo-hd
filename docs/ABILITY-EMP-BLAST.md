# EMCP Blast1025: conditional NPC executor (partial)

`general.py:157` starts damage at0, tests the target's `hasAbility[NPCEMPWeaknessAbility]`, then uses5000 and calls `dealAbilityDamage(..., Neutral, True)`. Catalog marker1026 is now an explicit per-NPC server grant. Names, caster ownership and ability request parameters never confer it. Activating1026 is refused. No production NPC receives the marker by default.

The stored1025 template has CastDirectObjects4, radius10, cast1s, recast30s, ExecutionFX0x28000a43, TestFunction2, MemoryCost1 and no stored IS cost or RequiresTool. The ordinary cast callback now evaluates visible living hostile same-district NPCs around the caster, using the existing selector4/same-floor area policy also used by Reactive Code942. Marked targets lose fixed neutral5000 without a damage roll, caster influence or resistance multiplier, through ordinary NPC HP/death/loot/reward handling. Ordinary targets lose0 and do not enter combat from a zero hit. Player subjects cannot own the NPC marker and take0. Existing cast/cooldown/control/lifecycle processing remains authoritative.

## Source ownership and limits

Read-only reproduction:

```powershell
python research/abilities/extract_emp_blast.py --client C:/Users/russe/Developer/mxo-client/MxO --output research/abilities/emp_blast_source.json
```

The [snapshot](../research/abilities/emp_blast_source.json) includes GOB SHA256, decoded1025/1026/item43046 templates and all raw byte-aligned references in every indexed ordinary/class record. Marker1026 occurs only in its own AbilityID. No production NPC grant owner is established. Server-side mission/event grants, indirect grants or runtime grants absent from this client GOB remain possible.

Item43046 `EMPDevice` (EMCP Device) has TriggeredAbility/ReqAbilityID1025, level1, IsAConsumable1, ExecuteRuleID10001, CantBeCrafted/CantBeDecompiled1 and Complexity35. This proves a consumable references1025; it does not prove item-only use or prohibit legitimately loaded1025. No invented item-only restriction is added. Item activation/consumption remains absent.

Remaining gaps: production marker grants/eligibility, TestFunction2/native target filtering, consumable source/wire contract, omitted engine IS default (existing server fallback20 remains), exact zero-damage output and native visuals. Target-hit FX currently reuses stored ExecutionFX, a server presentation choice requiring native comparison. The executor is partial coverage, not completed parity.

## Disposable fixture

`?combatdepth empweakness on|off [HP]` changes only the selected living hostile NPC. Optional positive HP explicitly sets fixture max/current HP. Exact MXO_DEV_COMMANDS=1 and database prefix mxotest_ are checked before lookup/mutation. No production caller-selected marker is accepted. `?combatdepth cast 1025` uses the ordinary ability handler.

Fresh scenario `ability-emp-blast` checks unmarked0, refusal of marker activation,30s cooldown denial, marked6000 to1000 and1000 to0, HP/FX packets and protocol parsing. Units cover target-specific grant/removal, unrelated grants, exact fixture gates and stored timing/selector values.

Claude-only NativeQa mode `emp-blast` owns2,17,1025 and starts at the normal NPC fixture. Read-only `--fixture-plan emp-blast` must report dev1 before setup. The worker never executes setup, opens the native client, takes its lock or submits broker requests. The PR contains the pinned-head one-pass native checklist.
