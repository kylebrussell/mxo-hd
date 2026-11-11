using System;
using System.Collections.Generic;
using System.Linq;
using hds.shared;
using hds.world.Structures;

namespace hds
{
    /// <summary>
    /// Dev command ?socialbot (MXO_DEV_COMMANDS=1): a player played by the server, standing next to whoever called it,
    /// so one real client can see the other side of the social features: chat rooms, mission teams and crews, paired
    /// emotes and stances. Like ?tradebot and ?duelbot. The bot is a real character as far as the rest of the server is
    /// concerned (<see cref="WorldClient.CreateBot"/>); what would be sent to it is dropped. It accepts every team, crew
    /// and faction invite and every paired emote a second after it gets them. See docs/CLIENT-ACTIONS.md for the steps.
    /// </summary>
    public static class SocialBot
    {
        public const string Name = "SocialBot";

        private sealed class Bot
        {
            public WorldClient Client, Owner;
        }

        private static readonly object Sync = new object();
        private static readonly List<Bot> Bots = new List<Bot>();
        private static int counter;
        private static System.Threading.Timer watchdog;

        public static bool IsBot(WorldClient client)
        {
            lock (Sync)
            {
                return client != null && Bots.Any(b => b.Client == client);
            }
        }

        public static WorldClient BotOf(WorldClient owner)
        {
            lock (Sync)
            {
                return Bots.FirstOrDefault(b => b.Owner == owner)?.Client;
            }
        }

        public static WorldClient OwnerOf(WorldClient bot)
        {
            lock (Sync)
            {
                return Bots.FirstOrDefault(b => b.Client == bot)?.Owner;
            }
        }

        /// <summary>
        /// ?socialbot [off | room &lt;name&gt; [password] | say &lt;room&gt; &lt;text&gt; | leave &lt;room&gt; | ...]: the
        /// sub-commands of each feature. Returns what to tell the player.
        /// </summary>
        public static string Command(WorldClient owner, string[] args)
        {
            string verb = args.Length > 0 ? args[0].ToLowerInvariant() : "";
            if (verb == "off")
            {
                return Dismiss(owner) ? Name + " left." : "You have no " + Name + ".";
            }

            WorldClient bot = BotOf(owner);
            if (verb == "")
            {
                Summon(owner);
                return Name + " is next to you.";
            }

            if (bot == null)
            {
                bot = Summon(owner);
            }

            switch (verb)
            {
                case "room":
                    if (args.Length < 2)
                    {
                        return "?socialbot room <name> [password]";
                    }

                    ChatRoomHandler.Handle(bot, ChatRoomHandler.Enter, RoomRequest(args[1], args.Length > 2 ? args[2] : ""));
                    ChatRoomHandler.TrySendRoomLine(bot, RoomLineRequest(args[1], "Hello from " + Name + "."), "Hello from " + Name + ".");
                    return Name + " tries to enter " + args[1] + ".";
                case "say":
                    if (args.Length < 3)
                    {
                        return "?socialbot say <room> <text>";
                    }

                    string text = string.Join(" ", args.Skip(2));
                    if (ChatRooms.Instance.CanSpeak(Name, args[1]) != ChatRooms.Status.Success)
                    {
                        return Name + " is not in " + args[1] + ".";
                    }

                    ChatRoomHandler.TrySendRoomLine(bot, RoomLineRequest(args[1], text), text);
                    return null;
                case "leave":
                    if (args.Length < 2)
                    {
                        return "?socialbot leave <room>";
                    }

                    ChatRoomHandler.Handle(bot, ChatRoomHandler.Leave, RoomRequest(args[1], null));
                    return null;
                default:
                    return Usage;
            }
        }

        public const string Usage = "?socialbot [off|room <name> [password]|say <room> <text>|leave <room>]";

        /// <summary>The rpcData of 81 c2 (or of 81 c7 with no password): RoomName at +2, Password at +4.</summary>
        private static byte[] RoomRequest(string room, string password)
        {
            MailRecord r = new MailRecord(0xc2);
            int roomField = r.Offset();
            int passwordField = password != null ? r.Offset() : -1;
            r.String(roomField, ChatAddress.Prefix + room);
            if (!string.IsNullOrEmpty(password))
            {
                r.String(passwordField, password);
            }

            return r.ToArray().Skip(2).ToArray();
        }

        /// <summary>The rpcData of 28 18, as /room sends it: +3 Text, +5 RoomName, +7 ChatHandle.</summary>
        private static byte[] RoomLineRequest(string room, string text)
        {
            PacketContent pak = new PacketContent();
            string address = ChatAddress.Prefix + room;
            pak.AddByte(0x18);
            pak.AddByte(0);
            pak.AddUint16(8, 1);
            pak.AddUint16((UInt16) (8 + 2 + text.Length + 1), 1);
            pak.AddByte(0);
            pak.AddSizedTerminatedString(text);
            pak.AddSizedTerminatedString(address);
            return pak.ReturnFinalPacket();
        }

        /// <summary>Puts a bot next to the owner, replacing the owner's previous one.</summary>
        public static WorldClient Summon(WorldClient owner)
        {
            Dismiss(owner);

            int n = System.Threading.Interlocked.Increment(ref counter);
            string key = "bot:social:" + n;
            WorldClient bot = WorldClient.CreateBot(key);
            PlayerCharacter me = bot.playerInstance, them = owner.playerInstance;

            double x = 0, y = 0, z = 0;
            NumericalUtils.LtVector3dToDoubles(them.Position.getValue(), ref x, ref y, ref z);
            UInt16 maxHealth = NumericalUtils.ByteArrayToUint16(them.MaxHealth.getValue(), 1);
            UInt16 maxInner = NumericalUtils.ByteArrayToUint16(them.InnerStrengthMax.getValue(), 1);
            UInt32 charId = 0x7e000000u + (UInt32) n;

            me.CharacterID.setValue(charId);
            me.CharacterName.setValue(Name);
            me.RealFirstName.setValue("Social");
            me.RealLastName.setValue("Bot");
            byte ownerYaw = them.YawInterval.getValue()[0];
            DuelBot.Spot(owner.playerData.getDistrictId(), x, y, z, ownerYaw, out double bx, out double by, out double bz, out NavLocation _);
            me.Position.setValue(NumericalUtils.doublesToLtVector3d(bx, by, bz));
            me.YawInterval.setValue(MobAggro.YawTowards(bx, bz, x, z));
            me.Health.setValue(maxHealth);
            me.MaxHealth.setValue(maxHealth);
            me.InnerStrengthAvailable.setValue(maxInner);
            me.InnerStrengthMax.setValue(maxInner);
            me.Level.setValue(them.Level.getValue()[0]);
            me.TitleAbility.setValue(NumericalUtils.ByteArrayToUint32(them.TitleAbility.getValue(), 1));
            me.OrganizationID.setValue(them.OrganizationID.getValue()[0]);

            ClientData data = bot.playerData;
            data.setCharID(charId);
            data.setRsiValues((int[]) owner.playerData.getRsiValues().Clone());
            data.setDistrict(owner.playerData.getDistrict());
            data.setDistrictId(owner.playerData.getDistrictId());

            lock (WorldServer.Clients)
            {
                data.setEntityId(WorldServer.entityIdCounter++);
                WorldServer.Clients.Add(key, bot);
            }

            data.setOnWorld(true);
            lock (Sync)
            {
                Bots.Add(new Bot {Client = bot, Owner = owner});
                if (watchdog == null)
                {
                    watchdog = new System.Threading.Timer(_ => Watch(), null, 2000, 2000);
                }
            }

            Output.WriteDebugLog("[SOCIALBOT] Summoned for " + SocialHandler.HandleOf(owner) + $" at {bx:0},{by:0},{bz:0}");
            return bot;
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
            // Out of its rooms and its team first, as any player leaving the world
            SocialHandler.PlayerLeftWorld(bot);
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

            Output.WriteDebugLog("[SOCIALBOT] Removed");
        }

        /// <summary>A bot whose owner is gone, or in another district, leaves too.</summary>
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
                Output.WriteLine("[SOCIALBOT] Watch failed: " + ex);
            }
        }
    }
}
