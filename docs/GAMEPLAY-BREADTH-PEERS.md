# Native social and marketplace QA

Use a fresh NativeQa fixture directory for each mode. Wait for `state.json` to say
`Ready` before logging in as CSR_Tester. The fixture uses disposable players,
inventory, crews and listings. Ambient aggression is disabled. The startup checks
require living helpers, the expected roles or listings, and clean server diagnostics.

For each helper request, place a JSON file with a unique name in the fixture's
`requests` directory. Read the matching file in `responses` for the result.

## Social

Use the `social-breadth` mode. QAFaction is already seeded with CSR_Tester
captaining the leading QACrew at rank 1 and QAOther captaining QAPeerCrew
at rank 5. QAFriend is nearby and unaffiliated.

1. Use the Crew UI to invite QAFriend. Submit a refusal, check the native message,
   then send a new invitation and accept. Verify the roster and make QAFriend
   first mate.
2. Send a native crew message. Ask QAFriend to reply and verify that the channel
   label and sender are correct.
3. Open the Faction roster after login and verify both crews. Promote QAPeerCrew
   from rank 5 to 4 and check faction chat in both directions. Run QAOther's
   `rank-refusal` while both crews are still members. Verify that the leading
   crew's rank stays at 1, then remove QAPeerCrew in the roster.
4. Target QAFriend and use `/hug`. A refusal should show “QAFriend declined your
   hug.” in System chat. Check that the accepted animation plays only after an
   accepted offer. Then have QAFriend ask CSR and accept on the native client.

| Helper | Request |
|---|---|
| QAFriend | `{"actor":"QAFriend","action":"social","step":"crew-answer","accept":false}` |
| QAFriend | `{"actor":"QAFriend","action":"social","step":"crew-answer","accept":true}` |
| QAFriend | `{"actor":"QAFriend","action":"social","step":"crew-chat"}` |
| QAOther | `{"actor":"QAOther","action":"social","step":"faction-chat"}` |
| QAOther | `{"actor":"QAOther","action":"social","step":"rank-refusal"}` |
| QAFriend | `{"actor":"QAFriend","action":"social","step":"hug-answer","accept":false}` |
| QAFriend | `{"actor":"QAFriend","action":"social","step":"hug-answer","accept":true}` |
| QAFriend | `{"actor":"QAFriend","action":"social","step":"hug-ask"}` |

## Faction founding

Use `faction-founding`. CSR_Tester and QAOther captain QACrew and QAPeerCrew.
The fixture checks that both crews start unaffiliated.

1. Target QAOther. Open **Organization › Faction**, choose **Modify**, and
   enter `QAFaction` in the name dialog. Choose **Recruit** with QAOther still targeted.
2. Submit `{"actor":"QAOther","action":"social","step":"faction-answer","accept":true}`.
3. Choose **More** and verify QACrew at rank 1 and QAPeerCrew at rank 5 in the roster.

The client enables the Faction menu after receiving faction state. A zero-faction
`7c` record initializes that state and lets the menu open the founding panel.
The panel's Recruit action sends `7a` for the named faction and selected captain.

### Atomic faction founding

The faction row, leading crew at rank 1 and the affiliations of that crew's
characters commit together. The founder must still captain an unaffiliated
crew when the server locks the persisted rows. Each refusal sends
`7b <name offset u16> <status u32> <sized name>` and a chat explanation.
The stock dispatcher at `621790b1` calls `621ba2e0`. The name getter at
`62127650` reads the offset at +1. A positive Status at +3 reaches
`621ba373`, which clears the pending creation state. Status 1 prints
“Faction could not be created.” in stock chat. Native runs showed
that both `6f` and an empty final `7c` left the pending state active.
Crew state and the invitation are published after commit.

Use `faction-founding-atomic`. CSR is at (17000,495,6400) and QAOther stands
east at (17220,495,6400), both on Mara NAV. Select QAOther with
`/target QAOther`. Open the people icon above chat, choose **Organization ›
Faction**, then use **Modify** to enter `QARetryFaction`.

1. Choose **Recruit**. The disposable fixture rejects the character
   affiliation write with CSR at 1,000 Info. Expect “Your crew was kept,”
   `7b`, an unaffiliated crew and no invitation for QAOther. Verify Modify
   and Recruit are available, then continue in the same session.
2. Open **Crew**, deposit 1 Info, and verify Crew 1 and personal Info 999.
   This releases the fixture's SQL check. Select QAOther again if needed.
3. Return to **Faction**, use the same name and choose **Recruit**. Expect
   the new faction status and one invitation to QAOther. Accept with
   `{"actor":"QAOther","action":"social","step":"faction-answer","accept":true}`.
   Use **More** to verify ranks 1 and 5, then check them again after relog.

### Atomic faction admission

Accepting a faction invitation with `6f 01` joins the invited crew at rank
5 and updates all its members' affiliations in one transaction. The
server checks that the accepting player still captains the invited crew
and the sender still leads the inviting faction. A failed write keeps
the crew unaffiliated and gives both captains a chat explanation. The
sender can offer a fresh invitation in the same session.

Use `faction-admission-atomic` and have QAOther send
`{"actor":"QAOther","action":"faction-admission"}` after CSR enters
Mara. Accept the stock prompt. The fixture refuses the affiliation write
while CSR has 1,000 Info. Expect the crew to stay unaffiliated and a
visible request for a new invitation. Open **Crew** from the people icon
and deposit 1 Info to release the fixture check. Have QAOther send the
same control again, accept, and open **Faction > More**. QAPeerCrew stays
at rank 1 and QACrew joins at rank 5. Check the same roster after relog.

### Atomic faction ejection

Removing another crew with `73` clears that crew's faction, rank and
member affiliations in one transaction. The server checks the issuing
captain's current rank and sends the removal packet only after commit.
If a write fails, it sends the ordinary `74` refusal and a chat
explanation. The stock `74` handler at `621b9600` displays its generic
failure without reading Status.

Use `faction-ejection-atomic`. Open the people icon above chat and choose
**Faction > More**. Right-click QAPeerCrew and choose **Remove**. Expect
“The roster was kept.” in chat and both crews still in the list. Send
`{"actor":"QAOther","action":"faction-ejection-retry"}`. The helper
deposits 1 Info into its crew bank, releasing the disposable fault.
Remove QAPeerCrew again in the same session. The roster should retain
QACrew at rank 1 and lose QAPeerCrew. Check the roster after relog.

### Atomic faction disband

Disbanding with `77 01` archives the faction and clears its crew ranks
and member affiliations in one transaction. The database verifies the
current faction leader before any write. The existing archived balance
is retained. On a rollback, the active roster and bank remain available
and the requester gets a visible explanation.

In `faction-disband-atomic`, open the people icon above chat and choose
**Faction > Disband**. The stock sender at `620b2acf` uses `77 01` and
prints “Disbanding Faction.” Confirm the request. The disposable check
blocks the last affiliation write. Expect “The roster and bank were
kept.”, both crews and 500 Info still in the Faction bank. Send
`{"actor":"QAOther","action":"faction-disband-retry"}` to deposit 1 into
QAPeerCrew's bank and release the fixture fault. Repeat Disband, then
relog. The faction is gone and QACrew is still present.

### Atomic crew member removal

The crew roster's **Remove** action sends `72 02 <character id u32>`.
The server checks the active captain and commits the member deletion and
the character's crew and faction resets together. The roster update is
sent after commit. A failed write keeps the member in both groups and
reports “The roster was kept.” in chat. All identifiable Crew Remove
refusals also send `72 02 00 00 00 00` to the requester. The stock
sender at `621c0c40` locks input through `62019fc0`. The `72`
receiver at `621c2500` calls `62019fe0` before checking the character ID.
ID zero has no roster row, so it clears the input lock without a roster
change. The standard six-byte record carries no status field.

Use `crew-member-removal-atomic` on Mara NAV. From the people icon
open **Crew**, right-click QAFriend in the roster and choose **Remove**.
The disposable SQL check blocks the character write. Expect the zero-ID
reply, QAFriend still in the roster, and the bank still at 200. Check that
right-click opens the same row's context menu again. Submit
`{"actor":"QAFriend","action":"crew-removal-retry"}` and wait for the
deposit confirmation. Remove QAFriend again in the same session. Expect
`72 02`, QAFriend gone, and CSR still captain in QAFaction. Relog and
verify the member is still absent and the crew bank is 201.

### Atomic crew captain transfer

The roster's **Make Captain** action sends `75 02 <character id u32>`.
The server checks the current captain and replacement against the crew
and commits captain flags, the first-mate clearing where needed, and the
faction master transfer in one transaction. Successful commits send
`75 02` to the crew and `7e` to the other crews in the faction.
On refusal the requester receives chat and `72 02 00 00 00 00`.
The Make Captain sender at `621c0700` sets the same stock input lock
as Remove, and the zero-ID reply releases it.

Use `crew-captain-transfer-atomic`. Open **Crew**, right-click first
mate QAFriend, then choose **Make Captain**. The disposable SQL check
blocks the faction master write. Verify CSR stays captain, QAFriend
stays first mate and the roster menu reopens. Deposit 1 through the
**Faction** bank controls and retry in Crew. QAFriend becomes captain
and the first-mate slot clears. Verify the crew roster and faction
bank of 501 after relog.

### Atomic crew founding

On acceptance of a founding invitation (`6f 02`), the server rechecks
both characters and the crew name under transaction locks. The new
crew, captain and member roster rows, and character affiliations commit
together. The crew creation window receives `80 85` with status
`0x020000b4` on a write failure. The stock handler at `621a9520` clears
the pending state and uses its generic failure text with the shipped
“Unknown Error” resource. The server also gives both players a specific
chat refusal. Success sends `80 85` and the committed roster.

Use `crew-founding-atomic`. From **Crew > Create Crew**, enter
**QARetryCrew**, choose Zion and invite QAFriend. Send
`{"actor":"QAFriend","action":"crew-founding-accept"}`.
The disclosed disposable check blocks QAFriend's affiliation write.
Verify the failure result and that CSR remains unaffiliated. Reopen
creation right away and use the same name and organization with QAOther.
Send `{"actor":"QAOther","action":"crew-founding-accept"}`.
Expect success and a two-member roster with CSR as captain. Verify
the name and roster after relog.

## Marketplace

Use the `marketplace-breadth` mode. QAFriend lists a Code Bit 1 for 300
information and a Code Bit 2 for 420 from Loading Area. Startup verifies the
listings, the seller's emptied slots, the recorded delist fees of 15 and 21,
and an ordinary search result. The fixture writes the IDs to
`marketplace-listings.json`.

1. Enter Loading Area through the native client and open Marketplace. Search
   **Codes › Code Fragments › Code Bit**. Buy Code Bit 1. Check one item in the native bag and a balance of
   700 from the starting 1,000.
2. Select the Code Bit 2 listing and leave it visible in the results. Submit
   QAOther's purchase and wait for its successful reply. Attempt to buy the same
   listing from CSR without refreshing. Check the native refusal and unchanged
   bag and cash. In the same session, verify the controls respond and a fresh
   search drops the sold listing.
3. Submit the seller verification below. The fixture checks a live sale notice,
   the seller's balance of 1,720 in Loading Area and both buyers' inventories
   and balances in the disposable database.

| Helper | Request |
|---|---|
| QAOther | `{"actor":"QAOther","action":"marketplace","step":"buy-stale"}` |
| QAFriend | `{"actor":"QAFriend","action":"marketplace","step":"verify-sales"}` |
