using System.Text;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task Large01PartialWorld(ScenarioContext context)
    {
        var p=await context.AddPlayerAsync(new CharacterSeed("Large01Traveller"){District="large01",DistrictId=12,X=13000,Y=-1302,Z=-7400});
        await p.EnterWorldAsync();
        Check.That(p.Received("06").Any(r=>Encoding.Latin1.GetString(r.Bytes).Contains("large01/large01.metr")),"Shipped large01 world resource");
        await p.WaitUntilAsync(()=>p.ViewsOfGoid(592).Count>0,"native sector50/static7 hardline592",TimeSpan.FromSeconds(20));
        var booth=p.ViewsOfGoid(592).Last();
        p.SendRpc($"80 c7 {MissionNpcId(booth)} 05 00");await p.WaitForRpcAsync("80 fb 01 00 00 00 01");
        p.SendRpc("80 fc 01");await p.WaitForRpcAsync("80 fd");p.SendRpc("80 fe 01");await p.WaitForRpcAsync("81 07");
        p.SendRpc("81 08");await p.WaitForRpcAsync("81 09");p.SendRpc("05");
        p.SendRpc("6c 04 00 00 00 00 00 00 00");await p.WaitForRpcAsync("6d 04 00 00 00 08 00 01 01 00 00 01 00 00 00 00");
        int selfSpawns=p.World.CreationSnapshot().Count(c=>c.ViewId==2),worldLoads=p.Received("06").Count;
        // This verifies the existing wire route, NOT the unresolved native LA tree/menu selection.
        p.SendRpc("80 ff 04 00 00 00 01 00 00 00");await p.WaitForRpcAsync("81 07");
        p.SendRpc("81 08"); // Normal-world setup emits06;8109 is exclusive to the Loading Area.
        await p.WaitUntilAsync(()=>p.Received("06").Count>worldLoads,"large01 resource reload",TimeSpan.FromSeconds(10));
        p.SendRpc("05");await p.WaitUntilAsync(()=>p.World.CreationSnapshot().Count(c=>c.ViewId==2)>selfSpawns,"new real self spawn after LA wire return",TimeSpan.FromSeconds(15));
        Check.Equal(12L,await context.Database.LongAsync($"SELECT districtId FROM characters WHERE charId={p.CharId}"),"Return to shipped hardline");
        Check.Equal("13000 -1302 -7400",await context.Database.ScalarAsync($"SELECT CONCAT(x,' ',y,' ',z) FROM characters WHERE charId={p.CharId}"),"Exact HDL arrival");
        var door=(await context.AddPlayersInWorldAsync(new CharacterSeed("Large01Door"){District="large01",DistrictId=12,X=12900,Y=95,Z=15500}))[0];
        door.SendRpc("80 c8 01 00 80 08 03 00");
        await door.WaitUntilAsync(()=>door.ViewsOfGoid(365).Count>0,"unambiguous native darkwood door136/1",TimeSpan.FromSeconds(10));
        var npc=(await context.AddPlayersInWorldAsync(new CharacterSeed("Large01Capture"){District="large01",DistrictId=12,X=7713,Y=-105,Z=53769}))[0];
        await npc.WaitForViewNamedAsync("Chopper Runner",TimeSpan.FromSeconds(20));
        foreach(var actor in new[]{p,door,npc})Check.Equal(0,actor.World.Problems.Count,"Large01 wire protocol");
        await File.WriteAllTextAsync(Path.Combine(Path.GetTempPath(),"mxo-large01-success-debug.log"),
            context.Server.FullOutput()+"\n--- DebugLog ---\n"+context.Server.DebugLog());
        Console.WriteLine("  large01: shipped592;6d12/1;LA/direct80ff/reload/new-self;native365door;existingcapturedChopperRunner. UIreturn unresolved.");
    }
}
