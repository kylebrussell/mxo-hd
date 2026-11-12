using System.Text;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task Medium01PartialWorld(ScenarioContext context)
    {
        var p=await context.AddPlayerAsync(new CharacterSeed("Medium01Traveller"){District="medium01",DistrictId=14,X=39250,Y=-953,Z=27000});
        await p.EnterWorldAsync();
        Check.That(p.Received("06").Any(r=>Encoding.Latin1.GetString(r.Bytes).Contains("medium01/medium01.metr")),"Shipped medium01 resource");
        await p.WaitUntilAsync(()=>p.ViewsOfGoid(592).Count>0,"native sector140/static1 booth592",TimeSpan.FromSeconds(20));
        var booth=p.ViewsOfGoid(592).Last();
        p.SendRpc($"80 c7 {MissionNpcId(booth)} 05 00");await p.WaitForRpcAsync("80 fb 01 00 00 00 01");
        p.SendRpc("80 fc 01");await p.WaitForRpcAsync("80 fd");p.SendRpc("80 fe 01");await p.WaitForRpcAsync("81 07");
        p.SendRpc("81 08");await p.WaitForRpcAsync("81 09");p.SendRpc("05");
        p.SendRpc("6c 0e 00 00 00 00 00 00 00");await p.WaitForRpcAsync("6d 0e 00 00 00 08 00 01 01 00 00 01 00 00 00 00");
        int selfSpawns=p.World.CreationSnapshot().Count(c=>c.ViewId==2),loads=p.Received("06").Count;
        // Existing direct wire route is diagnostic-only; it does not establish the native LA tree offers this construct.
        p.SendRpc("80 ff 0e 00 00 00 01 00 00 00");await p.WaitForRpcAsync("81 07");p.SendRpc("81 08");
        await p.WaitUntilAsync(()=>p.Received("06").Count>loads,"medium01 resource reload",TimeSpan.FromSeconds(10));
        p.SendRpc("05");await p.WaitUntilAsync(()=>p.World.CreationSnapshot().Count(c=>c.ViewId==2)>selfSpawns,"new self spawn after real world transition",TimeSpan.FromSeconds(15));
        Check.Equal(14L,await context.Database.LongAsync($"SELECT districtId FROM characters WHERE charId={p.CharId}"),"Return to shipped district");
        Check.Equal("39250 -953 27000",await context.Database.ScalarAsync($"SELECT CONCAT(x,' ',y,' ',z) FROM characters WHERE charId={p.CharId}"),"Exact shipped HDL arrival");
        Check.Equal(0,p.World.Problems.Count,"Medium01 wire framing");
        string path=Path.Combine(Path.GetTempPath(),"mxo-medium01-success-debug.log");
        await File.WriteAllTextAsync(path,context.Server.FullOutput()+"\n--- DebugLog ---\n"+context.Server.DebugLog());
        Console.WriteLine("medium01:592sector140/static1;14/1list;LA/direct80ff/reload/newself;HDL39250/-953/27000. NPC/nav and native UI selection remain partial. Runtime log:"+path);
    }
}
