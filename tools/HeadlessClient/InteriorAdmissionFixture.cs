using hds;
namespace HeadlessClient;
public sealed record InteriorAdmissionCase(string Name,int Mission,string Title,byte Organization,int LayoutSeed,double X,double Y,double Z,int[] Prerequisites,int TargetPhase,int Goal,uint Item,uint OriginalDoor,uint OriginalSource,int Level=50,int Objects=1,uint[]? OriginalSources=null);
public static class InteriorAdmissionFixture
{
    public const string ExpectedSourceEnvironment="MXO_INTERIOR_ADMISSION_PROOF_SOURCE_SHA256";
    public static void RequireSource(string? expected,string actual)
    {
        if(expected==null||expected.Length!=64||expected.Any(x=>!Uri.IsHexDigit(x))||!string.Equals(expected,actual,StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Interior proof requires root-pinned source SHA256 via "+ExpectedSourceEnvironment);
    }
    static readonly int[] Common={1201,1204,1203,1205,1206,1207,1208};
    public static readonly InteriorAdmissionCase Machines=new("interior-admission-machines",201201,"Disturbing Trends",2,201101,17000,495,2300,
        Common.Concat(Enumerable.Range(2201,10)).Concat(new[]{201101,201102,201103}).ToArray(),1,1,6783,0x0d100024,0x0d1001ed);
    public static readonly InteriorAdmissionCase Merv=new("interior-admission-merv",301203,"Turf of Vengeance",3,301203,17000,495,2300,
        MervPropagandaFixture.Prerequisites.Concat(new[]{301202}).ToArray(),1,7,6832,0x0ee00271,0x0ee0004b);
    public static readonly InteriorAdmissionCase Zion=new("interior-admission-zion",101209,"Hardline Hunter",1,101208,7800,95,13795,
        ZionFollowupFixture.Prerequisites.Concat(ZionFollowupFixture.Missions).Concat(new[]{101208}).ToArray(),0,1,0,0x0d100401,0x0d100402);
    public static readonly InteriorAdmissionCase Posters=new("interior-admission-posters",201202,"Poster Hunt",2,201202,17000,495,2300,
        PosterHuntFixture.Prerequisites,0,1,0,0x0d1003bb,0x0d1003dc,17,3,new uint[]{0x0d1003dc,0x0d100314,0x0d1002f1});
    public static readonly InteriorAdmissionCase[] Cases={Machines,Merv,Zion,Posters};
    public static string Seed(InteriorAdmissionCase c,long character)=>"INSERT IGNORE INTO char_missions(charId,missionId) VALUES "+string.Join(",",c.Prerequisites.Select(id=>$"({character},{id})"));
    public static IReadOnlyDictionary<string,string> Environment(InteriorAdmissionCase c)=>new Dictionary<string,string>{["MXO_DEV_COMMANDS"]="1",["MXO_MISSION_SITE_QA_SEED"]=c.LayoutSeed.ToString(System.Globalization.CultureInfo.InvariantCulture),["MXO_MOB_WANDER"]="0",["MXO_MOBS"]="test",["MXO_AGGRO"]="0"};
    public static uint DoorAt(IEnumerable<string> rows,double[] pose)
    {
        var ids=rows.Where(l=>!string.IsNullOrWhiteSpace(l)&&char.IsDigit(l[0])).Select(l=>l.Split(','))
            .Where(f=>f.Length>=11&&f[0]=="1"&&new ushort[]{417,419}.Contains(BitConverter.ToUInt16(Convert.FromHexString(f[4]),0))&&
                Enumerable.Range(0,3).All(i=>double.Parse(f[6+i],System.Globalization.CultureInfo.InvariantCulture)==pose[i]))
            .Select(f=>BitConverter.ToUInt32(Convert.FromHexString(f[2]),0)).Distinct().Take(2).ToArray();
        if(ids.Length!=1)throw new InvalidDataException("Exact current-site door position is missing or ambiguous");return ids[0];
    }
    public static ViewCreation? Body(IReadOnlyList<ViewCreation> views,int cursor,ushort goid,double[] centre)=>cursor<0||cursor>views.Count?null:views.Skip(cursor).LastOrDefault(v=>v.Goid==goid&&v.Attributes.TryGetValue("Position",out var p)&&p.Length==24&&Enumerable.Range(0,3).All(i=>BitConverter.ToDouble(p,i*8)==centre[i]));
}
