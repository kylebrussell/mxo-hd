using System.Text;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task Small03PartialWorld(ScenarioContext context)
    {
        var p=await context.AddPlayerAsync(new CharacterSeed("Small03Traveller") { District="small03",DistrictId=17,X=17700,Y=-503,Z=34100 });
        await p.EnterWorldAsync();
        Check.That(p.Received("06").Any(r=>Encoding.Latin1.GetString(r.Bytes).Contains("small03/small03.metr")),"Shipped small03 world resource");
        await p.WaitUntilAsync(()=>p.ViewsOfGoid(592).Count>0,"native static-bound hardline592",TimeSpan.FromSeconds(20));
        var booth=p.ViewsOfGoid(592).Last();
        p.SendRpc($"80 c7 {MissionNpcId(booth)} 05 00");
        await p.WaitForRpcAsync("80 fb 01 00 00 00 01");
        p.SendRpc("80 fc 01");await p.WaitForRpcAsync("80 fd");
        p.SendRpc("80 fe 01");await p.WaitForRpcAsync("81 07");
        p.SendRpc("81 08");await p.WaitForRpcAsync("81 09");p.SendRpc("05");await Task.Delay(500);
        p.SendRpc("80 ff 09 00 00 00 01 00 00 00");await p.WaitForRpcAsync("81 07");
        Check.Equal(17L,await context.Database.LongAsync($"SELECT districtId FROM characters WHERE charId={p.CharId}"),"Return to native hardline");
        Check.Equal("17700 -503 34100",await context.Database.ScalarAsync($"SELECT CONCAT(x,' ',y,' ',z) FROM characters WHERE charId={p.CharId}"),"Exact HDL arrival");
        var door=(await context.AddPlayersInWorldAsync(new CharacterSeed("Small03Door") { District="small03",DistrictId=17,X=21300,Y=1695,Z=39100 }))[0];
        door.SendRpc("80 c8 07 00 d0 00 03 00");
        await door.WaitUntilAsync(()=>door.ViewsOfGoid(417).Count>0,"unambiguous native elevator door",TimeSpan.FromSeconds(10));
        int count=door.ViewsOfGoid(417).Count;
        door.SendRpc("80 c8 0f 00 d0 00 03 00"); // ambiguous native key must be absent, not resolved arbitrarily
        door.SendRpc("81 48");await door.WaitForRpcAsync("81 49");
        Check.Equal(count,door.ViewsOfGoid(417).Count,"Excluded conflicting door cannot spawn");
        var npc=(await context.AddPlayersInWorldAsync(new CharacterSeed("Small03Capture") { District="small03",DistrictId=17,X=26829,Y=5695,Z=22900 }))[0];
        await npc.WaitForViewNamedAsync("SSR Security Guard",TimeSpan.FromSeconds(20));
        Check.Equal(0,p.World.Problems.Count,"Small03 hardline protocol");
        Check.Equal(0,door.World.Problems.Count,"Small03 door protocol");
        Check.Equal(0,npc.World.Problems.Count,"Existing captured NPC protocol");
    }
}
