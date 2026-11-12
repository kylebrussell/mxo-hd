# QA fixture revive in place

`?qarevive [online-handle]` is a trusted fixture operation. The default is self; a headless QAFriend can target copied CSR_Tester110 when native corpse UI blocks chat. It requires exact `MXO_DEV_COMMANDS=1` and the configured database name to start with `mxotest_` with a nonempty suffix. Both participants must be online in the same district, outside the Loading Area, with no pending world change; character IDs 0 and 2 are refused. The target must actually be dead, outside duel death, and neither participant may have pending reconstruction.

This uses the normal full-resource resurrection callback (416) and presentation, including combat teardown, life generation advancement, condition reset retaining PvP choice, own/observer IsDead and HP, own IS, and standing movement action 63. Death Effect/penalty and position/district remain unchanged. The pending resurrection offer is discarded; no consent reply is fabricated. It grants no ability and changes no retail costs, consent or death policy. The native death UI clearing remains a Claude verification item.

## Claude-only one-pass native checklist

Use the exact full PR head SHA recorded in the PR body. Build Debug and Release from that head; compare `--build-info` source/snapshot hashes with the fixture state before native input. Read-only preflight: `dotnet tools/NativeQa/bin/Debug/net8.0/NativeQa.dll --fixture-plan death-presentation` must report `MXO_DEV_COMMANDS=1`. Use the existing death-presentation fixture mode, with a disposable copied110 account and helper transport; do not use main characters or a production database.

1. Record copied110 position, district, HP/IS, PvP flag and Death Effect. Cause actual native death through the fixture. Before reconstruct, issue native `?qarevive` if chat is usable; otherwise use QAFriend's existing helper chat request: `{"actor":"QAFriend","action":"chat","text":"?qarevive CSR_Tester"}`. Helper response acknowledges dispatch only; inspect server and native outcome.
2. Confirm full HP/IS, corpse/death UI gone, standing/action63, normal movement and action use. Position/district and Death Effect/penalty must be unchanged. Observe the same standing/health state from another fixture player. Confirm old ability approval cannot apply to the new life.
3. Repeat once with a dead QAFriend, named from copied110, observing native remote pose/health. A living target, unknown name and malformed command must refuse without restoring anything.
4. Die again, choose Reconstruct, then issue QAFriend's command. Require refusal, preserving the normal Loading Area flow. Check exact dev value other than1 and a non-mxotest configured DB are rejected using unit/throwaway checks, never by starting a production fixture.

No native run was performed by Codex. Headless results establish server protocol assertions only.
