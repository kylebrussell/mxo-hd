# Loaded passives and healing modifiers

This follow-up replaces inert passive totals and the target-quarter-max-health heal placeholder. It does **not** declare complete retail ability parity. The generated [ability table](ABILITY-PARITY.md) distinguishes each shipped modifier from the server's consumers.

## Native evidence

The local 7.6005 `client.dll` modifier loader at `62520900` reads ValueFunction, ValueScale, MinValue and MaxValue into a 0x68-byte modifier descriptor. Tooltip wrapper `621cd5b0` supplies learned ability level and the XP-derived character level to evaluator `62517ff0`.

| Function | Evaluated value |
|---|---|
| 2 | MinValue + truncate(learned level × ValueScale), capped by positive MaxValue |
| 3 | MinValue + truncate(character level × ValueScale), capped by positive MaxValue |
| 0/1, fixed range | Fixed value × ValueScale |
| Random ranges / function4 | Still outside the deterministic passive executor |

Scaling retains the template's binary32 scale in the x87 multiply until `626a92d8` truncates toward zero. This matters: `.24f × 50` is just below12 and evaluates to11. A missing scale uses1, consistent with the captured Write Code level/cap behavior. Fixed equal ranges do not require guessing a random distribution.

Retail loaded-passive records use `80 bc 45 00`; active cast buffs use `80 bc 15 00`. Both carry the source, target stat, signed value, off byte and duration. The obsolete unconditional Hyper-Jump and Write Code login records are removed. Passive records now share the session's unique record allocator and are removed/replaced on loadout changes and upgrades, or rebuilt after a world reset.

The native health-modifier path `625ec512..625ec58a` looks up a damage-type influence, builds a binary32 `1 + influence / 100`, then multiplies the modifier value and truncates. Healing type3 resolves to HealDamage174. The server uses the shipped fixed heal base and the caster's available healing modifiers; HOT uses its stored total and [`tempmods.HOT`](../research/client-scripts/tempmods.py), including interval rounding and no tick at expiry. Mobius remains its separately scripted10% target-max heal. Simulacra repair requires a pet and is refused as a player heal.

## Gameplay wiring

- Owned, loaded memory/discipline rows and their grant closure contribute once. Claimed client loadout levels cannot override the owned level; an unrelated ability/slot pair is ignored.
- Learned-level functions use the existing Death Effect effective level. Character-level functions use character level. Upgrading, leveling, changing loadout, entering a world and Death Effect updates refresh totals.
- Direct and percent max-HP/IS changes compose with cast buffs; the unmodified baseline is retained. Refresh/reconnect does not compound bonuses, and reducing a maximum clamps the current vital.
- Passive accuracy, defence and resistance bonuses feed the existing NPC/PvP combat bases. Regeneration bonuses change health/IS ticks. Crafting uses the shared totals rather than its previous separate guessed formula.
- Melee/ballistic/viral influence changes ability hits in NPC and PvP paths and DOT totals once at application. Existing captured/generic damage bases remain approximations. Healing influence belongs to the caster, independently of the recipient's maximum health.

The native evaluator and packet layouts are direct binary evidence. Applying newly available percent bonuses to the server's existing combat and regeneration bases is an integration policy: those base formulas are still approximate. Complete character-attribute contributions, several stat families (including stealth/detection consumers), complete Determination bases, passive aura callbacks, random value functions and pets remain gaps. A modifier that is stored and displayed but lacks its consuming system is explicitly not full parity. Final retail healing/damage magnitudes still need comparison with a known caster build.

## Control-duration follow-up

`6251dd00` resolves the state attribute link; `625174e0` identifies class4. At `625ec56e..574`, a class-4 modifier multiplies its **float duration** by the binary32 influence factor and retains fractional seconds. Other classes follow the integer-value path. The stored links are:

| State | Duration bonus stat |
|---|---|
| Stun102 | 995 |
| Pacify85 | 1039 |
| Root99 | 1040 |
| Blind90 | 1041 |
| Powerless96 | 1042 |
| Confuse91 | 1043 |

`AbilityControlDurations` applies these links at cast application, for player and NPC targets. It does not invent an Enrage link where the shipped template has none. The state's expiry and record carry the same fractional duration. Explicit immunity timers remain separate, and root/pacify health-change rules are preserved. A fully reduced timed state is refused rather than accidentally becoming an indefinite state. Already applied states retain their original application duration when the caster changes loadout.

Target Determination mitigation now executes for eligible landed UDB0 debuffs; see [combat-depth evidence and limits](COMBAT-DEPTH.md). Complete attribute/template target bases remain unknown. Immunity retains its independent unmitigated timer.

## Native checklist (pending)

Use the NativeQa harness's `passives` mode, `CSR_AbilityQA`, and **CSR_Tester110 only**. This fixture starts near the test NPC and gives the tester a small named passive/support loadout; helpers have no passive loadout. The harness reserves `mxo-client.lock`, logs its ownership and resets110 on release. A native client must already be available through an allowed launch; no broker or launch workaround is part of this harness.

1. World entry: no crash, phantom PvP flag or unconditional Write Code/Hyper-Jump bonus. With Aikido Mastery269 and Execute Program45 at50, the fixture should have575 maxHP and220 maxIS. The character sheet should show the loaded passive records, without duplicate stacking after a relog.
2. At a hardline, unload/reload Execute Program and Aikido Mastery. Expect maxIS200/220 and maxHP500/575; a reduction clamps the current value. Cast Kung Fu Grand Master1064: its captured137HP composes with the passive bonus. Replacing/unloading the effects must restore the baseline.
3. Invite QAFriend into the mission team. Physician Heal1098 should use the same source amount for both recipients; the fixture's three healing passives give15% influence, yielding206 from the stored180 base under native float/truncation. This amount is a **server expectation awaiting retail comparison**, not a captured final heal. Unload those passives; Restore Health54 uses37, Restore Health77 uses9. Emergency Repairs169 applies a14-total HOT over15s at2.5s intervals. Check UI feedback, HP changes and cooldowns.
4. Compare viral damage against the test NPC and `?duelbot pvp` with Transmit Virus95 loaded/unloaded and Overclock327 on/off. Check both direct hits and virus ticks. Bystanders must still see combat.
5. `?die`: no false PvP message. Learned-level passive records should decrease; Aikido's character-level function stays unchanged. Reconstruct, travel back from the Loading Area, then relog. No duplicate passive stats, missing records or compounded maximum health. Verify recovery over five minutes.
6. Stop the harness and verify `stopped.json`: Mara `(23828,495,13984)`, full health, flag0, mission986 only. Do not select TheNeo or change security settings.

For the control-duration follow-up, use NativeQa `controls` mode. It adds Disrupt Connection384 and Wicked Aim278, plus Stun315, Code Freeze40 and Freeze System48. At50, expect Stun315 to show8.05s while its explicit immunity stays14s; Code Freeze40 should show27.5s and still break on damage; Freeze System48 should show6.66s. Check that actions remain blocked until the stun ends, then resume. Unload the duration passives and repeat with their unboosted7/25/6s durations. Verify presentation on the NPC and the hostile QA helper/DuelBot; use only CSR_Tester110 natively. These native checks are pending.

Headless coverage exercises owned loadouts, forged slot/level claims, upgrades, unload/removal, death scaling, a fresh server login, direct/group heal source values, HOT ticks and NPC/PvP paths. Native acceptance remains separate.

## Combined validation for PR102

Runtime `48fb975`, integrated parent `744469f`, test fixture correction `adec3c2`: **3,567/3,567 Debug and Release unit tests pass; 86/86 fresh-server scenarios pass in614.9s**, six servers at peak. The group-interlock endurance fixture now gives its20,000 currentHP a matching maximum, so legitimate vital clamping does not shorten the endurance test. NativeQa Release builds. CI `43133174615` passes; the workflow also covers stacked `codex/**` PR bases. Native checks remain pending.

## Partial native confirmation after merge

[Claude's independent client check of88e95b4](https://github.com/kylebrussell/mxo-hd/pull/103#issuecomment-6812841733) confirmed maxHP575, Restore Health54 healing400→442 with the loaded15% influence, stun/immunity messages and icons, cast denial during stun and successful casting after expiry, and the player's stun FX. PR102 merged as175d18f and PR103 ase9e4849. Exact8.05/27.5-second measurements, Code Freeze damage break, HOT ticks and group heal were not checked natively; those checklist items remain open. These observations came from Claude; Codex did not operate that native session.
