# Gameplay breadth evidence

`native-verified` identifies a named result observed in the real client. `headless-only` means the listed
path has automated evidence and still needs native QA on the current behavior. `broken` marks a reproduced
player-facing failure. A row covers the behavior described in that row.

| System | Evidence | Player impact | Verified scope or remaining check |
|---|---|---|---|
| Vendors | native-verified | High | Recycle cancel/confirm, bit-stack and leveled-code quotes, and Antibiotic activation. [Economy](VENDOR-ECONOMY.md) |
| Vendors | native-verified | High | PR287 observed buys and buyback of the 680-information shirt, 12,600-information leveled code and eight Code Bits. Buy Back restored each item and the full stack. [Buyback](VENDOR-BUYBACK.md) |
| Vendor Appraise | native-verified | High | PR291 showed one System-tab quote of 680 for the shirt and 10 for eight Code Bits. The item and wallet stayed intact. [Economy](VENDOR-ECONOMY.md) |
| Vendor discovery | headless-only | High | The old shop ID points away from its listed coordinates. The new fixture tests the Baldwin Heights Clothing Merchant at its shipped object location. Native discovery and interaction are pending. [Economy](VENDOR-ECONOMY.md) |
| Hardline travel | native-verified | High | PR295 traveled from Mara Northwest to South Vauxton. PR291 also jacked back in from an ordinary booth exit in Richland and Medium03. [PR295 native QA](https://github.com/kylebrussell/mxo-hd/pull/295#issuecomment-6982587085), [Hardline registration](LA-HARDLINE-REGISTRATION.md) |
| Loading Area exit | native-verified | High | PR295 left via Tabor Central and jacked in at Mara Northwest. PR291 confirmed Reconstruct at Mara Central with a standing, movable return. [PR295 native QA](https://github.com/kylebrussell/mxo-hd/pull/295#issuecomment-6982587085), [Loading Area](LA-HARDLINE-REGISTRATION.md) |
| Loading Area Character and Equipment preview | native-verified | Low | PR300 verified CSR's model and equipped outfit in both tabs after a booth exit and after Reconstruct, with Jack In to Mara Northwest each time. The Hardline label was blank after Reconstruct. [PR300 native QA](https://github.com/kylebrussell/mxo-hd/pull/300#issuecomment-6983961037) |
| Subway | native-verified | Medium | PR295 took the Yellow Line from South Vauxton to Tabor Park and reopened the map in the arrival car. [PR295 native QA](https://github.com/kylebrussell/mxo-hd/pull/295#issuecomment-6982587085) |
| Teams | native-verified | High | An ordinary helper kill credited the holder's goal and Team Contact awarded one completion on PR281 `6a8f2c15`. [Native checklist](GAMEPLAY-BREADTH-QA.md) |
| Teams | headless-only | High | Shared loot and relog coverage. Helper HUD and mission-item actions remain open. |
| Crews | native-verified | Medium | PR293 verified recruit, refusal, roster and First Mate. [Social fixture](GAMEPLAY-BREADTH-PEERS.md) |
| Faction ranks and chat | native-verified | Medium | PR294 verified the seeded roster, promotion, rank refusal, `/fc` chat both ways and removal. [Social fixture](GAMEPLAY-BREADTH-PEERS.md) |
| Faction founding | native-verified | Medium | PR297 opened the unaffiliated Faction panel, named and founded QAFaction with Recruit, accepted through QAOther and verified both crews at ranks 1 and 5 in More. [PR297 native QA](https://github.com/kylebrussell/mxo-hd/pull/297#issuecomment-6983521866), [Native founding checklist](GAMEPLAY-BREADTH-PEERS.md#faction-founding) |
| Area chat | native-verified | Medium | CSR sent the command that triggered the teammate helper during native PR281 QA. |
| Crew chat and paired emotes | native-verified | Medium | PR293 verified crew chat both ways, the hug offer and accepted animation, and no paired animation on decline. [Social fixture](GAMEPLAY-BREADTH-PEERS.md) |
| Hug refusal feedback | native-verified | Low | PR295 showed the decline message in native System chat. [PR295 native QA](https://github.com/kylebrussell/mxo-hd/pull/295#issuecomment-6982587085) |
| Chat rooms | broken | Medium | The stock client rejects the room commands. The server still handles the room routes in headless QA. [Native chat command check](CLIENT-ACTIONS.md#chat-rooms) |
| Trade | native-verified | High | The Secure Trade window, item/info exchange, Clear and Cancel with the QA bot. [Secure Trade](SECURE-TRADE.md) |
| Trade | native-verified | High | PR287 observed player-targeted and incoming offers, partner Accepted/Considering labels, a 250-information settlement, both cancellation paths and relog. [Secure Trade](SECURE-TRADE.md) |
| Trade Accept checkbox | native-verified | High | PR291 observed a clear checkbox after a partner edit with the pointer away, the reset notice, one settlement after a fresh accept, and a clear incoming window after completion. [Secure Trade](SECURE-TRADE.md) |
| Marketplace purchase | native-verified | High | PR293 verified item delivery, buyer and seller information, and purchase persistence across relog. [Marketplace fixture](GAMEPLAY-BREADTH-PEERS.md) |
| Marketplace stale Buy and Code Bit search | native-verified | High | PR294 verified Code Bit search, the stale-listing refusal dialog and fresh search in the same session with buyer bag and Info intact. [Marketplace fixture](GAMEPLAY-BREADTH-PEERS.md) |
| Crafting and compiling | native-verified | High | PR287 observed tool activation without an information debit, timed Health Boost 3.0 compiles with charge use, and one 9-information completion when closing the 2399 panel. [Ordinary compile](COMPILE-ORDINARY-IC.md) |
| Crafting and compiling | headless-only | High | Current timing/pricing, charge and material depletion, cancellation and rollback. [Storage depletion](CRAFTING-STORAGE-DEPLETION.md) |
| Death and resurrection | native-verified | High | Native duel defeat and reconstruction. [PvP](PVP.md) |
| Death and resurrection | native-verified | High | PR287 observed the down pose past nine seconds, decline followed by a fresh 416 offer, standing motion, restored HP/IS, movement and Death Effect. [Death presentation](DEATH-PRESENTATION.md) |
| Remote resurrection | native-verified | High | From the outdoor PR290 fixture, CSR saw QAFriend go down, stand after resurrection, and move. The same helper could make a second 416 offer after a decline. [Death presentation](DEATH-PRESENTATION.md) |
| NPC aggro and wander | native-verified | High | Grounded legs and idle pauses, selection/radial hold and chase to interlock on PR281 `5f74448f`. [Native checklist](GAMEPLAY-BREADTH-QA.md) |
| NPC chase around corners | headless-only | High | A Blackwood Goof followed the Mara corner to interlock. The railing regression uses a synthetic NAV gap. Native retest and retail speed calibration are open. [NPC chase QA](GAMEPLAY-BREADTH-QA.md#npc-chase-around-the-mara-corner) |
| PvP flagging | native-verified | Medium | Portrait/flag messages and ordinary attacks on a hostile flagged target. [PvP](PVP.md) |
| Levelling past 2 | native-verified | High | Level 3/5235 XP, HP250/IS106 and persistence on normal relog on PR281 `5f74448f`. [Native checklist](GAMEPLAY-BREADTH-QA.md) |
| Levelling past 3 | headless-only | High | A normal kill reaches level 4 with the corresponding maxima and persists on relog. |

[Native QA sessions](GAMEPLAY-BREADTH-SESSIONS.md)
