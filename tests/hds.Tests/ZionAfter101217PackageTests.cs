using hds;
using HeadlessClient;
using NativeQa.Modes;
using System.Xml;
namespace hds.Tests;
public sealed class ZionAfter101217PackageTests
{
    static string Root=>NativeBoundFurnitureFixture.FindRepository();
    static List<Mission> Missions()=>Directory.GetFiles(Path.Combine(Root,"data/missions"),"zion_*.xml").Select(f=>{var x=new XmlDocument();x.Load(f);return MissionXml.Parse(x.DocumentElement!);}).OrderBy(m=>m.order).ToList();
    [Fact] public void FiveAvailableDefinitionsHaveExactAuthoredCountsAndRealNextOfferWithout101305()
    {
        var all=Missions();var selected=all.Where(m=>ZionAfter101217Fixture.Missions.Contains(m.id)).ToArray();
        Assert.Equal(new[]{101301,101302,101303,101304,101306},selected.Select(m=>m.id));Assert.Equal(new[]{1301,1302,1303,1304,1306},selected.Select(m=>m.order));
        Assert.DoesNotContain(all,m=>m.id==101305);Assert.Equal(25,selected.Sum(m=>m.phases.Count));Assert.Equal(95,selected.Sum(m=>m.phases.Sum(p=>p.missionObjectives.Count)));
        Assert.Equal(new[]{15,12,17,42,9},selected.Select(m=>m.phases.Sum(p=>p.missionObjectives.Count)));
        var contact=MissionContacts.ForOrg(1,0);var prior=ZionAfter101217Fixture.Prerequisites.ToHashSet();
        foreach(var m in selected){Assert.Equal(m.id,Assert.Single(DataLoader.ContactOfferWithStandards(all.Where(m=>MissionContacts.Offers(contact,m)).ToList(),prior).Where(m=>m.plot)).id);prior.Add(m.id);}
        Assert.Equal(102101,Assert.Single(DataLoader.ContactOfferWithStandards(all.Where(m=>MissionContacts.Offers(contact,m)).ToList(),prior).Where(m=>m.plot)).id);
    }
    [Fact] public void PrerequisitesAreExactThirtySevenBeforeMarginAndNeverMissingSelectedOrNext()
    {
        var seed=new CharacterSeed("SourceOnly");var prepared=ZionAfter101217Fixture.BeforeMargin(seed,ZionAfter101217Fixture.Missions,ZionAfter101217Fixture.Prerequisites);
        Assert.Equal(37,prepared.CompletedMissions.Count);Assert.Empty(prepared.CompletedMissions.Intersect(ZionAfter101217Fixture.Missions.Append(101305).Append(102101)));
        Assert.Throws<InvalidOperationException>(()=>ZionAfter101217Fixture.BeforeMargin(seed,ZionAfter101217Fixture.Missions,ZionAfter101217Fixture.Prerequisites.Append(101305).ToArray()));
        Assert.Throws<InvalidOperationException>(()=>ZionAfter101217Fixture.BeforeMargin(seed,ZionAfter101217Fixture.Missions,ZionAfter101217Fixture.Prerequisites.Skip(1).Append(101301).ToArray()));
    }
    [Fact] public void RewardsAreExistingAuthoredPolicyNotNewRetailEvidence()
    {
        var selected=Missions().Where(m=>ZionAfter101217Fixture.Missions.Contains(m.id)).ToArray();
        Assert.Equal(ZionAfter101217Fixture.MissionExperience,selected.Select(m=>(uint)m.exp));Assert.Equal(ZionAfter101217Fixture.MissionInformation,selected.Select(m=>(uint)m.info));
        Assert.All(selected,m=>Assert.Equal((uint)m.exp,MissionRewards.Calculate(m,50).Experience));Assert.Equal(1821980L,selected.Sum(m=>(long)m.exp));Assert.Equal(479500L,selected.Sum(m=>(long)m.info));
        Assert.Equal(selected[3].exp,selected[4].exp); // Documented borrowed Exodus reward is retained, never presented as newly recovered.
    }
    [Fact] public void ModeIsDiscoveredWithNormalAbilitiesAndHonestUnqualifiedDependencies()
    {
        var mode=new ZionAfter101217Mode();Assert.Equal("zion-critical-after-101217",mode.Name);var p=mode.Plan;
        Assert.Equal(new ushort[]{2,17,1006,77},p.Abilities);Assert.False(p.FixedCallbackRolls);Assert.False(p.GmCharacter110);Assert.True(p.DevCommands);
        Assert.Equal("101301",p.EnvironmentOverrides["MXO_MISSION_SITE_QA_SEED"]);Assert.Equal(ZionAfter101217Fixture.SeedPrerequisites(110),p.AfterHelpers[0]);
        Assert.Equal(37,p.ExtraQaMetadata["EarlierReceiptsOnly"].GetInt32());Assert.Equal(true,p.ExtraQaMetadata["ExodusRetailRewardUnproved"].GetBoolean());
    }
}
