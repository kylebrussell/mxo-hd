using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
using hds.shared;
namespace hds.Tests;
[Collection("ConcentrationEnvironment")]
public class AbilityRefusalFeedbackTests:IDisposable
{
    readonly WorldClient oldClient=Store.currentClient;readonly ServerConfig oldConfig=Store.config;
    readonly Dictionary<string,WorldClient>? oldClients=WorldServer.Clients;
    readonly FieldInfo loaderField=typeof(DataLoader).GetField("Instance",BindingFlags.Static|BindingFlags.NonPublic)!;
    readonly object? oldLoader;
    readonly string[] keys={"MXO_ABILITY_REFUSAL_QA","MXO_DEV_COMMANDS","MXO_DB_NAME"};readonly string?[] oldEnv;
    public AbilityRefusalFeedbackTests()
    {
        oldLoader=loaderField.GetValue(null);oldEnv=keys.Select(Environment.GetEnvironmentVariable).ToArray();
        foreach(string key in keys)Environment.SetEnvironmentVariable(key,key=="MXO_DB_NAME"?"mxotest_misleading":"1");
        Store.config=new ServerConfig("unused");typeof(ServerConfig).GetField("_dbParams",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(Store.config,new DbParams{DatabaseName="mxotest_refusal"});
        WorldServer.Clients=new();var loader=(DataLoader)RuntimeHelpers.GetUninitializedObject(typeof(DataLoader));var ability=new AbilityItem();ability.setAbilityID(77);ability.setAbilityName("Repair RSI");ability.setCastAnimStart(new byte[4]);loader.AbilityDB=new(){ability};loaderField.SetValue(null,loader);
    }
    public void Dispose(){Store.currentClient=oldClient;Store.config=oldConfig;WorldServer.Clients=oldClients!;loaderField.SetValue(null,oldLoader);for(int i=0;i<keys.Length;i++)Environment.SetEnvironmentVariable(keys[i],oldEnv[i]);}
    static byte[] Hex(string s)=>Convert.FromHexString(s);
    static WorldClient Client(ushort inner=500)
    {
        var c=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));c.Alive=true;
        typeof(WorldClient).GetField("lastUsedTime",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(c,TimeUtils.getUnixTimeUint32());
        typeof(WorldClient).GetField("lastFlushTick",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(c,long.MaxValue);
        typeof(WorldClient).GetField("<TransportFence>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(c,new WorldSendFence());
        c.playerData=new ClientData();c.playerData.setCharID(110);c.playerData.setDistrictId(1);c.playerData.setOnWorld(true);
        c.playerInstance=new PlayerCharacter();c.playerInstance.Level.setValue((byte)50);c.playerInstance.Health.setValue((ushort)1000);c.playerInstance.MaxHealth.setValue((ushort)1000);
        c.playerInstance.InnerStrengthAvailable.setValue(inner);c.playerInstance.InnerStrengthMax.setValue((ushort)1000);c.messageQueue=new();c.viewMan=new();return c;
    }
    static byte[][] Rpcs(WorldClient c)=>c.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>().Select(m=>m.content.Skip(1).ToArray()).ToArray();

    [Fact]
    public void PersonalFirewall2OutsideCombatGateDoesNotStartItsCast()
    {
        var caster = Client();
        Store.currentClient = caster;
        caster.playerData.passives.Loaded[319] = 24;
        caster.playerData.combatStyle = CombatStyles.HackerStyle;
        var combat = CombatManager.GetHandler(caster);
        combat.isCombatRunning = true;
        try
        {
            byte[] request = { 0x3f, 0x01, 0x02, 0 };
            double before = caster.playerData.castEndsAt;
            new AbilityHandler().ProcessAbility(ref request);
            Assert.Equal(AbilityRefusals.Encode(new(319, AbilityRefusals.MustBeOutsideCombat)), Assert.Single(Rpcs(caster)));
            Assert.Equal(before, caster.playerData.castEndsAt);
            Assert.Equal(500, AbilityHandler.CurrentInnerStrength(caster));
            Assert.Empty(caster.playerData.cooldownEndsAt);
        }
        finally
        {
            combat.isCombatRunning = false;
            CombatManager.RemoveClient(caster);
        }
    }

    [Theory]
    [InlineData("unloaded", AbilityRefusals.NotLoaded)]
    [InlineData("style", AbilityRefusals.WrongStyle)]
    [InlineData("far", AbilityRefusals.InvalidTarget)]
    [InlineData("dead", AbilityRefusals.InvalidTarget)]
    [InlineData("opponent", AbilityRefusals.InvalidTarget)]
    [InlineData("blocked", AbilityRefusals.TargetNotInSight)]
    [InlineData("lost", 0u)]
    public void PersonalFirewall2ChecksTheSelectedFriendlyPlayerBeforeCastAndLanding(string caseName, uint denialReason)
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "data", "sector_properties.csv")))
            root = Path.GetDirectoryName(root)!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));

        var caster = Client();
        var friend = Client();
        caster.playerInstance.OrganizationID.setValue((byte)1);
        friend.playerInstance.OrganizationID.setValue((byte)1);
        caster.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(21750, 1095, 16150));
        int friendX = caseName == "far" ? 20550 : caseName == "blocked" ? 22350 : 21550;
        friend.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(friendX, 1095, 16150));
        if (caseName == "dead") friend.playerData.isDead = true;
        if (caseName == "opponent")
        {
            caster.playerData.duelId = 2;
            friend.playerData.duelId = 3;
        }
        friend.playerData.setCharID(111);
        friend.playerData.setEntityId(42);
        WorldServer.Clients.Add("friend", friend);
        var view = caster.viewMan.addView(42, 12);
        view.viewCreated = true;
        view.spawnId = 9;
        Store.currentClient = caster;
        caster.playerData.passives.Loaded[CombatStyles.HackerStyle] = 19;
        caster.playerData.combatStyle = caseName == "style" ? AbilityProgression.SelfDefenseAbility : CombatStyles.HackerStyle;
        if (caseName != "unloaded") caster.playerData.passives.Loaded[319] = 24;

        var ability = new AbilityItem();
        ability.setAbilityID(319);
        ability.setAbilityName("Personal Firewall 2.0");
        ability.setCastAnimStart(new byte[4]);
        DataLoader.getInstance().AbilityDB.Add(ability);
        var request = new byte[41];
        BitConverter.GetBytes((ushort)319).CopyTo(request, 0);
        BitConverter.GetBytes(view.ViewID).CopyTo(request, 2);
        BitConverter.GetBytes((ushort)view.spawnId).CopyTo(request, 4);
        double before = caster.playerData.castEndsAt;
        try
        {
            new AbilityHandler().ProcessAbility(ref request);
            if (denialReason == 0)
            {
                double end = caster.playerData.castEndsAt;
                Assert.True(end > before);
                Assert.Equal(455, AbilityHandler.CurrentInnerStrength(caster));
                friend.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(22350, 1095, 16150));
                DelayedActions.RunDue(end + 1);
                Assert.True(AbilityCooldowns.IsCoolingDown(caster.playerData, 319, TimeUtils.GetSimTimeSeconds()));
            }
            else
            {
                Assert.Equal(AbilityRefusals.Encode(new(319, denialReason)), Assert.Single(Rpcs(caster)));
                Assert.Equal(before, caster.playerData.castEndsAt);
                Assert.Equal(500, AbilityHandler.CurrentInnerStrength(caster));
                Assert.Empty(caster.playerData.cooldownEndsAt);
            }
            Assert.Empty(Rpcs(friend));
            Assert.Equal(0, AbilityStats.Player(friend, 157));
        }
        finally
        {
            CombatManager.RemoveClient(caster);
        }
    }

    [Theory]
    [InlineData(43, true)]
    [InlineData(48, true)]
    [InlineData(69, true)]
    [InlineData(69, false)]
    [InlineData(350, true)]
    [InlineData(351, false)]
    [InlineData(392, true)]
    [InlineData(394, false)]
    public void StarterViralDebuffRequiresTheOriginalHostileSpawnAtLanding(ushort id, bool replaceSpawn)
    {
        var caster = Client();
        var enemy = Client();
        caster.playerData.combatStyle = CombatStyles.HackerStyle;
        caster.playerData.passives.Loaded[id] = 19;
        enemy.playerData.setCharID(111);
        enemy.playerData.setEntityId(42);
        caster.playerInstance.OrganizationID.setValue((byte)1);
        enemy.playerInstance.OrganizationID.setValue((byte)2);
        caster.playerData.pvpFlag = enemy.playerData.pvpFlag = true;
        WorldServer.Clients.Add("enemy", enemy);
        var view = caster.viewMan.addView(42, 12);
        view.viewCreated = true;
        view.spawnId = 9;
        Store.currentClient = caster;

        var ability = new AbilityItem();
        ability.setAbilityID(id);
        ability.setAbilityName("Starter viral debuff");
        ability.setCastAnimStart(new byte[4]);
        DataLoader.getInstance().AbilityDB.Add(ability);
        var request = new byte[41];
        BitConverter.GetBytes(id).CopyTo(request, 0);
        BitConverter.GetBytes(view.ViewID).CopyTo(request, 2);
        BitConverter.GetBytes((ushort)view.spawnId).CopyTo(request, 4);
        new AbilityHandler().ProcessAbility(ref request);
        double end = caster.playerData.castEndsAt;
        Assert.True(end > TimeUtils.GetSimTimeSeconds());
        Assert.Equal(500 - AbilityCosts.Cost(id), AbilityHandler.CurrentInnerStrength(caster));

        if (replaceSpawn) view.spawnId++;
        else enemy.playerInstance.OrganizationID.setValue((byte)1);
        DelayedActions.RunDue(end + 1);

        Assert.Empty(Rpcs(enemy));
        Assert.Equal(1000, PvpCombat.HealthOf(enemy));
        Assert.True(AbilityCooldowns.IsCoolingDown(caster.playerData, id, TimeUtils.GetSimTimeSeconds()));
    }

    [Fact] public void NativeNineteenByteFieldsAreFullWidthAndLossless()
    {
        byte[] raw=Hex("80baefcdab89fe98badcfe1032547678563412");Assert.True(AbilityRefusals.TryDecode(raw,out var r));
        Assert.Equal(0x89abcdefu,r.AbilityID);Assert.Equal(0xfedcba98u,r.ErrorID);Assert.Equal(0x76543210u,r.InnerstrengthDeficit);Assert.Equal(0x12345678u,r.StatesNotMet);Assert.Equal(0xfe,r.ToolsSlot);Assert.Equal(raw,AbilityRefusals.Encode(r));
        foreach(var bad in new[]{raw[..18],raw.Concat(new byte[]{0}).ToArray(),new byte[19]})Assert.False(AbilityRefusals.TryDecode(bad,out _));
    }
    [Fact] public void LiteralMessagesPreserveDefaultsAndKnownRetailReuseRecord()
    {
        Assert.Equal(Hex("80ba660300006b050000000000000000000000"),AbilityRefusals.Encode(new(870,AbilityRefusals.ReuseWait)));
        Assert.Equal(Hex("80ba4d0000006b110000000f00000000000000"),AbilityRefusals.Encode(new(77,AbilityRefusals.InsufficientInnerStrength,15)));
        Assert.Equal(Hex("80ba4d0000006b070000000000000000000000"),AbilityRefusals.Encode(new(77,AbilityRefusals.NotLoaded)));
        Assert.Equal(Hex("80ba840000006b0d0000000000000000000000"),AbilityCooldowns.BuildDenied(132,CombatStyles.DeniedWrongStyle));
        Assert.Equal((byte)6,AbilityCooldowns.DeniedBusy);Assert.Equal((byte)6,AbilityCooldowns.DeniedLegacyAmbiguous);
    }
    [Theory]
    [InlineData(43)]
    [InlineData(48)]
    [InlineData(69)]
    [InlineData(350)]
    [InlineData(351)]
    [InlineData(392)]
    [InlineData(394)]
    public void UnloadedStarterViralDebuffRefusesBeforeCastOrCost(ushort id)
    {
        var caster = Client();
        Store.currentClient = caster;
        double castBefore = caster.playerData.castEndsAt;
        var request = new byte[41];
        BitConverter.GetBytes(id).CopyTo(request, 0);
        BitConverter.GetBytes((ushort)4).CopyTo(request, 2);
        BitConverter.GetBytes((ushort)2).CopyTo(request, 4);

        new AbilityHandler().ProcessAbility(ref request);

        byte[] packet = Assert.Single(Rpcs(caster));
        Assert.True(AbilityRefusals.TryDecode(packet, out var refusal));
        Assert.Equal((uint)id, refusal.AbilityID);
        Assert.Equal(AbilityRefusals.NotLoaded, refusal.ErrorID);
        Assert.Equal(500, AbilityHandler.CurrentInnerStrength(caster));
        Assert.Equal(castBefore, caster.playerData.castEndsAt);
        Assert.Empty(caster.playerData.cooldownEndsAt);
    }

    [Theory]
    [InlineData(350)]
    [InlineData(351)]
    [InlineData(392)]
    [InlineData(394)]
    public void SelectedHackerDebuffsRefuseMissingFriendlyStaleAndFarTargetsBeforeCost(ushort id)
    {
        var caster = Client();
        var target = Client();
        Store.currentClient = caster;
        caster.playerData.passives.Loaded[id] = 26;
        target.playerData.setCharID(111);
        target.playerData.setEntityId(42);
        caster.playerInstance.OrganizationID.setValue((byte)1);
        target.playerInstance.OrganizationID.setValue((byte)1);
        caster.playerData.pvpFlag = target.playerData.pvpFlag = true;
        caster.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(0, 495, 0));
        target.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(2100, 495, 0));
        WorldServer.Clients.Add("witness", target);
        var view = caster.viewMan.addView(42, 12);
        view.viewCreated = true;
        view.spawnId = 9;
        double castBefore = caster.playerData.castEndsAt;

        CheckRefusal(0, 0, AbilityRefusals.InvalidTarget);
        CheckRefusal(view.ViewID, 9, AbilityRefusals.InvalidTarget);
        target.playerInstance.OrganizationID.setValue((byte)2);
        CheckRefusal(view.ViewID, 8, AbilityRefusals.InvalidTarget);
        CheckRefusal(view.ViewID, 9, AbilityRefusals.TargetOutOfRange);

        void CheckRefusal(ushort selected, ushort spawn, uint reason)
        {
            caster.messageQueue.RPCMessagesQueue.Clear();
            var request = new byte[41];
            BitConverter.GetBytes(id).CopyTo(request, 0);
            BitConverter.GetBytes(selected).CopyTo(request, 2);
            BitConverter.GetBytes(spawn).CopyTo(request, 4);
            new AbilityHandler().ProcessAbility(ref request);
            Assert.True(AbilityRefusals.TryDecode(Assert.Single(Rpcs(caster)), out var refusal));
            Assert.Equal((uint)id, refusal.AbilityID);
            Assert.Equal(reason, refusal.ErrorID);
            Assert.Equal(500, AbilityHandler.CurrentInnerStrength(caster));
            Assert.Equal(castBefore, caster.playerData.castEndsAt);
            Assert.Empty(caster.playerData.cooldownEndsAt);
        }
    }

    [Fact] public void PoorFixtureUsesActualMinimumAndCappedRegeneration()
    {
        var c=Client(0);c.playerInstance.InnerStrengthMax.setValue((ushort)0);
        AbilityBuffs.RefreshVitals(c,false);
        Assert.Equal((ushort)1,AbilityHandler.MaxInnerStrength(c));
        Assert.Equal((ushort)1,AbilityHandler.RegenerateInnerStrength(c));
        for(int i=0;i<5;i++)Assert.Null(AbilityHandler.RegenerateInnerStrength(c));
        Assert.Equal((ushort)1,AbilityHandler.CurrentInnerStrength(c));
        Assert.Equal(9,AbilityCosts.Effective(c,77)-AbilityHandler.CurrentInnerStrength(c));
    }
    [Fact] public void ActualInsufficientPredicateReportsExactPositiveSnapshotWithoutStartingAnything()
    {
        var c=Client(4);double beforeCast=c.playerData.castEndsAt;Store.currentClient=c;Assert.Equal((ushort)10,AbilityCosts.Effective(c,77));byte[] request={77,0,2,0};new AbilityHandler().ProcessAbility(ref request);
        Assert.Contains(Rpcs(c),r=>r.SequenceEqual(Hex("80ba4d0000006b110000000600000000000000")));
        Assert.DoesNotContain(Rpcs(c),r=>r.Length>=2&&r[0]==0x80&&(r[1]==0xac||r[1]==0xbd));
        Assert.Equal((ushort)4,AbilityHandler.CurrentInnerStrength(c));Assert.Equal((ushort)1000,PvpCombat.HealthOf(c));Assert.Equal(beforeCast,c.playerData.castEndsAt);Assert.Empty(c.playerData.cooldownEndsAt);
    }
    [Theory]
    [InlineData(19)]
    [InlineData(25)]
    public void UnloadedDrainAndPowerBoostRefuseBeforeChangingCastState(ushort ability)
    {
        var caster = Client();
        double castEndsAt = caster.playerData.castEndsAt;
        Store.currentClient = caster;
        var request = new byte[41];
        BitConverter.GetBytes(ability).CopyTo(request, 0);

        new AbilityHandler().ProcessAbility(ref request);

        byte[] expected = AbilityRefusals.Encode(new(ability, AbilityRefusals.NotLoaded));
        Assert.Equal(expected, Assert.Single(Rpcs(caster)));
        Assert.Equal((ushort)500, AbilityHandler.CurrentInnerStrength(caster));
        Assert.Equal(castEndsAt, caster.playerData.castEndsAt);
        Assert.Empty(caster.playerData.cooldownEndsAt);
    }

    [Theory]
    [InlineData(36, 0)]
    [InlineData(36, 2)]
    [InlineData(514, 0)]
    [InlineData(514, 65535)]
    public void TinkeringWithoutALiveSelectionLeavesResourcesAndCastStateUntouched(ushort id, ushort view)
    {
        var client = Client();
        client.playerData.passives.Loaded[id] = 18;
        double castEndsAt = client.playerData.castEndsAt;
        ushort previousAbility = client.playerData.lastAbilityId;
        Store.currentClient = client;
        var item = new AbilityItem();
        item.setAbilityID(id);
        item.setAbilityName("Tinkering");
        DataLoader.getInstance().AbilityDB.Add(item);
        byte[] request = new byte[41];
        BitConverter.GetBytes(id).CopyTo(request, 0);
        BitConverter.GetBytes(view).CopyTo(request, 2);

        new AbilityHandler().ProcessAbility(ref request);

        Assert.Equal(ServerPackets.BuildSystemChatMessage("Select a living enemy before using this ability.", "BROADCAST"),
            Assert.Single(Rpcs(client)));
        Assert.Equal((ushort)500, AbilityHandler.CurrentInnerStrength(client));
        Assert.Equal(castEndsAt, client.playerData.castEndsAt);
        Assert.Empty(client.playerData.cooldownEndsAt);
        Assert.Equal(previousAbility, client.playerData.lastAbilityId);
    }

    [Fact]
    public void TinkeringSelectionRejectsStaleAndDeadPlayerViews()
    {
        var caster = Client();
        var target = Client();
        target.playerData.setEntityId(42);
        WorldServer.Clients.Add("target", target);
        var view = caster.viewMan.addView(42, 12);
        view.viewCreated = true;
        view.spawnId = 9;

        Assert.True(AbilityTargets.HasLiveAttackSelection(caster, view.ViewID, 9));
        Assert.False(AbilityTargets.HasLiveAttackSelection(caster, view.ViewID, 10));
        target.playerData.duelDeath = true;
        Assert.False(AbilityTargets.HasLiveAttackSelection(caster, view.ViewID, 9));
    }

    [Fact]
    public void TinkeringSelectionNeedsALiveHostileNpcWhenUsingAnNpcView()
    {
        var caster = Client();
        var mob = new Mob();
        mob.setEntityId(42);
        mob.setHealthC(100);
        var view = caster.viewMan.addView(42, 599);
        view.viewCreated = true;
        view.spawnId = 9;
        var original = WorldServer.mobs;
        try
        {
            WorldServer.mobs = new();
            WorldServer.mobs.Add(mob);
            Assert.True(AbilityTargets.HasLiveAttackSelection(caster, view.ViewID, 9));
            mob.isFriendly = true;
            Assert.False(AbilityTargets.HasLiveAttackSelection(caster, view.ViewID, 9));
            mob.isFriendly = false;
            mob.setIsDead(true);
            Assert.False(AbilityTargets.HasLiveAttackSelection(caster, view.ViewID, 9));
        }
        finally
        {
            WorldServer.mobs = original;
        }
    }

    [Theory]
    [InlineData(21, 41)]
    [InlineData(33, 41)]
    [InlineData(36, 41)]
    [InlineData(514, 41)]
    [InlineData(590, 41)]
    [InlineData(591, 41)]
    [InlineData(36, 4)]
    [InlineData(53, 41)]
    [InlineData(57, 41)]
    [InlineData(58, 41)]
    [InlineData(59, 41)]
    [InlineData(60, 41)]
    [InlineData(60, 4)]
    public void DelayedHostileCastCannotLandOnANewerSpawnInTheOriginalView(ushort abilityId, int length)
    {
        var caster = Client();
        if (abilityId is 21 or 33 or 36 or 514)
            caster.playerData.passives.Loaded[abilityId] = 18;
        var target = Client();
        target.playerData.setCharID(111);
        target.playerData.setEntityId(42);
        caster.playerInstance.OrganizationID.setValue((byte)1);
        target.playerInstance.OrganizationID.setValue((byte)2);
        caster.playerData.pvpFlag = target.playerData.pvpFlag = true;
        caster.playerData.combatStyle = abilityId == 53 || abilityId is >= 57 and <= 60
            ? CombatStyles.HackerStyle : CombatStyles.CoderStyle;
        caster.playerData.passives.Loaded[abilityId] = 50;
        WorldServer.Clients.Add("target", target);
        var view = caster.viewMan.addView(42, 12);
        view.viewCreated = true;
        view.spawnId = 9;
        Assert.True(AbilityTargets.HasLiveAttackSelection(caster, view.ViewID, 9, requireOpponent: true));
        Store.currentClient = caster;

        var ability = new AbilityItem();
        ability.setAbilityID(abilityId);
        ability.setAbilityName("Selected hostile cast");
        ability.setCastAnimStart(new byte[4]);
        DataLoader.getInstance().AbilityDB.Add(ability);
        var request = new byte[length];
        BitConverter.GetBytes(abilityId).CopyTo(request, 0);
        BitConverter.GetBytes(view.ViewID).CopyTo(request, 2);
        if (length == 41)
            BitConverter.GetBytes((ushort)view.spawnId).CopyTo(request, 4);

        new AbilityHandler().ProcessAbility(ref request);
        double end = caster.playerData.castEndsAt;
        Assert.True(end > TimeUtils.GetSimTimeSeconds());
        Assert.Equal(500 - AbilityCosts.Cost(abilityId), AbilityHandler.CurrentInnerStrength(caster));
        view.spawnId++;
        Assert.True(AbilityTargets.HasLiveAttackSelection(caster, view.ViewID, (ushort)view.spawnId, requireOpponent: true));

        DelayedActions.RunDue(end + 1);

        Assert.Equal(1000, PvpCombat.HealthOf(target));
        Assert.False(TimedEffects.ForPlayer(target).Effects.Has(TempMods.RootedAbility));
        Assert.True(AbilityCooldowns.IsCoolingDown(caster.playerData, abilityId, TimeUtils.GetSimTimeSeconds()));
        Assert.Empty(Rpcs(target));
    }

    [Theory]
    [InlineData(53)]
    [InlineData(57)]
    [InlineData(58)]
    [InlineData(59)]
    [InlineData(60)]
    public void UnloadedStarterViralDamageCastRefusesBeforeCost(ushort id)
    {
        var caster = Client();
        Store.currentClient = caster;
        double castBefore = caster.playerData.castEndsAt;
        var request = new byte[41];
        BitConverter.GetBytes(id).CopyTo(request, 0);
        BitConverter.GetBytes((ushort)4).CopyTo(request, 2);
        BitConverter.GetBytes((ushort)2).CopyTo(request, 4);

        new AbilityHandler().ProcessAbility(ref request);

        byte[] packet = Assert.Single(Rpcs(caster));
        Assert.True(AbilityRefusals.TryDecode(packet, out var refusal));
        Assert.Equal((uint)id, refusal.AbilityID);
        Assert.Equal(AbilityRefusals.NotLoaded, refusal.ErrorID);
        Assert.Equal(500, AbilityHandler.CurrentInnerStrength(caster));
        Assert.Equal(castBefore, caster.playerData.castEndsAt);
        Assert.Empty(caster.playerData.cooldownEndsAt);
    }

    [Fact] public void ActualTimerPredicateReportsReuseAndPreservesCostAndTimer()
    {
        var c=Client(500);double beforeCast=c.playerData.castEndsAt;Store.currentClient=c;double end=TimeUtils.GetSimTimeSeconds()+100;c.playerData.cooldownEndsAt[77]=end;
        byte[] request={77,0,2,0};new AbilityHandler().ProcessAbility(ref request);
        Assert.Equal(Hex("80ba4d0000006b050000000000000000000000"),Assert.Single(Rpcs(c)));
        Assert.Equal((ushort)500,AbilityHandler.CurrentInnerStrength(c));Assert.Equal(end,c.playerData.cooldownEndsAt[77]);Assert.Equal(beforeCast,c.playerData.castEndsAt);Assert.Equal((ushort)1000,PvpCombat.HealthOf(c));
    }
    [Fact] public void SuccessfulNormalCastStillPaysOnceHealsAndStartsItsExistingTimer()
    {
        var c=Client(500);c.playerInstance.Health.setValue((ushort)100);Store.currentClient=c;
        byte[] request={77,0,2,0};new AbilityHandler().ProcessAbility(ref request);double end=c.playerData.castEndsAt;Assert.True(end>TimeUtils.GetSimTimeSeconds());
        DelayedActions.RunDue(end+1);Assert.True(PvpCombat.HealthOf(c)>100);Assert.Equal((ushort)490,AbilityHandler.CurrentInnerStrength(c));
        Assert.True(AbilityCooldowns.IsCoolingDown(c.playerData,77,TimeUtils.GetSimTimeSeconds()));Assert.DoesNotContain(Rpcs(c),r=>r.Length>=2&&r[0]==0x80&&r[1]==0xba);
    }
    [Theory]
    [InlineData(40, false)]
    [InlineData(86, false)]
    [InlineData(86, true)]
    public void StarterMovementDebuffDoesNotLandOnAReplacementSpawn(ushort id, bool shortRequest)
    {
        var caster = Client();
        var enemy = Client();
        enemy.playerData.setCharID(111);
        enemy.playerData.setEntityId(42);
        caster.playerInstance.OrganizationID.setValue((byte)1);
        enemy.playerInstance.OrganizationID.setValue((byte)2);
        caster.playerData.pvpFlag = enemy.playerData.pvpFlag = true;
        caster.playerData.combatStyle = CombatStyles.HackerStyle;
        caster.playerData.passives.Loaded[id] = 19;
        WorldServer.Clients.Add("enemy", enemy);
        var view = caster.viewMan.addView(42, 12);
        view.viewCreated = true;
        view.spawnId = 9;
        Store.currentClient = caster;

        var ability = new AbilityItem();
        ability.setAbilityID(id);
        ability.setAbilityName("Starter movement debuff");
        ability.setCastAnimStart(new byte[4]);
        DataLoader.getInstance().AbilityDB.Add(ability);
        var request = new byte[shortRequest ? 4 : 41];
        BitConverter.GetBytes(id).CopyTo(request, 0);
        BitConverter.GetBytes(view.ViewID).CopyTo(request, 2);
        if (!shortRequest) BitConverter.GetBytes((ushort)view.spawnId).CopyTo(request, 4);

        new AbilityHandler().ProcessAbility(ref request);
        double end = caster.playerData.castEndsAt;
        Assert.True(end > TimeUtils.GetSimTimeSeconds());
        Assert.Equal(500 - AbilityCosts.Cost(id), AbilityHandler.CurrentInnerStrength(caster));
        view.spawnId++;
        Assert.True(AbilityTargets.HasLiveAttackSelection(caster, view.ViewID, (ushort)view.spawnId, requireOpponent: true));

        DelayedActions.RunDue(end + 1);

        Assert.Empty(Rpcs(enemy));
        Assert.False(TimedEffects.ForPlayer(enemy).Effects.Has(TempMods.RootedAbility));
        Assert.True(AbilityCooldowns.IsCoolingDown(caster.playerData, id, TimeUtils.GetSimTimeSeconds()));
    }

    [Fact] public void FixedProbeGuardsAndNamesCannotParameterizeAuthority()
    {
        bool Allowed(string o="1",string d="1",string db="mxotest_x",uint ch=110,bool alive=true,bool world=true,bool dead=false,bool pending=false,bool shutdown=false,bool la=false)=>AbilityRefusalQa.Allowed(o,d,db,ch,alive,world,dead,pending,shutdown,la);
        Assert.True(Allowed());Assert.False(Allowed(o:"true"));Assert.False(Allowed(d:"0"));foreach(string db in new[]{"reality_hd","mxotest_","mxotest_bad-name"})Assert.False(Allowed(db:db));
        foreach(uint ch in new uint[]{0,2,111})Assert.False(Allowed(ch:ch));Assert.False(Allowed(alive:false));Assert.False(Allowed(world:false));Assert.False(Allowed(dead:true));Assert.False(Allowed(pending:true));Assert.False(Allowed(shutdown:true));Assert.False(Allowed(la:true));
        foreach(string name in new[]{"reuse 999","inner 999","7","77","unloaded extra"})Assert.False(AbilityRefusalQa.TrySample(name,out _));
    }
    [Theory]
    [InlineData(40)]
    [InlineData(66)]
    [InlineData(86)]
    public void UnloadedStarterDebuffRefusesBeforeCostOrCooldown(ushort id)
    {
        var caster = Client();
        Store.currentClient = caster;
        double castBefore = caster.playerData.castEndsAt;
        var request = new byte[41];
        BitConverter.GetBytes(id).CopyTo(request, 0);
        BitConverter.GetBytes((ushort)4).CopyTo(request, 2);
        BitConverter.GetBytes((ushort)2).CopyTo(request, 4);

        new AbilityHandler().ProcessAbility(ref request);

        byte[] packet = Assert.Single(Rpcs(caster));
        Assert.True(AbilityRefusals.TryDecode(packet, out var refusal));
        Assert.Equal((uint)id, refusal.AbilityID);
        Assert.Equal(AbilityRefusals.NotLoaded, refusal.ErrorID);
        Assert.Equal(500, AbilityHandler.CurrentInnerStrength(caster));
        Assert.Equal(castBefore, caster.playerData.castEndsAt);
        Assert.Empty(caster.playerData.cooldownEndsAt);
    }

    [Fact] public void ActualDisplayEnqueueChangesNoResourcesTimersOrLoadedMembershipAndPrunesOnReset()
    {
        var c=Client(123);double beforeCast=c.playerData.castEndsAt;Store.currentClient=c;c.playerData.passives.Loaded[77]=50;c.playerData.cooldownEndsAt[77]=12345;
        c.messageQueue.addRpcMessage(new byte[]{0x81,0x49});var stamp=new AbilityRefusalQa.Stamp(c);
        foreach(string name in new[]{"reuse","inner","unloaded"})Assert.True(AbilityRefusalQa.Enqueue(c,name,stamp));
        Assert.Equal((ushort)123,AbilityHandler.CurrentInnerStrength(c));Assert.Equal((ushort)1000,PvpCombat.HealthOf(c));Assert.Equal((ushort)50,c.playerData.passives.Loaded[77]);Assert.Equal(12345,c.playerData.cooldownEndsAt[77]);Assert.Equal(beforeCast,c.playerData.castEndsAt);
        c.TransportFence.PublishIncoming(true,()=>{});long epoch=c.TransportFence.Capture();c.TransportFence.ApplyReset(epoch,()=>{});c.TransportFence.Resume(epoch);
        Assert.False(AbilityRefusalQa.Enqueue(c,"inner",stamp));Assert.True(c.TransportFence.TryRun(epoch,()=>c.messageQueue.PruneUnassignedWorldRpcs(c.playerData.worldGeneration,epoch)));
        Assert.Equal(new byte[]{0x81,0x49},Assert.Single(Rpcs(c)));
        var fresh=new AbilityRefusalQa.Stamp(c);c.playerData.lifeGeneration++;Assert.False(AbilityRefusalQa.Enqueue(c,"reuse",fresh));
        Store.config.dbParams.DatabaseName="reality_hd";Assert.False(AbilityRefusalQa.Enqueue(c,"reuse",new AbilityRefusalQa.Stamp(c)));
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OverloadChecksTheOriginalSpawnAfterAdmission(bool fullRequest)
    {
        const ushort overload = 66;
        var caster = Client();
        var target = Client();
        caster.playerData.combatStyle = CombatStyles.HackerStyle;
        caster.playerData.passives.Loaded[overload] = 19;
        caster.playerInstance.OrganizationID.setValue((byte)1);
        target.playerInstance.OrganizationID.setValue((byte)2);
        caster.playerData.pvpFlag = target.playerData.pvpFlag = true;
        target.playerData.setCharID(111);
        target.playerData.setEntityId(42);
        caster.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(17000, 495, 8200));
        target.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(17000, 495, 6600));
        WorldServer.Clients.Add("target", target);
        var view = caster.viewMan.addView(42, 12);
        view.viewCreated = true;
        view.spawnId = 9;
        Store.currentClient = caster;

        var ability = new AbilityItem();
        ability.setAbilityID(overload);
        ability.setAbilityName("Overload 1.0");
        ability.setCastAnimStart(new byte[4]);
        DataLoader.getInstance().AbilityDB.Add(ability);
        var request = new byte[fullRequest ? 41 : 4];
        BitConverter.GetBytes(overload).CopyTo(request, 0);
        BitConverter.GetBytes(view.ViewID).CopyTo(request, 2);
        if (fullRequest) BitConverter.GetBytes((ushort)view.spawnId).CopyTo(request, 4);

        new AbilityHandler().ProcessAbility(ref request);
        double end = caster.playerData.castEndsAt;
        Assert.True(end > TimeUtils.GetSimTimeSeconds());
        Assert.Equal(486, AbilityHandler.CurrentInnerStrength(caster));
        view.spawnId++;
        DelayedActions.RunDue(end + 1);

        Assert.Empty(Rpcs(target));
        Assert.True(AbilityCooldowns.IsCoolingDown(caster.playerData, overload, TimeUtils.GetSimTimeSeconds()));
    }

    [Theory]
    [InlineData(343, "empty", AbilityRefusals.InvalidTarget)]
    [InlineData(401, "ally", AbilityRefusals.InvalidTarget)]
    [InlineData(403, "stale", AbilityRefusals.InvalidTarget)]
    [InlineData(343, "far", AbilityRefusals.TargetOutOfRange)]
    [InlineData(401, "far", AbilityRefusals.TargetOutOfRange)]
    [InlineData(403, "far", AbilityRefusals.TargetOutOfRange)]
    [InlineData(343, "blocked", AbilityRefusals.TargetNotInSight)]
    [InlineData(401, "style", AbilityRefusals.WrongStyle)]
    [InlineData(403, "combat", AbilityRefusals.MustBeOutsideCombat)]
    [InlineData(343, "unloaded", AbilityRefusals.NotLoaded)]
    [InlineData(343, "replacement", 0u)]
    [InlineData(401, "retreat", 0u)]
    [InlineData(403, "sight-loss", 0u)]
    public void SelectedFieldUsesTheAdmittedLiveSpawnAndCannotSplashAfterInvalidation(ushort id, string action, uint refusal)
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "data", "sector_properties.csv")))
            root = Path.GetDirectoryName(root)!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        var caster = Client();
        var target = Client();
        var nearby = Client();
        caster.playerData.passives.Loaded[CombatStyles.HackerStyle] = 30;
        if (action != "unloaded") caster.playerData.passives.Loaded[id] = 30;
        if (action != "style") caster.playerData.combatStyle = CombatStyles.HackerStyle;
        caster.playerInstance.OrganizationID.setValue((byte)1);
        double farX = 21750 - 100 * (id == 343 ? 16 : id == 401 ? 13 : 15) - 1;
        caster.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(21750, 1095, 16150));
        var primary = Enter(target, 111, (byte)(action == "ally" ? 1 : 2), action == "far" ? farX : action == "blocked" ? 22350 : 21550);
        Enter(nearby, 112, 2, 21450);
        caster.playerData.pvpFlag = true;
        Store.currentClient = caster;

        var ability = new AbilityItem();
        ability.setAbilityID(id);
        ability.setAbilityName("Selected field");
        ability.setCastAnimStart(new byte[4]);
        DataLoader.getInstance().AbilityDB.Add(ability);
        var fight = CombatManager.GetHandler(caster);
        fight.isCombatRunning = action == "combat";
        var request = new byte[41];
        BitConverter.GetBytes(id).CopyTo(request, 0);
        BitConverter.GetBytes(action == "empty" ? (ushort)0 : primary.ViewID).CopyTo(request, 2);
        BitConverter.GetBytes((ushort)(action == "stale" ? 8 : 9)).CopyTo(request, 4);
        double before = caster.playerData.castEndsAt;
        try
        {
            new AbilityHandler().ProcessAbility(ref request);
            if (refusal == 0)
            {
                double end = caster.playerData.castEndsAt;
                Assert.True(end > TimeUtils.GetSimTimeSeconds());
                Assert.Equal(500 - AbilityCosts.Cost(id), AbilityHandler.CurrentInnerStrength(caster));
                if (action == "replacement") primary.spawnId++;
                if (action == "retreat" || action == "sight-loss")
                    target.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(action == "retreat" ? farX : 22350, 1095, 16150));
                DelayedActions.RunDue(end + 1);
                Assert.True(AbilityCooldowns.IsCoolingDown(caster.playerData, id, TimeUtils.GetSimTimeSeconds()));
            }
            else
            {
                Assert.Equal(AbilityRefusals.Encode(new(id, refusal)), Assert.Single(Rpcs(caster)));
                Assert.Equal(before, caster.playerData.castEndsAt);
                Assert.Equal(500, AbilityHandler.CurrentInnerStrength(caster));
                Assert.Empty(caster.playerData.cooldownEndsAt);
            }
            Assert.Empty(Rpcs(target));
            Assert.Empty(Rpcs(nearby));
            Assert.False(TimedEffects.ForPlayer(target).Effects.HasParent(id));
            Assert.False(TimedEffects.ForPlayer(nearby).Effects.HasParent(id));
        }
        finally
        {
            fight.isCombatRunning = false;
            CombatManager.RemoveClient(caster);
        }

        ClientView Enter(WorldClient player, uint character, byte organization, double x)
        {
            player.playerData.pvpFlag = true;
            player.playerData.setCharID(character);
            player.playerData.setEntityId(character);
            player.playerInstance.OrganizationID.setValue(organization);
            player.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(x, 1095, 16150));
            WorldServer.Clients.Add(character.ToString(), player);
            var view = caster.viewMan.addView(character, 12);
            view.viewCreated = true;
            view.spawnId = 9;
            return view;
        }
    }

    [Fact] public void ModeUsesProvenClosedLoadoutWithoutGmOrRandomControls()
    {
        var plan=new NativeQa.Modes.AbilityRefusalFeedbackMode().Plan;Assert.Equal(new ushort[]{2,17,45,1006,77},plan.Abilities);Assert.True(plan.DevCommands);Assert.False(plan.GmCharacter110);Assert.False(plan.FixedCallbackRolls);
        Assert.Equal("1",plan.EnvironmentOverrides["MXO_ABILITY_REFUSAL_QA"]);Assert.Equal("",NativeQa.NativeQaFixturePlan.EnvironmentFor("")["MXO_ABILITY_REFUSAL_QA"]);
    }

    [Theory]
    [InlineData(46)]
    [InlineData(68)]
    [InlineData(173)]
    public void UnloadedStarterSupportRefusesBeforeItsCastOrCost(ushort id)
    {
        var caster = Client();
        Store.currentClient = caster;
        double castBefore = caster.playerData.castEndsAt;
        var request = new byte[41];
        BitConverter.GetBytes(id).CopyTo(request, 0);
        BitConverter.GetBytes((ushort)4).CopyTo(request, 2);
        BitConverter.GetBytes((ushort)2).CopyTo(request, 4);

        new AbilityHandler().ProcessAbility(ref request);

        byte[] packet = Assert.Single(Rpcs(caster));
        Assert.True(AbilityRefusals.TryDecode(packet, out var refusal));
        Assert.Equal((uint)id, refusal.AbilityID);
        Assert.Equal(AbilityRefusals.NotLoaded, refusal.ErrorID);
        Assert.Equal(500, AbilityHandler.CurrentInnerStrength(caster));
        Assert.Equal(castBefore, caster.playerData.castEndsAt);
        Assert.Empty(caster.playerData.cooldownEndsAt);
    }
}
