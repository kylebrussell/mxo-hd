# Thrown Accuracy Bonus 1145: partial consumer

Primary client 6251fe10 selects 1032 for TestFunction 4; the catalog 1032 links 1145. Weak Points 164 already produces 10 at learned 50, Wicked Aim 278 produces 15, Master Knife Thrower 338 fixed 5. These producers/caps/lifetimes are preserved. The retail linked-owner coefficient is unrecovered. **OURS** applies the existing melee/ranged/Viral1147 convention: separately capped direct 1032 plus trunc(characterLevel*capped1145/100), once. Thus 164 adds 5 attack-base points at character 50, 278 adds 7, 338 adds 2. General 163, Blind, raw caps, other TFs, ordinary attacks, escapes, costs and RNG draws are unchanged. Existing 164 damage/defence producers remain separate.

The shared SpecialAttack path reaches normal queued NPC/PvP special rounds. Positive-cast/free thrown moves retain their current admission and damage path; this change does not invent a new free-cast contest. No full retail formula claim.

Case/mode `thrown-accuracy-bonus` uses ordinary Spy Style 1112 and one paid queued Leg Sweep 134 per off/on/off stage, keeping the full 17-ability prerequisite loadout fixed except 164. Normal helper Block prevents fixture damage pressure without injecting a control state or overriding RNG. Actual 1032 style value 66 follows binary32 1.3*50 truncation 64 plus min 2; current typed input 66/71/66, fresh RPC47 kind 6/ability 134 and real 12s own cooldown are required. Natural hits can miss; no causal random hit/damage difference is promised. Normal wire proof passed as recorded below.

## One-pass native checklist

1. Use disposable CSR110 mode `thrown-accuracy-bonus`. All parents are owned/loaded; 164 starts unloaded. QAEnemy logs in normally and selects normal Block. No GM, fixed roll or stat injection.
2. Use Actions to select Spy Style 1112, then ordinary Close Combat on QAEnemy. Queue Actions >Spy >Leg Sweep 134. Keep current actual records/logs: TF4/direct 1032 input 66. A miss is allowed; inspect normal queue/animation and actual input, not predicted damage.
3. Withdraw both normally (helper `stop` request), wait for teardown and the real 12s ability reuse. Use ordinary Hardline/LA to load only Weak Points 164 with parents unchanged. Return and repeat: existing 1145 record 10, typed input 71. Unload only 164 and repeat input 66.
4. Optional existing `?combatdepth specialattack134` reports read-only paired seeded diagnostics; it does not alter live RNG and cannot replace the real queued proof. NPC queued path is unit/source covered unless separately observed; free-cast throwing accuracy is not claimed.
5. Stop on unrelated transport/client or ownership failure. Native client/visible success is a separate check; no native pass is implied by headless proof.

## Source qualification

Source `7faa638c` on master `9782c8f0`: 71 focused Release tests passed, including actual producer values, separate caps/negative/Blind paths, paired unchanged RNG consumption, full normal reload prerequisites and exact queued-record framing. Earlier zero-test fixture compilation errors and 70/1 test setup failure are retained; the latter incorrectly queried state 90 as a producer, corrected to captured Pistol Whip 501. Runtime policy stayed unchanged. Both actual Debug/Release NativeQa builds, plans/build-info, exact-one discovery and conditional audit output passed. No local full suite by root's direction; final CI supplies the complete-checkout suite. All four hardlink properties true, no new worktree/data materialization or generated snapshot commit.

Frozen newest Debug source `B3B1FC4ED980517C0EA7AEECD16C5D5B5A346F7024541675BEC4C5AD940E62C0`; Release driver `6E52E737561276DB42E7366FC599578ECA461FB2EA370281911850B62EB5EA8B`; runner `81785C7B8E98D1E72E296D8B61A66D391ADF789A2B16EEB7EDF2A2846978D3D3`. Manifest `Temp/mxo-thrown-accuracy-20261115/freeze-7faa638c.json`. Normal case and actual custom-mode bootstrap passed; native visual validation remains separate.

## Frozen normal proof

Producer `1bf62dd4` (code identical to qualified `7faa638c`) passed the normal case in 61.2s, held driver exit 0. Actual queued TF4 inputs were **66 → 71 → 66**, direct 1032 stayed 66, bonus 1145 was 0/10/0. The normal 17-ability parent loadout remained fixed except 164; fresh kind-6 queue records and natural 12s own cooldown expiry passed. Natural blocked/hit/blocked outcomes were retained, without a forced roll or causal damage claim. Both session parsers reported zero problems (148 RPC/42 view sections and 111 RPC/26 view sections).

The actual custom-mode bootstrap passed in 23.4s with one custom hook, one normally logged-in Block helper, and no shared no-op substitution. Actual source matched the frozen Debug hash above. Independent cleanup found game schema `mxotest_run_1_d6a480cffd` absent/all 12 recorded creation identities gone, and bootstrap schema `mxotest_bootstrap_1cab7bdb8f` absent/all 12 gone. Descendant polling is observational, not an assertion that every short-lived process was sampled.

Raw game: `Temp/mxo-thrown-accuracy-mxotest_run_1_d6a480cffd.json`, SHA256 `0A555D2AA63803ABE1C208322AAD87EAF0F83A824FFEA613A985DDAE6B439105`. Summary: `Temp/mxo-thrown-accuracy-20261115/final-proof-summary-1bf62dd4.json`. Prior CI on `1bf62dd4` passed 6413 unit tests plus 12/43 policy checks; this proof-only descendant requires its own final CI. No runtime/data/tools/tests/research changes or rebuild accompany this documentation. No native pass is claimed.
