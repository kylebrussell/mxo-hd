# Hyper-Jump resource records

This package preserves current server resource state when emitting jump movement. It does not add a jump fee or claim retail fee parity. The active movement handlers do not debit Inner Strength; ordinary ability activation can separately debit its existing cost. The previous response overwrote available IS with literal 284. This overwrite is removed, while the existing movement attributes continue to serialize the current value.

Native start `80 c2` is exactly 41 bytes: XYZ double bits, sector/prefab/polygon u16, peak float bits, flag u8 and client tick sample u32. Cancel `80 c4` is exactly 32 bytes: XYZ double bits and three navigation u16 fields. It has no tick sample. The typed codecs retain all raw floating bits and full widths; strict record/body lengths refuse malformed requests without introducing new geometry admission rules.

## Primary evidence

Pinned client DLL SHA256 `cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a`; exact slices are in [the proof](../research/abilities/hyperjump-records-native-proof.json). Native start builder 6221ffe0 copies destination at +2, navigation at +0x1a/+0x1c/+0x1e, peak at +0x20, flag at +0x24 and `GetTickCount` at +0x25. Cancel constructor 6221e778 allocates 0x20 bytes and has no timestamp. Native response constructor 6213bf8c emits six-byte `80 c3` with u32 at +2; printer 62151fd0 names it TimeStamp.

The actual response dispatch table is 6217e9c0, entry (0xc3-0x0c), leading to 62176576. It reads +2 and calls 62077c50, which writes `GetTickCount() - sample` to global 629e2a74 every time. Import 6286808c is KERNEL32.GetTickCount. Thus repeated matching echoes are not idempotent: they resample elapsed time. Start emits one echo; cancel emits none and retains its existing movement response. No tracker, nonce, expiry or jump-ID authority is added. The unused stepped u16 jump ID code remains outside scope.

Positive capture: `research/packet-dumps/community-intake/20260607T032533Z-hdneo-public-movement-spawn-hardline-sanitized-selected/logs/mxopackets1_crashover2k_afterwhoruneo_proxy_003_abs_hyperjump_only_up_and_down.log.txt`, lines 1–21. Start tail `bf97be00`, intervening cancel tail `4f0314000500` (847/20/5), then one response `80c3bf97be00`. The cancel navigation tail is not a new timestamp.

## Limits intentionally unresolved

Native 62662e00 uses complete signed Super Jump total 567 to calculate configured default distance clamp(900+100*s,900,8000) and height clamp(300+400*s,300,20000), with float stores. Actual caller 6222093c uses complete getter 625c4e90, requires positive total, chooses normal 11 or beta 566 and checks native cost. Complete native base totals, configuration overrides, destination/peak geometry solver 6221f580 and server fee timing are not closed. This package does not implement those caps, change trajectory/end-time physics, beta selection or admission policy.

## Claude-only native check

Use discovered mode `hyperjump-resource-records`, consistent level 10/EXP with owned loaded Awakened 2 at 10, HyperSprint ability 184 at 1 and Hyper-Jump 11 at 10. At the outdoor Mara fixture, drag Hyper-Jump from Actions to a hotbar slot, activate it, aim and click a nearby visible outdoor landing point. Shipped string `0400057c` also documents default CTRL+Space for a forward jump; a user remapping may differ. Cancel an aimed jump normally and repeat after relog. Retain actual start, cancel, response and resource history. Verify each start has the matching single clock echo and cancel adds no fabricated echo. No claim of native rendering, fee or trajectory parity comes from headless records alone.

## Validation boundary

Actual handler tests first failed on the preexisting `00 c3` header while resource and movement checks passed. The native literal is now `80 c3`; the RED log is retained in Temp/mxo-hyperjump-test-detail.log. Tests pin captured start/cancel records, raw NaN/sign/width bits without new validity policy, exact header/body lengths, wrapped/high clock samples, a single start echo and no cancel echo. Actual resource cases 0, 1, 284 and 65535 verify both unchanged state and the independent literal combined self-view mask's resource field. Existing cancel destination and timing behavior remains unchanged.

The registered fresh case uses ordinary protocol start/cancel requests, independent literal encoding and literal self-view decoding. It verifies current IS 100 is serialized, no new debit or literal overwrite, one full-width clock echo, cancel-only no echo, and repeats after a cold owned-server restart. It does not prove native input, rendering, configured limits or retail expenditure. Full local suite: 4,782 passed; final focused suite: 21 passed. No fresh run has yet been authorized for this package.
