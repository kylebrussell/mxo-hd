using System.Text;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task SakuraPartialWorld(ScenarioContext context)
    {
        var p=(await context.AddPlayersInWorldAsync(new CharacterSeed("SakuraTraveller"){Level=35,Experience=(int)hds.PlayerProgression.ExperienceForLevel(35),Organization=1,
            X=111600,Y=695,Z=-6130,Items=new[]{(3,38537u,1)}}))[0];
        await p.WaitUntilAsync(()=>p.ViewsOfGoid(38282).Count>0,"actual Zion sender",TimeSpan.FromSeconds(20));
        p.SendRpc("63 03 6a");await p.WaitForRpcAsync("65 03 00 6a 00");
        await p.WaitForRpcAsync("81 07",TimeSpan.FromSeconds(185));
        p.SendRpc("81 08");await p.WaitForRpcAsync("81 08");
        var load=await p.WaitForRpcAsync("06");
        Check.Equal(12u,BitConverter.ToUInt32(load.Bytes,3),"retail construct12, not server map8");
        Check.That(Encoding.Latin1.GetString(load.Bytes).Contains("archive_sakura/archive_sakura.metr"),"actual Sakura resource");
        p.SendRpc("05");await p.WaitUntilAsync(()=>p.ViewsOfGoid(38271).Count>0,"ordinary source-arrival Zion returner",TimeSpan.FromSeconds(30));
        Check.Equal(8L,await context.Database.LongAsync($"SELECT districtId FROM characters WHERE charId={p.CharId}"),"map8 saved independently");
        Check.Equal("15500 295 15500",await context.Database.ScalarAsync($"SELECT CONCAT(x,' ',y,' ',z) FROM characters WHERE charId={p.CharId}"),"grounded source arrival");
        var returner=p.ViewsOfGoid(38271).Last();
        p.SendRpc($"80 c7 {MissionNpcId(returner)} 02 00");await p.WaitForRpcAsync("81 07");
        Check.Equal(1L,await context.Database.LongAsync($"SELECT districtId FROM characters WHERE charId={p.CharId}"),"session returns to city");
        Check.Equal("111600 695 -6130",await context.Database.ScalarAsync($"SELECT CONCAT(x,' ',y,' ',z) FROM characters WHERE charId={p.CharId}"),"exact original session position");
        Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND goid=38537 AND slot=106 AND count=1"),"owned reusable book retained");
        Check.Equal(0,p.World.Problems.Count,"archive wire parser");
        await File.WriteAllTextAsync(Path.Combine(Path.GetTempPath(),"mxo-sakura-success-debug.log"),context.Server.FullOutput()+"\n--- DebugLog ---\n"+context.Server.DebugLog());
        Console.WriteLine("  sakura: owned38537/equip106; ordinary sender cycle; construct12/map8; grounded arrival; returner38271; exact session origin; book retained. No ambient NPC or LA UI proof.");
    }
}
