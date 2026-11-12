# Disposable QA loadout commands

Native Status > Abilities has no load control in the reviewed client. In an authorized disposable native fixture, chat now accepts `?qaload <canonical ability id>` and `?qaunload <canonical ability id>`.

Both commands require exactly `MXO_DEV_COMMANDS=1`, an ASCII `mxotest_` database name with a nonempty suffix, the current authenticated world session, a live character, and character ID other than0 or2. A caller supplies no character, slot, learned level or loaded-state override. The helper reads only that character's archive and requires exactly one owned ability-code row below the character-attribute slot boundary. Missing, hidden-only and ambiguous rows are refused. It creates no ownership rows.

The nineteen-byte request body uses the captured `80 ae` shape documented by `AbilityProgression.TryParseLoadout`: object0, unload/load flag10, count1, entry0, owned slot u16, canonical ability u32, owned learned level u16. `QaLoadout.Dev` calls `PlayerHandler.ProcessLoadAbility` synchronously in the existing authenticated chat dispatch. All load acknowledgements, hidden grants, character prerequisite rejection, persistence and passive teardown still come from that normal handler. The submission notice confirms dispatch, not acceptance; a level rejection can precede it. Production loading rules are unchanged.

`?codeupload` already exists and uses the normal upload handler; this PR adds no upload shortcut or crafting policy.

## Validation

Unit tests cover exact opt-in/database/character/lifecycle scope, strict decimal syntax, byte-exact load/unload requests, own slot and learned level, missing/hidden-only/duplicate rows. Fresh headless `qa-loadout` and `qa-loadout-disabled` exercise the real chat and handler path: owned Write Code37 level15 loads grant970 and its passive, unload removes all three, level50 refuses7 and871 (require99), missing970 and extra target arguments cannot grant anything, a fresh login reads persisted loadout, and disabled commands preserve rows.

Use existing NativeQa `remaining` mode with focused IDs `2,17,7,12,37,871`. It already enables the dev environment, owns requested rows and unloaded discipline ancestors, and retains source/snapshot assembly-hash equality. No new fixture mode or setup path is introduced. The worker runs only builds and read-only `--fixture-plan` / `--build-info`; Claude owns setup, client operation, reset and merge.

## Claude-only single-pass acceptance

Pin the exact full head from the PR body. Build NativeQa in the chosen configuration; verify `--fixture-plan remaining` reports dev1 and `--build-info <worktree> 2,17,7,12,37,871` reports the matching source assembly hash and owned unloaded ancestor list. Start a fresh disposable fixture using the exact head and `remaining 2,17,7,12,37,871`; verify its source/snapshot hashes match that configuration. Use only copied CSR_Tester110. Never use main character2 or a main database for these commands.

1. With CSR110 level50, enter `?qaunload 37`. Confirm the parent/grant disappear and the Write Code passive is removed. Enter `?qaload 37`; confirm both37/970 return at the owned learned level and the passive returns.
2. Enter `?qaunload 7`, then `?qaload 7`. The normal level-too-low message must appear and7 must stay unloaded. Repeat unload/load for871. Do not raise the native actor above50. A submission notice alone is not success.
3. Enter `?qaload 970` (hidden grant, no owned archive row in this fixture), `?qaload 37 6111` and `?qaload -1`. Confirm owned-row/usage refusals and no new ownership or target changes. Relog and confirm accepted load/unload changes persist.
4. Stop the fixture, reset/verify the main CSR110 state through the established NativeQa lifecycle, release the lock, and record exact head/configuration/hashes/results. The worker does not run these native steps.
