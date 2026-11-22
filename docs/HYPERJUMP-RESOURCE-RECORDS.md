# Hyper-Jump resource records

An accepted flag-1 Hyper-Jump spends the 20 IS stored by its normal or beta ability at takeoff. The single 80 c3 echo and its jump view carry the resulting value. The stock takeoff report and ordinary cancellation both preserve that value. Ordinary flag-0 jumps spend no IS. A player needs the loaded ability and enough IS, and must be out of combat.

The retained `proxy_026_open_doors_and_some_hyper_jump_killed_by_Agent_etc` capture shows 304 IS before client sequence 567 and 284 in the matching start response, server sequence 473. Server sequence 1842 sends 284 in the response to a short flag-1 jump with peak 245.828125. In the Stamos up-and-down captures, the start and its cancel both report 227 of 247. See the [retail movement captures](../research/packet-dumps/community-intake/20260607T032533Z-hdneo-public-movement-spawn-hardline-sanitized-selected/logs).

Native start `80 c2` is exactly 41 bytes: XYZ double bits, sector/prefab/polygon u16, peak float bits, flag u8 and client tick sample u32. Cancel `80 c4` is exactly 32 bytes: XYZ double bits and three navigation u16 fields. It has no tick sample. The codec retains the raw floating-point bits.

## Primary evidence

Pinned client DLL SHA256 `cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a`; exact slices are in [the proof](../research/abilities/hyperjump-records-native-proof.json). Native start builder 6221ffe0 copies destination at +2, navigation at +0x1a/+0x1c/+0x1e, peak at +0x20, flag at +0x24 and `GetTickCount` at +0x25. Cancel constructor 6221e778 allocates 0x20 bytes and has no timestamp. Native response constructor 6213bf8c emits six-byte `80 c3` with u32 at +2; printer 62151fd0 names it TimeStamp.

The actual response dispatch table is 6217e9c0, entry (0xc3-0x0c), leading to 62176576. It reads +2 and calls 62077c50, which writes `GetTickCount() - sample` to global 629e2a74 every time. Import 6286808c is KERNEL32.GetTickCount. Thus repeated matching echoes are not idempotent: they resample elapsed time. Start emits one echo; cancel emits none and retains its existing movement response. No tracker, nonce, expiry or jump-ID authority is added. The unused stepped u16 jump ID code remains outside scope.

Positive capture: `research/packet-dumps/community-intake/20260607T032533Z-hdneo-public-movement-spawn-hardline-sanitized-selected/logs/mxopackets1_crashover2k_afterwhoruneo_proxy_003_abs_hyperjump_only_up_and_down.log.txt`, lines 1–21. Start tail `bf97be00`, intervening cancel tail `4f0314000500` (847/20/5), then one response `80c3bf97be00`. The cancel navigation tail is not a new timestamp.

## Limits intentionally unresolved

Native 62662e00 uses complete signed Super Jump total 567 to calculate configured default distance clamp(900+100*s,900,8000) and height clamp(300+400*s,300,20000), with float stores. Actual caller 6222093c uses complete getter 625c4e90, requires positive total, chooses normal 11 or beta 566 and checks native cost. Complete native base totals, configuration overrides, destination/peak geometry solver 6221f580 remain open. The current implementation charges the stored base cost. The native cost modifications remain unverified.

## Claude-only native check

Use discovered mode `hyperjump-resource-records`, consistent level 10/EXP with owned loaded Awakened 2 at 10, HyperSprint ability 184 at 1 and Hyper-Jump 11 at 10. At the outdoor Mara fixture, drag Hyper-Jump from Actions to a hotbar slot, activate it, aim and click a nearby visible outdoor landing point. Shipped string `0400057c` also documents default CTRL+Space for a forward jump; a user remapping may differ. Cancel an aimed jump normally and repeat after relog. Retain actual start, cancel, response and resource history. Verify each start has the matching single clock echo and cancel adds no fabricated echo. No claim of native rendering, fee or trajectory parity comes from headless records alone.

## Validation before the takeoff charge

Actual handler tests first failed on the preexisting `00 c3` header while resource and movement checks passed. The native literal is now `80 c3`; the RED log is retained in Temp/mxo-hyperjump-test-detail.log. Tests pin captured start/cancel records, raw NaN/sign/width bits without new validity policy, exact header/body lengths, wrapped/high clock samples, a single start echo and no cancel echo. Actual resource cases 0, 1, 284 and 65535 verify both unchanged state and the independent literal combined self-view mask's resource field. Existing cancel destination and timing behavior remains unchanged.

The registered fresh case uses ordinary protocol start/cancel requests, independent literal encoding and literal self-view decoding. It verifies current IS 100 is serialized, no new debit or literal overwrite, one full-width clock echo, cancel-only no echo, and repeats after a cold owned-server restart. It does not prove native input, rendering, configured limits or retail expenditure. Full local suite: 4,782 passed; final focused suite: 21 passed. Fresh passed at runtime/fixture pin `07e1493bf051296d24c06a061674d4e6643aef0d` in 25.9 seconds, exit 0, parallel 1. Both original and cold-relog sessions had 37 RPCs, 6 view lists and zero protocol problems. Actual Release DLL SHA256 `A51C400F214BFED436D07F0E2DC815396FD5C8EF743CF7D73078BEC5C88312A7`. Proof `mxo-hyperjump-records-mxotest_run_1_3942ceb9e9.json`, SHA256 `1A54212EC1F8779AA27DFB0CA95D854FC7D187FBD52008B78211240B6766DF88`. Samples FFFFFFF0 and 80000001 were echoed once, cancel/cancel-alone emitted no echo, and both combined resource fields were 100. Owned servers/database were disposed before publication.

Actual master `e239aa4ce7ca556caf1ccb508a76ff166d30a736` was then merged to resolve only scenario registration conflict. Both jump handler methods and all owned codec/packet/mode/driver blobs remain identical to the fresh pin. Combined 30 focused tests and both builds/read-only plans/provenance pass. The full 4,782 result is attributed to the pre-upstream fresh source; no fresh repeat was needed for unrelated refusal-feedback imports. Native validation remains pending Claude.
## Pre-charge master refresh (2026-11-15)

Frozen producer `69e55a8cab97416297fe791ad3b302c6ccf0685e` is refreshed onto master `93b5033d`. The scenario-list conflict was resolved by retaining both current Mobius registrations and the jump scenario. Ten focused tests and all 5,842 tests passed; actual Debug/Release builds, mode/default plans, provenance and exact-one discovery passed.

One fresh `hyperjump-resource-records` case passed in 51.7 seconds, held exit 0, including a cold server restart. High/wrapped clock samples `0xfffffff0` and `0x80000001` each echoed once; cancel and cancel-alone emitted no fabricated clock echo. Both sessions serialized current IS 100 and had no parser problems. One separate actual mode bootstrap passed in 34.41 seconds, held exit 0 (the inherited no-op hook on an authenticated live server). This is not a full current-mode matrix or native UI result.

Actual Debug source SHA256 `BB53C879302FC6FEA95EB548ADECC5595E4A791E92E4FE0CB3627962B4B90EAA`; Release driver `9BC7D73674B12BFD40CC7C9E7ADE389F5187C41C3101FB3B31A327EAC36F4568`; Release NativeQa `16C6BA013C563D12A15635C4FB48C1016569234707520D8AB08E877052297E2A`. Raw proof SHA256 `D221FE218FEAE0CA90DD1951BC159F88B7222EBF5CA1BB23EBBF316959938FB4`.

Proof and independent cleanup are under `Temp/mxo-pr-triage-20261115`. Exact schemas `mxotest_run_1_6512090488` and `mxotest_bootstrap_b2b0354d1c` are absent, and all 6 + 8 recorded creation identities are gone with no PID reuse. Polling cannot establish complete short-lived process history. No real client, client lock or main-database reset was used. Claude still qualifies native Actions/hotbar, aiming, cancel, relog and rendering; retail fee and trajectory parity remain unproved.

## PR689 full-peak timing comparison

The failed 1250/18/1 attempt (SSEQ 156) agrees with retail on the 82 time stamp, view 2, 03 01 08 00, mask 80 80 bc, 10300 peak, the exact requested XYZ and the echoed 32-bit tick. Server jump flags are 4 in this frame and in proxy_024, proxy_026 and session 009. Niobe has flags 2 for its separate sample. Available IS is the actual character value, 500 in the fixture and 284 or 374 in these captures. In every response the field order is peak, flags, end time, destination XYZ, available IS, then the ordered clock echo.

| Capture | Datagram stamp (s) | JumpEndTime (ms) | Remaining time (s) | Horizontal distance (m) | Distance/time (m/s) |
|---|---:|---:|---:|---:|---:|
| PR689 SSEQ 156 | 108.5782 | 117742 | 9.164 | 38.50 | 4.20 |
| proxy_024 SSEQ 30 | 87106.4375 | 87116339 | 9.902 | 78.46 | 7.92 |
| proxy_026 SSEQ 2033 | 89282.9219 | 89291808 | 8.886 | 70.53 | 7.94 |
| niobe3dualduality SSEQ 84 | 135158.8125 | 135168600 | 9.788 | 78.21 | 7.99 |
| session 009 SSEQ 1911 | 8326.6953 | 8334668 | 7.973 | 63.54 | 7.97 |

Retail takeoff coordinates are approximated from the immediately following 03 09 08 self update, which carries the in-flight position and sim time. Those samples can be a fraction into the jump. All four have that subsequent update. The timing candidate changes only the full-peak travel timer to horizontal distance at 800 units/s and retains the established short, vertical and cancel paths. The target at 38.5 m now gets about 4.813 s, the 41.5 m target about 5.188 s. The timing candidate still reproduced the instant cancel at x=17101 after SSEQ 156.

## PR689 positional self-view candidate

All four captured full-peak starts above have a following server view update with header `02 00 03 09 08 00`, float32 XYZ, and a uint32 simulation time in milliseconds. Its seven masks include all 48 self-view attributes. The two name fields are byte-length-prefixed; peak, flags, end, destination and available IS repeat the earlier delta. In the failed PR689 attempt the first positional self update was the cancel response, sent after the stock client had already sent `80 c4`.

This candidate adds the full positional self view after the existing `03 01` start delta in the same timed view list before the `80 c3` echo. It captures the player's actual takeoff position and admission clock, uses the live self-view fields, and leaves the previous timing, short-jump and cancel paths in place. The capture sends the positional view in a following datagram. The combined reply tests its presence before the reported immediate cancel.

## PR689 takeoff reports and actual cancels

The stock retest at 8bfe176f sent 80 c4 with the takeoff polygon and x+1 on three forward starts. Rank 10 landed at 20950 when the snap-back reply preceded the combined jump reply. Both rank-11 starts stayed at their takeoff positions when the snap-back reply followed it. Native rank 10 put 80 c2 and 80 c4 in the same incoming datagram. A rank-11 sample put them in separate datagrams within one second.

The matched successful full-peak retail starts above have no immediate 80 c4 at takeoff. Three have a later 80 c4 near the destination, after roughly the jump duration, with no immediate own-view 03 09 reply. The retail stream also contains actual mid-flight interrupts: proxy_026 CSEQ 2559/2561 received SSEQ 2442 with flags 0x14, a current airborne position and a route to the requested ground point. The Niobe CSEQ 178/181 pair likewise received an airborne response changing its destination, with flags 0x12. Both are evidence for retaining an interrupt path. They do not establish what every 80 c4 means.

For the reproduced stock case, suppress a 80 c4 reply only within one second of a flag-1 full-peak start on mapped ground, in the same world/life, with the exact takeoff polygon and within four centimetres of the original XYZ. Keep the assigned destination and its one clock echo. Different coordinates, unmapped origin, a different polygon, or a later request retain the existing cancel path. The headless fixture replays both bundled and separate stock takeoff reports. The full snapshot also uses the shipped ordinary RSI selector when the server field has not been initialized.

## Active full-peak redirects

In the Wan Chai proxy_026 pair, the initial end time is 90429206 ms. The C4 response has an airborne origin at (31487.092,115.519,16335.705), simulation time 90421345 ms, flags 0x14 and end time 90429095 ms. This keeps 7.750 s of the original 7.861 s remaining, even though the new horizontal destination is only 0.96 m from that origin. Niobe's CSEQ 181 response changes the destination with flags 0x12 and an end time 5 ms before the original deadline.

The server retains the original deadline for a full-peak redirect. It estimates an airborne origin by moving along the accepted route with a parabolic vertical offset. The stock interpolation and the reason for the small retail deadline adjustments remain open. The response preserves the three double-precision coordinates from C4 and the current IS, with no second clock echo. The disposable rank-19 Wan Chai fixture replays the original C2/C4 headlessly. The dev command `?jret` feeds that C4 during a stock-started flight to test client presentation.
