using hds;

namespace hds.Tests;

public class PvpArchiveTests
{
    [Fact]
    public void BooksUseStoredOrganizationAndInclusiveLevelGates()
    {
        var t = PvpArchives.Find(38538)!;
        Assert.Equal(1, t.Id);
        Assert.Null(PvpArchives.Refusal(t,1,20,1));
        Assert.Null(PvpArchives.Refusal(t,1,30,1));
        Assert.NotNull(PvpArchives.Refusal(t,1,19,1));
        Assert.NotNull(PvpArchives.Refusal(t,1,31,1));
        Assert.NotNull(PvpArchives.Refusal(t,2,25,2));
        Assert.NotNull(PvpArchives.Refusal(t,1,25,2));
        Assert.NotNull(PvpArchives.Refusal(null,1,25,1));
        Assert.Equal(7,PvpArchives.Find(47270)!.Id);
        Assert.Equal(8,PvpArchives.Find(47726)!.Id);
        for (int id=1;id<=8;id++) Assert.Equal((uint)(10+id),PvpArchives.ConstructId(PvpArchives.MapFor(id)));
        Assert.Equal(1u,PvpArchives.ConstructId(1));
        Assert.Equal(901u,PvpArchives.ConstructId(901));
    }

    [Theory]
    [InlineData(0,0xc1f)] [InlineData(117.99,0xc1f)] [InlineData(118,0xc21)]
    [InlineData(163,0xc1d)] [InlineData(174,0xc20)] [InlineData(176,0xc22)]
    [InlineData(178,0xc1e)] [InlineData(180,0xc1f)] [InlineData(358,0xc1e)]
    public void SenderCycleUsesClientStatesAndDurations(double at,uint state) => Assert.Equal(state,PvpArchives.Stage(at));

    [Fact]
    public void AllEightConstructsHaveWalkableArrivalsForEveryOrganization()
    {
        string root=AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root,"data","sector_properties.csv"))) root=Path.GetDirectoryName(root)!;
        SectorProperties.Load(Path.Combine(root,"data","sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root,"data","navmesh"));
        for (ushort map=4;map<=11;map++)
            for (uint org=1;org<=6;org++)
            {
                Assert.True(PvpArchives.Arrival(map,org,out var p),$"map{map}/org{org}");
                Assert.True(NavMesh.Locate(map,p.x,p.y,p.z,out var at),$"map{map}/org{org} polygon");
                Assert.Equal(at.floorY+95,p.y,3);
            }
    }
}
