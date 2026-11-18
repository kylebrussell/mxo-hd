# Gameplay breadth evidence

`native-verified` identifies a named result observed in the real client. `headless-only` means the listed
path has automated evidence and still needs native QA on the current behavior. `broken` marks a reproduced
player-facing failure. A row covers the behavior described in that row.

| System | Evidence | Player impact | Verified scope or remaining check |
|---|---|---|---|
| Vendors | native-verified | High | Recycle cancel/confirm, bit-stack and leveled-code quotes, and Antibiotic activation. [Economy](VENDOR-ECONOMY.md) |
| Vendors | native-verified | High | PR287 observed buys and buyback of the 680-information shirt, 12,600-information leveled code and eight Code Bits. Buy Back restored each item and the full stack. [Buyback](VENDOR-BUYBACK.md) |
| Vendor Appraise | broken | High | PR287 saw the 680 quote arrive in `81 12 01` with an empty vendor box and no chat text. The server system-message fallback needs native confirmation. [Economy](VENDOR-ECONOMY.md) |
| Vendor discovery | broken | High | The tested vendor with stock did not render at its catalog location. The disposable window opener lets QA test transactions. [Economy](VENDOR-ECONOMY.md) |
| Hardline travel | headless-only | High | Ordinary Loading Area exits and returns, including custom-world registration. [Hardline registration](LA-HARDLINE-REGISTRATION.md) |
| Loading Area after Reconstruct | broken | High | PR287 entered the Loading Area. The list and map could select a hardline, but the Jack into the Matrix action produced no `80 ff`. The corrected reset sequence needs native confirmation. [Death presentation](DEATH-PRESENTATION.md) |
| Subway | headless-only | Medium | Cross-district rides, refusal and continued packet flow after platform login. [Subway framing](SUBWAY-STATE-FRAMING.md) |
| Teams | native-verified | High | An ordinary helper kill credited the holder's goal and Team Contact awarded one completion on PR281 `6a8f2c15`. [Native checklist](GAMEPLAY-BREADTH-QA.md) |
| Teams | headless-only | High | Shared loot and relog coverage. Helper HUD and mission-item actions remain open. |
| Crews and factions | headless-only | Medium | Create, invite, ranks and removal. The crew/faction scenarios exercise role permissions. |
| Area chat | native-verified | Medium | CSR sent the command that triggered the teammate helper during native PR281 QA. |
| Chat and emotes | headless-only | Medium | Group chat and paired emote acceptance/refusal. |
| Chat rooms | broken | Medium | The stock client rejects the room commands. The server still handles the room routes in headless QA. [Native chat command check](CLIENT-ACTIONS.md#chat-rooms) |
| Trade | native-verified | High | The Secure Trade window, item/info exchange, Clear and Cancel with the QA bot. [Secure Trade](SECURE-TRADE.md) |
| Trade | native-verified | High | PR287 observed player-targeted and incoming offers, partner Accepted/Considering labels, a 250-information settlement, both cancellation paths and relog. [Secure Trade](SECURE-TRADE.md) |
| Trade Accept checkbox | broken | High | PR287 observed a checked box after a partner edited the offer and on reopening the window. Settlement still required a fresh accept. The explicit checkbox refresh needs native confirmation. [Secure Trade](SECURE-TRADE.md) |
| Marketplace | headless-only | High | Purchase, fee and seller persistence across world and Loading Area. |
| Crafting and compiling | native-verified | High | PR287 observed tool activation without an information debit, timed Health Boost 3.0 compiles with charge use, and one 9-information completion when closing the 2399 panel. [Ordinary compile](COMPILE-ORDINARY-IC.md) |
| Crafting and compiling | headless-only | High | Current timing/pricing, charge and material depletion, cancellation and rollback. [Storage depletion](CRAFTING-STORAGE-DEPLETION.md) |
| Death and resurrection | native-verified | High | Native duel defeat and reconstruction. [PvP](PVP.md) |
| Death and resurrection | native-verified | High | PR287 observed the down pose past nine seconds, decline followed by a fresh 416 offer, standing motion, restored HP/IS, movement and Death Effect. [Death presentation](DEATH-PRESENTATION.md) |
| Remote resurrection | headless-only | Medium | The server revived QAFriend in PR287. The lobby camera obscured the full standing motion. Repeat death and the PR287 duel check remain open. [Death presentation](DEATH-PRESENTATION.md) |
| NPC aggro and wander | native-verified | High | Grounded legs and idle pauses, selection/radial hold and chase to interlock on PR281 `5f74448f`. [Native checklist](GAMEPLAY-BREADTH-QA.md) |
| NPC chase around corners | broken | High | Native QA observed a pursuit stop at a railing. Retail movement speeds remain open. |
| PvP flagging | native-verified | Medium | Portrait/flag messages and ordinary attacks on a hostile flagged target. [PvP](PVP.md) |
| Levelling past 2 | native-verified | High | Level 3/5235 XP, HP250/IS106 and persistence on normal relog on PR281 `5f74448f`. [Native checklist](GAMEPLAY-BREADTH-QA.md) |
| Levelling past 3 | headless-only | High | A normal kill reaches level 4 with the corresponding maxima and persists on relog. |

[Native QA sessions](GAMEPLAY-BREADTH-SESSIONS.md)
