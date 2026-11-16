using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace hds
{
    public partial class MissionProgress
    {
        // Versioned storage DTO: definitions and live network/entity references are deliberately not serialized.
        public sealed class SavedState
        {
            public int Version { get; set; } = 1;
            public string Identity { get; set; }
            public int Mission { get; set; }
            public ushort Instance { get; set; }
            public long Started { get; set; }
            public int Phase { get; set; }
            public int Kills { get; set; }
            [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public StaticWorldObject DepartedSite { get; set; }
            [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public int? DepartedPhase { get; set; }
            public Dictionary<string, bool> Flags { get; set; } = new Dictionary<string, bool>();
            public int[] Done { get; set; } = Array.Empty<int>();
            public int[] EverDone { get; set; } = Array.Empty<int>();
            public int[] Fired { get; set; } = Array.Empty<int>();
            public ushort[] Arrived { get; set; } = Array.Empty<ushort>();
            public ushort[] Killed { get; set; } = Array.Empty<ushort>();
            public ushort[] Following { get; set; } = Array.Empty<ushort>();
            public int[] Waves { get; set; } = Array.Empty<int>();
            public ushort[] PendingNpcs { get; set; } = Array.Empty<ushort>();
            public int[] PendingWaves { get; set; } = Array.Empty<int>();
            public ushort[] JustArrived { get; set; } = Array.Empty<ushort>();
            public int[] WavesJustArrived { get; set; } = Array.Empty<int>();
            public ushort[] JustSwitched { get; set; } = Array.Empty<ushort>();
            public Dictionary<ushort, bool> Sides { get; set; } = new Dictionary<ushort, bool>();
            public Dictionary<uint, ushort> Items { get; set; } = new Dictionary<uint, ushort>();
            public Dictionary<ushort, double[]> Reserved { get; set; } = new Dictionary<ushort, double[]>();
            public Dictionary<int, List<double[]>> ReservedWaves { get; set; } = new Dictionary<int, List<double[]>>();
            // Elapsed time instead of process Stopwatch values. Offline time does not advance mission timers (OURS).
            public Dictionary<ushort, double> NpcElapsed { get; set; } = new Dictionary<ushort, double>();
            public Dictionary<int, double> WaveElapsed { get; set; } = new Dictionary<int, double>();
            public Dictionary<int, double> GoalElapsed { get; set; } = new Dictionary<int, double>();
            public MissionWaypoint[] Waypoints { get; set; } = Array.Empty<MissionWaypoint>();
            public double[][] Exits { get; set; } = Array.Empty<double[]>();
            public StaticWorldObject[] SiteDoors { get; set; } = Array.Empty<StaticWorldObject>();
            public List<SavedObject> Objects { get; set; } = new List<SavedObject>();
            [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public MissionOpenFloorPendingAdmission PendingOpenFloor { get; set; }
            [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public int? OpenFloorCurrentSiteIndex { get; set; }
            [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public int? OpenFloorPhase { get; set; }
            [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public int? OpenFloorLegacyPhase { get; set; }
            [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public MissionOpenFloorSite[] OpenFloorSites { get; set; }
            [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public MissionElevatorWitness[] ElevatorWitnesses {get;set;}
            // Parse only this recognized optional subtree after restoring identity/items. Invalid NPC metadata
            // can then quarantine the exact original checkpoint without a broad SavedState JSON catch.
            [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingDefault)] public JsonElement NpcInterior {get;set;}
            [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingDefault)] public JsonElement StreetAccess {get;set;}
            public List<SavedMob> Mobs { get; set; } = new List<SavedMob>();
        }
        public sealed class SavedObject
        {
            public ushort Id { get; set; }
            public StaticWorldObject Spot { get; set; }
            public ushort Goid { get; set; }
            public bool Unlocked { get; set; }
            public uint State { get; set; }
            [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public MissionOpenFloorPlacement OpenFloor { get; set; }
        }
        public sealed class SavedLoot
        {
            public uint Info {get;set;}
            public uint MissionItem {get;set;}
            public uint[] Items {get;set;}=Array.Empty<uint>();
        }
        public sealed class SavedMob
        {
            public string Identity {get;set;}
            public uint RewardExperience {get;set;}
            public bool Lootable {get;set;}
            public SavedLoot Loot {get;set;}
            public ushort Npc { get; set; }
            public byte Phase { get; set; }
            public string Name { get; set; }
            public string Rsi { get; set; }
            public ushort Level { get; set; }
            public ushort Health { get; set; }
            public ushort MaxHealth { get; set; }
            public uint Weapon { get; set; }
            public uint Rotation { get; set; }
            public double X { get; set; }
            public double Y { get; set; }
            public double Z { get; set; }
            public bool Dead { get; set; }
            public bool ScriptedCorpse { get; set; }
            public MobAggroKind Aggro { get; set; }
            public bool Friendly { get; set; }
            public bool Hostile { get; set; }
            public bool Giver { get; set; }
            public bool Taker { get; set; }
            public int? Wave { get; set; }
        }
        internal static readonly JsonSerializerOptions SaveJson = new JsonSerializerOptions() { IncludeFields = true };
        public SavedState Capture(ushort instance, IEnumerable<Mob> mobs, NpcLootCache loot = null) => CaptureNpcCore(instance,mobs,loot,null);
        internal SavedState CaptureWithNpcCatalog(ushort instance,IEnumerable<Mob> mobs,IReadOnlyList<MissionSite> catalog,NpcLootCache loot=null) => CaptureNpcCore(instance,mobs,loot,catalog);
        private SavedState CaptureNpcCore(ushort instance,IEnumerable<Mob> mobs,NpcLootCache loot,IReadOnlyList<MissionSite> npcCatalog)
        {
            double now = clock();
            var actors=mobs.ToArray();
            var state = new SavedState { Identity=persistenceId, Mission=mission.id, Instance=instance, Started=startedAt, Phase=phaseIndex, Kills=kills,
                Done=done.ToArray(), EverDone=everDone.ToArray(), Fired=Phase?.triggers.Select((t,i)=>(t,i)).Where(x=>fired.Contains(x.t)).Select(x=>x.i).ToArray() ?? Array.Empty<int>(),
                Arrived=arrived.ToArray(), Killed=killed.ToArray(), Following=following.ToArray(), Waves=wavesArrived.ToArray(),
                PendingNpcs=pendingNpcPlacements.ToArray(), PendingWaves=pendingWavePlacements.ToArray(), JustArrived=justArrived.Select(n=>n.id).ToArray(),
                WavesJustArrived=wavesJustArrived.Select(w=>w.id).ToArray(), JustSwitched=justSwitched.ToArray(),
                Sides=new Dictionary<ushort, bool>(sides), Items=new Dictionary<uint, ushort>(itemSlots), Reserved=new Dictionary<ushort, double[]>(reservedSpots), ReservedWaves=new Dictionary<int, List<double[]>>(reservedWaveSpots),
                NpcElapsed=npcTriggered.ToDictionary(p=>p.Key,p=>Math.Max(0,now-p.Value)), WaveElapsed=waveTriggered.ToDictionary(p=>p.Key,p=>Math.Max(0,now-p.Value)),
                DepartedSite=HasGeneratedDeparture ? departedSite : null, DepartedPhase=HasGeneratedDeparture ? phaseIndex : (int?)null,
                GoalElapsed=goalClock.ToDictionary(p=>p.Key,p=>Math.Max(0,now-p.Value)), Waypoints=waypoints.ToArray(), Exits=exits.ToArray(),
                SiteDoors=sites.Select(s=>s.elevatorDoor).ToArray(),
                Objects=objects.Values.Select(o=>new SavedObject { Id=o.def.id, Spot=o.spot, Goid=o.goid, Unlocked=o.unlockedOnce, State=o.shownState, OpenFloor=o.OpenFloor }).ToList() };
            var npcInterior=CaptureNpcInterior();
            var street=MissionStreetCheckpoint.Capture(this);if(street.ValueKind!=JsonValueKind.Undefined)state.StreetAccess=street;
            if(npcInterior!=null)state.NpcInterior=JsonSerializer.SerializeToElement(npcInterior,SaveJson);
            if(openFloorCheckpoint||pendingOpenFloor!=null||objects.Values.Any(o=>o.OpenFloor!=null)||npcInterior!=null||street.ValueKind!=JsonValueKind.Undefined)
            {
                state.Version=2;state.PendingOpenFloor=pendingOpenFloor;state.OpenFloorPhase=openFloorResolvedPhase;state.OpenFloorLegacyPhase=openFloorLegacyPhase;state.OpenFloorSites=sites.Select(MissionOpenFloorSite.From).ToArray();
                state.ElevatorWitnesses=elevatorWitnesses.Count==0?null:elevatorWitnesses.Values.ToArray();
                state.OpenFloorCurrentSiteIndex=site==null?-1:sites.FindIndex(s=>ReferenceEquals(s,site));
                if(site!=null&&state.OpenFloorCurrentSiteIndex<0)throw new InvalidOperationException("Box current site absent from exact history");
            }
            if(npcInterior!=null)MissionNpcInteriorCheckpoint.PrepareRestore(this,state,npcCatalog);
            MissionStreetCheckpoint.PrepareRestore(this,state,npcCatalog,npcInterior);
            state.Flags[nameof(waitingForExit)]= waitingForExit;
            state.Flags[nameof(entered)]= entered;
            state.Flags[nameof(failed)]= failed;
            state.Flags[nameof(inventoryFullNotice)]= inventoryFullNotice;
            state.Flags[nameof(completionNotice)]= completionNotice;
            state.Flags[nameof(entryItemExchanged)]= entryItemExchanged;
            state.Flags[nameof(goalsCompleteSent)]= goalsCompleteSent;
            state.Flags[nameof(leaveNotNeeded)]= leaveNotNeeded;
            state.Mobs=actors.Select(m=>new SavedMob { Identity=m.MissionPersistenceId??(m.MissionPersistenceId=Guid.NewGuid().ToString("N")), RewardExperience=m.MissionRewardExperience, Lootable=m.getIsLootable(), Npc=m.missionNpcId, Phase=m.missionPhase, Name=m.getName(), Rsi=m.getRsiHex(), Level=m.getLevel(),
                Health=m.getHealthC(), MaxHealth=m.getHealthM(), Weapon=m.getWeapon(), Rotation=m.getRotation(), X=m.getXPos(),Y=m.getYPos(),Z=m.getZPos(),
                Dead=m.getIsDead(), ScriptedCorpse=m.ScriptedCorpse, Aggro=m.aggroKind, Friendly=m.isFriendly, Hostile=hostiles.Contains(m), Giver=m.missionGiver, Taker=m.missionTaker,
                Wave=hostileWave.TryGetValue(m,out int wave)?(int?)wave:null }).ToList();
            if(loot!=null)
            {
                lock(loot)
                {
                    for(int i=0;i<actors.Length;i++)
                    {
                        var corpse=loot.Peek(actors[i].getEntityId());
                        if(corpse!=null)state.Mobs[i].Loot=new SavedLoot {Info=corpse.Info,MissionItem=corpse.MissionItem,Items=corpse.Items.ToArray()};
                    }
                }
            }
            return state;
        }
        internal static MissionSite FindSavedSite(IEnumerable<MissionSite> candidates,StaticWorldObject door)
        {
            if(door==null)return null;
            // A static key can have multiple legacy CSV poses. Keep the captured raw door,
            // rather than replacing it with the first alias after a normal relog.
            string signature=MissionOpenFloorSite.SpotFingerprint(door);
            // V1 stores no cluster fingerprint: identical raw-door clusters retain legacy order.
            return candidates.FirstOrDefault(s=>s?.elevatorDoor!=null&&s.elevatorDoor.mxoStaticId==door.mxoStaticId&&
                s.elevatorDoor.sectorID==door.sectorID&&s.elevatorDoor.metrId==door.metrId&&
                MissionOpenFloorSite.SpotFingerprint(s.elevatorDoor)==signature);
        }
        public static MissionProgress Restore(Mission mission, SavedState state, Func<double> clock = null) => RestoreCore(mission,state,clock,null);
        internal static MissionProgress RestoreFromCatalog(Mission mission,SavedState state,IReadOnlyList<MissionSite> siteCatalog,Func<double> clock=null) => RestoreCore(mission,state,clock,siteCatalog);
        private static MissionProgress RestoreCore(Mission mission,SavedState state,Func<double> clock,IReadOnlyList<MissionSite> siteCatalog)
            => RestoreNpcCore(mission,state,clock,siteCatalog,null);
        internal static MissionProgress RestoreWithNpcCatalog(Mission mission,SavedState state,IReadOnlyList<MissionSite> siteCatalog,IMissionNpcInteriorEvidenceProvider provider,Func<double> clock=null)
            => RestoreNpcCore(mission,state,clock,siteCatalog,provider);
        internal static MissionProgress RestoreWithStreetCatalog(Mission mission,SavedState state,IReadOnlyList<MissionSite> catalog,MissionPublicDoors.Dataset dataset,Func<uint,StaticWorldObject> lookup)
            =>RestoreNpcCore(mission,state,null,catalog,null,dataset,lookup);
        private static MissionProgress RestoreNpcCore(Mission mission,SavedState state,Func<double> clock,IReadOnlyList<MissionSite> siteCatalog,IMissionNpcInteriorEvidenceProvider npcProvider,MissionPublicDoors.Dataset streetDataset=null,Func<uint,StaticWorldObject> streetLookup=null)
        {
            if ((state.Version!=1&&state.Version!=2) || state.Mission!=mission.id || state.Phase<0 || state.Phase>=mission.phases.Count)
                throw new InvalidOperationException("Unsupported or invalid saved mission phase");
            if(state.Version==1&&!MissionStreetCheckpoint.Present(state)&&(state.ElevatorWitnesses!=null||state.PendingOpenFloor!=null||state.OpenFloorSites!=null||state.OpenFloorPhase.HasValue||state.OpenFloorLegacyPhase.HasValue||state.OpenFloorCurrentSiteIndex.HasValue||state.Objects.Any(o=>o.OpenFloor!=null)))
                throw new InvalidOperationException("Version1 cannot contain resolved open-floor metadata");
            if(state.Version==2&&state.NpcInterior.ValueKind==JsonValueKind.Undefined&&!MissionStreetCheckpoint.Present(state)&&(state.OpenFloorSites==null||!state.OpenFloorCurrentSiteIndex.HasValue||state.OpenFloorCurrentSiteIndex.Value < -1||state.OpenFloorCurrentSiteIndex.Value>=state.OpenFloorSites.Length||string.IsNullOrWhiteSpace(state.Identity)))throw new InvalidOperationException("Version2 requires exact mission site history");
            var p=new MissionProgress(mission) { streetPolicy=false,persistenceId=state.Identity, phaseIndex=state.Phase, startedAt=state.Started, kills=state.Kills };
            if(clock!=null)p.clock=clock;
            double now=p.clock();
            p.done.UnionWith(state.Done); p.everDone.UnionWith(state.EverDone); p.arrived.UnionWith(state.Arrived); p.killed.UnionWith(state.Killed);
            p.following.UnionWith(state.Following); p.wavesArrived.UnionWith(state.Waves); p.pendingNpcPlacements.UnionWith(state.PendingNpcs);p.pendingWavePlacements.UnionWith(state.PendingWaves);
            foreach(int i in state.Fired) if(i>=0&&i<p.Phase.triggers.Count)p.fired.Add(p.Phase.triggers[i]);
            p.waitingForExit=state.Flags.TryGetValue(nameof(waitingForExit),out bool waitingForExitValue)&&waitingForExitValue;
            p.entered=state.Flags.TryGetValue(nameof(entered),out bool enteredValue)&&enteredValue;
            p.failed=state.Flags.TryGetValue(nameof(failed),out bool failedValue)&&failedValue;
            p.inventoryFullNotice=state.Flags.TryGetValue(nameof(inventoryFullNotice),out bool inventoryFullNoticeValue)&&inventoryFullNoticeValue;
            p.completionNotice=state.Flags.TryGetValue(nameof(completionNotice),out bool completionNoticeValue)&&completionNoticeValue;
            p.entryItemExchanged=state.Flags.TryGetValue(nameof(entryItemExchanged),out bool entryItemExchangedValue)&&entryItemExchangedValue;
            p.goalsCompleteSent=state.Flags.TryGetValue(nameof(goalsCompleteSent),out bool goalsCompleteSentValue)&&goalsCompleteSentValue;
            p.leaveNotNeeded=state.Flags.TryGetValue(nameof(leaveNotNeeded),out bool leaveNotNeededValue)&&leaveNotNeededValue;
            foreach(var x in state.Sides)p.sides[x.Key]=x.Value;
            foreach(var x in state.Items)p.itemSlots[x.Key]=x.Value;
            foreach(var x in state.Reserved)p.reservedSpots[x.Key]=x.Value;
            foreach(var x in state.ReservedWaves)p.reservedWaveSpots[x.Key]=x.Value;
            foreach(var x in state.NpcElapsed)p.npcTriggered[x.Key]=now-x.Value;
            foreach(var x in state.WaveElapsed)p.waveTriggered[x.Key]=now-x.Value;
            foreach(var x in state.GoalElapsed)p.goalClock[x.Key]=now-x.Value;
            p.justArrived.AddRange(mission.npcs.Where(n=>state.JustArrived.Contains(n.id)));
            p.wavesJustArrived.AddRange(p.Phase.Waves().Where(w=>state.WavesJustArrived.Contains(w.id)));p.justSwitched.AddRange(state.JustSwitched);
            p.waypoints=state.Waypoints.ToList();p.exits.AddRange(state.Exits);
            var npcInterior=state.NpcInterior.ValueKind!=JsonValueKind.Undefined?MissionNpcInteriorCheckpoint.PrepareRestore(p,state,siteCatalog):null;
            var street=MissionStreetCheckpoint.PrepareRestore(p,state,siteCatalog,npcInterior);
            MissionSite legacyCurrent=null;
            if(state.Version==1)
                foreach(var door in state.SiteDoors)
                {
                    legacyCurrent=FindSavedSite(siteCatalog??MissionSites.For(mission.district),door);
                    if(legacyCurrent!=null)p.sites.Add(legacyCurrent);
                }
            else
            {
                p.openFloorCheckpoint=true;p.pendingOpenFloor=state.PendingOpenFloor;p.openFloorResolvedPhase=state.OpenFloorPhase;p.openFloorLegacyPhase=state.OpenFloorLegacyPhase;
                foreach(var saved in state.OpenFloorSites)
                {
                    var site=saved?.Resolve(siteCatalog??MissionSites.For(mission.district));
                    if(site==null)throw new InvalidOperationException("Missing or ambiguous exact open-floor site history");
                    p.sites.Add(site);
                }
            }
            p.site=state.Version==1?legacyCurrent:state.OpenFloorCurrentSiteIndex.Value==-1?null:p.sites[state.OpenFloorCurrentSiteIndex.Value];
            // Missing current raw identity is quarantined before adopting any NPC interior witness.
            if(state.Version==1&&state.SiteDoors.Length>0&&legacyCurrent==null)
                throw new MissionPlacementRestoreException(p,"MissingExactLegacyMissionSite");
            MissionNpcInteriorCheckpoint.Apply(p,npcInterior);
            MissionStreetCheckpoint.Apply(p,street);
            if(p.UsesGeneratedDeparture && state.DepartedPhase==p.phaseIndex && SameDepartureDoor(state.DepartedSite,p.site.elevatorDoor))
            {p.departedSite=p.site.elevatorDoor;p.departedPhase=p.phaseIndex;}
            foreach(var o in state.Objects)
            {
                var def=p.Phase.objects.Find(d=>d.id==o.Id);
                if(state.Version==2&&(def==null||p.objects.ContainsKey(o.Id)))throw new InvalidOperationException("Invalid open-floor checkpoint object identity");
                if(def!=null)p.objects[o.Id]=new MissionObjectState {def=def,spot=o.Spot,goid=o.Goid,unlockedOnce=o.Unlocked,shownState=o.State,OpenFloor=o.OpenFloor};
            }
            if(state.ElevatorWitnesses!=null)
            {
                foreach(var witness in state.ElevatorWitnesses)
                {
                    var matching=p.sites.Where(s=>witness!=null&&MissionOpenFloorSite.CatalogFingerprint(s)==witness.SiteFingerprint).ToArray();
                    if(matching.Length!=1||witness.Version!=1||witness.Sources==null||witness.Sources.Length==0||witness.Sources.Any(string.IsNullOrWhiteSpace)||
                        witness.Floor?.Length!=3||witness.Lobby?.Length!=3||witness.Floor.Concat(witness.Lobby).Any(v=>!double.IsFinite(v))||
                        witness.FloorKey!=matching[0].elevatorDoor.mxoStaticId||witness.LobbyKey!=matching[0].lobbyDoor.mxoStaticId||p.elevatorWitnesses.ContainsKey(witness.SiteFingerprint))
                        throw new InvalidOperationException("Invalid persisted mission elevator identity");
                    try
                    {
                        var pool=matching[0].objectSpots.Concat(matching[0].containers).Concat(matching[0].realObjects).Select(MissionOpenFloorSite.SpotFingerprint).ToArray();
                        var choices=witness.Sources.Select((raw,i)=>new MissionOpenFloorChoice((ushort)i,0,raw).ReadSpot()).ToArray();
                        if(choices.Any(raw=>!MissionOpenFloorAdmission.ValidSpot(raw)||pool.Count(f=>f==MissionOpenFloorSite.SpotFingerprint(raw))!=1)||
                            choices.Select(MissionOpenFloorSite.SpotFingerprint).Distinct().Count()!=choices.Length)
                            throw new InvalidOperationException("Invalid persisted mission elevator source identity");
                    }
                    catch(JsonException ex){throw new InvalidOperationException("Invalid persisted mission elevator source",ex);}
                    p.elevatorWitnesses.Add(witness.SiteFingerprint,witness);
                }
            }
            // 357 had no audited free-placement path before admission support. Never silently
            // bless or reroll an old free357 pose/items on reconnect, whether entered or not.
            if(state.Version==1&&p.objects.Values.Any(o=>o.goid==357&&MissionOpenFloorAdmission.Target(o.goid,o.spot)))
                throw new MissionPlacementRestoreException(p,"Legacy free Mainframe357 lacks an adopted witness");
            MissionBoundObjectPoses.ValidateRestored(p);
            if(state.Version==2)MissionOpenFloorAdmission.ValidateRestored(p,siteCatalog);
            MissionNpcInteriorCheckpoint.ValidateGeometry(p,npcInterior,npcProvider??MissionNpcInteriorEvidenceProvider.Instance);
            if(street!=null)MissionStreetCheckpoint.ValidateGeometry(p,street,streetDataset??MissionPublicDoors.CurrentDataset(),streetLookup??(key=>DataLoader.getInstance().getObjectValues(key,mission.district)),siteCatalog);
            return p;
        }
    }
}
