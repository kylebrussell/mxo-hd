using System.Text;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task Large02PartialWorld(ScenarioContext context)
    {
        var p=await context.AddPlayerAsync(new CharacterSeed("Large02Traveller"){District="large02",DistrictId=13,X=-7500,Y=95,Z=-2000});
        await p.EnterWorldAsync();
        Check.That(p.Received("06").Any(r=>Encoding.Latin1.GetString(r.Bytes).Contains("large02/large02.metr")),"Shipped large02 world resource");
        await p.WaitUntilAsync(()=>p.ViewsOfGoid(592).Count>0,"native sector207/static3 hardline592",TimeSpan.FromSeconds(20));
        p.SendRpc($"80 c7 {MissionNpcId(p.ViewsOfGoid(592).Last())} 05 00");await p.WaitForRpcAsync("80 fb 01 00 00 00 01");
        p.SendRpc("80 fc 01");await p.WaitForRpcAsync("80 fd");p.SendRpc("80 fe 01");await p.WaitForRpcAsync("81 07");
        p.SendRpc("81 08");await p.WaitForRpcAsync("81 09");p.SendRpc("05");
        p.SendRpc("6c 0d 00 00 00 00 00 00 00");await p.WaitForRpcAsync("6d 0d 00 00 00 08 00 01 01 00 00 01 00 00 00 00");
        int selfSpawns=p.World.CreationSnapshot().Count(c=>c.ViewId==2),loads=p.Received("06").Count;
        // Explicit protocol route: not proof that the native Loading Area menu offers this construct.
        p.SendRpc("80 ff 0d 00 00 00 01 00 00 00");await p.WaitForRpcAsync("81 07");p.SendRpc("81 08");
        await p.WaitUntilAsync(()=>p.Received("06").Count>loads,"normal-world06 after LA return",TimeSpan.FromSeconds(10));
        p.SendRpc("05");await p.WaitUntilAsync(()=>p.World.CreationSnapshot().Count(c=>c.ViewId==2)>selfSpawns,"new real self spawn after return",TimeSpan.FromSeconds(15));
        Check.Equal(13L,await context.Database.LongAsync($"SELECT districtId FROM characters WHERE charId={p.CharId}"),"Return map");
        Check.Equal("-7500 95 -2000",await context.Database.ScalarAsync($"SELECT CONCAT(x,' ',y,' ',z) FROM characters WHERE charId={p.CharId}"),"Exact shipped arrival");
        var door=(await context.AddPlayersInWorldAsync(new CharacterSeed("Large02Door"){District="large02",DistrictId=13,X=12300,Y=95,Z=-4100}))[0];
        door.SendRpc("80 c8 0b 00 b0 01 03 00");
        await door.WaitUntilAsync(()=>door.ViewsOfGoid(359).Count>0,"native unrotated greenmetal door27/11",TimeSpan.FromSeconds(10));
        Check.Equal(0,p.World.Problems.Count,"Hardline transport protocol");Check.Equal(0,door.World.Problems.Count,"Door protocol");
        await File.WriteAllTextAsync(Path.Combine(Path.GetTempPath(),"mxo-large02-success-debug.log"),context.Server.FullOutput()+"\n--- DebugLog ---\n"+context.Server.DebugLog());
        Console.WriteLine("  large02: shipped592;6d13/1;LA/direct80ff/reload/newself;native359door. No ambient NPC fabricated; UIreturn unresolved;47nav unavailable.");
    }
}
