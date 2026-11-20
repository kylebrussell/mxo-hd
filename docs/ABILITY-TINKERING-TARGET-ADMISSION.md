# Tinkering hostile target admission

Tinkering (36) and Advanced Tinkering (514) both use GOB selector CastDirectObjects 3 with CastValidTarget 3, a 16 m selection range, a 2 m target-centered effect, TestFunction 2 and a six-second cast. The selected hostile object supplies the area center.

The PR399 stock run showed an Advanced Tinkering request with view and spawn both zero at 19:10:53 ET. PacketLog 3c501e2874a7 lines 3073–3076 preserve the full request. The server sent the cast bar at line 3087 and began the 30-second reuse at 19:10:59 on line 3245. That cast produced no landed hit. The next attempt, with QAEnemy selected at 19:11:42, landed for 6 with the usual cast and reuse. [PR399 tested head](https://github.com/kylebrussell/mxo-hd/pull/399)

Both abilities now require a live attack target at admission. A full stock handle has to identify the current spawn. An absent, stale or dead target produces a caller-only notice before the cast, Inner Strength debit and reuse. Range, line of sight, PvP permission and the viral contest still run through the normal landing checks.

The disposable `ability-parity-tinkering-target-admission` mode replays the exact PR399 view-zero request. It derives a Tinkering view-zero request from PR389's selected capture. For each ability it also sends a stale spawn and immediately retries with the live selected handle. Preserve the notice, absence of a debit/bar/reuse for invalid inputs, and the ordinary six-second landing and 30-second reuse. Stock retest confirms the notice and immediate retry. The targeting audit remains open for wider admission and landing evidence.
