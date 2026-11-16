using System.Globalization;
using System.Text.Json;
using System.Xml;
using hds;
namespace hds.Tests;

[Collection("DelayedActions")]
public class MissionGeneratedSafeSourceTests
{
    static readonly JsonSerializerOptions Fields=new(){IncludeFields=true};
    static string Repo {get {string p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds.sln")))p=Path.GetDirectoryName(p)!;return p;}}
    static MissionPhase Phase(params string[] kinds)
    {
        var xml=new XmlDocument();xml.LoadXml("<mission id='9813' title='Safe sources' description='D'><phase site='Office'>"+
            string.Join("",kinds.Select((kind,i)=>$"<object id='{i+1}' kind='{kind}'/>"))+"</phase></mission>");
        return MissionXml.Parse(xml.DocumentElement!).phases[0];
    }
    static StaticWorldObject Row(ushort goid,uint key)=>new(){metrId=1,mxoStaticId=key,type=BitConverter.GetBytes(goid),quat="0000000000000000000000000000803f"};
    static MissionSite Site(params ushort[] spawners)
    {var site=new MissionSite();uint key=1;foreach(var goid in spawners)site.objectSpots.Add(Row(goid,key++));return site;}
    static MissionStaging.SourcePools Pools(MissionPhase phase,MissionSite site)
    {Assert.True(MissionStaging.TrySourcePools(phase,site,0,out var pools,out string why),why);return pools;}
    sealed class Keys:Random
    {public int Draws;public override int Next()=>Draws++;}
    [Fact] public void ComputerCannotConsumeTheOnlySupportedSafeSource()
    {
        var phase=Phase("Computer","Safe");var site=Site(1410,1408);var rng=new Keys();
        var choices=MissionStaging.SelectObjectsFromPools(phase,site,rng,Pools(phase,site)).ToArray();
        Assert.Equal(new ushort[]{1408,1410},choices.Select(c=>MissionSites.Goid(c.Spot)));Assert.Equal(new ushort[]{3,552},choices.Select(c=>c.Goid));Assert.Equal(2,rng.Draws);
        Assert.Equal((ushort)1416,MissionStaging.SelectObjects(phase,site,new Keys()).Last().Goid); // legacy API deliberately unchanged
    }
    [Fact] public void FreshStageUnionUsesOnlySourcePoolChoicesOnce()
    {
        var phase=Phase("Computer","Safe");var site=Site(1410,1408);var random=new Keys();
        var choices=MissionStaging.StageSourceChoices(phase,site,random,null!,0);
        Assert.Equal(2,random.Draws); // Calling then discarding either selector would consume four draws.
        Assert.Equal(new ushort[]{1408,1410},choices.Select(c=>MissionSites.Goid(c.Spot)));
        Assert.Equal(new ushort[]{3,552},choices.Select(c=>c.Goid)); // Old generic Safe path would yield1416.
    }
    [Fact] public void AuthoredOrderAndSameKindPriorityStillApplyWhenCapacityAllows()
    {
        var phase=Phase("Safe","Computer");var site=Site(1408,1410,1407);var choices=MissionStaging.SelectObjectsFromPools(phase,site,new Keys(),Pools(phase,site)).ToArray();
        Assert.Equal(new ushort[]{1410,1407},choices.Select(c=>MissionSites.Goid(c.Spot)));
    }
    [Theory][InlineData(1,false)][InlineData(2,true)]
    public void TwoSafesRequireTwoDistinctSupportedSources(int count,bool expected)
    {
        var phase=Phase("Safe","Safe");var site=Site(Enumerable.Repeat((ushort)1410,count).Concat(new ushort[]{1408,1407}).ToArray());
        Assert.Equal(expected,MissionStaging.TrySourcePools(phase,site,0,out var pools,out string why));
        if(expected)Assert.Equal(new ushort[]{552,552},MissionStaging.SelectObjectsFromPools(phase,site,new Keys(),pools).Select(c=>c.Goid));
        else {Assert.Null(pools);Assert.Equal("InsufficientCompatibleGeneratedSafeSources",why);}
    }
    [Theory][InlineData(1416)][InlineData(552)]
    public void GenuineNativeSafeKeepsPriorityAndOtherKindsKeep1408(ushort native)
    {
        var phase=Phase("Computer","Safe");var site=Site(1408);site.realObjects.Add(Row(native,50));
        var pools=Pools(phase,site);Assert.Null(pools);
        var choices=MissionStaging.SelectObjectsFromPools(phase,site,new Keys(),pools).ToArray();
        Assert.Same(site.realObjects[0],choices[1].Spot);Assert.Equal(native,choices[1].Goid);Assert.Equal((ushort)1408,MissionSites.Goid(choices[0].Spot));
    }
    [Fact] public void ContextNullCannotHideUnsupportedGeneratedSafeAndDoesNotAcquireAWitness()
    {
        var phase=Phase("Safe");var bad=Site(1408);Assert.False(MissionStaging.TrySourcePools(phase,bad,0,out var absent,out string why));Assert.Null(absent);Assert.Equal("InsufficientCompatibleGeneratedSafeSources",why);
        var good=Site(1410);var pools=Pools(phase,good);Assert.NotNull(pools);Assert.Null(pools.Elevator);
        Assert.Equal((ushort)552,Assert.Single(MissionStaging.SelectObjectsFromPools(phase,good,new Keys(),pools)).Goid);
    }
    [Fact] public void DuplicateSourceIdentityRefusesWithoutMutatingTheSite()
    {
        var site=Site(1410,1410);site.objectSpots[1].mxoStaticId=site.objectSpots[0].mxoStaticId;
        string before=JsonSerializer.Serialize(site.objectSpots,Fields);
        Assert.False(MissionStaging.TrySourcePools(Phase("Safe","Safe"),site,0,out _,out string why));Assert.Equal("AmbiguousGeneratedSafeSourcePool",why);
        Assert.Equal(before,JsonSerializer.Serialize(site.objectSpots,Fields));
    }
    [Fact] public void OriginalRowDrawCountAndNextRandomValueRemainExact()
    {
        var phase=Phase("Furniture","Computer","Safe");var site=Site(1410,1408,1407,1412);site.containers.Add(Row(1433,30));site.containers.Add(Row(1435,31));
        var actual=new Random(543);var legacy=new Random(543);var pools=Pools(phase,site);
        Assert.Equal(3,MissionStaging.SelectObjectsFromPools(phase,site,actual,pools).Count());
        _=MissionStaging.SelectObjects(phase,site,legacy).ToArray();Assert.Equal(legacy.Next(),actual.Next());
    }
    [Fact] public void NoFreeSafeDelegatesToExactOldSelector()
    {
        var phase=Phase("Computer","Mainframe","Furniture");var site=Site(1410,1408,1407);site.containers.Add(Row(1433,30));
        var pools=Pools(phase,site);Assert.Null(pools);var a=new Random(44);var b=new Random(44);
        var legacy=MissionStaging.SelectObjects(phase,site,a).ToArray();var fresh=MissionStaging.SelectObjectsFromPools(phase,site,b,pools).ToArray();
        Assert.Equal(legacy.Select(c=>(c.Spot,c.Goid)),fresh.Select(c=>(c.Spot,c.Goid)));Assert.Equal(a.Next(),b.Next());
    }
    [Fact] public void NativeSafeDoesNotReserveAnUnneeded1410ForLaterDefinitions()
    {
        var phase=Phase("Safe","Computer");var site=Site(1410);site.realObjects.Add(Row(1416,50));
        var choices=MissionStaging.SelectObjectsFromPools(phase,site,new Keys(),Pools(phase,site)).ToArray();
        Assert.Equal((ushort)1416,choices[0].Goid);Assert.Equal((ushort)3,choices[1].Goid);Assert.Same(site.objectSpots[0],choices[1].Spot);
    }
    [Fact] public void FurnitureCannotSpendASpawnerAndLeaveAMissingChoice()
    {
        var phase=Phase("Furniture","Safe");var site=Site(1410,1408);
        Assert.False(MissionStaging.TrySourcePools(phase,site,0,out _,out _));site.containers.Add(Row(1433,30));
        Assert.All(MissionStaging.SelectObjectsFromPools(phase,site,new Keys(),Pools(phase,site)),c=>Assert.NotNull(c.Spot));
    }
    static readonly Lazy<List<MissionSite>> Catalog=new(()=>
    {
        var rows=new List<StaticWorldObject>();
        foreach(string file in new[]{"staticObjects_slums_1.csv","staticObjects_slums_2.csv"})
        foreach(string line in File.ReadLines(Path.Combine(Repo,"data",file)).Skip(1).Where(l=>!string.IsNullOrWhiteSpace(l)&&char.IsDigit(l[0])))
        {
            var f=line.Split(',');double D(int i)=>double.Parse(f[i],CultureInfo.InvariantCulture);
            rows.Add(new(){metrId=ushort.Parse(f[0]),sectorID=ushort.Parse(f[1]),mxoStaticId=BitConverter.ToUInt32(Convert.FromHexString(f[2])),staticId=BitConverter.ToUInt32(Convert.FromHexString(f[3])),type=Convert.FromHexString(f[4][..4]),exterior=bool.Parse(f[5]),pos_x=D(6),pos_y=D(7),pos_z=D(8),rot=D(9),quat=f[10]});
        }
        return MissionSites.Build(rows,1);
    });
    static Mission Authored()
    {var xml=new XmlDocument();xml.Load(Path.Combine(Repo,"data/missions/merovingian_1_2_4_opportunity_knocks.xml"));return MissionXml.Parse(xml.DocumentElement!);}
    [Fact] public void Exact301204FailureInventoryRefusesFreshButLegacyChoiceAndRawPendingRemainImmutable()
    {
        var phase=Authored().phases[1];Assert.Equal(new[]{"Furniture","Safe"},phase.objects.Select(d=>d.kind));
        var site=Catalog.Value.Single(s=>s.elevatorDoor?.mxoStaticId==0x45e0012e&&s.objectSpots.Any(r=>r.mxoStaticId==0x45e0015f));
        Assert.DoesNotContain(site.objectSpots,r=>MissionSites.Goid(r)==1410);Assert.Empty(site.realObjects.Where(r=>MissionSites.RealObjects[MissionSites.Goid(r)]=="Safe"));
        var local=MissionStaging.SelectObjects(phase,site,new Random(7)).Select(MissionOpenFloorChoice.From).ToArray();
        Assert.Equal((ushort)1416,local[1].Goid); // Local RNG is a legacy API control, not the captured chooser stream.
        var furniture=site.containers.Single(r=>r.mxoStaticId==0x45e00154);Assert.Equal((ushort)1433,MissionSites.Goid(furniture));
        var raw=site.objectSpots.Single(r=>r.mxoStaticId==0x45e0015f);Assert.Equal((ushort)1408,MissionSites.Goid(raw));
        var captured=new[]{new MissionOpenFloorChoice(1,1433,JsonSerializer.Serialize(furniture,Fields)),new MissionOpenFloorChoice(2,1416,JsonSerializer.Serialize(raw,Fields))};
        var safe=captured[1];Assert.Equal((ushort)1416,safe.Goid);Assert.Equal(0x45e0015fu,safe.ReadSpot().mxoStaticId);
        var pending=new MissionOpenFloorPendingAdmission(1,301204,"retained",1,123,MissionOpenFloorSite.From(site),captured,null,null);
        string before=JsonSerializer.Serialize(pending,Fields);Assert.False(MissionStaging.TrySourcePools(phase,site,7,out var pools,out string why));Assert.Null(pools);Assert.Equal("InsufficientCompatibleGeneratedSafeSources",why);
        Assert.Equal(before,JsonSerializer.Serialize(pending,Fields));Assert.Null(pending.Elevator);Assert.Equal(safe.RawSpotJson,pending.Choices[1].RawSpotJson);
        Assert.Equal((ushort)1416,MissionStaging.ExpectedConcrete(phase.objects[1],safe.ReadSpot()));Assert.False(MissionOpenFloorAdmission.Target(safe.Goid,safe.ReadSpot()));
    }
    [Fact] public void NormalCatalogChooserGetsACompleteSupported301204PhaseWithoutForcedSource()
    {
        SectorProperties.Load(Path.Combine(Repo,"data/sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(Repo,"data/navmesh"));
        var phase=Authored().phases[1];var rng=new Random(301204);
        var site=MissionSites.Choose(Catalog.Value,phase.site,17000,2300,7,phase.objects.Count,rng,requiredFurniture:MissionStaging.FurnitureNeeded(phase),admissible:s=>MissionInteriorSites.Eligible(phase,s,out _,7));
        Assert.NotNull(site);Assert.True(MissionStaging.TrySourcePools(phase,site,7,out var pools,out string why),why);
        var choices=MissionStaging.SelectObjectsFromPools(phase,site,rng,pools).ToArray();Assert.Equal(2,choices.Length);Assert.All(choices,c=>Assert.NotNull(c.Spot));
        var safe=choices[1];Assert.True(safe.Goid==552&&MissionSites.Goid(safe.Spot)==1410||safe.Goid==MissionSites.Goid(safe.Spot)&&new ushort[]{552,1416}.Contains(safe.Goid));
        Assert.Equal(2,choices.Select(c=>MissionOpenFloorSite.SpotFingerprint(c.Spot)).Distinct().Count());
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void DurableGenerated1416PendingIsNotFilteredOrReassignedOnRestore(bool entered)
    {
        var phase=Phase("Furniture","Safe");var site=Catalog.Value.Single(s=>s.elevatorDoor?.mxoStaticId==0x45e0012e&&s.objectSpots.Any(r=>r.mxoStaticId==0x45e0015f));
        var choices=MissionStaging.SelectObjects(phase,site,new Random(7)).Select(MissionOpenFloorChoice.From).ToArray();
        var xml=new XmlDocument();xml.LoadXml("<mission id='9813' title='Retained' description='D'><phase><objective goal='1' type='Survive' seconds='20' text='Wait'/></phase><phase site='Office'><object id='1' kind='Furniture'/><object id='2' kind='Safe'/></phase></mission>");
        var mission=MissionXml.Parse(xml.DocumentElement!);var progress=new MissionProgress(mission){persistenceId=new string('a',32),startedAt=100,entered=entered};
        progress.itemSlots[6791]=7;progress.Complete(progress.Phase.missionObjectives.Single());progress.waitingForExit=true;
        progress.pendingOpenFloor=new(1,mission.id,progress.persistenceId,1,100,MissionOpenFloorSite.From(site),choices,null);MissionOpenFloorAdmission.MarkPendingCheckpoint(progress);
        var saved=progress.Capture(49,Array.Empty<Mob>());string before=JsonSerializer.Serialize(saved,Fields);
        var restored=MissionProgress.RestoreFromCatalog(mission,saved,new[]{site});
        Assert.Equal(before,JsonSerializer.Serialize(restored.Capture(49,Array.Empty<Mob>()),Fields));Assert.Equal((ushort)7,restored.itemSlots[6791]);
        Assert.Equal((ushort)1416,restored.pendingOpenFloor.Choices[1].Goid);Assert.Equal(choices[1].RawSpotJson,restored.pendingOpenFloor.Choices[1].RawSpotJson);Assert.Null(restored.pendingOpenFloor.Elevator);
        Assert.True(MissionOpenFloorAdmission.TryGeometry(mission,1,site,restored.pendingOpenFloor.Choices,out _,out string why,restored.pendingOpenFloor),why);
        Assert.Equal(before,JsonSerializer.Serialize(restored.Capture(49,Array.Empty<Mob>()),Fields));
    }

}
