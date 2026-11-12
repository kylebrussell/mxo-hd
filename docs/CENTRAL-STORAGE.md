# Central storage

Retail players had a central storage besides their bag. In the 7.6005 client it is the **code archive**: only codes and code fragments go in. The client's own refusal says so ("Upload Failed: Only codes and code fragments may be placed in Central Storage.", `ID_INVENTORY_ITEM_UPLOAD_FAILURE_INVALID_ITEMTYPES`). The server already had the archive (the hardline upload, `CodeStorage`, see CRAFTING.md). What was missing is moving one bag slot or one storage entry at a time from the Loading Area's storage window, which is what this adds.

No capture has a single-item move, so this all comes from `client.dll`. Each item below says whether it is read from the client or chosen by us.

## What the client has

**The storage (client).** Central storage is the known-abilities array, `0x629ea4e8 + 0x6dc`: 0x208 entries of 8 bytes, a bound every handler checks. Abilities share it.
- An entry is an ability when bit 31 is set and bit 26 is clear (0x62024800).
- Any other entry is stored code, `0x84000000 | goid << 7 | (count - 1)`. The client reads the goid as `(value >> 7) & 0x7ffff`.
- The margin's known-abilities block (character data 0x07) fills it at login. The server keeps it in `char_abilities`, in the slots below `CharacterAttributes.FirstSlot`, which is also 0x208. No new table was needed.

**Where it is offered (client).** The "LOADING AREA: CENTRAL STORAGE" window (`ID_LA_CENTRALSTORAGE`) has these tabs: Summary, Hardlines, Abilities, Equipment, Hovercraft, Marketplace and Cinematics.
- The Equipment tab is `laviewinventory.cpp` ("Loading the Central Storage Inventory Layout"). It lists storage beside the bag. Its event handler is 0x6203f5a0.
- The only drop target for a storage slot (drop handler 0x620a0000, destination 0x19) is pushed from that view (0x620401a5).
- At a hardline in the world there is only the bulk upload (booth action 04). You reach the Loading Area through the hardline's "Exit to Loading Area" (booth action 05).

**Capacity.**
- *Client:* 0x208 entries, abilities included.
- *Client:* each entry holds 128 at most (seven count bits).
- *Ours:* we use those limits. The hovercraft upgrades ("Expanded/High Storage Capacity: increases the size of each crew member's central storage") show that retail had a smaller base limit, but no number survives.

## Requests (client)

| Record | Sent by | Meaning |
|---|---|---|
| `62 <SourceSlot u16> <DestinationSlot u16> 01` | 0x621e2000 | A bag slot into storage. DestinationSlot is the storage slot it was dropped on. It is `6b` for a double-click on the bag item or the Move button (event `CISC`). Before sending, the client refuses any item that isn't a code (0x621ccde0). |
| `62 <SourceSlot u16> <DestinationSlot u16> 00` | 0x621d4c00 | A storage entry into the bag. DestinationSlot is a byte. It is the bag slot it was dropped on, the first free bag slot (double-click, 0x621ca000), or the first free slot on the shown bag page (Move button, event `CIPI`). It is `6b` when the client found none. |
| `67 <Slot1 u16> <Slot2 u16> 01` | 0x620a0880 | A storage entry dropped on another entry of the same item. A different item does nothing. |
| `68 <Source u8> <Dest u8> <Amount u32> 01` | 0x620a07ba | The split dialog's amount from a storage entry, dropped on a storage slot. The client writes both slots as bytes, and a Dest of 0 as 8. |
| `5d <Slot u16> 00 01` | 0x621d4ae0 | The window's Recycle button (event `CIRY`) on a storage entry. Slots below 0x208. |

## Answers

**Records (client).**
- The server-to-client item records branch on PersonalInventoryItem: 1 is the bag, 0 is storage.
- `5e <slot u16> 00000000 <value u32> 00` adds a storage entry. The handler is 0x621e1a70, and it also tells the storage window (0x6203dc40).
- `69 00 <slot u16> <value u32> 00 00` changes an entry's count.
- `5f <slot u16> 00` removes an entry (0x621e1b20). This was missing before: the compile path's `TakeOne` still leaves an emptied entry on screen until the next login, and that is Codex's area.
- `65` with IsCentralStorageItem goes to 0x621cd390, which is an empty `ret 8`. So the client can't show a move or a swap inside storage.

**The upload result (client, plus one capture).** The record is `81 18 <SingleItemUploaded u8> <AbilityCodeUploaded u8> <Status u32>`, handled at 0x621e1650. The client shows "Upload Status" with:
- "Item successfully uploaded!" (SingleItemUploaded) or "Items successfully uploaded!" for a success status;
- "Only some of your items were successfully uploaded..." for `LTOBJSRV_PARTIALUPLOAD` (0x8e000085);
- "Your upload exceeds the maximum capacity of the code archive." for `LTPS_SPCENTRALSTORAGEFULL` (0x0c0002c8);
- "Item(s) could not be uploaded!" for any other error;
- the tip "You've just uploaded an ability to Central Storage..." when AbilityCodeUploaded is set.

Retail sent `81 18 01 00 00 00 00 00` after a hardline upload that moved one item (crashover2k uploadcodebits line 239).

**Status codes (client).** The client registers these (0x622a6480), and the server uses them: `0c000259` INVALIDCENTRALSTORAGELOCATION, `25f` ITEMNOTSTACKABLE, `261` INVALIDPERSONALINVLOCATION, `28e` INVALIDPERSONALINVITEM, `294` CENTRALSTORAGEEMPTY, `295` CENTRALSTORAGENOTEMPTY, `29c` INVALIDINVENTORYTRANSACTION, `29d` PERSONALINVEMPTY, `29e` PERSONALINVNOTEMPTY, `29f` INVALIDCENTRALSTORAGEITEM, `2a7` INVALIDITEMTYPE, `2c8` CENTRALSTORAGEFULL, `2ca` PERSONALINVENTORYFULL. All of them are `LTPS_SP...`, category 0x0c, error. LOCATION means a slot, as in INVALIDPERSONALINVLOCATION and INVALIDABILITYINVLOCATION.

## What the server does (ours, from the above)

Each request is answered in the order shown:

| Request | Answer |
|---|---|
| Bag to storage | `5f <bag> 01` (or `69 01` if part of the stack stays), then `5e`/`69 00` for storage, then `81 18` (SingleItemUploaded when one item moved, status 0 or PARTIALUPLOAD). |
| Storage to bag | `5f <slot> 00` or `69 00` for storage, then `5e` (or `69 01`) for the bag. |
| Merge or split in storage | `69 00` / `5e` / `5f ... 00`. |
| Recycle | `80 e7`, `80 e4`, then `5f <slot> 00`. Retail paid a bag recycle this way, and it is paid the same (one item's price). |

**Storing a bag slot:**
- The bag slot's whole stack moves.
- With `6b` it joins the lowest entry of the same item that has room, otherwise it takes the lowest free slot.
- Onto an entry of the same item, it adds what fits up to 128. The rest stays in the bag, and the status is PARTIALUPLOAD.
- An unknown ability's code is learned in a free slot, as the bulk upload does (`5e` with the ability value, `81 18 01 01`).

**Taking an entry out:**
- It takes as much as one bag slot holds (64).
- With `6b` it joins the lowest bag stack of the same item that has room, otherwise it takes the first free bag slot.

**Refusals:**
- Storing: an `81 18` with the status, for example CENTRALSTORAGEFULL, INVALIDITEMTYPE, or CENTRALSTORAGENOTEMPTY for an occupied slot of another kind.
- Everything else: a chat line, "You can't do that: ...". The client has no record that carries a status for those.

**Choices we made, because the client is silent:**
- No swaps. Retail's NOTEMPTY codes exist, and the client's storage-to-storage drop of different items does nothing. A drop onto an occupied slot of another item is refused with CENTRALSTORAGENOTEMPTY or PERSONALINVNOTEMPTY.
- The requests are accepted only in the Loading Area, where the window exists. Anywhere else they get INVALIDINVENTORYTRANSACTION.
- `6b` always means "any slot", although storage slot 0x6b exists. The client's own paths use it that way.
- A split takes the client's byte slots as they come. Storage slots above 0xff can't be split, and a new character's code sits far below that.

## Tests

- `tests/hds.Tests/CentralStorageTests.cs`: parsing, records, and every rule and refusal above.
- `tools/HeadlessClient` scenario `central-storage-store-relog-retrieve`:
  1. It is refused in the world.
  2. In the Loading Area: a bad slot is refused, ten Code Bits are stored, then split and merged inside storage.
  3. A fresh server and login: the entry is in the margin's known-abilities block.
  4. Back in the Loading Area: an empty slot is refused, the entry is taken back into the bag, stored again and recycled.

## Check in the real client

Start the server with `MXO_DEV_COMMANDS=1`, then check these steps with one client.

1. In the world, type `?additem 7332` three times and `?additem 2440` once. The bag gets Code Bit 1 x3 and one "Function Subroutine: Hacker Ability" fragment.
2. Go to any hardline. Right-click it and choose **Exit to Loading Area**, then confirm.
3. In the Loading Area, open **Central Storage** and its **Equipment** tab. The bag is on one side and stored code on the other.
4. Double-click the Code Bit stack in the bag list.
   - *Expected:* the stack leaves the bag, a "Code Bit 1" entry (3) appears in storage, and "Upload Status: Items successfully uploaded!" pops up.
   - Do the same with the fragment. *Expected:* "Item successfully uploaded!".
5. Drag a non-code item (a shirt) onto the storage list. *Expected:* the client itself says "Upload Failed: Only codes and code fragments may be placed in Central Storage." The server gets nothing.
6. Select the Code Bit entry in storage and press **<= Move**, or double-click it. *Expected:* it leaves storage and comes back into the bag as a stack of 3.
7. Store it again. Quit the client, start it again, log in, and go to the Loading Area again. *Expected:* the entry is still in storage.
8. Drag the entry from storage onto an empty bag slot. *Expected:* it lands in that slot.

Watch the server log for `[CENTRAL STORAGE]` lines; a refusal names its LTPS status. Also look for:
- whether the window refreshes after each answer, without reopening;
- which button is "Move =>" and which is "<= Move". The events are `CISC` (bag to storage) and `CIPI` (storage to bag);
- whether the drag-split of a storage entry (shift-drag) opens the split dialog and sends `68 ... 01`.
