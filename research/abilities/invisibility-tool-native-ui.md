# Invisibility Belt native UI gates

Primary binaries: shipped client.dll SHA256 cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a; gameobjects.gob SHA256 3e47f11012f6f219a26c9f5decfa10cbc00726c397937d03c3068254e5424103. All disassembly/extraction is read-only. Reproducible Temp evidence: mxo-common-tool-activation-20261114, original-comparison.json and named address dumps. Original resources are unchanged.

## Bag use is a different gate

The actual item menu at620c0684 calls625da340; false branches past the item-use entry at620c068e. The latter wrapper resolves the item then calls625da0d0. That predicate returns true only for a nonzero stored ActivatedAbility (string6288c05c) or625d9c30's valid IsAConsumable activation path. Its successful menu branch620c06a7 uses text5e0000eb and event literal65735549 (IUse). GO460 Invisibility_Belt has Tool component10, Usable5, Tool/Class4002, category1951355977, and ReqAbilityID2148626432 (1116), but no ActivatedAbility or IsAConsumable. Its absent bag Activate entry therefore matches this primary predicate. GO531 CodeAnalyzerTool differs: it explicitly stores ActivatedAbility2148476928 (970), so its bag activation is not proof that every tool supports that menu.

## Actions > Tools has the fallback path

Collector620592a0 walks actual inventory slots0..106. Predicate620588b0 admits an item with Tool10 or Usable5 and ReqAbilityID, excluding category ancestry IW00 via621d0640. Rebuild6205b04f inserts candidates through620597f0 into widget+84; separate weapon and Abilities widgets are+80/+7c. GO460 has the required authored components/ReqAbilityID and categoryITOt. This is a static route, not a native listing assertion.

The Tools left-double-click branch6205a794 obtains the selected actual bag slot, rejects sentinel107, calls621cd0a0, and sends nonconsumables through621ed630 at6205a7f8.621cd0a0 tests IsAConsumable, not equipment-slot semantics.621ed630 validates the actual slot using621d84d0 and obtains its ability through621d0970: ActivatedAbility first, otherwise ReqAbilityID, decoded(value>>10)&4095. The normal shared producer621ecf10 receives this actual tool slot and flag0. GO460 therefore selects1116 through the fallback. No equipment slot or direct hidden-ability UI action is invented.

The preserved native locator422/ability215 capture in research/abilities/data-mining-actions.md demonstrates the same Actions > Tools fallback path for a tool with ReqAbilityID and no ActivatedAbility. GO615 is Disguise_Tool, also without ActivatedAbility; its stored ReqAbilityID2147606528 is120, so it is not evidence for blindly sending1114 from its bag icon. The native UI and server grant paths must remain distinct.

## Hotbar and Target qualification

The blocked native retest's8151 has a zero u32. It is already CLIENT_TARGET, dispatched by RpcManager to PlayerHandler.ProcessTargetChange; zero clears the target (docs/CLIENT-ACTIONS.md Target row D/C972). That record contains no belt slot or1116 identity and cannot establish an attempted item cast, a missing item handler, or an ACK failure. The exact hotbar entry construction/selection from that interaction is not closed by these retained packets. No hotbar activation claim is made and no8151 behavior changes.

Retest only through Actions > Tools > double-click Invisibility Belt, before callback checks. Require actual1116/current owned slot,50IS debit and observed duration. Native Tools listing/activation remains unproved until Claude checks it. The headless helper's ordinary80b9/slot proof remains attributed to04e815f3; expressibility of the chosen UI interaction is a separate native gate.
