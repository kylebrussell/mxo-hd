using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using hds.databases;
using hds.shared;

namespace hds
{
    /// <summary>What the mail service needs from the world: players, their bags and their information.</summary>
    public interface IMailHost
    {
        /// <summary>0 when no character has that handle.</summary>
        UInt32 CharIdByHandle(string handle);

        string HandleOf(UInt32 charId);

        /// <summary>Sends a record to the character if it is online.</summary>
        void Send(UInt32 charId, byte[] record);

        bool IsDead(UInt32 charId);

        long InfoOf(UInt32 charId);

        void AddInfo(UInt32 charId, long change);

        /// <summary>The item in a bag slot, or null.</summary>
        MailItem ItemAt(UInt32 charId, int slot);

        /// <summary>Takes the whole stack out of the slot.</summary>
        void RemoveItem(UInt32 charId, int slot);

        int FreeBagSlots(UInt32 charId);

        void GiveItem(UInt32 charId, MailItem item);
    }

    /// <summary>
    /// In-game email: the inbox, reading, sending with attachments, deleting and folders. The records are retail's and
    /// client.dll's (MailPackets); what the server checks, and that attached information moves when the mail is sent,
    /// are ours. See docs/CLIENT-ACTIONS.md, "Email".
    /// </summary>
    public class MailService
    {
        /// <summary>Address handle of the dev helper that answers every mail (MXO_DEV_COMMANDS=1).</summary>
        public const string BotHandle = "MailBot";
        public const string SystemHandle = "System";
        public const int MaxAttachments = 8;

        private readonly IMailStore store;
        private readonly IMailHost host;
        private readonly Func<string> serverName;
        private readonly Func<UInt32> clock;
        private readonly object sync = new object();
        private readonly Dictionary<UInt32, Dictionary<int, PendingChunk>> pending = new Dictionary<UInt32, Dictionary<int, PendingChunk>>();
        private readonly Dictionary<UInt32, int> lastChunkIndex = new Dictionary<UInt32, int>();

        /// <summary>Lets mail to <see cref="BotHandle"/> come back as a reply, so one client can test receiving.</summary>
        public bool BotEnabled;

        private class PendingChunk
        {
            public UInt32 TotalLength;
            public List<byte> Bytes = new List<byte>();
            public bool Complete => Bytes.Count >= TotalLength;
        }

        public MailService(IMailStore store, IMailHost host, Func<string> serverName, Func<UInt32> clock = null)
        {
            this.store = store;
            this.host = host;
            this.serverName = serverName;
            this.clock = clock ?? (() => (UInt32)DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        }

        public string AddressOf(UInt32 charId) => MailPackets.Address(serverName(), host.HandleOf(charId));
        private string SystemAddress => "soe+mxo+" + serverName() + "+" + SystemHandle;

        /// <summary>Client 81 ab: a long chunk of the next request follows in 81 ac records.</summary>
        public void OnChunkHeader(UInt32 charId, MailRequests.ChunkHeader header)
        {
            lock (sync)
            {
                if (!pending.TryGetValue(charId, out var chunks))
                {
                    pending[charId] = chunks = new Dictionary<int, PendingChunk>();
                }
                chunks[header.Index] = new PendingChunk { TotalLength = header.TotalLength };
                lastChunkIndex[charId] = header.Index;
            }
        }

        /// <summary>Client 81 ac: part of the chunk the last 81 ab announced.</summary>
        public void OnChunk(UInt32 charId, byte[] bytes)
        {
            lock (sync)
            {
                if (pending.TryGetValue(charId, out var chunks) && lastChunkIndex.TryGetValue(charId, out int index) &&
                    chunks.TryGetValue(index, out PendingChunk chunk))
                {
                    chunk.Bytes.AddRange(bytes);
                }
            }
        }

        private Dictionary<int, string> TakeChunks(UInt32 charId)
        {
            lock (sync)
            {
                Dictionary<int, string> done = new Dictionary<int, string>();
                if (pending.TryGetValue(charId, out var chunks))
                {
                    foreach (var entry in chunks)
                    {
                        byte[] bytes = entry.Value.Bytes.ToArray();
                        int length = Array.IndexOf(bytes, (byte)0);
                        done[entry.Key] = Encoding.UTF8.GetString(bytes, 0, length >= 0 ? length : bytes.Length);
                    }
                    pending.Remove(charId);
                    lastChunkIndex.Remove(charId);
                }
                return done;
            }
        }

        /// <summary>Client 81 ea: the inbox. Answer 81 eb, 81 ef, then one 81 ed per message (retail's order).</summary>
        public void OnHeaderRequest(UInt32 charId, MailRequests.HeaderRequest request)
        {
            List<MailMessage> all = store.List(charId).Where(m => m.Status != MailPackets.StatusPurged).ToList();
            IEnumerable<MailMessage> wanted = all;
            if (!string.IsNullOrEmpty(request.Category))
            {
                wanted = wanted.Where(m => string.Equals(m.Category, request.Category, StringComparison.OrdinalIgnoreCase));
            }
            if (request.StartTime != 0)
            {
                wanted = request.Descending
                    ? wanted.Where(m => m.SentTime < request.StartTime)
                    : wanted.Where(m => m.SentTime > request.StartTime);
            }
            wanted = request.Descending
                ? wanted.OrderByDescending(m => m.SentTime).ThenByDescending(m => m.Id)
                : wanted.OrderBy(m => m.SentTime).ThenBy(m => m.Id);
            List<MailMessage> page = (request.MaxHeaders > 0 ? wanted.Take((int)request.MaxHeaders) : wanted).ToList();

            host.Send(charId, MailPackets.BuildInboxLimit());
            host.Send(charId, MailPackets.BuildListEnd(AddressOf(charId), (UInt32)all.Count));
            for (int i = 0; i < page.Count; i++)
            {
                host.Send(charId, MailPackets.BuildHeader(page[i], i < page.Count - 1, false));
            }
        }

        /// <summary>Client 81 f0: open a message. A new or unread one becomes read.</summary>
        public void OnRead(UInt32 charId, UInt32 id)
        {
            MailMessage message = store.Get(charId, id);
            if (message == null || message.Status == MailPackets.StatusPurged)
            {
                host.Send(charId, MailPackets.BuildReadFailed(MailPackets.MessageNotFound));
                return;
            }
            if (message.Status == MailPackets.StatusNew || message.Status == MailPackets.StatusUnread)
            {
                message.Status = MailPackets.StatusRead;
                store.Update(message);
            }
            host.Send(charId, MailPackets.BuildMessage(message));
        }

        /// <summary>Client 81 f4: Delete (4) / Undelete (3) one message; 5 removes it.</summary>
        public void OnSetStatus(UInt32 charId, UInt32 id, UInt32 status)
        {
            MailMessage message = store.Get(charId, id);
            if (message == null || status < MailPackets.StatusNew || status > MailPackets.StatusPurged)
            {
                host.Send(charId, MailPackets.BuildStatus(0xf5, message == null ? MailPackets.MessageNotFound : MailPackets.BadData));
                return;
            }
            SetStatus(message, status);
            host.Send(charId, MailPackets.BuildStatus(0xf5, MailPackets.Success));
        }

        /// <summary>Client 81 f6, sent as the inbox closes: 1 -> 2 (seen) and 4 -> 5 (empty the deleted ones).</summary>
        public void OnChangeAll(UInt32 charId, UInt32 oldStatus, UInt32 newStatus)
        {
            if (newStatus >= MailPackets.StatusNew && newStatus <= MailPackets.StatusPurged)
            {
                foreach (MailMessage message in store.List(charId).Where(m => m.Status == oldStatus))
                {
                    SetStatus(message, newStatus);
                }
            }
            host.Send(charId, MailPackets.BuildStatus(0xf7, MailPackets.Success));
        }

        private void SetStatus(MailMessage message, UInt32 status)
        {
            if (status == MailPackets.StatusPurged)
            {
                store.Delete(message.OwnerCharId, message.Id);
                return;
            }
            message.Status = status;
            store.Update(message);
        }

        /// <summary>Client 81 f8: move messages to a folder.</summary>
        public void OnClassify(UInt32 charId, MailRequests.Classify classify)
        {
            int moved = 0;
            foreach (UInt32 id in classify.Ids)
            {
                MailMessage message = store.Get(charId, id);
                if (message != null)
                {
                    message.Folder = classify.NewFolder ?? "";
                    store.Update(message);
                    moved++;
                }
            }
            host.Send(charId, MailPackets.BuildStatus(0xf9, moved > 0 || classify.Ids.Count == 0 ? MailPackets.Success : MailPackets.MessageNotFound));
        }

        /// <summary>Client 81 fa: delete the messages (of a category, or all) sent before a time.</summary>
        public void OnDeleteOlder(UInt32 charId, MailRequests.DeleteOlder request)
        {
            UInt32 deleted = 0;
            foreach (MailMessage message in store.List(charId))
            {
                bool category = string.IsNullOrEmpty(request.Category) ||
                                string.Equals(message.Category, request.Category, StringComparison.OrdinalIgnoreCase);
                if (category && message.SentTime < request.TimeOlderThan && store.Delete(charId, message.Id))
                {
                    deleted++;
                }
            }
            host.Send(charId, MailPackets.BuildDeleteResult(AddressOf(charId), deleted, MailPackets.Success));
        }

        /// <summary>Client 81 fe: the client drops a message itself (it sends this for mail from someone it ignores).</summary>
        public void OnDelete(UInt32 charId, UInt32 id)
        {
            store.Delete(charId, id);
        }

        /// <summary>Client 81 fc: take a message's attached items into the bag.</summary>
        public void OnTake(UInt32 charId, MailRequests.Take take)
        {
            MailMessage message = store.Get(charId, take.Id);
            if (message == null)
            {
                host.Send(charId, MailPackets.BuildStatus(0xfd, MailPackets.MessageNotFound));
                return;
            }
            List<MailItem> taking = new List<MailItem>();
            List<MailItem> left = new List<MailItem>(message.Items);
            // The client lists what its copy of the message's ItemList holds; an empty list takes everything
            var requested = take.Items.Count > 0 ? take.Items : message.Items.Select(i => (i.ClassData, i.InstanceData)).ToList();
            foreach (var wanted in requested)
            {
                MailItem match = left.FirstOrDefault(i => i.ClassData == wanted.ClassData && i.InstanceData == wanted.InstanceData);
                if (match != null)
                {
                    left.Remove(match);
                    taking.Add(match);
                }
            }
            if (taking.Count == 0)
            {
                host.Send(charId, MailPackets.BuildStatus(0xfd, MailPackets.NothingToDo));
                return;
            }
            if (host.FreeBagSlots(charId) < taking.Count)
            {
                host.Send(charId, MailPackets.BuildStatus(0xfd, MailPackets.PersonalInventoryFull));
                return;
            }
            message.Items = left;
            store.Update(message);
            foreach (MailItem item in taking)
            {
                host.GiveItem(charId, item);
            }
            host.Send(charId, MailPackets.BuildStatus(0xfd, MailPackets.Success));
        }

        /// <summary>
        /// Client 81 e5. Chunks: 0 the recipients, 1 the subject, 2 the body, 3 the out-of-band XML (sent ahead in 81 ab /
        /// 81 ac when marked chunked). Everything is checked before anything moves; a recipient that doesn't exist fails
        /// the whole send, as retail answered DESTAVATARDOESNTEXIST and then mailed "Message Undeliverable".
        /// </summary>
        public void OnSend(UInt32 charId, MailRequests.Send send)
        {
            Dictionary<int, string> chunked = TakeChunks(charId);
            string Chunk(int index) => send.IsChunked(index) || send.Chunk(index) == null
                ? chunked.TryGetValue(index, out string text) ? text : send.Chunk(index) ?? ""
                : send.Chunk(index);

            string to = Chunk(0), subject = Chunk(1), body = Chunk(2);
            MailOob.Outgoing oob = MailOob.ParseOutgoing(Chunk(3));

            List<string> handles = (oob.Recipients.Count > 0 ? oob.Recipients : to.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList())
                .Select(MailPackets.HandleOf).Where(h => h.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            UInt32 failure = 0;
            string reason = null;
            List<UInt32> recipients = new List<UInt32>();
            bool toBot = false;
            foreach (string handle in handles)
            {
                if (BotEnabled && string.Equals(handle, BotHandle, StringComparison.OrdinalIgnoreCase))
                {
                    toBot = true;
                    continue;
                }
                UInt32 recipient = host.CharIdByHandle(handle);
                if (recipient == 0)
                {
                    failure = MailPackets.RecipientUnknown;
                    reason = "There is no one called " + handle + ".";
                    break;
                }
                recipients.Add(recipient);
            }
            if (failure == 0 && recipients.Count == 0 && !toBot)
            {
                failure = MailPackets.RecipientUnknown;
                reason = "The message had no recipient.";
            }

            bool attachments = oob.Info > 0 || oob.Items.Count > 0;
            List<(int Slot, MailItem Item)> items = new List<(int, MailItem)>();
            if (failure == 0 && attachments)
            {
                if (recipients.Count + (toBot ? 1 : 0) != 1) failure = MailPackets.BadData;
                else if (host.IsDead(charId)) failure = MailPackets.BadData;
                else if (oob.Info > host.InfoOf(charId)) failure = MailPackets.BadData;
                else if (oob.Items.Count > MaxAttachments) failure = MailPackets.BadData;
                foreach (var (slot, personal) in oob.Items)
                {
                    if (failure != 0) break;
                    MailItem item = personal && slot >= 0 && slot <= MyWorldDbAccess.LastBagSlot ? host.ItemAt(charId, slot) : null;
                    if (item == null || items.Any(i => i.Slot == slot))
                    {
                        failure = MailPackets.BadData;
                        break;
                    }
                    items.Add((slot, item));
                }
                if (failure == MailPackets.BadData)
                {
                    reason = "The attachments could not be sent.";
                }
            }
            if (failure == 0)
            {
                foreach (UInt32 recipient in recipients)
                {
                    if (send.EnforceInboxLimit && store.Count(recipient) >= MailPackets.InboxLimit)
                    {
                        failure = MailPackets.InboxFull;
                        break;
                    }
                }
            }

            if (failure != 0)
            {
                host.Send(charId, MailPackets.BuildSendResult(0, failure));
                if (failure == MailPackets.RecipientUnknown)
                {
                    Deliver(SystemMessage(charId, "Message Undeliverable",
                        "Your message \"" + subject + "\" to " + to + " could not be delivered. " + (reason ?? "")));
                }
                Output.WriteDebugLog("[MAIL] Send by " + host.HandleOf(charId) + " refused: " + failure.ToString("x8") + " " + reason);
                return;
            }

            foreach (var entry in items)
            {
                host.RemoveItem(charId, entry.Slot);
            }
            if (oob.Info > 0)
            {
                host.AddInfo(charId, -oob.Info);
            }

            UInt32 now = clock();
            UInt32 firstId = 0;
            foreach (UInt32 recipient in recipients)
            {
                MailMessage message = new MailMessage
                {
                    OwnerCharId = recipient,
                    Sender = AddressOf(charId),
                    Category = MailPackets.CategoryPlayer,
                    SentTime = now,
                    Status = MailPackets.StatusNew,
                    Subject = subject,
                    Body = body,
                    Items = items.Select(i => i.Item).ToList()
                };
                Deliver(message);
                if (oob.Info > 0)
                {
                    host.AddInfo(recipient, oob.Info);
                }
                if (firstId == 0)
                {
                    firstId = message.Id;
                }
            }
            host.Send(charId, MailPackets.BuildSendResult(firstId, MailPackets.Success));
            Output.WriteDebugLog("[MAIL] " + host.HandleOf(charId) + " sent \"" + subject + "\" to " + string.Join(", ", handles) +
                                 " with " + items.Count + " items and " + oob.Info + " info");

            if (toBot)
            {
                // The bot answers with the text quoted and gives back whatever was attached
                MailMessage reply = new MailMessage
                {
                    OwnerCharId = charId,
                    Sender = MailPackets.Address(serverName(), BotHandle),
                    Category = MailPackets.CategoryPlayer,
                    SentTime = now,
                    Subject = "RE: " + subject,
                    Body = "MailBot got your message:\n\n" + body,
                    Items = items.Select(i => i.Item).ToList()
                };
                Deliver(reply);
                if (oob.Info > 0)
                {
                    host.AddInfo(charId, oob.Info);
                }
            }
        }

        public MailMessage SystemMessage(UInt32 charId, string subject, string body, List<MailItem> items = null)
        {
            return new MailMessage
            {
                OwnerCharId = charId,
                Sender = SystemAddress,
                Category = MailPackets.CategorySystem,
                SentTime = clock(),
                Subject = subject,
                Body = body,
                Items = items ?? new List<MailItem>()
            };
        }

        /// <summary>Stores a new message and tells its owner, if online: 81 ed with Notification set (retail's
        /// "Message Undeliverable" arrived that way).</summary>
        public void Deliver(MailMessage message)
        {
            message.Status = MailPackets.StatusNew;
            store.Add(message);
            host.Send(message.OwnerCharId, MailPackets.BuildHeader(message, false, true));
        }
    }

    /// <summary>The live world behind MailService.</summary>
    public class WorldMailHost : IMailHost
    {
        public UInt32 CharIdByHandle(string handle) => Store.dbManager.WorldDbHandler.GetCharIdByHandle(handle);

        public string HandleOf(UInt32 charId)
        {
            WorldClient client = SocialHandler.FindOnlineByCharId(charId);
            if (client != null)
            {
                return SocialHandler.HandleOf(client);
            }
            return MySqlMailStore.HandleByCharId(charId) ?? charId.ToString();
        }

        public void Send(UInt32 charId, byte[] record)
        {
            WorldClient client = SocialHandler.FindOnlineByCharId(charId);
            if (client != null)
            {
                client.messageQueue.addRpcMessage(record);
                client.FlushQueue();
            }
        }

        public bool IsDead(UInt32 charId) => SocialHandler.FindOnlineByCharId(charId)?.playerData.isDead ?? false;

        public long InfoOf(UInt32 charId) => SocialHandler.FindOnlineByCharId(charId)?.playerData.getInfo() ?? 0;

        public void AddInfo(UInt32 charId, long change)
        {
            WorldClient client = SocialHandler.FindOnlineByCharId(charId);
            if (client == null)
            {
                if (change > 0)
                {
                    MarketPlaceHandler.Db.AddCash(charId, (UInt32)change);
                }
                return;
            }
            long info = Math.Max(0, client.playerData.getInfo() + change);
            client.playerData.setInfo(info);
            Store.dbManager.WorldDbHandler.SaveInfo(client, info);
            client.messageQueue.addRpcMessage(ServerPackets.BuildInfoCurrent((UInt32)info, MarketplacePackets.InfoReasonMarketplace));
        }

        public MailItem ItemAt(UInt32 charId, int slot)
        {
            MarketPlaceHandler.Db.GetInventoryItem(charId, (UInt16)slot, out UInt32 goid, out UInt32 purity);
            if (goid == 0)
            {
                return null;
            }
            return new MailItem
            {
                Goid = goid, Purity = purity,
                Count = ItemStacks.CountFromDb(MarketPlaceHandler.Db.GetInventoryCount(charId, (UInt16)slot))
            };
        }

        public void RemoveItem(UInt32 charId, int slot)
        {
            MarketPlaceHandler.Db.RemoveInventoryItem(charId, (UInt16)slot);
            WorldClient client = SocialHandler.FindOnlineByCharId(charId);
            client?.messageQueue.addRpcMessage(ServerPackets.BuildInventoryItemDelete((UInt16)slot));
        }

        public int FreeBagSlots(UInt32 charId)
        {
            HashSet<int> occupied = new HashSet<int>(Store.dbManager.WorldDbHandler.GetInventoryItems(charId).Select(i => (int)i.Slot));
            return TradeRules.FreeBagSlots(occupied).Count;
        }

        public void GiveItem(UInt32 charId, MailItem item)
        {
            HashSet<int> occupied = new HashSet<int>(Store.dbManager.WorldDbHandler.GetInventoryItems(charId).Select(i => (int)i.Slot));
            List<UInt16> free = TradeRules.FreeBagSlots(occupied);
            if (free.Count == 0)
            {
                return;
            }
            UInt16 slot = free[0];
            UInt32 descriptor = item.InstanceData;
            MarketPlaceHandler.Db.AddInventoryItem(charId, slot, item.Goid, item.Purity);
            if (item.Count > 1)
            {
                Store.dbManager.WorldDbHandler.SetInventoryCount(slot, charId, item.Count);
            }
            WorldClient client = SocialHandler.FindOnlineByCharId(charId);
            client?.messageQueue.addRpcMessage(ServerPackets.BuildInventoryItemAdd(slot, item.Goid,
                (byte)((descriptor >> 16) & 0xff), (byte)(descriptor >> 24)));
        }
    }

    /// <summary>RpcManager's entry points for the mail records (client 81 ab, 81 ac, 81 e5, 81 ea, 81 f0, 81 f4,
    /// 81 f6, 81 f8, 81 fa, 81 fc, 81 fe).</summary>
    public static class MailHandler
    {
        private static MailService service;
        private static readonly object InitLock = new object();

        public static MailService Service
        {
            get
            {
                lock (InitLock)
                {
                    if (service == null)
                    {
                        service = new MailService(new MySqlMailStore(), new WorldMailHost(), () => ChatAddress.WorldName)
                        {
                            BotEnabled = Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS") == "1"
                        };
                    }
                    return service;
                }
            }
            set
            {
                lock (InitLock)
                {
                    service = value;
                }
            }
        }

        private static UInt32 CharId(WorldClient client) => client.playerData.getCharID();

        private static void Unreadable(string what, byte[] data)
        {
            Output.WriteDebugLog("[MAIL] Unreadable " + what + ": " + StringUtils.bytesToString(data));
        }

        public static void Handle(WorldClient client, int header, byte[] data)
        {
            UInt32 charId = CharId(client);
            try
            {
                switch (header)
                {
                    case (int)RPCRequestHeader.CLIENT_LONG_STRING_HEADER:
                        if (MailRequests.TryParseChunkHeader(data, out var chunkHeader)) Service.OnChunkHeader(charId, chunkHeader);
                        else Unreadable("81 ab", data);
                        break;
                    case (int)RPCRequestHeader.CLIENT_LONG_STRING_CHUNK:
                        if (MailRequests.TryParseChunk(data, out byte[] chunk)) Service.OnChunk(charId, chunk);
                        else Unreadable("81 ac", data);
                        break;
                    case (int)RPCRequestHeader.CLIENT_MAIL_SEND:
                        if (MailRequests.TryParseSend(data, out var send)) Service.OnSend(charId, send);
                        else Unreadable("81 e5", data);
                        break;
                    case (int)RPCRequestHeader.CLIENT_MAIL_GET_HEADERS:
                        if (MailRequests.TryParseHeaderRequest(data, out var request)) Service.OnHeaderRequest(charId, request);
                        else Unreadable("81 ea", data);
                        break;
                    case (int)RPCRequestHeader.CLIENT_MAIL_READ:
                        if (MailRequests.TryParseId(data, out UInt32 readId)) Service.OnRead(charId, readId);
                        break;
                    case (int)RPCRequestHeader.CLIENT_MAIL_SET_STATUS:
                        if (MailRequests.TryParsePair(data, out UInt32 id, out UInt32 status)) Service.OnSetStatus(charId, id, status);
                        break;
                    case (int)RPCRequestHeader.CLIENT_MAIL_CHANGE_ALL_STATUS:
                        if (MailRequests.TryParsePair(data, out UInt32 oldStatus, out UInt32 newStatus)) Service.OnChangeAll(charId, oldStatus, newStatus);
                        break;
                    case (int)RPCRequestHeader.CLIENT_MAIL_CLASSIFY:
                        if (MailRequests.TryParseClassify(data, out var classify)) Service.OnClassify(charId, classify);
                        else Unreadable("81 f8", data);
                        break;
                    case (int)RPCRequestHeader.CLIENT_MAIL_DELETE_OLDER:
                        if (MailRequests.TryParseDeleteOlder(data, out var older)) Service.OnDeleteOlder(charId, older);
                        else Unreadable("81 fa", data);
                        break;
                    case (int)RPCRequestHeader.CLIENT_MAIL_TAKE_ATTACHMENTS:
                        if (MailRequests.TryParseTake(data, out var take)) Service.OnTake(charId, take);
                        else Unreadable("81 fc", data);
                        break;
                    case (int)RPCRequestHeader.CLIENT_MAIL_DELETE:
                        if (MailRequests.TryParseId(data, out UInt32 deleteId)) Service.OnDelete(charId, deleteId);
                        break;
                }
            }
            catch (Exception e)
            {
                Output.WriteDebugLog("[MAIL] " + header.ToString("x") + " failed: " + e.Message);
            }
        }

        /// <summary>Dev command ?mailbot: a message from MailBot with a Code Bit 1 attached, to test reading,
        /// the new-mail notice and taking attachments with one client.</summary>
        public static void SendBotMail(WorldClient client, string text)
        {
            UInt32 charId = CharId(client);
            MailMessage message = new MailMessage
            {
                OwnerCharId = charId,
                Sender = ChatAddress.Of(MailService.BotHandle),
                Category = MailPackets.CategoryPlayer,
                SentTime = (UInt32)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Subject = string.IsNullOrWhiteSpace(text) ? "Hello from MailBot" : text,
                Body = "This message was sent by ?mailbot. It carries a Code Bit 1: take it from the attachments.\n" +
                       "Mail sent to MailBot comes back as a reply.",
                Items = new List<MailItem> { new MailItem { Goid = NpcLoot.CodeBit1, Purity = 0, Count = 1 } }
            };
            Service.Deliver(message);
        }
    }
}
