using System.Text;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task Medium03PartialWorld(ScenarioContext context)
    {
        var p=await context.AddPlayerAsync(new CharacterSeed("Medium03Traveller"){District="medium03",DistrictId=16,X=37700,Y=495,Z=31700,Rotation=192});
        await p.EnterWorldAsync();
        Check.Equal((byte)192,p.World.CreationSnapshot().Last(v=>v.ViewId==2).Attributes["Yaw Interval"].Single(),"Exact native fixture byte heading survives initial UDP login");
        Check.That(p.Received("06").Any(r=>Encoding.Latin1.GetString(r.Bytes).Contains("medium03/medium03.metr")),"Shipped medium03 resource");
        await p.WaitUntilAsync(()=>p.ViewsOfGoid(592).Count>0,"native sector52/static1 hardline592",TimeSpan.FromSeconds(20));
        var booth=p.ViewsOfGoid(592).Last();
        p.SendRpc($"80 c7 {MissionNpcId(booth)} 05 00");await p.WaitForRpcAsync("80 fb 02 00 00 00 01");
        p.SendRpc("80 fc 01");await p.WaitForRpcAsync("80 fd");p.SendRpc("80 fe 01");await p.WaitForRpcAsync("81 07");
        p.SendRpc("81 08");await p.WaitForRpcAsync("81 09");p.SendRpc("05");
        p.SendRpc("6c 10 00 00 00 00 00 00 00");await p.WaitForRpcAsync("6d 10 00 00 00 08 00 01 01 00 00 02 00 00 00 00");
        int self=p.World.CreationSnapshot().Count(c=>c.ViewId==2),loads=p.Received("06").Count;
        // Direct wire route only: native Loading Area tree rejects map IDs >10.
        p.SendRpc("80 ff 10 00 00 00 02 00 00 00");await p.WaitForRpcAsync("81 07");p.SendRpc("81 08");
        await p.WaitUntilAsync(()=>p.Received("06").Count>loads,"medium03 reload",TimeSpan.FromSeconds(10));p.SendRpc("05");
        await p.WaitUntilAsync(()=>p.World.CreationSnapshot().Count(c=>c.ViewId==2)>self,"new self after direct wire return",TimeSpan.FromSeconds(15));
        Check.Equal(16L,await context.Database.LongAsync($"SELECT districtId FROM characters WHERE charId={p.CharId}"),"Return to medium03");
        Check.Equal("37700 495 31700",await context.Database.ScalarAsync($"SELECT CONCAT(x,' ',y,' ',z) FROM characters WHERE charId={p.CharId}"),"Exact HDL16/2 arrival");
        var door=(await context.AddPlayersInWorldAsync(new CharacterSeed("Medium03Door"){District="medium03",DistrictId=16,X=27100,Y=1695,Z=41100}))[0];
        door.SendRpc("80 c8 37 04 10 02 03 00");
        await door.WaitUntilAsync(()=>door.ViewsOfGoid(365).Count>0,"native sector33/static1079 darkwood door",TimeSpan.FromSeconds(10));
        int excludedViews=door.ViewsOfGoid(417).Count;
        door.SendRpc("80 c8 14 00 10 02 03 00"); // sector33/static20, omitted conflicting native key
        door.SendRpc("81 48");await door.WaitForRpcAsync("81 49");
        Check.Equal(excludedViews,door.ViewsOfGoid(417).Count,"Every ambiguous key variant remains absent");
        Check.Equal(0,p.World.Problems.Count,"Medium03 hardline wire");Check.Equal(0,door.World.Problems.Count,"Medium03 door wire");
        await File.WriteAllTextAsync(Path.Combine(Path.GetTempPath(),"mxo-medium03-success-debug.log"),context.Server.FullOutput()+"\n--- DebugLog ---\n"+context.Server.DebugLog());
        Console.WriteLine("  medium03 PARTIAL:fixtureYaw192/592/HDL16/2;6d16/2;radialLA exit/direct80ff return/newself;safe365door;ambiguous417 absent. No NPC/elevator/subway/LA return UI claim.");
    }
}
