# Transmit Code593 — OURS typed resistance bonuses

Transmit Code's existing loaded modifiers produce Thrown Resistance Bonus1146 and Viral Resistance Bonus1148. Their stored raw owners link1033→1146 and157→1148. Learned50 gives10 each. The authoritative retail owner coefficient remains unknown; this implementation is explicitly **OURS**.

For typed health damage, convert each existing capped bonus percentage once:

```
bonusPoints = trunc((4 * defenderLevel +20) * bonusPercent /100)
total = existingToughness144 + existingRawResistance + bonusPoints
```

Type7 uses1146; type2 uses1148. The level scale is the existing resistance curve's `4*level+20` range. Existing stat readers still enforce their existing caps, and the unchanged normalized raw curve retains its floor, clamp and zero-modifier behavior. Raw resistance is not converted again. Signed bonus values retain signed truncation; saturated arithmetic prevents overflow. No native2.0 owner coefficient or generic attribute-link traversal is invented.

At equal level50,10 bonus means22 resistance points. The existing typed curve then gives538→510 viral and83→78 thrown for the declared caster profile; this is not a flat10% reduction of health damage. The implementation is in the common typed adapter, covering direct player-to-NPC/PvP health damage, both DOT paths and both conditional-throw components. Existing guards/costs/RNG and the593 producer/load behavior are unchanged. Physical/ranged/unknown types, self/heal bypass, untyped specials, aura/health-callback damage and fixed EMP damage retain their existing behavior. No blanket viral/thrown immunity or complete retail parity is claimed.

## Native fixture

Mode `transmit-code-resistance` starts CSR at (17000,495,6400) facing the helper QAEnemy at (17000,495,6600). Both are on open outdoor NAV, away from the ice. No `?tp` or NPC selection is required. If an NPC appears behind CSR, strafe, turn the camera and single-click the intended name. CSR owns 593 with every ranked prerequisite loaded. The ability itself starts unloaded.

1. Stay at spawn. Send helper request `{"actor":"QAEnemy","action":"cast","ability":330,"target":"CSR_Tester"}`. The helper uses the complete stock Logic Barrage from PR321 (PacketLog `7a4fc7efcf1c:1742`), rebinding only the current target view and spawn. Its legal L50 profile contributes +27 viral influence from Hacker Style, plus +15, +3, +1 and +5 from abilities 95, 309, 310 and 334. Expect a two-second cast, 13 IS on QAEnemy, four-second reuse and 640 damage to CSR with zero resisted.
2. Type `?qaload 593`. This aid invokes the normal owned Archive handler. Check source 593 adds of +10 for both stats 1146 and 1148. Wait six seconds from the previous helper request, then repeat it. The same raw 640 now produces 606 damage and 34 resisted. The typed log reports 22 resistance points from the current bonus policy.
3. Type `?qaunload 593`. Check both modifier removals and repeat the same helper cast after reuse. It returns to 640 damage with zero resisted. The replay also compares each prerequisite's persisted Archive entry across both transitions.

The two gated viral contest draws ensure the attack lands without modifying resistance, damage, cooldowns or global rolls. Native observes the normal recipient damage and modifier records through the Archive bridge. Loading Area UI behavior, native thrown/DOT observations and the original Transmit Code coefficient remain open. The historical `transmit-code-resistance` HeadlessClient scenario uses an earlier narrow attacker seed and separately exercises the thrown and DOT consumers.

## Qualified proof (2026-11-15)

Runtime and data were frozen at `414235242e82543186ab987a855fce95996d4e25` on master `08d961eb`; the passing driver/mode source is `9a83c02f5d6027833bf94fb662f5ca8136573ab9`. Later commits only corrected fixture expectations and documentation. Full Release:5936 passed at41423524; final focused Release:85 passed at9a83. Debug/Release builds, exact discovery, actual plans and the Temp-only audit row593 passed; audit status is partial with conditional OURS consumer labels. No generated coverage snapshots are committed.

The normal case passed51.9s with held driver exit0. Exact received target8167 records prove593 off:viral538/absorbed0 and thrown83/0; normally load593:viral510/28 and thrown78/5, each total22. The fresh normal474 modifier carried Value1103/Duration30; the independent2.5s formula gives raw92, with actual final87/absorbed5. Caster167RPC/37viewlists and defender79RPC/16viewlists had zero parser problems. All parent ability rows remained identical across normal593 unload/load. This proves the declared aid-controlled before/after consumer, not native UI or retail parity.

Actual selected Debug HDS SHA256: `78484059172DF422C01DB5DF7CF7B43DFB9B8607C05B3A2B8E5C8F6E07C2E261`; Release driver: `8596A295DCBE0A0F33B2F2F6C623FB1E4AA1726A96BD35FE027879881AE04FF9`; Release runner: `E7C082E628D33F144FB3ACC66C0EF9C799EEB8E11A354898D4540DFB4F92B979`. Normal raw proof: `%TEMP%/mxo-transmit-code-mxotest_run_1_eacc8cdd99.json`, SHA256 `C047179C201A9B85F728778F995D77740F11EDE4DD26CD9F5927450716CE7C2D`. Driver31600/server51296; exact schema count0/all11 observed creation identities gone at20:23:15Z. Descendant polling is sampled, not an exhaustive OS history.

One actual explicit custom-mode bootstrap passed at the same pins:runner55284/server45776, schema`mxotest_bootstrap_0e20b03967`, one real QAEnemy normal Hacker-style hook, no shared no-op substitution. Held exit0; exact schema count0/all11 observed identities gone at20:24:33Z. The proof root is `%TEMP%/mxo-transmit-code-resistance-20261115`; native-proof/summary.json records actual source and declared/seeded rows. This is seed/authentication/hook validation, not native rendering or another gameplay run.

Retained failures remain failures:41423524 case40.6s expected unstyled424 after selecting Hacker;820e8600 case60.6s passed all four direct comparisons but wrongly predicted a fixed271 DOT tick. The final oracle reads the actual current modifier's random total without changing its roll. Both exact schemas were independently absent and all7/all9 observed identities gone. Supervisor preflight refusals (localized mtime and .NET current-directory mismatch during reflection) launched no server and are separately retained. No automatic game retry, cooldown reset or gameplay change was used for either correction.