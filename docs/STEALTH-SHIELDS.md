# Stealth throws and evasion state

1110 Bull Dog now applies its terminal effect and sends a sourced builtin throw motion. 1125 Disable Evasion now runs through its normal hostile cast, suppresses the target's Evade Combat child state, and schedules the caster's guarded restealth callback. Both remain **partial** retail coverage. No shield resistance, shield-health baseline or Gaussian520 contest is invented.

## Evidence and limits

* GOB1125 is `SneakFreeDisableShieldAbility`: hostile target3, direct-object0, casting4s, range8m, RequiredStates524288 (EvasionState624), RequiresTool1/category1951355977 (`ITOt`). It is not an automatically granted child: only Spy155/SpyBuff1113 reference it as a prerequisite. The normal ability handler automatically executes its script callbacks after a validated cast; no manual1125-only fixture executor is substituted. The category gate uses1537 GOB IDs exported by `research/abilities/extract_stealth_throw_tools.py`; carried inventory slots below97 qualify. Specific tool subclasses, weapon attack resolution/back-arc and the native free-throw hit contest remain unresolved. NPCs have no supported217 toggle parent, so1125 rejects them rather than pretending an isolated624 modifier is a functional shield.
* `ability/spy/misc.py:719` first calls `AbilityThrowFreeAttack`. Success schedules a caster-owned681 marker for1.5s with `DisableShieldRestealth`; its signed value is the longest remaining invisibility duration, otherwise-1 for Sneak. We capture the current stealth before ending it, then schedule on landed/validated cast. Exact native Test-versus-cast timing remains unverified. `tempmods.py:2390` rejects stance8, then restores1116 for a positive integer duration or1111 otherwise. `misc.py:303` consumes the positive GhostStrike override. Extra action/stealth-version/world/life guards prevent delayed resurrection after cancellation, death or zoning.
* `tempmods.py:2380/2385` removes only the `(EvadeCombat217,EvasionState624)` child and later reapplies217. Suppression lasts8s from GOB target710. Parent217 stays active with its original record IDs; its callbacks are retained, with no new cost/recast. Restoration checks the same parent generation/world/life, so turning217 off, unloading, death or zoning cannot recreate it. Own/observer/late-view health/state presentation preserves other flag bits and P4 blur/disguise state.
* Before this change217 was a toggle/child-record/shield-attribute presentation, with no gameplay resistance consumer. `EvadeCombatEvent` drains supplied IS when a shield resistance event occurs; no periodic drain is proven. `combat_calculations.py:233 GetShieldDamageValue` and `GetShieldResistanceISDrainValue` exist, but native wrappers6258d880/6258d8d0 have no recovered caller beyond registration. Maximum shield HP, normalized ff scale, attack eligibility and520 probability are not recovered. Suppression/restoration therefore changes supported state and presentation; it **does not prove fewer incoming hits or allow unconditional interlock resistance**. Gaussian49 retains its P4 total520 modifier without a fabricated new contest.
* GOB1110 uses target710, value10, duration.5s and **TermFunc NeutralDOT**, with no UpdateFuncInterval. It does not define generic direct damage. The descriptor is zero-initialized at62520998;62520cda reads the interval without a fallback;625e8b23 copies it into the tempmod;625e0875 exposes it. Literal script arithmetic with zero interval yields zero. The engine override is unknown. **OURS: apply raw10 once on natural.5s expiry**, rather than a guessed50..80 instant hit. It neither adds the P5 sneak bonus nor P4 typed thrown damage bonuses. Removal/replacement/death cancels this terminal callback. PvP legality and cast-start target identity/world/life are rechecked; NPC death generation is guarded. Ordinary HP, death, reward and health callbacks handle the resulting damage.
* `ability/spy/sneaks.py:172` requests `StanceAggro/Action_FreeAttackWeaponSMKnifeThrow` with a target. `data/character_movement_evidence.json` identifies builtin Aggro12 and KnifeThrow64. Native6267192c consumes the builtin byte; named25/29 have a different u16 layout. Target-kind1 at62671a21..55 resolves a receiver-local view/spawn handle with bit0x40 clear. Own and observer records use their own handles; late views retry once actor and target are created. **OURS:1.5s replay/idle-cleanup window**, since clip length was not recovered. New actions/death/world changes cancel stale motion replay. Native visual acceptance remains pending.

## Automated validation

`StealthShieldTests` covers independent builtin bytes, GOB/script shape, terminal-only expiry, tuple-specific681/710 replacement, parent identity/record retention, unload/off/death/world guards, cast-start target identity/life, restealth cancellation, fractional invis override and retained Gaussian blur bits. Five disposable fresh-server scenarios exercise normal casts: `stealth-shields-suppression`, `stealth-shields-lifecycle`, `stealth-shields-terminal-cancel`, `stealth-shields-bulldog-pvp` (including observer and late view), and `stealth-shields-bulldog-npc`. These passes do not establish native rendering or retail damage parity.

## One-pass native checklist (Claude only)

The P5 agent builds the harness but never starts NativeQa, takes its lock, uses broker requests, drives the client or changes the main DB. Claude uses **CSR_Tester110 only** through CSR_AbilityQA. The harness clones110 to a throwaway DB and owns normal stop/reset. Never use TheNeo/charid2.

```powershell
$root='C:/Users/russe/Developer/mxo-wt/codex-package-p5-stealth-shields'
$qa=Join-Path $env:TEMP ('mxo-p5-shields-native-'+[guid]::NewGuid().ToString('N'))
$head=(git -C $root rev-parse HEAD).Trim()
dotnet build "$root/tools/NativeQa/NativeQa.csproj" -c Release
# Start only after exit0. Record the printed path and actual head:
Write-Output $qa
dotnet "$root/tools/NativeQa/bin/Release/net8.0/NativeQa.dll" $root $qa $head stealth-shields
```

Wait for state.json Ready and verify the source/snapshot SHA256 match. Log in natively as CSR_AbilityQA/CSR_Tester. The fixture enables MXO_DEV_COMMANDS=1, supplies carried tool531, and loads1110/1125/217/1111/1116. QAEnemy is the disposable headless PvP target. In a second PowerShell use the saved `$qa` with this atomic helper (Claude only):

```powershell
function Send-Qa($name,$request) {
  $tmp=Join-Path $qa ($name+'.tmp')
  $out=Join-Path $qa ('requests/'+$name+'.json')
  $reply=Join-Path $qa ('responses/'+$name+'.json')
  $request | ConvertTo-Json -Compress | Set-Content -LiteralPath $tmp -Encoding utf8
  Move-Item -LiteralPath $tmp -Destination $out
  $until=(Get-Date).AddSeconds(60)
  while(!(Test-Path -LiteralPath $reply)) {
    if((Get-Date) -gt $until) { throw 'Helper timeout; inspect fixture logs' }
    Start-Sleep -Milliseconds 500
  }
  $result=Get-Content -LiteralPath $reply -Raw | ConvertFrom-Json
  if(!$result.Ok) { throw $result.Error }
}
Send-Qa '001-flag' @{actor='QAEnemy';action='chat';text='?pvpflag on'}
Send-Qa '002-shield' @{actor='QAEnemy';action='cast';ability=217;target='QAEnemy'}
```

1. Select QAEnemy, enter `?stealth invis`, then `?combatdepth cast 1125`. Confirm four-second cast, target217/624 child removal for8s, shield presentation clears then returns without a217 off/on toggle or replacement record IDs. Caster681 lasts1.5s after landing, then invisibility returns; record the actual displayed duration. No HP damage should occur. Capture own and observer state/attribute traffic plus visible shield behavior; resistance is not an expected assertion.
2. After1125's45s cooldown, repeat invis/1125. Immediately after landing run `Send-Qa '003-off' @{actor='QAEnemy';action='cast';ability=217;target='QAEnemy'}` to turn the retained parent off. Enter `?die` before1.5s if timing permits. Confirm target217 never returns after8s, and dead caster never restealths. Failure to meet the1.5s window is an unexecuted death subcheck, not a pass. Reenter the fixture with full resources if necessary; keep the same build hash and record any restart.
3. On a live NPC select it, enter `?stealth invis`, then `?combatdepth cast 1110`. Expect one raw10 terminal hit after.5s, one sourced builtin knife throw and idle cleanup at the documented1.5s window. Observe `[STEALTH-FREE]1110 terminal10 OURS` and the HP tick; generic/sneak/typedthrow extra damage is a failure. Repeat on live PvP-flagged QAEnemy after cooldown, recording own targeted motion and helper HP5000→4990 before regeneration. This validates the explicit fallback, not retail-exact amount.
4. For a native observer-role check enter `?pvpflag on`, then run `Send-Qa '004-invis' @{actor='QAEnemy';action='chat';text='?stealth invis'}` and `Send-Qa '005-throw' @{actor='QAEnemy';action='cast';ability=1110;target='CSR_Tester'}`. Observe the targeted builtin motion and cleanup on QAEnemy. Record native visual acceptance separately from fresh-server late-view packet coverage.
5. Enter `?stealth off`, `?combatdepth off`. Close the client, create `$qa/stop`, and allow normal harness exit. Inspect stopped.json and verify main110 Mara23828,495,13984, full HP/resources, flag0, missions986 only and released lock. Record head hash, source/snapshot SHA256, outcomes and unresolved visuals. Claude alone decides native acceptance/merge.
