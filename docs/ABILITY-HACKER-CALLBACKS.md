# Hacker aura and proc checks

Use `hacker-callbacks` from NativeQa for six Hacker aura checks. The bootstrap logs in a disposable helper using stock 05 and 0c 01 world entry, then requires an actual WinterFrost view. It replays the exact captured Combat Aura 2.0 and Deflect Virus 1.0 casts from proxy_024, checks the child damage-stat record and matched Deflect FX on/off record, then dismisses both through the decoded stock 80 bb producer. Deflect Virus's dismissal itself is captured at proxy_024 line 13475. The other four aura cases need stock Actions casts as CSR_Tester. Keep the full stock casts for later replay. Use the separate Mobius modes for the seventh case.

| Ability | Action and expected result |
|---|---|
| Miasma 1.0 (61) | Stand near WinterFrost, toggle on and wait one update. The child 701 applies -14 melee and ballistic defense for 5 s. Initial IS 5 and upkeep 3 every 5 s. Toggle off and observe natural expiry of the last child. |
| Static Field (89) | Stand near WinterFrost, toggle on and wait one update. The child 694 uses raw viral damage 1 before influence. Initial IS 11 and upkeep 12 every 5 s. Observe the activation line and damage in range. |
| Combat Aura 1.0 (42) | Toggle on and inspect Combat stats. The child 695 refreshes melee and ballistic accuracy every 5 s. Initial IS 8 and upkeep 12 every 5 s. |
| Combat Aura 2.0 (485) | Toggle on and inspect Bonuses. The child 696 refreshes melee and ballistic damage every 5 s. Initial IS 15 and upkeep 15 every 5 s. |
| Deflect Virus 1.0 (378) | Toggle on and inspect viral and melee defense. The child 693 lasts 5 s. Observe the 0.2 s effect 0x28000820 and upkeep 7 every 5 s after initial IS 10. The sourced cleanse rolls 4% per eligible debuff at child termination. Own reuse starts at activation and lasts 10 s. |
| Deflect Bullets 1.0 (380) | Toggle on and inspect ballistic and thrown defense plus Concentration +10. The child 692 lasts 5 s. Observe the 0.2 s effect 0x28000824 and upkeep 7 every 5 s after initial IS 10. |
| Mobius Code (967) | Use `mobius-free-attack-positive` and `mobius-free-attack-negative`. Read the scoped chance-control, HP, IS, FX and forced-entry steps in the Mobius checklist. [Mobius checklist](ABILITY-MOBIUS-FREE-ATTACK.md) |

Dismiss the active buff through the native buff UI before switching cases. The verified stock producer uses Shift + right click. The parent linked modifiers for Combat Aura 2.0 and Deflect Virus 1.0 keep the same record IDs through dismissal. A world reset, quit or death cancels the old source's scheduler. Delayed Deflect FX requires that the recipient passed the buff admission and remains in the same world and life. A previously applied pulse keeps its own lifetime. Cross-source child stacking policy, full native toggle/FX order and the remaining engine callers for crash, corrosion, disable, virus vaccination and reflection still require evidence.
