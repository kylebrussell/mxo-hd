# Starting class upgrade toggle reuse

The shipped Operative (1060), Soldier (1061), Martial Arts Initiate (1062), Gunman (1070), and Network Hacker (1078) upgrades share `StackingType=37`, `IsAToggleAbility=1` and a 30-second reuse. The stock hotbar can send an ordinary ability request to turn off an active upgrade during that reuse. PR392's [Gunman native capture](https://github.com/kylebrussell/mxo-hd/pull/392) shows the off request at +4 seconds, a locally held inactive click at +6 seconds, and reactivation after the original timer expires. [The client disassembly](GUNMAN-PARITY-2.md#gunman-upgrade-activation-and-reuse) identifies the separate timer and active-toggle fields.

The server applies this lifecycle to the five starting upgrades. Activation starts the timer and adds the upgrade's authored modifiers. Switching off during reuse removes those modifiers without issuing a second timer. Inactive requests remain subject to the original reuse. The tests exercise the same lifetime across all five upgrades.

## Native fixtures

| Mode | Starting grants | Stock request samples |
| --- | --- | --- |
| `ability-parity-starting-operative-martial-toggles` | 1060, 1061, 1062 | `606366a42641:1470`, `0291e4b5eae0:1880`, `074ab3d3d6fe:5015` |
| `ability-parity-starting-network-hacker-toggle` | 1078 | `c59bfd3cc0aa:1594` |

Both fixtures use level 19 and explicit prerequisite ranks within the 49-point ability memory limit. Keep the original `80 b9` request, own `80 bd 01` reuse, `80 bd 05` toggle, and `80 bc` adds and removals. For each granted upgrade, activate out of interlock, use the active slot at +4 seconds, attempt an inactive click at +6 seconds, and reactivate once the original 30 seconds have passed. Each fixture's disposable replay uses the full stock request with the live Melee Dummy handle substituted for 1060. The audit retains its existing statuses for all four rows while the repeat-use native samples are pending.
