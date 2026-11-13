using System.Text;
using System.Text.Json;
namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task LaHardlineSynchronize(ScenarioContext context)
    {
        var p=await context.AddPlayerAsync(new CharacterSeed("HlSync")
            {District="medium03",DistrictId=16,X=37700,Y=495,Z=31700,Rotation=192});
        Check.That(!p.CharacterData.ContainsKey(8) || p.CharacterData[8].Length==0,"No fixture visited-row grants");
        await p.EnterWorldAsync();
        await p.WaitUntilAsync(()=>p.ViewsOfGoid(592).Count>0,"Actual source booth",TimeSpan.FromSeconds(15));
        var booth=p.ViewsOfGoid(592).Last();
        p.SendRpc($"80 c7 {Le(booth.ViewId)} {Le((ushort)(booth.SpawnId+1))} 08 00");
        await Task.Delay(250);
        Check.Equal(0,p.Received("80 fa").Count,"Stale spawn cannot synchronize");
        Check.Equal(0L,await context.Database.LongAsync($"SELECT COUNT(*) FROM char_hardlines WHERE char_id={p.CharId}"),"No stale discovery");
        p.SendRpc($"80 c7 {MissionNpcId(booth)} 08 00");
        await p.WaitForRpcAsync("80 fa 08 00 00 00 02 00 00 00 01");
        p.SendRpc($"80 c7 {MissionNpcId(booth)} 08 00");
        await p.WaitForRpcAsync("80 fa 08 00 00 00 02 00 00 00 01");
        Check.Equal("16 2 1",await context.Database.ScalarAsync($"SELECT CONCAT(district_id,' ',hl_id,' ',COUNT(*)) FROM char_hardlines WHERE char_id={p.CharId} GROUP BY district_id,hl_id"),"One persisted internal-map discovery after retry");
        Check.Equal(0,p.Received("81 18").Count,"Synchronize does not upload bag items");
        Check.Equal(0,p.Received("81 07").Count,"Synchronize does not reset the world");
        var syncRpcs=p.World.RpcSnapshot().Select(r=>r.Hex).ToArray();
        p.SendRpc("80 fe 00");await p.WaitForRpcAsync("80 fd");p.Dispose();
        await Task.Delay(TimeSpan.FromSeconds(33)); // ordinary reservation cleanup, no server restart
        using var again=await HeadlessPlayer.LoginAsync(context.Server.Address,"user"+p.CharId,p.Handle);
        Check.That(again.CharacterData.TryGetValue(8,out var cache) && cache.SequenceEqual(Convert.FromHexString("0802000000")),"Margin08 uses persisted logical8/HID2");
        await again.EnterWorldAsync();
        await again.WaitUntilAsync(()=>again.ViewsOfGoid(592).Count>0,"Booth after ordinary relog",TimeSpan.FromSeconds(15));
        booth=again.ViewsOfGoid(592).Last();
        again.SendRpc($"80 c7 {MissionNpcId(booth)} 05 00");await again.WaitForRpcAsync("80 fb 02 00 00 00 01");
        again.SendRpc("80 fc 01");await again.WaitForRpcAsync("80 fd");again.SendRpc("80 fe 01");await again.WaitForRpcAsync("81 07");
        again.SendRpc("81 08");await again.WaitForRpcAsync("81 09");again.SendRpc("05");
        again.SendRpc("6c 08 00 00 00 00 00 00 00");
        await again.WaitForRpcAsync("6d 08 00 00 00 08 00 01 01 00 00 02 00 00 00 00");
        int loads=again.Received("06").Count;
        again.SendRpc("80 ff 08 00 00 00 02 00 00 00");await again.WaitForRpcAsync("81 07");again.SendRpc("81 08");
        await again.WaitUntilAsync(()=>again.Received("06").Count>loads,"Synchronized LA destination loads",TimeSpan.FromSeconds(15));
        again.SendRpc("05");await Task.Delay(300);
        Check.That(Encoding.Latin1.GetString(again.Received("06").Last().Bytes).Contains("medium03/medium03.metr"),"Source filename unchanged");
        Check.Equal("16 37700 495 31700",await context.Database.ScalarAsync($"SELECT CONCAT(districtId,' ',x,' ',y,' ',z) FROM characters WHERE charId={p.CharId}"),"Exact HDL arrival after sync/relog/LA");
        var proof=new {context.Server.SourceAssembly,SourceAssemblySha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(await File.ReadAllBytesAsync(context.Server.SourceAssembly))),
            InitialSyncRpcs=syncRpcs,RelogCache=Convert.ToHexString(cache!),RelogAndLaRpcs=again.World.RpcSnapshot().Select(r=>r.Hex).ToArray(),NativeUiProven=false};
        string path=Path.Combine(Path.GetTempPath(),$"mxo-hardline-sync-{context.Database.Name}.json");
        await File.WriteAllTextAsync(path,JsonSerializer.Serialize(proof,new JsonSerializerOptions{WriteIndented=true}));
        await File.WriteAllTextAsync(path+".log",context.Server.FullOutput()+"\n"+context.Server.DebugLog());
        Console.WriteLine("Hardline sync proof: "+path+"; actual source "+proof.SourceAssembly+" SHA256 "+proof.SourceAssemblySha256);
    }
}
