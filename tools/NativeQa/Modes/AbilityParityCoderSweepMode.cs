using System.Buffers.Binary;
using System.Text.Json;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class AbilityParityCoderSweepMode : NativeQaMode
{
    private sealed record Summon(ushort Ability, string Label, byte Level, float Reuse, string? Name);

    // simulacra.py LogicDaemon1_Subject and RemoteProxy1/2/3_Subject clamp a level 50
    // caster to these sheet levels. The fixture checks the actual owned view and window.
    private static readonly Summon[] Summons =
    {
        new(22, "Logic Daemon 1.0", 7, 10, "Nuker Simulacrum"),
        new(27, "Remote Proxy 1.0", 4, 8, null),
        new(28, "Remote Proxy 2.0", 7, 8, null),
        new(29, "Remote Proxy 3.0", 17, 8, null),
    };

    private static readonly (ushort Stat, int Value)[] CoderStyle =
    {
        (522, 64), (159, 26), (1009, 47), (1010, 47), (1011, 47), (342, 64), (172, 26), (183, 47), (1005, 66)
    };

    // Complete stock self-summon request captured for 594 in PR312. The compact ability
    // field selects the rank and all remaining fields are retained from the same producer.
    private const string SummonRequest =
        "80b95202000000000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
    // Stock PR333, PacketLog fe292a523b89:5075–5076. Only the selected view/spawn changes.
    private const string QuickCodingRequest =
        "80b90102040002000000000000000000000000000000000000000000785bc30001000000086b0000000000";

    private static readonly (ushort Ability, ushort Stat, int Value)[] CraftingPassives =
    {
        (24, 955, 33), (37, 649, 15), (275, 530, 10), (276, 707, 50),
        (277, 706, 50), (515, 954, 25), (515, 956, 25), (516, 653, 50), (528, 652, 50)
    };

    private static string CraftingItems => string.Join(',', new[] { (2, 531u), (3, 1271u) }.Concat(
        Scenarios.HealthPillMaterials.Select((item, index) => (4 + index, item)))
        .Select(item => $"(110,{item.Item2},{item.Item1},1,0),(6111,{item.Item2},{item.Item1},1,0)"));

    public override string Name => "ability-parity-coder-sweep";
    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2, 17, 1008, 19, 513 }.Concat(Summons.Select(summon => summon.Ability))
            .Concat(CraftingPassives.Select(passive => passive.Ability)).Distinct().ToArray(), 24800, -1498,
        new[] { "DELETE FROM inventory WHERE charId=110 AND slot<97" },
        new[] { "INSERT INTO char_abilities (char_id,slot,ability_id,level,is_loaded,ability_name) " +
            $"VALUES (110,400,{39413u << 7},1,0,'Health Boost 3.0 code')" },
        new[] { "UPDATE characters SET y=495,innerStrC=1200,innerStrM=2000,cash=100000 WHERE charId IN (110,6111)",
            $"INSERT INTO inventory (charId,goid,slot,count,purity) VALUES {CraftingItems}" },
        DevCommands: true,
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_DESPERATION_QA_HOLD_HEALTH"] = "1" },
        readyMessages: new[]
        {
            "In stock Actions, select Coder Style and cast each low-rank summon outside combat.",
            "Open Status before and after selecting Coder Style. Capture its nine modifier adds and the corresponding removals on returning to Self Defense.",
            "While in Coder Style, select wounded QAOther and cast Quick Coding (513) once. Expect 13 actual HP, from 113 to 126 after the bootstrap's heal. Regeneration is held in this disposable fixture.",
            "At level 50 expect Logic Daemon 1.0 (22) to show a level-7 Nuker. Proxy 1.0/2.0/3.0 (27/28/29) should show levels 4/7/17. The proxy sheet can vary.",
            "For each spell capture the full activation, eight-second cast, pet appearance and registration, and its own reuse timer. Dismiss from the Simulacra window before the next summon.",
            "For Remote Proxy 2.0 record whether a newly created proxy remains or disappears immediately. Its shipped callback calls destroyCurrentSimulacrum after create, so record both creation and removal order.",
            "For Drain Code 19, try without a pet first. Then target the last proxy while at least 50 IS below maximum. Record the pet's removal and the +50 IS return separately from normal regeneration.",
            "Open the Analyser in slot 2 and write Health Pill 1.0. Record the quoted and actual Info costs, ingredient removals, and ten charges. Open the Compiler in slot 3 and compile Health Boost 3.0. Record the quoted and actual costs, elapsed time, last-charge removal and single pill delivery."
        }, loadAbilityAncestors: true);

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer ?? throw new InvalidOperationException("The Coder sweep requires a disposable server.");
        var db = context.OwnedDatabase ?? throw new InvalidOperationException("The Coder sweep requires a disposable database.");
        using var owner = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend", stockCharacterLoad: true);
        await owner.EnterWorldAsync();
        int styleStart = owner.World.RpcSnapshot().Count;
        owner.SendRpc("3e 6b f0 03 00 00 00 00");
        await owner.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");
        var coderAdds = await WaitForStyleAsync(owner, styleStart, remove: false);
        var healTarget = await HeadlessPlayer.LoginAsync(context.Address, "QAOther", "QAOther", stockCharacterLoad: true);
        context.Helpers.Add("QAOther", healTarget);
        await healTarget.EnterWorldAsync();
        var healView = await owner.WaitForViewNamedAsync("QAOther");
        Check.Equal((ushort)100, BitConverter.ToUInt16(healView.Attributes["Health"]), "Quick Coding has a wounded friendly target");
        byte[] quickCoding = SendCast(owner, 513, healView, QuickCodingRequest);
        var quickCodingBar = await owner.WaitForRpcAsync("80 ac 01 02");
        Check.Equal(4f, BitConverter.ToSingle(quickCodingBar.Bytes, quickCodingBar.Bytes.Length - 4),
            "Quick Coding uses the shipped four-second cast");
        await owner.WaitForRpcAsync("80 bd 01 01 02 04 00 00 c0 40 01");
        await healTarget.WaitForViewUpdateAsync(2, "02 80 80 80 40 71 00");
        Check.That(server.DebugLog().Contains("[ABILITY] Heal 513 -> QAOther for 13", StringComparison.Ordinal),
            "Quick Coding applies the shipped 13 HP with no added caster healing influence");
        SendCast(owner, 19);
        await owner.WaitForTextAsync("You need an active simulacrum");
        Check.Equal(0, owner.Received("80 bd 01 13 00").Count, "Drain Code without an owned pet starts no reuse");
        var proof = new List<object>();
        foreach (Summon sample in Summons)
        {
            int rpcStart = owner.World.RpcSnapshot().Count;
            int viewStart = owner.World.CreationSnapshot().Count;
            byte[] request = SendCast(owner, sample.Ability);
            var window = await owner.WaitForRpcAsync("81 76", TimeSpan.FromSeconds(20));
            Check.Equal(46, window.Bytes.Length, "The owner receives the stock window registration");
            byte petId = window.Bytes[3];
            ViewCreation? pet = null;
            await owner.WaitUntilAsync(() =>
            {
                pet = owner.World.CreationSnapshot().Skip(viewStart).FirstOrDefault(view =>
                    view.Goid == 40376 && view.Attributes["SimulacraID"][0] == petId);
                return pet != null;
            }, $"{sample.Label} creates a fresh owned simulacrum view", HeadlessPlayer.DefaultTimeout);
            Check.Equal(owner.CharId, BitConverter.ToUInt32(pet!.Attributes["OwnerCharacterID"]), "The new pet is owned by the caster");
            Check.Equal(owner.CharId, BitConverter.ToUInt32(pet.Attributes["MasterCharacterID"]), "The master is the caster");
            Check.Equal(sample.Level, pet.Attributes["Level"][0], $"{sample.Label} uses the shipped level clamp");
            Check.That(sample.Name == null
                ? hds.CoderPets.ProxySheets.Any(sheet => pet.Name == $"{sheet} Simulacrum")
                : pet.Name == sample.Name, "The summoned view uses the specified role");
            string ability = WireFormat.ToHex(BitConverter.GetBytes(sample.Ability));
            string reuse = WireFormat.ToHex(BitConverter.GetBytes(sample.Reuse));
            var cast = BytePattern.Parse($"80 ac {ability} 00 00 00 00 00 00 00 00 00 00 00 00 00 41");
            var timer = BytePattern.Parse($"80 bd 01 {ability} 04 {reuse} 01");
            await owner.WaitUntilAsync(() =>
            {
                var packets = owner.World.RpcSnapshot().Skip(rpcStart).Select(record => record.Bytes).ToArray();
                return packets.Any(cast.IsMatch) && packets.Any(timer.IsMatch);
            }, $"{sample.Label} publishes its cast bar and own reuse timer", HeadlessPlayer.DefaultTimeout);
            byte[]? drain = null;
            int isStart = owner.World.ViewSnapshot().Count;
            if (sample.Ability == 29)
                drain = SendCast(owner, 19, pet);
            else
                owner.SendRpc("81 7a fe 01");
            var dismiss = await owner.WaitForRpcAsync($"81 77 {petId:x2}");
            if (drain != null)
            {
                await owner.WaitForRpcAsync("80 bd 01 13 00 04 00 00 68 42 01");
                await owner.WaitUntilAsync(() =>
                {
                    var previous = owner.World.ViewSnapshot().Take(isStart).SelectMany(InnerStrengthUpdates).TakeLast(1);
                    var subsequent = owner.World.ViewSnapshot().Skip(isStart).SelectMany(InnerStrengthUpdates);
                    var values = previous.Concat(subsequent).ToArray();
                    return values.Zip(values.Skip(1), (before, after) => after - before).Contains(50);
                }, "Drain Code restores a 50-point IS delta beyond natural regen", HeadlessPlayer.DefaultTimeout);
            }
            proof.Add(new { sample.Ability, sample.Level, sample.Reuse, pet.Name, PetId = petId,
                Activation = Convert.ToHexString(request), Registration = window.Hex, Dismissal = dismiss.Hex,
                DrainCode = drain == null ? null : Convert.ToHexString(drain) });
        }
        int removeStart = owner.World.RpcSnapshot().Count;
        owner.SendRpc("3e 6b 11 00 00 00 00 00");
        await owner.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 11 00 00 00");
        var coderRemoves = await WaitForStyleAsync(owner, removeStart, remove: true);
        object crafting = await VerifyCraftingAsync(owner, db, server);
        Check.Equal(0, owner.World.Problems.Count, "Coder summon sweep protocol framing");
        NativeQaReadiness.RequireNoAmbientAggro(server, owner);
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "coder-summon-evidence-bootstrap.json"),
            JsonSerializer.Serialize(new { Summons = proof, CoderStyleAdds = coderAdds, CoderStyleRemoves = coderRemoves,
                QuickCoding = Convert.ToHexString(quickCoding), Crafting = crafting },
                new JsonSerializerOptions { WriteIndented = true }));
        return Array.Empty<byte>();
    }

    private static async Task<object> VerifyCraftingAsync(HeadlessPlayer owner, ThrowawayDatabase db, ServerInstance server)
    {
        await owner.WaitUntilAsync(() => CraftingPassives.All(sample => owner.World.RpcSnapshot().Any(record =>
            ManageBonusRecord.TryDecode(record.Bytes, out var bonus) && bonus!.Family == 0x45 &&
            bonus.SourceId == sample.Ability && bonus.TargetId == sample.Stat && bonus.SignedValue == sample.Value)),
            "all nine crafting passive values came from enabled abilities", HeadlessPlayer.DefaultTimeout);

        uint id = owner.CharId;
        Scenarios.WriterTool(owner, 2, true);
        var writer = await owner.WaitForRpcAsync("80 bc ?? ?? ?? ?? 00 ca 03 00 00 c8 03 ?? ?? ?? ?? 00");
        owner.SendRpc(Scenarios.HealthPillWriterRequest);
        await owner.WaitForRpcAsync("81 2f 00 00 5f 09 00 00");
        Check.Equal(99_994L, await db.LongAsync($"SELECT cash FROM characters WHERE charId={id}"),
            "the Writer applies both 530 and 956 to the stock recipe's 9 Info base cost");
        Check.Equal(10L, await db.LongAsync($"SELECT level FROM char_abilities WHERE char_id={id} AND ability_id={2399u << 7}"),
            "the Writer creates the shipped ten-charge code");
        Check.Equal(0L, await db.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={id} AND slot BETWEEN 4 AND 8"),
            "the Writer consumes only the five selected ingredients");

        Scenarios.CompilerTool(owner, 3, true);
        var compiler = await owner.WaitForRpcAsync("80 bc ?? ?? ?? ?? 00 c9 03 00 00 c8 03 ?? ?? ?? ?? 00");
        const string compile = "60 f5 99 00 00 90 01 00 03 01";
        owner.SendRpc(compile);
        await owner.WaitForRpcAsync("61 00", TimeSpan.FromSeconds(14));
        Check.That(server.DebugLog().Contains("[COMPILE60] Started 39413 seconds 9.045 skill 50 954 25 955 33 cost 14796",
            StringComparison.Ordinal), "the ordinary compile uses both timer passives and the Programmer price discount");
        Check.Equal(85_198L, await db.LongAsync($"SELECT cash FROM characters WHERE charId={id}"),
            "the Compiler debits the exact discounted price");
        Check.Equal(0L, await db.LongAsync($"SELECT COUNT(*) FROM char_abilities WHERE char_id={id} AND slot=400"),
            "the last code charge is removed");
        Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={id} AND goid=39413 AND count=1"),
            "one Health Boost pill is materialized");
        return new { Writer = writer.Hex, Compiler = compiler.Hex, WriterInfoCost = 6, CompileInfoCost = 14796, CompileSeconds = 9.045 };
    }

    private static byte[] SendCast(HeadlessPlayer owner, ushort ability, ViewCreation? target = null, string template = SummonRequest)
    {
        byte[] request = Convert.FromHexString(template);
        Check.That(AbilityUseRecord.TryDecode(request, out _), "Complete stock Coder ability framing");
        BinaryPrimitives.WriteUInt16LittleEndian(request.AsSpan(2), ability);
        if (target != null)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(request.AsSpan(4), target.ViewId);
            BinaryPrimitives.WriteUInt16LittleEndian(request.AsSpan(6), target.SpawnId);
        }
        owner.SendRpc(request);
        return request;
    }

    private static IEnumerable<ushort> InnerStrengthUpdates(byte[] packet)
    {
        byte[] header = { 2, 0, 2, 0x80, 0x80, 0x80, 0x10 };
        for (int offset = BytePattern.IndexOf(packet, header, 0);
            offset >= 0 && offset + header.Length + 2 <= packet.Length;
            offset = BytePattern.IndexOf(packet, header, offset + header.Length + 2))
            yield return BitConverter.ToUInt16(packet, offset + header.Length);
    }

    private static async Task<string[]> WaitForStyleAsync(HeadlessPlayer owner, int start, bool remove)
    {
        string[] found = Array.Empty<string>();
        await owner.WaitUntilAsync(() =>
        {
            var records = owner.World.RpcSnapshot().Skip(start).ToArray();
            found = CoderStyle.Select(stat => records.FirstOrDefault(record =>
                ManageBonusRecord.TryDecode(record.Bytes, out var value) && value!.SourceId == 1008 &&
                value.TargetId == stat.Stat && value.SignedValue == stat.Value && value.Duration == 0 &&
                value.AddRemoveType == (remove ? 1 : 0))?.Hex).OfType<string>().ToArray();
            return found.Length == CoderStyle.Length;
        }, $"Coder Style publishes all nine level-50 modifiers, remove={remove}", HeadlessPlayer.DefaultTimeout);
        return found;
    }
}
