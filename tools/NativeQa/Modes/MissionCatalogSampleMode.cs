using System.Xml.Linq;
using System.Collections.Concurrent;

namespace NativeQa.Modes;

public sealed class MissionCatalogSampleMode : NativeQaMode
{
    public override string Name => "mission-catalog-sample";
    private readonly ConcurrentDictionary<string,NativeQaModePlan> plans = new(StringComparer.Ordinal);
    public override NativeQaModePlan Plan
    {
        get => plans.GetOrAdd(Environment.GetEnvironmentVariable("MXO_QA_MISSION_ID")??"1205",BuildPlan);
    }
    private static NativeQaModePlan BuildPlan(string value)
    {
            string? root=AppContext.BaseDirectory;
            while(root!=null&&!File.Exists(Path.Combine(root,"hds.sln")))root=Path.GetDirectoryName(root);
            if(root==null)throw new DirectoryNotFoundException("Mission catalog repository");
            var catalog=Directory.GetFiles(Path.Combine(root,"data","missions"),"*.xml").Select(XElement.Load).ToArray();
            var ids=value.Split(',',StringSplitOptions.TrimEntries).Select(v=>int.TryParse(v,out int n)?n:throw new ArgumentException("MXO_QA_MISSION_ID must contain catalog mission IDs")).ToArray();
            if(ids.Length>8||ids.Distinct().Count()!=ids.Length)throw new ArgumentException("Choose up to eight distinct catalog missions");
            var selected=ids.Select(candidate=>catalog.SingleOrDefault(m=>(int)m.Attribute("id")! == candidate)??throw new ArgumentException("Unknown catalog mission "+candidate)).ToArray();
            int id=ids[0];var mission=selected[0];
            int org=(int?)mission.Attribute("org")??1,district=(int?)mission.Attribute("district")??1;
            string contact=(string?)mission.Attribute("contact")??"Tyndall",districtName=(string?)mission.Attribute("districtName")??"slums";
            uint ticket=(uint?)mission.Attribute("entryItem")??0;
            if(selected.Any(m=>((int?)m.Attribute("district")??1)!=district)||ids.Length>1&&selected.Any(m=>((uint?)m.Attribute("entryItem")??0)!=0))
                throw new ArgumentException("A shared sample session requires one district and contact-issued missions");
            var hardline=File.ReadLines(Path.Combine(root,"data","hardline_network.csv")).Skip(1).Select(l=>l.Split(','))
                .First(r=>r.Length>=15&&r[0]==district.ToString()&&r[4].Length>0);
            double Coordinate(int index)=>double.Parse(hardline[index],System.Globalization.CultureInfo.InvariantCulture);
            double x=district==1?23828:Coordinate(11),y=district==1?495:Coordinate(12),z=district==1?13984:Coordinate(13);
            var setup=new List<string>{"DELETE FROM char_missions WHERE charId=110 AND missionId<>986",
                "DELETE FROM inventory WHERE charId=110",
                "INSERT IGNORE INTO char_missions(charId,missionId) VALUES "+string.Join(",",catalog.Where(m=>!ids.Contains((int)m.Attribute("id")!)).Select(m=>$"(110,{(int)m.Attribute("id")!})")),
                FormattableString.Invariant($"UPDATE characters SET level=50,exp={hds.PlayerProgression.ExperienceForLevel(50)},x={x},y={y},z={z},rotation=126,alignment={Math.Max(1,org)},repMero={selected.Max(m=>(int?)m.Attribute("minReputation")??0)},districtId={district},district='{districtName}',healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000,pvpflag=0 WHERE charId=110")};
            if(ticket!=0)setup.Add($"INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,{ticket},1,0,0)");
            return new(new ushort[]{2,17,45,1006,77},(int)x,(int)z,Array.Empty<string>(),Array.Empty<string>(),setup.ToArray(),
                DevCommands:true,environmentOverrides:new(){["MXO_MISSION_SITE_QA_SEED"]=ids.Length>1?"catalog":id.ToString(),["MXO_AGGRO"]="0",["MXO_MOB_WANDER"]="0"},
                readyMessages:selected.Select(m=>$"Catalog mission {(int)m.Attribute("id")!}: {(string?)m.Attribute("title")}; contact {(string?)m.Attribute("contact")??"Tyndall"}. Use ?org {Math.Max(1,(int?)m.Attribute("org")??1)} before calling that contact. This mission is uncompleted.")
                    .Append("Each accepted phase prints measured street/lobby/floor positions. Other receipts are fixture setup; use ordinary mission interactions and rides. Movement/weaken are assistance.").ToArray());
    }
}
