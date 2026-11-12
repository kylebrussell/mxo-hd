# Tyndall's Prima missions4-10

This slice is org1 contact Tyndall, mission IDs2304-2310, orders14-20: seven missions and 21 phases. It is distinct from the later archived critical missions named Zion 4.1,4.2 and so on. Existing XML attributes/transcribed story text are the Prima-guide-backed source; generated sites, role-name substitutions, item choices, actor representations and timing are OURS. Native completion/rendering remains unverified until Claude observes it.

## Candidate audit and queue

The initial audit found37 existing Zion4-10 candidates: these 7 Prima missions, plus 30 archived screenshot missions. The latter are 15 chapter 4 missions104101-104305, two chapter 5 missions105101/105102,107202,108304, ten chapter 9 missions109201-109305 and110205. They are separate chains and are not silently included in this completion claim. Next queue: archived4.1 IDs104101-104105, then4.2 and4.3. Later chapters have missing primary assets/definitions; missing chapter 6 content is not invented here. Full initial inventory is retained in Temp/mxo-zion-4-10-candidate-audit.json.

## Goals and authored ordering

| ID | Mission | Phases | Required UI actions |
|---|---|---:|---|
|2304|Moving Target|3|Gary Get; Linda Talk/Get; Paul Talk/Get plus filing-cabinet Open/Take All, then hardline Upload|
|2305|Protection|2|Recruit all three hostages by approaching; lead them out, then to Captain Taygeta; keep them alive|
|2306|Liberation of the Theses|2|Open/Take All from two distinct filing cabinets; Take data from the computer, then Upload|
|2307|Signal Loss|3|Cassini Talk/Get virus; defeat Zosma; Fabrizio Give virus/Get data; Upload|
|2308|Recon and Retrieval|3|Hadar Talk/box Open/Take All; defeat Adhara then Take computer data; lead both survivors out and to a hardline, Upload|
|2309|Buffer Overflow|3|Computer Take access code, mainframe Hack then Take; lead Pollux/Nunki out; Take from three separate computers and leave|
|2310|The Stolen Artifact|5|Castor Talk/escort; cabinet documents, escort/Upload; defeat all five Exiles/escort; safe artifact, defeat Ludovica/escort; Lalande Talk, escort Castor, Give artifact, defeat final hostile Castor|

2308 now gates the computer on defeatingAdhara, and its final upload on the survivors' building exit. The hardline escort and upload can then complete through the same ordinary Upload action.2310 gates the final Give on the escort, and the final kill/opponent arrival on that Give. These are explicit OURS pacing gates along the printed goal order, not proof of retail betrayal timing. The existing separate friendly/hostile Castor representation is retained; no unproven side-turn plot is substituted.

All goals still complete through ordinary object/NPC/hardline/combat events. Persistence and reward scaling are owned by separate M1/M3 work; this patch adds neither a save format nor a reward formula. Existing mission reward metadata is unchanged.

## Concrete softlock and narrow placement repair

The first actual-wire run completed2304's first two phases and Paul Whitley's Talk/Get, then found no filing cabinet for phase3. Server evidence: selected SlumApt Mara center35800,545,11075 and `[MISSION] Nowhere to put object1 (Furniture)`. The site passed the old combined container+generic-spawner count even though Furniture cannot use a generic object spawner.

MissionSites.Choose now accepts an optional required-furniture minimum, default0 for existing callers. MissionStaging computes that minimum with the exact PlaceObjects classifier: kinds absent from its spawnable ObjectGoids require real furniture. Preview and staging, both requested-style and Any fallback, pass the requirement. Total capacity remains required as before; dedicated furniture capacity is additionally required. No object kind, unsafe position or alternate asset is fabricated. The allocator already consumes spawners before furniture for spawned kinds, so total capacity plus the dedicated minimum protects either XML ordering. Unit cases cover the counterexample, both orders of generic+Furniture with 2 furniture/0 spawners and1+1, and multiple furniture-only kinds.

## Fixture controls and wire proof

Discovered mode `zion-chain` seeds only earlier completed missions 1201,1204,1203,1205-1208 and2301-2303. It does not grant any2304-2310 completion. Tyndall is a remote mission contact, so the fixture starts at the known Mara hardline rather than inventing a physical Tyndall NPC. It uses level 50,5000 initial HP and 2000 IS, standard owned combat/heal abilities, dev commands, no GM, no combat/callback RNG override and no health hold.

The sole new control is site/layout-only `MXO_MISSION_SITE_QA_SEED=230410`, accepted only with exact dev1 and a valid mxotest_ database. It seeds mission placement independently of combat RNG; ordinary servers keep their existing seed formulas. The mode and wire scenario use it. Existing `?weaken 1` lowers nearby NPC health only to shorten actual fights; normal hit rolls and ordinary combat packets still determine the kill. It also affects nearby friendly NPC health, so it is not an invulnerability control.

The full-chain wire scenario uses one character and only the ten prerequisite completion rows. NPC80c7 interactions, container Open/Take All, hardline Upload, ordinary close-combat rounds and existing elevator/movement helpers must earn all seven selected completion receipts. It never calls server Complete/Advance directly or grants a selected mission completion in SQL. Each action prints mission/phase/goal and the permanent run log includes the exit code. The first failed run/evidence is preserved; a missing failure file is never treated as a pass.

## Claude-only one-pass native checklist

After green CI on the exact pinned head, build Debug and start a new NativeQa directory with discovered mode `zion-chain`. Codex does not run native setup. Open Missions, selectTyndall, request her next story mission, open details and Accept 2304. Finish each row above in order; the next mission must be offered only after the preceding completion. No selected completion is pre-granted.

For each generated phase, follow the waypoint into its building and use the actual elevator door/panel. If using the existing QA shortcut, stand at the indicated street elevator and use `?missionfloor`; it still takes the existing elevator ride. Use actual NPC right-click/radial Talk/Get/Give, container Open and Take All, computer Take/Hack and hardline radial Upload. Recruit every named escort before leaving; confirm each follower reaches the street/destination. Preserve living hostages. For required fights, right-click > Close Combat, optionally use existing `?weaken 1` before the actual fight and let normal combat rounds kill the target; no combat roll override exists.

Check the two-cabinet and three-computer phases against their separate objective lines. In 2308, computer retrieval must wait for Adhara's defeat and upload must wait for the building escort. In 2310, verify all five wave enemies, the final Lalande talk/escort/handoff sequence, and the final hostile Castor's arrival only after handing over the artifact. The separate friendly/hostile Castor representation is an explicit authored limitation. Finish each mission through its normal ending and observe the summary/reward once; report actual observed mission IDs, any failed goal and remaining geometry/presentation limits. A wire pass alone does not prove native collision or UI acceptance.

## Recorded validation

The initial unseeded run failed at2304 phase3's missing Furniture placement after24.7s (exit1), preserving its server log and exact action trace. After the dedicated-capacity repair, the site-seeded full-chain run passed100.6s (exit0), earning all seven mission completions over21phases with zero protocol problems. Logs are Temp/mxo-zion-prima-chain-run-20261112.txt and Temp/mxo-zion-prima-chain-run-2-20261112.txt; the first failure artifact remains under Temp/mxo-zion-prima-chain-fresh-20261112. No source edits or builds occurred while either driver was active. Native QA remains pending.

The final driver resolves actual native usable numbers from ObjectStates: computer Take5/Hack4 and container Open3. It checks the computer's latest received CurrentState update (creation snapshots are immutable) before sending each action. This covers all seven computer Takes and the mainframe Hack, including Adhara's unlock and Hack-to-Take transition. The corrected full chain passed105.9s, exit0, in Temp/mxo-zion-native-uses-run-3-20261112.txt; its retained debug log is Temp/mxo-zion-native-uses-success-debuglog-20261112.txt.

Earlier corrected-driver attempts are preserved: run1 failed58.2s because the test inspected a stale creation state; run2 passed every native computer action but timed out87.2s waiting10s for2310 phase1's elevator after the operator's ride line. No callback exception or mission cancellation appeared in that log. A subsequent run with bounded elevator callback diagnostics passed the entire chain; this does not establish a root cause or a timeout fix. Claude should specifically check that first2310 elevator ride and report any stall. Logs remain Temp/mxo-zion-native-uses-run-20261112.txt and Temp/mxo-zion-native-uses-run-2-20261112.txt with their associated fresh failure directories.
