using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterSimulacrumTiersMode : NativeQaMode
{
    private const int X = 24800, Y = 495, Z = -1498;
    private const byte Rank = 34;
    private const ushort Coder = 1008, StartingIs = 700, MaxIs = 1000;
    // PR596 stock PacketLog 4f792a63f1be: 923, 1208, 1512, 1746.
    private static readonly (ushort Id, string Label, byte Level, ushort Hp, ushort Cost, float Reuse, bool Nuker, string StockRequest)[] Samples =
    {
        (586, "Remote Proxy 4.0", 34, 1350, 75, 8, false,
            "80b94a02000000000000000000000000000000000000000000000000585cb70001000000086b0000000000"),
        (611, "Logic Daemon 3.0", 34, 1350, 110, 10, true,
            "80b96302000000000000000000000000000000000000000000000000585cb70001000000086b0000000000"),
        (29, "Remote Proxy 3.0", 17, 712, 50, 8, false,
            "80b91d0000000000000000000000000000000000000000000000000000000090767241086b0000000000"),
        (601, "Logic Daemon 2.0", 17, 712, 90, 10, true,
            "80b95902000000000000000000000000000000000000000000000000585cb70001000000086b0000000000")
    };
    private static readonly ushort[] Abilities = StockAbilityProfile.Profile(
        Samples.Select(sample => sample.Id).Concat(new ushort[] { 2, 17, Coder }).ToArray()).Order().ToArray();
    private static readonly int Memory = Abilities.Sum(AbilityMemory.Cost);

    private const string CoderRequest = "80b9f0030000000000000000000000000000000000000000000000000000000090767241086b0000000000";
    private const string Deactivate = "81 7a fe 01"; // Stock /ordersimulacra All Deactivate in the same log.

    public override string Name => "ability-parity-starter-simulacrum-tiers";

    public override NativeQaModePlan Plan { get; } = new(Abilities, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Rank}) WHERE char_id=110" },
        new[]
        {
            $"UPDATE characters SET level={Rank},exp={PlayerProgression.ExperienceForLevel(Rank)},x={X},y={Y},z={Z}," +
                $"rotation={MobAggro.YawTowards(X,Z,X+150,Z)},innerStrC={StartingIs},innerStrM={MaxIs} WHERE charId=110"
        }, DevCommands: true, hotbarAbilities: new[] { Coder }.Concat(Samples.Select(sample => sample.Id)).ToArray(),
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "586,611,29,601", ["NativeCloseableRows"] = 4, ["MemoryUsed"] = Memory },
        readyMessages: new[]
        {
            $"Rank {Rank}, {Memory}/{AbilityMemory.ForLevel(Rank)} memory. Stay on the outdoor stand facing the summon point 1.5 m east. Select Coder Style (page 2 slot 1) and leave world selection clear. IS starts at {StartingIs}/{MaxIs} and does not regenerate in this fixture.",
            "Cast slots 2 through 5 in order. Each cast lasts 8 s. After each summon, enter /ordersimulacra All Deactivate in stock chat. Expect one 81 77 window removal and deletion of the pet view before the next cast.",
            "Slot 2: 586, level 34 Proxy, 1350 max HP, 75 IS (700 to 625) and 8 s reuse. Slot 3: 611, level 34 Nuker, 1350 max HP, 110 IS (625 to 515) and 10 s reuse.",
            "Slot 4: 29, level 17 Proxy, 712 max HP, 50 IS (515 to 465) and 8 s reuse. Slot 5: 601, level 17 Nuker, 712 max HP, 90 IS (465 to 375) and 10 s reuse. Proxy combat sheets may vary.",
            "If a lower tier is hidden in Actions, use its seeded shortcut. If it refuses, capture the tooltip and continue with the other tier. Press Enter to submit the stock dismissal command."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var loaded = AbilityMemoryUiFixture.Decode(actor.CharacterData[6])
            .Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(loaded.Keys.ToHashSet().SetEquals(Abilities) &&
            Abilities.All(id => AbilityPrerequisites.Current.Closure(id).All(required =>
                loaded.TryGetValue(required.Key, out ushort rank) && rank >= required.Value)),
            "Stock Margin sends all four summon tiers with their entire ranked prerequisite tree");
        Check.That(Memory <= AbilityMemory.ForLevel(Rank), "The complete summon tree fits at rank 34");

        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        Check.Equal(StartingIs, BitConverter.ToUInt16(actor.World.CreationSnapshot().First(view => view.ViewId == 2)
            .Attributes["InnerStrengthAvailable"]), "The disposable CSR starts at 700 IS");
        actor.SendRpc(CoderRequest);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");

        ushort expectedIs = StartingIs;
        var seenIds = new HashSet<byte>();
        foreach (var sample in Samples)
        {
            int rpcAt = actor.World.RpcSnapshot().Count;
            int viewAt = actor.World.ViewSnapshot().Count;
            int creationAt = actor.World.CreationSnapshot().Count;
            actor.SendRpc(sample.StockRequest);
            await EvidenceRetestWire.CastBarAsync(actor, sample.Id, 8);
            var registration = await StockAbilityProfile.ObserveAsync(actor, rpcAt, frame => frame.Bytes is [0x81, 0x76, ..],
                $"Tier {sample.Id} creates its stock Simulacra window");
            Check.Equal(46, registration.Bytes.Length, "Stock Simulacra window record length");
            byte simId = registration.Bytes[3];
            Check.That(seenIds.Add(simId), "The window ID is fresh for every summon");

            ViewCreation? pet = null;
            await actor.WaitUntilAsync(() => (pet = actor.World.CreationSnapshot().Skip(creationAt).FirstOrDefault(view =>
                view.Goid == 40376 && view.Attributes["SimulacraID"][0] == simId)) != null,
                $"Tier {sample.Id} creates the selected owned pet", HeadlessPlayer.DefaultTimeout);
            Check.Equal(actor.CharId, BitConverter.ToUInt32(pet!.Attributes["OwnerCharacterID"]), "CSR is the pet owner");
            Check.Equal(actor.CharId, BitConverter.ToUInt32(pet.Attributes["MasterCharacterID"]), "CSR is the pet master");
            Check.Equal(sample.Level, pet.Attributes["Level"][0], $"{sample.Label} clamps its level");
            Check.Equal(sample.Hp, BitConverter.ToUInt16(pet.Attributes["MaxHealth"]), $"{sample.Label} max HP");
            Check.That(sample.Nuker ? pet.Name == "Nuker Simulacrum" : CoderPets.ProxySheets.Any(sheet =>
                pet.Name == $"{sheet} Simulacrum"), $"{sample.Label} picks the shipped role or an allowed combat sheet");
            await EvidenceRetestWire.TimerAsync(actor, rpcAt, sample.Id, AbilityCooldowns.OwnTimer, sample.Reuse);
            expectedIs -= sample.Cost;
            await actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(viewAt)
                .SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates).LastOrDefault() == expectedIs,
                $"{sample.Label} spends {sample.Cost} IS and leaves {expectedIs}", HeadlessPlayer.DefaultTimeout);

            int dismissAt = actor.World.RpcSnapshot().Count;
            int deleteAt = actor.World.ViewSnapshot().Count;
            actor.SendRpc(Deactivate);
            await StockAbilityProfile.ObserveAsync(actor, dismissAt, frame => frame.Bytes is [0x81, 0x77, var id] && id == simId,
                $"Stock Deactivate removes {sample.Label}'s window");
            byte[] deletion = WireFormat.FromHex($"01 00 01 01 00 {StockAbilityProfile.LittleEndianHex(pet.ViewId)}");
            await actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(deleteAt).Any(list => list.AsSpan().IndexOf(deletion) >= 0),
                $"Stock Deactivate removes {sample.Label}'s world view", HeadlessPlayer.DefaultTimeout);
        }

        Check.Equal(0, actor.World.Problems.Count, "Simulacrum tier packet framing");
        return Array.Empty<byte>();
    }
}
