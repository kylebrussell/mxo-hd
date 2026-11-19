namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task CombatGunSpecialQueue(ScenarioContext context, ushort ability)
    {
        // Full CSR_Tester requests from the PR321 gunman PacketLog (c2d614206732).
        // Lines 3177, 2694, 10217, 5196, and 7809, respectively.
        (ushort Ability, ushort Style, uint Weapon, uint Move, float CastSeconds, string Request)[] specials =
        {
            (243, 129, 2565, 9518, 0, "80 b9 f3 00 05 00 03 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 f3 00 00 00 00 6b 00 00 08 6b 00 00 00 00 00"),
            (126, 129, 2565, 9522, 4, "80 b9 7e 00 05 00 03 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ff ff 7f 7f 00 00 00 00 08 6b 00 00 00 00 00"),
            (176, 158, 698, 21083, 4, "80 b9 b0 00 05 00 03 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 b0 00 00 00 00 6b 00 00 08 6b 00 00 00 00 00"),
            (254, 147, 692, 11361, 2, "80 b9 fe 00 05 00 03 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ff ff 7f 7f 00 00 00 00 08 6b 00 00 00 00 00"),
            (255, 147, 692, 11360, 2, "80 b9 ff 00 05 00 03 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ff ff 7f 7f 00 00 00 00 08 6b 00 00 00 00 00"),
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
        if (special.Ability == 255)
        {
            var freeTarget = await player.WaitForViewNamedAsync("Trainman");
            int queued = player.Received(queuePattern).Count;
            await Task.Delay(TimeSpan.FromSeconds(11)); // Natural reuse and post-interlock stance reset.
            SendCapturedGunSpecial(player, special.Request, freeTarget);
            await player.WaitUntilAsync(() => context.Server.DebugLog().Contains($"[ABILITY] 255 lands on view {freeTarget.ViewId} for "),
                "Disabling Shot also lands as a free cast when there is no matching interlock", TimeSpan.FromSeconds(8));
            Check.Equal(queued, player.Received(queuePattern).Count, "Free cast does not queue a round move");
        }
        Check.Equal(0, player.World.Problems.Count, $"Special {special.Ability} protocol framing");
    }

    private static void SendCapturedGunSpecial(HeadlessPlayer player, string capturedRequest, ViewCreation target)
    {
        byte[] request = WireFormat.FromHex(capturedRequest);
        Check.Equal(43, request.Length, "Stock ability activation has its complete payload");
        WireFormat.FromHex(HandleOf(target)).CopyTo(request, 4);
        player.SendRpc(request);
    }
}
