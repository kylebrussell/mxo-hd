# Sharpshooter 148 — ordinary free-fire penalty consumer

The shipped loaded callbacks in `tempmods.py` 41–60 disable firing-into-melee and firing-at-melee-range penalties; their termination reenables them. Catalog 148 has exactly those two while-enabled callbacks and costs one memory. The existing recovered `CombatRules.GunAgainstFistsModifier` was tested but unused. This package wires it into ordinary free fire with explicitly **OURS** caller, distance and opponent policies; the retail caller/default distance boundary remains unresolved.

The source gun is the actual weapon passed to ordinary firing; a player defender uses its current EquippedItemID, not a possibly stale stored Free Fire pick. For an actual gun against a target without a gun, the recovered edge is pistol/dual pistol 5, SMG 10, rifle 15 (other classes retain the existing helper's values). At short range the edge is negative; otherwise positive. More than one opponent costs another 15. Loaded 148 removes the short-range and into-melee penalties, but retains the long-range edge. Gun-vs-gun and unknown/non-gun source class retain zero modifier. No Multi-Fighting 140 or interlock attack/defense consumer is added.

OURS integration adds the modifier once to the existing attack input at all three ordinary `FreeAttacks.Fire` callers: player→NPC, NPC→player and player→player. Short range uses the existing `InMeleeReach` boundary of 3 metres; weapon reach, damage, tactics, recipient gates, RNG order and natural rolls are unchanged. No character base stats or accuracy percentages are invented. The loaded boolean is derived from current owned/loaded membership, including level-zero rows, not a sticky callback flag. Normal unload and new-world loadout rebuilds therefore apply automatically.

Opponent counts are per-shot snapshots of distinct live current participants: the NPC's combatants, or actual player interlock links and active gunfire plus NPCs currently attacking that player. Ended, dead/zero-health, off-world, pending-world-change, other-district and unrelated-target participants are excluded; the current shooter is included once. Snapshot registry locks are released before fight snapshots; PvP captures precede its shot lock. No new registry/cache is introduced. Applicable unloaded gun-vs-fists counts remain linear in current handler/NPC populations. Invalid shots, non-gun sources, gun targets and loaded 148 bypass count scans because the formula cannot depend on them. Such captures fence exact target weapon and loaded state before use; changes neutralize that shot's modifier and the next shot resnapshots. `countSampled=false` is not a claim of zero actual opponents.

## Normal proof setup

Case/mode `sharpshooter-freefire` owns the complete 148 prerequisite graph before Margin and changes only 148's normal loaded state. Parent SQL rows remain fixed. The shooter equips its owned Revolver1 537 through ordinary inventory 63/65 requests and uses normal Free Fire 3f/41 and holster 3d. Seeded level/positions/owned gear and hostile-server entry are aids. There are no damage, hit or global RNG controls. Natural hits/misses and damage are retained as exact complete 8168 records for both actors; deterministic input changes are separately labelled diagnostics, not fixed damage promises.

The case compares close pistol off/on/unload (-5/0/-5), gun target (0), long-range edge (+5), and a genuinely created ordinary helper interlock before firing into it (-20/0). Rifle/SMG and lifetime/count/race behavior have direct controls. No headless movement/teleport aid or forced NPC combat state is needed.

## One-pass native checklist

1. Mode `sharpshooter-freefire`, CSR110 only. Equip the owned Revolver1 from bag 10 using the actual inventory menu. 148 starts owned/unloaded; keep parents fixed. QAEnemy is unarmed nearby. Use normal Free Fire and inspect actual shots/logs; short-range diagnostic modifier is -5. Holster normally.
2. Load 148 through ordinary Hardline/Loading Area UI, return and repeat with the same weapon/target/range: modifier 0. Unload normally and confirm -5 again. Natural attack rolls/hits/damage may differ; no fixed health-loss comparison is claimed.
3. Optional actual multi-opponent check: request QAFriend's ordinary melee on QAEnemy, then fire normally into that fight. Off gives -20, loaded gives 0. Stop firing and end the helper fight with ordinary withdrawal; never inject combatants or states.
4. Armed QAOther (equipped normally by the helper) gives 0; unarmed QAFar at 5 metres retains the pistol +5 edge. Rifle/SMG direct controls retain their recovered 15/10 edge values; do not change reach to manufacture a comparison.

The dedicated custom bootstrap logs all four helpers in through their real mode profile and equips QAOther's owned pistol through the same normal 63/65+SQL check, but does not start a fight. Source/unit/headless evidence is not native visibility, damage-parity or full interlock parity proof.

## Qualified proof (2026-11-15)

The qualified producer is `ebf6b3329dbcb9d665b66bbea091c6f762705063`, a clean normal merge onto master `8dcd7375b322e8ac2c5ab8be246a2842709506d6`. Data is exactly that master. The pre-master runtime full Release suite passed 5,939 tests at `00295e43`; final post-master focused controls/all-mode metadata passed 256. Actual Debug/Release builds, both plans/build-info and exact-one case/mode discovery passed. The final combined full suite is supplied by publication CI. Temp-only audit controls show row148 partial with the actual consumer, cosmetic without its optional inventory field. No generated review snapshots are committed.

One normal PvP case passed in 40.4 seconds with held driver exit code 0. Base attack input stayed exactly 50 across the fixed parent loadout. Actual input results were:

| Control | Modifier | Final attack bonus |
|---|---:|---:|
| Close fists, 148 off / on / unloaded | -5 / 0 / -5 | 45 / 50 / 45 |
| Armed target, 148 on | 0 | 50 |
| Fists at 5m, 148 on / off | +5 / +5 | 55 / 55 |
| Actual two-opponent interlock target, off / on | -20 / 0 | 30 / 50 |

All eight stages produced one ordinary shot and complete ordered own/target 8168 pairs with matching actual attack/defense/damage values and 4e acknowledgement. The off-melee shot happened to miss and the loaded shot hit; different natural draws mean this is not a controlled causal hit/damage comparison. The proof is the deterministic actual input/modifier change plus self-consistent normal wire output. The reader also has a direct two-shot drain/cursor/mismatch regression; that multi-shot branch was not exercised live. Every parser was clean: shooter95RPC/33viewlists, close54/25, far38/24, gun38/25, ally50/26. NPC callsites and rifle/SMG behavior have source/direct controls, not a separate live NPC/rifle/SMG proof.

Actual newest Debug HDS SHA256: `079F0C844ACAD9D78FA7768B9A6F997D137162E201D9EA77BDB42F4D4E9F3B41`; Release driver: `13001FEA19D95103FDC0C5AF059D4BFB2B69318ECF5F817F2E573A34545FC060`; Release runner: `3944EBCE4BE1F9CC0D1441D789AAD3C65D2708EF43E9A14C6AB942B110860A7C`. Raw game proof: `%TEMP%/mxo-sharpshooter-mxotest_run_1_3dc4c750b6.json`, SHA256 `4DCA18061F7CD8A69AA0C5FCFB947B6C0F8390E06BEF8628F060EAE86447A8A5`. Driver58188/server23816; exact schema count0/all13 observed creation identities gone at22:18:50Z. The server's canonical creation identity was retained even though its final sampled CIM CommandLine became null. Process polling is sampled, not an exhaustive OS history or a blanket no-PID-reuse claim.

One actual explicit custom-mode bootstrap passed at the same source: runner43408/server45488, schema`mxotest_bootstrap_c55715e177`, four real helper logins and QAOther's normal owned-pistol equip65+SQL. Held exit0; exact schema count0/all12 observed identities gone at22:20:37Z. It starts no fight and is not a shared no-op or native rendering proof. Proof root is `%TEMP%/mxo-sharpshooter-freefire-20261115`; `final-proof-summary.json` ties the source/raw/identity/cleanup records together.

Retained qualification issues were unit-fixture/setup failures (missing synthetic liveness timestamp and scoped player registry) and one zero-test duplicate declaration compile failure. Their logs remain alongside the corrected passes. There was no failed live148 run, automatic RNG retry, forced hit/damage, timer reset, native client action or Temp native-directory cleanup.
## Mixed NPC membership follow-up

The subsequent NPC-only correction includes the victim's actual active NPC session even when another player became that NPC's last selected target. It unions into the existing NPC HashSet and retains the one existing mob scan. A new additive CombatSession provenance query fences the attacker, PlayerData/player references, character, world/life generations, district and NPC entity/death/mission identity; the handler also verifies its current session. Existing combat timing and the legacy CurrentNpcTarget property remain unchanged.

At `31927f24`, 197 focused Release tests passed, including the causal A-versus-X/B-last-target case, scan/current-NPC deduplication, ended/dead/world/life/owner/character/replaced-session negatives and existing fixture controls. Actual Debug/Release builds, plans, build-info and exact-one case/mode discovery passed. The eighth runtime file is CombatSession.cs. Final publication CI supplies the combined full suite. The 40.4-second PvP run and custom bootstrap above remain attributed to `ebf6b332`; this isolated mixed-NPC change has unit proof, not a new live NPC proof. Its fixture loadout/hook and PvP behavior are unchanged, so neither live case nor bootstrap was repeated. The final native producer uses the newly compiled binaries recorded in the PR body.

## Crowded NPC interlock

The ordinary retaliating NPC round uses the same sourced short-range and into-melee modifiers for the firearm user's attack and defence. Previously it suppressed both when the unarmed NPC had multiple combatants. It now counts the active player-versus-NPC sessions engaged with that NPC. Each live player contributes once whether they are in interlock or firing normally. Retained membership from an ended session or previous world does not count.

The mode ability-parity-starter-sharpshooter-npc-crowd compares solo rifle (-15/-15), two live opponents (-30/-30), loaded 148 with two opponents (0/0), and the return to -15/-15 when the second opponent stops. It uses an ordinary rifle shot and close-combat request for the two links. The original retail short-range classifier and other callers remain open.
