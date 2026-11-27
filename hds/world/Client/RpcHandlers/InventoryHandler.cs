using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Collections;
using System.Text;

using hds.shared;

namespace hds
{
    class InventoryHandler
    {
        private sealed class ItemCast { }
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<WorldClient, ItemCast> itemCasts =
            new System.Runtime.CompilerServices.ConditionalWeakTable<WorldClient, ItemCast>();

        private static bool ItemReady(WorldClient client) => client.Alive && client.playerData.getOnWorld() &&
            !client.playerData.worldChangePending && !client.playerData.DistrictIsLA() && ControlActions.CanUseAbilities(client);

        private static bool OutsideCombat(WorldClient client)
        {
            var combat = CombatManager.GetHandler(client);
            return !combat.isCombatRunning && !combat.HasActiveInterlock;
        }

        public void processItemMove(ref byte[] packet)
        {
            WorldClient client = Store.currentClient;
            var publication = new AbilityAcquisition.Publication(client);
            ushort source = packet?.Length >= 2 ? BitConverter.ToUInt16(packet, 0) : (ushort)0;
            if (packet == null || (packet.Length != 4 && packet.Length != 5) ||
                (packet.Length == 5 && packet[4] != 0))
            {
                RefuseSlotMove(client, publication, source, "That inventory move is unavailable.");
                return;
            }

            ushort dest = BitConverter.ToUInt16(packet, 2);
            if (PrisonDevices.Equipment(client, source, dest)) return;
            if (source > PvpArchives.BookSlot || dest > PvpArchives.BookSlot)
            {
                RefuseSlotMove(client, publication, source, "That inventory slot is unavailable.");
                return;
            }

            if (source != dest)
            {
                string refusal = CommitSlotMove(client, publication.Character, source, dest);
                if (refusal != null)
                {
                    RefuseSlotMove(client, publication, source, refusal);
                    return;
                }
            }

            if (publication.Publish(client, ServerPackets.BuildInventoryItemMove(source, dest))) client.FlushQueue();
            Output.WriteDebugLog($"[INVENTORY] Slot move committed char={publication.Character} source={source} dest={dest}");
        }

        private static string CommitSlotMove(WorldClient client, uint character, ushort source, ushort dest)
        {
            try
            {
                lock (client.playerData.missionSync)
                {
                    // These destination slots require a mission checkpoint change; regular swaps
                    // must never displace their rows. The dedicated source move expects an empty slot.
                    if (client.playerData.missionProgress?.itemSlots.ContainsValue(dest) == true)
                        return "That slot holds a mission item. Your items were kept.";

                    if (!MissionPersistence.MoveItem(client, source, dest) &&
                        !Store.dbManager.WorldDbHandler.MoveInventorySlots(character, source, dest))
                        return "Those slots cannot be moved. Your items were kept.";

                    TutorialMission.Of(client)?.OnInventoryItemMoved(source, dest);
                    return null;
                }
            }
            catch (Exception error) when (error is DbException || error is MissionCheckpointException)
            {
                Output.WriteDebugLog($"[INVENTORY] Slot move rolled back char={character} source={source} dest={dest}: {error.Message}");
                return "The inventory move could not be saved. Your items were kept.";
            }
        }

        private static void RefuseSlotMove(WorldClient client, AbilityAcquisition.Publication publication,
            ushort source, string reason)
        {
            if (source > PvpArchives.BookSlot) source = 0;
            if (publication.Publish(client, ServerPackets.BuildInventoryItemMove(source, source),
                ServerPackets.BuildSystemChatMessage(reason, "BROADCAST"))) client.FlushQueue();
        }

        public void processItemAdd(UInt32 itemID, UInt16 type)
        {
            // Called from looting and vendor buy (vendor buy takes care of the money).
            AddItem(Store.currentClient, itemID, type);
        }

        /// <summary>
        /// Puts one item into the inventory. A stackable item (Code Bits, code fragments, pills) joins a stack of the
        /// same item that has room (inventory.count + 1, <c>69 01 &lt;slot&gt; &lt;(count-1)*4&gt;</c>); anything else, or
        /// a full stack, takes the first free bag slot (<c>5e</c>). Returns the slot.
        /// </summary>
        public static UInt16 AddItem(WorldClient client, UInt32 itemID, UInt16 type)
        {
            UInt32 charId = client.playerData.getCharID();
            ServerPackets pak = new ServerPackets();

            if (ItemStacks.IsStackable(itemID))
            {
                foreach (Crafting.InventoryItem item in Store.dbManager.WorldDbHandler.GetInventoryItems(charId))
                {
                    if (item.Slot < 97 && item.Goid == itemID && item.Count < ItemStacks.MaxStack)
                    {
                        int count = item.Count + 1;
                        Store.dbManager.WorldDbHandler.SetInventoryCount(item.Slot, charId, count);
                        client.messageQueue.addRpcMessage(ItemStacks.BuildStackCount(item.Slot, count));
                        Output.WriteDebugLog("[INVENTORY] Item " + itemID + " stacked in slot " + item.Slot + " (" + count + ")");
                        return item.Slot;
                    }
                }
            }

            UInt16 freeSlot = Store.dbManager.WorldDbHandler.GetFirstNewSlot();
            if (freeSlot == 0)
            {
                pak.sendSystemChatMessage(client, "Your inventory is full.", "BROADCAST");
                Output.WriteDebugLog("[INVENTORY] No free bag slot for item " + itemID);
                return 0;
            }
            Store.dbManager.WorldDbHandler.AddItemToInventory(freeSlot, itemID);
            if (AbilityProgression.IsAbilityCode(itemID))
                client.messageQueue.addRpcMessage(ServerPackets.BuildAbilityCodeAdd(freeSlot, itemID));
            else
                pak.sendInventoryItemAdd(freeSlot, itemID, 0, type, client);
            return freeSlot;
        }

        /// <summary>
        /// Takes one item out of a slot: a stack shrinks by one (<c>69 01</c>, as retail did for a used cookie), the
        /// last item leaves the slot (<c>5f &lt;slot&gt; 01</c>).
        /// </summary>
        public static void RemoveOne(WorldClient client, UInt16 slot)
        {
            UInt32 charId = client.playerData.getCharID();
            int count = 1;
            foreach (Crafting.InventoryItem item in Store.dbManager.WorldDbHandler.GetInventoryItems(charId))
            {
                if (item.Slot == slot)
                {
                    count = item.Count;
                    break;
                }
            }

            if (count > 1)
            {
                Store.dbManager.WorldDbHandler.SetInventoryCount(slot, charId, count - 1);
                client.messageQueue.addRpcMessage(ItemStacks.BuildStackCount(slot, count - 1));
            }
            else
            {
                Store.dbManager.WorldDbHandler.RemoveItemAtInventorySlot(slot, charId);
                new ServerPackets().sendInventoryItemDelete(slot, client);
            }
        }

        public void processItemDelete(ref byte[] packet)
        {
            WorldClient client=Store.currentClient;
            if(!VendorTrading.TryRecycleRequest(packet,out UInt16 slot,out bool personal,out bool recycle))return;
            // No current native UI producer of RecycleItem0 has been recovered. Refuse rather than guess destructive semantics.
            if(!recycle){ClientActionsHandler.Notice(client,"Non-recycle removal is not supported.");return;}
            lock(client.playerData.craftingSync)
            {
                if(!personal){CentralStorageHandler.ProcessRecycle(client,slot);return;}
                var db=Store.dbManager.WorldDbHandler;
                Crafting.InventoryItem item=db.GetInventoryItems(client.playerData.getCharID()).Find(i=>i.Slot==slot);
                if(item.Goid==0){ClientActionsHandler.Notice(client,"There is nothing to recycle in that slot.");return;}
                UInt32 quote=VendorPricing.GetRecyclePrice(item.Goid,item.Count);
                try
                {
                    InventoryPayout result=db.ExchangeInventoryForInfo(client.playerData.getCharID(),slot,item.Goid,item.Count,quote);
                    if(result==null){ClientActionsHandler.Notice(client,"That inventory item changed; try again.");return;}
                    client.playerData.setInfo(result.Cash);
                    var packets=new ServerPackets();
                    packets.SendInfoDelta(client,result.Credited,ServerPackets.InfoReasonRecycle);
                    packets.SendInfoCurrent(client,result.Cash,ServerPackets.InfoReasonRecycle);
                    packets.sendInventoryItemDelete(slot,client);
                }
                catch(Exception e)
                {
                    Output.WriteDebugLog("[INVENTORY] Recycle rolled back: "+e.Message);
                    ClientActionsHandler.Notice(client,"The recycle could not be completed. Your item and information were kept.");
                }
            }
        }

        /// <summary>
        /// Uses the consumable in a bag slot, asked for with the item's ability in a 80 b9 request. Retail order
        /// (Antibiotic 5.0, PacketsZombiesPortingEtc lines 77036-77221): cast bar <c>80 ac</c> right away; when
        /// the cast ends, the item's and the group's timers (<c>80 bd 01 .. 00</c>) and the slot removal
        /// <c>5f &lt;slot&gt; 01</c>. Heal pills heal by the ability's value at that moment.
        /// Returns false when the slot holds no consumable for that ability (a normal ability use then).
        /// </summary>
        public bool UseItem(UInt16 slot, UInt16 abilityId)
        {
            if (slot < 1 || slot > 96) return false;

            WorldClient client = Store.currentClient;
            ClientData data = client.playerData;
            lock (data.craftingSync)
            {
                ServerPackets pak = new ServerPackets();
                UInt32 goid = Store.dbManager.WorldDbHandler.GetItemGOIDAtInventorySlot(slot);
                if (!ConsumableItems.TryGet(goid, out ConsumableItems.Consumable consumable) ||
                    consumable.AbilityId != abilityId && !OptimizerConsumables.MatchesItemAlias(goid, abilityId) &&
                    !AndvaranautCoupler.MatchesItemAlias(goid, abilityId))
                    return false;
                abilityId = consumable.AbilityId;
                bool appearance = consumable.Effect == ConsumableItems.Effect.Appearance;
                bool blueMarble = BlueMarbles.Handles(abilityId);
                bool andvaranaut = abilityId == AndvaranautCoupler.Ability;
                bool requiresOutsideCombat = appearance || blueMarble || andvaranaut;
                bool timedEffect = consumable.Effect == ConsumableItems.Effect.TimedBuff ||
                    consumable.Effect == ConsumableItems.Effect.HealOverTime;
                if (Stealth.IsOn(client, Stealth.MasterShadowAbility) && AbilityCatalog.Find(abilityId)?.CastSeconds > 0)
                {
                    pak.sendSystemChatMessage(client, "You cannot use a timed item while Master Shadow is active.", "SYSTEM");
                    return true;
                }
                if (AntiStunPills.Use(client, slot, abilityId, goid)) return true;
                if (HealthBoostPills.Handles(abilityId)) return HealthBoostPills.Use(client, slot, abilityId, goid);
                if (!ConsumableItems.IsUsable(consumable))
                {
                    pak.sendSystemChatMessage(client, "This item can't be used yet.", "MODAL");
                    return true;
                }
                if (client.playerInstance.Level.getValue()[0] < ConsumableItems.RequiredLevel(goid))
                {
                    pak.sendSystemChatMessage(client, "Your character level is too low to use this item.", "MODAL");
                    return true;
                }
                if (appearance)
                {
                    RsiPills.TryGet(goid, out var pill);
                    if (pill.Resolve(data.getRsiValues()[0], DataLoader.getInstance().newRSIItemsDb) < 0)
                    {
                        ClientActionsHandler.Notice(client, "This RSI pill is not compatible with your character.");
                        return true;
                    }
                }
                if (requiresOutsideCombat && !OutsideCombat(client))
                {
                    client.messageQueue.addRpcMessage(AbilityRefusals.Encode(
                        new AbilityRefusals.Record(abilityId, AbilityRefusals.MustBeOutsideCombat)));
                    client.FlushQueue();
                    return true;
                }
                int requiredRank = blueMarble ? BlueMarbles.RequiredAwakenedRank(abilityId)
                    : andvaranaut ? AndvaranautCoupler.RequiredAwakenedRank : 0;
                if (requiredRank > 0 && AbilityPassives.Level(client, AbilityProgression.AwakenedAbility) < requiredRank)
                {
                    client.messageQueue.addRpcMessage(AbilityRefusals.Encode(
                        new AbilityRefusals.Record(abilityId, AbilityRefusals.NoClientMessage)));
                    string itemName = blueMarble ? "This Blue Marble" : "The Andvaranaut Coupler";
                    ClientActionsHandler.Notice(client, $"{itemName} requires Awakened rank {requiredRank}.");
                    client.FlushQueue();
                    return true;
                }
                double now = TimeUtils.GetSimTimeSeconds();
                bool busy = !ItemReady(client) || itemCasts.TryGetValue(client, out _) || AbilityCooldowns.IsCasting(data, now);
                if (busy || AbilityCooldowns.IsTimerRunning(data, (UInt16)goid, now) ||
                    (consumable.Group != 0 && AbilityCooldowns.IsTimerRunning(data, consumable.Group, now)))
                {
                    client.messageQueue.addRpcMessage(AbilityCooldowns.BuildDenied(abilityId,
                        busy ? AbilityCooldowns.DeniedBusy : AbilityCooldowns.DeniedTimerRunning));
                    client.FlushQueue();
                    return true;
                }

                if (TacticBoostItems.Handles(abilityId) && TacticBoostItems.Outranked(client, abilityId))
                {
                    client.messageQueue.addRpcMessage(AbilityRefusals.Encode(
                        new AbilityRefusals.Record(abilityId, AbilityRefusals.NoClientMessage)));
                    ClientActionsHandler.Notice(client, "A stronger Tactic Boost is already active.");
                    client.FlushQueue();
                    return true;
                }

                // Both inventory paths interrupt compiling only after the pill passes its eligibility checks.
                CraftingSession.End(client);
                AbilityItem ability = DataLoader.getInstance().getAbilityByID(abilityId);
                var definition = AbilityCatalog.Find(abilityId);
                float castTime = definition?.Has("Ability/CastingTime") == true
                    ? definition.CastSeconds : ability?.getCastingTime() ?? 0f;
                UInt16 amount = (UInt16)Math.Max(1, ability != null ? ability.getValueFrom() : 1);
                int world = data.worldGeneration, life = data.lifeGeneration;
                var token = new ItemCast();
                itemCasts.Add(client, token);
                AbilityCooldowns.StartCast(data, now, castTime);
                double castEnd = data.castEndsAt;
                double reservedUntil = now + castTime + consumable.Seconds;
                if (consumable.Seconds > 0) lock (data.cooldownEndsAt)
                {
                    data.cooldownEndsAt[(UInt16)goid] = reservedUntil;
                    if (consumable.Group != 0) data.cooldownEndsAt[consumable.Group] = reservedUntil;
                }
                if (castTime > 0)
                {
                    pak.SendCastAbilityBar(abilityId, castTime);
                    client.FlushQueue();
                }
                DelayedActions.AfterFor(client, castTime, () =>
                {
                    lock (data.craftingSync)
                    {
                        if (!itemCasts.TryGetValue(client, out var current) || current != token) return;
                        itemCasts.Remove(client);
                        if (client.playerData != data || !ItemReady(client) ||
                            !ConsumableItems.CastMatches(data, world, life, castEnd) ||
                            (requiresOutsideCombat && !OutsideCombat(client)) ||
                            Store.dbManager.WorldDbHandler.GetItemGOIDAtInventorySlot(slot) != goid)
                        {
                            ReleaseReservation();
                            Output.WriteDebugLog("[INVENTORY] Item " + goid + " cast cancelled (slot, life, control, cast or world changed); not used");
                            return;
                        }
                        if (appearance)
                        {
                            CommitRsiPill(client, slot, goid);
                            return;
                        }
                        byte[] removal = CommitDose(client, slot, goid, out string refusal);
                        if (removal == null)
                        {
                            if (consumable.Seconds > 0) ReleaseReservation();
                            ReportDoseRefusal(client, refusal);
                            return;
                        }
                        if (timedEffect)
                        {
                            client.messageQueue.addRpcMessage(removal);
                            ApplyTimedItemEffect(client, goid, consumable);
                            return;
                        }
                        AbilityCooldowns.StartItem(client, (UInt16)goid, consumable.Group, consumable.Seconds, TimeUtils.GetSimTimeSeconds());
                        client.messageQueue.addRpcMessage(removal);
                        client.FlushQueue();
                        if (consumable.Effect == ConsumableItems.Effect.Heal)
                            AbilityHandler.HealSelf(client, amount);
                        else if (consumable.Effect == ConsumableItems.Effect.Cure || consumable.Effect == ConsumableItems.Effect.ConditionSweep)
                            // The item grants a canonical ability; its modifier is ability-owned, not an equipped item modifier.
                            AbilityHostile.ApplySweep(client, client, abilityId);
                        Output.WriteDebugLog("[INVENTORY] Used item " + goid + " from slot " + slot + " (ability " + abilityId + ")");
                    }
                });
                return true;

                void ReleaseReservation()
                {
                    lock (data.cooldownEndsAt)
                    {
                        if (data.cooldownEndsAt.TryGetValue((UInt16)goid, out double itemEnd) && itemEnd == reservedUntil)
                            data.cooldownEndsAt.Remove((UInt16)goid);
                        if (consumable.Group != 0 && data.cooldownEndsAt.TryGetValue(consumable.Group, out double groupEnd) && groupEnd == reservedUntil)
                            data.cooldownEndsAt.Remove(consumable.Group);
                    }
                }
            }
        }

        // Commit is separate from publication so buff admission can spend under its own lock.
        internal static byte[] CommitDose(WorldClient client, ushort slot, uint goid, out string refusal)
        {
            refusal = null;
            uint character = client.playerData.getCharID();
            int remaining;
            try { remaining = Store.dbManager.WorldDbHandler.ConsumeOwnedBagItem(character, slot, goid); }
            catch (System.Data.Common.DbException error)
            {
                Output.WriteDebugLog($"[INVENTORY] Item use rolled back char={character} item={goid} slot={slot}: {error.Message}");
                refusal = "This item could not be used. Your item was kept.";
                return null;
            }
            if (remaining < 0)
            {
                refusal = "The item in that bag slot changed. Try again.";
                return null;
            }
            return remaining == 0 ? ServerPackets.BuildInventoryItemDelete(slot) : ItemStacks.BuildStackCount(slot, remaining);
        }

        internal static void ReportDoseRefusal(WorldClient client, string refusal)
        {
            if (refusal != null) new ServerPackets().sendSystemChatMessage(client, refusal, "BROADCAST");
        }

        private static void ApplyTimedItemEffect(WorldClient client, uint goid, ConsumableItems.Consumable consumable)
        {
            if (consumable.Effect == ConsumableItems.Effect.HealOverTime)
                AbilitySupport.Land(client, consumable.AbilityId, 2);
            else if (OptimizerConsumables.Handles(consumable.AbilityId))
                OptimizerConsumables.Apply(client, consumable.AbilityId);
            else if (consumable.AbilityId == SmithRagePill.Ability)
                SmithRagePill.Apply(client);
            else
                AbilityBuffs.Apply(client, consumable.AbilityId, client);
            if (consumable.Seconds > 0)
                AbilityCooldowns.StartItem(client, (ushort)goid, consumable.Group, consumable.Seconds,
                    TimeUtils.GetSimTimeSeconds());
            if (TacticBoostItems.Handles(consumable.AbilityId))
                ServerPackets.SendPlayerAbilityEffect(client, TacticBoostItems.ActivationFx);
            else if (BlueMarbles.Handles(consumable.AbilityId))
                ServerPackets.SendPlayerAbilityEffect(client, BlueMarbles.ActivationFx(consumable.AbilityId));
            else if (consumable.AbilityId == AndvaranautCoupler.Ability)
                ServerPackets.SendPlayerAbilityEffect(client, AndvaranautCoupler.ActivationFx);
            else if (EventHealthRegenItems.Handles(consumable.AbilityId))
                ServerPackets.SendPlayerAbilityEffect(client, EventHealthRegenItems.ActivationFx(consumable.AbilityId));
            else client.FlushQueue();
        }

        private static void CommitRsiPill(WorldClient client, ushort slot, uint goid)
        {
            var publication = new AbilityAcquisition.Publication(client);
            var data = client.playerData;
            int[] rsi;
            int remaining;
            try
            {
                lock (data.rsiSync)
                {
                    (rsi, remaining) = Store.dbManager.WorldDbHandler.ConsumeRsiPill(publication.Character, slot, goid);
                    if (rsi != null) data.setRsiValues(rsi);
                }
            }
            catch (System.Data.Common.DbException error)
            {
                Output.WriteDebugLog($"[RSI PILL] Rolled back char={publication.Character} item={goid}: {error.Message}");
                publication.Notice(client, "The RSI change could not be saved. Your pill was kept.");
                return;
            }
            if (rsi == null)
            {
                publication.Notice(client, "The RSI pill could not be used. Your pill was kept.");
                return;
            }

            byte[] inventory = remaining == 0 ? ServerPackets.BuildInventoryItemDelete(slot)
                : ItemStacks.BuildStackCount(slot, remaining);
            if (!publication.Publish(client, inventory, ServerPackets.BuildSystemChatMessage("Your RSI has been updated.", "BROADCAST")))
                return;
            byte[] packed = PacketsUtils.getRSIBytes(rsi);
            client.messageQueue.addObjectMessage(ServerPackets.BuildAppearanceUpdate(packed), false);
            AnnounceAppearance(client, packed);
            client.FlushQueue();
            Output.WriteDebugLog($"[RSI PILL] Applied char={publication.Character} item={goid} slot={slot} remaining={remaining}");
        }

        private static void RefuseEquipment(byte[] request)
        {
            var client = Store.currentClient;
            client.messageQueue.addRpcMessage(ServerPackets.BuildEquipmentRefusal(request));
            new ServerPackets().sendSystemChatMessage(client,
                "That item cannot be equipped there. To use a tool, right-click it and choose Activate Item.", "SYSTEM");
            client.FlushQueue();
        }

        public void processUnmountItem(ref byte[] rpcData)
        {
            if (rpcData == null || rpcData.Length != 2 || rpcData[0] < 0x61 || rpcData[0] > 0x6a ||
                rpcData[1] > 96) { RefuseEquipment(rpcData); return; }
            byte sourceSlot = rpcData[0];
            byte destSlot   = rpcData[1];

            if (PrisonDevices.Equipment(Store.currentClient, sourceSlot, destSlot)) return;
            if (sourceSlot == PvpArchives.BookSlot || destSlot == PvpArchives.BookSlot)
            {
                uint before = Store.dbManager.WorldDbHandler.GetItemGOIDAtInventorySlot(sourceSlot);
                PvpArchives.Equipment(Store.currentClient, sourceSlot, destSlot);
                if (Store.dbManager.WorldDbHandler.GetItemGOIDAtInventorySlot(sourceSlot) == before)
                    RefuseEquipment(rpcData);
                return;
            }

            if (sourceSlot == WeaponSlot)
            {
                UnequipWeapon(destSlot);
                return;
            }

            ChangeClothing(sourceSlot, destSlot, equip: false);
        }

        /// <summary>Other players see a clothing change on their view of this player (RSIDescription).</summary>
        private static void AnnounceAppearance(WorldClient client, byte[] rsiData)
        {
            byte[] rsi = new byte[ServerPackets.RsiDescriptionSize];
            Array.Copy(rsiData, rsi, Math.Min(rsiData.Length, rsi.Length));
            client.playerInstance.RSIDescription.setValue(rsi);
            PlayerProgression.AnnounceToOthers(client, new List<Attribute> { client.playerInstance.RSIDescription });
        }

        /// <summary>The weapon slot on the paper doll; clothing uses 0x61 (hat) to 0x68 (shoes).</summary>
        public const byte WeaponSlot = 0x69;

        /// <summary>Equip follows the stock displaced-weapon move, then the bag-to-hand move.</summary>
        internal static bool EquipWeapon(byte sourceSlot) => ChangeWeapon(sourceSlot, equip: true);

        /// <summary>Send the held weapon to the selected bag slot, or the first free one for zero.</summary>
        internal static bool UnequipWeapon(byte destSlot) => ChangeWeapon(destSlot, equip: false);

        private static bool ChangeWeapon(byte bagSlot, bool equip)
        {
            WorldClient client = Store.currentClient;
            lock (client.playerData.craftingSync)
            {
                var publication = new AbilityAcquisition.Publication(client);
                (uint Goid, ushort BagSlot, ushort DisplacedSlot, string Refusal) move;
                try
                {
                    move = Store.dbManager.WorldDbHandler.MoveWeapon(publication.Character, bagSlot, equip);
                }
                catch (DbException error)
                {
                    Output.WriteDebugLog($"[INVENTORY] Weapon move rolled back char={publication.Character} " +
                        $"slot={bagSlot} equip={equip}: {error.Message}");
                    move = (0, 0, 0, "The weapon move could not be saved. Your items were kept.");
                }
                if (move.Refusal != null)
                {
                    RefuseSlotMove(client, publication, equip ? bagSlot : WeaponSlot, move.Refusal);
                    return false;
                }

                var packets = new ServerPackets();
                if (equip)
                {
                    bool refresh = FreeAttackBootstrap.PrepareEquip(client, move.Goid);
                    lock (client.messageQueue.RPCMessagesQueue.SyncRoot)
                    {
                        if (refresh) FreeAttackBootstrap.RefreshEquip(client, bagSlot, move.Goid);
                        if (move.DisplacedSlot != 0) packets.sendInventoryItemMove(WeaponSlot, move.DisplacedSlot, client);
                        packets.sendInventoryItemMove(bagSlot, WeaponSlot, client);
                    }
                    Output.WriteDebugLog($"[INVENTORY] Equipped weapon {move.Goid} from slot {bagSlot}");
                }
                else
                {
                    packets.sendInventoryItemMove(WeaponSlot, move.BagSlot, client);
                    Output.WriteDebugLog($"[INVENTORY] Unequipped weapon to slot {move.BagSlot}");
                }
                SetEquippedItem(client, move.Goid);
                return true;
            }
        }

        private static void SetEquippedItem(WorldClient client, uint goid)
        {
            client.playerInstance.EquippedItemID.setValue(goid);
            client.messageQueue.addObjectMessage(ServerPackets.BuildEquippedItemUpdate(goid), false);
            client.FlushQueue();
            PlayerProgression.AnnounceToOthers(client, new List<Attribute> { client.playerInstance.EquippedItemID });
        }

        public void processMountItem(ref byte[] rpcData)
        {
            if (rpcData == null || rpcData.Length != 2 || rpcData[0] < 1 || rpcData[0] > 96 ||
                rpcData[1] < 0x61 || rpcData[1] > 0x6b) { RefuseEquipment(rpcData); return; }
            byte sourceSlot = rpcData[0];
            byte destSlot   = rpcData[1];

            if (PrisonDevices.Equipment(Store.currentClient, sourceSlot, destSlot)) return;
            if (sourceSlot == PvpArchives.BookSlot || destSlot == PvpArchives.BookSlot)
            {
                uint before = Store.dbManager.WorldDbHandler.GetItemGOIDAtInventorySlot(sourceSlot);
                PvpArchives.Equipment(Store.currentClient, sourceSlot, destSlot);
                if (Store.dbManager.WorldDbHandler.GetItemGOIDAtInventorySlot(sourceSlot) == before)
                    RefuseEquipment(rpcData);
                return;
            }

            UInt32 sourceItem = Store.dbManager.WorldDbHandler.GetItemGOIDAtInventorySlot(sourceSlot);
            if (destSlot == WeaponSlot || destSlot == 0x6b && RangedWeapons.IsGun(sourceItem))
            {
                if (!RangedWeapons.IsGun(sourceItem)) { RefuseEquipment(rpcData); return; }
                EquipWeapon(sourceSlot);
                return;
            }

            ChangeClothing(sourceSlot, destSlot, equip: true);
        }

        private static void ChangeClothing(byte source, byte destination, bool equip)
        {
            WorldClient client = Store.currentClient;
            var data = client.playerData;
            var publication = new AbilityAcquisition.Publication(client);
            (uint Goid, ushort WornSlot, ushort BagSlot, ushort DisplacedSlot, int[] Rsi, string Refusal) move;
            lock (data.craftingSync)
            {
                try
                {
                    lock (data.rsiSync)
                    {
                        move = Store.dbManager.WorldDbHandler.MoveClothing(publication.Character, source, destination, equip);
                        if (move.Rsi != null) data.setRsiValues(move.Rsi);
                    }
                }
                catch (DbException error)
                {
                    Output.WriteDebugLog($"[INVENTORY] Clothing move rolled back char={publication.Character} " +
                        $"source={source} destination={destination}: {error.Message}");
                    move = (0, 0, 0, 0, null, "The clothing move could not be saved. Your items were kept.");
                }
                if (move.Refusal != null)
                {
                    RefuseSlotMove(client, publication, source, move.Refusal);
                    return;
                }

                byte[] transfer = equip ? ServerPackets.BuildInventoryItemMove(move.BagSlot, move.WornSlot)
                    : ServerPackets.BuildInventoryItemMove(move.WornSlot, move.BagSlot);
                bool published = move.DisplacedSlot == 0 ? publication.Publish(client, transfer)
                    : publication.Publish(client, ServerPackets.BuildInventoryItemMove(move.WornSlot, move.DisplacedSlot), transfer);
                if (!published) return;

                byte[] rsi = PacketsUtils.getRSIBytes(move.Rsi);
                client.messageQueue.addObjectMessage(ServerPackets.BuildAppearanceUpdate(rsi), false);
                AnnounceAppearance(client, rsi);
                client.FlushQueue();
                Output.WriteDebugLog($"[INVENTORY] Clothing move committed char={publication.Character} " +
                    $"goid={move.Goid} worn={move.WornSlot} bag={move.BagSlot} equip={equip}");
            }
            if (equip) TutorialMission.Of(client)?.OnItemEquipped(move.Goid);
        }
    }
}
