using hds;
using HeadlessClient;
using NativeQa.Modes;
using System.Xml;
namespace hds.Tests;
public class MachinesChainTests
{
    private static Mission Load(int id)
    {
        foreach(string file in Directory.GetFiles(DataLoader.DataPath("missions"),"machines_*.xml"))
        {
            var doc=new XmlDocument();doc.Load(file);var mission=MissionXml.Parse(doc.DocumentElement!);
            if(mission.id==id)return mission;
        }
        throw new Exception("Missing Machines mission "+id);
    }
    [Fact]
    public void FullAgentGrayPrimaSequenceUsesActualContactAndNoSelectedCompletionGrants()
    {
        var chain=MachinesChainFixture.Missions.Select(Load).ToList();
        Assert.Equal(25,chain.Sum(m=>m.phases.Count));Assert.Equal(52,chain.Sum(m=>m.phases.Sum(p=>p.missionObjectives.Count)));
        Assert.Equal(Enumerable.Range(11,10),chain.Select(m=>m.order));
        Assert.All(chain,m=>{Assert.Equal("Agent Gray",m.contact);Assert.Equal((byte)2,m.org);Assert.True(MissionContacts.Offers(2,m));});
        Assert.Equal(2001u,MissionContacts.ForOrg(2).id);
        var completed=MachinesChainFixture.Prerequisites.ToHashSet();
        Assert.Empty(completed.Intersect(MachinesChainFixture.Missions));
        foreach(var expected in chain)
        {Assert.Equal(expected.id,Assert.Single(DataLoader.ContactOffer(chain,completed)).id);completed.Add(expected.id);}
        Assert.Equal(chain.Count,DataLoader.ContactOffer(chain,completed).Count);
    }
    [Fact]
    public void EveryGoalReferencesAnAvailableNpcObjectAndInPhasePredecessor()
    {
        foreach(var mission in MachinesChainFixture.Missions.Select(Load))
        for(int phase=0;phase<mission.phases.Count;phase++)
        {
            var area=mission.phases[phase];Assert.NotEmpty(area.missionObjectives);
            foreach(var goal in area.missionObjectives)
            {
                if(goal.afterGoal>=0)Assert.Contains(area.missionObjectives,g=>g.goal==goal.afterGoal);
                if(goal.objectId!=0)Assert.Contains(area.objects,o=>o.id==goal.objectId);
                foreach(ushort npcId in goal.Npcs.Where(n=>n!=0))
                {
                    var npc=Assert.Single(mission.npcs.Where(n=>n.id==npcId));Assert.True(npc.at<=phase+1);
                }
                if(goal.type is MissionGoalType.Give or MissionGoalType.Put)
                    Assert.True(MissionRuntime.Provided(mission,phase,goal.item));
            }
        }
    }
    [Fact]
    public void AntaresHasRealTalkAndGetMenusBeforeBecomingTheRequiredHostile()
    {
        var mission=Load(2210);var progress=new MissionProgress(mission){phaseIndex=2};var antares=mission.npcs.Single(n=>n.id==2);
        var goals=mission.phases[2].missionObjectives;
        Assert.True(progress.IsFriendly(antares));Assert.Equal(2,antares.hostileAfter);Assert.False(antares.attacksOnSight);
        Assert.True(progress.IsOpen(goals.Single(g=>g.type==MissionGoalType.Talk)));
        progress.Complete(goals.Single(g=>g.type==MissionGoalType.Talk));
        Assert.True(progress.IsFriendly(antares));Assert.True(progress.IsOpen(goals.Single(g=>g.type==MissionGoalType.Get)));
        Assert.False(progress.IsOpen(goals.Single(g=>g.type==MissionGoalType.DefeatAll)));
        progress.Complete(goals.Single(g=>g.type==MissionGoalType.Get));
        Assert.False(progress.IsFriendly(antares));Assert.Contains((ushort)2,progress.justSwitched);
        Assert.True(progress.IsOpen(goals.Single(g=>g.type==MissionGoalType.DefeatAll)));
    }
    [Fact]
    public void OperativeDossierUsesExistingCorpseLootBeforeKillTimeWithoutRemovingKillAllRequirement()
    {
        var mission=Load(2207);var progress=new MissionProgress(mission){phaseIndex=1};
        var loot=mission.phases[1].missionObjectives.Single(g=>g.type==MissionGoalType.Loot);
        Assert.Equal(6848u,loot.item);Assert.True(loot.Has(4));Assert.True(loot.Has(5));Assert.True(progress.IsOpen(loot));
        Assert.DoesNotContain(mission.phases[1].objects,o=>o.kind=="Furniture");
        foreach(ushort operative in new ushort[]{4,5})
        {
            var data=new ClientData{missionProgress=progress};var corpse=new Mob{missionNpcId=operative};
            Assert.Equal(6848u,MissionRuntime.LootItemFor(data,corpse));
        }
        progress.Complete(loot);Assert.False(progress.AllDone);
        Assert.Contains(mission.phases[1].missionObjectives,g=>g.type==MissionGoalType.DefeatAll);
        Assert.True(MissionRuntime.Provided(mission,2,6848));
    }
    [Fact]
    public void NativeModeOwnsOnlyPrerequisitesAndLayoutSeedWithNoCombatRollOverride()
    {
        var plan=new MachinesChainMode().Plan;
        Assert.Equal(MachinesChainFixture.SeedPrerequisites(110),plan.AfterHelpers[0]);
        Assert.True(plan.DevCommands);Assert.False(plan.GmCharacter110);Assert.False(plan.FixedCallbackRolls);
        Assert.Equal("220110",plan.EnvironmentOverrides["MXO_MISSION_SITE_QA_SEED"]);Assert.Single(plan.EnvironmentOverrides);
        Assert.Equal(25,plan.ExtraQaMetadata["MachinesPrimaPhases"].GetInt32());
        Assert.Equal(52,plan.ExtraQaMetadata["MachinesPrimaGoals"].GetInt32());
        Assert.Contains("alignment=2",plan.AfterHelpers[1]);
    }
}
