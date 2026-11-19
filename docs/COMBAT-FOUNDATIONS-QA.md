# Shared combat foundations QA

## What changed

- NPC and PvP gunfire check the sight path when firing. Hostile targeted abilities check it at completion. A hostile area checks each recipient from the area center. The client scripts use `physics.clearLine` in `combatvalidate.py:CombatPhysicsOkay` and `ability/utility.py:GetClosestEnemyInSight`. The server samples its existing NAV floor mesh. A mapped floor break or a floor-height difference at covered endpoints can block a shot. Missing endpoint data preserves prior targeting. Windows, walls over continuous floors, and airborne sight still require collision geometry.
- Shipped templates for Bullet Spray 247, Suppression Fire 248, Pinning Fire 492 and Direct Fire 493 store CastDirectObjects 13, RadiusOfEffect 8 m and AngleOfEffect 45. The implementation treats 45° as the full horizontal arc and aims from the caster toward the selected enemy at impact. The native interpretation of the arc and axis still needs validation. The target-centered radius and reviewed caster-centered areas retain their centers.
- Logic Barrage 4.0 uses the existing catalog-selected viral resistance path. The fixture compares its captured base 424 against the existing normalized curve with +75 viral resistance at level 50, yielding 348. See [typed resistance evidence](ABILITY-TYPED-RESISTANCE.md).
- The existing viral contest and queued typed contests retain their current formulas. Ballistic free-cast contest inputs and final retail hit/deflection behavior remain unverified. Shipped client scripts reviewed for this change cover combat validation and target queries. NPC social-assist triggers, pull eligibility, group radius and reset rules still need server-side evidence.

## Disposable headless scenario

Run from the branch checkout after building `tools/NativeQa`.

```powershell
dotnet tools/HeadlessClient/bin/Debug/net8.0/HeadlessClient.dll --parallel 1 --filter combat-foundations
```

The ranged activation uses the five-byte `41 <view u16> <spawn u16>` record captured in the tutorial Uriah fight around line 13648. The companion `40` is a separate message. [Capture index and bytes](COMBAT-SYSTEM-ROADMAP.md)

The casts use the fixed client.dll inline producer at `621e5100` and serializer at `621d343f`. The harness writes the full 43-byte `80 b9` and takes each target's view and spawn from its current creation record. The ground, object, inventory and team values are fixture inputs. The precise incidental field values for native Direct Fire still need a capture. [Producer and field widths](ABILITY-USE-RECORD.md)

## Native checks for Claude

Use mode `combat-foundations` with disposable CSR110. Keep System chat and target health visible. The mode seeds a rifle in bag slot 1, dual SMGs in slot 2, level-50 style/ability loadout, and deterministic draws for the existing viral contest.

1. Enter `?combatdepth foundations-sight`. Equip the rifle. Select AB1 Blocked, use Free Fire, then Logic Barrage 4.0. Neither should lower the target's HP. The blocked cast uses ordinary cost and reuse. After the four-second reuse, cast Logic Barrage on AB1 Clear. Expect 424 damage.
2. Enter `?combatdepth foundations-cone`. Equip the dual SMGs and use Direct Fire on AB1 Aim. Expect damage on AB1 Aim and AB1 Inside. AB1 Outside is within 8 m but stays at full health. Capture the actual stock Direct Fire request and note any weapon, style or target prerequisite reported by the client.
3. Use Logic Barrage 4.0 on AB1 Resisted. Expect 348 damage, 76 less than the clear target. Capture the combat log and target health.
4. Shut down through the existing fixture stop procedure and report the outcome against the exact head.
