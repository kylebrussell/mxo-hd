using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
using hds.shared;

namespace hds.Tests;

public class GhostStrikeParityTests
{
    private const ushort Spy = CombatStyles.SpyStyle;
    private const ushort GhostStrike = GhostStrikeAdmission.Id; // 365

    private static byte[] Pos(double x, double y, double z)
    {
        var bytes = new byte[24];
        BitConverter.GetBytes(x).CopyTo(bytes, 0);
        BitConverter.GetBytes(y).CopyTo(bytes, 8);
        BitConverter.GetBytes(z).CopyTo(bytes, 16);
        return bytes;
    }

    private static WorldClient Player(uint charId, byte alignment, double x, double y, double z)
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
        client.playerInstance.Level.setValue((byte)50);
        client.playerInstance.OrganizationID.setValue(alignment);
        client.playerInstance.Position.setValue(Pos(x, y, z));
        client.playerInstance.Health.setValue((ushort)5000);
        client.playerInstance.MaxHealth.setValue((ushort)5000);
        client.viewMan = new ViewManager();
        client.messageQueue = new MessageQueue();
        typeof(WorldClient).GetField("lastFlushTick", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(client, long.MaxValue);
        return client;
    }

    private static void RegisterView(WorldClient observer, WorldClient target, ushort viewId, ushort spawnId)
    {
        var view = new ClientView(12, viewId, target.playerData.getEntityId()) { spawnId = spawnId, viewCreated = true };
        observer.viewMan.views.Add(view);
    }

    [Fact]
    public void GhostStrikeResolvesShippedFixedThrownBase29AndEnforcesGuardedAdmission()
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "hds.sln"))) root = Path.GetDirectoryName(root)!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));

        Assert.True(GhostStrikeAdmission.Handles(GhostStrike));
        var item = new AbilityItem();
        item.setAbilityID(GhostStrike);
        foreach (int seed in new[] { 1, 7, 99 })
            Assert.Equal((ushort)29, AbilityHandler.AbilityDamage(item, new Random(seed), characterLevel: 50));

        var plan = NativeQa.NativeQaModes.Find("ability-parity-ghost-strike-parity").Plan;
        Assert.True(plan.Abilities.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(50));

        var caster = Player(110, 1, 17000, 495, 6400);
        var enemy = Player(6114, 2, 16800, 495, 6400);
        var friend = Player(6111, 1, 17150, 495, 6400);
        var farEnemy = Player(6112, 2, 17000, 495, 7900);
        var wallCaster = Player(110, 1, 28650, 895, 12700);
        var acrossWall = Player(6113, 2, 29150, 895, 12700);

        RegisterView(caster, enemy, 4, 10);
        RegisterView(caster, friend, 5, 11);
        RegisterView(caster, farEnemy, 6, 12);
        RegisterView(wallCaster, acrossWall, 7, 13);

        var prevWorld = Store.world;
        var prevWorldCfg = Store.worldConfig;
        WorldServer.Clients ??= new Dictionary<string, WorldClient>();
        var saved = new Dictionary<string, WorldClient>(WorldServer.Clients);
        try
        {
            Store.world = (WorldServer)RuntimeHelpers.GetUninitializedObject(typeof(WorldServer));
            var cfg = (WorldConfig)RuntimeHelpers.GetUninitializedObject(typeof(WorldConfig));
            cfg.IsPvpServer = true;
            cfg.PvpMaxSafeLevel = 15;
            Store.worldConfig = cfg;
            WorldServer.Clients.Clear();
            WorldServer.Clients["gs-c"] = caster;
            WorldServer.Clients["gs-e"] = enemy;
            WorldServer.Clients["gs-f"] = friend;
            WorldServer.Clients["gs-far"] = farEnemy;
            WorldServer.Clients["gs-wc"] = wallCaster;
            WorldServer.Clients["gs-aw"] = acrossWall;

            Assert.True(SpyAbilityRegistry.TryRefuse(caster, GhostStrike, 4, 10, out uint styleReason));
            Assert.Equal(AbilityRefusals.WrongStyle, styleReason);

            AbilityPassives.SetLoaded(caster, plan.Abilities.Select(a => (a, (ushort)50)));
            CombatStyles.Select(caster, Spy);
            AbilityPassives.SetLoaded(wallCaster, plan.Abilities.Select(a => (a, (ushort)50)));
            CombatStyles.Select(wallCaster, Spy);

            Assert.Equal(29, AbilityStats.Influence(caster, 1034));
            Assert.Equal((ushort)37, AbilityStats.ScaleAbilityDamage(caster, GhostStrike, 29));
            Assert.Equal((ushort)30, TypedAbilityResistance.Amount(37, 50, 50, 75));

            Assert.True(SpyAbilityRegistry.TryRefuse(caster, GhostStrike, 0, null, out uint emptyReason));
            Assert.Equal(AbilityRefusals.InvalidTarget, emptyReason);
            Assert.True(SpyAbilityRegistry.TryRefuse(caster, GhostStrike, 5, 11, out uint allyReason));
            Assert.Equal(AbilityRefusals.InvalidTarget, allyReason);
            Assert.True(SpyAbilityRegistry.TryRefuse(caster, GhostStrike, 4, 99, out uint staleReason));
            Assert.Equal(AbilityRefusals.InvalidTarget, staleReason);
            Assert.True(SpyAbilityRegistry.TryRefuse(caster, GhostStrike, 6, 12, out uint farReason));
            Assert.Equal(AbilityRefusals.TargetOutOfRange, farReason);
            Assert.True(SpyAbilityRegistry.TryRefuse(wallCaster, GhostStrike, 7, 13, out uint wallReason));
            Assert.Equal(AbilityRefusals.TargetNotInSight, wallReason);
            Assert.False(SpyAbilityRegistry.TryRefuse(caster, GhostStrike, 4, 10, out _));

            var combat = CombatManager.GetHandler(caster);
            combat.isCombatRunning = true;
            try
            {
                Assert.True(SpyAbilityRegistry.TryRefuse(caster, GhostStrike, 4, 10, out uint combatReason));
                Assert.Equal(AbilityRefusals.MustBeOutsideCombat, combatReason);
            }
            finally
            {
                combat.isCombatRunning = false;
            }
        }
        finally
        {
            WorldServer.Clients.Clear();
            foreach (var kv in saved) WorldServer.Clients[kv.Key] = kv.Value;
            Store.world = prevWorld;
            Store.worldConfig = prevWorldCfg;
        }
    }
}
