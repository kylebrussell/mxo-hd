# Desperation1149: cosmetic snapshot foundation

The shipped tooltip says Desperation boosts initiative more strongly at lower health. The catalog specifies10IS,90s reuse,buffer10 and a20s on-use modifier targeting Initiative1044. `tempmods.py:2402 DesperationFunction` runs at application: influence=1+InitiativeBonus1046/100; HP>integer(MaxHP/2) stores10*influence, otherwise(1-HP/MaxHP)*100*influence. Exactly half health takes the stronger branch. The result truncates on integer stat storage. Current1046 totals use the existing cap;1044 totals retain their existing600/12-per-level cap. Missing character attribute bases are not fabricated.

The implementation snapshots once, retains the original value in its off record, and uses existing buff buffer accounting, cost, cooldown, expiry, death and zone cleanup. A loadout refresh that unloads1149 removes its snapshot. Buffer maximum100+4*level remains the server's documented approximation. This is **COSMETIC**, with no claimed initiative order, new combat chance or damage benefit.

## New native consumer boundary

Pinned `client.dll` SHA256: `cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a`.

Native625c5df0 conditionally reads1044 through625c4e90 at625c5e50. This is a combat tactic total helper, also adding a typed tactic stat, SneakAttackCombatTactics879 and CombatTactics163. It skips1044 when its fourth argument is0x1000. Otherwise625c1b00 requires CastWhatHow0/2;2 additionally checks the supplied state flag.62515b40 fetches CastWhatHow through string628c8a80.

All eight direct callers (620f70de,620f7190,620f723f,620f72ee,620f7e16,620f7ecb,620f7f7a,620f8029) pass0x1000, skip1044 and feed the Python GetCombatTacticRoll wrapper6258d770 before formatting UI text. The wrapper's only eight recovered direct callers are those same UI paths. Its startup binding62823450 writes GetCombatTacticRoll (string628d8608) into62b0af48 at628234aa. There are no literal pointer references to625c5df0. No other Python caller of GetCombatTacticRoll exists in recovered scripts. These facts do not establish that the unavailable retail server had no other consumer.

The recovered `interlock/combat_abil_level.py:75` assigns Level+20/10/0 by tactical setting. It does not read1044/1046. Therefore this change does not alter NPC/PvP combat rolls or order. A real retail gameplay consumer remains unresolved.

Reproduce the read-only inspection using Temp/mxo-combat-inspect.py `dis 0x625c5df0 580`, `calls 0x625c5df0`, `xref 0x625c5df0`, `calls 0x6258d770`, `dis 0x62515b40 110` and `dis 0x62823450 110`.

## Native check for Claude only

Use the exact full SHA recorded in the PR body, matching Release/Debug source and snapshot hashes. Build-info and fixture-plan are read-only; actual fixture setup is reserved for Claude. Mode `desperation` enables existing dev commands only with exact MXO_DEV_COMMANDS=1 and explicitly holds natural HP regeneration with MXO_DESPERATION_QA_HOLD_HEALTH=1 only in an mxotest_ database (fixture control, not a retail mechanic), owns1149 and its discipline ancestors without loading ancestor bonuses. Do not infer retail gameplay parity from a visible buff.

At full health cast1149: expect10IS spent,20s Initiative+10 record and90s cooldown. Health can be set with the strict dev command `?health N`; use a fresh fixture/player per90s reuse control. At exactly half health expect+50; at20% health expect+80, assuming1046=0. Change health after application: original modifier stays unchanged; expiry/off retains its original value. Test dismiss, unload, death and re-entry cleanup. Fill the existing upgrade buffer to deny a new cast. Confirm no new combat-order/accuracy/damage effect is claimed. Record screenshots, modifier on/off bytes, build/snapshot hashes and exact full SHA.
