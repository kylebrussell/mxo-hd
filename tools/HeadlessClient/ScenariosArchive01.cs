using System.Text;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task Archive01PartialWorld(ScenarioContext context)
    {
        var p=(await context.AddPlayersInWorldAsync(new CharacterSeed("Archive01Traveller"){Level=50,Organization=1,
            X=111600,Y=695,Z=-6130,Items=new[]{(3,38534u,1)}}))[0];
        await p.WaitUntilAsync(()=>p.ViewsOfGoid(38282).Count>0,"actual Zion sender",TimeSpan.FromSeconds(20));
        p.SendRpc("63 03 6a");await p.WaitForRpcAsync("65 03 00 6a 00");
        await p.WaitForRpcAsync("81 07",TimeSpan.FromSeconds(185));
        p.SendRpc("81 08");await p.WaitForRpcAsync("81 08");
        var load=await p.WaitForRpcAsync("06");
        Check.Equal(15u,BitConverter.ToUInt32(load.Bytes,3),"retail construct15, not server map4");
        Check.That(Encoding.Latin1.GetString(load.Bytes).Contains("archive01/archive01.metr"),"actual Archive01 resource");
        p.SendRpc("05");await p.WaitUntilAsync(()=>p.ViewsOfGoid(38271).Count>0,"ordinary source-arrival Zion returner",TimeSpan.FromSeconds(30));
        Check.Equal(4L,await context.Database.LongAsync($"SELECT districtId FROM characters WHERE charId={p.CharId}"),"map4 saved independently");
        Check.Equal("-4020 95 -46020",await context.Database.ScalarAsync($"SELECT CONCAT(x,' ',y,' ',z) FROM characters WHERE charId={p.CharId}"),"grounded source arrival");
        var returner=p.ViewsOfGoid(38271).Last();
        p.SendRpc($"80 c7 {MissionNpcId(returner)} 02 00");await p.WaitForRpcAsync("81 07");
        Check.Equal(1L,await context.Database.LongAsync($"SELECT districtId FROM characters WHERE charId={p.CharId}"),"session returns to city");
        Check.Equal("111600 695 -6130",await context.Database.ScalarAsync($"SELECT CONCAT(x,' ',y,' ',z) FROM characters WHERE charId={p.CharId}"),"exact original session position");
        Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND goid=38534 AND slot=106 AND count=1"),"owned reusable book retained");
        Check.Equal(0,p.World.Problems.Count,"archive wire parser");
        await File.WriteAllTextAsync(Path.Combine(Path.GetTempPath(),"mxo-archive01-success-debug.log"),context.Server.FullOutput()+"\n--- DebugLog ---\n"+context.Server.DebugLog());
        Console.WriteLine("  archive01: owned38534/equip106; ordinary sender cycle; construct15/map4; grounded arrival; returner38271; exact session origin; book retained. No ambient NPC or LA UI proof.");
    }
}
