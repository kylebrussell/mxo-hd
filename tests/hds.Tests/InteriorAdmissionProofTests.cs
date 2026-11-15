using hds;
using HeadlessClient;
using NativeQa;
using NativeQa.Modes;
using System.Xml;
namespace hds.Tests;
public class InteriorAdmissionProofTests
{
    static List<Mission> Load()
    {
        string root=AppContext.BaseDirectory;while(!Directory.Exists(Path.Combine(root,"data","missions")))root=Path.GetDirectoryName(root)!;
        return Directory.GetFiles(Path.Combine(root,"data","missions"),"*.xml").Select(f=>{var d=new XmlDocument();d.Load(f);return MissionXml.Parse(d.DocumentElement!);}).ToList();
    }
    [Fact] public void EachModeSelectsItsActualCriticalOfferWithOnlyEarlierReceipts()
    {
        var all=Load();foreach(var c in InteriorAdmissionFixture.Cases)
        {
            Assert.DoesNotContain(c.Mission,c.Prerequisites);var contact=MissionContacts.ForOrg(c.Organization,0);
            var offered=DataLoader.ContactOfferWithStandards(all.Where(m=>MissionContacts.Offers(contact,m)).OrderBy(m=>m.order).ToList(),c.Prerequisites.ToHashSet());
            Assert.Equal(c.Mission,Assert.Single(offered,m=>m.plot).id);Assert.Equal(c.Title,all.Single(m=>m.id==c.Mission).title);
            var plan=NativeQaModes.Find(c.Name).Plan;Assert.Equal(InteriorAdmissionFixture.Seed(c,110),plan.AfterHelpers[0]);Assert.False(plan.GmCharacter110);Assert.False(plan.FixedCallbackRolls);
            Assert.Equal(c.LayoutSeed.ToString(),plan.EnvironmentOverrides["MXO_MISSION_SITE_QA_SEED"]);Assert.Single(Scenarios.All,s=>s.Name==c.Name);
        }
    }
    [Fact] public void ZionNativePreviewOriginAndEarlierReceiptAreNotConflatedWithOtherCases()
    {
        var z=InteriorAdmissionFixture.Zion;Assert.Equal((7800d,95d,13795d),(z.X,z.Y,z.Z));Assert.Contains(101208,z.Prerequisites);Assert.Equal(101209,z.Mission);
        Assert.Equal(0x0d100401u,z.OriginalDoor);Assert.Equal(0x0d100402u,z.OriginalSource);Assert.Equal(101208,z.LayoutSeed);
        Assert.Equal(0x0ee0004bu,InteriorAdmissionFixture.Merv.OriginalSource);Assert.Equal(0x0d1001edu,InteriorAdmissionFixture.Machines.OriginalSource);
    }
    [Fact] public void PosterCaseRetainsOriginalLevelProfileAndValidCompatibilityTuple()
    {
        var c=InteriorAdmissionFixture.Posters;Assert.Equal(17,c.Level);Assert.Equal(9560000,PlayerProgression.ExperienceForLevel(c.Level));
        Assert.Contains(201201,c.Prerequisites);Assert.DoesNotContain(201202,c.Prerequisites);Assert.Equal(201202,c.LayoutSeed);Assert.Equal(3,c.Objects);
        Assert.Equal(0x0d1003bbu,c.OriginalDoor);Assert.Equal(new uint[]{0x0d1003dc,0x0d100314,0x0d1002f1},c.OriginalSources);
        Assert.Contains("level=17,exp=9560000",NativeQaModes.Find(c.Name).Plan.AfterHelpers[1]);
    }
    [Fact] public void FreshProofCannotSilentlyUseAnUnpinnedOrDifferentProducer()
    {
        string actual=new('A',64);InteriorAdmissionFixture.RequireSource(actual.ToLowerInvariant(),actual);
        Assert.Throws<InvalidDataException>(()=>InteriorAdmissionFixture.RequireSource(null,actual));
        Assert.Throws<InvalidDataException>(()=>InteriorAdmissionFixture.RequireSource(new string('B',64),actual));
    }
    [Fact] public void StaticDoorSelectionUsesExactNativePositionAndRefusesAmbiguity()
    {
        string Row(string key,double x)=>$"1,209,{key},01000000,a1010000,False,{x},145,13800,0,0000000000000000000000000000803f";
        var rows=new[]{Row("0100100d",23700),Row("0200100d",17000)};Assert.Equal(0x0d100001u,InteriorAdmissionFixture.DoorAt(rows,new[]{23700d,145d,13800d}));
        Assert.Throws<InvalidDataException>(()=>InteriorAdmissionFixture.DoorAt(rows,new[]{23701d,145d,13800d}));
        Assert.Throws<InvalidDataException>(()=>InteriorAdmissionFixture.DoorAt(rows.Append(Row("0300100d",23700)),new[]{23700d,145d,13800d}));
    }
    [Fact] public void ActualObjectBodyRequiresCurrentCursorAndExactFullPose()
    {
        ViewCreation Body(ushort view,double y)=>new(view,3,1,new Dictionary<string,byte[]>{["Position"]=NumericalUtils.doublesToLtVector3d(10,y,30)});
        var list=new[]{Body(5,20),Body(6,420),Body(7,20)};
        Assert.Equal((ushort)7,InteriorAdmissionFixture.Body(list,1,3,new[]{10d,20d,30d})!.ViewId);
        Assert.Null(InteriorAdmissionFixture.Body(list,3,3,new[]{10d,20d,30d}));Assert.Null(InteriorAdmissionFixture.Body(list,0,548,new[]{10d,20d,30d}));
    }
}
