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

## Owned weapons

Ordinary gun loot now uses descriptor `10`, also present in retail tutorial loot `crashover2k_002_create_mxoemu_character2.log.txt` lines 19367 and 19376. The vendor, tutorial-specific grant and existing dev grant already send their own captured item-add forms.

| Path | Trigger and ordered server inventory records |
| --- | --- |
| Ordinary equip, including `free-attack-equip` | After world entry, equipping a single gun while Free Attack reports fists queues `5f` and `5e` for the source bag slot before the normal `65` moves and EquippedItemID update. In the stock run the client selected slot 11 and then slot 105 with kind 16. Radial Fire! produced `41` and a 159 damage shot. The rifle and displaced gun each appeared once. |
| Disposable `free-attack-bootstrap` | Wait for initial fists, `0c 01`, and the first in-world `3e`. Send the held gun's `5f` and bag `5e` after the setup batch. Return it to slot 105 with `65` on a kind-16 report or after two seconds. A bag-only gun uses `5f` followed by `5e` for its saved slot. |

The stock equip proof and the earlier login failure are recorded together. The earlier login sent the bag addition and immediate move-back in the same refresh and received no new selection. [PR332 native result](https://github.com/kylebrussell/mxo-hd/pull/332#issuecomment-7007468901)

The delayed login test keeps the discovery slot visible until the client can select it. The Margin snapshot and persisted inventory stay intact. A temporary bag-slot report can resolve to zero before the follow-up hand-slot report resolves the actual gun. The replay checks both orders of the readiness reports, exactly one hand restoration on reply or timeout, and the original inventory row identities. The equip replay runs with regular production settings and checks that an existing Free Attack choice goes through the ordinary moves. Both replays provide the client requests explicitly.

## Native acceptance

1. Run `free-attack-bootstrap` with a fresh client process. Capture initial fists, loading-complete, and the first `3e`. Then capture the delayed bag addition, any automatic bag `3f`, and the move back to 105. Check the final equipped item and inventory, including if the two-second timeout is used. Right-click Melee Dummy and choose Fire!. Capture final `3f 69` kind 16, `41`, and the shot.
2. Run `free-attack-equip` with another fresh client process. With fists showing, use `?combatdepth fixture-weapon rifle`. The mode uses the production equip path. Capture the bag refresh, both moves, and EquippedItemID. Check that the client selects slot 105, the rifle is in hand, the previous gun appears once in the bag, and radial Fire! sends `41`.
