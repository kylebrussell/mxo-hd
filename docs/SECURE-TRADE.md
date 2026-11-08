# Secure trade

No retail capture of a trade exists and none is expected, so this comes from `client.dll` (7.6005). The findings are split by how they were established: each record's layout and effect are read from the client code; who gets which record, in what order, and the server's checks are our reading of it.

`docs/generated/client-rpc-classes.txt` lists all 514 RPC classes found the same way (opcode, constructor, printer, field names): use it for other gaps.

## How it was recovered
Every RPC class has a vtable (+8 = debug printer naming fields, reading [this+0x10]+offset) and a ctor that writes
the opcode as a word (0x8381 = bytes 81 83), the fixed size, and field defaults. Field names, offsets and sizes come from those.

Server->client dispatcher: 0x62175570, switch on key-0xC via table 0x6217e9c0 (key = 0x100 | second byte for 81 xx).
Keys 0x183, 0x184, 0x186, 0x187 have handlers. Keys 0x182, 0x185 hit the default 0x6217e9b0 (not handled, client->server only).

## Client -> server
81 82 size 0x0D  TradeRequest  ctor 0x621d3d40, sender 0x621eb790 (built at 0x621eb8bc)
  +2 TargetID u32 | +6 InfoTrade u8 (= InfoQuantity != 0) | +7 InfoQuantity u32 | +0B Slot u8 (default 0x6B = none) | +0C ItemQuantity u8 (never set by sender, 0)
  Sender bails if InfoQuantity==0 and Slot==0x6B. So a trade cannot start empty: it starts by dropping an item or information on a player.
  Callers: 0x620a03b8 (drop info, slot=0x6B), 0x620a0417 (drop item). TargetID is resolved from a handle through [0x62a2b2bc] vcall +0x14 and [0x629df7bc] vcall +4 (not traced further).
  Targets with interface 0x31 satisfying 0x62304070 take a different path (0x621eb080) with no 81 82; appears to be give-to-NPC (inferred).

81 85 size 0x0A  TradeAction  ctor 0x6209ba00, send 0x6214c700 on [0x629e68a8]
  +2 TradeAction u8 | +3 InfoTrade u8 | +4 InfoQuantity u32 | +8 Slot u8 (default 0x6B) | +9 ItemQuantity u8
  0 add     0x620a0296 (drop on trade window: source 3 -> Slot; 4 -> Slot+ItemQuantity; 5 -> InfoTrade=1, InfoQuantity; 8..10 -> client error EQUIPPED, not sent)
  1 remove  0x620a0578 (Slot) ; 0x62109010 (InfoTrade=1, InfoQuantity, from amount dialog event 'DWSt')
  2 clear   0x62107ef0 (UI event 'TrCl'; client prints ID_SECURE_TRADE_CLEAR_SELF immediately)
  3 cancel  0x62107ea0 (UI events 'TrCa', 'TrOk'); also 0x62176242 auto-decline when ID_AUTOIGNORE_COMMAND_TRADE (0xDA000006) is on
  4 accept / 5 reset accept  0x62108d40 ('TrAc'): checkbox checked -> 4, unchecked -> 5
    Before sending: if partnerInfo - myInfo + available >= 2^32 shows TOO_MUCH_INFORMATION dialog and sends nothing.
  Oddity: 0x620a0599 (drag source type 0x13 in the destination case at 0x620a04bd) sends action 0, InfoTrade=1, qty=u16 at +0x33C. Purpose not determined.

There is no separate invite accept or decline record. The target's window opens on 81 84. Declining is action 3.

## Server -> client
81 83 size 0x0F + string  RequestResult  ctor 0x62145320, dispatcher case 0x621762de
  +2 TargetName string offset u16 | +4 Status u32 | +8 InfoTrade u8 | +9 InfoQuantity u32 | +0D Slot u8 (default 0x6B) | +0E ItemQuantity u8
  Status 0: opens window 0x25 (0x62020860), 0x62109300: adds own info if InfoTrade, sets partner name, adds own slot if Slot != 0x6B.
  Other statuses show a message and do not open the window (table below). Unknown non-zero values: ignored.

81 84 size 0x12 + string  TradeOffered  ctor 0x621454c0, case 0x62176214
  +2 PartnerName string offset u16 | +4 InfoTrade u8 | +5 InfoQuantity u32 | +9 ClassData u32 | +0D InstanceData u32 | +11 ItemQuantity u8
  Ignored if window 0x25 already exists. If auto-ignore is on, client replies 81 85 action 3 without opening.
  Otherwise opens window, 0x62109190: name, partner item if ClassData != 0, partner info if InfoTrade, chat ID_TRADE_OFFER (0x600000A1).

81 86 size 0x13  OfferChanged  ctor 0x62145660, case 0x621761ce, handler 0x62108c40
  +2 AddItem u8 | +3 YourItem u8 | +4 InfoTrade u8 | +5 InfoQuantity u32 | +9 Slot u8 (default 0x6B) | +0A ClassData u32 | +0E InstanceData u32 | +12 ItemQuantity u8
  Ignored unless window exists. Always first unchecks own accept box and sets partner label to OTHER_WAITING_ACCEPT.
  InfoTrade=1: add/remove, your/their info by InfoQuantity (0x62108260, 0x621080e0, 0x62108330, 0x621081a0).
  InfoTrade=0: your item uses Slot (0x62108a80 add, 0x62107b20 remove); their item uses ClassData+InstanceData (0x62108b70, 0x62107c00).
  ClassData/InstanceData use the same pair as the client's inventory slot table at 0x629ea6c0 + slot*0xC.

81 87 size 0x16  SessionStatus  ctor 0x62145820, case 0x62175f78
  +2 Status u32 | +6 NumItemsGiven u32 | +0A NumItemReceived u32 | +0E InfoChange i64 (signed)
  Ignored if window 0x25 does not exist.

Strings: u16 offset from record start (0 = no string). At offset: u16 length including NUL, then bytes. Client writes NUL at str[len-1] (0x6212d0a0).

## Status codes
Encoding: (severityBit<<31) | (category<<24) | id. Lookup 0x622a52a0, registration 0x622a6480. Text ids from resource/string_tables/Strings.ltp (stringtable_client).

81 87 (switch at 0x62175fa7, table 0x6217f1ac):
  8E000019 COMPLETE: all counts 0 -> ID_SECURE_TRADE_COMPLETE_NO_ITEMS; else ID_SECURE_TRADE_SUMMARY with counts and info gained/lost; closes window
  8E00001A COMPLETENOTRADES: no-op, window stays open
  8E00001B CANCELLED: ID_SECURE_TRADE_SESSION_CANCELLED (0x600000BA), closes
  8E00001C YOUACCEPTED: 0x62107980(1) ID_SECURE_TRADE_SELF_ACCEPT
  8E00001D THEYACCEPTED: 0x621079c0(1) ID_SECURE_TRADE_OTHER_ACCEPT, label OTHER_ACCEPTED
  8E00001E YOURESET: 0x62107980(0) ID_SECURE_TRADE_SELF_CANCEL
  8E00001F THEYRESET: 0x621079c0(0) ID_SECURE_TRADE_OTHER_CANCEL, label OTHER_WAITING_ACCEPT
  0E000023 ABORTED: closes silently
  Every other value, including 0E000020..22, 0E000024..2C, 0E000035..37, 0E000086..88: no-op, window stays open.
Close path 0x621761a1: clears all slot locks (0x621c9cc0), closes window 0x25 (0x62020400).

81 83 (0x621762f8, table 0x6217f1c8):
  00000000 open window
  18000002 PARTNERTOOFAR -> ID_TOO_FAR_AWAY (0x60000067)
  18000003 INVALIDITEMOFFERED -> ID_SECURE_TRADE_NO_HAVE_ITEM
  18000004 NONTRADEABLEITEMOFFERED -> ID_SECURE_TRADE_NON_TRADEABLE_ITEM
  18000005 INVALIDPARTNER -> ID_SECURE_TRADE_NOT_PLAYER
  18000006 ININTERLOCK -> ID_SECURE_TRADE_IN_COMBAT
  18000007 DEAD -> ID_SECURE_TRADE_DEAD
  18000008 INPROGRESS -> ID_SECURE_TRADE_ALREADY_IN_PROGRESS
  18000009 EQUIPPEDITEMOFFERED -> ID_SECURE_TRADE_EQUIPPED_ITEM

## Flow
Certain from handlers: what each record does on arrival. Inferred: which side gets what, and when.
1. A drops item or info on B: 81 82.
2. Bad: A gets 81 83 Status=error. Good: A gets 81 83 Status=0, TargetName=B, echo of info and Slot. B gets 81 84 PartnerName=A plus item/info. Both windows open.
3. 81 85 action 0/1: 81 86 to A (YourItem=1, Slot) and to B (YourItem=0, ClassData/InstanceData). Server should clear both accepts: the client UI clears on any 81 86.
4. 81 85 action 4/5: 81 87 YOU* to actor, THEY* to partner.
5. Both accepted: 81 87 COMPLETE to each, with own counts and signed InfoChange. Inventory and info updates must be sent separately; this record changes neither.
6. Action 3: client does not close locally. Server must send 81 87 CANCELLED (or ABORTED).
7. Action 2: client only prints a message. Server presumably removes the offers with 81 86 (inferred).

Client-side checks: my item slot locked at 0x621c9c80 ([inv + (slot+0x28)*0xC] = 1, slots < 0x60); info offered subtracted from available by 0x621c9c50.

## Not found
- SecureTradeFlag: no client code reference found. Name is not in client.dll; gameobjects.pkb lists global attribute 0x21F (also 'Inventory/SecureTradeFlag'). `push 0x21f` hits are only string-table initialisers. Reads through a computed offset cannot be ruled out. The trade window code does not read it directly.
- Display of specific abort/validation reasons: ID_SECURE_TRADE_SESSION_VALIDATION_FAILED_*, ..._PARTNER_DISCONNECTED, ..._ABORTED etc have no immediate reference. Retail probably sent them by a generic StringID record. Which one is unknown.
- Retail server's real order and validation rules: no captures.
- Exact meaning of drag source types and UI event 'TrOk' placement (no strings; UI layout not parsed).
