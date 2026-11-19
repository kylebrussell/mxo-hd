namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task CombatGunSpecialQueue(ScenarioContext context, ushort ability)
    {
        // Full CSR_Tester requests from the PR321 gunman PacketLog (c2d614206732).
        // Lines 3177, 2694, and 10217, respectively.
        (ushort Ability, ushort Style, uint Weapon, uint Move, float CastSeconds, string Request)[] specials =
        {
            (243, 129, 2565, 9518, 0, "80 b9 f3 00 05 00 03 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 f3 00 00 00 00 6b 00 00 08 6b 00 00 00 00 00"),
            (126, 129, 2565, 9522, 4, "80 b9 7e 00 05 00 03 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ff ff 7f 7f 00 00 00 00 08 6b 00 00 00 00 00"),
            (176, 158, 698, 21083, 4, "80 b9 b0 00 05 00 03 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 b0 00 00 00 00 6b 00 00 08 6b 00 00 00 00 00"),
        };
        var special = specials.Single(move => move.Ability == ability);
        var player = await context.AddPlayerAsync(NearNpc($"Special{special.Ability}") with
        {
            OwnedLoadedAbilities = new[] { (special.Ability, (ushort)50), (special.Style, (ushort)50) },
            Items = new[] { (1, special.Weapon, 1) },
            InnerStrength = 1000, MaxInnerStrength = 1000
        });
        await player.EnterWorldAsync();
        var target = await player.WaitForViewNamedAsync("WinterFrost");
        player.SendRpc("63 01 69");
        await player.WaitForRpcAsync("65 01 00 69 00 00");
        await player.WaitForViewUpdateAsync(2, $"02 80 80 80 80 08 {Le(special.Weapon)}");
        SelectStyle(player, special.Style);
        await player.WaitForViewUpdateAsync(2, $"02 80 80 80 80 80 10 {Le((uint)special.Style)}");
        player.SendRpc($"40 {HandleOf(target)} 01 6b");
        var combat = await WaitForCombatView(player);
        await WaitForEntry(player, combat);

        string landed = $"[ABILITY] {special.Ability} lands on view {target.ViewId} for ";
        int logStart = context.Server.DebugLog().Length;
        uint abilityField = 0x80000000u | special.Ability;
        string queuePattern =
            $"47 00 ?? ?? 01 ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? 1a 00 06 00 00 00 01 00 00 01 ?? 01 ?? ?? ?? {Le(abilityField)}";
        bool won = false;
        for (int attempt = 0; attempt < 3 && !won; attempt++)
        {
            if (attempt != 0) await Task.Delay(TimeSpan.FromSeconds(11)); // Respect the normal reuse timer.
            SendCapturedGunSpecial(player, special.Request, target);
            if (special.CastSeconds > 0)
                await player.WaitForRpcAsync($"80 ac {Le(special.Ability)} 00 00 00 00 00 00 00 00 00 00 {Le(BitConverter.SingleToUInt32Bits(special.CastSeconds))}");
            await player.WaitForRpcAsync(queuePattern, TimeSpan.FromSeconds(8));
            var round = await player.WaitForRpcAsync($"45 ?? ?? ?? ?? ?? 00 01 ?? 01 ?? ?? ?? {Le(abilityField)}");
            won = (RoundResult(round) & 0x100) != 0;
        }
        Check.That(won, $"Ability {special.Ability} applies as an interlock special after up to three paid attempts");
        await AssertRuleMoveAsync(player, combat, special.Move, $"Special {special.Ability} uses ILDB move {special.Move}");
        await player.WaitUntilAsync(() => context.Server.DebugLog()[logStart..].Contains(landed),
            $"Ability {special.Ability} deals damage through its round", TimeSpan.FromSeconds(5));
        string fightLog = context.Server.DebugLog()[logStart..];
        int queuedAt = fightLog.IndexOf("is the next move", StringComparison.Ordinal);
        Check.That(queuedAt >= 0 && queuedAt < fightLog.IndexOf(landed, StringComparison.Ordinal),
            $"Ability {special.Ability} queues before applying damage");

        player.SendRpc("44 00");
        await player.WaitForRpcAsync($"46 {HandleOf(target)} 00");
        Check.Equal(0, player.World.Problems.Count, $"Special {special.Ability} protocol framing");
    }

    private static async Task CombatRifleOpener(ScenarioContext context, ushort ability)
    {
        // Stock 254 from PR326 05837386fab5:5004 and 255 from PR321 c2d614206732:7809.
        string request = ability == 254
            ? "80 b9 fe 00 05 00 03 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ff ff 7f 7f 00 00 00 00 08 6b 00 00 00 00 00"
            : "80 b9 ff 00 05 00 03 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ff ff 7f 7f 00 00 00 00 08 6b 00 00 00 00 00";
        var player = await context.AddPlayerAsync(NearNpc($"RifleOpener{ability}") with
        {
            OwnedLoadedAbilities = new[] { (ability, (ushort)50), ((ushort)147, (ushort)50) },
            Items = new[] { (105, 692u, 1) },
            InnerStrength = 1000, MaxInnerStrength = 1000
        });
        await player.EnterWorldAsync();
        var target = await player.WaitForViewNamedAsync("WinterFrost");
        SelectStyle(player, 147);
        await player.WaitForViewUpdateAsync(2, $"02 80 80 80 80 80 10 {Le(147u)}");
        player.SendRpc($"40 {HandleOf(target)} 01 6b");
        await WaitForEntry(player, await WaitForCombatView(player));

        SendCapturedGunSpecial(player, request, target);
        await player.WaitForRpcAsync($"80 ba {Le((uint)ability)} 6b 15 00 00 00 00 00 00 00 00 00 00 00");
        Check.Equal(0, player.Received($"80 ac {Le(ability)}").Count, "The in-combat request starts no cast");
        Check.Equal(0, player.Received($"80 bd 01 {Le(ability)}").Count, "The refusal consumes no reuse timer");
        Check.That(!context.Server.DebugLog().Contains($"[ABILITY] {ability} lands"), "The refused shot causes no damage");

        player.SendRpc("44 00");
        await player.WaitForRpcAsync($"46 {HandleOf(target)} 00");
        var freeTarget = await player.WaitForViewNamedAsync("Trainman");
        player.SendRpc("3d 01");
        await player.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 80 02 21");
        SendCapturedGunSpecial(player, request, freeTarget);
        await player.WaitForRpcAsync($"80 ac {Le(ability)} 00 00 00 00 00 00 00 00 00 00 {Le(BitConverter.SingleToUInt32Bits(2f))}");
        Check.That(!context.Server.DebugLog().Contains($"[ABILITY] {ability} lands"),
            "The opening shot waits for its cast bar before landing");
        await player.WaitUntilAsync(() => context.Server.DebugLog().Contains($"[ABILITY] {ability} lands on view {freeTarget.ViewId} for "),
            "The rifle opener deals direct damage after leaving combat", TimeSpan.FromSeconds(8));
        await player.WaitForRpcAsync($"80 bd 01 {Le(ability)} 04");
        string queuePattern = $"47 00 ?? ?? 01 ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? 1a 00 06 00 00 00 01 00 00 01 ?? 01 ?? ?? ?? {Le(0x80000000u | ability)}";
        Check.Equal(0, player.Received(queuePattern).Count, "The rifle opener never queues a round move");
        Check.Equal(0, player.World.Problems.Count, $"Rifle opener {ability} protocol framing");
    }

    private static void SendCapturedGunSpecial(HeadlessPlayer player, string capturedRequest, ViewCreation target)
    {
        byte[] request = WireFormat.FromHex(capturedRequest);
        Check.Equal(43, request.Length, "Stock ability activation has its complete payload");
        WireFormat.FromHex(HandleOf(target)).CopyTo(request, 4);
        player.SendRpc(request);
    }
}
