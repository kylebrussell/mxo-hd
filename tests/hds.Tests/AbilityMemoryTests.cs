using hds;
using NativeQa;
namespace hds.Tests;
public class AbilityMemoryTests
{
    private static AbilityMemory.Row Row(uint key,ushort slot,ushort id,bool loaded=true) =>
        new(key,slot,0x80000000u|((uint)id<<10),50,loaded);
    private static AbilityProgression.LoadoutEntry E(ushort slot,ushort id)=>new(){Slot=slot,Ability=id,Level=999};
    [Fact] public void ExactNativeCapacityTableAndExperienceThresholds()
    {
        int[] expected={16,24,27,30,33,36,37,38,39,40,41,42,43,44,45,46,47,48,49,50,51,52,53,54,55,56,57,58,59,60,62,64,66,68,70,72,74,76,78,80,82,84,86,88,90,92,94,96,98,100};
        for(int level=1;level<=50;level++)
        {
            Assert.Equal(expected[level-1],AbilityMemory.ForLevel(level));
            long at=PlayerProgression.ExperienceForLevel(level);
            Assert.Equal(expected[level-1],AbilityMemory.ForExperience(at));
            if(level>1) Assert.Equal(expected[level-2],AbilityMemory.ForExperience(at-1));
        }
        Assert.Equal(16,AbilityMemory.ForExperience(0));Assert.Equal(16,AbilityMemory.ForExperience(-1));
        Assert.Equal(100,AbilityMemory.ForExperience(long.MaxValue));
    }
    [Fact] public void ExactCapDuplicateAndOneOverAreSetBased()
    {
        var rows=new[]{Row(1,1,23),Row(2,2,356),Row(3,3,217,false),Row(4,4,113,false),Row(5,5,2)}; //Legacy15; legal new1, then2
        var exact=AbilityMemory.Plan(rows,new[]{E(3,217),E(3,4313)},false,0,50);
        Assert.Null(exact.Refusal);Assert.Single(exact.Accepted);Assert.Equal(16,exact.Used);
        var over=AbilityMemory.Plan(exact.Loaded.Append(rows[3]).ToArray(),new[]{E(4,113)},false,0,50);
        Assert.NotNull(over.Refusal);Assert.Empty(over.Accepted);Assert.Equal(16,over.Used);
    }
    [Fact] public void GrantsAreNotASecondChargeAndPersistedLevelCannotOverrideExp()
    {
        var rows=new[]{Row(1,1,356),Row(2,2,1087),Row(3,3,23,false),Row(4,4,75,false)};
        Assert.Equal(5,AbilityMemory.Plan(rows,Array.Empty<AbilityProgression.LoadoutEntry>(),false,0,50).Used);
        Assert.NotNull(AbilityMemory.Plan(rows,new[]{E(3,23),E(4,75)},false,0,50).Refusal);
    }
    [Fact] public void OverCapCanUnloadOrAddZeroButCannotAddPositive()
    {
        var rows=new[]{Row(1,1,23),Row(2,2,71),Row(3,3,2,false),Row(4,4,75,false)};
        Assert.Null(AbilityMemory.Plan(rows,new[]{E(1,23)},false,0,50).Refusal);
        Assert.Null(AbilityMemory.Plan(rows,new[]{E(3,2)},false,0,50).Refusal);
        Assert.NotNull(AbilityMemory.Plan(rows,new[]{E(4,75)},false,0,50).Refusal);
        var off=AbilityMemory.Plan(rows,new[]{E(1,23)},true,0,1);Assert.Null(off.Refusal);Assert.Equal(10,off.Used);
    }
    [Fact] public void CodeFamilyAndAmbiguousSlotsCannotMasqueradeAsKnownAbility()
    {
        var code=new AbilityMemory.Row(1,1,0x8404c180,0,true);Assert.Equal((ushort)304,code.Ability);
        Assert.False(AbilityMemory.IsAbility(code.Descriptor));
        var rows=new[]{code,Row(2,2,217,false),Row(3,3,2),Row(4,4,314,false),Row(5,4,314,false)};
        Assert.Empty(AbilityMemory.Plan(rows,new[]{E(1,304)},true,0,50).Accepted);
        Assert.Null(AbilityMemory.Plan(rows,new[]{E(2,217)},false,0,50).Refusal);
        Assert.Null(AbilityMemory.Plan(rows,new[]{E(3,2)},false,0,50).Refusal);
        Assert.Empty(AbilityMemory.Plan(rows,new[]{E(4,314)},false,0,50).Accepted);
    }
    [Fact] public void DistinctOwnedDuplicateIdsUnloadByExactPrimaryKeys()
    {
        var rows=new[]{Row(10,1,75),Row(11,2,75)};
        var result=AbilityMemory.Plan(rows,new[]{E(1,75),E(2,75),E(1,75)},true,0,50);
        Assert.Equal(2,result.Accepted.Length);Assert.Empty(result.Loaded);Assert.Equal(0,result.Used);
    }
    [Fact] public void ExistingCharacterLevelFilterAndUnknownUnloadRecoveryRemain()
    {
        var rows=new[]{Row(1,1,7,false),Row(2,2,4095),Row(3,3,314,false)};
        var result=AbilityMemory.Plan(rows,new[]{E(1,7)},false,0,50);Assert.True(result.LevelRefused);Assert.Empty(result.Accepted);
        Assert.NotNull(AbilityMemory.Plan(rows,new[]{E(3,314)},false,0,50).Refusal);
        Assert.Null(AbilityMemory.Plan(rows,new[]{E(2,4095)},true,0,1).Refusal);
        var unknownNew=new[]{Row(5,5,4095,false)};
        Assert.Empty(AbilityMemory.Plan(unknownNew,new[]{E(5,4095)},false,0,50).Accepted);
        Assert.Empty(AbilityMemory.Plan(unknownNew,new[]{E(5,4095)},false,0,50).Loaded);
    }
    [Fact] public void DescriptorAliasesShareCanonicalCostAndUnloadIdentity()
    {
        var alias=new AbilityMemory.Row(2,2,0x80459000,50,true);
        Assert.Equal((ushort)356,alias.Ability);
        var rows=new[]{Row(1,1,356),alias};
        var repeat=AbilityMemory.Plan(rows,new[]{E(2,4452)},false,0,50);
        Assert.Equal(5,repeat.Used);Assert.Single(repeat.Accepted);
        var off=AbilityMemory.Plan(rows,new[]{E(2,356)},true,0,50);
        Assert.Single(off.Accepted);Assert.Single(off.Loaded);Assert.Equal(5,off.Used);
    }
    [Fact] public void OneDuplicateUnloadsWithoutRemovingRemainingMembership()
    {
        var rows=new[]{Row(10,1,356),Row(11,2,356)};
        var result=AbilityMemory.Plan(rows,new[]{E(1,356)},true,0,50);
        Assert.Single(result.Loaded);Assert.Equal(5,result.Used);
        Assert.Contains((ushort)356,AbilityMemory.Membership(result.Loaded));
        Assert.Contains((ushort)1087,AbilityMemory.Membership(result.Loaded));
        var off=AbilityMemory.Plan(result.Loaded,new[]{E(2,356)},true,0,50);
        Assert.DoesNotContain((ushort)356,AbilityMemory.Membership(off.Loaded));
        Assert.DoesNotContain((ushort)1087,AbilityMemory.Membership(off.Loaded));
    }
    [Fact] public void FixtureUsesExplicitXpAndNoRandomOrGmOverride()
    {
        var plan=NativeQaModes.Find("ability-memory").Plan;
        Assert.Contains(plan.AfterHelpers,s=>s.Contains("exp=0"));
        Assert.False(plan.GmCharacter110);Assert.False(plan.FixedCallbackRolls);Assert.False(plan.DevCommands);
    }
}
