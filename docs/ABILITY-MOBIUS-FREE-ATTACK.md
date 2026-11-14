# Mobius Code967: partial incoming free-attack proc

This implements only the sourced one-percent, ten-second self proc and its incoming weapon free-attack consumer. The existing max-health/10 heal, five IS cost, own600s and shared heal-pill10s timers remain unchanged. This is **PARTIAL**: forced-combat entry, susceptibility967, proc FX/message/record presentation, and a standalone melee free-attack producer are unresolved. It does not grant general damage immunity or suppress abilities, interlock rounds, outgoing attacks, or ongoing firing sessions.

## Primary evidence

The actual GOB967 row matches `data/ability_catalog.json`: self direct object, WhenEnabled2, init `MobiusCodeEffect`, no scalar/TargetAttribute. Original `tempmods.pyc` SHA256 `968a7ab4525d5b62ed37f3703539da2b2ad66b8a7f1b1ba0cf60041fe0c5f060`, from Monolith.ltp `d2b95cd88634a19567f0c4be00d13061e4e615f6502fbde2ff0c36ba51b270f8`, has `MobiusCodeEffect` firstline2483: heal maxHP*.1, then `randrange(100) < MOBIUS_IMMUNITY_CHANCE` (global1). Success installs `('MobiusCodeInit','MobiusCodeTerm','','')` with positional arguments parent,parent,10,0,1. Init/term firstlines2506/2511 set allowed-to-be-free-attacked and allowed-to-be-forced-into-combat0/1, plus susceptibility9671/0. The outgoing allowed-to-free-attack setter is different and is not invoked.

Original `interlock/combat_free_attacks.pyc` SHA256 `204329c3883e58556d1bb0e16fb71881b1af0925c25e01de8d95656d1316fb1d` supplies the causal consumer. `PerformWeaponFreeAttack` firstline17 reads `defender.canBeFreeAttacked` at bytecode96 **after** attacker/defender contest totals. False forces failed result, damage0 and the ordinary failed weapon animation; the function still returns true for a valid weapon. `PerformMeleeFreeAttack` firstline127 has the same gate at bytecode71 and `FA_MELEE_FAILED`. Neither establishes an interlock-round or generic damage gate. This server currently exposes ordinary ranged freefire through `FreeAttacks.Fire`; it has no identified standalone melee free-attack producer. The common recipient callback is evaluated after the normal contest draws, before damage RNG. Existing miss presentation and shot scheduling remain in charge.

The bounded original archive scan covered62 modules/1333 code objects, zero decode errors. It did not recover the forced-entry native getter/bridge or refusal semantics. Do not infer blanket immunity from the original output message name. The known `abilities_mobiuscode` capture SHA256 `A676DCB28EE9075E0E5BB3E419BFC0C73DF6F50B3AFF1DA67F58034B4F264A05` shows the negative-proc FX and600/10 timers; no positive captured transition is claimed. Research extraction/disassembly remains in the external `Temp/mxo-mobius-967-primary-20261114` evidence folder; no generated snapshots are committed.

## Ownership and removal

The silent capability belongs to the target's timed-effect holder and captures the exact owner/data/world/life and holder-removal generation before the ordinary heal and its synchronous health-change callbacks, with strict expiry. Invalidated heal contexts are rejected before any proc chance draw. A stale callback cannot reinstall protection after reset/removal or adopt a new life. Queries invalidate stale state. Holder remove-all or remove-parent967 removes it even when there are no wire modifiers. No global flag or expiry-only callback can stick after death/reset/removal. No inferred80bc modifier, shield HP, FX, or generic immunity policy is emitted.

## Disposable QA controls

Discovered modes: `mobius-free-attack-positive` and `mobius-free-attack-negative`; matching normal-wire headless scenarios have the same names. Both preserve natural combat/damage RNG. Only `MXO_DEV_COMMANDS=1`, the runtime configured valid `mxotest_` database, `MXO_MOBIUS_QA=1`, authenticated live CSR110, and ordinary loaded967 permit:

```
?combatdepth mobius positive next
?combatdepth mobius negative next
```

This arms only the next successful normal967 producer callback's chance draw as0 or99, consumed once. Arming installs no effect and changes no resources, timers, hit rolls, damage, targets, goals, or other callbacks. Context changes invalidate the arm. A denied cast does not execute the callback. Use separate disposable positive/negative profiles because own reuse is600s; do not reset its timer.

The headless driver clones CSR110 solely from disposable template rows before its first Margin login, seeds an ordinary owned/loaded2+967 loadout, and uses normal967 use records and incoming41 gunfire with rifle692. SQL fixture HP/IS/level/loadout and initial positions are preparation aids. There is no teleport, weakened NPC, forced hit, combat roll control, or proc directly injected by the driver. Raw sessions/debug/source hash are retained before teardown, including failure. A winning incoming contest during the protected interval is required to distinguish this gate from ordinary misses; if normal RNG supplies none, that one pass is inconclusive and must not be silently retried.

## One-pass native checklist — CSR110 only

NativeQA and merge remain Claude-owned. No native pass is claimed by unit/headless tests.

1. Start the frozen positive mode with the reviewed source hash in a disposable schema. Log normal CSR110 in, confirm current HP2000/max5000, IS1000 and level50. Keep the existing loadout and site. Open Actions and locate Mobius Code under its ordinary discipline. The mode owns rifle692 in the bag; use the ordinary item menu to equip it if checking outgoing fire.
2. Use the normal `?pvpflag on` command on CSR110 before the fire request; SQL flags alone do not establish the current world flag. QAEnemy is logged in with normal equipped rifle692, opposite organization and PvP flag. Use the existing helper request `{"actor":"QAEnemy","action":"ranged","target":"CSR_Tester"}` after CSR is visible. Confirm incoming shots and damage before casting; do not issue another ranged request during the check.
3. Arm `?combatdepth mobius positive next`, clear target and use Mobius Code through **Actions**. Record normal cast, heal maxHP/10, IS debit5, own600s and shared10s timer, and exactly one `[MOBIUS-QA]` callback consumption. No new proc FX/icon is expected from this partial implementation.
4. During the ten-second interval record continuing ordinary failed attack animations/combat numbers and zero incoming free-attack damage. Match at least one server attack roll greater than defense to the failed outcome. Record health regen separately. Without such a roll, mark the gate observation inconclusive. Confirm shots keep running and naturally damage again after expiry, without another attack command. Use normal Escape to stop.
5. When practical, verify ordinary outgoing fire remains usable and an unrelated hostile ability/DOT/interlock is not made immune; these are negative scope checks, not claims about unrecovered forced-entry behavior. Do not interpret a missing FX as missing general immunity.
6. Stop/reset the positive fixture normally. Start the separately frozen negative mode, arm negative and cast through Actions. Observe unchanged heal/cost/timers and ordinary incoming damage across the same interval. Preserve raw wire/debug, source pins and session parser state. No cooldown reset, RNG retry, client lock, or main-DB mutation.
7. Reset via the normal NativeQA stop/reset flow and the client's ordinary Hardline/Loading Area path as required by the coordinator. Record teardown rather than treating a headless pass as native UI proof.

Validation status is reported with the frozen commit and external logs. Fresh/native execution requires a separate root grant; this checklist is not such a grant.
