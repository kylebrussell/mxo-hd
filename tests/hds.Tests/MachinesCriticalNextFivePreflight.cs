using System.Globalization;
using System.Text.Json;
using System.Xml;
using hds;
using HeadlessClient;
namespace hds.Tests;
/// <summary>Offline normal catalogue/admission preflight; no world client, server, database or source substitution.</summary>
public static class MachinesCriticalNextFivePreflight
{
    public static string Run(string repo)
    {
        Directory.SetCurrentDirectory(repo);
        SectorProperties.Load(Path.Combine(repo,"data","sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(repo,"data","navmesh"));
        IEnumerable<StaticWorldObject> Statics()
        {
            foreach(string file in new[]{"staticObjects_slums_1.csv","staticObjects_slums_2.csv"})
            foreach(string line in File.ReadLines(Path.Combine(repo,"data",file)).Skip(1))
            {
                if(string.IsNullOrWhiteSpace(line))continue;var f=line.Split(',');double D(int i)=>double.Parse(f[i],CultureInfo.InvariantCulture);
                yield return new(){metrId=ushort.Parse(f[0]),sectorID=ushort.Parse(f[1]),mxoStaticId=BitConverter.ToUInt32(Convert.FromHexString(f[2])),
                    staticId=BitConverter.ToUInt32(Convert.FromHexString(f[3])),type=Convert.FromHexString(f[4][..4]),exterior=bool.Parse(f[5]),
                    pos_x=D(6),pos_y=D(7),pos_z=D(8),rot=D(9),quat=f[10]};
            }
        }
        var sites=MissionSites.Build(Statics(),1);var hoods=MissionSites.LoadNeighbourhoods(Path.Combine(repo,"data","slums_neighbourhoods.csv"));
        foreach(var s in sites)if(s.elevatorDoor!=null&&hoods.TryGetValue(s.elevatorDoor.sectorID,out string? name))s.neighbourhood=name;
        var missions=Directory.GetFiles(Path.Combine(repo,"data","missions"),"machines_1_2_*.xml").Select(f=>{var d=new XmlDocument();d.Load(f);return MissionXml.Parse(d.DocumentElement!);}).ToArray();
        var rows=new List<object>();int failed=0;
        foreach(int id in MachinesCriticalNextFiveFixture.Missions)
        {
            var mission=missions.Single(m=>m.id==id);var used=new List<MissionSite>();double x=17000,z=2300;
            for(int phaseIndex=0;phaseIndex<mission.phases.Count;phaseIndex++)
            {
                var phase=mission.phases[phaseIndex];int seed=MissionStaging.QaSiteSeed("1","mxotest_offline_machines_next",MachinesCriticalNextFiveFixture.Seed.ToString(),id,phaseIndex)!.Value;
                var rng=new Random(seed);int npcs=mission.npcs.Count(n=>n.at==phaseIndex+1)+MissionStaging.SpotsForWaves(phase);
                var neighbourhoods=phase.neighbourhoods.Count>0?phase.neighbourhoods:mission.neighbourhoods;
                bool Eligible(MissionSite candidate)=>MissionInteriorSites.Eligible(phase,candidate,out _,npcs);
                var site=MissionSites.Choose(sites,phase.site,x,z,npcs,MissionStaging.PlacesNeeded(phase),rng,used,neighbourhoods,requiredFurniture:MissionStaging.FurnitureNeeded(phase),admissible:Eligible)
                    ??MissionSites.Choose(sites,"Any",x,z,npcs,MissionStaging.PlacesNeeded(phase),rng,used,neighbourhoods,requiredFurniture:MissionStaging.FurnitureNeeded(phase),admissible:Eligible);
                if(site==null){rows.Add(new{Mission=id,Phase=phaseIndex,Success=false,Refusal="NoSite",Origin=new[]{x,z}});failed++;break;}
                // Preview already selected phase0's site; acceptance restarts its RNG before selecting sources.
                if(phaseIndex==0)rng=new Random(seed);
                var choices=MissionStaging.SelectObjects(phase,site,rng).Select(MissionOpenFloorChoice.From).ToArray();
                bool requires=MissionOpenFloorAdmission.Requires(phase);
                bool ok=MissionOpenFloorAdmission.TryGeometry(mission,phaseIndex,site,choices,out var poses,out string why);
                rows.Add(new{Mission=id,Phase=phaseIndex,EffectiveSeed=seed,RequiresSharedAdmission=requires,Success=ok,Refusal=why,
                    Origin=new[]{x,z},Site=MissionOpenFloorSite.From(site),Choices=choices,Poses=poses,
                    LegacyChoiceQualification=requires?"Exact admission SelectObjects RNG order":"Catalogue preview only; legacy Stage may consume NPC RNG before object placement"});
                if(!ok)failed++; used.Add(site);
                // Nominal ordinary-door departure. Upload phases instead end at the actual Mara hardline.
                bool upload=phase.missionObjectives.Any(o=>o.type==MissionGoalType.Upload);
                x=upload?17000:site.elevatorDoor.pos_x;z=upload?2200:site.elevatorDoor.pos_z;
            }
        }
        return JsonSerializer.Serialize(new{Qualification="Offline nominal normal-door phase sequence from original offer origin17000/2300 with unchanged single layout seed201202. Actual live choices/early-exit origin differences must be retained by fresh wire evidence; no reroll/forced source.",
            Failed=failed,Rows=rows,SourceAssembly=typeof(Mission).Assembly.Location,SourceSha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(typeof(Mission).Assembly.Location)))},
            new JsonSerializerOptions{IncludeFields=true,WriteIndented=true});
    }
}
