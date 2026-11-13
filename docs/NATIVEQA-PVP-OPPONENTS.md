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
