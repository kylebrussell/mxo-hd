# Antibiotics and the condition sweep pill

The inventory paths now execute the existing ability-owned sweep after the cast completes. Antibiotic items 39406..39410 triggers 674..678, with an initial pulse and a 5-second update for 90 seconds; per-parent chances are 5/10/15/20/25 percent. Item 38539 triggers 659, with 25 percent chance every 5 seconds for 20 seconds and no initial pulse. This is bounded execution coverage, pending native QA.

## Source evidence

`research/abilities/extract_catalog.py:extract(client, raw_goids=True)` reads the shipped 7.6005 GOB without modifying the client. Raw item records contain `TriggeredAbility/ReqAbilityID` equal to `0x80000000 | abilityID << 10`, `ReqAbilityLevel=1`, `InvItem/IsAConsumable=1`, and `Usable/ExecuteRuleID=10001`. Antibiotics 2..5 have `CharStatsPrerequisite/ReqCharLevel=10/20/30/40`; antibiotic 1 and 38539 do not store that field. Thus these mappings are not name pairings. Item records have no StatModifier component; the referenced canonical ability owns the modifier and callbacks.

The canonical records in `data/ability_catalog.json` store the durations, chances, callbacks and intervals above. Antibiotics have StackingType 46; 659 has 14. Existing `AbilityHostile.ApplySweep` handles same-family replacement and `TimedEffects` handles records, expiry and debuff-family removal. This patch adds the missing initial callback and connects inventory completion to that executor.

`research/client-scripts/tempmods.py:182..310` selects debuff ability origins and nonexempt item origins, rejects equipping effects, follows `GetParentAbils`, rolls once per parent and removes all child modifiers. The removal guard is explicitly `if not tempMod.IsItem`, referring to the sweeping modifier. It is not inverted here: invoking an item-triggered canonical ability is distinct from an item-owned equipment modifier. Client.dll SHA256 `cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a` exposes `IsAbility` at 0x625e0ae8 (flags+0x2c bit 1) and `IsItem` at 0x625e0b0f (bit 2). Current server effect producers represent ability-owned parents; this patch does not invent item-owned/equipping producers or new parent-graph metadata. Existing `RemoveParent` remains the removal route, preserving linked records and inherited terminal handling.

The zombie grant removals and RSI/cloak reversion in the antibiotic callback require actual modeled zombie grants/capture states; those state producers remain outside this patch. They are not represented by speculative flags. Full retail parity is not claimed.

## Inventory execution

The existing 80b9 item-slot and 80c6 right-click paths share `InventoryHandler.UseItem`. They preserve casting time, use `RemoveOne` for stack consumption, start the item/shared timer at completion, and apply the canonical sweep. Shared family timers are reserved during casting. Moved items, death/duel defeat, incapacitation, disconnect and district changes cancel completion without spending the item or adding an effect. Level requirements are checked before casting. No normal learned-ability ownership bypass is added.

## Verification and Claude-only native checklist

Unit tests cover every antibiotic's initial cleanse, per-tier chance/duration/interval, later pulses, expiry, retained control immunity,659's lack of an initial pulse, and same-family replacement. Fresh headless scenarios use actual 80b9 and 80c6, check one-unit stack consumption, a sibling family request during cast, timed 659 removal and level refusal. A control cast landing during a pill cast checks cancellation, preserved stack and legal retry after stun. Wire tests do not prove native presentation.

Claude alone performs native QA and merge, on the exact PR head: use the actual inventory right-click/radial UI for antibiotic and 38539; verify one unit spent, 1 second cast, shared cooldown gray-out, visible buff duration/expiry and a cleansable debuff removed by a successful roll. Check a buff and control immunity remain, lower-level antibiotic refusal, cast cancellation after item move/world/life/control change, and no duplicate cast from double-click. Record working UI path and screenshots/packet evidence. Do not use production authority bypasses or substitute a console command for the inventory path.
