using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
using hds.shared;
using hds.world.Structures;

namespace hds.Tests;

[Collection("AbilityParityWorld")]
public class HackerBatchBParityTests
{
    private static WorldClient Client(byte level = 50)
    {
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.playerData.setDistrictId(1);
        client.playerInstance = new PlayerCharacter();
        client.playerInstance.Level.setValue(level);
        client.playerInstance.MaxHealth.setValue((ushort)1000);
        client.playerInstance.Health.setValue((ushort)400);
        client.playerInstance.InnerStrengthMax.setValue((ushort)200);
        client.playerInstance.InnerStrengthAvailable.setValue((ushort)100);
        client.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(0, 495, 0));
        client.messageQueue = new MessageQueue();
        client.viewMan = new ViewManager();
        typeof(WorldClient).GetField("lastFlushTick", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(client, long.MaxValue);
        return client;
    }

    [Fact]
    public void RepairRsi234HealsForTwentyAndCapsAtTargetMaximum()
    {
        var caster = Client();
        AbilitySupport.Land(caster, 234, 2);
        Assert.Equal(420, PvpCombat.HealthOf(caster));

        caster.playerInstance.Health.setValue((ushort)991);
        AbilitySupport.Land(caster, 234, 2);
        Assert.Equal(1000, PvpCombat.HealthOf(caster));

        caster.playerData.isDead = true;
        caster.playerInstance.Health.setValue((ushort)0);
        AbilitySupport.Land(caster, 234, 2);
        Assert.Equal(0, PvpCombat.HealthOf(caster));
    }

    [Fact]
    public void CombatAura696SnapshotsBothDamageTypesFromTheCasterUntilExpiry()
    {
        var caster = Client(20);
        var member = Client();
        Assert.True(AbilityBuffs.Apply(member, 696, caster));
        Assert.Equal(110, AbilityStats.ScaleTypedAbilityDamage(member, 0, 100));
        Assert.Equal(110, AbilityStats.ScaleTypedAbilityDamage(member, 1, 100));
        Assert.Equal(100, AbilityStats.ScaleTypedAbilityDamage(member, 2, 100));
        caster.playerInstance.Level.setValue((byte)50);

        var active = member.playerData.activeBuffs[696];
        TimedEffects.RunDue(active.Started + 4.99);
        Assert.Equal(110, AbilityStats.ScaleTypedAbilityDamage(member, 0, 100));
        TimedEffects.RunDue(active.Started + 5.01);
        Assert.Equal(100, AbilityStats.ScaleTypedAbilityDamage(member, 0, 100));
        Assert.Equal(100, AbilityStats.ScaleTypedAbilityDamage(member, 1, 100));
    }

    [Fact]
    public void CombatAura696UsesTheChildRadiusOnEveryPulse()
    {
        var caster = Client(50);
        var edge = Client(30);
        var beyond = Client(30);
        var outsider = Client();
        var downed = Client();
        var previous = WorldServer.Clients;
        var players = new Dictionary<string, WorldClient>
        {
            ["caster"] = caster, ["edge"] = edge, ["beyond"] = beyond,
            ["outsider"] = outsider, ["downed"] = downed
        };
        uint nextId = 7090;
        foreach (var player in players.Values)
        {
            player.playerData.setCharID(nextId++);
            player.playerData.setOnWorld(true);
        }
        caster.LivenessClock = () => 0;
        typeof(WorldClient).GetField("alive", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(caster, true);
        typeof(WorldClient).GetField("<TransportFence>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(caster, new WorldSendFence());
        edge.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(600, 495, 0));
        beyond.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(0, 495, 601));
        outsider.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(150, 495, 0));
        downed.playerData.isDead = true;
        var team = SocialGroups.Instance.CreateTeam(caster.playerData.getCharID(), "caster");
        try
        {
            WorldServer.Clients = players;
            SocialGroups.Instance.AddTeamMember(team.Id, edge.playerData.getCharID(), "edge");
            SocialGroups.Instance.AddTeamMember(team.Id, beyond.playerData.getCharID(), "beyond");
            SocialGroups.Instance.AddTeamMember(team.Id, downed.playerData.getCharID(), "downed");

            caster.playerData.passives.Loaded[485] = 50;
            AbilityAuras.Start(caster, 485);
            Assert.True(AbilityBuffs.IsActive(caster, 696));
            Assert.True(AbilityBuffs.IsActive(edge, 696));
            Assert.False(AbilityBuffs.IsActive(beyond, 696));
            Assert.False(AbilityBuffs.IsActive(outsider, 696));
            Assert.False(AbilityBuffs.IsActive(downed, 696));
            Assert.Equal(125, AbilityStats.ScaleTypedAbilityDamage(edge, 0, 100));
            Assert.Equal(125, AbilityStats.ScaleTypedAbilityDamage(edge, 1, 100));

            edge.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(601, 495, 0));
            beyond.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(0, 495, 600));
            var firstPulse = edge.playerData.activeBuffs[696];
            AbilityAuras.Tick(caster, 485, 1);
            Assert.Same(firstPulse, edge.playerData.activeBuffs[696]);
            Assert.True(AbilityBuffs.IsActive(beyond, 696));
            Assert.False(AbilityBuffs.IsActive(outsider, 696));
            Assert.False(AbilityBuffs.IsActive(downed, 696));
            Assert.Equal(85, AbilityHandler.CurrentInnerStrength(caster));

            AbilityAuras.Stop(caster, 485);
            Assert.True(AbilityBuffs.IsActive(beyond, 696));
            var lastPulse = beyond.playerData.activeBuffs[696];
            TimedEffects.RunDue(lastPulse.Started + 5.01);
            Assert.All(players.Values, player => Assert.False(AbilityBuffs.IsActive(player, 696)));
        }
        finally
        {
            AbilityAuras.Clear(caster, announce: false);
            foreach (var player in players.Values) AbilityBuffs.RemoveAll(player);
            SocialGroups.Instance.DisbandTeam(team.Id);
            WorldServer.Clients = previous;
        }
    }

    [Fact]
    public void CombatAuraTwoOverlappingSourcesReplaceOneChildAndKeepIndependentUpkeep()
    {
        const ushort parent = 485, child = 696;
        var first = Participant(77670, 0, 40);
        var second = Participant(77671, 1200, 50);
        var teammate = Participant(77672, 600, 50);
        var players = new[] { first, second, teammate };
        var groups = SocialGroups.Instance;
        var team = groups.CreateTeam(77670, "FirstAura");
        groups.AddTeamMember(team.Id, 77671, "SecondAura");
        groups.AddTeamMember(team.Id, 77672, "SharedRecipient");
        var previousClients = WorldServer.Clients;
        var previousWorld = Store.world;
        WorldServer.Clients = players.ToDictionary(player => player.playerData.getCharID().ToString());
        Store.world = (WorldServer)RuntimeHelpers.GetUninitializedObject(typeof(WorldServer));
        try
        {
            AbilityPassives.SetLoaded(first, new[] { (parent, (ushort)40) });
            AbilityPassives.SetLoaded(second, new[] { (parent, (ushort)50) });
            Assert.Equal(15, AbilityCosts.Cost(parent));
            var reuse = AbilityCooldowns.BuildTimer(parent, AbilityCooldowns.OwnTimer, 15, true);

            AbilityAuras.Start(first, parent);
            var firstChild = teammate.playerData.activeBuffs[child];
            AssertDamage(120);
            Assert.False(AbilityBuffs.IsActive(second, child));
            Assert.False(HasReuse(first, reuse));

            AbilityAuras.Start(second, parent);
            var secondChild = teammate.playerData.activeBuffs[child];
            Assert.NotSame(firstChild, secondChild);
            AssertDamage(125);
            Assert.Equal(5, secondChild.Definition.Seconds);
            Assert.False(HasReuse(second, reuse));

            AbilityAuras.Tick(first, parent, 1);
            Assert.Equal(85, AbilityHandler.CurrentInnerStrength(first));
            Assert.Equal(100, AbilityHandler.CurrentInnerStrength(second));
            AssertDamage(120);
            var firstRefresh = teammate.playerData.activeBuffs[child];

            Assert.True(AbilityAuras.Stop(first, parent));
            Assert.True(HasReuse(first, reuse));
            AbilityAuras.Tick(first, parent, 1);
            Assert.Equal(85, AbilityHandler.CurrentInnerStrength(first));
            Assert.Same(firstRefresh, teammate.playerData.activeBuffs[child]);
            AssertDamage(120);

            AbilityAuras.Tick(second, parent, 1);
            Assert.Equal(85, AbilityHandler.CurrentInnerStrength(second));
            var finalChild = teammate.playerData.activeBuffs[child];
            Assert.NotSame(firstRefresh, finalChild);
            AssertDamage(125);
            AbilityPassives.SetLoaded(second, Array.Empty<(ushort, ushort)>());
            Assert.False(AbilityAuras.IsOn(second, parent));
            Assert.True(HasReuse(second, reuse));

            TimedEffects.RunDue(finalChild.Started + 4.99);
            AssertDamage(125);
            TimedEffects.RunDue(finalChild.Started + 5.01);
            Assert.False(AbilityBuffs.IsActive(teammate, child));
            AssertDamage(100);
        }
        finally
        {
            foreach (var player in players)
            {
                AbilityAuras.Clear(player, announce: false);
                AbilityBuffs.RemoveAll(player);
            }
            groups.DisbandTeam(team.Id);
            Store.world = previousWorld;
            WorldServer.Clients = previousClients;
        }

        void AssertDamage(int expected)
        {
            Assert.Equal(expected, AbilityStats.ScaleTypedAbilityDamage(teammate, 0, 100));
            Assert.Equal(expected, AbilityStats.ScaleTypedAbilityDamage(teammate, 1, 100));
            Assert.Equal(100, AbilityStats.ScaleTypedAbilityDamage(teammate, 2, 100));
        }

        static bool HasReuse(WorldClient player, byte[] expected) => player.messageQueue.RPCMessagesQueue
            .OfType<SequencedMessage>().Any(message => message.content.AsSpan(1).SequenceEqual(expected));

        static WorldClient Participant(uint id, int x, byte level)
        {
            var player = Client(level);
            player.playerData.setCharID(id);
            player.playerData.setEntityId(id);
            player.playerData.setOnWorld(true);
            player.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(x, 495, 0));
            player.LivenessClock = () => 0;
            const BindingFlags instance = BindingFlags.NonPublic | BindingFlags.Instance;
            typeof(WorldClient).GetField("alive", instance)!.SetValue(player, true);
            typeof(WorldClient).GetField("<TransportFence>k__BackingField", instance)!.SetValue(player, new WorldSendFence());
            return player;
        }
    }

    [Fact]
    public void ForceMultiplier1091SweepsOnlyCleansableEffectsAndStopsOnClassReplacement()
    {
        var player = Client();
        var effects = TimedEffects.ForPlayer(player);
        Random previous = AbilityHostile.PulseRandom;
        try
        {
            Assert.True(TimedEffects.ApplyToPlayer(player, 361, "Enemy"));
            AbilityHostile.PulseRandom = new FixedRoll(5);
            AbilitySupport.Land(player, 1091, 2);
            var pulse = Assert.Single(effects.Effects.Snapshot(), m => m.Parent == 1091);
            Assert.Equal(5, pulse.Value);
            Assert.Equal(5, pulse.NextTick - pulse.Start, 4);
            Assert.Equal(32, AbilityCosts.Effective(player, 314));
            Assert.Equal(78, AbilityCastInterrupts.CastThroughPercent(0, 1000, (short)AbilityStats.Player(player, 564)));
            Assert.True(effects.Effects.HasParent(361));

            TimedEffects.RunDue(pulse.Start + 5.01);
            Assert.True(effects.Effects.HasParent(361));
            AbilityHostile.PulseRandom = new FixedRoll(4);
            TimedEffects.RunDue(pulse.Start + 10.01);
            Assert.False(effects.Effects.HasParent(361));
            Assert.True(AbilityBuffs.Apply(player, 1094));
            Assert.False(effects.Effects.HasParent(1091));
            Assert.Equal(35, AbilityCosts.Effective(player, 314));
        }
        finally
        {
            AbilityHostile.PulseRandom = previous;
            AbilityBuffs.RemoveAll(player);
        }
    }

    [Fact]
    public void FastHealing46AppliesOnlyToTheSelectedLivingFriendlyPlayerInRange()
    {
        var caster = Client();
        var friend = Client();
        var foe = Client();
        foreach (var (player, id, organization) in new[]
        {
            (caster, 7010u, (byte)1), (friend, 7011u, (byte)1), (foe, 7012u, (byte)2)
        })
        {
            player.playerData.setOnWorld(true);
            player.playerData.setEntityId(id);
            player.playerData.setCharID(id);
            player.playerData.pvpFlag = true;
            player.playerInstance.OrganizationID.setValue(organization);
        }
        friend.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(1000, 495, 0));
        foe.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(300, 495, 0));
        var friendlyView = caster.viewMan.addView(7011, 1);
        var hostileView = caster.viewMan.addView(7012, 1);
        friendlyView.viewCreated = hostileView.viewCreated = true;
        var previous = WorldServer.Clients;
        var previousWorld = Store.world;
        try
        {
            Store.world = (WorldServer)RuntimeHelpers.GetUninitializedObject(typeof(WorldServer));
            WorldServer.Clients = new() { ["caster"] = caster, ["friend"] = friend, ["foe"] = foe };
            Assert.Equal(PvpRules.Verdict.Allowed, PvpCombat.Check(caster, foe));
            AbilitySupport.Land(caster, 46, hostileView.ViewID);
            Assert.Equal(0, AbilityStats.Influence(foe, 112));
            Assert.Same(friend, Assert.Single(AbilitySupport.Targets(caster, 46, friendlyView.ViewID)));
            AbilitySupport.Land(caster, 46, friendlyView.ViewID);
            Assert.Equal(40, AbilityStats.Influence(friend, 112));
            Assert.Equal(0, AbilityStats.Influence(caster, 112));
            Assert.True(AbilityBuffs.Remove(friend, 46));
            friend.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(1001, 495, 0));
            Assert.Empty(AbilitySupport.Targets(caster, 46, friendlyView.ViewID));
            friend.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(1000, 495, 0));
            friend.playerData.isDead = true;
            Assert.Empty(AbilitySupport.Targets(caster, 46, friendlyView.ViewID));
            friend.playerData.isDead = false;
            friendlyView.viewCreated = false;
            Assert.Empty(AbilitySupport.Targets(caster, 46, friendlyView.ViewID));
        }
        finally
        {
            AbilityBuffs.RemoveAll(friend);
            WorldServer.Clients = previous;
            Store.world = previousWorld;
        }
    }

    [Fact]
    public void GuardianPatcherLoadsARecipientIndependentPassiveAndGrantsOnlyASelfUpgrade()
    {
        var player = Client();
        Assert.False(AbilitySupport.Handles(51));
        AbilityPassives.SetLoaded(player, new[] { ((ushort)51, (ushort)50) });
        Assert.Equal(1010, NumericalUtils.ByteArrayToUint16(player.playerInstance.MaxHealth.getValue(), 1));
        Assert.True(AbilityPassives.IsLoaded(player, 1094));
        Assert.Same(player, Assert.Single(AbilitySupport.Targets(player, 1094, 123)));
        AbilitySupport.Land(player, 1094, 123);
        Assert.Equal(20, AbilityStats.Player(player, 520));
        Assert.True(AbilityBuffs.Remove(player, 1094));
        AbilityPassives.SetLoaded(player, Array.Empty<(ushort, ushort)>());
        Assert.False(AbilityPassives.IsLoaded(player, 1094));
        Assert.Equal(1000, NumericalUtils.ByteArrayToUint16(player.playerInstance.MaxHealth.getValue(), 1));
    }

    [Fact]
    public void FastHealing46IncreasesTheActualHealthRegenTickForTenMinutes()
    {
        var player = Client();
        player.playerData.setOnWorld(true);
        AbilitySupport.Land(player, 46, 2);
        var active = player.playerData.activeBuffs[46];
        HealthRegen.Tick(player, 1);
        Assert.Equal(414, PvpCombat.HealthOf(player));

        TimedEffects.RunDue(active.Started + 599.99);
        Assert.True(AbilityBuffs.IsActive(player, 46));
        TimedEffects.RunDue(active.Started + 600.01);
        HealthRegen.Tick(player, 1);
        Assert.Equal(424, PvpCombat.HealthOf(player));
        Assert.False(AbilityBuffs.IsActive(player, 46));
    }

    [Theory]
    [InlineData(51, 1010, 1094)]
    [InlineData(70, 1230, 0)]
    public void LoadedGuardianPassivesChangeMaximumHealthAndUnloadCleanly(ushort id, int maximum, ushort grant)
    {
        var player = Client();
        AbilityPassives.SetLoaded(player, new[] { (id, (ushort)50) });
        Assert.Equal(maximum, NumericalUtils.ByteArrayToUint16(player.playerInstance.MaxHealth.getValue(), 1));
        if (grant != 0) Assert.True(AbilityPassives.IsLoaded(player, grant));
        if (id == 70) Assert.Equal(10, AbilityStats.Player(player, 1052));
        AbilityPassives.SetLoaded(player, Array.Empty<(ushort, ushort)>());
        if (grant != 0) Assert.False(AbilityPassives.IsLoaded(player, grant));
        Assert.Equal(1000, NumericalUtils.ByteArrayToUint16(player.playerInstance.MaxHealth.getValue(), 1));
    }

    [Theory]
    [InlineData(false, 475)]
    [InlineData(true, 0)]
    public void Firewall68SetsArmorAndResistsFourDamageTypesThenClearsAndTermHealsOnlyLivingPlayers(bool dead, int finalHealth)
    {
        var player = Client();
        AbilitySupport.Land(player, 68, 2);
        Assert.Equal(0x100, AbilityBuffs.DissemblingBits(player));
        Assert.Equal(40, AbilityStats.Player(player, 157));
        Assert.Equal(10, AbilityStats.Toughness(player, false));
        Assert.Equal(10, AbilityStats.Toughness(player, true));
        Assert.Equal(10, AbilityStats.Player(player, 1033));
        if (dead)
        {
            player.playerData.isDead = true;
            player.playerInstance.Health.setValue((ushort)0);
        }
        Assert.True(AbilityBuffs.Remove(player, 68));
        Assert.Equal(0, AbilityBuffs.DissemblingBits(player));
        Assert.Equal(0, AbilityStats.Player(player, 157));
        Assert.Equal(0, AbilityStats.Toughness(player, false));
        Assert.Equal(0, AbilityStats.Toughness(player, true));
        Assert.Equal(0, AbilityStats.Player(player, 1033));
        Assert.Equal(finalHealth, PvpCombat.HealthOf(player));
    }

    [Fact]
    public void AccuracyBuff173UsesCasterLevelAndCappedValuesAcrossCombatTypes()
    {
        var player = Client();
        var caster = Client(20);
        Assert.True(AbilityBuffs.Apply(player, 173, caster));
        Assert.Equal(25, AbilityStats.Attack(player, false));
        Assert.Equal(25, AbilityStats.Attack(player, true));
        Assert.Equal(25, AbilityStats.ThrownAccuracy(player));

        caster.playerInstance.Level.setValue((byte)50);
        Assert.True(AbilityBuffs.Apply(player, 173, caster));
        Assert.Equal(26, AbilityStats.Attack(player, false));
        Assert.Equal(26, AbilityStats.Attack(player, true));
        Assert.Equal(26, AbilityStats.ThrownAccuracy(player));
        AbilityBuffs.Remove(player, 173);
        Assert.Equal(0, AbilityStats.ThrownAccuracy(player));
    }

    [Theory]
    [InlineData(361, 0, -27, -27, 0, 0, 35)]
    [InlineData(480, -61, -61, 0, -31, -31, 25)]
    public void HostileAccuracyDebuffsModifyOnlyTheirTypesUntilExpiry(ushort id, int melee, int ballistic, int thrown, int meleeDefense, int ballisticDefense, int seconds)
    {
        var target = Client();
        Assert.True(TimedEffects.ApplyToPlayer(target, id, "Enemy"));
        Assert.Equal(melee, AbilityStats.Attack(target, false));
        Assert.Equal(ballistic, AbilityStats.Attack(target, true));
        Assert.Equal(thrown, AbilityStats.ThrownAccuracy(target));
        Assert.Equal(meleeDefense, AbilityStats.Defense(target, false));
        Assert.Equal(ballisticDefense, AbilityStats.Defense(target, true));

        var first = TimedEffects.ForPlayer(target).Effects.Snapshot().First();
        TimedEffects.RunDue(first.Start + seconds + .01);
        Assert.Equal(0, AbilityStats.Attack(target, false));
        Assert.Equal(0, AbilityStats.Attack(target, true));
        Assert.Equal(0, AbilityStats.ThrownAccuracy(target));
        Assert.Equal(0, AbilityStats.Defense(target, false));
        Assert.Equal(0, AbilityStats.Defense(target, true));
    }

    [Fact]
    public void InsidiousCode360ChangesMeleeDamageAndMeleeAndViralResistanceUntilExpiry()
    {
        var target = Client();
        Assert.True(TimedEffects.ApplyToPlayer(target, 360, "Enemy"));
        Assert.Equal(86, AbilityStats.ScaleTypedAbilityDamage(target, 0, 100));
        Assert.Equal(100, AbilityStats.ScaleTypedAbilityDamage(target, 1, 100));
        Assert.Equal(-100, AbilityStats.Toughness(target, false));
        Assert.Equal(-100, AbilityStats.Player(target, 157));

        var first = TimedEffects.ForPlayer(target).Effects.Snapshot().First();
        TimedEffects.RunDue(first.Start + 24.01);
        Assert.Equal(100, AbilityStats.ScaleTypedAbilityDamage(target, 0, 100));
        Assert.Equal(0, AbilityStats.Toughness(target, false));
        Assert.Equal(0, AbilityStats.Player(target, 157));
    }

    [Fact]
    public void GaussianBlur49ClearsItsVisibleBitAndEvasionModifierAtExpiry()
    {
        var player = Client();
        AbilitySupport.Land(player, 49, 2);
        Assert.Equal(0x40, AbilityBuffs.DissemblingBits(player) & 0x40);
        Assert.Equal(25, AbilityStats.Player(player, 520));
        var buff = player.playerData.activeBuffs[49];
        TimedEffects.RunDue(buff.Started + 600.01);
        Assert.Equal(0, AbilityBuffs.DissemblingBits(player) & 0x40);
        Assert.Equal(0, AbilityStats.Player(player, 520));
    }

    [Fact]
    public void ExecuteProgram45ChangesTheActualInnerStrengthMaximumAndRegen()
    {
        var player = Client();
        AbilityPassives.SetLoaded(player, new[] { ((ushort)45, (ushort)50) });
        Assert.Equal(220, AbilityHandler.MaxInnerStrength(player));
        Assert.Equal((ushort)108, AbilityHandler.RegenerateInnerStrength(player));
        Assert.Equal(5, AbilityStats.Player(player, 1049));
        Assert.Equal(5, AbilityStats.Player(player, 1090));
        AbilityPassives.SetLoaded(player, Array.Empty<(ushort, ushort)>());
        Assert.Equal(200, AbilityHandler.MaxInnerStrength(player));
        Assert.Equal(0, AbilityStats.Player(player, 1049));
    }

    [Fact]
    public void HackerStyle1006ContributesOnlyWhileSelected()
    {
        var player = Client();
        AbilityPassives.SetLoaded(player, new[] { ((ushort)17, (ushort)50), ((ushort)1006, (ushort)50) });
        int baselineViral = AbilityStats.ViralAccuracy(player);
        int baselineMelee = AbilityStats.Defense(player, false);
        CombatStyles.Select(player, 1006);
        Assert.Equal(baselineViral + 70, AbilityStats.ViralAccuracy(player));
        Assert.Equal(baselineMelee + 47, AbilityStats.Defense(player, false));
        Assert.Equal(47, AbilityStats.Defense(player, true));
        Assert.Equal(47, AbilityStats.Player(player, 1011));
        Assert.Equal(47, AbilityStats.Player(player, 183));
        Assert.Equal(64, AbilityStats.Player(player, 1005));
        Assert.Equal(126, AbilityStats.ScaleTypedAbilityDamage(player, 2, 100));
        Assert.Equal(25, AbilityHealing.Amount(player, player, 234));
        CombatStyles.Select(player, 17);
        Assert.Equal(baselineViral, AbilityStats.ViralAccuracy(player));
        Assert.Equal(20, AbilityHealing.Amount(player, player, 234));
    }

    [Fact]
    public void NetworkAttacker483GrantsOnlyWhileLoadedAndFeedsViralAccuracy()
    {
        var player = Client();
        AbilityPassives.SetLoaded(player, new[] { ((ushort)483, (ushort)50) });
        Assert.True(AbilityPassives.IsLoaded(player, 1080));
        Assert.Equal(2, AbilityStats.ViralAccuracy(player));
        AbilityPassives.SetLoaded(player, Array.Empty<(ushort, ushort)>());
        Assert.False(AbilityPassives.IsLoaded(player, 1080));
        Assert.Equal(0, AbilityStats.ViralAccuracy(player));
    }

    [Fact]
    public void CodeShield358FeedsViralAccuracyAndPowerlessDuration()
    {
        var player = Client();
        AbilityPassives.SetLoaded(player, new[] { ((ushort)358, (ushort)50) });
        Assert.Equal(2, AbilityStats.ViralAccuracy(player));
        Assert.Equal(11, AbilityStats.Player(player, 1030));
        Assert.Equal(11f, AbilityControlDurations.Scale(player, TempMods.PowerlessAbility, 10), 3);
        AbilityPassives.SetLoaded(player, Array.Empty<(ushort, ushort)>());
        Assert.Equal(10f, AbilityControlDurations.Scale(player, TempMods.PowerlessAbility, 10));
    }

    [Fact]
    public void EfficientCode477ChangesAttacksDamageAndStunDuration()
    {
        var player = Client();
        AbilityPassives.SetLoaded(player, new[] { ((ushort)477, (ushort)50) });
        Assert.Equal(5, AbilityStats.Attack(player, false));
        Assert.Equal(5, AbilityStats.Attack(player, true));
        Assert.Equal(110, AbilityStats.ScaleTypedAbilityDamage(player, 0, 100));
        Assert.Equal(110, AbilityStats.ScaleTypedAbilityDamage(player, 1, 100));
        Assert.Equal(10.5f, AbilityControlDurations.Scale(player, TempMods.StunAbility, 10), 3);
    }

    [Fact]
    public void HowitzerGrantAndUpgrade1087SeparateLoadedEffectsFromTheActiveClassBuff()
    {
        var player = Client();
        AbilityPassives.SetLoaded(player, new[] { ((ushort)356, (ushort)50) });
        Assert.True(AbilityPassives.IsLoaded(player, 1087));
        Assert.Equal(5, AbilityStats.Player(player, 1055));
        Assert.True(AbilityBuffs.Apply(player, 1087));
        Assert.Equal(13, AbilityStats.ViralAccuracy(player));
        Assert.Equal(108, AbilityStats.ScaleTypedAbilityDamage(player, 2, 100));
        Assert.Equal(78, AbilityCastInterrupts.CastThroughPercent(0, 1000, (short)AbilityStats.Player(player, 564)));
        Assert.Equal(8.19, AbilityRadius.TargetMetres(player, 362), 3);
        AbilitySupport.Land(player, 1094, 2);
        Assert.False(AbilityBuffs.IsActive(player, 1087));
        Assert.Equal(0, AbilityStats.ViralAccuracy(player));
        Assert.Equal(3, AbilityStats.Influence(player, 174));
        Assert.Equal(20, AbilityStats.Player(player, 520));
        Assert.Equal(7, AbilityStats.Influence(player, 564));
    }

    [Fact]
    public void WhiteNoise362BlindsForTwelveSecondsAndAttackRecovers()
    {
        var target = Client();
        Assert.True(TimedEffects.ApplyToPlayer(target, 362, "Enemy"));
        var blind = Assert.Single(TimedEffects.ForPlayer(target).Effects.Snapshot(), m => m.Target == TempMods.BlindAbility);
        Assert.Equal(12, blind.Seconds);
        Assert.Equal(-30, AbilityStats.Attack(target, true));
        TimedEffects.RunDue(blind.Start + 12.01);
        Assert.Equal(0, AbilityStats.Attack(target, true));
    }

    [Theory]
    [InlineData(true, 10.02f, -100, 123)]
    [InlineData(false, 6f, -80, 118)]
    public void FreezeArea479ChoosesValuesByCasterAndDamageBreakLeavesImmunity(bool npcCaster,
        float stateSeconds, int resistance, int damage)
    {
        var target = Client();
        var caster = npcCaster ? null : Client();
        Assert.True(TimedEffects.ApplyToPlayer(target, 479, "Enemy", caster));
        var effects = TimedEffects.ForPlayer(target);
        var pacify = Assert.Single(effects.Effects.Snapshot(), m => m.Target == TempMods.MezAbility);
        Assert.Equal(stateSeconds, pacify.Seconds);
        Assert.Equal(12, Assert.Single(effects.Effects.Snapshot(), m => m.Target == 157).Seconds);
        Assert.Equal(resistance, AbilityStats.Player(target, 157));
        Assert.False(ControlActions.CanUseAbilities(target));
        Assert.Equal(damage, TypedAbilityResistance.Player(Client(), target, 357, 100));
        TimedEffects.HealthChanged(effects, 1, true, damageEvent: true);
        Assert.True(ControlActions.CanUseAbilities(target));
        Assert.Equal(0, AbilityStats.Player(target, 157));
        Assert.True(effects.Effects.HasControlImmunity(pacify.Start + 11.99));
        Assert.False(effects.Effects.HasControlImmunity(pacify.Start + 12.01));
    }

    [Fact]
    public void CodeShock482AppliesAllFiveStatesAndTwelveSecondsOfImmunity()
    {
        var target = Client();
        Assert.True(TimedEffects.ApplyToPlayer(target, 482, "Enemy"));
        var effects = TimedEffects.ForPlayer(target);
        var stun = Assert.Single(effects.Effects.Snapshot(), m => m.Target == TempMods.StunAbility);
        Assert.Equal(6, stun.Seconds);
        Assert.Equal(6, Assert.Single(effects.Effects.Snapshot(), m => m.Target == TempMods.BlindAbility).Seconds);
        foreach (ushort state in new[] { TempMods.DazeAbility, TempMods.OffBalanceAbility, TempMods.StaggeredAbility })
            Assert.Equal(5, Assert.Single(effects.Effects.Snapshot(), m => m.Target == state).Seconds);
        Assert.False(ControlActions.CanMove(target));
        Assert.False(ControlActions.CanUseAbilities(target));
        Assert.False(effects.Effects.HasControlImmunity(stun.Start));
        var marker = Assert.Single(effects.Effects.Snapshot(), AbilityConditionalControls.IsCodeShockTimer);
        Assert.Equal(0.01f, marker.Seconds);
        Assert.Equal(12, marker.Value);
        TimedEffects.RunDue(stun.Start + 0.005);
        Assert.False(effects.Effects.HasControlImmunity(stun.Start + 0.005));
        double terminal = stun.Start + 0.011;
        TimedEffects.RunDue(terminal);
        var immunity = Assert.Single(effects.Effects.Snapshot(), mod => mod.Parent == TempMods.ControlImmunityTimerAbility);
        Assert.Equal(terminal, immunity.Start);
        Assert.Equal(12, immunity.Seconds);
        Assert.NotEqual(0, immunity.RecordId);
        Assert.DoesNotContain(effects.Effects.Snapshot(), AbilityConditionalControls.IsCodeShockTimer);
        TimedEffects.RunDue(stun.Start + 6.01);
        Assert.True(ControlActions.CanUseAbilities(target));
        Assert.True(effects.Effects.HasControlImmunity(terminal + 11.99));
        Assert.False(effects.Effects.HasControlImmunity(terminal + 12.01));

        // A second terminal marker cannot extend a currently active immunity record.
        Assert.NotEmpty(TimedEffects.Apply(effects, AbilityCatalog.Controls(482), terminal + 7));
        TimedEffects.RunDue(terminal + 7.02);
        Assert.Same(immunity, Assert.Single(effects.Effects.Snapshot(), mod => mod.Parent == TempMods.ControlImmunityTimerAbility));
        TimedEffects.RunDue(terminal + 12.01);
        Assert.False(effects.Effects.HasControlImmunity(terminal + 12.01));
        TimedEffects.RunDue(terminal + 13.01);
        Assert.Empty(effects.Effects.Snapshot());
    }

    [Theory]
    [InlineData(57, 8, 8)]
    [InlineData(357, 305, 392)]
    [InlineData(359, 645, 1183)]
    public void ViralDirectDamageUsesCurrentImplementedBaseRangeAndTargetResistance(ushort ability, int lower, int upper)
    {
        var caster = Client();
        var target = Client();
        var item = new AbilityItem();
        item.setAbilityID(ability);
        ushort Roll(Random random) => AbilityTargets.Retail.TryGetValue(ability, out var effect)
            ? AbilityTargets.RollDamage(effect, random) : AbilityHandler.AbilityDamage(item, random);
        Assert.Equal(lower, Roll(new FixedRoll(0)));
        Assert.Equal(upper, Roll(new LastRoll()));
        Assert.True(AbilityBuffs.Apply(target, 68));
        Assert.Equal(90, TypedAbilityResistance.Player(caster, target, ability, 100));
        Assert.True(AbilityBuffs.Remove(target, 68));
        Assert.Equal(100, TypedAbilityResistance.Player(caster, target, ability, 100));
    }

    [Theory]
    [InlineData(0.099, true)]
    [InlineData(0.101, false)]
    public void CodeFlux478AlwaysReducesAwarenessAndEvasionWithIndependentBlindRoll(double draw, bool expectedBlind)
    {
        var target = Client();
        target.playerInstance.StealthAwareness.setValue((byte)120);
        var effects = TimedEffects.ForPlayer(target);
        Assert.True(TempMods.TryGet(478, out var effect));
        var mods = TimedEffects.Apply(effects, effect, TimeUtils.GetSimTimeSeconds(), rng: new FractionRoll(draw));
        Assert.Equal(20, Stealth.AwarenessOf(target));
        Assert.Equal(-100, AbilityStats.Player(target, 520));
        Assert.Equal(expectedBlind, effects.Effects.Has(TempMods.BlindAbility));
        var awareness = Assert.Single(mods, m => m.Target == 204);
        TimedEffects.RunDue(awareness.Start + 8.01);
        Assert.False(effects.Effects.Has(TempMods.BlindAbility));
        Assert.Equal(20, Stealth.AwarenessOf(target));
        TimedEffects.RunDue(awareness.Start + 60.01);
        Assert.Equal(120, Stealth.AwarenessOf(target));
        Assert.Equal(0, AbilityStats.Player(target, 520));
    }

    [Fact]
    public void BufferOverflow481ReservesAvailableSpaceAndReleasesItOnExpiry()
    {
        var player = Client(1);
        Assert.True(AbilitySupport.BufferFits(player, 46));
        Assert.True(TimedEffects.ApplyToPlayer(player, 481, "Enemy"));
        var overflow = Assert.Single(TimedEffects.ForPlayer(player).Effects.Snapshot());
        Assert.Equal(60, overflow.Seconds);
        Assert.Equal(104, AbilityBuffBuffer.Reserved(player));
        AbilitySupport.Land(player, 46, 2);
        Assert.False(AbilityBuffs.IsActive(player, 46));
        TimedEffects.RunDue(overflow.Start + 60.01);
        Assert.Equal(0, AbilityBuffBuffer.Reserved(player));
        AbilitySupport.Land(player, 46, 2);
        Assert.True(AbilityBuffs.IsActive(player, 46));
        AbilityBuffs.RemoveAll(player);
    }

    private sealed class FractionRoll(double draw) : Random
    {
        public override double NextDouble() => draw;
    }

    private sealed class LastRoll : Random
    {
        public override int Next(int minValue, int maxValue) => maxValue - 1;
    }

    private sealed class FixedRoll(int roll) : Random
    {
        public override int Next(int maxValue) => roll;
        public override int Next(int minValue, int maxValue) => minValue + roll;
    }
}
