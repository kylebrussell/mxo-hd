# Concentration628: damage cast-through (partial)

Concentration628 supplies the stored +30 Concentration564 passive. Actual player damage now consumes that stat while a normal timed cast has an **explicit stored IsInterruptable=1**. This is a bounded implementation, not a claim that every retail cast or damage event is reproduced. The supported actual executors are Insidious Code360 (4s), Fumble Field361 (2s), White Noise362 (2s), and Concealment Grant1137 (2s). Jackout216 also stores1 but has no normal ability executor here. Missing and false flags remain untouched.

## Primary evidence

Client `client.dll` SHA256: `cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a` (7.6005). Addresses below are image virtual addresses. [combat_calculations.py](../research/client-scripts/combat_calculations.py#L274), DamageInterruptsCasting, computes:

```text
fraction = float(damageAmount / maxHealth)
chance = clamp(.75 + float(concentrationValue)*.005 - fraction*.75, .33, .9)
resisted = random.randrange(1,100) <= int(chance*100)
```

Original Monolith.ltp combat_calculations.pyc is Python2.3, flags0x43 (no future division). Function bytecode9..18 is `7c00007c010015830100`: LOAD_FAST damage, LOAD_FAST maxHealth, BINARY_DIVIDE21, CALL_FUNCTION float. The division occurs before float(). Native wrapper6258d960 reads damage i32, maxHealth u16, maxHealthBuff i16, character level u8, Concentration i16; its format628d83c0 is `iiiii`. Bridge623e8110 i-case623e6d65 calls pythonMXO.dll!PyInt_FromLong through6286874c at623e6d6f. Therefore the quotient is Python2 integer division, not a damage percentage calculated with floating-point division. maxHealthBuff and character level are unused by this script. Rolls are1..99 inclusive. A sub-maximal hit at Concentration0 gives75%; +30 gives90% after the cap.

The wrapper references function object62b0afb4 (name628d864c; init62823720). Bounded direct call and address-xref searches found no caller to6258d960 in the shipped client. The live server-side caller and complete attribute input construction were not recovered. This implementation qualifies only actual PvP damage, mob damage to a player, and player DOT ticks. It uses actual positive HP loss, current maximum HP, and available564/1090 modifier totals narrowed to the native signed16 input. Those event/input choices are **server policy inference**; complete base character attributes and native caller ordering remain unknown. HP cap clamps, buff expiry, admin writes, regen, heals and zero loss do not roll.

Native helper62516d90 reads IsInterruptable through621ce010. Generic getter622ffce0 zero-fills outside object storage, but correspondence between native storage length and archive stored_size is not fully established. The catalog has357 timed templates with omitted flags; these are deliberately excluded. The explicit-known subset above avoids guessing that default.

Cancel constructor6213ad10 allocates7 bytes: wire `80 ad`, canonical ability uint32 at2, reason byte at6. ClientShell62175b1c reads that reason and dispatches through6217f198. Reason2 maps via62175b60 to string600000eb (damage); ViewCasting6205ebb0 displays5a00002a (Interrupted). Other table entries1/3/4/5 are movement/combat/interact/another ability. Candidate81aa/620ae840 is an interrupted-completion callback, not this cancel packet. Native80ac's existing u16 ID plus zero padding is consistent with canonical uint32 layout.

Reproduce read-only using mxo-research/tools/client-data `ltp.py` plus `py23marshal.py` for the original function, and Temp/mxo-combat-inspect.py dis/calls/xref for these virtual addresses. The archive bytecode and native typed bridge are both required: decompiled Python alone hides integer-division semantics.

## Cast ownership and resource policy

All normal delayed branches now share one generation/context guard and one completion action. Cancellation invalidates every pending effect and completion cooldown. Replacement casts, reentry, world/life transitions and action-control changes cannot commit an old cast. An old token cannot reset a replacement item stamp or reservation. Ordinary IS remains paid at cast start, matching the existing cost policy; no unsupported refund is inferred. Item consumption remains in its existing independently stamped completion path.

## Deterministic fixture and checks

Discovered modes `concentration-control` and `concentration` load2,17,360,133,261; only the second loads628. Both use4s Insidious Code, opposing PvP QAEnemy,1000HP/2000IS, and a dedicated interrupt roll85. `MXO_CAST_INTERRUPT_QA_ROLL` accepts1..99 only when dev commands are exactly1 and database starts exactly `mxotest_`. Explicit injected RNG wins. It never overrides combat, hit, damage, callback or health rolls; generic callback forcing is disabled. Read-only `--fixture-plan` exposes these values before setup.

Fresh disposable wire scenarios cast360, observe actual80ac, and400ms later send ordinary80b9 ability261 from the enemy. Control observed actual71damage, through75/roll85, exact `80 ad 68 01 00 00 02`, no later360 modifier or completion cooldown. Loaded observed90damage, Concentration30/through90/roll85, actual360 modifier landing and no cancel. The existing ability-parity-support fresh regression passed. These are server/wire checks; native UI remains pending Claude.

### Native fixture repair after the blocked66ae67f check

Claude's native check [6833005064](https://github.com/kylebrussell/mxo-hd/pull/144#issuecomment-6833005064) verified360 casts/lands normally, but helper261 queued for a later interlock round and ordinary combat did not reliably damage the actor during the four-second window. The earlier headless helper cases remain valid server checks; they did not establish that the native timing procedure was usable.

The repaired profiles enable only `MXO_CONCENTRATION_QA_DAMAGE=1`. New `?combatdepth hurtme 100 next` **arms before casting**: it waits for the next explicitly interruptible normal cast, binds its exact token, then applies100 damage once after1s through ordinary `PvpCombat.Damage`. Arming alone does not damage. It requires exact dev1, an actual configured valid `mxotest_` database, the mode enable, current authenticated UDP/in-world/live registered caller, non-LA/non-transition state, and an identity other than0/2/TheNeo. Amounts are integer1..500 and strictly less than currentHP at arm and fire. Data/world/life/district/character or cast-token changes, cancellation, replacement, lost authorization and completed casts invalidate it. It targets only the caller; no arbitrary victim, heal, stat or combat/RNG override is added. Explicit interrupt-only roll85 remains unchanged. Profile/readiness metadata exposes command, amount, delay and cast target.

Claude-only one-pass checklist, each on a fresh profile:

1. Start `concentration-control`, wait concentration-ready.json, select QAEnemy and run `?pvpflag on` before testing. Keep the helper idle; do not depend on an interlock attack.
2. Type `?combatdepth hurtme 100 next` and wait for the armed acknowledgement. There is no four-second typing deadline: the arm waits for the cast.
3. Use actual normal Insidious Code360 from Actions, or the already verified `?combatdepth cast 360` command through the same handler, with QAEnemy selected. Observe the native four-second cast bar. Real self damage arrives after1s; through75/roll85 must send damage-reason80ad and interrupt, with no later360 effect/completion cooldown. IS paid at cast start remains paid.
4. Repeat on fresh `concentration` profile, verify628 +30 record. The same arm100/roll85 now uses through90: health decreases, the cast survives and actual360 effects/cooldown complete. Check native bar/reason/packet presentation and no parser errors. A consumed arm cannot fire on a later cast; arm again deliberately only after ordinary cast/cooldown rules permit.

The new fresh armed-fixture scenario uses this exact chat arm, deliberately waits before sending ordinary80b9/360, then observes real100 damage, control cancellation or loaded landing and wire framing. Native repair validation remains Claude-only; no new native PASS is claimed by Codex.
