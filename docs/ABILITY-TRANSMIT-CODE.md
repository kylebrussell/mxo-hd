# Transmit Code593 — OURS typed resistance bonuses

Transmit Code's existing loaded modifiers produce Thrown Resistance Bonus1146 and Viral Resistance Bonus1148. Their stored raw owners link1033→1146 and157→1148. Learned50 gives10 each. The authoritative retail owner coefficient remains unknown; this implementation is explicitly **OURS**.

For typed health damage, convert each existing capped bonus percentage once:

```
bonusPoints = trunc((4 * defenderLevel +20) * bonusPercent /100)
total = existingToughness144 + existingRawResistance + bonusPoints
```

Type7 uses1146; type2 uses1148. The level scale is the existing resistance curve's `4*level+20` range. Existing stat readers still enforce their existing caps, and the unchanged normalized raw curve retains its floor, clamp and zero-modifier behavior. Raw resistance is not converted again. Signed bonus values retain signed truncation; saturated arithmetic prevents overflow. No native2.0 owner coefficient or generic attribute-link traversal is invented.

At equal level50,10 bonus means22 resistance points. The existing typed curve then gives538→510 viral and83→78 thrown for the declared caster profile; this is not a flat10% reduction of health damage. The implementation is in the common typed adapter, covering direct player-to-NPC/PvP health damage, both DOT paths and both conditional-throw components. Existing guards/costs/RNG and the593 producer/load behavior are unchanged. Physical/ranged/unknown types, self/heal bypass, untyped specials, aura/health-callback damage and fixed EMP damage retain their existing behavior. No blanket viral/thrown immunity or complete retail parity is claimed.

## Case and controls

Case/mode `transmit-code-resistance` uses level50 opposing disposable actors. The defender owns the full593 prerequisite graph before Margin and keeps every parent row identical while normally unloading/reloading593. The caster has the same qualified typed-resistance loadout throughout, so unrelated parent damage bonuses cannot explain the change. Normal style selection fixes Hacker27/Spy29 influence. Actual received8167 values are decoded through complete offsets/counts/names, not substring matching or the production damage calculator. Existing own reuse timer removal is observed before reusing spells.

Controls are only the existing strict disposable1135raw draw65 and eligible free-cast viral attack/defence draws32766/0. They change no global combat/callback RNG and inject no resistance buff. The fixture's hostile-server entry profile and seeded level/owned abilities are aids. Headless normal80ae memory requests do not prove the native Hardline/LA UI.

## One-pass native checklist

1. Use mode `transmit-code-resistance`, CSR110 only.593 starts owned/unloaded with the same prerequisites already loaded. Select QAEnemy normally. Its fixed qualified caster loadout is separately installed before its login.
2. Use ordinary helper self-style/cast requests: Hacker1006 then330 at CSR110 (expect538); Spy1112 then1135 at CSR110 (expect83). Wait normal cooldowns. The helper requests are standard80b9 casts, not injected damage.
3. Load593 through ordinary Hardline/Loading Area memory UI. Return normally. Verify actual593→1146/1148+10 records. Repeat the same styles/spells:510 viral and78 thrown. Observe normal cast/cost/reuse and health loss.
4. Optional DOT control: select helper Hacker1006, then normal474 at CSR110; its normal866..880 total scales once with Hacker+27; derive each raw92/93 tick from the fresh sourced Value/30×2.5 formula, then expect87/88 under the same current bonus. No cooldown reset. Unload593 normally and verify both sourced removals; no parent loadout change.

Helper request shapes: `{"actor":"QAEnemy","action":"cast","target":"QAEnemy","ability":1006}` for style, and `{"actor":"QAEnemy","action":"cast","target":"CSR_Tester110","ability":330}` for incoming damage; use the actual selected native handle if it differs. No native result is claimed by this initial source checkpoint.

## Qualified proof (2026-11-15)

Runtime and data were frozen at `414235242e82543186ab987a855fce95996d4e25` on master `08d961eb`; the passing driver/mode source is `9a83c02f5d6027833bf94fb662f5ca8136573ab9`. Later commits only corrected fixture expectations and documentation. Full Release:5936 passed at41423524; final focused Release:85 passed at9a83. Debug/Release builds, exact discovery, actual plans and the Temp-only audit row593 passed; audit status is partial with conditional OURS consumer labels. No generated coverage snapshots are committed.

The normal case passed51.9s with held driver exit0. Exact received target8167 records prove593 off:viral538/absorbed0 and thrown83/0; normally load593:viral510/28 and thrown78/5, each total22. The fresh normal474 modifier carried Value1103/Duration30; the independent2.5s formula gives raw92, with actual final87/absorbed5. Caster167RPC/37viewlists and defender79RPC/16viewlists had zero parser problems. All parent ability rows remained identical across normal593 unload/load. This proves the declared aid-controlled before/after consumer, not native UI or retail parity.

Actual selected Debug HDS SHA256: `78484059172DF422C01DB5DF7CF7B43DFB9B8607C05B3A2B8E5C8F6E07C2E261`; Release driver: `8596A295DCBE0A0F33B2F2F6C623FB1E4AA1726A96BD35FE027879881AE04FF9`; Release runner: `E7C082E628D33F144FB3ACC66C0EF9C799EEB8E11A354898D4540DFB4F92B979`. Normal raw proof: `%TEMP%/mxo-transmit-code-mxotest_run_1_eacc8cdd99.json`, SHA256 `C047179C201A9B85F728778F995D77740F11EDE4DD26CD9F5927450716CE7C2D`. Driver31600/server51296; exact schema count0/all11 observed creation identities gone at20:23:15Z. Descendant polling is sampled, not an exhaustive OS history.

One actual explicit custom-mode bootstrap passed at the same pins:runner55284/server45776, schema`mxotest_bootstrap_0e20b03967`, one real QAEnemy normal Hacker-style hook, no shared no-op substitution. Held exit0; exact schema count0/all11 observed identities gone at20:24:33Z. The proof root is `%TEMP%/mxo-transmit-code-resistance-20261115`; native-proof/summary.json records actual source and declared/seeded rows. This is seed/authentication/hook validation, not native rendering or another gameplay run.

Retained failures remain failures:41423524 case40.6s expected unstyled424 after selecting Hacker;820e8600 case60.6s passed all four direct comparisons but wrongly predicted a fixed271 DOT tick. The final oracle reads the actual current modifier's random total without changing its roll. Both exact schemas were independently absent and all7/all9 observed identities gone. Supervisor preflight refusals (localized mtime and .NET current-directory mismatch during reflection) launched no server and are separately retained. No automatic game retry, cooldown reset or gameplay change was used for either correction.