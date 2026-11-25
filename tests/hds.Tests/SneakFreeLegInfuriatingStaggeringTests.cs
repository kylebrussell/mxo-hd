using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
using hds.shared;

namespace hds.Tests;

public class SneakFreeLegInfuriatingStaggeringTests
{
    private const ushort Spy = CombatStyles.SpyStyle;
    private const ushort LegThrow = SpySneakOpeners.LegThrow;                       // 1131
    private const ushort InfuriatingAssault = SpyMasterSneakOpeners.InfuriatingAssault; // 1130
    private const ushort StaggeringThrow = SpyMasterSneakOpeners.StaggeringThrow;   // 348

    private static byte[] Pos(double x, double y, double z)
    {
        var bytes = new byte[24];
        BitConverter.GetBytes(x).CopyTo(bytes, 0);
        BitConverter.GetBytes(y).CopyTo(bytes, 8);
        BitConverter.GetBytes(z).CopyTo(bytes, 16);
        return bytes;
    }

    private static WorldClient Player(uint charId, byte alignment, double x, double y, double z, byte yaw, byte level = 50)
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
        client.playerInstance.YawInterval.setValue(yaw);
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

    [Theory]
    [InlineData(LegThrow, 154, 237, 25, 6)]
    [InlineData(InfuriatingAssault, 131, 201, 40, 4)]
    [InlineData(StaggeringThrow, 190, 292, 45, 6)]
    public void RearStealthThrowsEnforceAdmissionDamageAndTargetedMotion(
        ushort id, ushort expectedBase, ushort expectedScaled, ushort expectedCost, int expectedRange)
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "hds.sln"))) root = Path.GetDirectoryName(root)!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));

        Assert.True(SneakFreeThrowAdmission.Handles(id));
        Assert.Equal(expectedCost, AbilityCosts.Cost(id));
        Assert.Equal(expectedRange, (int)AbilityCatalog.Find(id).Number("Ability/SelectionRange"));

        var caster = Player(110, 1, 21550, 1095, 16150, 64);
        var enemy = Player(6114, 2, 21750, 1095, 16150, 64);
        var friend = Player(6111, 1, 21650, 1095, 16250, 192);
        RegisterView(caster, enemy, 4, 10);
        RegisterView(caster, friend, 5, 11);

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
            WorldServer.Clients["sf-c"] = caster;
            WorldServer.Clients["sf-e"] = enemy;
            WorldServer.Clients["sf-f"] = friend;

            // Style -> Stealthed -> Ally -> Stale -> Frontal cone -> Blocked wall -> Out of range -> Valid rear
            Assert.True(SpyAbilityRegistry.TryRefuse(caster, id, 4, 10, out uint styleReason));
            Assert.Equal(AbilityRefusals.WrongStyle, styleReason);

            var plan = NativeQa.NativeQaModes.Find("ability-parity-leg-infuriating-staggering-throws").Plan;
            AbilityPassives.SetLoaded(caster, plan.Abilities.Select(a => (a, (ushort)50)));
            CombatStyles.Select(caster, Spy);

            Assert.True(SpyAbilityRegistry.TryRefuse(caster, id, 4, 10, out uint sneakReason));
            Assert.Equal(AbilityRefusals.CasterStateRequired, sneakReason);

            caster.playerData.conditionStateFlags |= Stealth.StealthedFlag;
            Assert.True(SpyAbilityRegistry.TryRefuse(caster, id, 5, 11, out uint allyReason));
            Assert.Equal(AbilityRefusals.InvalidTarget, allyReason);
            Assert.True(SpyAbilityRegistry.TryRefuse(caster, id, 4, 99, out uint staleReason));
            Assert.Equal(AbilityRefusals.InvalidTarget, staleReason);

            // Frontal 50-deg cone rejected.
            caster.playerInstance.Position.setValue(Pos(21850, 1095, 16200));
            Assert.True(SpyAbilityRegistry.TryRefuse(caster, id, 4, 10, out uint frontReason));
            Assert.Equal(AbilityRefusals.InvalidTarget, frontReason);

            // Blocked wall rejected.
            caster.playerInstance.Position.setValue(Pos(21950, 1095, 16150));
            Assert.True(SpyAbilityRegistry.TryRefuse(caster, id, 4, 10, out uint wallReason));
            Assert.Equal(AbilityRefusals.TargetNotInSight, wallReason);

            // Out of range rejected.
            caster.playerInstance.Position.setValue(Pos(20800, 1095, 16150));
            Assert.True(SpyAbilityRegistry.TryRefuse(caster, id, 4, 10, out uint farReason));
            Assert.Equal(AbilityRefusals.TargetOutOfRange, farReason);

            // Valid rear stand admitted.
            caster.playerInstance.Position.setValue(Pos(21550, 1095, 16150));
            Assert.False(SpyAbilityRegistry.TryRefuse(caster, id, 4, 10, out _));

            // Exact level-50 VF3 base and +54% scaled damage.
            Assert.True(SpyAbilityRegistry.TryResolveDamage(AbilityCatalog.Find(id), 50, new Random(0), out ushort baseDmg));
            Assert.Equal(expectedBase, baseDmg);
            Assert.Equal(54, AbilityStats.Influence(caster, 1034));
            Assert.Equal(expectedScaled, AbilityStats.ScaleAbilityDamage(caster, id, baseDmg));
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
