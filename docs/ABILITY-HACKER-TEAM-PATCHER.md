# Hacker Team Patcher parity

The `ability-parity-hacker-team-patcher` profile groups four heals and the Total Dedication passive in a 62-memory loadout. The fixture holds natural HP regeneration so the initial 100 HP wounds persist. The loaded ancestors provide +15% Healing and +24% friendly area radius.

After stock CSR is fully in world, stay on the seeded origin. Use helper chat to move each actor from the off-site staging position. For example, send `{"actor":"QAFriend","action":"chat","text":"?tp 25400 495 -1498 0 0 0"}`. Move the other helpers to the coordinates below using the same action.

| Helper | Stand | Role |
| --- | --- | --- |
| QAFriend | 25400, 495, −1498 | Teammate at 6 m |
| QAFar | 24800, 495, −698 | Teammate at 8 m |
| QAEnemy | 24800, 495, −2498 | Friendly teammate at 10 m despite the helper name |
| QAOther | 24560, 495, −1318 | Nonmember at 3 m, clear of the NPC stack |

Recruit the three teammates through the stock Mission Team radial. After each invite, submit `{"actor":"QAFriend","action":"accept","inviter":"CSR_Tester"}` with the actual nominee. Leave QAOther out of the team and select that nonmember. Keep Self Defense active.

| Candidate | Required stock observation |
| --- | --- |
| 50 | +12 HP on CSR and QAFriend only. Effective radius 7.44 m. |
| 434 | +36 HP on CSR, QAFriend and QAFar. Effective radius 8.68 m. |
| 435 | +34 HP on CSR and all three teammates. Effective radius 11.16 m. |
| 437 | Move QAEnemy to 24800, 495, −2698 (12 m). Observe +141 HP on CSR, QAFriend and QAFar. Neither QAEnemy nor QAOther receives a cast heal. |
| 433 Total Dedication | Record source 433’s +10% IS Regeneration Bonus and +5% Healing Bonus. Use `?qaunload 433` and `?qaload 433` and retain the matching modifier remove and add records. |

Wait for family reuse after each cast where applicable. Staggering the three teammate positions tests different radii without overlapping their pick volumes. Capture the outgoing RPC and health deltas for each expected and excluded recipient. Use the exact source records to identify 433’s contributions if the Status tab groups them with other values.

The fixture stages five stock observations. PR #339 provides the executable-only checks needed for their audit closures.
