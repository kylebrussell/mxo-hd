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

## Qualification and retained results (2026-11-15)

The runtime checkpoint is `2df5d961814f5fa38c0f0e4a7583b4a52c9b5c26`, based on master `08d961eb47810f754d3ab4cd55cd23f7dcc8b897`. Full Release: 5,931 tests passed; focused: 114 passed. The QA-only current-cast cursor correction at `6e6f8324e0168f78582c59197f93899597c6c45f` passed 115 focused tests and the Release build; runtime/data trees are identical. Actual Debug/Release builds, both plans/build-info, exact discovery and the Temp-only audit bundle passed. Row 23 is partial with a conditional OURS consumer label. No generated audit snapshots are committed.

The normal case passed in 79.1 seconds with held driver exit code 0. It observed complete 315→102 records with durations 7 / 6.3 / 7 seconds for ordinary 23 off / on / unloaded. Each cast independently retained its 734→681 immunity at 14 seconds. Real exact-record Stun removal, immunity removal and own reuse removal were required before another cast; no timer removal command or sleep substitution was used. The current-cast RPC cursors were 38 / 50 / 63, and parent SQL rows were asserted unchanged across normal memory requests. Caster 67 RPCs / 21 view lists and target 74 RPCs / 7 view lists had zero parser problems. This proves the declared scoped-control fixture, not native UI or the retail coefficient.

Actual selected Debug HDS SHA256: `ECCB172DA2368D4575624310D647089EB2E1B30C17FED9E7EE5392E3EBBB38D9`; Release driver: `DC8B9BED2AC1774A395C311E96A518DD5C803D4D95144A22BFC1E728DC9DE431`; Release runner: `BCAD19652B57B7AEAF2FFC93F870F31D7E09AA6672AB4AE8F09C0EED6F78A009`. Raw game proof: `%TEMP%/mxo-deflect-code-mxotest_run_1_7efcab2e0f.json`, SHA256 `7149043B8D38CCED11B66D4375235053F1C37219FC23E8F0ADE3B47B6B35B1FF`. Driver 21940 / server 51092; exact schema count 0 and all 9 observed process creation identities gone at 20:55:37Z. Descendant polling is sampled, not an exhaustive OS process history.

The explicit actual custom-mode bootstrap passed with the same source: runner 55448 / server 52064, schema `mxotest_bootstrap_f28d8c9f4e`. It ran the real QAEnemy login and normal Hacker-style hook, not a shared no-op. Held exit code 0; exact schema count 0 / all 11 observed identities gone at 20:56:42Z. Proof root: `%TEMP%/mxo-deflect-code-duration-20261115`, including `final-proof-summary.json` and both independently checked cleanup files. This bootstrap proves seed/authentication/hook execution, not native rendering or a second control-duration run.

The first normal case remains FAIL 29.4 seconds: baseline Stun was correctly 7 seconds, but a sequential wait consumed past the earlier immunity record. The producer deliberately emits immunity first. The corrected oracle uses only the fresh pre-cast absolute RPC window and matches each expiry to the original complete record with only its removal flag changed. Failed source `2df5d961` DLL bytes and raw proof are archived; its exact schema was absent and all 7 observed identities gone. Two earlier C# syntax compile failures ran zero tests and are retained separately. Neither correction changed gameplay or reset immunity/reuse, and no automatic game retry was used.