using hds;

namespace hds.Tests;

public class NpcCreationCostTests
{
    [Fact]
    public void NpcCreationMatchesTheDefinitionTableObjectWithoutBuildingTheWholeTable()
    {
        var mob=new Mob();mob.setName("Blackwood Goof");mob.setRsiHex("d9040058");mob.setHealthM(175);mob.setHealthC(120);mob.setLevel(3);
        mob.setXPos(18924);mob.setYPos(95);mob.setZPos(-8852);
        var table=new GameObjectDefinitions().Object599;
        var created=mob.getCreationData();
        Assert.IsType(table.GetType(),created);
        Assert.Equal(table.GetGoid(),created.GetGoid());
        Assert.Equal(table.GetName(),created.GetName());
        // The scale run spent ~88 ms per NPC view building every definition; 200 creations stay far under a second.
        var clock=System.Diagnostics.Stopwatch.StartNew();
        for(int i=0;i<200;i++)Assert.NotEmpty(mob.getCreationData().GetCreationAttributes());
        Assert.InRange(clock.Elapsed.TotalSeconds,0,2);
    }
}
