# Hacker ability parity, first batch

The batch covers 30 rows in Arbalest, Artillerist, Ballista and Corpsman. The audit [method](ABILITY-AUDIT.md) counts a row as done after its executable checks and stock-family requirement pass. The [batch evidence](../research/abilities/evidence/hacker-a1-runtime.json) names the specific checked source, executor and test.

## Grouped stock sweep

Run `ability-parity-hacker-sweep` with NativeQa on a disposable profile. The default profile uses 48 of 100 level-50 memory slots after loading ancestors. Use only CSR_Tester in the stock client. QAFriend, QAFar and QAOther stay in-world at the staging positions printed at READY. This disposable profile holds natural health regeneration so the target wounds survive setup. Keep the default Self Defense style, current loadout and starting position.

After CSR finishes entering the world, submit this helper request and wait for its response. The same connected helpers move into CSR's view range to create fresh player views on the three stands.

```json
{"actor":"QAFriend","action":"hacker-group-arrive"}
```

| Stock operation | Closeable rows | Required evidence |
| --- | --- | --- |
| Restore Group 2.0 and Group Repairs 2.0 | 371, 372 | Recruit QAFriend (4 m) and QAFar (9.25 m), leaving QAOther (3 m) outside the team. With the staged Resist Damage passive giving +24% radius, the bounds are about 8.68 and 9.92 m. 371 heals CSR and QAFriend for 22. 372 heals CSR, QAFriend and QAFar for 19. Record exact HP changes, the four-second cast and the corresponding reuse records. |
| Combat Training and Weapons Boost | 373, 374 | Use each on self through Actions. Status shows +64 Melee Accuracy and +64 Ballistic Accuracy respectively. Cast Weapons Boost while Combat Training is active and record the matching removal. Weapons Boost expires naturally. Headless tests also compare the +25 level-20 result and underlying attack inputs. [Stock replacement sample](https://github.com/kylebrussell/mxo-hd/pull/333#issuecomment-7007872279) |

Invite the two helpers separately with the stock **Recruit to Mission** option. For each invitation, submit the following request to the fixture, substituting `QAFar` for the second invite.

```json
{"actor":"QAFriend","action":"accept","inviter":"CSR_Tester"}
```

A fully passing default sweep could close **four** unique rows. Also record whether Corpsman's grant (369→1092) appears on entry. The separate Buff Area Radius Bonus consumer for 369 remains unresolved, so it is an evidence sample.

Three other rows already have complete headless checks and need a stock grant lifecycle in a legal alternate loadout. Arbalest (310), Ballista (334) and Artillerist (335) should register grants 1083, 1084 and 1086, then remove and restore them on normal unload/reload. The first two should also lose and regain their own +1% and +5% Viral Damage Bonus. Their ancestor set uses 79 memory, while merging it with the default profile would use 119. Keep the profiles separate. The prior stock review measured a combined +24% viral influence across four producers, so it did not isolate either grant parent's damage contribution. [Loaded stock build](https://github.com/kylebrussell/mxo-hd/pull/322#issuecomment-7001788160)

The mode's `AddHackerBSections` and `AddHackerCSections` hooks let the other Hacker lanes contribute distinct selections, instructions and self-checks through additional partial class files. Check the combined ancestor memory before adding sections. The disposable headless bootstrap verifies the three fresh creations on the CSR profile and runs the cast checks through a separate helper.

```powershell
dotnet build tools/NativeQa -c Release
dotnet tools/NativeQa/bin/Release/net8.0/NativeQa.dll --fixture-plan ability-parity-hacker-sweep
dotnet tools/NativeQa/bin/Release/net8.0/NativeQa.dll --bootstrap-checks "<repository>" "<proof-directory>" ability-parity-hacker-sweep
```

## Remaining batch checks

| Rows | Evidence and open behavior |
| --- | --- |
| 309, 346 | Loaded modifiers feed actual viral damage and, for Stand Off, the linked concentration input. The native construction and paid-cost contribution of stat 1053 remain open. |
| 311, 312, 313, 329, 330, 331 | Exact-ID hostile contests show that a deflection does not apply damage or effects. 330 has the stock sight and resistance result. The raw damage-to-final coefficient and success/deflection interlock presentations remain open where relevant. [Native sight and resistance](https://github.com/kylebrussell/mxo-hd/pull/321#issuecomment-7001527488) |
| 315, 333 | Timed application checks prove 7/14 and 5/10 state/immunity windows, damage retention, refusal, and reapplication after immunity. Stock recipients observed blocked actions and immunity announcements. Full targeting and interlock outcomes remain open. [Stun 1.0 recipient](https://github.com/kylebrussell/mxo-hd/pull/103#issuecomment-6812841733) · [Stun 2.0](https://github.com/kylebrussell/mxo-hd/pull/313#issuecomment-6998807737) |
| 336, 337, 343 | A deleting or not-yet-created selected NPC can no longer anchor a hostile area. Real deflections still allow independently contested secondaries. The Miasma −21 defense effects execute and expire. Area geometry, typed damage and remaining target branches still need checks. |
| 314, 344, 345 | Captured values remain in use. 314 applies +108 rather than the raw function-3 preview. 344 applies captured +22 defense/+24 cost reduction rather than raw +20/+15. 345 applies captured +21 Virus Area Radius rather than raw +12. The corresponding hit/defense/cost/radius consumers already execute. Preserve the value discrepancies until the capture contexts can be reconciled. [Upgrade Attacks Status](https://github.com/kylebrussell/mxo-hd/pull/315#issuecomment-6999207958) · [Range Booster evidence](ABILITY-DEBUFF-RADIUS.md) |
| 328, 369, 370 | Code Branching has linked resistance/defense sources still to reconcile. Corpsman grant membership is proved separately from the radius bonus, and Dedication still needs its Sweep Bonus consumer. |
| 332 | Emergency Repairs 2.0 has a stored total of 76, a 2.5 s interval and 19 actual four-point pre-expiry ticks. The HOT overflow callback remains open. |
| 1083, 1084, 1086 | The self-only class modifiers feed damage, viral accuracy, cast-through and Artillerist's target radius. Verify actual class-upgrade admission and replacement as separate behaviors. |
