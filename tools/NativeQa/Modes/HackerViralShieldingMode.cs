using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using System.Text.RegularExpressions;

namespace NativeQa.Modes;

public sealed class HackerViralShieldingMode : NativeQaMode
{
    private const ushort Hacker = 1006, Firewall = 63, Shield = 436;
    private const int Level = 50, X = 17000, Y = 495, Z = 6400;
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Hacker, Firewall, Shield).Order().ToArray();
    private static readonly (string Handle, int X, int Z)[] Helpers =
    {
        ("QAFriend", X, Z + 900), ("QAFar", X, Z + 1140), ("QAOther", X, Z + 1241), ("QAEnemy", X + 200, Z)
    };
    private static readonly (ushort Stat, int Value)[] FirewallStats = { (183, 35), (1009, 5), (1010, 5), (1011, 5) };
    private static readonly (ushort Stat, int Value)[] ShieldStats = { (183, 47), (1009, 11), (1010, 11), (1011, 11) };

    public override string Name => "ability-parity-hacker-viral-shielding";

    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y},rotation=0,innerStrC=400,innerStrM=2000,alignment=1,pvpflag=0 WHERE charId=110",
        "DELETE FROM char_abilities WHERE char_id BETWEEN 6111 AND 6114"
    }.Concat(Helpers.Select(stand =>
        $"UPDATE characters SET x={stand.X},y={Y},z={stand.Z},innerStrC=400,innerStrM=2000,alignment={(stand.Handle == "QAEnemy" ? 2 : 1)},pvpflag=0 WHERE handle='{stand.Handle}'"))
        .ToArray(), DevCommands: true, hotbarAbilities: new[] { Hacker, Shield, Firewall },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1" },
        extraQaMetadata: new() { ["ScopedStockRows"] = "436", ["NativeCloseableRows"] = 1, ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost) },
        readyMessages: new[]
        {
            "Accept QAFriend's team invitation. CSR starts at (17000,495,6400) facing the nearby teammate stands. QAFriend (17000,495,7300), QAFar (17000,495,7540) and QAOther (17000,495,7641) are team members. QAEnemy (17200,495,6400) remains outside the team. Stay on the starting stand.",
            """If the invite dialog never appears after CSR is in world, resend it with helper action {"actor":"QAFriend","action":"invite","target":"CSR_Tester"}.""",
            "Select page 2 slot 1 Hacker Style, then type /target QAEnemy. Use slot 2 Viral Shielding. Keep the first full outgoing 436 request. Expect an 8 s cast, [ABILITY-COST] 436 paid=100 IS=400->300 and 15 s own reuse. The stock bootstrap checks slot 3 Network Firewall as a separate same-family control.",
            "Expect 300 s [MODIFIER] adds from 436 on CSR (110), QAFriend (6111), and QAFar (6112) at 11.40 m: 183 +47 plus 1009/1010/1011 +11 each. QAOther at 12.41 m and selected nonmember QAEnemy receive none. Loaded rank-50 Resist Damage adds +24% to the 10 m base radius. The term record 710 does not appear as an active bonus.",
            "Use ?qaunload 436 after landing. All three buffs stay active. Use ?dismissbuff 436. Expect the four CSR removals and [VITALS] char=110 RefundIS source=436 IS=300->325. QAFriend automatically dismisses its copy and receives its own +25. Repeat ?dismissbuff 436 to confirm no second CSR refund. QAFar keeps its independent copy until term. Use ?qaload 436 to restore the Archive entry."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer ?? throw new InvalidOperationException("Missing Viral Shielding fixture server");
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Missing Viral Shielding NAV source");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (handle, x, z) in Helpers.Append(("CSR_Tester", X, Z)))
            Check.That(NavMesh.Locate(1, x, Y, z, out var at) && !at.indoors && Math.Abs(at.floorY + 95 - Y) < 1,
                $"{handle} ({x},{Y},{z}) is on outdoor NAV");

        foreach (var (handle, x, z) in Helpers)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, handle, handle, stockCharacterLoad: true);
            context.Helpers.Add(handle, helper);
            await helper.EnterWorldAsync();
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
            var position = helper.World.CreationSnapshot().Last(view => view.ViewId == 2).Attributes["Position"];
            Check.Equal((double)x, BitConverter.ToDouble(position), $"{handle} stands at X");
            Check.Equal((double)Y, BitConverter.ToDouble(position, 8), $"{handle} stands at Y");
            Check.Equal((double)z, BitConverter.ToDouble(position, 16), $"{handle} stands at Z");
        }

        var leader = context.Helpers["QAFriend"];
        var edge = context.Helpers["QAFar"];
        var outside = context.Helpers["QAOther"];
        var other = context.Helpers["QAEnemy"];
        await TeamBreadthMode.InviteAsync(leader, edge);
        await TeamBreadthMode.InviteAsync(leader, outside);
        if (context.StockProbe is not { } actor)
        {
            _ = ObserveNativeAsync(server, leader);
            return Array.Empty<byte>();
        }

        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(Loadout.Sum(AbilityMemory.Cost) == 63 && 63 <= AbilityMemory.ForLevel(Level) &&
            archive.Keys.ToHashSet().SetEquals(Loadout) && Loadout.All(id => AbilityPrerequisites.Current.Closure(id)
                .All(need => archive.TryGetValue(need.Key, out var row) && row.Level >= need.Value)),
            "The full ordinary Viral Shielding profile fits 63/100 memory");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        await TeamBreadthMode.InviteAsync(leader, actor);
        await actor.WaitForViewNamedAsync(other.Handle);
        actor.SendRpc(StarterHackerViralSightMode.StockStyle);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");

        // The complete stock 436 request is absent from the tracked intake. The retained Firewall
        // request from the stock Team Patcher sessions checks the shared group publication.
        var participants = new[] { actor, leader, edge, outside, other };
        var included = new[] { actor, leader, edge };
        var cursors = participants.ToDictionary(helper => helper, helper => helper.World.RpcSnapshot().Count);
        int start = server.DebugLog().Length;
        actor.SendRpc(StarterTeamPatcherBuffsMode.StockFirewall);
        await EvidenceRetestWire.CastBarAsync(actor, Firewall, 6);
        foreach (var helper in included)
            foreach (var (stat, value) in FirewallStats)
                await StockAbilityProfile.ObserveAsync(helper, cursors[helper],
                    packet => StockAbilityProfile.Bonus(packet, Firewall, stat, value, 300, remove: false), $"{helper.Handle} receives {stat}");
        foreach (var excluded in participants.Except(included))
        {
            int barrier = excluded.World.RpcSnapshot().Count;
            excluded.SendRpc("81 48");
            await StockAbilityProfile.ObserveAsync(excluded, barrier, packet => packet.Bytes is [0x81, 0x49, ..], "Recipient barrier");
            Check.That(!excluded.World.RpcSnapshot().Skip(cursors[excluded]).Any(packet =>
                ManageBonusRecord.TryDecode(packet.Bytes, out var record) && record!.SourceId == Firewall),
                $"{excluded.Handle} is outside the Firewall recipient set");
        }
        foreach (var helper in included) helper.SendChat("?dismissbuff 63");
        await actor.WaitUntilAsync(() =>
        {
            string log = server.DebugLog()[start..];
            var helperRefund = Regex.Match(log, @"char=6111 RefundIS source=63 IS=(\d+)->(\d+)");
            return log.Contains("char=110 RefundIS source=63 IS=350->360") && helperRefund.Success &&
                int.Parse(helperRefund.Groups[2].Value) - int.Parse(helperRefund.Groups[1].Value) == 10;
        }, "Distinct Firewall refund recipients", HeadlessPlayer.DefaultTimeout);
        Check.That(participants.All(player => player.World.Problems.Count == 0), "Firewall transport remained clean");
        return Array.Empty<byte>();
    }

    private static async Task ObserveNativeAsync(ServerInstance server, HeadlessPlayer leader)
    {
        int cursor = leader.World.RpcSnapshot().Count;
        bool invited = false;
        while (!server.HasExited)
        {
            if (!invited && leader.World.CreationSnapshot().Any(view => view.Name == "CSR_Tester"))
            {
                TeamBreadthMode.SendInvite(leader, "CSR_Tester");
                invited = true;
            }
            var records = leader.World.RpcSnapshot();
            if (ShieldStats.All(stat => records.Skip(cursor).Any(packet =>
                StockAbilityProfile.Bonus(packet, Shield, stat.Stat, stat.Value, 300, remove: false))) &&
                server.DebugLog().Contains("char=110 RefundIS source=436 IS=300->325", StringComparison.Ordinal))
            {
                leader.SendChat("?dismissbuff 436");
                Console.WriteLine("VIRAL SHIELDING: QAFriend received four modifiers and requested its own dismissal.");
                return;
            }
            await Task.Delay(100);
        }
    }
}
