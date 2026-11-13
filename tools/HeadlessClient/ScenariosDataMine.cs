using System.Text;
using System.Globalization;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task DataMinePartialWorld(ScenarioContext context)
    {
        var p=(await context.AddPlayersInWorldAsync(new CharacterSeed("DataMineBook"){Level=25,Organization=1,X=111600,Y=745,Z=-6130,Items=new[]{(3,47270u,1)}}))[0];
        await p.WaitUntilAsync(()=>p.ViewsOfGoid(38282).Count>0,"sourced Zion sender",TimeSpan.FromSeconds(15));
        p.SendRpc("63 03 6a");await p.WaitForRpcAsync("65 03 00 6a 00");
        await p.WaitForRpcAsync("81 07",TimeSpan.FromSeconds(185));p.SendRpc("81 08");await p.WaitForRpcAsync("81 08");var load=await p.WaitForRpcAsync("06");
        Check.Equal(17u,BitConverter.ToUInt32(load.Bytes,3),"DataMine retail construct17, independently of map7");
        Check.That(Encoding.Latin1.GetString(load.Bytes).Contains("archive_datamine/datamine.metr"),"Actual DataMine world resource");p.SendRpc("05");
        await p.WaitUntilAsync(()=>p.ViewsOfGoid(38271).Count>0,"existing OURS fallback retained at arrival",TimeSpan.FromSeconds(20));
        Check.Equal(7L,await context.Database.LongAsync($"SELECT districtId FROM characters WHERE charId={p.CharId}"),"Saved map index7");
        var rows=File.ReadLines(Path.Combine(context.RepositoryRoot,"data/staticObjects_datamine.csv")).Skip(1).Select(l=>l.Split(',')).Where(r=>r[4]=="a3b80000").ToArray();Check.Equal(8,rows.Length,"Eight exact source proxies");
        static double Number(string s)=>double.Parse(s,CultureInfo.InvariantCulture);
        static bool At(ViewCreation v,double x,double y,double z)=>v.Attributes.TryGetValue("Position",out var a) && Math.Abs(BitConverter.ToDouble(a)-x)<.01 && Math.Abs(BitConverter.ToDouble(a,8)-y)<.01 && Math.Abs(BitConverter.ToDouble(a,16)-z)<.01;
        static async Task Move(HeadlessPlayer actor,double x,double y,double z){actor.SkipReceived();actor.World.SendViewList(WireFormat.FromHex("02 00 01 08").Concat(BitConverter.GetBytes((float)x)).Concat(BitConverter.GetBytes((float)y)).Concat(BitConverter.GetBytes((float)z)).Concat(new byte[]{0,0}).ToArray());actor.SendRpc("81 48");await actor.WaitForRpcAsync("81 49");}
        ViewCreation? first=null,last=null;double lx=0,ly=0,lz=0;
        foreach(var r in rows)
        {
            lx=Number(r[6]);ly=Number(r[7]);lz=Number(r[8]);await Move(p,lx,ly-50,lz);
            await p.WaitUntilAsync(()=>p.ViewsOfGoid(47267).Any(v=>At(v,lx,ly,lz)),"source-bound47267 at "+r[2],TimeSpan.FromSeconds(15));
            last=p.ViewsOfGoid(47267).Last(v=>At(v,lx,ly,lz));first??=last;
            Check.That(last.Attributes["Orientation"].SequenceEqual(Convert.FromHexString(r[10])),"Source quaternion retained");
            Check.That(p.World.ViewSnapshot().Any(v=>WireFormat.ToHex(v).Contains("010008a3b8"+r[2])),"Actual03 native GOID47267 + related static key");
        }
        // These are ordinary position/view records, not evidence of a native walked path.
        async Task Refused(string handle,string why){int resets=p.Received("81 07").Count;p.SkipReceived();p.SendRpc($"80 c7 {handle} 02 00");p.SendRpc("81 48");await p.WaitForRpcAsync("81 49");Check.Equal(resets,p.Received("81 07").Count,why);}
        await Refused(MissionNpcId(first!),"Deleted old source view cannot return");
        await Refused($"{Le(last!.ViewId)} {Le((ushort)(last.SpawnId^1))}","Forged generation cannot return");
        await Move(p,lx+1200,ly-50,lz);await Refused(MissionNpcId(last!),"Current but remote source view cannot return");
        await Move(p,lx,ly-50,lz);p.SkipReceived();p.SendRpc($"80 c7 {MissionNpcId(last!)} 02 00");await p.WaitForRpcAsync("81 07");
        Check.Equal("1 111600 745 -6130",await context.Database.ScalarAsync($"SELECT CONCAT(districtId,' ',x,' ',y,' ',z) FROM characters WHERE charId={p.CharId}"),"Actual source proxy returns exact session city origin");
        p.SendRpc("81 08");await p.WaitForRpcAsync("81 08");p.SendRpc("05");await Refused(MissionNpcId(last!),"Cross-world old handle cannot return again");
        Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND goid=47270 AND slot=106"),"Normal book remains reusable");
        var foreign=(await context.AddPlayersInWorldAsync(new CharacterSeed("DataMineMeroProxy"){Organization=3,District="archive_datamine",DistrictId=7,X=-31200,Y=1495,Z=16450}))[0];
        await foreign.WaitUntilAsync(()=>foreign.ViewsOfGoid(47267).Any(v=>At(v,-31200,1545,16450)),"Mero sees actual generic source proxy",TimeSpan.FromSeconds(20));
        var generic=foreign.ViewsOfGoid(47267).Last(v=>At(v,-31200,1545,16450));foreign.SendRpc($"80 c7 {MissionNpcId(generic)} 02 00");await foreign.WaitForRpcAsync("81 07");
        Check.Equal("1 23828 495 13984",await context.Database.ScalarAsync($"SELECT CONCAT(districtId,' ',x,' ',y,' ',z) FROM characters WHERE charId={foreign.CharId}"),"No org restriction; no origin uses explicit OURS Mara fallback");
        Check.Equal(0,p.World.Problems.Count,"DataMine normal book and source proxy wire");Check.Equal(0,foreign.World.Problems.Count,"Generic no-org wire");
        await File.WriteAllTextAsync(Path.Combine(Path.GetTempPath(),"mxo-datamine-success-debug.log"),context.Server.FullOutput()+"\n--- DebugLog ---\n"+context.Server.DebugLog()+"\n--- Actual source 03 ---\n"+string.Join("\n",p.World.ViewSnapshot().Where(v=>WireFormat.ToHex(v).Contains("010008a3b8")).Select(WireFormat.ToHex)));
        Console.WriteLine("  DataMine PARTIAL: normal47270mount/180scycle/map7-retail17/eightactual47267staticbound03/sourcequats/forged,deleted,remote,crossworld refusal/exactorigin/noorgMaraFallback; no native walk/render/node claims.");
    }
}
