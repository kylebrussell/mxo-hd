# Native 80 ba refusal consumer source gate

Historical source audit before this feedback change, at master80d837eacee86f3ce186dfb88531bad389d3b21f. Native consumer evidence remains applicable; the producer inventory below describes the pre-change server. The new implementation and retained gaps are documented in ../../docs/ABILITY-REFUSAL-FEEDBACK.md.

## Closed packet contract

Pinned client.dll SHA256 cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a. Constructor6213b79c reserves 0x13 bytes, writes header80ba and initializes the fields below. Printer62151a00 independently reads the same offsets. Network adapter621e5890 extracts them and calls621e5280 at621e58dd.

| Packet offset | Size | Field | Evidence |
| --- | --- | --- | --- |
| 0 | 2 | header80ba | ctor6213b7b9 |
| 2 | 4 | AbilityID rawu32 | ctor6213b7c1; printer62151a27; adapter621e58a9 |
| 6 | 1 | ToolsSlot, default 0x6b | ctor6213b7cb; printer62151a4f; adapter621e58c0 |
| 7 | 4 | ErrorID rawu32 | ctor6213b7d2; printer62151a7a; adapter621e58bd |
| 11 | 4 | InnerstrengthDeficit rawu32 | ctor6213b7dc; printer62151aa2; adapter621e58af |
| 15 | 4 | StatesNotMet rawu32 mask | ctor6213b7e6; printer62151aca; adapter621e58b5 |

Current BuildDenied allocates 19 bytes, writes a ushort AbilityID and a byte reason into these fields, leaving upper bytes zero. That is zero-extension, not evidence those wire fields are ushort/u8. ToolsSlot6b matches the constructor/captured ordinary ability cases. No positive non-default item-slot refusal was recovered here; do not invent new item-slot behavior.

## Closed message dispatch

621e52ca reads ErrorID argument; subtract2, unsigned bound <= 0x23, then table621e5698. Codes 0/1 and>=38 take default no-message path;10 and19 also point there. This proves message behavior only, not a generic backend meaning or complete refusal side effects.

| ErrorID | Entry | Native string/message |
| --- | --- | --- |
| 2 | 0x621e52e3 | ID_MUST_BE_STANDING — You must be standing to use this ability. |
| 3 | 0x621e530d | ID_ABILITY_USE_UNKNOWN — Attempt to use an unknown ability? |
| 4 | 0x621e5329 | ID_ABILITY_DISABLED — This ability has been disabled due to virus. |
| 5 | 0x621e540d | ID_ABILITY_USE_TOO_SOON — You must wait %1I more seconds to use %2o again. |
| 6 | 0x621e5454 | ID_NOT_WITH_CURRENT_TACTIC — You cannot activate %o while using this tactic. |
| 7 | 0x621e547d | 0x80000081 — You do not have this ability loaded. |
| 8 | 0x621e548b | ID_ABILITY_CANT_PERFORM_FULL_QUEUE — You cannot perform this ability because your queue is full. |
| 9 | 0x621e531b | ID_TARGET_NOT_IN_LOS — You cannot see your target. |
| 10 | 0x621e5673 | (no message in this consumer) |
| 11 | 0x621e52f1 | ID_CANNOT_USE_WHILE_MOVING — You cannot use this ability while moving. |
| 12 | 0x621e52ff | ID_CANNOT_USE_WHILE_IN_TARGET_FOV — You cannot use this ability while within your target's sight. |
| 13 | 0x621e552f | ID_ABILITY_NOT_IN_CORRECT_STYLE — You are not in the correct combat style to use this ability. |
| 14 | 0x621e5499 | ID_ABILITY_REQUIRES_PROGRAM_LAUNCHER — This ability requires the Program Launcher to be equipped. |
| 15 | 0x621e54a7 | ID_CLIENT_ABILITY_NEEDS_ITEM — %1o needs %2z in order to be performed. |
| 16 | 0x621e54f0 | 0xc60027cf — This item must be equipped to be activated. |
| 17 | 0x621e550d | ID_INADEQUATE_INNER_STRENGTH — Could not perform %o.  You need %I more Inner Strength. |
| 18 | 0x621e553d | ID_CLIENT_ABILITY_NEEDS_ITEM — %1o needs %2z in order to be performed. |
| 19 | 0x621e5673 | (no message in this consumer) |
| 20 | 0x621e5586 | ID_ABILITY_MUST_BE_IN_COMBAT — %1o is usable in combat only. |
| 21 | 0x621e55af | ID_ABILITY_MUST_BE_OUTSIDE_COMBAT — %1o cannot be used during combat. |
| 22 | 0x621e5337 | ID_TARGET_NOT_IN_CURRENT_ABILITY_STATE — Your target needs to be (%s) in order for you to use %2o on them. |
| 23 | 0x621e539a | ID_CASTER_NOT_IN_CURRENT_ABILITY_STATE — You must be (%s) in order for you to use %2o. |
| 24 | 0x621e563e | ID_TARGET_NOT_VALID — Target is not valid. |
| 25 | 0x621e55d8 | ID_INVALID_INVENTORY_SLOT — Invalid inventory slot. |
| 26 | 0x621e55e6 | ID_ABILITY_NOT_GROUND_TARGET — Ground target is not valid. |
| 27 | 0x621e55f1 | ID_ABILITY_NOT_REACHABLE_TARGET — Can't reach the selected target. |
| 28 | 0x621e55fc | ID_ABILITY_NOT_FRIENDLY_TARGET — Target is not a friend. |
| 29 | 0x621e5607 | ID_ABILITY_NOT_DEAD_FRIENDLY_TARGET — Target is not a dead friend. |
| 30 | 0x621e5612 | ID_ABILITY_NOT_DEAD_ENEMY_TARGET — Target is not a dead enemy. |
| 31 | 0x621e561d | ID_ABILITY_NOT_ENEMY_TARGET — Target is not an enemy. |
| 32 | 0x621e5628 | ID_ABILITY_NOT_GROUP_TARGET — Target is not a group member. |
| 33 | 0x621e5633 | ID_ABILITY_NOT_OBJECT_TARGET — Target is not an object. |
| 34 | 0x621e5649 | ID_TARGET_NOT_IN_RANGE — Target is not in range of this ability. |
| 35 | 0x621e565f | ID_TARGET_NOT_A_FRIENDLY_SIMULACRA — Target is not a friendly simulacra. |
| 36 | 0x621e5654 | ID_TARGET_NOT_AN_ENEMY_SIMULACRA — Target is not an enemy simulacra. |
| 37 | 0x621e5521 | ID_ABILITY_CANT_WHILE_DISARMED — You cannot use this ability while disarmed. |

## Placeholder and auxiliary-field behavior

* Ability references are normalized to12 bits and rebuilt as the ability descriptor before object-name formatting (621e5290..a3). No higher alias bits are displayed as separate abilities.
* Code 5 calls inventory getter625c4cf0(ability,0), performs conversion626a92d8 and substitutes 1 only when EAX is exactly 0 (621e540d..543d). It formats ID_ABILITY_USE_TOO_SOON,0x76000255, with that local value plus the ability reference. It does NOT read a remaining-seconds field from80ba. Do not claim a ceiling/rounding rule, a generic minimum for negative inputs or a recovered authoritative server timer formula. Getter diagnostics explicitly contain ': no recast timer found for requested ability!'. Its full timer storage/clock conversion is outside this gate.
* Code 6 formats0x7a0002cf with the ability reference: 'You cannot activate %o while using this tactic.' It is NOT a native message for generic cast busy.
* Code 17 passes the packet's InnerstrengthDeficit through621df6d0 (621e550d..5517). That helper formats0x24000063 ID_INADEQUATE_INNER_STRENGTH: 'Could not perform %o. You need %I more Inner Strength.' The deficit comes from the packet; leaving it0 would report needing0 more. Current server Effective cost and available IS are bothUInt16, so a positive existing-check difference is exactly representable as u32 without changing cost law.
* Codes 15/18 use getter62516460. Its625164a5..b7 lookup reads BARE ReqCategory (string628c8b64), then623055f0 resolves its category label. Message0x7a000147 takes ability and category-name placeholders. Do not describe the second argument as a recovered required item GOID or invent category membership rules.
* Codes 22/23 pass StatesNotMet to621e0e60, which iterates 32 set bits, maps each through62519c00's native global tree62b049dc and formats state names using0x7a0001e2/1e3. The respective messages say target/caster NEEDS these states. This is not proof that our ConditionStateFlags have the same namespace, or that a currently prohibited stun bit should be sent as a positive requirement. Missing-state polarity and the server-state mapping remain unclosed.

## Bounded retail positives

Two existing selected logs were read, not the whole corpus: Warboy ProxyM2_m2_packets.log.txt and Rajko PacketsZombiesPortingEtc.log.txt. The scanner retained WORLD->Client payloads beginning02 and length-prefixed19-byte 80 ba literals. It found 31 samples:23 code 6,6 code 5,1 code 4,1 code 10. This is a bounded length-boundary extraction, not a new general packet parser or exhaustive causality study.

* Warboy line66753:80ba660300006b060000000000000000000000, ability870, code 6. Prior nearby repeated80b9 requests do not prove that6 means cast-busy; the native message means current tactic.
* Warboy line141668:80ba660300006b050000000000000000000000, ability870, code 5. This positively corroborates5 on the wire; native decoding establishes its reuse-wait message.
* Warboy line77137:80ba050100006b040000000000000000000000, ability261, code 4.
* Rajko line19692:80ba280400006b0a0000000000000000000000, ability1064, code 10. It is a positive silent-table case, not proof of its backend cause.

No code 31 appeared in these two bounded inputs. That does not assert absence elsewhere. Current code's claimed cooldown interpretation of0x1f is contradicted by its exact native table entry:31 means target is not an enemy.

The formatter also has direct callers at 621e871a, 621ed082, 621ed119 and 621ed2ed in addition to the network adapter. Their full admission roles are not classified here. A native text observation alone therefore does not establish that a received server80ba caused it; retain the native request/reply trace before attributing the208 observation. No208 runtime change is proposed.

## Current producer inventory and mismatches

* AbilityCooldowns.DeniedTimerRunning=0x1f is used by the main IsCoolingDown branch and generic consumables. It displays 'Target is not an enemy', not a timer message. Native code 5 is the source-backed message for an existing running-timer refusal.
* DeniedBusy=6 is explicitly a guess in current source. AbilityHandler.IsCasting uses it; private Deny(id,text) also uses it for internal-only ability activation, missing tools, controls/powerlessness, missing loaded search, exclusive buffs, disguise, pets, stealth/free-cast checks, full upgrade buffer, friendly/dead/range checks and insufficient IS. English broadcast text follows, but the native code adds an unrelated tactic message.
* InventoryHandler chooses6 for its broad busy predicate and31 for a timer. AntiStunPills, HealthBoostPills and PrisonDevices aggregate several distinct admission failures into their own6 helpers. CombatStyles uses6 for unloaded-style failures; its wrong-style13 is already correct. There is no authority to change all these aggregate branches to one different code.
* No acquisition or training-response codec change is implied. This gate concerns80ba only, not80b8/80b6/80b2 or loadout acknowledgment policy.

## Safe scope proposal and remaining NO-GO boundaries

GO candidate for a later explicitly reviewed feedback-only package: define the full native field layout/named reasons, preserve19-byte defaults, emit5 where existing code proves a running cooldown, emit17 with exact positive existing IS deficit, and emit7 where an existing branch specifically proves known-but-not-loaded. Preserve already-correct13. No backend admission, cooldown, IS cost, combat formula or ownership law changes.

Typed target decisions can eventually select24 invalid target,9 LOS,34 range,28 friend,31 enemy,29/30 dead-side,32 group,33 object and35/36 simulacrum side, but only at specific existing checks. Do not parse current English error strings or collapse combined floor/visibility/range/relationship predicates into one assumed cause.

NO-GO for a universal generic busy/control/ownership replacement: no generic cast-in-progress message is identified in this table;8 means queue full,4 virus-disabled,37 disarmed, and23 needs mapped missing-state metadata. Unknown/not-owned (3) and known-unloaded (7) are distinct; internal effects/marker ownership cannot automatically borrow either meaning. Preserve explanatory server text where no exact mapped reason is established, pending an explicit response-policy decision. Silent codes are not established generic fallback policies.

Any eventual UI diagnostic must distinguish client rendering of a known refusal from ordinary native UI generating the request: native preflight may block insufficient-IS/reuse requests before the server sees them. A strictly scoped codec-view probe may be needed; no such helper or runtime is implemented here.

Committed artifact ability-refusal-native-proof.json contains exact primary bytes, jump table, strings digest, reason table and bounded retail literals. Original scratch artifacts: mxo-80ba-pinned-proof.json contains exact primary bytes, jump table and strings digest; mxo-80ba-reason-table.json contains the36 dispatch entries; mxo-80ba-bounded-retail-samples.json retains every bounded sample and source line. Raw disassembly files mxo-80ba-primary-disassembly.txt, mxo-80ba-callers.txt, mxo-80ba-special-formatters.txt and mxo-80ba-cooldown-item-inputs.txt retain the named paths. The remaining scratch files are Temp-only; the committed proof retains all primary byte slices, the table and every bounded literal.
