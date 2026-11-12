# Direct door diagnostic profiles

`large01-door` and `large02-door` start directly beside existing native doors so native QA can inspect ordinary radial **Open** and natural closing without a long or blocked approach. This diagnostic start is **not proof of a walking route**, interior access from the hardline, geometry completeness, or Loading Area UI return. The ordinary `large01` and `large02` profiles, world data and server runtime are unchanged.

| Profile | Existing source key / GOID | Door position | Diagnostic standing position | Actual NAV |
|---|---|---|---|---|
| `large01-door` | map12, sector136/local1, static `0x08800001`, GOID365 |12900,145,15600|12900,95,15500|sector136, region70, polygon25; floor0, indoors|
| `large02-door` | map13, sector27/local669, static `0x01b0029d`, GOID414 |14900,145,6400|14900,95,6300|sector27, region93, polygon313; floor0, indoors|

Both keys are unique, retained in the existing static exports, absent from every excluded-key set, and originate in placement rotation0 with identity quaternion. Standing positions were checked through the existing compiled NAV implementation against the shipped NAV data; they are 100 units horizontally and50 vertically from their doors. Player heading is explicit protocol byte0 (not a radian value).

The stored GOIDs use AttributeClass364. `object_usables.csv` offers Open/use3 with4004 DoorOpenGeneric and max distance1000. `object_states.csv` starts Closed `0x835` with Open enabled. Open reaches `0x834`, naturally advances to Closing `0x838` after20 seconds, then Closed `0x835` one second later. Neither source object provides a manual Close usable. The checklist must not invent one. These diagnostic profiles add no dev/GM permissions, forced random rolls, door state commands, or new engine behavior.

## One-pass native checklist — Claude only

For each profile, use the discovered mode name with the existing NativeQa workflow. The read-only plan exposes the world, exact static key/GOID, standing point, heading and diagnostic limitations.

1. Log in normally. Confirm the nearby door and the expected interior render; report absent geometry or missing interaction instead of assuming a pass.
2. Right-click that door and choose the ordinary **Open** action. Confirm it opens.
3. Wait without another command. Confirm natural closing after about20 seconds and Closed about1 second later. There is no sourced manual Close menu item.
4. Record this as a direct door diagnostic only. Do not promote it to a walk-route or LA UI return pass.

The two `*-world-door-diagnostic` headless cases share one helper and perform an ordinary login, normal static `80 c8` Open3, exact-source door creation with state834, then actual838 and835 wire updates. They save each success server/debug log before standard disposable-server teardown. One runner can execute both sequentially using `--filter world-door-diagnostic --parallel 1`. It needs an explicit coordinator grant and sufficient physical free space.

## Verification status

**DRAFT / NOT READY.** Both builds and the actual read-only plans/provenance pass. The37 initial focused tests passed; after importing master161,49 focused profile/source/NAV/contract/legacy fixture/door/heading tests pass. The12 Python audit tests pass. Fresh verification passes: Large01 case41.4s, Large02 case50.2s; one serial runner reported2passed/0failed in91.5s, exit0, peak1. Each case observed ordinary login, exact-source Open3/state834, natural838 then835, and saved its raw server/debug log before disposal. The fresh run preceded the upstream-only master161 import; door mode/scenario source and both actual plans are unchanged across that merge, while focused tests and both builds/provenance were refreshed. CI and native QA are pending. No existing native-world PASS is claimed for these new diagnostics.

When capacity permits, build/test with the four coordinator-approved command-only MSBuild hardlink properties; do not change project/user/OS settings or remove existing outputs. Inspect physical free space and output hardlinks. Standard headless execution uses a Temp working directory/disposable database with read-only data from the built directory; it does not copy all data. Freeze source/binaries for the two-case fresh run, retain both raw success logs, then report exit code and disposal.
