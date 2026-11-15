using System.Text.Json;
using hds;
namespace hds.Tests;
public class AbilityLoadQualificationTests
{
    static AbilityMemory.Row Row(ushort id,bool loaded=false,ushort level=1,ushort? slot=null,uint? descriptor=null)=>new((uint)(100+id),(ushort)(slot??(id%500)),descriptor??(0x80000000u|((uint)id<<10)),level,loaded);
    static long Xp(int level)=>PlayerProgression.ExperienceForLevel(level);
    static AbilityMemory.Row[] Closed(ushort root,int level)
    {
        var g=AbilityPrerequisites.Current;
        return g.Closure(root).Select(p=>Row(p.Key,level:(ushort)Math.Max(1,p.Value))).Append(Row(root)).ToArray();
    }
    [Theory][InlineData((ushort)14,12,(ushort)9)][InlineData((ushort)15,8,(ushort)7)][InlineData((ushort)16,12,(ushort)7)]
    public void NativeAutoParentsWithCharacter99DoNotBecomeSelectedRootGates(ushort root,int level,ushort parent)
    {
        var batch=Closed(root,level);Assert.Equal((byte)99,AbilityPrerequisites.Current.Find(parent).CharacterLevel);
        Assert.Null(AbilityLoadQualification.Refusal(batch,batch,Xp(level),out _));
        Assert.Null(AbilityLoadQualification.Refusal(batch,batch.Reverse().ToArray(),Xp(level),out _));
        string direct=AbilityLoadQualification.Refusal(batch,new[]{batch.Single(r=>r.Ability==parent)},Xp(level),out bool refused);
        Assert.NotNull(direct);Assert.True(refused);
    }
    [Fact] public void AllTwentyOneSourceCounterexamplesRemainQualifiableAsExplicitClosedBatches()
    {
        string root=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(root,"hds.sln")))root=Path.GetDirectoryName(root)!;
        using var manifest=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"research","abilities","ability-load-prerequisites.json")));
        var examples=manifest.RootElement.GetProperty("parentCharacterCounterexamples");Assert.Equal(21,examples.GetArrayLength());
        foreach(var example in examples.EnumerateArray())
        {
            ushort id=example.GetProperty("root").GetUInt16();int level=example.GetProperty("characterLevel").GetInt32();
            var batch=Closed(id,level);Assert.Null(AbilityLoadQualification.Refusal(batch,batch,Xp(level),out _));
        }
    }
    [Fact] public void MissingUnloadedAndUndertrainedParentsAreRefusedButSameBatchWorks()
    {
        var parent=Row(2);var child=Row(217);var rows=new[]{parent,child};
        Assert.NotNull(AbilityLoadQualification.Refusal(rows,new[]{child},0,out _));
        Assert.Null(AbilityLoadQualification.Refusal(rows,rows,0,out _));
        Assert.NotNull(AbilityLoadQualification.Refusal(new[]{Row(2,true,0),child},new[]{child},0,out _));
        Assert.NotNull(AbilityLoadQualification.Refusal(new[]{Row(2,true,ushort.MaxValue),child},new[]{child},0,out _)); //Native MOVSX rejects wrapped negative base levels.
        Assert.Null(AbilityLoadQualification.Refusal(new[]{Row(2,true),child},new[]{child},0,out _));
    }
    [Fact] public void ExistingInvalidNoopRootCannotExcuseForbiddenNewParent()
    {
        var child=Row(15,true);var parent=Row(7);var rows=new[]{child,parent};
        Assert.NotNull(AbilityLoadQualification.Refusal(rows,rows, Xp(8),out bool refused));Assert.True(refused);
        Assert.Null(AbilityLoadQualification.Refusal(rows,new[]{child},Xp(8),out _));
    }
    [Fact] public void ArchivedParentTrainingCapDoesNotBecomeGlobalSelectedOrCurrentCap()
    {
        var parent=Row(2,false,50);var child=Row(217,false,50);
        Assert.NotNull(AbilityLoadQualification.Refusal(new[]{parent,child},new[]{parent,child},0,out _));
        parent=Row(2,true,50);Assert.Null(AbilityLoadQualification.Refusal(new[]{parent,child},new[]{child},0,out _));
        Assert.Null(AbilityLoadQualification.Refusal(new[]{child},Array.Empty<AbilityMemory.Row>(),0,out _));
    }
    [Fact] public void HiddenCodesClientLevelsAndAliasesCannotCreateMissingRealParentMembership()
    {
        var child=Row(217);var hidden=Row(2,true,50,520);var code=Row(2,true,50,10,0x84000800);
        Assert.NotNull(AbilityLoadQualification.Refusal(new[]{hidden,code,child},new[]{child},0,out _));
        var alias=Row(2,true,1,10,0x80400800);Assert.Null(AbilityLoadQualification.Refusal(new[]{alias,child},new[]{child},0,out _));
    }
    [Fact] public void CharacterRequirementUsesExperienceRatherThanPersistedLevelParameter()
    {
        var row=Row(7);var result=AbilityMemory.Plan(new[]{row},new[]{new AbilityProgression.LoadoutEntry{Ability=7,Slot=row.Slot,Level=99}},false,0,50);
        Assert.NotNull(result.Refusal);Assert.True(result.LevelRefused);Assert.Empty(result.Accepted);
        Assert.Null(AbilityMemory.Plan(new[]{row.WithLoaded(true)},Array.Empty<AbilityProgression.LoadoutEntry>(),false,0,50).Refusal);
        Assert.Null(AbilityMemory.Plan(new[]{row.WithLoaded(true)},new[]{new AbilityProgression.LoadoutEntry{Ability=7,Slot=row.Slot}},true,0,1).Refusal);
    }
    [Fact] public void CyclicOrSelfRequiredNewBatchFailsClosedWithoutRepairingLegacyState()
    {
        var graph=AbilityPrerequisites.Parse("{\"schema_version\":1,\"abilities\":[{\"id\":1,\"components\":[{\"name\":\"A\",\"type\":39}],\"fields\":{\"A/ReqAbilityID\":2147484672}}]}");
        var row=Row(1);Assert.NotNull(AbilityLoadQualification.Refusal(new[]{row},new[]{row},0,out _,graph));
        Assert.Null(AbilityLoadQualification.Refusal(new[]{row.WithLoaded(true)},new[]{row.WithLoaded(true)},0,out _,graph));
    }
    [Fact] public void NativeProfileOwnsTheRealClosedLowTierGraphWithoutDeveloperBypass()
    {
        var plan=new NativeQa.Modes.AbilityPrerequisiteMode().Plan;
        Assert.False(plan.DevCommands);Assert.False(plan.GmCharacter110);Assert.False(plan.FixedCallbackRolls);
        Assert.Equal(new ushort[]{2,17,113,130,217,628},plan.Abilities);
        Assert.Contains(plan.AfterHelpers,s=>s.Contains("level=1,exp=0"));
        Assert.Equal(new ushort[]{2,113},AbilityPrerequisites.Current.Closure(130).Keys.Order());
        Assert.Contains("slot<520",HeadlessClient.AbilityPrerequisiteFixture.ArchiveSql(110));
    }
    [Theory][InlineData((ushort)520)][InlineData(ushort.MaxValue)]
    public void HiddenSlotsCannotBeAcceptedEvenWhenPureCallerSuppliesSuchRows(ushort slot)
    {
        var row=Row(2,false,1,slot);var request=new AbilityProgression.LoadoutEntry{Slot=slot,Ability=2,Level=1};
        Assert.Empty(AbilityMemory.Plan(new[]{row},new[]{request},false,0,1).Accepted);
        Assert.Empty(AbilityMemory.Plan(new[]{row.WithLoaded(true)},new[]{request},true,0,1).Accepted);
        var real=Row(2,false,1,519);request.Slot=519;
        Assert.Single(AbilityMemory.Plan(new[]{real},new[]{request},false,0,1).Accepted);
    }
    [Fact] public void ActualCharacter33RootUsesLockedExperienceInBothMismatchDirections()
    {
        var rows=Closed(358,33);var request=rows.Select(r=>new AbilityProgression.LoadoutEntry{Slot=r.Slot,Ability=r.Ability,Level=999}).ToArray();
        var below=AbilityMemory.Plan(rows,request,false,Xp(32),50);
        Assert.NotNull(below.Refusal);Assert.True(below.LevelRefused);Assert.Empty(below.Accepted);
        var at=AbilityMemory.Plan(rows,request,false,Xp(33),1);
        Assert.Null(at.Refusal);Assert.False(at.LevelRefused);Assert.Equal(rows.Length,at.Accepted.Length);
        Assert.Equal(49,at.Used); //Actual Code Shield closure fits the sourced level33 capacity.
    }
}
