using System.Text.Json;
using System.Text.Json.Nodes;
using HeadlessClient;
using hds;
namespace hds.Tests;
public class MissionNoSiteActorHistoryTests
{
    static MissionProgress.SavedState Captured()
    {
        var d=new DirectoryInfo(AppContext.BaseDirectory);while(d!=null&&!File.Exists(Path.Combine(d.FullName,"tests/hds.Tests/Fixtures/NoSiteHistory-101214-3c6a2953d1.json")))d=d.Parent;
        return JsonSerializer.Deserialize<MissionProgress.SavedState>(File.ReadAllText(Path.Combine(d!.FullName,"tests/hds.Tests/Fixtures/NoSiteHistory-101214-3c6a2953d1.json")),new JsonSerializerOptions{IncludeFields=true})!;
    }
    // This is a COUNTERFACTUAL transition control derived from the actual AFTER payload, not retained live BEFORE evidence.
    static MissionProgress.SavedState PriorMembership()
    {
        var s=Captured();s.Phase--;foreach(var a in s.Mobs){a.Hostile=true;if(a.Npc>=1000)a.Wave=0;}return s;
    }
    [Fact] public void ActualNoSitePayloadRetainsSevenOldActorsIncludingThreeAlive()
    {
        var s=Captured();Assert.Equal(101214,s.Mission);Assert.Equal(1,s.Phase);Assert.Equal(-1,s.OpenFloorCurrentSiteIndex);
        Assert.Equal(7,s.Mobs.Count);Assert.Equal(3,s.Mobs.Count(m=>!m.Dead));Assert.All(s.Mobs,m=>Assert.Equal(3295,m.Y));
        MissionNoSiteActorHistory.RequireHistoricalContext(s);MissionNoSiteActorHistory.RequireStable(s,Captured());
        MissionNoSiteActorHistory.RequireTransition(PriorMembership(),s);
    }
    [Theory] [InlineData("added")] [InlineData("removed")] [InlineData("identity")] [InlineData("position")] [InlineData("health")] [InlineData("name")] [InlineData("side")] [InlineData("npc")] [InlineData("wirephase")] [InlineData("loot")] [InlineData("duplicate")] [InlineData("hostile")] [InlineData("wave")]
    public void TransitionNeverAllowsNewChangedOrCurrentActorAuthority(string fault)
    {
        var before=PriorMembership();var after=Captured();var actor=after.Mobs[0];
        if(fault=="added")after.Mobs.Add(new MissionProgress.SavedMob{Identity=new string('e',32)});
        if(fault=="removed")after.Mobs.RemoveAt(0);if(fault=="identity")actor.Identity=new string('e',32);
        if(fault=="position")actor.X++;if(fault=="health")actor.Health++;if(fault=="name")actor.Name="another";
        if(fault=="side")actor.Friendly=!actor.Friendly;if(fault=="npc")actor.Npc++;if(fault=="wirephase")actor.Phase++;
        if(fault=="loot")actor.Lootable=!actor.Lootable;if(fault=="duplicate")after.Mobs.Add(actor);
        if(fault=="hostile")actor.Hostile=true;if(fault=="wave")actor.Wave=0;
        Assert.Throws<InvalidDataException>(()=>MissionNoSiteActorHistory.RequireTransition(before,after));
    }
    [Fact] public void CurrentOrForeignWitnessAndWrongOwnerCannotBeCalledHistorical()
    {
        var s=Captured();var n=JsonNode.Parse(s.NpcInterior.GetRawText())!;n["CurrentPhase"]=1;s.NpcInterior=JsonSerializer.SerializeToElement(n);
        Assert.Throws<InvalidDataException>(()=>MissionNoSiteActorHistory.RequireHistoricalContext(s));
        s=Captured();n=JsonNode.Parse(s.NpcInterior.GetRawText())!;n["Witnesses"]![0]!["Selection"]!["Phase"]=1;s.NpcInterior=JsonSerializer.SerializeToElement(n);
        Assert.Throws<InvalidDataException>(()=>MissionNoSiteActorHistory.RequireHistoricalContext(s));
        s=Captured();s.Identity=new string('e',32);Assert.Throws<InvalidDataException>(()=>MissionNoSiteActorHistory.RequireHistoricalContext(s));
        s=Captured();s.Phase++;Assert.Throws<InvalidDataException>(()=>MissionNoSiteActorHistory.RequireTransition(PriorMembership(),s));
    }
    [Fact] public void ExistingNoSiteTokenCannotForgetLaterHistoricalActorMutation()
    {
        var baseline=Captured();var current=Captured();current.Mobs[6].Health++;
        Assert.Throws<InvalidDataException>(()=>MissionNoSiteActorHistory.RequireStable(baseline,current));
    }
    [Theory] [InlineData("pending")] [InlineData("current")] [InlineData("legacy")]
    public void TopLevelFreePlacementAuthorityCannotMasqueradeAsNoSiteHistory(string fault)
    {
        var s=Captured();
        if(fault=="pending")s.PendingOpenFloor=new MissionOpenFloorPendingAdmission(1,s.Mission,s.Identity,s.Phase,s.Started,s.OpenFloorSites![0],Array.Empty<MissionOpenFloorChoice>(),null);
        if(fault=="current")s.OpenFloorPhase=s.Phase;if(fault=="legacy")s.OpenFloorLegacyPhase=s.Phase;
        Assert.Throws<InvalidDataException>(()=>MissionNoSiteActorHistory.RequireHistoricalContext(s));
    }
}
