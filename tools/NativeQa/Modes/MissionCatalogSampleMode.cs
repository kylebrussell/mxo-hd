using System.Xml.Linq;

namespace NativeQa.Modes;

public sealed class MissionCatalogSampleMode : NativeQaMode
{
    public override string Name => "mission-catalog-sample";
    public override NativeQaModePlan Plan
    {
        get
        {
            string? root=AppContext.BaseDirectory;
            while(root!=null&&!File.Exists(Path.Combine(root,"hds.sln")))root=Path.GetDirectoryName(root);
            if(root==null)throw new DirectoryNotFoundException("Mission catalog repository");
            var catalog=Directory.GetFiles(Path.Combine(root,"data","missions"),"*.xml").Select(XElement.Load).ToArray();
            string value=Environment.GetEnvironmentVariable("MXO_QA_MISSION_ID")??"1205";
            if(!int.TryParse(value,out int id))throw new ArgumentException("MXO_QA_MISSION_ID must be a catalog mission ID");
            var mission=catalog.SingleOrDefault(m=>(int)m.Attribute("id")! == id)??throw new ArgumentException("Unknown catalog mission "+id);
            int org=(int?)mission.Attribute("org")??1,district=(int?)mission.Attribute("district")??1;
            string contact=(string?)mission.Attribute("contact")??"Tyndall",districtName=(string?)mission.Attribute("districtName")??"slums";
            uint ticket=(uint?)mission.Attribute("entryItem")??0;
            var hardline=File.ReadLines(Path.Combine(root,"data","hardline_network.csv")).Skip(1).Select(l=>l.Split(','))
                .First(r=>r.Length>=15&&r[0]==district.ToString()&&r[4].Length>0);
            double Coordinate(int index)=>double.Parse(hardline[index],System.Globalization.CultureInfo.InvariantCulture);
            double x=district==1?23828:Coordinate(11),y=district==1?495:Coordinate(12),z=district==1?13984:Coordinate(13);
            var setup=new List<string>{"DELETE FROM char_missions WHERE charId=110 AND missionId<>986",
                "DELETE FROM inventory WHERE charId=110",
                "INSERT IGNORE INTO char_missions(charId,missionId) VALUES "+string.Join(",",catalog.Where(m=>(int)m.Attribute("id")! !=id).Select(m=>$"(110,{(int)m.Attribute("id")!})")),
                FormattableString.Invariant($"UPDATE characters SET level=50,exp={hds.PlayerProgression.ExperienceForLevel(50)},x={x},y={y},z={z},rotation=126,alignment={Math.Max(1,org)},repMero={(int?)mission.Attribute("minReputation")??0},districtId={district},district='{districtName}',healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000,pvpflag=0 WHERE charId=110")};
            if(ticket!=0)setup.Add($"INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,{ticket},1,0,0)");
            return new(new ushort[]{2,17,45,1006,77},(int)x,(int)z,Array.Empty<string>(),Array.Empty<string>(),setup.ToArray(),
                DevCommands:true,environmentOverrides:new(){["MXO_MISSION_SITE_QA_SEED"]=id.ToString(),["MXO_AGGRO"]="0",["MXO_MOB_WANDER"]="0"},
                readyMessages:new[]{$"Catalog mission {id}: {(string?)mission.Attribute("title")}; contact {contact}. Earlier receipts are fixture setup; this mission is uncompleted.","Use ordinary mission interactions and elevator rides. Movement and weaken commands are available for travel/combat assistance."});
        }
    }
}
