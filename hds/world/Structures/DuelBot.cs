using System;
using System.Collections.Generic;
using System.Linq;
using hds.shared;

namespace hds
{
    /// <summary>
    /// Dev command ?duelbot (MXO_DEV_COMMANDS=1): a player played by the server, standing next to whoever called it, so
    /// one real client can see the duel dialog, both sides of a fight between players, and its end. Like ?tradebot for
    /// trades. The bot is a real character in the world as far as every other piece of the server is concerned
    /// (<see cref="WorldClient.CreateBot"/>); what would be sent to it is dropped. See docs/PVP.md for the steps.
    /// </summary>
    public static class DuelBot
    {
        public const string Name = "DuelBot";

        public enum Mode
        {
            /// <summary>Accepts a duel and leaves the first blow to the player.</summary>
            Wait,
            /// <summary>Accepts a duel and pulls the player into close combat a few seconds later.</summary>
            Attack,
            /// <summary>Accepts a duel and opens fire a few seconds later.</summary>
            Gun,
            /// <summary>Challenges the player itself, then attacks like <see cref="Attack"/>.</summary>
            Challenge,
            /// <summary>No duel: flagged for PvP and of another organisation, to be attacked where the rules allow it.</summary>
            Pvp,
        }

        private sealed class Bot
        {
            public WorldClient Client, Owner;
            public Mode Mode;
        }

        private static readonly object Sync = new object();
        private static readonly List<Bot> Bots = new List<Bot>();
        private static int counter;
        private static System.Threading.Timer watchdog;

        /// <summary>The Redpill Special, the gun every character starts with.</summary>
        private const UInt32 BotGun = 2565;

        /// <summary>Seconds between the duel's start and the bot's first move, so the player sees the start first.</summary>
        public const double AttackDelaySeconds = 3;

        private static Bot Find(WorldClient client)
        {
            lock (Sync)
            {
                return Bots.FirstOrDefault(b => b.Client == client);
            }
        }

        public static bool TryParseMode(string text, out Mode mode)
        {
            switch ((text ?? "").ToLowerInvariant())
            {
                case "": case "wait": mode = Mode.Wait; return true;
                case "attack": mode = Mode.Attack; return true;
                case "gun": mode = Mode.Gun; return true;
                case "challenge": mode = Mode.Challenge; return true;
                case "pvp": mode = Mode.Pvp; return true;
                default: mode = Mode.Wait; return false;
            }
        }

        /// <summary>Puts a bot three metres from the owner, at their level, replacing the owner's previous one.</summary>
        public static WorldClient Summon(WorldClient owner, Mode mode)
        {
            Dismiss(owner);

            int n = System.Threading.Interlocked.Increment(ref counter);
            WorldClient bot = WorldClient.CreateBot("bot:duel:" + n);
            PlayerCharacter me = bot.playerInstance, them = owner.playerInstance;

            double x = 0, y = 0, z = 0;
            NumericalUtils.LtVector3dToDoubles(them.Position.getValue(), ref x, ref y, ref z);
            UInt16 maxHealth = NumericalUtils.ByteArrayToUint16(them.MaxHealth.getValue(), 1);
            UInt16 maxInner = NumericalUtils.ByteArrayToUint16(them.InnerStrengthMax.getValue(), 1);
            UInt32 charId = 0x7f000000u + (UInt32)n;
            byte ownerOrg = them.OrganizationID.getValue()[0];

            me.CharacterID.setValue(charId);
            me.CharacterName.setValue(Name);
            me.RealFirstName.setValue("Duel");
            me.RealLastName.setValue("Bot");
            byte ownerYaw = them.YawInterval.getValue()[0];
            bool onMesh = Spot(owner.playerData.getDistrictId(), x, y, z, ownerYaw, out double bx, out double by, out double bz, out NavLocation spot);
            me.Position.setValue(NumericalUtils.doublesToLtVector3d(bx, by, bz));
            // Facing the owner: in an interlock the bot doesn't turn by itself (see docs/PVP.md)
            me.YawInterval.setValue(MobAggro.YawTowards(bx, bz, x, z));
            me.Health.setValue(maxHealth);
            me.MaxHealth.setValue(maxHealth);
            me.InnerStrengthAvailable.setValue(maxInner);
            me.InnerStrengthMax.setValue(maxInner);
            me.Level.setValue(them.Level.getValue()[0]);
            me.TitleAbility.setValue(NumericalUtils.ByteArrayToUint32(them.TitleAbility.getValue(), 1));
            // Another organisation than the owner's: the PvP rules need two different ones (1 Zion, 2 Machines)
            me.OrganizationID.setValue(mode == Mode.Pvp ? (byte)(ownerOrg == 1 ? 2 : 1) : ownerOrg);
            if (mode == Mode.Gun)
            {
                me.EquippedItemID.setValue(BotGun);
                bot.playerData.freeFireWeaponGoid = BotGun;
            }

            ClientData data = bot.playerData;
            data.setCharID(charId);
            data.setRsiValues((int[])owner.playerData.getRsiValues().Clone());
            data.setDistrict(owner.playerData.getDistrict());
            data.setDistrictId(owner.playerData.getDistrictId());
            data.pvpFlag = mode == Mode.Pvp;
            me.ConditionStateFlags.setValue(PvpFlag.FlagsOf(data));

            lock (WorldServer.Clients)
            {
                data.setEntityId(WorldServer.entityIdCounter++);
                WorldServer.Clients.Add("bot:duel:" + n, bot);
            }
            data.setOnWorld(true);
            lock (Sync)
            {
                Bots.Add(new Bot { Client = bot, Owner = owner, Mode = mode });
                if (watchdog == null)
                {
                    watchdog = new System.Threading.Timer(_ => Watch(), null, 2000, 2000);
                }
            }
            Output.WriteDebugLog("[DUELBOT] Summoned for " + PvpFlag.Name(owner) + " (" + mode + ") at " +
                                 $"{bx:0},{by:0},{bz:0}, " + (onMesh
                                     ? $"sector {spot.sector}, region {spot.region}, polygon {spot.polygon}{(spot.indoors ? " (indoors)" : "")}"
                                     : "no nav mesh here") +
                                 $"; the owner at {x:0},{y:0},{z:0} facing {ownerYaw}");

            if (mode == Mode.Challenge)
            {
                // Once the owner's client has the bot's view (the visibility pass spawns it within a second)
                DelayedActions.After(AttackDelaySeconds, () => ChallengeOwner(bot, owner));
            }
            return bot;
        }

        /// <summary>How far in front of the owner the bot stands (world units, 100 to the metre), nearest first.</summary>
        private static readonly double[] Distances = { 250, 200, 300, 150, 100 };

        /// <summary>Which way from the owner's facing to try, in yaw steps (256 to the turn): ahead first, then around.</summary>
        private static readonly int[] Turns = { 0, 16, -16, 32, -32, 48, -48, 64, -64, 96, -96, 128 };

        /// <summary>The steps along the line from the owner to a spot (world units): thinner than any nav polygon.</summary>
        private const double LineStep = 10;

        /// <summary>Highest step up or down between two points of the line (world units).</summary>
        private const double MaxStep = 40;

        /// <summary>
        /// Where the bot stands: where the owner can see it and walk to it. It tries about 2.5 m in front of the owner,
        /// then turned either way and nearer or further, and takes the first spot reached by a straight line that never
        /// leaves the nav mesh, never climbs more than <see cref="MaxStep"/> at once, and only crosses from one polygon
        /// to another over an edge they share (<see cref="NavMesh.OpenBetween"/>): never through a wall, and never into
        /// another indoor region (another prefab: a room behind a door, an elevator car). Failing that, 3 m along +x
        /// (where the bot used to go) if that is in the owner's own region; failing that, where the owner stands.
        /// Where the district has no nav mesh, 2.5 m ahead as it is. Returns where the spot is on the mesh, if it is.
        /// The yaw is the owner's YawInterval, in the convention of <see cref="MobAggro.YawTowards"/> (0 is +z, a
        /// quarter turn is 64 towards +x).
        /// </summary>
        public static bool Spot(uint district, double x, double y, double z, byte yaw, out double bx, out double by, out double bz,
            out NavLocation at)
        {
            UInt16 metr = (UInt16)district;
            if (!NavMesh.Locate(metr, x, y, z, out NavLocation here))
            {
                double ahead = yaw * 2 * Math.PI / 256;
                bx = x + Distances[0] * Math.Sin(ahead); by = y; bz = z + Distances[0] * Math.Cos(ahead);
                at = new NavLocation();
                return false;
            }
            foreach (double distance in Distances)
            {
                foreach (int turn in Turns)
                {
                    double angle = (yaw + turn) * 2 * Math.PI / 256;
                    double tx = x + distance * Math.Sin(angle), tz = z + distance * Math.Cos(angle);
                    if (WalkableLine(metr, here, x, z, tx, tz, out at))
                    {
                        bx = tx; by = at.floorY + 95; bz = tz;
                        return true;
                    }
                }
            }
            if (NavMesh.Locate(metr, x + 300, y, z, out at) && at.sector == here.sector && at.region == here.region && at.indoors == here.indoors)
            {
                bx = x + 300; by = at.floorY + 95; bz = z;
                return true;
            }
            bx = x; by = y; bz = z;
            at = here;
            return true;
        }

        /// <summary>The line from the owner to the spot, a <see cref="LineStep"/> at a time: see <see cref="Spot"/>.</summary>
        private static bool WalkableLine(UInt16 district, NavLocation from, double x, double z, double tx, double tz, out NavLocation end)
        {
            end = from;
            int steps = (int)Math.Ceiling(Math.Sqrt((tx - x) * (tx - x) + (tz - z) * (tz - z)) / LineStep);
            for (int i = 1; i <= steps; i++)
            {
                double px = x + (tx - x) * i / steps, pz = z + (tz - z) * i / steps;
                if (!NavMesh.Locate(district, px, end.floorY + 95, pz, out NavLocation step) || Math.Abs(step.floorY - end.floorY) > MaxStep ||
                    !NavMesh.OpenBetween(district, end, step))
                {
                    return false;
                }
                end = step;
            }
            return true;
        }

        private static void ChallengeOwner(WorldClient bot, WorldClient owner)
        {
            UInt32 ownerForBot = PvpCombat.HandleOf(bot, owner);
            if (Find(bot) == null || ownerForBot == 0)
            {
                return;
            }
            byte[] request = BitConverter.GetBytes(ownerForBot);
            DuelHandler.ProcessChallenge(bot, request);
        }

        /// <summary>Takes the owner's bot out of the world. Returns false when they have none.</summary>
        public static bool Dismiss(WorldClient owner)
        {
            Bot bot;
            lock (Sync)
            {
                bot = Bots.FirstOrDefault(b => b.Owner == owner);
                if (bot == null)
                {
                    return false;
                }
                Bots.Remove(bot);
            }
            Remove(bot.Client);
            return true;
        }

        private static void Remove(WorldClient bot)
        {
            // A duel with it ends first (DuelID 0 and 56 for the other side, sent now, in a packet of their own); then
            // the other side's interlock, if one is still up; then the bot's view
            PvpCombat.OnPlayerGone(bot);
            foreach (WorldClient other in SocialHandler.OnlineWhere(c => c != bot && !c.IsBot))
            {
                other.FlushQueue(false, true);
            }
            PvpFight.OnPlayerGone(bot, true);
            bot.playerData.setOnWorld(false);
            string key = null;
            List<WorldClient> others;
            lock (WorldServer.Clients)
            {
                foreach (KeyValuePair<string, WorldClient> entry in WorldServer.Clients)
                {
                    if (entry.Value == bot)
                    {
                        key = entry.Key;
                    }
                }
                if (key != null)
                {
                    WorldServer.Clients.Remove(key);
                }
                others = WorldServer.Clients.Values.Where(c => c != null && !c.IsBot).ToList();
            }
            foreach (WorldClient other in others)
            {
                ClientView view = other.viewMan.FindViewForEntity(bot.playerData.getEntityId());
                if (view != null)
                {
                    if (view.viewCreated)
                    {
                        new ServerPackets().sendDeleteViewPacket(other, view.ViewID);
                    }
                    other.viewMan.removeViewByViewId(view.ViewID);
                }
            }
            CombatManager.RemoveClient(bot);
            if (key != null)
            {
                lock (WorldServer.Clients)
                {
                    Store.world.objMan.PopClient(key);
                }
            }
            Output.WriteDebugLog("[DUELBOT] Removed");
        }

        /// <summary>A bot whose owner is gone leaves too.</summary>
        private static void Watch()
        {
            try
            {
                List<Bot> orphans;
                lock (Sync)
                {
                    orphans = Bots.Where(b => !b.Owner.Alive || !b.Owner.playerData.getOnWorld() ||
                                              b.Owner.playerData.getDistrictId() != b.Client.playerData.getDistrictId()).ToList();
                    foreach (Bot b in orphans)
                    {
                        Bots.Remove(b);
                    }
                }
                foreach (Bot b in orphans)
                {
                    Remove(b.Client);
                }
            }
            catch (Exception ex)
            {
                Output.WriteLine("[DUELBOT] Watch failed: " + ex);
            }
        }

        /// <summary>Someone challenged a bot: it says yes a second later (no on a bot that isn't there to duel).</summary>
        public static void OnChallenged(WorldClient target, WorldClient challenger)
        {
            Bot bot = Find(target);
            if (bot == null)
            {
                return;
            }
            DelayedActions.After(1.0, () => DuelHandler.AnswerFor(target, bot.Mode != Mode.Pvp));
        }

        /// <summary>A duel began with <paramref name="one"/> in it: a bot makes its move after a few seconds.</summary>
        public static void OnDuelBegan(WorldClient one, WorldClient opponent)
        {
            Bot bot = Find(one);
            if (bot == null || bot.Mode == Mode.Wait)
            {
                return;
            }
            DelayedActions.After(AttackDelaySeconds, () =>
            {
                if (Find(one) == null || !DuelHandler.AreDuelling(one, opponent))
                {
                    return;
                }
                UInt32 handle = PvpCombat.HandleOf(one, opponent);
                if (handle == 0)
                {
                    Output.WriteDebugLog("[DUELBOT] No view of the opponent yet");
                    return;
                }
                if (bot.Mode == Mode.Gun)
                {
                    PvpCombat.TryRangedCombat(one, handle);
                }
                else
                {
                    // The client's own request: 40 <target u32> <ForcedInterlock> <WeaponToUseSlot: 6b fists>
                    byte[] request = { (byte)handle, (byte)(handle >> 8), (byte)(handle >> 16), 0x00, 0x01, 0x6b };
                    PvpCombat.TryCloseCombat(one, request, handle);
                }
            });
        }

        /// <summary>A bot that was killed for real is back ten seconds later, where it fell.</summary>
        public static void OnKilled(WorldClient one)
        {
            if (Find(one) == null)
            {
                return;
            }
            DelayedActions.After(10.0, () =>
            {
                if (Find(one) == null)
                {
                    return;
                }
                UInt16 maxHealth = NumericalUtils.ByteArrayToUint16(one.playerInstance.MaxHealth.getValue(), 1);
                one.playerData.isDead = false;
                one.playerInstance.IsDead.setValue(false);
                one.playerInstance.Health.setValue(maxHealth);
                PvpFlag.ToWatchers(one, PlayerViewAttributes.Build(
                    (PlayerViewAttributes.OtherIsDead, PlayerViewAttributes.U8(0)),
                    (PlayerViewAttributes.OtherHealth, PlayerViewAttributes.U16(maxHealth))));
            });
        }

        /// <summary>After a duel a bot gets back on its feet with full health, ready for the next one.</summary>
        public static void OnDuelOver(WorldClient one)
        {
            if (Find(one) == null)
            {
                return;
            }
            DelayedActions.After(5.0, () =>
            {
                if (Find(one) == null)
                {
                    return;
                }
                DuelHandler.StandUp(one);
                UInt16 maxHealth = NumericalUtils.ByteArrayToUint16(one.playerInstance.MaxHealth.getValue(), 1);
                one.playerInstance.Health.setValue(maxHealth);
                PvpFlag.ToWatchers(one, PlayerViewAttributes.Build((PlayerViewAttributes.OtherHealth, PlayerViewAttributes.U16(maxHealth))));
            });
        }
    }
}
