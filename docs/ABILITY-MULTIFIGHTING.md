# Multifighting 140: partial consumer

The original `tempmods.py` toggles `disableMultiOnOnePenalty`; original `combat_modifiers.py` supplies the exact penalty math already in CombatRules.140 has no invented numeric stat. OURS caller policy uses threshold 1 and distinct current incoming/outgoing links in the same live PvP interlock. Two opponents give -10 off / 0 loaded; three give -15/-5. Each ordinary or queued attack and defence input receives one penalty, before the unchanged RNG rolls. Withdrawal uses those existing contested inputs too; admission, timers and costs remain unchanged.

One-on-one NPC combat remains neutral. Arbitrary multi-NPC fights, historical opponents-fought counters and the original engine threshold/caller policy are not implemented or claimed as parity. Current endpoint observation happens outside fight/RNG locks; final membership, owner, character/world/life/transport and loaded membership checks reject replaced or ended links. There is no world scan or new registry.

## Normal proof

`multifighting-interlock` requires MXO_MULTIFIGHTING_SOURCE_SHA256. It creates four ordinary level50 players with the same owned prerequisite closure (2/113/130/185/143/116/135, plus fixed style17), changes only 140 normally, and runs two/three direct opponents in off/on/off stages. Assertions cover actual deterministic inputs and retained complete wire records; they do not require a random hit or causal damage difference. Helpers, hostile-PvP settings and seeded health are disclosed fixture aids. No RNG override, injected buff or forced target relationship is used. Historical pre-run checkpoint; completed normal and actual-mode proof is recorded below. Native has not run.

Source `e4bfe45f` qualified on master `773b5e59`: 171 focused tests and 6373 full Release tests passed (4m 14s), followed by actual Debug/Release NativeQa builds, both read-only plans/provenance and exact-one case discovery. All four hardlink properties were true. AbilityAudit compiled after restoring its missing assets file; no generated audit snapshot is committed. Earlier language-version/regex zero-test compile failures remain in Temp proof logs.

Actual newest HDS is Debug `E4F9D2BE39D2F41533FBC09B927C3B9414454319EE6A3506EE045C4384718160`; Release driver `71D5CCA50FF869927F060B74DCFCABC74204B82C20E46F2DAFE3F3E41564C881`; Release runner `DC0A77896FDB67109F32DEBFD0340D2217C6D5C9CBE263D6C63BE14A3BD27167`. Exact timestamps/paths are retained in `Temp/mxo-multifighting-interlock-20261115/freeze-e4bfe45f.json`.

## One-pass native checklist

Use discovered mode `multifighting-interlock` in its disposable CSR110 fixture. Ordinary helper players enter through the custom hook. Use supported helper requests (`actor:QAFriend/QAFar/QAOther`, `action:melee`, `target:CSR_Tester`) to add actual opponents; these are normal40 requests, not relationship injection.

1. Leave 140 unloaded; keep parent abilities fixed. Have QAFriend and QAFar attack CSR_Tester. Observe ordinary interlock animations/rounds and current CSR input logs: opponents2, penalty -10. Add QAOther: opponents3, penalty -15.
2. Withdraw CSR and all helpers normally (`stop` helper action); wait for ordinary teardown. Use Hardline/LA to load owned 140. Repeat: opponents2 penalty 0 and opponents3 penalty -5. Natural hit/damage outcomes can vary.
3. Withdraw all; unload only 140 through Hardline/LA. Repeat baseline. A healthy one-on-one fight has penalty 0 both ways.
4. Save raw packets/logs, actual current loadout and visible outcome. Stop on unrelated client/transport failure. This is not a native multi-NPC or retail-complete claim.

## Combined master checkpoint

`7eb4b77f` normally merges master `6a040a2e` (Desperation). Both attack inputs now wrap the existing `InterlockInitiative.Attack(actor, ability)` exactly once; defence wrappers are unchanged. The initiative helper's TF6 exclusion is preserved. Combined focus including Desperation/Initiative passed 188 tests; both actual builds/plans/provenance and exact-one discovery passed. The 6,373-test full result remains attributed to earlier `e4bfe45f`; final CI covers the combined tree. A failed merge script assumed two conflict blocks instead of the single contiguous block and left markers; its zero-test compile failure is retained before the corrected checkpoint.

Current frozen newest Debug HDS `59A8311A1D3FBF6EE71EB5CFF6BAE8DA773BDE283A90272C898ABEBBCD3566B9`; Release driver `620D37BE775F6E27CA0FE6F2987477C14F43DFBED070FB19187B3DB929C9C62C`; runner `9BCF03EAC2DCC897D7473339368CAB21B1D89BCFB5D67C18C3AE69F506E9ECE7`. Exact timestamps and paths: `freeze-7eb4b77f.json` in the same Temp proof directory. Historical pre-run checkpoint; see completed proof below.

## Current loaded-state closure

`69497d7d` separates structural links from 140 membership: an opponent's load/unload cannot erase its live link. The actor's current 140 membership is read from captured PlayerData under the existing passives lock, followed by owner/life/transport revalidation. Unload-after-snapshot now gives the off penalty; unrelated loaded changes retain the same count. Changed-source focus 188 and both actual builds/plans/discovery passed. Historical focus/full records above are not relabelled. Lock audit: no new passives-to-fight path, no registry or gate capture under fight/RNG/passives locks.

Current newest Debug HDS `99F19B1B7D3AC602DEABDA0E7758678754DFE05DDBB058144218193E82AA7C77`; Release driver `1F36FBD11822F547D043E04C7D8B3512081BD85950B9D7EB60F6882B1EE08EA0`; runner `CE9262685D769635A068D39C0684D9F5588950415C100428D644B60EF7E30430`. Manifest `freeze-69497d7d.json`. Completed proof follows below.

## Completed normal and custom-bootstrap proof

Frozen producer `56522203` / runtime source `69497d7d`: ONE authorized normal case **PASS 54.4s, held driver exit 0**. Same prerequisite loadout was SQL-checked throughout; only 140 changed through normal requests.

| Direct opponents | Off attack/defence | Loaded attack/defence | Unloaded again |
|---|---|---|---|
| 2 | 50->40 / 47->37 | 50->50 / 47->47 | 50->40 / 47->37 |
| 3 | 50->35 / 47->32 | 50->45 / 47->42 | 50->35 / 47->32 |

All four parsers reported 0 problems; RPC/view-section counts were 127/34, 84/32, 86/31, 86/32. Natural hits and damage vary: this proves deterministic actual input deltas, not a random damage difference. Ordinary helpers, seeded health and hostile-PvP settings are disclosed aids. No RNG override, injected buff or forced relationship. Live proof covers ordinary group rounds; queued integration and stale-owner/reset/load-race controls are source/unit evidence, not a separate live queued-ability claim.

Raw `Temp/mxo-multifighting-mxotest_run_1_b662ce2bfe.json`, SHA256 `2F28695B6456B418CDE17DBED3C76DAE07E2B75697EACED826D837210747D0C3`. Exact schema independently COUNT0 and all 11 recorded canonical process creation identities gone at 2026-11-16T00:44:47Z. A 300ms monitor may miss short-lived descendants; this is an observed-identity inventory.

One separately authorized actual custom-mode bootstrap **PASS, held runner exit 0**: one mode/server/custom hook, four ordinary helper logins, state serialization and actual-source match. No shared no-op substitution, all-mode matrix or native-client proof. Summary SHA256 `3036CCDBC62CA1198CB822B2BB71A35B21134040112B8D7C828E2DD5B4B64152`; schema `mxotest_bootstrap_cb8b43e7ca` independently COUNT0 and all 10 recorded identities gone at 2026-11-16T00:46:45Z.

Final runtime-source CI43781184572 passed 6378 units plus 12/43 policy tests. Earlier local full 6373 remains attributed to e4. Publication is docs-only over frozen producer 565; hds/data/tools/tests/research trees and actual DLLs are unchanged. No gameplay rerun. Proofs and cleanup: `Temp/mxo-multifighting-interlock-20261115/final-proof-summary-56522203.json`. Native checklist remains above; native has not run.
