namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task ClientActionsResearchCanary(ScenarioContext context)
    {
        var players=await context.AddPlayersInWorldAsync(new CharacterSeed("C1Research"){X=23828,Z=13984,Info=12345,Items=new[]{(3,2399u,2)}},new CharacterSeed("C1Observer"){X=23928,Z=13984});
        var p=players[0];var observer=players[1];
        long cash=await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={p.CharId}");
        long items=await context.Database.LongAsync($"SELECT SUM(GREATEST(count,1)) FROM inventory WHERE charId={p.CharId}");
        int worldChanges=p.Received("81 07").Count+p.Received("80 fa").Count;
        // These are standalone native-shaped requests. No unknown response or backend semantics are fabricated.
        string[] records={"3b","81 55","81 56","81 37 78 56 34 12","81 46 01 00 00 00 02 00 00 00",
            "81 aa 78 56 34 12 01","80 89 00 00 00 00 00 00 00","80 f2 00 00 00 00",
            "80 f6 00 00 00 00 00 00 00 00","80 ef 00 00"};
        foreach(string record in records)
        {
            p.SendRpc(record);
            p.SendRpc("81 4d 78 56 34 12");await p.WaitForRpcAsync("81 4d 78 56 34 12");
        }
        p.SendChat("C1 diagnostic roundtrip");await observer.WaitForTextAsync("C1 diagnostic roundtrip");
        Check.Equal(cash,await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={p.CharId}"),"Ignored requests do not mutate cash");
        Check.Equal(items,await context.Database.LongAsync($"SELECT SUM(GREATEST(count,1)) FROM inventory WHERE charId={p.CharId}"),"Ignored requests do not mutate owned inventory");
        Check.Equal(23828L,await context.Database.LongAsync($"SELECT CAST(x AS SIGNED) FROM characters WHERE charId={p.CharId}"),"Stored X unchanged");
        Check.Equal(13984L,await context.Database.LongAsync($"SELECT CAST(z AS SIGNED) FROM characters WHERE charId={p.CharId}"),"Stored Z unchanged");
        Check.Equal(worldChanges,p.Received("81 07").Count+p.Received("80 fa").Count,"No invented world transition");
        Check.Equal(0,p.World.Problems.Count,"No client parser problems");
    }
}
