using System;
using System.Collections.Generic;
using System.Data.Common;
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

        /// <summary>Queues records together before flushing to the online character.</summary>
        void Send(UInt32 charId, params byte[][] records);

        bool IsDead(UInt32 charId);

        void PublishCash(UInt32 charId, UInt32 cash);
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
        private readonly Func<IMailTransfer> beginTransfer;
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

        public MailService(IMailStore store, IMailHost host, Func<IMailTransfer> beginTransfer, Func<string> serverName, Func<UInt32> clock = null)
        {
            this.store = store;
            this.host = host;
            this.beginTransfer = beginTransfer;
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
                        if (!entry.Value.Complete) continue;
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
        public void OnSetStatus(uint charId, uint id, uint status)
        {
            if (!ValidStatus(status))
            {
                host.Send(charId, MailPackets.BuildStatus(0xf5, MailPackets.BadData));
                return;
            }

            bool changed;
            try
            {
                changed = store.SetStatus(charId, id, status);
            }
            catch (DbException error)
            {
                Output.WriteDebugLog($"[MAIL] Status change rolled back char={charId} id={id} status={status}: {error.Message}");
                MailMessage saved = null;
                try
                {
                    saved = store.Get(charId, id);
                }
                catch (DbException refreshError)
                {
                    Output.WriteDebugLog($"[MAIL] Status refresh failed char={charId} id={id}: {refreshError.Message}");
                }

                // With the existing id cached, 81 ed redraws one row in place. A failure
                // 81 f5 would clear the cache and request a full list, which inflates the count.
                host.Send(charId, saved != null
                    ? MailPackets.BuildHeader(saved, false, false)
                    : MailPackets.BuildStatus(0xf5, MailPackets.BadData));
                Notice(charId, "The mail status could not be changed. The message was kept.");
                return;
            }

            host.Send(charId, MailPackets.BuildStatus(0xf5, changed ? MailPackets.Success : MailPackets.MessageNotFound));
        }

        /// <summary>Client 81 f6, sent as the inbox closes: 1 -> 2 (seen) and 4 -> 5 (empty the deleted ones).</summary>
        public void OnChangeAll(uint charId, uint oldStatus, uint newStatus)
        {
            if (!ValidStatus(oldStatus) || !ValidStatus(newStatus))
            {
                host.Send(charId, MailPackets.BuildStatus(0xf7, MailPackets.BadData));
                return;
            }

            try
            {
                store.ChangeAllStatus(charId, oldStatus, newStatus);
            }
            catch (DbException error)
            {
                Output.WriteDebugLog($"[MAIL] Inbox status change rolled back char={charId} {oldStatus}->{newStatus}: {error.Message}");
                host.Send(charId, MailPackets.BuildStatus(0xf7, MailPackets.BadData));
                Notice(charId, "The inbox status could not be changed. Your messages were kept.");
                return;
            }

            host.Send(charId, MailPackets.BuildStatus(0xf7, MailPackets.Success));
        }

        private static bool ValidStatus(uint status) => status >= MailPackets.StatusNew && status <= MailPackets.StatusPurged;

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

        private sealed class Refusal : Exception
        {
            public readonly uint Status;
            public readonly string Notice;
            public Refusal(uint status, string notice = null)
            {
                Status = status;
                Notice = notice;
            }
        }

        /// <summary>Client 81 fc: move attachments from one owned message into the bag.</summary>
        public void OnTake(uint character, MailRequests.Take take)
        {
            var delivered = new List<(ushort Slot, MailItem Item)>();
            MailMessage message = null;
            try
            {
                using var transfer = beginTransfer();
                if (transfer.ReadCash(character) < 0) throw new Refusal(MailPackets.MessageNotFound);
                message = transfer.ReadMessage(character, take.Id);
                if (message == null) throw new Refusal(MailPackets.MessageNotFound);

                var left = new List<MailItem>(message.Items);
                var taking = new List<MailItem>();
                var requested = take.Items.Count > 0 ? take.Items : message.Items.Select(i => (i.ClassData, i.InstanceData)).ToList();
                foreach (var wanted in requested)
                {
                    MailItem item = left.FirstOrDefault(i => i.ClassData == wanted.ClassData && i.InstanceData == wanted.InstanceData);
                    if (item == null) continue;
                    left.Remove(item);
                    taking.Add(item);
                }
                if (taking.Count == 0) throw new Refusal(MailPackets.NothingToDo);

                var occupied = transfer.ReadBag(character).Select(row => (int)row.Slot).ToHashSet();
                List<ushort> free = TradeRules.FreeBagSlots(occupied);
                if (free.Count < taking.Count) throw new Refusal(MailPackets.PersonalInventoryFull);
                var updated = message.Copy();
                updated.Items = left;
                transfer.SetAttachments(updated);
                for (int i = 0; i < taking.Count; i++)
                {
                    transfer.AddItem(character, free[i], taking[i]);
                    delivered.Add((free[i], taking[i]));
                }
                transfer.Commit();
                message = updated;
            }
            catch (Refusal error)
            {
                host.Send(character, MailPackets.BuildStatus(0xfd, error.Status));
                RefreshClaimedMessage(character, message);
                if (error.Status == MailPackets.PersonalInventoryFull)
                    Notice(character, "Your inventory is full. The attachments are still in the message.");
                return;
            }
            catch (Exception error) when (error is DbException || error is InvalidOperationException || error is OverflowException)
            {
                Output.WriteDebugLog($"[MAIL] Claim rolled back char={character} id={take.Id}: {error.Message}");
                host.Send(character, MailPackets.BuildStatus(0xfd, MailPackets.BadData));
                RefreshClaimedMessage(character, message);
                Notice(character, "Could not take the attachments. Try again.");
                return;
            }

            // Keep the bag adds and claim result together before refreshing the inbox.
            host.Send(character, delivered.Select(row =>
                ServerPackets.BuildInventoryItemAdd(row.Slot, row.Item.Goid, row.Item.InstanceData))
                .Append(MailPackets.BuildStatus(0xfd, MailPackets.Success)).ToArray());
            RefreshClaimedMessage(character, message);
            Output.WriteDebugLog($"[MAIL] Claimed char={character} id={take.Id} items={delivered.Count}");
        }

        private void RefreshClaimedMessage(uint character, MailMessage message)
        {
            // Take All disables the stock inbox. 81 fd reports the result, while 81 f3 redraws the
            // attachment grid from the message's current ItemList and re-enables the controls.
            if (message != null)
                host.Send(character, MailPackets.BuildMessage(message));
        }

        /// <summary>Client 81 e5: send the mail and move any assets in one transaction.</summary>
        public void OnSend(uint character, MailRequests.Send send)
        {
            Dictionary<int, string> chunked = TakeChunks(character);
            string Chunk(int index) => send.IsChunked(index) || send.Chunk(index) == null
                ? chunked.TryGetValue(index, out string text) ? text : send.Chunk(index) ?? ""
                : send.Chunk(index);

            string to = Chunk(0), subject = Chunk(1), body = Chunk(2);
            MailOob.Outgoing oob = MailOob.ParseOutgoing(Chunk(3));
            if (oob.Invalid || Enumerable.Range(0, 4).Any(index => send.IsChunked(index) && !chunked.ContainsKey(index)))
            {
                Notice(character, "The mail data was incomplete. Reopen the compose window and retry.");
                host.Send(character, MailPackets.BuildSendResult(0, MailPackets.BadData));
                return;
            }
            var handles = (oob.Recipients.Count > 0 ? oob.Recipients
                : to.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList())
                .Select(MailPackets.HandleOf).Where(h => h.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            var recipients = new List<uint>();
            bool toBot = false;
            string missing = null;
            var messages = new List<MailMessage>();
            var changedCash = new Dictionary<uint, uint>();
            var slots = new List<ushort>();
            try
            {
                foreach (string handle in handles)
                {
                    if (BotEnabled && string.Equals(handle, BotHandle, StringComparison.OrdinalIgnoreCase))
                    {
                        toBot = true;
                        continue;
                    }
                    uint recipient = host.CharIdByHandle(handle);
                    if (recipient == 0)
                    {
                        missing = handle;
                        throw new Refusal(MailPackets.RecipientUnknown);
                    }
                    if (!recipients.Contains(recipient)) recipients.Add(recipient);
                }
                if (recipients.Count == 0 && !toBot) throw new Refusal(MailPackets.RecipientUnknown);

                uint now = clock();
                string senderHandle = host.HandleOf(character);
                string sender = MailPackets.Address(serverName(), senderHandle);
                string bot = toBot ? MailPackets.Address(serverName(), BotHandle) : null;
                bool attachments = oob.Info > 0 || oob.Items.Count > 0;
                if (attachments && (recipients.Count + (toBot ? 1 : 0) != 1 || host.IsDead(character) ||
                    oob.Items.Count > MaxAttachments || oob.Items.Any(item => !item.FromPersonalInventory || item.Slot < 0 ||
                        item.Slot > MyWorldDbAccess.LastBagSlot) ||
                    oob.Items.Select(item => item.Slot).Distinct().Count() != oob.Items.Count))
                    throw new Refusal(MailPackets.BadData);

                using var transfer = beginTransfer();
                var cash = new Dictionary<uint, long>();
                // Taking the character rows in ID order also serializes inbox capacity checks.
                foreach (uint id in recipients.Append(character).Distinct().OrderBy(id => id))
                {
                    long balance = transfer.ReadCash(id);
                    if (balance < 0) throw new Refusal(MailPackets.BadData);
                    cash.Add(id, balance);
                }
                foreach (uint recipient in recipients)
                {
                    if (recipient != character && transfer.RecipientIgnores(recipient, senderHandle))
                        throw new Refusal(MailPackets.Ignoring,
                            "A recipient is ignoring you. Your items and information were kept.");
                    if (send.EnforceInboxLimit && transfer.InboxCount(recipient) >= MailPackets.InboxLimit)
                        throw new Refusal(MailPackets.InboxFull);
                }

                var items = new List<MailItem>();
                if (oob.Items.Count > 0)
                {
                    var bag = transfer.ReadBag(character).ToLookup(row => (int)row.Slot, row => row.Item);
                    foreach (var attachment in oob.Items)
                    {
                        var matches = bag[attachment.Slot].ToArray();
                        if (matches.Length != 1 || matches[0].Goid == 0) throw new Refusal(MailPackets.BadData);
                        if (DataMining.IsNonTradeable(matches[0].Goid))
                            throw new Refusal(MailPackets.BadData, "This item cannot be attached to mail.");
                        items.Add(matches[0]);
                        slots.Add((ushort)attachment.Slot);
                    }
                }
                if (oob.Info > cash[character]) throw new Refusal(MailPackets.BadData);
                if (oob.Info != 0 && !toBot && recipients[0] != character)
                {
                    uint recipient = recipients[0];
                    if (cash[recipient] + oob.Info > int.MaxValue) throw new Refusal(MailPackets.BadData);
                    changedCash.Add(character, (uint)(cash[character] - oob.Info));
                    changedCash.Add(recipient, (uint)(cash[recipient] + oob.Info));
                }

                foreach (ushort slot in slots) transfer.RemoveItem(character, slot);
                foreach (var balance in changedCash) transfer.SetCash(balance.Key, balance.Value);
                foreach (uint recipient in recipients)
                    messages.Add(new MailMessage { OwnerCharId = recipient, Sender = sender, SentTime = now,
                        Subject = subject, Body = body, Items = items });
                if (toBot)
                    messages.Add(new MailMessage { OwnerCharId = character, Sender = bot, SentTime = now,
                        Subject = "RE: " + subject, Body = "MailBot got your message:\n\n" + body, Items = items });
                foreach (var message in messages) transfer.AddMessage(message);
                transfer.Commit();
            }
            catch (Refusal error)
            {
                if (error.Notice != null)
                    Notice(character, error.Notice);
                else if (error.Status == MailPackets.BadData)
                    Notice(character, "Mail could not be sent. Check the attached items and information.");
                host.Send(character, MailPackets.BuildSendResult(0, error.Status));
                if (error.Status == MailPackets.RecipientUnknown)
                {
                    string reason = missing == null ? "The message had no recipient." : "There is no one called " + missing + ".";
                    Deliver(SystemMessage(character, "Message Undeliverable",
                        "Your message \"" + subject + "\" to " + to + " could not be delivered. " + reason));
                }
                return;
            }
            catch (Exception error) when (error is DbException || error is InvalidOperationException || error is OverflowException)
            {
                Output.WriteDebugLog($"[MAIL] Send rolled back char={character}: {error.Message}");
                Notice(character, "Could not send the mail. Check your attachments and information before retrying.");
                host.Send(character, MailPackets.BuildSendResult(0, MailPackets.BadData));
                return;
            }

            foreach (ushort slot in slots) host.Send(character, ServerPackets.BuildInventoryItemDelete(slot));
            if (changedCash.TryGetValue(character, out uint senderCash)) host.PublishCash(character, senderCash);
            foreach (var message in messages.Take(recipients.Count))
            {
                Notify(message);
                if (message.OwnerCharId != character && changedCash.TryGetValue(message.OwnerCharId, out uint cash))
                    host.PublishCash(message.OwnerCharId, cash);
            }
            host.Send(character, MailPackets.BuildSendResult(recipients.Count > 0 ? messages[0].Id : 0, MailPackets.Success));
            if (toBot) Notify(messages[messages.Count - 1]);
            Output.WriteDebugLog($"[MAIL] Sent char={character} recipients={recipients.Count} items={slots.Count} info={oob.Info}");
        }

        private void Notice(uint character, string text) =>
            host.Send(character, ServerPackets.BuildSystemChatMessage(text, "BROADCAST"));

        internal void OnMalformedSend(uint character)
        {
            TakeChunks(character);
            Notice(character, "The mail data was incomplete. Reopen the compose window and retry.");
            host.Send(character, MailPackets.BuildSendResult(0, MailPackets.BadData));
        }

        internal void OnMalformedClaim(uint character)
        {
            Notice(character, "The attachment request was incomplete. Open the message again and retry.");
            host.Send(character, MailPackets.BuildStatus(0xfd, MailPackets.BadData));
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
            Notify(message);
        }

        private void Notify(MailMessage message) =>
            host.Send(message.OwnerCharId, MailPackets.BuildHeader(message, false, true));
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

        public void Send(UInt32 charId, params byte[][] records)
        {
            WorldClient client = MailClient(charId);
            if (client != null)
            {
                lock (client.messageQueue.RPCMessagesQueue.SyncRoot)
                    foreach (byte[] record in records)
                        client.messageQueue.addRpcMessage(record);
                client.FlushQueue();
            }
        }

        public bool IsDead(UInt32 charId) => SocialHandler.FindOnlineByCharId(charId)?.playerData.isDead ?? false;

        public void PublishCash(uint charId, uint cash)
        {
            WorldClient client = MailClient(charId);
            if (client == null) return;
            var publication = new AbilityAcquisition.Publication(client);
            if (publication.Character == charId && publication.PublishPurchase(client, cash,
                ServerPackets.BuildInfoCurrent(cash, MarketplacePackets.InfoReasonMarketplace)))
                client.FlushQueue();
        }

        private static WorldClient MailClient(uint charId)
        {
            lock (WorldServer.Clients)
                return WorldServer.Clients.Values.FirstOrDefault(client => client != null && client.Alive &&
                    client.playerData != null && client.playerData.getCharID() == charId &&
                    (client.playerData.getOnWorld() || client.playerData.DistrictIsLA()));
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
                        var store = new MySqlMailStore();
                        service = new MailService(store, new WorldMailHost(), store.BeginTransfer, () => ChatAddress.WorldName)
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
                        else Service.OnMalformedSend(charId);
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
                        else Service.OnMalformedClaim(charId);
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
