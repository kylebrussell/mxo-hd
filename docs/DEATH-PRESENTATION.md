# Character death presentation and buff dismissal

The player corpse request is **our presentation policy using verified client stances**, pending native motion QA. It requests Prone5/Idle0 rather than inventing Action_Die or assigning the NPC CurrentState to a player. Resurrection now requests the script's actual built-in Action_Resurrect63, with its ability context. Unit/headless checks establish bytes and server lifecycle, not rendered motion.

## Read-only source evidence

Profile: client.dll SHA256 `cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a`.

- `62692ea0` loops the27 stance pointers at `629ce160`, exporting `Stance_%s = index`. Stand0, Prone5, ProneF13 and Crumple25 are explicit. `62692ee3` similarly exports213 action pointers at `629ce1d0`: Idle0 and Resurrect63. **There is no Die entry in that complete built-in table.** No guessed Action_Die constant is added.
- Movement reader `626718c0`, especially `626718fe..62671980`, reads counter, kind, stance, action and abilityu16. Kind bits7/6 are unpacked separately from its low6 bits. Actions25/29 have an extra u16; other built-ins do not. Built-ins without a target go through `62671b45 -> 625a7850 -> 6259cfd0`. Named emote25 takes `62671bea -> 625ba4b0`; sending63 as an emote ID would select an unrelated emote.
- `62671c21` reads the ordinary movement action byte when flag02 is set. `6266ffa0..6267005f` independently computes field lengths and includes self simulation time after position. Existing captured stand-up and observer stance records establish self `28` versus other `2a` (extra ordinary action00, no simulation time).
- `research/client-scripts/ability/hacker/health.py` calls `playScript(Stance_Stand, Action_Resurrect, <ability>)`. The u16 context is the same position that has InvalidAbility1000 in ordinary stance/emote packets; ranged hits carry Handguns129 there. Resurrection325/326/375/416 therefore carry their own IDs instead of1000.
- The **movement** master is separate from the large combat master: RID `3a0002f7`, `interface_interlock_materials.pkb`, offset71858414, size63002, SHA256 `f913856f05aec6d9d12a88eab717f079f13cb482ca9446f989be2d975a6ddcf1`. Its Aggro group includes `Stand to Prone`, `Stand to ProneF` and `Aggro to Prone/ProneF`, with named transition rules and animation IDs1159/115a. Shared animation catalog entries include Prone069a, ProneF0f3f and Prone-Stand_Resurrect0f4d / ProneF-Stand_Resurrect0f4e. These establish actual down/restore assets, not a captured server's exact death selection.
- Retail `enti_lastday2`22:19:10 and `niobe3dualduality`03:04:07 include C0 movement records near deaths/hits. They are **not** copied as an invented death opcode: their action81 is not Die in this client table. NPC CurrentState7fd remains the existing NPC event, with separate NPC movement wire layout; no player CurrentState is fabricated.

Reproduce the constants and movement-asset snapshot without launching or modifying the client:

```powershell
python -X utf8 research/combat/extract_character_movement.py --client C:/Users/russe/Developer/mxo-client/MxO
```

The generated `data/character_movement_evidence.json` preserves all27/213 constant names and the relevant movement transitions. This evidence export is not a runtime dependency.

## Server behavior

`DeathPresentation` provides built-in-action packet builders and Corpse/Stand/Resuscitate helpers. Ordinary death now sends down motion plus IsDead/Health to self and existing observers; previously NPC/virus/dev deaths could leave observers without even the death attributes. Late observer creation appends death attributes and down motion for either ordinary death or duel defeat. Self creation also reapplies the corpse request if its lifecycle is still dead.

Duel defeat requests the same down stance. The post-interlock reassertion covers players and bots. It captures life/world generations and checks the current corpse state under lifeSync; reconstruction, resurrection, world exit and a subsequent duel cannot inherit an old delayed pose. Duel knockdown and standing now advance lifeGeneration. The captured self stand-up packet remains unchanged; observers now receive its corresponding standing motion as well as alive/Health1.

Reconstruction discards the old view and clears death through the existing Loading Area/new-world path. No corpse movement is sent after that reset. Resurrection retains Death Effect and actual PvP flags and requests Stand/Resurrect with the accepting ability. Acceptance resources and other P1 behavior remain unchanged.

NPC_BASE has a different movement part/layout. Pet work should use `ServerPackets.BuildNpcDeathUpdate`'s existing verified NPC path; do not put the player-only `Build(false,...)` body into an NPC view.

## 80bb input and native fallback

Client sender `6205e290` is reached through `6205e350`. That guard requires FourCC `0x6c43524d` (MRCl/right-click) and calls `6235f9e0(0x10)`, whose `GetAsyncKeyState` import at628685f0 tests high bit8000. The source gesture is **Shift plus right-click on the selected buff control/row**. A plain right-click, double-click or Shift-left-click is not this path. The exact selectable child control in this broken-menu installation is still unverified; **no successful native gesture is claimed**.

For a concrete native check without that UI path, `MXO_DEV_COMMANDS=1` enables `?dismissbuff <decimal ability ID>`. It calls `AbilityCancellation.Process` with the same two-byte body as80bb, retaining the ability-range, UBF0 category and CanNOTRemoveBuff gates, and removing only the requested own buff. `62517780` negates CanNOTRemoveBuff: existing server polarity is correct. With commands disabled the shortcut does nothing; native80bb remains enabled. The acknowledgement says requested/refused and is not itself proof that an active icon was removed.

## Claude-only native checklist

Native input and fixture execution belong to Claude. Use only copied CSR_Tester110 in the fixture, never TheNeo. The fixture acquires the client lock itself; do not pre-create/remove another owner's lock. Its normal stop resets main110 to Mara23828,495,13984, full HP/IS, flag0, mission986 only and releases its lock.

1. Build `tools/NativeQa/NativeQa.csproj -c Debug`. Start `NativeQa.dll <this-worktree> <fresh-qaRoot> <full-PR-head> death-presentation`. Wait for state.json Ready and `Im'running :D`; independently launch/login the native client as CSR_AbilityQA/CSR_Tester. The fixture clones the existing QA credential (its printed placeholder password is not literal). No other native character is permitted.
2. While alive at Mara enter `?die`. Verify the self model falls/lies, HP0/death UI, and remains down beyond9seconds. Record the rendered result, not merely IsDead. Do not click Reconstruct yet. Create the fixture's helpers marker, then send QAFriend cast416 targeting CSR_Tester, as in PR104's JSON helper protocol. Wait for Application Request, click Yes once; verify actual down-to-standing animation, restored resources and movement. Retain Death Effect/no false PvP flag. Native dialogs remain P1's independent pending check.
3. Observer view with the same native110: send helper QAFriend `chat` text `?die`. Verify the nearby helper visibly lies down. Send QAOther cast416 targeting QAFriend, then QAFriend `approve` accept=true after its offer arrives. Verify native110 sees the helper stand/restore. Helpers are headless; only110 receives native inputs. Fresh-server scenarios separately verify observers created after death.
4. At full life enter `?health 30`, then `?duelbot challenge`; accept the duel and let the bot win. Verify the self corpse immediately and after the8second interlock teardown. Click Reconstruct: stand at the same coordinates, HP1, no Loading Area. Start another duel after recovery; confirm no stale down motion appears. `?duelbot off` cleans up the bot. Check observers' down/up records in the packet log; do not call wire evidence a visual observer pass.
5. Enter `?die`, verify down, click Reconstruct and complete normal Loading Area/hardline entry. Verify a standing, movable self model and no delayed fall in the next world. Repeat death/resurrection once to check counters/lifecycle.
6. Double-click Hyper Dodge10 or Kung Fu Master1064 in Actions while alive and verify its active buff icon. Enter `?dismissbuff 10` or `?dismissbuff 1064`: verify only that icon/modifiers disappear and the80bd off record is logged. Enter `?dismissbuff 17` while Self Defense is active; verify refusal and retained style. After ordinary death/restore, `?dismissbuff 202` must refuse and retain Death Effect. Optional Shift-right-click is exploratory only; record whether80bb actually appears and which exact control received it.
7. Close the native client, create `<qaRoot>/stop`, allow normal fixture exit, inspect stopped.json for the required Mara/full-health/flag0/mission986 reset and released lock. Record full hash, visual results, packet evidence and any failures in the PR/handoff. Claude merges only after green CI and independent native approval.
