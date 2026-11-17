using System.Globalization;
using hds;
namespace hds.Tests;
public class MissionDuplicateCarLookupTests
{
    static string Repo {get{string p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds.sln")))p=Path.GetDirectoryName(p)!;return p;}}
    [Theory][InlineData("67059029")][InlineData("37059029")]
    public void DuplicateCatalogKeyUsesTheActiveSitesOriginalCarWithoutChangingEitherSource(string key)
    {
        var rows=File.ReadLines(Path.Combine(Repo,"data/staticObjects_slums_1.csv")).Skip(1)
            .Select(l=>l.Split(',')).Where(f=>f.Length>=11&&f[2]==key).Select(f=>new StaticWorldObject{
                metrId=ushort.Parse(f[0]),sectorID=ushort.Parse(f[1]),mxoStaticId=BitConverter.ToUInt32(Convert.FromHexString(f[2])),
                type=Convert.FromHexString(f[4][..4]),pos_x=double.Parse(f[6],CultureInfo.InvariantCulture),
                pos_y=double.Parse(f[7],CultureInfo.InvariantCulture),pos_z=double.Parse(f[8],CultureInfo.InvariantCulture),quat=f[10]}).ToArray();
        Assert.Equal(417,MissionSites.Goid(rows[0]));var selected=rows.Single(r=>MissionSites.Goid(r)==419);
        var first=rows[0];double originalX=first.pos_x;
        var progress=new MissionProgress(new Mission{id=1,district=1}){site=new MissionSite{metrId=1,lobbyDoor=selected}};
        Assert.Same(selected,MissionElevatorPoses.RequestedRawDoor(progress,first));
        Assert.Equal(originalX,first.pos_x);Assert.Equal(417,MissionSites.Goid(first));
        progress.sites.Add(progress.site);progress.site=new MissionSite{metrId=1};
        Assert.Same(selected,MissionElevatorPoses.RequestedRawDoor(progress,first));
        progress.sites.Clear();
        progress.site=null;Assert.Same(first,MissionElevatorPoses.RequestedRawDoor(progress,first));
        progress.site=new MissionSite{metrId=2,lobbyDoor=selected};progress.mission.district=2;
        Assert.Same(first,MissionElevatorPoses.RequestedRawDoor(progress,first));
        Assert.Same(first,MissionElevatorPoses.RequestedRawDoor(null!,first));
    }
}
