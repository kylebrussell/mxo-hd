using System.Text.Json;
using System.Text.RegularExpressions;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class HackerMedicalParityMode : NativeQaMode
{
    private const ushort Guardian = 51, GuardianUpgrade = 1094, Physician = 418, PhysicianHeal = 1098;
    private const int OriginX = 24800, Y = 495, OriginZ = -1498, FarTestZ = -398;
    private static readonly (string Name, int X, int Z)[] Stands =
    {
        ("QAFriend", 25200, OriginZ), ("QAFar", OriginX, -1073),
        ("QAOther", 24560, -1318), ("QAEnemy", OriginX, -1798)
    };

    public override string Name => "ability-parity-hacker-medical";

    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2, 17, 46, Guardian, 237, Physician }, OriginX, OriginZ,
        Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            "UPDATE characters SET healthC=65535,healthM=5000,innerStrC=65535,innerStrM=2000,y=495,alignment=1,pvpflag=0 WHERE charId=110",
            "UPDATE characters SET x=31000+100*(charId-6111),y=495,z=-1498,healthC=5000,healthM=5000,alignment=CASE WHEN charId=6114 THEN 2 ELSE 1 END,pvpflag=0 WHERE charId BETWEEN 6111 AND 6114",
            "DELETE FROM char_abilities WHERE char_id BETWEEN 6111 AND 6114",
            "DELETE FROM inventory WHERE charId=110"
        }, DevCommands: true, loadAbilityAncestors: true, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15"
        }, readyMessages: new[]
        {
            "Medical parity preserves natural HP regeneration. After CSR is fully in world, move the four helpers from their off-site positions using chat requests. Example {\"actor\":\"QAFriend\",\"action\":\"chat\",\"text\":\"?tp 25200 495 -1498 0 0 0\"}.",
            "Other stands: QAFar 24800 495 -1073 (4.25 m north), QAOther 24560 495 -1318 (3 m northwest, friendly nonmember clear of the WinterHelper stack), QAEnemy 24800 495 -1798 (3 m south, flagged hostile). QAFriend is 4 m east. Recruit only QAFriend and QAFar and send normal helper accept requests with inviter CSR_Tester.",
            "Guardian Patcher 51 is a loaded passive. Observe +1% maximum HP and grant 1094 disappear on ?qaunload 51 and return on ?qaload 51. Cast the 1094 grant with QAOther selected and observe its self-only effect, then dismiss it.",
            "Physician 418 gives +5% Healing and grants 1098. Observe unload and reload. Use ?health 100 on CSR and helper chat to wound QAFriend, QAFar and QAOther shortly before casting Physician Heal 1098 with QAOther selected. Compare actual cast deltas separately from natural regen. CSR and QAFriend receive +216, QAFar and QAOther receive no cast heal.",
            "Move QAFar to 24800 495 -398 (11 m north). Wound QAFriend again. Cast Fast Healing 1.0 (46) and Fast Healing 2.0 (237) on QAFriend separately, trying far and hostile refusal before each positive cast. 5000-max helper ticks start at +100, rise to +140 or +160 for the individual ranks, and return to +100 after helper ?dismissbuff. Keep Self Defense active. See docs/ABILITY-HACKER-MEDICAL-PARITY.md."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer ?? throw new InvalidOperationException("Expected the disposable medical fixture server.");
        foreach (var stand in Stands)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, stand.Name, stand.Name, stockCharacterLoad: true);
            context.Helpers.Add(stand.Name, helper);
            await helper.EnterWorldAsync();
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }
        if (context.StockProbe is not { } caster)
            return Array.Empty<byte>();

        await caster.EnterWorldAsync();
        Check.That(caster.World.CreationSnapshot().All(view => !Stands.Any(stand => stand.Name == view.Name)),
            "No helper enters the stock player's initial view");
        foreach (var stand in Stands)
        {
            await MoveAsync(context.Helpers[stand.Name], stand.X, stand.Z);
            var view = await caster.WaitForViewNamedAsync(stand.Name);
            byte[] position = view.Attributes["Position"];
            Check.Equal((double)stand.X, BitConverter.ToDouble(position), $"{stand.Name} medical stand X");
            Check.Equal((double)Y, BitConverter.ToDouble(position, 8), $"{stand.Name} medical stand Y");
            Check.Equal((double)stand.Z, BitConverter.ToDouble(position, 16), $"{stand.Name} medical stand Z");
        }

        var friend = context.Helpers["QAFriend"];
        var far = context.Helpers["QAFar"];
        var outsider = context.Helpers["QAOther"];
        var enemy = context.Helpers["QAEnemy"];
        var nearView = await caster.WaitForViewNamedAsync(friend.Handle);
        var farView = await caster.WaitForViewNamedAsync(far.Handle);
        var outsideView = await caster.WaitForViewNamedAsync(outsider.Handle);
        var enemyView = await caster.WaitForViewNamedAsync(enemy.Handle);
        foreach (var member in new[] { friend, far })
            await TeamBreadthMode.InviteAsync(caster, member);

        await VerifyPassiveAsync(caster, Guardian, GuardianUpgrade, 1057, 1);
        Console.WriteLine("  Guardian grant and maximum HP lifecycle verified");
        var upgrade = EvidenceRetestWire.Cast(caster, GuardianUpgrade, outsideView);
        await ModifierAsync(caster, 0, GuardianUpgrade, 520, 20, 0);
        Check.That(!HasModifier(outsider, 0, GuardianUpgrade, 520, 20, 0, false), "Guardian upgrade ignores the selected nonmember");
        caster.SendRpc("80 bb " + EvidenceRetestWire.Le(GuardianUpgrade));
        await ModifierAsync(caster, 0, GuardianUpgrade, 520, 20, 0, remove: true);
        await VerifyPassiveAsync(caster, Physician, PhysicianHeal, 1141, 5);

        Console.WriteLine("  Physician grant and Healing Bonus lifecycle verified");
        var participants = new[] { caster, friend, far, outsider };
        foreach (var actor in participants)
            await WoundAsync(actor);
        int healStart = server.DebugLog().Length;
        var heal = EvidenceRetestWire.Cast(caster, PhysicianHeal, outsideView);
        int HealDelta(string handle)
        {
            string nextLine = Regex.Escape($"[ABILITY] Heal {PhysicianHeal} -> {handle} for 216");
            var match = Regex.Match(server.DebugLog()[healStart..],
                @"\[ABILITY\] Healed (\d+) -> (\d+)\r?\n[^\r\n]*" + nextLine + @"(?:\r?\n|$)");
            return match.Success ? int.Parse(match.Groups[2].Value) - int.Parse(match.Groups[1].Value) : 0;
        }
        await caster.WaitUntilAsync(() => HealDelta(caster.Handle) == 216 && HealDelta(friend.Handle) == 216,
            "Physician Heal changes caster and exact 4 m teammate HP by 216 each", HeadlessPlayer.DefaultTimeout);
        foreach (var excluded in new[] { far, outsider })
            Check.That(!server.DebugLog()[healStart..].Contains($"Heal {PhysicianHeal} -> {excluded.Handle} for ",
                StringComparison.Ordinal), $"{excluded.Handle} receives no Physician cast heal");

        await MoveAsync(far, OriginX, FarTestZ);
        // The next feature reply fences the server-side move before the new range checks.
        far.SendRpc("81 48");
        await far.WaitForRpcAsync("81 49");

        var regen = new List<object>();
        foreach (var (id, rate) in new (ushort Id, int Rate)[] { (46, 40), (237, 60) })
        {
            foreach (var excluded in new[] { farView, enemyView })
            {
                EvidenceRetestWire.Cast(caster, id, excluded);
                await caster.WaitForRpcAsync("80 ba " + EvidenceRetestWire.Le(id));
                await caster.WaitForTextAsync("Choose a living friendly target within range.");
                await Task.Delay(TimeSpan.FromSeconds(0.6));
            }
            await WoundAsync(friend);
            int plain = await WaitRegenStepAsync(friend, 100);
            int friendAt = friend.World.RpcSnapshot().Count;
            int casterAt = caster.World.RpcSnapshot().Count;
            var cast = EvidenceRetestWire.Cast(caster, id, nearView);
            await EvidenceRetestWire.CastBarAsync(caster, id, 2);
            await ModifierAsync(friend, friendAt, id, 112, rate, 600);
            int buffed = await WaitRegenStepAsync(friend, 100 + rate);
            Check.That(!HasModifier(caster, casterAt, id, 112, rate, 600, false), "Fast Healing does not buff its caster");
            friend.SendChat($"?dismissbuff {id}");
            await ModifierAsync(friend, friendAt, id, 112, rate, 600, remove: true);
            int restored = await WaitRegenStepAsync(friend, 100);
            regen.Add(new { Id = id, Rpc = Convert.ToHexString(cast), Before = plain, During = buffed, After = restored });
        }

        Check.Equal(0, caster.World.Problems.Count + context.Helpers.Values.Sum(helper => helper.World.Problems.Count),
            "Medical fixture packet framing");
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "hacker-medical-parity.json"),
            JsonSerializer.Serialize(new
            {
                Guardian = GuardianUpgrade, GuardianRpc = Convert.ToHexString(upgrade),
                Physician = PhysicianHeal, HealRpc = Convert.ToHexString(heal), Recipients = new[] { caster.Handle, friend.Handle },
                Heal = 216, Regeneration = regen
            }));
        return Array.Empty<byte>();
    }

    private static async Task MoveAsync(HeadlessPlayer actor, int x, int z)
    {
        actor.SendChat(FormattableString.Invariant($"?tp {x} {Y} {z} 0 0 0"));
        await actor.WaitForTextAsync("Teleported to 0/0");
    }

    private static async Task WoundAsync(HeadlessPlayer actor)
    {
        int cursor = actor.World.ViewSnapshot().Count;
        actor.SendChat("?health 100");
        await EvidenceRetestWire.ViewSinceAsync(actor, cursor, 2, "02 80 80 80 40 64 00");
    }

    private static async Task<int> WaitRegenStepAsync(HeadlessPlayer actor, int amount)
    {
        int cursor = actor.World.ViewSnapshot().Count;
        await actor.WaitUntilAsync(() =>
        {
            var samples = PvpOpponentsFixture.HealthHistory(actor.World.ViewSnapshot(), cursor, 2, self: true);
            return samples.Zip(samples.Skip(1), (a, b) => b - a).Count(delta => delta == amount) >= 2;
        }, $"{actor.Handle} has two natural +{amount} HP ticks", TimeSpan.FromSeconds(12));
        return amount;
    }

    private static async Task VerifyPassiveAsync(HeadlessPlayer caster, ushort parent, ushort grant, ushort stat, int value)
    {
        string added = "80 b2 " + EvidenceRetestWire.Le(grant) + " 00 00 08 02";
        Check.That(caster.Received(added).Count > 0, $"Loaded parent {parent} publishes child {grant}");
        await ModifierAsync(caster, 0, parent, stat, value, 0);
        await ModifierAsync(caster, 0, parent, grant, 0, 0);
        ushort initialMax = BitConverter.ToUInt16(caster.World.CreationSnapshot().Last(view => view.ViewId == 2)
            .Attributes["MaxHealth"]);
        int cursor = caster.World.RpcSnapshot().Count;
        int viewCursor = caster.World.ViewSnapshot().Count;
        caster.SendChat($"?qaunload {parent}");
        await caster.WaitForRpcAsync("80 b3 " + EvidenceRetestWire.Le(grant));
        await ModifierAsync(caster, cursor, parent, stat, value, 0, remove: true);
        await ModifierAsync(caster, cursor, parent, grant, 0, 0, remove: true);
        if (parent == Guardian)
            await MaximumHealthAsync(caster, viewCursor, (ushort)(initialMax - 50));
        cursor = caster.World.RpcSnapshot().Count;
        viewCursor = caster.World.ViewSnapshot().Count;
        caster.SendChat($"?qaload {parent}");
        await caster.WaitForRpcAsync(added);
        await ModifierAsync(caster, cursor, parent, stat, value, 0);
        await ModifierAsync(caster, cursor, parent, grant, 0, 0);
        if (parent == Guardian)
            await MaximumHealthAsync(caster, viewCursor, initialMax);
    }

    private static Task MaximumHealthAsync(HeadlessPlayer caster, int cursor, ushort value)
    {
        string amount = EvidenceRetestWire.Le(value);
        var maximum = BytePattern.Parse($"02 00 02 80 80 01 {amount}");
        var maximumAndCurrent = BytePattern.Parse($"02 00 02 80 80 81 {amount} 40 {amount}");
        return caster.WaitUntilAsync(() => caster.World.ViewSnapshot().Skip(cursor).Any(packet =>
            maximum.Occurs(packet) || maximumAndCurrent.Occurs(packet)),
            $"maximum health updated to {value}", HeadlessPlayer.DefaultTimeout);
    }

    private static bool HasModifier(HeadlessPlayer actor, int start, ushort source, ushort stat, int value, float seconds,
        bool remove) => actor.World.RpcSnapshot().Skip(start).Any(rpc => ManageBonusRecord.TryDecode(rpc.Bytes, out var m) &&
            m!.SourceId == source && m.TargetId == stat && m.SignedValue == value && m.Duration == seconds &&
            m.AddRemoveType == (remove ? 1 : 0));

    private static Task ModifierAsync(HeadlessPlayer actor, int start, ushort source, ushort stat, int value, float seconds,
        bool remove = false) => actor.WaitUntilAsync(() => HasModifier(actor, start, source, stat, value, seconds, remove),
            $"{actor.Handle} modifier {source}/{stat} {value}, duration {seconds}, remove={remove}", HeadlessPlayer.DefaultTimeout);
}
