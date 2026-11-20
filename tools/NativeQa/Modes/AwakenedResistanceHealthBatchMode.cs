using System.Buffers.Binary;
using HeadlessClient;
using hds;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class AwakenedResistanceHealthBatchMode : NativeQaMode
{
    private const ushort CalmMind = 5, IgnorePain = 196, LegacyAwakened = 408;
    private const int FixtureLevel = 99;
    private static readonly ushort[] Loadout = Profile(2, 17, CalmMind, IgnorePain, LegacyAwakened).Order().ToArray();
    private static readonly (ushort Parent, ushort Child, (ushort Stat, int Value)[] Stats)[] LegacyStats =
        { (LegacyAwakened, 0, new (ushort, int)[] { (163, 200), (183, 200) }) };
    // Complete PR382 stock Calm Mind request (7bc903fb3dd5 lines 5886–5887). Replace its live selection with none.
    private const string StockCalmMind = "80b905000700050000000000000000000000000000000000000000585cc60001000000086b0000000000";

    public override string Name => "ability-parity-awakened-resistance-and-health";
    public override NativeQaModePlan Plan { get; } = new(Loadout, 23828, 13984, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        $"UPDATE characters SET level={FixtureLevel},exp={PlayerProgression.ExperienceForLevel(PlayerProgression.MaxLevel)},healthC=1000,healthM=1000,innerStrC=1000,innerStrM=1000,y=495 WHERE charId=110",
        "UPDATE char_abilities SET level=50 WHERE char_id=110"
    }, DevCommands: true, FixedCallbackRolls: true, HealthCallbackQa: true, loadAbilityAncestors: true,
        hotbarAbilities: new ushort[] { CalmMind, IgnorePain }, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "5,196,408", ["NativeCloseableRows"] = 2 },
        readyMessages: new[]
        {
            "Level 99 disposable diagnostic because Ignore Pain (196) has a shipped level-99 prerequisite. Its sample is fixed to 10 in this fixture. No ordinary level-50 acquisition claim.",
            "Inspect Status and cycle the distinct seed 408 (?qaunload 408, then ?qaload 408). Expect +200 Combat Tactics and +200 Viral Defense from this rank-50 legacy row. Capture both removal and restoration. The normal 2 Awakened style stays loaded.",
            "Page 2 slot 1 Calm Mind is the known self-buff control. With 196 off, use ?health 450 after Calm Mind and confirm there is no immediate +10 heal.",
            "Page 2 slot 2 Ignore Pain: confirm +5 Melee Resistance and +5 Ballistic Resistance, the 30s reuse and the 10s duration. After it starts, use ?health 400 and ?health 460 promptly. Capture 410 and 470 health. After the buff ends, ?health 420 should give 420 with no immediate +10. Capture the stock 196 request if sent."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var rows = AbilityMemoryUiFixture.Decode(actor.CharacterData[6])
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        var proof = new List<object>();
        await CheckPassivesAsync(actor, proof, LegacyStats, 0, remove: false);
        int cursor = await ChangeLoadoutAsync(actor, rows, new[] { LegacyAwakened }, load: false);
        await CheckPassivesAsync(actor, proof, LegacyStats, cursor, remove: true);
        cursor = await ChangeLoadoutAsync(actor, rows, new[] { LegacyAwakened }, load: true);
        await CheckPassivesAsync(actor, proof, LegacyStats, cursor, remove: false);

        cursor = actor.World.RpcSnapshot().Count;
        actor.SendRpc(SelfRequest(CalmMind));
        await ObserveAsync(actor, cursor, packet => Bonus(packet, CalmMind, 1033, (int)(FixtureLevel * 1.5), 20, remove: false),
            "Calm Mind publishes its level-scaled thrown resistance");
        await SetHealthAsync(actor, 450, 450);

        cursor = actor.World.RpcSnapshot().Count;
        actor.SendRpc(SelfRequest(IgnorePain));
        foreach (ushort stat in new ushort[] { 180, 181, IgnorePain })
            await ObserveAsync(actor, cursor, packet => Bonus(packet, IgnorePain, stat, stat == IgnorePain ? 10 : 5, 10, remove: false),
                $"Ignore Pain emits the {stat} record");
        await EvidenceRetestWire.TimerAsync(actor, cursor, IgnorePain, AbilityCooldowns.OwnTimer, 30);
        await SetHealthAsync(actor, 400, 410);
        await SetHealthAsync(actor, 460, 470);
        await actor.WaitUntilAsync(() => actor.World.RpcSnapshot().Skip(cursor)
            .Any(packet => Bonus(packet, IgnorePain, IgnorePain, 10, 10, remove: true)),
            "Ignore Pain expires and removes the sampled effect", TimeSpan.FromSeconds(13));
        await SetHealthAsync(actor, 420, 420);
        Check.Equal(0, actor.World.Problems.Count, "Awakened passive, active and health callback framing");
        return Array.Empty<byte>();
    }

    private static async Task SetHealthAsync(HeadlessPlayer actor, ushort requested, ushort expected)
    {
        int cursor = actor.World.ViewSnapshot().Count;
        actor.SendChat($"?health {requested}");
        var status = BytePattern.Parse($"02 00 02 80 80 80 40 {LittleEndianHex(expected)}");
        await actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(cursor).Any(status.Occurs),
            $"fresh health update after {requested} is {expected}", HeadlessPlayer.DefaultTimeout);
    }

    private static byte[] SelfRequest(ushort ability)
    {
        byte[] request = Convert.FromHexString(StockCalmMind);
        BinaryPrimitives.WriteUInt16LittleEndian(request.AsSpan(2), ability);
        request.AsSpan(4, 4).Clear();
        return request;
    }
}
