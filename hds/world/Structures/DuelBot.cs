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
            me.Position.setValue(NumericalUtils.doublesToLtVector3d(x + 300, y, z));
            me.YawInterval.setValue(them.YawInterval.getValue()[0]);
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
            Output.WriteDebugLog("[DUELBOT] Summoned for " + PvpFlag.Name(owner) + " (" + mode + ")");

            if (mode == Mode.Challenge)
            {
                // Once the owner's client has the bot's view (the visibility pass spawns it within a second)
                DelayedActions.After(AttackDelaySeconds, () => ChallengeOwner(bot, owner));
            }
            return bot;
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
            PvpCombat.OnPlayerGone(bot);
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
