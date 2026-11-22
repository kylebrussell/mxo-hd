using System.Diagnostics;
using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterSimulacraSummonMembershipMode : NativeQaMode
{
    private const int Level = 18, X = 17000, Y = 495, Z = 6400;
    private const ushort Coder = 1008, Proxy = 27, Proxy2 = 28, LogicDaemon = 22, Proxy3 = 29, PatchDaemon = 602;
    private static readonly ushort[] Hotbar = { Coder, Proxy, Proxy2, LogicDaemon, Proxy3, PatchDaemon };
    private static readonly ushort[] Loadout = Hotbar.Concat(new ushort[] { 2, 17 })
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();
    private static readonly int Memory = Loadout.Sum(AbilityMemory.Cost);
    private sealed record Summon(ushort Id, byte Rank, ushort Hp, ushort Cost, int Reuse, string Request);
    // Full stock requests from PR593 (27 and 22), PR603 (28 and 602) and PR596 (29).
    private static readonly Summon[] Summons =
    {
        new(Proxy, 4, 224, 50, 8, "80b91b00000000000000000000000000000000000000000000000000585cb40001000000086b0000000000"),
        new(Proxy2, 7, 337, 50, 8, "80b91c00000000000000000000000000000000000000000000000000585ca80001000000086b0000000000"),
        new(LogicDaemon, 7, 337, 50, 10, "80b916000000000000000000000000000000000000000000000000000090767241086b0000000000"),
        new(Proxy3, 17, 712, 50, 8, "80b91d0000000000000000000000000000000000000000000000000000000090767241086b0000000000"),
        new(PatchDaemon, 18, 750, 100, 10, "80b95a02000000000000000000000000000000000000000000000000585ca80001000000086b0000000000")
    };

    public override string Name => "ability-parity-starter-simulacra-summon-membership";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" },
        new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)}," +
                $"x={X},y={Y},z={Z},rotation=64,healthC=500,healthM=500,innerStrC=650,innerStrM=1000 WHERE charId=110"
        }, DevCommands: true, hotbarAbilities: Hotbar, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "22,27,28,29,602", ["NativeCloseableRows"] = 0, ["MemoryUsed"] = Memory },
        readyMessages: new[]
        {
            $"Level {Level}, all ranked prerequisites fit {Memory}/{AbilityMemory.ForLevel(Level)} memory. CSR ({X},{Y},{Z}) faces the expected pet stand ({X + 150},{Y},{Z}) on verified outdoor NAV, clear of the ice. Page 2 Coder Style/27/28/22/29/602. Leave selection clear.",
            "Use Coder Style. Type ?qaunload with 602, 29, 22, 28 and 27 individually. Try their slots. Stock may disable the slots locally. If a request sends, expect one reason 7 and 650 IS, with no cast, reuse or window. Reload in order 27, 28, 22, 29, 602. Use Actions if a seeded shortcut disappeared.",
            "Summon 27, L4/224 HP for 50 IS, 8 s cast/reuse. Start 28. During its 8 s cast type ?qaunload 28 and, if time allows, ?qaload 28. Expect [PET] 28 fizzles: the summon was unloaded during the cast. The paid 50 IS and 8 s reuse remain. The same 27 body and window stay active. Reload 28 after the bar if needed.",
            "Use 22, then fresh 28 after its own reuse, then 29 and 602. Each 8 s cast replaces exactly one prior window. HP and rank in this order are L7/337, L7/337, L17/712, L18/750. Reuse 10/8/8/10 s; costs 50/50/50/100. IS 650→600→550→500→450→400→300. Finally ?qaunload 602 dismisses only that owning summon and its single window. Reloading 602 stays quiet. Projected DONE delta 0."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Summon membership needs repository NAV data.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int x in new[] { X, X + 150 })
            Check.That(NavMesh.Locate(1, x, Y, Z, out var nav) && !nav.indoors && Math.Abs(nav.floorY + 95 - Y) < 1,
                $"Verified outdoor stand ({x},{Y},{Z})");
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, X + 150, Y, Z), "Clear sight to the summon stand");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var server = context.ActualServer!;
        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(Memory <= AbilityMemory.ForLevel(Level) && Loadout.All(id => archive.ContainsKey(id) &&
            AbilityPrerequisites.Current.Closure(id).All(need => archive.TryGetValue(need.Key, out var row) && row.Level >= need.Value)),
            "Complete level 18 ranked summon profile fits");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        actor.SendRpc(StarterCoderPetBuffsAndDetonateMode.CoderStock);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");

        var batch = Summons.Select(row => row.Id).ToArray();
        await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, batch, load: false);
        int refusedAt = actor.World.RpcSnapshot().Count;
        foreach (Summon row in Summons)
            await StarterHackerControlTargetMode.RefusedAsync(actor, row.Id, WireFormat.FromHex(row.Request), AbilityRefusals.NotLoaded);
        Check.That(!actor.World.RpcSnapshot().Skip(refusedAt).Any(frame => frame.Opcode is 0x176 or 0x177),
            "An unloaded summon cannot create a pet window");
        await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, batch, load: true);
        await Task.Delay(TimeSpan.FromSeconds(AbilityHandler.DuplicateAbilityWindowSeconds + 0.15));

        int isAfter = 650;
        byte active = await SummonAsync(Summons[0], 0);
        var interrupted = Summons[1];
        int pendingAt = actor.World.RpcSnapshot().Count, pendingViews = actor.World.ViewSnapshot().Count;
        int pendingLog = server.DebugLog().Length;
        actor.SkipReceived();
        var bar = Stopwatch.StartNew();
        actor.SendRpc(interrupted.Request);
        await EvidenceRetestWire.CastBarAsync(actor, interrupted.Id, 8);
        await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { interrupted.Id }, load: false);
        await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { interrupted.Id }, load: true);
        Check.That(bar.Elapsed.TotalSeconds < 8, "Both ordinary Archive acknowledgements land during the pending summon");
        await EvidenceRetestWire.TimerAsync(actor, pendingAt, interrupted.Id, AbilityCooldowns.OwnTimer, interrupted.Reuse);
        await actor.WaitUntilAsync(() => server.DebugLog()[pendingLog..].Contains("[PET] 28 fizzles: the summon was unloaded during the cast"),
            "The restored Archive row does not revive the earlier pending summon", HeadlessPlayer.DefaultTimeout);
        isAfter -= interrupted.Cost;
        await ExpectIsAsync(pendingViews);
        Check.That(!actor.World.RpcSnapshot().Skip(pendingAt).Any(frame => frame.Opcode is 0x176 or 0x177),
            "The unrelated active Remote Proxy has no replacement or dismissal");
        actor.SkipReceived();
        actor.SendChat("?pet");
        await actor.WaitForTextAsync("224/224 HP");

        active = await SummonAsync(Summons[2], active);
        byte[] retry = AbilityCooldowns.BuildTimer(interrupted.Id, AbilityCooldowns.OwnTimer, interrupted.Reuse, start: false);
        await actor.WaitUntilAsync(() => actor.World.RpcSnapshot().Skip(pendingAt).Any(frame => frame.Bytes.AsSpan().SequenceEqual(retry)),
            "Ordinary pending summon reuse is complete", TimeSpan.FromSeconds(12));
        active = await SummonAsync(interrupted, active);
        active = await SummonAsync(Summons[3], active);
        active = await SummonAsync(Summons[4], active);
        int finalAt = actor.World.RpcSnapshot().Count;
        await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { PatchDaemon }, load: false);
        await StockAbilityProfile.ObserveAsync(actor, finalAt, frame => frame.Bytes is [0x81, 0x77, var id] && id == active,
            "Unloading the active summon dismisses its window");
        await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { PatchDaemon }, load: true);
        await Task.Delay(TimeSpan.FromMilliseconds(300));
        Check.That(actor.World.RpcSnapshot().Skip(finalAt).Count(frame => frame.Opcode == 0x177) == 1 &&
            !actor.World.RpcSnapshot().Skip(finalAt).Any(frame => frame.Opcode == 0x176), "Reload leaves the dismissed summon inactive");
        Check.Equal(0, actor.World.Problems.Count, "Summon membership framing");
        Console.WriteLine("SIMULACRA SUMMON MEMBERSHIP PASS: five unloaded refusals, no pending revival, five live summons and owning unload.");
        return Array.Empty<byte>();

        async Task<byte> SummonAsync(Summon row, byte previous)
        {
            int rpc = actor.World.RpcSnapshot().Count, views = actor.World.ViewSnapshot().Count;
            actor.SkipReceived();
            actor.SendRpc(row.Request);
            await EvidenceRetestWire.CastBarAsync(actor, row.Id, 8);
            await EvidenceRetestWire.TimerAsync(actor, rpc, row.Id, AbilityCooldowns.OwnTimer, row.Reuse);
            var registration = await StockAbilityProfile.ObserveAsync(actor, rpc, frame => frame.Opcode == 0x176, "A new summon window");
            byte id = registration.Bytes[3];
            if (previous != 0)
                await StockAbilityProfile.ObserveAsync(actor, rpc, frame => frame.Bytes is [0x81, 0x77, var replaced] && replaced == previous,
                    "The successful summon replaces the prior window");
            await actor.WaitUntilAsync(() => actor.World.CreationSnapshot().Any(view =>
                view.Goid == CoderPets.SimulacraGoId && view.Attributes["SimulacraID"][0] == id &&
                view.Attributes["Level"][0] == row.Rank && BitConverter.ToUInt16(view.Attributes["MaxHealth"]) == row.Hp),
                $"Summon {row.Id} has shipped rank {row.Rank} and expected max HP {row.Hp}", HeadlessPlayer.DefaultTimeout);
            isAfter -= row.Cost;
            await ExpectIsAsync(views);
            return id;
        }

        Task ExpectIsAsync(int views) => actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(views)
            .SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates).Any(value => value == isAfter),
            $"CSR has {isAfter} IS", HeadlessPlayer.DefaultTimeout);
    }
}
