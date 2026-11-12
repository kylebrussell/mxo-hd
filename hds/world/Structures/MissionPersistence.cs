using System;
using System.Linq;
using System.Text.Json;
using hds.shared;

namespace hds
{
    public static class MissionPersistence
    {
        public static bool DevAllowed(string dev,string database,uint character,bool live,bool onWorld,bool dead,string handle) =>
            QaLoadout.Allowed(dev,database,character,live&&onWorld,dead) && !string.Equals(handle,"TheNeo",StringComparison.OrdinalIgnoreCase);
        public static bool Dev(WorldClient client,string[] words)
        {
            if(words.Length!=2||!DevAllowed(Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"),Store.config?.dbParams?.DatabaseName,
                client.playerData.getCharID(),client.Alive,client.playerData.getOnWorld(),client.playerData.isDead,
                StringUtils.charBytesToString_NZ(client.playerInstance.CharacterName.getValue())))return false;
            if(words[1]=="start")
            {
                if(client.playerData.activeMission!=null)return false;
                var mission=DataLoader.getInstance().missions.Find(m=>m.id==2202);
                if(mission==null||client.playerData.getDistrictId()!=mission.district)return false;
                return MissionHandler.Accept(client,mission,0);
            }
            if(words[1]!="status")return false;
            lock(client.playerData.missionSync)
            {
                Save(client);var progress=client.playerData.missionProgress;
                new ServerPackets().sendSystemChatMessage(client,progress==null?"Mission persistence: none":
                    "Mission persistence: "+progress.mission.id+" phase "+(progress.phaseIndex+1)+" owned items "+progress.itemSlots.Count+" NPCs "+client.playerData.missionMobs.Count,"BROADCAST");
                return true;
            }
        }
        private static bool Available(WorldClient client) => client?.playerData!=null && Store.dbManager?.WorldDbHandler!=null;
        internal static void LoadCheckpoint(WorldClient client)
        {
            if(!Available(client)||client.playerData.missionCheckpointLoaded)return;
            client.playerData.missionCheckpoint=Store.dbManager.WorldDbHandler.LoadActiveMission(client.playerData.getCharID());
            client.playerData.missionCheckpointLoaded=true;
        }
        public static void Save(WorldClient client, bool clear = false)
        {
            if(!Available(client))return;
            lock(client.playerData.missionSync)
            {
                var data=client.playerData;
                if(data.missionProgress==null&&!clear)return;
                LoadCheckpoint(client);
                string next=null;
                if(data.missionProgress!=null)
                {
                    lock(data.missionMobs) next=JsonSerializer.Serialize(data.missionProgress.Capture(data.missionInstanceCounter,data.missionMobs),MissionProgress.SaveJson);
                }
                if(next==data.missionCheckpoint)return;
                Store.dbManager.WorldDbHandler.CommitActiveMission(data.getCharID(),data.missionCheckpoint,next);
                data.missionCheckpoint=next;
            }
        }
        internal static bool MoveItem(WorldClient client,ushort from,ushort to)
        {
            lock(client.playerData.missionSync)
            {
                var data=client.playerData;var progress=data.missionProgress;
                if(progress==null||!progress.itemSlots.Values.Contains(from))return false;
                var owned=progress.itemSlots.Single(p=>p.Value==from);
                progress.itemSlots[owned.Key]=to;
                try
                {
                    string next;
                    lock(data.missionMobs)next=JsonSerializer.Serialize(progress.Capture(data.missionInstanceCounter,data.missionMobs),MissionProgress.SaveJson);
                    Store.dbManager.WorldDbHandler.CommitActiveMission(data.getCharID(),data.missionCheckpoint,next,from,to);
                    data.missionCheckpoint=next;
                }
                catch {progress.itemSlots[owned.Key]=from;throw;}
                return true;
            }
        }
        // Used on disconnect only: inventory ownership is kept. Abandon/complete still use End.
        public static void Suspend(WorldClient client)
        {
            lock(client.playerData.missionSync)
            {
                Save(client);
                MissionRuntime.Detach(client);
            }
        }
        public static void WorldEntry(WorldClient client)
        {
            if(!Available(client)||client.playerData.DistrictIsLA())return;
            lock(client.playerData.missionSync)
            {
                var data=client.playerData;
                if(data.missionProgress!=null)
                {
                    // World changes rebuild client-local object views, but never reroll placements or replay item grants.
                    MissionRuntime.Reannounce(client);
                    return;
                }
                LoadCheckpoint(client);
                if(data.missionCheckpoint==null)return;
                var saved=JsonSerializer.Deserialize<MissionProgress.SavedState>(data.missionCheckpoint,MissionProgress.SaveJson);
                var mission=DataLoader.getInstance().missions.Find(m=>m.id==saved.Mission);
                if(mission==null)throw new InvalidOperationException("Saved mission definition not found: "+saved.Mission);
                var progress=MissionProgress.Restore(mission,saved);
                data.activeMission=mission;data.missionProgress=progress;data.missionInstanceCounter=saved.Instance;
                foreach(var record in saved.Mobs)
                {
                    var mob=new Mob();mob.setEntityId(WorldServer.entityIdCounter++);mob.setDistrict(mission.district);mob.setDistrictName(mission.districtName);
                    mob.setName(record.Name);mob.setRsiHex(record.Rsi);mob.setLevel(record.Level);mob.setHealthM(record.MaxHealth);mob.setHealthC(record.Health);
                    mob.setWeapon(record.Weapon);mob.setRotation(record.Rotation);mob.setXPos(record.X);mob.setYPos(record.Y);mob.setZPos(record.Z);
                    mob.xBase=record.X;mob.yBase=record.Y;mob.zBase=record.Z;mob.setIsDead(record.Dead);mob.setIsLootable(false);
                    mob.isFriendly=record.Friendly;mob.missionNpcId=record.Npc;mob.missionPhase=record.Phase;mob.missionInstance=saved.Instance;
                    mob.missionGiver=record.Giver;mob.missionTaker=record.Taker;
                    // Only the original definition grants attacks-on-sight. Generated hostile templates use their baseline.
                    var def=mission.npcs.Find(n=>n.id==record.Npc);
                    mob.aggroKind=record.Aggro;
                    mob.ScriptedCorpse=record.ScriptedCorpse || def?.dead==true;
                    mob.setMobId(record.Npc);
                    lock(WorldServer.mobs.SyncRoot)WorldServer.mobs.Add(mob);
                    lock(WorldServer.gameServerEntities.SyncRoot)WorldServer.gameServerEntities.Add(mob);
                    MissionRuntime.AddMob(client,mob);
                    if(record.Hostile)progress.hostiles.Add(mob);
                    if(record.Wave.HasValue)progress.hostileWave[mob]=record.Wave.Value;
                }
                MissionRuntime.Reannounce(client);
                Output.WriteDebugLog("[MISSION] Restored "+mission.id+" phase "+progress.phaseIndex+" instance "+saved.Instance);
            }
        }
    }
}
