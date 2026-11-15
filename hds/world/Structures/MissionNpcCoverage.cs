using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using E = hds.MissionInteriorEvidence;
using G = hds.MissionOpenFloorResolver;
namespace hds
{
    internal static class MissionNpcCoverage
    {
        internal const string DomainSha256="A959429450937021030D353C31735B4A5DFD7D777FF04DF5E58E05C90A7CADAA";
        private sealed class Provider : IMissionNpcInteriorEvidenceProvider
        {
            private readonly DataLoader loader; private readonly string directory; private readonly E.Manifest manifest;
            private readonly Dictionary<ushort,StaticWorldObject[]> markers;
            private readonly ConcurrentDictionary<ushort,Lazy<E>> scenes=new ConcurrentDictionary<ushort,Lazy<E>>();
            internal Provider(DataLoader loader,string directory,E.Manifest manifest)
            {
                this.loader=loader;this.directory=directory;this.manifest=manifest;
                markers=loader.WorldObjectsDB.Where(o=>o.metrId==1&&o.type!=null&&o.type.Length>=2&&MissionSites.Goid(o)==19).GroupBy(o=>o.sectorID).ToDictionary(g=>g.Key,g=>g.ToArray());
            }
            public E Evidence(ushort world,ushort sector)
            {
                if(world!=1)throw new InvalidDataException("UndeclaredActiveInteriorWorld");
                return scenes.GetOrAdd(sector,s=>new Lazy<E>(()=>E.LoadActive(directory,1,s,manifest),LazyThreadSafetyMode.ExecutionAndPublication)).Value;
            }
            internal bool Saved(ushort world,ushort sector,string digest,out E evidence)
            {
                evidence=null;if(world!=1||!manifest.Sectors.Any(r=>r.Sector==sector&&r.Digest.Equals(digest,StringComparison.OrdinalIgnoreCase)))return false;
                evidence=Evidence(world,sector);return true;
            }
            public E SavedEvidence(ushort world,ushort sector,string digest) => MissionNpcInteriorEvidenceProvider.Instance.SavedEvidence(world,sector,digest);
            public StaticWorldObject[] Markers(ushort world,ushort sector) => world==1&&markers.TryGetValue(sector,out StaticWorldObject[] rows)?(StaticWorldObject[])rows.Clone():Array.Empty<StaticWorldObject>();
            public StaticWorldObject Door(ushort world,uint key) => loader.getObjectValues(key,world);
            public MissionOpenFloorEntry.Result Entry(ushort world,G.Position door,double x,double z,string evidence) => MissionOpenFloorEntry.Calculate(world,door,x,z,evidence);
        }
        private sealed class State
        {
            internal readonly MissionNpcDomainCertificates Domains; internal readonly IMissionNpcInteriorEvidenceProvider Provider; internal readonly string Refusal;
            internal State(MissionNpcDomainCertificates domains,IMissionNpcInteriorEvidenceProvider provider,string refusal) {Domains=domains;Provider=provider;Refusal=refusal;}
        }
        private static State current;
        internal static bool Required(Mission mission,int phase,out int roles)
        {
            roles=0;
            if(mission?.district!=1||!MissionNpcInteriorPlanning.Roles(mission,phase,out var selected,out _))return false;
            roles=selected.Length;return true;
        }
        // Called once before listeners. No scene gzip or route planning is performed here.
        internal static void Prepare(DataLoader loader)
        {
            var timer=System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var sites=MissionSites.For(1);string directory=DataLoader.DataPath("mission_interior_evidence_v2","1");
                E.Manifest manifest=E.ActiveManifest(directory,1);
                var domains=MissionNpcDomainCertificates.Load(File.ReadAllBytes(Path.Combine(directory,"npc-domains.json.gz")),DomainSha256,sites,
                    MissionNpcDomainCertificates.Inputs(loader.WorldObjectsDB),E.ActiveManifestSha256);
                Interlocked.Exchange(ref current,new State(domains,new Provider(loader,directory,manifest),null));
                Output.WriteLine("[MISSION NPC] certified fresh catalogue "+domains.Count+" sites in "+timer.ElapsedMilliseconds+" ms (selected scenes load on adoption)");
            }
            catch(Exception ex) when(ex is IOException||ex is InvalidDataException||ex is UnauthorizedAccessException||ex is JsonException)
            {
                Interlocked.Exchange(ref current,new State(null,null,"NpcCoverageUnavailable:"+ex.GetType().Name));
                Output.WriteLine("[MISSION NPC] new generated NPC selection refused: "+ex.Message);
            }
        }
        internal static E SavedEvidence(ushort world,ushort sector,string digest)
        {
            State state=Volatile.Read(ref current);
            if(state?.Provider is Provider provider&&provider.Saved(world,sector,digest,out E evidence))return evidence;
            return E.LoadSaved(DataLoader.DataPath("mission_interior_evidence_v2",world.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                DataLoader.DataPath("mission_interior_evidence",world.ToString(System.Globalization.CultureInfo.InvariantCulture)),world,sector,digest);
        }
        internal static Func<MissionSite,bool> Filter(Mission mission,int phase)
        {
            if(!Required(mission,phase,out int roles))return null;
            State state=Volatile.Read(ref current);
            return site=>state?.Domains!=null&&state.Domains.TryGet(site,roles,out _,out _);
        }
        internal static bool TrySelected(Mission mission,int phase,MissionSite site,out MissionNpcDomainCertificates.Binding binding,out IMissionNpcInteriorEvidenceProvider provider,out string refusal)
        {
            binding=null;provider=null;refusal="NpcCoverageNotPrepared";State state=Volatile.Read(ref current);
            if(!Required(mission,phase,out int roles)){refusal="NotCertifiedNpcPhase";return false;}
            if(state?.Domains==null){refusal=state?.Refusal??refusal;return false;}
            if(!state.Domains.TryGet(site,roles,out binding,out refusal))return false;
            provider=state.Provider;return true;
        }
    }
}
