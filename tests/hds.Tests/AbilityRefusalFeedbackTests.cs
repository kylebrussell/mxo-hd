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

    [Theory]
    [InlineData(238, "empty", AbilityRefusals.InvalidTarget)]
    [InlineData(238, "far", AbilityRefusals.TargetOutOfRange)]
    [InlineData(238, "changed", 0u)]
    [InlineData(311, "ally", AbilityRefusals.InvalidTarget)]
    [InlineData(311, "far", AbilityRefusals.TargetOutOfRange)]
    [InlineData(311, "sight", AbilityRefusals.TargetNotInSight)]
    [InlineData(312, "spawn", AbilityRefusals.InvalidTarget)]
    [InlineData(312, "unloaded", AbilityRefusals.NotLoaded)]
    [InlineData(312, "retreat", 0u)]
    [InlineData(313, "style", AbilityRefusals.WrongStyle)]
    [InlineData(313, "combat", AbilityRefusals.MustBeOutsideCombat)]
    public void DoctorAndArbalestViralCastsGuardAdmissionAndTheAdmittedSpawn(ushort id, string state, uint refusal)
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "data", "sector_properties.csv"))) root = Path.GetDirectoryName(root)!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));

        var caster = Client();
        var target = Client();
        caster.playerData.setEntityId(110);
        target.playerData.setCharID(111);
        target.playerData.setEntityId(111);
        caster.playerData.pvpFlag = target.playerData.pvpFlag = true;
        caster.playerInstance.OrganizationID.setValue((byte)1);
        target.playerInstance.OrganizationID.setValue((byte)(state == "ally" ? 1 : 2));
        caster.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(21750, 1095, 16150));
        double outside = 21749 - AbilityCatalog.Find(id).Number("Ability/SelectionRange") * 100;
        SetTarget(state == "far" ? outside : state == "sight" ? 22350 : 21550);
        WorldServer.Clients.Add("caster", caster);
        WorldServer.Clients.Add("target", target);
        var view = caster.viewMan.addView(111, 12);
        view.viewCreated = true;
        view.spawnId = 9;
        caster.playerData.passives.Loaded[CombatStyles.HackerStyle] = 40;
        if (state != "style") caster.playerData.combatStyle = CombatStyles.HackerStyle;
        if (state != "unloaded") caster.playerData.passives.Loaded[id] = 40;
        Store.currentClient = caster;

        var ability = new AbilityItem();
        ability.setAbilityID(id);
        ability.setAbilityName("Doctor/Arbalest viral cast");
        ability.setCastAnimStart(new byte[4]);
        DataLoader.getInstance().AbilityDB.Add(ability);
        var fight = CombatManager.GetHandler(caster);
        fight.isCombatRunning = state == "combat";
        // Internal handler body for admission regression. The native bootstrap keeps captured requests only.
        var body = new byte[41];
        BitConverter.GetBytes(id).CopyTo(body, 0);
        BitConverter.GetBytes(state == "empty" ? (ushort)0 : view.ViewID).CopyTo(body, 2);
        BitConverter.GetBytes((ushort)(state == "spawn" ? 8 : 9)).CopyTo(body, 4);
        double before = caster.playerData.castEndsAt;
        try
        {
            new AbilityHandler().ProcessAbility(ref body);
            if (refusal != 0)
            {
                Assert.Equal(AbilityRefusals.Encode(new(id, refusal)), Assert.Single(Rpcs(caster)));
                Assert.Equal(before, caster.playerData.castEndsAt);
                Assert.Equal(500, AbilityHandler.CurrentInnerStrength(caster));
                Assert.Empty(caster.playerData.cooldownEndsAt);
            }
            else
            {
                double end = caster.playerData.castEndsAt;
                Assert.True(end > TimeUtils.GetSimTimeSeconds());
                Assert.Equal(500 - AbilityCosts.Cost(id), AbilityHandler.CurrentInnerStrength(caster));
                if (state == "changed") view.spawnId++;
                else SetTarget(outside);
                DelayedActions.RunDue(end + 1);
                Assert.True(AbilityCooldowns.IsCoolingDown(caster.playerData, id, TimeUtils.GetSimTimeSeconds()));
            }
            Assert.Empty(Rpcs(target));
            Assert.Equal(1000, PvpCombat.HealthOf(target));
        }
        finally
        {
            fight.isCombatRunning = false;
            CombatManager.RemoveClient(caster);
        }

        void SetTarget(double x) => target.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(x, 1095, 16150));
    }

    [Theory]
    [InlineData("unloaded", AbilityRefusals.NotLoaded)]
    [InlineData("wrong weapon", AbilityRefusals.RequiredItemCategory)]
    [InlineData("exposed", AbilityRefusals.CasterStateRequired)]
    [InlineData("sneak", AbilityRefusals.CasterStateRequired)]
    [InlineData("combat", AbilityRefusals.MustBeOutsideCombat)]
    [InlineData("ally", AbilityRefusals.InvalidTarget)]
    [InlineData("stale", AbilityRefusals.InvalidTarget)]
    [InlineData("far", AbilityRefusals.TargetOutOfRange)]
    [InlineData("blocked", AbilityRefusals.TargetNotInSight)]
    [InlineData("changed", 0u)]
    [InlineData("left sight", 0u)]
    [InlineData("left range", 0u)]
    [InlineData("weapon swapped", 0u)]
    [InlineData("unloaded in cast", 0u)]
    [InlineData("style changed", 0u)]
    [InlineData("combat started", 0u)]
    [InlineData("neutral hit", 0u)]
    [InlineData("resisted hit", 0u)]
    public void SniperShotBindsItsConcealedRifleCastToTheOriginalLiveTarget(string state, uint denial)
    {
        const ushort shot = CombatSpecials.SniperShot;
        const int near = 21550, blocked = 22350, far = 18550;
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "data", "sector_properties.csv"))) root = Path.GetDirectoryName(root)!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        var caster = Client();
        var enemy = Client();
        caster.playerData.setEntityId(110);
        enemy.playerData.setCharID(111);
        enemy.playerData.setEntityId(111);
        caster.playerData.pvpFlag = enemy.playerData.pvpFlag = true;
        caster.playerInstance.OrganizationID.setValue((byte)1);
        enemy.playerInstance.OrganizationID.setValue((byte)(state == "ally" ? 1 : 2));
        caster.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(21750, 1095, 16150));
        PlaceEnemy(state == "far" ? far : state == "blocked" ? blocked : near);
        WorldServer.Clients.Add("caster", caster);
        WorldServer.Clients.Add("enemy", enemy);
        var view = caster.viewMan.addView(111, 12);
        view.viewCreated = true;
        view.spawnId = 9;
        const ushort level = 26;
        caster.playerInstance.Level.setValue((byte)level);
        enemy.playerInstance.Level.setValue((byte)level);
        caster.playerInstance.EquippedItemID.setValue(state == "wrong weapon" ? CombatFixtureWeapons.Handgun : CombatFixtureWeapons.Rifle);
        var profile = new ushort[] { 2, 17, CombatStyles.Rifles, 503, CombatSpecials.ConcealedShot, shot }
            .SelectMany(entry => AbilityPrerequisites.Current.Closure(entry).Keys.Append(entry)).Distinct().ToArray();
        Assert.Equal(46, profile.Sum(AbilityMemory.Cost));
        AbilityPassives.SetLoaded(caster, profile.Where(entry => state != "unloaded" || entry != shot).Select(entry => (entry, level)));
        CombatStyles.Select(caster, CombatStyles.Rifles);
        Assert.Equal(12, AbilityStats.Influence(caster, 171));
        ushort stealth = state == "sneak" ? Stealth.SneakGrantAbility : Stealth.ConcealmentGrantAbility;
        if (state != "exposed") Assert.NotNull(Stealth.Begin(caster, Stealth.Defs[stealth], 26));
        if (state == "resisted hit")
            TimedEffects.ForPlayer(enemy).Effects.Apply(1061, new[] { new TimedEffects.Active
            {
                Target = 181, Value = 20, Seconds = 600
            } }, TimeUtils.GetSimTimeSeconds());
        caster.messageQueue.RPCMessagesQueue.Clear();
        enemy.messageQueue.RPCMessagesQueue.Clear();
        Store.currentClient = caster;

        var ability = new AbilityItem();
        ability.setAbilityID(shot);
        ability.setAbilityName("Sniper Shot");
        ability.setCastAnimStart(new byte[4]);
        ability.setValueFrom(4);
        DataLoader.getInstance().AbilityDB.Add(ability);
        var fight = CombatManager.GetHandler(caster);
        fight.isCombatRunning = state == "combat";
        // Handler regression body. NativeQa only sends live stock captures.
        var body = new byte[41];
        BitConverter.GetBytes(shot).CopyTo(body, 0);
        BitConverter.GetBytes(view.ViewID).CopyTo(body, 2);
        BitConverter.GetBytes((ushort)(state == "stale" ? 8 : 9)).CopyTo(body, 4);
        try
        {
            double before = TimeUtils.GetSimTimeSeconds();
            new AbilityHandler().ProcessAbility(ref body);
            if (denial != 0)
            {
                uint flags = denial == AbilityRefusals.CasterStateRequired ? Stealth.ConcealmentFlag : 0;
                Assert.Contains(Rpcs(caster), rpc => rpc.AsSpan().SequenceEqual(AbilityRefusals.Encode(new(shot, denial, states: flags))));
                Assert.Equal(state != "exposed", Stealth.IsOn(caster, stealth));
                Assert.Equal(500, AbilityHandler.CurrentInnerStrength(caster));
                Assert.False(AbilityCooldowns.IsCasting(caster.playerData, before));
                Assert.Empty(caster.playerData.cooldownEndsAt);
                Assert.Equal(1000, PvpCombat.HealthOf(enemy));
                return;
            }

            Assert.InRange(caster.playerData.castEndsAt - before, 6, 6.5);
            Assert.False(Stealth.IsOn(caster, stealth));
            Assert.Equal(440, AbilityHandler.CurrentInnerStrength(caster));
            Assert.Equal(1000, PvpCombat.HealthOf(enemy));
            if (state == "changed") view.spawnId++;
            else if (state == "left sight") PlaceEnemy(blocked);
            else if (state == "left range") PlaceEnemy(far);
            else if (state == "weapon swapped") caster.playerInstance.EquippedItemID.setValue(CombatFixtureWeapons.Handgun);
            else if (state == "unloaded in cast") caster.playerData.passives.Loaded.Remove(shot);
            else if (state == "style changed") caster.playerData.combatStyle = CombatStyles.SelfDefense;
            else if (state == "combat started") fight.isCombatRunning = true;
            DelayedActions.RunDue(caster.playerData.castEndsAt + 1);
            Assert.True(AbilityCooldowns.IsCoolingDown(caster.playerData, shot, TimeUtils.GetSimTimeSeconds()));

            int expectedDamage = state == "neutral hit" ? 52 : state == "resisted hit" ? 47 : 0;
            Assert.Equal(1000 - expectedDamage, PvpCombat.HealthOf(enemy));
        }
        finally
        {
            fight.isCombatRunning = false;
            CombatManager.RemoveClient(caster);
            Stealth.Cleared(caster);
            TimedEffects.Clear(TimedEffects.ForPlayer(enemy));
        }

        void PlaceEnemy(int x) => enemy.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(x, 1095, 16150));
    }
    public void Dispose(){Store.currentClient=oldClient;Store.config=oldConfig;WorldServer.Clients=oldClients!;loaderField.SetValue(null,oldLoader);for(int i=0;i<keys.Length;i++)Environment.SetEnvironmentVariable(keys[i],oldEnv[i]);}

    [Fact]
    public void ArchivedDoctorGrantIsRefusedBeforeCostAndCannotBeUsedThroughAStaleShortcut()
    {
        const ushort id = AbilityBuffs.DoctorUpgrade;
        var caster = Client();
        Store.currentClient = caster;
        var ability = new AbilityItem();
        ability.setAbilityID(id);
        ability.setAbilityName("Doctor Upgrade");
        ability.setCastAnimStart(new byte[4]);
        DataLoader.getInstance().AbilityDB.Add(ability);
        // The native mode captures the actual complete 1097 request. This is the internal handler input.
        var request = new byte[41];
        BitConverter.GetBytes(id).CopyTo(request, 0);
        double before = caster.playerData.castEndsAt;

        new AbilityHandler().ProcessAbility(ref request);

        Assert.Equal(AbilityRefusals.Encode(new(id, AbilityRefusals.NotLoaded)), Assert.Single(Rpcs(caster)));
        Assert.Equal(500, AbilityHandler.CurrentInnerStrength(caster));
        Assert.Equal(before, caster.playerData.castEndsAt);
        Assert.Empty(caster.playerData.cooldownEndsAt);
        Assert.False(AbilityBuffs.IsActive(caster, id));

        AbilityPassives.SetLoaded(caster, new (ushort, ushort)[] { (236, 30) });
        caster.playerData.lastAbilityTime = TimeUtils.GetSimTimeSeconds() - AbilityHandler.DuplicateAbilityWindowSeconds - 1;
        new AbilityHandler().ProcessAbility(ref request);
        Assert.True(AbilityBuffs.IsActive(caster, id));
        Assert.Equal(480, AbilityHandler.CurrentInnerStrength(caster));
        Assert.True(caster.playerData.cooldownEndsAt.TryGetValue(id, out double readyAt));
        Assert.InRange(readyAt - TimeUtils.GetSimTimeSeconds(), 29, 30);

        AbilityPassives.SetLoaded(caster, Array.Empty<(ushort, ushort)>());
        caster.playerData.lastAbilityTime = TimeUtils.GetSimTimeSeconds() - AbilityHandler.DuplicateAbilityWindowSeconds - 1;
        new AbilityHandler().ProcessAbility(ref request);
        Assert.False(AbilityBuffs.IsActive(caster, id));
        Assert.Equal(480, AbilityHandler.CurrentInnerStrength(caster));
        Assert.Equal(readyAt, caster.playerData.cooldownEndsAt[id]);
    }

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
    [InlineData(UtilityAbilities.RecallTeamMember, AbilityRefusals.NotLoaded)]
    [InlineData(UtilityAbilities.RecallTeamMember, AbilityRefusals.WrongStyle)]
    [InlineData(UtilityAbilities.RecallTeamMember, AbilityRefusals.MustBeOutsideCombat)]
    public void RecallCasterFailuresDoNotReserveACastOrSpendInnerStrength(ushort id, uint reason)
    {
        var client = Client();
        Store.currentClient = client;
        var item = new AbilityItem();
        item.setAbilityID(id);
        DataLoader.getInstance().AbilityDB.Add(item);
        if (reason != AbilityRefusals.NotLoaded) client.playerData.passives.Loaded[id] = 50;
        client.playerData.passives.Loaded[CombatStyles.HackerStyle] = 50;
        client.playerData.combatStyle = reason == AbilityRefusals.WrongStyle ? CombatStyles.SelfDefense : CombatStyles.HackerStyle;
        var combat = CombatManager.GetHandler(client);
        combat.isCombatRunning = reason == AbilityRefusals.MustBeOutsideCombat;
        byte[] request = new byte[41];
        BitConverter.GetBytes(id).CopyTo(request, 0);
        try
        {
            double castEndsAt = client.playerData.castEndsAt;
            new AbilityHandler().ProcessAbility(ref request);
            Assert.Equal(AbilityRefusals.Encode(new AbilityRefusals.Record(id, reason)), Assert.Single(Rpcs(client)));
            Assert.Equal((ushort)500, AbilityHandler.CurrentInnerStrength(client));
            Assert.Equal(castEndsAt, client.playerData.castEndsAt);
            Assert.Empty(client.playerData.cooldownEndsAt);
        }
        finally
        {
            combat.isCombatRunning = false;
            CombatManager.RemoveClient(client);
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
    [InlineData(395, true)]
    [InlineData(396, true)]
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
    [InlineData(588)]
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
    [InlineData(247, 20, 25, 30, 2)]
    [InlineData(248, 25, 15, 35, 4)]
    public void DualSmgConeLandsItsFixedBallisticComponentOnlyOnEligibleRecipients(
        ushort id, byte level, ushort storedDamage, int cost, float castSeconds)
    {
        LoadDualSmgConeNav();
        var caster = DualSmgTestCaster(id, level);
        caster.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(17000, 495, 6400));
        var primary = NewConePlayer(caster, 111, level, 17000, 495, 6600);
        var inside = NewConePlayer(caster, 112, level, 17165, 495, 6800);
        var edge = NewConePlayer(caster, 113, level, 17000, 495, 7200);
        var wide = NewConePlayer(caster, 114, level, 17166, 495, 6800);
        var far = NewConePlayer(caster, 115, level, 17000, 495, 7201);
        var ally = NewConePlayer(caster, 116, level, 16900, 495, 6800, friendly: true);
        var admitted = new[] { primary, inside, edge };
        var excluded = new[] { wide, far, ally };
        Assert.All(admitted.Concat(excluded), actor => Assert.True(CombatSight.Between(caster, actor)));

        ushort damage = AbilityStats.ScaleAbilityDamage(caster, id, storedDamage);
        var request = DualSmgRequest(id, caster.viewMan.FindViewForEntity(primary.playerData.getEntityId()));
        var previousMobs = WorldServer.mobs;
        WorldServer.mobs = new System.Collections.ArrayList();
        try
        {
            double startedAt = TimeUtils.GetSimTimeSeconds();
            new AbilityHandler().ProcessAbility(ref request);
            double end = caster.playerData.castEndsAt;
            Assert.InRange(end - startedAt, castSeconds - .01, castSeconds + 1);
            Assert.Equal(500 - cost, AbilityHandler.CurrentInnerStrength(caster));
            Assert.All(admitted, actor => Assert.Equal(1000, PvpCombat.HealthOf(actor)));
            AbilityCastInterrupts.FinishDue(caster, end + .01);

            Assert.All(admitted, actor => Assert.Equal(1000 - damage, PvpCombat.HealthOf(actor)));
            Assert.All(excluded, actor => Assert.Equal(1000, PvpCombat.HealthOf(actor)));
            Assert.False(CombatManager.GetHandler(caster).HasActiveInterlock);
            if (id == 248)
            {
                Assert.All(admitted, actor => Assert.True(TimedEffects.ForPlayer(actor).Effects.Has(TempMods.RootedAbility)));
                Assert.All(excluded, actor => Assert.False(TimedEffects.ForPlayer(actor).Effects.Has(TempMods.RootedAbility)));
            }
        }
        finally
        {
            foreach (var actor in admitted.Concat(excluded)) TimedEffects.Clear(TimedEffects.ForPlayer(actor));
            CombatManager.RemoveClient(caster);
            WorldServer.mobs = previousMobs;
        }
    }

    [Theory]
    [InlineData(247, "single SMG", AbilityRefusals.RequiredItemCategory)]
    [InlineData(248, "unloaded", AbilityRefusals.NotLoaded)]
    [InlineData(247, "combat", AbilityRefusals.MustBeOutsideCombat)]
    [InlineData(248, "ally", AbilityRefusals.InvalidTarget)]
    [InlineData(247, "stale", AbilityRefusals.InvalidTarget)]
    [InlineData(248, "far", AbilityRefusals.TargetOutOfRange)]
    [InlineData(247, "blocked", AbilityRefusals.TargetNotInSight)]
    [InlineData(248, "retreat", 0u)]
    public void DualSmgConeRefusesBeforeCostOrPreservesTheAdmittedAim(ushort id, string scenario, uint refusal)
    {
        LoadDualSmgConeNav();
        var caster = DualSmgTestCaster(id, 25, loaded: scenario != "unloaded", dual: scenario != "single SMG");
        caster.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(21750, 1095, 16150));
        var victim = NewConePlayer(caster, 111, 25, scenario == "far" ? 20949 : scenario == "blocked" ? 22350 : 21550, 1095, 16150,
            friendly: scenario == "ally");
        var bystander = NewConePlayer(caster, 112, 25, 21500, 1095, 16150);
        var view = caster.viewMan.FindViewForEntity(victim.playerData.getEntityId());
        var request = DualSmgRequest(id, view, scenario == "stale");
        var handler = CombatManager.GetHandler(caster);
        handler.isCombatRunning = scenario == "combat";
        try
        {
            new AbilityHandler().ProcessAbility(ref request);
            if (refusal != 0)
            {
                Assert.Equal(AbilityRefusals.Encode(new(id, refusal)), Assert.Single(Rpcs(caster)));
                Assert.Equal(500, AbilityHandler.CurrentInnerStrength(caster));
                Assert.Empty(caster.playerData.cooldownEndsAt);
            }
            else
            {
                Assert.Equal(465, AbilityHandler.CurrentInnerStrength(caster));
                victim.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(20949, 1095, 16150));
                AbilityCastInterrupts.FinishDue(caster, caster.playerData.castEndsAt + .01);
            }
            Assert.Equal(1000, PvpCombat.HealthOf(victim));
            Assert.Equal(1000, PvpCombat.HealthOf(bystander));
        }
        finally
        {
            handler.isCombatRunning = false;
            CombatManager.RemoveClient(caster);
            TimedEffects.Clear(TimedEffects.ForPlayer(victim));
            TimedEffects.Clear(TimedEffects.ForPlayer(bystander));
        }
    }

    private static WorldClient DualSmgTestCaster(ushort id, byte level, bool loaded = true, bool dual = true)
    {
        var caster = Client();
        caster.playerData.setEntityId(110);
        caster.playerData.pvpFlag = true;
        caster.playerInstance.Level.setValue(level);
        caster.playerInstance.OrganizationID.setValue((byte)1);
        caster.playerInstance.EquippedItemID.setValue(dual ? 2503u : 698u);
        var profile = new ushort[] { 2, 17, CombatStyles.SubmachineGuns, id }
            .SelectMany(ability => AbilityPrerequisites.Current.Closure(ability).Keys.Append(ability)).Distinct().ToArray();
        Assert.True(profile.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(level));
        AbilityPassives.SetLoaded(caster, profile.Where(ability => loaded || ability != id).Select(ability => (ability, (ushort)level)));
        CombatStyles.Select(caster, CombatStyles.SubmachineGuns);
        caster.messageQueue.RPCMessagesQueue.Clear();
        caster.messageQueue.ObjectMessagesQueue.Clear();
        WorldServer.Clients.Add("caster", caster);
        Store.currentClient = caster;
        var item = new AbilityItem();
        item.setAbilityID(id);
        item.setAbilityName(AbilityCatalog.Find(id).Name);
        item.setValueFrom(id == 247 ? (short)25 : (short)0);
        item.setCastAnimStart(new byte[4]);
        DataLoader.getInstance().AbilityDB.Add(item);
        return caster;
    }

    private static WorldClient NewConePlayer(WorldClient caster, uint id, byte level, double x, double y, double z, bool friendly = false)
    {
        var actor = Client();
        actor.playerData.setCharID(id);
        actor.playerData.setEntityId(id);
        actor.playerData.pvpFlag = true;
        actor.playerInstance.Level.setValue(level);
        actor.playerInstance.OrganizationID.setValue(friendly ? (byte)1 : (byte)2);
        actor.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(x, y, z));
        WorldServer.Clients.Add(id.ToString(), actor);
        var view = caster.viewMan.addView(id, 12);
        view.viewCreated = true;
        view.spawnId = 9;
        return actor;
    }

    private static byte[] DualSmgRequest(ushort id, ClientView view, bool stale = false)
    {
        // Exercise the ordinary handler in process without inventing a captured stock request.
        var request = new byte[41];
        BitConverter.GetBytes(id).CopyTo(request, 0);
        BitConverter.GetBytes(DuelPackets.Handle(view.ViewID, stale ? 8u : view.spawnId)).CopyTo(request, 2);
        return request;
    }

    private static void LoadDualSmgConeNav()
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "data", "sector_properties.csv"))) root = Path.GetDirectoryName(root)!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
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
    [InlineData("unloaded", AbilityRefusals.NotLoaded)]
    [InlineData("weapon", AbilityRefusals.RequiredItemCategory)]
    [InlineData("active combat", AbilityRefusals.MustBeOutsideCombat)]
    [InlineData("empty", AbilityRefusals.InvalidTarget)]
    [InlineData("ally", AbilityRefusals.InvalidTarget)]
    [InlineData("far", AbilityRefusals.TargetOutOfRange)]
    [InlineData("blocked", AbilityRefusals.TargetNotInSight)]
    [InlineData("combat during cast", 0u)]
    [InlineData("out of range during cast", 0u)]
    [InlineData("rifle", 0u)]
    [InlineData("shotgun", 0u)]
    public void WoundingShotUsesAnOutsideCombatRifleCategoryAndItsOriginalLiveTarget(string scenario, uint reason)
    {
        const ushort id = CombatSpecials.WoundingShot;
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "data", "sector_properties.csv"))) root = Path.GetDirectoryName(root)!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));

        var caster = Client();
        var victim = Client();
        victim.playerData.setCharID(111);
        victim.playerData.setEntityId(111);
        caster.playerInstance.Level.setValue((byte)20);
        victim.playerInstance.Level.setValue((byte)20);
        caster.playerInstance.OrganizationID.setValue((byte)1);
        victim.playerInstance.OrganizationID.setValue((byte)(scenario == "ally" ? 1 : 2));
        caster.playerData.pvpFlag = victim.playerData.pvpFlag = true;
        caster.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(21750, 1095, 16150));
        MoveTarget(scenario == "far" ? 19749 : scenario == "blocked" ? 22350 : 21550);
        caster.playerInstance.EquippedItemID.setValue(scenario == "weapon" ? 2565u : scenario == "shotgun" ? 696u : 692u);
        WorldServer.Clients.Add("caster", caster);
        WorldServer.Clients.Add("victim", victim);

        var fullProfile = new ushort[] { 2, 17, CombatStyles.Rifles, id }
            .SelectMany(ability => AbilityPrerequisites.Current.Closure(ability).Keys.Append(ability)).Distinct().ToArray();
        Assert.True(fullProfile.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(20));
        AbilityPassives.SetLoaded(caster, fullProfile.Where(ability => scenario != "unloaded" || ability != id)
            .Select(ability => (ability, (ushort)20)));
        CombatStyles.Select(caster, CombatStyles.Rifles);
        caster.messageQueue.RPCMessagesQueue.Clear();
        caster.messageQueue.ObjectMessagesQueue.Clear();

        var view = caster.viewMan.addView(111, 12);
        view.viewCreated = true;
        view.spawnId = 9;
        var item = new AbilityItem();
        item.setAbilityID(id);
        item.setAbilityName("Wounding Shot");
        item.setValueFrom(6);
        item.setCastAnimStart(new byte[4]);
        DataLoader.getInstance().AbilityDB.Add(item);
        Store.currentClient = caster;
        var combat = CombatManager.GetHandler(caster);
        combat.isCombatRunning = scenario == "active combat";
        // In-process handler input. The stock replays keep the original 254/255 frames.
        var request = new byte[41];
        BitConverter.GetBytes(id).CopyTo(request, 0);
        BitConverter.GetBytes(scenario == "empty" ? 0u : DuelPackets.Handle(view.ViewID, view.spawnId)).CopyTo(request, 2);
        var effects = TimedEffects.ForPlayer(victim);
        try
        {
            new AbilityHandler().ProcessAbility(ref request);
            if (reason != 0)
            {
                Assert.Equal(AbilityRefusals.Encode(new(id, reason)), Assert.Single(Rpcs(caster)));
                Assert.Equal(500, AbilityHandler.CurrentInnerStrength(caster));
                Assert.Empty(caster.playerData.cooldownEndsAt);
                Assert.Equal(1000, PvpCombat.HealthOf(victim));
                return;
            }

            double castEnd = caster.playerData.castEndsAt;
            Assert.True(castEnd > TimeUtils.GetSimTimeSeconds());
            Assert.Equal(480, AbilityHandler.CurrentInnerStrength(caster));
            Assert.Equal(1000, PvpCombat.HealthOf(victim));
            if (scenario == "combat during cast") combat.isCombatRunning = true;
            if (scenario == "out of range during cast") MoveTarget(19749);
            AbilityCastInterrupts.FinishDue(caster, castEnd + .01);
            Assert.True(AbilityCooldowns.IsCoolingDown(caster.playerData, id, TimeUtils.GetSimTimeSeconds()));
            if (scenario is "rifle" or "shotgun")
            {
                Assert.Equal(994, PvpCombat.HealthOf(victim));
                var dot = Assert.Single(effects.Effects.Snapshot(), mod => mod.Parent == id && mod.Proc == TempMods.Proc.Dot);
                Assert.Equal((14, 10f, 2.5), (dot.Value, dot.Seconds, dot.Interval));
                Assert.False(combat.HasActiveInterlock);
            }
            else
            {
                Assert.Equal(1000, PvpCombat.HealthOf(victim));
                Assert.False(effects.Effects.HasParent(id));
            }
        }
        finally
        {
            combat.isCombatRunning = false;
            CombatManager.RemoveClient(caster);
            TimedEffects.Clear(effects);
        }

        void MoveTarget(double x) => victim.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(x, 1095, 16150));
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
    [InlineData(588, 41)]
    [InlineData(588, 4)]
    [InlineData(253, 41)]
    [InlineData(253, 4)]
    [InlineData(254, 41)]
    [InlineData(254, 4)]
    [InlineData(255, 41)]
    [InlineData(255, 4)]
    [InlineData(404, 41)]
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
        caster.playerData.combatStyle = abilityId is 53 or 404 or (>= 57 and <= 60)
            ? CombatStyles.HackerStyle : CombatStyles.CoderStyle;
        if (abilityId == 404) caster.playerData.passives.Loaded[CombatStyles.HackerStyle] = 30;
        if (CombatSpecials.IsRifleOpener(abilityId))
        {
            caster.playerData.combatStyle = CombatStyles.Rifles;
            caster.playerData.passives.Loaded[CombatStyles.Rifles] = 50;
            caster.playerInstance.EquippedItemID.setValue(692u);
        }
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
        Assert.False(TimedEffects.ForPlayer(target).Effects.Has(TempMods.EnrageAbility));
        Assert.False(TimedEffects.ForPlayer(target).Effects.HasParent(abilityId));
        Assert.True(AbilityCooldowns.IsCoolingDown(caster.playerData, abilityId, TimeUtils.GetSimTimeSeconds()));
        Assert.Empty(Rpcs(target));
    }

    [Theory]
    [InlineData(53)]
    [InlineData(57)]
    [InlineData(58)]
    [InlineData(59)]
    [InlineData(60)]
    [InlineData(404)]
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

    [Theory]
    [InlineData(399, "empty", AbilityRefusals.InvalidTarget)]
    [InlineData(400, "ally", AbilityRefusals.InvalidTarget)]
    [InlineData(399, "range", AbilityRefusals.TargetOutOfRange)]
    [InlineData(400, "range", AbilityRefusals.TargetOutOfRange)]
    [InlineData(399, "combat", AbilityRefusals.MustBeOutsideCombat)]
    [InlineData(400, "style", AbilityRefusals.WrongStyle)]
    [InlineData(399, "sight", AbilityRefusals.TargetNotInSight)]
    [InlineData(400, "sight", AbilityRefusals.TargetNotInSight)]
    [InlineData(399, "unloaded", AbilityRefusals.NotLoaded)]
    [InlineData(399, "stale", 0u)]
    [InlineData(400, "left range", 0u)]
    [InlineData(400, "combat", 0u)]
    public void DespoilerAreasValidateTheOriginalSelectionBeforeItsSplash(ushort id, string action, uint refusal) =>
        CheckSelectedFieldAdmission(id, action, refusal);

    [Theory]
    [InlineData(361, "empty", AbilityRefusals.InvalidTarget)]
    [InlineData(465, "ally", AbilityRefusals.InvalidTarget)]
    [InlineData(361, "range", AbilityRefusals.TargetOutOfRange)]
    [InlineData(465, "range", AbilityRefusals.TargetOutOfRange)]
    [InlineData(361, "sight", AbilityRefusals.TargetNotInSight)]
    [InlineData(465, "sight", AbilityRefusals.TargetNotInSight)]
    [InlineData(361, "style", AbilityRefusals.WrongStyle)]
    [InlineData(465, "unloaded", AbilityRefusals.NotLoaded)]
    [InlineData(361, "combat", AbilityRefusals.MustBeOutsideCombat)]
    [InlineData(465, "combat", AbilityRefusals.MustBeOutsideCombat)]
    [InlineData(361, "stale", 0u)]
    [InlineData(465, "left range", 0u)]
    [InlineData(361, "land", 0u)]
    [InlineData(465, "land", 0u)]
    public void FumbleFieldAndUiLagBindTheOriginalHostileBeforeSpendingOrLanding(ushort id, string action, uint refusal) =>
        CheckSelectedFieldAdmission(id, action, refusal);

    private static void CheckSelectedFieldAdmission(ushort id, string action, uint refusal)
    {
        var caster = Client();
        var target = Client();
        caster.playerData.passives.Loaded[CombatStyles.HackerStyle] = 30;
        if (action != "unloaded") caster.playerData.passives.Loaded[id] = 24;
        if (action != "style") caster.playerData.combatStyle = CombatStyles.HackerStyle;
        caster.playerInstance.OrganizationID.setValue((byte)1);
        target.playerInstance.OrganizationID.setValue((byte)(action == "ally" ? 1 : 2));
        caster.playerData.pvpFlag = target.playerData.pvpFlag = true;
        target.playerData.setCharID(111);
        target.playerData.setEntityId(42);
        double outsideRange = AbilityCatalog.Find(id).Number("Ability/SelectionRange") * 100 + 1;
        target.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(action == "range" ? outsideRange : 200, 0, 0));
        if (action == "sight")
        {
            string repository = HeadlessClient.NativeBoundFurnitureFixture.FindRepository();
            SectorProperties.Load(Path.Combine(repository, "data", "sector_properties.csv"));
            NavMesh.UseDirectory(Path.Combine(repository, "data", "navmesh"));
            caster.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(21925, 1095, 16100));
            target.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(21675, 1095, 16100));
            Assert.False(NavMesh.HasSightPath(1, 21925, 1095, 16100, 21675, 1095, 16100));
        }
        WorldServer.Clients.Add("target", target);
        var view = caster.viewMan.addView(42, 12);
        view.viewCreated = true;
        view.spawnId = 9;
        Store.currentClient = caster;

        var ability = new AbilityItem();
        ability.setAbilityID(id);
        ability.setAbilityName("Selected hostile field");
        ability.setCastAnimStart(new byte[4]);
        DataLoader.getInstance().AbilityDB.Add(ability);
        var fight = CombatManager.GetHandler(caster);
        fight.isCombatRunning = action == "combat";
        var request = new byte[41];
        BitConverter.GetBytes(id).CopyTo(request, 0);
        BitConverter.GetBytes(action == "empty" ? (ushort)0 : view.ViewID).CopyTo(request, 2);
        BitConverter.GetBytes((ushort)9).CopyTo(request, 4);
        double before = caster.playerData.castEndsAt;
        bool lands = action == "land";
        string[] viralKeys = { "MXO_FREECAST_VIRAL_QA", FreeCastViralContest.AttackQa, FreeCastViralContest.DefenseQa };
        string?[] savedViral = viralKeys.Select(Environment.GetEnvironmentVariable).ToArray();
        try
        {
            if (lands)
            {
                caster.playerData.setEntityId(110);
                caster.playerData.setUDPSessionEstablished(true);
                target.playerData.setUDPSessionEstablished(true);
                foreach (var (key, value) in viralKeys.Zip(new[] { "1", "32766", "0" }))
                    Environment.SetEnvironmentVariable(key, value);
            }
            new AbilityHandler().ProcessAbility(ref request);
            if (refusal == 0)
            {
                double end = caster.playerData.castEndsAt;
                Assert.True(end > TimeUtils.GetSimTimeSeconds());
                Assert.Equal(500 - AbilityCosts.Cost(id), AbilityHandler.CurrentInnerStrength(caster));
                if (action == "left range")
                    target.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(outsideRange, 0, 0));
                else if (!lands)
                    view.spawnId++;
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
            if (!lands) Assert.Empty(Rpcs(target));
            Assert.Equal(lands, TimedEffects.ForPlayer(target).Effects.HasParent(id));
            Assert.Equal(1000, PvpCombat.HealthOf(target));
        }
        finally
        {
            if (lands)
            {
                TimedEffects.Clear(TimedEffects.ForPlayer(target));
                foreach (var (key, value) in viralKeys.Zip(savedViral)) Environment.SetEnvironmentVariable(key, value);
            }
            fight.isCombatRunning = false;
            CombatManager.RemoveClient(caster);
        }
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

    [Theory]
    [InlineData("unloaded", AbilityRefusals.NotLoaded)]
    [InlineData("single SMG", AbilityRefusals.RequiredItemCategory)]
    [InlineData("out of combat", AbilityRefusals.InvalidTarget)]
    [InlineData("other opponent", AbilityRefusals.InvalidTarget)]
    [InlineData("stale request", AbilityRefusals.InvalidTarget)]
    [InlineData("outside range", AbilityRefusals.TargetOutOfRange)]
    [InlineData("blocked sight", AbilityRefusals.TargetNotInSight)]
    [InlineData("no daze", AbilityRefusals.TargetStateRequired)]
    [InlineData("current daze", 0u)]
    [InlineData("current tactic daze", 0u)]
    public void SlidingVolleyQueuesOnlyOnTheDazedCurrentOpponent(string scenario, uint refusal)
    {
        const ushort id = CombatSpecials.SlidingVolley, level = 22;
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "data", "sector_properties.csv"))) root = Path.GetDirectoryName(root)!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));

        var caster = Client();
        caster.playerInstance.Level.setValue((byte)level);
        caster.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(21750, 1095, 16150));
        caster.playerInstance.EquippedItemID.setValue(scenario == "single SMG" ? 698u : 2503u);
        var profile = new ushort[] { 2, 17, CombatStyles.SubmachineGuns, id }
            .SelectMany(ability => AbilityPrerequisites.Current.Closure(ability).Keys.Append(ability)).Distinct().ToArray();
        Assert.True(profile.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(level));
        AbilityPassives.SetLoaded(caster, profile.Where(ability => scenario != "unloaded" || ability != id)
            .Select(ability => (ability, level)));
        CombatStyles.Select(caster, CombatStyles.SubmachineGuns);
        caster.messageQueue.RPCMessagesQueue.Clear();
        caster.messageQueue.ObjectMessagesQueue.Clear();
        Store.currentClient = caster;

        var enemy = new Mob();
        enemy.setEntityId(249000);
        enemy.setDistrict(1);
        enemy.setLevel(level);
        enemy.setHealthC(1000);
        enemy.setHealthM(1000);
        enemy.setIsSpawned(true);
        enemy.setXPos(scenario == "outside range" ? 20149 : scenario == "blocked sight" ? 22350 : 21550);
        enemy.setYPos(1095);
        enemy.setZPos(16150);
        var view = caster.viewMan.addView(enemy.getEntityId(), 1);
        view.viewCreated = true;
        view.spawnId = 9;
        uint handle = DuelPackets.Handle(view.ViewID, view.spawnId);
        var session = new CombatSession(caster, enemy, scenario == "other opponent" ? handle ^ 0x10000 : handle,
            CombatSession.CombatType.Melee) { CurrentMove = new CombatInterlock.Move() };
        typeof(CombatSession).GetField("<IsActive>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(session, true);
        var handler = CombatManager.GetHandler(caster);
        var sessionField = typeof(CombatHandler).GetField("currentSession", BindingFlags.Instance | BindingFlags.NonPublic)!;
        if (scenario != "out of combat") sessionField.SetValue(handler, session);
        var previousMobs = WorldServer.mobs;
        WorldServer.mobs = new System.Collections.ArrayList { enemy };
        var subject = TimedEffects.ForMob(enemy);

        try
        {
            if (scenario == "current tactic daze")
            {
                typeof(Mob).GetField("conditionFlags", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(enemy, CombatInterlock.ConditionBit(CombatTactic.Speed));
            }
            else if (scenario != "no daze")
            {
                var daze = CombatDepthDaze.Effect(30);
                subject.Effects.Apply(daze.Ability, TimedEffects.Instantiate(daze, new Random(1)), TimeUtils.GetSimTimeSeconds());
            }
            var ability = new AbilityItem();
            ability.setAbilityID(id);
            ability.setAbilityName("Sliding Volley");
            ability.setCastAnimStart(new byte[4]);
            DataLoader.getInstance().AbilityDB.Add(ability);

            // Internal handler frame, never replayed by an external bootstrap.
            var request = new byte[41];
            BitConverter.GetBytes(id).CopyTo(request, 0);
            BitConverter.GetBytes(scenario == "stale request" ? handle ^ 0x10000 : handle).CopyTo(request, 2);
            new AbilityHandler().ProcessAbility(ref request);

            if (refusal != 0)
            {
                Assert.Equal(AbilityRefusals.Encode(new(id, refusal, states: scenario == "no daze" ? 2u : 0u)),
                    Assert.Single(Rpcs(caster)));
                Assert.Null(session.QueuedAbility);
                Assert.Equal(500, AbilityHandler.CurrentInnerStrength(caster));
                Assert.Empty(caster.playerData.cooldownEndsAt);
                return;
            }

            Assert.Same(ability, session.QueuedAbility);
            Assert.Equal(view.ViewID, session.QueuedAbilityTarget);
            Assert.Equal(485, AbilityHandler.CurrentInnerStrength(caster));
            Assert.Equal(1000, enemy.getHealthC());
            Assert.True(AbilityCooldowns.IsCoolingDown(caster.playerData, id, TimeUtils.GetSimTimeSeconds()));
            Assert.True(AbilityCooldowns.TryGet(id, out var cooldown));
            Assert.Equal(10f, cooldown.Seconds);
            Assert.Equal(689, cooldown.Group);
            Assert.Equal(5f, cooldown.GroupSeconds);
            Assert.Equal(21081u, CombatAnimations.Special(id, true, CombatTactic.Speed)?.Id);
            Assert.True(handler.CanResolveNpcQueuedAbility(session, ability));
            view.spawnId++;
            Assert.False(handler.CanResolveNpcQueuedAbility(session, ability));
            view.spawnId--;
            view.viewNeedsToBeDeleted = true;
            Assert.False(handler.CanResolveNpcQueuedAbility(session, ability));
            view.viewNeedsToBeDeleted = false;
            caster.playerInstance.EquippedItemID.setValue(698u);
            Assert.False(handler.CanResolveNpcQueuedAbility(session, ability));
            caster.playerInstance.EquippedItemID.setValue(2503u);
            caster.playerData.passives.Loaded.Remove(id);
            Assert.False(handler.CanResolveNpcQueuedAbility(session, ability));
        }
        finally
        {
            TimedEffects.Clear(subject);
            sessionField.SetValue(handler, null);
            CombatManager.RemoveClient(caster);
            WorldServer.mobs = previousMobs;
        }
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
    [InlineData(473, "empty", AbilityRefusals.InvalidTarget)]
    [InlineData(473, "far", AbilityRefusals.TargetOutOfRange)]
    [InlineData(473, "blocked", AbilityRefusals.TargetNotInSight)]
    [InlineData(473, "replacement", 0u)]
    [InlineData(473, "unloaded", AbilityRefusals.NotLoaded)]
    [InlineData(479, "ally", AbilityRefusals.InvalidTarget)]
    [InlineData(479, "far", AbilityRefusals.TargetOutOfRange)]
    [InlineData(479, "style", AbilityRefusals.WrongStyle)]
    [InlineData(479, "combat", AbilityRefusals.MustBeOutsideCombat)]
    [InlineData(479, "retreat", 0u)]
    [InlineData(462, "empty", AbilityRefusals.InvalidTarget)]
    [InlineData(462, "far", AbilityRefusals.TargetOutOfRange)]
    [InlineData(462, "replacement", 0u)]
    [InlineData(467, "ally", AbilityRefusals.InvalidTarget)]
    [InlineData(467, "blocked", AbilityRefusals.TargetNotInSight)]
    [InlineData(467, "retreat", 0u)]
    [InlineData(1088, "unloaded", AbilityRefusals.NotLoaded)]
    [InlineData(1088, "combat", AbilityRefusals.MustBeOutsideCombat)]
    [InlineData(1088, "sight-loss", 0u)]
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
    [InlineData(353, "empty", AbilityRefusals.InvalidTarget)]
    [InlineData(353, "far", AbilityRefusals.TargetOutOfRange)]
    [InlineData(353, "replacement", 0u)]
    [InlineData(402, "far", AbilityRefusals.TargetOutOfRange)]
    [InlineData(402, "blocked", AbilityRefusals.TargetNotInSight)]
    [InlineData(402, "combat", AbilityRefusals.MustBeOutsideCombat)]
    [InlineData(595, "empty", AbilityRefusals.InvalidTarget)]
    [InlineData(595, "ally", AbilityRefusals.InvalidTarget)]
    [InlineData(595, "stale", AbilityRefusals.InvalidTarget)]
    [InlineData(595, "far", AbilityRefusals.TargetOutOfRange)]
    [InlineData(595, "blocked", AbilityRefusals.TargetNotInSight)]
    [InlineData(595, "style", AbilityRefusals.WrongStyle)]
    [InlineData(595, "combat", AbilityRefusals.MustBeOutsideCombat)]
    [InlineData(595, "unloaded", AbilityRefusals.NotLoaded)]
    [InlineData(595, "replacement", 0u)]
    [InlineData(595, "retreat", 0u)]
    [InlineData(595, "sight-loss", 0u)]
    [InlineData(360, "empty", AbilityRefusals.InvalidTarget)]
    [InlineData(360, "ally", AbilityRefusals.InvalidTarget)]
    [InlineData(360, "far", AbilityRefusals.TargetOutOfRange)]
    [InlineData(360, "blocked", AbilityRefusals.TargetNotInSight)]
    [InlineData(360, "style", AbilityRefusals.WrongStyle)]
    [InlineData(360, "combat", AbilityRefusals.MustBeOutsideCombat)]
    [InlineData(360, "unloaded", AbilityRefusals.NotLoaded)]
    [InlineData(360, "replacement", 0u)]
    [InlineData(360, "retreat", 0u)]
    [InlineData(360, "sight-loss", 0u)]
    [InlineData(1081, "empty", AbilityRefusals.InvalidTarget)]
    [InlineData(1081, "ally", AbilityRefusals.InvalidTarget)]
    [InlineData(1081, "far", AbilityRefusals.TargetOutOfRange)]
    [InlineData(1081, "blocked", AbilityRefusals.TargetNotInSight)]
    [InlineData(1081, "style", AbilityRefusals.WrongStyle)]
    [InlineData(1081, "combat", AbilityRefusals.MustBeOutsideCombat)]
    [InlineData(1081, "unloaded", AbilityRefusals.NotLoaded)]
    [InlineData(1081, "replacement", 0u)]
    [InlineData(1081, "retreat", 0u)]
    [InlineData(1081, "sight-loss", 0u)]
    [InlineData(333, "empty", AbilityRefusals.InvalidTarget)]
    [InlineData(333, "ally", AbilityRefusals.InvalidTarget)]
    [InlineData(333, "stale", AbilityRefusals.InvalidTarget)]
    [InlineData(333, "far", AbilityRefusals.TargetOutOfRange)]
    [InlineData(333, "blocked", AbilityRefusals.TargetNotInSight)]
    [InlineData(333, "style", AbilityRefusals.WrongStyle)]
    [InlineData(333, "unloaded", AbilityRefusals.NotLoaded)]
    [InlineData(333, "replacement", 0u)]
    [InlineData(333, "retreat", 0u)]
    [InlineData(333, "sight-loss", 0u)]
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
        ushort style = CombatStyles.RequiredStyle(id);
        caster.playerData.passives.Loaded[style] = 38;
        if (action != "unloaded")
        {
            if (id == 1081)
            {
                AbilityPassives.SetLoaded(caster, new[] { (style, (ushort)50), ((ushort)470, (ushort)50) });
                Assert.True(AbilityPassives.IsLoaded(caster, id));
                caster.messageQueue.RPCMessagesQueue.Clear();
                caster.messageQueue.ObjectMessagesQueue.Clear();
            }
            else caster.playerData.passives.Loaded[id] = 38;
        }
        if (action != "style") caster.playerData.combatStyle = style;
        caster.playerInstance.OrganizationID.setValue((byte)1);
        double farX = 21750 - 100 * AbilityCatalog.Find(id).Number("Ability/SelectionRange") - 1;
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
            if (id == 1088 && action != "unloaded")
            {
                AbilityPassives.SetLoaded(caster, new[] { (style, (ushort)50), ((ushort)469, (ushort)50) });
                caster.messageQueue.RPCMessagesQueue.Clear();
            }
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

    [Theory]
    [InlineData(605, "unloaded", AbilityRefusals.NotLoaded)]
    [InlineData(618, "unloaded", AbilityRefusals.NotLoaded)]
    [InlineData(605, "rest", AbilityRefusals.InvalidTarget)]
    [InlineData(618, "rest", AbilityRefusals.InvalidTarget)]
    [InlineData(605, "stale", AbilityRefusals.InvalidTarget)]
    [InlineData(618, "stale", AbilityRefusals.InvalidTarget)]
    [InlineData(605, "far", AbilityRefusals.TargetOutOfRange)]
    [InlineData(618, "far", AbilityRefusals.TargetOutOfRange)]
    [InlineData(605, "blocked", AbilityRefusals.TargetNotInSight)]
    [InlineData(618, "blocked", AbilityRefusals.TargetNotInSight)]
    [InlineData(618, "no-stagger", AbilityRefusals.TargetStateRequired)]
    [InlineData(605, "ready", 0u)]
    [InlineData(618, "ready", 0u)]
    [InlineData(243, "unloaded", AbilityRefusals.NotLoaded)]
    [InlineData(243, "rest", AbilityRefusals.InvalidTarget)]
    [InlineData(243, "stale", AbilityRefusals.InvalidTarget)]
    [InlineData(243, "far", AbilityRefusals.TargetOutOfRange)]
    [InlineData(243, "blocked", AbilityRefusals.TargetNotInSight)]
    [InlineData(243, "ready", 0u)]
    public void SelectedSpecialsRequireTheLoadedMoveAndLiveCurrentOpponent(ushort id, string scenario, uint reason)
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "data", "sector_properties.csv")))
            root = Path.GetDirectoryName(root)!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));

        var caster = Client();
        Store.currentClient = caster;
        ushort style = id == AbilityConditionalControls.PistolAerial ? CombatStyles.Handguns : CombatStyles.CoderStyle;
        caster.playerData.passives.Loaded[style] = id == AbilityConditionalControls.PistolAerial ? (byte)25 : (byte)19;
        caster.playerData.combatStyle = style;
        if (scenario != "unloaded") caster.playerData.passives.Loaded[id] = 38;

        var target = new Mob();
        target.setEntityId(618000);
        target.setDistrict(1);
        target.setHealthC(1000);
        target.setHealthM(1000);
        target.setLevel(50);
        target.setIsSpawned(true);
        caster.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(21750, 1095, 16150));
        target.setXPos(scenario == "blocked" ? 22350 : scenario == "far" ? 20149 : 21550);
        target.setYPos(1095);
        target.setZPos(16150);
        var view = caster.viewMan.addView(target.getEntityId(), 1);
        view.viewCreated = true;
        view.spawnId = 9;
        uint handle = DuelPackets.Handle(view.ViewID, view.spawnId);
        var session = new CombatSession(caster, target, handle, CombatSession.CombatType.Melee)
        {
            CurrentMove = new CombatInterlock.Move()
        };
        var handler = CombatManager.GetHandler(caster);
        var sessionField = typeof(CombatHandler).GetField("currentSession", BindingFlags.Instance | BindingFlags.NonPublic)!;
        typeof(CombatSession).GetField("<IsActive>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(session, true);
        if (scenario != "rest") sessionField.SetValue(handler, session);
        var previousMobs = WorldServer.mobs;
        WorldServer.mobs = new System.Collections.ArrayList { target };

        var ability = new AbilityItem();
        ability.setAbilityID(id);
        ability.setAbilityName(AbilityCatalog.Find(id).Name);
        ability.setCastAnimStart(new byte[4]);
        DataLoader.getInstance().AbilityDB.Add(ability);

        try
        {
            if (reason == 0 && id == 618)
                TimedEffects.ForMob(target).Effects.Apply(883, TimedEffects.Instantiate(CombatDepthStagger.Effect(30), new Random(1)), TimeUtils.GetSimTimeSeconds());
            var request = new byte[41];
            BitConverter.GetBytes(id).CopyTo(request, 0);
            BitConverter.GetBytes(scenario == "stale" ? handle ^ 0x10000u : handle).CopyTo(request, 2);
            new AbilityHandler().ProcessAbility(ref request);
            if (reason != 0)
            {
                Assert.Equal(AbilityRefusals.Encode(new(id, reason, states: scenario == "no-stagger" ? 8u : 0u)),
                    Assert.Single(Rpcs(caster)));
                Assert.Null(session.QueuedAbility);
                Assert.Equal(500, AbilityHandler.CurrentInnerStrength(caster));
                Assert.Empty(caster.playerData.cooldownEndsAt);
                return;
            }

            Assert.Same(ability, session.QueuedAbility);
            Assert.Equal(view.ViewID, session.QueuedAbilityTarget);
            Assert.Equal(500 - AbilityCosts.Cost(id), AbilityHandler.CurrentInnerStrength(caster));
            Assert.True(AbilityCooldowns.IsCoolingDown(caster.playerData, id, TimeUtils.GetSimTimeSeconds()));
            Assert.Equal(1000, target.getHealthC()); // Queuing leaves the target for the real round contest.
        }
        finally
        {
            TimedEffects.Clear(TimedEffects.ForMob(target));
            sessionField.SetValue(handler, null);
            CombatManager.RemoveClient(caster);
            WorldServer.mobs = previousMobs;
        }
    }

    [Theory]
    [InlineData("unloaded", AbilityRefusals.NotLoaded, 250)]
    [InlineData("single SMG", AbilityRefusals.RequiredItemCategory, 250)]
    [InlineData("missing", AbilityRefusals.InvalidTarget, 250)]
    [InlineData("ally", AbilityRefusals.InvalidTarget, 250)]
    [InlineData("stale request", AbilityRefusals.InvalidTarget, 250)]
    [InlineData("far", AbilityRefusals.TargetOutOfRange, 250)]
    [InlineData("blocked", AbilityRefusals.TargetNotInSight, 250)]
    [InlineData("left range", 0u, 250)]
    [InlineData("lost sight", 0u, 250)]
    [InlineData("new spawn", 0u, 250)]
    [InlineData("unloaded while casting", 0u, 250)]
    [InlineData("swapped while casting", 0u, 250)]
    [InlineData("neutral", 0u, 250)]
    [InlineData("resisted", 0u, 250)]
    [InlineData("missing stagger", AbilityRefusals.TargetStateRequired, 256)]
    [InlineData("unloaded", AbilityRefusals.NotLoaded, 256)]
    [InlineData("single SMG", AbilityRefusals.RequiredItemCategory, 256)]
    [InlineData("missing", AbilityRefusals.InvalidTarget, 256)]
    [InlineData("ally", AbilityRefusals.InvalidTarget, 256)]
    [InlineData("stale request", AbilityRefusals.InvalidTarget, 256)]
    [InlineData("far", AbilityRefusals.TargetOutOfRange, 256)]
    [InlineData("blocked", AbilityRefusals.TargetNotInSight, 256)]
    [InlineData("left range", 0u, 256)]
    [InlineData("lost sight", 0u, 256)]
    [InlineData("new spawn", 0u, 256)]
    [InlineData("unloaded while casting", 0u, 256)]
    [InlineData("swapped while casting", 0u, 256)]
    [InlineData("neutral", 0u, 256)]
    [InlineData("resisted", 0u, 256)]
    public void TimedGunSpecialBindsItsEligibleTargetAndConsumesTypedResistance(string scenario, uint refusal, int idValue)
    {
        ushort id = (ushort)idValue;
        bool rifle = id == CombatSpecials.ThreeRoundBurst;
        int outside = rifle ? 19749 : 20349;
        var caster = TimedGunSpecialCaster(id, unloaded: scenario == "unloaded", wrongWeapon: scenario == "single SMG");
        var target = Client();
        target.playerData.setCharID(111);
        target.playerData.setEntityId(111);
        target.playerInstance.Level.setValue((byte)27);
        caster.playerInstance.OrganizationID.setValue((byte)1);
        target.playerInstance.OrganizationID.setValue((byte)(scenario == "ally" ? 1 : 2));
        caster.playerData.pvpFlag = target.playerData.pvpFlag = true;
        SetTargetX(scenario == "far" ? outside : scenario == "blocked" ? 22350 : 21550);
        WorldServer.Clients.Add("caster", caster);
        WorldServer.Clients.Add("target", target);
        var view = caster.viewMan.addView(111, 12);
        view.viewCreated = true;
        view.spawnId = 9;
        uint selected = scenario == "missing" ? 0 :
            DuelPackets.Handle(view.ViewID, scenario == "stale request" ? 8 : view.spawnId);
        byte[] request = TimedGunSpecialRequest(id, selected);
        double start = TimeUtils.GetSimTimeSeconds();
        if (rifle && scenario != "missing stagger") SeedStagger(TimedEffects.ForPlayer(target));
        if (scenario == "resisted")
        {
            TimedEffects.ForPlayer(target).Effects.Apply(181,
                new[] { new TimedEffects.Active { Target = 181, Value = 20, Seconds = 60, Proc = TempMods.Proc.Stat } }, start);
        }

        try
        {
            new AbilityHandler().ProcessAbility(ref request);
            if (refusal != 0)
            {
                Assert.Equal(AbilityRefusals.Encode(new(id, refusal, states: scenario == "missing stagger" ? 8u : 0u)),
                    Assert.Single(Rpcs(caster)));
                Assert.Equal(500, AbilityHandler.CurrentInnerStrength(caster));
                Assert.Empty(caster.playerData.cooldownEndsAt);
                return;
            }

            double castEnd = caster.playerData.castEndsAt;
            Assert.InRange(castEnd - start, 3.99, 4.25);
            Assert.Equal(rifle ? 480 : 470, AbilityHandler.CurrentInnerStrength(caster));
            Assert.Equal(1000, PvpCombat.HealthOf(target));
            Assert.Contains(Rpcs(caster), wire => wire.SequenceEqual(
                Hex(rifle ? "80ac00010000000000000000000000008040" : "80acfa000000000000000000000000008040")));
            Assert.False(AbilityCooldowns.IsCoolingDown(caster.playerData, id, TimeUtils.GetSimTimeSeconds()));
            Assert.True(AbilityCooldowns.TryGet(id, out var cooldown));
            Assert.Equal(10f, cooldown.Seconds);
            Assert.Equal(689, cooldown.Group);
            Assert.Equal(5f, cooldown.GroupSeconds);

            switch (scenario)
            {
                case "left range": SetTargetX(outside); break;
                case "lost sight": SetTargetX(22350); break;
                case "new spawn": view.spawnId++; break;
                case "unloaded while casting": caster.playerData.passives.Loaded.Remove(id); break;
                case "swapped while casting": caster.playerInstance.EquippedItemID.setValue(698u); break;
            }

            Assert.Equal(13, AbilityStats.Influence(caster, 171));
            AbilityCastInterrupts.FinishDue(caster, castEnd + .01);
            Assert.True(AbilityCooldowns.IsCoolingDown(caster.playerData, id, TimeUtils.GetSimTimeSeconds()));
            var expectedHealth = scenario switch
            {
                "neutral" => rifle ? 910 : 971,
                "resisted" => rifle ? 918 : 974,
                _ => 1000
            };
            Assert.Equal(expectedHealth, PvpCombat.HealthOf(target));
        }
        finally { TimedEffects.Clear(TimedEffects.ForPlayer(target)); }

        void SetTargetX(double x) =>
            target.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(x, 1095, 16150));
    }

    [Theory]
    [InlineData("same fight", 250)]
    [InlineData("different opponent", 250)]
    [InlineData("fight ends", 250)]
    [InlineData("fight replaced", 250)]
    [InlineData("same fight", 256)]
    [InlineData("different opponent", 256)]
    [InlineData("fight ends", 256)]
    [InlineData("fight replaced", 256)]
    [InlineData("wrong state", 256)]
    public void TimedGunSpecialQueuesAgainstTheOriginalInterlock(string scenario, int idValue)
    {
        ushort id = (ushort)idValue;
        bool rifle = id == CombatSpecials.ThreeRoundBurst;
        var caster = TimedGunSpecialCaster(id);
        var target = new Mob();
        target.setEntityId(250000);
        target.setDistrict(1);
        target.setHealthC(1000);
        target.setHealthM(1000);
        target.setLevel(27);
        target.setIsSpawned(true);
        target.setXPos(21550);
        target.setYPos(1095);
        target.setZPos(16150);
        var view = caster.viewMan.addView(target.getEntityId(), 1);
        view.viewCreated = true;
        view.spawnId = 9;
        uint handle = DuelPackets.Handle(view.ViewID, view.spawnId);
        var original = NewFight(scenario == "different opponent" ? handle ^ 0x10000 : handle);
        var handler = CombatManager.GetHandler(caster);
        var sessionField = typeof(CombatHandler).GetField("currentSession", BindingFlags.Instance | BindingFlags.NonPublic)!;
        sessionField.SetValue(handler, original);
        var previousMobs = WorldServer.mobs;
        WorldServer.mobs = new System.Collections.ArrayList { target };

        var targetEffects = TimedEffects.ForMob(target);
        if (rifle && scenario != "wrong state") SeedStagger(targetEffects);
        try
        {
            byte[] request = TimedGunSpecialRequest(id, handle);
            new AbilityHandler().ProcessAbility(ref request);
            Assert.Null(original.QueuedAbility);
            if (scenario is "different opponent" or "wrong state")
            {
                bool wrongState = scenario == "wrong state";
                Assert.Equal(AbilityRefusals.Encode(new(id,
                    wrongState ? AbilityRefusals.TargetStateRequired : AbilityRefusals.InvalidTarget,
                    states: wrongState ? 8u : 0u)), Assert.Single(Rpcs(caster)));
                Assert.Equal(500, AbilityHandler.CurrentInnerStrength(caster));
                return;
            }

            Assert.Equal(rifle ? 480 : 470, AbilityHandler.CurrentInnerStrength(caster));
            CombatSession? replacement = scenario == "fight replaced" ? NewFight(handle) : null;
            if (scenario != "same fight") sessionField.SetValue(handler, replacement);
            AbilityCastInterrupts.FinishDue(caster, caster.playerData.castEndsAt + .01);
            Assert.Equal(1000, target.getHealthC());
            if (scenario == "same fight")
            {
                var queued = Assert.IsType<AbilityItem>(original.QueuedAbility);
                Assert.Equal(id, queued.getAbilityID());
                Assert.Equal(view.ViewID, original.QueuedAbilityTarget);
                Assert.Equal(rifle ? 11367u : 21082u, CombatAnimations.Special(id, true, CombatTactic.Speed)?.Id);
                Assert.Null(CombatAnimations.Special(id, false, CombatTactic.Speed));
                if (rifle) Assert.Equal(AbilityStats.Attack(caster, true) + 21, AbilityStats.SpecialAttack(caster, queued));
                Assert.True(CombatAnimations.CanResolveQueuedAbility(caster, queued));
                caster.playerInstance.EquippedItemID.setValue(698u);
                Assert.False(CombatAnimations.CanResolveQueuedAbility(caster, queued));
                caster.playerInstance.EquippedItemID.setValue(rifle ? 692u : 2503u);
                caster.playerData.passives.Loaded.Remove(id);
                Assert.False(CombatAnimations.CanResolveQueuedAbility(caster, queued));
            }
            else
            {
                Assert.Null(original.QueuedAbility);
                Assert.Null(replacement?.QueuedAbility);
            }
        }
        finally
        {
            TimedEffects.Clear(targetEffects);
            sessionField.SetValue(handler, null);
            CombatManager.RemoveClient(caster);
            WorldServer.mobs = previousMobs;
        }

        CombatSession NewFight(uint opponent)
        {
            var fight = new CombatSession(caster, target, opponent, CombatSession.CombatType.Melee)
            {
                CurrentMove = new CombatInterlock.Move()
            };
            typeof(CombatSession).GetField("<IsActive>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(fight, true);
            return fight;
        }
    }

    private static WorldClient TimedGunSpecialCaster(ushort id, bool unloaded = false, bool wrongWeapon = false)
    {
        bool rifle = id == CombatSpecials.ThreeRoundBurst;
        ushort style = rifle ? CombatStyles.Rifles : CombatStyles.SubmachineGuns;
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "data", "sector_properties.csv"))) root = Path.GetDirectoryName(root)!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));

        var caster = Client();
        caster.playerInstance.Level.setValue((byte)27);
        caster.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(21750, 1095, 16150));
        caster.playerInstance.EquippedItemID.setValue(wrongWeapon ? 698u : rifle ? 692u : 2503u);
        var profile = new ushort[] { 2, 17, style, id }
            .SelectMany(entry => AbilityPrerequisites.Current.Closure(entry).Keys.Append(entry)).Distinct().ToArray();
        Assert.True(profile.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(27));
        AbilityPassives.SetLoaded(caster, profile.Where(entry => !unloaded || entry != id).Select(entry => (entry, (ushort)27)));
        CombatStyles.Select(caster, style);
        caster.messageQueue.RPCMessagesQueue.Clear();
        caster.messageQueue.ObjectMessagesQueue.Clear();
        Store.currentClient = caster;

        var ability = new AbilityItem();
        ability.setAbilityID(id);
        ability.setAbilityName(rifle ? "Three Round Burst" : "Controlled Burst");
        ability.setIsCastable(true);
        ability.setCastingTime(4);
        ability.setCastAnimStart(new byte[4]);
        DataLoader.getInstance().AbilityDB.Add(ability);
        return caster;
    }

    private static void SeedStagger(TimedEffects.Subject subject)
    {
        var stagger = CombatDepthStagger.Effect(30);
        subject.Effects.Apply(stagger.Ability, TimedEffects.Instantiate(stagger, new Random(1)), TimeUtils.GetSimTimeSeconds());
    }

    private static byte[] TimedGunSpecialRequest(ushort id, uint handle)
    {
        // In-process handler frame. No external bootstrap treats it as a stock capture.
        var request = new byte[41];
        BitConverter.GetBytes(id).CopyTo(request, 0);
        BitConverter.GetBytes(handle).CopyTo(request, 2);
        return request;
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

    [Theory]
    [InlineData(352, "empty", AbilityRefusals.InvalidTarget)]
    [InlineData(393, "ally", AbilityRefusals.InvalidTarget)]
    [InlineData(352, "spawn", AbilityRefusals.InvalidTarget)]
    [InlineData(393, "far", AbilityRefusals.TargetOutOfRange)]
    [InlineData(352, "far", AbilityRefusals.TargetOutOfRange)]
    [InlineData(393, "blocked", AbilityRefusals.TargetNotInSight)]
    [InlineData(352, "blocked", AbilityRefusals.TargetNotInSight)]
    [InlineData(393, "combat", AbilityRefusals.MustBeOutsideCombat)]
    [InlineData(352, "style", AbilityRefusals.WrongStyle)]
    [InlineData(393, "unloaded", AbilityRefusals.NotLoaded)]
    [InlineData(352, "swap", 0u)]
    [InlineData(393, "retreat", 0u)]
    [InlineData(352, "retreat", 0u)]
    [InlineData(393, "unload", 0u)]
    public void SelectedPacifyRefusesBeforeCostAndRetainsTheAdmittedSpawn(ushort id, string state, uint reason)
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "data", "sector_properties.csv")))
            root = Path.GetDirectoryName(root)!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        var caster = Client();
        var target = Client();
        caster.playerData.passives.Loaded[CombatStyles.HackerStyle] = 30;
        if (state != "unloaded") caster.playerData.passives.Loaded[id] = 24;
        if (state != "style") caster.playerData.combatStyle = CombatStyles.HackerStyle;
        caster.playerInstance.OrganizationID.setValue((byte)1);
        target.playerInstance.OrganizationID.setValue((byte)(state == "ally" ? 1 : 2));
        caster.playerData.pvpFlag = target.playerData.pvpFlag = true;
        target.playerData.setCharID(111);
        target.playerData.setEntityId(42);
        caster.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(21750, 1095, 16150));
        double distantX = id == 393 ? 20349 : 19749;
        double initialX = state == "far" ? distantX : state == "blocked" ? 22350 : 21550;
        target.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(initialX, 1095, 16150));
        WorldServer.Clients.Add("target", target);
        var targetView = caster.viewMan.addView(42, 12);
        targetView.viewCreated = true;
        targetView.spawnId = 9;
        Store.currentClient = caster;

        var ability = new AbilityItem();
        ability.setAbilityID(id);
        ability.setAbilityName("Selected Pacify");
        ability.setCastAnimStart(new byte[4]);
        DataLoader.getInstance().AbilityDB.Add(ability);
        var combat = CombatManager.GetHandler(caster);
        combat.isCombatRunning = state == "combat";
        var request = new byte[41];
        BitConverter.GetBytes(id).CopyTo(request, 0);
        BitConverter.GetBytes(state == "empty" ? (ushort)0 : targetView.ViewID).CopyTo(request, 2);
        BitConverter.GetBytes((ushort)(state == "spawn" ? 8 : 9)).CopyTo(request, 4);
        double castBefore = caster.playerData.castEndsAt;
        try
        {
            new AbilityHandler().ProcessAbility(ref request);
            if (reason == 0)
            {
                double castEnd = caster.playerData.castEndsAt;
                Assert.True(castEnd > TimeUtils.GetSimTimeSeconds());
                Assert.Equal(500 - AbilityCosts.Cost(id), AbilityHandler.CurrentInnerStrength(caster));
                if (state == "swap") targetView.spawnId++;
                if (state == "retreat")
                    target.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(distantX, 1095, 16150));
                if (state == "unload") caster.playerData.passives.Loaded.Remove(id);
                DelayedActions.RunDue(castEnd + 1);
                Assert.True(AbilityCooldowns.IsCoolingDown(caster.playerData, id, TimeUtils.GetSimTimeSeconds()));
            }
            else
            {
                Assert.Equal(AbilityRefusals.Encode(new(id, reason)), Assert.Single(Rpcs(caster)));
                Assert.Equal(castBefore, caster.playerData.castEndsAt);
                Assert.Equal(500, AbilityHandler.CurrentInnerStrength(caster));
                Assert.Empty(caster.playerData.cooldownEndsAt);
            }
            Assert.Empty(Rpcs(target));
            Assert.True(ControlActions.CanUseAbilities(TimedEffects.ForPlayer(target).Effects));
        }
        finally
        {
            combat.isCombatRunning = false;
            CombatManager.RemoveClient(caster);
        }
    }
}
