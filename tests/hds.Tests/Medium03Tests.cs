using hds;
using System.Text.Json;
namespace hds.Tests;
public class Medium03Tests
{
    private static string Root { get { var p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds.sln")))p=Path.GetDirectoryName(p)!;return p;} }
    [Fact]
    public void EveryVariantOfUnprovenOrConflictingKeysIsExcluded()
    {
        using var audit=JsonDocument.Parse(File.ReadAllText(Path.Combine(Root,"research","world-data","medium03-static-audit.json")));
        var a=audit.RootElement;
        using var resources=JsonDocument.Parse(File.ReadAllText(Path.Combine(Root,"research","world-data","medium03-resources.json")));
        Assert.Equal(60,resources.RootElement.GetProperty("navSectorCount").GetInt32());Assert.Equal(2,resources.RootElement.GetProperty("hardlineHid").GetInt32());
        Assert.Equal(0,resources.RootElement.GetProperty("subwayCnetCount").GetInt32());
        Assert.Equal(4762,a.GetProperty("rawReferences").GetInt32());Assert.Equal(4625,a.GetProperty("uniqueKeys").GetInt32());
        Assert.Equal(1282,a.GetProperty("exportedKeys").GetInt32());Assert.Equal(3321,a.GetProperty("unprovenRotationKeys").GetArrayLength());
        Assert.Equal(134,a.GetProperty("conflicts").EnumerateObject().Count());Assert.Equal(180,a.GetProperty("sourceSha256").EnumerateObject().Count());
        var excluded=a.GetProperty("excludedKeys").EnumerateArray().Select(k=>(k[0].GetInt32(),k[1].GetInt32())).ToHashSet();
        Assert.Equal(3343,excluded.Count);
        var rows=File.ReadLines(Path.Combine(Root,"data","staticObjects_medium03.csv")).Skip(1).Select(l=>l.Split(',')).ToArray();
        Assert.Equal(1282,rows.Length);Assert.Equal(1282,rows.Select(r=>r[2]).Distinct().Count());
        var keys=rows.Select(r=>(int.Parse(r[1]),(int)BitConverter.ToUInt32(Convert.FromHexString(r[3])))).ToHashSet();
        Assert.Empty(keys.Intersect(excluded));Assert.All(rows,r=>Assert.Equal("16",r[0]));
        foreach(var raw in a.GetProperty("raw").EnumerateArray())
            if(raw[5].GetInt32()!=0)Assert.Contains((raw[0].GetInt32(),raw[1].GetInt32()),excluded);
        foreach(var conflict in a.GetProperty("conflicts").EnumerateObject())
            foreach(var variant in conflict.Value.EnumerateArray())Assert.Contains((variant[0].GetInt32(),variant[1].GetInt32()),excluded);
        var booth=Assert.Single(rows,r=>r[4]=="50020000");
        Assert.Equal("01004003",booth[2]);Assert.Equal(new[]{"37800.0","545.0","31700.0"},booth[6..9]);
        Assert.Equal(18,rows.Count(r=>r[4]=="281b0000"));
    }
    [Fact]
    public void ShippedHid2ArrivalAndSafeDoorApproachHaveExactFloors()
    {
        HardlineNetwork.Load(Path.Combine(Root,"data","hardline_network.csv"));
        SectorProperties.Load(Path.Combine(Root,"data","sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(Root,"data","navmesh"));
        Assert.True(HardlineNetwork.Covers(16));Assert.Equal(new ushort[]{2},HardlineNetwork.Ids(16));var h=HardlineNetwork.Get(16,2);
        Assert.Equal(37700,h.spotX);Assert.Equal(495,h.spotY);Assert.Equal(31700,h.spotZ);
        Assert.True(NavMesh.Locate(16,h.spotX,h.spotY,h.spotZ,out var arrival));Assert.Equal(52,arrival.sector);Assert.Equal(7,arrival.region);Assert.Equal(4,arrival.polygon);Assert.Equal(400,arrival.floorY);Assert.False(arrival.indoors);
        Assert.True(NavMesh.Locate(16,37700,495,31600,out var beside));Assert.Equal(400,beside.floorY);
        Assert.True(NavMesh.Locate(16,27100,1695,41100,out var door));Assert.Equal(33,door.sector);Assert.True(door.indoors);Assert.Equal(1600,door.floorY);
        // Existing captures are retained, not moved into this world's sector footprint.
        // Header-aware check below avoids treating a capture outside all sectors as a usable population.
        var lines=File.ReadAllLines(Path.Combine(Root,"data","npc_spawns","Instance.csv"));var header=lines[0].Split(';');
        int district=Array.IndexOf(header,"District"),x=Array.IndexOf(header,"X"),z=Array.IndexOf(header,"Z");
        var npcs=lines.Skip(1).Select(l=>l.Split(';')).Where(r=>r[district]=="16").ToArray();Assert.Equal(58,npcs.Length);
        Assert.All(npcs,r=>Assert.Null(SectorProperties.At(16,double.Parse(r[x],System.Globalization.CultureInfo.InvariantCulture),double.Parse(r[z],System.Globalization.CultureInfo.InvariantCulture))));
    }
    [Fact]
    public void FixtureUsesSourceDerivedByteYawRatherThanRadianDatabaseSeed()
    {
        HardlineNetwork.Load(Path.Combine(Root,"data","hardline_network.csv"));
        double yaw=HardlineNetwork.Get(16,2).yaw;
        int sourceByte=(int)Math.Round(yaw*128/Math.PI)&255;
        Assert.Equal(192,sourceByte);
        Assert.Equal(0d,new HeadlessClient.CharacterSeed("DefaultHeading").Rotation);
        string sql=Assert.Single(new NativeQa.Modes.Medium03Mode().Plan.AfterLoadout);
        var value=System.Text.RegularExpressions.Regex.Match(sql,@"rotation\s*=\s*(-?\d+(?:\.\d+)?)").Groups[1].Value;
        decimal stored=decimal.Parse(value,System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(sourceByte,stored);Assert.InRange(stored,0m,255m);Assert.Equal((byte)192,(byte)stored);
        // Audit every discovered seed literal: SQL character rotation stores a byte, never raw HDL radians.
        foreach(string name in NativeQa.NativeQaModes.Names)
            foreach(string statement in NativeQa.NativeQaModes.Find(name).Plan.BeforeLoadout.Concat(NativeQa.NativeQaModes.Find(name).Plan.AfterLoadout).Concat(NativeQa.NativeQaModes.Find(name).Plan.AfterHelpers))
                foreach(System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(statement,@"(?<![A-Za-z])rotation\s*=\s*(-?\d+(?:\.\d+)?)"))
                {decimal v=decimal.Parse(m.Groups[1].Value,System.Globalization.CultureInfo.InvariantCulture);Assert.InRange(v,0m,255m);Assert.Equal(decimal.Truncate(v),v);}
    }
    [Fact]
    public void ModeHasNoDevelopmentOrRandomAuthority()
    {
        var p=new NativeQa.Modes.Medium03Mode().Plan;
        Assert.False(p.DevCommands);Assert.False(p.GmCharacter110);Assert.False(p.FixedCallbackRolls);
        Assert.Contains("districtId=16",Assert.Single(p.AfterLoadout));
        var e=NativeQa.NativeQaFixturePlan.EnvironmentFor("medium03");Assert.Equal("",e["MXO_MOBS"]);Assert.Equal("",e["MXO_CALLBACK_QA_ROLL100"]);
    }
}
