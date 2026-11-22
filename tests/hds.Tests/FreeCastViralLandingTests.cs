using hds;
using hds.shared;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
namespace hds.Tests;
[Collection("ConcentrationEnvironment")]
public class FreeCastViralLandingTests : IDisposable
{
    readonly Dictionary<string,WorldClient> oldClients=WorldServer.Clients;
    readonly ServerConfig oldConfig=Store.config;
    readonly WorldClient oldCurrent=Store.currentClient;
    readonly System.Collections.ArrayList oldMobs=WorldServer.mobs;
    readonly string[] keys={"MXO_DEV_COMMANDS","MXO_FREECAST_VIRAL_QA","MXO_FREECAST_VIRAL_QA_ATTACK15","MXO_FREECAST_VIRAL_QA_DEFENSE15","MXO_DB_NAME"};
    readonly string?[] oldEnv;
    public FreeCastViralLandingTests()
    {
        oldEnv=keys.Select(Environment.GetEnvironmentVariable).ToArray();
        foreach(var pair in keys.Zip(new[]{"1","1","0","32766","reality_hd"}))Environment.SetEnvironmentVariable(pair.First,pair.Second);
        Store.config=new ServerConfig("unused");typeof(ServerConfig).GetField("_dbParams",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(Store.config,new DbParams{DatabaseName="mxotest_viral"});
        WorldServer.Clients=new();WorldServer.mobs=new();
    }
    public void Dispose()
    {
        foreach(var c in WorldServer.Clients.Values)AbilityPassives.SetLoaded(c,Array.Empty<(ushort,ushort)>());
        WorldServer.Clients=oldClients;Store.config=oldConfig;Store.currentClient=oldCurrent;WorldServer.mobs=oldMobs;
        for(int i=0;i<keys.Length;i++)Environment.SetEnvironmentVariable(keys[i],oldEnv[i]);
    }
    static WorldClient Client(uint id,int district=1)
    {
        var c=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        c.playerData=new();c.playerData.setCharID(id);c.playerData.setEntityId(id);c.playerData.setDistrictId((uint)district);c.playerData.setOnWorld(true);c.playerData.setUDPSessionEstablished(true);
        c.playerInstance=new();c.messageQueue=new();c.viewMan=new();c.Alive=true;
        c.playerInstance.Health.setValue((ushort)1000);c.playerInstance.MaxHealth.setValue((ushort)1000);c.playerInstance.Level.setValue((byte)50);
        var name=new byte[32];Encoding.ASCII.GetBytes("Viral"+id).CopyTo(name,0);c.playerInstance.CharacterName.setValue(name);
        typeof(WorldClient).GetField("lastUsedTime",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(c,TimeUtils.getUnixTimeUint32());
        typeof(WorldClient).GetField("lastFlushTick",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(c,long.MaxValue);
        WorldServer.Clients[id.ToString()]=c;return c;
    }
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)]
    public void ActualQaDrawRejectsSpoofedDbAndLostFixtureOwnership(int mutation)
    {
        var c=Client(110);AbilityPassives.SetLoaded(c,new[]{((ushort)33,(ushort)50)});
        Assert.True(FreeCastViralContest.TryQaDraw(c,33,out int a,out int d));Assert.Equal(0,a);Assert.Equal(32766,d);
        if(mutation==0)typeof(ServerConfig).GetField("_dbParams",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(Store.config,new DbParams{DatabaseName="reality_hd"});
        if(mutation==1)AbilityPassives.SetLoaded(c,Array.Empty<(ushort,ushort)>());
        if(mutation==2)c.playerData.setUDPSessionEstablished(false);
        if(mutation==3)c.playerData.worldChangePending=true;
        if(mutation==4)c.playerData.setDistrictId((uint)MxOLocations.LA);
        if(mutation==5)c.Alive=false;
        if(mutation==6){var n=new byte[32];Encoding.ASCII.GetBytes("TheNeo").CopyTo(n,0);c.playerInstance.CharacterName.setValue(n);}
        Environment.SetEnvironmentVariable("MXO_DB_NAME","mxotest_spoof");
        Assert.False(FreeCastViralContest.TryQaDraw(c,33,out _,out _));
    }
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void MissingUncreatedOrDeletingPlayerViewProducesNoContestOrEffects(int state)
    {
        var c=Client(110);var victim=Client(111);Store.currentClient=c;
        c.playerData.pvpFlag=victim.playerData.pvpFlag=true;c.playerInstance.OrganizationID.setValue((byte)1);victim.playerInstance.OrganizationID.setValue((byte)2);
        AbilityPassives.SetLoaded(c,new[]{((ushort)33,(ushort)50)});
        if(state!=0){var view=c.viewMan.addView(111,1);view.viewCreated=state==2;view.viewNeedsToBeDeleted=state==2;}
        foreach(var client in WorldServer.Clients.Values){client.messageQueue.RPCMessagesQueue.Clear();client.messageQueue.ObjectMessagesQueue.Clear();}
        Assert.Equal(PvpRules.Verdict.Allowed,PvpCombat.Check(c,victim));
        var ability=new AbilityItem();ability.setAbilityID(33);
        Assert.False(FreeCastViralContest.Player(c,ability,victim));
        foreach(var client in WorldServer.Clients.Values){Assert.Empty(client.messageQueue.RPCMessagesQueue);Assert.Empty(client.messageQueue.ObjectMessagesQueue);}
        Assert.Equal(1000,PvpCombat.HealthOf(victim));
    }
    [Theory]
    [InlineData(19, 5, 19, 692u, 0, 0, 8)]
    [InlineData(19, 19, 19, 692u, 0, 0, 8)]
    [InlineData(50, 19, 19, 692u, 0, 0, 8)]
    [InlineData(50, 19, 19, 693u, 0, 0, 8)]
    [InlineData(50, 19, 50, 692u, 0, 0, 9)]
    [InlineData(50, 19, 19, 692u, 35, 0, 11)]
    [InlineData(50, 19, 19, 692u, 35, 75, 9)]
    public void BodyShotUsesItsFixedBaseWithIndependentRankWeaponAndInfluenceInputs(
        byte level, ushort abilityRank, ushort rifleStyleRank, uint rifle, int ballisticBonus, int resistance, ushort landed)
    {
        const ushort bodyShot = 105;
        var caster = Client(110);
        var target = Client(111);
        Store.currentClient = caster;
        caster.playerData.pvpFlag = target.playerData.pvpFlag = true;
        caster.playerInstance.Level.setValue(level);
        target.playerInstance.Level.setValue(level);
        caster.playerInstance.OrganizationID.setValue((byte)1);
        target.playerInstance.OrganizationID.setValue((byte)2);
        caster.viewMan.addView(111, 1).viewCreated = true;
        caster.playerInstance.EquippedItemID.setValue(rifle);
        AbilityPassives.SetLoaded(caster, new[] { (bodyShot, abilityRank), (CombatStyles.Rifles, rifleStyleRank) });
        CombatStyles.Select(caster, CombatStyles.Rifles);

        Assert.Equal(CombatStyles.Rifles, CombatStyles.Active(caster));
        Assert.Equal(abilityRank, AbilityPassives.Level(caster, bodyShot));
        var weapon = RangedWeapons.GetStats(rifle);
        Assert.Equal(RangedWeapons.ClassRifle, weapon.AttackClass);
        Assert.Equal(rifle == 692 ? 2 : 4, weapon.BaseDamage);

        var source = TimedEffects.ForPlayer(caster);
        var victim = TimedEffects.ForPlayer(target);
        var shot = new AbilityItem();
        shot.setAbilityID(bodyShot);
        shot.setValueFrom(-5);
        shot.setValueTo(0);
        try
        {
            var now = TimeUtils.GetSimTimeSeconds();
            if (ballisticBonus != 0)
                source.Effects.Apply(383, new[]
                {
                    new TimedEffects.Active { Kind = TempMods.KindStat, Target = 171, Value = ballisticBonus, Seconds = 10, Proc = TempMods.Proc.Stat }
                }, now);
            if (resistance != 0)
                victim.Effects.Apply(271, new[]
                {
                    new TimedEffects.Active { Kind = TempMods.KindStat, Target = 181, Value = resistance, Seconds = 10, Proc = TempMods.Proc.Stat }
                }, now);
            int rifleInfluence = rifleStyleRank == 50 ? 13 : 6;
            Assert.Equal(rifleInfluence + ballisticBonus, AbilityStats.Influence(caster, 171));
            Assert.Equal((ushort)8, AbilityHandler.AbilityDamage(shot, new Random(17), characterLevel: level));

            PvpCombat.LandAbilityOn(caster, shot, target);

            Assert.Equal(1000 - landed, PvpCombat.HealthOf(target));
            Assert.Equal(resistance - 5, AbilityStats.Player(target, 181));
            var penalties = victim.Effects.Snapshot().Where(effect => effect.Parent == bodyShot).OrderBy(effect => effect.Target).ToArray();
            Assert.Equal(new ushort[] { 180, 181 }, penalties.Select(effect => effect.Target));
            Assert.All(penalties, effect =>
            {
                Assert.Equal(-5, effect.Value);
                Assert.Equal(10f, effect.Seconds);
            });
        }
        finally
        {
            TimedEffects.Clear(source);
            TimedEffects.Clear(victim);
        }
    }

    [Fact]
    public void FastHealingUsesTheSelectedLivingFriendlyPlayerWithinTenMetres()
    {
        var caster = Client(110);
        var friendly = Client(111);
        var hostile = Client(112);
        Store.currentClient = caster;
        caster.playerData.pvpFlag = friendly.playerData.pvpFlag = hostile.playerData.pvpFlag = true;
        caster.playerInstance.OrganizationID.setValue((byte)1);
        friendly.playerInstance.OrganizationID.setValue((byte)1);
        hostile.playerInstance.OrganizationID.setValue((byte)2);
        caster.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(0, 495, 0));
        friendly.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(1000, 495, 0));
        hostile.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(700, 495, 0));
        var friendView = caster.viewMan.addView(111, 1);
        friendView.viewCreated = true;
        var enemyView = caster.viewMan.addView(112, 1);
        enemyView.viewCreated = true;

        Assert.Same(friendly, Assert.Single(AbilitySupport.Targets(caster, 237, friendView.ViewID)));
        Assert.Empty(AbilitySupport.Targets(caster, 237, enemyView.ViewID));
        Assert.Empty(AbilitySupport.Targets(caster, 237, friendView.ViewID, (ushort)(friendView.spawnId + 1)));
        friendView.viewNeedsToBeDeleted = true;
        Assert.Empty(AbilitySupport.Targets(caster, 237, friendView.ViewID, (ushort)friendView.spawnId));
        friendView.viewNeedsToBeDeleted = false;
        AbilitySupport.Land(caster, 237, friendView.ViewID);
        Assert.Equal(60, AbilityStats.Influence(friendly, 112));
        Assert.Equal(0, AbilityStats.Influence(caster, 112));
        Assert.Equal(0, AbilityStats.Influence(hostile, 112));
        friendly.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(1001, 495, 0));
        Assert.Empty(AbilitySupport.Targets(caster, 237, friendView.ViewID));
        Assert.True(AbilityBuffs.Remove(friendly, 237));
        Assert.Equal(0, AbilityStats.Influence(friendly, 112));
    }

    [Theory]
    [InlineData(399)]
    [InlineData(468)]
    public void RetiringPlayerViewCannotAnchorAHostileArea(ushort abilityId)
    {
        var caster = Client(110);
        var primary = Client(111);
        var secondary = Client(112);
        Store.currentClient = caster;
        caster.playerData.pvpFlag = primary.playerData.pvpFlag = secondary.playerData.pvpFlag = true;
        caster.playerInstance.OrganizationID.setValue((byte)1);
        primary.playerInstance.OrganizationID.setValue((byte)2);
        secondary.playerInstance.OrganizationID.setValue((byte)2);
        var view = caster.viewMan.addView(111, 1);
        view.viewCreated = true;
        view.viewNeedsToBeDeleted = true;
        caster.viewMan.addView(112, 1).viewCreated = true;
        var ability = new AbilityItem();
        ability.setAbilityID(abilityId);

        Assert.Equal(PvpRules.Verdict.Allowed, PvpCombat.Check(caster, primary));
        Assert.False(PvpCombat.LandAbility(caster, ability, view.ViewID, origin: AbilityCastOrigin.NormalFreeCast));
        Assert.Empty(caster.messageQueue.RPCMessagesQueue);
        Assert.Empty(primary.messageQueue.RPCMessagesQueue);
        Assert.Empty(secondary.messageQueue.RPCMessagesQueue);
        Assert.Empty(TimedEffects.ForPlayer(secondary).Effects.Snapshot());
    }

    [Theory]
    [InlineData(399, 800, TempMods.PowerlessAbility, 0, 11.0526, 0, false)]
    [InlineData(400, 500, TempMods.DOTPlaceHolderAbility, 74, 15, 12, false)]
    [InlineData(399, 800, TempMods.PowerlessAbility, 0, 11.0526, 0, true)]
    [InlineData(400, 500, TempMods.DOTPlaceHolderAbility, 74, 15, 12, true)]
    public void DespoilerFieldsLandOnTheSelectedEnemyAndExactRadiusEdge(
        ushort id, int edgeRadius, ushort stat, int amount, double duration, int damagePerTick, bool deflectPrimary)
    {
        var caster = Client(110);
        Store.currentClient = caster;
        caster.playerData.pvpFlag = true;
        caster.playerInstance.Level.setValue((byte)30);
        caster.playerInstance.OrganizationID.setValue((byte)1);
        caster.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(17000, 495, 6400));
        AbilityPassives.SetLoaded(caster, new NativeQa.Modes.HackerDespoilerAreaControlsMode().Plan.Abilities
            .Select(ability => (ability, (ushort)30)));
        CombatStyles.Select(caster, CombatStyles.HackerStyle);
        Environment.SetEnvironmentVariable("MXO_FREECAST_VIRAL_QA_ATTACK15", "32766");
        Environment.SetEnvironmentVariable("MXO_FREECAST_VIRAL_QA_DEFENSE15", "0");

        var primary = Recipient(111, 6600);
        var edge = Recipient(112, 6600 + edgeRadius);
        var outside = Recipient(113, 6601 + edgeRadius);
        var ally = Recipient(114, 6700, organization: 1);
        var unseen = Recipient(115, 6700, visible: false);
        var view = caster.viewMan.FindViewForEntity(111);
        var affected = deflectPrimary ? new[] { edge } : new[] { primary, edge };
        var excluded = deflectPrimary ? new[] { caster, outside, ally, unseen, primary } : new[] { caster, outside, ally, unseen };
        var subjects = affected.Concat(excluded).Select(TimedEffects.ForPlayer).ToArray();
        var ability = new AbilityItem();
        ability.setAbilityID(id);

        try
        {
            if (deflectPrimary)
            {
                Assert.True(AbilityBuffs.Apply(primary, 272));
                primary.playerData.combatTactic = CombatTactic.Block;
                Assert.True(AbilityBuffs.Apply(caster, 314));
                Environment.SetEnvironmentVariable("MXO_FREECAST_VIRAL_QA_ATTACK15", "0");
                Environment.SetEnvironmentVariable("MXO_FREECAST_VIRAL_QA_DEFENSE15", "32766");
            }
            Assert.True(AbilityTargets.Land(caster, ability, view.ViewID, origin: AbilityCastOrigin.NormalFreeCast));
            if (deflectPrimary)
                Assert.Equal(FreeCastViralContest.DeflectFx,
                    NumericalUtils.ByteArrayToUint32(primary.playerInstance.EffectID.getValue(), 1));
            var effects = affected.Select(target =>
            {
                var subject = TimedEffects.ForPlayer(target);
                var effect = Assert.Single(subject.Effects.Snapshot());
                Assert.Equal(id, effect.Parent);
                Assert.Equal(stat, effect.Target);
                Assert.Equal(amount, effect.Value);
                Assert.Equal(duration, effect.Seconds, 3);
                Assert.Equal(id == 400, ControlActions.CanUseAbilities(subject.Effects));
                Assert.True(ControlActions.CanMove(subject.Effects));
                Assert.True(ControlActions.CanUseWeapons(subject.Effects));
                Assert.True(ControlActions.CanWithdraw(subject.Effects));
                Assert.Equal(1000, PvpCombat.HealthOf(target));
                return effect;
            }).ToArray();
            Assert.All(excluded, target => Assert.False(TimedEffects.ForPlayer(target).Effects.HasParent(id)));

            if (id == 400)
                TimedEffects.ForPlayer(edge).Effects.Apply(8,
                    new[] { new TimedEffects.Active { Target = 157, Value = 43, Seconds = 5.1f, Proc = TempMods.Proc.Stat } },
                    effects[0].Start);
            TimedEffects.RunDue(effects.Max(effect => effect.Start) + 2.51);
            Assert.All(affected, target => Assert.Equal(1000 - damagePerTick + (id == 400 && target == edge ? 2 : 0), PvpCombat.HealthOf(target)));
            TimedEffects.RunDue(effects.Max(effect => effect.Start) + 5.01);
            Assert.All(affected, target => Assert.Equal(1000 - 2 * damagePerTick + (id == 400 && target == edge ? 4 : 0), PvpCombat.HealthOf(target)));
            TimedEffects.RunDue(effects.Max(effect => effect.Expires) + 0.01);
            TimedEffects.RunDue(effects.Max(effect => effect.Expires) + 3);
            Assert.All(affected, target => Assert.Equal(1000 - 5 * damagePerTick + (id == 400 && target == edge ? 4 : 0), PvpCombat.HealthOf(target)));
            Assert.All(excluded, target => Assert.Equal(1000, PvpCombat.HealthOf(target)));
            Assert.All(subjects, subject =>
            {
                Assert.False(subject.Effects.HasParent(id));
                Assert.True(ControlActions.CanUseAbilities(subject.Effects));
            });
        }
        finally
        {
            foreach (var subject in subjects) TimedEffects.Clear(subject);
            AbilityBuffs.RemoveAll(caster);
            AbilityBuffs.RemoveAll(primary);
        }

        WorldClient Recipient(uint id, int z, byte organization = 2, bool visible = true)
        {
            var target = Client(id);
            target.playerData.pvpFlag = true;
            target.playerInstance.Level.setValue((byte)30);
            target.playerInstance.OrganizationID.setValue(organization);
            target.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(17000, 495, z));
            if (visible) caster.viewMan.addView(id, 1).viewCreated = true;
            return target;
        }
    }

    [Fact] public void ExistingUpgradeAttacksAndSereneCalmChangeTheActualCommonContest()
    {
        var c=Client(110);var target=Client(111);Store.currentClient=c;
        c.playerData.pvpFlag=target.playerData.pvpFlag=true;c.playerInstance.OrganizationID.setValue((byte)1);target.playerInstance.OrganizationID.setValue((byte)2);
        c.viewMan.addView(111,1).viewCreated=true;AbilityPassives.SetLoaded(c,new[]{((ushort)33,(ushort)50)});
        var ability=new AbilityItem();ability.setAbilityID(33);
        try
        {
            Assert.False(FreeCastViralContest.Player(c,ability,target)); //50 versus69
            Assert.True(AbilityBuffs.Apply(c,314));Assert.Equal(108,AbilityStats.Player(c,342));
            Assert.True(FreeCastViralContest.Player(c,ability,target)); //112 versus69
            Assert.True(AbilityBuffs.Apply(target,272));Assert.Equal(54,AbilityStats.Player(target,183));
            Assert.False(FreeCastViralContest.Player(c,ability,target)); //112 versus117
            Assert.True(AbilityBuffs.Remove(target,272));Assert.True(FreeCastViralContest.Player(c,ability,target));
            Assert.True(AbilityBuffs.Remove(c,314));Assert.False(FreeCastViralContest.Player(c,ability,target));
        }
        finally {AbilityBuffs.RemoveAll(c);AbilityBuffs.RemoveAll(target);}
    }
    [Theory]
    [InlineData(333, false)]
    [InlineData(333, true)]
    [InlineData(336, false)]
    [InlineData(337, true)]
    [InlineData(343, true)]
    public void InvalidAreaPrimaryNeverStartsAContestOrExpandsToLiveNearbyViews(ushort abilityId, bool deleting)
    {
        var caster = Client(110);
        Store.currentClient = caster;
        AbilityPassives.SetLoaded(caster, new[] { (abilityId, (ushort)50) });
        var primary = new Mob();
        primary.setEntityId(1000);
        primary.setDistrict(1);
        primary.setHealthC(1000);
        primary.setHealthM(1000);
        var secondary = new Mob();
        secondary.setEntityId(1001);
        secondary.setDistrict(1);
        secondary.setHealthC(1000);
        secondary.setHealthM(1000);
        WorldServer.mobs.Add(primary);
        WorldServer.mobs.Add(secondary);
        var stale = caster.viewMan.addView(1000, 1);
        stale.viewCreated = deleting;
        stale.viewNeedsToBeDeleted = deleting;
        caster.viewMan.addView(1001, 1).viewCreated = true;
        caster.messageQueue.RPCMessagesQueue.Clear();
        caster.messageQueue.ObjectMessagesQueue.Clear();

        var ability = new AbilityItem();
        ability.setAbilityID(abilityId);
        Assert.False(AbilityTargets.Land(caster, ability, stale.ViewID, origin: AbilityCastOrigin.NormalFreeCast));
        Assert.Empty(caster.messageQueue.RPCMessagesQueue);
        Assert.Empty(caster.messageQueue.ObjectMessagesQueue);
        Assert.Equal((ushort)1000, secondary.getHealthC());
    }

    [Theory]
    [InlineData(21)]
    [InlineData(36)]
    [InlineData(514)]
    [InlineData(33)]
    [InlineData(330)]
    [InlineData(40)]
    [InlineData(57)]
    [InlineData(58)]
    [InlineData(86)]
    [InlineData(354)]
    [InlineData(476)]
    [InlineData(355)]
    [InlineData(311)]
    [InlineData(312)]
    [InlineData(313)]
    [InlineData(43)]
    [InlineData(48)]
    [InlineData(315)]
    [InlineData(329)]
    [InlineData(331)]
    [InlineData(333)]
    [InlineData(336)]
    [InlineData(337)]
    [InlineData(343)]
    [InlineData(350)]
    [InlineData(351)]
    [InlineData(468)]
    [InlineData(59)]
    [InlineData(60)]
    [InlineData(66)]
    [InlineData(69)]
    [InlineData(352)]
    [InlineData(392)]
    [InlineData(393)]
    [InlineData(394)]
    [InlineData(395)]
    [InlineData(396)]
    [InlineData(588)]
    public void ActualNpcLandingDeflectsBeforeDamageDebuffAndProcAndOnlyRoutesToVisibleSameWorld(ushort abilityId)
    {
        var caster=Client(110);Store.currentClient=caster;AbilityPassives.SetLoaded(caster,new[]{(abilityId,(ushort)50)});
        var visible=Client(111);var hidden=Client(112);var foreign=Client(113,2);var deleting=Client(114);
        var mob=new Mob();mob.setEntityId(1000);mob.setMobId(1);mob.setDistrict(1);mob.setName("Target");mob.setHealthC(1000);mob.setHealthM(1000);mob.setLevel(50);WorldServer.mobs.Add(mob);
        foreach(var c in new[]{caster,visible,foreign,deleting}){var v=c.viewMan.addView(1000,1);v.viewCreated=true;if(c==deleting)v.viewNeedsToBeDeleted=true;}
        foreach(var c in WorldServer.Clients.Values){c.messageQueue.RPCMessagesQueue.Clear();c.messageQueue.ObjectMessagesQueue.Clear();}
        int state=mob.currentState;var ability=new AbilityItem();ability.setAbilityID(abilityId);
        Assert.True(AbilityTargets.Land(caster,ability,caster.viewMan.FindViewForEntity(1000).ViewID,origin:AbilityCastOrigin.NormalFreeCast));
        Assert.Equal((ushort)1000,mob.getHealthC());Assert.Equal(state,mob.currentState);
        Assert.Empty(TimedEffects.ForMob(mob).Effects.Snapshot());
        Assert.Equal(0,AbilityStats.Npc(mob,342));Assert.Equal(0,AbilityStats.Npc(mob,183));
        Assert.Single(caster.messageQueue.RPCMessagesQueue);Assert.Single(visible.messageQueue.RPCMessagesQueue);
        Assert.Single(caster.messageQueue.ObjectMessagesQueue);Assert.Single(visible.messageQueue.ObjectMessagesQueue);
        foreach(var c in new[]{hidden,foreign,deleting}){Assert.Empty(c.messageQueue.RPCMessagesQueue);Assert.Empty(c.messageQueue.ObjectMessagesQueue);}
    }
    [Theory]
    [InlineData(343, 5, 1009, -21)]
    [InlineData(401, 7, 117, -20)]
    [InlineData(403, 5, 183, -50)]
    public void SelectedFieldUsesThePrimaryCentreAndEachRecipientOwnContest(ushort id, int radius, ushort witnessStat, int value)
    {
        var caster = Client(110);
        var primary = Client(111);
        var edge = Client(112);
        var outside = Client(113);
        var ally = Client(114);
        var unseen = Client(115);
        Store.currentClient = caster;
        var names = new[] { caster, primary, edge, outside, ally, unseen };
        double centre = 200, boundary = centre + radius * 100;
        double[] positions = { 0, centre, boundary, boundary + 1, centre + 100, centre + 100 };
        foreach (var (player, x) in names.Zip(positions))
        {
            player.playerData.pvpFlag = true;
            player.playerInstance.OrganizationID.setValue((byte)(player == caster || player == ally ? 1 : 2));
            player.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(x, 0, 0));
            if (player != caster && player != unseen)
                caster.viewMan.addView(player.playerData.getEntityId(), 1).viewCreated = true;
        }
        var view = caster.viewMan.FindViewForEntity(primary.playerData.getEntityId());
        var ability = new AbilityItem();
        ability.setAbilityID(id);
        AbilityPassives.SetLoaded(caster, new[] { (id, (ushort)30) });

        try
        {
            Assert.True(AbilityBuffs.Apply(caster, 314)); // fixed draw wins against the edge recipient
            Assert.True(AbilityBuffs.Apply(primary, 272)); // +54 Viral Defense makes the same draw deflect at centre
            Assert.True(AbilityTargets.Land(caster, ability, view.ViewID, origin: AbilityCastOrigin.NormalFreeCast));
            Assert.False(TimedEffects.ForPlayer(primary).Effects.HasParent(id));
            Assert.Equal(FreeCastViralContest.DeflectFx, NumericalUtils.ByteArrayToUint32(primary.playerInstance.EffectID.getValue(), 1));
            Assert.Equal(value, AbilityStats.Player(edge, witnessStat));
            Assert.True(TimedEffects.ForPlayer(edge).Effects.HasParent(id));
            Assert.Equal(1000, PvpCombat.HealthOf(edge));
            foreach (var excluded in new[] { caster, outside, ally, unseen })
                Assert.False(TimedEffects.ForPlayer(excluded).Effects.HasParent(id));
            TimedEffects.RunDue(TimeUtils.GetSimTimeSeconds() + 46);
            Assert.Equal(0, AbilityStats.Player(edge, witnessStat));
        }
        finally
        {
            foreach (var player in names)
            {
                TimedEffects.Clear(TimedEffects.ForPlayer(player));
                AbilityBuffs.RemoveAll(player);
            }
        }
    }
}
