using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
using hds.shared;
namespace hds.Tests;
[Collection("DelayedActions")]
public class AttributeAllocationTests
{
    private static AttributeAllocation.Row Row(byte a,long? value=null,int? slot=null,long? raw=null,uint identity=1)=>new(identity,slot??CharacterAttributes.SlotOf(a),raw??CharacterAttributes.RowValueOf(a),value??CharacterAttributes.BaseValue(a));
    private static AttributeAllocation.Row[] Fresh()=>CharacterAttributes.All.Select((a,i)=>Row(a,identity:(uint)i+1)).ToArray();
    private static CharacterAttributes.Raise[] Raises(params byte[] attrs)=>attrs.Select(a=>new CharacterAttributes.Raise{Attribute=a,Points=1}).ToArray();
    [Theory]
    [InlineData("04000100005100000001",1)][InlineData("04000200004e00000001005100000002",2)]
    public void NativeWholeBatchDecodes(string hex,int count)=>Assert.Equal(count,CharacterAttributes.ParseRaises(Convert.FromHexString(hex)).Count);
    [Theory]
    [InlineData("")][InlineData("040001000051000000")][InlineData("0400010000510000000100")]
    [InlineData("00000100005100000001")][InlineData("04000000")][InlineData("04000600")]
    [InlineData("04000100015100000001")][InlineData("04000100005101000001")][InlineData("04000100005100000000")]
    [InlineData("04000200005100000001005100000001")][InlineData("04000200005100000001")]
    public void InvalidEntireBatchesNeverAcceptPrefixesOrByteAliases(string hex)=>Assert.Empty(CharacterAttributes.ParseRaises(Convert.FromHexString(hex)));
    [Fact] public void NativePointBudgetIsSharedAndCapAppliesToShownValue()
    {
        var rows=Fresh();long xp=PlayerProgression.ExperienceForLevel(2);
        Assert.True(AttributeAllocation.TryPlan(rows,Raises(78),xp,out var one));Assert.Equal(9,one[78]);
        Assert.False(AttributeAllocation.TryPlan(rows,Raises(78,81),xp,out _));
        Assert.False(AttributeAllocation.TryPlan(rows,Raises(78),0,out _));
        rows[0]=Row(78,30);Assert.False(AttributeAllocation.TryPlan(rows,Raises(78),PlayerProgression.ExperienceForLevel(50),out _));
        Assert.True(AttributeAllocation.TryPlan(rows,Raises(82),PlayerProgression.ExperienceForLevel(50),out var belief));
        Assert.Equal(6,belief[82]);Assert.Equal(9,CharacterAttributes.NewLevelOf(82,belief[82]));
    }
    [Fact] public void AcceptedPartialPlanPreservesEveryMissingLogicalValueAndOnlyRequestedDelta()
    {
        var before=Fresh().Where(r=>r.Slot!=CharacterAttributes.SlotOf(78)).ToArray();
        Assert.True(AttributeAllocation.TryPlan(before,Raises(81),PlayerProgression.ExperienceForLevel(18),out var after));
        Assert.Equal(24,after[78]);Assert.Equal(9,after[81]);Assert.Equal(11,after[79]);Assert.Equal(5,after[82]);Assert.Equal(8,after[84]);
        Assert.Equal(17,after.Sum(v=>CharacterAttributes.SpentPoints(v.Key,v.Value)));
        //All-five lazy rows reproduce the same logical values in world and margin readers after relog.
        var persisted=CharacterAttributes.All.Select(a=>Row(a,after[a])).ToArray();Assert.Equal(after.OrderBy(v=>v.Key),AttributeAllocation.ReadValues(persisted).OrderBy(v=>v.Key));
        Assert.Equal("024e0000001000510000000100",Convert.ToHexString(CharacterAttributes.BuildSpentPoints(after)).ToLowerInvariant());
        Assert.False(AttributeAllocation.TryPlan(persisted,Raises(84),PlayerProgression.ExperienceForLevel(18),out _));
    }
    [Fact] public void NoRowLegacyMismatchRemainsExplicitAndRefusedWithoutInventedBaseline()
    {
        Assert.Equal(90,CharacterAttributes.All.Sum(a=>CharacterAttributes.SpentPoints(a,CharacterAttributes.LegacyValue(a))));
        Assert.False(AttributeAllocation.TryPlan(Array.Empty<AttributeAllocation.Row>(),Raises(78),PlayerProgression.ExperienceForLevel(50),out _));
        Assert.Equal("03510000000400520000000b00540000000100",Convert.ToHexString(CharacterAttributes.BuildSpentPoints(new())).ToLowerInvariant());
    }
    [Fact] public void ExactTypedSlotsPreventHighSlotWrapAndCodeFamilyAliases()
    {
        Assert.True(AttributeAllocation.Matches(Row(78),78));Assert.True(AttributeAllocation.Matches(Row(78,raw:0x80013801),78));
        var high=Row(78,slot:CharacterAttributes.SlotOf(78)+256);Assert.False(AttributeAllocation.Matches(high,78));
        Assert.False(AttributeAllocation.Matches(Row(78,raw:0x84013800),78));Assert.False(AttributeAllocation.Matches(Row(78,raw:0x180013800L),78));
        Assert.Empty(AttributeAllocation.ReadValues(new[]{high}));
        var rows=Fresh();rows[0]=Row(78,raw:0x84013800);Assert.False(AttributeAllocation.TryPlan(rows,Raises(81),PlayerProgression.ExperienceForLevel(50),out _));
        Assert.False(AttributeAllocation.TryPlan(Fresh().Append(Row(78,identity:99)),Raises(81),PlayerProgression.ExperienceForLevel(50),out _));
        rows=Fresh();rows[0]=Row(78,31);Assert.False(AttributeAllocation.TryPlan(rows,Raises(81),PlayerProgression.ExperienceForLevel(50),out _));
    }
    [Fact] public void LiteralResponseIsOneNormalizedNativeBatchAndPositiveError()
    {
        var result=new AttributeAllocation.Result(true,"",new Dictionary<byte,ushort>{{78,9},{82,6}});
        Assert.Equal("80b60800000000000200004e00000009005200000009",Convert.ToHexString(AttributeAllocation.Reply(result,Raises(78,82))).ToLowerInvariant());
        Assert.Equal("80b608000c0000180000",Convert.ToHexString(AttributeAllocation.Reply(new(false,"refused"),Raises(78))).ToLowerInvariant());
        Assert.True(unchecked((int)AttributeAllocation.FailureStatus)>=1);
    }
    [Fact] public void ProfileHasActual50And49PointsWithoutPrivilegeOverrides()
    {
        var plan=new NativeQa.Modes.AttributeAllocationMode().Plan;Assert.False(plan.DevCommands);Assert.False(plan.GmCharacter110);Assert.False(plan.FixedCallbackRolls);
        Assert.Contains("exp="+PlayerProgression.ExperienceForLevel(50),string.Join(" ",plan.AfterHelpers));
        Assert.Equal(5,CharacterAttributes.All.Length);Assert.Equal(40,CharacterAttributes.All.Sum(a=>CharacterAttributes.BaseValue(a)));
    }
    private static WorldClient Client()
    {
        var c=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));c.playerData=new ClientData();c.playerData.setCharID(110);c.playerData.setOnWorld(false);c.messageQueue=new MessageQueue();c.Alive=true;
        typeof(WorldClient).GetField("lastUsedTime",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(c,TimeUtils.getUnixTimeUint32());
        typeof(WorldClient).GetField("lastFlushTick",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(c,long.MaxValue);
        typeof(WorldClient).GetField("<TransportFence>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(c,new WorldSendFence());return c;
    }
    [Fact] public void PublicationDoesNotRequireAvatarAndCallbackIsOutsideFenceAfterPackets()
    {
        var c=Client();var d=c.playerData;long epoch=c.TransportFence.Capture();Assert.True(AttributeAllocation.Current(c,d,0,d.worldGeneration,0,epoch,110));
        bool called=false;AttributeAllocation.Finish(c,epoch,d.worldGeneration,()=>true,new(true,"",new Dictionary<byte,ushort>{{78,9}}),Raises(78),()=>
        {
            var queued=(SequencedMessage)Assert.Single(c.messageQueue.RPCMessagesQueue)!;
            Assert.Equal(new byte[]{0x80,0xb6},queued.content.Skip(1).Take(2));
            var other=Task.Run(()=>c.TransportFence.TryRun(epoch,()=>{}));Assert.True(other.Wait(TimeSpan.FromSeconds(2)));Assert.True(other.Result);called=true;
        },()=>
        {
            var other=Task.Run(()=>c.TransportFence.TryRun(epoch,()=>{}));Assert.True(other.Wait(TimeSpan.FromSeconds(2)));Assert.True(other.Result);
        });Assert.True(called);
        called=false;AttributeAllocation.Finish(c,epoch,d.worldGeneration,()=>true,new(false,"refused"),Raises(78),()=>called=true,()=>{});Assert.False(called);
        c.TransportFence.PublishIncoming(true,()=>{});AttributeAllocation.Finish(c,epoch,d.worldGeneration,()=>AttributeAllocation.Current(c,d,0,d.worldGeneration,0,epoch,110),new(true,"",new Dictionary<byte,ushort>{{78,9}}),Raises(78),()=>called=true,()=>{});Assert.False(called);
    }
    [Fact] public void ModalBuilderIsPureAndRefusalFlushRunsOutsideFence()
    {
        const string header="00000000000000000024000000000000000000000000000000000000000000000000";
        Assert.Equal("2e17"+header+"03006e6f00",Convert.ToHexString(ServerPackets.BuildSystemChatMessage("no","MODAL")).ToLowerInvariant());
        var c=Client();long epoch=c.TransportFence.Capture();bool flushed=false,callback=false;
        AttributeAllocation.Finish(c,epoch,c.playerData.worldGeneration,()=>true,new(false,"no"),Raises(78),()=>callback=true,()=>
        {
            Assert.Equal(2,c.messageQueue.RPCMessagesQueue.Count);
            var other=Task.Run(()=>c.TransportFence.TryRun(epoch,()=>{}));Assert.True(other.Wait(TimeSpan.FromSeconds(2)));Assert.True(other.Result);flushed=true;
        });Assert.True(flushed);Assert.False(callback);
        Assert.Null(new HeadlessClient.CharacterSeed("legacy").AttributeValues);
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)]
    public void PublicationRejectsChangedCharacterWorldLifeEpochOrGeneration(int change)
    {
        var c=Client();var d=c.playerData;int world=d.worldGeneration;long epoch=c.TransportFence.Capture();
        Assert.True(AttributeAllocation.Current(c,d,0,world,0,epoch,110));
        if(change==0)d.setCharID(111);if(change==1)d.worldGeneration++;if(change==2)d.lifeGeneration++;if(change==3)c.TransportFence.PublishIncoming(true,()=>{});if(change==4)d.attributeGeneration++;
        Assert.False(AttributeAllocation.Current(c,d,0,world,0,epoch,110));
    }
    [Fact] public void MalformedRequestsCannotQueueTutorialAttributeGoals()
    {
        var previous=Store.currentClient;try
        {
            var c=Client();Store.currentClient=c;c.playerData.tutorial=(TutorialMission)Activator.CreateInstance(typeof(TutorialMission),BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{c},null)!;
            byte[] invalid={4,0,1,0};new RpcManager().HandleRpc(0xb5,ref invalid);
            var held=(HashSet<long>)typeof(TutorialMission).GetField("held",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(c.playerData.tutorial)!;Assert.Empty(held);
        }finally{Store.currentClient=previous;}
    }
}
