# Agent Gray: Prima Machines missions1–10

This package covers Agent Gray's **Prima seduction chain**, template2201–2210/orders11–20: ten missions,25 phases and52 goals. It is distinct from the later archive critical chapters named Mach1.x or Mach10.x. Source: Prima Official Game Guide (2005), printed pp.231–236, locally extracted in mxo-research/prima/missions-machines.json and its FINDINGS.md. The book is not copied into this repository.

Existing XML preserves the guide's titles, summaries, phase text, goal intent and enemy organisations. Names/appearances, template IDs, generated locations, distances, pacing dependencies and item GOID choices are emulator authorship. Narrative mentions of card-key/combination/trap puzzles do not establish their retail implementation here. Reputation, complete retail NPC AI and exact retail mission generation remain outside this package. Rewards use [the explicit OURS reference-level policy](MISSION-REWARDS.md), with the default archive reference50; these files do not invent capture levels.

## Concrete interaction repairs

- **Agent Information2207:** the dossier now uses the already implemented **corpse Loot** path on live operatives Capella or Tycho. Its item must be captured when an ordinary kill creates the killer-owned loot cache, so the Loot goal is open before either dies. The separate gated DefeatAll still requires all hostiles. The player searches an actual corpse and hands the resulting dossier to Agent Harrow; a furniture stand-in is removed. Choosing either operative and this pacing are OURS.
- **The Stolen Artifact2210:** Antares starts friendly so the client can offer normal Talk/Get. Taking the artifact (phase3 goal2) flips him hostile through the existing hostileAfter mechanism, then opens the kill-all goal. Hostile NPCs do not expose the ordinary friendly Giver tracker; merely sending hidden Talk/Get RPCs would not establish a usable native path. The handoff/hostility timing is authored glue for the printed find/take/kill sequence.

The shared furniture-capacity fix is inherited from the Zion chain parent; it is not independently reimplemented. Computer/Mainframe Put uses the shared source-backed correction: gameobjects-derived [object_states.csv](../data/object_states.csv) ST_Taker0x962 enables use8, [object_usables.csv](../data/object_usables.csv) associates Put with4006 GenericKeyTest, and [clientrules.py](../research/client-scripts/clientrules.py#L295) requires a matching mission-key item. Earlier empty-inventory native probes did not establish that Put was absent. Take uses5, Hack uses4; containers use Open3 and ordinary loot messages. No GenericKey bypass or fabricated completion command is used.

## Fixture and proof contract

Discovered mode `machines-chain` starts a level50 Machine actor at Mara with only the seven Tyndall introduction prerequisites recorded:1201,1204,1203,1205,1206,1207,1208. Selected2201–2210 receipts are never seeded. Agent Gray is the remote mission contact; no fake street contact is spawned. Existing dev commands allow travel/HP preparation. `MXO_MISSION_SITE_QA_SEED=220110` affects only site/layout selection under the shared exact-dev/disposable-database guard. Combat, drops and callback RNG are not forced; no GM or health hold is enabled.

The headless chain driver earns every selected completion through mission list/details/accept, normal80c7 interactions, real combat/loot, escort movement, hardline upload and ordinary phase exits. It observes actual80a0 completion records and80a2 mission ends and checks one persisted completion per selected mission. It does not call MissionProgress.Complete, send a force-complete command or grant selected completion receipts. Native UI acceptance is a separate Claude-only check. Unit tests cover contact/ordering/prerequisites, all25/52 content references, item flow, the Antares handoff-side transition, real corpse-loot eligibility and mode privileges.

## Claude-only one-pass native route

Use a fresh `machines-chain` disposable profile. Open the Mission Contacts window, choose **Agent Gray**, select the first Critical mission and accept. After each completion return to Agent Gray for the next mission in the table. Follow the marked door; use the real mission elevator prompt. Perform the objective shown in the native HUD, using right-click/radial menus. Existing `?weaken 1` before combat is optional QA preparation; then use ordinary right-click Close Combat or normal ranged attacks. Do not use a direct-completion shortcut.

| Mission / guide page | Phase route |
|---|---|
|2201 The End of the Beginning /231|Open the marked safe and take its dossier. At the next site Give it to Alnitak, defeat the Zion attackers and keep Alnitak alive; exit.|
|2202 Watchful Eyes /231|Get both devices from Herschel. At each subsequent site use the marked computer's **Put** radial while carrying the matching mission item; exit each site.|
|2203 Revolting Exiles /232|Defeat Lucien, then Ottavia, then Ambrose at their three sites. In the last site Open the safe and take the plans before exiting.|
|2204 Viral Insurance /232|Get the virus from Agent Kessler. At the next site Put it in the mainframe; exit.|
|2205 Compromised /233|Talk to Lattice, Get the key and lead him out alive. At the next site lead him to the marked computer, then approach/use the marked phone area and keep him alive. Phone completion remains the existing authored proximity fallback.|
|2206 Back to the Pods /233|Approach/recruit Achernar and escort him out alive. At the next site lead him to Agent Voss, retaining the follower between phases.|
|2207 Agent Information /234|Talk to Agent Harrow and lead Gaspard/Benedetto out alive. Bring them to Capella, defeat every Zion soldier plus Capella/Tycho, Loot one operative's corpse and take the dossier. At the final office Give it to Agent Harrow.|
|2208 Intel Recovery /234|Defeat the Exiles, Take the data from the computer and upload it through a real hardline radial; protect Deneb/Shaula.|
|2209 Control /235|Take the access codes, Hack the mainframe, then Take its data. Escort Bastion/Rampart/Bulwark out. At the last site Take data from each of the three marked computers; exit.|
|2210 The Stolen Artifact /236|Open the marked furniture and take the documents. Give them to Lindqvist, Hack her computer, then Take the dossier. Talk to Antares and Get the statue; reselect his new hostile view, then defeat him and all Zion crew. Give the statue to Agent Corwin at the final office.|

At each phase confirm goals close, the next briefing appears and no earlier-phase objective or stale view blocks progress. Check the final native summary/reward chat and that Agent Gray advances to the next selected mission. After the tenth, all ten receipts must have been earned once. Native QA and all merges belong to Claude; this worker never runs native setup/client/lock or touches main data.


## Recorded automated validation

On the merged persistence base, the full suite passed 4,178 tests and the real read-only NativeQa plan/provenance commands passed in Debug and Release. The normal-protocol ten-mission scenario passed all 25 phases / 52 goals in 139.4 seconds (exit0, peak one disposable server), including enabled computer Put8/Take5/Hack4, actual giver trackers, corpse loot and the final handoff. No native-client PASS is claimed.

An earlier run completed missions2201�2209 and2210 phase3 but timed out on its final elevator callback. That failure is retained; the successful retry does not prove this intermittent timing issue fixed. Bounded callback/current-mission/navigation logs were added for diagnosis. The successful last rides ran after about2.0�2.1seconds and returned through navigation.
