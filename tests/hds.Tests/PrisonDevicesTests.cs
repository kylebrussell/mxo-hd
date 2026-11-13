using hds;
using hds.shared;
namespace hds.Tests;
[Collection("DelayedActions")]
public class PrisonDevicesTests
{
    [Theory]
    [InlineData(949,42722,30,1,2,25)]
    [InlineData(957,42723,45,2,3,26)]
    public void ExactStoredDeviceAndScriptRules(int id,int goid,int level,int initial,int increment,int threshold)
    {
        ushort ability=(ushort)id;
        Assert.Equal((uint)goid,PrisonDevices.Item(ability));
        Assert.Equal(level,PrisonDevices.RequiredLevel(ability));
        Assert.True(PrisonDevices.TryGet(ability,out var buff));
        Assert.Equal(30,buff.Seconds);Assert.Equal(0,buff.Family); //958 is a recast timer, not a proved buff-stacking family
        Assert.Equal(initial,Assert.Single(buff.Modifiers).Value);
        Assert.Equal(144,buff.Modifiers[0].Stat);
        Assert.Equal(initial,PrisonDevices.NextValue(ability,initial,0));
        Assert.Equal(initial,PrisonDevices.NextValue(ability,initial,-10));
        Assert.Equal(initial+increment,PrisonDevices.NextValue(ability,initial,1));
        Assert.Equal(threshold,PrisonDevices.NextValue(ability,threshold,1));
        // Script tests the old value; no invented clamp after the increment.
        Assert.Equal(threshold-1+increment,PrisonDevices.NextValue(ability,threshold-1,1));
        Assert.True(AbilityCooldowns.TryGet(ability,out var timer));
        Assert.Equal(360,timer.Seconds);Assert.Equal(958,timer.Group);
    }
    [Fact]
    public void BothProvenFlagsRequireExactEquipmentSlotAndStateIsNotAFlag()
    {
        var body=new byte[41];BitConverter.GetBytes((ushort)949).CopyTo(body,0);body[35]=106;body[36]=1;
        Assert.True(PrisonDevices.Request(body,949));
        body[35]=1;Assert.False(PrisonDevices.Request(body,949));
        body[35]=106;body[36]=0;Assert.True(PrisonDevices.Request(body,949));
        body[34]=8;Assert.True(PrisonDevices.Request(body,949));
        body[34]=0x6b;body[36]=1;Assert.True(PrisonDevices.Request(body,949));
        body[36]=2;Assert.False(PrisonDevices.Request(body,949));
        body[36]=1;Assert.False(PrisonDevices.Request(body,957));
        Assert.False(PrisonDevices.Request(body[..40],949));
        Assert.False(PrisonDevices.TryGet(2,out _));
    }
    [Fact]
    public void ActualNativeToolsRequestMustAdmitOrdinaryFlagZero()
    {
        byte[] body=Convert.FromHexString("b5030000000000000000000000000000000000000000000000000cf51a002096983b016a0000000000");
        Assert.Equal(41,body.Length);Assert.Equal(1,body[34]);Assert.Equal(106,body[35]);Assert.Equal(0,body[36]);
        Assert.True(PrisonDevices.Request(body,949));
    }
    [Theory][InlineData(949)][InlineData(957)]
    public void HeadlessUsesNativeOrdinaryFlagZeroAndIndependentState(int ability)
    {
        var rpc=HeadlessClient.PrisonDeviceWire.Activate((ushort)ability);
        Assert.Equal(43,rpc.Length);Assert.Equal(new byte[]{0x80,0xb9},rpc[..2]);
        Assert.Equal(1,rpc[36]);Assert.Equal(106,rpc[37]);Assert.Equal(0,rpc[38]);
        Assert.True(PrisonDevices.Request(rpc[2..],(ushort)ability));
        Assert.False(PrisonDevices.Request(HeadlessClient.PrisonDeviceWire.Activate((ushort)ability,1)[2..],(ushort)ability));
        Assert.Throws<ArgumentOutOfRangeException>(()=>HeadlessClient.PrisonDeviceWire.Activate(969));
    }
    [Fact]
    public void FixtureGrantsOnlyRealToolsAndNoRandomOverrides()
    {
        var plan=NativeQa.NativeQaModes.Find("prison-devices").Plan;
        Assert.DoesNotContain((ushort)949,plan.Abilities);Assert.DoesNotContain((ushort)957,plan.Abilities);
        Assert.False(plan.GmCharacter110);Assert.False(plan.FixedCallbackRolls);Assert.True(plan.DevCommands);
        Assert.Contains(plan.AfterLoadout,s=>s.Contains("42722")&&s.Contains("42723"));
        var scenario=HeadlessClient.Scenarios.All.Single(s=>s.Name=="prison-devices");
        Assert.DoesNotContain(scenario.Environment!.Keys,k=>k.Contains("ROLL"));
    }
    private static WorldClient Client()
    {
        var c=(WorldClient)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        c.playerData=new ClientData();c.playerInstance=new PlayerCharacter();c.messageQueue=new MessageQueue();
        c.playerInstance.MaxHealth.setValue((ushort)500);c.playerInstance.Health.setValue((ushort)400);
        c.playerInstance.InnerStrengthMax.setValue((ushort)200);c.playerInstance.InnerStrengthAvailable.setValue((ushort)10);
        typeof(WorldClient).GetField("lastFlushTick",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.SetValue(c,long.MaxValue);
        return c;
    }
    private static WorldClient ReadyClient()
    {
        var c=Client();c.Alive=true;c.playerData.setOnWorld(true);c.playerData.setDistrictId(1);
        c.playerInstance.Level.setValue((byte)50);
        typeof(WorldClient).GetField("lastUsedTime",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.SetValue(c,TimeUtils.getUnixTimeUint32());
        typeof(WorldClient).GetField("<TransportFence>k__BackingField",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.SetValue(c,new WorldSendFence());
        AbilityPassives.SetLoaded(c,new[]{((ushort)2,(ushort)50)});return c;
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void LandingRejectsSameGoidReplacementOrStaleLifecycle(int mutation)
    {
        var c=ReadyClient();var original=new PrisonDevices.Owned(10,106,42722,1,50,50);
        var current=mutation==0?new PrisonDevices.Owned(11,106,42722,1,50,50):original;
        long epoch=c.TransportFence.Capture();int world=c.playerData.worldGeneration,life=c.playerData.lifeGeneration;
        c.playerData.castEndsAt=7;
        if(mutation==1)c.playerData.worldGeneration++;
        if(mutation==2)c.playerData.lifeGeneration++;
        if(mutation==3)c.playerData.waitForRPCShutDown=true;
        if(mutation==4){c.TransportFence.PublishIncoming(true,()=>{});long next=c.TransportFence.Capture();c.TransportFence.ApplyReset(next,()=>{});c.TransportFence.Resume(next);}
        int queued=c.messageQueue.RPCMessagesQueue.Count;
        Assert.False(PrisonDevices.CommitLanding(c,949,original,current,world,life,7,epoch));
        Assert.Empty(c.playerData.activeBuffs);Assert.Equal(queued,c.messageQueue.RPCMessagesQueue.Count);
    }
    [Fact]
    public void ActualFencedLandingAcceptsTheSameOwnedRowAndEquipmentNeedsNoAbilityControlBypass()
    {
        var c=ReadyClient();var row=new PrisonDevices.Owned(10,106,42722,1,50,50);c.playerData.castEndsAt=7;
        Assert.True(PrisonDevices.EquipmentReady(c));
        Assert.True(PrisonDevices.CommitLanding(c,949,row,row,c.playerData.worldGeneration,c.playerData.lifeGeneration,7,c.TransportFence.Capture()));
        Assert.True(AbilityBuffs.IsActive(c,949));AbilityBuffs.RemoveAll(c);
        var effects=TimedEffects.ForPlayer(c).Effects;
        effects.Apply(900,new[]{new TimedEffects.Active{Kind=TempMods.KindState,Target=TempMods.PowerlessAbility,Proc=TempMods.Proc.State,Seconds=10}},TimeUtils.GetSimTimeSeconds());
        Assert.False(ControlActions.CanUseAbilities(c));Assert.True(PrisonDevices.EquipmentReady(c));
        effects.RemoveParent(900);
        Assert.False(PrisonDevices.SameItem(row,new PrisonDevices.Owned(10,106,42722,0,50,50)));
        Assert.False(PrisonDevices.Admitted(new PrisonDevices.Owned(10,106,42723,1,50,50),949));
    }
    private static int Inner(WorldClient c)=>NumericalUtils.ByteArrayToUint16(c.playerInstance.InnerStrengthAvailable.getValue(),1);
    [Fact]
    public void DamageMutatesOnlyActiveInstanceAndExpiryRefundsExactlyOnce()
    {
        var c=Client();var other=Client();
        Assert.True(AbilityBuffs.Apply(c,949,c));Assert.True(AbilityBuffs.Apply(other,949,other));
        PrisonDevices.Damage(c,10);PrisonDevices.Damage(c,-10);PrisonDevices.Damage(c,0);
        Assert.Equal(3,c.playerData.activeBuffs[949].Definition.Modifiers[0].Value);
        Assert.Equal(1,other.playerData.activeBuffs[949].Definition.Modifiers[0].Value);
        Assert.Equal(3,AbilityBuffs.Total(c,144));
        int generation=c.playerData.activeBuffs[949].Generation;
        TimedEffects.RunDue(TimeUtils.GetSimTimeSeconds()+31);Assert.Equal(16,Inner(c));
        Assert.Equal(12,Inner(other));
        Assert.False(AbilityBuffs.Remove(c,949,generation:generation));Assert.Equal(16,Inner(c));
        TimedEffects.RunDue(TimeUtils.GetSimTimeSeconds()+31);Assert.Equal(16,Inner(c));
        Assert.Empty(c.playerData.activeBuffs);
    }
    [Fact]
    public void UnequipClearsBothParentsButNotUnrelatedBuffAndOldExpiryCannotRefundNewGeneration()
    {
        var c=Client();Assert.True(AbilityBuffs.Apply(c,949,c));Assert.True(AbilityBuffs.Apply(c,957,c));
        var old=c.playerData.activeBuffs[957];PrisonDevices.Damage(c,1);
        Assert.True(AbilityBuffs.Apply(c,72,c));
        PrisonDevices.Unequipped(c);Assert.Equal(26,Inner(c));
        Assert.False(AbilityBuffs.IsActive(c,949));Assert.False(AbilityBuffs.IsActive(c,957));Assert.True(AbilityBuffs.IsActive(c,72));
        Assert.True(AbilityBuffs.Apply(c,957,c));
        Assert.False(AbilityBuffs.Remove(c,957,generation:old.Generation));Assert.Equal(26,Inner(c));
        PrisonDevices.Unequipped(c);Assert.Equal(30,Inner(c));
        PrisonDevices.Unequipped(c);Assert.Equal(30,Inner(c));
        AbilityBuffs.RemoveAll(c);
        Assert.Equal(100,PrisonDevices.Refund(80));
    }
}
