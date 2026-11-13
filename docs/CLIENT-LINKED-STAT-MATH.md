# Complete client linked-total path: 177 <- 1055

Offline client source report, 2026-11-12. Offsets are hexadecimal. This is evidence, not a server implementation/default policy. The primary client.dll SHA256 is cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a.

## Conclusion and authority boundary

The initial client template's owner scale zero produces a zero contribution from the 177 <- 1055 LINK under the traced path and ordinary finite inputs. There is no zero-scale replacement, additive unscaled linked value, exponent or normalization after that multiplication. This does NOT establish that retail ability75 has no effect: the direct177 total, inventory value and runtime coefficient can be supplied or changed independently, including potentially server-derived/replicated totals. The compact client GOB is not proven to be the authoritative server template.

The shared getter's concrete radius caller is client selection/display code, not a recovered authoritative retail server executor. All algebra below is about this client implementation and its flags, not a server-default policy recommendation.

## Notation and exact predicates

S16(x) = signed interpretation of low16 bits. I32(x) = signed32 machine arithmetic. F(x) = binary32 store. T(x) = native626a92d8 truncation toward zero (the full helper adjusts x87 FISTP to truncation); only ordinary finite representable inputs are claimed.

- Rank(q): 62515800 reads AbilityRank, NOT ValueType.
- Class(q): 625158d0 reads AbilityClass.
- A1(q): Rank=1 AND Class in {1,2}; A2(q): Rank=2 AND Class in {1,2}.
- Present(q): map at inventory+110 contains q.
- Mask(q): membership in the separate modifier-state set tested by625e3f90. Its precise semantic name is not assigned here.
- I(q): map node+14 word. Treat this as an actor inventory/base value; do not invent its actual magnitude.
- D(q):625e3d40 reads the modifier aggregate's node+12 word; absent ->0. It is a direct subtotal, not the linked contribution.
- M: runtime float reached through inventory+104 -> pointer -> float. This belongs to the queried inventory/actor, not the linked template. A named metadata binding/default was not independently closed in this pass.
- L: byte reached from the same inventory's parent object (inventory+28), CharacterBase component13, member+54 -> pointer -> byte; component absent ->0. This is the level cap input. Component13 is independently named CharacterBase by GOB component data.
- S: console variable current integer62b11ee0, StartAttributeEffectLevel.
- G: console variable current integer62b04928, BuffBonusScaleCapDefault.

## Wrapper625c4e90(q, includeModifiers, unusedFlag, detailVector)

The method returns AX, so consumers sign-extending AX see S16 of the result. The third argument at stack+10 is not read in this method's complete body; do not assign it invented authority semantics.

1. If Present(q) AND NOT Mask(q):
   - includeModifiers=0: return S16(I(q)).
   - includeModifiers!=0: return S16(I(q) + B(q, cap=1, details)).
   The inventory/base word is added AFTER the modifier calculation's caps. There is no final cap on that sum in this wrapper.
2. Otherwise, if (A1(q) OR A2(q)) AND includeModifiers!=0, return B(q, cap=1, details).
3. Otherwise return0.

Addresses:625c4ea4..4edf and625c4ee6..4f30. Recursive linked reads push (linkedID,1,1,0), so they include modifiers, use cap=1 internally, and do not request recursive detail vectors.

## Complete modifier path B =625c47a0(q, capFlag, details)

1. Compute A1/A2. If neither ordinary inventory admission (Present AND NOT Mask) nor A1/A2 holds, emit its diagnostic and return0 (625c47f5..48a0).
2. Read direct subtotal D(q). If Class(q) is NOT1 or2, add D(160). Classes1/2 skip that generic addition (625c48a4..48d1). For177 and1055, Class=2, so D160 is not added.
3. If details is non-null, clear its existing entries (625c48d1..4908).
4. If neither A1 nor A2, skip the link branch and retain the direct subtotal.
5. Otherwise ask link map6251d780 for q. A failed lookup skips the branch. Initial177 has the single pair(1055,0); initial1055 has(82,2).
6. For each pair(r,c):
   - If r==q, emit the self-link error, clean temporary vectors and return0. This guard is before/independent of c, so c=0 does not bypass it.
   - h = S16(GetTotal(r,1,1,null)).
   - If h<S: z=0.
   - Else: z = T(F(I32(h-S)) * c).
   - term = S16(z). The low16 narrowing is real; there is no saturation here.
   - sum = F(sum + F(term)), starting at float0. If requested, append detail(r,term), including zero contributions.
   Addresses625c4950..49d2.
7. After ALL links, there are two more narrowing/scale stages:
   - k = S16(T(sum)).
   - result = T(F(k) * M + F(S16(directSubtotal))).
   Multiplication by M and addition of the direct subtotal occur before that final truncation. The result is retained in a32-bit local, but later comparisons/returns use its signed low16.
   Addresses625c49d8..4a14. There is no branch checking whether c was zero, no fallback coefficient1, no raw linked-value add, no pow and no division in this branch.
8. Cleanup the temporary link vector. Read L and the cap properties.
9. If capFlag=0, return S16(result).
10. If capFlag!=0:
    - If62517860 supplies TotalMaxBuffValue, replace result when its signed16 cap is smaller.
    - u =62517f10(q,L) = T(F(L) * scale), where scale is the bare-name BuffBonusCapScale float if getter62517eb0 succeeds; otherwise F(G).
    - Compare signed16(result) to signed16(u). If result<u, return result. Otherwise recompute62517f10 and return that result. For stable inputs this is the signed16 minimum. A concurrent configuration change between the two calls is not ruled out.
    There is no lower-bound clamp in this code.
    Addresses625c4b08..4b88.
    TotalMaxBuffValue getter62517860 applies to Class1/2; missing property getter defaults to word500. That fallback is not used for the two concrete templates below.

## Exact initial schemas and specialization

177 DebuffAreaRadiusAbility: schema315, storage_size422, stored_size22. Class2 at0, Rank1 at5, AttributeLink1055 at6, TotalMaxBuffValue100 at19, ValueType1 at21. Qualified AbilityAttributeLink/AttributeLevelBonusScale is at229 and returns float0 via the proved bounds getter. The client loader retains22 bytes, not an expanded422-byte default block.

Important distinct cap lookup: schema315 declares qualified Ability/BuffBonusCapScale at48, but has NO bare BuffBonusCapScale alias.62517eb0 calls621cdf90 with the bare name, which62301370 looks up exactly. Thus its lookup fails and the cap path uses G; it does NOT read qualified offset48 and zero-fill it. Raw bounds/aliases are saved in mxo-a1-link-schema-bounds.json.

1055 DebuffAreaRadiusBonusAbility: schema112, stored_size30. Class2, Rank2; owner scale2.0 at6; AttributeLink82 (BeliefAttributeAbility), NOT997, at10; bare BuffBonusCapScale50.0 at14; bare TotalMaxBuffValue130 at27; ValueType1 at29. Therefore its link term is based on H82 and its OWN2.0 scale; the initial1055 cap is min(130, signed16(T(F(L)*50))), applied to its modifier subtotal before an optional I1055 add by the wrapper.

For stable normal finite values and the initial one-link177 map:
- term177 = S16(T(F(max-by-branch(H1055-S,0)) * 0)) =0.
- linked sum177 =0; k177=0.
- For finite M, S16(result177)=S16(D177).
- B177(cap=1) = signed16 min(D177,100,T(F(L)*F(G))).
- H177 = S16(I177 + B177) if Present177 AND NOT Mask177; otherwise B177 when modifiers are enabled, otherwise0.
This is NOT necessarily zero: I177 and/or D177 can be nonzero independently. No native magnitude is assigned to either.

## New primary initialization proof

PE virtual-tail bytes being zero are NOT the completed construction defaults.

StartAttributeEffectLevel:
-62828250 pushes default5 and name628db800, this=62b11eac, then calls620022f0.
-6200239a..23a0 writes the constructor's integer default to this+30 and this+34.
-this+34 =62b11ee0. Compiled construction default is5, not0.
Runtime configuration/indirect writes can still override it.

BuffBonusScaleCapDefault:
-62819080 pushes default1 and name628c94e4, this=62b048f4, calls the same constructor.
-this+34 =62b04928. Compiled construction default is1.
Do not replace this with the pre-constructor PE zero. Runtime current G remains symbolic unless its actual configuration is proven.

When ownerScale177 is0, S and the entire recursive H1055 calculation do not affect the numeric177 link contribution under finite defined arithmetic; they are still evaluated and can affect diagnostics/details. M is numerically irrelevant to zero linked sum if finite. G, L, absolute100 cap, I177 and D177 still matter to the177 result. The child caps/defaults matter to H1055 generally but are erased by this outer zero coefficient numerically. Self-link/error/admission branches still matter independently.

## Radius producer and UI boundary

625c5500 takes an ability ID and the queried inventory in ECX. It resolves the base radius through62519100 (RadiusOfEffect or its native dynamic radius function). For UBF0 it reads127; otherwise for UDB0 it calls625c4e90(177,1,1,0). It sign-extends AX, divides by100.0 at625c5587 and returns baseRadius * (1 + H177/100). No extra link fallback appears here. A failed base-radius lookup returns the native negative sentinel; do not substitute an invented radius.

The sole recovered direct call is621ed033: it obtains the local client avatar global629eee08, its inventory pointer at+b4, passes the ability and uses the resulting radius in the client selection/effect path. It does not pass a target's inventory or a separate caster parameter. The generic routine may have other dynamic callers; no complete authoritative retail server caller is claimed.

## Server-only/replicated-total hypothesis remains OPEN

The declared422 versus client stored22 difference may reflect compact client/static or server-only property data. Descriptor flag values in the examined records are0, but their client/server semantic definition was NOT independently recovered, so this does not settle that hypothesis. Existing80bc45(passive) and80bc15(active) modifier families carry target stat/value; they demonstrate replicated inputs exist, not whether a retail75 update arrives as direct177,1055 or an already-derived base value. This pass did not decode a retail75 capture showing that complete producer chain.

Doc proposal: state only that the initial client map's177<-1055 link coefficient is0, while its complete177 getter can retain direct/base totals and caps. Record compiled threshold5 and cap-default1 separately from live configuration. Do not call retail75 inert, invent server coefficients, promote a row or import this as a production formula without server-side/replicated-input evidence.

Primary evidence is committed under [research/protocol](../research/protocol/): `stat-linked-total-native.txt`, `stat-linked-wrapper-native.txt`, `stat-linked-tail-native.txt`, `stat-linked-caps-native.txt`, `stat-linked-truncation-native.txt`, `stat-linked-schema-bounds.json`, `stat-linked-templates.json`, `stat-linked-threshold-native.txt`, `stat-linked-cap-default-native.txt`, `stat-linked-console-constructor-native.txt`, `stat-linked-radius-native.txt` and `stat-linked-loader-evidence.txt`. These are bounded offline disassembly/schema excerpts, not generated parity snapshots.

Producer follow-up: [STAT-PRODUCERS.md](STAT-PRODUCERS.md) now closes the packetâ†’I/D writers and supplies positive direct177 captures, without a causal75 conversion claim. Primary excerpts are retained under research/protocol/stat-producers*.txt and stat-linked*.txt/json; temporary filenames above identify the original bounded probes.
