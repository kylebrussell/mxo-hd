using System.Text;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task LaHardlineRegistration(ScenarioContext context) => await LaHardlineRegistrationCase(context,false);
    private static async Task LaHardlineDiscovery(ScenarioContext context) => await LaHardlineRegistrationCase(context,true);
    private static async Task LaHardlineRegistrationCase(ScenarioContext context,bool discovery)
    {
        string config=Path.Combine(context.RepositoryRoot,"WorldConfig.xml.dist");
        string configHash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(await File.ReadAllBytesAsync(config)));
        var p=await context.AddPlayerAsync(new CharacterSeed("LaRegistry"){District="medium03",DistrictId=16,X=37700,Y=495,Z=31700,Rotation=192});
        byte[] login=p.MarginLoginReply;
        // Independent wire decoding; do NOT use production registry or builder for expected values.
        Check.Equal((byte)9,login[0],"Margin09");int off=BitConverter.ToUInt16(login,13),count=BitConverter.ToUInt16(login,off);
        Check.Equal(15,off,"Native absolute array offset");Check.Equal(9,count,"Advertised filenames");
        Check.That(new ushort[]{12,7,169,379,384,377,382,383,381}.SequenceEqual(Enumerable.Range(0,count).Select(i=>BitConverter.ToUInt16(login,off+2+2*i))),"Ordered Universe IDs");
        var cache=p.CharacterData.TryGetValue(8,out byte[]? block)?block:Array.Empty<byte>();Check.Equal(0,cache.Length%5,"Known cache five-byte records");
        var keys=Enumerable.Range(0,cache.Length/5).Select(i=>(logical:cache[5*i],hid:BitConverter.ToUInt32(cache,5*i+1))).ToArray();
        Check.That(keys.All(k=>k.logical>=1 && k.logical<=9),"No internal map IDs in cache");
        Check.Equal(!discovery,keys.Contains(((byte)8,2u)),"Cache respects existing discovery policy");
        await p.EnterWorldAsync();await p.WaitUntilAsync(()=>p.ViewsOfGoid(592).Count>0,"Source medium03 booth",TimeSpan.FromSeconds(20));
        var booth=p.ViewsOfGoid(592).Last();p.SendRpc($"80 c7 {MissionNpcId(booth)} 05 00");await p.WaitForRpcAsync("80 fb 02 00 00 00 01");
        p.SendRpc("80 fc 01");await p.WaitForRpcAsync("80 fd");p.SendRpc("80 fe 01");await p.WaitForRpcAsync("81 07");
        p.SendRpc("81 08");await p.WaitForRpcAsync("81 09");p.SendRpc("05");await Task.Delay(300);
        p.SendRpc("6c 08 00 00 00 00 00 00 00");
        await p.WaitForRpcAsync(discovery?"6d 08 00 00 00 08 00 01 00 00":"6d 08 00 00 00 08 00 01 01 00 00 02 00 00 00 00");
        int resets=p.Received("81 07").Count;
        foreach(uint bad in new uint[]{0,10,16,65544,uint.MaxValue})p.SendRpc($"80 ff {Le(bad)} {Le(2u)}");
        p.SendRpc($"80 ff {Le(8u)} {Le(65538u)}");
        await Task.Delay(250);Check.Equal(resets,p.Received("81 07").Count,"Invalid logical/HID requests cannot reset or warp");
        if(discovery){p.SendRpc($"80 ff {Le(8u)} {Le(2u)}");await Task.Delay(300);Check.Equal(16L,await context.Database.LongAsync($"SELECT districtId FROM characters WHERE charId={p.CharId}"),"Unuploaded LA destination cannot change saved world");
            // QA-only known rows; production authority still uses normal uploads.
            await context.Database.ExecuteAsync($"INSERT INTO char_hardlines(char_id,district_id,hl_id,added) VALUES({p.CharId},16,2,NOW())");}
        int loads=p.Received("06").Count;
        p.SendRpc($"80 ff {Le(8u)} {Le(2u)}");await p.WaitForRpcAsync("81 07");p.SendRpc("81 08");
        await p.WaitUntilAsync(()=>p.Received("06").Count>loads,"Exact medium03 reload",TimeSpan.FromSeconds(15));p.SendRpc("05");await Task.Delay(400);
        Check.That(Encoding.Latin1.GetString(p.Received("06").Last().Bytes).Contains("medium03/medium03.metr"),"06 filename unchanged");
        Check.Equal("16 37700 495 31700",await context.Database.ScalarAsync($"SELECT CONCAT(districtId,' ',x,' ',y,' ',z) FROM characters WHERE charId={p.CharId}"),"Exact shipped HDL16/2");
        p.SendRpc($"80 ff {Le(9u)} {Le(1u)}"); // stale LA selection while now on world: refused.
        p.SendRpc($"81 8e {Le(2u)} {Le(65544u)} {Le(1u)} {Le(9u)}");
        p.SendRpc($"81 8e {Le(65538u)} {Le(8u)} {Le(1u)} {Le(9u)}");
        await Task.Delay(200);Check.Equal(16L,await context.Database.LongAsync($"SELECT districtId FROM characters WHERE charId={p.CharId}"),"Malformed source/LA replay cannot warp");
        if(discovery)
        {
            p.SendRpc($"81 8e {Le(2u)} {Le(8u)} {Le(1u)} {Le(9u)}");await Task.Delay(250);
            Check.Equal(16L,await context.Database.LongAsync($"SELECT districtId FROM characters WHERE charId={p.CharId}"),"Direct destination requires uploaded row");
            await context.Database.ExecuteAsync($"INSERT INTO char_hardlines(char_id,district_id,hl_id,added) VALUES({p.CharId},17,1,NOW())");
        }
        p.SendRpc($"81 8e {Le(2u)} {Le(8u)} {Le(1u)} {Le(9u)}");await p.WaitForRpcAsync("81 07");
        Check.Equal(17L,await context.Database.LongAsync($"SELECT districtId FROM characters WHERE charId={p.CharId}"),"Direct hardline translates destination9 to map17");
        Check.Equal(configHash,Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(await File.ReadAllBytesAsync(config))),"Source config bytes untouched");
        await File.WriteAllTextAsync(Path.Combine(Path.GetTempPath(),discovery?"mxo-la-discovery-success.log":"mxo-la-registration-success.log"),context.Server.FullOutput()+"\n"+context.Server.DebugLog());
    }
}
