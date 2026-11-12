using hds;
using System.Globalization;
namespace hds.Tests;

public class Small03Tests
{
    private static string Root
    {
        get { string p=AppContext.BaseDirectory; while(!File.Exists(Path.Combine(p,"data","sector_properties.csv"))) p=Path.GetDirectoryName(p)!; return p; }
    }
    [Fact]
    public void OnlyUnambiguousNativeKeysAreExported()
    {
        var rows=File.ReadLines(Path.Combine(Root,"data","staticObjects_small03.csv")).Skip(1).Select(l=>l.Split(',')).ToArray();
        Assert.Equal(358,rows.Length);
        Assert.Equal(358,rows.Select(r=>r[2]).Distinct().Count());
        Assert.All(rows,r=>Assert.Equal("17",r[0]));
        Assert.DoesNotContain(rows,r=>r[1]=="13" && new[]{"0d000000","0f000000","10000000"}.Contains(r[3]));
        var booth=Assert.Single(rows,r=>r[4]=="50020000");
        Assert.Equal("0d005000",booth[2]);
        Assert.Equal(new[]{"17700.0","-453.0","34200.0"},booth[6..9]);
        Assert.Equal(6,rows.Count(r=>r[4]=="281b0000"));
        Assert.Single(rows,r=>r[4]=="53010000"); // separate SIDG designer layer, Pidgey Flock339
    }
    [Fact]
    public void UnnamedShippedHardlineIsCoveredWithoutEnablingOtherConstructs()
    {
        HardlineNetwork.Load(Path.Combine(Root,"data","hardline_network.csv"));
        Assert.True(HardlineNetwork.Covers(17));
        Assert.False(HardlineNetwork.Covers(14));
        Assert.Equal(new ushort[]{1},HardlineNetwork.Ids(17));
        Assert.Equal((ushort)1,HardlineNetwork.Nearest(17,17700,34200));
        var line=HardlineNetwork.Get(17,1);
        Assert.Equal(17700,line.spotX); Assert.Equal(-503,line.spotY); Assert.Equal(34100,line.spotZ);
    }
    [Fact]
    public void ShippedHardlineArrivalAndInteriorFloorHaveRealNavigation()
    {
        SectorProperties.Load(Path.Combine(Root,"data","sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(Root,"data","navmesh"));
        Assert.True(NavMesh.Locate(17,17700,-503,34100,out NavLocation booth));
        Assert.Equal(5,booth.sector); Assert.False(booth.indoors);
        Assert.True(NavMesh.Locate(17,21300,1695,39100,out NavLocation interior));
        Assert.Equal(13,interior.sector); Assert.True(interior.indoors);
    }
}
