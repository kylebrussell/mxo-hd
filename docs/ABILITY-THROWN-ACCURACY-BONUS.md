# Thrown Accuracy Bonus 1145: partial consumer

Primary client 6251fe10 selects 1032 for TestFunction 4; the catalog 1032 links 1145. Weak Points 164 already produces 10 at learned 50, Wicked Aim 278 produces 15, Master Knife Thrower 338 fixed 5. These producers/caps/lifetimes are preserved. The retail linked-owner coefficient is unrecovered. **OURS** applies the existing melee/ranged/Viral1147 convention: separately capped direct 1032 plus trunc(characterLevel*capped1145/100), once. Thus 164 adds 5 attack-base points at character 50, 278 adds 7, 338 adds 2. General 163, Blind, raw caps, other TFs, ordinary attacks, escapes, costs and RNG draws are unchanged. Existing 164 damage/defence producers remain separate.

The shared SpecialAttack path reaches normal queued NPC/PvP special rounds. Positive-cast/free thrown moves retain their current admission and damage path; this change does not invent a new free-cast contest. No full retail formula claim.

Case/mode `thrown-accuracy-bonus` uses ordinary Spy Style 1112 and one paid queued Leg Sweep 134 per off/on/off stage, keeping the full 17-ability prerequisite loadout fixed except 164. Normal helper Block prevents fixture damage pressure without injecting a control state or overriding RNG. Actual 1032 style value 66 follows binary32 1.3*50 truncation 64 plus min 2; current typed input 66/71/66, fresh RPC47 kind 6/ability 134 and real 12s own cooldown are required. Natural hits can miss; no causal random hit/damage difference is promised. Proof not yet run.

## One-pass native checklist

1. Use disposable CSR110 mode `thrown-accuracy-bonus`. All parents are owned/loaded; 164 starts unloaded. QAEnemy logs in normally and selects normal Block. No GM, fixed roll or stat injection.
2. Use Actions to select Spy Style 1112, then ordinary Close Combat on QAEnemy. Queue Actions >Spy >Leg Sweep 134. Keep current actual records/logs: TF4/direct 1032 input 66. A miss is allowed; inspect normal queue/animation and actual input, not predicted damage.
3. Withdraw both normally (helper `stop` request), wait for teardown and the real 12s ability reuse. Use ordinary Hardline/LA to load only Weak Points 164 with parents unchanged. Return and repeat: existing 1145 record 10, typed input 71. Unload only 164 and repeat input 66.
4. Optional existing `?combatdepth specialattack134` reports read-only paired seeded diagnostics; it does not alter live RNG and cannot replace the real queued proof. NPC queued path is unit/source covered unless separately observed; free-cast throwing accuracy is not claimed.
5. Stop on unrelated transport/client or ownership failure. Native client/visible success is a separate check; no native pass is implied by headless proof.
