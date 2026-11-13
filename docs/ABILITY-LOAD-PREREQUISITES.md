# Ability load prerequisites: source contract and dormant helper

This dependent draft adds sourced prerequisite metadata and bounded server admission through PR206's exact existing planner/transaction. It includes exact PR206 source `b368ee291877c6c4e3a285a80fa1181b9b489a9f` as an explicit dependency; it is not claimed to be on actual master or ready for native/merge while that dependency is unmerged. No second transaction, parser change, implicit parent load or hidden grant ownership is introduced.

## Primary evidence

Pinned client.dll SHA256: `cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a`.

- `6251d3c0` enumerates actual component type 39, reads `ReqAbilityID`, canonicalizes its 12-bit ID, initializes required level 0 and reads `ReqAbilityLevel`. `ReqAbilityGroupRequired` is not consulted. The helper requires a genuine ability descriptor family; this stricter corrupt-catalog guard is conservative, and every shipped edge has that family. It is not evidence of behavior for arbitrary malformed templates.
- `6251d660` traverses prerequisites, deduplicates IDs and retains the maximum required level (`6251d76e`); discovered nodes are recorded before recursion. The helper's visited set makes cycles finite, without silently removing the root if a synthetic cycle requires it. Sentinel `0x1000` means 4096, not decimal 1000.
- `625182b0` obtains the named one-byte `ReqCharLevel` getter. Its load caller initializes output 0; absent/invalid descriptor returns false, and the existing common loader zero-fills a declared-but-unstored byte. No positive fallback level is invented. The JSON catalog has one unambiguous stored character prerequisite at most per template; malformed duplicate descriptors are refused by the helper.
- Real load operation `621e07a0` calls validator `621de9f0` at `621e07cf`, then constructs ordinary 80ae through `621d4150` only after success. Other callers use the same validator for UI presentation. This is actual load evidence, not purchase/upload/training qualification.

The reproducible [manifest](../research/abilities/ability-load-prerequisites.json) covers all 1,404 templates: 963 with direct prerequisites, 1,429 edges and 53 positive stored character prerequisites. Every edge references a known template; the shipped graph has no cycles. [Generator](../research/abilities/extract_load_prerequisites.py) reads only `data/ability_catalog.json`; `--output` can reproduce to an isolated file. The manifest is source evidence, not generated ability coverage or a parity promotion.

## Owned versus loaded, training and folders

The native validator first requires a real archive slot below 520 and a genuine ability descriptor. For a missing-loaded prerequisite, `621d0520` searches qualifying owned archive rows with matching canonical ID and stored level at least the requirement. `621cd360` rejects an archived candidate whose training level exceeds EXP-derived character level; `62513700` is the identity function. A qualifying parent's actual slot, ID and full stored level are appended to the SAME outgoing80ae list (`621deb33..5c`). Therefore a valid native request can load parents and child together; parents need not have been loaded before that request.

An already-loaded prerequisite must have sufficient level and a CSSlot other than 520 (`621deb72..b4`). Hidden grants alone cannot satisfy archive ownership. `DisciplineBelongsTo` is UI ancestry, not this graph. Explicitly referenced disciplines still have real prerequisites: Coder 18 requires 27, 36, 37 (level 2), 1008 (level 2); Howitzer 356 requires 358 (level 50), 357, 359.217 and 628 each require 2 (level 1);75 requires98(level 0) and95(level6);314 requires309(level22) and311(level 0). Owning UI folders does not satisfy these edges automatically.

The selected ability's `ReqCharLevel` compares against EXP-derived character level (`621debc7..e7`). This validator does not impose a general selected-row training-level cap. Do not expand the narrower archived-parent search rule into a new global training policy. Native unload `621dec60` collects reverse dependents separately and emits an unload list; this package does not implement server auto-unload or block owned-unload recovery.

## Bounded live-level follow-up

`621d5ad0` uses the inventory loaded map in LA/inventory states (node+1a level,+28 CSSlot). In a live world it first calls `625c2720(ability,0)`, requiring an I-map entry and absence from the modifier object's suppression set (`625e3f90`, object+18). It then obtains level with `625c4e90(ability,0,0,0)` and CSSlot with `625c2770`.

For an existing unsuppressed entry, flags-zero `625c4e90` returns the word at I-node+14 directly. The numeric/link bonus getter `625c47a0` is called only when the first flag is nonzero; fallback is 0 when flags are zero. Thus raw numeric modifiers do not raise this prerequisite level. [Stat producer evidence](STAT-PRODUCERS.md) establishes that ordinary 80b2 supplies the base level and CSSlot. A normal registered loaded row therefore matches its transmitted stored level; hidden membership and suppression remain separate eligibility concerns. No linked-stat formula or server default is inferred.

## Explicit OURS batch interpretation

The ordinary single-selection client inserts its selected row first through `621dde60`, then inserts parents at the front. `621d4150` serializes forward, so the selected row is LAST for that ordinary caller. However, the other positive caller `621df621` reconciles saved slot-set `629ebd68` with current loaded entries and emits multi-root loadouts. Therefore the server does not assume the first or last row universally identifies the selected root.

The source manifest retains **all 21** counterexamples where auto-included parents have a higher ReqCharLevel than the selected child otherwise needs. For example, Power Shot14 at EXP-derived level12 can auto-include owned Energized Attacks9 with required training0 and parent ReqCharLevel99. Precise Blow15 at8 and Punishing Blows16 at12 can auto-include Detect Vulnerability7 with required training0 and parent ReqCharLevel99. Applying selected-root gates independently to every wire row would reject those native batches. This does not promote their effects or claim the retail server accepted every such historical build.

The approved server interpretation is explicit **OURS** for ambiguous crafted/multi-root batches:

- Consider only new eligible canonical additions, excluding existing loaded/no-op rows. Roots are new IDs not in another new ID's transitive prerequisite closure; order is irrelevant.
- Every root must meet its ReqCharLevel using the character EXP locked by the existing transaction. Stored character Level cannot bypass it. Auto-parent additions covered by those roots are exempt from their own selected-root ReqCharLevel gate.
- A prerequisite must be a real owned archive row in the final current-plus-requested loaded set, with sufficient raw base training. Hidden slot520, codes, folder ancestry and client-provided training do not qualify. The native loaded-level comparison sign-extends its16-bit word (`621deb77`), so wrapped-negative base levels do not qualify.
- Newly included archived parents must also have stored training no greater than EXP-derived level, as in the native archived-parent search. This does not become a global selected-row or already-loaded-row training cap.
- Every new node must belong to a qualified root/closure. Cyclic/rootless batches fail closed. Existing invalid graphs and no-op requests do not legitimize new forbidden parents, but remain recoverable by owned unload. There is no automatic loading, training, unloading or repair of legacy graphs.

`AbilityMemory.Plan` performs this pure qualification using the rows and EXP already locked by `CommitAbilityLoadout`, before SQL changes or publication. It retains canonical family/slot/PK ownership filtering and the exact memory capacity/unknown/atomic rules. Accepted flags commit before the existing PlayerHandler acknowledgements/passives and merged207 grant lifecycle; no life/transport lock or callback is added inside the transaction. Refusal uses the existing MODAL path; internal native statuses are not fabricated response codecs. Durable-row qualification is OURS where it differs from the native actor membership/suppression lifecycle.

## Fixture migration and validation boundary

Historical PR206 proofs and source pins remain retained as capacity-only evidence. Their new high-tier314/75/356 additions omitted required parents and are now correctly refused. No production exception preserves that fixture. The old `ability-memory-admission` registration now forwards to the stronger prerequisite-legal successor `ability-load-prerequisites`; unit capacity examples use legal217/113 additions while retaining invalid preexisting-graph no-op/unload recovery checks. The ordinary206 level1 closed UI profile is unchanged.

The new headless case has an exact pre-login owned profile, then tests owned-but-unloaded and undertrained parents, a code-family impostor, EXP versus stored character level, explicit parent+child in reversed order, exact16/one-over capacity, ordinary unload recovery, an actual second-write SQL failure with no positive acknowledgements and whole-batch rollback/retry, ordinary relog and a two-connection capacity race. Its storedLevel50/EXP0 mismatch and invalid requests are explicitly headless authority probes, not a legal native UI build. No fresh result is claimed until separately run and retained.

## Claude-only native one pass

Discovered mode **ability-load-prerequisites** seeds consistent Level1/EXP0, training1, owned2/17/113/130/217/628. Only Awakened2 and Self Defense17 start loaded; folder ancestors are owned only for UI grouping. Dev, GM and fixed rolls are off.

1. Normal hardline radial > Abilities. Verify only Awakened and Self Defense are initially loaded.
2. Double-click archived Hindering Shot130. The native client should include its owned Combat Insight113 parent in the same ordinary load operation; verify both loaded and memory4/16. Retain the actual80ae rows and server acknowledgements.
3. Unload/reload through the normal UI, then load Evade Combat217 and Concentration628. Full relog must preserve the admitted flags.

No random result, helper client or developer bypass is required. Invalid child-only RPCs and character-gate failures are headless authority cases: native UI should prevent sending them. Native rendering/tree grouping, automatic parent request and persistence remain Claude-only pending. This draft is not READY until source review, complete validation, granted fresh proof and dependency merge.
