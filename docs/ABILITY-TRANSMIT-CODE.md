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
