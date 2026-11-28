using System;
using System.Collections.Generic;
using System.Data.Common;
using hds.shared;

namespace hds
{
    /// <summary>
    /// Items a killed NPC drops. The retail loot windows (81 19) in research/packet-dumps mostly held Code Bits 1-8
    /// (goids 7332-7339, the crafting material), now and then a Health Pill 1.0 (2399) and rarely clothing, class
    /// routines or event items; up to three items, often none. The odds below are a rough fit to those windows.
    /// </summary>
    public static class NpcLoot
    {
        public const UInt32 CodeBit1 = 7332; // Code Bit 1 .. Code Bit 8 are 7332 .. 7339
        public const UInt32 HealthPill1 = 2399;
        internal const string AtomicFixture = "MXO_CORPSE_TAKE_ALL_QA";
        private const double ReplacementCloseWaitSeconds = 2;

        /// <summary>
        /// Retail never auto-pushed 81 19 on NPC death (82/82 retail 81 19 replies answered client 80 c7).
        /// For our automatic post-kill corpse popup, wait briefly after SendNpcDies + SendCombatEnd so the
        /// 7.6005 client exits the interlock UI before receiving 81 1a + 81 19.
        /// </summary>
        public const double AutoOpenAfterCombatEndSeconds = 0.25;

        /// <summary>Code Bit tier for an NPC level: 1 up to level 9, then one tier per ten levels, at most 8.</summary>
        public static UInt32 CodeBitFor(ushort level)
        {
            int tier = Math.Min(8, Math.Max(1, 1 + level / 10));
            return CodeBit1 + (UInt32)(tier - 1);
        }

        public static List<UInt32> Roll(ushort level, Random random)
        {
            List<UInt32> items = new List<UInt32>();
            if (random.NextDouble() < 0.6)
            {
                items.Add(CodeBitFor(level));
            }
            if (random.NextDouble() < 0.2)
            {
                items.Add(CodeBitFor((ushort)Math.Max(0, level - 10)));
            }
            if (random.NextDouble() < 0.15)
            {
                items.Add(HealthPill1);
            }
            return items;
        }

        /// <summary>
        /// Retail loot window: <c>81 19 00 00 00 00 10 00 &lt;info u32&gt; 00 00 00 00 &lt;count u16&gt;</c> then per item
        /// <c>00 00 00 &lt;flags&gt; &lt;goid u32&gt;</c> (e.g. <c>... 03 00 | 00 00 00 00 a7 1c 00 00 | 00 00 00 00 51 b8 00 00 | ...</c>).
        /// Code Bits and pills had flags 00; mission items go out with 08, which the client already accepted.
        /// </summary>
        public static byte[] BuildLootWindow(UInt32 info, IList<UInt32> items, IList<byte> flags)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16((UInt16)RPCResponseHeaders.SERVER_LOOT_WINDOW_RESPONSE, 0);
            pak.AddHexBytes("000000001000");
            pak.AddUint32(info, 1);
            pak.AddHexBytes("00000000");
            pak.AddUint16((UInt16)items.Count, 1);
            for (int i = 0; i < items.Count; i++)
            {
                pak.AddHexBytes("000000");
                pak.AddByte(flags[i]);
                pak.AddUint32(items[i], 1);
            }
            return pak.ReturnFinalPacket();
        }

        public static void Drop(WorldClient client, Mob source, UInt32 info, UInt32 missionItem, IEnumerable<UInt32> items)
        {
            DropAndRegister(client, source, info, missionItem, items, deferPresentation: false);
        }

        /// <summary>
        /// Records a freshly killed NPC's corpse loot in the recipient's cache (and mission checkpoint when applicable).
        /// When <paramref name="deferPresentation"/> is true, the corpse is immediately lootable via manual double-click
        /// (<c>80 c7 ... 05 00</c>), while the automatic <c>81 1a</c> + <c>81 19</c> popup is left for
        /// <see cref="ScheduleAutoOpen"/> after combat teardown and SQL commit.
        /// </summary>
        internal static NpcLootCache.Corpse DropAndRegister(WorldClient client, Mob source, UInt32 info, UInt32 missionItem,
            IEnumerable<UInt32> items, bool deferPresentation)
        {
            if (client.playerData.missionProgress != null && client.playerData.missionMobs.Contains(source))
            {
                return MissionPersistence.Mutate(client, () => DropCore(client, source, info, missionItem, items, deferPresentation));
            }
            return DropCore(client, source, info, missionItem, items, deferPresentation);
        }

        private static NpcLootCache.Corpse DropCore(WorldClient client, Mob source, UInt32 info, UInt32 missionItem,
            IEnumerable<UInt32> items, bool deferPresentation)
        {
            if (Environment.GetEnvironmentVariable(AtomicFixture) == "1" && client.playerData.getCharID() == 110 &&
                (source.getName() == "Loot Test Dummy" || source.getName() == "Loot Spare Dummy") &&
                (Store.config?.dbParams?.DatabaseName?.StartsWith("mxotest_", StringComparison.Ordinal) ?? false))
            {
                info = source.getName() == "Loot Test Dummy" ? 25u : 35u;
                items = new[] { CodeBit1, HealthPill1 };
            }
            lock (client.playerData.loot)
            {
                NpcLootCache.Corpse corpse = client.playerData.loot.Add(source, info, missionItem, items);
                if (!deferPresentation)
                {
                    SendWindow(client, corpse);
                }
                return corpse;
            }
        }

        /// <summary>
        /// Schedules the automatic post-kill <c>81 1a</c> + <c>81 19</c> corpse window after combat teardown.
        /// Call only after any mission SQL mutation has committed.
        /// </summary>
        internal static void ScheduleAutoOpen(WorldClient client, NpcLootCache.Corpse corpse,
            double delaySeconds = AutoOpenAfterCombatEndSeconds)
        {
            if (client == null || corpse == null) return;
            long epoch = client.TransportFence.Capture();
            int worldGen = client.playerData.worldGeneration;
            int combatGen = client.playerData.combatGeneration;
            DelayedActions.AfterFor(client, delaySeconds, () => TryPresentAutoOpen(client, corpse, epoch, worldGen, combatGen));
        }

        internal static bool TryPresentAutoOpen(WorldClient client, NpcLootCache.Corpse corpse,
            long epoch, int worldGen, int combatGen)
        {
            if (client == null || corpse == null || !client.Alive || !client.playerData.getOnWorld() ||
                client.playerData.worldGeneration != worldGen || client.playerData.combatGeneration != combatGen ||
                !client.TransportFence.IsCurrent(epoch))
            {
                return false;
            }

            lock (client.playerData.loot)
            {
                if (!client.playerData.loot.WindowOpen || !ReferenceEquals(client.playerData.loot.Current, corpse) ||
                    !corpse.Available || corpse.Presented)
                {
                    return false;
                }
                SendWindow(client, corpse);
                Output.WriteDebugLog($"[COMBAT] Loot window sent with {corpse.Info} info and {corpse.Items.Count + (corpse.MissionItem == 0 ? 0 : 1)} items");
            }
            client.FlushQueue(false, true);
            return true;
        }

        /// <summary>Reopens the killer's saved contents for this corpse, including a partially looted full bag.</summary>
        public static void Open(WorldClient client, UInt64 entity)
        {
            lock (client.playerData.loot)
            {
                SendWindow(client, client.playerData.loot.Select(entity));
            }
            client.FlushQueue(false, true);
        }

        private static void SendWindow(WorldClient client, NpcLootCache.Corpse corpse)
        {
            var loot = client.playerData.loot;
            if (corpse != null) corpse.Presented = true; // Manual Search also reserves a pending presentation.
            if (loot.Replacement is NpcLootCache.PendingPresentation waiting)
            {
                waiting.Corpse = corpse;
                return;
            }
            if (!loot.HasPresentedWindow)
            {
                SendWindowContents(client, corpse, clearCache: true);
                return;
            }

            // The native client echoes 81 1a when it actually closes the previous visible window.
            // Expose the new selection only after that echo, so the old close cannot clear it.
            var pending = new NpcLootCache.PendingPresentation(client, corpse);
            loot.Replacement = pending;
            loot.HasPresentedWindow = false;
            client.messageQueue.addRpcMessage(ServerPackets.BuildLootClosed());
            // A client whose old window was already hidden can clear its cache without echoing the close.
            DelayedActions.AfterFor(client, ReplacementCloseWaitSeconds, () => CompleteReplacement(client, pending, echoed: false));
        }

        internal static bool AcknowledgeWindowClose(WorldClient client)
        {
            var loot = client.playerData.loot;
            NpcLootCache.PendingPresentation pending;
            lock (loot) pending = loot.Replacement;
            if (pending == null) return false;
            CompleteReplacement(client, pending, echoed: true);
            return true;
        }

        private static void CompleteReplacement(WorldClient client, NpcLootCache.PendingPresentation pending, bool echoed)
        {
            var data = client.playerData;
            var loot = data.loot;
            lock (loot)
            {
                if (!ReferenceEquals(loot.Replacement, pending)) return;
                loot.Replacement = null;
                if (!client.Alive || !data.getOnWorld() || data.worldChangePending ||
                    data.worldGeneration != pending.World || !client.TransportFence.IsCurrent(pending.Transport) ||
                    !loot.WindowOpen || !ReferenceEquals(loot.Current, pending.Corpse)) return;
                SendWindowContents(client, pending.Corpse);
                Output.WriteDebugLog($"[LOOT] Replacement window opened char={data.getCharID()} closeEcho={echoed}");
            }
            client.FlushQueue(false, true);
        }

        private static void SendWindowContents(WorldClient client, NpcLootCache.Corpse corpse, bool clearCache = false)
        {
            List<UInt32> items = new List<UInt32>();
            List<byte> flags = new List<byte>();
            if (corpse != null)
            {
                if (corpse.MissionItem != 0)
                {
                    items.Add(corpse.MissionItem);
                    flags.Add(0x08);
                }
                foreach (UInt32 item in corpse.Items)
                {
                    items.Add(item);
                    flags.Add(0);
                }
            }
            client.playerData.loot.HasPresentedWindow = true;
            if (clearCache)
                foreach (var reply in BuildLootPresentation(corpse?.Info ?? 0, items, flags))
                    client.messageQueue.addRpcMessage(reply);
            else
                client.messageQueue.addRpcMessage(BuildLootWindow(corpse?.Info ?? 0, items, flags));
        }

        // Client6202088b returns a cached container without showing it. Close the previous UI
        // before replacing its selection;811a clears that cache, then8119 creates a visible window.
        internal static byte[][] BuildLootPresentation(UInt32 info,IList<UInt32> items,IList<byte> flags) =>
            new[]{ServerPackets.BuildLootClosed(),BuildLootWindow(info,items,flags)};

        /// <summary>Grants the selected corpse once, retaining every item that does not fit.</summary>
        public static bool TryTakeAll(WorldClient client)
        {
            lock(client.playerData.missionSync)
            {
                Mob source;
                lock(client.playerData.loot)source=client.playerData.loot.Current?.Source;
                if(client.playerData.missionProgress!=null&&source!=null&&client.playerData.missionMobs.Contains(source))
                    return MissionPersistence.Mutate(client,()=>TryTakeAllCore(client));
            }
            return TryTakeAllCore(client);
        }
        private static bool TryTakeAllCore(WorldClient client)
        {
            Mob emptied = null;
            ServerPackets packets = new ServerPackets();
            lock (client.playerData.loot)
            {
                if (!client.playerData.loot.WindowOpen)
                {
                    return false;
                }
                if (client.playerData.loot.Replacement != null) return true; // The request belongs to the old window.
                NpcLootCache.Corpse corpse = client.playerData.loot.Current;
                if (corpse == null)
                {
                    packets.SendLootAccepted(client);
                    return true;
                }

                bool missionCorpse = client.playerData.missionProgress != null && client.playerData.missionMobs.Contains(corpse.Source);
                if (!missionCorpse && corpse.MissionItem == 0)
                {
                    if (ClaimOrdinary(client, corpse) && corpse.Items.Count == 0) emptied = corpse.Source;
                }
                else
                {
                    if (corpse.Info > 0)
                    {
                        if (missionCorpse)
                        {
                            corpse.Info = 0;
                            MissionPersistence.Save(client);
                        }
                        else
                        {
                            UInt32 total = (UInt32)Math.Min(client.playerData.getInfo() + (long)corpse.Info, UInt32.MaxValue);
                            Store.dbManager.WorldDbHandler.SaveInfo(client, total);
                            client.playerData.setInfo(total);
                            corpse.Info = 0;
                        }
                    }
                    packets.SendInfoCurrent(client, (UInt32)client.playerData.getInfo());
                    var claimed = GiveItems(client, corpse.Items);
                    if (corpse.MissionItem != 0 && MissionRuntime.OnMissionItemLooted(client, corpse.MissionItem))
                    {
                        claimed.Add((new byte[] { 0, 0, 0, 0x08 }, corpse.MissionItem));
                        corpse.MissionItem = 0;
                    }
                    packets.SendLootAccepted(client, claimed.ToArray());
                    if (corpse.Items.Count == 0 && corpse.MissionItem == 0) emptied = corpse.Source;
                }
            }
            // Tutorial callbacks can remove NPCs. Keep their lock outside the loot cache's lock.
            if (emptied != null)
            {
                TutorialMission.Of(client)?.OnCorpseLooted(emptied);
            }
            return true;
        }

        private static bool ClaimOrdinary(WorldClient client, NpcLootCache.Corpse corpse)
        {
            var publication = new AbilityAcquisition.Publication(client);
            (uint Cash, (int Index, ushort Slot, int Count, bool Added)[] Items) result;
            try
            {
                result = Store.dbManager.WorldDbHandler.ClaimCorpseLoot(publication.Character, corpse.Info, corpse.Items);
            }
            catch (DbException error)
            {
                Output.WriteDebugLog($"[LOOT] Take All rolled back char={publication.Character}: {error.Message}");
                result = default;
            }
            if (result.Items == null)
            {
                if (publication.Publish(client, ServerPackets.BuildSystemChatMessage(
                        "The loot could not be saved. The items and information were kept.", "BROADCAST")))
                {
                    SendWindow(client, corpse);
                    client.FlushQueue();
                }
                return false;
            }

            var replies = new List<byte[]> { ServerPackets.BuildInfoCurrent(result.Cash, ServerPackets.InfoReasonLoad) };
            var claimed = new (byte[] Data, uint Item)[result.Items.Length];
            for (int i = 0; i < result.Items.Length; i++)
            {
                var item = result.Items[i];
                uint goid = corpse.Items[item.Index];
                claimed[i] = (new byte[4], goid);
                replies.Add(!item.Added ? ItemStacks.BuildStackCount(item.Slot, item.Count)
                    : AbilityProgression.IsAbilityCode(goid) ? ServerPackets.BuildAbilityCodeAdd(item.Slot, goid)
                    : ServerPackets.BuildInventoryItemAdd(item.Slot, goid, 0, (ushort)(RangedWeapons.IsGun(goid) ? 0x10 : 0)));
            }
            for (int i = result.Items.Length - 1; i >= 0; i--)
                corpse.Items.RemoveAt(result.Items[i].Index);
            if (corpse.Items.Count > 0)
                replies.Add(ServerPackets.BuildSystemChatMessage("Your inventory is full. The remaining items were kept.", "BROADCAST"));
            replies.Add(ServerPackets.BuildLootAccepted(claimed));
            Output.WriteDebugLog($"[LOOT] Take All committed char={publication.Character} items={claimed.Length} info={corpse.Info}");
            corpse.Info = 0;
            if (publication.PublishPurchase(client, result.Cash, replies.ToArray())) client.FlushQueue();
            return true;
        }

        /// <summary>
        /// 81 1b "loot one item" (ListID u32, ObjectID u32, InstanceData u32). client.dll 7.6005 compiles the record but never
        /// sends it (docs/CLIENT-ACTIONS.md), so this is OURS for clients that do: ObjectID is the item's GOID and
        /// InstanceData the window row's leading u32 (flag 0x08 in its top byte marks the mission item). One matching item
        /// is granted if it fits and answered with the same 81 20 claimed identity as Take All; Info stays for Take All.
        /// </summary>
        public static bool TryTakeOne(WorldClient client, UInt32 goid, UInt32 instanceData)
        {
            lock(client.playerData.missionSync)
            {
                Mob source;
                lock(client.playerData.loot)source=client.playerData.loot.Current?.Source;
                if(client.playerData.missionProgress!=null&&source!=null&&client.playerData.missionMobs.Contains(source))
                    return MissionPersistence.Mutate(client,()=>TryTakeOneCore(client,goid,instanceData));
            }
            return TryTakeOneCore(client,goid,instanceData);
        }
        private static bool TryTakeOneCore(WorldClient client, UInt32 goid, UInt32 instanceData)
        {
            Mob emptied = null;
            lock (client.playerData.loot)
            {
                if (!client.playerData.loot.WindowOpen) return false;
                if (client.playerData.loot.Replacement != null) return true;
                NpcLootCache.Corpse corpse = client.playerData.loot.Current;
                var claimed = new List<(byte[] Data, UInt32 Item)>();
                if (corpse != null)
                {
                    bool missionRow = (instanceData >> 24) == 0x08;
                    if (missionRow && corpse.MissionItem != 0 && corpse.MissionItem == goid)
                    {
                        if (MissionRuntime.OnMissionItemLooted(client, corpse.MissionItem))
                        {
                            claimed.Add((new byte[] { 0, 0, 0, 0x08 }, corpse.MissionItem));
                            corpse.MissionItem = 0;
                        }
                    }
                    else if (!missionRow)
                    {
                        claimed.AddRange(ClaimOne(corpse.Items, goid, item => GiveItems(client, new List<UInt32> { item }).Count == 1));
                    }
                    if (claimed.Count > 0 && corpse.Items.Count == 0 && corpse.MissionItem == 0 && corpse.Info == 0)
                        emptied = corpse.Source;
                }
                Output.WriteDebugLog("[LOOT] Take one " + goid + " instance " + instanceData.ToString("x8") + ": " +
                                     (claimed.Count > 0 ? "granted" : "not granted"));
                new ServerPackets().SendLootAccepted(client, claimed.ToArray());
            }
            if (emptied != null) TutorialMission.Of(client)?.OnCorpseLooted(emptied);
            return true;
        }

        /// <summary>Removes each granted item immediately, so a later failure cannot grant it again on retry.</summary>
        private static List<(byte[] Data, UInt32 Item)> GiveItems(WorldClient client, List<UInt32> items) =>
            ClaimItems(items, goid =>
            {
                // Existing inventory policy stacks Code Bits/pills and refuses a full bag.
                // The retail tutorial Redpill Special loot had the weapon descriptor 0x10.
                UInt16 type = RangedWeapons.GetStats(goid) != null ? (UInt16)0x10 : (UInt16)0;
                UInt16 slot = InventoryHandler.AddItem(client, goid, type);
                Output.WriteDebugLog("[LOOT] Item " + goid + " into slot " + slot);
                return slot != 0;
            });

        /// <summary>Grants one copy of <paramref name="goid"/> from the corpse and removes only that copy.</summary>
        internal static List<(byte[] Data, UInt32 Item)> ClaimOne(List<UInt32> items, UInt32 goid, Func<UInt32,bool> give)
        {
            var claimed = new List<(byte[] Data, UInt32 Item)>();
            int index = items.IndexOf(goid);
            if (index >= 0 && give(goid))
            {
                items.RemoveAt(index);
                claimed.Add((new byte[4], goid));
            }
            return claimed;
        }

        internal static List<(byte[] Data, UInt32 Item)> ClaimItems(List<UInt32> items, Func<UInt32,bool> give)
        {
            var claimed = new List<(byte[] Data, UInt32 Item)>();
            int index = 0;
            while (index < items.Count)
            {
                UInt32 goid = items[index];
                if (!give(goid)) index++;
                else
                {
                    items.RemoveAt(index);
                    claimed.Add((new byte[4], goid));
                }
            }
            return claimed;
        }
    }
}
