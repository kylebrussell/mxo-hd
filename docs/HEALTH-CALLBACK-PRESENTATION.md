# Completed health callbacks must precede final HP presentation

Ignore Pain and other health listeners may synchronously heal or damage during `TimedEffects.HealthChanged`. A writer must reread the authoritative health after that call before producing its final HP packet, return value or defeated decision. Previously, an external heal could store120, invoke a10-point proc to reach130, then overwrite the owner's and observer's HP display with the stale120. Regeneration, NPC attacks and DOT ticks had the same ordering problem.

This correction adds no abilities, random mappings or callback policies. Fatal/zero-health safeguards are preserved. `HealSelf` now returns the completed result. NPC attacks also publish their resulting HP to existing player watchers. The existing gated `?health` command, with no argument, reports `Current health: <actual>` without mutating health; numeric arguments retain their existing behavior.

## All callback callers audited

| Caller | Presentation / decision after callback |
|---|---|
| AbilityHandler.HealSelf | Rereads health; own/watcher packets, log and returned HP use it. |
| HealthRegen.Tick | Rereads health; own/watcher packets use it. |
| Mob.HitCombatTarget (player target) | Rereads health; own/watcher packets, log and defeated decision use it. |
| PlayerEffects.Ticked (damage) | Rereads health; own HP+tick FX, watcher HP, log and death decision use it. Damage amount RPC still describes the incoming tick. |
| PvpCombat.Damage | Existing damage packets precede callback-generated updates, so they cannot overwrite the final callback result. Defeated return now reads actual health after callbacks. |
| AbilityBuffs.RefreshVitals | Serializes live attribute references after callbacks; no stale scalar HP captured in the packet. No change needed. |
| ChatHelper numeric ?health | Already rereads health after callbacks. No change needed to the setter. |
| Mob.ReceiveDamage (NPC target) | Serializes current mutable healthC after callback. NPC health listeners do not run player procs. No change needed. |

## Verification

The targeted unit checks `HealSelf` return value and final own HP bytes, natural regeneration, NPC damage, DOT HP+FX and a fatal NPC hit with fixed Ignore Pain10. Two fresh scenarios compare the **last** own HP and observer HP against a read-only authoritative query: `ability-health-presentation-heal-regen` covers an actual landed external77 heal and the next natural-regeneration tick; `ability-health-presentation-damage` covers an actual474 DOT tick. The parser retains ordering across ordinary HP and DOT HP+FX records. An earlier matching intermediate packet is insufficient.

These scenarios use fixed callback roll0 only inside the existing dev-enabled disposable database fixture. Callback effect behavior remains the PR119 implementation. Latest masterd9c4012 is integrated; its scenario-registration conflict retains both packages' cases. All previous NativeQa modes and the shared fixture plan are preserved.

## Exact native follow-up (Claude only; pending)

Use a fresh disposable fixture only when the prior native session has stopped. This agent did not run NativeQa setup or use the native client, lock, broker or main database. Do not run against another actor or TheNeo.

```powershell
$root='C:/Users/russe/Developer/mxo-wt/codex-package-p4-health-presentation'
$qa=Join-Path $env:TEMP ('mxo-p4-hp-native-'+[guid]::NewGuid().ToString('N'))
$head='<exact tested PR head>'
dotnet build "$root/tools/NativeQa/NativeQa.csproj" -c Debug
# After successful build and independently verifying native lock availability:
dotnet "$root/tools/NativeQa/bin/Debug/net8.0/NativeQa.dll" $root $qa $head health-callbacks '2,17,196,77,474'
```

1. Confirm Ready/`Im'running :D`, state.json full tested head and health-callback fixed rolls0, then independently launch/log in as CSR_Tester110. Enable helpers by creating `$qa/helpers`. Use `Send-Qa` from `docs/ABILITY-HEALTH-CALLBACKS.md` (with this fresh qaRoot). Helpers inherit this small loadout. All requests below use fresh IDs. A response acknowledges submission, not success.
2. Before196, enter `?health 100`. Activate196. Ask QAFriend to heal native: `Send-Qa 'hp01' @{actor='QAFriend';action='cast';ability=77;target='CSR_Tester'}`. After its2s cast, enter read-only `?health`. Compare the last own-view HP packet/UI with the printed authoritative health. With the10-point proc, the final display must include both the9-point external heal and the10-point proc, plus any intervening regeneration. It must not fall back by10 after the proc packet. Record packet order, not merely an intermediate expected value.
3. During the10s proc, wait for a natural2s regen tick and query `?health` again. Its final own HP must agree with the query and include regen+10 until the max-health cap. Dismiss196 with `?dismissbuff 196`; repeat an external heal or regen tick and verify no extra proc. Existing headless cases verify the final observer HP byte-for-byte; native observer rendering is not claimed here.
4. Restart the fixture fresh for a clean proc window. Enable native PvP with `?pvpflag on`; QAEnemy is already flagged. Activate196, then `Send-Qa 'hp02' @{actor='QAEnemy';action='cast';ability=474;target='CSR_Tester'}`. After its first actual DOT tick, query `?health`. The final own HP+tick-FX record must equal actual HP after the10-point proc; a later stale pre-proc DOT packet must not lower the display again. Retain the actual incoming tick amount in its damage message. Dismiss196 to compare ordinary behavior. If native input timing misses the10s listener, retry fresh rather than claim that attempt.
5. Verify fatal damage still leaves health0/dead without Ignore Pain recovery (the focused unit covers the NPC fatal branch). Close native normally, write `$qa/stop`, wait for stopped.json/full-resource/flag0/mission986 reset and normal fixture lock release. Record full head, observed values, packet order and any unavailable check. This checklist remains pending until Claude records results.
