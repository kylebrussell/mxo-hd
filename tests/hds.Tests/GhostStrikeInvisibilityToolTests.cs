using hds;
using HeadlessClient;
using NativeQa;
namespace hds.Tests;
public class GhostStrikeInvisibilityToolTests
{
    [Fact] public void NormalParentClosureFitsLevel50WithoutManuallyOwnedHiddenGrant()
    {
        var ids=GhostStrikeFixture.Abilities;var graph=AbilityPrerequisites.Current;
        var required=graph.Closure(175).Keys.Concat(graph.Closure(365).Keys).Append((ushort)175).Append((ushort)365).Append((ushort)17).Distinct().Order().ToArray();
        Assert.Equal(required,ids.Order());Assert.DoesNotContain((ushort)1116,ids);
        Assert.Equal(54,ids.Sum(AbilityMemory.Cost));Assert.Equal(100,AbilityMemory.ForLevel(50));
        var rows=ids.Select((id,i)=>new AbilityMemory.Row((uint)i+1,(ushort)i,0x80000000u|((uint)id<<10),50,false)).ToArray();
        var request=rows.Select(r=>new AbilityProgression.LoadoutEntry{Slot=r.Slot,Ability=r.Ability,Level=50}).ToArray();
        var plan=AbilityMemory.Plan(rows,request,false,PlayerProgression.ExperienceForLevel(50),50);
        Assert.Null(plan.Refusal);Assert.Equal(rows.Length,plan.Accepted.Length);
        Assert.Contains((ushort)1116,AbilityMemory.Membership(plan.Loaded));
        Assert.Contains((ushort)1116,AbilityProgression.GrantsOf(175));
        Assert.DoesNotContain(ids.SelectMany(id=>AbilityCatalog.Find(id).Modifiers),m=>m.Target==117&&m.Number("WhenEnabled",-1)==0);
    }
    [Fact] public void CurrentOwnedBeltSlotIsDecodedFromMarginInventoryAndAmbiguityIsRejected()
    {
        var row=new byte[10];BitConverter.GetBytes((ushort)40).CopyTo(row,0);BitConverter.GetBytes(460u).CopyTo(row,2);
        Assert.Equal((byte)40,GhostStrikeInvisibilityTool.OwnedSlot(row));
        Assert.Throws<InvalidDataException>(()=>GhostStrikeInvisibilityTool.OwnedSlot(row[..^1]));
        Assert.Throws<ScenarioFailure>(()=>GhostStrikeInvisibilityTool.OwnedSlot(row.Concat(row).ToArray()));
        row[0]=41;Assert.Throws<ScenarioFailure>(()=>GhostStrikeInvisibilityTool.OwnedSlot(row));
    }
    [Fact] public void OrdinaryToolRequestUsesActualBeltSlotAndNoPillOrCompilerFlag()
    {
        var rpc=GhostStrikeInvisibilityTool.Request(40);Assert.Equal(43,rpc.Length);
        Assert.Equal("80B95C040200",Convert.ToHexString(rpc[..6]));Assert.Equal(40,rpc[37]);Assert.Equal(0,rpc[38]);
        Assert.Throws<ArgumentOutOfRangeException>(()=>GhostStrikeInvisibilityTool.Request(41));
        Assert.Throws<InvalidDataException>(()=>GhostStrikeInvisibilityTool.AbilityIds(new byte[5]));
        var mode=NativeQaModes.Find("ghost-strike-callback");Assert.Contains("460,40",string.Join("\n",mode.Plan.AfterHelpers));
        Assert.Contains("Activate Item",string.Join("\n",mode.Plan.ReadyMessages));
    }
}
