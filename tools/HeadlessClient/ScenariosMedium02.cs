using System.Text;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task Medium02PartialWorld(ScenarioContext context)
    {
        var p=await context.AddPlayerAsync(new CharacterSeed("Medium02Traveller"){District="medium02",DistrictId=15,X=-1000,Y=95,Z=2500});
        await p.EnterWorldAsync();
        Check.That(p.Received("06").Any(r=>Encoding.Latin1.GetString(r.Bytes).Contains("medium02/medium02.metr")),"Shipped medium02 world resource");
        await p.WaitUntilAsync(()=>p.ViewsOfGoid(592).Count>0,"native hardline56/7",TimeSpan.FromSeconds(20));
        p.SendRpc($"80 c7 {MissionNpcId(p.ViewsOfGoid(592).Last())} 05 00");await p.WaitForRpcAsync("80 fb 01 00 00 00 01");
        p.SendRpc("80 fc 01");await p.WaitForRpcAsync("80 fd");p.SendRpc("80 fe 01");await p.WaitForRpcAsync("81 07");
        p.SendRpc("81 08");await p.WaitForRpcAsync("81 09");p.SendRpc("05");
        p.SendRpc("6c 0f 00 00 00 00 00 00 00");await p.WaitForRpcAsync("6d 0f 00 00 00 08 00 01 01 00 00 01 00 00 00 00");
        int spawns=p.World.CreationSnapshot().Count(c=>c.ViewId==2),loads=p.Received("06").Count;
        // Direct wire return only; the native LA menu's availability remains unresolved.
        p.SendRpc("80 ff 0f 00 00 00 01 00 00 00");await p.WaitForRpcAsync("81 07");p.SendRpc("81 08");
        await p.WaitUntilAsync(()=>p.Received("06").Count>loads,"normal-world06 return",TimeSpan.FromSeconds(10));
        p.SendRpc("05");await p.WaitUntilAsync(()=>p.World.CreationSnapshot().Count(c=>c.ViewId==2)>spawns,"new self spawn at shipped arrival",TimeSpan.FromSeconds(15));
        Check.Equal(15L,await context.Database.LongAsync($"SELECT districtId FROM characters WHERE charId={p.CharId}"),"Return map");
        Check.Equal("-1000 95 2500",await context.Database.ScalarAsync($"SELECT CONCAT(x,' ',y,' ',z) FROM characters WHERE charId={p.CharId}"),"Exact HDL arrival");
        var door=(await context.AddPlayersInWorldAsync(new CharacterSeed("Medium02Door"){District="medium02",DistrictId=15,X=-5700,Y=95,Z=14900}))[0];
        door.SendRpc("80 c8 d2 00 c0 04 03 00");
        await door.WaitUntilAsync(()=>door.ViewsOfGoid(414).Count>0,"native unrotated graymetal door76/210",TimeSpan.FromSeconds(10));
        var view=door.ViewsOfGoid(414).Last();
        Check.Equal(0x834u,BitConverter.ToUInt32(view.Attributes["CurrentState"]),"Normal supported door opens through native schema");
        await door.WaitForViewUpdateAsync(view.ViewId,"01 80 02 35 08 00 00",TimeSpan.FromSeconds(25));
        Check.Equal(0,p.World.Problems.Count,"Hardline protocol");Check.Equal(0,door.World.Problems.Count,"Door protocol");
        await File.WriteAllTextAsync(Path.Combine(Path.GetTempPath(),"mxo-medium02-success-debug.log"),context.Server.FullOutput()+"\n--- DebugLog ---\n"+context.Server.DebugLog());
        Console.WriteLine("  medium02:592booth;6d15/1;LA/direct80ff/new06/newself;414nativeOpen/state834/normalClosed835. NPCs not validated;nativeUIreturn unresolved.");
    }
}
