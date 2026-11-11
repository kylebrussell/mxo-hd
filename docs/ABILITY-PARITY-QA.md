# Native checks for the ability audit

Initial automated validation at runtime commit `531e5a6`: all 3,465 unit tests passed in Debug and Release; all 56 headless scenarios passed in 432.2 seconds with four disposable servers. This includes the four new ability scenarios, NPC scope, Function Data loot, missions and the merged PvP/duel regressions. Subsequent generated-JSON compaction was checked for exact semantic equality and the catalog/scope unit checks were rerun. Native follow-up results are below.

Headless passes establish server behavior and packet structure. Repeat these checks with the 7.6005 native client when changing the corresponding paths. Use the [native QA fixture](../tools/NativeQa/README.md), `mxo-client.lock`, and CSR_Tester (110) only. The ability audit's current `partial` labels remain appropriate: native value scaling, summons, stealth, prerequisites and several callbacks remain unresolved.

Use a disposable level-50 CSR_Tester copy with sufficient IS and the required abilities. Have the harness seed a second friendly player or team bot for support checks. The real client's target selection, range checks and UI determine what must be observed; bypassing those with forged requests is headless coverage only.

| Check | Actions | Expected |
|---|---|---|
| Friendly heal | Select a wounded friendly player, cast Repair RSI 1.0 (77). Then select a hostile NPC and try the heal. | The friendly target's health rises on both views. The hostile selection is rejected without IS/cooldown consumption or NPC damage. |
| Self-only heal | Select a hostile NPC and use Mobius Code (967) while wounded. | CSR_Tester receives 10% of maximum health, capped at maximum; the NPC is unchanged. The rare immunity proc is not implemented. |
| HOT | Emergency Repairs 1.0 (169) on a wounded friendly player. | Health rises in 2.5-second steps for the 15-second effect, not as an immediate quarter-max heal. Magnitude remains a placeholder, documented in the audit. |
| Team range | Group Repairs 3.0 (371), with a teammate within 7 m, another beyond 7 m and an unrelated player nearby. | Caster and nearby teammate heal. The far teammate and unrelated player do not. Observe the shared heal cooldown after Emergency Repairs. |
| Class maximums | Kung Fu Grand Master (1064), then Aikido Master (1067), then Team Healer (1089). | Class buffs replace each other. The maximum-health bar reflects the active class on both views; switching between HP buffs preserves current health up to the new maximum. Removing the HP buff restores the base maximum and clamps current health. |
| Negative vital buff | Force Enhancer (383), including a copy whose base maximum IS is below the captured 317-point penalty. Let it expire or remove it. | Maximum IS clamps at one while active and returns to the original base on removal. Refreshing does not raise the base maximum. |
| Stun and immunity | Stun 1.0 (315) on an opponent already firing, then attempt movement, an ability, and Free Fire while stunned. Repeat a stun within its immunity timer. | Gunfire pauses, movement is corrected, and actions are refused for 7 s. The 14-second immunity prevents another control from landing. A stunned NPC also cannot attack. Interlock swing animations remain a separate known PvP gap. |
| Root and breaking | Immobilizing Shot (254) or Paralyzing Throw (1135), then try moving and casting. Damage the rooted target again. | Movement and jumps stop; abilities remain usable. Paralyzing Throw's protected first health change flips its signed proc, and later damage breaks its root. Immobilizing Shot uses its 50% chance after the protected first change. |
| Pacification | Freeze System (48 or 352), then damage its target. | Movement and actions stop while pacified; actual damage breaks the parent effect, while its separate immunity timer remains. |
| Area floors | Code Nuke (359), Stun 2.0 (333) and Plague Zone 2.0 (466) among visible hostile NPCs on one floor with another nearby on a different floor. | Eligible same-floor targets are affected; the other floor is excluded. DOTs continue on secondary targets while fighting a different NPC. Each killed NPC awards once. LOS and full retail resistance/deflection rules remain incomplete. |
| Other control gates | Powerless, disarm and enrage effects. | Powerless refuses abilities; disarm pauses weapon fire; enrage refuses withdrawal. Stopping gunfire remains possible. Verify native effect icons and off records. |

Record visible results, build hash and any protocol logs in the shared handoff. On releasing the lock, restore CSR_Tester to Mara `(23828,495,13984)`, full health, PvP flag 0 and mission 986 only through the harness. Do not use TheNeo or change Defender/firewall settings.

## Native follow-up, 2026-11-11

Build: `19be1e9` plus the native effect-routing and ranged-holster fixes committed with this record. Sessions `mxo-native-ability-20261111b`, `c`, and `d` under the local temporary directory used disposable level-50 copies of CSR_Tester and headless helper players. Only CSR_Tester was selected in the real client.

| Check | Observed result |
|---|---|
| Heals and HOT | Repair 77 raised the selected friendly player's health; selecting hostile Blackwoods produced the friendly-target error without a cast or NPC damage. Mobius healed CSR by 50 of 500 maximum with a hostile player selected. Emergency Repairs ticked by 21 every 2.5 s. Group 371 healed CSR and the nearby teammate by 125, excluding the far teammate and unrelated nearby player. |
| Class/vital buffs | Class icons replaced each other; Kung Fu raised native maximum health to 637 and Team Patcher restored 500. Force Enhancer clamped IS to 1/1 and expiry restored the base maximum 200. |
| Stun | Native stars appeared on the affected CSR after the routing fix. Actions were refused and ongoing gunfire paused for the control, then resumed. Repeated stun was refused during immunity. |
| Root | Native root icon/message appeared, movement was blocked, and HyperSpeed remained usable. Two independent headless runs additionally verify damage breaks Paralyzing Throw before its natural expiry. |
| Pacify/powerless/disarm | Native pacify and powerless effects refused HyperSpeed. Pacify off/immunity records appeared after damage. Disarm's sourced 25% proc eventually landed, showed its icon and paused gunfire. |
| Area attacks | Native Stun 333 and Plague 466 affected the same-floor test cluster; server records show independent DOT ticks on secondary NPCs. Fresh session d cast Code Nuke 359 on living Blackwoods: native cast/death FX and corpse loot appeared, and eight NPC damage/reward paths ran. WinterHelper on the lower floor was excluded. |
| Holster | Native Escape sends stance-idle RPC 3d00 without RPC 44. After the fix, one shot at durable QAEnemy was followed by stop/idle and no further shots over 20 s. The headless gunfire scenario also checks a seven-second quiet period after 3d00. |
| Enrage withdrawal | Enrage icons appeared, but native Withdraw preparation sent tactic RPC 42 (Block), not withdrawal RPC 44, so this native check remains inconclusive. Server-side withdrawal gates are covered headlessly. Revisit with combat round entries; do not claim a native pass. |

The native run exposed a legacy delayed animation callback that used the caster's later target and could apply unrelated demo NPC damage. It has been removed. Player landing FX now update the resolved recipient's correctly framed self view and the corresponding observer attributes, including zero-damage control casts. Exact packet tests and the control scenario cover both recipients and reject caster-only landing FX.

After these fixes all 3,466 unit tests passed in Debug and Release. Focused headless checks passed for all four ability scenarios, root breaking (two runs), walkable mission arrival fallback (four random sites), and native-style ranged holster. Interlock swings remain the separate combat-round task; these checks do not establish retail-complete resistance, LOS, scaling or animation parity.
