# Free Attack initialization

Margin character data type 05 sends the saved inventory before world entry. The own-view creation reads the equipped GOID from hand slot 105. Neither record directly assigns the client toolbar's Free Attack choice. In the retained native run 8a7931515f57, the stock client sent `3f 6b 00 00 00 00 00` (fists) in the same client batch as loading-complete `0c 01`. Right-click and Fire! produced only draw `3d 01`, which the server acknowledged with CombatantMode 21.

## Retail packet paths

| Event | Record and direction |
| --- | --- |
| Redpill Special loot | S→C `5e 04 00 05 0a 00 00 00 00 00 14 01`. Another tutorial capture has descriptor `10` for the same item. |
| First Free Attack selection | C→S `3f 04 00 10 00 00 00`, immediately after the loot. The next server packet has an unrelated own-view update. |
| Equip selected weapon | C→S `63 04 69`. S→C `65 04 00 69 00 00`, own-view EquippedItemID 2565, and equipment-specific bonuses. |
| Client follows the moved item | C→S `3e 69 00 10 00 00 00 01` and `3f 69 00 10 00 00 00`. The two choices are separate. |
| Later world entry | C→S `3f 69 00 10 00 00 00` with character setup RPCs. The client had already chosen and equipped this gun in that process. |

`3e` is Close Combat style selection and may update own-view attribute 39 (CurCombatExclusiveAbility). `3f` reports the client-owned ranged item selection. No server `3f` echo was found in these exchanges. After the stock client sends `41 <target>`, the server can use the reported gun or fall back to the equipped weapon when the choice is empty. The companion `40 <target> 00 <slot>` is ignored while firing. [Capture reference and client sender addresses](COMBAT-SYSTEM-ROADMAP.md)

The native control `3f35506baa90` reproduced the full chain. A genuine grant sent `5e 06 00 05 0a 00 00 00 00 00 10 01` at PacketLog line 1455, and the client sent `3f 06 00 10 00 00 00` at line 1460. The top radial choice then sent `3d 01`, `41 04 00 02 00`, and `40 04 00 02 00 00 06` together at line 1586. The shot landed for 273.

## Owned weapon experiments

Ordinary gun loot now uses descriptor `10`, also present in retail tutorial loot `crashover2k_002_create_mxoemu_character2.log.txt` lines 19367 and 19376. The vendor, tutorial-specific grant and existing dev grant already send their own captured item-add forms.

| Disposable mode | Trigger and ordered server inventory records |
| --- | --- |
| `free-attack-bootstrap` | After initial fists and `0c 01`, a held gun is removed from the client's hand slot by `5f 69 00 01`, added with ordinary `5e` to a genuinely empty bag slot, then moved back to 105 with ordinary `65`. A bag-only gun uses `5f` followed by `5e` for its saved slot. |
| `free-attack-equip` | An ordinary equip while Free Attack reports fists queues `5f` and `5e` for the source bag slot before the ordinary `65` moves. Normal EquippedItemID publication follows. |

The login sequence is a UI refresh for the same inventory item. The original Margin snapshot, persisted slot and inventory row ID remain intact. The intermediate bag slot exists only in the three ordered messages. A client report for that slot can resolve to zero before the expected follow-up `3f 69 00 10 00 00 00` resolves the actual hand item. The equip sequence uses existing database moves. Both compositions need native verification, including the hand-slot removal, selection-follow, and final paper doll state.

Headless replay checks exact packet order, an empty staging slot, the held and bag GOIDs, unchanged login inventory rows, one refresh per session, and isolation from a second client with an existing selection. The equip replay checks the original rifle row in the hand slot, the previous gun displaced to the bag, and the resulting own-view equipped attribute. Replays provide the client selection and Fire! requests explicitly to check the server's shot path.

## Native acceptance

1. Run `free-attack-bootstrap` with a fresh client process. Capture initial fists and loading-complete, then the ordered `5f`, `5e`, and `65`. Check that the revolver remains equipped and there is no extra gun in the bag. Record any automatic `3f`, especially slot 105 kind 16. Right-click Melee Dummy and choose Fire!. Capture `41`, any companion `40`, and the ranged shot.
2. Run `free-attack-equip` with another fresh client process. With fists showing, use `?combatdepth fixture-weapon rifle`. Capture the bag refresh, both moves, and EquippedItemID. Check that the client selects slot 105, the rifle is in hand, the previous gun appears once in the bag, and radial Fire! sends `41`.
