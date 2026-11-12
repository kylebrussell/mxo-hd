# Write Code activation (970)

## Primary evidence

The shipped tooltip `0x010000dc` requires activating a Code Analyser Tool. Resource 37
`WriteCodeAbility` grants resource 970 `WriteCodeActivateAbility`. Resource 970 stores
37 in `Ability/AbilityID`, but that field alone does not establish a runtime alias:
the client has an explicit 969/970 UI dispatcher and sends 970 when closing the writer.
The server must keep parent skill 37 and activation grant 970 distinct.

`data/crafting_rules.json` records tool 531 `CodeAnalyzerTool` with
`Tool/ActivatedAbility=2148476928` (970). The compiler is a different tool and session.
The catalog marks 970 exclusive, toggled, interruptible, noncombat, requiring a tool,
and using UI. Its while-enabled modifier targets 968 and has `WriteCodeActivateTerm`.
`research/client-scripts/ability/hacker/crafting.py` enters `Stance_StandType` idle;
both deactivation and `tempmods.py:WriteCodeActivateTerm` restore standing when still
in that stance.

Read-only disassembly of client.dll SHA256
`cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a`:

* `621d0ad0..621d0b58` compares 969 then 970, closes the other crafting window,
  opens compiler window `0x2b` or writer window `0x2a`, and binds the passed tool slot
  at compiler `+0xc1` or writer `+0x145`.
* `621ed3d7..621ed3e3` activates an owned tool through
  `621ecf10(ability, 1, toolSlot)`.
* `62087e20..62087e68` looks up (970,968), tests the active record, then sends
  `621ecf10(970, 0, writerToolSlot)` when the writer closes. The same close handler
  also sends empty `81 1a`; this latter packet is also used by containers and is
  not sufficient authority to start a crafting session.
* `621d3b00..621d3b49`, populated at `621ed223..621ed256`, defines `81 31` as
  ability u32, source inventory slot u8, tool slot u8, and recipe-known hint u8.
  It is not a recipe result identifier, nor permission to mint an item from an
  ordinary `80 b9` ability-use request.
* The `81 31` branch is gated at `621ed177..621ed193` by
  `62515bb0(ability, &target)` and `target == 7`. That helper reads
  `CastValidTarget` using string `628c8a8c`; resource 970 stores target 0.
  Consequently this inventory-target packet cannot establish writer activation.

## Remaining limits

Activation and cleanup do not implement the writer's code synthesis output path.
Known recipe generation, code packing, purity/stability, signal boosts, charges,
experience, and exact native presentation still require recovery or native checks.
The existing compile and decompile economy is separate; do not manufacture a
writer result or reinterpret the duplicate stored ID as an alias to claim parity.

## Runtime and validation

`CraftingSession` shares the compiler's single session and lifecycle hooks. It binds
the selected mode, exact tool slot, loaded grant, district and world generation.
Starting another mode closes the prior record; repeated flag1 is idempotent. A
matching flag0 closes only the selected mode/tool. Empty `81 1a` closes only a
writer, retaining its existing container behavior. Short ordinary ability use and
`81 31` cannot start the writer or create items. The compiler executors require a
compiler session and cannot borrow writer authority.

Both scripts enter stance3 (`StandType`, shipped movement table `629ce160`) and
return to stance0 only when still typing. Positive damage, another ability,
combat, death, control loss, loadout/tool loss and world changes use the common
cleanup. A changed stance invalidates the session. Exact retail interruption
ordering and whether movement itself cancels remain unmeasured.

`?writersession [off]` only reports/revokes current writer state under exact
`MXO_DEV_COMMANDS=1`; it cannot grant, load or activate anything. Fresh headless
scenarios cover grant identity, tool mismatch, duplicate activation, stale close,
both writer closes, compiler/writer exclusion, stance/damage/ability interruption,
tool loss, unload, rejected inventory/compile shortcuts and disabled dev helpers.
These tests do not establish native UI acceptance.

## Native checklist (Claude only)

Use the PR body's exact full pinned head. Build Release server and NativeQa from
that head. First inspect `--fixture-plan write-code` (dev commands must be1) and
`--build-info ROOT 2,17,37,277`; both exit without setup. Only Claude may then run
NativeQa setup in `write-code` mode, with default focused loaded2/17/37/277,
owned unloaded discipline ancestors18/515, analyser in bag2 and compiler in bag1.
Require fixture source/snapshot assembly hashes to match in the same configuration
and `WriteCodeQa=true`; Debug and Release hashes are separate builds.

Perform one pass on that pinned head: right-click analyser2 and choose **Activate
Item** (double-click sends equip `63`, not activation). Capture outgoing
970/tool2/flag1, incoming970 linked968/toggle-on, visible writer and typing idle.
Opening alone must change no cash, bag or archive. Close it and capture
970/tool2/flag0 plus empty `81 1a`, record/toggle-off and standing idle. Reopen,
right-click compiler1 and choose **Activate Item**, then activate writer2 the same
way, verifying each prior UI/session closes and tool
bindings remain distinct. Reopen and move/remove analyser2 or unload parent37;
the writer must close and no stale compile may execute. Restore/reopen, take
damage or enter combat, then use a hardline radial menu **Loading Area**, confirm,
and jack back in; each must clean state. Confirm
the compiler window and session still open and close with compiler1. The archive
contains ordinary Antibiotic item code for the Compile list; selecting it currently
must return a refusal without changing resources. Code-bit entries are filtered out
by that UI; successful bit/fragment compilation is covered headlessly. Record UI/animation
differences and unrecovered synthesis requests honestly; no native pass or full
writing economy is asserted by headless success.
