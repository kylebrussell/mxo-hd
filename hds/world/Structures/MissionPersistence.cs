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
            if(client.playerData.worldChangePending||client.playerData.DistrictIsLA()||words.Length!=2||!DevAllowed(Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"),Store.config?.dbParams?.DatabaseName,
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
        [ThreadStatic] private static WorldClient deferredContainerCheckpoint;
        // Container grants and every goal callback commit as one mutation, including a late goal failure.
        internal static T DeferContainerCheckpoint<T>(WorldClient client, Func<T> action)
        {
            var previous = deferredContainerCheckpoint;
            deferredContainerCheckpoint = client;
            try { return action(); }
            finally { deferredContainerCheckpoint = previous; }
        }
        [ThreadStatic] private static WorldClient deferredKill;
        internal static void MutateKill(WorldClient client,Mob mob,Action action)
        {
            Mutate(client,()=>
            {
                var previous=deferredKill;deferredKill=client;
                try {action();mob.MissionDeathSettled=true;}
                finally {deferredKill=previous;}
            });
        }
        internal static T Mutate<T>(WorldClient client,Func<T> action,bool beginMission=false)
        {
            lock(client.playerData.missionSync)
            {
                if(MissionOutput.Buffered(client.messageQueue)||client.playerData.missionProgress==null&&!beginMission)return action();
                Mob[] previousActors;
                lock(client.playerData.missionMobs)previousActors=client.playerData.missionMobs.ToArray();
                using(var output=MissionOutput.Begin(client.messageQueue))
                {
                    try
                    {
                        T result=action();Save(client);
                        output.Commit();client.FlushQueue();return result;
                    }
                    catch(Exception ex)
                    {
                        output.Dispose();
                        // Never keep uncommitted goal/ownership state, or publish its queued goal/item messages.
                        // The cached payload advances only after a successful SQL commit.
                        MissionRuntime.Detach(client);
                        foreach(var mob in previousActors)Store.world?.DeleteMobViews(mob);
                        WorldEntry(client);
                        if(ex is MySqlConnector.MySqlException || ex is MissionCheckpointException)
                        {
                            Output.WriteLine("[MISSION] Checkpoint action rolled back: "+ex.Message);
                            new ServerPackets().sendSystemChatMessage(client,"Mission progress could not be saved. Please retry the action.","BROADCAST");
                            client.FlushQueue();return default(T);
                        }
                        throw;
                    }
                }
            }
        }
        internal static void Mutate(WorldClient client,Action action,bool beginMission=false)=>Mutate(client,()=>{action();return true;},beginMission);
        internal static bool PendingLootOwner(WorldClient client,Mob mob)
        {
            if(client?.playerData==null||mob==null||!mob.getIsDead()||!mob.getIsLootable())return false;
            var data=client.playerData;
            lock(data.missionSync)
            {
                if(data.missionProgress==null||data.activeMission!=data.missionProgress.mission||mob.missionInstance==0||
                    mob.missionInstance!=data.missionInstanceCounter||!data.missionMobs.Contains(mob))return false;
                lock(data.loot)
                {
                    var corpse=data.loot.Peek(mob.getEntityId());
                    return corpse!=null&&(corpse.Info>0||corpse.MissionItem!=0||corpse.Items.Count>0);
                }
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
            if(!Available(client)||deferredKill==client||deferredContainerCheckpoint==client)return;
            lock(client.playerData.missionSync)
            {
                var data=client.playerData;
                if(data.missionProgress==null&&!clear)return;
                // Death presentation precedes its reward callback. Never checkpoint that transient dead/unpaid gap.
                if(data.missionProgress!=null)
                    lock(data.missionMobs)if(data.missionMobs.Any(m=>(m.getIsDead()||m.getHealthC()==0)&&!m.ScriptedCorpse&&!m.MissionDeathSettled))return;
                LoadCheckpoint(client);
                string next=null;
                if(data.missionProgress!=null)
                {
                    lock(data.missionMobs) next=JsonSerializer.Serialize(data.missionProgress.Capture(data.missionInstanceCounter,data.missionMobs,data.loot),MissionProgress.SaveJson);
                }
                if(next==data.missionCheckpoint)return;
                var account=Store.dbManager.WorldDbHandler.CommitActiveMission(data.getCharID(),data.missionCheckpoint,next);
                data.missionCheckpoint=next;
                if(account!=null)
                {
                    data.setExperience(account.ExperienceTotal);data.setInfo(account.InfoTotal);
                    if(account.ExperienceCredited>0)
                    {
                        new ServerPackets().SendEXPGain(client,account.ExperienceCredited);
                        client.messageQueue.addRpcMessage(ServerPackets.BuildEXPTotal((uint)account.ExperienceTotal,ServerPackets.ExpGainFighting));
                        PlayerProgression.ApplyLevelUps(client);
                    }
                }
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
                    lock(data.missionMobs)next=JsonSerializer.Serialize(progress.Capture(data.missionInstanceCounter,data.missionMobs,data.loot),MissionProgress.SaveJson);
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
                    SocialHandler.RestoreMissionTeam(client);
                    MissionRuntime.Reannounce(client);
                    return;
                }
                LoadCheckpoint(client);
                if(data.missionCheckpoint==null)return;
                var saved=JsonSerializer.Deserialize<MissionProgress.SavedState>(data.missionCheckpoint,MissionProgress.SaveJson);
                var mission=MissionDefinitions.Find(saved.Mission);
                if(mission==null)throw new InvalidOperationException("Saved mission definition not found: "+saved.Mission);
                var progress=MissionProgress.Restore(mission,saved);
                data.activeMission=mission;data.missionProgress=progress;data.missionInstanceCounter=saved.Instance;
                foreach(var record in saved.Mobs)
                {
                    var mob=new Mob();mob.setEntityId(WorldServer.entityIdCounter++);mob.setDistrict(mission.district);mob.setDistrictName(mission.districtName);
                    mob.setName(record.Name);mob.setRsiHex(record.Rsi);mob.setLevel(record.Level);mob.setHealthM(record.MaxHealth);mob.setHealthC(record.Health);
                    mob.setWeapon(record.Weapon);mob.setRotation(record.Rotation);mob.setXPos(record.X);mob.setYPos(record.Y);mob.setZPos(record.Z);
                    mob.xBase=record.X;mob.yBase=record.Y;mob.zBase=record.Z;mob.setIsDead(record.Dead);mob.setIsLootable(record.Lootable);
                    mob.MissionPersistenceId=record.Identity;mob.MissionRewardExperience=record.RewardExperience;mob.MissionDeathSettled=record.Dead;
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
                    if(record.Loot!=null)lock(data.loot)data.loot.Add(mob,record.Loot.Info,record.Loot.MissionItem,record.Loot.Items);
                    if(record.Hostile)progress.hostiles.Add(mob);
                    if(record.Wave.HasValue)progress.hostileWave[mob]=record.Wave.Value;
                }
                lock(data.loot)data.loot.Close();
                SocialHandler.RestoreMissionTeam(client);
                MissionRuntime.Reannounce(client);
                Output.WriteDebugLog("[MISSION] Restored "+mission.id+" phase "+progress.phaseIndex+" instance "+saved.Instance);
            }
        }
    }
}
