# Player against player: fights, the PvP flag and duels

Players can fight each other in close combat (the interlock) and with guns and abilities, die by it and respawn; who may attack whom follows the client's own rule; and duels work from the challenge to the loser standing up again.

**Checked so far:** 3,438 combined unit tests and all 51 headless scenarios pass at 94667f5 (426.1 s, four disposable servers at once). The real 7.6005 client passed the duel flow at f3894ef, including placement, both sides of defeat, reconstruction, the selection lock clearing, the challenge dialog and surrender. Native follow-up found and fixed two more faults: player shots used an NPC state marker, corrupting the shooter's appearance; and `?org` changed the database but left the live organisation at zero. See "Real-client QA" for evidence and remaining limits.

## Where it comes from

Three kinds of evidence, and each statement below says which it rests on.

- **Retail captures** (`research/packet-dumps/community-intake/`). Fights and duels between players are in five logs:

| Short name | File | What it has |
|---|---|---|
| died_in_pvp | `20260607T191353Z-…/mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp` | The capturing player attacked by two others, shot at, killed, respawned. Always the attacked side. |
| some_pvp | `20260607T191353Z-…/mxopackets1_crashover2k_008_some_pvp_and_teleports_and_some_doors_open` | Another attacked player and a PvP death; the flag at world entry |
| lastday2 | `20260607T015831Z-…/mxopackets1_entilsar_enti_lastday2` | Two duels the capturing player was challenged to, struck first in and won: the attacker's and the winner's side |
| niobe3dualduality | `20260608T122812Z-…/mxopackets1_medanon_niobe3dualduality` | A duel lost (the loser's side to standing up again), challenges sent, declined and refused |
| ProxyM2 | `20260608T130150Z-…/ProxyM2_m2_packets` | Twelve duels accepted (51, 52, 55) |

- **client.dll (7.6005)**, read as for `docs/SECURE-TRADE.md`: each RPC class's constructor (size, defaults), printer (field names) and handler, and the function that decides whether another character is an opponent. Addresses are given so they can be read again.
- **The client's rule scripts** (`research/client-scripts/`): `tempmods.py` for the flag's tempmods; the combat rules are the ones NPC fights already use (`docs/COMBAT-SYSTEM-ROADMAP.md`).

No capture has a client attacking a player outside a duel, `/pvp` (81 9e), a surrender or a 57 record sent bare. Those rest on client.dll alone.

## Who may fight whom

### The client's rule

`IsOpponent(self, target)` at 0x625ed830 decides what the client lets a player attack. For two players, in order:

1. The same character: no.
2. **Duel partners: yes.** Both `DuelID`s set and equal but for bit 0 (0x625ed7e0). This comes before everything else.
3. Otherwise the server mode must make the target fair game, or both must stand in PvP sectors:
   - `PvPServer` 2, 4 or 6 (the three behave alike): the **target's** `ConditionStateFlags` has bit 0x01000000, the PvP flag. The attacker's own flag is never read.
   - `PvPServer` 1: both levels strictly above `PvPMaxSafeLevel`.
   - `PvPServer` 3: everyone. 5: everyone not on the same mission team.
   - `PvPServer` 0 (the client's default): nothing; only PvP sectors count.
   - PvP sector: `isPvPSector` (0x625ed320) on each one's `NavPolyID`.
4. The same mission team: no.
5. **Both must have an organisation, and different ones.** Two flagged Zionites are not opponents; a player with no organisation fights nobody outside a duel.

`PvpRules.IsOpponent` is a port of that, so the server allows what the client offers and nothing else. Not ported: the mission-instance test (two players in the same mission instance are never opponents) and everything about NPCs and pets.

### What the server sends for it

- **`PvPServer` and `PvPMaxSafeLevel`** at world entry, as `3a 05 00 <offset of the value> <key> 04 00 <u32>`. Retail's PvP server sent 6 and 15 (died_in_pvp, some_pvp); the ProxyM2 server sent 2. This server sends 6 when `IsPvPServer` is on in `WorldConfig.xml` and 2 otherwise, and the configured safe level. With nothing sent the client assumes 0 and ignores the flag altogether.
  - The record builder was wrong before (one header byte, a length where the offset goes, the key without its 0). Nothing sent it with the default configuration.
  - `MXO_PVP_SERVER=1` and `MXO_PVP_MAX_SAFE_LEVEL=n` override the configuration for one process (tests).
- **The flag**, on the player's own view (attribute 42, `02 00 02 80 80 80 80 80 80 01 <flags>`), on everyone's view of them (attribute 30, `<view> 02 80 80 80 80 04 <flags>`), and in the creation record others get. Retail carried it in all three.
- **`OrganizationID`** was already in creation records; `?org` now updates the live byte-sized value and both the player's own view (attribute 8) and other players' views (attribute 4).
- **PvP sectors** are the client's own data (`sector_properties.csv`, `pvp_zone`: the archive and combat constructs only). The server looks up where each player stands (`SectorProperties.IsPvpZone`).

### What is ours

The client's rule says who is an opponent. These are the server's additions (`PvpRules.Check`, `PvpCombat`):

- Nobody dead fights or is fought (RPC 4d reason 2: the client has both texts).
- A player in a duel fights only the duel partner. The client enforces half of it itself: in a duel it lets the player select nobody else.
- A refused attack is answered with RPC 4d reason 24, "Target is not an opponent." (see below).
- One interlock per player: a third player's close-combat request gets reason 6, "too many participants". Retail's interlocks held more.
- A close-combat request from more than 15 m gets reason 9, out of range.
- No fights with or by a character in the tutorial.

### The flag: who has it

| | Retail evidence | This server |
|---|---|---|
| PvP server, above the safe level | Flag on the own view right after world entry (some_pvp); cleared by the respawn heal; back on when the next fight started | The same. `/pvp` can't take it off (ours). |
| PvP server, at or below the safe level | None | Not flagged, so not attackable. `/pvp` (81 9e) turns the flag on and off, saved in `characters.pvpflag`. Attacking a player flags the attacker (ours: the client reads only the target's flag, so the target could not hit back). |
| Other servers | The client string "Cannot change flag. Not a PvP server." | `/pvp` gets that string (0x7a000294, sent as `2e 67`). The flag only comes from the zone abilities, and is gone at the next world entry. |

- **`/pvp`** is client RPC `81 9e` with no fields (0x621d5280). The client expects no reply: it prints "You are now / no longer susceptible to PvP" when bit 24 of its own flags changes (0x625fdca0).
- **The safe level** protects by the comparison the client uses for mode 1, level > `PvPMaxSafeLevel`. That retail's PvP server spared exactly those levels is inferred.
- **The flag abilities.** `tempmods.py` has four tempmods: `PVPFlagFlipperInit` (flip), `PVPFlagOnInit` (on), `PVPFlagMsgOnInit` (on, with ID_ABILITY_PVPPULSE if it was off) and `PVPFlagMsgOffInit` (off, with ID_ABILITY_PACZONE if it was on). `PvpFlag.Abilities` maps abilities to them **by name** (the ability data isn't in the scripts): PVPFlipper 721/735/736 flip, PVPPulseFlipper 1210/1212/1214 turn on, PacZoneFlipper 1232/1233/1234 turn off. None is a player's ability: they belong to NPCs and to the PvP-pulse and pacification-zone objects of live events. They work when they land on a player (`PvpFlag.ApplyAbility`); nothing in the world casts them yet. `?pvpability <id>` lands one on yourself.
  - Not done: `PvPIfInInventory` (the PvP_Pass items), `PVPFlagMsgOnTimeInit` (leaving a pacification zone) and the org rule of the PvP teleporters (`serverrules.TeleporterOrgCheck`, `rshooks.LookupTeleportLocationData`): the archive constructs they lead to aren't reachable on this server.

### Refusals: RPC 4d

`CombatRequestResult`, size 9 (constructor 0x6213432d, handler 0x621a1c80): `4d <reason> <is target reason> <is force combat> <start recast timer> <target handle>`. Retail: `4d 0e 00 01 01 0d 00 07 00`, a resisted interlock (lastday2). The texts are picked at 0x6219c410: 2 dead ("You cannot fight while you are dead" / "Your target is dead"), 6 too many participants, 9 out of range, 14 resisted, **24 "Target is not an opponent."**. The server sends 2, 6, 9 and 24. Retail's own use of 24 isn't in a capture.

## Fights

`PvpCombat` is the entry: RPC 40, 41 and attack abilities whose target view is a player go there instead of to the NPC code.

A **handle** is a player as the receiving client knows them: `<view u16> <spawn id u8> 00`, its own view being `02 00 <self spawn id>`. Every record below carries handles in the receiver's terms.

- Player views used to be stored with the wrong spawn id (the counter after the one the creation record used), so any handle the server built for a player would have been off by one. Fixed in `ViewVisibleThread`.

### Close combat (`PvpFight`)

The rules are the ones NPC fights use, from retail's own scripts (`CombatRules`, `CombatInterlock`): both roll attack against defense with their tactics, the preferred tactic attacks first, the opening round is the starter's opportunity attack, a special move that hits wins the round, a withdrawal is tried each round. Both sides are players here, so both have tactics (RPC 42), Energized/Precise, special moves (an instant ability on the opponent becomes the next move, RPC 47 kind 6) and withdrawals (RPC 44). A round takes 4.5 s; Block regains Inner Strength; health and Inner Strength regenerate at the in-fight rate.

**What each side is sent** when A attacks B:

| | A, the attacker | B, the attacked |
|---|---|---|
| Source | lastday2 (a duel's first blow) and the NPC flow, which the real client has run | died_in_pvp 19:41:11 and 19:41:49, some_pvp, niobe3dualduality |
| 1 | `4e 01` if the request forced the interlock; the echo `40 <B> 00 6b` | own flags: in combat |
| 2 | own flags: in combat | the combat view (ILCombatHandler) |
| 3 | the combat view | `47` kind 4 with **B's own** first move, has-ability byte **0** (`47 00 00 01 01 01 01 00 00 00 00 00 …`) |
| 4 | combat init, **A first**: `… <A = 02 00 self> <B> 01 01 02` | the CombatEffect records (`80 bc`, −80 regen) |
| 5 | `46 <B> 01` | `40 <A> 00 6b`, unasked, and `46 <A> 01` |
| 6 | `47` kind 4 with A's opening move (has-ability byte 3); CombatEffect | combat init, **A first again**: `… <A> <B = 02 00 self> 01 01 02` |
| 7 | `45`, the opening round | `45`, the opening round |

- Retail's order for B varied between the two fights in died_in_pvp (the combat view came after the first 45 in the second). The server uses the first.
- B's own flags in retail were `02 00 02 80 80 80 80 80 90 11 00 00 00 01 00 00 10 01`: CurCombatExclusiveAbility 0x11 and the flags. The server sends the in-combat update NPC fights use.

**RPC 45 between players** is the 89 bytes of an NPC fight, read from the receiver's side:

- The first move is the receiver's own, the second the opponent's; both target fields are the opponent.
- "won" is 1 only when the receiver won the round. An opening round won by the attacker is 00 for the attacked.
- The first damage field is damage to the receiver, the second to the opponent.
- The byte before the opponent's tactic is the opponent's has-ability byte: 3 on the attacker's opening move (`… 00 03 00 01 01 46 00 …`). NPC fights have 0 there.
- Unit test `AttackedPlayersOpeningRound` builds retail's record byte for byte, except its last four bytes: in PvP they held the opponent's weapon (`22 b6 00 00`); the server sends 0.

**The end.** Each side still there gets what ends an NPC fight: CombatEffect off, the combat view's final record, `46 <opponent> 00`, the delete of the combat view, and the stance and flags reset ten seconds later. Retail ended an unfinished fight that way (died_in_pvp 19:41:48).

- **What ends the fight goes first. The teardown follows, in packets of its own** (`PvpFight.End`). The rounds stop at once, but the combat view comes down later:
  - **8 s** after a death (a duel lost, a PvP kill);
  - **1 s** after a duel stopped any other way, or after a player left;
  - at once after a withdrawal.
- Retail: the duel's loser got the defeat at 03:03:51 and the delete of the combat view at 03:04:00, with the combat view ticking in between (niobe3dualduality). The winner got DuelID 0 and `56` at 21:54:53 and the teardown at 21:55:01 (lastday2).
- The side that died gets no `46 … 00`, as in retail.
- A pending teardown runs at once when the player starts another fight (any close-combat request) or stands up from a duel. It is dropped if the player's world reloads or they are on their way to the Loading Area.

- A player whose client is gone or reloading its world (teleport, Loading Area, disconnect) gets nothing; the other side gets the teardown.

**Not done:** more than two players in one interlock; the combat view and init for onlookers (retail sent both to every bystander, and nothing else); the round entries on the combat view; stuns keeping a player from hitting (NPC stuns do).

### Health, conditions

- **Health** goes to the player's own view (`02 00 02 80 80 80 40 <hp>`) and to everyone's view of them as attribute 18: `<view> 02 80 80 10 <hp>`. With an effect: `<view> 02 80 80 90 <hp> 0c <EffectID> <EffectCounter>`. Both are retail's (died_in_pvp); the NPC form `04 80 80 80 40` is not used for players.
  - Retail's health updates came 3 to 7 s after the 45 of the round. The server sends them with the round.
- **Tactic conditions** (Daze, Off Balance, Staggered: flags 0x02, 0x04, 0x08) land on players as on NPCs, a quarter of the hits, for ten seconds, on both views. Retail showed them on a player's own flags (`… 01 04 00 10 01`). Staggered adds 5% damage. The odds are the same guess as for NPCs.

### Gunfire (`PvpGunfire`)

RPC 41 at a player. As against an NPC the server fires for the shooter at the gun's interval until the target dies, gets out of range for three shots' time, or the shooter sends 44. The target fires back only if their own client asks. Hit and damage are the free-attack rules (`FreeAttacks`).

| To | Record | Retail |
|---|---|---|
| The shooter | own-view shot `02 00 03 28 <ctr> 01 0c <class> <81 00 hit / 00 10 miss> <target> <pos> <time> …` and `4e 00` | the NPC gunfights; a player target in died_in_pvp: `02 00 03 3d 30 01 0c c3 81 00 05 00 f8 00 …` (an ability shot) |
| Everyone who sees the shooter | `<shooter's view> 03 2a <ctr> 01 0c <class> <hit> <target as they know it> 00 <pos>`; for the target itself that is `02 00 <self spawn>` | `0f 00 03 2a 71 01 0c 37 81 00 02 00 d3 00 00 …` (died_in_pvp) |
| Shooter and target | an `81 68` combat-log line each, attacker-is-you 1 and 0 | `81 68 2d 00 1d 00 03 00 00 00 65 00 0a 00 9a 00 66 00 … 0e 00 AfterWhoruNeo 09 00 f1rewa11` |

- The 40 the client sends with every 41 is left unanswered, as for NPCs.
- A third player may shoot at someone in an interlock. Retail allowed it: the capturing player of died_in_pvp was shot at, and then killed by an ability, by a third player while in an interlock with another.
- One retail shot left its target at 1 health instead of killing. Whether shots from outside an interlock could not kill is unknown; here they can.

### Abilities

An attack ability on a player (RPC 80 b9 with a player's view):

- In an interlock with that player, an instant ability is the next move and lands if the round is won (result 0x100), as against NPCs.
- Otherwise it lands when its cast ends, if the target is an opponent: the damage and effect of `AbilityTargets.Retail` where known, the timed effects retail put on players (`TimedEffects.ApplyToPlayer`), the `81 67` damage lines for both, and the flag abilities.
- Not an opponent: RPC 4d reason 24.
- **The Duelist ability** (497, with its "Duelist Upgrade" 1076) is not about duels: it is the handgun discipline of the Gunman tree, between SMG Specialist and Handgun Artistry in the ability list. No script refers to it. It is in the ability data like every discipline ability and needs nothing here.

### Death and respawn

A player killed by a player dies as retail's PvP deaths did (died_in_pvp 19:42:29, some_pvp):

1. The fights they are in stop. Their interlock comes down 8 s after the death (see "The end" above).
2. Their own view: `02 00 02 80 80 80 c1 01 00 00 80 80 01 00 00 00 01` (IsDead, Health 0), the buffs' off-records and the death message `2e 67 … 92 00 00 60`. This is the death the server already had for NPC kills: that one was taken from these same captures.
3. Everyone who sees them: `<view> 02 80 80 12 01 00 00`, IsDead 1 and Health 0 on their view.
4. **The kill notice** to the victim, the killer and onlookers: `81 9d <killer> <victim> 0c 00 <length> | 81 67 17 00 <offset> 86 00 00 60 00 00 10 00 … 08 <killer's name> <victim's name>`.
   - `81 9d` is `RoutedRPC` (constructor 0x62146dab): two handles and an embedded record, which the client runs through its normal dispatcher (0x6217f1f0). It ignores the handles.
5. The respawn is the usual one: `81 4c`, the heal, `80 fd`, the Loading Area. The heal sets `ConditionStateFlags` to 0, so the flag is gone, as in retail; a PvP server flags the player again when they next fight.

No experience, information or loot changes hands: the captures show none. The retail deaths also set EffectID, DeathPenalty (0.7) and MovementScale on the own view; the server doesn't.

## Duels

### Records

All eight are in client.dll; the dispatcher (0x62175570) has no case for 50 and 52, which are the client's.

| RPC | Name, size | Fields | Client |
|---|---|---|---|
| `50` → server | DuelChallenge, 5 | target handle | `/duel` on the current target (0x62234ea0). `/duel stop` sends target 0. The client only lets "allies" be challenged, and nobody while in a duel. |
| `51` → client | DuelChallenged, 5 | challenger handle | Opens the Yes/No dialog "%s (Level %I, Discipline: %s) challenges you to a duel." (0x62236870). The challenger must be in view: there is no name. |
| `52` → server | DuelResponse, 7 | challenger handle, accepts u8, reason u8 | Yes: 1, 0. No, or the dialog's 15 s (`ChallengeMaxConsiderTime`) ran out, or duels are auto-ignored: 0, 0. Challenger not in view: 0, 2. |
| `53` → client | DuelRequestFailed, 0x0d | reason u8, from-target u8, opponent handle, name offset u16, wait f32 | Prints a line; changes nothing (0x62236640) |
| `54` → client | DuelChallengeSent, 7 | target handle, name offset | "You have challenged %s to a duel!" |
| `55` → client | DuelBegin, 5 | opponent handle | "You are now in a duel to the death with %s." and notes the opponent (0x62238330) |
| `56` → client | DuelStop, 9 | reason u8, from-target u8, opponent handle, name offset | Prints the line and forgets the opponent (0x62236bf0) |
| `57` → client | DuelBroadcast, 0x0e | reason u8, player handle, opponent handle, two name offsets | "%2s has defeated %1s in a dueling challenge!" (reason 1) or "%1s has surrendered to %2s" (2). The player is the loser. |

**53 reasons** (from-target 1 picks the "they" text where there is one): 0 unknown / declined, 1 unavailable, 2 too far, 3 not a player, 4 on a mission team, 5 on a team (told to the target), 6 in combat, 7 already dueling, 8 waiting for another answer, 9 considering another challenge, 10 already challenged this player, 11 already dueling this player, 12 must wait (the seconds are the f32), 13 disabled here.

**56 reasons:** 1 defeated, 2 surrendered, 3 killed by someone else, 4 the other is gone, 5 too far apart. From-target 0 is the receiver's own doing ("%s has defeated you"), 1 the opponent's ("You have defeated %s").

**What makes it a duel is `DuelID`**, not a record. The client treats two players as opponents when their DuelIDs pair, and while its own is set it lets the player select only the partner (0x62231380). Own view attribute 35, others' view attribute 26.

### The flow

| Step | Records | Evidence |
|---|---|---|
| A challenges B | A: `54 <B> 00 00`. B: `51 <A>`. | niobe3dualduality (54), all three (51) |
| It can't be | A: `53 <reason> <from-target> <B> …` | `53 04 01 …` after a `54` (B on a team), `53 0c 00 … 07 01 34 42` |
| B says no | A: `53 00 01 <B> …` | niobe3dualduality 03:24:42 |
| B says yes | Both, each in one packet: own `DuelID` (`02 00 02 80 80 80 80 80 01 <id>`), the other's on their view (`<view> 02 80 80 80 20 <id>`), `55 <other>` | niobe3dualduality 03:03:20; ids 0xcca2/0xcca3, 0x626c/0x626d, 0x3dc0/0x3dc1 |
| They fight | An ordinary fight | lastday2, niobe3dualduality |
| One falls | See below | niobe3dualduality (loser), lastday2 (winner) |

- **The challenger's id is the even one**: in all three captures the capturing player had been challenged, and their own id was the odd one.
- **After a "no" the challenger waits a minute** before asking the same player again: a decline at 03:24:42, a new challenge at 03:24:57 answered with 45.0 s (`DuelHandler.RechallengeSeconds`). An unanswered challenge counts as a "no" after 20 s.
- **Checks before a challenge is passed on** (`DuelHandler.Obstacle`), in this order, each with its reason: the challenger already dueling, already challenging, considering a challenge, in combat, on a mission team; then the target unavailable, on a team, in combat, dueling, challenging, considering; then more than 30 m apart; then the wait. The reasons are the client's. The order and the 30 m are ours.

### The end of a duel

**Defeat.** A duelist out of health at the partner's hand isn't dead. Retail, for the loser (niobe3dualduality 03:03:51):

- Own view `02 00 02 80 80 c1 <MaxHealth> 01 c1 01 00 00 80 89 00 00 00 00 <DeathPenalty f32> 01 <flags>`: **IsDuelDeath 1**, IsDead 1, Health 0, DuelID 0. The server sends the same without DeathPenalty (0.911 there; what it does is unknown).
- The winner's DuelID 0 on their view; the buffs' off-records; the kill notice.
- `2e 67 … de 00 00 3a`, ID_DUEL_DEATH_MESSAGE: "You have been defeated in a duel. Type /reconstruct to stand up."
- The result for everyone nearby: `81 9d <loser> <winner> 0c 00 <length> | 57 01 00 00 00 00 00 00 00 00 0e 00 <offset> <loser's name> <winner's name>`. Retail sent the 57 this way, with both of its handles 0.
- `56 01 00 <winner>`.

For the winner (lastday2): own DuelID 0, the loser's view IsDead 1, Health 0, DuelID 0, and `56 01 01 <loser>`.

- All of it goes out while both interlocks are still up. The teardown follows 8 s later, for the loser without `46 … 00` (see "The end" under Fights).
- The loser's ConditionStateFlags lose the in-combat bit with the defeat: retail's record had `00 00 00 01`, the PvP flag alone.
- **Ours:** 0.25 s after each player's teardown, their own view gets the duel's end once more, in a packet of its own: DuelID 0, or for a loser still lying down, the whole defeat record. The values are the ones already sent, so a client that took them sees no change. Retail didn't do this. It is there because the real client lost both records once (see "Real-client QA", A and B).

**Standing up.** The loser's client sends `81 4c` (its /reconstruct). Retail (03:04:08): `02 00 03 28 <ctr> 40 00 00 00 10 <x y z f32> <sim ms> 80 80 80 41 00 01 00`, an action with position and time, then IsDead 0 and **Health 1**. No Loading Area. IsDuelDeath stayed set and does here. Others get IsDead 0 and Health 1 on their view (inferred; the winner's capture ends before it).

- Retail's loser stood up after the interlock was down. A loser who types /reconstruct sooner gets the teardown first, then the stand-up 0.25 s later in a packet of its own.
- The client's own check (0x62258e38) reads one byte of the local player's attributes (`[[player+0xac]+0x22c]`) and answers "You cannot /reconstruct, you are not dead." (0x60000096) when it is 0. Since an NPC kill passes that check, the byte is IsDead. The client sends `81 4c` only when it is set.

**The other ends** rest on the client's records alone; no capture shows one:

- `/duel stop` in a duel is a surrender: 56 reason 2 to both, the broadcast with reason 2, nobody falls.
- Killed by someone else: 56 reason 3 to both. The death is a real one.
- One of them gone (disconnect, world reload): 56 reason 4 for the one who stays.
- More than 60 m apart, or out of each other's sight: 56 reason 5 to both. Checked every two seconds. The distance is ours.

**Not done:** the "duels disabled here" reason; ID_MISSION_INDUEL (no missions while dueling); the unexplained attribute 27 (`01 00`) retail set on the loser's view for others.

## Real-client QA (PR 94)

At 7380df8, CSR_Tester (level 1, no organisation) against `?duelbot` in the Mara apartment lobby. Challenges, both sides of the interlock, and the messages of winning and losing all worked. Four things did not.

The follow-up at f3894ef passed A, B and D, the challenge dialog and surrender. C's facing is correct; its interlock animation limitation remains.

### E. A gunshot corrupted the player's appearance

The native follow-up on the combined PR85/PR94 tree (6128ab8) showed the bot disappearing on its first shot and returning with a different appearance after the duel. Player shots had reused the NPC builder's `02 2a` prefix. On an Object12 player, `02` begins an attribute update, so the client read the shot body as attributes. Retail's other-player shot uses `03 2a` (died_in_pvp, 19:38:35). `BuildOtherPlayerRangedShot` now uses that prefix, while NPC shots retain `02`. Both PvP and the broadcast of a player's shots at NPCs use the player builder.

At 46bfd06, the native retest passed: the bot remained visible with its pistol, firing animation, tracers and hit reactions; Free Fire returned fire, both health bars and combat-log lines worked, and surrender stopped both shooters. The test gun was added as the Free Fire selection without equipping it, so CSR_Tester's hand model remained empty. This does not establish equipped-weapon appearance parity.

### F. `?org 1` left the player without a live organisation

The duel-less native check initially showed the bot as friendly Zion. `?org` passed a signed integer to an attribute with no signed-integer setter; overload resolution chose the float setter, whose first byte was zero. Its own-view update also omitted the player-view framing. At 94667f5 the command sets an explicit byte and sends own attribute 8 with full view framing. The headless bot scenario now starts without an organisation, runs `?org 1`, checks the own-view bytes and persisted alignment, then attacks the opposing flagged bot without a duel.

The native retest at 94667f5 passed: `?org 1` followed by `?duelbot pvp` showed a red Machines opponent and "[DuelBot] is hostile." Native Free Fire killed it without a duel and printed "DuelBot was just killed by CSR_Tester." Firing stopped and the bot returned with full health. The packet log pins death at 11:33:09 ET and the clear-IsDead/full-health update at 11:33:19 ET on 2026-11-11. The flag on/off check also passed its native messages and portrait indicator. QA used only CSR_Tester (110), copied into disposable databases with a dedicated account exposing no other characters. The client lock was held throughout, then released after resetting the main CSR_Tester through the harness to Mara (23828,495,13984), full health, PvP flag 0 and mission 986 only.

### A. The loser didn't fall, and /reconstruct was refused

- **What happened.** After `?duelbot attack`, `/duel` and `?health 20`, the bot won. The messages were right. The player's health bar emptied but the character kept standing, and `/reconstruct` gave the client's own "You cannot /reconstruct, you are not dead." So the client never stored IsDead.
- **The record itself was retail's.** The own-view defeat matches niobe3dualduality 03:03:51 attribute for attribute (indices 14, 20, 21, 27, 35, 42; masks `80 80 c1 … c1 … 80 81/89 … 01`), with two differences:
  - DeathPenalty (38) is left out. It is optional in the mask, and retail's ordinary deaths sent it with other values.
  - The flags kept the in-combat bit (`00 00 10 00`), where retail's had only the PvP flag. Now fixed: the defeat clears it.
- **What was different from retail is the order.** The server tore the interlock down first (final record, `46 … 00`, the delete of the combat view, the position handoff) and sent the defeat right after it. The queue gathers whatever is sent within 50 ms into one packet, so the defeat usually went out in the same packet as the teardown. Retail sent the defeat while the interlock was up and took the combat view down 9 s later. The NPC-kill path the client accepts also had the teardown first. That does not fit neatly, so the order is the most likely cause, not a proven one: client.dll's handling of an own-view update that arrives with an interlock ending was not traced.
- **Fix.** Retail's order and timing (see "The end" under Fights), the flags without the in-combat bit, and the defeat sent once more after the teardown, in a packet of its own (ours). `/reconstruct` before the teardown is due runs the teardown first and stands the player up in the next packet. The server already answered `81 4c` from a duel's loser with the stand-up (`DuelHandler.StandUp`, before the respawn path); that is unchanged.

### B. After a duel, the client stayed locked onto the old opponent

- **What happened.** After winning, `?duelbot off` then `?duelbot attack`. Clicking the new bot was refused with "...but your opponent.", and `/duel` said "You must target another player before issuing a duel challenge." The removed bot stayed in the target frame until a relog.
- **The refusal is the client's.** It is "While in a duel you cannot select anything but your opponent." (0x5e0000d9), printed at 0x6209b085 when the select check 0x62231380 refuses with reason 4. That check reads the player's own DuelID (`[[player+0xa8]+0x78]`). When it is non-zero, it allows only a target whose DuelID pairs with it (0x62231350: equal but for bit 0). So the lock is the own DuelID attribute. The `55`/`56` records only note and forget the opponent for messages. The `/duel` text is 0x3a0000a0/a1 (0x62234ee3): no target could be selected.
- **Cause.** The winner's own DuelID 0 was sent in the same way as the loser's defeat: after the teardown, in its packet. The client never took it, so its DuelID stayed set. The DuelID at the duel's start, sent in a packet of its own, was taken.
- **Fix.** As for A: DuelID 0 goes out before the teardown, and again after it. A player who leaves or a bot taken away mid-duel now ends the duel for the other side first: DuelID 0 and `56 04`, flushed. Only then come the interlock teardown and the delete of the gone player's view (`DuelBot.Remove`, `PvpCombat.OnPlayerGone`).
- **Where the strings are.** `resource/string_tables/Strings.ltp` holds only the ids (`stringtable_client.pyc`, zip-style entries with raw deflate). The texts are in `resource/string_tables/stringtable_client.sta`, packed in `packmaps/zmisc_1.pkb` (rezmap.lta, entry 199). They are UTF-16, each preceded by its id, so a UTF-16 search of the .pkb finds them.

### C. The bot showed no fight animations to its attacker (minor)

- **What happened.** The bot stood still, facing away, while damage numbers showed.
- **Facing: fixed.** The bot now spawns facing the player (D). When an interlock starts, it turns to its opponent on everyone's view with the state record other players' views get (`<view> 01 0c <yaw> <x y z f32>`). The byte layout is retail's (died_in_pvp, view 03: `01 0c e6 1e 6a 8c 46 00 80 f7 43 65 0e 95 44`).
- **Animations: not done.** In retail, a player opponent was animated through the combat view's round entries. lastday2 21:54:49 has an entry on the combat view (`… 01 01 02 00 00 00 00 <one fighter's offset from the centre, 3 × f32> <the other's, mirrored> 00 00 0a 00 ed 93 …`), which places both fighters about 0.7 m either side of the centre, followed by what looks like animation fields. The server sends no round entries, for NPC fights or PvP ("Not done" under Fights). NPCs animate without them; another player apparently does not. Decoding the entry is the next step.

### D. `?duelbot` put the bot inside a wall

It stood 3 m along +x. It now stands on walkable floor about 2.5 m in front of the player, facing them (see Dev commands).

- **Re-test of 7975b15: still wrong in the Mara lobby.** In sector 209, indoor region 84, polygon 3, at about 23828, 495, 13984, facing the elevator, the bot went into the elevator car behind its closed doors. The car is region 87, another prefab placement. Its floor is at the lobby's height and starts about 1.75 m ahead, so a check that only looked for floor along the way let the bot through.
- **Now** every step of the line must cross polygon edges that are open (`NavMesh.OpenBetween`). Indoors it must also stay in the player's own region. A unit test pins the lobby: for every facing the bot stays in region 84, and the car is not open from the lobby.
- **What the data offers.** The client's `.nav` and `INAV` edges name their neighbour, with `0xffff` for a wall. The packed `data/navmesh` files keep only outlines, so an open edge is found as one the two polygons share. Doors and portals join different prefabs, that is, different indoor regions. A sector side outdoors is open.

## Dev commands

All need `MXO_DEV_COMMANDS=1`.

| Command | Does |
|---|---|
| `?duelbot` | A server-played player, "DuelBot", 2.5 m in front of you on walkable floor, facing you, at your level. It accepts a duel a second after `/duel` and waits for you to strike. |
| `?duelbot attack` | The same, and it pulls you into close combat three seconds after the duel begins |
| `?duelbot gun` | It opens fire with a Redpill Special instead |
| `?duelbot challenge` | It challenges you three seconds after appearing, then attacks |
| `?duelbot pvp` | No duel: it is flagged and of another organisation than yours |
| `?duelbot off` | Takes it away |
| `?pvpflag on\|off` | Your flag, whatever the server type |
| `?pvpability <id>` | A flag ability lands on you (1210 a PvP pulse, 1232 a pacification zone, 721 a flip) |
| `?health <n>` | Your health, to keep test fights short |

**Where the bot stands** (`DuelBot.Spot`):

- It first tries 2.5 m along the player's facing, then the same distance turned 22.5°, 45°, and so on either way, then 2 m, 3 m, 1.5 m and 1 m.
- It takes the first spot reached by a straight line that, every 10 cm, has nav-mesh floor (`data/navmesh`), climbs no more than 40 units at a step, and moves from one polygon to the next only over an edge both share for at least 30 units (`NavMesh.OpenBetween`). Outdoors that includes crossing to the next sector. Indoors the line stays in the player's own region, never into another prefab such as an elevator car or a room behind a door.
- Failing all of those, 3 m along +x (the old spot) if that is in the player's own region; failing that, where the player stands.
- The `[DUELBOT] Summoned` log line gives the spot, its sector, region and polygon, and where the player stood and faced.
- It stands on that floor (the floor's height + 95), facing the player.
- Where a district has no nav mesh, it stands 2.5 m ahead without these checks.
- It used to stand 3 m along +x, which was inside a wall in the Mara apartment lobby (sector 1118).

The bot is a whole character to the rest of the server (`WorldClient.CreateBot`): it is in the client list, the visibility thread spawns it for players nearby, and it fights through the same code as a player. What would be sent to it is dropped. It picks a tactic each round as NPCs do, gets up five seconds after losing a duel, and leaves with its owner.

## Tests

- **Unit** (`tests/hds.Tests/PvpTests.cs`): the attribute updates, the duel records, the kill notice and broadcast, the attacked player's 47 and 45, the combat init and the settings records against retail's bytes; the opponent rule case by case; the flag tempmods; the order of challenge obstacles; the loser's and winner's own-view records at the duel's end, the bot's turn record against retail's, the teardown delays; where the bot stands (on the mesh, with a clear line to it, straight ahead on open ground, and in the Mara room by a wall).
- **Headless** (`tools/HeadlessClient/ScenariosPvp.cs`):

| Scenario | Covers |
|---|---|
| `pvp-close-combat-to-death` | Refusals without the flag; the flag reaching both; both sides' interlock records and the same round seen from both ends; health on both views; death, kill notice, respawn |
| `pvp-gunfire` | Shots on the shooter's, the target's and an onlooker's client; combat-log lines; health; stopping |
| `pvp-server-flags` | PvPServer 6: flagged at entry above the safe level, the flag in creation records, `/pvp` for the low level, attacking flags the attacker |
| `pvp-flag-abilities` | `/pvp` refused off a PvP server; pulse and pacification abilities; a withdrawal between players |
| `duel-to-defeat` | Challenge, competing challenges, accept, DuelIDs, the fight, both sides of the defeat, the broadcast for an onlooker, standing up. The defeat and the winner's DuelID 0 arrive in earlier packets than either combat view's delete, never in the same one; no `46 … 00` for the loser; both records again after the teardown |
| `duel-decline-surrender-and-distance` | Not a player, decline, the wait, surrender (DuelID 0 and `56` before the teardown, in another packet), too far apart |
| `duelbot-duels` | The bot accepting and attacking, a tactic change and an ability as the next move, the bot challenging, the bot taken away mid-duel (DuelID 0 before its view's delete), losing to the bot and `81 4c` before the teardown was due (the teardown first, the stand-up in a later packet) |
| `duelbot-gun-and-pvp` | The bot firing in a duel; the flagged bot of another organisation declining a duel and being attacked without one |

A pass means the server does what this document says. It doesn't show the client accepts it.

## To check in the real client

Start the server with `MXO_DEV_COMMANDS=1`. One client is enough. In order of how much rests on it:

1. **Does the bot look like a player?** `?duelbot`. A character named DuelBot should stand next to you and be selectable. If the client drops it, everything below fails and the spawn is the place to look.
2. **The challenge.** Select DuelBot, type `/duel`. Expect "You have challenged DuelBot to a duel!" (54) and a second later "You are now in a duel to the death with DuelBot." (55). The bot should now show as an opponent (hostile selection colour, Close Combat offered): that is the DuelIDs working. Selecting anything else should be refused by the client.
3. **The attacker's side.** Double-click the bot. The interlock should start as with an NPC: camera, both characters fighting, damage numbers both ways, the bot's health bar falling. Change tactics from the Actions window.
4. **Winning.** `?duelbot` again for a fresh one if needed; fight it down (it has your maximum health; `?tactic power` helps). Expect "You have defeated DuelBot! The duel is now over.", the area line "CSR_Tester has defeated DuelBot in a dueling challenge!", and the bot lying down, then up again after five seconds. Your interlock ends about 8 s after the duel's end, as in retail. Use CSR_Tester (110) for native QA.
5. **The attacked side.** `?duelbot attack`, `/duel`. Three seconds after the duel begins the bot attacks: the interlock must start without any request from the client. This is the side only captures describe, so it is the likeliest to need fixing: watch for a client error, a fight with no camera, or no fight at all.
6. **Losing.** In that fight, `?health 20`. Expect your character to fall, "DuelBot has defeated you! The duel is now over." and "You have been defeated in a duel. Type /reconstruct to stand up." Then `/reconstruct`: you should stand up where you are with 1 health and stay in the world. Try it once within 8 s of falling and once after.
   - **After a duel** (B). Win or lose a duel, then `?duelbot off` and `?duelbot attack`. You should be able to select the new bot and `/duel` it. Also take the bot away mid-duel (`?duelbot off`): expect "Your duel has been stopped because your target's RSI could not be found in the matrix." and selection free again.
7. **The dialog.** `?duelbot challenge`. Three seconds later the Yes/No dialog "DuelBot (Level …) challenges you to a duel." should open. Try Yes, No, and letting it run out (15 s).
8. **Surrender.** In a duel, `/duel stop`: "You have surrendered to DuelBot."
9. **Gunfire.** `?duelbot gun`, `/duel`: the bot should draw and fire, with tracers or hit reactions and its lines in the combat log. Then shoot it yourself with the Free Fire button.
10. **The flag.** `?pvpflag on`: "You are now susceptible to PvP." and whatever the client's flag indicator is. `?pvpflag off`. `/pvp` on a default server: "Cannot change flag. Not a PvP server."
11. **A real kill.** `?org 1`, `?duelbot pvp`: the bot is flagged and of another organisation, so the client should offer to attack it with no duel. Kill it; expect the kill line in chat. It is back after ten seconds.

Two real clients are still needed for anything a client decides about another real client: what the challenged player's client shows of the challenger, and both ends of one fight at once.
