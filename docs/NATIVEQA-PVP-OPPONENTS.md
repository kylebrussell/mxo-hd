# Native PvP opponent entry fixture

This tools-only fixture uses the existing hostile-server configuration. It does not change PvP rules or prove PR205's Powerless effect on players.

## Source and authority

`MXO_PVP_SERVER=1` selects existing server mode 6, while `MXO_PVP_MAX_SAFE_LEVEL=15` supplies the existing safe-level threshold. `PlayerHandler.processSpawn` calls `PvpFlag.OnEnterWorld` before self creation. Level-50 actors therefore receive the runtime flag through ordinary entry even though every seeded database `pvpflag` is zero. In PvE, ordinary entry clears that flag; seeding it alone did not provide a valid opponent fixture.

The fixture gives CSR110 organization 1, QAEnemy organization 2 and QAFriend organization 1. All use consistent level 50 and level-50 EXP, ordinary abilities 2 and 17, and grounded Mara positions. It sets no duel IDs, team IDs, flag commands, GM authority or random overrides. The existing relationship predicate remains authoritative.

The `pvp-opponents` mode eagerly enters only the four headless helpers. Before Ready, it records their actual mode-6 and safe-level-15 `3a` settings, self flag updates and mutual observer creations. It never waits for CSR110, who enters after Ready. `pvp-opponents-helpers.json` explicitly leaves native CSR entry unproven.

Observer evidence requires the actual character ID, organization, level and condition flag. Duel and team fields are retained when present; omitted fields are not fabricated as zero. The wire condition flag is `0x01000000`.

## One-pass native checklist

Claude alone runs native QA. Use the discovered `pvp-opponents` mode and ordinary CSR110 entry after Ready; do not run `/duel`, a flag command or a dev command.

1. Retain `pvp-opponents-helpers.json` and the native packet log. Observe CSR110's ordinary entry, hostile settings and PvP indication. This is the native CSR-entry check, rather than a bootstrap assertion.
2. Select QAFriend at the same Mara location. The same-organization helper must not be offered as a legal opponent. The client may suppress its attack action, so a refusal RPC is not required from native input. The separate headless case checks the ordinary request's `4d 18` refusal.
3. Select QAEnemy and use ordinary Close Combat. Retain the `40` request, `46` entry, `45` round and the corresponding HP change. No duel negotiation or `55` event is expected. The existing opening opportunity attack supplies damage without a new random control; later random outcomes and exact damage magnitudes are not checklist requirements.
4. Use Withdraw on CSR110 and send the existing helper request `{"actor":"QAEnemy","action":"stop"}`. Both ordinary `44` withdrawal requests make the existing next-round mutual-withdrawal branch deterministic. Retain both `46` exit messages.

Native targetability, UI presentation and this ordinary non-duel combat path remain pending until Claude observes them. This does not expand previous NPC-only effect coverage into PvP effect coverage.

## Headless proof contract

`pvp-opponents-entry` uses one disposable harness server and database. It checks three normal entries from database flags zero, independent actual `3a` settings, self flag updates and all mutual observer identities. The same-organization request must be refused without entering combat or damaging the friend. The different-organization request must enter normal non-duel combat.

HP evidence is operation-scoped: the round's damage must match a health value received after the attack cursor by the victim, attacker and unrelated observer. All values are retained, including later regeneration; latest HP alone cannot hide or manufacture the opening loss. Both ordinary withdrawal requests must end combat. Every session must have no parser problems or duel event, and database flag values must remain zero.

A unique JSON file is written before teardown on success or failure. It retains the actual selected server assembly path, configuration and SHA256, request cursors, raw RPC and `03` lists, observations, step/failure and server output. Delivery bytes prove the headless path, not native rendering or client menu availability.

## Retained headless result

Frozen source `4daec74b5ce5466ebc20268a18e1e98beab6d7ef` passed `pvp-opponents-entry` in 23.5 seconds with exit 0 and one server at peak. The actual selected server was the Debug assembly, SHA256 `2E7E4DC50E31F42FC42A685CA0E7D6F1B273EAD8D46AD0BE0BD1333EDEB56A93`; the runner executable's Release directory is not evidence of server configuration.

The ordinary opening round dealt 40 damage from 5000 HP. Victim, attacker and observer each received the corresponding current-operation 4960 HP value. Same-organization refusal, ordinary paired withdrawal, absence of duel events, unchanged database flags zero and clean parsing passed. The three sessions retained 50/47/34 RPCs and 10/9/8 view lists, with zero parser problems each. These are observed results, not fixed future damage expectations.

Raw proof: `C:/Users/russe/AppData/Local/Temp/mxo-pvp-opponents-mxotest_run_1_0a4105b2b2.json`, SHA256 `45F803158ACC34A68CF1267D11CBD3E46FACE1EFA89343A51BC3DEADB265771F`. Driver output: `mxo-pvp-opponents-4daec74b-driver.log` in the same directory. Both server and disposable database exited through the harness disposal path; no owned server process remained. Native CSR entry, menu availability and UI observation remain pending.
## Current master refresh (2026-11-15)

Frozen producer `cf4d02229c815d38ff6b49e9b38d0d98a2d9dfd6` uses the bootstrap context's actual server address. The former fixed-port login is gone. On master `93b5033d`, 6 focused tests and all 5,836 tests passed, with actual Debug/Release builds and read-only plans/provenance.

One actual `AfterStartAsync` run passed in 25.10 seconds, held exit 0, against dynamic ports 51777/51778/62901. All four helpers entered and their opponent observations were written. One separate `pvp-opponents-entry` scenario passed in 38.5 seconds, held exit 0: same-organisation refusal, ordinary non-duel opening damage of 50 (5000 to 4950 in victim/attacker histories), and normal paired withdrawal. All three parsers reported no problems. Native CSR entry and client menus remain for Claude.

Actual Debug source SHA256 `D3217307089FCF518614FEA4C4581DC53D1EEEBDA1913B1022D9BA42E7639A2F`; Release driver `BD4E277C4E50B627B9F858321086456D783C78A3812A5517F94F8CE472DFE805`; Release NativeQa `DC11FE074450671F12DD0DED9F41E85E8745B0EF8DEFFAA30F0CE6FF51C8DF91`. Raw scenario proof SHA256 `1CB1583F788DB7B0F4D965948F262658EF708D535C43889CBAD3E350C65D1109`.

Proof and source guards are under `Temp/mxo-pr-triage-20261115`. Exact schemas `mxotest_bootstrap_45e15b5499` and `mxotest_run_1_09ca72bb62` are absent. Independent checks found all 9 + 9 recorded process creation identities gone, with no PID reuse; polling cannot establish complete short-lived process history. No client lock, native launch, main-database reset or full current-mode matrix was performed by these checks.
