# Awakened and Gunman parity, first batch

Hyper-Sense now replaces and is replaced by the other three Hyper defenses. All four GOB templates use StackingType 40. The new replay starts with Hyper-Sense and verifies signed removal and add records through Hyper-Deflect, Hyper-Block, and Hyper-Dodge. The focused tests connect those modifiers to their separate incoming attack contests.

## Native retest order

| Mode | Rows a passing run can close | Procedure |
| --- | --- | --- |
| `data-mining` | 540, 541, 621 (3); also sample 215 | Use the printed handheld and goggles setup. Record 541's yellow ping, 540's light-object aura and removal, and 621's use and ten-second expiry. Capture 215 independently so its unresolved skill dependency stays separate. |
| `awakened-hyper-defenses` | 1100, 1101 (2) | Use the four defenses in the printed order. Record the outgoing casts, old icon and bonus removal, and displayed deltas of +47 thrown, +77 viral, +52 melee, and +51 ballistic. |
| `health-callbacks` | 196 (1) | Use the Ignore Pain subsection of the existing fixture manual. Compare both resistance deltas, the 10 s duration, one heal after damage, one after external healing, and no proc after dismissal. The fixed roll is a magnitude control. [Checklist](ABILITY-HEALTH-CALLBACKS.md) |

A passing observation applies to the listed row only when its complete checks match. The current generated audit carries the per-row source, runtime and executable test evidence.

## Thirty-row review

| IDs | Executable coverage and remaining work |
| --- | --- |
| 1, 6 | Matching ballistic/melee damage mitigation, other damage category and 20 s expiry. Existing stock Awakened self-buff evidence completes both rows. |
| 5, 8 | Thrown/viral resistance consumers and expiry verified. Exact retail typed-resistance bases and ordering remain open, with 8 also needing a native sample. |
| 2 | Rank-dependent detection and Self Defense grant tested through load and unload. Native grant/contest behavior and UpdateCombatStyle remain open. |
| 9, 15 | Mutual refusal, actual close-combat damage direction, and return to baseline tested. Periodic interlock FX and native behavior remain open. |
| 10, 1099 | Captured ballistic/melee defense fed to the appropriate contest and removed during family replacement. Existing native family evidence completes both rows. |
| 1100, 1101 | Viral/thrown contest and all replacement records verified. The native status deltas remain open. |
| 11 | Movement and control gate have an executor. Rank-dependent range and the Super Jump stat link remain open. |
| 12 | Captured movement packet and 600 s HUD proof exist. Stealth movement consumers and callback coverage remain open. |
| 13 | Learned rank 20 gives +40 Melee Damage. Physical influence changes with activation and removal. Existing drain and shortfall tests pass. Periodic Hyper-Strength FX and stock proof remain open. |
| 14 | Power Shot ships a function-4 IS modifier and a physical hit range. Actual damage, VS target admission and IS scaling remain unresolved. |
| 16 | The 1 IS/s scheduled drain consumes available IS and terminates on shortfall. Vulnerability state admission and exact damage linkage remain open. |
| 17 | Existing style and round tests cover selection. The complete nine-stat consumer matrix remains open. |
| 184 | Captured +0.15 motion update and movement bit are tested. The shipped 25 IS per 5 s movement-aware drain and stealth/invisibility movement behavior remain open. |
| 196 | Melee/ballistic resistance, sampled health callback, lifecycle and expiry verified through the real health paths. Needs the native callbacks run. |
| 197 | The -10 Melee Accuracy effect reaches the target's melee attack for 20 s. Damage scaling and complete admission remain open. |
| 198 | The delivered Cheap Shot exchange uses ILDB 285. Damage scaling and admission remain open. |
| 215 | Base locator script, tool ownership and 10 s execution verified. The numeric Locate Data Node Skill link remains open. |
| 217 | Shield suppression and lifecycle executor exist. Shield HP and exact incoming damage interaction remain open. |
| 298 | Radial visibility/reveal has a runnable replay. Stored area, stats and target contest scope remain open. |
| 628 | Loaded Concentration +30 reaches the interruption contest against the same damage and roll. The native cast-through comparison and complete template eligibility remain open. |
| 1149 | Health snapshot, initiative add and expiration exercised against real interlock calculations. The retail consumer remains open. |
| 540, 541, 621 | Device-specific aura and yellow ping, independent mastery activation, cleanup and expiry checked. Stock UI evidence remains open. |
| 497 | Ballistic defense, Powerless duration and grant/unload exercised. Shipped 1046 Initiative Bonus and direct 1044 are distinct. Their linked retail coefficient and native proof remain open. |
