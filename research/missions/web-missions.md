# The Matrix Online: mission list from web research

Compiled 2026-11-08. Paraphrased notes on facts; nothing was downloaded and no screenshot was opened.

## How far to trust this file

- **Fandom wiki entries were read through search-index excerpts only.** The wiki returns HTTP 402 to the fetch tool, so its mission tables came back in fragments. Names may have column errors. Check them against the screenshot logs.
- **IDs are inferred from list order** (position N in the wiki's list = mission N). Three cross-checks agree: Merv "Good Manners" = 6.3.3 ([forum](https://soeforums.mxoemu.info/mxo/posts/list2bcb.html?start=0&topic_id=36300009142)), Zion "Phreak" = third 9.1 mission ([wiki](https://matrixonline.fandom.com/wiki/Phreak_(Episode_9.1))), Merv "The Disputed Death of Mary MacHenry" = 8.3.3 ([dev plan](https://soeforums.mxoemu.info/mxo/posts/list4a08.html?start=15&topic_id=36300027578)).
- **Known conflicts:** "Recruit with Machines" and "Mechanical Harmony" appeared under Zion in one excerpt and Machines in another. "Persistence" is called 10.3 on one page but sits in the 10.1 row. "Down the Wire" is listed in both Machines 2.2 and 7.2. "Hell-Broth" is listed in 7.2, with a stray appearance in 6.2. The 9.3 Machine and Merv lists show six names where five are expected.
- **Dev plans are not as-shipped.** Rarebit says his outlines were written beforehand and differ from what went live.

## Primary evidence: the Filing Cabinet screenshot logs

- [hdneo/mxo-missions](https://github.com/hdneo/mxo-missions): folders of screenshots per mission, credited to Vesuveus. 4.07 GB by the GitHub tree (9,097 JPGs). Original [Google Drive folder](https://drive.google.com/drive/folders/1NUDd-jZ3mzUsvCNAkAbbQ2JnCJOgXbLI) (size not checked).
- Its [ReadMe_2021.txt](https://github.com/hdneo/mxo-missions/blob/main/ReadMe_2021.txt) claims 529 critical missions, 516 logged (97.54%). Stated gaps: Merv 1.2.13, Merv 3.2.1 to 3.3.5, Merv 4.2.5, Zion 1.3.5.

| Org | Folders in GitHub copy | Screenshots | Expected | Gap |
|---|---|---|---|---|
| Machines | 179 (177 plus Mach1-2-17_2 and Mach8-3-4_Bonus) | 3,589 | 177 | none visible |
| Merovingian | 164 | 3,529 | 176 | matches the 12 stated gaps |
| Zion | 90 | 1,979 | 177 | **about 87 missing: only 5.1.1, 5.1.2, 7.2.2, 8.3.4, 9.2.1 to 9.3.5, 10.2.5, 11.1.5, 11.2.3, 11.2.4, 11.3.2, 11.3.3 exist after 4.3** |

The Zion gap is in the GitHub copy only as far as the readme can tell (it lists just 1.3.5 as missing), so the Drive folder may hold the rest. Not verified. Expected counts: 1.1 = 3, 1.2 = 17 (Merv 16), 1.3 = 6, then 5 per subchapter through 11.2, then 3 each for 11.3 and 12.1.

## Missions with objective detail

Steps are paraphrased. WIKI = search excerpt of a fan transcript. LOG = player recap on the forum mirror.

### Starter and early

- **Tutorial** (no mission name found): first thing after character creation; teaches movement, combat and interaction; replayable from the hardlines list in the Loading Area. [Official DN1 copy](https://mxoarchive.net/archives/dn1/thematrixonline.warnerbros.com/web/live/game_info/game_basics.html)
- **Zion 1.1 Redpill Rescue**, Tyndall: kill the Machine operatives pinning Zion people, search the bodies, lead Duboshin, Altaj and Tolusa to a hardline. WIKI [page](https://matrixonline.fandom.com/wiki/Redpill_Rescue_(Episode_1.1))
- **Zion 1.2 The Power of Propaganda**, Tyndall: tear down Morpheus posters across two buildings. WIKI [page](https://matrixonline.fandom.com/wiki/The_Power_of_Propaganda_(Episode_1.2))
- **Zion 1.2 Unexpected Alliances**, Tyndall: talk a Zion captain out of helping Morpheus without fighting him or his crew. WIKI [page](https://matrixonline.fandom.com/wiki/Unexpected_Alliances_(Episode_1.2))
- **Machines 1.2 Disturbing Trends**, Agent Gray: ask a Zion-loyal source about Morpheus; prove you are not with the Machines by stealing code for her (worthless, so Gray allows it). WIKI [page](https://matrixonline.fandom.com/wiki/Chapters)
- **Merv 1.2 Mod Box**, Flood: collect a modified Black Box used to damage hardline junction boxes, take it to a Merv operative, repair a junction box. WIKI [page](https://matrixonline.fandom.com/wiki/Mod_Box_(Episode_1.2))
- **Machines 1.3 Trace Route**, Agent Gray: trace the Assassin through blowfly code. WIKI [page](https://matrixonline.fandom.com/wiki/Trace_Route_(Episode_1.3))
- **Merv 1.3 The Murder of Morpheus**, Flood: search a rundown building the killer visited; crash Exile Amun's party, collect virus-laced liquor, deliver it to Stanley Watson, who then talks. WIKI [page](https://matrixonline.fandom.com/wiki/The_Murder_of_Morpheus_(Episode_1.3))
- **The Stolen Artifact (Machines)**, Agent Gray, initiation: find Castafiore's crew in a Redpill Extraction Point, take the artifact, then kill your old recruit as a loyalty test. WIKI [page](https://matrixonline.fandom.com/wiki/The_Stolen_Artifact_(Machine_Mission))
- **The Stolen Artifact (Merv)**, Flood, initiation: same artifact; final loyalty test is killing Pegasi. WIKI [page](https://matrixonline.fandom.com/wiki/The_Stolen_Artifact_(Merovingian_Mission))

### Chapters 2 to 7

- **Machines 2.3.4 Investigations** (archive): kill two bodyguards so the contact will talk; he points to a Merv phage in a Westview building; about 20 non-hostile Mervs inside; search computers. Players found it unclear which NPC advances it. LOG [thread](https://soeforums.mxoemu.info/mxo/posts/listb44d.html?topic_id=36300027476)
- **Merv 2.3 Enter Desayd**, Flood: retrieve a package from Finsen's rival Alligator and deliver it to Desayd. WIKI [page](https://matrixonline.fandom.com/wiki/Enter_Desayd_(Episode_2.3))
- **Merv 2.3 Dissension**: Desayd turns hostile; commando Phillips has you carry an alliance proposal. WIKI [page](https://matrixonline.fandom.com/wiki/Dissension_(Episode_2.3))
- **Machines 2.3 Damage Control**, Agent Gray: watch Zionist Loti and contact Tad Zappa; commando Robertson joins, and his RSI gets scrambled. WIKI [page](https://matrixonline.fandom.com/wiki/Damage_Control_(Episode_2.3))
- **Machines 3.1 Operation Myrmidon**, Agent Gray: strike commandos and scan a dossier pointing to the Colonel. WIKI [page](https://matrixonline.fandom.com/wiki/Operation_Myrmidon_(Episode_3.1))
- **Machines 3.1 Decapitation**: target the Colonel; his RSI carries a signal tied to a bomb under a section of pods. WIKI [page](https://matrixonline.fandom.com/wiki/Decapitation_(Episode_3.1))
- **Zion 3.1 Back Door**, Tyndall: enter a building the Merv uses to reach constructs; take a data disk from a new Exile type. WIKI [page](https://matrixonline.fandom.com/wiki/Back_Door_(Episode_3.1))
- **Zion 3.1 Dire Connection**, Tyndall: hunt dire lupine activity, leading to the Effectuator. WIKI [page](https://matrixonline.fandom.com/wiki/Dire_Connection_(Episode_3.1))
- **Merv 3.1 The Gatekeeper**: escort Dire Lupines to the Effectuator's portal and hold off Zionites. WIKI [page](https://matrixonline.fandom.com/wiki/The_Gatekeeper_(Episode_3.1))
- **Zion 4.1 Scanning the Enemy**: meet commandos and test new troops against Merv forces. WIKI [page](https://matrixonline.fandom.com/wiki/Scanning_the_Enemy_(Episode_4.1))
- **Machines 4.2 Distractions**: repel attacks on two facilities, then learn it was a diversion to free the Effectuator at Tabor Park. WIKI [page](https://matrixonline.fandom.com/wiki/Distractions_(Episode_4.2))
- **Zion 4.2 The Kid**, Anome: find the Kid; his room is full of EPN Crusaders, so you leave. WIKI [page](https://matrixonline.fandom.com/wiki/The_Kid_(Episode_4.2))
- **Zion 4.2 Groundwork**, Tyndall: plant a bug in a Merv operations center. WIKI [page](https://matrixonline.fandom.com/wiki/Groundwork_(Episode_4.2))
- **Machines 5.1 Mysteries Revealed**: get Anome data from a Zion representative and send it for analysis; trail goes cold. WIKI [page](https://matrixonline.fandom.com/wiki/Mysteries_Revealed_(Episode_5.1))
- **Zion 5.2 Repairman**, the Effectuator: repair sabotaged hardline junction boxes. Reputation effect: -1 Machines, -1 Merv. WIKI [page](https://matrixonline.fandom.com/wiki/Repairman_(Episode_5.2))
- **Machines 5.3 Blackout**: rolling blackouts; protect a failing power control station. WIKI [page](https://matrixonline.fandom.com/wiki/Blackout_(Episode_5.3))
- **Zion 5.3 Paging Ms. Utley**: Anome's forces threaten drinking water. WIKI [page](https://matrixonline.fandom.com/wiki/Paging_Ms._Utley_(Episode_5.3))
- **Machines 6.1 The Price of Betrayal**, Agent Gray: find a program who helped write the cheat codes; he is shut down; a kill code for Anome is still needed. WIKI [page](https://matrixonline.fandom.com/wiki/The_Price_of_Betrayal_(Episode_6.1))
- **Zion 6.2 Subtext**: attend a meeting between Niobe and the Machines. WIKI [page](https://matrixonline.fandom.com/wiki/Subtext_(Episode_6.2))
- **Machines 6.3.4 Access Denied**: shut down commando access points. WIKI [page](https://matrixonline.fandom.com/wiki/Access_Denied_(Episode_6.3))
- **Zion 6.3.2 Whirlybird**: reach the Oracle during the Merv's attack, then find where commandos took Sati. WIKI [page](https://matrixonline.fandom.com/wiki/Whirlybird_(Episode_6.3))
- **Merv 6.3.3 Good Manners**: reveals Hypatia's ties to the Bartender and a deal with Deus Ex Machina. LOG [thread](https://soeforums.mxoemu.info/mxo/posts/list2bcb.html?start=0&topic_id=36300009142)

### Chapter 8 onward

- **Zion 8.1.1 War**, Tyndall: Machine agents attacking operatives outdoors in Westview, International and Downtown; report to Ghost's emergency briefing. WIKI [page](https://matrixonline.fandom.com/wiki/War_(Zion))
- **Merv 8.1.1 War**: 4 areas: talk to Malphas; search area, far-room computer, upload at a hardline; talk to Dame White and Mr Black; talk to the Merovingian. No enemies. LOG [thread](https://soeforums.mxoemu.info/mxo/posts/list41e8.html?topic_id=36300028422)
- **Merv 8.1.2 Sea of Serenity**: 4 areas: Malphas and the Effectuator; search computer, talk to the Effectuator; search computer past higher-level Zion enemies, then exit; talk to the Merovingian. LOG [thread](https://soeforums.mxoemu.info/mxo/posts/list41e8.html?topic_id=36300028422)
- **Merv 8.2.1 The Fields**: 4 areas: Persephone; search area and far computer; the Coroner; Malphas and the Merovingian. No enemies. LOG [thread](https://soeforums.mxoemu.info/mxo/posts/list41e8.html?topic_id=36300028422)
- **Machines 8.2.1 Process of Elimination**, Agent Gray: destroy a Merv business, cut his ties to the Auditor and Hypatia, deactivate a relay to the General. WIKI [page](https://matrixonline.fandom.com/wiki/Process_of_Elimination_(Episode_8.2))
- **Machines 8.3 White Hallway Data Retrieval** (bonus): outpost full of hostile Zion NPCs. WIKI [page](https://matrixonline.fandom.com/wiki/White_Hallway_Data_Retrieval_(Episode_8.3)), [recap file](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach8-3-4_Bonus)
- **Zion 9.1.3 Phreak**: use a land-line at a police-call site, disable the Machine jammer, retry; the intruder picks up briefly. WIKI [page](https://matrixonline.fandom.com/wiki/Phreak_(Episode_9.1))
- **Merv 9.1.1 Apparition**: check on blood-drinkers in trouble, then join the hunt for the wire-using intruder. WIKI [page](https://matrixonline.fandom.com/wiki/Apparition_(Episode_9.1))
- **Merv 9.2.2 Down to a Sunless Sea**: search Machine records for Stalingrad references from the General's destroyed base. WIKI [page](https://matrixonline.fandom.com/wiki/Down_to_a_Sunless_Sea_(Episode_9.2))
- **Zion 10.1 Unfulfilled**: after the Oracle's death, pursue override programs and a suspected Carlyne signal. WIKI [page](https://matrixonline.fandom.com/wiki/Unfulfilled_(Episode_10.1))
- **Machines 10.3.2 Help Desk**, Agent Gray: neutralize Exiles at a location and search their data. WIKI [page](https://matrixonline.fandom.com/wiki/Help_Desk_(Episode_10.3))
- **Zion 11.3.1 Promises**, Tyndall: meet the injured Kid in west Hampton Green, investigate override readings, fight Accelerated Suits, meet Danielle Wright. LOG [thread](https://soeforums.mxoemu.info/mxo/posts/list71e6.html?topic_id=36300026386)
- **Zion 11.3.2 Resistance**: search override area in Hampton (Machine Howitzers, no trail); second area, new accelerated program, dead Cypherite. LOG [same](https://soeforums.mxoemu.info/mxo/posts/list71e6.html?topic_id=36300026386)
- **Machines 11.3.1 Dead Suits**, Agent Gray: glitched Downtown computer room with dead bluepill; fight Exiles then Accelerated Suits; intercept Wright in Maribeau. LOG [same](https://soeforums.mxoemu.info/mxo/posts/list71e6.html?topic_id=36300026386)
- **Machines 11.3.2 Measure and Countermeasure**: meet Veil; collect override code; bring it to Neglect (13th floor south of Club Cyclo, -1411 107 -326); fight three Wright programs; return. LOG [same](https://soeforums.mxoemu.info/mxo/posts/list71e6.html?topic_id=36300026386)
- **Machines 11.3.3 Severance**: beat a level 55 Wright Accelerated Program; meet Cryptos; get Decelerate and Signal Jamming anti-overrides; confront Wright on Hampton Green rooftop (98 131 -216). LOG [same](https://soeforums.mxoemu.info/mxo/posts/list71e6.html?topic_id=36300026386)
- **Merv 11.3.1 Mismanagement**, Flood: scout Hampton, Nicky G, Suit Executive, follow Manager's team to the Historic District, return. LOG [same](https://soeforums.mxoemu.info/mxo/posts/list71e6.html?topic_id=36300026386)
- **Merv 11.3.2 The Mechanical Man**, Flood: Ouroboros outpost in Vauxton, computer, beat Agent and guards, upload file; recover data from the Machine facility in Maribeau. LOG [same](https://soeforums.mxoemu.info/mxo/posts/list71e6.html?topic_id=36300026386)
- **Merv 11.3.3 Empty Vessel**, Flood: reach mainframe in Park East, beat two Machine Captains, the Effectuator unlocks it, retrieve data, meet the Merovingian, Persephone and the Twins. LOG [same](https://soeforums.mxoemu.info/mxo/posts/list71e6.html?topic_id=36300026386)
- **Zion 12.1.1 Self Image**, Tyndall: talk to Ghost, search Murasaki for a signal, kill Machine and Oligarch hostiles; points to Shirakaba. LOG [thread](https://soeforums.mxoemu.info/mxo/posts/list1797.html?topic_id=36300027006)
- **Zion 12.1.2 Angel**: gated by trading Shirakaba override traces to the Ikebukuro contact; level 40 per the wiki; search Downtown, Trinity appears, kill Exiles and Elite Guards. LOG [thread](https://soeforums.mxoemu.info/mxo/posts/list1797.html?topic_id=36300027006), WIKI [page](https://matrixonline.fandom.com/wiki/Angel_(Episode_12.1))
- **Zion 12.1.3 Impersonal**: gated by trading Baldwin Heights function fragments; talk to Ghost, search area, kill the Override Executable. LOG [thread](https://soeforums.mxoemu.info/mxo/posts/list1797.html?topic_id=36300027006)
- **Merv 12.1.1 Hothouse**, Flood: follow a lead with Ookami, meet Oligarch Tesarova, beat Machine redpills; afterwards kill 4 Accelerated Exile groups in South Vauxton for 4 Override Particles to unlock the next mission. LOG [thread](https://soeforums.mxoemu.info/mxo/posts/list1797.html?topic_id=36300027006)
- **Merv 12.1.2 Deep Cover**, Flood: beat Override Functions at Azuna's old spot; find her on the museum's second sublevel in Creston Heights; trade ten fragments for Azuna's Surveillance item. LOG [thread](https://soeforums.mxoemu.info/mxo/posts/list1797.html?topic_id=36300027006)
- **Machines 12.1 Representative**, Agent Gray: starts in International at level 30 with 20 reputation. WIKI [page](https://matrixonline.fandom.com/wiki/Representative_(Episode_12.1))

## Developer design briefs by crit (Rarebit, Feb 2009)

Unnamed one-liners per org from the dev's own plan. Mostly as planned, not as shipped. Each is paraphrased.

Sources: [5.2 to 8.2](https://soeforums.mxoemu.info/mxo/posts/list0747.html?topic_id=36300027578), [8.3 to 10.3](https://soeforums.mxoemu.info/mxo/posts/list4a08.html?start=15&topic_id=36300027578), [10.3 to 12.1](https://soeforums.mxoemu.info/mxo/posts/liste4a7.html?start=30&topic_id=36300027578). Outline: [chapters 7 to 14](https://soeforums.mxoemu.info/mxo/posts/listd645.html?topic_id=36300027574).

Chapters 6.1 to 7.3 are in the first link but were only read at chapter level, so they aren't broken out here.

| Crit | Zion | Machines | Merovingian |
|---|---|---|---|
| 5.2.1 | Find source of network interference | Study new enemy code | Find who is blocking ports |
| 5.2.2 | Isolate enforcers' code signature | Track "B" | Track "A" and junction-box sabotage |
| 5.2.3 | Repair junction boxes | Analyze diluted code | Identify "C", get bluepill target list |
| 5.2.4 | Fix last junction box | Track "D" | Clear Anome's goons from hubs |
| 5.2.5 | Wrap-up with Effectuator, Ghost, Niobe | Altered cheat-code effects | Recover discarded vial |
| 5.3.1 | Unlimit attacks P-A warehouses | Plot against aqueduct control | Test synthesized codes |
| 5.3.2 | Poisoned food at warehouses | Foil G's sabotage | Capture Unlimit boss, trace "I" |
| 5.3.3 | Foil food poisoning | Stop power-station attack | Persephone as bait for "I" |
| 5.3.4 | Water tampering, link to "E" | Push "H" to breakdown | Nearly catch "I" |
| 5.3.5 | Water plot, Brenda missing | Begin Anome kill-code | Fluid samples, decompile code |
| 8.1.1 | Check facilities, meet Niobe, hovercraft berth | Network hub, Lock directive | Retrieve General code, Dame White and Mr Black |
| 8.1.2 | Carry firewall to new lab | Virus into Zion nodes | Zion tunnel map via bluepill front |
| 8.1.3 | Broadcast data to emergency up-link | Theft leads to broadcast center | Seize the Ardeshir, direct Sentinels |
| 8.1.4 | Missing recruiting manager | Capture recruiter, upload data | Meet the Oracle via Seraph |
| 8.1.5 | Rescue a potential from detention | Red pill extraction of Navin Manohar | Power research with Raini, Silver, Sunshine |
| 8.2.1 | Mainframe transfer, recruit Wright | Pressure Auditor, Hypatia, relays | Birth and death stats, Coroner, Malphas |
| 8.2.2 | Test Wright's prototype pills | Utility failures, SWAT ambush | Retaliate against EPN |
| 8.2.3 | Pod-control code via Persephone | Raid Wright Research, get a pill | Infiltrate Wright Research, lupine trap |
| 8.2.4 | Distribute pills, extraction choice | Argent Biometrics, warehouse, Exile | Pod clusters, Sunshine's estimates |
| 8.2.5 | Gate hijack, find Niobe's beacon | Trace Wright, virus into mainframe | Hijack media dish to reach General |
| 8.3.1 | Colt introduced, defend facilities | Morpheus sim data | Identify Mauser |
| 8.3.2 | Persuade Morpheus sim | Persuade Morpheus sim | Oracle, then Taskmaster |
| 8.3.3 | Morpheus data from General | Cryptos deal for RSI archive | MacHenry's disappearance |
| 8.3.4 | Rescue compromised operatives | Decrypt archive, kill Zionites | Caretaker program DMT3R |
| 8.3.5 | Rescue Strenlo, evacuate Maston | Hunt veteran Zionites | Retrieve rev.1 data |
| 9.1.1 | Track Intruder | Missing Agents | Dead Accelerated Exiles |
| 9.1.2 | Retrieve the Auditor | Hunt Intruder | Introduce Intruder to Merv figures |
| 9.1.3 | Phone Intruder, disable jammer | Disrupt Zion contact | Crush Zion callers |
| 9.1.4 | Arrange meeting | Find way to talk | Meetings with Persephone, the Chef |
| 9.1.5 | Colt talks to Intruder | Intruder's origin | Confront Intruder |
| 9.2.1 | Block network access | Track override codes | Dig for dirt |
| 9.2.2 | Neo's wake-up room | Hunt override programs | Stalingrad research |
| 9.2.3 | Lock's message | Counter accelerated programs | Investigate Sati |
| 9.2.4 | Surface lab | Counter Terminator program | Biological interface program lead |
| 9.2.5 | Search Wright Research | Meet the Architect | Persephone and Silver |
| 9.3.2 | Halborn pumps Colt | Architect names Oracle as author | Give Halborn Oracle's location |
| 9.3.3 | Fights in Mara | Find the Oracle, Seraph arrives | Carlyne exposes player |
| 9.3.4 | Carlyne vs Halborn | Architect meeting, follow Halborn | Override feedback, bail |
| 9.3.5 | Oracle contacts player | Oracle code fragments | Bait Carlyne with fake Agents |
| 10.1.1 | Plan against Halborn | Halborn meets Pace | Flood reviews Oracle codes |
| 10.1.2 | Broadcast clusters attacked | Test EPN with fake message | Ship location, 800 miles NW |
| 10.1.3 | Blackmail commando for database | Halborn wipes computers | General's no-fly zone |
| 10.1.4 | Scan Halborn's ship | Halborn angry at trap | Machine ambush, end deal |
| 10.1.5 | Carlyne leaves, overrides persist | Cryptos starts resistance code | Blackout facility |
| 10.2.1 | Mauser at internet cafe | Harass Zionites | EEG pod broadcast |
| 10.2.2 | Restricted network, phone | Bank log to Mauser | Boost General's signal |
| 10.2.3 | Bluepill office, Soren | Signal near old Zion | Capture clerk's mainframe |
| 10.2.4 | Dead Nightmare Exile | Lock data to Wright's lab | Mauser found dead |
| 10.2.5 | Coppertop switchbox | Mauser scan disrupted | Oligarch data headers |
| 10.3.1 | Cryptic message to Silver | EPN ambush, shut Mauser line | Kidnapped OR manager, level 5 clerk |
| 10.3.2 | Wright facility break-in | Repel Mervs at OR | OR passwords |
| 10.3.3 | Trace hack to Ouroboros | Offer help, rebuffed | Letter exposing tampering |
| 10.3.4 | Extract blueprint from insider | Subpoena Utley | (not read) |
| 10.3.5 | Hack OR, meet Seraph | Chase Mervs in subnet | Break firewall |
| 11.1.1 | WR's reps bribed | BIP stolen from OR | Analyze BIP |
| 11.1.2 | WR break-in lead | Quell riots | Find Silver |
| 11.1.3 | Stolen prototypes | OR services failing | BIP missing, recovered |
| 11.1.4 | Clerk in two places | Power-plant overload | Move BIP to operative |
| 11.1.5 | Confiscate lab system | Freeze OR, trace source | Carlyne dies, release BIP |
| 11.2.1 | Coordinates toward BIP | Intruder is Wright | BIP in the chateau |
| 11.2.2 | Chateau guard | Wright agrees to talk | Sentinels into no-fly zone |
| 11.2.3 | Seraph, apartment message | Chase overrides | Rescue Seraph |
| 11.2.4 | Wright's kills, pod ring | Extract BIP from Kroller | Persephone into pods |
| 11.2.5 | Slum room | Cryptos needs data | Persephone meets Wright |

## Non-critical content

- **Standard mission types:** Assassination, Courier, Retrieval, Rescue, Escort, Infiltration, Recruitment. [Guide](https://soeforums.mxoemu.info/mxo/posts/listd91a.html?topic_id=36300027193)
- **Neighborhood contact mission:** Operetta bluepill side task, plant a bug in a desk. Chessman (Tabor Park) directs you on to another contact. A filing cabinet can hold an item decompiler. [Thread](https://soeforums.mxoemu.info/mxo/posts/list9f84.html?topic_id=36300028558)
- **Neighborhood contacts:** 51 named, with coordinates. [mxoarchive](https://mxoarchive.net/contacts.html), [mxostory](http://mxostory.mxoemu.info/neighborhood2.html), [mxo-docs](https://github.com/hdneo/mxo-docs/blob/main/game-info/mission-contacts.md)
- **Archivists:** Merv Apollyon (224, 1, -1514), Machine Midian Park (1215, 1, -1155), Zion Moriah Projects (1114, 7, -74). [mxo-docs](https://github.com/hdneo/mxo-docs/blob/main/game-info/archive-contacts.md)
- **Traders:** Machine Ikebukuro NE, Zion Kaede Central, Merv Sai Kung SW. [Guide](https://soeforums.mxoemu.info/mxo/posts/listfe17.html?topic_id=35900000482)

## Named mission table

Names from the wiki grid. Where the Source link is the Chapters page, no page for that mission appeared in search.

### 1.1 Care for a Cookie?

| ID | Org | Mission (trailing ? = unsure) | Source | Screenshot log |
|---|---|---|---|---|
| 1.1.1 | Zion | Redpill Rescue | [link](https://matrixonline.fandom.com/wiki/Redpill_Rescue_(Episode_1.1)) | [Zion1-1-1](https://github.com/hdneo/mxo-missions/tree/main/Zion%20Criticals/Zion1-1-1) |
| 1.1.2 | Zion | Recruit with Machines? | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Zion1-1-2](https://github.com/hdneo/mxo-missions/tree/main/Zion%20Criticals/Zion1-1-2) |
| 1.1.3 | Zion | Mechanical Harmony? | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Zion1-1-3](https://github.com/hdneo/mxo-missions/tree/main/Zion%20Criticals/Zion1-1-3) |
| 1.1.1 | Machines | Interference | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach1-1-1](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach1-1-1) |
| 1.1.2 | Machines | Redpill Redux | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach1-1-2](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach1-1-2) |
| 1.1.3 | Machines | Scheduled for Deletion | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach1-1-3](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach1-1-3) |
| 1.1.1 | Merovingian | Digital Device | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv1-1-1](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv1-1-1) |
| 1.1.2 | Merovingian | Securing the Signal | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv1-1-2](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv1-1-2) |
| 1.1.3 | Merovingian | Signature Manipulation | [link](https://matrixonline.fandom.com/wiki/Signature_Manipulation_(Episode_1.1)) | [Merv1-1-3](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv1-1-3) |

### 1.2 Why Won't They Give Up His Remains?

| ID | Org | Mission (trailing ? = unsure) | Source | Screenshot log |
|---|---|---|---|---|
| ? | Zion | Sympathy From a Friend | [link](https://matrixonline.fandom.com/wiki/Chapters) |  |
| ? | Zion | The Power of Propaganda | [link](https://matrixonline.fandom.com/wiki/The_Power_of_Propaganda_(Episode_1.2)) |  |
| ? | Zion | Confiscation | [link](https://matrixonline.fandom.com/wiki/Chapters) |  |
| ? | Zion | Unexpected Alliances | [link](https://matrixonline.fandom.com/wiki/Unexpected_Alliances_(Episode_1.2)) |  |
| ? | Zion | Protection Racket | [link](https://matrixonline.fandom.com/wiki/Chapters) |  |
| ? | Zion | Redpill Rescue Revisited | [link](https://matrixonline.fandom.com/wiki/Chapters) |  |
| ? | Zion | Pack Grab | [link](https://matrixonline.fandom.com/wiki/Chapters) |  |
| ? | Zion | Witness Protection | [link](https://matrixonline.fandom.com/wiki/Chapters) |  |
| ? | Machines | Disturbing Trends | [link](https://matrixonline.fandom.com/wiki/Chapters) |  |
| ? | Machines | Poster Hunt | [link](https://matrixonline.fandom.com/wiki/Chapters) |  |
| ? | Machines | Hard Time | [link](https://matrixonline.fandom.com/wiki/Chapters) |  |
| ? | Machines | No Allies Here | [link](https://matrixonline.fandom.com/wiki/Chapters) |  |
| ? | Machines | Under Warranty | [link](https://matrixonline.fandom.com/wiki/Chapters) |  |
| ? | Machines | Gathering Evidence | [link](https://matrixonline.fandom.com/wiki/Chapters) |  |
| ? | Merovingian | The Search for Morpheus | [link](https://matrixonline.fandom.com/wiki/The_Search_for_Morpheus_(Episode_1.2)) |  |
| ? | Merovingian | Propaganda | [link](https://matrixonline.fandom.com/wiki/Propaganda_(Episode_1.2)) |  |
| ? | Merovingian | Turf of Vengeance | [link](https://matrixonline.fandom.com/wiki/Chapters) |  |
| ? | Merovingian | Opportunity Knocks | [link](https://matrixonline.fandom.com/wiki/Opportunity_Knocks_(Episode_1.2)) |  |
| ? | Merovingian | Renegade Roundup | [link](https://matrixonline.fandom.com/wiki/Renegade_Roundup_(Episode_1.2)) |  |
| ? | Merovingian | Mod Box | [link](https://matrixonline.fandom.com/wiki/Mod_Box_(Episode_1.2)) |  |

### 1.3 The Death of Morpheus(?)

| ID | Org | Mission (trailing ? = unsure) | Source | Screenshot log |
|---|---|---|---|---|
| ? | Zion | Lord of Flies | [link](https://matrixonline.fandom.com/wiki/Chapters) |  |
| ? | Zion | Miracle Man | [link](https://matrixonline.fandom.com/wiki/Chapters) |  |
| ? | Zion | Untouchable | [link](https://matrixonline.fandom.com/wiki/Chapters) |  |
| ? | Zion | Exodus | [link](https://matrixonline.fandom.com/wiki/Chapters) |  |
| 1.3.1 | Machines | Trace Route | [link](https://matrixonline.fandom.com/wiki/Trace_Route_(Episode_1.3)) | [Mach1-3-1](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach1-3-1) |
| 1.3.2 | Machines | The Zero One | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach1-3-2](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach1-3-2) |
| 1.3.3 | Machines | A Matter of Faith | [link](https://matrixonline.fandom.com/wiki/A_Matter_of_Faith_(Episode_1.3)) | [Mach1-3-3](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach1-3-3) |
| 1.3.4 | Machines | The Bitter Pill | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach1-3-4](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach1-3-4) |
| 1.3.5 | Machines | Downtime | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach1-3-5](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach1-3-5) |
| 1.3.6 | Machines | Bugged Line | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach1-3-6](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach1-3-6) |
| ? | Merovingian | The Murder of Morpheus | [link](https://matrixonline.fandom.com/wiki/The_Murder_of_Morpheus_(Episode_1.3)) |  |
| ? | Merovingian | Network Connections | [link](https://matrixonline.fandom.com/wiki/Chapters) |  |
| ? | Merovingian | Data Encryption | [link](https://matrixonline.fandom.com/wiki/Chapters) |  |
| ? | Merovingian | Medical Records | [link](https://matrixonline.fandom.com/wiki/Chapters) |  |
| ? | Merovingian | Love and Loathing | [link](https://matrixonline.fandom.com/wiki/Chapters) |  |

### 2.1 A Swarm of Flies

| ID | Org | Mission (trailing ? = unsure) | Source | Screenshot log |
|---|---|---|---|---|
| ? | Zion | Better Killing Through Chemistry | [link](https://matrixonline.fandom.com/wiki/Chapters) |  |
| ? | Zion | Chemical Fear | [link](https://matrixonline.fandom.com/wiki/Chapters) |  |
| ? | Zion | Chemical Reaction? | [link](https://matrixonline.fandom.com/wiki/Chapters) |  |
| ? | Zion | Poison for a Parasite? | [link](https://matrixonline.fandom.com/wiki/Chapters) |  |
| 2.1.1 | Machines | The Device | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach2-1-1](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach2-1-1) |
| 2.1.2 | Machines | Silence Speaks Louder | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach2-1-2](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach2-1-2) |
| 2.1.3 | Machines | The Return | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach2-1-3](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach2-1-3) |
| 2.1.4 | Machines | Sum of its Parts | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach2-1-4](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach2-1-4) |
| 2.1.5 | Machines | Reprisal | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach2-1-5](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach2-1-5) |
| 2.1.1 | Merovingian | Defender of the Fifth? | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv2-1-1](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv2-1-1) |
| 2.1.2 | Merovingian | Unfaithful Existence | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv2-1-2](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv2-1-2) |
| 2.1.3 | Merovingian | The Misinformation Age | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv2-1-3](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv2-1-3) |
| 2.1.4 | Merovingian | The Art of Piracy | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv2-1-4](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv2-1-4) |
| 2.1.5 | Merovingian | Meat for the Maggots? | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv2-1-5](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv2-1-5) |

### 2.2 Experimental Tests

| ID | Org | Mission (trailing ? = unsure) | Source | Screenshot log |
|---|---|---|---|---|
| 2.2.1 | Zion | In Exile | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Zion2-2-1](https://github.com/hdneo/mxo-missions/tree/main/Zion%20Criticals/Zion2-2-1) |
| 2.2.2 | Zion | Schemes and Schematics | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Zion2-2-2](https://github.com/hdneo/mxo-missions/tree/main/Zion%20Criticals/Zion2-2-2) |
| 2.2.3 | Zion | The Flit Gun | [link](https://matrixonline.fandom.com/wiki/The_Flit_Gun_(Episode_2.2)) | [Zion2-2-3](https://github.com/hdneo/mxo-missions/tree/main/Zion%20Criticals/Zion2-2-3) |
| 2.2.4 | Zion | Industrial Sabotage | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Zion2-2-4](https://github.com/hdneo/mxo-missions/tree/main/Zion%20Criticals/Zion2-2-4) |
| 2.2.5 | Zion | Rotten Fruit | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Zion2-2-5](https://github.com/hdneo/mxo-missions/tree/main/Zion%20Criticals/Zion2-2-5) |
| 2.2.1 | Machines | One Step Ahead | [link](https://matrixonline.fandom.com/wiki/One_Step_Ahead_(Episode_2.2)) | [Mach2-2-1](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach2-2-1) |
| 2.2.2 | Machines | Strange Bedfellows | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach2-2-2](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach2-2-2) |
| 2.2.3 | Machines | Reconnaissance | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach2-2-3](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach2-2-3) |
| 2.2.4 | Machines | Catching Up | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach2-2-4](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach2-2-4) |
| 2.2.5 | Machines | Down the Wire | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach2-2-5](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach2-2-5) |
| 2.2.1 | Merovingian | With Friends Like These | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv2-2-1](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv2-2-1) |
| 2.2.2 | Merovingian | Honor Among Thieves | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv2-2-2](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv2-2-2) |
| 2.2.3 | Merovingian | Burning Both Ends | [link](https://matrixonline.fandom.com/wiki/Burning_Both_Ends_(Episode_2.2)) | [Merv2-2-3](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv2-2-3) |
| 2.2.4 | Merovingian | The Lighter Side of Slaughter | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv2-2-4](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv2-2-4) |
| 2.2.5 | Merovingian | Perfect Enemy | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv2-2-5](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv2-2-5) |

### 2.3 Whoever "needs us"

| ID | Org | Mission (trailing ? = unsure) | Source | Screenshot log |
|---|---|---|---|---|
| 2.3.1 | Zion | Your info or Your RSI | [link](https://matrixonline.fandom.com/wiki/Episode_2.3:_Whoever_%22needs_us%22) | [Zion2-3-1](https://github.com/hdneo/mxo-missions/tree/main/Zion%20Criticals/Zion2-3-1) |
| 2.3.2 | Zion | Party Crash | [link](https://matrixonline.fandom.com/wiki/Episode_2.3:_Whoever_%22needs_us%22) | [Zion2-3-2](https://github.com/hdneo/mxo-missions/tree/main/Zion%20Criticals/Zion2-3-2) |
| 2.3.3 | Zion | Incommunicado | [link](https://matrixonline.fandom.com/wiki/Episode_2.3:_Whoever_%22needs_us%22) | [Zion2-3-3](https://github.com/hdneo/mxo-missions/tree/main/Zion%20Criticals/Zion2-3-3) |
| 2.3.4 | Zion | Free Your Mind | [link](https://matrixonline.fandom.com/wiki/Episode_2.3:_Whoever_%22needs_us%22) | [Zion2-3-4](https://github.com/hdneo/mxo-missions/tree/main/Zion%20Criticals/Zion2-3-4) |
| 2.3.5 | Zion | The Heights | [link](https://matrixonline.fandom.com/wiki/Episode_2.3:_Whoever_%22needs_us%22) | [Zion2-3-5](https://github.com/hdneo/mxo-missions/tree/main/Zion%20Criticals/Zion2-3-5) |
| 2.3.1 | Machines | Recruit This | [link](https://matrixonline.fandom.com/wiki/Episode_2.3:_Whoever_%22needs_us%22) | [Mach2-3-1](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach2-3-1) |
| 2.3.2 | Machines | Rule of Thumb | [link](https://matrixonline.fandom.com/wiki/Episode_2.3:_Whoever_%22needs_us%22) | [Mach2-3-2](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach2-3-2) |
| 2.3.3 | Machines | Damage Control | [link](https://matrixonline.fandom.com/wiki/Damage_Control_(Episode_2.3)) | [Mach2-3-3](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach2-3-3) |
| 2.3.4 | Machines | Investigations | [link](https://matrixonline.fandom.com/wiki/Episode_2.3:_Whoever_%22needs_us%22) | [Mach2-3-4](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach2-3-4) |
| 2.3.5 | Machines | Misdirections | [link](https://matrixonline.fandom.com/wiki/Episode_2.3:_Whoever_%22needs_us%22) | [Mach2-3-5](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach2-3-5) |
| 2.3.1 | Merovingian | Enter Desayd | [link](https://matrixonline.fandom.com/wiki/Enter_Desayd_(Episode_2.3)) | [Merv2-3-1](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv2-3-1) |
| 2.3.2 | Merovingian | Dissension | [link](https://matrixonline.fandom.com/wiki/Dissension_(Episode_2.3)) | [Merv2-3-2](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv2-3-2) |
| 2.3.3 | Merovingian | Military Action | [link](https://matrixonline.fandom.com/wiki/Episode_2.3:_Whoever_%22needs_us%22) | [Merv2-3-3](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv2-3-3) |
| 2.3.4 | Merovingian | A Little Research | [link](https://matrixonline.fandom.com/wiki/Episode_2.3:_Whoever_%22needs_us%22) | [Merv2-3-4](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv2-3-4) |
| 2.3.5 | Merovingian | Killing Blow | [link](https://matrixonline.fandom.com/wiki/Episode_2.3:_Whoever_%22needs_us%22) | [Merv2-3-5](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv2-3-5) |

### 3.1 Morpheus Avenged

| ID | Org | Mission (trailing ? = unsure) | Source | Screenshot log |
|---|---|---|---|---|
| 3.1.1 | Zion | Back Door | [link](https://matrixonline.fandom.com/wiki/Back_Door_(Episode_3.1)) | [Zion3-1-1](https://github.com/hdneo/mxo-missions/tree/main/Zion%20Criticals/Zion3-1-1) |
| 3.1.2 | Zion | Dire Connection | [link](https://matrixonline.fandom.com/wiki/Dire_Connection_(Episode_3.1)) | [Zion3-1-2](https://github.com/hdneo/mxo-missions/tree/main/Zion%20Criticals/Zion3-1-2) |
| 3.1.3 | Zion | Cause and Effect | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Zion3-1-3](https://github.com/hdneo/mxo-missions/tree/main/Zion%20Criticals/Zion3-1-3) |
| 3.1.4 | Zion | A Feast for Wolves | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Zion3-1-4](https://github.com/hdneo/mxo-missions/tree/main/Zion%20Criticals/Zion3-1-4) |
| 3.1.5 | Zion | Meltdown | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Zion3-1-5](https://github.com/hdneo/mxo-missions/tree/main/Zion%20Criticals/Zion3-1-5) |
| 3.1.1 | Machines | Whats in the Box | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach3-1-1](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach3-1-1) |
| 3.1.2 | Machines | Operation Myrmidon | [link](https://matrixonline.fandom.com/wiki/Operation_Myrmidon_(Episode_3.1)) | [Mach3-1-2](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach3-1-2) |
| 3.1.3 | Machines | Decapitation | [link](https://matrixonline.fandom.com/wiki/Decapitation_(Episode_3.1)) | [Mach3-1-3](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach3-1-3) |
| 3.1.4 | Machines | Defuse the Situation | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach3-1-4](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach3-1-4) |
| 3.1.5 | Machines | Final Reckoning | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach3-1-5](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach3-1-5) |
| 3.1.1 | Merovingian | To the Dogs | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv3-1-1](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv3-1-1) |
| 3.1.2 | Merovingian | Pack Management | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv3-1-2](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv3-1-2) |
| 3.1.3 | Merovingian | The Worm Turns | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv3-1-3](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv3-1-3) |
| 3.1.4 | Merovingian | Bon Voyage | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv3-1-4](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv3-1-4) |
| 3.1.5 | Merovingian | The Gatekeeper | [link](https://matrixonline.fandom.com/wiki/The_Gatekeeper_(Episode_3.1)) | [Merv3-1-5](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv3-1-5) |

### 3.2 Propoganda [sic]

| ID | Org | Mission (trailing ? = unsure) | Source | Screenshot log |
|---|---|---|---|---|
| 3.2.1 | Zion | Strange Rumours | [link](https://matrixonline.fandom.com/wiki/Episode_3.2:_Propoganda) | [Zion3-2-1](https://github.com/hdneo/mxo-missions/tree/main/Zion%20Criticals/Zion3-2-1) |
| 3.2.2 | Zion | Opening Boxes | [link](https://matrixonline.fandom.com/wiki/Episode_3.2:_Propoganda) | [Zion3-2-2](https://github.com/hdneo/mxo-missions/tree/main/Zion%20Criticals/Zion3-2-2) |
| 3.2.3 | Zion | Swallow Your Pride | [link](https://matrixonline.fandom.com/wiki/Swallow_Your_Pride_(Episode_3.2)) | [Zion3-2-3](https://github.com/hdneo/mxo-missions/tree/main/Zion%20Criticals/Zion3-2-3) |
| 3.2.4 | Zion | Run the Fox to Ground | [link](https://matrixonline.fandom.com/wiki/Episode_3.2:_Propoganda) | [Zion3-2-4](https://github.com/hdneo/mxo-missions/tree/main/Zion%20Criticals/Zion3-2-4) |
| 3.2.5 | Zion | Urgent News | [link](https://matrixonline.fandom.com/wiki/Urgent_News_(Episode_3.2)) | [Zion3-2-5](https://github.com/hdneo/mxo-missions/tree/main/Zion%20Criticals/Zion3-2-5) |
| 3.2.1 | Machines | Always Watching... | [link](https://matrixonline.fandom.com/wiki/Episode_3.2:_Propoganda) | [Mach3-2-1](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach3-2-1) |
| 3.2.2 | Machines | A Veiled Communique | [link](https://matrixonline.fandom.com/wiki/Episode_3.2:_Propoganda) | [Mach3-2-2](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach3-2-2) |
| 3.2.3 | Machines | Rising Tensions | [link](https://matrixonline.fandom.com/wiki/Episode_3.2:_Propoganda) | [Mach3-2-3](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach3-2-3) |
| 3.2.4 | Machines | Behind The Curtain | [link](https://matrixonline.fandom.com/wiki/Behind_The_Curtain_(Episode:3.2)) | [Mach3-2-4](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach3-2-4) |
| 3.2.5 | Machines | Denial | [link](https://matrixonline.fandom.com/wiki/Episode_3.2:_Propoganda) | [Mach3-2-5](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach3-2-5) |
| ? | Merovingian | Seek the Veil? | [link](https://matrixonline.fandom.com/wiki/Episode_3.2:_Propoganda) |  |
| ? | Merovingian | Temptation | [link](https://matrixonline.fandom.com/wiki/Temptation) |  |

### 3.3 Guns, lots of guns

| ID | Org | Mission (trailing ? = unsure) | Source | Screenshot log |
|---|---|---|---|---|
| 3.3.1 | Zion | House Call | [link](https://matrixonline.fandom.com/wiki/Episode_3.3:_Guns,_lots_of_guns) | [Zion3-3-1](https://github.com/hdneo/mxo-missions/tree/main/Zion%20Criticals/Zion3-3-1) |
| 3.3.2 | Zion | Like Candy | [link](https://matrixonline.fandom.com/wiki/Episode_3.3:_Guns,_lots_of_guns) | [Zion3-3-2](https://github.com/hdneo/mxo-missions/tree/main/Zion%20Criticals/Zion3-3-2) |
| 3.3.3 | Zion | Trauma | [link](https://matrixonline.fandom.com/wiki/Trauma_(Episode_3.3)) | [Zion3-3-3](https://github.com/hdneo/mxo-missions/tree/main/Zion%20Criticals/Zion3-3-3) |
| 3.3.4 | Zion | Strange Epiphany | [link](https://matrixonline.fandom.com/wiki/Episode_3.3:_Guns,_lots_of_guns) | [Zion3-3-4](https://github.com/hdneo/mxo-missions/tree/main/Zion%20Criticals/Zion3-3-4) |
| 3.3.5 | Zion | Simulating the Simulation | [link](https://matrixonline.fandom.com/wiki/Episode_3.3:_Guns,_lots_of_guns) | [Zion3-3-5](https://github.com/hdneo/mxo-missions/tree/main/Zion%20Criticals/Zion3-3-5) |
| 3.3.1 | Machines | The Missing Case | [link](https://matrixonline.fandom.com/wiki/Episode_3.3:_Guns,_lots_of_guns) | [Mach3-3-1](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach3-3-1) |
| 3.3.2 | Machines | Pound the Pavement | [link](https://matrixonline.fandom.com/wiki/Episode_3.3:_Guns,_lots_of_guns) | [Mach3-3-2](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach3-3-2) |
| 3.3.3 | Machines | Death and Demons | [link](https://matrixonline.fandom.com/wiki/Episode_3.3:_Guns,_lots_of_guns) | [Mach3-3-3](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach3-3-3) |
| 3.3.4 | Machines | Revelations | [link](https://matrixonline.fandom.com/wiki/Episode_3.3:_Guns,_lots_of_guns) | [Mach3-3-4](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach3-3-4) |
| 3.3.5 | Machines | Miscommunication | [link](https://matrixonline.fandom.com/wiki/Episode_3.3:_Guns,_lots_of_guns) | [Mach3-3-5](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach3-3-5) |
| ? | Merovingian | The General Situation | [link](https://matrixonline.fandom.com/wiki/The_General_Situation_(Episode_3.3)) |  |
| ? | Merovingian | Stay Focused? | [link](https://matrixonline.fandom.com/wiki/Episode_3.3:_Guns,_lots_of_guns) |  |

### 4.1 I think we can do business

| ID | Org | Mission (trailing ? = unsure) | Source | Screenshot log |
|---|---|---|---|---|
| 4.1.1 | Zion | Demonstration of Force | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Zion4-1-1](https://github.com/hdneo/mxo-missions/tree/main/Zion%20Criticals/Zion4-1-1) |
| 4.1.2 | Zion | Scanning the Enemy | [link](https://matrixonline.fandom.com/wiki/Scanning_the_Enemy_(Episode_4.1)) | [Zion4-1-2](https://github.com/hdneo/mxo-missions/tree/main/Zion%20Criticals/Zion4-1-2) |
| 4.1.3 | Zion | Firepower | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Zion4-1-3](https://github.com/hdneo/mxo-missions/tree/main/Zion%20Criticals/Zion4-1-3) |
| 4.1.4 | Zion | Agent of Change | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Zion4-1-4](https://github.com/hdneo/mxo-missions/tree/main/Zion%20Criticals/Zion4-1-4) |
| 4.1.5 | Zion | Combat Run | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Zion4-1-5](https://github.com/hdneo/mxo-missions/tree/main/Zion%20Criticals/Zion4-1-5) |
| 4.1.1 | Machines | Laying the Groundwork | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach4-1-1](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach4-1-1) |
| 4.1.2 | Machines | Laying the Trap | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach4-1-2](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach4-1-2) |
| 4.1.3 | Machines | Tightening the Circle | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach4-1-3](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach4-1-3) |
| 4.1.4 | Machines | Springing the Jaws | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach4-1-4](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach4-1-4) |
| 4.1.5 | Machines | Interrogation | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach4-1-5](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach4-1-5) |
| 4.1.1 | Merovingian | Cover All Your Bases | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv4-1-1](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv4-1-1) |
| 4.1.2 | Merovingian | String Pulling | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv4-1-2](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv4-1-2) |
| 4.1.3 | Merovingian | Nothing Ever Goes As Planned | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv4-1-3](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv4-1-3) |
| 4.1.4 | Merovingian | The Gathering Storm | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv4-1-4](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv4-1-4) |
| 4.1.5 | Merovingian | A Chance for Redemption | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv4-1-5](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv4-1-5) |

### 4.2 From many, one. E Pluribus Neo

| ID | Org | Mission (trailing ? = unsure) | Source | Screenshot log |
|---|---|---|---|---|
| ? | Zion | The Kid | [link](https://matrixonline.fandom.com/wiki/The_Kid_(Episode_4.2)) |  |
| ? | Zion | Groundwork | [link](https://matrixonline.fandom.com/wiki/Groundwork_(Episode_4.2)) |  |
| ? | Machines | Distractions | [link](https://matrixonline.fandom.com/wiki/Distractions_(Episode_4.2)) |  |
| ? | Merovingian | Relocation Program | [link](https://matrixonline.fandom.com/wiki/Episode_4.2:_From_many,_one._E_Pluribus_Neo) |  |
| ? | Merovingian | A Little R & D | [link](https://matrixonline.fandom.com/wiki/Episode_4.2:_From_many,_one._E_Pluribus_Neo) |  |

### 4.3 

| ID | Org | Mission (trailing ? = unsure) | Source | Screenshot log |
|---|---|---|---|---|
| 4.3.1 | Zion | The Van | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Zion4-3-1](https://github.com/hdneo/mxo-missions/tree/main/Zion%20Criticals/Zion4-3-1) |
| 4.3.2 | Zion | The Project | [link](https://matrixonline.fandom.com/wiki/The_Project_(Episode_4.3)) | [Zion4-3-2](https://github.com/hdneo/mxo-missions/tree/main/Zion%20Criticals/Zion4-3-2) |
| 4.3.3 | Zion | Shakedown | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Zion4-3-3](https://github.com/hdneo/mxo-missions/tree/main/Zion%20Criticals/Zion4-3-3) |
| 4.3.4 | Zion | Disrupted Connection | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Zion4-3-4](https://github.com/hdneo/mxo-missions/tree/main/Zion%20Criticals/Zion4-3-4) |
| 4.3.5 | Zion | Hot Potato | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Zion4-3-5](https://github.com/hdneo/mxo-missions/tree/main/Zion%20Criticals/Zion4-3-5) |
| 4.3.1 | Machines | Cross-Analysis | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach4-3-1](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach4-3-1) |
| 4.3.2 | Machines | Bells and Bullets | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach4-3-2](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach4-3-2) |
| 4.3.3 | Machines | The Most Likely Path | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach4-3-3](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach4-3-3) |
| 4.3.4 | Machines | Cracking the Egg | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach4-3-4](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach4-3-4) |
| 4.3.5 | Machines | Praeparet Bellum | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach4-3-5](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach4-3-5) |
| 4.3.1 | Merovingian | For Every Lock... | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv4-3-1](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv4-3-1) |
| 4.3.2 | Merovingian | There Is A Key | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv4-3-2](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv4-3-2) |
| 4.3.3 | Merovingian | Highly Polished | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv4-3-3](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv4-3-3) |
| 4.3.4 | Merovingian | Weigh the Options | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv4-3-4](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv4-3-4) |
| 4.3.5 | Merovingian | Repercussions | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv4-3-5](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv4-3-5) |

### 5.1 Saving Captain Niobe (Part 1)

| ID | Org | Mission (trailing ? = unsure) | Source | Screenshot log |
|---|---|---|---|---|
| 5.1.1 | Zion | Perplexity | [link](https://matrixonline.fandom.com/wiki/Episode_5.1:_Saving_Captain_Niobe_(Part_1)) | [Zion5-1-1](https://github.com/hdneo/mxo-missions/tree/main/Zion%20Criticals/Zion5-1-1) |
| 5.1.2 | Zion | Desperate Measures | [link](https://matrixonline.fandom.com/wiki/Episode_5.1:_Saving_Captain_Niobe_(Part_1)) | [Zion5-1-2](https://github.com/hdneo/mxo-missions/tree/main/Zion%20Criticals/Zion5-1-2) |
| 5.1.3 | Zion | Flood's Errand | [link](https://matrixonline.fandom.com/wiki/Episode_5.1:_Saving_Captain_Niobe_(Part_1)) | not in repo |
| 5.1.4 | Zion | Deal with the Devil | [link](https://matrixonline.fandom.com/wiki/Episode_5.1:_Saving_Captain_Niobe_(Part_1)) | not in repo |
| 5.1.5 | Zion | Buying Time | [link](https://matrixonline.fandom.com/wiki/Episode_5.1:_Saving_Captain_Niobe_(Part_1)) | not in repo |
| 5.1.1 | Machines | Mysteries Revealed | [link](https://matrixonline.fandom.com/wiki/Mysteries_Revealed_(Episode_5.1)) | [Mach5-1-1](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach5-1-1) |
| 5.1.2 | Machines | Following the Trail | [link](https://matrixonline.fandom.com/wiki/Episode_5.1:_Saving_Captain_Niobe_(Part_1)) | [Mach5-1-2](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach5-1-2) |
| 5.1.3 | Machines | Smoke Signals | [link](https://matrixonline.fandom.com/wiki/Episode_5.1:_Saving_Captain_Niobe_(Part_1)) | [Mach5-1-3](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach5-1-3) |
| 5.1.4 | Machines | No Stone Unturned | [link](https://matrixonline.fandom.com/wiki/Episode_5.1:_Saving_Captain_Niobe_(Part_1)) | [Mach5-1-4](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach5-1-4) |
| 5.1.5 | Machines | Emergency Response | [link](https://matrixonline.fandom.com/wiki/Episode_5.1:_Saving_Captain_Niobe_(Part_1)) | [Mach5-1-5](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach5-1-5) |
| 5.1.1 | Merovingian | Poor Winners | [link](https://matrixonline.fandom.com/wiki/Episode_5.1:_Saving_Captain_Niobe_(Part_1)) | [Merv5-1-1](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv5-1-1) |
| 5.1.2 | Merovingian | Those Less Fortunate | [link](https://matrixonline.fandom.com/wiki/Episode_5.1:_Saving_Captain_Niobe_(Part_1)) | [Merv5-1-2](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv5-1-2) |
| 5.1.3 | Merovingian | The Device | [link](https://matrixonline.fandom.com/wiki/Episode_5.1:_Saving_Captain_Niobe_(Part_1)) | [Merv5-1-3](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv5-1-3) |
| 5.1.4 | Merovingian | Irresistible | [link](https://matrixonline.fandom.com/wiki/Episode_5.1:_Saving_Captain_Niobe_(Part_1)) | [Merv5-1-4](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv5-1-4) |
| 5.1.5 | Merovingian | Into the Crosshairs | [link](https://matrixonline.fandom.com/wiki/Episode_5.1:_Saving_Captain_Niobe_(Part_1)) | [Merv5-1-5](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv5-1-5) |

### 5.2 Saving Captain Niobe Part 2

| ID | Org | Mission (trailing ? = unsure) | Source | Screenshot log |
|---|---|---|---|---|
| 5.2.1 | Zion | Network Trouble | [link](https://matrixonline.fandom.com/wiki/Chapters) | not in repo |
| 5.2.2 | Zion | Know Your Enemy | [link](https://matrixonline.fandom.com/wiki/Know_Your_Enemy_(Episode_5.2)) | not in repo |
| 5.2.3 | Zion | Repairman | [link](https://matrixonline.fandom.com/wiki/Repairman_(Episode_5.2)) | not in repo |
| 5.2.4 | Zion | Double Betrayal | [link](https://matrixonline.fandom.com/wiki/Chapters) | not in repo |
| 5.2.5 | Zion | Safe and Sound | [link](https://matrixonline.fandom.com/wiki/Chapters) | not in repo |
| 5.2.1 | Machines | Deviation | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach5-2-1](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach5-2-1) |
| 5.2.2 | Machines | Genev | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach5-2-2](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach5-2-2) |
| 5.2.3 | Machines | Corrupt Readings | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach5-2-3](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach5-2-3) |
| 5.2.4 | Machines | Weit | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach5-2-4](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach5-2-4) |
| 5.2.5 | Machines | Free Radicals | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach5-2-5](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach5-2-5) |
| 5.2.1 | Merovingian | Spoofed Location Data | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv5-2-1](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv5-2-1) |
| 5.2.2 | Merovingian | Viral | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv5-2-2](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv5-2-2) |
| 5.2.3 | Merovingian | Insertion? | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv5-2-3](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv5-2-3) |
| 5.2.4 | Merovingian | Point to Point? | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv5-2-4](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv5-2-4) |
| 5.2.5 | Merovingian | The Vial | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv5-2-5](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv5-2-5) |

### 5.3 Anome's Unlimit

| ID | Org | Mission (trailing ? = unsure) | Source | Screenshot log |
|---|---|---|---|---|
| 5.3.1 | Zion | Warehouse C | [link](https://matrixonline.fandom.com/wiki/Episode_5.3:_%22Anome's_Unlimit%22) | not in repo |
| 5.3.2 | Zion | Death by Tastee Wheat | [link](https://matrixonline.fandom.com/wiki/Episode_5.3:_%22Anome's_Unlimit%22) | not in repo |
| 5.3.3 | Zion | Sweet Tastee Wheat | [link](https://matrixonline.fandom.com/wiki/Episode_5.3:_%22Anome's_Unlimit%22) | not in repo |
| 5.3.4 | Zion | Aqua Gulp | [link](https://matrixonline.fandom.com/wiki/Episode_5.3:_%22Anome's_Unlimit%22) | not in repo |
| 5.3.5 | Zion | Paging Ms. Utley | [link](https://matrixonline.fandom.com/wiki/Paging_Ms._Utley_(Episode_5.3)) | not in repo |
| 5.3.1 | Machines | Return to Sender | [link](https://matrixonline.fandom.com/wiki/Episode_5.3:_%22Anome's_Unlimit%22) | [Mach5-3-1](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach5-3-1) |
| 5.3.2 | Machines | Obsession | [link](https://matrixonline.fandom.com/wiki/Episode_5.3:_%22Anome's_Unlimit%22) | [Mach5-3-2](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach5-3-2) |
| 5.3.3 | Machines | Blackout | [link](https://matrixonline.fandom.com/wiki/Blackout_(Episode_5.3)) | [Mach5-3-3](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach5-3-3) |
| 5.3.4 | Machines | Over the Edge | [link](https://matrixonline.fandom.com/wiki/Episode_5.3:_%22Anome's_Unlimit%22) | [Mach5-3-4](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach5-3-4) |
| 5.3.5 | Machines | The Quarry | [link](https://matrixonline.fandom.com/wiki/Episode_5.3:_%22Anome's_Unlimit%22) | [Mach5-3-5](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach5-3-5) |
| 5.3.1 | Merovingian | Side Effects May Include | [link](https://matrixonline.fandom.com/wiki/Episode_5.3:_%22Anome's_Unlimit%22) | [Merv5-3-1](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv5-3-1) |
| 5.3.2 | Merovingian | I Come in Peace | [link](https://matrixonline.fandom.com/wiki/Episode_5.3:_%22Anome's_Unlimit%22) | [Merv5-3-2](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv5-3-2) |
| 5.3.3 | Merovingian | Persuasion | [link](https://matrixonline.fandom.com/wiki/Episode_5.3:_%22Anome's_Unlimit%22) | [Merv5-3-3](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv5-3-3) |
| 5.3.4 | Merovingian | The Moth | [link](https://matrixonline.fandom.com/wiki/Episode_5.3:_%22Anome's_Unlimit%22) | [Merv5-3-4](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv5-3-4) |
| 5.3.5 | Merovingian | Precious Bodily Fluids | [link](https://matrixonline.fandom.com/wiki/Episode_5.3:_%22Anome's_Unlimit%22) | [Merv5-3-5](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv5-3-5) |

### 6.1 Four-armed Frenzy

| ID | Org | Mission (trailing ? = unsure) | Source | Screenshot log |
|---|---|---|---|---|
| ? | Zion | The Corporate Ladder | [link](https://matrixonline.fandom.com/wiki/Chapters) |  |
| ? | Zion | Corporate Takeover | [link](https://matrixonline.fandom.com/wiki/Chapters) |  |
| ? | Machines | Messages in the Void | [link](https://matrixonline.fandom.com/wiki/Chapters) |  |
| ? | Machines | The Price of Betrayal | [link](https://matrixonline.fandom.com/wiki/The_Price_of_Betrayal_(Episode_6.1)) |  |
| 6.1.1 | Merovingian | Miracle Drug | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv6-1-1](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv6-1-1) |
| 6.1.2 | Merovingian | Mixer | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv6-1-2](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv6-1-2) |
| 6.1.3 | Merovingian | Legwork | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv6-1-3](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv6-1-3) |
| 6.1.4 | Merovingian | All the King's Men | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv6-1-4](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv6-1-4) |
| 6.1.5 | Merovingian | Catching Up | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv6-1-5](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv6-1-5) |

### 6.2 A Clever Ruse

| ID | Org | Mission (trailing ? = unsure) | Source | Screenshot log |
|---|---|---|---|---|
| 6.2.1 | Zion | Storage Details | [link](https://matrixonline.fandom.com/wiki/Episode_6.2:_%22A_Clever_Ruse) | not in repo |
| 6.2.2 | Zion | Subtext | [link](https://matrixonline.fandom.com/wiki/Subtext_(Episode_6.2)) | not in repo |
| 6.2.3 | Zion | For Every Man, Woman, and Child | [link](https://matrixonline.fandom.com/wiki/Episode_6.2:_%22A_Clever_Ruse) | not in repo |
| 6.2.4 | Zion | Baggage | [link](https://matrixonline.fandom.com/wiki/Episode_6.2:_%22A_Clever_Ruse) | not in repo |
| 6.2.5 | Zion | Cold Sheets | [link](https://matrixonline.fandom.com/wiki/Episode_6.2:_%22A_Clever_Ruse) | not in repo |
| 6.2.1 | Machines | A Simple Request | [link](https://matrixonline.fandom.com/wiki/Episode_6.2:_%22A_Clever_Ruse) | [Mach6-2-1](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach6-2-1) |
| 6.2.2 | Machines | Counter-Demand | [link](https://matrixonline.fandom.com/wiki/Episode_6.2:_%22A_Clever_Ruse) | [Mach6-2-2](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach6-2-2) |
| 6.2.3 | Machines | Whispers of Ones and Zeroes | [link](https://matrixonline.fandom.com/wiki/Episode_6.2:_%22A_Clever_Ruse) | [Mach6-2-3](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach6-2-3) |
| 6.2.4 | Machines | Code Pulse | [link](https://matrixonline.fandom.com/wiki/Episode_6.2:_%22A_Clever_Ruse) | [Mach6-2-4](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach6-2-4) |
| 6.2.5 | Machines | Authority | [link](https://matrixonline.fandom.com/wiki/Episode_6.2:_%22A_Clever_Ruse) | [Mach6-2-5](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach6-2-5) |
| ? | Merovingian | Silver Tongue | [link](https://matrixonline.fandom.com/wiki/Episode_6.2:_%22A_Clever_Ruse) |  |
| ? | Merovingian | Invention | [link](https://matrixonline.fandom.com/wiki/Episode_6.2:_%22A_Clever_Ruse) |  |
| ? | Merovingian | Red and Yellow | [link](https://matrixonline.fandom.com/wiki/Episode_6.2:_%22A_Clever_Ruse) |  |
| ? | Merovingian | Pound of Flesh | [link](https://matrixonline.fandom.com/wiki/Episode_6.2:_%22A_Clever_Ruse) |  |

### 6.3 A Piece of Blue Sky

| ID | Org | Mission (trailing ? = unsure) | Source | Screenshot log |
|---|---|---|---|---|
| 6.3.1 | Zion | Boots on the Carpet | [link](https://matrixonline.fandom.com/wiki/Chapters) | not in repo |
| 6.3.2 | Zion | Whirlybird | [link](https://matrixonline.fandom.com/wiki/Whirlybird_(Episode_6.3)) | not in repo |
| 6.3.3 | Zion | Bogeyman | [link](https://matrixonline.fandom.com/wiki/Chapters) | not in repo |
| 6.3.4 | Zion | Steely Embrace | [link](https://matrixonline.fandom.com/wiki/Chapters) | not in repo |
| 6.3.5 | Zion | Red Eye | [link](https://matrixonline.fandom.com/wiki/Chapters) | not in repo |
| 6.3.1 | Machines | Remote Control | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach6-3-1](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach6-3-1) |
| 6.3.2 | Machines | Outside the System | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach6-3-2](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach6-3-2) |
| 6.3.3 | Machines | Unauthorized Access | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach6-3-3](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach6-3-3) |
| 6.3.4 | Machines | Access Denied | [link](https://matrixonline.fandom.com/wiki/Access_Denied_(Episode_6.3)) | [Mach6-3-4](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach6-3-4) |
| 6.3.5 | Machines | Strata | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach6-3-5](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach6-3-5) |
| 6.3.1 | Merovingian | Fallen Angel | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv6-3-1](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv6-3-1) |
| 6.3.2 | Merovingian | They Can Only Be Given | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv6-3-2](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv6-3-2) |
| 6.3.3 | Merovingian | Good Manners | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv6-3-3](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv6-3-3) |
| 6.3.4 | Merovingian | Beret | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv6-3-4](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv6-3-4) |
| 6.3.5 | Merovingian | Love Note | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv6-3-5](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv6-3-5) |

### 7.1 The Search

| ID | Org | Mission (trailing ? = unsure) | Source | Screenshot log |
|---|---|---|---|---|
| 7.1.1 | Zion | Leave of his Senses | [link](https://matrixonline.fandom.com/wiki/Chapters) | not in repo |
| 7.1.2 | Zion | Amnesia | [link](https://matrixonline.fandom.com/wiki/Chapters) | not in repo |
| 7.1.3 | Zion | Trauma | [link](https://matrixonline.fandom.com/wiki/Chapters) | not in repo |
| 7.1.4 | Zion | Lucidity | [link](https://matrixonline.fandom.com/wiki/Lucidity_(Episode_7.1)) | not in repo |
| 7.1.5 | Zion | Beacon | [link](https://matrixonline.fandom.com/wiki/Chapters) | not in repo |
| 7.1.1 | Machines | Projection | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach7-1-1](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach7-1-1) |
| 7.1.2 | Machines | Secured Channels | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach7-1-2](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach7-1-2) |
| 7.1.3 | Machines | Detection | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach7-1-3](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach7-1-3) |
| 7.1.4 | Machines | Veils | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach7-1-4](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach7-1-4) |
| 7.1.5 | Machines | Transit | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach7-1-5](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach7-1-5) |
| 7.1.1 | Merovingian | Logistics | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv7-1-1](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv7-1-1) |
| 7.1.2 | Merovingian | Lab Work | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv7-1-2](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv7-1-2) |
| 7.1.3 | Merovingian | Dockside | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv7-1-3](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv7-1-3) |
| 7.1.4 | Merovingian | Coven | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv7-1-4](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv7-1-4) |
| 7.1.5 | Merovingian | Charon | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv7-1-5](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv7-1-5) |

### 7.2 Desperation

| ID | Org | Mission (trailing ? = unsure) | Source | Screenshot log |
|---|---|---|---|---|
| 7.2.1 | Zion | Feeling the Way | [link](https://matrixonline.fandom.com/wiki/Chapters) | not in repo |
| 7.2.2 | Zion | Hear Me | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Zion7-2-2](https://github.com/hdneo/mxo-missions/tree/main/Zion%20Criticals/Zion7-2-2) |
| 7.2.3 | Zion | Seeing Red | [link](https://matrixonline.fandom.com/wiki/Chapters) | not in repo |
| 7.2.4 | Zion | Inner Child | [link](https://matrixonline.fandom.com/wiki/Chapters) | not in repo |
| 7.2.5 | Zion | They Grow Up So Fast? | [link](https://matrixonline.fandom.com/wiki/Chapters) | not in repo |
| 7.2.1 | Machines | Get to the Choppers | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach7-2-1](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach7-2-1) |
| 7.2.2 | Machines | If You Are Unable to Speak | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach7-2-2](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach7-2-2) |
| 7.2.3 | Machines | An Intuitive Program | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach7-2-3](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach7-2-3) |
| 7.2.4 | Machines | Death in Many Forms | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach7-2-4](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach7-2-4) |
| 7.2.5 | Machines | Down the Wire? | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach7-2-5](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach7-2-5) |
| 7.2.1 | Merovingian | Favors | [link](https://matrixonline.fandom.com/wiki/Favors_(Episode_7.2)) | [Merv7-2-1](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv7-2-1) |
| 7.2.2 | Merovingian | Something Wicked | [link](https://matrixonline.fandom.com/wiki/Something_Wicked_(Episode_7.2)) | [Merv7-2-2](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv7-2-2) |
| 7.2.3 | Merovingian | Boil, Bubble | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv7-2-3](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv7-2-3) |
| 7.2.4 | Merovingian | Hell-Broth | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv7-2-4](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv7-2-4) |
| 7.2.5 | Merovingian | Pernicious Hour | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv7-2-5](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv7-2-5) |

### 7.3 

| ID | Org | Mission (trailing ? = unsure) | Source | Screenshot log |
|---|---|---|---|---|
| 7.3.1 | Zion | To Fade Away | [link](https://matrixonline.fandom.com/wiki/Chapters) | not in repo |
| 7.3.2 | Zion | Dead Systems | [link](https://matrixonline.fandom.com/wiki/Chapters) | not in repo |
| 7.3.3 | Zion | Check Your Comms | [link](https://matrixonline.fandom.com/wiki/Chapters) | not in repo |
| 7.3.4 | Zion | She Insists | [link](https://matrixonline.fandom.com/wiki/Chapters) | not in repo |
| 7.3.5 | Zion | Et Sapientiae Tuae Non Est Numerus | [link](https://matrixonline.fandom.com/wiki/Chapters) | not in repo |
| 7.3.1 | Machines | A Key with No Lock | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach7-3-1](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach7-3-1) |
| 7.3.2 | Machines | Subways | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach7-3-2](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach7-3-2) |
| 7.3.3 | Machines | What Is Humanity | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach7-3-3](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach7-3-3) |
| 7.3.4 | Machines | Misinformation | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach7-3-4](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach7-3-4) |
| 7.3.5 | Machines | Aliqua Portio Creaturae Tuae | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach7-3-5](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach7-3-5) |
| 7.3.1 | Merovingian | Furlough | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv7-3-1](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv7-3-1) |
| 7.3.2 | Merovingian | Timetable | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv7-3-2](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv7-3-2) |
| 7.3.3 | Merovingian | Inside Job | [link](https://matrixonline.fandom.com/wiki/Inside_Job_(Episode_7.3)) | [Merv7-3-3](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv7-3-3) |
| 7.3.4 | Merovingian | Transfer | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv7-3-4](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv7-3-4) |
| 7.3.5 | Merovingian | Quia Superbis Resistis | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv7-3-5](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv7-3-5) |

### 8.1 War

| ID | Org | Mission (trailing ? = unsure) | Source | Screenshot log |
|---|---|---|---|---|
| 8.1.1 | Zion | War | [link](https://matrixonline.fandom.com/wiki/War_(Zion)) | not in repo |
| 8.1.2 | Zion | Firewall | [link](https://matrixonline.fandom.com/wiki/Chapters) | not in repo |
| 8.1.3 | Zion | Uplink | [link](https://matrixonline.fandom.com/wiki/Chapters) | not in repo |
| 8.1.4 | Zion | Broadcast | [link](https://matrixonline.fandom.com/wiki/Chapters) | not in repo |
| 8.1.5 | Zion | Depth | [link](https://matrixonline.fandom.com/wiki/Chapters) | not in repo |
| 8.1.1 | Machines | War | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach8-1-1](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach8-1-1) |
| 8.1.2 | Machines | Vulnerability | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach8-1-2](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach8-1-2) |
| 8.1.3 | Machines | Spiral | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach8-1-3](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach8-1-3) |
| 8.1.4 | Machines | Potential | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach8-1-4](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach8-1-4) |
| 8.1.5 | Machines | Extraction | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach8-1-5](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach8-1-5) |
| 8.1.1 | Merovingian | War | [link](https://matrixonline.fandom.com/wiki/War_(Merovingian)) | [Merv8-1-1](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv8-1-1) |
| 8.1.2 | Merovingian | Sea of Serenity | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv8-1-2](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv8-1-2) |
| 8.1.3 | Merovingian | The Lesser Evil | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv8-1-3](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv8-1-3) |
| 8.1.4 | Merovingian | Eyes | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv8-1-4](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv8-1-4) |
| 8.1.5 | Merovingian | Battery | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv8-1-5](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv8-1-5) |

### 8.2 

| ID | Org | Mission (trailing ? = unsure) | Source | Screenshot log |
|---|---|---|---|---|
| 8.2.1 | Zion | Danielle | [link](https://matrixonline.fandom.com/wiki/Chapters) | not in repo |
| 8.2.2 | Zion | Prototype | [link](https://matrixonline.fandom.com/wiki/Chapters) | not in repo |
| 8.2.3 | Zion | A Free Berth | [link](https://matrixonline.fandom.com/wiki/Chapters) | not in repo |
| 8.2.4 | Zion | Distribution | [link](https://matrixonline.fandom.com/wiki/Chapters) | not in repo |
| 8.2.5 | Zion | Kamikaze | [link](https://matrixonline.fandom.com/wiki/Chapters) | not in repo |
| 8.2.1 | Machines | Process of Elimination | [link](https://matrixonline.fandom.com/wiki/Process_of_Elimination_(Episode_8.2)) | [Mach8-2-1](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach8-2-1) |
| 8.2.2 | Machines | War of Attrition | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach8-2-2](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach8-2-2) |
| 8.2.3 | Machines | Caplet | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach8-2-3](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach8-2-3) |
| 8.2.4 | Machines | Scramble | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach8-2-4](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach8-2-4) |
| 8.2.5 | Machines | Execute | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach8-2-5](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach8-2-5) |
| 8.2.1 | Merovingian | The Fields | [link](https://matrixonline.fandom.com/wiki/The_Fields_(Episode_8.2)) | [Merv8-2-1](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv8-2-1) |
| 8.2.2 | Merovingian | Hotline | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv8-2-2](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv8-2-2) |
| 8.2.3 | Merovingian | The Price | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv8-2-3](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv8-2-3) |
| 8.2.4 | Merovingian | The Pods | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv8-2-4](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv8-2-4) |
| 8.2.5 | Merovingian | Long Distance | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv8-2-5](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv8-2-5) |

### 8.3 

| ID | Org | Mission (trailing ? = unsure) | Source | Screenshot log |
|---|---|---|---|---|
| 8.3.1 | Zion | Contact Lost | [link](https://matrixonline.fandom.com/wiki/Chapters) | not in repo |
| 8.3.2 | Zion | Old Ties | [link](https://matrixonline.fandom.com/wiki/Chapters) | not in repo |
| 8.3.3 | Zion | Samples | [link](https://matrixonline.fandom.com/wiki/Chapters) | not in repo |
| 8.3.4 | Zion | Evacuation | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Zion8-3-4](https://github.com/hdneo/mxo-missions/tree/main/Zion%20Criticals/Zion8-3-4) |
| 8.3.5 | Zion | Life and Death | [link](https://matrixonline.fandom.com/wiki/Chapters) | not in repo |
| 8.3.1 | Machines | Dream Compilation | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach8-3-1](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach8-3-1) |
| 8.3.2 | Machines | Of Two Minds | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach8-3-2](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach8-3-2) |
| 8.3.3 | Machines | The Hook | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach8-3-3](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach8-3-3) |
| 8.3.4 | Machines | The Hunt | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach8-3-4](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach8-3-4) |
| 8.3.5 | Machines | Terminate | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach8-3-5](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach8-3-5) |
| 8.3.1 | Merovingian | Among the Missing | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv8-3-1](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv8-3-1) |
| 8.3.2 | Merovingian | Humble Beginnings | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv8-3-2](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv8-3-2) |
| 8.3.3 | Merovingian | The Disputed Death of Mary MacHenry | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv8-3-3](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv8-3-3) |
| 8.3.4 | Merovingian | Seance | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv8-3-4](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv8-3-4) |
| 8.3.5 | Merovingian | Unplugged | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv8-3-5](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv8-3-5) |

### 9.1 Intrusion

| ID | Org | Mission (trailing ? = unsure) | Source | Screenshot log |
|---|---|---|---|---|
| 9.1.1 | Zion | The Accelerated Man | [link](https://matrixonline.fandom.com/wiki/Chapters) | not in repo |
| 9.1.2 | Zion | Incompatability | [link](https://matrixonline.fandom.com/wiki/Chapters) | not in repo |
| 9.1.3 | Zion | Phreak | [link](https://matrixonline.fandom.com/wiki/Phreak_(Episode_9.1)) | not in repo |
| 9.1.4 | Zion | Questions | [link](https://matrixonline.fandom.com/wiki/Chapters) | not in repo |
| 9.1.5 | Zion | Ignorance | [link](https://matrixonline.fandom.com/wiki/Chapters) | not in repo |
| 9.1.1 | Machines | Intruder | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach9-1-1](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach9-1-1) |
| 9.1.2 | Machines | Override | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach9-1-2](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach9-1-2) |
| 9.1.3 | Machines | Best Interests | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach9-1-3](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach9-1-3) |
| 9.1.4 | Machines | Denial of Service | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach9-1-4](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach9-1-4) |
| 9.1.5 | Machines | Only Human | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach9-1-5](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach9-1-5) |
| 9.1.1 | Merovingian | Apparition | [link](https://matrixonline.fandom.com/wiki/Apparition_(Episode_9.1)) | [Merv9-1-1](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv9-1-1) |
| 9.1.2 | Merovingian | Induction | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv9-1-2](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv9-1-2) |
| 9.1.3 | Merovingian | Maneuvers | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv9-1-3](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv9-1-3) |
| 9.1.4 | Merovingian | High Life | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv9-1-4](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv9-1-4) |
| 9.1.5 | Merovingian | Misanthropy | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv9-1-5](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv9-1-5) |

### 9.2 

| ID | Org | Mission (trailing ? = unsure) | Source | Screenshot log |
|---|---|---|---|---|
| ? | Zion | Snoop 1313 | [link](https://matrixonline.fandom.com/wiki/Chapters) |  |
| ? | Zion | Trigger | [link](https://matrixonline.fandom.com/wiki/Chapters) |  |
| ? | Zion | Muzzle | [link](https://matrixonline.fandom.com/wiki/Chapters) |  |
| ? | Zion | Building 27 | [link](https://matrixonline.fandom.com/wiki/Chapters) |  |
| 9.2.1 | Machines | Slowdown | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach9-2-1](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach9-2-1) |
| 9.2.2 | Machines | Puncture | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach9-2-2](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach9-2-2) |
| 9.2.3 | Machines | Overload | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach9-2-3](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach9-2-3) |
| 9.2.4 | Machines | Killer | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach9-2-4](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach9-2-4) |
| 9.2.5 | Machines | Confession | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach9-2-5](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach9-2-5) |
| 9.2.1 | Merovingian | Muckraker | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv9-2-1](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv9-2-1) |
| 9.2.2 | Merovingian | Down to a Sunless Sea | [link](https://matrixonline.fandom.com/wiki/Down_to_a_Sunless_Sea_(Episode_9.2)) | [Merv9-2-2](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv9-2-2) |
| 9.2.3 | Merovingian | Parvati | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv9-2-3](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv9-2-3) |
| 9.2.4 | Merovingian | Heart's Desire | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv9-2-4](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv9-2-4) |
| 9.2.5 | Merovingian | Behold a Pale Horse | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv9-2-5](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv9-2-5) |

### 9.3 

| ID | Org | Mission (trailing ? = unsure) | Source | Screenshot log |
|---|---|---|---|---|
| 9.3.1 | Zion | Mistrust | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Zion9-3-1](https://github.com/hdneo/mxo-missions/tree/main/Zion%20Criticals/Zion9-3-1) |
| 9.3.2 | Zion | Senseless | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Zion9-3-2](https://github.com/hdneo/mxo-missions/tree/main/Zion%20Criticals/Zion9-3-2) |
| 9.3.3 | Zion | Namuci | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Zion9-3-3](https://github.com/hdneo/mxo-missions/tree/main/Zion%20Criticals/Zion9-3-3) |
| 9.3.4 | Zion | Redline | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Zion9-3-4](https://github.com/hdneo/mxo-missions/tree/main/Zion%20Criticals/Zion9-3-4) |
| 9.3.5 | Zion | Unsaid | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Zion9-3-5](https://github.com/hdneo/mxo-missions/tree/main/Zion%20Criticals/Zion9-3-5) |
| ? | Machines | Cooperation | [link](https://matrixonline.fandom.com/wiki/Chapters) |  |
| ? | Machines | Fabrication | [link](https://matrixonline.fandom.com/wiki/Chapters) |  |
| ? | Machines | Giving Up | [link](https://matrixonline.fandom.com/wiki/Chapters) |  |
| ? | Machines | Gotama | [link](https://matrixonline.fandom.com/wiki/Chapters) |  |
| ? | Machines | The Sheeted Dead | [link](https://matrixonline.fandom.com/wiki/Chapters) |  |
| ? | Machines | Acceptable Levels | [link](https://matrixonline.fandom.com/wiki/Chapters) |  |
| ? | Merovingian | Duality | [link](https://matrixonline.fandom.com/wiki/Chapters) |  |
| ? | Merovingian | Why She Swallowed the Fly | [link](https://matrixonline.fandom.com/wiki/Chapters) |  |
| ? | Merovingian | World of Hungry Shades | [link](https://matrixonline.fandom.com/wiki/Chapters) |  |
| ? | Merovingian | A Man of One Dimension | [link](https://matrixonline.fandom.com/wiki/Chapters) |  |
| ? | Merovingian | Less Obvious | [link](https://matrixonline.fandom.com/wiki/Chapters) |  |
| ? | Merovingian | Signs of the Apocalypse | [link](https://matrixonline.fandom.com/wiki/Chapters) |  |

### 10.1 Oligarchy

| ID | Org | Mission (trailing ? = unsure) | Source | Screenshot log |
|---|---|---|---|---|
| ? | Zion | Unfulfilled | [link](https://matrixonline.fandom.com/wiki/Unfulfilled_(Episode_10.1)) |  |
| ? | Zion | Dead Man at the Wheel | [link](https://matrixonline.fandom.com/wiki/Chapters) |  |
| ? | Zion | Unibody Ping | [link](https://matrixonline.fandom.com/wiki/Chapters) |  |
| ? | Zion | Don't Let the Door Hit You | [link](https://matrixonline.fandom.com/wiki/Chapters) |  |
| 10.1.1 | Machines | Persistence | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach10-1-1](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach10-1-1) |
| 10.1.2 | Machines | Jurisdiction | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach10-1-2](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach10-1-2) |
| 10.1.3 | Machines | Licence | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach10-1-3](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach10-1-3) |
| 10.1.4 | Machines | Cut Both Ways | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach10-1-4](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach10-1-4) |
| 10.1.5 | Machines | End of the Abstract | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach10-1-5](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach10-1-5) |

### 10.2 

| ID | Org | Mission (trailing ? = unsure) | Source | Screenshot log |
|---|---|---|---|---|
| ? | Zion | Cache | [link](https://matrixonline.fandom.com/wiki/Chapters) |  |
| ? | Zion | Splice | [link](https://matrixonline.fandom.com/wiki/Chapters) |  |
| ? | Zion | Blip | [link](https://matrixonline.fandom.com/wiki/Chapters) |  |
| ? | Machines | Situation Normal | [link](https://matrixonline.fandom.com/wiki/Chapters) |  |
| 10.2.1 | Merovingian | Handshake | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv10-2-1](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv10-2-1) |
| 10.2.2 | Merovingian | The Rain Fell in Torrents | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv10-2-2](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv10-2-2) |
| 10.2.3 | Merovingian | Incoming | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv10-2-3](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv10-2-3) |
| 10.2.4 | Merovingian | Smoke | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv10-2-4](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv10-2-4) |
| 10.2.5 | Merovingian | Packet Rider | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv10-2-5](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv10-2-5) |

### 10.3 

| ID | Org | Mission (trailing ? = unsure) | Source | Screenshot log |
|---|---|---|---|---|
| 10.3.1 | Zion | Thou Madest Man | [link](https://matrixonline.fandom.com/wiki/Chapters) | not in repo |
| 10.3.2 | Zion | They Know We Know | [link](https://matrixonline.fandom.com/wiki/Chapters) | not in repo |
| 10.3.3 | Zion | The Personal Touch | [link](https://matrixonline.fandom.com/wiki/Chapters) | not in repo |
| 10.3.4 | Zion | Body Modification | [link](https://matrixonline.fandom.com/wiki/Chapters) | not in repo |
| 10.3.5 | Zion | The Perfidy of Space and Time | [link](https://matrixonline.fandom.com/wiki/Chapters) | not in repo |
| 10.3.1 | Machines | Cities and Storms | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach10-3-1](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach10-3-1) |
| 10.3.2 | Machines | Help Desk | [link](https://matrixonline.fandom.com/wiki/Help_Desk_(Episode_10.3)) | [Mach10-3-2](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach10-3-2) |
| 10.3.3 | Machines | Alarm | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach10-3-3](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach10-3-3) |
| 10.3.4 | Machines | Served | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach10-3-4](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach10-3-4) |
| 10.3.5 | Machines | Out of Touch | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Mach10-3-5](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach10-3-5) |
| 10.3.1 | Merovingian | Office Party | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv10-3-1](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv10-3-1) |
| 10.3.2 | Merovingian | A Plethora of Passwords | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv10-3-2](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv10-3-2) |
| 10.3.3 | Merovingian | Burnt Toast | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv10-3-3](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv10-3-3) |
| 10.3.4 | Merovingian | The Chasm Between Two Contradictions | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv10-3-4](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv10-3-4) |
| 10.3.5 | Merovingian | Theophany | [link](https://matrixonline.fandom.com/wiki/Chapters) | [Merv10-3-5](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv10-3-5) |

### 11.2 Wright

| ID | Org | Mission (trailing ? = unsure) | Source | Screenshot log |
|---|---|---|---|---|
| ? | Zion | Out of the Ordinary | [link](https://matrixonline.fandom.com/wiki/Chapters) |  |
| ? | Zion | The Simulation | [link](https://matrixonline.fandom.com/wiki/Chapters) |  |
| ? | Zion | Mara Ring 149 101 | [link](https://matrixonline.fandom.com/wiki/Chapters) |  |
| ? | Machines | A Mistake | [link](https://matrixonline.fandom.com/wiki/Chapters) |  |
| ? | Merovingian | The Chateau | [link](https://matrixonline.fandom.com/wiki/Chapters) |  |
| ? | Merovingian | The Useful Man | [link](https://matrixonline.fandom.com/wiki/Chapters) |  |
| ? | Merovingian | Clipped | [link](https://matrixonline.fandom.com/wiki/Chapters) |  |
| ? | Merovingian | The Apartment | [link](https://soeforums.mxoemu.info/mxo/posts/list1828.html?topic_id=36300026043) |  |

### 11.3 Overrides

| ID | Org | Mission (trailing ? = unsure) | Source | Screenshot log |
|---|---|---|---|---|
| ? | Zion | Promises | [link](https://soeforums.mxoemu.info/mxo/posts/list71e6.html?topic_id=36300026386) |  |
| ? | Zion | Resistance | [link](https://soeforums.mxoemu.info/mxo/posts/list71e6.html?topic_id=36300026386) |  |
| 11.3.1 | Machines | Dead Suits | [link](https://matrixonline.fandom.com/wiki/Dead_Suits_(Episode_11.3)) | [Mach11-3-1](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach11-3-1) |
| 11.3.2 | Machines | Measure and Countermeasure | [link](https://soeforums.mxoemu.info/mxo/posts/list71e6.html?topic_id=36300026386) | [Mach11-3-2](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach11-3-2) |
| 11.3.3 | Machines | Severance | [link](https://matrixonline.fandom.com/wiki/Unnamed_Machine_Mission_3_(Episode_11.3)) | [Mach11-3-3](https://github.com/hdneo/mxo-missions/tree/main/Machine%20Criticals/Mach11-3-3) |
| 11.3.1 | Merovingian | Mismanagement | [link](https://soeforums.mxoemu.info/mxo/posts/list71e6.html?topic_id=36300026386) | [Merv11-3-1](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv11-3-1) |
| 11.3.2 | Merovingian | The Mechanical Man | [link](https://soeforums.mxoemu.info/mxo/posts/list71e6.html?topic_id=36300026386) | [Merv11-3-2](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv11-3-2) |
| 11.3.3 | Merovingian | Empty Vessel | [link](https://soeforums.mxoemu.info/mxo/posts/list71e6.html?topic_id=36300026386) | [Merv11-3-3](https://github.com/hdneo/mxo-missions/tree/main/Merovingian%20Criticals/Merv11-3-3) |

### 12.1 

| ID | Org | Mission (trailing ? = unsure) | Source | Screenshot log |
|---|---|---|---|---|
| 12.1.1 | Zion | Self Image | [link](https://soeforums.mxoemu.info/mxo/posts/list1797.html?topic_id=36300027006) | not in repo |
| 12.1.2 | Zion | Angel | [link](https://matrixonline.fandom.com/wiki/Angel_(Episode_12.1)) | not in repo |
| 12.1.3 | Zion | Impersonal | [link](https://matrixonline.fandom.com/wiki/Impersonal_(Episode_12.1)) | not in repo |
| ? | Machines | Representative | [link](https://matrixonline.fandom.com/wiki/Representative_(Episode_12.1)) |  |
| ? | Merovingian | Hothouse | [link](https://soeforums.mxoemu.info/mxo/posts/list1797.html?topic_id=36300027006) |  |
| ? | Merovingian | Deep Cover | [link](https://soeforums.mxoemu.info/mxo/posts/list1797.html?topic_id=36300027006) |  |