using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
using hds.shared;
namespace hds.Tests;
[Collection("DelayedActions")]
public class AbilityTrainingTests
{
    [Theory]
    [InlineData(2,1,1,800u)][InlineData(37,1,3,2900u)][InlineData(1006,1,3,2900u)]
    [InlineData(477,32,1,217800u)][InlineData(298,1,49,8584800u)][InlineData(391,30,20,6694000u)]
    [InlineData(151,1,1,400u)][InlineData(1112,1,1,400u)]
    [InlineData(186,1,1,260900u)][InlineData(621,1,1,8000u)]
    public void LiteralNativeQuotesPreserveCapturedPricesAndMasterySurcharge(int id,int old,int count,uint price)
    {Assert.True(AbilityTraining.TryCost((ushort)id,old,count,out uint actual));Assert.Equal(price,actual);Assert.Equal(price,AbilityProgression.UpgradeCost((ushort)id,old,count));}
    private static AbilityProgression.UpgradeRequest Request(ushort ability=37,ushort slot=300,byte count=1)=>new(){Ability=ability,Slot=slot,Levels=count};
    private static AbilityTraining.ArchiveRow Row(ushort ability=37,int level=14,int slot=300,bool unsigned=false)=>new(10,slot,unsigned?0x80000000L+((long)ability<<10):unchecked((int)(0x80000000u|((uint)ability<<10))),level,true);
    [Fact] public void PersistedExperienceAndPositiveAbilityCapAreIndependentLimits()
    {
        long xp=PlayerProgression.ExperienceForLevel(50);var r=Request();var row=Row();
        Assert.True(AbilityTraining.Admit(r,row,xp,out var level,out var cost));Assert.Equal(15,level);Assert.Equal(22500u,cost);
        Assert.False(AbilityTraining.Admit(r,Row(level:15),xp,out _,out _));
        Assert.False(AbilityTraining.Admit(r,row,0,out _,out _)); //stored/avatarLevel50 cannot substitute for EXP
        Assert.True(AbilityTraining.Admit(Request(2),Row(2,1),PlayerProgression.ExperienceForLevel(2),out _,out _));
        Assert.False(AbilityTraining.Admit(Request(44),Row(44,10),xp,out _,out _));
        Assert.False(AbilityTraining.Admit(r,row,-1,out _,out _));Assert.False(AbilityTraining.Admit(r,row,(long)uint.MaxValue+1,out _,out _));
    }
    [Fact] public void DescriptorClassificationAcceptsOnlyExactLegacy32BitRepresentations()
    {
        Assert.True(AbilityTraining.DescriptorMatches(Row().Descriptor,37));Assert.True(AbilityTraining.DescriptorMatches(Row(unsigned:true).Descriptor,37));
        Assert.False(AbilityTraining.DescriptorMatches(0x84009400,37));Assert.False(AbilityTraining.DescriptorMatches(0x180009400L,37));
        Assert.True(AbilityTraining.DescriptorMatches(0x80009401,37));Assert.False(AbilityTraining.DescriptorMatches(0x80009400,38));
        Assert.False(AbilityTraining.Ordinary(0));Assert.False(AbilityTraining.Ordinary(706));Assert.False(AbilityTraining.Ordinary(4133));
    }
    [Theory][InlineData(-1)][InlineData(51)][InlineData(65535)]
    public void CorruptOwnedLevelsAreNotRewrittenOrQuoted(int old)=>Assert.False(AbilityTraining.Admit(Request(),Row(level:old),PlayerProgression.ExperienceForLevel(50),out _,out _));
    [Fact] public void NativeCanonicalAliasesAndStoredZeroLevelRemainTrainable()
    {
        //Legacy native family ID4452 aliases canonical356; low descriptor bits do not change the ID.
        Assert.True(AbilityTraining.DescriptorMatches(0x80000000L+(4452L<<10)+7,356));
        Assert.True(AbilityTraining.Admit(Request(2),Row(2,0),0,out ushort level,out uint cost));
        Assert.Equal((ushort)1,level);Assert.Equal(200u,cost);
        var rows=new[]{Row(37,14,300),Row(37,1,301)};
        Assert.False(AbilityTraining.UniqueOwnedAbility(rows,37));
        Assert.True(AbilityTraining.UniqueOwnedAbility(new[]{rows[0],new AbilityTraining.ArchiveRow(13,301,0x84009400,1,false)},37));
        Assert.False(AbilityTraining.UniqueOwnedAbility(new[]{Row(356,1,300),new AbilityTraining.ArchiveRow(14,301,0x80000000L+(4452L<<10),1,false)},356));
    }
    [Fact] public void TrainingErrorStatusTakesNativeSignedPositiveErrorBranch()
    {
        Assert.Equal(0x1800000cu,AbilityTraining.FailureStatus);
        Assert.True(unchecked((int)AbilityTraining.FailureStatus)>=1);
        Assert.True(unchecked((int)ServerPackets.StatusAbilityUpgradeRefused)<1); //legacy constant is intentionally untouched
    }
    [Fact] public void PostCommitCallbacksDoNotHoldFenceAndEpochChangeSuppressesPublication()
    {
        var c=Client();var d=c.playerData;long epoch=c.TransportFence.Capture();bool published=false;
        AbilityTraining.CompleteCurrent(c,epoch,()=>AbilityTraining.Current(c,d,0,0,0,epoch,110),()=>
        {
            //Another thread can enter the fence while reconciliation is running: holder->fence cannot deadlock against us.
            var other=System.Threading.Tasks.Task.Run(()=>c.TransportFence.TryRun(epoch,()=>{}));
            Assert.True(other.Wait(TimeSpan.FromSeconds(2)));Assert.True(other.Result);
            c.TransportFence.PublishIncoming(true,()=>{});
        },()=>published=true);
        Assert.False(published);
        bool reconciled=false;
        AbilityTraining.CompleteCurrent(c,epoch,()=>true,()=>reconciled=true,()=>published=true);
        Assert.False(reconciled);Assert.False(published);
    }
    [Fact] public void PostCommitTutorialRunsAfterPublicationOutsideFenceAndNeverAfterSuppressedPublication()
    {
        var c=Client();long epoch=c.TransportFence.Capture();var order=new List<string>();
        AbilityTraining.CompleteCurrent(c,epoch,()=>true,()=>order.Add("state"),()=>order.Add("packets"),()=>
        {
            var other=System.Threading.Tasks.Task.Run(()=>c.TransportFence.TryRun(epoch,()=>{}));
            Assert.True(other.Wait(TimeSpan.FromSeconds(2)));Assert.True(other.Result);order.Add("tutorial");
        });
        Assert.Equal(new[]{"state","packets","tutorial"},order);
        bool tutorial=false;
        AbilityTraining.CompleteCurrent(c,epoch,()=>c.TransportFence.Capture()==epoch,()=>{},()=>c.TransportFence.PublishIncoming(true,()=>{}),()=>tutorial=true);
        Assert.False(tutorial);
    }
    [Theory]
    [InlineData(-1,0,false,0)][InlineData(799,800,false,0)][InlineData(800,800,true,0)]
    [InlineData(4294967295L,0,true,4294967295L)][InlineData(4294968095L,800,true,4294967295L)]
    [InlineData(4294968096L,800,false,4294967296L)][InlineData(long.MaxValue,800,false,long.MaxValue-800)]
    public void OnlyResultingInformationMustFitNativeUnsignedField(long cash,uint cost,bool valid,long expected)
    {Assert.Equal(valid,AbilityTraining.TryRemainingInfo(cash,cost,out long remaining));Assert.Equal(expected,remaining);}
    [Fact] public void NativeBodyIsExactAndCountPositive()
    {
        Assert.NotNull(AbilityProgression.ParseUpgrade(Convert.FromHexString("dd01370101")));
        Assert.Null(AbilityProgression.ParseUpgrade(Convert.FromHexString("dd0137010100")));
        Assert.Null(AbilityProgression.ParseUpgrade(Convert.FromHexString("dd01370100")));
        Assert.False(AbilityTraining.Admit(Request(slot:520),Row(slot:520),PlayerProgression.ExperienceForLevel(50),out _,out _));
        Assert.False(AbilityTraining.TryCost(37,1,255,out _));Assert.False(AbilityTraining.TryCost(706,1,1,out _));
    }
    [Fact] public void CommittedSnapshotRetainsOtherOwnedLoadedRowsAndFiltersCodeFamily()
    {
        var rows=new[]{Row(52,50,300),Row(45,50,301),new AbilityTraining.ArchiveRow(12,302,0x8404c180,1,true),new AbilityTraining.ArchiveRow(13,520,0x80013800,11,true)};
        var result=new AbilityTraining.Result(true,52,50,500000,1,"",AbilityTraining.LoadedSnapshot(rows));
        Assert.Equal(new[]{((ushort)52,(ushort)50),((ushort)45,(ushort)50)},result.Loaded);
        rows[0]=Row(52,1,300);Assert.Equal((ushort)50,result.Loaded[0].Level); //immutable values, not mutable DB objects
    }
    [Fact] public void UnloadedTrainingCannotGrantAndLoadedOtherCopyKeepsItsEffectiveLevel()
    {
        Assert.Empty(AbilityTraining.GrantRefreshes(new(true,37,15,1,1,"")));
        Assert.Equal(new[]{((ushort)970,(ushort)0)},AbilityTraining.GrantRefreshes(new(true,37,15,1,1,"",new[]{((ushort)37,(ushort)15)})));
        Assert.Equal(new[]{((ushort)17,(ushort)50)},AbilityTraining.GrantRefreshes(new(true,2,2,1,1,"",new[]{((ushort)2,(ushort)50)})));
        Assert.Empty(AbilityTraining.GrantRefreshes(new(false,37,15,1,1,"",new[]{((ushort)37,(ushort)15)})));
        Assert.Empty(AbilityTraining.GrantRefreshes(new(true,143,2,1,1,"",new[]{((ushort)143,(ushort)2)}))); //207 owns observed zero-grant registration
    }
    [Fact] public void NativeProfileOwnsStoredPrerequisiteAndFolderClosureWithoutFreeGrants()
    {
        var rows=HeadlessClient.AbilityTrainingFixture.Rows.ToDictionary(r=>r.Ability);
        foreach(var row in rows.Values)
        {
            var entry=AbilityCatalog.Find(row.Ability)!;
            foreach(var property in entry.Fields.EnumerateObject().Where(p=>p.Name.EndsWith("/ReqAbilityID")))
            {
                ushort parent=AbilityCatalog.IdOf(property.Value.GetUInt32());if(parent==0)continue;
                int need=Math.Max(1,(int)entry.Number(property.Name[..property.Name.LastIndexOf('/')]+"/ReqAbilityLevel",1));
                Assert.True(rows.ContainsKey(parent));Assert.True(rows[parent].Level>=need);
            }
            ushort folder=AbilityCatalog.IdOf((uint)entry.Number("Ability/DisciplineBelongsTo"));if(folder!=0)Assert.Contains(folder,rows.Keys);
            Assert.False(row.Ability>=4096);
        }
        var plan=new NativeQa.Modes.AbilityTrainingMode().Plan;
        Assert.False(plan.DevCommands);Assert.False(plan.GmCharacter110);Assert.False(plan.FixedCallbackRolls);
        Assert.Contains("260900",string.Join(" ",plan.ReadyMessages));Assert.Contains("199400",string.Join(" ",plan.ReadyMessages));
    }
    private static WorldClient Client()
    {
        var c=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));c.playerData=new ClientData();c.playerData.setCharID(110);
        c.playerData.setOnWorld(true);c.playerInstance=new PlayerCharacter();c.messageQueue=new MessageQueue();c.Alive=true;
        typeof(WorldClient).GetField("lastUsedTime",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(c,TimeUtils.getUnixTimeUint32());
        typeof(WorldClient).GetField("lastFlushTick",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(c,long.MaxValue);
        typeof(WorldClient).GetField("<TransportFence>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(c,new WorldSendFence());return c;
    }
    [Fact] public void TrainingPublicationDoesNotRequireWorldOrAvatarAvailability()
    {
        var c=Client();c.playerData.setOnWorld(false);c.playerInstance=null!;
        int world=c.playerData.worldGeneration;Assert.True(AbilityTraining.Current(c,c.playerData,0,world,0,c.TransportFence.Capture(),110));
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)]
    public void LatePublicationRejectsOtherGenerationWorldLifeOrTransport(int change)
    {
        var c=Client();var d=c.playerData;long epoch=c.TransportFence.Capture();
        Assert.True(AbilityTraining.Current(c,d,0,0,0,epoch,110));
        if(change==0)d.trainingGeneration++;if(change==1)d.worldGeneration++;if(change==2)d.lifeGeneration++;if(change==3)c.TransportFence.PublishIncoming(true,()=>{});if(change==4)d.setCharID(111);
        Assert.False(AbilityTraining.Current(c,d,0,0,0,epoch,110));
    }
    [Fact] public void TruncatedTrainingCannotQueueFutureTutorialCompletion()
    {
        var old=Store.currentClient;
        try
        {
            var c=Client();Store.currentClient=c;
            var tutorial=(TutorialMission)Activator.CreateInstance(typeof(TutorialMission),BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{c},null)!;
            c.playerData.tutorial=tutorial;byte[] truncated={2,0,1};new RpcManager().HandleRpc(0xb7,ref truncated);
            var held=(HashSet<long>)typeof(TutorialMission).GetField("held",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(tutorial)!;
            Assert.Empty(held); //old unconditional RpcManager callback queued30..33 despite parser refusal
        }
        finally{Store.currentClient=old;}
    }
}
