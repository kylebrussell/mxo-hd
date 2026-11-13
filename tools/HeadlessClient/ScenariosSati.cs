using System.Text;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static Task SatiDoorDiagnostic(ScenarioContext context)=>NativeWorldDoorDiagnostic(context,NativeWorldDoorFixtures.Sati);
    private static async Task SatiPartialWorld(ScenarioContext context)
    {
        var p=(await context.AddPlayersInWorldAsync(new CharacterSeed("SatiTraveller"){Level=50,Experience=(int)hds.PlayerProgression.ExperienceForLevel(50),Organization=1,
            X=111600,Y=695,Z=-6130,Items=new[]{(3,47726u,1)}}))[0];
        await p.WaitUntilAsync(()=>p.ViewsOfGoid(38282).Count>0,"actual Zion sender",TimeSpan.FromSeconds(20));
        p.SendRpc("63 03 6a");await p.WaitForRpcAsync("65 03 00 6a 00");
        await p.WaitForRpcAsync("81 07",TimeSpan.FromSeconds(185));
        p.SendRpc("81 08");await p.WaitForRpcAsync("81 08");
        var load=await p.WaitForRpcAsync("06");
        Check.Equal(18u,BitConverter.ToUInt32(load.Bytes,3),"retail construct18, not server map9");
        Check.That(Encoding.Latin1.GetString(load.Bytes).Contains("archive_sati/sati.metr"),"actual Sati resource");
        bool IsFallback(ViewCreation v)=>v.Goid==38271 && v.Attributes.TryGetValue("Position",out var at) && at.Length==24 && BitConverter.ToDouble(at)==14070 && BitConverter.ToDouble(at,8)==95 && BitConverter.ToDouble(at,16)==-4070;
        p.SendRpc("05");await p.WaitUntilAsync(()=>p.ViewsOfGoid(38271).Any(IsFallback),"UNCHANGED generated returner at existing OURS fallback",TimeSpan.FromSeconds(30));
        Check.Equal(9L,await context.Database.LongAsync($"SELECT districtId FROM characters WHERE charId={p.CharId}"),"map9 saved independently");
        Check.Equal("14070 95 -4070",await context.Database.ScalarAsync($"SELECT CONCAT(x,' ',y,' ',z) FROM characters WHERE charId={p.CharId}"),"existing OURS fallback; exact retail arrival unknown");
        var returner=p.ViewsOfGoid(38271).Last(IsFallback);int selfSpawns=p.World.CreationSnapshot().Count(v=>v.ViewId==2);
        p.SendRpc($"80 c7 {MissionNpcId(returner)} 02 00");await p.WaitForRpcAsync("81 07");
        p.SendRpc("81 08");await p.WaitForRpcAsync("81 08");var city=await p.WaitForRpcAsync("06");
        Check.That(Encoding.Latin1.GetString(city.Bytes).Contains("slums_barrens_full.metr"),"actual city resource reload");p.SendRpc("05");
        await p.WaitUntilAsync(()=>p.World.CreationSnapshot().Count(v=>v.ViewId==2)>selfSpawns,"new self after full city return",TimeSpan.FromSeconds(20));
        Check.Equal(1L,await context.Database.LongAsync($"SELECT districtId FROM characters WHERE charId={p.CharId}"),"session returns to city");
        Check.Equal("111600 695 -6130",await context.Database.ScalarAsync($"SELECT CONCAT(x,' ',y,' ',z) FROM characters WHERE charId={p.CharId}"),"exact original session position");
        Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND goid=47726 AND slot=106 AND count=1"),"owned reusable book retained");
        Check.Equal(0,p.World.Problems.Count,"archive wire parser");
        await NativeWorldDoorDiagnostic(context,NativeWorldDoorFixtures.Sati);
        await File.WriteAllTextAsync(Path.Combine(Path.GetTempPath(),"mxo-sati-success-debug.log"),context.Server.FullOutput()+"\n--- DebugLog ---\n"+context.Server.DebugLog());
        Console.WriteLine("  sati: owned47726/equip106; ordinary sender cycle; construct18/map9; existing OURS fallback; returner38271; exact session origin; book retained. No ambient NPC or LA UI proof.");
    }
}
