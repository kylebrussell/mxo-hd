namespace HeadlessClient;

public static partial class Scenarios
{
    private const string HugId = "fd 0d 00 58"; // /hug's string id 0x58000dfd
    private const uint Hug = 0x58000dfd;

    private static byte[] EmoteRecord(uint emoteId, byte[] target) =>
        new byte[] { 0x30 }.Concat(BitConverter.GetBytes(emoteId)).Concat(target).ToArray();

    private static byte[] HandleBytes(ViewCreation view) =>
        new[] { (byte)view.ViewId, (byte)(view.ViewId >> 8), view.SpawnId, (byte)0 };

    /// <summary>
    /// /hug on another player: the target gets 31 with the requester as it knows them; everyone sees the requester
    /// beckon; the target's 32 accept reaches the requester as 32 with IsRequester 1 and the accepted animation plays.
    /// Then a refusal, and a plain emote everyone sees.
    /// </summary>
    private static async Task PairedEmoteAskAcceptAndRefuse(ScenarioContext context)
    {
        (HeadlessPlayer alice, HeadlessPlayer bob, ViewCreation bobForAlice, ViewCreation aliceForBob) =
            await TwoNearby(context, new CharacterSeed("Alice"), new CharacterSeed("Bob") { X = 17120 });

        alice.SendRpc(EmoteRecord(Hug, HandleBytes(bobForAlice)));
        await bob.WaitForRpcAsync($"31 {HugId} {HandleOf(aliceForBob)}");
        // Alice beckons (action 201) with Bob as each client knows him: her own view, and Bob's view of her
        await alice.WaitForViewUpdateAsync(2, $"01 28 ?? 01 00 25 c9 00 00 10 {HandleOf(bobForAlice)}");
        await bob.WaitForViewUpdateAsync(aliceForBob.ViewId, $"01 2a ?? 01 00 25 c9 00 00 10 {SelfHandle(bob)} 00");

        // Bob accepts as his client does: 32 01 <emote> <Alice as he knows her> 00000000 00
        bob.SendRpc($"32 01 {HugId} {HandleOf(aliceForBob)} 00 00 00 00 00");
        await alice.WaitForRpcAsync($"32 01 {HugId} {HandleOf(bobForAlice)} 00 00 00 00 01");
        await alice.WaitForViewUpdateAsync(2, $"01 28 ?? 02 00 25 ca 00 00 10 {HandleOf(bobForAlice)} 00 00 00 00 00 00 00 00 00 00 0c 43 db 0f 49 40");
        await bob.WaitForViewUpdateAsync(aliceForBob.ViewId, $"01 2a ?? 02 00 25 ca 00 00 10 {SelfHandle(bob)}");
        Check.Equal(0, bob.Received("32").Count, "The target gets no 32");

        // Bob refuses the next one: Alice hears it, and nothing more plays
        alice.SendRpc(EmoteRecord(Hug, HandleBytes(bobForAlice)));
        await bob.WaitForRpcAsync($"31 {HugId}");
        bob.SendRpc($"32 02 {HugId} {HandleOf(aliceForBob)} 00 00 00 00 00");
        await alice.WaitForRpcAsync($"32 02 {HugId}");

        // A hug on nobody: "not a player"
        alice.SendRpc(EmoteRecord(Hug, new byte[4]));
        await alice.WaitForRpcAsync($"32 05 {HugId}");

        // A plain emote (/clap, 0d 00 00 3a → action 4 in the server's table) now reaches others too
        alice.SendRpc(EmoteRecord(0x3a00000d, new byte[4]));
        await bob.WaitForViewUpdateAsync(aliceForBob.ViewId, "01 2a ?? 40 00 25 04 00 00 10 00");
    }

    /// <summary>/sit (34 0a) and /stand (33 0a): the player's own view and other players' views of them, with the
    /// sitting bit of ConditionStateFlags; a dance (34 13) has no attributes.</summary>
    private static async Task StancesSeenByOthers(ScenarioContext context)
    {
        (HeadlessPlayer alice, HeadlessPlayer bob, _, ViewCreation aliceForBob) =
            await TwoNearby(context, new CharacterSeed("Alice"), new CharacterSeed("Bob") { X = 17120 });

        alice.SendRpc("34 0a");
        await alice.WaitForViewUpdateAsync(2, $"03 28 ?? 40 0a 00 00 10 {Wild(16)} 80 80 80 80 80 80 01 00 00 20 00");
        await bob.WaitForViewUpdateAsync(aliceForBob.ViewId, $"03 2a ?? 40 0a 00 00 10 00 {Wild(12)} 80 80 80 80 04 00 00 20 00");

        alice.SendRpc("33 0a");
        await alice.WaitForViewUpdateAsync(2, $"03 28 ?? 40 00 00 00 10 {Wild(16)} 80 80 80 80 80 80 01 00 00 00 00");
        await bob.WaitForViewUpdateAsync(aliceForBob.ViewId, $"03 2a ?? 40 00 00 00 10 00 {Wild(12)} 80 80 80 80 04 00 00 00 00");

        alice.SendRpc("34 13");
        await bob.WaitForViewUpdateAsync(aliceForBob.ViewId, "01 2a ?? 40 13 00 00 10 00");
    }

    /// <summary>SocialBot sits for one client to see, asks for a hug, and accepts one.</summary>
    private static async Task SocialBotEmotesAndStances(ScenarioContext context)
    {
        HeadlessPlayer alice = (await context.AddPlayersInWorldAsync(new CharacterSeed("Alice")))[0];
        alice.SendChat("?socialbot");
        ViewCreation bot = await alice.WaitForViewNamedAsync("SocialBot");

        alice.SendChat("?socialbot sit");
        await alice.WaitForViewUpdateAsync(bot.ViewId, $"03 2a ?? 40 0a 00 00 10 00 {Wild(12)} 80 80 80 80 04 00 00 20 00");
        alice.SendChat("?socialbot stand");
        await alice.WaitForViewUpdateAsync(bot.ViewId, "03 2a ?? 40 00 00 00 10 00");

        // The bot asks: Alice's client gets 31 with the bot as it knows it, and accepts
        alice.SendChat("?socialbot hug");
        await alice.WaitForRpcAsync($"31 {HugId} {HandleOf(bot)}");
        alice.SendRpc($"32 01 {HugId} {HandleOf(bot)} 00 00 00 00 00");
        await alice.WaitForViewUpdateAsync(bot.ViewId, $"01 2a ?? 02 00 25 ca 00 00 10 {SelfHandle(alice)}");

        // Alice asks the bot: it accepts a second later
        alice.SendRpc(EmoteRecord(Hug, HandleBytes(bot)));
        await alice.WaitForRpcAsync($"32 01 {HugId} {HandleOf(bot)} 00 00 00 00 01");
    }
}
