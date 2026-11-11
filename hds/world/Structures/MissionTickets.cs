using System;
using System.Collections.Generic;
using System.Linq;
using hds.shared;

namespace hds
{
    /// <summary>Collector mission tickets use native RPC 59 and the gates in their shipped GoldenTicket properties.</summary>
    internal static class MissionTickets
    {
        public static List<Mission> MerovingianOffers() => DataLoader.getInstance().missions
            .Where(mission => mission.org == MissionContacts.Merovingian && mission.entryItem != 0)
            .OrderBy(mission => mission.order).ToList();

        public static void Use(WorldClient client, byte[] payload)
        {
            if (payload.Length != 1 || payload[0] < 1 || payload[0] > 96)
            {
                return;
            }
            UInt32 item = Store.dbManager.WorldDbHandler.GetItemGOIDAtInventorySlot(payload[0]);
            Mission mission = DataLoader.getInstance().missions.Find(candidate => candidate.entryItem == item && item != 0);
            if (mission == null)
            {
                return;
            }
            MissionHandler.Accept(client, mission, 0);
        }

        public static bool CanStart(WorldClient client, Mission mission)
        {
            if (mission.entryItem == 0)
            {
                return true;
            }
            string refusal = null;
            if (client.playerData.activeMission != null || TutorialMission.Of(client) != null)
            {
                refusal = "Finish or abort your current mission before using this ticket.";
            }
            else if (client.playerData.isDead || client.playerInstance.Level.getValue()[0] < mission.minLevel)
            {
                refusal = "This mission ticket requires level " + mission.minLevel + ".";
            }
            else if (MissionContacts.Reputation(client, mission.org) < mission.minReputation)
            {
                refusal = "This mission ticket requires at least " + mission.minReputation + " reputation.";
            }
            else if (client.playerData.getDistrictId() != mission.district)
            {
                refusal = "Use this mission ticket in " + (mission.district == 1 ? "the Richland/Westview world." : "Downtown.");
            }
            else if (!Store.dbManager.WorldDbHandler.GetInventoryItems(client.playerData.getCharID())
                         .Any(item => item.Goid == mission.entryItem && item.Slot >= 1 && item.Slot <= 96))
            {
                refusal = "The mission ticket must be in your backpack.";
            }
            if (refusal == null)
            {
                return true;
            }
            new ServerPackets().sendSystemChatMessage(client, refusal, "BROADCAST");
            client.FlushQueue();
            return false;
        }

        public static bool Complete(WorldClient client, MissionProgress progress)
        {
            Mission mission = progress.mission;
            if (mission.entryItem == 0 || progress.entryItemExchanged)
            {
                return true;
            }
            CollectorExchange exchange = Store.dbManager.WorldDbHandler.ExchangeCollectorItems(
                client.playerData.getCharID(), mission.entryItem, 1, mission.completionItem);
            if (exchange == null)
            {
                if (!progress.completionNotice)
                {
                    new ServerPackets().sendSystemChatMessage(client,
                        "Keep the mission ticket in your backpack and make room for the completion reward.", "BROADCAST");
                    client.FlushQueue();
                    progress.completionNotice = true;
                }
                return false;
            }
            progress.entryItemExchanged = true;
            CollectorHandler.NotifyExchange(client, exchange, mission.completionItem);
            return true;
        }
    }
}
