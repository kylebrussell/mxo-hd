using hds;
using System.Text.Json;
namespace hds.Tests;
public class DataMineWorldTests
{
    private static string Root { get {var p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds.sln")))p=Path.GetDirectoryName(p)!;return p;} }
    private static StaticWorldObject[] Objects()=>File.ReadLines(Path.Combine(Root,"data/staticObjects_datamine.csv")).Skip(1).Select(l=>l.Split(',')).Select(r=>new StaticWorldObject{
        metrId=ushort.Parse(r[0]),sectorID=ushort.Parse(r[1]),mxoStaticId=BitConverter.ToUInt32(Convert.FromHexString(r[2])),staticId=BitConverter.ToUInt32(Convert.FromHexString(r[3])),type=Convert.FromHexString(r[4]),exterior=bool.Parse(r[5]),pos_x=double.Parse(r[6],System.Globalization.CultureInfo.InvariantCulture),pos_y=double.Parse(r[7],System.Globalization.CultureInfo.InvariantCulture),pos_z=double.Parse(r[8],System.Globalization.CultureInfo.InvariantCulture),quat=r[10]}).ToArray();
    private static void Nav(){SectorProperties.Load(Path.Combine(Root,"data/sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(Root,"data/navmesh"));}
    [Fact]public void KnownStaticsExcludeEveryConflictAndWholeMissingSectorAfterNativeRollout()
    {
        using var d=JsonDocument.Parse(File.ReadAllText(Path.Combine(Root,"research/world-data/datamine-static-audit.json")));var a=d.RootElement;
        Assert.Equal(13219,a.GetProperty("rawReferences").GetInt32());Assert.Equal(12960,a.GetProperty("uniqueKeys").GetInt32());Assert.Equal(12785,a.GetProperty("exportedKeys").GetInt32());Assert.Equal(6821,a.GetProperty("historicallyUnprovenRotationKeys").GetArrayLength());Assert.Equal(175,a.GetProperty("conflicts").EnumerateObject().Count());
        var missing=Assert.Single(a.GetProperty("missingSectors").EnumerateArray());Assert.Equal(34,missing.GetProperty("sector").GetInt32());Assert.Equal(0x04000800,missing.GetProperty("declaredSidRid").GetInt32());Assert.Equal(JsonValueKind.Null,missing.GetProperty("unknownExteriorCount").ValueKind);
        var excluded=a.GetProperty("excludedKeys").EnumerateArray().Select(k=>(k[0].GetInt32(),k[1].GetInt32())).ToHashSet();Assert.Equal(175,excluded.Count);
        var rows=Objects();Assert.Equal(12785,rows.Length);Assert.Equal(12785,rows.Select(s=>s.mxoStaticId).Distinct().Count());Assert.All(rows,s=>{Assert.Equal(7,s.metrId);Assert.NotEqual(34,s.sectorID);Assert.DoesNotContain(((int)s.sectorID,(int)s.staticId),excluded);});
        var historical=a.GetProperty("historicallyUnprovenRotationKeys").EnumerateArray().Select(k=>(k[0].GetInt32(),k[1].GetInt32())).ToHashSet();
        var rawKeys=a.GetProperty("raw").EnumerateArray().Select(r=>(r[0].GetInt32(),r[1].GetInt32())).ToHashSet();
        var oldSafe=rawKeys.Except(historical.Union(excluded)).ToHashSet();Assert.Equal(6105,oldSafe.Count);
        var actual=rows.Select(r=>((int)r.sectorID,(int)r.staticId)).ToHashSet();Assert.True(oldSafe.IsSubsetOf(actual));Assert.True(actual.SetEquals(rawKeys.Except(excluded)));Assert.Equal(6680,actual.Count(historical.Contains));
        foreach(var c in a.GetProperty("conflicts").EnumerateObject())foreach(var r in c.Value.EnumerateArray())Assert.Contains((r[0].GetInt32(),r[1].GetInt32()),excluded);
    }
    [Fact]public void EightExactGenericReturnersHaveSourceFloorsAndIncompleteBindingsRefuse()
    {
        Nav();var rows=Objects();var bound=DataMineReturners.Validate(rows);Assert.Equal(8,bound.Count);
        foreach(var s in bound){Assert.True(NavMesh.Locate(7,s.pos_x,s.pos_y-50,s.pos_z,out var at));Assert.InRange(Math.Abs(at.floorY-(s.pos_y-145)),0,3.01);}
        Assert.Empty(DataMineReturners.Validate(bound.Skip(1)));Assert.Empty(DataMineReturners.Validate(bound.Concat(bound.Take(1))));
        bound[0].pos_y+=400;Assert.Empty(DataMineReturners.Validate(bound));bound[0].pos_y=double.NaN;Assert.Empty(DataMineReturners.Validate(bound));
    }
    [Fact]public void GenericUseRequiresCurrentCreatedSourceBoundSameWorldNearbyView()
    {
        Nav();var b=DataMineReturners.Validate(Objects());var s=b[0];var v=new ClientView(47267,42,DataMineReturners.Entity(s)){viewCreated=true,spawnId=8,staticObject=s};
        bool Can(ClientView current,ushort map=7,bool world=true,double dx=0)=>DataMineReturners.CanUseView(v,current,b,map,world,s.pos_x+dx,s.pos_y-50,s.pos_z);
        Assert.True(Can(v));Assert.False(Can(v,6));Assert.False(Can(v,7,false));Assert.False(Can(v,7,true,1001));Assert.False(Can(new ClientView(47267,42,v.entityId)));
        v.entityId++;Assert.False(Can(v));v.entityId--;v.staticObject=new StaticWorldObject();Assert.False(Can(v));v.staticObject=s;v.viewCreated=false;Assert.False(Can(v));v.viewCreated=true;v.viewNeedsToBeDeleted=true;Assert.False(Can(v));
    }
    [Fact]public void DiagnosticModeUsesExactSourceStandingPointWithoutAuthorityOrOrigin()
    {
        Nav();var p=new NativeQa.Modes.DataMineSourceProxyMode().Plan;Assert.Equal(-31200,p.X);Assert.Equal(16450,p.Z);Assert.False(p.DevCommands);Assert.False(p.GmCharacter110);Assert.False(p.FixedCallbackRolls);Assert.Contains("y=1495",Assert.Single(p.AfterLoadout));Assert.Contains("districtId=7",Assert.Single(p.AfterLoadout));Assert.True(p.ExtraQaMetadata["NoCityOriginSession"].GetBoolean());Assert.True(NavMesh.Locate(7,p.X,1495,p.Z,out var at));Assert.Equal(1400,at.floorY);
    }
    [Fact]public void ModeSeedsNormalZionBookAndNoDevelopmentAuthority()
    {
        var p=new NativeQa.Modes.DataMineMode().Plan;Assert.False(p.DevCommands);Assert.False(p.GmCharacter110);Assert.False(p.FixedCallbackRolls);Assert.Contains(p.AfterLoadout,s=>s.Contains("47270"));Assert.Equal(111600,p.X);Assert.Equal(-6130,p.Z);
        Assert.Equal(7,PvpArchives.MapFor(7));Assert.Equal(17u,PvpArchives.ConstructId(7));var t=PvpArchives.Find(47270);Assert.Null(PvpArchives.Refusal(t,1,1,1));Assert.Null(PvpArchives.Refusal(t,1,100,1));
    }
}
