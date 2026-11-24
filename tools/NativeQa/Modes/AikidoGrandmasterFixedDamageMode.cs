using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class AikidoGrandmasterFixedDamageMode : MartialFixedDamageMode
{
    // PR473 landed stock specials and PR476 no-target Ki Burst.
    public AikidoGrandmasterFixedDamageMode() : base("aikido-grandmaster", 101, MartialArtsStarterAdmissionMode.StockAikido,
        new Move[]
        {
            new(407, 21, 28, 23, 10, 827, "80b9970104000200000000000000000000000000000000000000000097010000006b0000086b0000000000"),
            new(411, 96, 129, 106, 10, 735, "80b99b010400020000000000000000000000000000000000000000009b010000006b0000086b0000000000"),
            new(412, 113, 152, 125, 10, 751, "80b99c010400020000000000000000000000000000000000000000009c010000006b0000086b0000000000")
        },
        new[]
        {
            "Start with Aikido slot 1 and clear selection. Use slot 5 Ki Burst for a 1 s area cast. After reuse use ?cd a75, confirm CSR is at printed AREA HOME (17000,495,6448) or tp there, select Aikido if needed and repeat slot 5. Then ?cd a0 to restore neutral and move to the interlock steps.",
            "411 and 412 get Off Balance. Use 412 last and let its possible resistance loss expire before the next neutral stage."
        }, extraAbilities: new ushort[] { 406 }, area: true)
    {
    }

    protected override async Task<ViewCreation> BeforeOpeningAsync(NativeQaBootstrapContext context, HeadlessPlayer actor, ViewCreation dummy)
    {
        const string kiBurst = "80b99601000000000000000000000000000000000000000000000000585cc70001000000086b0000000000";
        Console.WriteLine($"READY AREA HOME {InvisibilityBeltWatcherMode.Warp(X, Y, 6448, 0, inside: false)}");
        Check.That(MartialArtsRemainingSpecials.TryResolve(AbilityCatalog.Find(406), out var stored) && stored == 23,
            "Ki Burst has one fixed physical GOB hit");
        actor.SendRpc(MartialArtsStarterAdmissionMode.StockAikido);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 65 00 00 00");
        await AreaAsync(31, resisted: false);
        dummy = await ResetAsync("?cd a75");
        await AreaAsync(25, resisted: true);
        return await ResetAsync("?cd a0");

        async Task<ViewCreation> ResetAsync(string chat)
        {
            int from = actor.World.CreationSnapshot().Count;
            actor.SkipReceived();
            actor.SendChat(chat);
            await actor.WaitForTextAsync("Fixture reset: Melee Dummy restored");
            await actor.WaitUntilAsync(() => actor.World.CreationSnapshot().Skip(from).Any(v => v.Name == dummy.Name),
                "Fresh dummy for Ki Burst", HeadlessPlayer.DefaultTimeout);
            return actor.World.CreationSnapshot().Last(v => v.Name == dummy.Name);
        }

        async Task AreaAsync(int amount, bool resisted)
        {
            var server = context.ActualServer!;
            int rpc = actor.World.RpcSnapshot().Count, log = server.DebugLog().Length;
            actor.SendRpc(kiBurst);
            await EvidenceRetestWire.CastBarAsync(actor, 406, 1);
            await EvidenceRetestWire.TimerAsync(actor, rpc, 406, AbilityCooldowns.OwnTimer, 15);
            await actor.WaitUntilAsync(() =>
            {
                string tail = server.DebugLog()[log..];
                return tail.Contains($"[ABILITY] 406 lands on view {dummy.ViewId} for {amount}", StringComparison.Ordinal) &&
                    (!resisted || tail.Contains("[TYPED-RESISTANCE OURS] 406 type0 total75 raw31 final25", StringComparison.Ordinal));
            }, $"Fixed Ki Burst hit {amount}", HeadlessPlayer.DefaultTimeout);
            if (resisted)
                await EvidenceRetestWire.TimerAsync(actor, rpc, 688, AbilityCooldowns.GroupTimer, 5, ended: true);
            else
            {
                var ended = BytePattern.Parse("80 bd 01 96 01 04 00 00 70 41 00");
                await actor.WaitUntilAsync(() => actor.World.RpcSnapshot().Skip(rpc).Any(f => ended.IsMatch(f.Bytes)),
                    "15 s Ki Burst timer expires", TimeSpan.FromSeconds(18));
            }
        }
    }
}
