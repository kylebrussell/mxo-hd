using hds;
using System.Text.Json;
namespace hds.Tests;
public class Medium01Tests
{
    private static string Root {get {var p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds.sln")))p=Path.GetDirectoryName(p)!;return p;}}
    [Fact]
    public void AllThirtyNineKeysAreUniqueUnrotatedAndOnlyTheTwoProvenObjectKinds()
    {
        using var audit=JsonDocument.Parse(File.ReadAllText(Path.Combine(Root,"research","world-data","medium01-static-audit.json")));
        var a=audit.RootElement;Assert.Equal(39,a.GetProperty("rawReferences").GetInt32());Assert.Equal(39,a.GetProperty("uniqueKeys").GetInt32());
        Assert.Equal(39,a.GetProperty("exportedKeys").GetInt32());Assert.Empty(a.GetProperty("excludedKeys").EnumerateArray());Assert.Empty(a.GetProperty("conflicts").EnumerateObject());
        Assert.All(a.GetProperty("raw").EnumerateArray(),r=>{Assert.Equal(0,r[5].GetInt32());Assert.Equal("ext",r[6].GetString());});
        var rows=File.ReadLines(Path.Combine(Root,"data","staticObjects_medium01.csv")).Skip(1).Select(l=>l.Split(',')).ToArray();
        Assert.Equal(39,rows.Length);Assert.Equal(39,rows.Select(r=>r[2]).Distinct().Count());Assert.All(rows,r=>Assert.Equal("14",r[0]));
        Assert.Equal(38,rows.Count(r=>r[4]=="c3950000"));var booth=Assert.Single(rows,r=>r[4]=="50020000");
        Assert.Equal("0100c008",booth[2]);Assert.Equal(new[]{"39350.0","-903.0","27000.0"},booth[6..9]);
        Assert.Equal((byte)5,ObjectStates.UseNumber(592,"Loading Area"));
        Assert.Null(ObjectStates.UseNumber(38339,"Open"));
    }
    [Fact]
    public void ShippedHardlineHasNavigationButCapturedNpcFootprintIsNotAValidFloorClaim()
    {
        HardlineNetwork.Load(Path.Combine(Root,"data","hardline_network.csv"));SectorProperties.Load(Path.Combine(Root,"data","sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(Root,"data","navmesh"));
        Assert.True(HardlineNetwork.Covers(14));Assert.True(HardlineNetwork.Covers(12));Assert.True(HardlineNetwork.Covers(17));Assert.False(HardlineNetwork.Covers(99));
        Assert.Equal(new ushort[]{1},HardlineNetwork.Ids(14));var h=HardlineNetwork.Get(14,1);
        Assert.Equal(39250,h.spotX);Assert.Equal(-953,h.spotY);Assert.Equal(27000,h.spotZ);
        Assert.True(NavMesh.Locate(14,h.spotX,h.spotY,h.spotZ,out var arrival));Assert.Equal(140,arrival.sector);Assert.False(arrival.indoors);Assert.Equal(-1050,arrival.floorY,2);
        var npcs=File.ReadLines(Path.Combine(Root,"data","npc_spawns","Instance.csv")).Skip(1).Select(l=>l.Split(';')).Where(r=>r[0]=="14").ToArray();Assert.Equal(12,npcs.Length);
        foreach(var npc in npcs){double x=double.Parse(npc[7]),y=double.Parse(npc[8]),z=double.Parse(npc[9]);Assert.NotNull(SectorProperties.At(14,x,z));Assert.False(NavMesh.Locate(14,x,y,z,out _));}
    }
    [Fact]
    public void NativeNavPackContainsAllOneHundredFortyFourSectorsAndNoPrefabAssumptions()
    {
        using var file=File.OpenRead(Path.Combine(Root,"data","navmesh","14.bin.gz"));
        using var gzip=new System.IO.Compression.GZipStream(file,System.IO.Compression.CompressionMode.Decompress);
        using var memory=new MemoryStream();gzip.CopyTo(memory);var bytes=memory.ToArray();
        Assert.Equal(785872,bytes.Length);Assert.Equal("MXNV",System.Text.Encoding.ASCII.GetString(bytes,0,4));
        Assert.Equal(3,BitConverter.ToUInt16(bytes,4));Assert.Equal(14,BitConverter.ToUInt16(bytes,6));
        Assert.Equal(0u,BitConverter.ToUInt32(bytes,8));Assert.Equal(144u,BitConverter.ToUInt32(bytes,12));
    }
    [Fact]
    public void DiagnosticModeHasNoDevelopmentOrRandomPrivileges()
    {
        var p=new NativeQa.Modes.Medium01Mode().Plan;Assert.False(p.DevCommands);Assert.False(p.GmCharacter110);Assert.False(p.FixedCallbackRolls);
        Assert.Contains("districtId=14",Assert.Single(p.AfterLoadout));Assert.Equal(39250,p.X);Assert.Equal(27000,p.Z);
        Assert.Equal("",NativeQa.NativeQaFixturePlan.EnvironmentFor("medium01")["MXO_MOBS"]);
        HardlineNetwork.Load(Path.Combine(Root,"data","hardline_network.csv"));
        var source=HardlineNetwork.Get(14,1);int steps=(int)Math.Round(source.yaw*256/(2*Math.PI));
        byte yaw=(byte)((steps%256+256)%256);Assert.Equal((byte)192,yaw);
        Assert.Contains("rotation="+yaw,Assert.Single(p.AfterLoadout));
    }
}
