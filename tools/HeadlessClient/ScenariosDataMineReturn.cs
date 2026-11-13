using System.Text;
namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task DataMineNativeReturnRegression(ScenarioContext context)
    {
        var p = (await context.AddPlayersInWorldAsync(new CharacterSeed("DataMineReturn")
        { Level=25, Organization=1, X=111600, Y=695, Z=-6130, Items=new[]{(3,47270u,1)} }))[0];
        await p.WaitUntilAsync(()=>p.ViewsOfGoid(38282).Any(), "ordinary Zion sender", TimeSpan.FromSeconds(15));
        int citySenderCount = p.ViewsOfGoid(38282).Count;
        p.SendRpc("63 03 6a"); await p.WaitForRpcAsync("65 03 00 6a 00");
        await p.WaitForRpcAsync("81 07", TimeSpan.FromSeconds(185));
        await Load(p,17,"archive_datamine/datamine.metr");
        await p.WaitUntilAsync(()=>p.ViewsOfGoid(38271).Any(), "generated normal-arrival returner", TimeSpan.FromSeconds(20));
        foreach(uint key in new uint[]{0x01b00004,0x00c00005})
        {
            byte[] request = new byte[]{0x80,0xc9}.Concat(BitConverter.GetBytes(key))
                .Concat(BitConverter.GetBytes(0f)).Concat(BitConverter.GetBytes(0f)).Concat(BitConverter.GetBytes(0f)).ToArray();
            p.SendRpc(request);
        }
        p.SendRpc("81 48"); await p.WaitForRpcAsync("81 49");
        Check.Equal(2, context.Server.DebugLog().Split("[REGION] Unavailable source object; request ignored").Length-1,
            "Both exact missing native requests safely refuse without exceptions");
        var fallback=p.ViewsOfGoid(38271).Last(); p.SkipReceived();
        p.SendRpc($"80 c7 {MissionNpcId(fallback)} 02 00"); await p.WaitForRpcAsync("81 07");
        await Load(p,1,"slums_barrens_full.metr");
        await p.WaitUntilAsync(()=>p.ViewsOfGoid(38282).Count>citySenderCount, "city sender rebuilt after real reset", TimeSpan.FromSeconds(15));
        Check.Equal("1 111600 695 -6130", await context.Database.ScalarAsync($"SELECT CONCAT(districtId,' ',x,' ',y,' ',z) FROM characters WHERE charId={p.CharId}"), "Exact normal origin restored");
        Check.Equal(1L, await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND goid=47270 AND slot=106"), "Book retained equipped after return");
        p.SendRpc("80 fe 00"); await p.WaitForRpcAsync("80 fd"); p.Dispose();
        await Task.Delay(TimeSpan.FromSeconds(33));
        using var again=await HeadlessPlayer.LoginAsync(context.Server.Address,"user"+p.CharId,p.Handle);
        await again.EnterWorldAsync();
        await again.WaitUntilAsync(()=>again.ViewsOfGoid(38282).Any(), "sender after ordinary relog", TimeSpan.FromSeconds(15));
        Check.Equal(1L, await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND goid=47270 AND slot=106"), "Persisted equipped book after relog");
        await again.WaitForRpcAsync("81 07", TimeSpan.FromSeconds(185));
        await Load(again,17,"archive_datamine/datamine.metr");
        Check.Equal(7L, await context.Database.LongAsync($"SELECT districtId FROM characters WHERE charId={p.CharId}"), "Normal sender refires after same-server relog");
        Check.That(p.World.Problems.Count==0,"First session parser: "+string.Join("; ",p.World.Problems));
        Check.That(again.World.Problems.Count==0,"Relog parser: "+string.Join("; ",again.World.Problems));
        await File.WriteAllTextAsync(Path.Combine(Path.GetTempPath(),"mxo-datamine-native-return-success.log"),context.Server.FullOutput()+"\n--- DebugLog ---\n"+context.Server.DebugLog());
        Console.WriteLine("  DataMine native-failure regression: both missing80c9 keys safely refuse; normal fallbackUse/fullcityreset/exactorigin/book106/ordinaryrelog/normal180scycle refire. Native client crash/render remain pending.");
        static async Task Load(HeadlessPlayer actor,uint world,string resource)
        {
            actor.SendRpc("81 08"); await actor.WaitForRpcAsync("81 08");
            var load=await actor.WaitForRpcAsync("06");
            Check.Equal(world,BitConverter.ToUInt32(load.Bytes,3),"Actual loaded world");
            Check.That(Encoding.Latin1.GetString(load.Bytes).Contains(resource),"Actual world resource");
            actor.SendRpc("05");
        }
    }
}
