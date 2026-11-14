using System;
using System.Runtime.CompilerServices;
using hds.shared;

namespace hds
{
    public static class OrdinaryItemCompile
    {
        private sealed class Pending { public double Stamp; }
        private static readonly ConditionalWeakTable<WorldClient, Pending> pending = new ConditionalWeakTable<WorldClient, Pending>();
        private static readonly Random random = new Random();
        private static int Roll()
        {
            lock (random) return (int)Math.Clamp(50 + 16 * Math.Sqrt(-2 * Math.Log(1 - random.NextDouble())) * Math.Cos(2 * Math.PI * random.NextDouble()), 0, 100);
        }
        private static bool Ready(WorldClient client, byte tool) => client.Alive && client.playerData.getOnWorld() &&
            !client.playerData.worldChangePending && !client.playerData.isDead && !client.playerData.duelDeath &&
            CraftingSession.Authorized(client, tool) && CombatSelection.IsLoaded(277,
                Store.dbManager.MarginDbHandler.LoadAbilities((int)client.playerData.getCharID()));

        public static void Cancel(WorldClient client)
        {
            lock (client.playerData.craftingSync)
            {
                if (!pending.TryGetValue(client, out var token)) return;
                pending.Remove(client);
                if (client.playerData.castEndsAt == token.Stamp) client.playerData.castEndsAt = 0;
                Finish(client, 1);
            }
        }

        /// <summary>Returns false for other code families so the existing flagged executor stays unchanged.</summary>
        public static bool TryStart(WorldClient client, ClientActionRequests.StoredCompile request)
        {
            if (request == null || !OrdinaryItemCode.Supported(request.Item)) return false;
            var data = client.playerData;
            lock (data.craftingSync)
            {
                var db = Store.dbManager.WorldDbHandler;
                double now = TimeUtils.GetSimTimeSeconds();
                if (!request.FromCrafting || request.Slot >= 0x208 || !Ready(client, request.Tool) ||
                    pending.TryGetValue(client, out _) || AbilityCooldowns.IsCasting(data, now)) { Finish(client, 1); return true; }
                var source = db.GetOrdinaryItemCode(data.getCharID(), request.Slot);
                var item = CraftingRules.Find(request.Item);
                if (source == null || source.Goid != request.Item) { Finish(client, 1); return true; }
                int skill = CraftingHandler.Skill(client, 706), specialization = CraftingHandler.Skill(client, 656);
                long cost = OrdinaryItemCode.Cost(item, CraftingHandler.Skill(client, 530));
                if (data.getInfo() < cost) { Finish(client, 4); return true; }
                int general = CraftingHandler.Skill(client, 954), consumable = CraftingHandler.Skill(client, 955);
                float seconds = OrdinaryItemCode.Seconds(item.Complexity.Value, skill, general, consumable);
                var token = new Pending(); pending.Add(client, token);
                var session = data.craftingSession;
                int world = data.worldGeneration, life = data.lifeGeneration;
                AbilityCooldowns.StartCast(data, now, seconds);
                double stamp = data.castEndsAt;
                token.Stamp = stamp;
                // Native already starts its Compile window timer after sending60. No invented cast-bar packet.
                Output.WriteDebugLog("[COMPILE60] Started " + request.Item + " seconds " + seconds + " skill " + skill + " 954 " + general + " 955 " + consumable + " cost " + cost);
                DelayedActions.AfterFor(client, seconds, () =>
                {
                    lock (data.craftingSync)
                    {
                        if (!pending.TryGetValue(client, out var current) || current != token) return;
                        pending.Remove(client);
                        bool committed = false;
                        CompleteSafely(() =>
                        {
                        if (client.playerData != data || !Ready(client, request.Tool) || data.craftingSession != session ||
                            !Matches(world, life, stamp, data.worldGeneration, data.lifeGeneration, data.castEndsAt))
                        { Finish(client, 1); return; }
                        if (!OrdinaryItemCode.Succeeds(skill, item.Complexity.Value, Roll(), specialization))
                        { Output.WriteDebugLog("[COMPILE60] Failed " + request.Item + "; code and Info retained"); Finish(client, 1); return; }
                        var transfer = db.MaterializeOrdinaryItemCode(data.getCharID(), source, request.Tool, cost);
                        committed = transfer.Result == 0;
                        if (transfer.Result == 0)
                        {
                            client.messageQueue.addRpcMessage(transfer.Remaining == 0 ? CodeStorage.BuildRemove(source.Slot) : OrdinaryItemCode.Update(source.Slot, source.Base, transfer.Remaining));
                            if (transfer.BagCount > 1) client.messageQueue.addRpcMessage(ItemStacks.BuildStackCount(transfer.Slot, transfer.BagCount));
                            else new ServerPackets().sendInventoryItemAdd(transfer.Slot, source.Goid, 0, 0, client);
                            data.setInfo(transfer.Cash); new ServerPackets().SendInfoCurrent(client, (uint)transfer.Cash);
                        }
                        Output.WriteDebugLog("[COMPILE60] Finished " + request.Item + " result " + transfer.Result + " remaining " + transfer.Remaining);
                        Finish(client, transfer.Result);
                        }, ex =>
                        {
                            Output.WriteDebugLog("[COMPILE60] Completion failed for " + request.Item + "; committed=" + committed + ": " + ex);
                            // A delivery error after a successful commit cannot turn it into a failed economy operation.
                            Finish(client, committed ? (byte)0 : (byte)1);
                        });
                    }
                });
                return true;
            }
        }
        public static bool Matches(int world, int life, double stamp, int currentWorld, int currentLife, double currentStamp) =>
            world == currentWorld && life == currentLife && stamp == currentStamp;
        internal static void CompleteSafely(Action completion, Action<Exception> failed)
        {
            try { completion(); }
            catch (Exception ex) { failed(ex); }
        }
        private static void Finish(WorldClient client, byte result)
        {
            if (!client.Alive) return;
            client.messageQueue.addRpcMessage(new byte[] { 0x61, result }); client.FlushQueue();
        }
    }
}
