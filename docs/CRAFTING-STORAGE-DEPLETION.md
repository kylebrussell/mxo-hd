# Compile storage depletion

Compiling the last item in a code-storage entry now queues `5f <slot u16 LE> 00` after removing its database row. Partial consumption still queues `69 00`; bag deletions still end in `01`. Both native `81 2e` and the dev compile command share `CraftingHandler.Compile` / `CodeStorageHandler.TakeOne`.

This is independent of PR #110's central-storage moves: that branch owns `62` and its own `CentralStorage.BuildRemove`; this change uses `CodeStorage.BuildRemove` and can merge in either order. No `60` implementation or storage-capacity change is included.

## Evidence and limits

Read-only disassembly of the installed 7.6005 `client.dll`, SHA256 `cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a`:

- At `0x62179908`, the item-removal dispatch reads byte 3. Nonzero takes the bag path (`0x621e18c0`); zero branches to `0x62179935`, reads the slot as u16 at offset 1, and calls `0x621e1b20`.
- `0x621e1b38` bounds the slot below `0x208`; `0x621e1b48` addresses the known-abilities array at `slot * 8 + 0x6dc`; `0x621e1b52` / `0x621e1b5a` clear that entry. `0x621e1b63` selects window `0x6b`, and `0x621e1b71` calls `0x6203df00` to remove its row. This agrees with PR #110's documented answer.
- Existing captures establish storage adds/updates and bag deletions (see CODE STORAGE in `docs/CRAFTING.md`); no capture establishes the compile-depletion sequence. This fix follows the client removal handler, and its actual UI acceptance remains native QA work.
- Bag-first ingredient selection remains this server's policy; no new retail claim is made. `research/client-scripts/crafting_server.py` supplies compile contests, not the removal wire format.

## Automated check

`CodeStorageTests` asserts little-endian storage removal at low and high slots, the distinct bag flag, repeated ingredients, and exact-count exhaustion. `compile-storage-depletion` starts a fresh isolated server/database, exercises mixed bag/storage via the dev command, then repeated stored ingredients via native `81 2e`, checks update-before-delete ordering and no extra bag deletion, rejects a replay, and verifies the entry is gone at a second fresh server/login while an unrelated high-slot entry survives. It also tests enabled/disabled dev helpers.

```powershell
dotnet test tests/hds.Tests/hds.Tests.csproj --filter "FullyQualifiedName~CodeStorageTests|FullyQualifiedName~Crafting"
dotnet build tools/HeadlessClient/HeadlessClient.csproj
dotnet tools/HeadlessClient/bin/Debug/net8.0/HeadlessClient.dll --parallel 1 --filter compile-storage-depletion
```

## Native QA checklist (Claude only; not performed by Codex)

Use a disposable server/database and a fresh disposable character with no Code Bit 5 (7336) in bag or storage. Do not use the main database or TheNeo. Start with `MXO_DEV_COMMANDS=1`.

1. Type `?additem 7336` three times, then `?codeupload`. The upload helper invokes the existing hardline upload path, avoiding right-click menus. Expect the bag stack to disappear and a storage `5e` containing `0x840e5402` (three bits); note its u16 slot S.
2. Type `?compile 7458`. Expect `Compiled Function Subroutine: Consumable.` (the name may follow local item data), one result in the bag, and storage updates for S to `0x840e5401` and then `0x840e5400` (one remains). There must be no `5f <S> 00` yet.
3. Type `?additem 7336` once, then `?compile 7458`. The bag copy is consumed first (`5f <bag slot> 01`), then the remaining stored copy (`5f <S u16 LE> 00`). The bag now contains two results. The removal must not be a `69` with packed zero count, which represents one item.
4. Without reconnecting, use the existing QA travel helper to reach the Loading Area: `80 fc 01` -> `80 fd`, `80 fe 01` -> `81 07`, `81 08` -> `81 09`, then `05`. Open **Central Storage > Equipment**. Expect Code Bit 5 to be absent from the storage list in this same session. If that window was already open while commands were issued, its row should vanish without reopening; record whether that stronger live-window check was reachable.
5. Reconnect once: Code Bit 5 must remain absent and both result fragments must remain in the bag. A third `?compile 7458` must report missing ingredients, granting nothing.
6. On a server without `MXO_DEV_COMMANDS=1`, `?compile 7458` must report that it is a dev command; `?codeupload` must not move items. Normal native `81 2e` remains available under its existing checks.

Native `81 2e` is covered headlessly; the deterministic commands above isolate removal presentation from compile skill/recipe/tool setup. Native `60` from the Loading Area compile button is outside this fix. If chat is unavailable in the Loading Area, perform steps 1-3 in world and then step 4 without reconnecting; do not treat reconnect-only success as a live refresh pass.
