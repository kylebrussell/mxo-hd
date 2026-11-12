using System.Runtime.CompilerServices;
using hds;
using hds.shared;
namespace hds.Tests;
[Collection("DelayedActions")]
public class StealthShieldTests
{
    private static WorldClient Client()
    {
        var c=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        typeof(WorldClient).GetProperty("IsBot")!.SetValue(c,true);
        c.playerData=new ClientData();c.playerData.isBot=true;c.playerData.setOnWorld(true);c.playerData.setDistrictId(1);
        c.playerInstance=new PlayerCharacter();c.messageQueue=new MessageQueue();
        c.playerInstance.Level.setValue((byte)50);c.playerInstance.Health.setValue((ushort)500);c.playerInstance.MaxHealth.setValue((ushort)500);
        c.playerInstance.InnerStrengthMax.setValue((ushort)500);c.playerInstance.InnerStrengthAvailable.setValue((ushort)500);
        typeof(WorldClient).GetField("alive",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.SetValue(c,true);
        return c;
    }
    private sealed class Subject:TimedEffects.Subject
    {
        internal override WorldClient Context=>null!;
        internal override void Applied(TempMods.Effect e,List<TimedEffects.Active>a,TimedEffects.Active i,List<TimedEffects.Active>r){}
        internal override bool Ticked(TimedEffects.Active m,int damage)=>true;
        internal override void Ended(List<TimedEffects.Active>ended){}
    }
    private static TempMods.Effect Marker(float seconds=8)=>new(){Ability=1125,Mods=new[]{new TempMods.Mod{Kind=TempMods.KindStat,
        Proc=TempMods.Proc.Terminal,Target=710,Seconds=seconds}}};
    [Fact]
    public void NativeBuiltinThrowHasAnActionByteAndReceiverLocalTargetWithoutNamed25Prefix()
    {
        Assert.Equal(Convert.FromHexString("0200012007010C40560422004400"),StealthShieldAbilities.BuildThrow(true,7,1110,0x00440022));
        Assert.Equal(Convert.FromHexString("012007010C4056049900AA00"),StealthShieldAbilities.BuildThrow(false,7,1110,0x00aa0099));
    }
    [Fact]
    public void GOBDistinguishesTargetSuppressionFromTheCastersOwnRestealthMarker()
    {
        var e=AbilityCatalog.Find(1125)!;var m=Assert.Single(e.Modifiers);
        Assert.Equal(3,e.Number("Ability/CastValidTarget"));Assert.Equal(0,e.Number("Ability/CastDirectObjects"));
        Assert.Equal(710,m.Target);Assert.Equal(8,m.Number("Duration"));Assert.Equal("DisableEvasion",m.Text("StatModInitFunc"));
        Assert.Equal("EnableEvasion",m.Text("StatModTermFunc"));Assert.Equal(1.5,StealthShieldAbilities.RestealthSeconds);
        Assert.DoesNotContain(AbilityCatalog.All.Values,e=>e.Modifiers.Any(m=>m.Number("TargetAbilityGrant")==2148635648));
        var dog=Assert.Single(AbilityCatalog.Find(1110)!.Modifiers);Assert.Equal("NeutralDOT",dog.Text("StatModTermFunc"));
        Assert.Equal(.5,dog.Number("Duration"));Assert.Equal(10,dog.Number("MinValue"));Assert.False(dog.Has("UpdateFuncInterval"));
        Assert.Equal(0,SneakAttacks.Bonus(1110,true));
    }
    [Fact]
    public void NaturalExpiryFiresOnceButRemovalReplacementAndDeathClearDoNotFireTerminalCallbacks()
    {
        var s=new Subject();int calls=0;double now=TimeUtils.GetSimTimeSeconds()+100;
        void Apply()=>TimedEffects.Apply(s,Marker(.5f),now,configure:m=>m.OnExpired=(_,_)=>calls++);
        Apply();TimedEffects.RunDue(now+.49);Assert.Equal(0,calls);TimedEffects.RunDue(now+.51);Assert.Equal(1,calls);
        TimedEffects.RunDue(now+2);Assert.Equal(1,calls);
        Apply();TimedEffects.RemoveParent(s,1125);TimedEffects.RunDue(now+2);Assert.Equal(1,calls);
        Apply();Apply();TimedEffects.RunDue(now+2);Assert.Equal(2,calls); // only the replacement expires
        Apply();TimedEffects.Clear(s);TimedEffects.RunDue(now+2);Assert.Equal(2,calls);
    }
    [Fact]
    public void Caster681AndTarget710MarkersCoexistAndRefreshOnlyTheirOwnTuple()
    {
        var holder=new TimedEffects.Holder();
        var target=new TimedEffects.Active{Target=710,Seconds=8};var self=new TimedEffects.Active{Target=681,Seconds=1.5f};
        holder.Apply(1125,new[]{target},100,true);Assert.Empty(holder.Apply(1125,new[]{self},101,true));
        Assert.Equal(2,holder.Snapshot().Count);
        Assert.Same(self,Assert.Single(holder.Apply(1125,new[]{new TimedEffects.Active{Target=681,Seconds=1.5f}},102,true)));
        Assert.Contains(target,holder.Snapshot());Assert.Equal(108,target.Expires);
    }
    [Fact]
    public void SuppressionKeepsTheParentRecordIdsGenerationAndToggleWhileOnlyTheChildStateIsOff()
    {
        var c=Client();Assert.True(AbilityBuffs.Apply(c,217));var parent=c.playerData.activeBuffs[217];
        Assert.True(EvasionShield.HasState(c));Assert.Equal((byte)255,c.playerInstance.EvadeShieldHealth.getValue()[0]);
        double now=TimeUtils.GetSimTimeSeconds()+100;TimedEffects.Apply(TimedEffects.ForPlayer(c),Marker(),now);
        Assert.True(AbilityBuffs.IsToggledOn(c,217));Assert.False(EvasionShield.HasState(c));
        Assert.Same(parent,c.playerData.activeBuffs[217]);Assert.Equal((byte)0,c.playerInstance.EvadeShieldHealth.getValue()[0]);
        TimedEffects.RunDue(now+8.1);Assert.True(EvasionShield.HasState(c));Assert.Same(parent,c.playerData.activeBuffs[217]);
        Assert.Equal((byte)255,c.playerInstance.EvadeShieldHealth.getValue()[0]);AbilityBuffs.Remove(c,217);
    }
    [Fact]
    public void EndCannotRecreateAParentThatWasTurnedOffUnloadedOrLostItsWorld()
    {
        foreach(int reason in new[]{0,1,2,3})
        {
            var c=Client();AbilityPassives.SetLoaded(c,new[]{((ushort)217,(ushort)50)});AbilityBuffs.Apply(c,217);
            double now=TimeUtils.GetSimTimeSeconds()+100;TimedEffects.Apply(TimedEffects.ForPlayer(c),Marker(),now);
            if(reason==0)AbilityBuffs.Remove(c,217);
            if(reason==1)AbilityPassives.SetLoaded(c,Array.Empty<(ushort,ushort)>());
            if(reason==2)c.playerData.worldGeneration++;
            if(reason==3){c.playerData.lifeGeneration++;c.playerData.isDead=true;}
            TimedEffects.RunDue(now+8.1);Assert.False(EvasionShield.HasState(c));
            if(reason<2)Assert.False(AbilityBuffs.IsToggledOn(c,217));AbilityBuffs.Remove(c,217);
        }
    }
    [Fact]
    public void RestoreGuardsRejectJumpStanceNewActionsNewStealthAndNewLifeOrWorld()
    {
        var c=Client();Stealth.Begin(c,Stealth.Defs[1116],10);
        var snapshot=StealthShieldAbilities.Capture(c);Stealth.BreakForAction(c,"test throw");StealthShieldAbilities.Arm(c,snapshot);
        Assert.True(StealthShieldAbilities.CanRestore(c,snapshot));
        c.playerData.stance=8;Assert.False(StealthShieldAbilities.CanRestore(c,snapshot));c.playerData.stance=0;
        c.playerData.worldGeneration++;Assert.False(StealthShieldAbilities.CanRestore(c,snapshot));c.playerData.worldGeneration--;
        c.playerData.lifeGeneration++;Assert.False(StealthShieldAbilities.CanRestore(c,snapshot));c.playerData.lifeGeneration--;
        StealthShieldAbilities.ActionTaken(c);Assert.False(StealthShieldAbilities.CanRestore(c,snapshot));
        StealthShieldAbilities.Arm(c,snapshot);Stealth.Begin(c,Stealth.Defs[1111],5);Assert.False(StealthShieldAbilities.CanRestore(c,snapshot));
        Stealth.Cleared(c);
    }
    [Fact]
    public void RestoredInvisibilityUsesItsOverrideForPacketsAndExpiryWithoutLosingBlurBits()
    {
        var c=Client();Assert.True(AbilityBuffs.Apply(c,49));
        var state=Stealth.Begin(c,Stealth.Defs[1116],50,durationOverride:7.5f)!;
        Assert.Equal(7.5f,state.Seconds);Assert.InRange(state.Expires-state.Started,7.49,7.51);
        Assert.Equal((ushort)0x44,BitConverter.ToUInt16(c.playerInstance.DissemblingType.getValue()));
        Stealth.Cleared(c);Assert.Equal((ushort)0x40,BitConverter.ToUInt16(c.playerInstance.DissemblingType.getValue()));
    }
}
