# Close the login reader on every exit

`SetPlayerValues` previously closed its reader and thread-local connection only after every database field mapped successfully. A query or conversion exception could leave the provider-backed reader active and poison subsequent work on that worker. The method now owns a local reader and closes it in `finally`; a nested `finally` closes the connection even if reader cleanup throws. Other mapping formulas, health policy and the native-safe persisted-heading fallback are unchanged. This splits only the cleanup from reference commits `e3ceed03` and `5deb55e7`; it does not copy their old Medium01 documentation.

## Error proof boundary

The shipped schema declares `characters.healthC` as signed MEDIUMINT. A disposable row with health-1 and valid yaw192 passes schema insertion and yaw mapping, then the existing decimal-to-UInt16 health conversion throws in initial UDP login. This is a **provider-backed mapping exception**: the real MySql reader is active, but `Decimal.ToUInt16` throws, not the provider itself. No clamp, repair or new malformed-row policy is introduced.

The headless regression seeds that row before initial UDP and requires its full owned-char Overflow/UInt16/SetPlayerValues/processInitUDPSession stack. The failed `HeadlessPlayer.LoginAsync` path already disposes its world and margin sockets on cancellation; no partially constructed player is retained. The negative row must remain-1 and offline. An ordinary healthy actor then logs in, quits through80fe/80fd, waits normal reservation cleanup and relogs on the **same fresh server**. There must be no already-in-use error. Threadpool scheduling is not forced, so the test does not claim a particular OS worker was reused; unit tests prove unconditional cleanup directly.

The generic test harness acknowledges only the exact already-observed diagnostic line **and count**, after socket disposal and stack verification. Another character, another exception, or another identical occurrence beyond that count still fails. There is no global exception suppression or mode-named bypass. Raw failure and success output is retained before teardown.

## Native checklist — Claude only

Discovered mode `login-reader-cleanup` seeds normal valid CSR110 at Mara `(23828,495,13984)`, heading126, health300/300, abilities2/17. It has no dev/GM permissions or forced rolls. **No malformed actor is used natively.**

1. Log in normally and confirm Mara, self, health and Actions render.
2. Quit through the ordinary UI, wait the normal reservation cleanup, then log in again.
3. Confirm normal self/world entry and absence of connection-already-in-use errors. This is a valid-login native regression; only the disposable headless case exercises the malformed row.

Focused23 and full4328 unit tests pass; after an upstream-only master170/173 import,50 focused cleanup/heading/fixture tests pass. Debug/Release builds and actual read-only plans/provenance pass; Python12 audit tests pass. Provider-backed diagnostic retry passed in79.5s, exit0, peak1, with normal disposal. It observed exactly one expected UInt16 mapping overflow, unchanged negative row/offline state, healthy login/normal quit/33s cleanup/relog and no already-in-use errors. All captured client-problem stages were empty. The successful fresh retry preceded that upstream-only world/profile import; the cleanup method and named plan are unchanged, with both builds/plans/provenance refreshed afterward. Native QA is pending. No main database, char2, client lock, native setup, settings or cleanup operations are part of this package.

The initial fresh attempt (80.1s) verified the expected mapping stack and healthy login/logout/relog but failed on one healthy-session wire diagnostic whose text was absent from the old count-only assertion. Its raw failure is retained. Stage-specific exact problem capture and joined assertion messages were added without suppressing client problems or changing production behavior; the coordinator-authorized diagnostic retry passed with empty problem lists before quit, after quit, before/after disposal and on/after relog. The first count-only wire diagnostic remains unexplained; no parser or lifecycle fix/suppression is claimed.
