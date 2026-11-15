using System.Xml;
using hds;
using HeadlessClient;
using NativeQa;
namespace hds.Tests;
public class MachinesCriticalNextFiveTests
{
    [Fact] public void CurrentHostilesIncludeNamedXmlActorsWithoutTreatingOldStageOrFriendlyActorsAsTargets()
    {
        var authored=new HashSet<ushort>{1};
        var phage=new MissionProgress.SavedMob{Npc=1,Friendly=false,Hostile=false,Dead=false,Phase=0};
        Assert.True(MachinesCriticalNextFiveFixture.CurrentHostile(phage,authored));
        Assert.False(MachinesCriticalNextFiveFixture.CurrentHostile(phage,new HashSet<ushort>{2}));
        phage.Friendly=true;Assert.False(MachinesCriticalNextFiveFixture.CurrentHostile(phage,authored));
        phage.Friendly=false;phage.Dead=true;Assert.False(MachinesCriticalNextFiveFixture.CurrentHostile(phage,authored));
        Assert.True(MachinesCriticalNextFiveFixture.CurrentHostile(new MissionProgress.SavedMob{Npc=1050,Hostile=true},authored));
    }
    [Fact] public void LatestGenerationCannotReselectHistoricalSameViewId()
    {
        var attrs=new Dictionary<string,byte[]>();
        var old=new ViewCreation(7,599,1,attrs);var current=new ViewCreation(7,599,2,attrs);
        Assert.Same(current,Assert.Single(MachinesCriticalNextFiveFixture.LatestViews(new[]{old,current},0)));
        Assert.Same(current,Assert.Single(MachinesCriticalNextFiveFixture.LatestViews(new[]{old,current},1)));
    }
    [Fact] public void ActualNpcLevelIsOneByteAndMalformedSchemaIsRejected()
    {
        Assert.Equal((byte)50,MachinesCriticalNextFiveFixture.NpcLevel(new Dictionary<string,byte[]>{{"Level",new byte[]{0x32}}}));
        Assert.Throws<InvalidDataException>(()=>MachinesCriticalNextFiveFixture.NpcLevel(new Dictionary<string,byte[]>()));
        Assert.Throws<InvalidDataException>(()=>MachinesCriticalNextFiveFixture.NpcLevel(new Dictionary<string,byte[]>{{"Level",new byte[]{0x32,0}}}));
    }
    [Fact] public void LootProofUsesClaimedIdentityNotAnInventorySlotOrOnlyCount()
    {
        var window=new byte[26];window[0]=0x81;window[1]=0x19;window[16]=1;
        Convert.FromHexString("000000088C1A0000").CopyTo(window,18);
        var claim=new byte[22];claim[0]=0x81;claim[1]=0x20;claim[12]=1;
        window.AsSpan(18,8).CopyTo(claim.AsSpan(14));
        Assert.Equal(MachinesCriticalNextFiveFixture.LootIdentities(window,false),MachinesCriticalNextFiveFixture.LootIdentities(claim,true));
        claim[14]=1;Assert.NotEqual(MachinesCriticalNextFiveFixture.LootIdentities(window,false)[0],MachinesCriticalNextFiveFixture.LootIdentities(claim,true)[0]);
        Assert.Throws<InvalidDataException>(()=>MachinesCriticalNextFiveFixture.LootIdentities(claim[..^1],true));
    }
    private static string Root {get {var p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds.sln")))p=Path.GetDirectoryName(p)!;return p;}}
    private static List<Mission> Load() => Directory.GetFiles(Path.Combine(Root,"data","missions"),"*.xml").Select(f=>{var d=new XmlDocument();d.Load(f);return MissionXml.Parse(d.DocumentElement!);}).ToList();
    [Fact] public void ActualAgentGrayOrderDeterminesTheFiveAndFollowingCritical()
    {
        var all=Load(); MissionContacts.Load(Path.Combine(Root,"data","mission_contacts.csv"));
        var contact=MissionContacts.ForOrg(2,0); Assert.Equal(2001u,contact.id);
        var catalogue=all.Where(m=>MissionContacts.Offers(contact,m)).OrderBy(m=>m.order).ToList();
        var completed=MachinesCriticalNextFiveFixture.Prerequisites.ToHashSet();
        Assert.Equal(21,completed.Count); Assert.Empty(completed.Intersect(MachinesCriticalNextFiveFixture.Missions.Append(MachinesCriticalNextFiveFixture.NextMission)));
        var intro=new[]{1201,1204,1203,1205,1206,1207,1208};
        Assert.Equal(completed.Order(),catalogue.Where(m=>m.plot&&m.order<1202).Select(m=>m.id).Concat(intro).Order());
        foreach(int id in MachinesCriticalNextFiveFixture.Missions.Append(MachinesCriticalNextFiveFixture.NextMission))
        {
            var offers=DataLoader.ContactOfferWithStandards(catalogue,completed);
            var mission=Assert.Single(offers.Where(m=>m.plot)); Assert.Equal(id,mission.id);
            Assert.DoesNotContain(offers,m=>m.id==20001); completed.Add(id);
        }
    }
    [Fact] public void SourceMissionGoalStructureAndIndependentLevel50RewardAmountsStayExplicit()
    {
        var all=Load();var selected=MachinesCriticalNextFiveFixture.Missions.Select(id=>all.Single(m=>m.id==id)).ToArray();
        Assert.Equal(new[]{"Poster Hunt","Hard Time","No Allies Here","Under Warranty","Gathering Evidence"},selected.Select(m=>m.title));
        Assert.Equal(new[]{3,4,2,2,2},selected.Select(m=>m.phases.Count));
        foreach(var m in selected)
        {
            var expected=MachinesCriticalNextFiveFixture.ExpectedReward(m.id);
            if(m.id==201202){Assert.Equal(17,m.rewardLevel);Assert.Equal(132000,m.exp);Assert.Equal(59500,m.info);
                Assert.Equal(404250u,expected.Xp);Assert.Equal(175000u,expected.Info);}
            else{Assert.Equal(0,m.rewardLevel);Assert.Equal(expected.Xp,(uint)m.exp);Assert.Equal(expected.Info,(uint)m.info);}
        }
        Assert.Equal(8,selected[0].phases.Sum(p=>p.objects.Count));
        var warranty=selected[3];Assert.Single(warranty.phases[1].waves);Assert.Equal(3,warranty.phases[1].waves[0].count);
    }
    [Fact] public void NativeModeHasOnlyEarlierReceiptsAndLayoutControl()
    {
        var mode=NativeQaModes.Find("machines-critical-next-five"); Assert.Equal("machines-critical-next-five",mode.Name);
        var p=mode.Plan; Assert.False(p.GmCharacter110);Assert.False(p.FixedCallbackRolls); Assert.True(p.DevCommands);
        Assert.Equal(new ushort[]{2,17,1006,77},p.Abilities);
        Assert.Equal("201202",Assert.Single(p.EnvironmentOverrides).Value);
        Assert.Equal(MachinesCriticalNextFiveFixture.SeedPrerequisites(110),p.AfterHelpers[0]);
        Assert.DoesNotContain("char_active_missions",string.Join("\n",p.AfterHelpers));
        Assert.DoesNotContain("inventory",string.Join("\n",p.AfterHelpers));
        Assert.Single(Scenarios.All,s=>s.Name=="machines-critical-next-five");
    }
}
