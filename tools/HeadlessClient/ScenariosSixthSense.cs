namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task SixthSenseScan(ScenarioContext context)
    {
        const ushort sixthSense = 149;
        var a = await context.AddPlayerAsync(SixthSenseFixture.At("Scanner") with
            { Organization = 1, OwnedLoadedAbilities = new (ushort, ushort)[] { (sixthSense, 50) } });
        var neutral = await context.AddPlayerAsync(SixthSenseFixture.At("Neutral", 17000) with { Z = 1600 });
        var enemy = await context.AddPlayerAsync(SixthSenseFixture.At("Opponent", SixthSenseFixture.NearX) with { Organization = 2 });
        var friend = await context.AddPlayerAsync(SixthSenseFixture.At("Teammate", 17300));
        var far = await context.AddPlayerAsync(SixthSenseFixture.At("Outside", SixthSenseFixture.FarX) with { Organization = 2 });
        foreach (var player in new[] { a, neutral, enemy, friend, far }) await player.EnterWorldAsync();
        await a.WaitForRpcAsync(PassiveRecord(sixthSense, 646, 200));
        var selected = await a.WaitForViewNamedAsync(far.Handle);
        a.SendRpc(GroupInvite(Team, friend.Handle, ""));
        await friend.WaitForRpcAsync("80 8f 0c 00");
        friend.SendRpc(InviteAnswer(Team, 0, a.Handle));
        await a.WaitForRpcAsync($"70 03 {Le(friend.CharId)} 09 00");

        ushort before = RefusalOwnValue(a, "InnerStrengthAvailable", "02 80 80 80 10");
        Cast(a, sixthSense, selected.ViewId);
        var map = (await a.WaitForRpcAsync("80 c5")).Bytes;
        var entries = ReadMinimapPositions(map, 10);
        Check.Equal(2, entries.Length, "Only two characters receive map entries");
        Check.That(entries.Contains(((sbyte)0, (sbyte)-7, (byte)0)) && entries.Contains(((sbyte)8, (sbyte)0, (byte)2)),
            "The scan stays centred on the caster and encodes a yellow neutral and red hostile");
        await a.WaitForViewUpdateAsync(2, $"02 80 80 80 10 {Le((ushort)(before - 10))}");
        foreach (var target in new[] { neutral, enemy })
        {
            await target.WaitForRpcAsync(SixthSenseFixture.Scan);
            await target.WaitForRpcAsync(SixthSenseFixture.Discovery);
            Check.Equal(0, target.Received("80 c5").Count, "Only the caster receives positions");
        }
        foreach (var target in new[] { friend, far })
        {
            target.SendRpc("81 48");
            await target.WaitForRpcAsync("81 49");
            Check.Equal(0, target.Received(SixthSenseFixture.Scan).Count, "Group allies and targets outside 15 m receive no scan");
        }

        Cast(neutral, sixthSense);
        await neutral.WaitForTextAsync("Load Sixth Sense");
        Check.Equal(0, neutral.Received("80 c5").Count, "An unowned ability cannot scan");
        foreach (var player in new[] { a, neutral, enemy, friend, far })
            Check.Equal(0, player.World.Problems.Count, $"{player.Handle} scan protocol");
    }
}
