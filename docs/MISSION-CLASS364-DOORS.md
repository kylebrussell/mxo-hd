# Ordinary class364 door family

The catalog derives supported IDs once from the existing `AttributeClass364` definitions. Its 76 IDs exactly match the original GOB contracts in `tests/hds.Tests/Fixtures/Class364DoorContracts.json`, including 343, 346, 580, 6958 and 6959. No per-door numeric switch or full `GameObjectDefinitions` instance is constructed for each request. Elevators 417/419 remain separate.

All 76 primary rows supply Model 6, NavMeshController 4, PortalController 52, Open client/execute rule 4004 and Defaultable 1. Their native MaxDistance is 1000; that is source evidence, not a new server distance policy. Existing use-distance checks and lifecycle timing are retained. The common state machine is closed 0835, open 0834, closing 0838 after 20 seconds, then closed after 1 more second. No manually invented Close action is added.

The ordinary static Open handler now enters the existing captured-owner overlay path for the entire family. The catalog does not authorize a scene pose, move global statics or bypass owner/world/life checks. Scene-backed measured positions, required passage doors and their persistence belong to the separate interior evidence/provider integration. Until that integration is qualified, this package does not claim corrected measured 6958/6959 poses or native passage.

## Diagnostic and native pass

Headless case and discovered native mode: `class364-doors`. Both reuse the already audited medium03 prefab fixture, GO580, source sector 33/local 1380. This direct diagnostic start is an aid; it does not prove a walk route from a normal hardline or a mission source selection. The headless path uses ordinary 80c8 Open and the existing 20-second open/1-second closing waits, verifies source pose/quaternion and parser state, and seeds no mission progress, items or rewards. No economic ledger is claimed.

In the native client, right-click the visible source door and use Open, observe the actual animation/state, walk through its opening, and observe natural closing. Use fixed camera/clear avatar view. Stop on a bad pivot, missing overlay, failed native selection or blocked passage and retain the actual world/key/GOID/pose. A successful 580 check is one actual family member, not proof that all 76 assets or expanded mission scene mappings were tested. Broader scene-backed 6958/6959 checks follow the provider's qualified current witness; do not force old source coordinates.

Original GOB SHA256: `3e47f11012f6f219a26c9f5decfa10cbc00726c397937d03c3068254e5424103`. No native rendering or passage result is claimed.

## Qualified standalone result

At clean `ae745d460fb1492a31c1fc21cee04a9c6e73c4c9`, 199 focused checks and 5,923 full Release tests passed with all four hardlink flags. Actual Debug/Release builds, plans/provenance and exact case/mode discovery passed. This is the standalone master08d catalog/owner gate; no expanded interior scene provider was imported.

One separately granted `class364-doors --parallel 1 --repeat 1` case passed in 39.7 seconds, actual held driver exit 0. Ordinary world 16 login opened the existing source580/key33/1380 at the audited pose/quaternion, then observed natural834→838→835 and zero parser problems. Direct diagnostic starting position remains an aid. This is backend protocol evidence for one member, not native rendering, walking, all 76 assets or 6958/6959 passage proof.

Actual selected Debug hds SHA256: `9902839EC6D87023A05CF341A28765C6151B2CEE6A8315D084237F141A4D82DE`; Release driver: `4053796001EB01A64F18244D116C121AB501F1EF80291B3A8B4A018FE3DBCB2D`. Raw result `Temp/mxo-class364-doors-mxotest_run_1_2ed863f797.json` SHA256 `A1944DE7DC48F9AEBBA9F95F44C663B3A0D5C64398578068B867520B8BBFEDF9`; paired PacketLog SHA256 `9F3CD6B64395CC0CDAF87118F5D730F7DEC44A698F5C22BD1358DE62104D2354`. Independent cleanup verified schema `mxotest_run_1_2ed863f797` COUNT0 and all 7 recorded creation identities gone.

One explicit real `--bootstrap-checks` run for only `class364-doors` also passed, actual held runner exit 0. It seeded the actual medium03 mode plan with abilities 2/17, authenticated against one actual server and invoked its inherited no-op hook. It is startup/state-schema evidence, not another door-use or native result. Actual server/state source hash matched 9902 above; no shared baseline substitution. Independent cleanup verified schema `mxotest_bootstrap_38a752ee08` COUNT0 and all 11 recorded creation identities gone. Raw startup and ownership proofs are in `Temp/mxo-class364-doors-20261115/bootstrap-class364-ae745d46`.

The publication descendant changes documentation only. Runtime, tools and tests remain byte-identical to qualified ae745; no rebuild or repeated live run is inferred. Final CI qualifies the published tree. Claude alone performs the native pass and merge.
