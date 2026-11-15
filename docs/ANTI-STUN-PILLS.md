# Anti-stun pills: owned item effect

Items 42763/42764 now activate abilities 971/972 through the ordinary item request. They remove the user's Stun and Mez modifier tuples and shorten existing control immunity according to the retail script. They remain item/internal templates, not new player-loadable abilities; parity is partial and native acceptance is pending.

## Source contract

`research/client-scripts/tempmods.py:2024`, `AntiStunPillEffect`, iterates current tempmods. For every target StunAbility or MezAbility it calls `removeTempMod(parent,target)`. For each parent ControlImmunityTimerAbility it computes `TimeLeft / 2` and calls `addTempMod(734,681,0,time)` only when the result is at least one second. This preserves unrelated root, powerless, stat and damage records, including siblings from the same parent. A remaining timer shorter than two seconds is left alone, not removed or lengthened.

The original 7.6005 GOB explicitly links AntiStunPill1 (42763) to 971 and AntiStunPill2 (42764) to 972. Both store `InvItem/CanUseWhileStunned=1` and `InvItem/IsAConsumable=1`. This permission belongs to the exact owned items; it grants no free cast or general control bypass. The matching ability records store a one-second cast, own recast 900/600 seconds, and shared family973 whose stored recast is600 seconds. They attach the `AntiStunPillEffect` init callback. `data/anti_stun_pills.json` retains the raw item records, including prerequisite GOIDs.

Client DLL SHA256: `cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a`. GOB SHA256: `3e47f11012f6f219a26c9f5decfa10cbc00726c397937d03c3068254e5424103`.

```powershell
python research/abilities/extract_anti_stun_pills.py --client C:/Users/russe/Developer/mxo-client/MxO
dotnet run --project tools/AbilityAudit
python research/abilities/build_audit.py
```

## Authority and lifecycle

The supported routes are existing `80 b9` with the actual bag slot and item flag, and existing `80 c6 <slot u16>` pill action. The native four-byte80c6 constructor at `0x621d35c0` writes header `80 c6` and the default slot at payload+2; see `IGNORED-CLIENT-ACTIONS.md`. No new request is invented.

Both routes resolve the authenticated character's owned slot before granting the stun exception. Plain971/972 requests, wrong items, missing items and invalid slots cannot reach the callback. Ordinary pills remain blocked while stunned or pacified. A legal cast interrupts the current Compile or Write Code UI session. Dead, duel-defeated, disconnected, Loading Area, world-changing, already-casting and cooling-down users are refused.

The pending cast records world/life generation and cast end. At completion it rechecks those values, current readiness and the item in the original slot before consuming one stack unit, starting stored own/shared timers and applying the cure. Moving the item, zoning, death or a changed cast cancels the pending effect. Nothing is granted into inventory. The consumable route uses the existing no-IS-charge item policy rather than the generic missing-cost fallback. The stored activation text/FX and native item cooldown display remain unverified; no complete retail parity is claimed.

No TimedEffects code was changed: the helper uses existing snapshot/remove/off-record/apply/refresh APIs. Removing a control state sends its ordinary linked off records and restores the existing movement/action gates; immunity refresh emits the old off record before its new on record.

## Verification

Unit coverage checks both source item mappings/permissions and stored cooldowns, Stun/Mez tuple removal with root/blind/powerless siblings retained, and actual remaining immunity at8/2/1.9/0.5 seconds. Fresh `anti-stun-pills` uses real owned stacks and ordinary packets for both variants, proves the free request and ordinary pill refusals while stunned, observes cure and immunity refresh, checks one-unit consumption and cross-variant shared cooldown, and cancels casts by item movement and a normal Loading Area transition. The developer stun fixture supplies the real315 state and734 timer; it does not invoke the pill effect or bypass item authority.

```powershell
dotnet test tests/hds.Tests/hds.Tests.csproj
dotnet build tools/HeadlessClient/HeadlessClient.csproj
dotnet tools/HeadlessClient/bin/Debug/net8.0/HeadlessClient.dll --parallel 1 --filter anti-stun --logs <unique-own-output-folder>
```

The additional fresh `anti-stun-writer-interruption` scenario opens Write Code first, then activates an owned anti-stun pill. It verifies writer linked-off before the pill cast bar, no early consumption, consumption at landing and an inactive writer session afterward.

## Native review: Claude only

Claude reported the native checklist passed on the original feature head. The integration below preserves that feature code, translating only `CompileSession.End` to the shared `CraftingSession.End`. The following procedure remains available for future regressions.

Use only the reviewer-owned disposable fixture and CSR_Tester110. The exact full PR head, configuration-specific source/snapshot hashes and commands in the PR body take precedence. Codex has not run fixture setup, acquired client locks or interacted with the client.

1. Build the pinned head in Release and run read-only `NativeQa --build-info <worktree> "2,17,315,40"` and `--fixture-plan styles`. Compare the hds.dll hash with the same Release snapshot before launching the reviewer-owned fixture. The setup shape is `NativeQa <worktree> <unique-reviewer-fixture> <full-head> styles "2,17,315,40"`; setup belongs exclusively to Claude.
2. In that disposable fixture obtain42763 and42764 through the existing developer `?additem` command. Apply `?combatdepth stun`. Ordinary actions and a normal health pill must remain blocked. Use the actual inventory item via right-click -> Activate Item (or its available Use label), rather than a free971 ability. The one-second cast must complete, the stun icon/action gate must clear early, and only one item must be spent.
3. While the shared600-second timer is active, attempt the other variant: no consumption or second cure. Verify own900/600 timers and the immunity remaining-time reduction against packet logs. Use a fresh disposable user/session to check the second variant independently; do not wait out long timers or change main character data.
4. On a fresh owned pill, start the cast and move the item or exit to Loading Area. It must remain unspent and produce no completed cure/cooldown. With overlapping root/debuff controls, curing Stun/Mez must retain unrelated icons and restrictions. Record actual native menu sender, icon clearing and cooldown display; retain any presentation limitations noted in the native review.
