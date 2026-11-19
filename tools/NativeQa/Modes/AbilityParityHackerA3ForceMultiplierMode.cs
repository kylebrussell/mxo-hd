using System.Text.Json;
using HeadlessClient;
using hds;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class AbilityParityHackerA3ForceMultiplierMode : NativeQaMode
{
    private const ushort Passive = 484, Sweep = 487, Improve = 489, Debuff = 481;
    private static readonly (string Handle, int X)[] Stands =
    {
        ("QAFriend", 25300), ("QAFar", 25600), ("QAOther", 25200), ("QAEnemy", 26000)
    };
    private static readonly ushort[] OpponentLoadout =
    {
        2, 17, 40, 41, 45, 52, 57, 58, 64, 65, 73, 77, 95, 97, 98, 327,
        390, 391, 392, 394, 477, 478, 480, Debuff, 483, 1006
    };
    private static readonly (ushort Stat, int Value)[] PassiveStats =
    {
        (1031, 10), (1144, 5), (1140, 5), (1056, 10), (1028, 10)
    };

    public override string Name => "ability-parity-hacker-a3-force-multiplier";

    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2, 17, Passive, Sweep, Improve }, 24800, -1498,
        Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            "UPDATE characters SET healthC=5000,healthM=5000,innerStrC=4000,innerStrM=2000,y=495,z=-1498 WHERE charId IN (110,6111,6112,6113,6114)",
            "UPDATE characters SET x=31000+100*(charId-6111),alignment=CASE WHEN charId=6113 THEN 2 ELSE 1 END,pvpflag=0 WHERE charId IN (6111,6112,6113,6114)",
            "DELETE FROM inventory WHERE charId=110",
            OpponentLoadoutSql()
        }, DevCommands: true, loadAbilityAncestors: true, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766",
            ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        }, readyMessages: new[]
        {
            "Helpers start outside the view radius. After CSR is fully visible and controllable, stay at spawn and use the helper chat action to send each stand command. For example {\"actor\":\"QAFriend\",\"action\":\"chat\",\"text\":\"?tp 25300 495 -1498 0 0 0\"}. Confirm all four fresh player spawns before inviting.",
            "Use QAFriend x25300 (5 m), QAFar x25600 (8 m), QAEnemy x26000 (12 m), and QAOther x25200 (4 m), with y495/z-1498 for each. QAEnemy is a friendly teammate despite the name; QAOther is the hostile nonmember. The required 74 ancestor gives +24% radius: 7.44 m for 489 and 11.16 m for 487.",
            "At spawn compare Supreme Damage Resistance (484) loaded, then use ?qaunload 484 and ?qaload 484. Capture Max IS and Status: +10% Viral Damage, Thrown Damage and Max IS, +5% Melee and Ballistic resistance.",
            "Invite QAFriend, QAFar and QAEnemy using the Mission Team UI. After each invite submit {\"actor\":\"QAFriend\",\"action\":\"accept\",\"inviter\":\"CSR_Tester\"}, substituting the invited name. Select the nonmember QAOther and cast Improved Hacking (489). CSR and QAFriend receive +25% Viral Damage and Healing; neither farther teammate nor QAOther receives it.",
            "Dismiss 489 before the next sample. Submit {\"actor\":\"QAOther\",\"action\":\"cast\",\"ability\":481,\"target\":\"CSR_Tester\"}. Verify Buffer Overflow is present, then cast Sweep Party 2.0 (487) with QAOther selected. CSR, QAFriend and QAFar get the pulse; QAEnemy and QAOther do not. Capture an actual 481 removal after a five-second pulse. The cleanse has a natural 30% roll, so repeat after reuse if needed."
        });

    private static string OpponentLoadoutSql() =>
        "DELETE FROM char_abilities WHERE char_id=6113; INSERT INTO char_abilities" +
        "(char_id,slot,ability_id,level,is_loaded,ability_name) VALUES " +
        string.Join(",", OpponentLoadout.Select((id, slot) =>
            $"(6113,{slot},{0x80000000L | ((long)id << 10)},50,1,'QA Force Multiplier opponent')"));

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer ?? throw new InvalidOperationException("Force Multiplier needs the isolated fixture server.");
        foreach (string handle in context.HelperNames)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, handle, handle, stockCharacterLoad: true);
            context.Helpers.Add(handle, helper);
            await helper.EnterWorldAsync();
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }

        if (context.StockProbe is not { } caster)
            return Array.Empty<byte>();

        await caster.EnterWorldAsync();
        var near = context.Helpers["QAFriend"];
        var middle = context.Helpers["QAFar"];
        var far = context.Helpers["QAEnemy"];
        var outsider = context.Helpers["QAOther"];
        var participants = new[] { caster, near, middle, far, outsider };
        await FenceAsync(participants);
        Check.That(caster.World.CreationSnapshot().All(view => !Stands.Any(stand => view.Name == stand.Handle)),
            "Stock CSR enters before the four helpers are visible");
        foreach (var stand in Stands)
        {
            context.Helpers[stand.Handle].SendChat($"?tp {stand.X} 495 -1498 0 0 0");
            var view = await caster.WaitForViewNamedAsync(stand.Handle);
            byte[] position = view.Attributes["Position"];
            Check.Equal((double)stand.X, BitConverter.ToDouble(position), $"{stand.Handle} exact stage X");
            Check.Equal(495d, BitConverter.ToDouble(position, 8), $"{stand.Handle} exact stage Y");
            Check.Equal(-1498d, BitConverter.ToDouble(position, 16), $"{stand.Handle} exact stage Z");
        }
        var target = await caster.WaitForViewNamedAsync(outsider.Handle);
        var casterView = await outsider.WaitForViewNamedAsync(caster.Handle);
        foreach (var member in new[] { near, middle, far })
            await TeamBreadthMode.InviteAsync(caster, member);

        foreach (var (stat, value) in PassiveStats)
            await WaitModifier(caster, 0, Passive, stat, value);
        int passiveAt = caster.World.RpcSnapshot().Count;
        caster.SendChat("?qaunload 484");
        await caster.WaitForRpcAsync("80 b3 " + EvidenceRetestWire.Le(Passive));
        foreach (var (stat, _) in PassiveStats)
            await WaitModifier(caster, passiveAt, Passive, stat, remove: true);
        passiveAt = caster.World.RpcSnapshot().Count;
        caster.SendChat("?qaload 484");
        await caster.WaitForRpcAsync("80 b2 " + EvidenceRetestWire.Le(Passive));
        foreach (var (stat, value) in PassiveStats)
            await WaitModifier(caster, passiveAt, Passive, stat, value);

        var improvement = EvidenceRetestWire.Cast(caster, Improve, target);
        foreach (var actor in new[] { caster, near })
        {
            await WaitModifier(actor, 0, Improve, 172, 25);
            await WaitModifier(actor, 0, Improve, 174, 25);
        }
        await FenceAsync(participants);
        foreach (var actor in new[] { middle, far, outsider })
            Check.That(!HasModifier(actor, 0, Improve, 174), $"{actor.Handle} is outside Improved Hacking");

        caster.SendRpc("80 bb " + EvidenceRetestWire.Le(Improve));
        await WaitModifier(caster, 0, Improve, 174, remove: true);
        byte[] debuff = EvidenceRetestWire.Cast(outsider, Debuff, casterView);
        await WaitModifier(caster, 0, Debuff, Debuff);

        byte[] sweep = EvidenceRetestWire.Cast(caster, Sweep, target);
        foreach (var actor in new[] { caster, near, middle })
            await WaitModifier(actor, 0, Sweep, Sweep, 30);
        await FenceAsync(participants);
        foreach (var actor in new[] { far, outsider })
            Check.That(!HasModifier(actor, 0, Sweep, Sweep), $"{actor.Handle} is outside Sweep Party");

        Check.Equal(0, participants.Sum(actor => actor.World.Problems.Count), "Force Multiplier packet framing");
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "force-multiplier-proof.json"),
            JsonSerializer.Serialize(new
            {
                Passive = PassiveStats.Select(stat => new { stat.Stat, stat.Value }),
                ImprovementRpc = Convert.ToHexString(improvement), Improved = new[] { caster.Handle, near.Handle },
                DebuffRpc = Convert.ToHexString(debuff), SweepRpc = Convert.ToHexString(sweep),
                Swept = new[] { caster.Handle, near.Handle, middle.Handle }
            }));
        return Array.Empty<byte>();
    }

    private static async Task FenceAsync(IEnumerable<HeadlessPlayer> actors)
    {
        foreach (var actor in actors)
        {
            actor.SendRpc("81 48");
            await actor.WaitForRpcAsync("81 49");
        }
    }

    private static bool HasModifier(HeadlessPlayer actor, int from, ushort id, ushort stat, int? value = null,
        bool remove = false) => actor.World.RpcSnapshot().Skip(from).Any(rpc =>
        ManageBonusRecord.TryDecode(rpc.Bytes, out var record) && record!.SourceId == id &&
        record.TargetId == stat && (!value.HasValue || record.SignedValue == value) &&
        record.AddRemoveType == (remove ? 1 : 0));

    private static Task WaitModifier(HeadlessPlayer actor, int from, ushort id, ushort stat, int? value = null,
        bool remove = false) => actor.WaitUntilAsync(() => HasModifier(actor, from, id, stat, value, remove),
        $"{actor.Handle} modifier {id}/{stat} value {value}, remove={remove}", HeadlessPlayer.DefaultTimeout);
}
