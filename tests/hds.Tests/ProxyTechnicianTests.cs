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
        Assert.Equal(30, AbilityBuffBuffer.Size(589));
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
        internal void RemoveOwnPet() { Pet.Gone=true;lock(Registry)Registry.Remove(Owner.playerData.getCharID()); }
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
        var b=ProxyTechnician.Project(sourceB.Owner,recipient.Pet,now+1);Assert.NotNull(b);Assert.NotSame(a,b);
        ProxyTechnician.End(sourceA.Owner);sourceA.Owner.playerData.lifeGeneration++;
        Assert.Equal(64,ProxyTechnician.Accuracy(recipient.Pet,now+2,false));
        sourceB.Owner.playerData.setOnWorld(false);sourceB.Owner.playerData.setOnWorld(true);
        Assert.Equal(0,ProxyTechnician.Accuracy(recipient.Pet,now+2,false));
        Assert.Null(ProxyTechnician.Project(sourceB.Owner,recipient.Pet,now+3));
        Assert.True(ProxyTechnician.Begin(sourceB.Owner));Assert.NotNull(ProxyTechnician.Project(sourceB.Owner,recipient.Pet,now+3));
        recipient.Owner.playerData.lifeGeneration++;Assert.Equal(0,ProxyTechnician.Accuracy(recipient.Pet,now+4,false));
    }

    [Fact]
    public void CapturedOldActivationCannotOverwriteARestartOrAnotherCastersCurrentReplacement()
    {
        using var sourceA=new Fixture();using var sourceB=new Fixture();using var recipient=new Fixture();double now=TimeUtils.GetSimTimeSeconds();
        var old=ProxyTechnician.Capture(sourceA.Owner);Assert.NotNull(old);
        ProxyTechnician.End(sourceA.Owner);Assert.True(ProxyTechnician.Begin(sourceA.Owner,7));
        var restarted=ProxyTechnician.Capture(sourceA.Owner,7);Assert.NotNull(restarted);Assert.NotSame(old,restarted);
        Assert.Null(ProxyTechnician.Capture(sourceA.Owner,6));
        var replacement=ProxyTechnician.Project(sourceB.Owner,recipient.Pet,now);Assert.NotNull(replacement);replacement.Melee=9;
        Assert.Null(ProxyTechnician.ProjectCaptured(sourceA.Owner,recipient.Pet,old,now+1));
        Assert.Equal(9,ProxyTechnician.Accuracy(recipient.Pet,now+1,false));
        ProxyTechnician.End(sourceA.Owner);Assert.Null(ProxyTechnician.ProjectCaptured(sourceA.Owner,recipient.Pet,restarted,now+2));
        Assert.Equal(9,ProxyTechnician.Accuracy(recipient.Pet,now+2,false));
    }

    [Fact]
    public void ASourceWithNoLiveOwnPetStillReachesAFriendlyRecipientsPet()
    {
        using var source=new Fixture();using var recipient=new Fixture();source.RemoveOwnPet();
        Assert.Null(CoderPets.Of(source.Owner));Assert.True(ProxyTechnician.Matches(source.Owner));
        double now=TimeUtils.GetSimTimeSeconds();Assert.NotNull(ProxyTechnician.Project(source.Owner,recipient.Pet,now));
        source.Owner.playerInstance.InnerStrengthAvailable.setValue((ushort)10);
        Assert.True(AbilityAuras.PayDrain(source.Owner,589));Assert.Equal(5,AbilityHandler.CurrentInnerStrength(source.Owner));
        Assert.NotNull(ProxyTechnician.Project(source.Owner,recipient.Pet,now+6));
        Assert.Equal(64,ProxyTechnician.Accuracy(recipient.Pet,now+7,false));
        Assert.Null(ProxyTechnician.Project(source.Owner,source.Pet,TimeUtils.GetSimTimeSeconds()));
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
    public void DaemonBoostUsesTheFriendlyOwnerAndInclusivePetRadiusWithIndependentExpiry()
    {
        using var source = new Fixture();
        using var target = new Fixture();
        source.RemoveOwnPet();
        source.Owner.playerData.passives.Loaded[609] = 25;
        source.Owner.playerInstance.Level.setValue((byte)25);
        target.Pet.Mob.setXPos(2000);
        Assert.Equal(19, AbilityCatalog.Find(699).Number("Ability/CastDirectObjects"));
        Assert.Equal(699, AbilityAuras.EffectOf(609));
        Assert.Equal(5, AbilityAuras.DrainOf(609));
        Assert.Equal(5, AbilityAuras.IntervalOf(609));
        var pulse = DaemonBoost.Project(source.Owner, target.Pet, 20);
        Assert.NotNull(pulse);
        Assert.Equal((12, 12), DaemonBoost.Values(target.Pet, 24));
        Assert.Equal(25, pulse.Until);
        target.Pet.Mob.setXPos(2001);
        Assert.Null(DaemonBoost.Project(source.Owner, target.Pet, 21));

        target.Pet.Mob.setXPos(1900);
        target.Owner.playerData.pvpFlag = true;
        target.Owner.playerInstance.OrganizationID.setValue((byte)2);
        Assert.Equal(PvpRules.Verdict.Allowed, PvpCombat.Check(source.Owner, target.Owner));
        Assert.Null(DaemonBoost.Project(source.Owner, target.Pet, 22));
        Assert.Equal((12, 12), DaemonBoost.Values(target.Pet, 24));
        Assert.Equal((0, 0), DaemonBoost.Values(target.Pet, 25));
    }

    [Fact]
    public void DaemonBoostAffectsOnlyNukerDamageAndHealerHealingAndCannotFollowAnOldSource()
    {
        using var source = new Fixture();
        using var next = new Fixture();
        using var target = new Fixture();
        source.Owner.playerData.passives.Loaded[609] = 40;
        source.Owner.playerInstance.Level.setValue((byte)40);
        next.Owner.playerData.passives.Loaded[609] = 50;
        Assert.NotNull(DaemonBoost.Project(source.Owner, target.Pet, 20));
        target.Pet.Kind = CoderPets.Kind.Proxy;
        Assert.Equal(80, CoderPets.DaemonBoostDamage(target.Pet, 80, 21));
        Assert.Equal(80, CoderPets.DaemonBoostHealing(target.Pet, 80, 21));
        target.Pet.Kind = CoderPets.Kind.Nuker;
        Assert.Equal(96, CoderPets.DaemonBoostDamage(target.Pet, 80, 21));
        Assert.Equal(80, CoderPets.DaemonBoostHealing(target.Pet, 80, 21));
        target.Pet.Kind = CoderPets.Kind.Healer;
        Assert.Equal(80, CoderPets.DaemonBoostDamage(target.Pet, 80, 21));
        Assert.Equal(96, CoderPets.DaemonBoostHealing(target.Pet, 80, 21));

        source.Owner.playerData.passives.Loaded.Remove(609);
        DaemonBoost.LoadoutChanged(source.Owner);
        Assert.Null(DaemonBoost.Project(source.Owner, target.Pet, 22));
        Assert.Equal(96, CoderPets.DaemonBoostHealing(target.Pet, 80, 22));
        Assert.NotNull(DaemonBoost.Project(next.Owner, target.Pet, 23));
        source.Owner.playerData.lifeGeneration++;
        Assert.Equal(100, CoderPets.DaemonBoostHealing(target.Pet, 80, 24));
        next.Owner.playerData.worldGeneration++;
        Assert.Equal(80, CoderPets.DaemonBoostHealing(target.Pet, 80, 24));
        Assert.NotNull(DaemonBoost.Project(next.Owner, target.Pet, 25));
        target.Owner.playerData.lifeGeneration++;
        Assert.Equal(80, CoderPets.DaemonBoostHealing(target.Pet, 80, 25));
    }

    [Fact]
    public void DaemonAccuracyUsesTheTenMetreFriendlyBoundaryAndThreeActualAttackChannels()
    {
        using var source = new Fixture();
        using var target = new Fixture();
        source.RemoveOwnPet();
        source.Owner.playerData.passives.Loaded[619] = 50;
        Assert.True(AbilityAuras.Handles(619));
        Assert.Equal(47, AbilityBuffBuffer.Size(619));
        Assert.Equal(19, AbilityCatalog.Find(700).Number("Ability/CastDirectObjects"));
        Assert.Equal(700, AbilityAuras.EffectOf(619));
        Assert.Equal(5, AbilityAuras.DrainOf(619));
        Assert.Equal(5, AbilityAuras.IntervalOf(619));
        target.Pet.Mob.setXPos(1000);

        var pulse = Assert.IsType<DaemonBoost.PulseEffect>(DaemonBoost.Project(source.Owner, target.Pet, 20, 619));
        Assert.Equal((0, 0, 64, 64, 64), pulse.Values);
        Assert.Equal(25, pulse.Until);
        Assert.Equal(64, DaemonBoost.Accuracy(target.Pet, 24, ranged: false));
        Assert.Equal(64, DaemonBoost.Accuracy(target.Pet, 24, ranged: true));
        Assert.Equal(95, CoderPets.HitChance(target.Pet, 50, 24));
        target.Pet.Kind = CoderPets.Kind.Nuker;
        Assert.Equal(64, DaemonBoost.Accuracy(target.Pet, 24, ranged: true));
        Assert.Equal(95, CoderPets.HitChance(target.Pet, 50, 24));
        target.Pet.Kind = CoderPets.Kind.Healer;
        Assert.Equal(0, DaemonBoost.Accuracy(target.Pet, 24, ranged: false));
        Assert.Equal(80, CoderPets.DaemonBoostHealing(target.Pet, 80, 24));

        target.Pet.Mob.setXPos(1001);
        Assert.Null(DaemonBoost.Project(source.Owner, target.Pet, 21, 619));
        target.Pet.Mob.setXPos(500);
        target.Owner.playerData.pvpFlag = true;
        target.Owner.playerInstance.OrganizationID.setValue((byte)2);
        Assert.Equal(PvpRules.Verdict.Allowed, PvpCombat.Check(source.Owner, target.Owner));
        Assert.Null(DaemonBoost.Project(source.Owner, target.Pet, 22, 619));
        target.Pet.Kind = CoderPets.Kind.Proxy;
        Assert.Equal(64, DaemonBoost.Accuracy(target.Pet, 24, ranged: false));
        Assert.Equal(75, CoderPets.HitChance(target.Pet, 50, 25));
    }

    [Theory]
    [InlineData(CoderPets.Kind.Proxy, "Karate", 79)]
    [InlineData(CoderPets.Kind.Proxy, "Rifle", 82)]
    [InlineData(CoderPets.Kind.Nuker, "Nuker", 86)]
    [InlineData(CoderPets.Kind.Healer, "Healer", 75)]
    public void DaemonAccuracyConsumesOnlyTheAttackingPetChannel(CoderPets.Kind kind, string sheet, int expected)
    {
        using var source = new Fixture();
        using var target = new Fixture();
        target.Pet.Kind = kind;
        target.Pet.Sheet = sheet;
        target.Pet.DaemonBoostAccuracyEffect = new DaemonBoost.PulseEffect(source.Owner, target.Owner, 700,
            (0, 0, 4, 7, 11), 15);

        Assert.Equal(expected, CoderPets.HitChance(target.Pet, 50, 14));
        Assert.Equal(75, CoderPets.HitChance(target.Pet, 50, 15));
    }

    [Fact]
    public void DaemonAccuracyAndDamageChildrenKeepIndependentExpiriesAndSourceLifetimes()
    {
        using var source = new Fixture();
        using var next = new Fixture();
        using var target = new Fixture();
        source.Owner.playerData.passives.Loaded[609] = 50;
        source.Owner.playerData.passives.Loaded[619] = 50;
        next.Owner.playerData.passives.Loaded[619] = 47;
        next.Owner.playerInstance.Level.setValue((byte)47);

        CoderPets.ApplyPowerBoost(target.Pet, 50, 10);
        Assert.NotNull(DaemonBoost.Project(source.Owner, target.Pet, 308));
        Assert.NotNull(DaemonBoost.Project(source.Owner, target.Pet, 309, 619));
        Assert.Equal(100, CoderPets.PowerBoostDamage(target.Pet, 80, 309));
        Assert.Equal(80, CoderPets.PowerBoostDamage(target.Pet, 80, 310));
        Assert.Equal(95, CoderPets.HitChance(target.Pet, 50, 310));
        Assert.Equal((25, 25), DaemonBoost.Values(target.Pet, 312));
        Assert.Equal((0, 0), DaemonBoost.Values(target.Pet, 313));
        Assert.Equal(64, DaemonBoost.Accuracy(target.Pet, 313, ranged: false));
        Assert.Equal(75, CoderPets.HitChance(target.Pet, 50, 314));

        Assert.NotNull(DaemonBoost.Project(source.Owner, target.Pet, 320, 619));
        source.Owner.playerData.passives.Loaded.Remove(619);
        DaemonBoost.LoadoutChanged(source.Owner);
        Assert.Null(DaemonBoost.Project(source.Owner, target.Pet, 321, 619));
        Assert.Equal(64, DaemonBoost.Accuracy(target.Pet, 321, ranged: false));
        Assert.NotNull(DaemonBoost.Project(next.Owner, target.Pet, 322, 619));
        source.Owner.playerData.lifeGeneration++;
        Assert.Equal(61, DaemonBoost.Accuracy(target.Pet, 322, ranged: false));
        target.Owner.playerData.worldGeneration++;
        Assert.Equal(0, DaemonBoost.Accuracy(target.Pet, 323, ranged: false));
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
