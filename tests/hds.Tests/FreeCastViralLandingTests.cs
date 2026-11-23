using hds;
using hds.shared;
using MxoHd.PacketResearch;
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

    [Theory]
    [InlineData(238, 0, true, 46)]
    [InlineData(311, 0, true, 43)]
    [InlineData(312, 0, true, 55)]
    [InlineData(313, 0, true, 25)]
    [InlineData(238, 64, true, 37)]
    [InlineData(311, 64, true, 35)]
    [InlineData(312, 64, true, 45)]
    [InlineData(313, 64, true, 20)]
    [InlineData(313, 0, false, 0)]
    public void DoctorAndArbalestFixedDamageUsesTheLiveViralContestAndTypedHealthPath(ushort id, int resistance,
        bool hits, int expected)
    {
        var caster = Client(110);
        var target = Client(111);
        caster.playerInstance.Level.setValue((byte)43);
        target.playerInstance.Level.setValue((byte)43);
        caster.playerInstance.OrganizationID.setValue((byte)1);
        target.playerInstance.OrganizationID.setValue((byte)2);
        caster.playerData.pvpFlag = target.playerData.pvpFlag = true;
        caster.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(17000, 495, 6400));
        target.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(17000, 495, 6600));
        var view = caster.viewMan.addView(111, 12);
        view.viewCreated = true;
        view.spawnId = 9;
        var profile = new NativeQa.Modes.HackerDoctorArbalestViralMode().Plan.Abilities;
        AbilityPassives.SetLoaded(caster, profile.Select(loaded => (loaded, (ushort)43)));
        CombatStyles.Select(caster, CombatStyles.HackerStyle);
        Assert.Equal(40, AbilityStats.Influence(caster, 172));
        Assert.Equal(4, AbilityStats.Influence(caster, 117));
        Assert.Equal(id == 238 ? 29 : AbilityCosts.Cost(id), AbilityCosts.Effective(caster, id));
        target.playerData.passives.Totals[157] = resistance;
        if (!hits) target.playerData.passives.Totals[183] = 100;
        Environment.SetEnvironmentVariable("MXO_FREECAST_VIRAL_QA_ATTACK15", hits ? "32766" : "0");
        Environment.SetEnvironmentVariable("MXO_FREECAST_VIRAL_QA_DEFENSE15", hits ? "0" : "32766");
        Assert.True(FreeCastViralContest.TryQaDraw(caster, id, out _, out _));

        var ability = new AbilityItem();
        ability.setAbilityID(id);
        ability.setAbilityName(AbilityCatalog.Find(id).Name);
        Assert.True(PvpCombat.LandAbility(caster, ability, view.ViewID, origin: AbilityCastOrigin.NormalFreeCast));
        Assert.Equal(1000 - expected, PvpCombat.HealthOf(target));
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

    [Theory]
    [InlineData(50, 0, 181, 0, 16)]
    [InlineData(50, 0, 180, 75, 16)]
    [InlineData(50, 0, 181, 75, 13)]
    [InlineData(25, 9, 181, 0, 17)]
    [InlineData(25, 9, 181, 20, 15)]
    public void PistolAerialLandsItsFixedBaseAgainstBallisticResistance(byte level, int influence, ushort stat, int resistance, ushort damage)
    {
        var caster = Client(110);
        var target = Client(111);
        Store.currentClient = caster;
        caster.playerData.pvpFlag = target.playerData.pvpFlag = true;
        caster.playerInstance.OrganizationID.setValue((byte)1);
        target.playerInstance.OrganizationID.setValue((byte)2);
        caster.viewMan.addView(111, 1).viewCreated = true;
        var shot = new AbilityItem();
        shot.setAbilityID(AbilityConditionalControls.PistolAerial);
        caster.playerInstance.Level.setValue(level);
        target.playerInstance.Level.setValue(level);
        var casterEffects = TimedEffects.ForPlayer(caster);
        var effects = TimedEffects.ForPlayer(target);
        try
        {
            casterEffects.Effects.Apply(129, new[]
            {
                new TimedEffects.Active { Kind = TempMods.KindStat, Target = 171, Value = influence,
                    Seconds = 10, Proc = TempMods.Proc.Stat }
            }, TimeUtils.GetSimTimeSeconds());
            effects.Effects.Apply(271, new[]
            {
                new TimedEffects.Active { Kind = TempMods.KindStat, Target = stat, Value = resistance,
                    Seconds = 10, Proc = TempMods.Proc.Stat }
            }, TimeUtils.GetSimTimeSeconds());

            PvpCombat.LandAbilityOn(caster, shot, target);

            Assert.Equal(1000 - damage, PvpCombat.HealthOf(target));
        }
        finally
        {
            TimedEffects.Clear(casterEffects);
            TimedEffects.Clear(effects);
        }
    }

    [Fact]
    public void TrojanHorseDrainsOnlyNearbyHostilePlayersEveryFourthSecond()
    {
        var caster = Client(110);
        var target = Client(111);
        var distant = Client(112);
        var ally = Client(113);
        var unseen = Client(114);
        typeof(WorldClient).GetField("<TransportFence>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(caster, new WorldSendFence());
        caster.playerData.passives.Loaded[945] = 30;
        foreach (var player in new[] { caster, target, distant, ally, unseen })
        {
            player.playerData.pvpFlag = true;
            player.playerInstance.OrganizationID.setValue((byte)(player == caster || player == ally ? 1 : 2));
            player.playerInstance.InnerStrengthMax.setValue((ushort)200);
            player.playerInstance.InnerStrengthAvailable.setValue((ushort)200);
            double x = player == caster ? 0 : player == distant ? 301 : 299;
            player.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(x, 0, 0));
            if (player != caster && player != unseen)
                caster.viewMan.addView(player.playerData.getEntityId(), 1).viewCreated = true;
        }

        try
        {
            Assert.True(AbilityAuras.Handles(945));
            Assert.Equal(4, AbilityAuras.IntervalOf(945));
            Assert.Equal(946, AbilityAuras.EffectOf(945));
            Assert.Equal(PvpRules.Verdict.Allowed, PvpCombat.Check(caster, target));
            AbilityAuras.Start(caster, 945);
            Assert.True(AbilityAuras.IsOn(caster, 945));
            Assert.Equal(200, AbilityHandler.CurrentInnerStrength(target)); // no initial pulse
            Assert.True(FreeCastViralContest.TryQaDraw(caster, 946, out int attack, out int defense));
            Assert.Equal(0, attack);
            Assert.Equal(32766, defense);

            AbilityAuras.Tick(caster, 945, 1);
            Assert.Equal(180, AbilityHandler.CurrentInnerStrength(caster));
            Assert.Equal(200, AbilityHandler.CurrentInnerStrength(target));
            Assert.Equal(FreeCastViralContest.DeflectFx, NumericalUtils.ByteArrayToUint32(target.playerInstance.EffectID.getValue(), 1));
            Assert.Equal(1000, PvpCombat.HealthOf(target));
            foreach (var excluded in new[] { distant, ally, unseen })
            {
                Assert.Equal(200, AbilityHandler.CurrentInnerStrength(excluded));
                Assert.Equal(0u, NumericalUtils.ByteArrayToUint32(excluded.playerInstance.EffectID.getValue(), 1));
            }

            Environment.SetEnvironmentVariable(FreeCastViralContest.AttackQa, "32766");
            Environment.SetEnvironmentVariable(FreeCastViralContest.DefenseQa, "0");
            AbilityAuras.Tick(caster, 945, 1);
            Assert.Equal(160, AbilityHandler.CurrentInnerStrength(caster));
            Assert.Equal(180, AbilityHandler.CurrentInnerStrength(target));
            target.playerInstance.InnerStrengthAvailable.setValue((ushort)8);
            AbilityAuras.Tick(caster, 945, 1);
            Assert.Equal(140, AbilityHandler.CurrentInnerStrength(caster));
            Assert.Equal(0, AbilityHandler.CurrentInnerStrength(target));
            Assert.Equal(1000, PvpCombat.HealthOf(target));

            caster.playerInstance.InnerStrengthAvailable.setValue((ushort)19);
            target.playerInstance.InnerStrengthAvailable.setValue((ushort)100);
            AbilityAuras.Tick(caster, 945, 1);
            Assert.False(AbilityAuras.IsOn(caster, 945));
            AbilityAuras.Tick(caster, 945, 1);
            Assert.Equal(19, AbilityHandler.CurrentInnerStrength(caster));
            Assert.Equal(100, AbilityHandler.CurrentInnerStrength(target));
        }
        finally
        {
            AbilityAuras.Clear(caster, announce: false);
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
    [InlineData(false)]
    [InlineData(true)]
    public void StunPulseReachesTheSelectedAreaWithIndependentDeflectionAndImmunity(bool deflectPrimary)
    {
        const ushort pulse = 595, proxyMaster = 599;
        const int x = 17000, y = 495, casterZ = 6400, centreZ = 6600;
        var caster = Client(110);
        var primary = Recipient(111, centreZ);
        var edge = Recipient(112, centreZ + 700);
        var outside = Recipient(113, centreZ + 701);
        var friendly = Recipient(114, centreZ + 100, 1);
        var subjects = new[] { primary, edge, outside, friendly }.Select(TimedEffects.ForPlayer).ToArray();
        var primaryView = caster.viewMan.FindViewForEntity(111);
        primaryView.spawnId = 9;

        caster.playerInstance.Level.setValue((byte)50);
        caster.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(x, y, casterZ));
        caster.playerInstance.OrganizationID.setValue((byte)1);
        caster.playerInstance.InnerStrengthAvailable.setValue((ushort)700);
        caster.playerInstance.InnerStrengthMax.setValue((ushort)900);
        caster.playerData.pvpFlag = true;
        typeof(WorldClient).GetField("<TransportFence>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(caster, new WorldSendFence());
        AbilityPassives.SetLoaded(caster, new[] { (pulse, (ushort)38), (CombatStyles.CoderStyle, (ushort)50), (proxyMaster, (ushort)50) });
        CombatStyles.Select(caster, CombatStyles.CoderStyle);
        Store.currentClient = caster;

        var loaderField = typeof(DataLoader).GetField("Instance", BindingFlags.Static | BindingFlags.NonPublic)!;
        object? previous = loaderField.GetValue(null);
        var loader = (DataLoader)RuntimeHelpers.GetUninitializedObject(typeof(DataLoader));
        var ability = new AbilityItem();
        ability.setAbilityID(pulse);
        ability.setAbilityName("StunPulseAbility");
        ability.setIsBuff(true); // The legacy data row carries this classification.
        ability.setCastingTime(6);
        ability.setCastAnimStart(new byte[4]);
        loader.AbilityDB = new() { ability };
        loaderField.SetValue(null, loader);

        try
        {
            if (deflectPrimary)
            {
                Assert.True(AbilityBuffs.Apply(primary, 272));
                primary.playerData.combatTactic = CombatTactic.Block;
                Assert.True(AbilityBuffs.Apply(caster, 314));
            }
            else
            {
                Environment.SetEnvironmentVariable("MXO_FREECAST_VIRAL_QA_ATTACK15", "32766");
                Environment.SetEnvironmentVariable("MXO_FREECAST_VIRAL_QA_DEFENSE15", "0");
            }

            // Server-handler regression input. The native bootstrap uses only captured stock frames.
            var request = new byte[41];
            BitConverter.GetBytes(pulse).CopyTo(request, 0);
            BitConverter.GetBytes(primaryView.ViewID).CopyTo(request, 2);
            BitConverter.GetBytes((ushort)primaryView.spawnId).CopyTo(request, 4);
            double before = TimeUtils.GetSimTimeSeconds();
            new AbilityHandler().ProcessAbility(ref request);
            double end = caster.playerData.castEndsAt;
            Assert.InRange(end, before + 6, TimeUtils.GetSimTimeSeconds() + 6);
            Assert.Equal(30, AbilityCosts.Cost(pulse));
            Assert.Equal(671, AbilityHandler.CurrentInnerStrength(caster));
            Assert.All(subjects, subject => Assert.Empty(subject.Effects.Snapshot()));
            AbilityCastInterrupts.FinishDue(caster, end);
            Assert.True(AbilityCooldowns.IsCoolingDown(caster.playerData, pulse, TimeUtils.GetSimTimeSeconds()));

            var expected = deflectPrimary ? new[] { edge } : new[] { primary, edge };
            Assert.True(TempMods.TryGet(pulse, out var effect));
            var applied = expected.Select(target =>
            {
                var subject = TimedEffects.ForPlayer(target);
                var records = subject.Effects.Snapshot();
                var stun = Assert.Single(records, effect => effect.Parent == pulse);
                Assert.Equal(TempMods.StunAbility, stun.Target);
                Assert.Equal(6, stun.Seconds);
                var immunity = Assert.Single(records, effect => effect.Parent == TempMods.ControlImmunityTimerAbility);
                Assert.Equal(TempMods.ISDrainTargetAbility, immunity.Target);
                Assert.Equal(12, immunity.Seconds);
                Assert.False(ControlActions.CanUseAbilities(subject.Effects));

                // A successful hostile contest before immunity expires cannot refresh either record.
                Assert.Empty(TimedEffects.Apply(subject, effect, stun.Start + 1, caster));
                return (subject, stun, immunity);
            }).ToArray();

            double afterStun = applied.Max(record => record.stun.Expires) + 0.01;
            TimedEffects.RunDue(afterStun);
            Assert.All(applied, record =>
            {
                Assert.True(ControlActions.CanUseAbilities(record.subject.Effects));
                Assert.True(record.subject.Effects.HasControlImmunity(afterStun));
            });

            double afterImmunity = applied.Max(record => record.immunity.Expires) + 0.01;
            TimedEffects.RunDue(afterImmunity);
            Assert.All(applied, record => Assert.False(record.subject.Effects.HasControlImmunity(afterImmunity)));

            if (deflectPrimary)
            {
                Assert.Equal(FreeCastViralContest.DeflectFx,
                    NumericalUtils.ByteArrayToUint32(primary.playerInstance.EffectID.getValue(), 1));
                Assert.Empty(TimedEffects.ForPlayer(primary).Effects.Snapshot());
            }
            Assert.Empty(TimedEffects.ForPlayer(outside).Effects.Snapshot());
            Assert.Empty(TimedEffects.ForPlayer(friendly).Effects.Snapshot());
            Assert.All(new[] { caster, primary, edge, outside, friendly }, target => Assert.Equal(1000, PvpCombat.HealthOf(target)));
        }
        finally
        {
            foreach (var subject in subjects) TimedEffects.Clear(subject);
            AbilityBuffs.RemoveAll(caster);
            AbilityBuffs.RemoveAll(primary);
            CombatManager.RemoveClient(caster);
            loaderField.SetValue(null, previous);
        }

        WorldClient Recipient(uint id, int z, byte organization = 2)
        {
            var target = Client(id);
            target.playerData.pvpFlag = true;
            target.playerInstance.Level.setValue((byte)50);
            target.playerInstance.OrganizationID.setValue(organization);
            target.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(x, y, z));
            caster.viewMan.addView(id, 1).viewCreated = true;
            return target;
        }
    }

    [Theory]
    [InlineData(361, 800, 35, false)]
    [InlineData(361, 800, 35, true)]
    [InlineData(465, 400, 17, false)]
    [InlineData(465, 400, 17, true)]
    public void FumbleFieldAndUiLagApplyEveryTypedModifierAndExpireOnIndependentRecipients(
        ushort id, int radius, float duration, bool primaryDeflects)
    {
        var caster = Client(110);
        Store.currentClient = caster;
        caster.playerData.pvpFlag = true;
        caster.playerInstance.OrganizationID.setValue((byte)1);
        caster.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(17000, 495, 6400));
        AbilityPassives.SetLoaded(caster, new[] { (id, (ushort)50) });
        Environment.SetEnvironmentVariable("MXO_FREECAST_VIRAL_QA_ATTACK15", "32766");
        Environment.SetEnvironmentVariable("MXO_FREECAST_VIRAL_QA_DEFENSE15", "32766");

        const int centre = 6700;
        var primary = Recipient(111, centre);
        var edge = Recipient(112, centre + radius);
        var outside = Recipient(113, centre + radius + 1);
        var friend = Recipient(114, centre + 100, organization: 1);
        var hidden = Recipient(115, centre + 100, visible: false);
        if (primaryDeflects) primary.playerData.passives.Totals[183] = 150;
        var affected = primaryDeflects ? new[] { edge } : new[] { primary, edge };
        var excluded = primaryDeflects ? new[] { caster, outside, friend, hidden, primary } : new[] { caster, outside, friend, hidden };
        var participants = affected.Concat(excluded).ToArray();
        var ability = new AbilityItem();
        ability.setAbilityID(id);
        (ushort Stat, int Value)[] expected = id == 361
            ? new (ushort, int)[] { (524, -27), (1032, -27) }
            : new (ushort, int)[] { (524, -20), (1010, -18), (1011, -18), (1032, -20) };

        try
        {
            var targetView = caster.viewMan.FindViewForEntity(111);
            Assert.True(AbilityTargets.Land(caster, ability, targetView.ViewID, origin: AbilityCastOrigin.NormalFreeCast));
            if (primaryDeflects)
                Assert.Equal(FreeCastViralContest.DeflectFx,
                    NumericalUtils.ByteArrayToUint32(primary.playerInstance.EffectID.getValue(), 1));

            double lastExpiry = 0;
            foreach (var target in affected)
            {
                var effects = TimedEffects.ForPlayer(target).Effects.Snapshot().Where(effect => effect.Parent == id).ToArray();
                Assert.Equal(expected, effects.OrderBy(effect => effect.Target).Select(effect => (effect.Target, effect.Value)));
                Assert.All(effects, effect => Assert.Equal(duration, effect.Seconds));
                Assert.Equal(expected.Select(pair => (pair.Stat, pair.Value, duration)), Records(target, false)
                    .Select(record => (record.TargetId, record.SignedValue, record.Duration)));
                lastExpiry = Math.Max(lastExpiry, effects.Max(effect => effect.Expires));
                foreach (var (stat, value) in expected) Assert.Equal(value, AbilityStats.Player(target, stat));
            }
            Assert.All(excluded, target =>
            {
                Assert.False(TimedEffects.ForPlayer(target).Effects.HasParent(id));
                Assert.Empty(Records(target, false));
            });
            Assert.All(participants, target => Assert.Equal(1000, PvpCombat.HealthOf(target)));

            TimedEffects.RunDue(lastExpiry + 0.01);
            foreach (var target in affected)
            {
                Assert.False(TimedEffects.ForPlayer(target).Effects.HasParent(id));
                Assert.Equal(Records(target, false).Select(record => (record.TargetId, record.RecordId)),
                    Records(target, true).Select(record => (record.TargetId, record.RecordId)));
                foreach (var (stat, _) in expected) Assert.Equal(0, AbilityStats.Player(target, stat));
            }
        }
        finally
        {
            foreach (var client in participants) TimedEffects.Clear(TimedEffects.ForPlayer(client));
        }

        WorldClient Recipient(uint id, int z, byte organization = 2, bool visible = true)
        {
            var recipient = Client(id);
            recipient.playerData.pvpFlag = true;
            recipient.playerInstance.OrganizationID.setValue(organization);
            recipient.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(17000, 495, z));
            if (visible) caster.viewMan.addView(id, 1).viewCreated = true;
            return recipient;
        }

        ManageBonusRecord[] Records(WorldClient client, bool remove) => client.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>()
            .Select(message => ManageBonusRecord.TryDecode(message.content.AsSpan(1), out var record) ? record : null)
            .OfType<ManageBonusRecord>().Where(record => record.SourceId == id && record.AddRemoveType == (remove ? 1 : 0))
            .OrderBy(record => record.TargetId).ToArray();
    }

    [Theory]
    [InlineData(361, 30, 58, 8)]
    [InlineData(465, 17, 30, 4)]
    public void FumbleFieldAndUiLagUseTheFullRankedCostAndTheirOwnNativeTimers(ushort id, ushort cost, int reuse, double radius)
    {
        var caster = Client(110);
        var profile = new NativeQa.Modes.HackerFumbleFieldUiLagMode().Plan.Abilities;
        AbilityPassives.SetLoaded(caster, profile.Select(loaded => (loaded, (ushort)50)));
        Assert.Equal(15, AbilityStats.Influence(caster, 117));
        Assert.Equal(cost, AbilityCosts.Effective(caster, id));
        Assert.Equal(radius, AbilityRadius.TargetMetres(caster, id));
        Assert.Equal(2f, AbilityCatalog.Find(id).CastSeconds);
        Assert.True(AbilityCooldowns.TryGet(id, out var cooldown));
        Assert.Equal(new AbilityCooldowns.Cooldown(reuse), cooldown);
    }

    [Theory]
    [InlineData(361)]
    [InlineData(465)]
    public void FumbleFieldAndUiLagRequireSightFromTheirSelectedCentreToEachSecondary(ushort id)
    {
        string root = HeadlessClient.NativeBoundFurnitureFixture.FindRepository();
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        var caster = Client(110);
        var primary = Client(111);
        var blocked = Client(112);
        Store.currentClient = caster;
        caster.playerData.pvpFlag = primary.playerData.pvpFlag = blocked.playerData.pvpFlag = true;
        caster.playerInstance.OrganizationID.setValue((byte)1);
        primary.playerInstance.OrganizationID.setValue((byte)2);
        blocked.playerInstance.OrganizationID.setValue((byte)2);
        caster.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(22125, 1095, 16100));
        primary.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(21925, 1095, 16100));
        blocked.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(21675, 1095, 16100));
        var selected = caster.viewMan.addView(111, 1);
        selected.viewCreated = true;
        caster.viewMan.addView(112, 1).viewCreated = true;
        AbilityPassives.SetLoaded(caster, new[] { (id, (ushort)50) });
        Environment.SetEnvironmentVariable("MXO_FREECAST_VIRAL_QA_ATTACK15", "32766");
        Environment.SetEnvironmentVariable("MXO_FREECAST_VIRAL_QA_DEFENSE15", "0");
        var ability = new AbilityItem();
        ability.setAbilityID(id);

        try
        {
            Assert.True(CombatSight.Between(caster, primary));
            Assert.Equal("sight", AbilityHostile.PlayerAreaReason(caster, blocked, 21925, 1095, 16100,
                AbilityRadius.TargetMetres(caster, id), primary, out _));
            Assert.True(AbilityTargets.Land(caster, ability, selected.ViewID, origin: AbilityCastOrigin.NormalFreeCast));
            Assert.True(TimedEffects.ForPlayer(primary).Effects.HasParent(id));
            Assert.False(TimedEffects.ForPlayer(blocked).Effects.HasParent(id));
        }
        finally
        {
            TimedEffects.Clear(TimedEffects.ForPlayer(primary));
            TimedEffects.Clear(TimedEffects.ForPlayer(blocked));
        }
    }

    [Theory]
    [InlineData(399, 800, TempMods.PowerlessAbility, 0, 6.54, 0, false)]
    [InlineData(400, 500, TempMods.DOTPlaceHolderAbility, 74, 15, 12, false)]
    [InlineData(399, 800, TempMods.PowerlessAbility, 0, 6.54, 0, true)]
    [InlineData(400, 500, TempMods.DOTPlaceHolderAbility, 74, 15, 12, true)]
    [InlineData(353, 500, TempMods.DOTPlaceHolderAbility, 114, 30, 10, false)]
    [InlineData(402, 600, 115, -125, 18, 0, false)]
    public void DespoilerFieldsLandOnTheSelectedEnemyAndExactRadiusEdge(
        ushort id, int edgeRadius, ushort stat, int amount, double duration, int damagePerTick, bool deflectPrimary)
    {
        var caster = Client(110);
        Store.currentClient = caster;
        caster.playerData.pvpFlag = true;
        bool infectAndBottleneck = id == 353 || id == 402;
        byte level = infectAndBottleneck ? (byte)24 : (byte)30;
        caster.playerInstance.Level.setValue(level);
        caster.playerInstance.OrganizationID.setValue((byte)1);
        caster.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(17000, 495, 6400));
        var plan = infectAndBottleneck ? new NativeQa.Modes.HackerInfectAndBottleneckMode().Plan :
            new NativeQa.Modes.HackerDespoilerAreaControlsMode().Plan;
        AbilityPassives.SetLoaded(caster, plan.Abilities.Select(ability => (ability, (ushort)level)));
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
                var landing = subject.Effects.Snapshot();
                Assert.Equal(id == 402 ? 4 : 1, landing.Count);
                var effect = Assert.Single(landing, item => item.Target == stat);
                Assert.Equal(id, effect.Parent);
                Assert.Equal(stat, effect.Target);
                Assert.Equal(amount, effect.Value);
                Assert.Equal(duration, effect.Seconds, 3);
                Assert.Equal(id != 399, ControlActions.CanUseAbilities(subject.Effects));
                if (id == 402) Assert.Equal(-1f, BitConverter.ToSingle(target.playerInstance.MovementScale.getValue()));
                Assert.True(ControlActions.CanMove(subject.Effects));
                Assert.True(ControlActions.CanUseWeapons(subject.Effects));
                Assert.True(ControlActions.CanWithdraw(subject.Effects));
                Assert.Equal(1000, PvpCombat.HealthOf(target));
                return effect;
            }).ToArray();
            Assert.All(excluded, target => Assert.False(TimedEffects.ForPlayer(target).Effects.HasParent(id)));

            int resistedDamage = id switch { 353 => 3, 400 => 2, _ => 0 };
            if (resistedDamage != 0)
                TimedEffects.ForPlayer(edge).Effects.Apply(8,
                    new[] { new TimedEffects.Active { Target = 157, Value = 43, Seconds = 5.1f, Proc = TempMods.Proc.Stat } },
                    effects[0].Start);
            TimedEffects.RunDue(effects.Max(effect => effect.Start) + 2.51);
            Assert.All(affected, target => Assert.Equal(1000 - damagePerTick + (target == edge ? resistedDamage : 0), PvpCombat.HealthOf(target)));
            TimedEffects.RunDue(effects.Max(effect => effect.Start) + 5.01);
            Assert.All(affected, target => Assert.Equal(1000 - 2 * damagePerTick + (target == edge ? 2 * resistedDamage : 0), PvpCombat.HealthOf(target)));
            TimedEffects.RunDue(effects.Max(effect => effect.Expires) + 0.01);
            TimedEffects.RunDue(effects.Max(effect => effect.Expires) + 3);
            int ticks = id == 353 ? 11 : 5;
            Assert.All(affected, target => Assert.Equal(1000 - ticks * damagePerTick + (target == edge ? 2 * resistedDamage : 0), PvpCombat.HealthOf(target)));
            if (id == 402) Assert.All(affected, target => Assert.Equal(0f, BitConverter.ToSingle(target.playerInstance.MovementScale.getValue())));
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
            target.playerInstance.Level.setValue(level);
            target.playerInstance.OrganizationID.setValue(organization);
            target.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(17000, 495, z));
            if (visible) caster.viewMan.addView(id, 1).viewCreated = true;
            return target;
        }
    }

    [Theory]
    [InlineData(19, 5, 19, 0, 0, 0, 7)]
    [InlineData(19, 19, 19, 0, 0, 0, 7)]
    [InlineData(50, 19, 19, 0, 0, 0, 7)]
    [InlineData(50, 19, 50, 0, 0, 0, 9)]
    [InlineData(50, 19, 19, 0, 35, 0, 7)]
    [InlineData(50, 19, 19, 35, 0, 0, 10)]
    [InlineData(50, 19, 19, 35, 0, 75, 8)]
    public void OverhandSmashUsesItsFixedBaseWithRankAndTypedMeleeInputs(
        byte level, ushort abilityRank, ushort karateRank, int meleeBonus, int ballisticBonus, int resistance, ushort landed)
    {
        const ushort overhandSmash = 145;
        var caster = Client(110);
        var target = Client(111);
        Store.currentClient = caster;
        caster.playerData.pvpFlag = target.playerData.pvpFlag = true;
        caster.playerInstance.Level.setValue(level);
        target.playerInstance.Level.setValue(level);
        caster.playerInstance.OrganizationID.setValue((byte)1);
        target.playerInstance.OrganizationID.setValue((byte)2);
        caster.viewMan.addView(111, 1).viewCreated = true;
        AbilityPassives.SetLoaded(caster, new[] { (overhandSmash, abilityRank), (CombatStyles.Karate, karateRank) });
        CombatStyles.Select(caster, CombatStyles.Karate);
        Assert.Equal(CombatStyles.Karate, CombatStyles.Active(caster));
        Assert.Equal(abilityRank, AbilityPassives.Level(caster, overhandSmash));

        var source = TimedEffects.ForPlayer(caster);
        var victim = TimedEffects.ForPlayer(target);
        var smash = new AbilityItem();
        smash.setAbilityID(overhandSmash);
        try
        {
            var now = TimeUtils.GetSimTimeSeconds();
            source.Effects.Apply(185, new[] { Stat(159, meleeBonus), Stat(171, ballisticBonus) }, now);
            victim.Effects.Apply(271, new[] { Stat(180, resistance) }, now);
            int karateInfluence = karateRank == 50 ? 29 : 13;
            Assert.Equal(karateInfluence + meleeBonus, AbilityStats.Influence(caster, 159));
            Assert.Equal(ballisticBonus, AbilityStats.Influence(caster, 171));
            Assert.Equal((ushort)7, AbilityHandler.AbilityDamage(smash, new Random(17), characterLevel: level));

            PvpCombat.LandAbilityOn(caster, smash, target);

            Assert.Equal(1000 - landed, PvpCombat.HealthOf(target));
            var penalties = victim.Effects.Snapshot().Where(effect => effect.Parent == overhandSmash &&
                effect.Proc == TempMods.Proc.Stat).OrderBy(effect => effect.Target).ToArray();
            Assert.Equal(new ushort[] { 522, 1009 }, penalties.Select(effect => effect.Target));
            Assert.All(penalties, effect =>
            {
                Assert.Equal(-10, effect.Value);
                Assert.Equal(10f, effect.Seconds);
            });
        }
        finally
        {
            TimedEffects.Clear(source);
            TimedEffects.Clear(victim);
        }

        static TimedEffects.Active Stat(ushort targetStat, int value) => new()
        {
            Kind = TempMods.KindStat, Target = targetStat, Value = value, Seconds = 10, Proc = TempMods.Proc.Stat
        };
    }

    [Theory]
    [InlineData(605, 17, 19, 33, 0, 19)]
    [InlineData(605, 17, 19, 0, 75, 15)]
    [InlineData(618, 66, 38, 33, 0, 77)]
    [InlineData(618, 66, 38, 0, 75, 63)]
    public void CoderSpecialsUseFixedPhysicalDamageAndOnlyBashAppliesControl(
        ushort id, ushort raw, ushort rank, int ballisticBonus, int resistance, ushort expected)
    {
        var caster = Client(110);
        var target = Client(111);
        caster.playerData.pvpFlag = target.playerData.pvpFlag = true;
        caster.playerInstance.OrganizationID.setValue((byte)1);
        target.playerInstance.OrganizationID.setValue((byte)2);
        caster.viewMan.addView(111, 1).viewCreated = true;
        AbilityPassives.SetLoaded(caster, new[] { (id, rank), (CombatStyles.CoderStyle, (ushort)19), ((ushort)608, (ushort)50) });
        CombatStyles.Select(caster, CombatStyles.CoderStyle);
        var source = TimedEffects.ForPlayer(caster);
        var victim = TimedEffects.ForPlayer(target);
        var ability = new AbilityItem();
        ability.setAbilityID(id);
        target.playerData.combatTactic = CombatTactic.Block;
        target.playerInstance.InnerStrengthAvailable.setValue((ushort)100);
        target.playerInstance.InnerStrengthMax.setValue((ushort)200);

        try
        {
            double now = TimeUtils.GetSimTimeSeconds();
            source.Effects.Apply(185, new[] { Stat(171, ballisticBonus) }, now);
            victim.Effects.Apply(271, new[] { Stat(180, resistance) }, now);
            Assert.Equal(17, AbilityStats.Influence(caster, 159));
            Assert.Equal(raw, AbilityHandler.AbilityDamage(ability, new Random(17), characterLevel: 50));
            CombatHandler.ApplyRoundInnerStrength(target);
            Assert.Equal(110, AbilityHandler.CurrentInnerStrength(target));
            PvpCombat.LandAbilityOn(caster, ability, target);
            Assert.Equal(1000 - expected, PvpCombat.HealthOf(target));

            var states = victim.Effects.Snapshot().Where(effect => effect.Parent == id).OrderBy(effect => effect.Target).ToArray();
            if (id == 605)
            {
                Assert.Empty(states);
                CombatHandler.ApplyRoundInnerStrength(target);
                Assert.Equal(120, AbilityHandler.CurrentInnerStrength(target));
                return;
            }

            Assert.Equal(new[] { TempMods.EnrageAbility, TempMods.StunAbility }, states.Select(effect => effect.Target));
            Assert.Equal(8, states[0].Seconds);
            Assert.Equal(4, states[1].Seconds);
            Assert.False(ControlActions.CanUseAbilities(target));
            Assert.False(ControlActions.CanUseWeapons(victim.Effects));
            Assert.False(ControlActions.CanWithdraw(victim.Effects));
            Assert.False(ControlActions.CanChangeInterlockTarget(victim.Effects));
            Assert.True(victim.Effects.HasControlImmunity(TimeUtils.GetSimTimeSeconds()));
            Assert.Empty(victim.Effects.HealthChanged(1, belowMaximum: true, new Random(1)));
            var immunity = Assert.Single(victim.Effects.Snapshot(), effect => effect.Parent == TempMods.ControlImmunityTimerAbility);
            Assert.Equal(4, immunity.Seconds);

            TimedEffects.RunDue(states[1].Expires + 0.01);
            Assert.True(ControlActions.CanUseAbilities(victim.Effects));
            Assert.True(ControlActions.CanUseWeapons(victim.Effects));
            Assert.False(victim.Effects.HasControlImmunity(states[1].Expires + 0.01));
            Assert.False(ControlActions.CanWithdraw(victim.Effects));
            Assert.False(ControlActions.CanChangeInterlockTarget(victim.Effects));
            Assert.True(ControlActions.CanChangeTactic(victim.Effects));
            CombatHandler.ApplyRoundInnerStrength(target);
            Assert.Equal(110, AbilityHandler.CurrentInnerStrength(target));

            TimedEffects.RunDue(states[0].Expires + 0.01);
            Assert.True(ControlActions.CanWithdraw(victim.Effects));
            Assert.True(ControlActions.CanChangeInterlockTarget(victim.Effects));
            CombatHandler.ApplyRoundInnerStrength(target);
            Assert.Equal(120, AbilityHandler.CurrentInnerStrength(target));
        }
        finally
        {
            TimedEffects.Clear(source);
            TimedEffects.Clear(victim);
        }

        static TimedEffects.Active Stat(ushort targetStat, int value) => new()
        {
            Kind = TempMods.KindStat, Target = targetStat, Value = value, Seconds = 12, Proc = TempMods.Proc.Stat
        };
    }

    [Theory]
    [InlineData(0, 87)]
    [InlineData(75, 71)]
    public void StaticBarrageUsesItsFixedViralBaseWithMatchingResistance(int resistance, int damage)
    {
        const ushort barrage = 598;
        var caster = Client(110);
        var victim = Client(111);
        caster.playerData.pvpFlag = victim.playerData.pvpFlag = true;
        caster.playerInstance.OrganizationID.setValue((byte)1);
        victim.playerInstance.OrganizationID.setValue((byte)2);
        caster.viewMan.addView(111, 1).viewCreated = true;
        AbilityPassives.SetLoaded(caster, new[] { (barrage, (ushort)44), (CombatStyles.CoderStyle, (ushort)50), ((ushort)585, (ushort)50) });
        CombatStyles.Select(caster, CombatStyles.CoderStyle);
        var target = TimedEffects.ForPlayer(victim);
        double now = TimeUtils.GetSimTimeSeconds();
        var ability = new AbilityItem();
        ability.setAbilityID(barrage);

        try
        {
            target.Effects.Apply(8, new[] { new TimedEffects.Active
            {
                Kind = TempMods.KindStat, Target = 157, Proc = TempMods.Proc.Stat, Value = resistance, Seconds = 20
            } }, now);
            Assert.Equal((ushort)64, AbilityHandler.AbilityDamage(ability, new Random(1), characterLevel: 50));
            Assert.Equal(36, AbilityStats.Influence(caster, 172));
            PvpCombat.LandAbilityOn(caster, ability, victim);
            Assert.Equal(1000 - damage, PvpCombat.HealthOf(victim));
        }
        finally { TimedEffects.Clear(target); }
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
    [InlineData(361)]
    [InlineData(465)]
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
    [InlineData(333, 4, TempMods.StunAbility, 0)]
    [InlineData(343, 5, 1009, -21)]
    [InlineData(401, 7, 117, -20)]
    [InlineData(403, 5, 183, -50)]
    [InlineData(360, 5, 157, -100)]
    [InlineData(1081, 0, 183, -12)]
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
        AbilityPassives.SetLoaded(caster, new[] { (id == 1081 ? (ushort)470 : id, (ushort)50) });

        try
        {
            Assert.True(FreeCastViralContest.TryQaDraw(caster, id, out _, out _));
            Assert.True(AbilityBuffs.Apply(caster, 314)); // fixed draw wins against the edge recipient
            Assert.True(AbilityBuffs.Apply(primary, 272)); // +54 Viral Defense makes the same draw deflect at centre
            if (id == 1081) Assert.True(AbilityBuffs.Apply(primary, 46));
            Assert.True(AbilityTargets.Land(caster, ability, view.ViewID, origin: AbilityCastOrigin.NormalFreeCast));
            Assert.False(TimedEffects.ForPlayer(primary).Effects.HasParent(id));
            Assert.Equal(FreeCastViralContest.DeflectFx, NumericalUtils.ByteArrayToUint32(primary.playerInstance.EffectID.getValue(), 1));
            if (id == 1081)
            {
                // A successful single-target Ravager hit has no area fallback after a deflection.
                Assert.False(TimedEffects.ForPlayer(edge).Effects.HasParent(id));
                Assert.True(AbilityBuffs.IsActive(primary, 46));
                AbilityBuffs.Remove(primary, 272);
                Assert.True(AbilityTargets.Land(caster, ability, view.ViewID, origin: AbilityCastOrigin.NormalFreeCast));
                Assert.False(AbilityBuffs.IsActive(primary, 46));
                Assert.False(ControlActions.CanChangeTactic(TimedEffects.ForPlayer(primary).Effects));
                Assert.Equal(-12, AbilityStats.Player(primary, 183));
                Assert.Equal(1000, PvpCombat.HealthOf(primary));
                Assert.False(PvpCombat.Damage(primary, 1));
                Assert.False(ControlActions.CanChangeTactic(TimedEffects.ForPlayer(primary).Effects));
                Assert.All(names.Where(player => player != primary), player =>
                    Assert.False(TimedEffects.ForPlayer(player).Effects.HasParent(id)));
                double started = TimedEffects.ForPlayer(primary).Effects.Snapshot().Single(effect =>
                    effect.Parent == id && effect.Target == TempMods.ConfuseAbility).Start;
                TimedEffects.RunDue(started + 8.01);
                Assert.True(ControlActions.CanChangeTactic(TimedEffects.ForPlayer(primary).Effects));
                Assert.Equal(-12, AbilityStats.Player(primary, 183));
                TimedEffects.RunDue(started + 30.01);
                Assert.Equal(0, AbilityStats.Player(primary, 183));
                AbilityPassives.SetLoaded(caster, Array.Empty<(ushort, ushort)>());
                Assert.False(FreeCastViralContest.TryQaDraw(caster, id, out _, out _));
                return;
            }
            Assert.Equal(value, AbilityStats.Player(edge, witnessStat));
            Assert.Contains(TimedEffects.ForPlayer(edge).Effects.Snapshot(), effect =>
                effect.Parent == id && effect.Target == witnessStat && effect.Value == value);
            Assert.Equal(1000, PvpCombat.HealthOf(edge));
            foreach (var excluded in new[] { caster, outside, ally, unseen })
                Assert.False(TimedEffects.ForPlayer(excluded).Effects.HasParent(id));
            TimedEffects.RunDue(TimeUtils.GetSimTimeSeconds() + 46);
            Assert.Equal(0, AbilityStats.Player(edge, witnessStat));
            Assert.False(TimedEffects.ForPlayer(edge).Effects.HasParent(id));
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
