using hds;
using NativeQa;
using System.Xml;
namespace hds.Tests;
public class MissionCourierTests
{
    static string Root(){string p=AppContext.BaseDirectory;while(!Directory.Exists(Path.Combine(p,"data","missions")))p=Path.GetDirectoryName(p)!;return p;}
    static Mission Courier(){var d=new XmlDocument();d.Load(Path.Combine(Root(),"data","missions","standard_zion_courier.xml"));return MissionXml.Parse(d.DocumentElement);}
    [Fact] public void StoredCourierHasUniqueUnreservedIdAndOneOwnedDeliveryItem()
    {
        var c=Courier();Assert.Equal(20001,c.id);Assert.False(c.plot);Assert.Equal((byte)1,c.org);Assert.Equal("Tyndall",c.contact);
        Assert.Equal("Courier",c.title);Assert.Equal(2,c.phases.Count);Assert.Equal(50,c.rewardLevel);
        var ids=Directory.GetFiles(Path.Combine(Root(),"data","missions"),"*.xml").Select(p=>{var d=new XmlDocument();d.Load(p);return int.Parse(d.DocumentElement!.GetAttribute("id"));}).ToArray();
        Assert.Equal(1,ids.Count(id=>id==20001));Assert.DoesNotContain(ids,id=>id>=3000&&id<=3999);
        var d=new XmlDocument();d.Load(Path.Combine(Root(),"data","missions","standard_zion_courier.xml"));
        Assert.Equal(2,d.SelectNodes("/mission/phase[@enemies='0']")!.Count);
        Assert.Equal("6831",d.SelectSingleNode("/mission/phase/objective[@type='Get']")!.Attributes!["item"]!.Value);
        Assert.Equal("6831",d.SelectSingleNode("/mission/phase/objective[@type='Give']")!.Attributes!["item"]!.Value);
        Assert.Equal(22000u,MissionRewards.Calculate(c,50).Experience);Assert.Equal(31500u,MissionRewards.Calculate(c,50).Info);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void StandardNeverBlocksNextStoryAndIgnoresItsCompletedHistory(bool completedStandard)
    {
        var a=new Mission{id=1,plot=true};var b=new Mission{id=2,plot=true};var c=Courier();
        var done=new HashSet<int>{1};if(completedStandard)done.Add(c.id);
        Assert.Equal(new[]{b,c},DataLoader.ContactOfferWithStandards(new(){c,a,b},done));
        Assert.Equal(new[]{a,b,c},DataLoader.ContactOfferWithStandards(new(){c,a,b},new(){1,2,c.id}));
    }
    [Fact]public void NoStandardLeavesExistingStoryOfferExactlyUnchanged()
    {
        var stories=new List<Mission>{new(){id=1,plot=true},new(){id=2,plot=true}};
        foreach(var done in new[]{new HashSet<int>(),new HashSet<int>{1},new HashSet<int>{1,2}})
            Assert.Equal(DataLoader.ContactOffer(stories,done),DataLoader.ContactOfferWithStandards(stories,done));
    }
    [Fact]public void StandardsOnlyAreAlwaysRepeatableInStableIdOrder()
    {
        var a=new Mission{id=20002};var b=Courier();
        Assert.Equal(new[]{b,a},DataLoader.ContactOfferWithStandards(new(){a,b},new(){a.id,b.id}));
    }
    [Fact]public void CourierContactCannotLeakToOtherOrganizationsOrNeighbourhoods()
    {
        MissionContacts.Load(Path.Combine(Root(),"data","mission_contacts.csv"));var c=Courier();
        Assert.True(MissionContacts.Offers(MissionContacts.ById(2000),c));
        Assert.False(MissionContacts.Offers(MissionContacts.ById(2001),c));
        Assert.False(MissionContacts.Offers(MissionContacts.ById(47),c));
    }
    [Fact]public void NativeModeGrantsNoMissionItemProgressOrReceiptAndOnlySeedsLayout()
    {
        var p=NativeQaModes.Find("mission-courier").Plan;Assert.True(p.DevCommands);Assert.False(p.GmCharacter110);Assert.False(p.FixedCallbackRolls);
        Assert.Empty(p.BeforeLoadout);Assert.Empty(p.AfterLoadout);Assert.Single(p.AfterHelpers);
        Assert.DoesNotContain("char_missions",p.AfterHelpers[0]);Assert.DoesNotContain("inventory",p.AfterHelpers[0]);
        Assert.Equal("20001",p.EnvironmentOverrides["MXO_MISSION_SITE_QA_SEED"]);Assert.Single(p.EnvironmentOverrides);
    }
}
