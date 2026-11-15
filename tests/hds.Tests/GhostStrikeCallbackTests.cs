using hds;
using System.Runtime.CompilerServices;
namespace hds.Tests;
[Collection("DelayedActions")]
public class GhostStrikeCallbackTests
{
    private static WorldClient Client()
    {
        var c=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        typeof(WorldClient).GetProperty("IsBot")!.SetValue(c,true);
        c.playerData=new ClientData();c.playerData.isBot=true;c.playerData.setOnWorld(true);c.playerData.setDistrictId(1);
        c.playerInstance=new PlayerCharacter();c.messageQueue=new MessageQueue();
        c.playerInstance.Level.setValue((byte)50);c.playerInstance.Health.setValue((ushort)500);c.playerInstance.MaxHealth.setValue((ushort)500);
        c.playerInstance.InnerStrengthMax.setValue((ushort)500);c.playerInstance.InnerStrengthAvailable.setValue((ushort)500);c.Alive=true;
        typeof(WorldClient).GetField("<TransportFence>k__BackingField",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.SetValue(c,new WorldSendFence());
        return c;
    }
    private static GhostStrikeCallbacks.Ticket Ticket(WorldClient c,WorldClient target,ushort hidden=1116)
    {
        var expiry=Stealth.Begin(c,Stealth.Defs[hidden],50,durationOverride:20).Expires;
        var t=new GhostStrikeCallbacks.Ticket{Source=c,SourceCurrent=GhostStrikeCallbacks.Lifetime(c),
            Target=target,TargetCurrent=GhostStrikeCallbacks.Lifetime(target),View=8,Level=50,Expires=expiry,
            InitialAction=StealthShieldAbilities.ActionVersion(c),InitialStealthVersion=Stealth.Version(c)};
        Stealth.BreakForAction(c,"used ability365");GhostStrikeCallbacks.Arm(t);
        var cast=AbilityCastInterrupts.Begin(c,365,1.5f);GhostStrikeCallbacks.Bind(t,cast);
        Assert.True(AbilityCastInterrupts.Complete(cast,()=>{}));return t;
    }
    [Theory][InlineData(8,true)][InlineData(9,false)]
    public void AcceptedCallbackDrawsOnceAndRestoresNormalGrantOnlyUntilOriginalExpiry(int roll,bool restores)
    {
        var c=Client();var target=Client();var t=Ticket(c,target);int draws=0;
        Assert.Null(Stealth.Current(c));Assert.True(GhostStrikeCallbacks.BeforeHit(t,c,target,8));
        Assert.Equal(restores,GhostStrikeCallbacks.Accepted(t,()=>{draws++;return roll;}));
        Assert.False(GhostStrikeCallbacks.Accepted(t,()=>{draws++;return roll;}));Assert.Equal(1,draws);
        if(restores){var state=Stealth.Current(c);Assert.Equal((ushort)1116,state.Def.Ability);Assert.Equal(t.Expires,state.Expires);Assert.InRange(state.Seconds,19,20);Assert.True(Stealth.IsHidden(c));}
        else Assert.Null(Stealth.Current(c));
        Stealth.Cleared(c);
    }
    [Theory][InlineData("action")][InlineData("clear")][InlineData("death")][InlineData("world")][InlineData("owner")][InlineData("expire")][InlineData("newstealth")][InlineData("transport")][InlineData("district")]
    public void InvalidSourceNeverDrawsOrRestores(string change)
    {
        var c=Client();var t=Ticket(c,Client());
        switch(change){case "action":Stealth.BreakForAction(c,"other action");break;case "clear":Stealth.Cleared(c);break;
            case "death":c.playerData.isDead=true;break;case "world":c.playerData.worldGeneration++;break;
            case "owner":c.playerData=Client().playerData;break;case "expire":t.Expires=0;break;
            case "newstealth":Stealth.Begin(c,Stealth.Defs[1111],50);break;
            case "transport":c.TransportFence.PublishIncoming(true,()=>{});break;case "district":c.playerData.setDistrictId(2);break;}
        int draws=0;Assert.False(GhostStrikeCallbacks.Accepted(t,()=>{draws++;return 8;}));Assert.Equal(0,draws);Stealth.Cleared(c);
    }
    [Fact]public void TargetMustBeCurrentBeforeHitButDeathByAcceptedHitDoesNotCancelSourceCallback()
    {
        var c=Client();var target=Client();var t=Ticket(c,target);
        Assert.False(GhostStrikeCallbacks.BeforeHit(t,c,Client(),8));Assert.False(GhostStrikeCallbacks.BeforeHit(t,c,target,9));
        target.playerData.isDead=true;Assert.False(GhostStrikeCallbacks.BeforeHit(t,c,target,8));target.playerData.isDead=false;
        Assert.True(GhostStrikeCallbacks.BeforeHit(t,c,target,8));target.playerData.isDead=true;
        Assert.True(GhostStrikeCallbacks.Accepted(t,()=>8));Stealth.Cleared(c);
    }
    [Fact]public void StrictCallbackDrawOverrideCannotAffectOrdinaryActorsOrOtherDatabases()
    {
        Assert.Equal(8,GhostStrikeCallbacks.QaDraw("1","mxotest_run_abc","1",110,"8"));
        Assert.Equal(9,GhostStrikeCallbacks.QaDraw("1","mxotest_run_abc","1",110,"9"));
        Assert.Null(GhostStrikeCallbacks.QaDraw("true","mxotest_run_abc","1",110,"8"));
        Assert.Null(GhostStrikeCallbacks.QaDraw("1","mxo","1",110,"8"));Assert.Null(GhostStrikeCallbacks.QaDraw("1","mxotest_run_abc","0",110,"8"));
        Assert.Null(GhostStrikeCallbacks.QaDraw("1","mxotest_run_abc","1",111,"8"));Assert.Null(GhostStrikeCallbacks.QaDraw("1","mxotest_run_abc","1",110,"1"));
    }
    [Fact]public void SupportedImpartedStateRestoresNormal1116WithoutExtendingItsOriginalExpiry()
    {
        var c=Client();var t=Ticket(c,Client(),629);Assert.True(GhostStrikeCallbacks.Accepted(t,()=>8));
        Assert.Equal((ushort)1116,Stealth.Current(c).Def.Ability);Assert.Equal(t.Expires,Stealth.Current(c).Expires);Stealth.Cleared(c);
    }
    [Fact]public void UncompletedOrReplacedNormalCastCannotInvokeTheCallback()
    {
        var c=Client();var t=Ticket(c,Client());int draws=0;
        var first=AbilityCastInterrupts.Begin(c,365,1.5f);GhostStrikeCallbacks.Bind(t,first);
        Assert.False(GhostStrikeCallbacks.Accepted(t,()=>{draws++;return 8;}));
        AbilityCastInterrupts.Begin(c,365,1.5f);
        Assert.False(AbilityCastInterrupts.Complete(first,()=>GhostStrikeCallbacks.Accepted(t,()=>{draws++;return 8;})));
        Assert.Equal(0,draws);Assert.Null(Stealth.Current(c));
    }
    [Fact]public void DiscoveredModeHasOnlyNormallyLoadableAbilitiesAndCallbackScopedControls()
    {
        var mode=NativeQa.NativeQaModes.Find("ghost-strike-callback");
        Assert.Equal(HeadlessClient.GhostStrikeFixture.Abilities,mode.Plan.Abilities);
        Assert.DoesNotContain((ushort)366,mode.Plan.Abilities);Assert.DoesNotContain((ushort)629,mode.Plan.Abilities);
        Assert.False(mode.Plan.FixedCallbackRolls);Assert.False(mode.Plan.GmCharacter110);
        Assert.Equal("1",NativeQa.NativeQaFixturePlan.EnvironmentFor(mode.Name)["MXO_GHOST_STRIKE_QA"]);
        Assert.Equal("",NativeQa.NativeQaFixturePlan.EnvironmentFor("")["MXO_GHOST_STRIKE_QA"]);
    }
    [Fact]public void ArmingRequiresExactlyItsOwnBreakAndTheCapturedStealthGeneration()
    {
        var c=Client();var t=Ticket(c,Client());int draws=0;
        t.InitialAction--;GhostStrikeCallbacks.Arm(t);Assert.False(t.Armed);
        Assert.False(GhostStrikeCallbacks.Accepted(t,()=>{draws++;return 8;}));Assert.Equal(0,draws);
        t.InitialAction++;t.InitialStealthVersion--;GhostStrikeCallbacks.Arm(t);Assert.False(t.Armed);
    }
    [Fact]public void StaleTargetTransportDistrictHealthAndViewSpawnCannotQualifyBeforeTheHit()
    {
        foreach(string change in new[]{"transport","district","health","spawn"})
        {
            var c=Client();var target=Client();var t=Ticket(c,target);t.Spawn=3;
            Assert.True(GhostStrikeCallbacks.BeforeHit(t,c,target,8,3));uint spawn=3;
            switch(change){case "transport":target.TransportFence.PublishIncoming(true,()=>{});break;
                case "district":target.playerData.setDistrictId(2);break;case "health":target.playerInstance.Health.setValue((ushort)0);break;case "spawn":spawn=4;break;}
            Assert.False(GhostStrikeCallbacks.BeforeHit(t,c,target,8,spawn));Stealth.Cleared(c);
        }
    }
    [Fact]public void CancelledCurrentCastNeverCompletesOrDrawsTheSourceCallback()
    {
        var c=Client();var t=Ticket(c,Client());int draws=0;
        c.playerData.castEndsAt=TimeUtils.GetSimTimeSeconds()+1.5;
        var cast=AbilityCastInterrupts.Begin(c,365,1.5f);GhostStrikeCallbacks.Bind(t,cast);
        c.playerData.castEndsAt=-1; // actual cancellation deadline used by the ordinary interrupt path
        Assert.False(AbilityCastInterrupts.Complete(cast,()=>GhostStrikeCallbacks.Accepted(t,()=>{draws++;return 8;})));
        Assert.False(GhostStrikeCallbacks.Accepted(t,()=>{draws++;return 8;}));Assert.Equal(0,draws);Assert.Null(Stealth.Current(c));
    }
}
