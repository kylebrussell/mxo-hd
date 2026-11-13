# Ability load prerequisites: source contract and dormant helper

This package is pure catalog metadata and tests. `AbilityPrerequisites` has no gameplay callers, mutation, admission, implicit loading or grant expansion. Runtime integration waits for PR206's actual merge and a separate source review. It does not import that unmerged transaction or change the current parser, archive, prerequisites, load acknowledgements or passives.

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

## Integration proposal, not implemented

After actual PR206 merge, reuse its owned-row transaction, canonical/type/PK safeguards, EXP input and committed snapshot rather than creating a second mutation path. Validate eligible new additions against the final current-plus-requested set of real owned loaded rows. No hidden slot 520, code-family row, client-provided level or folder ancestry may substitute. Do not auto-load missing parents: the native load caller already emits qualifying parent rows. Preserve repeat loads, existing accepted acknowledgement/grant order after commit and owned unload recovery, including preexisting invalid graphs. Preserve merged207 typed registration.

Proposed atomic refusal and durable-row qualification are explicit OURS server-authority policies, not recovered retail server partial-batch semantics. Existing MODAL text is the compatible refusal path; native internal statuses are not invented negative wire codecs. A future fixture must separate invalid-wire server authority from ordinary native UI prevention and prove same-batch parent/child acceptance, under-level rejection, EXP mismatch, memory coexistence, database rollback and two-connection races. No runtime wiring, fresh fixture, native readiness or parity status is claimed here.
