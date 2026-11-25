using System.Reflection;
using System.Runtime.CompilerServices;
using hds;

namespace hds.Tests;

[Collection("DelayedActions")]
public sealed class EscapeAndEvasionTests : IDisposable
{
    private const ushort Escape = Stealth.EscapeAndEvasionAbility, CombatAura = 42;
    private readonly Dictionary<string, WorldClient> previousClients = WorldServer.Clients;
    private readonly WorldClient client;

    public EscapeAndEvasionTests()
    {
        WorldServer.Clients = new Dictionary<string, WorldClient>();
        client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.playerData.setCharID(50701);
        client.playerData.setDistrictId(1);
        client.playerData.setOnWorld(true);
        client.playerInstance = new PlayerCharacter();
        client.playerInstance.Level.setValue((byte)25);
        client.playerInstance.Health.setValue((ushort)500);
        client.playerInstance.MaxHealth.setValue((ushort)500);
        client.playerInstance.InnerStrengthAvailable.setValue((ushort)400);
        client.playerInstance.InnerStrengthMax.setValue((ushort)400);
        client.messageQueue = new MessageQueue();
        client.viewMan = new ViewManager();
        typeof(WorldClient).GetField("lastFlushTick", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(client, long.MaxValue);
        typeof(WorldClient).GetField("<TransportFence>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(client, new WorldSendFence());
        client.LivenessClock = () => 0;
        client.Alive = true;
        AbilityPassives.SetLoaded(client, new (ushort, ushort)[] { (151, 19), (Escape, 23) });
        client.messageQueue.RPCMessagesQueue.Clear();
    }

    [Fact]
    public void LandingShutsDownTheAuraAndDamageReleasesItsOwnSneakState()
    {
        AbilityAuras.Start(client, CombatAura);
        Assert.True(AbilityAuras.IsOn(client, CombatAura));

        Stealth.Land(client, Escape, 0);

        var state = Assert.IsType<Stealth.State>(Stealth.Current(client));
        Assert.Equal(Escape, state.Def.Ability);
        Assert.Equal(23, state.Level);
        Assert.Equal(25, Stealth.StealthLevelOf(client));
        Assert.Equal(AnimationPackets.StanceSneaking, EmoteHandler.Stance(client));
        Assert.Equal(.25f, MovementScale());
        Assert.False(AbilityAuras.IsOn(client, CombatAura));
        Assert.True(HasRpc(AbilityBuffs.BuildExclusive(CombatAura, false)));
        Assert.True(HasRpc(AbilityBuffs.BuildExclusive(Escape, true)));
        var movement = Assert.Single(state.Mods.Select((mod, i) => (mod, i)), pair => pair.mod.Stat == 877);
        ushort record = state.RecordIds[movement.i];
        Assert.True(HasRpc(AbilityBuffs.BuildModifier(AbilityBuffs.KindStat, record, Escape, 877, 25, false, 0)));

        Stealth.HealthChanged(client, 0, belowMaximum: true);
        Assert.Same(state, Stealth.Current(client));
        Stealth.HealthChanged(client, 1, belowMaximum: true);

        Assert.Null(Stealth.Current(client));
        Assert.Equal(AnimationPackets.StanceStanding, EmoteHandler.Stance(client));
        Assert.Equal(.03f, MovementScale(), 3); // Rank-19 loaded 151 Sneak supplies +3% (0.03) ordinary Run baseline via stat 998.
        Assert.True(HasRpc(AbilityBuffs.BuildExclusive(Escape, false)));
        Assert.True(HasRpc(AbilityBuffs.BuildModifier(AbilityBuffs.KindStat, record, Escape, 877, 25, true, 0)));
    }

    [Fact]
    public void StatusDismissalRestoresTheSurvivingSpeedBuffAndLeavesOrdinarySneakAlone()
    {
        Assert.True(AbilityCancellation.CanDismiss(Escape));
        Assert.True(AbilityBuffs.Apply(client, 12));
        Assert.Equal(.48f, MovementScale(), 3); // 0.45 Hyper-Speed + 0.03 loaded rank-19 151 Sneak (stat 998).

        Stealth.Land(client, Escape, 0);
        Assert.Equal(.37f, MovementScale(), 3);
        Assert.True(AbilityCancellation.Dismiss(client, Escape));
        Assert.Null(Stealth.Current(client));
        Assert.True(AbilityBuffs.IsActive(client, 12));
        Assert.Equal(.48f, MovementScale(), 3);

        Stealth.Land(client, 1111, 0);
        var ordinary = Assert.IsType<Stealth.State>(Stealth.Current(client));
        Assert.Equal(.12f, MovementScale(), 3);
        Assert.False(AbilityCancellation.Dismiss(client, Escape));
        Assert.Same(ordinary, Stealth.Current(client));
    }

    [Fact]
    public void TheShippedCastWindowAndOwnedAbilityAreCheckedAtLanding()
    {
        var entry = AbilityCatalog.Find(Escape);
        Assert.Equal(1, entry.Number("Ability/CastWhenUsed"));
        Assert.Equal(1, entry.Number("Ability/IsAToggleAbility"));
        Assert.Equal(2, entry.Number("Ability/CastingTime"));
        Assert.Equal(0, entry.Number("Ability/DetectionDifficultyChange", -1));
        var combat = CombatManager.GetHandler(client);
        combat.isCombatRunning = true;
        Assert.NotNull(Stealth.Refusal(client, Escape, 0, out uint reason));
        Assert.Equal(AbilityRefusals.MustBeOutsideCombat, reason);
        Stealth.Land(client, Escape, 0);
        Assert.Null(Stealth.Current(client));

        combat.isCombatRunning = false;
        AbilityPassives.SetLoaded(client, new (ushort, ushort)[] { (151, 19) });
        Assert.NotNull(Stealth.Refusal(client, Escape, 0, out reason));
        Assert.Equal(AbilityRefusals.NotLoaded, reason);
        Stealth.Land(client, Escape, 0);
        Assert.Null(Stealth.Current(client));
    }

    private float MovementScale() => BitConverter.ToSingle(client.playerInstance.MovementScale.getValue(), 0);

    private bool HasRpc(byte[] expected) => client.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>()
        .Any(message => message.content.AsSpan(1).SequenceEqual(expected));

    public void Dispose()
    {
        Stealth.Cleared(client);
        AbilityAuras.Stop(client, CombatAura);
        AbilityBuffs.RemoveAll(client);
        CombatManager.RemoveClient(client);
        WorldServer.Clients = previousClients;
    }
}
