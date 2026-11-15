# Native QA duel acceptance

This tools-only package adds a separate `duel-accept` request and a discovered `duel-helper` profile. Gameplay eligibility, timers, duel state and resurrection approval are unchanged. Profile abilities are only2/17, with explicit process-local PvE configuration (`MXO_PVP_SERVER=0`) and no dev, GM or RNG override.

## Why the old helper failed

The retained native205 request `Temp/mxo-nativeqa-conditional-powerless-081018/requests/01-approve.json` asked QAEnemy to approve, but the response timed out waiting81 88 while recording an actual5104000200. Existing approve handles resurrection81 88, not a duel. That51 challenge needs52040002000100: opcode52, identical challenger handle, accepts1, reason0. Existing DuelPackets, docs/PVP.md184-190 and the normal duel headless cases establish the codec and native producers62234ea0/62236870/62238330.

NativeQa seeds QAEnemy's DB PvP flag1, but ordinary PvE world entry clears it. Native205 logs show CSR's later flag-on and no QAEnemy flag-on; they do not prove both runtime flags were on. Mode2 already permits an actually flagged differently aligned target. This package does not change that law. Unanswered headless challenges expire after the existing20 seconds, then normal60-second rechallenge policy applies;45 was remaining time, not a timer to reset.

## Operator sequence (Claude only)

Use `duel-helper` and bring helpers online. Arm a request file BEFORE issuing /duel:

`{"actor":"QAEnemy","action":"duel-accept","challenger":"CSR_Tester"}`

Wait for responses/<requeststem>.armed.json, then challenge. Do not wait for the final request response before challenging. It waits up to30 seconds for the NEXT51 from the expected visible challenger, validates the exact handle/view/spawn, sends ordinary52<samehandle>0100, and succeeds only after a NEW matching55 and new paired DuelID updates. Historical51/55/DuelIDs cannot satisfy it. The arm wait does not extend the server's20-second lifetime once a challenge exists. Missing, malformed, mismatched or expired challenges fail with evidence; no direct server DuelHandler calls or timer resets are used.

Resurrection approve and invitation accept retain their existing request meanings. The same small helper is exercised by an ordinary ephemeral-port headless scenario, not by starting NativeQa/setup or fixed-port servers. It reuses the existing duel-ID view reader with a start cursor; existing callers default to their original behavior.

The profile keeps both actors as PvE allies so the native client can offer /duel. An accepted duel supplies paired IDs and normal combat eligibility. For future non-duel PvP fixtures, a dedicated profile can use existing process-only MXO_PVP_SERVER=1 with level50, safe15, organisations1/2, no mission team/tutorial, same world and live views: existing entry logic flags both. Do not enable that globally or assume a DB flag survives PvE entry. No new hostile profile is added here.

## Verification boundary

Units check the literal failed-native51 to exact52 conversion, malformed/wrong-kind/zero/mismatched handles and no-extra-privilege profile. The normal wire fixture proves initial PvE attack refusal, armed helper, ordinary challenge/accept, new55 and paired IDs, legal combat with actual damage, normal stop, and rejection of a stale cached challenge by a second arm. Required raw RPC/all03/problems/full log/source DLL identity is saved before teardown, including failures. Native dialog/menu usability remains Claude-only pending. Validation and fresh results await coordinator review.

## Candidate validation and one pass

The candidate passed 35 focused codec/duel tests and 33 profile/metadata tests. Both actual Debug and Release NativeQa builds (including HeadlessClient), discovered plans and read-only build provenance passed. Server code and data are byte-identical to master bfdb9875; this is a tools-only change. Debug hds SHA256: 1211BE81DB35BCF18FEFA943A29BF7E37579295637CA38029374EC5026E6ECD0. Release: 85234E61CF01B339FFEAAE4EF2FFD53CA62C18F46CE1D9A4AAC4A22AF1798183. Fresh execution awaits coordinator review and grant.

For Claude's disposable CSR110 one pass, use the normal CSR_Tester and QAEnemy profiles with abilities 2 and 17. First confirm ordinary Attack is refused in this PvE configuration. Bring QAEnemy online and arm the duel-accept request above. Wait for its responses/<requeststem>.armed.json acknowledgement, then issue `/duel QAEnemy` from CSR_Tester; the helper response must show the exact new challenge, reply, begin and paired IDs. Confirm normal combat becomes available and exchanges damage. Stop the duel normally, then verify ordinary non-duel hostility is refused again. Do not use resurrection `approve` to answer the challenge, alter flags, or reset challenge timers. Native menu/dialog behavior remains unproven until this pass.

## Explicit arm barrier and failure evidence

Native operators must use a new request stem and wait for its `responses/<requeststem>.armed.json` before issuing `/duel`. This acknowledgement is written only after the expected visible challenger has resolved and the helper's new RPC/view cursors are captured. It includes actor, challenger, exact handle and both cursors. Do not wait for the final response before challenging. The shared helper exposes an optional typed asynchronous onArmed callback; the headless scenario awaits its explicit completion signal before sending50. No scheduler timing assumption is needed.

A failed duel-accept response additionally preserves the helper's raw RPC, all received03 lists, protocol problems and concrete exception type. A stale challenge still raises TimeoutException; other helper actions and resurrection approval retain their original response behavior.

After integrating actual master0d333c5c,68 focused duel/codec/profile checks and both actual builds/plans/provenance passed. Server/data bytes match that master exactly. Current DebugC6A4309B7EEDABF19F12ADF6FD6A500A51EEFC991C0FC65B2FF7B78B0A7A3F14; Release2E0479658F3CACAFDA6090A0CCD92876B9718BD4BD2FB1BECEA8D25970F10D82. Fresh and native UI checks remain pending coordinator review/grant and Claude respectively.

## Fresh wire proof

Frozen12c2093aa9521d9b0e853d6613a477dddafb1c37 passed nativeqa-duel-helper in18.2 seconds, exit0, parallel1, normal server disposal. Required preteardown proof Temp/mxo-nativeqa-duel-mxotest_run_1_150044b9d0.json SHA256 6548C5BCC557FD8A293165AFAE83CF8D70115CCA41A1AFD1C72669E65EB01F67 records Passed true, Step complete, Failure null and actual selected Release DLL2E0479658F3CACAFDA6090A0CCD92876B9718BD4BD2FB1BECEA8D25970F10D82. Paired full stdout/debug and all raw RPC/03 are retained.

Initial PvE attack was refused. The explicit arm captured RPC cursor33/view cursor4 before the challenge. Actual new records were5104000200, ordinary reply52040002000100 and new begin5504000200 at index34. New paired IDs were4099 and4098. Ordinary combat then exchanged actual health damage; normal duel stop succeeded. A second arm timed out rather than reusing the historical challenge/begin. CSR_Tester retained49 RPCs/10 view lists and QAEnemy47/10, both with zero protocol problems. No server legality, flags, timers, RNG or resurrection response was changed. Native request-file/UI one pass remains Claude-only pending.
