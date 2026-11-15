# Preserve a V1 mission site after relog

Legacy world CSV data can contain several poses for the same static door key. V1 restoration previously selected the first world/sector/key match, so an earned 101210 carry relog changed its site door from 54200/2945/-16700 to 53200/2945/-17700 while keeping the original waypoint and exit.

Restoration now matches the complete captured raw door using the existing source fingerprint: world, sector, key, GO/type, static ID, exterior flag, XYZ, rotation and quaternion. It neither moves the door nor rewrites waypoints, exits or mission progress. If the last saved/current door is missing, the existing typed restore quarantine retains the durable payload and owned item slots; an older history site cannot become current. Missing older history retains the legacy omission rule when the exact current door is available.

V1 has no cluster fingerprint. When multiple clusters share the identical raw door, their existing first-match order remains; this change does not claim complete V2 identity or native room/accessibility validation. The retained 86da run earned 101208 and 101209 before exposing this 101210 restore mismatch. Existing V1 persistence coverage and a verbatim earned checkpoint exercise the correction.

Native one-pass mode: `mission-v1-site-restore`. It starts outdoors near the preceding ordinary exit, with a level 50 CSR, normal owned abilities and full current template health/IS. It seeds the 29 earlier receipts through 101209 only, including 101208/101209; no active mission, mission item, selected/future completion or reward is seeded. Layout seed 101208 is a disclosed fixture aid. No GM, combat-roll override or mission-floor teleport is used.

1. Call Tyndall > Critical > **Interview with a Bluepill** and accept normally. Use the ordinary waypoint door and Talk to Maynard; confirm he follows. This authored mission uses Talk/Escort, not a courier Get.
2. Before completing the escort, jack out normally. Allow 33 seconds for the old reservation to release, then relog. Verify the waypoint and current floor door stay in the same place; walk/hover the door before using it.
3. Lead Maynard to the ordinary exit, ride down, follow the new waypoint through the ordinary door, bring him to Lonny and Talk. Record any room, hover or walking failure separately; the restore identity fix alone does not establish native accessibility.

Launch the frozen producer through the normal NativeQa command with mode `mission-v1-site-restore`; use the producer commit and source hashes from the package freeze. Only Claude performs native/client testing. The inherited bootstrap hook does not execute this playthrough.
