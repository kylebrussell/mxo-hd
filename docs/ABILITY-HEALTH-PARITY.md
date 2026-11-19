# Direct heals, periodic effects and resurrection

## Source and runtime checks

- Restore Health 2.0 (54) uses stored base 37. The native modifier evaluator at `625ec512..625ec58a` multiplies type 3 by the binary32 `1 + HealDamage / 100` and truncates. With +15% influence it heals for 42. Claude observed 400 to 442 and healing capped at 575 using stock Actions. [Native result](https://github.com/kylebrussell/mxo-hd/pull/103#issuecomment-6812841733)
- Emergency Repairs 1.0 (169) and 2.0 (332) store a type-3 total against DOTPlaceHolder 1157. The total gets the healing influence once. The Python HOT callback apportions that total by `int(total / duration * interval + 0.5)` and submits each tick as NeutralHeal. At zero influence, 169 stores 14 over 15 seconds and requests +2 per tick. With +15% it stores 16 and requests +3. Both use five ticks at 2.5, 5, 7.5, 10 and 12.5 seconds under the server's before-expiry scheduler. The inhaler templates 716–720 omit type 3. Their stored values use untyped HOT evaluation without HealDamage. Their item activation and omitted tempmod target default remain unverified. [Shipped HOT script](../research/client-scripts/tempmods.py#L975)
- DOT totals use the individual periodic modifier's type for influence, and typed ticks still pass through the existing resistance calculation. Captured totals and durations retain precedence, with a uniquely matched template supplying the update interval. Processor Lag 1.0 (69) and Wounding Throw (281) store 5-second intervals. Wounding Shot (253) submits NeutralDOT alongside its separate ranged hit. Plague Zone 2.0 (466) retains captured bases 424–444 over 15 seconds. With the fixture's +15% viral influence that becomes 487–510, giving five raw ticks of 81–85 before resistance. The retail capture's observed 424 total gives five raw 71-point ticks at the same interval. [Shipped DOT scripts](../research/client-scripts/tempmods.py#L896)
- Restore 325 plays its own context and restores 1 HP and 1 IS. Revitalize 326, Revive 375 and Rejuvenate 416 play their own contexts and restore full HP and IS. Renew 438 uses Revive 375 for `Stance_Stand` and `Action_Resurrect` while the approval and result messages keep 438. Claude's previous stock Rejuvenate session passed No, Yes, standing in place and observer motion at 2f7495e8. [Shipped resurrection callbacks](../research/client-scripts/ability/hacker/health.py#L11) · [Native result](https://github.com/kylebrussell/mxo-hd/pull/290#issuecomment-6974177832)

The catalog stores raw values and proc timing. Most final retail coefficients, the exact resistance curve, HOT behavior in the overflow branch, unsupported periodic initialization such as Assassin Mask's neutral callbacks, and stock Renew motion still need comparison. The shipped HOT script computes `max_health - newHealth` when the next tick overshoots. The server caps the ordinary heal at the remaining deficit. A retail trace of that branch is needed. A flagged corpse keeps its flag, and the script's conditional 1208 refresh remains unresolved. The offer padding semantics, refund timing and per-ability native presentation checks remain open.

## Fresh-server replay sources

`ability-health-parity` replays these three complete 43-byte casts, substituting only the current observed target view and spawn ID. It checks actual ally HP delta and the received health update, both periodic records, five ticks through natural expiry and DOT raw totals reconstructed from the damage lines. The existing controlled contest draw guarantees a landed DOT in the headless fixture. The draw does not pin the DOT total sample.

| Ability | Stock request source |
| --- | --- |
| 77 Repair RSI 1.0 | `mxopackets1_luizoe_old_sniffer_Unknown.log.txt:115307–115309` |
| 169 Emergency Repairs 1.0 | Claude's native CSR PacketLog `455f0446826c:27051–27054` |
| 466 Plague Zone 2.0 | `actions_combat.log.txt:16214–16216` |

The exact 54-byte Rejuvenate Yes reply came from the stock recipient in PR290, PacketLog `54d32748b0ba:8303–8306`. The bootstrap now replays the full stock PR316 cast from `53a738c0add2:36645` with the current corpse handle. In the native session, QAFriend receives the stock offer and uses the captured Yes reply.

## QAFriend's live maxima

The disposable seed gives QAFriend **500 base HP and 1000 base IS**. The cloned rank-50 loadout includes Guardian Healer (51, fixed +1% max HP), Quick Recovery (70, +0.48 percentage points per effective level, truncated) and Execute Program (45, +0.2 percentage points per effective level, truncated). `AbilityBuffs.RefreshVitals` applies those totals to the seeded maxima. A fresh QAFriend has **620 max HP and 1100 max IS** at full effective rank 50.

Ordinary death applies a 15% Death Effect, then its six-second refresh recalculates loaded passives as the effect recovers over five minutes. The table uses the effective ranks from the most recent refresh. The recovery times below reproduce the ranks in the focused test.

| State | Effective rank for 70 and 45 | Max HP | Max IS |
| --- | ---: | ---: | ---: |
| Before death | 50 | 500 × 1.24 = 620 | 1000 × 1.10 = 1100 |
| Fresh Death Effect | 42 | 500 × 1.21 = 605 | 1000 × 1.08 = 1080 |
| Death Effect refreshed at rank 45 (e.g. 104 s into recovery) | 45 | 500 × 1.22 = 610 | 1000 × 1.09 = 1090 |
| Death Effect refreshed at rank 46 (e.g. 144 s into recovery) | 46 | 500 × 1.23 = 615 | 1000 × 1.09 = 1090 |

PR316's 615/1090 and PR321's 610/1090 are both consistent with the live maxima during recovery. The stock callback reads the recipient's `MaxHealth` and `InnerStrengthMax`, and the server retains Death Effect on resurrection. The approval helper samples the current maxima after receiving the offer and checks fresh self-view vitals and standing motion after the captured Yes reply. [PR316 native QA](https://github.com/kylebrussell/mxo-hd/pull/316#issuecomment-6999442131) · [PR321 native QA](https://github.com/kylebrussell/mxo-hd/pull/321#issuecomment-7001527488)

## Native verification

Run NativeQa with mode `ability-health-parity` and a fresh output directory. QAFriend and flagged QAEnemy enter before Ready. The preset uses level 50, +15% healing influence from 71/233/409 and +15% viral influence from 95, with CSR inventory empty in the disposable DB.

1. Ask QAFriend to use `?health 100`. Select QAFriend and cast Repair RSI 1.0 in stock Actions. Expect +10 HP from the cast. Use `[ABILITY] Healed` in DebugLog to separate the cast from natural regeneration.
2. Prepare QAFriend at 100 HP again. Cast Emergency Repairs 1.0. Expect no instant heal and five +3 HP effect ticks 2.5 seconds apart. Use `[EFFECTS] 169 heals QAFriend` to count them.
3. Flag CSR, ensure QAEnemy is flagged and select QAEnemy. Cast Plague Zone 2.0. After a hit expect five ticks 2.5 seconds apart. Add the resisted value to the applied amount to compare each tick with the raw 81–85 expectation.
4. Send QAFriend `?die` via its helper and select the corpse. Cast Rejuvenate RSI from stock Actions. Send `{"actor":"QAFriend","action":"approve","accept":true}`. The server prints the living maxima at Ready, and the helper prints the current corpse maxima before approval. Check that the response confirms those restored values and the in-place stand. To reproduce 615/1090 from a fresh death, allow roughly 2½ minutes for Death Effect recovery before casting, then approve promptly. Use the helper’s current reading to account for the six-second refresh.
