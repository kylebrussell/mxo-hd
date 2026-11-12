# Combat styles and style specials

Combat styles are the `IsCombatExclusive` toggles: Self Defense 17, Aikido 101, Karate 132, Kung-Fu 133, Hacker Style 1006, Coder Style 1008 and Spy Style 1112 (plus Handguns 129, Rifles 147 and Submachine Guns 158, whose numbers belong to the weapon-proficiency work). `tempmods.UpdateCombatStyle` only calls the native `AbilityInv.updateCombatStyle`, so the rules below come from the stored GOB fields, `client.dll` and the shipped ILDB. Code: `CombatStyles.cs`, `CombatSpecials.cs`, `CombatSelection.cs`.

## Client requests

### `3e` selection: what produces it

Layout after the opcode: slot u8, ability u32, IsSlot u8, IgnoreStyleUpdate u8. The sender `6219f460(ability, slot, ignore)` first calls the local selection setter `625dd5f0`, which nothing else calls, so the client has already switched its own toolbar. It then writes the slot when it isn't `6b`, otherwise the ability. IsSlot is never written. `3f` (Free Attack: slot, ability, IsSlot) is its ranged twin, sent by `6219f340`. The callers:

| Caller | When | Values |
|---|---|---|
| `62076a91`, `62076b71` | The toolbar's **dropdown slot** list. Its event handler `62077ac0` routes the combo events `CPPA` to `62076a00` and `CPPB` to `62076ae0`. Each reads the chosen list entry (`[list+0x6c]` vfuncs `0x178`/`0x150`, entry `+0x40`) and sends `3e` when the slot's mode `[+0x84]` is 0 (Close Combat) or `3f` when it is 1 (Free Attack). The tooltips are "Use this dropdown slot to select your Close Combat style." (`8800006c`) and "... Free Attack style." (`8800006d`). | The chosen style. A bag slot is sent as `80000000\|slot`. IgnoreStyleUpdate 0. |
| `6210677b` | The combat toolbar window's constructor `62104a10`, created by the UI factory at `62021272`. It re-sends both current selections (`3f` then `3e`) when the toolbar is built. This is the world-entry `3e`. | E.g. `6b 85 00 00 00 00 00` (Kung-Fu, 37 captures) or `6b 00 10 ..` (none, 147 captures). |
| `621e00a0` | An inventory move of the selected item between slots (types 4/5). | The new slot, with IgnoreStyleUpdate 1. |

The client needs no reply to apply its own selection. The server validates ownership (an owned bag slot, or a loaded ability or grant) and retains the selection. It echoes own-view attribute 39 `CurCombatExclusiveAbility`. When the value is a style and IgnoreStyleUpdate is 0, that style becomes active, and the server sends the same records as for `80 b9` below. The style icon on the toolbar is not the dropdown. Clicking it sends `80 b9 <style> <view>` and, without a target, the client shows `ID_TOOLBAR_IL_NOTARGET` "You do not have a valid target selected." itself.

### `80 b9 <style>`: using a style from the Actions window

Double-clicking a style in the Actions window sends an ordinary ability use. The client changes nothing locally: native QA saw no `3e` and the toolbar stayed on Self Defense. Retail's server switched the style. In `actions_combat_marketplace_abilitychangecoder` at 10:36:05, the client sends `80 b9 ee 03 0f 00` (Hacker Style, with a target selected), and one server packet answers:

1. Own view: `02 00 02 80 80 80 80 80 10 ee 03 00 00` (attribute 39 = 1006).
2. Self Defense's nine `80 bc 15 00 <record> 00 11 00 00 00 <stat> <value> 01` records off, then `80 bd 05 11 00 00 00 00 00 00 00` (its toggle off).
3. Hacker Style's eight `80 bc 15 00 <record> 00 ee 03 00 00 <stat> <value> 00` records on, then `80 bd 05 ee 03 00 00 00 00 00 01`.
4. `81 67` with string `c600221c` "You are now using the %1z martial arts style.", flag 1, ability 1006, values [108, 0]. `c600221c` is the ActivationStringOnSelf of the style's first modifier (ViralAcc for Hacker Style, MeleeAcc for the martial styles), and 108 is that modifier's value.

No `80 ac` cast echo, IS cost or cooldown follows. The server now does exactly this, in this order, for a loaded style used through `80 b9` and for a `3e` style switch (`CombatStyles.UseStyle`/`Switch`/`Select`). Style records therefore use kind `0x15`, as in the capture; loaded passives keep `0x45`. Using the active style again only re-echoes the selection. Using a style that isn't loaded gets an `80 ba` refusal and does not switch.

**Values.** The capture's record values don't fit the plain native function 2 with one ability level per style. Self Defense's MeleeDam 25 = 4 + trunc(50 × .42) fits level 50, but its MeleeAcc 77 and viral defence 76, and Hacker Style's ViralAcc 108 and ViralDam 46, need different levels (or other contributions) per stat. The character's build is unknown. The server keeps the stored function; exact per-stat parity is an open gap.

### `43` MartialArt is never sent by 7.6005

The only producer is `6219f0a0(u16)`. It builds `43 <u16>`, sends it and stores the value at `this+0x88`. It has no call or data reference anywhere in the image. The constructor `6219e890` has none either. The matching server message `81 6e` carries Flags u32, ForcedTacticalSetting u8, ForcedMartialArt u16 and ForcedTacticalValue i16 (printer `62157300`, builder `62144c2d`). Its handler `6219e240` stores ForcedMartialArt at `+0xb6`, which nothing reads; only the tactical setting and value reach the UI (`620b8460`). MartialArt is therefore a vestige of an older style mechanism that 3e replaced. The server keeps a received value for logging only. It neither changes the style nor sends a reply.

## Active style

- Only the active style's modifiers with `WhenEnabled 2` and `DurationType 2` count. They are evaluated with ValueFunction 2 at the style's learned level, using the native binary32 truncation of `AbilityValues`; for example Kung-Fu @50 gives MeleeAcc 10 + trunc(50 × 1.3f) = 74. They join `AbilityPassives` totals, so accuracy, defence, damage influence and healing use them like other passive totals.
- A selected style that isn't loaded (directly or as a grant) falls back to Self Defense, which Awakened grants.
- Weapon styles can be active but add nothing here.
- Hacker/Coder Style add viral accuracy and damage and Healing; Spy Style adds thrown and melee bonuses; each style has its own defence and Withdraw values (see the CSV).

## Special-move set

A `CombatSpecialMove` ability's `CastRequiredCombatStance` names its style: Karate (Berserker Attack 535, Sky High Sidekick 267), Aikido (Tomo Nage 412), Spy Style (Wounding Throw 281), Self Defense (Cheap Shot 198, Head Butt 197), Handguns, Rifles and so on. The client's `80 ba` handler (`621e5280`, string table `621e5698`, index = code − 2) shows code 13 as `ID_ABILITY_NOT_IN_CORRECT_STYLE` "You are not in the correct combat style to use this ability." That suggests retail refused wrong-style moves rather than switching. The server sends that code when the required style isn't loaded. When the required style **is** loaded, it still switches to it (the same records as above) and lets the move through. No client code was found that sends `3e` for a special move, so this switch is a server choice. Strict retail behaviour would refuse with code 13 here too; changing to that would also need the Paralyzing Throw parity scenario and the NativeQa root checks to select the style first.

Abilities that have a stance but aren't special moves are **not** gated yet. That covers viruses (Hacker Style), Enrage (Coder Style), Poison Knife and Sever Artery (Spy Style), and they work in any style. Whether the native client auto-switches for them still needs native QA.

## Style shown to other players

`CurCombatExclusiveAbility` is a self-view-only attribute: Object12 has no other-view index for it. Other players see a style through the interlock. The ILDB's exchange groups are lettered by style, and its own groups give the letters: `Stand Transitions:To KungFuAbility` holds `Trans Stand-WS..WB`, `To KarateAbility` holds `KS..KB`, `To AikidoAbility` holds `AS..AB`, and `To Self-Defense` holds `SS..SB` (also MuayThai M, TaiChi T, Pistol P, SubM G, Rifle R, DualWeapon D). Tactic-pair exchanges exist for A D G K P R S W. Round entries now choose each fighter's exchange from the pair `<style><tactic> vs <style><tactic>` and its opening `Trans Stand-<style><tactic>`. Fighters and observers receive the same entry. NPCs use Self-Defense. Hacker, Coder and Spy Style have no exchange set of their own, so they use Self-Defense's; that is a choice. Missing pairs fall back to Self-Defense. `research/combat/extract_animations.py` now exports the S/A/K/W static pairs and their stand transitions (2,244 moves).

## Special attacks

| Ability | Server behaviour | Evidence / choice |
|---|---|---|
| Poison Knife 146 | ThrownDOT 17 over 25 s, tick int(17/25×2.5+.5) = 2, on NPC or player. The three thrown DOTs are UDB0 catalog debuffs, so `AbilityHostile.Catalog` runs them (with their caster/target lines); `CombatSpecials` keeps their landing damage off the generic fallback. | Stored DOT; tempmods.ThrownDOT. |
| Wounding Throw 281 | ThrownDOT 61 over 50 s. A special move that needs Spy Style. | Stored. |
| Sever Artery 341 | ThrownDOT 68 over 30 s; MovementSpeed, Sneak and Invisibility movement −50 and ForceCombat −10 for 30 s. | Stored. **RandomCombatStateChance** (10% per 2.5 s Daze/OffBalance/Staggered) is not modelled. |
| Berserker Attack 535 | When the special lands, the caster gets Melee Damage +11 and Melee and Ranged Damage Resistance +50 for 21 s. | Stored `WhenEnabled 1` modifiers. Applying them on landing, rather than on use, is a choice. |
| Side Roll Escape 121 | When it lands, the caster gets Combat Evasion +20 for 30 s, then leaves the fight (NPC: at once; PvP: the withdrawal request). | Stored buff. The escape is inferred from its name, TestFunction 6 and the ILDB's `Withdraw Sideroll` moves. |
| Taunt 647 | Hostile NPCs within RadiusOfEffect (4 m) of the caster hate the caster most. One not already fighting the caster turns on them. While the taunter fights it, it lands no hits on other players' fights. Players are unaffected. | `tempmods.TauntAggro` (`AI.setMostHated`; humans get "cannot taunt"). The hold lapses when the taunter stops fighting that NPC; that is a choice. The script's caster/target strings are not sent. |
| Wooden Stake 207 / Enhanced 1022 | 10 damage; ×10 (Enhanced ×75) against NPCKilledOnlyByWood 464. | `ability/general.py` constants. |
| Silver Bullet 208 / Enhanced 1024 | The same against NPCKilledOnlyBySilver 463. | general.py. |
| Holy Water 547 / Enhanced 1023 | The same against NPCKilledOnlyByHolyWater 432. | general.py. Removing a player's zombie virus is not modelled. |

The NPC templates that grant the NPCKilledOnlyBy* abilities are not shipped. Server policy assigns them by name: "Vampire" → wood, "Werewolf"/"Lycan" → silver, "Zombie" → holy water. The holy-water link is supported by `tempmods.py` 2912/2940, where zombie-virus characters receive NPCKilledOnlyByHolyWater. Only zombies spawn in the current data. `?exileweakness wood|silver|holywater|none [NPC name]` (MXO_DEV_COMMANDS=1) sets a test NPC's weakness.

Precise Blow 15 and Punishing Blows 16 are toggles (`IsAToggleAbility`, not `IsCombatExclusive` or `CombatSpecialMove`). They are not style specials and remain missing. Self Defense 17 is a style and is covered above.

## Validation

- Unit: `CombatStyleTests` (19 tests): active-only modifiers and native truncation, fallback, all seven styles, stance gate and auto-switch, the `80 b9` style switch in retail's record order and format, the wrong-style refusal code, weapon styles, ILDB letters and transitions, thrown DOTs, caster buffs, Exile damage, MartialArt.
- Headless (fresh servers): `combat-style-switching` covers 3e records and toggles off/on with the activation line, attribute 39, the Aikido refusal (`80 ba` code 13), the Spy auto-switch, `80 b9 85 00` switching to Kung-Fu with no cast echo, and Kung-Fu W exchanges against an NPC. `combat-specials-npc` covers Taunt turning WinterFrost from another player, Poison Knife's DOT ticks, and Silver Bullet 10 / Wooden Stake 100 on a wood-weak NPC. `combat-specials-pvp` covers the Sever Artery records on a flagged player, the Berserker buff on the caster only, and Taunt ignoring players.
- No native pass is claimed. See the native QA steps below.

## Native QA (NativeQa fixture, `styles` mode)

```powershell
dotnet tools/NativeQa/bin/Debug/net8.0/NativeQa.dll <checkout> <new-output-dir> <label> styles
```

CSR_Tester starts beside the test NPC cluster with Karate, Kung-Fu, Hacker/Coder/Spy Style, the specials above, Sky High Sidekick, Tomo Nage (Aikido deliberately absent) and Cheap Shot. The server runs with dev commands.

1. Enter the world.
   - Open the Actions window, find Kung-Fu under Martial Arts Initiate and double-click it. The client sends `80 b9 85 00`. Check that the toolbar style icon changes to Kung-Fu, the chat shows "You are now using the Kung-Fu martial arts style.", and the server log has `[STYLE] ... now fights with Kung-Fu`. The packet log should show attribute 39 `85 00 00 00`, Self Defense's `80 bc 15 00` records off, `80 bd 05 11 .. 00`, Kung-Fu's records on and `80 bd 05 85 .. 01`. Check the character sheet's melee accuracy/defence.
   - Double-click Karate: the Kung-Fu records and toggle go off.
   - Then try the toolbar's Close Combat **dropdown slot** (tooltip "Use this dropdown slot to select your Close Combat style."). Open its list, likely with a left click on the slot's arrow or edge rather than the icon itself; right-click menus don't open in this client. Choose Kung-Fu. The client should send `3e 6b 85 00 00 00 00 00` with the same server answer. Record how the list opens.
   - Clicking the style icon itself is the interlock/attack button. Without a target it shows "You do not have a valid target selected."
   - Relog and confirm the world-entry `3e` re-applies the selected style.
2. Engage WinterFrost in Kung-Fu, and later Karate. Watch the stance and exchanges: Kung-Fu/Karate moves instead of Self-Defense. `events.jsonl` names each round's animation group (`WS vs SP`, `KP vs SS`...). Create `helpers`, then let QAOther watch from outside: the bystander sees the same style moves.
3. In Kung-Fu, click Berserker Attack. The client should switch to Karate itself (a `3e` before `80 b9`); otherwise the server switches it. Check the Berserker buff icon (21 s). Click Tomo Nage and check the refusal message. Note whether the client greys it out.
4. Pick Spy Style and throw Poison Knife and Sever Artery at an NPC: DOT numbers every 2.5 s; Sever Artery slows the NPC. Use Side Roll Escape in a fight: Combat Evasion icon, and the character leaves the interlock.
5. With QAOther fighting an NPC (request `{"actor":"QAOther","action":"melee","target":"WinterFrost"}`), cast Taunt next to it. The NPC turns to CSR_Tester and QAOther's health stops dropping.
6. `?exileweakness wood Trainman`, then Wooden Stake (100) and Enhanced Wooden Stake (750) on Trainman. Silver Bullet shows 10. Check the damage numbers and the combat log.
7. Record whether a viral ability (Hacker Style stance) auto-switches the client's style. That decides whether non-special stance abilities should be gated.

Stop with the `stop` file and verify `stopped.json` and the released lock, as the fixture README describes.
