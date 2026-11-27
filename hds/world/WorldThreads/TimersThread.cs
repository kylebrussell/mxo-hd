using System;
using System.Collections.Generic;
using System.Threading;
using hds.shared;

namespace hds
{
    public partial class WorldThreads
    {
        private const uint SaveIntervalSeconds = 20;

        public void TimersThreadProcess()
        {
            Output.WriteLine("[WORLD SERVER]TimersThread started");
            List<WorldClient> clients = new List<WorldClient>();
            while (true)
            {
                // ToDo: This should update "timers" like Buffs, Skill Execution or something
                Thread.Sleep(2000);
                try
                {
                    SnapshotClientsForTimers(clients);
                    CheckAndResendClients(clients);
                    SavePlayers(clients);
                }
                catch (Exception ex)
                {
                    Output.WriteLine("[TIMERS] Timer pass failed: " + ex);
                }
            }
        }

        private static void SnapshotClientsForTimers(List<WorldClient> destination)
        {
            destination.Clear();
            lock (WorldServer.Clients)
            {
                if (destination.Capacity < WorldServer.Clients.Count)
                {
                    destination.Capacity = WorldServer.Clients.Count;
                }
                foreach (WorldClient client in WorldServer.Clients.Values)
                {
                    destination.Add(client);
                }
            }
        }

        internal static void CheckAndResendClients(List<WorldClient> clients)
        {
            for (int i = 0; i < clients.Count; i++)
            {
                WorldClient client = clients[i];
                if (client == null || client.IsBot)
                {
                    continue;
                }

                // Isolate per-client faults so one bad session never kills TimersThread for the server,
                // and skip idle clients with nothing queued or unacked.
                try
                {
                    if (client.HasPendingMessages())
                    {
                        client.CheckAndResend();
                    }
                }
                catch (Exception ex)
                {
                    uint charId = client.playerData != null ? client.playerData.getCharID() : 0;
                    Output.WriteLine("[TIMERS] CheckAndResend failed for charId " + charId + ": " + ex.Message);
                }
            }
        }

        internal static uint ComputeInitialSaveTime(uint now, uint charId)
        {
            // Deterministically stagger initial autosave timestamps across [now - 19, now] so a
            // simultaneous login wave spreads its 20 s MySQL writes evenly instead of bursting together.
            uint offset = charId % SaveIntervalSeconds;
            return now > offset ? now - offset : 1;
        }

        private static void SavePlayers(List<WorldClient> clients)
        {
            foreach (WorldClient thisclient in clients)
            {
                if (thisclient == null || thisclient.IsBot)
                {
                    continue; // a server-played player has nothing to save or resend
                }

                // An exception here used to end the timer thread and with it the whole server (e.g. a failed save).
                try
                {
                    if (thisclient.playerData.lastSaveTime == 0)
                    {
                        thisclient.playerData.lastSaveTime = ComputeInitialSaveTime(
                            TimeUtils.getUnixTimeUint32(), thisclient.playerData.getCharID());
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

            if (Environment.GetEnvironmentVariable("MXO_SIMULACRUM_QA_HOLD_IS") == "1" &&
                thisclient.playerData.getCharID() == 110 &&
                QaLoadout.Allowed(Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"),
                    Store.config?.dbParams?.DatabaseName, 110, thisclient.playerData.getOnWorld(), thisclient.playerData.isDead))
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
            if (thisclient != null && (TimeUtils.getUnixTimeUint32() - thisclient.playerData.lastSaveTime) > SaveIntervalSeconds && thisclient.playerData.getOnWorld())
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
