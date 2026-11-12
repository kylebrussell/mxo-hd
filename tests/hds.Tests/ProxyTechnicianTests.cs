using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
using hds.shared;
using NativeQa;
namespace hds.Tests;

[Collection("DelayedActions")]
public class ProxyTechnicianTests
{
    [Theory]
    [InlineData(1,1)] [InlineData(10,12)] [InlineData(30,38)] [InlineData(50,64)]
    public void SnapshotUsesStoredBinary32CasterLevelAndFiveSecondDuration(int level,int expected)
    {
        var value=ProxyTechnician.Values(level,100);
        Assert.Equal(expected,value.Melee);Assert.Equal(expected,value.Ballistic);Assert.Equal(105,value.Until);
        Assert.Equal(698,AbilityAuras.EffectOf(589));Assert.Equal(5,AbilityAuras.DrainOf(589));Assert.Equal(5,AbilityAuras.IntervalOf(589));
        Assert.True(AbilityAuras.Handles(589));Assert.False(AbilityAuras.Handles(35));
        Assert.Equal(20,AbilityCatalog.Find(698).Number("Ability/RadiusOfEffect"));
    }

    [Theory]
    [InlineData("1","mxotest_proxy_123",110u,true,false,"80",true)]
    [InlineData("true","mxotest_proxy_123",110u,true,false,"80",false)]
    [InlineData("1","reality_hd",110u,true,false,"80",false)]
    [InlineData("1","mxotest_",110u,true,false,"80",false)]
    [InlineData("1","mxotest_bad-name",110u,true,false,"80",false)]
    [InlineData("1","mxotest_proxy",2u,true,false,"80",false)]
    [InlineData("1","mxotest_proxy",0u,true,false,"80",false)]
    [InlineData("1","mxotest_proxy",110u,false,false,"80",false)]
    [InlineData("1","mxotest_proxy",110u,true,true,"80",false)]
    [InlineData("1","mxotest_proxy",110u,true,false,"NaN",false)]
    [InlineData("1","mxotest_proxy",110u,true,false,"Infinity",false)]
    [InlineData("1","mxotest_proxy",110u,true,false,"100",false)]
    [InlineData("1","mxotest_proxy",110u,true,false,"-1",false)]
    public void QaRollCannotEscapeExactFlagDatabaseIdentityLifecycleOrBounds(string dev,string db,uint id,bool world,bool dead,string roll,bool valid) =>
        Assert.Equal(valid,ProxyTechnician.QaAllowed(dev,db,id,world,dead,roll,out _));

    private sealed class Fixture : IDisposable
    {
        private static int nextId=4000000;
        internal readonly WorldClient Owner;
        internal readonly CoderPets.Pet Pet;
        private static Dictionary<uint,CoderPets.Pet> Registry => (Dictionary<uint,CoderPets.Pet>)typeof(CoderPets).GetField("pets",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;
        internal Fixture()
        {
            Owner=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
            Owner.playerData=new ClientData();Owner.playerData.setCharID((uint)Interlocked.Increment(ref nextId));Owner.playerData.setDistrictId(1);Owner.playerData.setOnWorld(true);
            Owner.playerInstance=new PlayerCharacter();Owner.playerInstance.OrganizationID.setValue((byte)1);Owner.playerInstance.Level.setValue((byte)50);Owner.playerInstance.Health.setValue((ushort)500);Owner.playerInstance.MaxHealth.setValue((ushort)500);
            Owner.messageQueue=new MessageQueue();
            typeof(WorldClient).GetField("lastFlushTick",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(Owner,long.MaxValue);
            typeof(WorldClient).GetField("alive",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(Owner,true);
            typeof(WorldClient).GetField("lastUsedTime",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(Owner,TimeUtils.getUnixTimeUint32());
            Owner.playerData.passives.Loaded[589]=50;
            var mob=new Mob();mob.setDistrict(1);mob.setHealthC(1000);mob.setHealthM(1000);mob.setLevel(50);mob.setXPos(0);mob.setYPos(0);mob.setZPos(0);
            Pet=new CoderPets.Pet {Owner=Owner,Mob=mob,Kind=CoderPets.Kind.Proxy,Sheet="Karate"};
            lock(Registry)Registry.Add(Owner.playerData.getCharID(),Pet);
            Assert.True(ProxyTechnician.Begin(Owner));
        }
        public void Dispose() {ProxyTechnician.End(Owner);lock(Registry)Registry.Remove(Owner.playerData.getCharID());}
    }

    [Fact]
    public void ActualPetHitChanceConsumesOnlyTheMatchingProxyChannelAndExpiresTheSnapshot()
    {
        using var f=new Fixture();double now=TimeUtils.GetSimTimeSeconds();
        Assert.Equal(75,CoderPets.HitChance(f.Pet,50,now));
        var value=ProxyTechnician.Project(f.Owner,f.Pet,now);Assert.NotNull(value);
        Assert.Equal(95,CoderPets.HitChance(f.Pet,50,now)); // Existing approximate chance cap, not a recovered retail stat cap.
        value.Ballistic=7; // Distinct test channels expose accidental use of melee tactics by firearms.
        f.Pet.Sheet="Rifle";Assert.Equal(82,CoderPets.HitChance(f.Pet,50,now));
        f.Pet.Kind=CoderPets.Kind.Nuker;Assert.Equal(75,CoderPets.HitChance(f.Pet,50,now));
        f.Pet.Kind=CoderPets.Kind.Proxy;Assert.Equal(75,CoderPets.HitChance(f.Pet,50,now+5));
    }

    [Fact]
    public void RangeLossAndEnemyOwnerDoNotRefreshTheLastIndependentFiveSecondChild()
    {
        using var a=new Fixture();using var b=new Fixture();double now=TimeUtils.GetSimTimeSeconds();
        Assert.NotNull(ProxyTechnician.Project(a.Owner,a.Pet,now));
        b.Owner.playerInstance.OrganizationID.setValue((byte)2);a.Owner.playerData.pvpFlag=true;
        Assert.Equal(PvpRules.Verdict.Allowed,PvpCombat.Check(b.Owner,a.Owner));
        Assert.Null(ProxyTechnician.Project(b.Owner,a.Pet,now+1));
        a.Pet.Mob.setXPos(2000);Assert.True(ProxyTechnician.Eligible(a.Owner,a.Pet));
        a.Pet.Mob.setXPos(2001);Assert.Null(ProxyTechnician.Project(a.Owner,a.Pet,now+1));
        Assert.Equal(64,ProxyTechnician.Accuracy(a.Pet,now+2,false));
        Assert.Equal(0,ProxyTechnician.Accuracy(a.Pet,now+5,false));
    }

    [Fact]
    public void UnloadStopsNewPulsesWhileDeathWorldChangeAndReplacementCannotInheritOldScores()
    {
        using var f=new Fixture();double now=TimeUtils.GetSimTimeSeconds();Assert.NotNull(ProxyTechnician.Project(f.Owner,f.Pet,now));
        f.Owner.playerData.passives.Loaded.Remove(589);Assert.False(ProxyTechnician.Matches(f.Owner));Assert.Null(ProxyTechnician.Project(f.Owner,f.Pet,now+1));
        Assert.Equal(64,ProxyTechnician.Accuracy(f.Pet,now+1,false)); // Child lifetime is independent of parent unload.
        f.Owner.playerData.lifeGeneration++;Assert.Equal(0,ProxyTechnician.Accuracy(f.Pet,now+1,false));
        f.Owner.playerData.passives.Loaded[589]=50;Assert.True(ProxyTechnician.Begin(f.Owner));Assert.NotNull(ProxyTechnician.Project(f.Owner,f.Pet,now+1));
        f.Owner.playerData.setOnWorld(false);f.Owner.playerData.setOnWorld(true);Assert.False(ProxyTechnician.Matches(f.Owner));Assert.Equal(0,ProxyTechnician.Accuracy(f.Pet,now+2,false));
        Assert.True(ProxyTechnician.Begin(f.Owner));Assert.NotNull(ProxyTechnician.Project(f.Owner,f.Pet,now+2));
        f.Owner.playerData.isDead=true;Assert.Equal(0,ProxyTechnician.Accuracy(f.Pet,now+3,false));Assert.False(ProxyTechnician.Matches(f.Owner));
        f.Owner.playerData.isDead=false;f.Pet.Gone=true;Assert.Equal(0,ProxyTechnician.Accuracy(f.Pet,now+3,false));
    }

    [Fact]
    public void FriendlyRecipientReplacementCannotBeClearedByThePreviousSourcesStopOrWorldChange()
    {
        using var sourceA=new Fixture();using var sourceB=new Fixture();using var recipient=new Fixture();double now=TimeUtils.GetSimTimeSeconds();
        var a=ProxyTechnician.Project(sourceA.Owner,recipient.Pet,now);Assert.NotNull(a);
        var b=ProxyTechnician.Project(sourceB.Owner,recipient.Pet,now+1);Assert.NotNull(b);Assert.NotSame(a.SourceToken,b.SourceToken);
        ProxyTechnician.End(sourceA.Owner);sourceA.Owner.playerData.lifeGeneration++;
        Assert.Equal(64,ProxyTechnician.Accuracy(recipient.Pet,now+2,false));
        sourceB.Owner.playerData.setOnWorld(false);sourceB.Owner.playerData.setOnWorld(true);
        Assert.Equal(0,ProxyTechnician.Accuracy(recipient.Pet,now+2,false));
        Assert.Null(ProxyTechnician.Project(sourceB.Owner,recipient.Pet,now+3));
        Assert.True(ProxyTechnician.Begin(sourceB.Owner));Assert.NotNull(ProxyTechnician.Project(sourceB.Owner,recipient.Pet,now+3));
        recipient.Owner.playerData.lifeGeneration++;Assert.Equal(0,ProxyTechnician.Accuracy(recipient.Pet,now+4,false));
    }

    [Fact]
    public void SourcedDrainPaysAtEqualityAndShortfallPreservesTheRemainingResource()
    {
        using var f=new Fixture();
        f.Owner.playerInstance.InnerStrengthAvailable.setValue((ushort)5);
        Assert.True(AbilityAuras.PayDrain(f.Owner,589));Assert.Equal(0,AbilityHandler.CurrentInnerStrength(f.Owner));
        f.Owner.playerInstance.InnerStrengthAvailable.setValue((ushort)4);
        Assert.False(AbilityAuras.PayDrain(f.Owner,589));Assert.Equal(4,AbilityHandler.CurrentInnerStrength(f.Owner));
    }

    [Fact]
    public void ModeOwnsItsIndependentPetRollWithoutLeakingItToDefaultOrOtherModes()
    {
        var plan=NativeQaModes.Find("proxy-technician").Plan;
        Assert.Equal(new ushort[]{2,17,594,589},plan.Abilities);
        Assert.Equal("80",NativeQaFixturePlan.EnvironmentFor("proxy-technician")[ProxyTechnician.QaRollVariable]);
        foreach(string mode in new[]{"","unknown","health-callbacks","zen-master","conditional-controls"})
            Assert.Equal("",NativeQaFixturePlan.EnvironmentFor(mode)[ProxyTechnician.QaRollVariable]);
        Assert.False(plan.FixedCallbackRolls);Assert.False(plan.GmCharacter110);Assert.True(plan.ExtraQaMetadata["ProxyTechnicianQa"].GetBoolean());
    }
}
