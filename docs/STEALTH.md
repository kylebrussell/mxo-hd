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
  - Invisibility: Movement Speed -300 and StealthState, for 30 s + 2 s per level (`misc.py` INVISIBILITY_BASE_TIME).
  - Imparted invisibility (629): kInvisible only, for 120 s.
  - Master Shadow: kShadowObject for 180 s, taking 25 IS every 5 s (ISDrainWithAbilityDeactivation). It ends when IS runs short, and its recast starts when it ends (AddRecastTimer).

## Detection (evidence)

`abilitylib.sneakTest(SA, SL, NL, dist)` is native (client.dll 0x62513950, arguments are bytes):

- With a stealth level of 0, the player is always seen.
- Otherwise the player is seen when `max(0, dist/100 - 3) * NPCDetection_DistanceFactor - NL * NPCDetection_NoiseFactor + 2*SL + roll < 2*SA + 50`.
  - `roll = (rand()*100) >> 15`, which gives 0..99.
  - Both console variables default to 1.0 (registered at 0x628190b0).
  - The debug string reads "detect : distfac - noise + st + roll < SA +50".

`ability/utility.py` SneakTest runs this every 5 s (Sneak's UpdateFuncInterval):

- It checks every enemy within `300 + (2000 - 2000*min(level,50)/50)` units (level = the state's ability level) who faces the sneaker. Facing means `isObjWithinFrust`: ahead of them and less than 60 degrees off their facing.
- One success turns the ability off for everyone.
- InitialSneakTest refuses Sneak, Concealment and Escape and Evasion while such an enemy is watching.
- Invisibility is refused if an enemy within 800 units faces the player. Vanish skips this test ("Vanish Prox Override").

SA is the player's StealthAwareness: the creation value of 1, plus Stealth Detection (204) totals, which come from Awakened's passive and Stealth Awareness. SL is the state's Stealth record plus any passive Stealth.

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
  - Our choice: an NPC's StealthAwareness is its level. Retail creation records carried per-NPC values we can't map.
- Vanish: after 2 s (its modifier's Duration, VanishInit as term function) Invisibility goes on without the proximity test. Our choice: the current fight ends with it.

## Not done

- **Line of sight:** `physics.clearLine` is not checked.
- **Noise:** the noise level is passed as 0. Sneak's +10 Noise modifier feeds the native CharMvt.NoiseLevel, whose computation is not decoded.
- **Movement:** sneak and invisible movement speeds rely on the client applying the stat records. Stance_Sneak / Stance_Invis animations (activateSneak, ConcealUpdate) are not sent. Concealment's "panic end" when not exclusive and its stand-still behaviour are missing.
- **Vanish item:** Vanish's smoke-bomb requirement (`Inventory.hasItem(460)`, "SMOKE_BOMB_MISSING_ITEM") is not enforced.
- **Detection ability:** the Detection ability (`general.py` Detection_DirectObject: random(100) + level against 50 + 4/8 per level, breaking invisibility or disguise) is not wired.
- **Stealth attacks:** the sneak-attack damage constants in `ability/spy/sneaks.py` (75-1200) are defined but no decompiled function reads them. SneakAttackDetection is native, so stealth openers have no stealth bonus here.
- **Disguise (1114):** missing. It needs the tool's ModelOverride and setModel, and kDisguised alone would only fake it.
- **Out of scope:** Hyper-Sense (1101), Spy Style (1112), Find Weakness (123) and Stealth Awareness's record (156) are numeric beneficial buffs, which belong to the scaled-buffs work. Stealth Awareness's total already feeds detection through `AbilityStats`.

## Dev commands (MXO_DEV_COMMANDS=1)

- `?stealth <sneak|improved|evasion|conceal|invis|imparted|shadow|off>` puts the state on yourself (own view; no refusal test).
- `?stealthbot <state> [pvp]` replies whether the bot is really your enemy (and why not) and summons a DuelBot 3 m away and puts the state on it 3 s later. With `pvp` the bot is PvP-flagged and of another organisation, so it is your enemy. `?stealthbot off` dismisses it.

### Native QA steps (one client)

1. Start the server with `MXO_DEV_COMMANDS=1` and log in a character above level 15.
2. `?stealthbot sneak`: DuelBot appears, and 3 s later it shimmers with the invisibility effect. It stays targetable and visible.
3. `?stealthbot invis`: same effect, through kInvisible.
4. `?stealthbot shadow`: shadow-object effect.
5. Your character needs an organisation: PvP rules (`PvpRules.IsOpponent`) make nobody an opponent of a character with organisation 0, so the flagged bot is not your enemy and stays visible. `?stealthbot sneak pvp` says so (and logs `[STEALTH] ?stealthbot pvp for ...: you have no organisation`); use `?org 1` (or 2, 3) first. Then `?stealthbot sneak pvp`: DuelBot appears, then vanishes from your screen when it goes stealthed. Keep your back to it: it stays gone. Turn to face it (the bot sneaks at level 5: SL 5, a 21 m range, about a 40% chance per 5 s pass against StealthAwareness 1): within a few passes it should reappear, as the sneak ends for everyone. The server log shows `[STEALTH] ... sees DuelBot`.
6. `?stealth sneak` on yourself: check that your own character shows the invisibility effect, the Sneak icon is toggled on, and movement is slowed. `?health 300` (from full) should end it. `?stealth invis` and then any attack should end it.
7. `?stealthbot off`, then `?stealth off`.
