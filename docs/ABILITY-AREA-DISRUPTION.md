# Area Disruption38: caster-centred hostile targeting

Area Disruption's own shipped description says: Use this area-effect viral attack to stun nearby enemies for a short time. Its raw selector4 and native UI identify enemies within radius of self. The old route required a selected target within2m, expanded only selector3 and did nothing without selection. Only38 now joins the reviewed97 producer at successful ordinary cast completion. No other selector4 rows or internal callbacks are enabled;47 still has conflicting near-target text.

## Primary evidence and unchanged effects

Client DLL SHA256cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a; GOB SHA2563e47f11012f6f219a26c9f5decfa10cbc00726c397937d03c3068254e5424103. Raw38 stores radius8m, selection2m, cast2s, IS14, reuse43s, UDB0, state102/Stun3s and AddControlImmunityTimer input8s. These are source values, not a recovered measured38 retail capture. Existing control mitigation/stacking/immunity/expiry and zero direct damage remain authoritative. No captured override exists for38 in the current Retail dictionaries.

Packed client STA SHA2569640747067a1f0b05c81e2a966ae2897822b5684d03905fb2ea3eb7312770d24: Info7c0000c4 Area Disruption; MoreInfo7c0000c5 nearby-enemies description. Native62515c90 reads CastDirectObjects, UI621e2424/switch621e26c8 index4 points621e25c2, which uses7a0001cb Enemies and7a0001ce Affects: %z within %Im of self. This proves centre/category intent, not all engine LOS/admission predicates.

The shared CasterHostileArea allowlist is exactly97 or38. The old UiLagArea API remains97-only. Existing97 values, fixtures and per-recipient behavior remain unchanged. Each legal current recipient gets one existing normal free-cast contest before ordinary landing. Existing same-world/floor/current-view/PvP/pet rules are server policy. World/life generations precede centre snapshot and are rechecked after snapshot and before every recipient. There is no emitter, pulse or new join authority.

**OURS existing policy:** centre is caster position at completion; native snapshot time is unrecovered. Ordinary movement is not itself a server cast-cancel predicate.38 lacks a stored IsInterruptable flag, so no damage-interrupt default is invented. Existing2s cast ownership/control/world/life/end/generation guards, resource/recast behavior, sourceCastingScript148 and presentation remain unchanged. Existing signed/f32 stat177 UDB0 radius helper is reused; no75/link/magnitude claim.

## Fixture inventory and Claude-only one pass

Discovered `area-disruption-hit` and `area-disruption-deflect`: CSR110 only at17000/495/2300, level50, organization2, owned/loaded2/17/1006/38,5000HP/2000IS, heading0. Existing PvP-server rules flag level50; no GM or flag injection. Strict dev1/actualvalidmxotest per-contest draws only (hit32766/0 or deflect0/32766); generic combat/health/callback/drop RNG unchanged. `?qaload 38` refreshes this already-owned ability through normal80ae if needed; no internal38freegrant.

QAEnemy is exactly8m at17800/495/2300, QAFar8.01m at17801, QAFriend same-org1m and QAOther1m away/+2m above. All same-floor positions have sourceNAV floor400. Source testNPC table was inventoried: none lies within8m of17000/2300; WinterFrost is84.06m away, not4m. Headless also asserts actual createdNPC views are absent inside8m before player casts. A separate NPC actor inventories and asserts every legal live NPC view in the24499/495/-1498 cluster, preserving duplicate names as distinct recipients.

1. Use actual Actions Area Disruption with no enemy selected. Observe ordinary2s cast, IS14 and43s reuse.
2. Hit profile: QAEnemy should receive source3s stun and existing8s immunity; QAFar/QAFriend/QAOther/caster excluded. Deflect profile: QAEnemy receives result but no state/HP change.
3. After natural immunity/reuse, select QAFar and cast again: selected target must not move the centre or become a required recipient. Do not bypass timers with a command.
4. Record actual native range/type labels, cast/control icons/expiry and geometry. Headless is not native rendering or PvP UI proof. If real NPCs unexpectedly appear within8m, record all legitimate recipients rather than assuming onlyone.

## Validation

Full4522 tests PASS56s before upstream-only189 import; final106 scoped tests PASS, including both97/38 registry-lock lifecycle barriers and upstream mission definitions. Both actual NativeQa builds,38 hit/deflect plans/provenance pass; old97 Plan and Environment are byte-equivalent after JSON decoding. Python12 policy and exact CRLF3-artifact roundtrip pass. Current ignored all-module audit:561playercandidates/grants,843other;532partial/27cosmetic/1missing/1inaccessible. Historical tracked CSV/JSON/Markdown are exact master. Fresh and native UI remain PENDING; source review/slot grant required before fresh. No generated ability snapshots are committed;38 remains PARTIAL, not full retail parity.
