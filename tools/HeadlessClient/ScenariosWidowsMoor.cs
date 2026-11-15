using System.Text;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task WidowsMoorPartialWorld(ScenarioContext context)
    {
        var p=(await context.AddPlayersInWorldAsync(new CharacterSeed("WidowsMoorTraveller"){Level=40,Organization=1,
            X=111600,Y=695,Z=-6130,Items=new[]{(3,38536u,1)}}))[0];
        await p.WaitUntilAsync(()=>p.ViewsOfGoid(38282).Count>0,"actual Zion sender",TimeSpan.FromSeconds(20));
        p.SendRpc("63 03 6a");await p.WaitForRpcAsync("65 03 00 6a 00");
        await p.WaitForRpcAsync("81 07",TimeSpan.FromSeconds(185));
        p.SendRpc("81 08");await p.WaitForRpcAsync("81 08");
        var load=await p.WaitForRpcAsync("06");
        Check.Equal(13u,BitConverter.ToUInt32(load.Bytes,3),"retail construct13, not server map10");
        Check.That(Encoding.Latin1.GetString(load.Bytes).Contains("archive_widowsmoor/archive_widowsmoor.metr"),"actual Widow's Moor resource");
        bool IsArrivalReturner(ViewCreation v)=>v.Goid==38271 && v.Attributes.TryGetValue("Position",out var pos) && pos.Length==24 && BitConverter.ToDouble(pos)==-18500 && BitConverter.ToDouble(pos,8)==95 && BitConverter.ToDouble(pos,16)==-7500;
        p.SendRpc("05");await p.WaitUntilAsync(()=>p.ViewsOfGoid(38271).Any(IsArrivalReturner),"existing dynamic Zion returner at actual arrival",TimeSpan.FromSeconds(30));
        Check.Equal(10L,await context.Database.LongAsync($"SELECT districtId FROM characters WHERE charId={p.CharId}"),"map10 saved independently");
        var arrival=await context.Database.ScalarAsync($"SELECT CONCAT(x,' ',y,' ',z) FROM characters WHERE charId={p.CharId}");
        var returner=p.ViewsOfGoid(38271).Last(IsArrivalReturner);
        p.SendRpc($"80 c7 {MissionNpcId(returner)} 02 00");await p.WaitForRpcAsync("81 07");
        p.SendRpc("81 08");await p.WaitForRpcAsync("81 08");await p.WaitForRpcAsync("06");p.SendRpc("05");
        await p.WaitUntilAsync(()=>p.ViewsOfGoid(38282).Count>0,"returned city sender",TimeSpan.FromSeconds(30));
        Check.Equal(1L,await context.Database.LongAsync($"SELECT districtId FROM characters WHERE charId={p.CharId}"),"session returns to city");
        Check.Equal("111600 695 -6130",await context.Database.ScalarAsync($"SELECT CONCAT(x,' ',y,' ',z) FROM characters WHERE charId={p.CharId}"),"exact original session position");
        Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND goid=38536 AND slot=106 AND count=1"),"owned reusable book retained");
        Check.Equal(0,p.World.Problems.Count,"archive wire parser");
        await File.WriteAllTextAsync(Path.Combine(Path.GetTempPath(),"mxo-widowsmoor-success-debug.log"),context.Server.FullOutput()+"\n--- DebugLog ---\n"+context.Server.DebugLog());
        Console.WriteLine($"  widowsmoor: actual book38536/equip106/cycle; construct13/map10; NAV arrival {arrival}; returner38271/exact origin; book retained. Source-static returner usability and NPC grounding remain separate gaps.");
    }
}
