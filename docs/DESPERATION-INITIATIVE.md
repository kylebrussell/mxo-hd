# Desperation initiative — OURS approximation

The existing Initiative1044 total now adds attack-base points in ordinary and queued-special interlock contests against NPCs and players. Desperation1149's sourced application snapshot therefore helps combat: full health grants10, half health50, and20% health80 before the existing InitiativeBonus and stat cap. The modifier is applied once and keeps its original value when health changes.

This is explicitly OURS. Native625c5df0 can add1044 to a combat tactic total, but its authoritative retail gameplay caller remains unresolved. The implementation chooses that simple additive interpretation for playability. It adds no turn-order scheduler or damage multiplier. Free fire, free-cast abilities and escape/withdraw contests retain their existing calculations. Costs,90s reuse,20s expiry, buffer use and unload/death/zone cleanup are unchanged.

`?combatdepth initiative` is a guarded read-only diagnostic in a dev mxotest_ fixture. It reports current baseline/bonus/total and1000 paired seed731 tactic contests. It changes no live RNG, health, inventory or timer. The fixture's natural-health hold is the existing disposable Desperation control, not gameplay.

## Claude's one-pass native check

Use mode `desperation-initiative`, CSR_Tester110 only, normal client lock/reset procedure.

1. At full health run `?combatdepth initiative` and record the zero bonus. Cast Desperation1149 normally from Actions. Expect the existing10IS cost and20s Initiative+10. The diagnostic must now show10 more attack-base points and more seeded hits.
2. Fight a normal nearby mob or ordinary DuelBot interlock. The DebugLog must show `[INITIATIVE OURS]` with that same bonus. Swings/ordinary round entries must still appear. These are normal combat rolls; a specific individual hit is not guaranteed.
3. Let the modifier expire naturally. The diagnostic must return to its exact original total and seeded hits. A further cast during the ordinary90s reuse must remain refused.
4. Optional lower-health control after the real cooldown, or a fresh fixture: use `?health` to set half the current maximum, cast normally and expect a50-point snapshot. Do not claim native turn-order or retail chance parity.

Headless case `desperation-initiative` checks a normal half-health cast, the read-only before/after diagnostic, actual ordinary PvP interlock consumption and natural expiry. Unit tests cover the real producer/cap/unload, health snapshot stability, escape exclusion, unchanged free-fire accuracy and identical isolated roll sequences. Native rendering and NPC live consumption remain separate from that PvP proof.

## Qualified results

Runtime539a442a and test-only childb2580f1e passed80 focused tests and5912 full Release tests. Debug/Release builds, exact-one case discovery and the actual mode plan passed. The first full run had5911 passes and one fixture-scope failure: its old assertion allowed health hold only for the earlier Desperation profile. The corrected assertion includes the two dedicated profiles and keeps every other profile excluded; gameplay was unchanged.

One normal-wire case passed39.3s with held exit0. Actual half-health cast produced Initiative+50; current interlock input changed0→50, and the isolated seed731 diagnostic changed716→881 hits/1000. Ordinary PvP rounds used that input with no live RNG override. Natural20s expiry restored input0 and716/1000; ordinary paired withdrawal ended the fight. Both parsers were clean. The raw proof is `Temp/mxo-desperation-initiative-mxotest_run_1_230e10bb3b.json`; exact schema count0 and all seven recorded process identities gone.

The actual `desperation-initiative` mode bootstrap passed separately against its own seeded server and inherited no-op hook. It checks startup/authentication/state serialization, not native gameplay. Schema37d77a5d8e count0 and all seven recorded identities gone. Raw retained proof and binaries are associated with `Temp/mxo-desperation-initiative-consumer-20261115`; process observation is sampled. The audit review bundle is generated externally; no historical coverage snapshot is committed.
