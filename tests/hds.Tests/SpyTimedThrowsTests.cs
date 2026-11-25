using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
using hds.shared;
namespace hds.Tests;

[Collection("ConcentrationEnvironment")]
public class SpyTimedThrowsTests : IDisposable
{
    private const ushort Blinding = SpyTimedThrows.BlindingThrow, Paralyzing = SpyTimedThrows.ParalyzingThrow;
    readonly Dictionary<string, WorldClient>? oldClients = WorldServer.Clients;

    public SpyTimedThrowsTests() => WorldServer.Clients = new();
    public void Dispose() => WorldServer.Clients = oldClients!;

    [Theory]
    [InlineData(Blinding)]
    [InlineData(Paralyzing)]
    public void ShippedTemplatesAreSelectedSpyStyleTimedSpecialsWithANamedSuccessMove(ushort id)
    {
        var entry = AbilityCatalog.Find(id)!;
        Assert.Equal(2.0, entry.Number("Ability/CastingTime"));
        Assert.Equal(2, entry.Number("Ability/CastWhenUsed"));
        Assert.Equal(16, entry.Number("Ability/SelectionRange"));
        Assert.Equal(1, entry.Number("Ability/CombatSpecialMove"));
        Assert.Equal(1, entry.Number("Ability/HasSuccessInterlockMove"));
        Assert.Equal(CombatStyles.SpyStyle, CombatStyles.RequiredStyle(id));
        Assert.True(CombatStyles.IsSpecialMove(id));
    }

    [Fact]
    public void BothThrowsUseTheirOwnHandlerAndKeepTheirSourcedDamage()
    {
        Assert.True(SpyAbilityRegistry.TryGet(Blinding, out var blinding));
        Assert.True(SpyAbilityRegistry.TryGet(Paralyzing, out var paralyzing));
        Assert.IsType(blinding.GetType(), paralyzing);
        Assert.Equal(new ushort[] { Blinding, Paralyzing }, blinding.AbilityIds);
        Assert.False(MasterKnifeThrowerBatch.Handles(Blinding));

        Assert.True(SpyAbilityRegistry.TryResolveDamage(AbilityCatalog.Find(Paralyzing), 50, new Random(1), out ushort damage));
        Assert.Equal(30, damage);
        // 282's stored health records are the ThrownDOT and the Off Balance initializer, never a direct hit.
        Assert.False(SpyAbilityRegistry.TryResolveDamage(AbilityCatalog.Find(Blinding), 50, new Random(1), out _));
    }

    [Theory]
    [InlineData(Blinding, "ally", AbilityRefusals.InvalidTarget)]
    [InlineData(Blinding, "stale", AbilityRefusals.InvalidTarget)]
    [InlineData(Blinding, "far", AbilityRefusals.TargetOutOfRange)]
    [InlineData(Blinding, "wall", AbilityRefusals.TargetNotInSight)]
    [InlineData(Blinding, "legal", 0u)]
    [InlineData(Paralyzing, "ally", AbilityRefusals.InvalidTarget)]
    [InlineData(Paralyzing, "stale", AbilityRefusals.InvalidTarget)]
    [InlineData(Paralyzing, "far", AbilityRefusals.TargetOutOfRange)]
    [InlineData(Paralyzing, "wall", AbilityRefusals.TargetNotInSight)]
    [InlineData(Paralyzing, "legal", 0u)]
    public void SelectedOpponentRangeAndSightAreCheckedBeforeCost(ushort id, string state, uint reason)
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "data", "sector_properties.csv")))
            root = Path.GetDirectoryName(root)!;
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
        // Clear rear approach 21550 -> 21750 (FreedomToJointsMode); 21750 -> 22350 crosses a solid wall.
        double casterX = state == "wall" ? 22350 : 21550;
        double targetX = state == "far" ? 21550 + 1700 : 21750;
        caster.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(casterX, 1095, 16150));
        target.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(targetX, 1095, 16150));
        WorldServer.Clients.Add("caster", caster);
        WorldServer.Clients.Add("target", target);
        var view = caster.viewMan.addView(111, 12);
        view.viewCreated = true;
        view.spawnId = 9;

        bool refused = SpyAbilityRegistry.TryRefuse(caster, id, view.ViewID, (ushort)(state == "stale" ? 8 : 9), out uint actual);
        Assert.Equal(reason != 0, refused);
        Assert.Equal(reason, actual);
    }

    [Theory]
    [InlineData(Blinding, 291u, "Blinding Throw SR")]
    [InlineData(Paralyzing, 292u, "Paralyzing Throw SR")]
    public void ALandedQueuedThrowPlaysItsNamedMoveAndAMissKeepsTheOrdinaryExchange(ushort id, uint move, string name)
    {
        foreach (var tactic in new[] { CombatTactic.Speed, CombatTactic.Power, CombatTactic.Block, CombatTactic.Grab })
        {
            var landed = CombatAnimations.Special(id, true, tactic);
            Assert.Equal(move, landed?.Id);
            Assert.Equal(name, landed?.Name);
            Assert.Equal(0x100u, landed?.Flags);
            Assert.Null(CombatAnimations.Special(id, false, tactic));
        }
    }

    [Theory]
    [InlineData(Blinding)]
    [InlineData(Paralyzing)]
    public void OnlyTheStoredSpyStyleLetsTheCompletedCastBecomeTheNextMove(ushort id)
    {
        var caster = Client();
        var ability = new AbilityItem();
        ability.setAbilityID(id);
        ability.setCastingTime(2f);

        caster.playerData.passives.Loaded[CombatStyles.SpyStyle] = 50;
        caster.playerData.passives.Loaded[CombatStyles.HackerStyle] = 50;
        caster.playerData.combatStyle = CombatStyles.HackerStyle;
        Assert.False(SpyTimedThrows.CanQueue(caster, id));
        Assert.False(CombatAnimations.CanResolveQueuedAbility(caster, ability));

        caster.playerData.combatStyle = CombatStyles.SpyStyle;
        Assert.True(SpyTimedThrows.CanQueue(caster, id));
        Assert.True(CombatAnimations.CanResolveQueuedAbility(caster, ability));

        // Other timed casts still never resolve as a queued interlock move.
        var other = new AbilityItem();
        other.setAbilityID(281);
        other.setCastingTime(2f);
        Assert.False(SpyTimedThrows.CanQueue(caster, 281));
        Assert.False(CombatAnimations.CanResolveQueuedAbility(caster, other));
    }

    static WorldClient Client(ushort inner = 500)
    {
        var c = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient)); c.Alive = true;
        typeof(WorldClient).GetField("lastUsedTime", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(c, TimeUtils.getUnixTimeUint32());
        typeof(WorldClient).GetField("lastFlushTick", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(c, long.MaxValue);
        typeof(WorldClient).GetField("<TransportFence>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(c, new WorldSendFence());
        c.playerData = new ClientData(); c.playerData.setCharID(110); c.playerData.setDistrictId(1); c.playerData.setOnWorld(true);
        c.playerInstance = new PlayerCharacter(); c.playerInstance.Level.setValue((byte)50); c.playerInstance.Health.setValue((ushort)1000); c.playerInstance.MaxHealth.setValue((ushort)1000);
        c.playerInstance.InnerStrengthAvailable.setValue(inner); c.playerInstance.InnerStrengthMax.setValue((ushort)1000); c.messageQueue = new(); c.viewMan = new(); return c;
    }
}
