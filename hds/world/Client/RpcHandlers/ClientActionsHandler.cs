using System;
using System.Collections.Generic;
using System.Linq;
using hds.shared;

namespace hds
{
    public static class ClientActionsHandler
    {
        private static readonly object SupportSync = new object();
        public static void Notice(WorldClient client, string text) => new ServerPackets().sendSystemChatMessage(client, text, "SYSTEM");

        public static void Afk(WorldClient client, byte[] body)
        {
            if (body?.Length != 1 || body[0] > 1) return;
            client.playerInstance.AFK.setValue(body[0]);
            client.messageQueue.addObjectMessage(PlayerViewAttributes.BuildSelf((47, new[] { body[0] })), false);
            PvpFlag.ToWatchers(client, PlayerViewAttributes.Build((35, new[] { body[0] })));
            Notice(client, body[0] == 0 ? "You are no longer away." : "You are now away.");
            client.FlushQueue();
        }
        public static void Echo(WorldClient client, byte[] body)
        {
            byte[] echo = ClientActionRequests.Echo(body); if (echo != null) SocialHandler.Send(client, echo);
        }
        public static void Pill(WorldClient client, byte[] body)
        {
            if (body?.Length != 2) return;
            if (client.playerData.isDead || client.playerData.duelDeath || ControlActions.Incapacitated(TimedEffects.ForPlayer(client).Effects)) return;
            ushort slot = BitConverter.ToUInt16(body, 0);
            if (slot < 1 || slot > 96) return;
            uint goid = Store.dbManager.WorldDbHandler.GetItemGOIDAtInventorySlot(slot);
            if (ConsumableItems.TryGet(goid, out var pill)) new InventoryHandler().UseItem(slot, pill.AbilityId);
            else Notice(client, "That slot does not contain a usable pill.");
        }
        public static void CloseCollector(WorldClient client, byte[] body)
        {
            if (body?.Length == 5 && body.All(b => b == 0)) client.playerData.activeCollector = null;
        }
        public static void CloseElevator(WorldClient client, byte[] body)
        {
            if (body?.Length == 2) client.playerData.elevatorPanel = null;
        }
        public static void CloseVendor(WorldClient client, byte[] body)
        {
            if (body?.Length == 5 && body[4] == 1) client.playerData.activeVendor = null;
        }
        public static void CloseContainer(WorldClient client, byte[] body)
        {
            if (body?.Length != 0) return;
            TutorialMission.Of(client)?.CloseContainer();
            lock (client.playerData.missionSync)
            {
                lock (client.playerData.loot) client.playerData.loot.Close();
                if (client.playerData.missionProgress != null)
                    foreach (var container in client.playerData.missionProgress.objects.Values)
                        container.lootOpen = container.putOpen = false;
            }
        }
        public static bool Dev(WorldClient client, string[] words)
        {
            if (words.Length == 0 || words[0] != "?clientactions") return false;
            if (Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS") != "1")
            { Notice(client, "?clientactions is a dev command (MXO_DEV_COMMANDS=1)."); return true; }
            string verb = words.Length > 1 ? words[1] : "help";
            string text = string.Join(" ", words.Skip(2));
            switch (verb)
            {
                case "afk": Afk(client, new byte[] { text == "1" ? (byte)1 : (byte)0 }); break;
                case "petition":
                    byte[] request = ClientActionRequests.String(ClientActionRequests.Packet(0xb0, 10), 6, text);
                    Support(client, 0x1b0, request.Skip(2).ToArray()); break;
                case "list": Support(client, 0x1b2, Array.Empty<byte>()); break;
                case "read":
                    if (uint.TryParse(text, out uint id)) Support(client, 0x1b4, BitConverter.GetBytes(id)); break;
                case "cancel":
                    if (uint.TryParse(text, out uint cancel))
                    {
                        var b = ClientActionRequests.Packet(0xb0, 10); ClientActionRequests.U32(b, 2, cancel); b[9] = 1;
                        Support(client, 0x1b0, ClientActionRequests.String(b, 6, "").Skip(2).ToArray());
                    }
                    break;
                case "kb": Support(client, 0x1b9, ClientActionRequests.String(ClientActionRequests.Packet(0xb9, 4), 2, text).Skip(2).ToArray()); break;
                case "pill": if (ushort.TryParse(text, out ushort slot)) Pill(client, BitConverter.GetBytes(slot)); break;
                case "vendor":
                    var vendorId = BitConverter.GetBytes(98u); // existing Mara tools-vendor fixture, same stock/session path
                    new ObjectInteractionHandler().processVendorOpen(ref vendorId); break;
                case "snoop": Snoop(client, 0x157, BitConverter.GetBytes(PvpCombat.HandleOf(client,client)).Concat(new byte[]{1,1,1}).ToArray()); break;
                case "snoopcrew": case "snoopfaction":
                    byte op = verb == "snoopcrew" ? (byte)0x59 : (byte)0x5b;
                    var group = ClientActionRequests.Packet(op, 5);
                    Snoop(client, op == 0x59 ? 0x159 : 0x15b, ClientActionRequests.String(group, 3, text).Skip(2).ToArray()); break;
                default: Notice(client, "?clientactions afk 0|1; petition <text>; list; read <ticket>; cancel <ticket>; kb <search>; pill <slot>; vendor; snoop; snoopcrew <name>; snoopfaction <name>. Snoop still requires server-granted GM privileges."); break;
            }
            client.FlushQueue(); return true;
        }
        public static void Snoop(WorldClient client, int opcode, byte[] body)
        {
            // A login identity, never a GM flag/name/manager supplied by the request.
            if (!ClientActionRequests.TrustedGm(client.playerData.getCharID(), Environment.GetEnvironmentVariable("MXO_GM_CHARACTER_IDS")))
            { Notice(client, "Snoop requires server-granted GM privileges."); return; }
            var db = Store.dbManager.WorldDbHandler;
            if (opcode == 0x157)
            {
                if (body?.Length != 7 || body.Skip(4).Any(b => b > 1)) return;
                uint handle = BitConverter.ToUInt32(body, 0);
                WorldClient target = SocialHandler.OnlineWhere(c => PvpCombat.HandleOf(client, c) == handle && handle != 0).FirstOrDefault();
                if (target == null) { Notice(client, "No current player view for that handle."); return; }
                uint id = target.playerData.getCharID();
                Notice(client, "GM loadout " + SocialHandler.HandleOf(target) + " (" + id + "):");
                if (body[4] != 0) Notice(client, "Loaded abilities: " + string.Join(",", Store.dbManager.MarginDbHandler.LoadAbilities((int)id)
                    .Where(a => a.getLoaded()).Select(a => AbilityCatalog.IdOf(unchecked((uint)a.getAbilityID())))));
                if (body[5] != 0) foreach (var item in db.GetInventoryItems(id)) Notice(client, "Slot " + item.Slot + ": " + item.Goid + " x" + item.Count);
                if (body[6] != 0) lock (target.playerData.activeBuffs) Notice(client, "Active buffs: " + string.Join(",", target.playerData.activeBuffs.Keys));
            }
            else
            {
                // 621a5130 / 621b7d10: manager u8, name offset u16; five bytes including opcode.
                if (body == null || body.Length < 3 || !ClientActionRequests.ReadString(body, 1, 3, 64, out string name)) return;
                bool faction = opcode == 0x15b;
                uint id = db.FindSupportGroup(name, faction);
                if (id == 0) { Notice(client, "No such " + (faction ? "faction." : "crew.")); return; }
                if (faction)
                {
                    var group = db.FetchFaction(id); Notice(client, "GM faction " + group.name + ": leader " + group.masterPlayerHandle + ", bank " + group.money);
                    foreach (var crew in group.crews) Notice(client, "Crew " + crew.crewName + ", rank " + crew.factionRank);
                }
                else
                {
                    var group = db.GetCrewData(id); Notice(client, "GM crew " + group.crewName + ": captain " + group.characterMasterName + ", bank " + group.money);
                    foreach (var member in db.GetCrewMembersForCrewId(id)) Notice(client, "Member " + member.handle);
                }
            }
            client.FlushQueue();
        }

        public static void StoredCompile(WorldClient client, byte[] body)
        {
            lock (client.playerData.craftingSync)
            {
                var r = ClientActionRequests.ParseStoredCompile(body);
                byte result = 1;
                var db = Store.dbManager.WorldDbHandler;
                if (r != null && !client.playerData.isDead && !client.playerData.duelDeath && !client.playerData.worldChangePending &&
                    (r.FromCrafting ? !client.playerData.DistrictIsLA() && ControlActions.CanUseAbilities(client) &&
                        !CombatManager.GetHandler(client).isCombatRunning &&
                        CombatSelection.IsLoaded(969, Store.dbManager.MarginDbHandler.LoadAbilities((int)client.playerData.getCharID())) &&
                        db.GetInventoryItems(client.playerData.getCharID()).Any(i => i.Slot == r.Tool && i.Goid == 1271)
                        : client.playerData.DistrictIsLA()))
                {
                    var entries = db.GetCodeStorage(client.playerData.getCharID());
                    var entry = entries.FirstOrDefault(e => e.Slot == r.Slot && e.Goid == r.Item && e.Count > 0);
                    var item = CraftingRules.Find(r.Item);
                    if (entry.Goid != 0 && CodeStorage.IsUploadable(r.Item) && item != null)
                    {
                        long cost = CraftingRules.InfoCost(item, client.playerData.DistrictIsLA() ? 0 : CraftingHandler.Skill(client, 530));
                        var transfer = db.MaterializeStoredCode(client.playerData.getCharID(), r.Slot, r.Item, cost);
                        result = transfer.Result;
                        if (result == 0)
                        {
                            client.messageQueue.addRpcMessage(transfer.Remaining == 0 ? CodeStorage.BuildRemove(r.Slot) :
                                CodeStorage.BuildUpdate(r.Slot, r.Item, transfer.Remaining));
                            if (transfer.BagCount > 1) client.messageQueue.addRpcMessage(ItemStacks.BuildStackCount(transfer.Slot, transfer.BagCount));
                            else new ServerPackets().sendInventoryItemAdd(transfer.Slot, r.Item, 0, 0, client);
                            client.playerData.setInfo(transfer.Cash);
                            new ServerPackets().SendInfoCurrent(client, (uint)transfer.Cash);
                        }
                    }
                }
                client.messageQueue.addRpcMessage(new byte[] { 0x61, result });
                if (result == 1) Notice(client, "This compile requires an owned uploaded Code Bit or fragment and a valid Loading Area or Compile session. Ability and equipment-code compilation is not reconstructed.");
                client.FlushQueue();
            }
        }

        public static void Support(WorldClient client, int opcode, byte[] body)
        {
            uint character = client.playerData.getCharID();
            var db = Store.dbManager.WorldDbHandler;
            switch (opcode)
            {
                case 0x1b0:
                    var request = ClientActionRequests.ParsePetition(body);
                    uint ticket = 0;
                    if (request != null && (request.Cancel || !string.IsNullOrWhiteSpace(request.Text)))
                        lock (SupportSync) ticket = db.SaveSupportTicket(character, SocialHandler.HandleOf(client), request.Ticket, request.Text, request.Cancel);
                    client.messageQueue.addRpcMessage(LocalSupport.Acknowledge(ticket == 0 ? 1u : 0u, ticket == 0 ? request?.Ticket ?? 0 : ticket, request?.Cancel == true));
                    Notice(client, ticket == 0 ? "Petition not saved. Use an owned open ticket and at most 3500 UTF-8 bytes; chunked submissions are not supported. Maximum ten open tickets." :
                        "Local petition #" + ticket + (request.Cancel ? " cancelled." : " saved for this server's administrators. No external support service is contacted."));
                    break;
                case 0x1b2:
                    if (body?.Length != 0) return;
                    var tickets = db.SupportTickets(character);
                    client.messageQueue.addRpcMessage(LocalSupport.Header(null, tickets.Count > 0));
                    for (int i = 0; i < tickets.Count; i++) client.messageQueue.addRpcMessage(LocalSupport.Header(tickets[i], i + 1 < tickets.Count));
                    break;
                case 0x1b4:
                    if (body?.Length != 4) return;
                    uint id = BitConverter.ToUInt32(body, 0);
                    client.messageQueue.addRpcMessage(LocalSupport.Detail(db.SupportTickets(character).FirstOrDefault(t => t.Id == id), id));
                    break;
                case 0x1b9:
                    if (!ClientActionRequests.ReadString(body, 0, 2, 200, out string query)) return;
                    var articles = LocalSupport.Search(query).ToList();
                    if (articles.Count == 0) client.messageQueue.addRpcMessage(LocalSupport.SearchResult(null, false));
                    for (int i = 0; i < articles.Count; i++) client.messageQueue.addRpcMessage(LocalSupport.SearchResult(articles[i], i + 1 < articles.Count));
                    break;
                case 0x1b7:
                    if (body?.Length != 0) return;
                    client.messageQueue.addRpcMessage(LocalSupport.Categories());
                    break;
                case 0x1bb:
                    if (!ClientActionRequests.ReadString(body, 0, 2, 100, out string articleId)) return;
                    client.messageQueue.addRpcMessage(LocalSupport.ArticleResult(LocalSupport.Articles.FirstOrDefault(a => a.Id == articleId)));
                    break;
                case 0x1bd:
                    if (body?.Length != 0) return;
                    client.messageQueue.addRpcMessage(ClientActionRequests.String(ClientActionRequests.Packet(0xbe, 9), 6, LocalSupport.Articles[0].Text));
                    break;
                default: return;
            }
            client.FlushQueue();
        }
    }
}
