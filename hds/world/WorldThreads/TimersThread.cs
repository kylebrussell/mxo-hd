using System;
using System.Collections.Generic;
using System.Threading;

namespace hds
{
    public partial class WorldThreads
    {
        public void TimersThreadProcess()
        {
            Output.WriteLine("[WORLD SERVER]TimersThread started");
            while (true)
            {
                // ToDo: This should update "timers" like Buffs, Skill Execution or something
                Thread.Sleep(2000);
                List<WorldClient> clients = SnapshotClientsForTimers();
                CheckAndResendClients(clients);
                SavePlayers(clients);
            }
        }
        
        private static List<WorldClient> SnapshotClientsForTimers()
        {
            lock (WorldServer.Clients)
            {
                return new List<WorldClient>(WorldServer.Clients.Values);
            }
        }

        private static void CheckAndResendClients(List<WorldClient> clients)
        {
            foreach (WorldClient client in clients)
            {
                if (client != null)
                {
                    client.CheckAndResend();
                }
            }
        }

        private static void SavePlayers(List<WorldClient> clients)
        {
            foreach (WorldClient thisclient in clients)
            {
                if (thisclient == null)
                {
                    continue;
                }

                // An exception here used to end the timer thread and with it the whole server (e.g. a failed save).
                try
                {
                    if (thisclient.playerData.lastSaveTime == 0)
                    {
                        thisclient.playerData.lastSaveTime = TimeUtils.getUnixTimeUint32();
                    }

                    CheckToSaveClientData(thisclient);
                    RegenerateInnerStrength(thisclient);
                    HealthRegen.Tick(thisclient, 2.0); // this loop runs every 2 s
                    CheckForFinalJackout(thisclient);
                    CheckForStaticViews(thisclient);
                }
                catch (Exception ex)
                {
                    Output.WriteLine("[TIMERS] Client update failed for charId " + thisclient.playerData.getCharID() + ": " + ex.Message);
                }
            }
        }

        private static void RegenerateInnerStrength(WorldClient thisclient)
        {
            if (thisclient == null || !thisclient.playerData.getOnWorld())
            {
                return;
            }

            UInt16? innerStrength = AbilityHandler.RegenerateInnerStrength(thisclient);
            if (innerStrength.HasValue)
            {
                new ServerPackets().sendISCurrent(thisclient, innerStrength.Value);
            }
        }

        private static void CheckForFinalJackout(WorldClient thisclient)
        {
            // Retail answered the jack-out request (80 fc 02) with 80 fd 00 00 00 00 00 00 30 s later in all 32
            // captured jack-outs (e.g. crashover2k proxy_027_just_login_and_spawn_on_rogers_way_ctrl_And_jackout);
            // the client then sends 80 fe 00. The old check subtracted the other way round, so the unsigned
            // difference wrapped and the finish went out after about a second, then again on every tick.
            if (thisclient.playerData.isJackoutInProgress &&
                TeleportHandler.JackoutFinished(thisclient.playerData.jackoutStartTime, TimeUtils.getUnixTimeUint32()))
            {
                thisclient.playerData.isJackoutInProgress = false;
                ServerPackets packets = new ServerPackets();
                packets.sendExitGame(thisclient);
            }
        }

        private static void CheckToSaveClientData(WorldClient thisclient)
        {
            if (thisclient != null && (TimeUtils.getUnixTimeUint32() - thisclient.playerData.lastSaveTime) > 20 && thisclient.playerData.getOnWorld())
            {
                thisclient.playerData.lastSaveTime = TimeUtils.getUnixTimeUint32();
                // Save Player
                new PlayerHelper().SavePlayerInfo(thisclient);
                // Notify Player about save
                ServerPackets pak = new ServerPackets();
                pak.sendSaveCharDataMessage(thisclient,
                    StringUtils.charBytesToString_NZ(thisclient.playerInstance.CharacterName.getValue()));
            }
        }

        private static void CheckForStaticViews(WorldClient thisclient)
        {
            //List<StaticWorldObject> staticWorldObjects = DataLoader.getInstance().findObjectsInSectorAndWorld(thisclient.playerData.getDistrictId(), 1);
        }
    }
}
