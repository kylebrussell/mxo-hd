# Combat actions and archive transport

This extends the audit in [CLIENT-ACTIONS.md](CLIENT-ACTIONS.md). The request layouts below were read from the shipped 7.6005 `client.dll`; the rules use the shipped GOB and `research/client-scripts`. Headless checks exercise native wire layouts against disposable databases. They do not establish native presentation parity.

## Requests

| Request | Layout after opcode | Implemented behavior | Limits |
|---|---|---|---|
| `3e` | slot u8, ability u32, IsSlot u8, IgnoreStyleUpdate u8 | Validates ownership/loadout, retains combat selection and style, updates own attribute 39 | Selection does not cast a special move. Style-specific stats and animation sets remain incomplete. |
| `43` | MartialArt u16 | Retains the opaque selection separately | **Partial:** no producer, value mapping or server response found in the available captures. No invented mapping to a style ability. |
| `80 bb` | ability u16 | Removes the caller's removable beneficial parent buff and linked timed effects; emits native off records | UBF0 category and CanNOTRemoveBuff gate protect viruses, stuns, styles and Death Effect. |
| `81 21` | bag slot u8 | Deposits the expected mission item into the currently open nearby Put container; replies `81 22` | Generic container storage is not implemented. Corpse loot is read-only. |
| `81 2e` | SourceCode u32, array offset u16, tool slot u8; array count u16 + `(amount u32, goid u32)` | Validates owned compiler, loaded Compile grant, known recipe, exact ingredients, space, information and skill contest; consumes ingredients and grants result | Code Writer/Analyzer path, charges/purity, signal boosts and XP remain incomplete. |
| `81 31` | ability u32, source bag slot u8, tool slot u8, recipe-known hint u8 | Decompile276 validates owned source, loaded ability, learnable recipe and contest; persists learning, removes source and sends `81 30`/`81 2f` | Fragment recovery, purity/stability contribution, other inventory-target abilities and exact tool semantics remain incomplete. Recipe-known hint is never trusted as authority. |

`81 21` previously opened the marketplace. Marketplace open remains `81 29`. Put now opens an empty native container window; the drop request, rather than opening the window, consumes the mission item. Wrong item, replay, missing window, stale ownership and excessive distance are refused.

Binary evidence:

- `3e`: constructor `6219e500`, printer `6219fd10`, setter `625dd5f0`. Ability `1000` and slot `6b` mean no change. Slots are stored as `80000000|slot`, including the producer which leaves IsSlot zero.
- `43`: constructor `6219e890`, printer `6219fea0`; two payload bytes.
- `80 bb`: constructor `6205e190`, sender `6205e290`; `62517780` negates CanNOTRemoveBuff; `62515ef0` checks UBF0, whereas `62515f30` checks UDB0.
- `81 21/22`: drop handler `620a045b`, reply `6214176c/621549a0`. Reply fields are ObjectID u32, InstanceData u32, Status u32.
- `81 2e`: constructor `62087ae0`, array setter `62087c80`. Offset includes the two opcode bytes. `6208c13d..c169` decodes SourceCode as ability GOID `80000000|(source&003ffc00)` or item `(source>>7)&7ffff`.
- `81 31`: constructor `621d3b00`, producer `621ed223`. Its first field is a base **ability ID**, not an item recipe GOID.
- `81 2f`: constructor `6214225d`, UI `6208cad8`: result u8, slot u8, TypeID u32, XP float. `81 30` constructor `621423dc`: ObjectID u32, Complexity u8, Category u32.
- Compile cost multiplier `.12`: `6269fd01..fd70`, immediate `3df5c28f`. `crafting_server.py` supplies cost discount, Gaussian roll (mean50, deviation16, clamp0..100), strict `4*(skill+specialization/10)+roll > 4*complexity+50`. Missing stored complexity is refused.

`data/crafting_rules.json` exports 42,804 stored item rule rows. `research/crafting/extract_rules.py` reproduces this and `data/pvp_archives.json` directly from the client packmap. Missing GOB fields remain null. `char_recipes` persists learned recipes and the margin includes them on subsequent logins. The historical baseline recipe list is preserved.

## Shared PvP interlock

The combat init now accepts slots1..6 and their receiver-local fighter handles. A free player can join a target's existing interlock, choose another legal opponent in that interlock, withdraw individually and rejoin. A seventh fighter is refused with reason6. Joining two already populated interlocks is not supported. Duels remain two-person fights.

Fighters and observers receive the same mapping and round-entry slots. Observer promotion reuses its existing combat view; observers without a view of a new fighter are removed until visibility is sufficient. A departing fighter removes only its slot from survivors. A pending death teardown is not recreated as an observer view. Whole-fight teardown removes all occupied slots. Native group choreography still needs checking.

Round scheduling is server policy: each fighter gets at most one attack in a round, with reciprocal opponents resolved together. A player defending against several attackers receives each exchange, without gaining an extra counterattack. Each RPC45 names that exchange's actual opponent. Timed callbacks cannot overlap. Existing shared control gates suppress stunned/paralyzed hits and ability use; gunfire stun suppression has dedicated headless coverage.

## Death Effect

PlayerCharacter's stored AbilityInv/DeathPenalty default is **1.0**. Client string `c40000c7` describes a 15% reduction in ability levels, fading over five minutes. `tempmods.DeathEffectUpdate` adds3 per update. The implementation loses .15 on an ordinary death and recovers .15 over300s, with six-second presentation updates. Additional losses accumulate; that stacking model is an inference consistent with the .7 factor in ordinary death captures.

`char_death_penalty` stores the factor and UTC time so recovery continues through disconnects and restarts. Own attribute38 and protected ability202 modifier records present recovery. Duels preserve an existing penalty and add no loss (inference consistent with the duel captures). Death and duel buff cleanup do not remove the penalty.

**Mechanical limit:** effective learned levels currently feed crafting skill calculations. Existing fixed-value damage/buff implementations do not yet reevaluate every effect at the reduced ability level. Death Effect presentation and persistence are implemented; complete combat scaling remains partial.

## Archive books and transporters

GOB Ticket fields identify 24 reusable archive books, their organization and inclusive level gates. Archivist CheckInventorySlotID is106; books must be equipped there. Having a book in the bag does **not** set the PvP flag. `PvPIfInInventory` is a separate tempmod and is not evidence that all these books should flag their holder.

| PvPID | Construct ID | Server map | Destination | Levels |
|---|---|---|---|---|
| 1 | 11 | 6 | Ashencourte | 20–30 |
| 2 | 12 | 8 | Sakura | 30–40 |
| 3 | 13 | 10 | Widow's Moor | 36–45 |
| 4 | 14 | 11 | Yuki | 15–25 |
| 5 | 15 | 4 | Zero One | 40–50 |
| 6 | 16 | 5 | One Zero | 26–35 |
| 7 | 17 | 7 | Datamine | 1–100 |
| 8 | 18 | 9 | Sati's Playground | 1–100 |

Names are from the books' STA Info strings. `rshooks.LookupCID` uses `ConstructIDBase + PvPID - 1`; `626a2824` sets base11. The archive01 retail world-load capture also carries construct15. Resource lookup, saved district and navmesh continue to use the server map index; world-load Atlas carries the construct ID.

City sender placements come from the actual static CSV rows:

- Zion38282: Richland `(111600,745,-6130)`, sector1205, mxo static bytes `9507504b`.
- Machines38278: `(120600,145,-114930)`, sector95, `ae00f005`.
- Merovingian38279: `(22600,145,-150130)`, sector577, `88021024`.

Their GOB radius is500 units. They admit only their stored organization with a matching equipped book and level. The sender cycle uses the GOB events/durations: Waiting c1f118s, Pregather c2145s, Gathering c1d11s, GoAway c202s, Preteleport c222s, Teleporting c1e2s. State changes are broadcast on actual bound sender views; eligible nearby players travel at Teleporting.

`rshooks.LookupConstructData` supplies six organizations' arrivals for the first six maps. Organization5 is PluribusNeo,6 Cypherites. Arrivals are snapped to verified navmesh floor. The exported scripts lack Datamine/Sati arrivals: these use a documented nearest-walkable-floor fallback near `(-500,2100,500)`, with only the caller's organization returner shown to avoid six overlapping generated objects. Their exact retail arrivals remain unknown.

Returners are generated at the script's organization arrivals. Generic object creation now retains the actual sent spawn counter in ClientView, allowing native handle validation; previously the counter stayed zero and valid returner interactions were refused. A forged stale handle is rejected. Use2 enforces `serverrules.TeleporterOrgCheck` (9007), proximity and alive/out-of-combat state. They return to the session's city origin; a reconnect with no origin uses Mara `(23828,495,13984)` as a safe server fallback. Returner placements and reconnect fallback need native QA. The city sender cycle is synchronized to UTC modulo180s (server policy).

The six older archives preserve the source sector PvP/safe-area flags. Datamine uses EveryoneButTeam mode5 (inferred from its book description permitting attacks on any operative), Sati uses mode0. Client and server receive/use the same per-world mode.

Archive nav data is generated with `python -I research/world-data/navmesh.py <mxo-research/tools/client-data> data/sector_properties.csv data/navmesh 4,5,6,7,8,9,10,11`. Archive polygon coordinates require version3 with i32 x/z; city version2/i16 files are unchanged. The loader accepts both. All48 organization/map arrivals have `NavMesh.Locate` coverage.

## Native checklist and current status

Native attempts A/B crashed before gameplay. Both minidumps fault at `client.dll621206bb`, inside the feature-list array string accessor called from `6217daa7/6217dac1`. PR96's literal `81 49` has an extra zero at offset16, making its count512 rather than2. PR99 isolates a 74-byte structured response with two bounded seven-byte rows and relative string offsets14/28. Its CI passes. Native retry remains pending: automatic approval review blocked both shell launch commands with “blocked by policy.” No native pass is claimed for this branch.

Use the NativeQa actions fixture, lock and CSR_Tester110 only. Check:

1. World entry with FeatureList enabled, then repeated `81 48` queries without a crash.
2. Switch a loaded martial style/special selection; inspect own selection, toolbar and ability behavior. Record any `43` producer/value evidence.
3. Dismiss Hyper-Dodge from its buff icon. Verify other buffs remain and hostile effects/styles cannot be dismissed.
4. Search a mission container, open a Put goal, drag its expected item into the window. Verify the container reply and goal completion; try a wrong item and a distant drop.
5. Open the compiler/Code Writer through the actual UI, compile a known fragment, decompile an owned item, relog to verify recipe learning. Capture requests and tool slots before expanding the incomplete branches.
6. Group interlock: three through six players, switch targets, observe from outside, individual withdrawal/rejoin and death. Verify other fighters keep their combat view; stun one fighter and verify no hits while it is active.
7. Ordinary death/recovery/relog, then duel with an existing penalty. Inspect tooltip, ability levels and recovery; note the combat-scaling limit above.
8. Equip an eligible archive book at the matching sender, watch the complete cycle, verify arrival, safe/PvP areas and organization returners. Test a wrong level/org/book and a relog in the archive.

Release the lock with CSR110 at Mara, full health, pvpflag0 and mission986 only. Do not use TheNeo or change firewall/Defender settings.


## Validation on 2026-11-11

- Final Debug and Release: 3,532/3,532 unit tests each; Release solution build passes.
- All48 map/organization arrivals locate walkable floor; book level/org gates and the complete sender state cycle have unit coverage.
- Native-wire crafting, owned buff cancellation, wrong/replayed container deposits, saved Death Effect across a fresh server, and six-player observer mapping/withdrawal/rejoin pass headlessly.
- Archive transport passes end to end (215.8s): matching equipped book, simultaneous underlevel refusal, construct11 with server map6, returner creation, forged stale handle refusal, valid return and reusable book preserved.
- Final complete70-case regression is in progress; the handoff file records its final result. Earlier complete run passed69 cases with the archive assertion failure; the subsequent corrected test exposed and fixed the generic spawn-counter bug described above.
- Native client A/B failed before gameplay; corrected native retry remains unverified. CSR110 was reset/verified at Mara, health250/250, flag0, mission986; NativeD stopped and released the lock at18:01ET.
