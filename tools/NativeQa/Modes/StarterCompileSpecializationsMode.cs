using System.Buffers.Binary;
using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterCompileSpecializationsMode : NativeQaMode
{
    private sealed record Sample(ushort Ability, ushort Stat, uint Item, ushort ArchiveSlot, string StockRequest);

    private static readonly Sample[] Samples =
    {
        new(517, 655, 700, 400, "60 bc 02 00 00 0f 00 00 01 01"),
        new(518, 654, 2485, 401, "60 b5 09 00 00 10 00 00 01 01"),
        new(521, 656, 39413, 402, "60 f5 99 00 00 90 01 00 01 01")
    };
    private static readonly ushort[] Passives = Samples.Select(sample => sample.Ability).ToArray();
    // Tool open/close and three Compile60 requests captured in native PR479.
    private const string OpenCompiler = "80b9c903000000000000000000000000000000000000000000000000000000000000000008010100000000";
    private const string CloseCompiler = "80b9c903000000000000000000000000000000000000000000000000000000000000006208010000000000";

    public override string Name => "ability-parity-starter-compile-specializations";
    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2, 17, 37, 275, 277 }.Concat(Passives).ToArray(), 23828, 13984,
        Array.Empty<string>(), new[]
        {
            "DELETE FROM inventory WHERE charId=110 AND slot BETWEEN 1 AND 96",
            "INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,1271,1,1,0)",
            "DELETE FROM char_abilities WHERE char_id=110 AND (slot BETWEEN 400 AND 402 OR ability_id IN (" +
                string.Join(",", Samples.Select(sample => sample.Item << 7)) + "))",
            "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded,ability_name) VALUES " +
                string.Join(",", Samples.Select(sample => $"(110,{sample.ArchiveSlot},{sample.Item << 7},2,0,'Specialization comparison code')")),
            "UPDATE characters SET y=495,cash=100000 WHERE charId=110"
        }, Array.Empty<string>(), DevCommands: true, loadAbilityAncestors: true, hotbarAbilities: Passives,
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        extraQaMetadata: new() { ["ScopedStockRows"] = "517,518,521", ["NativeCloseableRows"] = 3 },
        readyMessages: new[]
        {
            "Page 2 identifies three passive specializations. The Compiler is bag slot 1. At login check the Status or PacketLog adds for Weapon, Tool and Upgrade Crafting at +50 each.",
            "Unload all with ?qaunload 517, 518 and 521 as separate commands. Use Enter to submit. Open Compiler from the bag item and compile Kayer Snub Nose (700), Lakeland Mark I Program Launcher (2485) and Health Boost 3.0 (39413) once. Each log should end in specialization 0. The second charge remains.",
            "Close Compiler. For each item load only its matching passive (517 for 700, 518 for 2485, 521 for 39413). Use the Compiler item again and compile the remaining charge. Look for specialization 50 and the matching item result. Close Compiler and unload that passive before the next item."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var db = context.OwnedDatabase!;
        foreach (var sample in Samples)
            Check.Equal(2L, await db.LongAsync($"SELECT level FROM char_abilities WHERE char_id=110 AND slot={sample.ArchiveSlot} AND ability_id={sample.Item << 7}"),
                $"Two charges of {sample.Item} are seeded in the correct archive slot");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var owned = AbilityMemoryUiFixture.Decode(actor.CharacterData[6])
            .Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        var server = context.ActualServer!;
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        foreach (var sample in Samples)
            await CheckPassiveAsync(sample, 0, remove: false);
        int unloaded = await StockAbilityProfile.ChangeLoadoutAsync(actor, owned, Passives, load: false);
        foreach (var sample in Samples)
            await CheckPassiveAsync(sample, unloaded, remove: true);

        await ToggleCompilerAsync(open: true);
        foreach (var sample in Samples)
            await CompileAsync(sample, specialization: 0, remaining: 1);
        await ToggleCompilerAsync(open: false);

        foreach (var sample in Samples)
        {
            int added = await StockAbilityProfile.ChangeLoadoutAsync(actor, owned, new[] { sample.Ability }, load: true);
            await CheckPassiveAsync(sample, added, remove: false);
            await ToggleCompilerAsync(open: true);
            await CompileAsync(sample, specialization: 50, remaining: 0);
            await ToggleCompilerAsync(open: false);
            int removed = await StockAbilityProfile.ChangeLoadoutAsync(actor, owned, new[] { sample.Ability }, load: false);
            await CheckPassiveAsync(sample, removed, remove: true);
        }
        Check.Equal(0, actor.World.Problems.Count, "Compile specialization replay framing");
        return Array.Empty<byte>();

        Task<RpcRecord> CheckPassiveAsync(Sample sample, int first, bool remove) => StockAbilityProfile.ObserveAsync(actor, first,
            rpc => StockAbilityProfile.Bonus(rpc, sample.Ability, sample.Stat, 50, 0, remove),
            $"Expected crafting stat {sample.Stat} from {sample.Ability}, remove={remove}");

        async Task ToggleCompilerAsync(bool open)
        {
            actor.SkipReceived();
            actor.SendRpc(open ? OpenCompiler : CloseCompiler);
            await actor.WaitForRpcAsync($"80 bd 05 c9 03 00 00 00 00 00 {(open ? "01" : "00")}");
        }

        async Task CompileAsync(Sample sample, int specialization, int remaining)
        {
            byte[] request = WireFormat.FromHex(sample.StockRequest);
            BinaryPrimitives.WriteUInt16LittleEndian(request.AsSpan(5), sample.ArchiveSlot);
            int start = server.DebugLog().Length;
            actor.SkipReceived();
            actor.SendRpc(request);
            await actor.WaitForRpcAsync("61 00", TimeSpan.FromSeconds(24));
            Check.That(server.DebugLog()[start..].Split('\n').Any(line =>
                line.Contains($"[COMPILE60] Started {sample.Item} ", StringComparison.Ordinal) &&
                line.TrimEnd().EndsWith($"specialization {specialization}", StringComparison.Ordinal)),
                $"Item {sample.Item} reaches the real compiler with specialization {specialization}");
            Check.Equal((long)remaining, await db.LongAsync(
                $"SELECT COALESCE(MAX(level),0) FROM char_abilities WHERE char_id=110 AND slot={sample.ArchiveSlot} AND ability_id={sample.Item << 7}"),
                $"Item {sample.Item} compilation consumes exactly one code charge");
            Check.Equal(2L - remaining, await db.LongAsync(
                $"SELECT COALESCE(SUM(count),0) FROM inventory WHERE charId=110 AND goid={sample.Item}"),
                $"Item {sample.Item} is materialized once per successful compile");
        }
    }
}
