# Native social and marketplace QA

Use a fresh NativeQa fixture directory for each mode. Wait for `state.json` to say
`Ready` before logging in as CSR_Tester. The fixture uses disposable players,
inventory, crews and listings. Ambient aggression is disabled. The startup checks
require living helpers, the expected roles or listings, and clean server diagnostics.

For each helper request, place a JSON file with a unique name in the fixture's
`requests` directory. Read the matching file in `responses` for the result.

## Social

Use the `social-breadth` mode. CSR_Tester captains QACrew. QAFriend is nearby
and unaffiliated. QAOther is the nearby captain of QAPeerCrew.

1. Use the Crew UI to invite QAFriend. Submit a refusal, check the native message,
   then send a new invitation and accept. Verify the roster and make QAFriend
   first mate.
2. Send a native crew message. Ask QAFriend to reply and verify that the channel
   label and sender are correct.
3. Found a faction and invite QAOther's crew. Accept with QAOther. Verify that
   QACrew leads and QAPeerCrew joins at the lowest rank. Promote QAPeerCrew once,
   check faction chat, then test the subordinate captain's attempted rank change.
   Verify the native ranks and membership after removing QAPeerCrew.
4. Target QAFriend and use `/hug`. Try refusal and acceptance in separate offers.
   Then let QAFriend ask CSR and accept on the native client. Observe the offer
   and both characters' animations, and check that a refusal plays no paired hug.

| Helper | Request |
|---|---|
| QAFriend | `{"actor":"QAFriend","action":"social","step":"crew-answer","accept":false}` |
| QAFriend | `{"actor":"QAFriend","action":"social","step":"crew-answer","accept":true}` |
| QAFriend | `{"actor":"QAFriend","action":"social","step":"crew-chat"}` |
| QAOther | `{"actor":"QAOther","action":"social","step":"faction-answer","accept":true}` |
| QAOther | `{"actor":"QAOther","action":"social","step":"faction-chat"}` |
| QAOther | `{"actor":"QAOther","action":"social","step":"rank-refusal"}` |
| QAFriend | `{"actor":"QAFriend","action":"social","step":"hug-answer","accept":false}` |
| QAFriend | `{"actor":"QAFriend","action":"social","step":"hug-answer","accept":true}` |
| QAFriend | `{"actor":"QAFriend","action":"social","step":"hug-ask"}` |

## Marketplace

Use the `marketplace-breadth` mode. QAFriend lists a Code Bit 1 for 300
information and a Code Bit 2 for 420 from Loading Area. Startup verifies the
listings, the seller's emptied slots, the recorded delist fees of 15 and 21,
and an ordinary search result. The fixture writes the IDs to
`marketplace-listings.json`.

1. Enter Loading Area through the native client and open Marketplace. Search for
   Code Bits. Buy Code Bit 1. Check one item in the native bag and a balance of
   700 from the starting 1,000.
2. Select the Code Bit 2 listing and leave it visible in the results. Submit
   QAOther's purchase and wait for its successful reply. Attempt to buy the same
   listing from CSR without refreshing. Check the native refusal, unchanged bag
   and cash, and that a fresh search drops the listing.
3. Submit the seller verification below. The fixture checks a live sale notice,
   the seller's balance of 1,720 in Loading Area and both buyers' inventories
   and balances in the disposable database.

| Helper | Request |
|---|---|
| QAOther | `{"actor":"QAOther","action":"marketplace","step":"buy-stale"}` |
| QAFriend | `{"actor":"QAFriend","action":"marketplace","step":"verify-sales"}` |
