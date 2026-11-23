# Spy stealth

Sneak, Improved Sneak, Escape and Evasion, Concealment, Invisibility, Impart Invisibility, Vanish and Master Shadow run in `hds/world/Structures/Stealth.cs`. No retail capture shows a stealthed player, so everything here comes from `client.dll` 7.6005, the client's own server scripts (`research/client-scripts`) and the ability catalog. Each section below says which parts are evidence and which are our choice.

## How the state reaches the client (evidence)

- **DissemblingType** is a bit mask: own view attribute 36, other players' views attribute 27 (Object12). client.dll registers the Python constants at 0x62521e2f: `kSneak 0x02`, `kInvisible 0x04`, `kDisguised 0x08`, `kShadowObject 0x10`, `kDisruptedConnection 0x20`, `kBlur 0x40`, `kOverclock 0x80`, `kArmor 0x100`, `kAdminInvis 0x200`, `kSpeeding 0x400`, `kAdminDisguise 0x800`, `kHyperSpeed 0x1000`, `kHyperDodge 0x2000`, `kNeoRSI 0x4000`. kSpeeding and kHyperSpeed match the retail Hyper-Sprint (`04 00`) and Hyper-Speed (`00 10`) captures already used by `AbilityBuffs`.
- The client's CharMvt handler (cmpcharmvtclient.cpp, 0x625bcfa4) goes through the bits of whichever view changed. For kSneak and kInvisible it attaches `FX invis_self_loop`, and for kShadowObject `FX shadow_object_loop`. No local-player check guards these bits, so an observer who gets the attribute sees the same effect. When the bits are cleared it plays `invis_self_off`.
- **ConditionStateFlags**: the client's condition table (0x6269ef26) gives `ST_CONCEALMENT 0x00040000` and `ST_STEALTHED 0x02000000`. The same table has `ST_PVP 0x01000000` and `ST_SITTING_EFFECT 0x00200000`, which match retail captures. The catalog's StealthState (881) and Concealed (623) states set these bits.
- Records: each state sends its stored modifiers as `80 bc` records (stat kind 0x15, state kind 0x19, as for Evade Combat), with the toggle switch `80 bd 05 <id> ... 01` on and `... 00` off. Values come from `AbilityValues` (native functions 2 and 3):
  - Sneak: Stealth (209) = ability level, Noise modifier (509) +10, Movement Speed (115) -200, StealthState.
  - Escape and Evasion: Sneak Movement (877) +25, Stealth = character level, StealthState.
  - Concealment: Stealth = ability level, Concealed and StealthState.
  - Invisibility: Movement Speed -300 and StealthState, for 30 s + 2 s per level + active InvisDuration644 (`misc.py` InvisibilityGrant_DirectObject). The existing Shadow1117 class buff supplies +3; snapshot once, preserve explicit remaining-time overrides. Stat1051 from175 is movement speed, not duration. [Consumer proof and native visibility gate](ABILITY-INVISIBILITY-DURATION.md).
  - Imparted invisibility (629): kInvisible only, for 120 s.
  - Master Shadow: kShadowObject for 180 s, taking 25 IS every 5 s (ISDrainWithAbilityDeactivation). It ends when IS runs short, and its recast starts when it ends (AddRecastTimer).

## Improved Sneak (186)

The shipped catalog defines 186 as a two-second, out-of-combat exclusive toggle, with a rank-25 prerequisite on Sneak 151 and a mastery link to that same ability. The 186 template stores no own modifiers or callbacks and has an explicit DetectionDifficultyChange of zero. The stored 151 grant supplies the actual learned rank to 1111. The older Actions collector trace establishes the no-tool and self-cast eligibility fields. The group and the outbound ID from a mastery action still need a stock observation.

The server already uses ordinary Sneak's effects for a direct 186 request. That path now reads the loaded 1111 rank for its 209 record, applies the same Sneaking pose, and retains 186 on its own toggle and modifier records. It uses the existing ordinary distance, facing and sight contest with no additional difficulty bonus. Unloading the mastery or its prerequisite ends its active state and cancels an unfinished cast. An ordinary 1111 activation remains its own ability when both rows are loaded. These are the server's explicit mastery behavior and lifetime. The original engine's activation selection and effective mastery rank remain open.

Fixture `ability-parity-improved-sneak` seeds the real owned prerequisites at level 25, uses rank-25 Sneak and rank-1 Improved Sneak, and discovers the first stock 186 request in Actions or from the supplied page-2 shortcut. Its headless bootstrap replays only the previously captured Spy Style, ordinary Sneak and Countermeasures inputs. The helper starts outside the documented 13 m detection radius and approaches during an active state with the normal +80 awareness buff. A request that activates 1111 is ordinary Sneak evidence.

## Detection (evidence)

`abilitylib.sneakTest(SA, SL, NL, dist)` is native (client.dll 0x62513950, arguments are bytes):

- With a stealth level of 0, the player is always seen.
- Otherwise the player is seen when `max(0, dist/100 - 3) * NPCDetection_DistanceFactor - NL * NPCDetection_NoiseFactor + 2*SL + roll < 2*SA + 50`.
  - `roll = (rand()*100) >> 15`, which gives 0..99.
  - Both console variables default to 1.0 (registered at 0x628190b0).
  - The debug string reads "detect : distfac - noise + st + roll < SA +50".

`ability/utility.py` SneakTest runs this every 5 s (Sneak's UpdateFuncInterval):

- It checks every enemy within `300 + (2000 - 2000*min(level,50)/50)` units (level = the current Sneak grant level, read by GetSneakLevel) who faces the sneaker. Facing means `isObjWithinFrust`: ahead of them and less than 60 degrees off their facing.
- One success turns the ability off for everyone.
- InitialSneakTest refuses Sneak, Concealment and Escape and Evasion while such an enemy is watching.
- Invisibility is refused if an enemy within 800 units faces the player and is in sight. The callback skips enemies with the Blind temp mod. Vanish skips this test ("Vanish Prox Override").

SA is the player's StealthAwareness: the creation value of 1, plus Stealth Detection (204) totals, which come from Awakened's passive and Stealth Awareness. SL is the state's Stealth record plus loaded and active Stealth totals. Player and NPC awareness is bounded to the byte input range so negative debuffs cannot wrap into high awareness.

## Escape and Evasion (507)

The shipped startup loads `ability.spy.sneaks`. Its 507 implementation calls `activateSneak`, installs `SneakUpdate` and `EscapeAndEvasionTerm`, and turns off the auras named by `utility.turnOffAuras`. The deactivation function removes 507 and deactivates Sneak. The term function explicitly restores Standing, deactivates Sneak and turns off its parent. The catalog's health callback uses the existing damage break. [Activation, test and deactivation](../research/client-scripts/ability/spy/sneaks.py#L58) Â· [Term callback](../research/client-scripts/tempmods.py#L2302)

The authored 507 records are +25 Sneak Movement 877, character-level Stealth 209 and StealthState 881. The server projects the 877 movement contribution while this 507 state is active and restores the remaining scale on exit. The status category permits the existing ordinary dismiss request. The same 507 holder ends on a matching dismissal, a second activation, a reveal or damage below maximum HP. The initial and periodic detection rules use the loaded ordinary Sneak grant's range, with no additional difficulty adjustment from 507.

`ability-parity-escape-and-evasion` prepares the rank-23 ability and its legal prerequisites at character level 25. Normal Sneak rank 19 is the distinct movement and reveal control. The fixture's first 507 action and its Status dismissal are stock capture steps. The headless replay uses only the prior Spy Style, ordinary Sneak and Countermeasures stock requests. The current IS debit is the server's fallback because GOB omits the player cost field.

## What breaks it

- **Evidence:**
  - Damage taken while below maximum health ends Sneak, Escape and Evasion and Concealment (OnHealthReduceDeactivateAbility on their modifiers).
  - Invisibility has no health callback, so damage does not end it.
  - Expiry ends the timed states, and death clears every state.
  - Using the ability again switches it off (all of them are toggles).
- **Our choice:**
  - Any attack (close-combat request `40`, fire `41`) or any non-stealth ability ends the state.
  - The scripts never state this rule. The abilities are all `AbilityIsExclusive`, and `serverrules.py` BreakExclusiveAbility ends the exclusive ability on interactions, but the native combat interlock is not in the scripts.
  - Interactions (doors, containers, hardlines) do not yet call BreakExclusiveAbility / BreakShadowObject.

## Observers and NPCs (our choice, built on the evidence)

- The server sends DissemblingType to watchers. The other-view attribute exists, and the client draws the effect on any character carrying it. Friendly or non-hostile observers (team, unflagged players) keep the view and see the shimmer.
- A sneaking or invisible player is kept out of the view of anyone who could attack them (`PvpCombat.Check(observer, target) == Allowed`). The visibility pass deletes that view and recreates it when the state ends.
  - The scripts reveal a sneaker to everyone at once (SneakUpdate turns the ability off), so there is no per-observer partial detection to model.
  - Master Shadow (kShadowObject, evade state) keeps the view.
- NPCs ignore invisible and shadowed players.
  - A sneaker is tested once every 5 s against the NPCs that would aggro and face them. One success ends the sneak, and the NPC then attacks as usual.
  - Captured placement rows use the NPC_BASE StealthAwareness byte from column 22 (one-based); runtime stat204 modifiers also contribute. Synthesized NPCs with no captured byte retain the previous level fallback.
- Vanish: after 2 s (its modifier's Duration, VanishInit as term function) Invisibility goes on without the proximity test. Our choice: the current fight ends with it.

## Not done

- **Line of sight:** initial watching and periodic player/NPC detection now use a floor-mesh sight approximation. This blocks mapped gaps; it is not `physics.clearLine` collision parity. See [combat-depth evidence](COMBAT-DEPTH.md).
- **Noise:** the contest reads the current server-side NoiseLevel byte. Movement, ranged-noise decay and the mapping from stat509's modifier into that byte remain unrecovered. The fixture starts with zero noise.
- **Movement:** SneakInit/Term pair activateSneak with deactivateSneak. InvisibilityInit/Term use Stance_Invis and Stance_Stand. Ordinary Sneak and Belt activation now send stances 09 and 1a to the owner and visible observers. Ending either clears its owned stance. The stock PR728 diagnostic measured 599.4/1305.5 units for Sneak and 723.6/1301.4 for the Belt over matched forward runs. Only the stance changed: stat 115 stayed at -200 or -300. The game client switched itself to walk state 01. [Native stance measurements](https://github.com/kylebrussell/mxo-hd/pull/728#issuecomment-7073904435).
- **Vanish item:** Vanish's smoke-bomb requirement (`Inventory.hasItem(460)`, "SMOKE_BOMB_MISSING_ITEM") is not enforced.
- **Detection ability (298):** The active reveal requires a carried Concealment Countermeasure Tool (2388). The shipped component-10 prerequisite maps that tool to 298, and the existing native tool search checks inventory slots 0 through 106. The tool is reusable. Loading Detection still contributes its Awareness and Detect stats while the tool is absent. one random draw from 0–99 plus current Detect (638) must strictly exceed 50 + 8× the corresponding duration stat + 4× the loaded Disguise or Invisibility ability level. Successful Detection ends the actual disguise or invisibility state and plays `FX_WEAPON_CONCEALMENT_COUNTER_PLAYER`. Sneak and Master Shadow are separate states. The player cast uses the stored 12 m caster-centred enemy selector and the server's existing same-floor area/PvP policy, admitting hidden opponents without a view. Stock QA verified the radius boundary, ordinary Disguise, both forms of Invisibility and counter FX 2800059d. NPC Detection 627 is a separate authored ability. Exact retail collision geometry remains open.
- **Stealth attacks:** the script constants75-1200 now add damage when the cast began hidden, with the snapshot retained through the stealth break. The trigger/addition is explicit server policy because the decompiled tests are stubs; see [combat depth](COMBAT-DEPTH.md).
- **Presentation:** the periodic player's reveal strings still need verified string IDs. The existing Invisibility/Disguise activation proximity checks cover enemy players. Full NPC admission and the native exclusive-ability rules for interacting with world objects remain open.
- **Out of scope:** Hyper-Sense (1101), Spy Style (1112), Find Weakness (123) and Stealth Awareness's record (156) are numeric beneficial buffs, which belong to the scaled-buffs work. Stealth Awareness's total already feeds detection through `AbilityStats`.

## Dev commands (MXO_DEV_COMMANDS=1)

- `?stealth <sneak|improved|evasion|conceal|invis|imparted|shadow|off>` puts the state on yourself (own view; no refusal test).
- `?stealthbot <state> [pvp]` replies whether the bot is really your enemy (and why not) and summons a DuelBot 3 m away and puts the state on it 3 s later. With `pvp` the bot is PvP-flagged and of another organisation, so it is your enemy. `?stealthbot off` dismisses it.

### Native QA steps (one client)

1. Start the server with `MXO_DEV_COMMANDS=1` and log in a character above level 15.
2. `?stealthbot sneak`: DuelBot appears, and 3 s later it shimmers with the invisibility effect. It stays targetable and visible.
3. `?stealthbot invis`: same effect, through kInvisible.
4. `?stealthbot shadow`: shadow-object effect.
5. Your character needs an organisation: PvP rules (`PvpRules.IsOpponent`) make nobody an opponent of a character with organisation 0, so the flagged bot is not your enemy and stays visible. `?stealthbot sneak pvp` says so (and logs `[STEALTH] ?stealthbot pvp for ...: you have no organisation`); use `?org 1` (or 2, 3) first. Then `?stealthbot sneak pvp`: DuelBot appears, then vanishes from your screen when it goes stealthed. Keep your back to it: it stays gone. Turn to face it (the bot sneaks at level 5: SL 5, a 21 m range, about a 40% chance per 5 s pass against StealthAwareness 1): within a few passes it should reappear. The facing test uses the yaw the server last got from your client, which changes only when your own view update carries it (turning or moving the character, not just the camera). DuelBot may stand off-axis (the nav mesh can push it 48 yaw steps round, about 63 degrees, just outside the 60-degree frustum), so turn or step towards it. Every 5 s pass logs `[STEALTH] detect <you> vs DuelBot: <m> m, <deg> deg off facing (yaw <n>): out of range | not facing | roll <r> missed`, or the `sees` line, as the sneak ends for everyone. The server log shows `[STEALTH] ... sees DuelBot`.
6. `?stealth sneak` on yourself: check that your own character shows the invisibility effect, the Sneak icon is toggled on, and movement is slowed. `?health 300` (from full) should end it. `?stealth invis` and then any attack should end it.
7. `?stealthbot off`, then `?stealth off`.

## Low and high detection fixture

Use NativeQa mode `stealth-detection` on the disposable CSR110 profile. It loads ordinary Sneak151 at 28 and enables PvP. QAFriend has awareness 1. QAFar has awareness 28, from the same baseline plus loaded level-50 Awakened (+12) and Detection (+15). They face west from x=18400 and x=18900 on y=495/z=2300. The helper bootstrap uses the ordinary captured world-entry `05` and `0c 01` records documented in [Client actions](CLIENT-ACTIONS.md). It checks positions, loadouts, PvP and the phase geometry before publishing readiness.

1. Enter on CSR110 at (17000,495,2300). Use Sneak through Actions. Expect self translucency and the toggle on. If the ability is absent, record the Actions tree and stop the native check.
2. Walk east to about x=17300, keeping z around 2300. Wait until the log reports a QAFriend miss with SA 1 and SL 28. QAFar is outside the 11.8 m contest range at this stop.
3. Walk east to about x=17800. Allow up to ten five-second rolls for QAFar to succeed. Expect CSR to become visible and Sneak to toggle off. Keep CSR stationary during each wait.
4. Collect `stealth-detection-result.json`. The helper check requires the low miss before the high success and both deletion/recreation histories. Record the native self animation and toggle separately. A high success ends Sneak globally, so both helpers regain CSR after the reveal.

The active-Detection headless scenario uses the legacy gameplay harness request path to test the executor. A level-1 Detection cast cannot beat a level-25 Invisibility defense threshold of 150. A level-50 cast always beats it. The scenario checks removal, the recipient's counter FX and restored hostile views. This does not establish a native Detection request or its display. The native fixture exercises periodic Sneak only.
