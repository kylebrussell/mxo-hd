using System.Text;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task AshencourtePartialWorld(ScenarioContext context)
    {
        var p=(await context.AddPlayersInWorldAsync(new CharacterSeed("AshencourteTraveller"){Level=25,Organization=1,
            X=111600,Y=695,Z=-6130,Items=new[]{(3,38538u,1)}}))[0];
        await p.WaitUntilAsync(()=>p.ViewsOfGoid(38282).Count>0,"actual Zion sender",TimeSpan.FromSeconds(20));
        p.SendRpc("63 03 6a");await p.WaitForRpcAsync("65 03 00 6a 00");
        await p.WaitForRpcAsync("81 07",TimeSpan.FromSeconds(185));
        p.SendRpc("81 08");await p.WaitForRpcAsync("81 08");
        var load=await p.WaitForRpcAsync("06");
        Check.Equal(11u,BitConverter.ToUInt32(load.Bytes,3),"retail construct11, not server map6");
        Check.That(Encoding.Latin1.GetString(load.Bytes).Contains("archive_ashencourte/archive_ashencourte.metr"),"actual Ashencourte resource");
        p.SendRpc("05");await p.WaitUntilAsync(()=>p.ViewsOfGoid(38271).Count>0,"ordinary source-arrival Zion returner",TimeSpan.FromSeconds(30));
        Check.Equal(6L,await context.Database.LongAsync($"SELECT districtId FROM characters WHERE charId={p.CharId}"),"map6 saved independently");
        Check.Equal("-18500 95 -7500",await context.Database.ScalarAsync($"SELECT CONCAT(x,' ',y,' ',z) FROM characters WHERE charId={p.CharId}"),"grounded source arrival");
        var returner=p.ViewsOfGoid(38271).Last();
        p.SendRpc($"80 c7 {MissionNpcId(returner)} 02 00");await p.WaitForRpcAsync("81 07");
        Check.Equal(1L,await context.Database.LongAsync($"SELECT districtId FROM characters WHERE charId={p.CharId}"),"session returns to city");
        Check.Equal("111600 695 -6130",await context.Database.ScalarAsync($"SELECT CONCAT(x,' ',y,' ',z) FROM characters WHERE charId={p.CharId}"),"exact original session position");
        Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND goid=38538 AND slot=106 AND count=1"),"owned reusable book retained");
        Check.Equal(0,p.World.Problems.Count,"archive wire parser");
        var visitor=(await context.AddPlayersInWorldAsync(new CharacterSeed("AshencourteCapture"){District="archive_ashencourte",DistrictId=6,X=11070,Y=95,Z=3974}))[0];
        await visitor.WaitForViewNamedAsync("Blackwood Goof",TimeSpan.FromSeconds(20));
        Check.Equal(0,visitor.World.Problems.Count,"existing captured Goof wire parser");
        await File.WriteAllTextAsync(Path.Combine(Path.GetTempPath(),"mxo-ashencourte-success-debug.log"),context.Server.FullOutput()+"\n--- DebugLog ---\n"+context.Server.DebugLog());
        Console.WriteLine("  ashencourte: owned38538/equip106; ordinary sender cycle; construct11/map6; grounded arrival; returner38271; exact session origin; book retained. Existing captured Goof visible; unsupported Mushfake/LA UI unverified.");
    }
}
