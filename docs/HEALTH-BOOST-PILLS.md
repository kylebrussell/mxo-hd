# Owned health boost pills

The health boost bag items now enter an owned-item cast path and use the existing stored `AbilityBuffs` executor. No ability ownership or IS cost is fabricated for the internal triggered ability. Ordinary ability requests cannot grant a free boost, including requests from a character carrying a pill without selecting it.

## Source

`research/abilities/extract_health_boost_pills.py --client <read-only MxO>` reproduces `data/health_boost_pills.json` directly from the shipped GOB, retaining raw item and ability GOIDs and omitted fields. The GOB hash is recorded in the export. The source client.dll used for surrounding native research is SHA256 `cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a`.

| Item | Triggered ability | Stored minimum character level | Max HP | Duration | Own recast |
|---|---|---:|---:|---:|---:|
|2407 MaxHealthBoostPill|187 PillBolsterHealthAbility|absent|+100|600s|30s|
|39411 MaxHealthBoostPill1|672 PillBolsterHealth1Ability|10|+100|600s|30s|
|39412 MaxHealthBoostPill2|188 PillBolsterHealth2Ability|20|+200|600s|30s|
|39413 MaxHealthBoostPill3|189 PillBolsterHealth3Ability|30|+300|600s|30s|
|39414 MaxHealthBoostPill4|673 PillBolsterHealth4Ability|40|+400|600s|30s|

The exact item `TriggeredAbility/ReqAbilityID` confirms the legacy2407→187 pair. All five store `IsAConsumable=1`, `Usable/Defaultable=1`, execute rule10001 and triggered level10. The level10 field does not establish that a character must learn this internal ability; no learned-ability prerequisite is invented. No `CanUseWhileStunned` permission or `FamilyRecastTimer` is stored. The previous shared HealPill10s mapping was inferred by name and has been removed for these items. Their stored own30s timers govern reuse; there is no invented shared group.

Each ability stores cast1s, CastDirectObjects1, CastValidTarget0, CastWhatHow3, CastWhenUsed2, one fixed stat106 modifier enabled2, duration type0 and stacking type3. Legacy187 stores CastingScript112 and priority15; modern variants store script114 and priorities10/20/30/40. The exact animation semantics of scripts112/114 are unverified. These numeric IDs are not assumed to be Python function IDs. The server uses its existing cast bar and stat modifier record presentation; this work does not claim native animation parity. General family replacement currently follows the existing buff executor; exact native stacking priority behavior remains a gap, including lower-priority reuse.

## Authority and lifecycle

Both inventory80b9 and pill80c6 resolve the actual owned slot and item. At cast start and finish, ready-world, life, normal control and character-level gates apply. A per-client cast token and world/life generations prevent late grants; moved items cancel without spending. A second item cast cannot overlap another cast. Successful completion applies the sourced caster buff, spends exactly one item with the existing `RemoveOne` API and starts the stored ability cooldown. Existing buff-buffer admission, record generations, expiry, dismiss/death/zone cleanup, vital recomputation and currentHP clamping are reused. Raising maximum HP does not grant a heal.

## Validation and native checklist

Focused units cover every source mapping,600s modifier,30s own/group0 timers, maxHP changes without healing, simulated expiry with currentHP cap and family replacement. Fresh headless `health-boost-pills` exercises all five owned bag items, both wire entry points, free/wrong-item rejection, consumption after cast, cooldown denial, buff dismissal, level denial, moved/zoning cast cancellation and normal stun denial. Headless protocol assertions cannot establish acceptance by the real client.

Claude only, on the exact PR head: use a real owned modern health boost pill through inventory right-click/radial (and double-click if available); confirm one-second cast, one item consumed, visible600s boost icon, correct maxHP increase without instant heal,30s own reuse display and no unsupported HealPill10s display. Try a selected other player and confirm only the caster gains the boost. Dismiss through the native buff UI and verify maxHP/currentHP cap; try while stunned and below the item's required level, then while eligible. Check cancel/death/zone behavior and600s natural expiry. Record actual CastingScript112/114 animation and stacking-priority behavior independently. No native client operation, fixture setup or merge was performed by Codex.
