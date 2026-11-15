# NPC-interior v2 models and restore checkpoint (not runtime adoption)

This child checkpoint is based on the audited pure layer plus masterc7984328 (214/247 retained). The frozen pure producer remainsd84dde56. New authoritative profiles remain world1 sectors550/1349 only; world2 and842/Bornmann are not added.

Only capture/restore of an explicitly present new NPC subtree is integrated. There are no new Stage, Accept, Advance, Ride, door-publication or packet-output call sites. No normal mission creates the new fields yet. A prepared pure plan does not assign itself to progress, choose a site, consume RNG, spawn anything or save SQL.

## Typed data and presence

The new immutable selection/pending/witness types carry a separate version2/`NpcInterior` tag, exact logical actor roles (story phase separate from wire phase), raw marker JSON/planned standing, exact raw site identity, saved floor/lobby entry context, node paths and GO-specific required door snapshots. Arrays are owned/cloned. They never reuse v1 object `Sources` as NPC metadata.

SavedState uses a nonnullable JsonElement with Undefined default and `WhenWritingDefault` omission. Thus an absent field preserves the old v1 and ordinaryv2 serialized shape, while explicitJSONnull survives real outer deserialization and is recognized malformed metadata. Only this subtree is parsed after the existing identity/items/flags have been restored. A recognized malformed version/kind/shape/raw marker throws MissionPlacementRestoreException carrying that progress, so the existing quarantine and exact cached SQL/Items-preservation path can operate. There is no broad SavedState parser catch. Globally invalid JSON syntax still has its previous schema/parser behavior; a truncated RawMarkerJson inside otherwise valid recognized metadata is narrowly quarantined.

New fields force explicit SavedStateVersion2/exact site history on capture. Version1 with present NPC content (includingnull) refuses rather than being promoted. Old Version2 without the new field follows its existing parser/validation path. Existing bound-object and v1/free-object restore checks retain their ordering, then new NPC evidence is revalidated. No current or historical legacy NPC checkpoint acquires a witness automatically.

## Pure planning and fixed replay

The planner receives an already normally selected site and a read-only evidence/static/NAV provider. It reproduces authored NPC order followed by supported generated-wave order, and the existing stable farthest-from-RAW-elevator spot order. It validates only the exact selected prefix; finite unused points are not alternate candidates or blanket-approved. Relative actor arrivals and synthetic lap offsets are explicitly unsupported, not changed. Existing committed pending is returned exactly/refused without provider reads or new selection.

A before-state observation binds owner/progress/active mission, phase/site/pending/history count, target identity/start, character/world/life/district and transport epoch. It uses existing IsCurrent as observation only. After all source work, owner and selected site/role fingerprints must still match. It performs no work inside the transport publication gate and grants no publication authority by itself.

Fixed restore reuses the saved entry node and standing point; it does not run a new ordinary entry calculation or seek a new region. Native positions/quaternions and immutable dataset digests must still match, exact marker rows must remain in the current raw catalogue, and the authored route plus selected GO-specific portal door snapshots must match the saved proof. Failure quarantines without rewriting the input. A selected pending with no witness and an explicit retained refusal remains such; it is never refilled with newly invented evidence.

The current pure Prepare method intentionally refuses an already committed pending; the explicit durable replay/transaction coordinator is a later reviewed step. No retry/Stage path is wired in this checkpoint. Static catalogue evidence and graph reachability still do not assert native door hover, rendering, collision clearance or ordinary passage.

## Review / validation boundary

Source controls cover literal Tabor planned prefix and raw marker; model ownership/roundtrip; absent/null/wrongkind/truncated recognized content; unchanged legacy shape; exact capture/restore rollback body; old pending immutability; source-catalog loss; owner/phase/pending/world/life/reset invalidation; unsupported synthetic lap/relative wave. SQL preservation is covered by the existing quarantine guard and captured input/model invariants here, not claimed as a new live SQL transaction proof.

This model delta is submitted for source review before a focused compile/test grant. No new build, full suite, server, native action, push or PR is claimed for the child. Pure d84 separately passed17focused/5848full and both actual NativeQa builds/plans; those results do not validate this child delta. Runtime transaction/publication integration remains a separate review, with actual ordinary Tabor completion still the goal.
