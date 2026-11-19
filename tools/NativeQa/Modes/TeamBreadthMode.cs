using System.Text;
using System.Text.Json;
using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class TeamBreadthMode : NativeQaMode
{
    private const string Partner = "QAFriend", Owner = "CSR_Tester";
    private const string Guard = "Team Guard", Receiver = "Team Receiver";
    private const int HelperHealth = 500, Dossier = 6799, Mission = 399912;

    public override string Name => "team-breadth";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2 }, TeamMissionFixture.HolderX, TeamMissionFixture.Z,
        new[] { $"DELETE FROM inventory WHERE charId=110 AND goid={Dossier}" },
        new[] { "UPDATE char_abilities SET level=1 WHERE char_id=110" },
        new[] { $"""
            UPDATE characters SET y={TeamMissionFixture.Y},alignment=3,pvpflag=0 WHERE charId=110;
            UPDATE characters SET x={TeamMissionFixture.HolderX},y={TeamMissionFixture.Y},z={TeamMissionFixture.Z},
                alignment=3,pvpflag=0 WHERE handle='QAOther';
            UPDATE characters SET level=3,exp={PlayerProgression.ExperienceForLevel(3)},
                x={TeamMissionFixture.HelperX},y={TeamMissionFixture.Y},z={TeamMissionFixture.Z},
                healthC={HelperHealth},healthM={HelperHealth},alignment=3,pvpflag=0
                WHERE handle IN ('{Partner}','QAEnemy');
            """ },
        missionFixture: "team-breadth.xml",
        environmentOverrides: new() { ["MXO_MOBS"] = "test", ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[] { "Invite QAFriend to the mission team, then submit team-breadth accept. Open the team HUD.",
            "Call Flood and accept Team Breadth Fixture. Submit team-breadth assist. Watch QAFriend's health during the fight.",
            "Take the Dossier from the shared kill's Loot window and Give it to Team Receiver." });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer ?? throw new InvalidOperationException("Team QA requires an isolated server.");
        var db = context.OwnedDatabase ?? throw new InvalidOperationException("Team QA requires a disposable database.");
        var partner = await HeadlessPlayer.LoginAsync(context.Address, Partner, Partner);
        context.Helpers.Add(Partner, partner);
        await partner.EnterWorldAsync();

        using var holder = await HeadlessPlayer.LoginAsync(context.Address, "QAOther", "QAOther");
        using var fighter = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy");
        await holder.EnterWorldAsync();
        await fighter.EnterWorldAsync();
        await partner.WaitForViewNamedAsync(holder.Handle);
        var fighterView = await holder.WaitForViewNamedAsync(fighter.Handle);
        Check.Equal((ushort)HelperHealth, BitConverter.ToUInt16(fighterView.Attributes["Health"]),
            "The holder receives the helper's initial health");
        await InviteAsync(holder, fighter);

        holder.SendRpc("80 94 05 00 03");
        await holder.WaitForTextAsync("Team Breadth Fixture");
        await holder.WaitForRpcAsync("80 97 05 00");
        holder.SendRpc("80 98 06 00 00 00 01 02 00");
        await holder.WaitForRpcAsync("80 99");
        holder.SendRpc("80 9b 07 00 01");
        await holder.WaitForRpcAsync("80 9c");
        var receiver = await holder.WaitForViewNamedAsync(Receiver);
        int beforeFight = holder.World.ViewSnapshot().Count;
        await AssistAsync(fighter);
        await holder.WaitUntilAsync(() => PvpOpponentsFixture.HealthHistory(holder.World.ViewSnapshot(), beforeFight,
            fighterView.ViewId, self: false).Any(hp => hp > 0 && hp < HelperHealth),
            "the holder receives the fighting helper's lower health", TimeSpan.FromSeconds(45));
        await holder.WaitForRpcAsync("80 a0 01 06 00 01", TimeSpan.FromSeconds(75));
        var loot = await holder.WaitForRpcAsync("81 19");
        Check.That(BytePattern.IndexOf(loot.Bytes, WireFormat.FromHex("00 00 00 08 8f 1a 00 00"), 0) >= 0,
            "The holder's loot window contains the Dossier from the helper's kill");
        holder.SendRpc("81 1f");
        await holder.WaitForRpcAsync("80 a0 02 06 00 01");
        Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={holder.CharId} AND goid={Dossier}"),
            "The holder claimed the shared kill's one mission item");
        holder.SendRpc($"80 c7 {View(receiver)} 0e 00");
        await holder.WaitForRpcAsync("80 a2 00");
        Check.Equal(0L, await db.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={holder.CharId} AND goid={Dossier}"),
            "Delivery consumes the mission item");
        Check.Equal(1L, await db.LongAsync($"SELECT completions FROM char_missions WHERE charId={holder.CharId} AND missionId={Mission}"),
            "The holder receives one mission receipt");
        Check.Equal(0L, await db.LongAsync($"SELECT COUNT(*) FROM char_missions WHERE charId={fighter.CharId} AND missionId={Mission}"),
            "The helper did not get a second receipt or take the item");
        Check.Equal(0, holder.World.Problems.Count + fighter.World.Problems.Count, "Team probe packets decode cleanly");
        NativeQaReadiness.RequireNoAmbientAggro(server, partner);
        return Array.Empty<byte>();
    }

    public static async Task<object> SendActionAsync(HeadlessPlayer actor, JsonElement request)
    {
        if (actor.Handle != Partner) throw new ArgumentException("Select QAFriend for the team fixture.");
        switch (request.GetProperty("step").GetString())
        {
            case "accept":
                await AcceptAsync(actor, Owner);
                return "QAFriend joined the mission team.";
            case "assist":
                await AssistAsync(actor);
                return "QAFriend is fighting Team Guard.";
            default:
                throw new ArgumentException("Use accept or assist for the team fixture.");
        }
    }

    internal static async Task InviteAsync(HeadlessPlayer inviter, HeadlessPlayer member)
    {
        var who = Sized(member.Handle);
        inviter.SendRpc(new byte[] { 0x6e, 3, 6, 0 }.Concat(BitConverter.GetBytes((ushort)(6 + who.Length)))
            .Concat(who).Concat(Sized("")).ToArray());
        await AcceptAsync(member, inviter.Handle);
        await inviter.WaitForRpcAsync($"70 03 {WireFormat.ToHex(BitConverter.GetBytes(member.CharId))} 09 00");
    }

    private static async Task AcceptAsync(HeadlessPlayer member, string inviter)
    {
        var prefix = BytePattern.Parse("80 8f 0c 00");
        byte[] name = Encoding.ASCII.GetBytes(inviter + "\0");
        await member.WaitForRpcAsync(r => prefix.IsMatch(r.Bytes) && BytePattern.IndexOf(r.Bytes, name, 0) >= 0,
            $"pending team invitation from {inviter}");
        member.SendRpc(new byte[] { 0x6f, 3, 8, 0, 0, 0, 0, 0 }.Concat(Sized(inviter)).ToArray());
        await member.WaitForRpcAsync("80 8e");
    }

    private static async Task AssistAsync(HeadlessPlayer fighter)
    {
        var target = await fighter.WaitForViewNamedAsync(Guard);
        fighter.SendRpc("3d 01");
        fighter.SendRpc($"40 {View(target)} 00 00 00 00 00 00 00");
    }

    private static string View(ViewCreation view) =>
        $"{WireFormat.ToHex(BitConverter.GetBytes(view.ViewId))} {view.SpawnId:x2} 00";
    private static byte[] Sized(string value) => BitConverter.GetBytes((ushort)(value.Length + 1))
        .Concat(Encoding.ASCII.GetBytes(value)).Append((byte)0).ToArray();
}
