# Native QA sessions for gameplay breadth

Claude runs the four sessions in order. Each uses one fresh disposable NativeQa fixture and only
native CSR_Tester110. Budget 5–10 minutes for setup, 35–45 for the checks and 10 for evidence and
normal shutdown. Set aside any remaining checks if the window or fixture is blocked. Log the head,
mode and observed result, then keep the corresponding PacketLog and server log. Use the fixture's
normal stop and verify its CSR reset and lock release before the next session.

Use the usual Debug harness invocation for each profile.

```powershell
dotnet build tools/NativeQa/NativeQa.csproj -c Debug
dotnet tools/NativeQa/bin/Debug/net8.0/NativeQa.dll <repository> <fresh-qaRoot> <commit> <mode>
```

## 1. Trade · 55 minutes · `secure-trade`

QAFriend is a headless player beside CSR_Tester, carrying one Code Bit 2 and 500 information. CSR
starts with one Code Bit 1 in each of slots 3 and 4, and 1,000 information. Use the native bag and
trade window for all CSR actions. To drive the partner, write one JSON request at a time below
`<qaRoot>/requests` with a new filename. Wait for the matching response and the native UI update
before issuing the next action.

```json
{"actor":"QAFriend","action":"trade","step":"add-info","information":200}
```

1. Drop the slot-3 bit on QAFriend. Verify the Secure Trade window names QAFriend, lists the bit
   and locks slot 3. Have QAFriend add 200 information. Tick Accept in the native UI.
2. Have QAFriend add 50 more. Verify CSR's Accept box clears and the total is 250. Have QAFriend
   send `accept`, then `reset`. Verify the partner-state label changes both ways.
3. Have QAFriend send `accept` again, then tick Accept natively. Verify one completion, one bit
   transferred and CSR Info 1,250. Slot 4 stays occupied.
4. Have QAFriend send `open`. Verify the incoming window shows one Code Bit 2 from QAFriend. Cancel
   natively. Reopen from CSR slot 4 and have QAFriend send `cancel`. Verify both cancellations
   close the window, unlock the slot and leave balance/items unchanged.
5. Relog in the same fixture and verify CSR's inventory and balance. Capture native request,
   incoming offer, partner update, completion and cancel. [Trade reference](SECURE-TRADE.md)

## 2. Vendors · 50 minutes · `vendor-buyback`

1. Type `?clientactions vendor` to open tools vendor 98. Use the native window to appraise the
   slot-11 shirt. It should quote 680 with the starting 10,000 Info and inventory unchanged.
2. Sell the shirt, then use Buy Back. Expect a single 680 receipt, restoration of the shirt and
   the starting balance, and a cleared Buy Back row. Buy one displayed item and compare actual
   Info delta and bag entry with the displayed quote.
3. Appraise the slot-13 leveled ability code for 12,600. Test sale and buyback of both the code and
   slot-12 Code Bits. Look for preserved ability level and whole-stack quantity.
4. Close the vendor and recycle the remaining bit stack. Cancel once, then confirm for 40. Confirm
   the ability-code recycle at 500 if still held. Retain the cash and inventory changes.
5. Reopen and relog after a sale. Record whether a last-row receipt is offered, and whether a
   visible stale row refuses. The QA opener starts the ordinary vendor window and leaves world-NPC
   location for separate QA. [Economy](VENDOR-ECONOMY.md) · [Buyback](VENDOR-BUYBACK.md)

## 3. Crafting · 60 minutes · `ordinary-ic-compile`

1. Load owned 37, 1008 and 36 with `?qaload`. Keep Minor Repairs 24 unloaded. Right-click
   compiler 1271 and choose **Activate Item**. Confirm opening keeps Info and charges intact.
2. Compile Max Health Boost Pill 3. Expect about 18 seconds, one pill, two charges to one and
   Info 200,000 to 183,560.
3. Close the tool, load 24 with `?qaload 24` and reopen. Compile the second charge. Expect about
   12.06 seconds, one more pill, the exhausted code row gone and Info 167,120.
4. Start the 2399 code and close immediately. Match the outcome to the emitted input. A genuine
   tool-off cancels without consumption. Target-clear alone permits one completion at 9 Info.
5. If time permits, run the documented bag/storage depletion sequence in the same fixture before
   relogging. Use the visible code-storage list to verify its exhausted row disappears. The
   deterministic upload and compile commands are fixture assistance. [Ordinary IC](COMPILE-ORDINARY-IC.md) · [Storage depletion](CRAFTING-STORAGE-DEPLETION.md)

## 4. Death and resurrection · 60 minutes · `death-presentation`

1. Verify the bootstrapped helpers are present. Use `?die` on CSR. Observe down motion, HP0,
   the death UI and a pose that persists past nine seconds.
2. Have QAFriend cast 416 on CSR through the helper request directory. Decline one offer if the
   window provides that choice, then accept a fresh offer once. Observe standing motion, health,
   movement, Death Effect and the PvP flag.
3. Have QAFriend die with the normal helper chat action. Observe the remote down pose from CSR.
   Have QAOther cast 416 on the friend and QAFriend approve. Observe remote standing and movement.
4. Die and choose native Reconstruct. Follow the Loading Area return path. Observe the new
   spawn, standing pose and controls. Repeat one ordinary death and reconstruct to catch stale
   pose or UI.
5. If time permits, verify a duel defeat stands the player in place without entering Loading
   Area. Compare the native render with server lifecycle evidence. [Death and helper instructions](DEATH-PRESENTATION.md)

## Following sessions

- **Travel · 60 minutes.** Start with `la-hardline-registration`. Verify the radial and
  Synchronize on map 16, return to it through Loading Area, then take an ordinary city
  exit and subway ride. Record world transitions and post-ride interaction separately.
- **Social · 60 minutes.** Group crew/faction invitation and ranks with crew chat and a paired
  emote. Use a single social fixture with one captain and two scripted peers so ranks,
  permission refusals and emote cancellation can be checked on the same login.
- **Marketplace · 50 minutes.** Use a single disposable seller/listing fixture. Browse and
  buy from CSR with the seller in Loading Area. Check resulting bag, cash and stale-listing UI.
  The social and marketplace sessions need those peer/listing seeds before scheduling.
