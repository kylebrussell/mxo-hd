# P4 continuation: health callbacks and Deflect / Line Up

This follow-up began on PR105 `bc6cb4a5efb49e318fed2bdff5593fe8c0ff5069` in its own worktree and integrates master `3b7ac60` after105 merged. It leaves105's native-QA branch untouched. It closes executable callback gaps, not all retail parity. Generated inventory uses deterministic previews; runtime random samples are explicit and retained in active definitions/off records.

## Source and effect table

| Ability | Source evidence | Executed behavior / limits |
|---|---|---|
| Ignore Pain196 | `tempmods.OnHealthReduceHealProc` calls `GenericConstHealthRegenEvent` without checking damageTaken. Shipped modifier: function0,10..20,10s; melee/ballistic resistance5. | Samples10..19 once at application, retains it through healing/damage callbacks and off records. External healing also invokes it. Expiry/dismissal/death stop it. The proc's health0 and recursive-delivery policies below are ours. |
| Deflect Bullets380→692 | Parent init/update `ApplyAuraEffect`,7 IS/5s, child radius3/duration5; `virii.DeflectBullets1Effect_DirectObject` schedules PlayFX after.2s. | Actual parent schedules team/caster pulses; caster-level ballistic/thrown defence and concentration10 reach recipients. Delayed sourced FX0x28000824 is guarded against a new world/life. No independent free client cast of692. |
| Deflect Virus378→693 | Same parent scheduling/drain; radius6/duration5; child `NegativeConditionSweepPulse` term value4; delayed PlayFX0x28000820. | Viral/melee defence plus an actual4% cleanse per cleansable debuff parent on child termination, including refresh/dismissal/dispel. Source callback has no expiry-only condition. Death cleanup omits proc FX/cleansing while the ordinary death path clears all effects. No free client693 cast. |
| Line Up The Shot506 | `spy.misc.LineUpTheShot_Test` returns1 if Concealment1137 is active, otherwise2. Subject result1 reapplies1137. Stored instant init `ApplyAuraEffect` names1137. Damage component has `OnHealthReduceDeactivateAbility`. | Captures prior concealment before casting and preserves it during the4s cast. Subject and instant init reapplications run without adding a new cast cost. Character-level ballistic accuracy lasts20s, damage12s. Damage while the12s callback lives removes the whole506 parent; healing does not. After12s the remaining accuracy survives damage until20s. Concealment's own damage-break remains separate. Nonspecial rifle/category and required-state eligibility are not newly invented. |
| Balance861→862 |30s health callback `ApplyAuraEffectOnHealthChange` names862. Child health type6/function0/range50..100. Client binding62521f21 pushes6 for `NeutralHeal`. | Actual neutral self-heal50..99 on external health changes, without healing influence. No damage-only filter. Child862 cannot be cast directly by a client. Recursion/zero-health policies below remain explicit. |
| Reactive Code941→942 | Toggle drains7 IS every1s; health callback names942. Child CastDirectObjects4/radius3/Neutral5/range50..150. | Actual sampled50..149 neutral damage to visible legal hostile NPCs/players within3m, sourced from the damaged/healed owner, not the original attacker. PvP legality and ordinary damage/death processing remain authoritative. External healing can trigger it. Internal942 is not a free client cast. |
| Code Conversion943→944 | Toggle drains6 IS/1s; callback rolls `random.randrange(0,100)<Value30`, reapplies parent+1. Child selector7/radius3/Heal3/range60..110. | Actual60..109 sampled healing times the owner's healing influence, to nearby live friendly players including self/non-team friends. Selector7 all-friendly interpretation reuses existing PvP/support friendliness policy; team-only selector9 is not substituted. Friendly NPC/pet healing is still absent. Internal944 cannot be client-cast. |
| Zen Master871->872 | Parent871 permanent WhenEnabled0 ApplyAuraEffectOnHealthChange names872; child instant CurrentIS+5/chance.15. Parent requires character99. | Loaded-parent damage/external healing executes one15% chance per bounded transaction; restore5IS capped at maximum, sourced line/FX. Direct872 refused. Parent is retail50-inaccessible. Native RNG sequence remains unknown. |
| Desperation1149 | `DesperationFunction` computes health-dependent Initiative and InitiativeBonus. Recovered `interlock/combat_abil_level.SelectCombatAbilityLevel` uses player.Level+tactical bonus; no corresponding stat consumer found. | **Not completed:** no invented initiative combat-order formula or record-only parity claim. |

The original buffer policy remains explicitly ours:100+4×characterLevel+stat162 bonuses. A parent consumes BufferSize once. Line Up's two timed components share one parent cost; concealment has its separate16-unit parent cost; replacing a family gets its held-buffer credit across numeric and timed executors. The inherited nonzero StackingType family replacement policy remains; full native priority semantics are not reconstructed.

## Exact random mapping

Read-only inspection of the7.6005 client.dll (SHA256 `cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a`) identifies ValueFunction0's branch at6251802e..6251804e. Import slot628682ec is `MSVCR71.dll!rand`. It multiplies the15-bit roll by signed16(max-min), shifts logically by15, adds min, narrows to signed16, then follows the existing scale/truncate path. Thus the positive callback ranges here exclude their upper endpoint:10..19,50..99,50..149,60..109. Ignore Pain samples once per modifier application; effect children sample when their new modifier is applied, separately for each recipient.

`AbilityValues.TryRandomValue(modifier,roll15,...)` implements this mapping explicitly. `TryValue` remains deterministic and refuses unknown random ranges; the general numeric matcher is not widened to random hybrids. The audit recognizes a template with roll0 and never advances runtime RNG. Server entropy uses `System.Random.Next(32768)`, and probability rolls use Next(100): **mapping/threshold equivalence, not the native MSVCR71 or Python global RNG sequence**, is claimed. Exhaustive32768-roll unit coverage verifies the positive-range mapping.

## Explicit event-delivery policies and safety

The scripts distinguish damage-only breaks from callbacks with no damage filter. The latter still receive ordinary external healing. Real zero-delta updates do not generate new callbacks. `Interlock.giveHealth` versus AbilityInv health-modifier event delivery is not recovered, so the server makes these choices explicit:

- A queued transaction captures each active parent definition/generation and its world/life generation. Removed, replaced, expired or previous-life/world listeners do not execute from an old snapshot.
- A given owner/parent runs once per synchronous transaction, including nested healing and cross-player reactive chains. This prevents infinite self-healing and reactive ping-pong. It is a server policy, not a recovered retail recursion rule.
- A transaction is bounded to256 callbacks/pending entries; truncation logs a safety-budget message. No callback code runs while the active-buff/effect-list locks are held.
- Self-healing effects do not recover a character from health0. Already-dead/duel-dead listeners are skipped. A health0 but not-yet-dead Reactive owner can still produce its sourced non-healing reapplication before ordinary death processing; child healing ignores dead/zero-health recipients. Exact retail fatal-event ordering remains unverified.
- Drain equality pays the IS and keeps the toggle active; the next insufficient tick deactivates it. Existing generation/world guards stop stale drain timers. Beneficial-aura/off-time and numeric-toggle cooldowns reuse the existing server policy; native default timing for new parents without an AddRecastTimer callback remains unverified. A dispel removes the active parent before further callback dispatch.

The dev `?health` command now presents the **post-callback** health value, rather than overwriting the display with its pre-proc requested number. This only affects the existing gated QA command; regular health packets keep their normal path.

## Fixture, validation and exact native checklist

No native client, lock, broker or main database was used. NativeQa `--build-info` remains a read-only early exit before fixture setup. The new **health-callbacks** mode uses the existing disposable setup/reset lifecycle, gives the actors2000 IS, supplies small custom loadouts and enables dev commands. It sets QA roll15=0 and roll100=0 so the checklist is reproducible. These overrides are honored only when **both** MXO_DEV_COMMANDS=1 and MXO_DB_NAME starts `mxotest_`. The state file/console record the overrides. This is not a distribution sample: it forces minimum magnitudes and successful chance tests, including693's4% term sweep.

Claude alone runs this fixture and launches/drives the native client. Do not start it while another native session owns the lock; never pre-create/delete someone else's lock. Use only copied CSR_Tester110, never TheNeo.

```powershell
$root='C:/Users/russe/Developer/mxo-wt/codex-package-p4-health-callbacks'
$qa=Join-Path $env:TEMP ('mxo-p4-callback-native-'+[guid]::NewGuid().ToString('N'))
$head='<exact tested follow-up commit>'
dotnet build "$root/tools/NativeQa/NativeQa.csproj" -c Debug
# Only after build success, and after confirming the prior native session stopped:
dotnet "$root/tools/NativeQa/bin/Debug/net8.0/NativeQa.dll" $root $qa $head health-callbacks '2,17,196,861'
```

Wait for Ready and `Im'running :D`, verify state.json fixed rolls, then launch/login independently. To start helper actors and make exact requests from a second terminal:

```powershell
$qa='<exact fresh qaRoot from terminal1>'
if (!(Test-Path "$qa/helpers")) { New-Item -ItemType File -Path "$qa/helpers" }
function Send-Qa([string]$id,[hashtable]$request) {
  $pending=Join-Path $qa "requests/$id.tmp"; $target=Join-Path $qa "requests/$id.json"
  if (Test-Path $target) { throw 'Use a fresh request ID' }
  $request | ConvertTo-Json -Compress | Set-Content -LiteralPath $pending -Encoding utf8
  Move-Item -LiteralPath $pending -Destination $target
  $response=Join-Path $qa "responses/$id.json"; $until=(Get-Date).AddSeconds(60)
  while (!(Test-Path $response)) {
    if ((Get-Date) -gt $until) { throw "No response: $id" }; Start-Sleep -Milliseconds 500
  }
  $reply=Get-Content -LiteralPath $response -Raw | ConvertFrom-Json
  if (!$reply.Ok) { throw $reply.Error }; $reply
}
```

A helper response means submission, not native success. Wait for actual packet/UI effects. Keep native at Mara23828,495,13984 for the fixed helper positions, or move helpers correspondingly.

1. **Ignore Pain/Balance:** use196, then enter `?health 50` immediately: expect60 before natural regeneration. Enter `?health 200` while the10s listener remains: expect210, proving an external increase also triggers it once. `?dismissbuff 196`; `?health 50` must now stay50 before regen. Cast861, wait its2s cast, `?health 50`→100 and `?health 200`→250. Die while861 is active: remain a corpse/0 HP, listener gone. Reconstruct through the native UI. Record actual values/events rather than only the dismissal acknowledgement.
2. Stop normally and restart fresh with `'2,17,941,943'`. Enable native PvP with `?pvpflag on`; QAEnemy is an enemy already flagged, within3m. Use941, then `?health 50`: observe Reactive damage50 to QAEnemy and7 IS/s drain. For a bounded reaction check, `Send-Qa '101-reactive' @{actor='QAEnemy';action='cast';ability=941;target='CSR_Tester'}`; wait2s for its activation, then native `?health 300`. Expect one50 response back (native250 before regen), not an unbounded loop. Dismiss941 on native and helper: `Send-Qa '102-off' @{actor='QAEnemy';action='chat';text='?dismissbuff 941'}`.
3. Use943. Set the nearby non-team QAFriend's health: `Send-Qa '103-friend-hp' @{actor='QAFriend';action='chat';text='?health 100'}`. Native `?health 50` must trigger its forced-success chance, heal native by60 and QAFriend by60 at minimum influence, drain6 IS/s, and exclude QAEnemy. Dismiss943 and repeat a native health change: no further conversion. Note that normal chance is30%, not100%; the fixture forces this roll.
4. Restart fresh `'2,17,378,380,481'`. `Send-Qa '201-team' @{actor='QAFriend';action='invite';target='CSR_Tester'}` and accept the native team dialog. QAFriend starts2.78m away at character20. `Send-Qa '202-bullets' @{actor='QAFriend';action='cast';ability=380;target='CSR_Tester'}`: verify repeated5s native692 defence19/9 plus concentration10, and delayed Deflect FX. Toggle380 off with a fresh request of the same action; wait for its last5s pulse to expire. Start378 similarly:693 viral/melee defence19/9 and7 IS/5s source drain. Native flag on; `Send-Qa '203-overflow' @{actor='QAEnemy';action='cast';ability=481;target='CSR_Tester'}`. Its debuff must be cleansed at the next693 termination within a pulse interval (forced roll0<4), not merely show numeric defence. Stop378 and verify the remaining child expires. Record source/recipient distinctions and the deterministic-roll override.
5. Restart fresh `'2,17,1137,506'`. Activate1137, then506. Concealment must remain during506's4s cast. At completion native50 gets ballistic accuracy64 for20s and damage25 for12s; subject result1 and init reapply1137. An early `?health 1` must end concealment and both506 components. After reuse, try506 without pre-concealment if the native UI permits it: subject result2 still gets the init1137 path; otherwise record the client gate rather than forcing it. Wait until the12s damage component expires, then `?health 1`: concealment ends but remaining accuracy must last until20s. Healing while components live must not break them.
6. For expiry/dispel/drain cleanup, repeat one listener with a low IS pool only if a separately verified dev path supplies it; otherwise use the sourced drain units/fresh evidence and record native drain display. No unverified resource-setting command is assumed. Dismiss/expire861 and confirm later health changes do not heal. Close native client, write `$qa/stop`, let the fixture terminate normally, inspect stopped.json reset/full resources/flag0/mission986 and lock release. Log full commit, loadout, overrides and observed native result before Claude merges.

Unit tests cover sampled snapshots, exhaustive range mapping, external healing, bounded nested listeners, health0/death, stale generations/expiry,693 term cleansing, two506 durations, damage/healing break rules, family replacement and buffer accounting. Four fresh scenarios exercise those live protocol paths, including legal reactive counter-damage, non-team friendly conversion and real parent Deflect scheduling. Any failed or unavailable native interaction must stay explicitly pending.

## Validation at the follow-up head

Debug and Release each pass all3,727 unit tests. Ten unique fresh-headless scenarios pass: `ability-health-proc-lifecycle`, `ability-health-deflect-lineup`, `ability-health-lineup-timings`, `ability-health-reactive-area`, `ability-buff-buffer-death`, `ability-heal-magnitudes`, `ability-control-duration-bonuses`, `ability-beneficial-groups`, `stealth-breaks`, and `hostile-fields-pvp`. The final reactive case includes two opposing listeners and non-team friendly conversion. NativeQa builds and read-only configuration-matching `--build-info` probes pass in Debug and Release. Re-running ToolAbilityAudit and build_audit.py reproduces all three generated artifacts byte for byte. The inventory on this inherited branch is511partial/36cosmetic/14missing; that is classification, not retail-completeness proof. Native execution of this follow-up checklist remains pending Claude.

The master integration preserves all shared NativeQa modes and unloaded discipline-folder ownership. Its only conflict was state.json metadata, resolved by retaining both ancestor IDs and callback-QA fields. Both configuration-matching build-info probes resolve the complete callback loadout to unloaded ancestors236/383/483/508/871/945 without lock or database access. Callback runtime merged without conflicts, so the ten prior fresh cases remain applicable.

After merging master3b7ac60, the integrated full Release suite passes all3,730 tests (0failed/0skipped,1m13s). Debug/Release NativeQa build and read-only loadout probes pass at this integration head.
