# UI Lag1.0 caster-centred recipients (partial)

UI Lag97 now enumerates hostile recipients around the caster once at ordinary cast completion. A selected enemy does not move the centre; no selected enemy is required. This change applies only to97. It preserves ordinary ownership/loadout, IS12, cast2s, recast58s, cast generation/world/life/control guards, existing per-target mitigation/stacking/expiry and the merged normal free-cast viral contest.

## Primary source and limits

Client DLL SHA256 cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a. Native62515c90 reads CastDirectObjects. UI621e2424 switches through621e26c8; entry4 at621e26d8 selects621e25c2, which pushes STA7a0001cb Enemies and7a0001ce Affects: %z within %Im of self. Raw97 stores selector4, CastValidTarget0 and RadiusOfEffect3. Its description7c0000d3 independently says enemies near you. This is positive centre/category evidence; the complete retail recipient scheduler and relationships are not recovered.

The current recipient holder retains captured97 records: thrown accuracy1032 -10, viral defence183 -17, thrown defence1011 -12, viral accuracy342 -14, each27s. Raw fixed template values are all -10; this package preserves the existing captured values, rather than inventing a scaling formula. Retail morpheuzzz_00109:45:41 contains parent97 HIT/infection and those four records. One victim capture does not prove the full recipient list or centre snapshot time.

Completion-time centre, existing same-district live/visible NPC and legal-player predicates, less-than150 world-unit vertical separation, 3D radius and NPC friendly/pet exclusion remain explicit server policy. No geometric line-of-sight engine, retail alliance reconstruction, pet targeting or cross-world admission is added. Caster radius uses the existing UDB0/177 radius calculation without changing its math. Per-recipient landing invokes the existing free-cast342/183 contest: modeled totals, modern flags, neutral NPC tactic and 64-bit significand policy remain the documented partial approximation from ABILITY-FREECAST-VIRAL.md. Deflection precedes every modifier, damage and proc.

This is not a persistent field or aura. Existing StaticField694 through parent89, EMP1025, pet detonation1109 through parent34, Reactive Code942 and Miasma701 remain separate executors.38 and47 are not enabled;47's description conflicts with its stored selector. No ability row is promoted to complete.

The legacy97 HitFx0x2800059c association remains unproven and unchanged. Shipped generic_free_attacks.py183 and ltfxmap.py31 assign that FX to generic deflection, including other viral abilities.97's raw modifier ActivationFX0x280000f2 alone does not establish a replacement wire presentation. Native effect appearance remains pending separate evidence.

## Native one-pass checklist (Claude only)

Use discovered ui-lag-area-hit and then ui-lag-area-deflect profiles, CSR_Tester110 only. Helpers are headless and eagerly enter the world. Ordinary PvP-server level50 auto-flagging remains authoritative. General callback, health and combat RNG are unchanged; only the existing strict disposable/owned free-cast contest draws are selected. No GM authority is enabled.

1. In hit profile, use `?qaload 97` to refresh the already owned fixture ability through the normal80ae load path. At17000/495/2300, clear selection and use UI Lag1.0 from Actions. QAEnemy at17200/495/2300 is inside2m; QAFar17301 is outside3.01m; QAFriend17100 is same-org; QAOther17100/695 is the explicit vertical exclusion control. Same-floor positions have shipped NAV floor400/standing495. The elevated control is a QA placement, not a proved walkable upper platform.
2. Only QAEnemy should receive the four27s modifiers. Read actual [FREECAST-VIRAL] and [HOSTILE][AREA-QA] decisions; do not infer native icon correctness solely from server output. Verify expiry. HP stays unchanged because97 is a debuff.
3. Wait for ordinary cooldown before another use. Select QAFar and cast again: nearby QAEnemy remains the recipient, while QAFar does not move the area centre or receive the effect. No second native client or helper-issued cast is required.
4. In deflect profile, refresh owned97 similarly and cast with no target. QAEnemy should deflect with no97 modifiers, HP damage or on-hit callbacks. Keep generic FX appearance findings separate from recipient/effect correctness.

Headless cases ui-lag-area-hit/deflect additionally use ordinary no-target casts beside a real NPC and preserve every received RPC/03 list, actual selected DLL path/configuration/hash and runtime log before teardown, including failure. They test the selected-distant-target control using a second independently owned caster, without bypassing cooldown. Native rendering and click/UI acceptance remain pending Claude. Validation results will be recorded after final checks and authorized fresh execution.

## Validation checkpoint

Initial full4,486 passed, then final full4,492 passed after per-recipient caster generation guards and additional exclusion coverage. Python12, both actual NativeQa builds/plans/provenance and ignored all-module audit plus exact deterministic CRLF three-artifact roundtrip passed. Tracked historical snapshots and CSV are unchanged; current ignored inventory retains27 cosmetic/1 missing.

Initial source84d929057f6060ad69fee6a48ecb0c861a97c331 fresh hit33.6s and deflect27.7s both passed, driver exit0, peak1, total61.4s, with normal disposal. Actual selected server was Debug8CF3DA39611976D406648D51849418D37F2018E037B708AD9D03BAAFCA585683. Unique preteardown hit JSON mxo-ui-lag-hit-mxotest_run_1_0053850cd8-wire.json SHA256d318c8119a9f893e94846d11b2258451fc79332eafbc492d6b6fc0befb35106f; deflect JSON mxo-ui-lag-deflect-mxotest_run_2_a916525ca3-wire.json SHA2564238d96e28be1a166225ecea8ab5276e33b57cdc75886ebe5a4438545075f016. Both include every RPC/03 list and runtime log; no native rendering claim.

Final guard follow-up captures world/life before reading the centre, rechecks after reading it and before enumeration, and includes Alive/ordinary state gates for every recipient. Four actual registry-lock barrier units invalidate world, life, Alive or pending state while enumeration is blocked, then prove no result or effect escapes.87 scoped tests pass. Final fresh results will be recorded separately because this guard changes runtime; the first proof is not relabelled as testing the new guard.

Two earlier unit-fixture failures are retained in Temp: NPC-hit setup lacked Store.world; a dead-NPC exclusion setup changed HP without setting the real Mob.isDead flag. Corrected test setup passes; no production workaround was introduced for either mistake.

Final guard source7a3ba10f6352812b33a416c2f733716af5ceea8d fresh hit30.8s and deflect37.1s both passed, driver exit0,68.0s total, peak1. Both servers/databases disposed normally. Actual selected Debug DLL5326C1CA19E6C2929C57333821760B5ECDE9500C6B9CFEE8B199B74BDAFD5DF6. Preteardown hit mxo-ui-lag-hit-mxotest_run_1_c9caaeefbe-wire.json54,590bytes SHA256c8d79220de55030a6b87a07d7b8bae990d79f2eb88c75812e66b6db241567a80; deflect mxo-ui-lag-deflect-mxotest_run_2_0da53a7f3d-wire.json52,423bytes SHA2569863da3ff7629ec49879f84e16cfffe78d5de86a17d3772c4bab2ec75165a176. The received near-player records contain two successful normal casts, each preserving all four captured values/durations, followed by actual timed removal; excluded recipients have none. Source-world/life barrier proof remains unit evidence, not a claimed concurrent world-change wire exercise.

Pure upstream master188fb2df courier import leaves all four tested runtime blobs byte-identical. Scoped checks and both actual read-only profiles/provenance are repeated after that import; full4,492 and final68.0s fresh are retained without an unrelated rerun. Native Claude checks remain pending.
