# Deflect Code 23 — OURS control-duration consumer

The existing loaded passive produces Determination Bonus 1047: learned level 50 gives +10. Raw Determination 575 links to it, but the existing script duration formula starts mitigating only above `determination / 5 > casterLevel`. Adding 10 raw points at level 50 would do nothing. The retail bonus coefficient is unresolved; this consumer is explicitly **OURS**.

For a newly applied known hostile control state, after existing caster-duration scaling:

```
rawMitigated = existing Mitigate(seconds, casterLevel, raw575)
result = max(seconds * 0.33, rawMitigated * (1 - clamp(positive1047, 0, 100) / 100))
```

The existing player stat reader retains its 1047 cap; NPC timed totals are bounded by the same catalog maximum. No character base Determination is invented. The total duration never drops below the existing 33% floor. Nonpositive bonus preserves the exact old result.

Known controls are the existing Stun, Mez, Powerless, Root, Blind, Confuse, Enrage, Disarmed, Daze, Off Balance and Staggered state targets. The normal path requires an actual state-kind/state-proc record from an existing hostile UDB0 ability on a nonself target. Friendly buffs, self effects, DOTs, numeric modifiers, unknown targets and immunity timers receive no new shortening. Existing raw575 mitigation of other hostile durations is unchanged.

All three existing mitigation paths consume the bonus: normal TimedEffects application, delayed Sever Artery Daze/Off Balance children, and Reverse Bulldog's terminal Stun. Existing captured world/life/owner callback fences remain; no new holder, global toggle or roll override is introduced. Already active durations retain their snapshots when 23 is loaded/unloaded. Immunity is still calculated from the original state; normal Stun 315 retains its explicit 734→681 immunity duration of 14 seconds.

## Proof setup and limits

Case/mode `deflect-code-duration` uses opposing disposable level-50 players. The defender owns its prerequisite graph before Margin and changes only normal loaded state of 23; every parent row is checked unchanged. The caster's fixed loadout includes Hacker 1006 and Stun 315. This fixture owns those active spells explicitly; it does not prove a normal progression/ability purchase chain for the caster. Normal hostile-server entry and seeded level/abilities are disclosed aids.

Only existing scoped eligible free-cast viral attack/defense draws 32766/0 ensure the normal Stun cast lands. No shared RNG, duration, immunity, cooldown or health is reset. The case reads complete current 80bc state records, expects 7→6.3→7 seconds across normal 23 off/on/off, and waits for actual Stun removal, real 14-second immunity removal and normal own reuse removal before casting again. `?combatdepth stun` is excluded because it removes immunity.

## One-pass native checklist

1. Start mode `deflect-code-duration`, CSR110 only. It starts with 23 owned/unloaded and its parents loaded. QAEnemy has the separate fixed caster loadout and selects Hacker through its ordinary self-cast.
2. Ask QAEnemy to cast normal 315 on the actual CSR handle. Verify the 315→102 state record says 7 seconds, actual Stun expiry, and 734→681 immunity says 14 seconds. Wait for real immunity and own reuse removal before another cast.
3. Load 23 through ordinary Hardline/Loading Area UI; return normally. Verify sourced 23→1047 +10. Repeat the same normal cast: 6.3 seconds Stun, still 14 seconds immunity. Keep parents unchanged.
4. After real expiry, unload 23 normally, return and repeat: 7 seconds again. Observe actual expiry and ordinary IS cost/reuse; do not remove immunity or use a control-state command.

Helper request shapes: `{"actor":"QAEnemy","action":"cast","target":"QAEnemy","ability":1006}` and `{"actor":"QAEnemy","action":"cast","target":"CSR_Tester","ability":315}`; use the actual native handle. Native UI/rendering and the original retail coefficient are not proven by source/unit/headless tests.
