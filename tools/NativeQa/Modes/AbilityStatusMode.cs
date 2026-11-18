using System.Buffers.Binary;
using System.Text.Json;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class AbilityStatusMode : NativeQaMode
{
    public override string Name => "ability-status";

    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2, 17, 314, 354, 476, 1006, 1099, 1102 },
        PvpOpponentsFixture.X, PvpOpponentsFixture.Z, Array.Empty<string>(), Array.Empty<string>(),
        new[] { PvpOpponentsFixture.NativeSql }, DevCommands: true,
        environmentOverrides: new()
        {
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_AGGRO"] = "0", ["MXO_FREECAST_VIRAL_QA"] = "1",
            ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        },
        readyMessages: new[]
        {
            "Capture the Status Combat and Bonuses tabs before each cast, after it lands and after timed recovery.",
            "On CSR110: Hyper-Block 1099 adds 52 melee defense, Determination 1102 adds 150 Determination, Upgrade Attacks 314 adds 108 viral accuracy for 20s. Compare displayed deltas.",
            "Request status-debuff as QAEnemy, ability 354 or 476. Destroy Resolve 354 adds -58 max IS and -50 IS regeneration for 30s. Destroy Health 476 adds -520 max HP and -20 Health Regeneration for 40s. Expected maxima 2000 to 1942 and 5000 to 4480.",
            "See docs/ABILITY-STATUS.md for the helper request and exact headless checks."
        });

    // Full stock RPC images from the listed framing samples in ABILITY-STATUS.md.
    private const string HyperBlock =
        "80 b9 4b 04 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 40 30 37 26 00 00 00 00 08 6b 00 00 00 00 00";
    private const string Determination =
        "80 b9 4e 04 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 32 00 00 00 00 00 00 00 08 6b 00 00 00 00 00";
    private const string Targeted =
        "80 b9 3a 01 0d 00 5a 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 08 6b 00 00 00 00 00";

    private static byte[] SendCast(HeadlessPlayer actor, string sample, ushort ability, ViewCreation? target = null)
    {
        byte[] raw = WireFormat.FromHex(sample);
        Check.That(AbilityUseRecord.TryDecode(raw, out _), "Complete captured 43-byte ability RPC");
        BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(2), ability);
        if (target != null)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(4), target.ViewId);
            BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(6), target.SpawnId);
        }
        actor.SendRpc(raw);
        return raw;
    }

    public static async Task<object> CastDebuffAsync(HeadlessPlayer helper, ushort ability)
    {
        Check.That(helper.Handle == "QAEnemy" && ability is 354 or 476, "Use QAEnemy and ability 354 or 476");
        var target = await helper.WaitForViewNamedAsync("CSR_Tester");
        return new { Ability = ability, RpcHex = Convert.ToHexString(SendCast(helper, Targeted, ability, target)) };
    }

    private static async Task<RpcRecord> ModifierAsync(HeadlessPlayer player, int start, ushort source,
        ushort stat, int value, float seconds, bool remove = false)
    {
        RpcRecord? match = null;
        await player.WaitUntilAsync(() =>
        {
            match = player.World.RpcSnapshot().Skip(start).FirstOrDefault(r =>
                ManageBonusRecord.TryDecode(r.Bytes, out var m) && m!.SourceId == source &&
                m.TargetId == stat && m.SignedValue == value && m.Duration == seconds &&
                m.AddRemoveType == (remove ? 1 : 0));
            return match != null;
        }, $"modifier {source}/{stat} {value:+#;-#;0} for {seconds}s, remove={remove}", TimeSpan.FromSeconds(remove ? 48 : 12));
        return match!;
    }

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var helper = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy");
        context.Helpers.Add("QAEnemy", helper);
        await PvpOpponentsFixture.EnterAsync(helper);
        var self = helper.World.CreationSnapshot().Last(v => v.ViewId == 2);
        int start = helper.World.RpcSnapshot().Count;
        SendCast(helper, Targeted, 1006, self);
        var ready = await ModifierAsync(helper, start, 1006, 342, 70, 0);
        if (context.Server == null) return ready.Bytes;

        // --bootstrap-checks supplies an isolated address. Actual native setup leaves CSR available to the client.
        using var player = await HeadlessPlayer.LoginAsync(context.Address, "CSR_AbilityQA", "CSR_Tester");
        await PvpOpponentsFixture.EnterAsync(player);
        var playerSelf = player.World.CreationSnapshot().Last(v => v.ViewId == 2);
        Check.Equal((ushort)5000, BinaryPrimitives.ReadUInt16LittleEndian(playerSelf.Attributes["MaxHealth"]), "Baseline max HP");
        Check.Equal((ushort)2000, BinaryPrimitives.ReadUInt16LittleEndian(playerSelf.Attributes["InnerStrengthMax"]), "Baseline max IS");
        start = player.World.RpcSnapshot().Count;
        int viewStart = player.World.ViewSnapshot().Count;
        var requests = new List<object>();
        var records = new List<string>();

        async Task Buff(string sample, ushort ability, ushort stat, int value, float seconds, bool selfTarget = false)
        {
            requests.Add(new { Ability = ability, RpcHex = Convert.ToHexString(
                SendCast(player, sample, ability, selfTarget ? playerSelf : null)) });
            records.Add((await ModifierAsync(player, start, ability, stat, value, seconds)).Hex);
        }

        await Buff(HyperBlock, 1099, 1009, 52, 0);
        await Buff(Determination, 1102, 575, 150, 0);
        await Buff(Targeted, 314, 342, 108, 20, selfTarget: true);
        requests.Add(await CastDebuffAsync(helper, 354));
        records.Add((await ModifierAsync(player, start, 354, 109, -58, 30)).Hex);
        records.Add((await ModifierAsync(player, start, 354, 114, -50, 30)).Hex);
        requests.Add(await CastDebuffAsync(helper, 476));
        records.Add((await ModifierAsync(player, start, 476, 106, -520, 40)).Hex);
        records.Add((await ModifierAsync(player, start, 476, 112, -20, 40)).Hex);

        records.Add((await ModifierAsync(player, start, 314, 342, 108, 20, remove: true)).Hex);
        records.Add((await ModifierAsync(player, start, 354, 109, -58, 30, remove: true)).Hex);
        records.Add((await ModifierAsync(player, start, 476, 106, -520, 40, remove: true)).Hex);
        // Object12 own attributes 14/23. Health and IS may be clamped in the same update.
        var views = player.World.ViewSnapshot().Skip(viewStart).ToArray();
        Check.That(views.Any(BytePattern.Parse("02 00 02 80 80 ?? 80 11").Occurs),
            "Destroy Health publishes actual maximum on the self view");
        Check.That(views.Any(BytePattern.Parse("02 00 02 80 80 01 88 13").Occurs),
            "Destroy Health expiry republishes baseline maximum");
        Check.That(views.Any(BytePattern.Parse("02 00 02 80 80 80 ?? 96 07").Occurs),
            "Destroy Resolve publishes actual signed maximum on the self view");
        Check.That(views.Any(BytePattern.Parse("02 00 02 80 80 80 04 d0 07").Occurs),
            "Destroy Resolve expiry republishes baseline maximum");
        Check.Equal(0, player.World.Problems.Count, "Status fixture protocol problems");
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "ability-status-bootstrap.json"),
            JsonSerializer.Serialize(new { Requests = requests, Records = records, Views = views.Select(Convert.ToHexString).ToArray() }));
        return ready.Bytes;
    }
}
