## Current master refresh

Refreshed onto cc8f5555 after the open-floor trio passed native QA and merged. The original PR202 scope is retained: only Machines201201/201202 rewardLevel17 metadata and its existing QA profiles change payout behavior. No production formula or completion transaction is changed. Level50 extrapolation remains OURS, not a recovered retail curve.

The former Box picking/dependency HOLD is superseded for native QA. Complete the entire Poster Hunt chain using each current published Root/Route/Standing witness, then verify literal rewards at both level17 and50. Old raw positions and headless results below predate the shared resolver and are not new native or current headless results. Preserve all existing poster/safe diagnostic registrations alongside the two captured-reward cases.

# Observed archive reward reference levels

Only Machines 201201 **Disturbing Trends** and 201202 **Poster Hunt** now declare the already-supported `rewardLevel="17"`. Their stored reward amounts are unchanged. The production formula, completion transaction and every other mission reference remain unchanged.

## Evidence and interpretation

`research/missions/transcripts/Mach1-2-1.md:7` and `Mach1-2-2.md:7` identify TiAmo's visible HUD level as 17. The completion system lines record 54,871 XP / 45,339 information and 132,000 XP / 59,500 information respectively (lines 41/55 and 42/60). The second mission's separate 1,710 body-loot information is excluded.

These are archived replays from `[Archived]Agent Gray`; the actor is tagged Zion. Screenshots were taken after completion, and the transcript qualifies the reward association as very likely. This is observed **post-completion level-17 archive context**, not an independently recovered pre-award XP trace, normal-story difficulty, team multiplier or retail reward curve.

The change anchors the existing explicitly **OURS** scaling policy at that observed point. It does not establish archive/normal equivalence. Borrowed Hard Time 201203 and unknown-level Zion 101202 remain on the default reference 50. Other unknown-level definitions are not inferred from these neighboring captures.

## Exact economic change

Current policy floors XP by `(L-1)/(R-1)` with its existing level-1 guards, and information by `L/R`. The default R is 50; only these two definitions now use R = 17. This policy remains OURS.

| Mission | Actor level | Before XP / info | After XP / info |
| --- | ---: | ---: | ---: |
| Disturbing Trends | 17 | 17,917 / 15,415 | 54,871 / 45,339 |
| Disturbing Trends | 50 | 54,871 / 45,339 | 168,042 / 133,350 |
| Poster Hunt | 17 | 43,102 / 20,230 | 132,000 / 59,500 |
| Poster Hunt | 50 | 132,000 / 59,500 | 404,250 / 175,000 |

The level-50 increases are **material OURS extrapolations**, not recovered retail amounts. New tests use independent literal values for both levels and the previous default behavior. Existing caps, flooring and completion-time quoting before XP changes the level remain untouched.

## Normal earned payout fixtures

Discovered modes `captured-rewards-17` and `captured-rewards-50` use CSR 110, actual matching level/XP, Machines alignment, Mara start and the ordinary support loadout. Only the exact earlier Poster Hunt prerequisites are seeded; selected 201202 is never granted. The existing layout seed is 201202, with no GM, forced outcome RNG, injected item or forced completion.

Both headless cases run normal Poster Hunt through the shared list/accept/Open/exit/receipt driver. A minimal optional actor-level argument leaves the previous default level-50/XP-0 seed exactly unchanged; only these cases opt in to matching XP. Compatibility tests cover every serialized legacy seed field. The shared driver did not assume raw XML reward values, so no general quote rule was added.

After ordinary completion, each wrapper checks the actual `80a2` summary, literal XP/info values, persistent deltas and one earned receipt. Required raw RPC/03, selected DLL/configuration/hash and server evidence are saved before teardown; a separate payout file records the explicit comparisons. There is no direct completion repository call or payout injection.

**Native Box model/picking/placement remains unverified.** Existing Box proxies are authored substitutes and can use generic source markers. StillNpcs disables unsolicited aggro and test movement can cross walls. Headless success cannot prove native interaction, survival or walking. Disturbing Trends receives quote-unit coverage only: its separate source-1407 native action blocker remains unresolved; this package does not work around it.

## Claude-only native one-pass — pending

There is no claimed offer-screen quote. The witnessed UI surface is the ordinary **completion summary and system/log reward messages**.

1. Start `captured-rewards-17` with the actual level-17 profile. Call Agent Gray > Critical > Poster Hunt, accept, and use ordinary elevators and Open actions on the existing authored proxies (3, 1, then 4).
2. If any proxy is hidden or Open is unavailable, stop and record the blocker. Do not alter range, pose, seed or model to obtain a payout.
3. Complete and leave normally. Verify **132,000 XP and 59,500 information** in the completion summary/chat and one earned completion. This tests applying the observed archive point through the current normal-mission policy, not retail archive/normal equivalence.
4. In a separate `captured-rewards-50` fixture, repeat the same ordinary path. Verify **404,250 XP and 175,000 information**, explicitly labeled OURS extrapolation.

No normal native payout or retail parity result is claimed until separately recorded. Fresh testing waits for root's reviewed clean pin and slot grant.

## Local checks before fresh review

Full Release suite: **4,591 passed** in 59 seconds. Focused captured-reference, legacy-profile compatibility and M3 reward checks: **24 passed**. Both actual Debug/Release NativeQa builds, all four read-only plans and assembly provenance passed. Python snapshot policy: nine passed. No fresh result is claimed yet.

Checked hds hashes: Debug `075f4bad91425c37d584de7faf6a1411e9868f16d4bf2f3eb723a33b1e73702a`; Release `47ace9e5dbc4ada27926f52c5f5073edd5667a17a9b0f4ca319a529940472ec2`. Production hds source is unchanged from the package's base. Disturbing Trends and Poster Hunt are the only definitions whose reference metadata changed; borrowed and unknown-level reward tests pass unchanged.

## Fresh policy-payout evidence

Frozen source `ee6fabcddcc33bbbe2a8f4341ec12f72168205a0`, serial `--parallel 1`:

| Case | Duration | Result |
| --- | ---: | --- |
| `captured-rewards-17` | 28.6 s | PASS: normal summary 132,000 XP / 59,500 information, exact persistent deltas, one receipt |
| `captured-rewards-50` | 28.2 s | PASS: normal summary 404,250 XP / 175,000 information, exact persistent deltas, one receipt |
| Existing `poster-hunt-count` default profile | 26.4 s | PASS: unchanged default caller completes normally after the helper/data changes |

Each driver exited 0, peak parallelism was one, and every owned server disposed. Slot 1 was released. Source and binaries were frozen throughout all three calls. Actual selected configuration was **Debug**, hds SHA256 `075f4bad91425c37d584de7faf6a1411e9868f16d4bf2f3eb723a33b1e73702a`; this is not a Release fresh result.

The level-17 final totals were 9,692,000 XP and 60,500 information; level-50 totals were 296,486,750 XP and 176,000 information. Both new profiles retained 118 actual RPCs and 30 view lists with no parser problems. The default regression retained 118 RPCs and 28 view lists, also with no parser problems.

Required raw and literal payout evidence was saved before teardown:

| Evidence | SHA256 |
| --- | --- |
| `Temp/mxo-captured-rewards-17-mxotest_run_1_b87675aca2.json` | `f728293e3a98ce847f55aa0d681cab21e61ffeb40af000c7403a8da84d40c6fe` |
| Its `.json.payout.json` | `f2e4fd54b8ea02ed8e3449bd6e585fcc66231456da5dea2c01eb877244788891` |
| `Temp/mxo-captured-rewards-50-mxotest_run_1_f91a623f04.json` | `f9e4ecd861a259daec80e4d14315085343cae08a16fe868f5501dfed45e1e423` |
| Its `.json.payout.json` | `7fbee567759a20b639d6c8aaad4e0ab15400af959b1bf27caa5498235a0cbcdc` |
| `Temp/mxo-poster-hunt-mxotest_run_1_af2f06d9b9.json` | `6171e50a1aa19ef9a3ac497634ab14c095d92e800b9a76cd88d09bea58e1248e` |

Sibling debug logs and all file sizes/hashes are listed in `Temp/mxo-captured-reference-final-evidence.json`. Permanent stdout and explicit exit records use the `mxo-captured-reference-*` Temp prefix.

These results prove normal server policy application, not a retail reward curve or archive/normal equivalence. **The PR remains draft: native completion is blocked/unverified by the existing Box placement/picking limits.** Native payout visibility must be recorded separately through ordinary completion; do not force progress or alter geometry to obtain it. Disturbing Trends still has quote-unit coverage only, with its independent 1407 native-action blocker unchanged.
