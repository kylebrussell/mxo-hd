# Native QA duel acceptance

This tools-only package adds a separate `duel-accept` request and a discovered `duel-helper` profile. Gameplay eligibility, timers, duel state and resurrection approval are unchanged. Profile abilities are only2/17, with explicit process-local PvE configuration (`MXO_PVP_SERVER=0`) and no dev, GM or RNG override.

## Why the old helper failed

The retained native205 request `Temp/mxo-nativeqa-conditional-powerless-081018/requests/01-approve.json` asked QAEnemy to approve, but the response timed out waiting81 88 while recording an actual5104000200. Existing approve handles resurrection81 88, not a duel. That51 challenge needs52040002000100: opcode52, identical challenger handle, accepts1, reason0. Existing DuelPackets, docs/PVP.md184-190 and the normal duel headless cases establish the codec and native producers62234ea0/62236870/62238330.

NativeQa seeds QAEnemy's DB PvP flag1, but ordinary PvE world entry clears it. Native205 logs show CSR's later flag-on and no QAEnemy flag-on; they do not prove both runtime flags were on. Mode2 already permits an actually flagged differently aligned target. This package does not change that law. Unanswered headless challenges expire after the existing20 seconds, then normal60-second rechallenge policy applies;45 was remaining time, not a timer to reset.

## Operator sequence (Claude only)

Use `duel-helper` and bring helpers online. Arm a request file BEFORE issuing /duel:

`{"actor":"QAEnemy","action":"duel-accept","challenger":"CSR_Tester"}`

Do not wait for its response before challenging. It waits up to30 seconds for the NEXT51 from the expected visible challenger, validates the exact handle/view/spawn, sends ordinary52<samehandle>0100, and succeeds only after a NEW matching55 and new paired DuelID updates. Historical51/55/DuelIDs cannot satisfy it. The arm wait does not extend the server's20-second lifetime once a challenge exists. Missing, malformed, mismatched or expired challenges fail with evidence; no direct server DuelHandler calls or timer resets are used.

Resurrection approve and invitation accept retain their existing request meanings. The same small helper is exercised by an ordinary ephemeral-port headless scenario, not by starting NativeQa/setup or fixed-port servers. It reuses the existing duel-ID view reader with a start cursor; existing callers default to their original behavior.

The profile keeps both actors as PvE allies so the native client can offer /duel. An accepted duel supplies paired IDs and normal combat eligibility. For future non-duel PvP fixtures, a dedicated profile can use existing process-only MXO_PVP_SERVER=1 with level50, safe15, organisations1/2, no mission team/tutorial, same world and live views: existing entry logic flags both. Do not enable that globally or assume a DB flag survives PvE entry. No new hostile profile is added here.

## Verification boundary

Units check the literal failed-native51 to exact52 conversion, malformed/wrong-kind/zero/mismatched handles and no-extra-privilege profile. The normal wire fixture proves initial PvE attack refusal, armed helper, ordinary challenge/accept, new55 and paired IDs, legal combat with actual damage, normal stop, and rejection of a stale cached challenge by a second arm. Required raw RPC/all03/problems/full log/source DLL identity is saved before teardown, including failures. Native dialog/menu usability remains Claude-only pending. Validation and fresh results await coordinator review.
