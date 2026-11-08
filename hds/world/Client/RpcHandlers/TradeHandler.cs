using System;
using System.Collections.Generic;
using System.Linq;
using hds.databases;
using hds.shared;

namespace hds
{
    /// <summary>
    /// Secure trade between two players (client 81 82, 81 85; server 81 83, 81 84, 81 86, 81 87). The records come
    /// from client.dll, the flow and the checks are our reading of it: see docs/SECURE-TRADE.md.
    /// </summary>
    public class TradeHandler
    {
        /// <summary>How far apart (metres) two players can start a trade. A guess: the client only has the message.</summary>
        public const double MaxDistanceMetres = 30;

        private const int FirstEquippedSlot = 97;

        private static readonly object TradeLock = new object();
        private static readonly Dictionary<WorldClient, TradeSession> Sessions = new Dictionary<WorldClient, TradeSession>();

        private static void Send(WorldClient client, byte[] record)
        {
            client.messageQueue.addRpcMessage(record);
            client.FlushQueue();
        }

        private static string NameOf(WorldClient client)
        {
            return StringUtils.charBytesToString_NZ(client.playerInstance.CharacterName.getValue());
        }

        private static WorldClient FindPlayerInView(WorldClient client, UInt32 targetId)
        {
            ClientView view = client.viewMan.getViewById((UInt16)targetId);
            if (view == null)
            {
                return null;
            }
            return SocialHandler.OnlineWhere(c => c != client && c.playerData.getEntityId() == view.entityId).FirstOrDefault();
        }

        private static double DistanceMetres(WorldClient a, WorldClient b)
        {
            double ax = 0, ay = 0, az = 0, bx = 0, by = 0, bz = 0;
            NumericalUtils.LtVector3dToDoubles(a.playerInstance.Position.getValue(), ref ax, ref ay, ref az);
            NumericalUtils.LtVector3dToDoubles(b.playerInstance.Position.getValue(), ref bx, ref by, ref bz);
            return Math.Sqrt((ax - bx) * (ax - bx) + (az - bz) * (az - bz)) / 100;
        }

        private static bool InCombat(WorldClient client)
        {
            return CombatManager.GetHandler(client).isCombatRunning;
        }

        /// <summary>Reads an item out of the bag for an offer; null when the slot doesn't hold enough of it.</summary>
        private static TradeItem ReadItem(WorldClient client, byte slot, byte quantity, out UInt32 refusal)
        {
            refusal = 0;
            if (slot >= FirstEquippedSlot)
            {
                refusal = TradePackets.RequestEquipped;
                return null;
            }
            UInt32 charId = client.playerData.getCharID();
            MarketPlaceHandler.Db.GetInventoryItem(charId, slot, out UInt32 goid, out UInt32 purity);
            int count = ItemStacks.CountFromDb(MarketPlaceHandler.Db.GetInventoryCount(charId, slot));
            if (goid == 0 || quantity > count)
            {
                refusal = TradePackets.RequestNoSuchItem;
                return null;
            }
            // The client leaves the quantity at 0 for a whole stack
            return new TradeItem { Slot = slot, Goid = goid, Purity = purity, Quantity = quantity == 0 ? count : quantity };
        }

        /// <summary>Client 81 82: drop an item or information on a player.</summary>
        public void ProcessRequest(ref byte[] packet)
        {
            WorldClient client = Store.currentClient;
            if (!TradeRequests.TryParseRequest(packet, out TradeRequests.Request request))
            {
                Output.WriteDebugLog("[TRADE] Unreadable request " + StringUtils.bytesToString(packet));
                return;
            }
            Output.WriteDebugLog("[TRADE] Request by " + NameOf(client) + ": target " + request.TargetId.ToString("x8") +
                                 ", info " + request.Info + ", slot " + request.Slot);

            lock (TradeLock)
            {
                WorldClient target = FindPlayerInView(client, request.TargetId);
                string targetName = target != null ? NameOf(target) : "";
                TradeItem item = null;
                UInt32 refusal = 0;

                if (client.playerData.isDead) refusal = TradePackets.RequestDead;
                else if (target == null) refusal = TradePackets.RequestNotAPlayer;
                else if (target.playerData.isDead) refusal = TradePackets.RequestDead;
                else if (Sessions.ContainsKey(client) || Sessions.ContainsKey(target)) refusal = TradePackets.RequestInProgress;
                else if (InCombat(client) || InCombat(target)) refusal = TradePackets.RequestInCombat;
                else if (DistanceMetres(client, target) > MaxDistanceMetres) refusal = TradePackets.RequestTooFar;
                else if (request.Info > client.playerData.getInfo()) refusal = TradePackets.RequestNoSuchItem;
                else if (request.Slot != TradePackets.NoSlot) item = ReadItem(client, request.Slot, request.ItemQuantity, out refusal);

                if (refusal != 0)
                {
                    Output.WriteDebugLog("[TRADE] Refused: " + refusal.ToString("x8"));
                    Send(client, TradePackets.BuildRequestResult(targetName, refusal, 0, TradePackets.NoSlot, 0));
                    return;
                }

                TradeSession session = new TradeSession(client, target);
                session.RequesterOffer.Info = request.Info;
                UInt32 classData = 0, instanceData = 0;
                byte quantity = 0;
                if (item != null)
                {
                    session.RequesterOffer.Items[item.Slot] = item;
                    classData = item.Goid;
                    instanceData = TradePackets.Descriptor(item.Purity, item.Quantity);
                    quantity = (byte)item.Quantity;
                }
                Sessions[client] = session;
                Sessions[target] = session;

                Send(client, TradePackets.BuildRequestResult(targetName, TradePackets.RequestOk, request.Info, request.Slot, request.ItemQuantity));
                Send(target, TradePackets.BuildOffered(NameOf(client), request.Info, classData, instanceData, quantity));
                Output.WriteDebugLog("[TRADE] Session open: " + NameOf(client) + " with " + targetName);
            }
        }

        /// <summary>Client 81 85: change the offer, accept, reset or cancel.</summary>
        public void ProcessAction(ref byte[] packet)
        {
            WorldClient client = Store.currentClient;
            if (!TradeRequests.TryParseChange(packet, out TradeRequests.Change change))
            {
                Output.WriteDebugLog("[TRADE] Unreadable action " + StringUtils.bytesToString(packet));
                return;
            }
            Output.WriteDebugLog("[TRADE] Action " + change.Action + " by " + NameOf(client) + ": info " + change.Info + ", slot " + change.Slot);

            lock (TradeLock)
            {
                if (!Sessions.TryGetValue(client, out TradeSession session))
                {
                    Output.WriteDebugLog("[TRADE] No session for this action");
                    return;
                }
                WorldClient partner = session.PartnerOf(client);
                TradeOffer offer = session.OfferOf(client);

                switch (change.Action)
                {
                    case TradeRequests.ActionAdd:
                        AddToOffer(session, client, offer, partner, change);
                        break;

                    case TradeRequests.ActionRemove:
                        RemoveFromOffer(session, client, offer, partner, change);
                        break;

                    case TradeRequests.ActionClear:
                        ClearOffer(session, client, offer, partner);
                        break;

                    case TradeRequests.ActionCancel:
                        End(session);
                        Send(client, TradePackets.BuildStatus(TradePackets.StatusCancelled));
                        Send(partner, TradePackets.BuildStatus(TradePackets.StatusCancelled));
                        Output.WriteDebugLog("[TRADE] Cancelled");
                        break;

                    case TradeRequests.ActionAccept:
                        offer.Accepted = true;
                        Send(client, TradePackets.BuildStatus(TradePackets.StatusYouAccepted));
                        Send(partner, TradePackets.BuildStatus(TradePackets.StatusTheyAccepted));
                        if (session.BothAccepted)
                        {
                            Complete(session);
                        }
                        break;

                    case TradeRequests.ActionResetAccept:
                        offer.Accepted = false;
                        Send(client, TradePackets.BuildStatus(TradePackets.StatusYouReset));
                        Send(partner, TradePackets.BuildStatus(TradePackets.StatusTheyReset));
                        break;

                    default:
                        Output.WriteDebugLog("[TRADE] Unknown action " + change.Action);
                        break;
                }
            }
        }

        private static void SendChange(WorldClient owner, WorldClient partner, bool add, UInt32 info, TradeItem item)
        {
            byte slot = item != null ? item.Slot : TradePackets.NoSlot;
            byte quantity = item != null ? (byte)item.Quantity : (byte)0;
            UInt32 classData = item != null ? item.Goid : 0;
            UInt32 instanceData = item != null ? TradePackets.Descriptor(item.Purity, item.Quantity) : 0;
            Send(owner, TradePackets.BuildChanged(add, true, info, slot, 0, 0, quantity));
            Send(partner, TradePackets.BuildChanged(add, false, info, TradePackets.NoSlot, classData, instanceData, quantity));
        }

        private static void AddToOffer(TradeSession session, WorldClient client, TradeOffer offer, WorldClient partner, TradeRequests.Change change)
        {
            if (change.InfoTrade)
            {
                if (offer.Info + change.Info > client.playerData.getInfo())
                {
                    new ServerPackets().sendSystemChatMessage(client, "You do not have enough information.", "SYSTEM");
                    return;
                }
                offer.Info += change.Info;
                session.ClearAccepts();
                SendChange(client, partner, true, change.Info, null);
                return;
            }

            if (offer.Items.ContainsKey(change.Slot))
            {
                return;
            }
            TradeItem item = ReadItem(client, change.Slot, change.ItemQuantity, out UInt32 refusal);
            if (item == null)
            {
                Output.WriteDebugLog("[TRADE] Item refused: " + refusal.ToString("x8"));
                return;
            }
            offer.Items[item.Slot] = item;
            session.ClearAccepts();
            SendChange(client, partner, true, 0, item);
        }

        private static void RemoveFromOffer(TradeSession session, WorldClient client, TradeOffer offer, WorldClient partner, TradeRequests.Change change)
        {
            if (change.InfoTrade)
            {
                UInt32 amount = (UInt32)Math.Min(offer.Info, change.Info);
                offer.Info -= amount;
                session.ClearAccepts();
                SendChange(client, partner, false, amount, null);
                return;
            }

            if (offer.Items.TryGetValue(change.Slot, out TradeItem item))
            {
                offer.Items.Remove(change.Slot);
                session.ClearAccepts();
                SendChange(client, partner, false, 0, item);
            }
        }

        private static void ClearOffer(TradeSession session, WorldClient client, TradeOffer offer, WorldClient partner)
        {
            foreach (TradeItem item in offer.Items.Values.ToList())
            {
                SendChange(client, partner, false, 0, item);
            }
            if (offer.Info > 0)
            {
                SendChange(client, partner, false, (UInt32)offer.Info, null);
            }
            offer.Items.Clear();
            offer.Info = 0;
            session.ClearAccepts();
        }

        private static void End(TradeSession session)
        {
            Sessions.Remove(session.Requester);
            Sessions.Remove(session.Target);
        }

        /// <summary>Why this side can't hand over its offer now, or null. The bag may have changed since the offer.</summary>
        private static string CheckOffer(WorldClient giver, TradeOffer offer)
        {
            if (offer.Info > giver.playerData.getInfo())
            {
                return NameOf(giver) + " does not have enough information.";
            }
            UInt32 charId = giver.playerData.getCharID();
            foreach (TradeItem item in offer.Items.Values)
            {
                MarketPlaceHandler.Db.GetInventoryItem(charId, item.Slot, out UInt32 goid, out UInt32 ignored);
                if (goid != item.Goid || ItemStacks.CountFromDb(MarketPlaceHandler.Db.GetInventoryCount(charId, item.Slot)) < item.Quantity)
                {
                    return NameOf(giver) + " no longer has an offered item.";
                }
            }
            return null;
        }

        /// <summary>Bag slots the receiver has free once its own offered whole stacks are gone.</summary>
        private static List<UInt16> FreeSlotsFor(WorldClient receiver, TradeOffer ownOffer)
        {
            HashSet<int> occupied = new HashSet<int>();
            foreach (Crafting.InventoryItem item in Store.dbManager.WorldDbHandler.GetInventoryItems(receiver.playerData.getCharID()))
            {
                bool leaves = ownOffer.Items.TryGetValue((byte)item.Slot, out TradeItem offered) &&
                              offered.Quantity >= ItemStacks.CountFromDb(item.Count);
                if (!leaves)
                {
                    occupied.Add(item.Slot);
                }
            }
            return TradeRules.FreeBagSlots(occupied);
        }

        private static void TakeFrom(WorldClient giver, TradeItem item)
        {
            UInt32 charId = giver.playerData.getCharID();
            int count = ItemStacks.CountFromDb(MarketPlaceHandler.Db.GetInventoryCount(charId, item.Slot));
            if (item.Quantity >= count)
            {
                MarketPlaceHandler.Db.RemoveInventoryItem(charId, item.Slot);
                giver.messageQueue.addRpcMessage(ServerPackets.BuildInventoryItemDelete(item.Slot));
            }
            else
            {
                Store.dbManager.WorldDbHandler.SetInventoryCount(item.Slot, charId, count - item.Quantity);
                giver.messageQueue.addRpcMessage(ItemStacks.BuildStackCount(item.Slot, count - item.Quantity));
            }
        }

        private static void GiveTo(WorldClient receiver, TradeItem item, Queue<UInt16> freeSlots)
        {
            UInt32 charId = receiver.playerData.getCharID();
            if (ItemStacks.IsStackable(item.Goid))
            {
                foreach (Crafting.InventoryItem existing in Store.dbManager.WorldDbHandler.GetInventoryItems(charId))
                {
                    int count = ItemStacks.CountFromDb(existing.Count);
                    if (existing.Slot < FirstEquippedSlot && existing.Goid == item.Goid && count + item.Quantity <= ItemStacks.MaxStack)
                    {
                        Store.dbManager.WorldDbHandler.SetInventoryCount(existing.Slot, charId, count + item.Quantity);
                        receiver.messageQueue.addRpcMessage(ItemStacks.BuildStackCount(existing.Slot, count + item.Quantity));
                        return;
                    }
                }
            }

            UInt16 slot = freeSlots.Dequeue();
            UInt32 descriptor = TradePackets.Descriptor(item.Purity, item.Quantity);
            MarketPlaceHandler.Db.AddInventoryItem(charId, slot, item.Goid, item.Purity);
            if (item.Quantity > 1)
            {
                Store.dbManager.WorldDbHandler.SetInventoryCount(slot, charId, item.Quantity);
            }
            receiver.messageQueue.addRpcMessage(ServerPackets.BuildInventoryItemAdd(slot, item.Goid,
                (byte)((descriptor >> 16) & 0xff), (byte)(descriptor >> 24)));
        }

        /// <summary>Both accepted: re-check everything, then move the items and information and report to each side.</summary>
        private static void Complete(TradeSession session)
        {
            WorldClient a = session.Requester, b = session.Target;
            TradeOffer offerA = session.RequesterOffer, offerB = session.TargetOffer;
            End(session);

            List<UInt16> freeA = FreeSlotsFor(a, offerA), freeB = FreeSlotsFor(b, offerB);
            string failure = CheckOffer(a, offerA) ?? CheckOffer(b, offerB);
            if (failure == null && freeB.Count < offerA.Items.Count) failure = NameOf(b) + " does not have enough backpack space.";
            if (failure == null && freeA.Count < offerB.Items.Count) failure = NameOf(a) + " does not have enough backpack space.";
            if (failure != null)
            {
                // The client has no message for these in the status record itself: say why, then close both windows
                Output.WriteDebugLog("[TRADE] Aborted: " + failure);
                foreach (WorldClient side in new[] { a, b })
                {
                    new ServerPackets().sendSystemChatMessage(side, "Trade cancelled: " + failure, "SYSTEM");
                    Send(side, TradePackets.BuildStatus(TradePackets.StatusAborted));
                }
                return;
            }

            Queue<UInt16> slotsA = new Queue<UInt16>(freeA), slotsB = new Queue<UInt16>(freeB);
            foreach (TradeItem item in offerA.Items.Values) TakeFrom(a, item);
            foreach (TradeItem item in offerB.Items.Values) TakeFrom(b, item);
            foreach (TradeItem item in offerA.Items.Values) GiveTo(b, item, slotsB);
            foreach (TradeItem item in offerB.Items.Values) GiveTo(a, item, slotsA);

            long changeA = offerB.Info - offerA.Info;
            ApplyInfo(a, changeA);
            ApplyInfo(b, -changeA);

            uint itemsA = (uint)offerA.Items.Values.Sum(i => i.Quantity), itemsB = (uint)offerB.Items.Values.Sum(i => i.Quantity);
            Send(a, TradePackets.BuildStatus(TradePackets.StatusComplete, itemsA, itemsB, changeA));
            Send(b, TradePackets.BuildStatus(TradePackets.StatusComplete, itemsB, itemsA, -changeA));
            Output.WriteDebugLog("[TRADE] Complete: " + NameOf(a) + " gave " + itemsA + " items and " + offerA.Info + " info, " +
                                 NameOf(b) + " gave " + itemsB + " items and " + offerB.Info + " info");
        }

        private static void ApplyInfo(WorldClient client, long change)
        {
            if (change == 0)
            {
                return;
            }
            long info = client.playerData.getInfo() + change;
            client.playerData.setInfo(info);
            Store.dbManager.WorldDbHandler.SaveInfo(client, info);
            client.messageQueue.addRpcMessage(ServerPackets.BuildInfoCurrent((UInt32)info, MarketplacePackets.InfoReasonMarketplace));
        }

        /// <summary>This player is leaving (disconnect, Loading Area, world reload, death): the trade ends for both.</summary>
        public static void Abort(WorldClient client)
        {
            lock (TradeLock)
            {
                if (!Sessions.TryGetValue(client, out TradeSession session))
                {
                    return;
                }
                End(session);
                Output.WriteDebugLog("[TRADE] Aborted: " + NameOf(client) + " left");
                WorldClient partner = session.PartnerOf(client);
                new ServerPackets().sendSystemChatMessage(partner, "Trade cancelled: your partner is gone.", "SYSTEM");
                Send(partner, TradePackets.BuildStatus(TradePackets.StatusAborted));
            }
        }
    }

    public static class TradeRules
    {
        public static List<UInt16> FreeBagSlots(ICollection<int> occupied)
        {
            List<UInt16> free = new List<UInt16>();
            for (UInt16 slot = 1; slot <= MyWorldDbAccess.LastBagSlot; slot++)
            {
                if (!occupied.Contains(slot))
                {
                    free.Add(slot);
                }
            }
            return free;
        }
    }
}
