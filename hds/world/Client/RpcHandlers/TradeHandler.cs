using System;
using System.Collections.Generic;
using System.Data.Common;
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

        /// <summary>The dev command's server-side partner (no client behind it); only real players get records.</summary>
        public const string BotName = "TradeBot";
        private const UInt32 BotInfo = 100;
        private const UInt32 BotItem = NpcLoot.CodeBit1;

        private static void Send(WorldClient client, byte[] record)
        {
            if (client == null)
            {
                return;
            }
            client.messageQueue.addRpcMessage(record);
            client.FlushQueue();
        }

        private static string NameOf(WorldClient client)
        {
            if (client == null)
            {
                return BotName;
            }
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
            TradeItem owned = Store.dbManager.WorldDbHandler.GetTradeItem(client.playerData.getCharID(), slot);
            if (owned == null || quantity > owned.Quantity)
            {
                refusal = TradePackets.RequestNoSuchItem;
                return null;
            }
            if (DataMining.IsNonTradeable(owned.Goid))
            {
                refusal = TradePackets.RequestNotTradeable;
                return null;
            }
            // The client leaves the quantity at 0 for a whole stack.
            if (quantity != 0) owned.Quantity = quantity;
            return owned;
        }

        /// <summary>
        /// Dev command ?tradebot: opens a trade with a partner played by the server, offering a Code Bit and some
        /// information, and accepting as soon as the player does. Exercises the client's whole trade window with one client.
        /// </summary>
        public static void StartBotTrade(WorldClient client)
        {
            lock (TradeLock)
            {
                if (Sessions.ContainsKey(client))
                {
                    new ServerPackets().sendSystemChatMessage(client, "You are already trading.", "SYSTEM");
                    return;
                }
                TradeSession session = new TradeSession(client, null);
                session.TargetOffer.Info = BotInfo;
                session.TargetOffer.Items[0] = new TradeItem { Slot = 0, Goid = BotItem, Purity = 0, Quantity = 1 };
                Sessions[client] = session;
                Send(client, TradePackets.BuildOffered(BotName, BotInfo, BotItem, TradePackets.Descriptor(0, 1), 1));
                Send(client, TradePackets.BuildAcceptanceRefresh());
                Output.WriteDebugLog("[TRADE] Session open: " + NameOf(client) + " with " + BotName);
            }
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
                Send(client, TradePackets.BuildAcceptanceRefresh());
                Send(target, TradePackets.BuildAcceptanceRefresh());
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
                        if (partner == null && !session.TargetOffer.Accepted)
                        {
                            session.TargetOffer.Accepted = true;
                            Send(client, TradePackets.BuildStatus(TradePackets.StatusTheyAccepted));
                        }
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

        private static void NotifyAcceptanceReset(TradeSession session)
        {
            bool requesterAccepted = session.RequesterOffer.Accepted;
            bool targetAccepted = session.TargetOffer.Accepted;
            session.ClearAccepts();
            // OfferChanged clears the client's checked bit. This native status gives the player
            // explicit feedback even if their client continues drawing the selected image.
            if (requesterAccepted)
                Send(session.Requester, TradePackets.BuildStatus(TradePackets.StatusYouReset));
            if (targetAccepted)
                Send(session.Target, TradePackets.BuildStatus(TradePackets.StatusYouReset));
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
                SendChange(client, partner, true, change.Info, null);
                NotifyAcceptanceReset(session);
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
                if (refusal == TradePackets.RequestNotTradeable)
                    Send(client, ServerPackets.BuildSystemChatMessage("This item cannot be traded.", "BROADCAST"));
                return;
            }
            offer.Items[item.Slot] = item;
            SendChange(client, partner, true, 0, item);
            NotifyAcceptanceReset(session);
        }

        private static void RemoveFromOffer(TradeSession session, WorldClient client, TradeOffer offer, WorldClient partner, TradeRequests.Change change)
        {
            if (change.InfoTrade)
            {
                UInt32 amount = (UInt32)Math.Min(offer.Info, change.Info);
                offer.Info -= amount;
                SendChange(client, partner, false, amount, null);
                NotifyAcceptanceReset(session);
                return;
            }

            if (offer.Items.TryGetValue(change.Slot, out TradeItem item))
            {
                offer.Items.Remove(change.Slot);
                SendChange(client, partner, false, 0, item);
                NotifyAcceptanceReset(session);
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
            NotifyAcceptanceReset(session);
        }

        private static void End(TradeSession session)
        {
            Sessions.Remove(session.Requester);
            if (session.Target != null)
            {
                Sessions.Remove(session.Target);
            }
        }

        /// <summary>Both accepted: commit the exchange before publishing assets or completion.</summary>
        private static void Complete(TradeSession session)
        {
            WorldClient requester = session.Requester, target = session.Target;
            End(session);
            TradeSettlement settlement;
            try
            {
                settlement = Store.dbManager.WorldDbHandler.SettleTrade(
                    requester.playerData.getCharID(), session.RequesterOffer,
                    target?.playerData.getCharID() ?? 0, session.TargetOffer);
            }
            catch (Exception error) when (error is DbException || error is InvalidOperationException)
            {
                Output.WriteDebugLog($"[TRADE] Settlement rolled back: {error.Message}");
                settlement = TradeSettlement.Denied("The exchange could not be saved. Your items and information were kept.");
            }

            if (settlement.Failure != null)
            {
                Output.WriteDebugLog($"[TRADE] Aborted: {settlement.Failure}");
                foreach (WorldClient side in new[] { requester, target }.Where(client => client != null))
                {
                    new ServerPackets().sendSystemChatMessage(side, "Trade cancelled: " + settlement.Failure, "BROADCAST");
                    Send(side, TradePackets.BuildStatus(TradePackets.StatusAborted));
                }
                return;
            }

            uint requested = (uint)session.RequesterOffer.Items.Values.Sum(item => item.Quantity);
            uint offered = (uint)session.TargetOffer.Items.Values.Sum(item => item.Quantity);
            PublishSettlement(requester, settlement.Requester, requested, offered);
            PublishSettlement(target, settlement.Target, offered, requested);
            Output.WriteDebugLog($"[TRADE] Complete: {NameOf(requester)} gave {requested} items and {session.RequesterOffer.Info} info, " +
                                 $"{NameOf(target)} gave {offered} items and {session.TargetOffer.Info} info");
        }

        private static void PublishSettlement(WorldClient client, TradeSettlement.Receipt receipt, uint given, uint received)
        {
            if (client == null) return;
            foreach (byte[] record in receipt.BagRecords) client.messageQueue.addRpcMessage(record);
            if (receipt.InfoChange != 0)
            {
                client.playerData.setInfo(receipt.Balance);
                client.messageQueue.addRpcMessage(ServerPackets.BuildInfoCurrent(receipt.Balance, MarketplacePackets.InfoReasonMarketplace));
            }
            Send(client, TradePackets.BuildStatus(TradePackets.StatusComplete, given, received, receipt.InfoChange));
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
                if (partner != null)
                {
                    new ServerPackets().sendSystemChatMessage(partner, "Trade cancelled: your partner is gone.", "SYSTEM");
                    Send(partner, TradePackets.BuildStatus(TradePackets.StatusAborted));
                }
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
