using System;
using System.Collections.Generic;
using System.Linq;

namespace hds
{
    /// <summary>The shared combat view's presentation, including clients watching either kind of interlock.
    /// Observers receive creation/init/round/delete only; they never receive participant RPCs or own combat flags.</summary>
    public sealed class CombatPresentation
    {
        private static readonly object registrySync = new object();
        private static readonly HashSet<CombatPresentation> registry = new HashSet<CombatPresentation>();
        private readonly object sync = new object();
        private readonly WorldClient attacker, defender;
        private readonly Mob mob;
        private readonly GameObject handler;
        private readonly ulong entity;
        private readonly byte[] centre;
        private readonly uint started;
        private readonly float[] aOffset, bOffset;
        private readonly Dictionary<byte, WorldClient> fighters = new Dictionary<byte, WorldClient>();
        private readonly Dictionary<byte, float[]> offsets = new Dictionary<byte, float[]>();
        private readonly Dictionary<WorldClient, ushort> observers = new Dictionary<WorldClient, ushort>();
        private bool ended;
        private uint sequence;

        public CombatPresentation(WorldClient attacker, WorldClient defender, Mob mob, AttributeClass3664 handler, ulong entity)
        {
            this.attacker = attacker; this.defender = defender; this.mob = mob; this.handler = handler; this.entity = entity;
            centre = handler.Position.getValue().ToArray();
            started = BitConverter.ToUInt32(handler.StartTime.getValue(), 0);
            double ax = 0, ay = 0, az = 0, bx = 0, by = 0, bz = 0;
            NumericalUtils.LtVector3dToDoubles(centre, ref ax, ref ay, ref az);
            if (defender != null) NumericalUtils.LtVector3dToDoubles(defender.playerInstance.Position.getValue(), ref bx, ref by, ref bz);
            else { bx = mob.getXPos(); by = mob.getYPos(); bz = mob.getZPos(); }
            double dx = bx - ax, dz = bz - az, length = Math.Sqrt(dx * dx + dz * dz);
            if (length < 0.01) { dx = 0; dz = 1; length = 1; }
            // Retail keeps the fighters 70 world units either side of the centre after the approach.
            aOffset = new[] { (float)(-70 * dx / length), 0f, (float)(-70 * dz / length) };
            bOffset = new[] { -aOffset[0], (float)(by - ay), -aOffset[2] };
            fighters.Add(1, attacker); offsets.Add(1, aOffset); offsets.Add(2, bOffset);
            if (defender != null) fighters.Add(2, defender);
            lock (registrySync) registry.Add(this);
        }

        public static void CheckObservers()
        {
            CombatPresentation[] active;
            lock (registrySync) active = registry.ToArray();
            foreach (var fight in active) fight.RefreshObservers();
        }
        private uint Handle(WorldClient receiver, bool first)
        {
            if (first) return PvpCombat.HandleOf(receiver, attacker);
            if (defender != null) return PvpCombat.HandleOf(receiver, defender);
            ClientView view = receiver.viewMan.FindViewForEntity(mob.getEntityId());
            return view?.viewCreated == true ? DuelPackets.Handle(view.ViewID, view.spawnId) : 0;
        }
        private bool CanSee(WorldClient client) => client.Alive && client.playerData.getOnWorld() && fighters.Count > 0 &&
            client.playerData.getDistrictId() == fighters.Values.First().playerData.getDistrictId() &&
            fighters.Values.All(fighter => PvpCombat.HandleOf(client, fighter) != 0) && (mob == null || Handle(client, false) != 0);

        private IReadOnlyDictionary<byte, uint> Handles(WorldClient receiver)
        {
            var handles = fighters.ToDictionary(p => p.Key, p => PvpCombat.HandleOf(receiver, p.Value));
            if (mob != null) handles.Add(2, Handle(receiver, false));
            return handles;
        }

        /// <summary>Promote an observer's existing view rather than creating the same view twice.</summary>
        public void AddFighter(WorldClient client, byte slot)
        {
            lock (sync)
            {
                if (ended || slot < 1 || slot > 6 || fighters.ContainsKey(slot)) throw new InvalidOperationException("Combat slot unavailable");
                observers.Remove(client);
                fighters.Add(slot, client);
                double angle = (slot - 2) * Math.PI / 2;
                offsets[slot] = new[] { (float)(160 * Math.Cos(angle)), 0f, (float)(160 * Math.Sin(angle)) };
                sequence++;
                foreach (var old in observers.Where(o => !CanSee(o.Key)).ToArray())
                { DeleteObserver(old.Key,old.Value); observers.Remove(old.Key); }
                foreach (WorldClient receiver in fighters.Values.Concat(observers.Keys).ToArray())
                {
                    ClientView view = receiver.viewMan.FindViewForEntity(entity);
                    if (view?.viewCreated != true) continue;
                    receiver.messageQueue.addObjectMessage(ServerPackets.BuildCombatInitialize(view.ViewID, centre, Handles(receiver), sequence), false);
                    receiver.FlushQueue();
                }
            }
        }

        public void RemoveFighter(WorldClient client, byte slot)
        {
            lock (sync)
            {
                if (!fighters.Remove(slot)) return;
                offsets.Remove(slot);
                sequence++;
                byte[] removed = ServerPackets.BuildCombatViewFinalRecord(centre, sequence, 1u << (slot - 1));
                foreach (var receiver in fighters.Values.Concat(observers.Keys).Where(c => c != client)) SendToFighter(receiver, removed);
            }
        }

        private void RefreshObservers()
        {
            WorldClient[] clients;
            lock (WorldServer.Clients) clients = WorldServer.Clients.Values.ToArray();
            lock (sync)
            {
                if (ended) return;
                foreach (var old in observers.ToArray())
                    if (!CanSee(old.Key) || old.Key.viewMan.getViewById(old.Value)?.entityId != entity)
                    { DeleteObserver(old.Key, old.Value); observers.Remove(old.Key); }
                foreach (WorldClient client in clients)
                {
                    if (fighters.ContainsValue(client) || observers.ContainsKey(client) || !CanSee(client)) continue;
                    // A departed fighter may still be waiting for the death teardown. Do not recreate its live view.
                    if (client.viewMan.FindViewForEntity(entity)?.viewCreated == true) continue;
                    new ServerPackets().SendSpawnGameObject(client, handler, entity);
                    ushort view = client.viewMan.FindViewForEntity(entity).ViewID;
                    client.messageQueue.addObjectMessage(ServerPackets.BuildCombatInitialize(view, centre, Handles(client), sequence + 1), false);
                    observers.Add(client, view);
                    client.FlushQueue();
                }
            }
        }
        public void Round(bool attackerWins, uint flags, CombatTactic attackerTactic, CombatTactic defenderTactic,
            ushort attackerRoll, ushort defenderRoll, bool opening)
            => RoundPair(1, 2, attackerWins, flags, attackerTactic, defenderTactic, attackerRoll, defenderRoll, opening);

        public void RoundPair(byte attackerSlot, byte defenderSlot, bool attackerWins, uint flags,
            CombatTactic attackerTactic, CombatTactic defenderTactic, ushort attackerRoll, ushort defenderRoll, bool opening)
        {
            RefreshObservers();
            lock (sync)
            {
                if (ended) return;
                sequence++;
                var firstTactic = attackerWins ? attackerTactic : defenderTactic;
                var secondTactic = attackerWins ? defenderTactic : attackerTactic;
                var animation = CombatAnimations.Choose(firstTactic, secondTactic, flags, sequence);
                ushort approach = opening ? (ushort)300 : (ushort)0;
                ushort duration = (ushort)(animation.DurationMs + (opening ? 1300 : 0));
                var entry = new CombatRoundEntry
                {
                    FirstSlot = attackerWins ? attackerSlot : defenderSlot, SecondSlot = attackerWins ? defenderSlot : attackerSlot,
                    FirstOffset = offsets[attackerWins ? attackerSlot : defenderSlot], SecondOffset = offsets[attackerWins ? defenderSlot : attackerSlot],
                    FirstApproachMs = approach, SecondApproachMs = approach,
                    Sequence = checked((ushort)sequence), StartMs = unchecked(CombatHandler.CombatStartTime() - started) + 200,
                    FirstDurationMs = duration, SecondDurationMs = duration, Animation = animation.Reference,
                    FirstTransition = opening ? CombatRoundEntry.Reference(CombatAnimations.Transition(firstTactic)) : 0,
                    SecondTransition = opening ? CombatRoundEntry.Reference(CombatAnimations.Transition(secondTactic)) : 0
                };
                CombatRoundEntry.U32(entry.ResultData, 0, attackerWins ? attackerRoll : defenderRoll);
                CombatRoundEntry.U32(entry.ResultData, 4, attackerWins ? defenderRoll : attackerRoll);
                CombatRoundEntry.U16(entry.ResultData, 8, 0x1000); CombatRoundEntry.U16(entry.ResultData, 10, 0x1000);
                byte[] update = ServerPackets.BuildCombatViewRound(centre, sequence, entry);
                foreach (WorldClient fighter in fighters.Values) SendToFighter(fighter, update);
                foreach (var observer in observers) Send(observer.Key, observer.Value, update);
                Output.WriteDebugLog($"[COMBAT-VIEW] Round {sequence}: animation {animation.Id} ({animation.Name}), {observers.Count} observers");
            }
        }
        private void SendToFighter(WorldClient client, byte[] update)
        {
            ClientView view = client.viewMan.FindViewForEntity(entity);
            if (client.Alive && client.playerData.getOnWorld() && view?.viewCreated == true) Send(client, view.ViewID, update);
        }
        private static void Send(WorldClient client, ushort view, byte[] update)
        {
            client.messageQueue.addObjectMessage(WorldServer.BuildViewUpdate(view, update), false);
            client.FlushQueue();
        }
        public void End(double delay = 0)
        {
            lock (sync) { if (ended) return; ended = true; }
            lock (registrySync) registry.Remove(this);
            if (delay > 0) DelayedActions.After(delay, ClearObservers); else ClearObservers();
        }
        private void ClearObservers()
        {
            lock (sync)
            {
                foreach (var observer in observers) DeleteObserver(observer.Key, observer.Value);
                observers.Clear();
            }
        }
        private void DeleteObserver(WorldClient client, ushort view)
        {
            if (client.viewMan.getViewById(view)?.entityId != entity) return;
            if (client.Alive && client.playerData.getOnWorld())
            {
                uint mask = 0;
                foreach (byte slot in offsets.Keys) mask |= 1u << (slot - 1);
                Send(client, view, ServerPackets.BuildCombatViewFinalRecord(centre, sequence, mask));
                byte[] delete = new byte[] { 1, 0, 1, 1, 0, (byte)view, (byte)(view >> 8) };
                client.messageQueue.addObjectMessage(delete, false); client.FlushQueue();
            }
            client.viewMan.removeViewByViewId(view);
        }
    }
}
