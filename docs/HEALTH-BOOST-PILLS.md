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

Claude only, on the exact PR head: use a real owned modern health boost pill through inventory right-click/radial (and double-click if available); confirm one-second cast, one item consumed, visible600s boost icon, correct maxHP increase without instant heal and enforced30s own reuse. Record the client display separately: it was absent in the limited check below, and retail presentation remains unverified. Try a selected other player and confirm only the caster gains the boost. Dismiss through the native buff UI and verify maxHP/currentHP cap; try while stunned and below the item's required level, then while eligible. Check cancel/death/zone behavior and600s natural expiry. Record actual CastingScript112/114 animation and stacking-priority behavior independently. No native client operation, fixture setup or merge was performed by Codex.


## Limited native evidence and short integration recheck

[Claude's real-client check of d4c833b](https://github.com/kylebrussell/mxo-hd/pull/139#issuecomment-6829613001) used NativeQa `actions`, Debug. Native `?additem 39412` twice made an owned stack. Right-click -> Activate Item reached188, consumed2->1, showed a buff icon and emitted stat106 +200 for600s. Health readings showed ordinary regeneration rather than an instant pill heal. A later pill replaced the boost; immediate reuse was refused without another cast or consumption. No exceptions were observed.

The refused reuse showed **no client cooldown message or30s timer**. This is an explicit presentation gap; retail behavior is unverified, and no packet fix is invented here. Native coverage did **not** include another-player selection, buff-UI dismissal, level/stun denial, moved-item cancellation or zoning. Other variants, natural600s expiry and native stacking priority remain unproven. Unit/headless checks are separate evidence. Claude requested a short native recheck after the master136/137 dispatch union; this limited result is not a blanket merge-ready claim.

Concrete Claude-only recheck after exact-head CI: use matching-config NativeQa `actions`, log CSR_Tester into the world, enter native `?additem 39412` twice, find the actual stack (slot12 was observed previously but is not guaranteed), and right-click -> Activate Item. Observe188/+200/600s, one consumed and no instant heal; reuse immediately and record refusal/unchanged stack plus the known missing client timer/message. Opening analyser bag2 first supplies a real writer-interruption case. Setup/lock lifecycle remains Claude-only. Report every additional checklist item above individually as tested or untested.

Claude-only setup command, after CI and matching Debug build: `dotnet tools/NativeQa/bin/Debug/net8.0/NativeQa.dll C:/Users/russe/Developer/mxo-wt/codex-pill-health-boost <fresh-qaRoot> <pinned-full-SHA> actions` (run from this checkout). Use a new output directory; do not reuse another fixture.
