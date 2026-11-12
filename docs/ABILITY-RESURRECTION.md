# Hacker resurrection callbacks and approval

This implements Restore RSI325, Revitalize RSI326, Revive RSI375 and Rejuvenate RSI416. These remain **partial** in the parity audit until the native dialog, transition and feedback have been checked. The approval protocol is decoded from the shipped `client.dll`; no retail resurrection packet capture was found.

## Source behavior

`research/client-scripts/ability/hacker/health.py` calls `PlayerCharacter.resuscitate()` in all four direct-object callbacks. Restore325 sets health and Inner Strength to1. The other three restore both to their maximum. All play `FX_CHARACTER_RESTORE_RSI`, stand with `Action_Resurrect`, and send the caster/target resurrection lines. Their conditional ability1208 applies only when the target is already PvP; this implementation preserves the existing flag and never flags an unflagged corpse. Death Effect is retained.

The shipped catalog defines dead-player target kind9, approval60seconds, ranges4m for325 and10m for the others, casts8/4/6/2seconds and reuse120/45/30/15seconds. Costs are75/100/100/150 IS. Existing cost, cast, cooldown and control gates apply. The server offers approval after the cast completes. Requiring the caster still alive, friendly and in range at completion/acceptance is conservative server policy, not a claimed captured retail sequence.

## Native protocol evidence

- Constructor `621459b0` uses initializer `6212d3c0`, which writes opcode **81 88**. Loader `6212d360` reads a54-byte prefix and a sized name at the relative offset stored at52. `621dd390` returns that name.
- Dialog `621e6570` reads abilityu32 at2, masks the ability ID into its GOID for its name, and formats STA `aa00001d`: “%1o has been applied on you by %2s, do you want to complete it?” Title `aa00001e` is “Application Request”. Timeout is the ability's `TargetAbilityApprovalTime` through `6251f410`, default60seconds.
- Reply `621d51d0` clones the entire stored request and changes byte6 to its timeout argument and byte7 to the inverse of its acceptance argument. Yes at`62022f48` produces6=0,7=0; No at`62022c12` produces0,1; timeout at`621d5a70` produces1,0. It sends **81 88**, not the generic81 90 modal callback.
- Prefix layout from the constructors/loaders: opcode0; abilityu32 at2; timeout/refusal6/7; opaqueu32 at8;18-byte default locator at12; opaqueu32 at30;18-byte default locator at34; relative nameoffsetu16 at52. The server writes the name at54 as lengthu16 including NUL, then ASCII plus NUL.
- The server uses opaque field8 as its own per-offer correlation token and leaves the other opaque/default-locator fields zero. Their original semantic meanings are **not decoded**. The dialog's traced path reads the ability/name and retains the full request for its echo; acceptance of these defaults still needs a native check.
- Community `research/community-data/fxlisthex.txt` identifies the restore effect as`280006af`. Shipped STA lines `c600283e` / `c6002840` are the target/caster “revives” messages. Nearby opcode81 8a is inventory feedback, not an approval acknowledgment; none is invented here.

## Server lifecycle and limits

Each target has one outstanding offer; a second caster cannot replace its active native dialog. Replies must match the original request exactly except the two native response bytes. Malformed replies do not consume it; matching Yes/No/timeout consumes it once. Unique correlations prevent an old Yes accepting a later offer. Server expiry is independently enforced even if a client forges Yes after the UI's timer.

Targets must be ordinary dead players in the same district within range. Living players, duel knockdowns, opponents, self, Loading Area, world resets, disconnected players and pending Reconstruct are refused. Hostility uses `IsOpponent` directly because the usual combat `Check` returns `TargetDead` before testing hostility. Caster/target world generations and lifecycle generations pin the original cast/corpse, preventing resurrection after travel, reconstruction, another death or a completed resurrection. The target's lifecycle lock arbitrates acceptance versus Reconstruct/death. World exit changes the generation even when the following district is identical.

The self and observer receive alive/health/condition updates and the target receives its actual restored IS. Any remaining interlock is torn down. The existing verified stand transition is reused; **the script-specific Action_Resurrect animation is not yet reconstructed**. Full native animation parity is therefore not claimed. The approval frame's opaque fields, exact cast/approval timing, PvP flag timer refresh and interrupt/refund policy remain native comparison points.

## Validation and native checklist

Unit coverage exercises native frame offsets, copied Yes/No/timeout, exact expiry, malformed/truncated/forged/replayed replies, callback resources, PvP preservation, Death Effect preservation, corpse hostility/range/world gates and generation changes. Fresh-server cases cover all four real casts and approvals, observer updates, single use, refusal, timeout, forged correlation and Reconstruct rejection.

No native check has run for this change. Use the disposable NativeQa harness with CSR_Tester110 and an independent/manual client launch, taking and recording `mxo-client.lock` first. A helper caster should offer each resurrection while CSR_Tester is ordinarily dead: verify the ability/caster in Application Request, No, timeout, Yes, correct1/1 versus full resources, corpse-to-standing presentation and observers. Test accepting after clicking Reconstruct, moving the caster out of range and resetting the world. Confirm no false “susceptible to PvP” line and Death Effect retained. Restore CSR_Tester to Mara `(23828,495,13984)`, full health, flag0, mission986 only through the harness before releasing the lock. Never use TheNeo.

Local validation:3591/3591 Debug and Release units pass. The final two resurrection headless scenarios pass63.4seconds, including the added out-of-range acceptance refusal. NativeQa Release builds with a new `resurrection` fixture mode; helper JSON `cast` offers to CSR_Tester, and `approve` with `accept` (and optional `timeout`) answers only a headless helper's own pending dialog. Native CSR_Tester buttons still require Computer Use. Combined90-scenario regression and native checks remain pending.

Combined90-case run:89/90 pass679.1s. The sole failure waited for a diagnostic resurrection log line although the target received the native revive message and resource updates. DebugLog appends are unsynchronized and swallow write failures; packet state is the appropriate assertion. The headless cases now check the ability-specific native revive message, a new self-view alive/HP/IS/flags update, observer state and replay counts directly. Both revised cases pass42.5s. Earlier delivery-stall cases all pass in this run; load sensitivity remains a recorded limitation pending further stability evidence.
