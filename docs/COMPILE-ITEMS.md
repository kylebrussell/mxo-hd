# Compile Items (969)

Compile Items now owns a server session bound to the activating Code Compiler Tool's bag slot. Opening the UI never compiles or consumes anything. Both existing execution paths (`60` stored-code materialization and `81 2e` recipe assembly) require that same live session and tool. The Loading Area's separate `60` path still works without an in-world session.

## Primary contract

The shipped string table's `010000d9` says to use a Code Compiler Tool to compile an item from an item code in central storage. Reproduce by reading `resource/string_tables/stringtable_client.sta` with `mxo-research/tools/client-data/sta.py` and `rezmap_ltb.read`; title `0100008a` is Compile Items. These are the `Info/MoreInfoID` and `Info/InfoID` in `data/ability_catalog.json`, resource969 `CompileActivateAbility`.

The catalog stores `RequiresTool`, `UsesUI`, `NonCombatOnlyAbility`, `IsAToggleAbility`, `AbilityIsExclusive`, `IsInterruptable` as1. Its while-enabled duration-type2 modifier targets attribute968, with no numeric bonus, cast time, IS cost or recast field. Parent277 (`CompileAbility`) grants969. `ClientRequiresTool` keeps this activation out of the ordinary Actions ability list; activate compiler1271 from inventory. The session does not manufacture a second recipe executor or an IS cost.

Binary evidence below is from client.dll SHA256 `cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a`. Reproduce read-only with `mxo-combat-inspect.py dis ADDRESS SIZE`:

* `621ed3d7..621ed3e3`: item activation calls `621ecf10(ability,1,toolSlot)`.
* `621ecf26..621ecf3d`: the generic ability request stores that flag and tool slot. `621e5129..621e5138` serializes tool at packet+0x25 and flag at+0x26, body offsets35/36 in the41-byte `80 b9` body. The consumable-slot parser cannot parse close: it intentionally only recognizes flag1.
* `620806f0..62080738`: Compile-window close looks up `(969,968)`, tests its active state, then calls the same sender with `(969,0,window.toolSlot)`. Therefore start1 is idempotent, close0 ends only the matching slot; a repeated start is not a toggle-off.
* `621e123c..621e1276`: an on linked modifier with attribute968 calls `621d0ad0(sourceAbility,pendingToolSlot)`. `621d0ad0..621d0b58` explicitly distinguishes969 from970, opens the corresponding UI and stores the tool slot in compiler+0xc1 or writer+0x145. This is why969 uses `80 bc 1d` linked968, with source969, and the normal `80 bd 05` exclusive marker. Record IDs come from the shared session allocator.
* `620836b0..6208378c`: the compiler button sends `60`, decoded stored goid, archive slot, personal-inventory0, tool slot and FromCrafting1. This is distinct from `81 2e` recipe assembly.

`ability/hacker/crafting.py` has separate Compile subject/deactivate callbacks: subject plays stand-type idle; deactivate restores standing idle only if still in that stance. Native UI and animation acceptance remain a native check, not a headless claim.

## Authority and cleanup

`CraftingSession` serializes start, close, validation and executor authority on `ClientData.craftingSync`, shared with both crafting handlers. It snapshots the district and world generation. Every execution rechecks ownership of compiler1271 in the exact slot, loaded969 (including277's grant), in-world/non-LA/alive/noncombat status and ability control gates. A different compiler slot cannot borrow a session. Invalidating the original tool ends it instead of allowing a later replacement request to resurrect it. The writer970 shares this state with its own tool531 binding; switching modes ends the old record before starting the new one. Both sourced scripts use typing stance3, which restores standing on cleanup only if still typing.

Matching native close, another ability request, entering combat, positive health damage, death/control clear and hardline/LA transitions send the linked record off and toggle off once. The view tick checks tool loss, unload and control/world eligibility even without another compile request. A new world clears stale state without replaying the prior world's records. Reconnection creates fresh ClientData. Interruption on another ability request and positive damage is the server's conservative implementation of exclusive/interruptable; exact retail interruption ordering and movement cancellation remain unmeasured. No movement restriction or invented movement-cancel packet is added.

`?compilesession [off]` reports/revokes the actual session only with exact `MXO_DEV_COMMANDS=1`; it cannot grant a tool, load an ability, start a session or compile an item. Existing `?compile` remains a separately gated deterministic QA shortcut; it is not the native activation path.

## Remaining economy boundary

The tooltip's primary item-code/equipment materialization is **still incomplete**. The existing `60` executor only materializes owned stored bits/fragments under its documented local deterministic policy; ability-code copying, equipment codes, full archive success/charge/purity rules and XP are not reconstructed. The existing `81 2e` implementation assembles known recipes with its script-backed contest and cost. This change fixes activation and session authority for those bounded executors, not the unresolved economy. Ability969 remains partial in the generated audit. Write Code970 is a separate change.

## Reproducible verification and native handoff

Units exercise native start/close parsing, exact tool/world/eligibility authority and on/off wire records. Fresh headless `compile-session-authority` proves both executors refuse no-session requests, zero-IS activation emits UI state without producing items, repeated start is idempotent, wrong tool and stale close are refused, matching60 consumes one owned source, and close/tool loss/unload revoke authority. `compile-storage-depletion` and `native-crafting-requests` preserve successful native recipe behavior after tool activation. The fresh relog also verifies the new helper stays disabled with dev commands0.

Native preparation is for Claude only. Use the PR's exact full pinned head, build matching Release server and NativeQa, inspect `--fixture-plan compile-items` (must report dev1) and `--build-info ROOT 2,17,277` before setup. Run NativeQa with mode `compile-items`, default focused loadout2/17/277, owned unloaded discipline ancestors, compiler in bag1, bits4..11 and archived bit in slot400. The fixture retains source/snapshot assembly hash equality and records `CompileItemsQa=true` provenance. Do not execute setup while a different native lock/session owns the client.

One pass on that pinned head: activate bag compiler1; record outgoing969/tool1/flag1 and incoming source969 linked968/toggle-on, visible Compile UI and stand-type idle. Confirm opening alone changes no cash/archive/bag item. Close the UI: record969/tool1/flag0, matching record/toggle-off, closed UI and standing idle. Reopen, compile the fixture archive bit once through the real UI and verify source/bag changes; do not claim equipment-code parity. Reopen, unload parent277 or move/remove the compiler and verify session/UI off; reload/restore/reopen. Enter combat or take damage and verify off, with no stale replay materialization. Leave for LA and return, checking no old state survives and the LA's separate stored-code operation still works. Record native animation/UI differences as failures, not cosmetic execution success. No native pass is asserted here.
