using System.Text;

namespace HeadlessClient;

public static partial class Scenarios
{
    public const string CrewFoundingName = "QARetryCrew";
    public const string CrewFoundingFault = "qa_crew_founder_member_update";
    private const string CrewFoundingRefusal = "The crew could not be created. Both memberships were kept.";

    public static string CrewFoundingFaultSql => $"""
        SET SESSION sql_mode='';
        ALTER TABLE characters ADD CONSTRAINT {CrewFoundingFault}
            CHECK (handle<>'QAFriend' OR crewId=0);
        """;

    private static async Task AtomicCrewFounding(ScenarioContext context)
    {
        HeadlessPlayer[] players = await context.AddPlayersInWorldAsync(new CharacterSeed("QACreator") { X = 17000, Y = 495, Z = 6400 },
            new CharacterSeed("QAFriend") { X = 17120, Y = 495, Z = 6400 }, new CharacterSeed("QAOther") { X = 17220, Y = 495, Z = 6400 });
        await context.Database.ExecuteAsync(CrewFoundingFaultSql);
        await ProveAtomicCrewFoundingAsync(players[0], players[1], players[2], context.Database);
    }

    public static async Task AcceptCrewFoundingAsync(HeadlessPlayer invitee, string founder)
    {
        var invite = BytePattern.Parse("80 88 07 00");
        byte[] from = Encoding.ASCII.GetBytes(founder + "\0");
        byte[] name = Encoding.ASCII.GetBytes(CrewFoundingName + "\0");
        await invitee.WaitForRpcAsync(record => invite.IsMatch(record.Bytes) &&
            BytePattern.IndexOf(record.Bytes, from, 0) >= 0 && BytePattern.IndexOf(record.Bytes, name, 0) >= 0,
            $"the {CrewFoundingName} crew invitation from {founder}");
        invitee.SendRpc(InviteAnswer(Crew, 0, founder));
    }

    public static async Task ProveAtomicCrewFoundingAsync(HeadlessPlayer founder, HeadlessPlayer refused,
        HeadlessPlayer accepted, ThrowawayDatabase db)
    {
        founder.SkipReceived();
        refused.SkipReceived();
        accepted.SkipReceived();
        founder.SendRpc(CrewCreate(CrewFoundingName, refused.Handle, 1));
        await AcceptCrewFoundingAsync(refused, founder.Handle);
        var failure = await founder.WaitForRpcAsync("80 85 00 00 00 00 0d 00 01 b4 00 00 02");
        Check.That(failure.Bytes.Skip(13).SequenceEqual(Sized(CrewFoundingName)), "The stock result includes the original crew name");
        await founder.WaitForTextAsync(CrewFoundingRefusal);
        await refused.WaitForTextAsync(CrewFoundingRefusal);
        Check.Equal(0L, await db.LongAsync($"SELECT COUNT(*) FROM crews WHERE crew_name='{CrewFoundingName}'"),
            "A failed affiliation write rolls back the inserted crew and releases its name");
        Check.Equal(0L, await db.LongAsync($"SELECT COUNT(*) FROM crew_members WHERE char_id IN ({founder.CharId},{refused.CharId})"),
            "Neither rolled-back roster row remains");
        Check.Equal(2L, await db.LongAsync($"SELECT COUNT(*) FROM characters WHERE charId IN ({founder.CharId},{refused.CharId}) AND crewId=0 AND factionId=0"),
            "Both players stay unaffiliated");
        Check.Equal(0, founder.Received("80 85 ?? ?? ?? ?? 0d 00 01 00 00 00 00").Count,
            "No success is published on rollback");

        founder.SendRpc(CrewCreate(CrewFoundingName, accepted.Handle, 1));
        await AcceptCrewFoundingAsync(accepted, founder.Handle);
        var success = await founder.WaitForRpcAsync("80 85 ?? ?? ?? ?? 0d 00 01 00 00 00 00");
        uint crew = BitConverter.ToUInt32(success.Bytes, 2);
        Check.That(crew != 0, "The same name yields a new crew id");
        await founder.WaitForRpcAsync($"80 86 {Le(founder.CharId)} {Le(crew)}");
        await accepted.WaitForRpcAsync($"80 86 {Le(accepted.CharId)} {Le(crew)}");
        Check.Equal(1L, await db.LongAsync($"SELECT COUNT(*) FROM crews WHERE id={crew} AND crew_name='{CrewFoundingName}' " +
            $"AND master_player_handle='{founder.Handle}' AND org=1 AND faction_id=0 AND deleted_at IS NULL"),
            "The selected organisation and captain persist");
        Check.Equal(2L, await db.LongAsync($"""
            SELECT COUNT(*) FROM crew_members m JOIN characters p ON p.charId=m.char_id AND p.crewId=m.crew_id
            WHERE m.crew_id={crew} AND p.factionId=0
              AND ((p.charId={founder.CharId} AND m.is_captain=1) OR (p.charId={accepted.CharId} AND m.is_captain=0));
            """), "Both roster roles and character affiliations commit together");
        Check.Equal(0L, await db.LongAsync($"SELECT crewId FROM characters WHERE charId={refused.CharId}"),
            "The first invitee remains unaffiliated after the successful retry");
    }
}
