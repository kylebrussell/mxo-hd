using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterCoderWriterInitializerMode : NativeQaMode
{
    private const ushort MinorRepairs = 24, WriteCode = CraftingSession.WriteCode;
    private const ushort Writer = CraftingSession.WriterAbility, Compiler = CraftingSession.Ability;
    private const ushort Sneak = Stealth.SneakGrantAbility;
    private const int StartX = 17000, ObserverX = 19000, Y = 495, Z = 2300;
    // PR530 ordinary Sneak and PR479 bag-slot tool activation.
    private const string StockSneak = "80b95704000000000000000000000000000000000000000000000000585cc20001000000086b0000000000";
    private const string StockWriter = "80b9ca03000000000000000000000000000000000000000000000000000000000000000008020100000000";
    private const string StockCompiler = "80b9c903000000000000000000000000000000000000000000000000000000000000000008010100000000";

    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, MinorRepairs, WriteCode, 277)
        .Append((ushort)151).Concat(AbilityPrerequisites.Current.Closure(151).Keys).Distinct().Order().ToArray();

    public override string Name => "ability-parity-starter-coder-writer-initializer";
    public override NativeQaModePlan Plan { get; } = new(
        Loadout, StartX, Z,
        Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET x={StartX},y={Y},z={Z},rotation=64,alignment=1,pvpflag=0,level=50,exp={PlayerProgression.ExperienceForLevel(50)} WHERE charId=110",
            $"UPDATE characters SET x={ObserverX},y={Y},z={Z},rotation=192,alignment=2,pvpflag=0,level=50,exp={PlayerProgression.ExperienceForLevel(50)} WHERE charId=6114",
            "UPDATE char_abilities SET level=LEAST(level,15) WHERE char_id=110",
            "DELETE FROM inventory WHERE charId=110 AND slot BETWEEN 1 AND 96",
            $"INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,{CraftingSession.Tool},1,1,0),(110,{CraftingSession.WriterTool},2,1,0)"
        }, DevCommands: true, hotbarAbilities: new[] { Sneak },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1",
            ["MXO_PVP_MAX_SAFE_LEVEL"] = "15"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "37,969,970", ["NativeCloseableRows"] = 1 },
        readyMessages: new[]
        {
            "Stay at the marked start with no target. Page 2 slot 1 is ordinary Sneak, used as the control for Write Code. QAEnemy is a hostile observer 20 m east.",
            "Enter ?qaunload 37, press Sneak, then enter ?qaload 37. Expect Sneak to end when 37 is loaded, with no crafting tool open. Look for [STEALTH] 1111 off (loaded Write Code).",
            "Press Sneak again with 37 still loaded, then enter ?qaunload 24. Sneak should stay active. Use Code Analyser from the third visible bag square. Writer should open and break Sneak. Close Writer.",
            "Press Sneak again, then use Code Compiler from the second visible bag square. Compiler should open and break Sneak. Enter submits fixture commands."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var observer = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(observer.Handle, observer);
        await PvpOpponentsFixture.EnterAsync(observer);
        var server = context.ActualServer!;
        NativeQaReadiness.RequireNoAmbientAggro(server, observer);
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var owned = AbilityMemoryUiFixture.Decode(actor.CharacterData[6])
            .Where(row => AbilityMemory.IsAbility(row.Descriptor)).ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(50), "The crafting and Sneak chains fit in memory");
        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);

        await StockAbilityProfile.ChangeLoadoutAsync(actor, owned, new[] { WriteCode }, load: false);
        await ActivateSneakAsync();
        int creations = observer.World.CreationSnapshot().Count;
        int loaded = await StockAbilityProfile.ChangeLoadoutAsync(actor, owned, new[] { WriteCode }, load: true);
        await ExclusiveAsync(loaded, Sneak, on: false);
        await ObserveReappearanceAsync(creations);
        Check.That(server.DebugLog().Contains("1111 off (loaded Write Code)", StringComparison.Ordinal),
            "Loading learned Write Code itself ends Sneak");

        await Task.Delay(650);
        await ActivateSneakAsync();
        int unrelated = await StockAbilityProfile.ChangeLoadoutAsync(actor, owned, new[] { MinorRepairs }, load: false);
        actor.SendRpc("81 48");
        await StockAbilityProfile.ObserveAsync(actor, unrelated, frame => BytePattern.Parse("81 49").IsMatch(frame.Bytes),
            "The unrelated archive update is complete");
        Check.That(!actor.World.RpcSnapshot().Skip(unrelated).Any(frame =>
            frame.Bytes.AsSpan().SequenceEqual(AbilityBuffs.BuildExclusive(Sneak, false))),
            "Rebuilding an unrelated row with Write Code still loaded preserves Sneak");
        await UseToolAsync(Writer, StockWriter);

        await Task.Delay(650);
        await ActivateSneakAsync();
        await UseToolAsync(Compiler, StockCompiler);
        Check.Equal(0, actor.World.Problems.Count + observer.World.Problems.Count, "Crafting and observer packet framing");
        return Array.Empty<byte>();

        async Task ActivateSneakAsync()
        {
            var visible = await observer.WaitForViewNamedAsync(actor.Handle);
            int views = observer.World.ViewSnapshot().Count;
            int cursor = actor.World.RpcSnapshot().Count;
            actor.SendRpc(StockSneak);
            await ExclusiveAsync(cursor, Sneak, on: true);
            byte[] delete = { 1, 1, 0, (byte)visible.ViewId, (byte)(visible.ViewId >> 8) };
            await observer.WaitUntilAsync(() => observer.World.ViewSnapshot().Skip(views)
                .Any(list => BytePattern.IndexOf(list, delete, 0) >= 0), "The hostile observer loses CSR's view",
                HeadlessPlayer.DefaultTimeout);
        }

        async Task UseToolAsync(ushort tool, string request)
        {
            int views = observer.World.CreationSnapshot().Count;
            int cursor = actor.World.RpcSnapshot().Count;
            actor.SendRpc(request);
            await ExclusiveAsync(cursor, Sneak, on: false);
            await ExclusiveAsync(cursor, tool, on: true);
            await ObserveReappearanceAsync(views);
        }

        Task ObserveReappearanceAsync(int views) => observer.WaitUntilAsync(() =>
            observer.World.CreationSnapshot().Skip(views).Any(view => view.Name == actor.Handle),
            "The hostile observer regains CSR's view", HeadlessPlayer.DefaultTimeout);

        Task<RpcRecord> ExclusiveAsync(int cursor, ushort ability, bool on) => StockAbilityProfile.ObserveAsync(actor, cursor,
            frame => frame.Bytes.AsSpan().SequenceEqual(AbilityBuffs.BuildExclusive(ability, on)), $"Ability {ability} on={on}");
    }
}
