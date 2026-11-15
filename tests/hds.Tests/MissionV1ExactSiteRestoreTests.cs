using System.Globalization;
using System.Text.Json;
using System.Xml;
using hds;
namespace hds.Tests;
public class MissionV1ExactSiteRestoreTests
{
    // Earned101210/0 before the ordinary carry logout in retained86da; no runtime fixture injection.
    const string Captured=@"{""Version"":1,""Identity"":""6b8508afbfd84e948b54e9ebcee7d9ea"",""Mission"":101210,""Instance"":49,""Started"":1794759147,""Phase"":0,""Kills"":0,""Flags"":{""waitingForExit"":false,""entered"":true,""failed"":false,""inventoryFullNotice"":false,""completionNotice"":false,""entryItemExchanged"":false,""goalsCompleteSent"":false,""leaveNotNeeded"":false},""Done"":[1],""EverDone"":[1],""Fired"":[],""Arrived"":[],""Killed"":[],""Following"":[1],""Waves"":[],""PendingNpcs"":[],""PendingWaves"":[],""JustArrived"":[],""WavesJustArrived"":[],""JustSwitched"":[],""Sides"":{},""Items"":{},""Reserved"":{},""ReservedWaves"":{},""NpcElapsed"":{},""WaveElapsed"":{},""GoalElapsed"":{},""Waypoints"":[{""x"":54200,""y"":2945,""z"":-16700,""doorHex"":""28076050""}],""Exits"":[[54200,145,-16700]],""SiteDoors"":[{""exterior"":false,""pos_x"":54200,""pos_y"":2945,""pos_z"":-16700,""rot"":-90.9999883795,""quat"":""00000000f20435bf00000000f304353f"",""sectorID"":1286,""metrId"":1,""mxoStaticId"":1348470568,""staticId"":1832,""type"":""owE="",""strType"":null}],""Objects"":[],""Mobs"":[{""Identity"":""c6bc49080464484e9f1384f43499836e"",""RewardExperience"":0,""Lootable"":false,""Loot"":null,""Npc"":1,""Phase"":0,""Name"":""Maynard Berman"",""Rsi"":""35000024"",""Level"":50,""Health"":2150,""MaxHealth"":2150,""Weapon"":0,""Rotation"":0,""X"":52550,""Y"":2895,""Z"":-15700,""Dead"":false,""ScriptedCorpse"":false,""Aggro"":0,""Friendly"":true,""Hostile"":false,""Giver"":false,""Taker"":false,""Wave"":null},{""Identity"":""33ecb71379af466b867e833988151a6b"",""RewardExperience"":0,""Lootable"":false,""Loot"":null,""Npc"":2,""Phase"":0,""Name"":""Darren Allen"",""Rsi"":""38000024"",""Level"":50,""Health"":2150,""MaxHealth"":2150,""Weapon"":0,""Rotation"":0,""X"":52550,""Y"":2895,""Z"":-16700,""Dead"":false,""ScriptedCorpse"":false,""Aggro"":0,""Friendly"":true,""Hostile"":false,""Giver"":false,""Taker"":false,""Wave"":null}]}";
    static readonly JsonSerializerOptions Fields=new(){IncludeFields=true};
    static string Repo {get{string p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds.sln")))p=Path.GetDirectoryName(p)!;return p;}}
    static MissionProgress.SavedState Saved()=>JsonSerializer.Deserialize<MissionProgress.SavedState>(Captured,Fields)!;
    static StaticWorldObject Copy(StaticWorldObject d)=>JsonSerializer.Deserialize<StaticWorldObject>(JsonSerializer.Serialize(d,Fields),Fields)!;
    static Mission Definition(){var xml=new XmlDocument();xml.Load(Path.Combine(Repo,"data/missions/zion_1_2_10_interview_with_a_bluepill.xml"));return MissionXml.Parse(xml.DocumentElement!);}
    static readonly Lazy<List<MissionSite>> Catalog=new(()=>
    {
        var rows=new List<StaticWorldObject>();
        foreach(string file in new[]{"staticObjects_slums_1.csv","staticObjects_slums_2.csv"})
        foreach(string line in File.ReadLines(Path.Combine(Repo,"data",file)).Where(l=>l.StartsWith("1,1286,",StringComparison.Ordinal)))
        {
            var f=line.Split(',');double D(int i)=>double.Parse(f[i],CultureInfo.InvariantCulture);
            rows.Add(new(){metrId=ushort.Parse(f[0]),sectorID=ushort.Parse(f[1]),mxoStaticId=BitConverter.ToUInt32(Convert.FromHexString(f[2])),staticId=BitConverter.ToUInt32(Convert.FromHexString(f[3])),
                type=Convert.FromHexString(f[4][..4]),exterior=bool.Parse(f[5]),pos_x=D(6),pos_y=D(7),pos_z=D(8),rot=D(9),quat=f[10]});
        }
        return MissionSites.Build(rows,1);
    });
    static bool Exact(StaticWorldObject a,StaticWorldObject b)=>MissionOpenFloorSite.SpotFingerprint(a)==MissionOpenFloorSite.SpotFingerprint(b);
    [Theory][InlineData(false)][InlineData(true)]
    public void EarnedV1CarryRestoreKeepsTheCapturedDoorRegardlessOfAliasOrder(bool reverse)
    {
        var saved=Saved();var door=Assert.Single(saved.SiteDoors);Assert.Equal(0x50600728u,door.mxoStaticId);
        Assert.Equal((54200d,2945d,-16700d),(door.pos_x,door.pos_y,door.pos_z));Assert.Equal((ushort)419,MissionSites.Goid(door));
        var aliases=Catalog.Value.Where(s=>s.elevatorDoor?.mxoStaticId==door.mxoStaticId).ToList();
        Assert.Contains(aliases,s=>s.elevatorDoor.pos_x==53200&&s.elevatorDoor.pos_z==-17700);
        Assert.Contains(aliases,s=>Exact(s.elevatorDoor,door));if(reverse)aliases.Reverse();
        string input=JsonSerializer.Serialize(saved,Fields);var expected=aliases.First(s=>Exact(s.elevatorDoor,door));
        var restored=MissionProgress.RestoreFromCatalog(Definition(),saved,aliases);
        Assert.Same(expected,restored.site);Assert.Same(expected,Assert.Single(restored.sites));
        var output=restored.Capture(saved.Instance,Array.Empty<Mob>());
        Assert.True(Exact(door,Assert.Single(output.SiteDoors)));Assert.Equal(JsonSerializer.Serialize(saved.Waypoints,Fields),JsonSerializer.Serialize(output.Waypoints,Fields));
        Assert.Equal(JsonSerializer.Serialize(saved.Exits,Fields),JsonSerializer.Serialize(output.Exits,Fields));
        Assert.Equal(saved.Identity,output.Identity);Assert.Equal(saved.Started,output.Started);Assert.Equal(saved.Instance,output.Instance);Assert.Equal(saved.Phase,output.Phase);
        Assert.Equal(saved.Done,output.Done);Assert.Equal(saved.EverDone,output.EverDone);Assert.Equal(saved.Following,output.Following);Assert.Equal(saved.Items,output.Items);
        Assert.Equal(input,JsonSerializer.Serialize(saved,Fields)); // Actor identities and captured raw state are not rewritten.
        Assert.Equal(new[]{"c6bc49080464484e9f1384f43499836e","33ecb71379af466b867e833988151a6b"},saved.Mobs.Select(m=>m.Identity));
    }
    [Theory]
    [InlineData("world")][InlineData("sector")][InlineData("key")][InlineData("goid")][InlineData("static")][InlineData("exterior")]
    [InlineData("x")][InlineData("y")][InlineData("z")][InlineData("rotation")][InlineData("quaternion")]
    public void EveryCapturedRawIdentityFieldMustMatchWithoutPoseFallback(string change)
    {
        var door=Saved().SiteDoors.Single();var wrong=Copy(door);
        switch(change)
        {
            case "world":wrong.metrId++;break;case "sector":wrong.sectorID++;break;case "key":wrong.mxoStaticId++;break;
            case "goid":wrong.type=BitConverter.GetBytes((ushort)417);break;case "static":wrong.staticId++;break;case "exterior":wrong.exterior=!wrong.exterior;break;
            case "x":wrong.pos_x++;break;case "y":wrong.pos_y++;break;case "z":wrong.pos_z++;break;case "rotation":wrong.rot++;break;
            case "quaternion":wrong.quat="0000000000000000000000000000803f";break;
        }
        var other=new MissionSite{metrId=wrong.metrId,elevatorDoor=wrong};Assert.Null(MissionProgress.FindSavedSite(new[]{other},door));
        var exact=new MissionSite{metrId=door.metrId,elevatorDoor=Copy(door)};
        Assert.Same(exact,MissionProgress.FindSavedSite(new[]{other,exact},door));Assert.Same(exact,MissionProgress.FindSavedSite(new[]{exact,other},door));
    }
    [Fact] public void MissingExactCatalogRowDoesNotSelectTheSameKeyAtAnotherPose()
    {
        var saved=Saved();var door=saved.SiteDoors.Single();var aliases=Catalog.Value.Where(s=>s.elevatorDoor?.mxoStaticId==door.mxoStaticId&&!Exact(s.elevatorDoor,door)).ToArray();Assert.NotEmpty(aliases);
        Assert.Null(MissionProgress.FindSavedSite(aliases,door));var restored=MissionProgress.RestoreFromCatalog(Definition(),saved,aliases);
        Assert.Null(restored.site);Assert.Empty(restored.sites);Assert.Equal(54200,restored.waypoints.Single().x);Assert.Equal(54200,restored.exits.Single()[0]);
        Assert.Equal(new ushort[]{1},restored.following);Assert.Equal(new[]{1},restored.Capture(saved.Instance,Array.Empty<Mob>()).Done);
    }
    [Fact] public void IdenticalRawDoorClustersRetainTheExistingLegacyFirstOrder()
    {
        var door=Saved().SiteDoors.Single();var first=new MissionSite{elevatorDoor=Copy(door),minX=1};var second=new MissionSite{elevatorDoor=Copy(door),minX=2};
        Assert.Same(first,MissionProgress.FindSavedSite(new[]{first,second},door));Assert.Same(second,MissionProgress.FindSavedSite(new[]{second,first},door));
        Assert.Null(MissionProgress.FindSavedSite(new[]{first},null));
    }
}
