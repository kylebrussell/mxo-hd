# Separate Box548 floor diagnostic

This QA-only experiment isolates the Box asset from the unidentified obstruction beside Poster Hunt's mission proxy. It does not fix mission placement or prove native picking. Every normal mission object, source, goal, item and reward remains unchanged; the probe never writes a checkpoint. The existing ordinary use handler still captures normal elapsed-clock fields even for an unrelated entity. No global mapping, Hidden, bounds, reach, executor or reward changes are included.

The separate free Box548 uses the actual Class46 fields: closed state 0x835, UseCount 1, MissionKey 0. It has no mission or container executor. Ordinary Open requests are logged by the existing handler and may be refused; no loot or completion is promised.

## Source and pose

Pinned client.dll SHA256 cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a. Pinned GOB SHA256 3e47f11012f6f219a26c9f5decfa10cbc00726c397937d03c3068254e5424103.

Box548 MOA 0x28000872 links the cardboardbox_large skeleton/base/flaps. The base MGA has 84 vertices, 28 triangles and literal local bounds (-50,-40,-70)..(50,40,70). Its skeleton root's two matrices are identity, with no translation. QA centre 25100/2040/14100 therefore puts measured minY -40 on NAV floor 2000. Identity orientation is literal; this is an explicit diagnostic placement, not retail mission placement or a HalfExtents offset.

The entire footprint X25050..25150/Z14030..14170 lies on sector209/region54/poly12, floor2000. Grounded approach 24960/2095/14100 is on poly17 and the same connected component as the normal arrival 25579.31146596289/2095/13959.481277120463. The geometric route is not proof of native walking, LOS, rendered emptiness or radial selection. The source resource scan found no nearby modeled object within 350 horizontal/250 vertical units, but secondary styling may add geometry.

Mesh proof: MOA SHA256 76e59536d70505c4a22799fa1b211f8ec32f922a06278dd9ade72cece04f211a; base MGA SHA256 10ed8bc5b72e6b13d3b92c65e6e98944a9bc90287a0829615cb7892837d90af2; skeleton SHA256 4b5f5e720b6b8983afcabde0e31364803e155ad7d5aa21b6f634b8880bd55008. MGA structure is 63-byte header, vertexCount*32 then triangleCount*6. Source evidence is retained in Temp/mxo-box-asset-pivot-gate.json, mxo-box-mesh-bounds.json and mxo-box-qa-entry-path.json.

## Guards and lifetime

The explicit `poster-box-floor` mode enables MXO_POSTER_BOX_FLOOR_QA=1 and dev commands. The command checks the actual configured database has a valid mxotest_ name, actual CSR110, live world1 and owned current mission201202 phase0/object1. Concrete548 must retain source1415/key0d1003dc and exact legacy23940/2245/15050/quaternion. The existing source view/spawn must belong to the current client. The private lease captures mission/source/view identity, transport epoch and world/life generations. Off, mission detach/phase change, death, session cleanup or reset forgets only this private probe; a reused view/spawn or bound source is never deleted.

## One-pass native checklist

Use discovered NativeQa mode `poster-box-floor`. No normal mission completion, item or selected receipt is seeded. Call Agent Gray, choose Critical > Poster Hunt, accept and use the normal elevator. Enter `?posterboxfloor on`. Walk normally to 24960/2095/14100; stop and report any obstruction. Face +X and LOOK DOWN at the floor toward 25100/2040/14100. First verify empty space and the rendered model, then hover/target/right-click/radial Open and retain actual client requests (337 versus 80c7). No server Open result is required. Enter `?posterboxfloor off` and verify only the extra Box disappears while mission proxies remain.

Native render, emptiness, walking and picking remain pending Claude. This diagnostic has no retail parity/completion claim. The original source1415 has no Model; the nearby schema369 dresser's published codec has no proven Hidden field, so neither is hidden here.

## Local verification

Pending clean-pin validation and root fresh grant. The normal-protocol fixture accepts the actual offer/elevator, verifies literal actual03 pose/state/key, sends an ordinary use request only for observation, toggles off, and checks every non-clock checkpoint field plus saved objects/goals/economy/receipt invariance. Full original and final payloads are retained; only the existing NpcElapsed/WaveElapsed/GoalElapsed clock maps may advance through the ordinary handler. Unique disposable-DB proof includes all raw03/RPC and actual DLL configuration/hash, including failure.
