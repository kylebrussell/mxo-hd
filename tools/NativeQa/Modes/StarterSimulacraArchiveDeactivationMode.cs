using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterSimulacraArchiveDeactivationMode : NativeQaMode
{
    private const int Level = 18, X = 24800, Y = 495, Z = -1498;
    private const ushort Coder = 1008, Proxy1 = 27, Proxy2 = 28, Daemon = 22;
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Coder, Proxy1, Proxy2, Daemon).Order().ToArray();
    // PR593: Coder, Proxy 1.0 and Logic Daemon. PR603 PacketLog 738: Proxy 2.0.
    private const string CoderCast = "80b9f003000000000000000000000000000000000000000000000000585cb40001000000086b0000000000";
    private static readonly (ushort Ability, string Capture, byte Rank, ushort MaxHp, int Reuse)[] Summons =
    {
        (Proxy1, "80b91b00000000000000000000000000000000000000000000000000585cb40001000000086b0000000000", 4, 224, 8),
        (Proxy2, "80b91c00000000000000000000000000000000000000000000000000585ca80001000000086b0000000000", 7, 337, 8),
        (Daemon, "80b916000000000000000000000000000000000000000000000000000000000090767241086b0000000000", 7, 337, 10)
    };

    public override string Name => "ability-parity-starter-simulacra-archive-deactivation";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" },
        new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z}," +
                $"rotation={MobAggro.YawTowards(X,Z,X+150,Z)},innerStrC=800,innerStrM=1000 WHERE charId=110",
            $"UPDATE characters SET x={X - 420},y={Y},z={Z - 300} WHERE charId=6111"
        }, DevCommands: true, hotbarAbilities: new[] { Coder, Proxy1, Proxy2, Daemon },
        testMobFixture: "martial-player-controls.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "22,27,28", ["NativeCloseableRows"] = 0, ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost) },
        readyMessages: new[]
        {
            "CSR faces the pet stand 1.5 m ahead with no ambient mobs. QAFriend observes from 4.2 m west and 3 m south. Page 2 is Coder Style, Remote Proxy 1.0, Remote Proxy 2.0, Logic Daemon. Stay on this stand and leave world selection clear.",
            "Select Coder Style, then use slot 2. After the eight-second cast, expect an L4 Proxy, Max HP 224, 50 IS charged and eight-second reuse. Open the Archive. Unload Logic Daemon first and confirm the Proxy stays. Reload Logic Daemon for the final step.",
            "Unload Remote Proxy 1.0 in the Archive with the Proxy still alive. Expect [PET] Summon 27 unloaded, one 81 77 removal for the Proxy, and its map view and pet window to disappear. Reloading Remote Proxy 1.0 should stay quiet until another summon.",
            "Repeat with slots 3 and 4. Each costs 50 IS and creates an L7 pet (Max HP 337) after eight seconds. Reuse is eight seconds for 28 and ten seconds for 22. Each own Archive unload emits its matching [PET] line and a single window removal. IS after the three summons is 800 → 750 → 700 → 650.",
            "The Archive is preferred. If it is inaccessible at this stand, enter ?qaunload 27 and ?qaload 27 (and the equivalent for 28 or 22). This uses the same unload handler. Record which route you used."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer!;
        var watcher = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend", stockCharacterLoad: true);
        context.Helpers.Add(watcher.Handle, watcher);
        await watcher.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, watcher);
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var rows = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) &&
            rows.Keys.ToHashSet().SetEquals(Loadout) && Loadout.All(id => AbilityPrerequisites.Current.Closure(id)
                .All(need => rows.TryGetValue(need.Key, out var row) && row.Level >= need.Value)),
            "The full level-18 Coder profile fits the Archive");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        actor.SendRpc(CoderCast);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");

        var isAfter = 800;
        foreach (var summon in Summons)
        {
            int rpcAt = actor.World.RpcSnapshot().Count;
            int isAt = actor.World.ViewSnapshot().Count;
            int actorAt = actor.World.CreationSnapshot().Count;
            int watcherAt = watcher.World.CreationSnapshot().Count;
            actor.SendRpc(summon.Capture);
            await EvidenceRetestWire.CastBarAsync(actor, summon.Ability, 8);
            var registered = await StockAbilityProfile.ObserveAsync(actor, rpcAt, frame => frame.Bytes is [0x81, 0x76, ..],
                $"{summon.Ability} creates a registered window");
            byte id = registered.Bytes[3];
            var pet = await ViewAsync(actor, actorAt, id);
            var remote = await ViewAsync(watcher, watcherAt, id);
            Check.Equal(summon.Rank, pet.Attributes["Level"][0], "The pet uses its shipped level clamp");
            Check.Equal(summon.MaxHp, BitConverter.ToUInt16(pet.Attributes["MaxHealth"]), "The pet has the expected Max HP");
            await EvidenceRetestWire.TimerAsync(actor, rpcAt, summon.Ability, AbilityCooldowns.OwnTimer, summon.Reuse);
            isAfter -= 50;
            await actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(isAt)
                .SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates).Any(value => value == isAfter),
                $"{summon.Ability} leaves {isAfter} IS", HeadlessPlayer.DefaultTimeout);

            if (summon.Ability == Proxy1)
            {
                int unrelated = await StockAbilityProfile.ChangeLoadoutAsync(actor, rows, new[] { Daemon }, load: false);
                await StockAbilityProfile.ChangeLoadoutAsync(actor, rows, new[] { Daemon }, load: true);
                Check.That(!actor.World.RpcSnapshot().Skip(unrelated).Any(Removed),
                    "Unloading a different summon does not remove the current Proxy");
            }

            int localViews = actor.World.ViewSnapshot().Count;
            int remoteViews = watcher.World.ViewSnapshot().Count;
            int unloaded = await StockAbilityProfile.ChangeLoadoutAsync(actor, rows, new[] { summon.Ability }, load: false);
            await StockAbilityProfile.ObserveAsync(actor, unloaded, Removed, "The owning summon unload dismisses its window");
            await DeletedAsync(actor, localViews, pet.ViewId);
            await DeletedAsync(watcher, remoteViews, remote.ViewId);
            int reloaded = await StockAbilityProfile.ChangeLoadoutAsync(actor, rows, new[] { summon.Ability }, load: true);
            Check.Equal(1, actor.World.RpcSnapshot().Skip(unloaded).Count(Removed), "The pet window is removed once");
            Check.That(!actor.World.RpcSnapshot().Skip(reloaded).Any(frame => frame.Bytes is [0x81, 0x76, ..]),
                "Reloading the archive ability does not summon a replacement");
            bool Removed(RpcRecord frame) => frame.Bytes is [0x81, 0x77, var sim] && sim == id;
        }
        Check.Equal(0, actor.World.Problems.Count + watcher.World.Problems.Count, "The Archive deactivation frames parse cleanly");
        return Array.Empty<byte>();

        async Task<ViewCreation> ViewAsync(HeadlessPlayer player, int cursor, byte id)
        {
            ViewCreation? view = null;
            await player.WaitUntilAsync(() => (view = player.World.CreationSnapshot().Skip(cursor).FirstOrDefault(row =>
                row.Goid == CoderPets.SimulacraGoId && row.Attributes["SimulacraID"][0] == id &&
                BitConverter.ToUInt32(row.Attributes["OwnerCharacterID"]) == actor.CharId)) != null,
                $"Pet {id} visible to {player.Handle}", HeadlessPlayer.DefaultTimeout);
            return view!;
        }
    }

    private static Task DeletedAsync(HeadlessPlayer player, int cursor, ushort view)
    {
        byte[] deletion = WireFormat.FromHex($"01 00 01 01 00 {StockAbilityProfile.LittleEndianHex(view)}");
        return player.WaitUntilAsync(() => player.World.ViewSnapshot().Skip(cursor).Any(list => list.AsSpan().IndexOf(deletion) >= 0),
            $"Retired pet view {view} is deleted for {player.Handle}", HeadlessPlayer.DefaultTimeout);
    }
}
