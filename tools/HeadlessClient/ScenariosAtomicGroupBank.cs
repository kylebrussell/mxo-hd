namespace HeadlessClient;

public static partial class Scenarios
{
    public const int QaCrewBankId = 9031;
    public const int QaFactionBankId = 9032;

    public static string GroupBankSeedSql(uint owner, uint member = 0) => $"""
        INSERT INTO factions(id,name,master_player_handle,money)
            SELECT {QaFactionBankId},'QA Bank Faction',handle,500 FROM characters WHERE charId={owner};
        INSERT INTO crews(id,crew_name,master_player_handle,money,org,faction_id,faction_rank)
            SELECT {QaCrewBankId},'QA Bank Crew',handle,200,1,{QaFactionBankId},1 FROM characters WHERE charId={owner};
        DELETE FROM crew_members WHERE char_id IN ({owner},{member});
        INSERT INTO crew_members(crew_id,char_id,is_captain,is_first_mate,created_at)
            SELECT {QaCrewBankId},charId,charId={owner},0,NOW() FROM characters WHERE charId IN ({owner},{member});
        UPDATE characters SET crewId={QaCrewBankId}, factionId={QaFactionBankId}, cash=IF(charId={owner},1000,500)
            WHERE charId IN ({owner},{member});
        """;

    public static string GroupBankFaultSql => $"""
        SET SESSION sql_mode='';
        ALTER TABLE characters ADD CONSTRAINT qa_group_bank_wallet CHECK (crewId<>{QaCrewBankId} OR cash<>970);
        """;

    private static async Task AtomicGroupBank(ScenarioContext context)
    {
        var owner = await context.AddPlayerAsync(new CharacterSeed("BankOwner")
        {
            X = 17000, Y = 495, Z = 6400, CrewId = QaCrewBankId, FactionId = QaFactionBankId
        });
        var member = await context.AddPlayerAsync(new CharacterSeed("BankMember")
        {
            X = 17060, Y = 495, Z = 6400, CrewId = QaCrewBankId, FactionId = QaFactionBankId, Info = 500
        });
        var outsider = await context.AddPlayerAsync(new CharacterSeed("BankOutsider") { X = 17120, Y = 495, Z = 6400 });
        var db = context.Database;
        await db.ExecuteAsync(GroupBankSeedSql(owner.CharId, member.CharId));
        await db.ExecuteAsync(GroupBankFaultSql);
        await owner.EnterWorldAsync();
        await member.EnterWorldAsync();
        await outsider.EnterWorldAsync();
        await ProveAtomicGroupBankAsync(owner, db, member);

        owner.SkipReceived();
        member.SkipReceived();
        owner.SendRpc("78 02 c8 00 00 00 00");
        member.SendRpc("78 02 c8 00 00 00 00");
        await owner.WaitForRpcAsync("79 02 c8 00 00 00 ?? ?? ?? ?? 00");
        await member.WaitForRpcAsync("79 02 c8 00 00 00 ?? ?? ?? ?? 00");
        await Task.WhenAll(Fence(owner), Fence(member), Fence(outsider));
        Check.Equal(50L, await db.LongAsync($"SELECT money FROM crews WHERE id={QaCrewBankId}"),
            "Only one competing 200 withdrawal fits the 250 balance");
        Check.Equal(1625L, await db.LongAsync($"SELECT SUM(cash) FROM characters WHERE charId IN ({owner.CharId},{member.CharId})"),
            "The two players receive exactly one withdrawal");
        Check.Equal(1, owner.Received("79 02 c8 00 00 00").Count, "Only the committed withdrawal reaches the crew");
        Check.Equal(0, outsider.Received("79 ??").Count, "A player outside these groups receives no bank delta");

        await db.ExecuteAsync("ALTER TABLE characters DROP CHECK qa_group_bank_wallet");
        long ownerCash = await db.LongAsync($"SELECT cash FROM characters WHERE charId={owner.CharId}");
        owner.SendRpc("78 02 1e 00 00 00 01");
        await owner.WaitForRpcAsync($"79 02 1e 00 00 00 {Le(owner.CharId)} 01");
        await owner.WaitForRpcAsync($"80 e4 {Le((uint)(ownerCash - 30))}");
        Check.Equal(80L, await db.LongAsync($"SELECT money FROM crews WHERE id={QaCrewBankId}"), "The exact deposit retry now commits");
        Check.Equal(ownerCash - 30, await db.LongAsync($"SELECT cash FROM characters WHERE charId={owner.CharId}"), "The retry is charged once");
        Check.Equal(0, outsider.World.Problems.Count, "The outsider decodes the session");

        static async Task Fence(HeadlessPlayer player)
        {
            player.SendRpc("81 48");
            await player.WaitForRpcAsync("81 49");
        }
    }

    public static async Task ProveAtomicGroupBankAsync(HeadlessPlayer actor, ThrowawayDatabase db, HeadlessPlayer? member = null)
    {
        actor.SkipReceived();
        member?.SkipReceived();
        int priorWalletMessages = actor.Received("80 e4").Count;
        actor.SendRpc("78 02 1e 00 00 00 01");
        await actor.WaitForTextAsync("Your information was kept.");
        await Balances(1000, 200, 500);
        Check.Equal(0, actor.Received("79 02").Count + (member?.Received("79 02").Count ?? 0),
            "The rolled-back transfer never changes the visible bank ledger");
        Check.Equal(priorWalletMessages, actor.Received("80 e4").Count, "The rollback sends no wallet mutation");

        await Transfer(2, 100, true, 900);
        await Balances(900, 300, 500);
        await Transfer(2, 50, false, 950);
        await Balances(950, 250, 500);
        await Transfer(1, 25, true, 925);
        await Balances(925, 250, 525);
        Check.Equal(0, actor.World.Problems.Count + (member?.World.Problems.Count ?? 0), "Clients decode the bank replies");

        async Task Transfer(byte type, uint amount, bool deposit, uint cash)
        {
            string operation = $"{type:x2} {Le(amount)}";
            string direction = deposit ? "01" : "00";
            string delta = $"79 {operation} {Le(actor.CharId)} {direction}";
            actor.SendRpc($"78 {operation} {direction}");
            await actor.WaitForRpcAsync(delta);
            await actor.WaitForRpcAsync($"80 e4 {Le(cash)}");
            if (member != null) await member.WaitForRpcAsync(delta);
        }

        async Task Balances(long cash, long crew, long faction)
        {
            Check.Equal(cash, await db.LongAsync($"SELECT cash FROM characters WHERE charId={actor.CharId}"), "Persisted wallet");
            Check.Equal(crew, await db.LongAsync($"SELECT money FROM crews WHERE id={QaCrewBankId}"), "Persisted crew bank");
            Check.Equal(faction, await db.LongAsync($"SELECT money FROM factions WHERE id={QaFactionBankId}"), "Persisted faction bank");
        }
    }
}
