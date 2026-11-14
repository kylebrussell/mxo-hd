# Mission level rewards: explicit OURS policy

Mission XP and information now scale with the player's **completion-time level, captured before granting XP**. The same immutable payout pair feeds80a2 summary,80e6/80e5 XP and80e4 information persistence/presentation. This implements level-sensitive rewards, but the retail server formula remains unknown. The formulas and fallback reference below are explicitly **OURS**, not reverse-engineered retail math.

## What the evidence actually says

Run `python research/missions/inspect_reward_scaling.py`. The checked-in research script locates each exact Welcome1201 completion packet from [capture-missions.json](../research/missions/capture-missions.json), verifies80a2 against its indexed values, and verifies adjacent framed80e6 gain/80e5 total (gain type2). Subtracting the reward from the total gives the exact pre-reward XP. Native625d8590/625d8610 and [PlayerProgression.cs](../hds/world/Structures/PlayerProgression.cs) establish the level thresholds.

| Capture / completion line | Difficulty | Pre-reward XP / level | XP reward | Info reward |
|---|---:|---:|---:|---:|
| luizoe Recursion,42223 |2|4318 /2|5500|21000|
| luizoe Recursion_Char,50887 |2|7512 /3|11000|31500|
| luizoe Unknown,50680 |2|6597 /3|3664|21000|
| crashover2k create_char_devoted,60926 |1|892 /2|3000|21000|

Full repository-relative source paths are emitted by the script. The3,000 and5,500 samples were **both level2**, with different difficulty. They cannot establish a level-only curve. Likewise the two level3 samples differ, and accept-time level/team/split/difficulty effects are not fully recovered. The chosen policy matches the5,500/21,000 and11,000/31,500 same-difficulty examples; it does not explain3,000 or3,664. No missing team or difficulty multiplier is invented.

Bounded searches of shipped mission scripts (Mission185/Mission986, missionvalidate and rshooks), native mission/reward string references and GOB mission/reward field names did not recover a mission XP/info producer. rshooks.RunMissionRewards decodes/applies an already supplied reward, not this level formula. The client's resource/missions directory contains sponsors.xml rather than server mission definitions. Other reconstructed missions have archive or authored baseline rewards; those are not evidence of a retail reference level.

## Policy and metadata

For level L clamped1..50 and reference R in1..50:

```text
XP = floor(max(0,baseXP) * max(1,L-1) / max(1,R-1))
info = floor(max(0,baseInfo) * L / R)
```

Arithmetic uses64-bit intermediates and caps each award at signed32-bit maximum (the legacy database column range; the wire field is unsigned32-bit). Missing/invalid `rewardLevel` selects the explicitly named `MissionRewards.DefaultArchiveReference = 50`: an **OURS tuning baseline**, not an inferred capture level. That preserves the authored base at50 and prevents large archive rewards being multiplied from an assumed level1 baseline. It scales all legacy/generated missions down at lower levels. Positive tiny rewards may floor to0; negative authored amounts become0. The cap bounds an individual award, not existing account totals; aggregate signed-column overflow remains a separate legacy constraint until the completion/persistence work handles it. Existing total-XP/info account handling remains in PlayerRewards; no refund, team share, per-phase award or transaction semantics are inferred here.

The468 checked-in definitions have only three declared level gates: Hothouse30, Deep Cover40 and Courtship50. They now explicitly author `rewardLevel` to those gates. Using a gate as the reward baseline is OURS; Hothouse's reward transcript independently identifies a level30 player, while the other two reuse that baseline amount in existing reconstruction. Welcome explicitly uses2. Other definitions without supported reference metadata use the default archive reference50. Machines201201/201202 now explicitly anchor the observed post-completion archive HUD level17; their higher-level application remains OURS extrapolation (see MISSION-CAPTURED-REFERENCES.md). Their unknown retail reference levels remain a parity gap. The largest unanchored bases include Disrupted Connection440,000XP, Incommunicado437,160 and Exodus/Grooving Up Slowly423,360. M4 chain authors should supply an explicit `rewardLevel` with evidence or label their chosen baseline OURS; never infer it from the reward amount alone.

## Completion ownership and validation

MissionRuntime already serializes completion through missionSync and clears the active mission after payout. This change computes one pure `MissionRewards.Payout` before XP can change the level; it does not independently add a receipt or persistence transaction. M1's durable mission-completion work owns that atomic receipt seam and must commit this same pair with completion state, ticket exchange and snapshot deletion. No exactly-once crash guarantee is claimed for the pre-M1 separate legacy saves.

Units cover capture anchors, integer flooring, nonnegative values, saturation, clamped levels, default50 preserving baseline/monotonic bounded scaling, XML opt-in and real mission reference metadata. Fresh scenarios complete actual Welcome at2/3 and Hothouse at30/31, checking phase objectives, summary, mission XP/info records, database deltas, repeated completion-trigger movement/actions and post-completion relog after the ordinary UDP inactivity cleanup. The actual80fe00/80fd quit handshake clears the database online flag, but the current server retains its margin character reservation until WorldClient.Alive times out after30s and ViewVisibleThread removes it; the fixture waits33s rather than bypassing that lifecycle. They use existing ordinary mission actions and dev `?weaken 1` only to shorten combat; no reward/combat RNG is overridden. Verification results belong to the exact PR head, not a retail parity claim.

## Claude-only one-pass native checklist

Use discovered `mission-rewards` profile (actual server/client level2, XP500) then `mission-rewards-higher` (level3, XP5000), each a fresh disposable native fixture. Read-only plan/state metadata lists expected mission1201 reward. Both have dev commands only for existing teleport/weaken preparation, no GM or forced callback rolls; rewards are deterministic.

Open Tyndall in the mission contacts window, select Critical Mission **Welcome to the Matrix**, and accept. Near the mission NPCs use right-click/radial **Talk** on Abalakin, Rigel and Crow, finishing all three objectives. On Watchman use right-click **Close Combat**; optional `?weaken 1` before fighting reduces the existing target HP without completing an objective. Finish it through ordinary combat, confirm the kill objective closes, then leave through the mission elevator/waypoint. Verify the actual summary and chat: level2 gives5,500XP +21,000info; level3 gives11,000XP +31,500info. The level2 XP award can level the character up, but its information award must stay21,000. Repeated radial actions/exit movement and relog must not grant a second reward. Use actual radial paths; do not rely on a fabricated force-complete command. Native UI verification and merge are Claude-only; Codex never starts NativeQa setup or a real client.
