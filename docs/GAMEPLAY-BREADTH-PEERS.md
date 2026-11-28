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
crew when the server locks the persisted rows. Each refusal returns a final
`7c` with the founder's current faction status and a chat explanation.
Crew state and the invitation are published after commit.

Use `faction-founding-atomic`. CSR is at (17000,495,6400) and QAOther stands
east at (17220,495,6400), both on Mara NAV. Select QAOther with
`/target QAOther`. Open the people icon above chat, choose **Organization ›
Faction**, then use **Modify** to enter `QARetryFaction`.

1. Choose **Recruit**. The disposable fixture rejects the character
   affiliation write with CSR at 1,000 Info. Expect “Your crew was kept,”
   an unaffiliated crew and no invitation for QAOther. Verify Modify and
   Recruit are available again. Continue in the same session.
2. Open **Crew**, deposit 1 Info, and verify Crew 1 and personal Info 999.
   This releases the fixture's SQL check. Select QAOther again if needed.
3. Return to **Faction**, use the same name and choose **Recruit**. Expect
   the new faction status and one invitation to QAOther. Accept with
   `{"actor":"QAOther","action":"social","step":"faction-answer","accept":true}`.
   Use **More** to verify ranks 1 and 5, then check them again after relog.

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
