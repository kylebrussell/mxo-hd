using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
using hds.shared;

namespace hds.Tests;

[Collection("AbilityParityWorld")]
public class DeadlyCripplingThrowsParityTests
{
    private const ushort DeadlyThrow = DeadlyThrowDamage.Id;               // 279
    private const ushort CripplingThrow = SpyAbilityDamage.CripplingThrow; // 280
    private const ushort SpyStyle = CombatStyles.SpyStyle;                 // 1112

    private static byte[] Pos(double x, double y, double z)
    {
        var bytes = new byte[24];
        BitConverter.GetBytes(x).CopyTo(bytes, 0);
        BitConverter.GetBytes(y).CopyTo(bytes, 8);
        BitConverter.GetBytes(z).CopyTo(bytes, 16);
        return bytes;
    }

    private static WorldClient Player(uint charId, byte alignment, double x, double y, double z, byte level = 25)
    {
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.playerData.setCharID(charId);
        client.playerData.setEntityId(charId);
        client.playerData.setDistrictId(1);
        client.playerData.setOnWorld(true);
        client.playerData.pvpFlag = true;
        client.playerData.isBot = true;
        client.playerInstance = new PlayerCharacter();
        client.playerInstance.Level.setValue(level);
        client.playerInstance.OrganizationID.setValue(alignment);
        client.playerInstance.Position.setValue(Pos(x, y, z));
        client.playerInstance.Health.setValue((ushort)5000);
        client.playerInstance.MaxHealth.setValue((ushort)5000);
        client.playerInstance.InnerStrengthAvailable.setValue((ushort)1000);
        client.playerInstance.InnerStrengthMax.setValue((ushort)1000);
        client.viewMan = new ViewManager();
        client.messageQueue = new MessageQueue();
        typeof(WorldClient).GetField("lastFlushTick", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(client, long.MaxValue);
        return client;
    }

    private static void RegisterView(WorldClient observer, WorldClient target, ushort viewId, ushort spawnId)
    {
        var view = new ClientView(12, viewId, target.playerData.getEntityId())
        {
            spawnId = spawnId,
            viewCreated = true
        };
        observer.viewMan.views.Add(view);
    }

    private static string RepoRoot()
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "hds.sln")))
            root = Path.GetDirectoryName(root)!;
        return root;
    }

    [Fact]
    public void DeadlyAndCripplingThrowEnforceHostileSightRangeStaleAndCombatGates()
    {
        string root = RepoRoot();
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));

        var caster = Player(110, 1, 17000, 495, 6400);
        var enemy = Player(6114, 2, 16800, 495, 6400);
        var friend = Player(6111, 1, 17150, 495, 6400);
        var farEnemy = Player(6112, 2, 17000, 495, 8401);
        var wallCaster = Player(110, 1, 28650, 895, 12700);
        var acrossWall = Player(6113, 2, 29150, 895, 12700);

        RegisterView(caster, enemy, 4, 10);
        RegisterView(caster, friend, 5, 11);
        RegisterView(caster, farEnemy, 6, 12);
        RegisterView(wallCaster, acrossWall, 7, 13);

        var prevWorld = Store.world;
        var prevWorldCfg = Store.worldConfig;
        WorldServer.Clients ??= new Dictionary<string, WorldClient>();
        var savedClients = new Dictionary<string, WorldClient>(WorldServer.Clients);
        try
        {
            Store.world = (WorldServer)RuntimeHelpers.GetUninitializedObject(typeof(WorldServer));
            var worldCfg = (WorldConfig)RuntimeHelpers.GetUninitializedObject(typeof(WorldConfig));
            worldCfg.IsPvpServer = true;
            worldCfg.PvpMaxSafeLevel = 15;
            Store.worldConfig = worldCfg;
            WorldServer.Clients.Clear();
            WorldServer.Clients["dc-caster"] = caster;
            WorldServer.Clients["dc-enemy"] = enemy;
            WorldServer.Clients["dc-friend"] = friend;
            WorldServer.Clients["dc-far"] = farEnemy;
            WorldServer.Clients["dc-wall-caster"] = wallCaster;
            WorldServer.Clients["dc-across"] = acrossWall;
            Assert.True(KnifeThrowerThrowsAdmission.Handles(DeadlyThrow));
            Assert.True(KnifeThrowerThrowsAdmission.Handles(CripplingThrow));

            foreach (ushort id in new[] { DeadlyThrow, CripplingThrow })
            {
                Assert.True(SpyAbilityRegistry.TryRefuse(caster, id, 0, null, out uint emptyReason));
                Assert.Equal(AbilityRefusals.InvalidTarget, emptyReason);

                Assert.True(SpyAbilityRegistry.TryRefuse(caster, id, 5, 11, out uint allyReason));
                Assert.Equal(AbilityRefusals.InvalidTarget, allyReason);

                Assert.True(SpyAbilityRegistry.TryRefuse(caster, id, 4, 99, out uint staleReason));
                Assert.Equal(AbilityRefusals.InvalidTarget, staleReason);

                Assert.True(SpyAbilityRegistry.TryRefuse(caster, id, 6, 12, out uint farReason));
                Assert.Equal(AbilityRefusals.TargetOutOfRange, farReason);

                Assert.True(SpyAbilityRegistry.TryRefuse(wallCaster, id, 7, 13, out uint wallReason));
                Assert.Equal(AbilityRefusals.TargetNotInSight, wallReason);

                Assert.False(SpyAbilityRegistry.TryRefuse(caster, id, 4, 10, out _));
            }

            // Crippling Throw (CastWhenUsed=1) refuses in combat; Deadly Throw (CastWhenUsed=2) remains usable in combat.
            var combat = CombatManager.GetHandler(caster);
            combat.isCombatRunning = true;
            try
            {
                Assert.True(SpyAbilityRegistry.TryRefuse(caster, CripplingThrow, 4, 10, out uint combatReason));
                Assert.Equal(AbilityRefusals.MustBeOutsideCombat, combatReason);
                Assert.False(SpyAbilityRegistry.TryRefuse(caster, DeadlyThrow, 4, 10, out _));
            }
            finally
            {
                combat.isCombatRunning = false;
            }
        }
        finally
        {
            WorldServer.Clients.Clear();
            foreach (var kv in savedClients) WorldServer.Clients[kv.Key] = kv.Value;
            Store.world = prevWorld;
            Store.worldConfig = prevWorldCfg;
        }
    }

    [Fact]
    public void DeadlyThrowProcsQaSelectsExactIndependentControlBranchesInDisposableFixtureOnly()
    {
        var prevConfig = Store.config;
        string oldMode = Environment.GetEnvironmentVariable(DeadlyThrowProcsQa.Mode);
        string oldDev = Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS");
        try
        {
            var cfg = (ServerConfig)RuntimeHelpers.GetUninitializedObject(typeof(ServerConfig));
            typeof(ServerConfig).GetField("_dbParams", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(cfg, new DbParams { DatabaseName = "mxotest_deadly_procs" });
            Store.config = cfg;
            Environment.SetEnvironmentVariable(DeadlyThrowProcsQa.Mode, "1");
            Environment.SetEnvironmentVariable("MXO_DEV_COMMANDS", "1");

            var csr = Player(110, 1, 17000, 495, 6400);
            var neo = Player(2, 1, 17000, 495, 6400);
            var enemy = Player(6114, 2, 16800, 495, 6400);

            Assert.True(TempMods.TryGet(DeadlyThrow, out var effect));
            Assert.Null(DeadlyThrowProcsQa.ProcRandom(neo, enemy, DeadlyThrow));

            DeadlyThrowProcsQa.Dev(csr, new[] { "both" });
            var both = TimedEffects.Instantiate(effect, DeadlyThrowProcsQa.ProcRandom(csr, enemy, DeadlyThrow)!);
            Assert.Contains(both, m => m.Target == TempMods.DazeAbility && m.Seconds == 8f);
            Assert.Contains(both, m => m.Target == TempMods.StaggeredAbility && m.Seconds == 8f);

            DeadlyThrowProcsQa.Dev(csr, new[] { "daze" });
            var daze = TimedEffects.Instantiate(effect, DeadlyThrowProcsQa.ProcRandom(csr, enemy, DeadlyThrow)!);
            Assert.Contains(daze, m => m.Target == TempMods.DazeAbility && m.Seconds == 8f);
            Assert.DoesNotContain(daze, m => m.Target == TempMods.StaggeredAbility);

            DeadlyThrowProcsQa.Dev(csr, new[] { "stagger" });
            var stagger = TimedEffects.Instantiate(effect, DeadlyThrowProcsQa.ProcRandom(csr, enemy, DeadlyThrow)!);
            Assert.DoesNotContain(stagger, m => m.Target == TempMods.DazeAbility);
            Assert.Contains(stagger, m => m.Target == TempMods.StaggeredAbility && m.Seconds == 8f);

            DeadlyThrowProcsQa.Dev(csr, new[] { "none" });
            var none = TimedEffects.Instantiate(effect, DeadlyThrowProcsQa.ProcRandom(csr, enemy, DeadlyThrow)!);
            Assert.DoesNotContain(none, m => m.Target == TempMods.DazeAbility || m.Target == TempMods.StaggeredAbility);
            Assert.Equal(4, none.Count);

            DeadlyThrowProcsQa.Dev(csr, new[] { "off" });
            Assert.Null(DeadlyThrowProcsQa.ProcRandom(csr, enemy, DeadlyThrow));
        }
        finally
        {
            DeadlyThrowProcsQa.ResetForTests();
            Store.config = prevConfig;
            Environment.SetEnvironmentVariable(DeadlyThrowProcsQa.Mode, oldMode);
            Environment.SetEnvironmentVariable("MXO_DEV_COMMANDS", oldDev);
        }
    }

    [Fact]
    public void Level25ClosureDamageAndRootDurationMatchParityModeExpectations()
    {
        var caster = Player(110, 1, 17000, 495, 6400, level: 25);
        ushort[] closure = new ushort[] { 2, 17, SpyStyle, DeadlyThrow, CripplingThrow }
            .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id))
            .Distinct().ToArray();
        AbilityPassives.SetLoaded(caster, closure.Select(id => (id, (ushort)25)));
        CombatStyles.Select(caster, SpyStyle);

        Assert.Equal(28, AbilityStats.Influence(caster, 1034));
        var deadlyItem = new AbilityItem();
        deadlyItem.setAbilityID(DeadlyThrow);
        Assert.Equal((ushort)32, AbilityHandler.AbilityDamage(deadlyItem, new Random(1), characterLevel: 25));
        Assert.Equal((ushort)40, AbilityStats.ScaleAbilityDamage(caster, DeadlyThrow, 32));
        Assert.Equal(33, ConditionalThrownDamage.Value(DeadlyThrow, 25));
        Assert.Equal((ushort)42, AbilityStats.ScaleAbilityDamage(caster, DeadlyThrow, 33));
        Assert.Equal((ushort)1, AbilityStats.ScaleAbilityDamage(caster, CripplingThrow, 1));
        Assert.Equal(15.75f, AbilityControlDurations.Scale(caster, TempMods.RootedAbility, 15f), 4);
    }
}
