using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
namespace hds
{
    internal static class MissionStreetCoverage
    {
        internal const string ContextsSha256="E10CD1DC6B1E57664A8259ED01BE3FF3D876B04247DD07CD7BA40904EFF9177A";
        private sealed class State
        {
            internal readonly MissionStreetContexts Contexts;internal readonly string Refusal;
            internal State(MissionStreetContexts contexts,string refusal){Contexts=contexts;Refusal=refusal;}
        }
        private static State current;
        // Mandatory normal-server preflight before listeners. Geometry-only APIs retain their pre-server behavior.
        internal static void Prepare(DataLoader loader)
        {
            var timer=System.Diagnostics.Stopwatch.StartNew();
            try
            {
                byte[] packed=File.ReadAllBytes(DataLoader.DataPath("mission_street_access","1","contexts.json.gz"));
                if(!Convert.ToHexString(SHA256.HashData(packed)).Equals(ContextsSha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("StreetContextPayloadHash");
                using var stream=new MemoryStream(packed);using var zip=new GZipStream(stream,CompressionMode.Decompress);using var output=new MemoryStream();zip.CopyTo(output);
                var contexts=MissionStreetContexts.Read(output.ToArray(),MissionSites.For(1),MissionNpcDomainCertificates.Inputs(loader.WorldObjectsDB),MissionPublicDoors.IndexIdentity);
                Volatile.Write(ref current,new State(contexts,null));Output.WriteLine("[MISSION STREET] compact public-access contexts ready in "+timer.ElapsedMilliseconds+" ms");
            }
            catch(Exception ex) when(ex is IOException||ex is InvalidDataException||ex is JsonException||ex is ArgumentException||ex is UnauthorizedAccessException)
            {Volatile.Write(ref current,new State(null,"StreetCoverageUnavailable:"+ex.Message));Output.WriteLine("[MISSION STREET] new interior selection refused: "+ex.Message);}
        }
        internal static bool Enroll(Mission mission)=>mission?.district==1&&Volatile.Read(ref current)!=null;
        internal static Func<MissionSite,bool> Filter(Mission mission,int phase)
        {
            string kind=MissionStreetCheckpoint.KindFor(mission,phase);State state=Volatile.Read(ref current);
            if(kind==null||state==null)return null;
            return site=>state.Contexts!=null&&state.Contexts.Allows(site,kind=="Npc");
        }
        internal static bool TryPrepare(MissionProgress p,int phase,MissionSite site,out JsonElement json,out string refusal)
        {
            json=default;refusal=null;string kind=MissionStreetCheckpoint.KindFor(p.mission,phase);State state=Volatile.Read(ref current);
            if(kind==null||state==null||!p.streetPolicy)return true;
            MissionStreetContexts.Context context=null;
            bool accepted=state.Contexts!=null&&state.Contexts.TryGet(site,kind=="Npc",out context,out refusal);
            if(state.Contexts==null)refusal=state.Refusal;
            if(accepted)accepted=MissionStreetContexts.ValidateSelected(context,site,MissionPublicDoors.CurrentDataset(),key=>DataLoader.getInstance().getObjectValues(key,p.mission.district),out refusal);
            var selected=new MissionStreetSelection(1,p.mission.id,p.persistenceId,p.startedAt,phase,MissionOpenFloorSite.From(site),kind,context,accepted?null:refusal??"NoCertifiedStreetContext");
            json=JsonSerializer.SerializeToElement(selected,MissionProgress.SaveJson);return accepted;
        }
        internal static bool Replay(MissionProgress p,JsonElement json,out string refusal)
        {
            refusal=null;if(json.ValueKind==JsonValueKind.Undefined)return true;
            var selected=MissionStreetCheckpoint.Pending(p,json,MissionSites.For(p.mission.district));
            if(selected.Refusal!=null){refusal=selected.Refusal;return false;}
            return MissionStreetContexts.ValidateSelected(selected.Context,selected.Site.Resolve(MissionSites.For(p.mission.district)),MissionPublicDoors.CurrentDataset(),
                key=>DataLoader.getInstance().getObjectValues(key,p.mission.district),out refusal);
        }
        internal static MissionStreetSelection Stage(MissionProgress p,MissionSite site,JsonElement json)
        {
            if(json.ValueKind==JsonValueKind.Undefined)return null;
            var selected=MissionStreetCheckpoint.Staging(p,json,site);
            p.streetWitnesses[p.phaseIndex]=selected;return selected;
        }
        internal static MissionStreetSelection ForSite(MissionProgress p,MissionSite site)
        {
            if(p==null||site==null)return null;string fingerprint=MissionOpenFloorSite.CatalogFingerprint(site);
            var matches=p.streetWitnesses.Values.Where(s=>s.Site.Fingerprint==fingerprint).ToArray();
            if(matches.Length==0)return null;
            if(matches.Any(s=>s.Context.Key!=matches[0].Context.Key))throw new MissionCheckpointException("Conflicting fixed street contexts");
            return matches[0];
        }
        internal static bool OwnsDoor(MissionProgress p,StaticWorldObject raw)=>p!=null&&raw!=null&&raw.metrId==p.mission.district&&p.streetWitnesses.Values.Any(s=>
            MissionNpcInteriorPlanning.ReadRaw(s.Context.Floor.RawDoorJson).mxoStaticId==raw.mxoStaticId||MissionNpcInteriorPlanning.ReadRaw(s.Context.Lobby.RawDoorJson).mxoStaticId==raw.mxoStaticId);
        internal static MissionElevatorPoses.Overlay Overlay(MissionProgress p,StaticWorldObject raw)
        {
            if(p==null||raw==null||raw.metrId!=p.mission.district)return null;MissionElevatorPoses.Overlay resolved=null;
            foreach(var selected in p.streetWitnesses.Values)
            {
                var context=selected.Context;var entries=new[]{context.Floor,context.Lobby};
                foreach(var entry in entries.Where(e=>MissionNpcInteriorPlanning.ReadRaw(e.RawDoorJson).mxoStaticId==raw.mxoStaticId))
                {
                    if(p.restoreRefusal!=null)return new MissionElevatorPoses.Overlay(MissionElevatorPoses.OverlayKind.Refused,refusal:"QuarantinedStreetContext");
                    if(!MissionPublicDoors.CurrentDataset().TryScene(raw.metrId,raw.sectorID,out var evidence,out _,out string why))return new MissionElevatorPoses.Overlay(MissionElevatorPoses.OverlayKind.Refused,refusal:why);
                    try{MissionNpcInteriorCheckpoint.SavedEntry(evidence,entry,raw);}
                    catch(Exception ex) when(ex is MissionCheckpointException||ex is InvalidDataException||ex is JsonException){return new MissionElevatorPoses.Overlay(MissionElevatorPoses.OverlayKind.Refused,refusal:"InvalidStreetCarAlias:"+ex.Message);}
                    var candidate=new MissionElevatorPoses.Overlay(MissionElevatorPoses.OverlayKind.Resolved,MissionElevatorPoses.Position(entry.Position),entry.Quaternion);
                    if(resolved!=null&&(!resolved.Position.Equals(candidate.Position)||resolved.Quaternion!=candidate.Quaternion))return new MissionElevatorPoses.Overlay(MissionElevatorPoses.OverlayKind.Refused,refusal:"ConflictingStreetCarHistory");
                    resolved=candidate;
                }
            }
            return resolved;
        }
    }
}
