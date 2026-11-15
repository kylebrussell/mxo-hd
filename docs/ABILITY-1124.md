# Disrupt Connection Old1124: tool eligibility

Normal80b9 previously reached the generic cast path for1124: two-second cast,25IS cost and animation, although no effect executor exists. The handler now enforces its explicit `RequiresTool=1` before duplicate, cost, cast or cooldown state. This is an eligibility correction;1124 remains **missing** in the parity audit.

Primary7.6005 evidence (client.dll SHA256 `cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a`):

- Python `Inventory.findToolForAbility` binding at0x625f12c0 scans slots0..106, requires component10 (Tool), and compares the requested compact ability with helper0x625f0be0.
- Helper0x625f0be0 reads `ReqAbilityID` (string0x62871018), checks the ability GOID class and returns `(GOID>>10)&0xfff`.
- `research/abilities/extract_ability_tools.py --client <read-only MxO folder>` extracts all869 component10 templates into `data/ability_tools.json`, retaining raw prerequisite GOIDs. None names1124. Tool470 DisruptConnectionTool actually names1125;531 also cannot satisfy1124. Absent prerequisites remain absent.
- Native `discovery.abilityIDToGameObjectID` at0x62692da0 maps compact1124 to its Old resource. Its stored `AbilityID`384 does not justify routing1124 to384. No shipped modifier supplies an on-use effect on1124.
- Both templates refer to Info0xc400005a/MoreInfo0xc400006c. The string explicitly describes increased stun/enrage/withdraw influences, matching384's three passive modifiers.384 and its consumers remain unchanged.

Stock player inventory therefore cannot satisfy1124's native matching-tool prerequisite. This does not establish absolute retirement, NPC eligibility or an alias. No immobilize, network-disconnection effect or free activation is invented.

Verification: focused units check wrong470/531/no-tool, +4096 canonical request, passive384 and the extracted negative match. Fresh headless `ability-1124-tool-refusal` uses owned loaded1124 and384 at level50, checks ordinary80b9 refusals before the stored cast interval, no cost/cast/cooldown/modifier, retained384 passive, and strict dev cast through the same handler. Existing `styles` NativeQa mode enables dev commands explicitly and accepts the custom1124/384 loadout; no new fixture mode is introduced.

Native reviewer: use the PR's exact pinned-head checklist. `--build-info` and `--fixture-plan styles` are read-only; setup and client interaction belong to the native reviewer.
