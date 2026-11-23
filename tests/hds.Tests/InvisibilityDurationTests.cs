using System.Runtime.CompilerServices;
using hds;
using hds.shared;
using HeadlessClient;
namespace hds.Tests;
[Collection("DelayedActions")]
public class InvisibilityDurationTests
{
    private static WorldClient Client()
    {
        var c=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));typeof(WorldClient).GetProperty("IsBot")!.SetValue(c,true);
        c.playerData=new ClientData{isBot=true};c.playerData.setOnWorld(true);c.playerData.setDistrictId(1);c.playerInstance=new PlayerCharacter();c.messageQueue=new MessageQueue();
        c.playerInstance.Level.setValue((byte)50);c.playerInstance.Health.setValue((ushort)500);c.playerInstance.MaxHealth.setValue((ushort)500);
        c.playerInstance.InnerStrengthMax.setValue((ushort)500);c.playerInstance.InnerStrengthAvailable.setValue((ushort)500);
        typeof(WorldClient).GetField("alive",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.SetValue(c,true);return c;
    }
    [Fact] public void OriginalDurationReferenceIs644And175MovementBonusIsDistinct()
    {
        Assert.Equal(644,Stealth.InvisDurationAbility);Assert.Equal("InvisDurationAbility",AbilityCatalog.Find(644).Name);
        Assert.Equal("InvisMovementSpeedBonusAbility",AbilityCatalog.Find(1051).Name);
        var passive=Assert.Single(AbilityCatalog.Find(175).Modifiers,m=>m.Target!=0);Assert.Equal(1051,passive.Target);Assert.Equal(.5,passive.Number("ValueScale"));
        var duration=Assert.Single(AbilityCatalog.Find(1117).Modifiers,m=>m.Target==644);Assert.Equal(2148143104,duration.Number("TargetAttribute"));Assert.Equal(3,duration.Number("MinValue"));Assert.Equal(3,duration.Number("MaxValue"));
        Assert.True(AbilityBuffs.TryGet(1117,out var buff));Assert.Contains(buff.Modifiers,m=>m.Stat==644&&m.Value==3);
    }
    [Fact] public void NormalProfileQualifiesAndGrantsBothActionsWithoutArchivingHiddenChildren()
    {
        var ids=InvisibilityDurationFixture.Abilities;Assert.DoesNotContain((ushort)1116,ids);Assert.DoesNotContain((ushort)1117,ids);
        var rows=ids.Select((id,i)=>new AbilityMemory.Row((uint)i,(ushort)i,0x80000000u|((uint)id<<10),50,false)).ToArray();
        var requests=rows.Select(r=>new AbilityProgression.LoadoutEntry{Slot=r.Slot,Ability=r.Ability,Level=50}).ToArray();
        var plan=AbilityMemory.Plan(rows,requests,false,PlayerProgression.ExperienceForLevel(50),50);Assert.Null(plan.Refusal);Assert.Equal(rows.Length,plan.Accepted.Length);Assert.Equal(46,plan.Used);Assert.Equal(100,plan.Maximum);
        var membership=AbilityMemory.Membership(plan.Loaded);Assert.Contains((ushort)1116,membership);Assert.Contains((ushort)1117,membership);
        Assert.Contains((ushort)1117,AbilityProgression.GrantsOf(290));
    }
    [Fact] public void ActualLoaded175DoesNotExtendDurationButActivated1117AddsThreeOnce()
    {
        var c=Client();AbilityPassives.SetLoaded(c,InvisibilityDurationFixture.Abilities.Select(id=>(id,(ushort)50)));
        Assert.Equal(25,AbilityStats.Player(c,1051));Assert.Equal(0,AbilityStats.Player(c,644));
        Assert.Equal(130,Stealth.Begin(c,Stealth.Defs[1116],50).Seconds);Stealth.Cleared(c);
        Assert.True(AbilityBuffs.Apply(c,1117));Assert.Equal(3,AbilityStats.Player(c,644));
        var state=Stealth.Begin(c,Stealth.Defs[1116],50);Assert.Equal(133,state.Seconds);Assert.Equal(133,state.Expires-state.Started,4);
        var expires=state.Expires;Assert.True(AbilityBuffs.Apply(c,1117));Assert.Equal(3,AbilityStats.Player(c,644));Assert.Same(state,Stealth.Current(c));Assert.Equal(expires,state.Expires);
        Assert.True(AbilityBuffs.Remove(c,1117));Assert.Equal(0,AbilityStats.Player(c,644));Assert.Equal(expires,state.Expires);
        DelayedActions.RunDue(expires-1);Assert.Same(state,Stealth.Current(c));DelayedActions.RunDue(expires+1);Assert.Null(Stealth.Current(c));
    }
    [Fact]
    public void ShadowUpgradeUsesTheDirectInvisibleMovementChannelAndRestoresItWithoutChangingLifetime()
    {
        var actor = Client();
        AbilityPassives.SetLoaded(actor, InvisibilityDurationFixture.Abilities.Select(id => (id, (ushort)50)));
        Assert.Equal(25, AbilityStats.Player(actor, 1051));
        Assert.NotNull(Stealth.Begin(actor, Stealth.Defs[1116], 50));
        Assert.Equal(0f, Scale());
        Stealth.Cleared(actor);

        Assert.True(AbilityBuffs.Apply(actor, 1117));
        Assert.Equal(0f, Scale());
        Assert.NotNull(Stealth.Begin(actor, Stealth.Defs[1111], 50));
        Assert.Equal(0f, Scale());
        Stealth.Cleared(actor);

        var belt = Stealth.Begin(actor, Stealth.Defs[1116], 50);
        Assert.Equal(133, belt.Seconds);
        Assert.Equal(.10f, Scale());
        Assert.True(AbilityBuffs.Apply(actor, 12));
        Assert.Equal(.22f, Scale(), 5);
        Assert.True(AbilityBuffs.Remove(actor, 1117));
        Assert.Equal(.12f, Scale());
        Assert.Same(belt, Stealth.Current(actor));
        Assert.Equal(133, belt.Expires - belt.Started, 5);
        Assert.True(AbilityBuffs.Remove(actor, 12));
        Assert.Equal(0f, Scale());
        Stealth.Cleared(actor);

        float Scale() => BitConverter.ToSingle(actor.playerInstance.MovementScale.getValue());
    }

    [Fact]
    public void ShadowUpgradeFeedsTheOrdinaryWithdrawalAndTypedThrowInputs()
    {
        var actor = Client();
        AbilityPassives.SetLoaded(actor, NativeQa.Modes.ShadowUpgradeConsumersMode.Abilities.Select(id => (id, (ushort)50)));
        CombatStyles.Select(actor, CombatStyles.SelfDefense);
        var sweep = new AbilityItem();
        sweep.setAbilityID(134);
        Assert.Equal(65, AbilityStats.Player(actor, 1005));
        Assert.Equal(25, AbilityStats.Player(actor, 1045));
        Assert.Equal(81, WithdrawalInput.Attack(actor, null, ordinary: true));
        CombatStyles.Select(actor, CombatStyles.SpyStyle);
        Assert.Equal(66, AbilityStats.SpecialAttack(actor, sweep));

        Assert.True(AbilityBuffs.Apply(actor, 1117));
        Assert.Equal(71, AbilityStats.SpecialAttack(actor, sweep));
        CombatStyles.Select(actor, CombatStyles.SelfDefense);
        Assert.Equal(71, AbilityStats.Player(actor, 1005));
        Assert.Equal(88, WithdrawalInput.Attack(actor, null, ordinary: true));

        Assert.True(AbilityBuffs.Remove(actor, 1117));
        Assert.Equal(65, AbilityStats.Player(actor, 1005));
        Assert.Equal(25, AbilityStats.Player(actor, 1045));
        Assert.Equal(81, WithdrawalInput.Attack(actor, null, ordinary: true));
        CombatStyles.Select(actor, CombatStyles.SpyStyle);
        Assert.Equal(66, AbilityStats.SpecialAttack(actor, sweep));
    }

    [Fact]
    public void ImpartedInvisibilityRequiresItsLearnedActionAndDoesNotReplaceBelt()
    {
        var c = Client();
        Assert.NotNull(Stealth.Refusal(c, 629, 0, out uint reason));
        Assert.Equal(AbilityRefusals.NotLoaded, reason);
        AbilityPassives.SetLoaded(c, new[] { ((ushort)629, (ushort)50) });

        var belt = Stealth.Begin(c, Stealth.Defs[1116], 50);
        Assert.NotNull(Stealth.Refusal(c, 629, 0, out reason));
        Assert.Equal(AbilityRefusals.NoClientMessage, reason);
        Stealth.Land(c, 629, 0);
        Assert.Same(belt, Stealth.Current(c));
        Stealth.Cleared(c);

        Stealth.Land(c, 629, 0);
        var imparted = Stealth.Current(c);
        Assert.Equal(120, imparted.Seconds);
        Assert.Empty(imparted.Mods);
        Assert.Equal(Stealth.Invisible, Stealth.BitsOf(c));
        c.playerData.stance = AnimationPackets.StanceSitting;
        DelayedActions.RunDue(imparted.Expires + 1);
        Assert.Null(Stealth.Current(c));
        Assert.Equal(AnimationPackets.StanceStanding, c.playerData.stance);
    }

    [Fact] public void ExistingInvisibilityDoesNotExtendWhenDurationBuffArrivesLater()
    {
        var c=Client();var state=Stealth.Begin(c,Stealth.Defs[1116],50);double expires=state.Expires;
        Assert.True(AbilityBuffs.Apply(c,1117));Assert.Equal(130,state.Seconds);Assert.Equal(expires,state.Expires);Stealth.Cleared(c);
    }
    [Fact] public void ImpartedAndExplicitRemainingTimeNeverReceiveNormalDurationBonus()
    {
        var c=Client();Assert.True(AbilityBuffs.Apply(c,1117));Assert.Equal(3,AbilityStats.Player(c,644));
        Assert.Equal(120,Stealth.Begin(c,Stealth.Defs[629],50).Seconds);
        var state=Stealth.Begin(c,Stealth.Defs[1116],50,durationOverride:7.5f);Assert.Equal(7.5f,state.Seconds);Assert.Equal(7.5,state.Expires-state.Started,4);Stealth.Cleared(c);
    }
    [Fact] public void OldExpiryCannotRemoveANewerStateAfterNormalClear()
    {
        var c=Client();Assert.True(AbilityBuffs.Apply(c,1117));var old=Stealth.Begin(c,Stealth.Defs[1116],50);Stealth.Cleared(c);
        var next=Stealth.Begin(c,Stealth.Defs[1116],50,durationOverride:200);DelayedActions.RunDue(old.Expires+1);Assert.Same(next,Stealth.Current(c));Stealth.Cleared(c);
    }
    [Fact] public void WireReaderRetainsExactRecordIdentityDurationAndOffBit()
    {
        var b=Convert.FromHexString("80bc15002700005c0400007300d4feffff0000000543");var m=InvisibilityDurationFixture.Read(b)!;
        Assert.Equal(1116u,m.Parent);Assert.Equal((ushort)115,m.Target);Assert.Equal(133,m.Seconds);Assert.False(m.Off);Assert.Equal(-300,m.Value);Assert.Equal((ushort)39,m.Record);
        b[17]=1;Assert.True(InvisibilityDurationFixture.Read(b)!.Off);Assert.Null(InvisibilityDurationFixture.Read(b[..21]));
    }
    [Fact] public void RegisteredModeHasNormalProfileAndExplicitUnprovedNativeVisibilityGate()
    {
        var mode=NativeQa.NativeQaModes.Find("invisibility-duration");Assert.IsType<NativeQa.Modes.InvisibilityDurationMode>(mode);
        Assert.Equal(InvisibilityDurationFixture.Abilities,mode.Plan.Abilities);Assert.False(mode.Plan.GmCharacter110);Assert.False(mode.Plan.FixedCallbackRolls);Assert.False(mode.Plan.DevCommands);
        Assert.True(mode.Plan.ExtraQaMetadata["RequiresNativeShadowActionsVisibilityGate"].GetBoolean());Assert.False(mode.Plan.ExtraQaMetadata["NativeActionsVisibilityProven"].GetBoolean());
        Assert.Contains("STOP/HOLD",string.Join(" ",mode.Plan.ReadyMessages));Assert.Contains("Inventory",string.Join(" ",mode.Plan.ReadyMessages));
        Assert.Equal(InvisibilityDurationFixture.Environment,Assert.Single(Scenarios.All,x=>x.Name==mode.Name).Environment);
    }
    [Fact] public void BorrowedBeltCodecUsesActualNineByteMarginRowAndOrdinaryFlagZero()
    {
        Assert.Equal((byte)40,GhostStrikeInvisibilityTool.OwnedSlot(Convert.FromHexString("28CC0100000000000C")));
        Assert.Throws<InvalidDataException>(()=>GhostStrikeInvisibilityTool.OwnedSlot(new byte[10]));
        Assert.Throws<ScenarioFailure>(()=>GhostStrikeInvisibilityTool.OwnedSlot(Convert.FromHexString("28CC0100000000000C28CC0100000000000C")));
        var request=GhostStrikeInvisibilityTool.Request(40);Assert.Equal(43,request.Length);Assert.Equal(1116,BitConverter.ToUInt16(request,2));Assert.Equal(40,request[37]);Assert.Equal(0,request[38]);
    }
    [Fact] public void ReadinessRequiresObservedStartAndLatestMatchingOwnTimerOff()
    {
        byte[] on=Convert.FromHexString("80bd015c04040000f04101"),off=Convert.FromHexString("80bd015c04040000f04100");
        Assert.False(InvisibilityDurationFixture.CooldownReadiness(Array.Empty<byte[]>()).Ready);
        Assert.False(InvisibilityDurationFixture.CooldownReadiness(new[]{off}).Ready);
        Assert.False(InvisibilityDurationFixture.CooldownReadiness(new[]{on}).Ready);
        var ready=InvisibilityDurationFixture.CooldownReadiness(new[]{on,off});Assert.True(ready.Ready);Assert.Equal(30,ready.Latest!.Seconds);Assert.Equal(1,ready.Latest.RpcIndex);
        Assert.False(InvisibilityDurationFixture.CooldownReadiness(new[]{on,off,on}).Ready); // a later start defeats an old OFF
        var other=on.ToArray();other[3]=0x5d;var group=on.ToArray();group[5]=0;
        Assert.True(InvisibilityDurationFixture.CooldownReadiness(new[]{on,off,other,group}).Ready);
        var invalid=on.ToArray();invalid[10]=2;Assert.Throws<InvalidDataException>(()=>InvisibilityDurationFixture.CooldownReadiness(new[]{invalid}));
    }
    [Fact] public void Current1116CooldownIsUseTimeAndItsNaturalEndDoesNotInventATermTimer()
    {
        Assert.True(AbilityBuffs.CooldownStartsOnUse(1116));Assert.True(AbilityCooldowns.TryGet(1116,out var cooldown));Assert.Equal(30,cooldown.Seconds);
        var c=Client();var state=Stealth.Begin(c,Stealth.Defs[1116],50);DelayedActions.RunDue(state.Expires+1);Assert.Null(Stealth.Current(c));
        Assert.False(AbilityCooldowns.IsCoolingDown(c.playerData,1116,TimeUtils.GetSimTimeSeconds()));
    }
}
