using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed partial class HackerParitySweepMode : NativeQaMode
{
    private const int GroundY = 495, StandZ = -1498, StagingZ = 13984;
    private static readonly (string Name, int X)[] GroupStands =
        { ("QAFriend", 25200), ("QAFar", 25725), ("QAOther", 24500) };

    private sealed record SweepSection(ushort[] Abilities, string[] Instructions,
        Func<NativeQaBootstrapContext, HeadlessPlayer, Task<object>> Verify);

    private static List<SweepSection> Sections()
    {
        var sections = new List<SweepSection>
        {
            new(new ushort[] { 371, 372, 373, 374 }, new[]
            {
                "After CSR is in-world, submit {\"actor\":\"QAFriend\",\"action\":\"hacker-group-arrive\"} through requests. Wait for the arrival response before recruiting. The three helpers are already live off-site, so their nearby views are created after your client loads.",
                "Keep Self Defense active and stay on the seeded origin. QAFriend is 4 m away, QAFar 9.25 m away and QAOther 3 m away after arrival.",
                "From CSR's stock radial, recruit QAFriend and QAFar to the mission team. Submit {\"actor\":\"QAFriend\",\"action\":\"accept\",\"inviter\":\"CSR_Tester\"}, then repeat with QAFar. Leave QAOther outside the team. The loaded Resist Damage passive expands the source radii to 8.68 m and 9.92 m.",
                "Restore Group 2.0 (371) should heal CSR and QAFriend by 22. Group Repairs 2.0 (372) should heal CSR, QAFriend and QAFar by 19. QAOther receives neither. Read the heal and health-delta records separately from regeneration.",
                "From Actions, cast Combat Training (373) on self for +64 Melee Accuracy. Cast Weapons Boost (374) while it is active. Capture the original modifier removal before +64 Ballistic Accuracy. Let Weapons Boost expire.",
                "Corpsman 369 is already loaded as an ancestor. Record its granted action 1092 on entry as a separate sample. The radius bonus is an open consumer check."
            }, VerifyCorpsmanAsync)
        };
        AddHackerBSections(sections);
        AddHackerCSections(sections);
        return sections;
    }

    static partial void AddHackerBSections(List<SweepSection> sections);
    static partial void AddHackerCSections(List<SweepSection> sections);

    public override string Name => "ability-parity-hacker-sweep";

    public override NativeQaModePlan Plan { get; } = MakePlan();

    private static NativeQaModePlan MakePlan()
    {
        var sections = Sections();
        return new(new ushort[] { 2, 17 }.Concat(sections.SelectMany(section => section.Abilities)).Distinct().ToArray(),
            24800, -1498, Array.Empty<string>(), Array.Empty<string>(),
            new[]
            {
                "UPDATE characters SET healthC=100,healthM=5000,innerStrC=65535,innerStrM=2000,y=495,alignment=1,pvpflag=0 WHERE charId IN (110,6111,6112,6113,6114)",
                $"UPDATE characters SET x=CASE charId WHEN 6111 THEN {GroupStands[0].X} WHEN 6112 THEN {GroupStands[1].X} WHEN 6113 THEN {GroupStands[2].X} ELSE 24800 END,z=CASE WHEN charId=6114 THEN {StandZ} ELSE {StagingZ} END WHERE charId BETWEEN 6111 AND 6114",
                "DELETE FROM inventory WHERE charId=110"
            }, DevCommands: true, environmentOverrides: new()
            {
                ["MXO_AGGRO"] = "0",
                ["MXO_MOB_WANDER"] = "0",
                ["MXO_DESPERATION_QA_HOLD_HEALTH"] = "1"
            },
            readyMessages: new[] { "Natural HP regeneration is held in this disposable fixture to keep targets wounded during setup." }
                .Concat(sections.SelectMany(section => section.Instructions)).ToArray(), loadAbilityAncestors: true);
    }

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer ?? throw new InvalidOperationException("Expected the disposable Hacker sweep server.");
        foreach (var (name, x) in GroupStands)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, name, name);
            context.Helpers.Add(name, helper);
            await helper.EnterWorldAsync();
            var ownView = helper.World.CreationSnapshot().Last(view => view.ViewId == 2);
            Check.Equal((ushort)100, BinaryPrimitives.ReadUInt16LittleEndian(ownView.Attributes["Health"]),
                $"{name} entered wounded for the group-heal checks");
            Check.Equal((X: (double)x, Y: (double)GroundY, Z: (double)StagingZ), Position(ownView),
                $"{name} entered at its live staging position");
            Console.WriteLine($"READY {name} live at {x},{GroundY},{StagingZ}. Arrival stand {x},{GroundY},{StandZ}.");
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }
        // Native launch leaves CSR and its team empty for ordinary stock Actions and radial input.
        if (context.Server == null) return Array.Empty<byte>();

        // Use the bootstrap's existing CSR login to prove the post-load delivery to the real observer profile.
        var observer = context.StockProbe ?? throw new InvalidOperationException("Expected the stock-profile CSR probe.");
        await observer.EnterWorldAsync();
        await observer.WaitForViewNamedAsync("WinterFrost");
        Check.That(!observer.World.CreationSnapshot().Any(view => GroupStands.Any(stand => stand.Name == view.Name)),
            "CSR receives no premature player creation for the off-site helpers");
        await ArriveAsync(context.Helpers);
        var arrivals = new List<object>();
        foreach (var (name, x) in GroupStands)
        {
            var view = await observer.WaitForViewNamedAsync(name);
            Check.Equal((X: (double)x, Y: (double)GroundY, Z: (double)StandZ), Position(view),
                $"CSR receives {name}'s live remote view on the seeded stand after arrival");
            arrivals.Add(new { Handle = name, view.ViewId, view.SpawnId, X = x, Y = GroundY, Z = StandZ });
        }

        using var caster = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy");
        await caster.EnterWorldAsync();
        var proof = new List<object>();
        foreach (var section in Sections())
            proof.Add(await section.Verify(context, caster));
        Check.Equal(0, observer.World.Problems.Count + caster.World.Problems.Count + context.Helpers.Values.Sum(member => member.World.Problems.Count),
            "Hacker sweep participant packet framing");
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "hacker-parity-sweep.json"),
            JsonSerializer.Serialize(new { Arrivals = arrivals, Sections = proof }, new JsonSerializerOptions { WriteIndented = true }));
        return Array.Empty<byte>();
    }

    internal static async Task<object> ArriveAsync(IReadOnlyDictionary<string, HeadlessPlayer> helpers)
    {
        var arrived = new List<object>();
        foreach (var (name, x) in GroupStands)
        {
            var helper = helpers[name];
            helper.SendChat(FormattableString.Invariant($"?tp {x} {GroundY} {StandZ} 0 0 0"));
            await helper.WaitForTextAsync("Teleported to 0/0");
            await helper.WaitForViewNamedAsync("CSR_Tester");
            Console.WriteLine($"READY {name} live on stand {x},{GroundY},{StandZ} and in view range of CSR.");
            arrived.Add(new { Handle = name, X = x, Y = GroundY, Z = StandZ });
        }
        return arrived;
    }

    private static (double X, double Y, double Z) Position(ViewCreation view)
    {
        byte[] position = view.Attributes["Position"];
        return (BitConverter.ToDouble(position, 0), BitConverter.ToDouble(position, 8), BitConverter.ToDouble(position, 16));
    }

    private static async Task<object> VerifyCorpsmanAsync(NativeQaBootstrapContext context, HeadlessPlayer caster)
    {
        var server = context.ActualServer ?? throw new InvalidOperationException("Expected the disposable Hacker sweep server.");
        var grant = await caster.WaitForRpcAsync("80 b2 44 04 00 00 08 02");
        var members = new[] { context.Helpers["QAFriend"], context.Helpers["QAFar"] };
        foreach (var member in members)
        {
            caster.SendRpc(new byte[] { 0x6e }.Concat(SocialRequests.BuildGroupInvite(3, member.Handle, "")).ToArray());
            await member.WaitForRpcAsync("80 8f 0c 00");
            byte[] inviter = Encoding.ASCII.GetBytes(caster.Handle + "\0");
            member.SendRpc(new byte[] { 0x6f, 3, 8, 0, 0, 0, 0, 0 }
                .Concat(BitConverter.GetBytes((ushort)inviter.Length)).Concat(inviter).ToArray());
            await member.WaitForRpcAsync("80 8e");
        }

        var nearby = members[0];
        var far = members[1];
        var outsider = context.Helpers["QAOther"];
        var healed = new List<object>();
        var own = caster.World.CreationSnapshot().Last(view => view.ViewId == 2);
        ushort fullIs = BinaryPrimitives.ReadUInt16LittleEndian(own.Attributes["InnerStrengthMax"]);
        int lastCastAt = -1;

        async Task<int> FullIsCursorAsync()
        {
            if (lastCastAt >= 0)
                await VitalSinceAsync(caster, lastCastAt, "10", fullIs, "IS before the next cast");
            return caster.World.ViewSnapshot().Count;
        }
        var outsiderView = await caster.WaitForViewNamedAsync(outsider.Handle);
        var participants = new[] { caster, nearby, far, outsider };
        foreach (var sample in new[] { (Id: (ushort)371, Group: (ushort)685, Cost: 18, Amount: 22, Far: false),
            (Id: (ushort)372, Group: (ushort)686, Cost: 21, Amount: 19, Far: true) })
        {
            lastCastAt = await FullIsCursorAsync();
            int logAt = server.DebugLog().Length;
            var viewCursors = participants.ToDictionary(member => member, member => member.World.ViewSnapshot().Count);
            byte[] request = SendCast(caster, sample.Id, outsiderView);
            await CastBarAsync(caster, sample.Id);
            await VitalSinceAsync(caster, lastCastAt, "10", (ushort)(fullIs - sample.Cost), $"IS debit for {sample.Id}");
            await caster.WaitForRpcAsync($"80 bd 01 {Le(sample.Id)} 04 00 00 80 40 01");
            await caster.WaitForRpcAsync($"80 bd 01 {Le(sample.Group)} 00 00 00 80 40 01");
            await caster.WaitForRpcAsync($"80 bd 01 {Le(sample.Group)} 00 00 00 80 40 00");
            string log = server.DebugLog()[logAt..];
            var results = new List<object>();
            foreach (var target in participants)
            {
                var line = Regex.Match(log, @"\[ABILITY\] Healed (\d+) -> (\d+)\r?\n[^\r\n]*" +
                    Regex.Escape($"[ABILITY] Heal {sample.Id} -> {target.Handle} for {sample.Amount}") + @"(?:\r?\n|$)");
                bool expected = target == caster || target == nearby || sample.Far && target == far;
                Check.Equal(expected, line.Success, $"Group heal {sample.Id} correct admission for {target.Handle}");
                if (!expected)
                {
                    Check.That(!log.Contains($"Heal {sample.Id} -> {target.Handle} for ", StringComparison.Ordinal),
                        $"No group heal reached {target.Handle}");
                    continue;
                }
                int before = int.Parse(line.Groups[1].Value);
                int after = int.Parse(line.Groups[2].Value);
                Check.Equal(sample.Amount, after - before, $"Group heal {sample.Id} amount for {target.Handle}");
                await VitalSinceAsync(target, viewCursors[target], "40", (ushort)after, $"group-heal HP for {target.Handle}");
                results.Add(new { target.Handle, Before = before, After = after });
            }
            healed.Add(new { sample.Id, Request = Convert.ToHexString(request), Recipients = results });
        }

        lastCastAt = await FullIsCursorAsync();
        byte[] trainingRequest = SendCast(caster, 373);
        await CastBarAsync(caster, 373);
        await VitalSinceAsync(caster, lastCastAt, "10", (ushort)(fullIs - 45), "IS debit for Combat Training");
        var training = await ModifierAsync(caster, 373, 522, 0);
        lastCastAt = await FullIsCursorAsync();
        byte[] boostRequest = SendCast(caster, 374);
        await CastBarAsync(caster, 374);
        await VitalSinceAsync(caster, lastCastAt, "10", (ushort)(fullIs - 75), "IS debit for Weapons Boost");
        var replaced = await ModifierAsync(caster, 373, 522, 1);
        var boost = await ModifierAsync(caster, 374, 524, 0);
        Check.Equal(training.Hex[..34], replaced.Hex[..34], "Weapons Boost removes the original Combat Training record");
        return new { CorpsmanGrant = grant.Hex, GroupHeals = healed, CombatTrainingRequest = Convert.ToHexString(trainingRequest),
            WeaponsBoostRequest = Convert.ToHexString(boostRequest), CombatTrainingAdd = training.Hex,
            CombatTrainingRemove = replaced.Hex, WeaponsBoostAdd = boost.Hex };
    }

    private static async Task CastBarAsync(HeadlessPlayer caster, ushort ability)
    {
        var bar = await caster.WaitForRpcAsync("80 ac " + Le(ability));
        Check.Equal(4f, BinaryPrimitives.ReadSingleLittleEndian(bar.Bytes.AsSpan(bar.Bytes.Length - 4)), $"Cast time for {ability}");
    }

    private static Task VitalSinceAsync(HeadlessPlayer player, int cursor, string field, ushort value, string description)
    {
        var wire = BytePattern.Parse($"02 00 02 80 80 80 {field} {Le(value)}");
        return player.WaitUntilAsync(() => player.World.ViewSnapshot().Skip(cursor).Any(wire.Occurs), description,
            TimeSpan.FromSeconds(20));
    }

    private static Task<RpcRecord> ModifierAsync(HeadlessPlayer caster, ushort ability, ushort stat, byte operation) =>
        caster.WaitForRpcAsync(rpc => ManageBonusRecord.TryDecode(rpc.Bytes, out var record) && record!.SourceId == ability &&
            record.TargetId == stat && record.SignedValue == 64 && record.Duration == 300 && record.AddRemoveType == operation,
            $"modifier {ability}/{stat} with value 64 and operation {operation}");

    private static string Le(ushort value) => WireFormat.ToHex(BitConverter.GetBytes(value));

    private static byte[] SendCast(HeadlessPlayer caster, ushort ability, ViewCreation? target = null)
    {
        // Full stock 80 b9 selector shape from the native status fixture. Set only the ability and direct object.
        byte[] raw = Convert.FromHexString("80b93a010000000000000000000000000000000000000000000000000000000000000000086b0000000000");
        BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(2), ability);
        if (target != null)
            BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(4), target.ViewId | ((uint)target.SpawnId << 16));
        Check.That(AbilityUseRecord.TryDecode(raw, out _), "Stock ability activation is the complete 43-byte record");
        caster.SendRpc(raw);
        return raw;
    }
}
