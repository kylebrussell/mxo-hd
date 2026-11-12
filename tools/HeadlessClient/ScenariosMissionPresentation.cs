namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task MissionPresentation(ScenarioContext context)
    {
        var donor=await context.AddPlayerAsync(new CharacterSeed("MissionDonor"){Level=50,X=59000,Y=1095,Z=12200,Organization=1});
        donor.Dispose();
        // Only this scenario's throwaway DB: clone its newly created actor into the strict native CSR110 fixture slot.
        await context.Database.ExecuteAsync($@"DELETE FROM characters WHERE charId=110;DELETE FROM rsivalues WHERE charid=110;
CREATE TEMPORARY TABLE qa_body_actor AS SELECT * FROM characters WHERE charId={donor.CharId};
UPDATE qa_body_actor SET charId=110,handle='CSR_Tester';INSERT INTO characters SELECT * FROM qa_body_actor;DROP TEMPORARY TABLE qa_body_actor;
CREATE TEMPORARY TABLE qa_body_rsi AS SELECT * FROM rsivalues WHERE charid={donor.CharId};
UPDATE qa_body_rsi SET charid=110;INSERT INTO rsivalues SELECT * FROM qa_body_rsi;DROP TEMPORARY TABLE qa_body_rsi;");
        using var p=await HeadlessPlayer.LoginAsync(context.Server.Address,"user"+donor.CharId,"CSR_Tester");
        await context.Database.ExecuteAsync("CREATE TABLE IF NOT EXISTS char_missions (charId INT UNSIGNED NOT NULL,missionId INT NOT NULL,completions INT NOT NULL DEFAULT 1,PRIMARY KEY(charId,missionId))");
        await context.Database.ExecuteAsync(MissionPresentationFixture.PriorMissionsSql(p.CharId));
        await p.EnterWorldAsync();p.SendRpc("80 94 05 00 01");await p.WaitForTextAsync("The Internuncio");
        await p.WaitForRpcAsync("80 97 05 00");p.SendRpc("80 98 06 00 00 00 01 02 00");await p.WaitForRpcAsync("80 99");
        p.SendRpc("80 9b 07 00 01");await p.WaitForRpcAsync("80 9e 00");
        var body=await p.WaitForViewNamedAsync("Boyce");
        Check.Equal((byte)1,body.Attributes["IsDead"][0],"Scripted body arrives dead");
        Check.Equal((ushort)0,BitConverter.ToUInt16(body.Attributes["Health"]),"Scripted body has no health");
        await p.WaitForViewUpdateAsync(body.ViewId,"07 01 1d 0d 00");
        p.SkipReceived();p.SendChat("?missionpresentation speech");
        var speech=await p.WaitForTextAsync("[QA] Hostile mission presentation diagnostic.");
        Check.Equal(1,p.Received("80 a1 1e 12 00 28").Count,"Original hostile portrait dialog retained");
        await p.WaitForRpcAsync("2e 07");
        await Task.Delay(400);
        Check.Equal(2,p.World.RpcSnapshot().Count(r=>Contains(r.Bytes,"[QA] Hostile mission presentation diagnostic.")),"One original dialog and one system-chat fallback");
        var late=await context.AddPlayerAsync(new CharacterSeed("LateBodies"){Level=50,X=59000,Y=1095,Z=12200});await late.EnterWorldAsync();
        var lateBody=await late.WaitForViewNamedAsync("Boyce");
        Check.Equal((byte)1,lateBody.Attributes["IsDead"][0],"Late viewer sees dead creation");
        await late.WaitForViewUpdateAsync(lateBody.ViewId,"07 01 1d 0d 00");
        Check.Equal(0,p.World.Problems.Count,"Mission corpse protocol");Check.Equal(0,late.World.Problems.Count,"Late corpse protocol");
    }
}
