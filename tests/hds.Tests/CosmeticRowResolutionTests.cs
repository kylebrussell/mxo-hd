using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
using hds.shared;
using Xunit;

namespace hds.Tests;

/// <summary>
/// The audit's cosmetic rows whose shipped data carries no gameplay executor of their own
/// (docs/ABILITY-COSMETIC-ROWS.md). Loading one must publish exactly its stored passive records and nothing else.
/// </summary>
[Collection("DelayedActions")]
public class CosmeticRowResolutionTests
{
    private const ushort SneakAttackDetection = 577, DebuffAreaRadiusBonus = 1055, DebuffAreaRadius = 177;

    private static WorldClient Player(uint charId = 110, byte alignment = 1, int x = 0, int y = 495, int z = 0, byte yaw = 64)
    {
        var c = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        c.playerData = new ClientData();
        c.playerData.setCharID(charId);
        c.playerData.setEntityId(charId);
        c.playerData.setDistrictId(1);
        c.playerData.setOnWorld(true);
        c.playerData.pvpFlag = true;
        c.playerData.isBot = true;
        c.playerInstance = new PlayerCharacter();
        c.playerInstance.Level.setValue((byte)50);
        c.playerInstance.OrganizationID.setValue(alignment);
        c.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(x, y, z));
        c.playerInstance.YawInterval.setValue(yaw);
        c.playerInstance.Health.setValue((ushort)5000);
        c.playerInstance.MaxHealth.setValue((ushort)5000);
        c.playerInstance.InnerStrengthAvailable.setValue((ushort)2000);
        c.playerInstance.InnerStrengthMax.setValue((ushort)2000);
        c.messageQueue = new MessageQueue();
        c.viewMan = new ViewManager();
        typeof(WorldClient).GetField("lastFlushTick", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(c, long.MaxValue);
        return c;
    }

    /// <summary>Every stat id the catalog can name, so an unexpected contribution cannot hide in an unlisted stat.</summary>
    private static readonly ushort[] AllStats = Enumerable.Range(1, 4095).Select(i => (ushort)i)
        .Where(i => AbilityCatalog.Find(i) != null).ToArray();

    private static Dictionary<ushort, int> Totals(WorldClient client) => AllStats.ToDictionary(s => s, s => AbilityStats.Player(client, s));

    private static Dictionary<ushort, int> ChangedStats(ushort ability, ushort level)
    {
        var baseline = Totals(Player());
        var loaded = Player();
        AbilityPassives.SetLoaded(loaded, new[] { (ability, level) });
        return Totals(loaded).Where(p => p.Value != baseline[p.Key]).ToDictionary(p => p.Key, p => p.Value);
    }

    [Theory]
    [InlineData((ushort)18)]   // Coder: tree container for 24/27/30/36/37/970/1008
    [InlineData((ushort)397)]  // Game Master: no children; only NPCAssassin714 names it as a prerequisite
    [InlineData((ushort)537)]  // Data Miner: tree container for 538-541/621/622
    [InlineData((ushort)538)]  // Data Node Goggles: prerequisite tier of 537 and 539
    [InlineData((ushort)539)]  // Data Tap: prerequisite tier, no dependent
    [InlineData((ushort)1199)] // Organization: tree container for the organization abilities 1163-1198
    public void MetadataOnlyRowsAuthorNoModifierGrantCallbackOrExecutor(ushort ability)
    {
        var entry = AbilityCatalog.Find(ability);
        Assert.NotNull(entry);
        Assert.Empty(entry.Modifiers);
        Assert.False(entry.Has("Ability/TestFunction"));
        Assert.False(entry.Has("Ability/CastingScript"));
        Assert.False(entry.Has("Ability/RequiresTool"));
        Assert.Empty(AbilityPassives.Modifiers(ability));
        Assert.Empty(AbilityProgression.GrantsOf(ability));
        Assert.False(AbilityAuras.Handles(ability));
        Assert.False(AbilityBuffs.TryGet(ability, out _));
        Assert.False(TempMods.Has(ability));
        Assert.False(CoderPets.Handles(ability));
        Assert.False(UtilityAbilities.Handles(ability));
        // The discipline gates its tree; its children are loaded individually and are never granted by it.
        Assert.Empty(ChangedStats(ability, 50));
    }

    [Fact]
    public void CoderDisciplineDoesNotReachTheShippedCoderApplicationCraftingBonuses()
    {
        // CoderApplication(subject, invItem, buffValue) adds seven 60 s crafting records; no shipped template,
        // script or client string names a caller, so loading Coder must leave every crafting total unchanged.
        Assert.Empty(ChangedStats(18, 2));
    }

    [Fact]
    public void ResistInfectionPublishesOnly1055AndLeavesEveryDebuffRadiusUnchanged()
    {
        var modifier = Assert.Single(AbilityPassives.Modifiers(75));
        Assert.Equal(DebuffAreaRadiusBonus, modifier.Target);
        Assert.Equal(new Dictionary<ushort, int> { [DebuffAreaRadiusBonus] = 25 }, ChangedStats(75, 50));
        var caster = Player();
        double[] before = new ushort[] { 333, 38, 60, 97 }.Select(a => AbilityRadius.TargetMetres(caster, a)).ToArray();
        AbilityPassives.SetLoaded(caster, new (ushort, ushort)[] { (75, 50) });
        Assert.Equal(0, AbilityStats.Player(caster, DebuffAreaRadius));
        // GOB 177 links 1055 with its own owner coefficient, which the native getter zero-fills (ABILITY-DEBUFF-RADIUS.md).
        Assert.Equal(before, new ushort[] { 333, 38, 60, 97 }.Select(a => AbilityRadius.TargetMetres(caster, a)).ToArray());
    }

    [Fact]
    public void UncannySensesPublishesOnlySneakAttackDetectionAtItsRankCap()
    {
        var modifier = Assert.Single(AbilityPassives.Modifiers(161));
        Assert.Equal(SneakAttackDetection, modifier.Target);
        Assert.Equal(10, AbilityCatalog.Find(161).Number("Ability/AbilityLevelCap"));
        // Stored learned-level function 2 x 4.0; the stat has no AttributeLink, so detection 204 is untouched.
        Assert.Equal(new Dictionary<ushort, int> { [SneakAttackDetection] = 40 }, ChangedStats(161, 10));
        Assert.Empty(AbilityCatalog.Find(SneakAttackDetection).Modifiers);
        Assert.False(AbilityCatalog.Find(SneakAttackDetection).Has("AbilityAttributeLink/AttributeLink"));
    }

    [Theory]
    [InlineData(SuplexDamage.Suplex)]
    [InlineData(SpyMasterSneakOpeners.DivingFrankensteiner)]
    public void SneakOpenerAdmissionIgnoresTheTargetsSneakAttackDetection(ushort opener)
    {
        // Shipped BaseSneakAttackTestFn sets SUCCESS without reading directObject.abilities[SneakAttackDetectionAbility].
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "hds.sln"))) root = Path.GetDirectoryName(root)!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        var plan = NativeQa.NativeQaModes.Find("ability-parity-spy-instant-sneak-openers").Plan;

        var caster = Player(110, 1, 21550, 1095, 16150, yaw: 64);
        var enemy = Player(6114, 2, 21750, 1095, 16150, yaw: 64);
        caster.viewMan.views.Add(new ClientView(12, 4, enemy.playerData.getEntityId()) { spawnId = 10, viewCreated = true });

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
            WorldServer.Clients["c"] = caster;
            WorldServer.Clients["e"] = enemy;
            AbilityPassives.SetLoaded(caster, plan.Abilities.Select(a => (a, (ushort)50)));
            CombatStyles.Select(caster, CombatStyles.SpyStyle);
            caster.playerData.conditionStateFlags = Stealth.StealthedFlag;

            Assert.Equal(0, AbilityStats.Player(enemy, SneakAttackDetection));
            Assert.False(SpyAbilityRegistry.TryRefuse(caster, opener, 4, 10, out _));
            int detection = AbilityStats.Player(enemy, 204);

            AbilityPassives.SetLoaded(enemy, new (ushort, ushort)[] { (161, 10) });
            Assert.Equal(40, AbilityStats.Player(enemy, SneakAttackDetection));
            Assert.Equal(detection, AbilityStats.Player(enemy, 204));
            Assert.False(SpyAbilityRegistry.TryRefuse(caster, opener, 4, 10, out _));
        }
        finally
        {
            WorldServer.Clients.Clear();
            foreach (var kv in saved) WorldServer.Clients[kv.Key] = kv.Value;
            Store.world = prevWorld;
            Store.worldConfig = prevWorldCfg;
        }
    }

    [Theory]
    [InlineData("ability-parity-metadata-rows", new ushort[] { 18, 397, 537, 538, 539, 1199, 75 })]
    public void NativeModesLoadTheirRowsWithinLevel50Memory(string mode, ushort[] rows)
    {
        var plan = NativeQa.NativeQaModes.RequireKnown(mode).Plan;
        Assert.All(rows, row => Assert.Contains(row, plan.Abilities));
        Assert.True(plan.DevCommands);
        Assert.True(plan.Abilities.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(50));
        foreach (ushort id in plan.Abilities)
            Assert.All(AbilityPrerequisites.Current.Closure(id).Keys, required => Assert.Contains(required, plan.Abilities));
    }
}
