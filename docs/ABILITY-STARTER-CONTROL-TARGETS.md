# Starter Hacker control selection

Code Freeze 1.0 (40), Disrupt Inputs 1.0 (43), and Freeze System 1.0 (48) use the shipped hostile selector 3. A baseline replay cleared the target handle on each captured action. All three still started a two-second cast and their own reuse. The updated handler checks for a live attack selection and the current spawn before touching cast state. An invalid selection receives the single stock reason 24, “Target is not valid.” Range, line of sight, contest and landed effects still use the normal resolver. [Shipped catalog](../data/ability_catalog.json), [native refusal table](../research/abilities/ability-refusal-consumer-evidence.md).

The fixture `ability-parity-starter-hacker-control-targets` loads the exact level-14 prerequisite closure at rank 12, using 37 of 44 memory. It replays the full stock requests from `6971d24474bd` with only the destination handle replaced. Use Hacker Style out of interlock, clear selection, and try each action. A request that reaches the server should give one invalid-target line and preserve Inner Strength and reuse. Select Melee Dummy and retry immediately.

| Ability | PacketLog line | Selected control | Own reuse |
| --- | --- | --- | --- |
| Code Freeze 1.0 | 4113 | Rooted for 25 seconds | 58 seconds |
| Disrupt Inputs 1.0 | 5109 | Blinded for 16 seconds | 43 seconds |
| Freeze System 1.0 | 4933 | Pacified for 6 seconds | 43 seconds |

The disposable also checks a stale spawn for each ability. The grouped sample covers initial target admission and the positive control. The wider range and contest cases remain open.
