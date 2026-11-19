# Awakened resistance stock capture

Use `awakened-mind-resistance` as level-50 CSR110 and enter `?pvpflag on` before the first attack. The isolated fixture keeps the existing scoped 1135 draw of 65 and fixes eligible viral contest draws at attack 32766 and defense 0. It leaves callback, control and regeneration behavior ordinary. The expected damage below is from the current server model.

1. Select QAEnemy and activate Calm Mind, Calm Body (5) from Actions. Capture the caster's +75 thrown resistance record, the 20 Inner Strength debit and 60-second reuse. Have the armed helper use 1135 on CSR110 with its ordinary action request. Expect 68 damage and 15 resisted during the buff. After its 20-second natural expiry, repeat 1135 from the same helper and expect 83 damage and 0 resisted.
2. Keep the same selected opponent and activate Empty Mind (8). Capture the caster's +75 viral resistance, the 20 Inner Strength debit and 60-second reuse. Use normal helper 330 on CSR110 for 348 damage and 76 resisted.
3. Before Empty Mind expires, request one 474 infection from the same helper. Save the tick damage and resisted operands before and after natural expiry, from that single infection. The summed underlying damage should be the same on both sides. After expiry, a second 330 from the same attacker should deal 424 damage and 0 resisted.

Use the existing fixture helper action, for example `{"actor":"QAEnemy","action":"cast","ability":330,"target":"CSR_Tester"}`. Keep the outgoing stock requests, the buff add/remove and own resource packets, and the paired incoming damage and resisted values. Stock PR #312 observed ability 5 activation and expiry. This run targets the missing sourced values and Empty Mind stock activation. [PR #312 stock evidence](https://github.com/kylebrussell/mxo-hd/pull/312#issuecomment-6995363576)

Neither template declares a control or regeneration callback. The focused tests exercise the existing stun gate and natural health and Inner Strength tick inputs while the resistance buff is present. The native resistance caller and coefficients remain independent source checks.

See the [typed resistance source inventory](ABILITY-TYPED-RESISTANCE.md) for the known inputs and unresolved caller.
