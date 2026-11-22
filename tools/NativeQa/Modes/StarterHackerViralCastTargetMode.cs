using hds;
using HeadlessClient;
using static NativeQa.Modes.StarterHackerViralBaseMode;

namespace NativeQa.Modes;

public sealed class StarterHackerViralCastTargetMode : NativeQaMode
{
    private const int X = 17000, Y = 495, CasterZ = 8200, InZ = 6800, OutZ = 6100;
    private const int StartingIs = 700;
    private const ushort Family = 682;
    private static readonly NativeQaModePlan BasePlan = new StarterHackerViralBaseMode().Plan;
    private static readonly (string Handle, int Id, int X, int Z)[] Stands =
        { ("QAEnemy", 6114, X, InZ), ("QAFar", 6112, X + 100, InZ - 100), ("QAFriend", 6111, X + 100, OutZ) };

    public override string Name => "ability-parity-starter-hacker-viral-cast-target";

    public override NativeQaModePlan Plan { get; } = new(BasePlan.Abilities.ToArray(), X, CasterZ,
        BasePlan.BeforeLoadout.ToArray(), BasePlan.AfterLoadout.ToArray(), new[]
        {
            StarterHackerPassiveFixture.PutActor(X),
            $"UPDATE characters SET x={X},y={Y},z={CasterZ},rotation={MobAggro.YawTowards(X,CasterZ,X,InZ)} WHERE charId=110"
        }.Concat(Stands.SelectMany(helper => new[]
        {
            StarterHackerPassiveFixture.PutHelper(helper.Id, helper.X, 2, 3000, 3000),
            $"UPDATE characters SET z={helper.Z} WHERE charId={helper.Id}"
        }))
            .Append("DELETE FROM char_abilities WHERE char_id IN (6111,6112,6114)").ToArray(),
        DevCommands: true, hotbarAbilities: BasePlan.HotbarAbilities.ToArray(),
        testMobFixture: BasePlan.TestMobFixture, environmentOverrides: new(BasePlan.EnvironmentOverrides),
        extraQaMetadata: new() { ["ScopedStockRows"] = "53,57,58,59,60", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            $"Level 19, {BasePlan.Abilities.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(19)} memory. CSR ({X},{Y},{CasterZ}) faces QAEnemy IN ({X},{Y},{InZ}), 14 m south. QAFar ({X + 100},{Y},{InZ - 100}) and QAFriend ({X + 100},{Y},{OutZ}) stand near the two possible splash centres. All stands are verified outdoor NAV and clear of the ice.",
            "Choose Hacker Style on page 2 slot 1 and type /target QAEnemy. Start Harmful Code (slot 3, 4 s), then move QAEnemy OUT while the bar is still filling. Expect [ABILITY] 53 fizzles: the selected viral target changed or left range, 18 IS, 8 s own and 4 s shared reuse, with no damage.",
            $$"""OUT helper request: {"actor":"QAEnemy","action":"move","x":{{X}},"y":{{Y}},"z":{{OutZ}}}. IN helper request: {"actor":"QAEnemy","action":"move","x":{{X}},"y":{{Y}},"z":{{InZ}}}. CSR remains on the initial stand. OUT is 21 m away, beyond all five abilities' ranges.""",
            "After reuse, move QAEnemy IN. Start Code Nuke (slot 6, 4 s), then move QAEnemy OUT during the bar. Expect the same fizzle for 60, 11 IS, 2 s own and shared reuse, and no hit on QAEnemy, QAFar or QAFriend. Headless also runs the move during the 2 s slots 2/4/5.",
            "Move QAEnemy IN. Keep or reselect it and press slots 2–6, waiting for shared reuse each time. [PVP] hits QAEnemy for 8/12/11/17/7. Only 60 hits QAFar (7). QAFriend takes no damage. Cast/own/shared seconds: 2/4/4, 4/8/4, 2/4/4, 2/4/4, 4/2/2. [ABILITY-COST] 5/18/6/7/11, with final IS 624 after the two native fizzles. No max [VITALS] changes or interlock rounds."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (x, z) in Stands.Select(helper => (helper.X, helper.Z)).Prepend((X, OutZ)).Prepend((X, CasterZ)))
            Check.That(NavMesh.Locate(1, x, Y, z, out var floor) && !floor.indoors && Math.Abs(floor.floorY + 95 - Y) < 1 &&
                NavMesh.HasSightPath(1, X, Y, CasterZ, x, Y, z), $"Verified outdoor NAV and sight at ({x},{Y},{z})");

        var server = context.ActualServer!;
        foreach (var stand in Stands)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, stand.Handle, stand.Handle, stockCharacterLoad: true);
            context.Helpers.Add(helper.Handle, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var enemy = context.Helpers["QAEnemy"];
        var near = context.Helpers["QAFar"];
        var far = context.Helpers["QAFriend"];
        StarterHackerPassiveFixture.VerifyProfile(actor, BasePlan.Abilities.ToArray());
        uint handle = await StarterHackerPassiveFixture.StartAsync(actor, enemy, server, pvp: true);
        var view = await actor.WaitForViewNamedAsync(enemy.Handle);
        await actor.WaitForViewNamedAsync(near.Handle);
        await actor.WaitForViewNamedAsync(far.Handle);
        var recipients = new[] { enemy, near, far };
        int remainingIs = StartingIs;
        foreach (var sample in Samples)
        {
            int rpcStart = actor.World.RpcSnapshot().Count;
            int logStart = server.DebugLog().Length;
            int[] hitsStart = recipients.Select(player => player.World.RpcSnapshot().Count).ToArray();
            await BeginAsync(sample);
            await MoveAsync(OutZ);
            await actor.WaitUntilAsync(() => server.DebugLog()[logStart..].Contains(
                $"[ABILITY] {sample.Id} fizzles: the selected viral target changed or left range"),
                $"Accepted {sample.Id} fizzles when the original target leaves range", HeadlessPlayer.DefaultTimeout);
            await VerifyCostAndReuseAsync(sample, rpcStart);
            for (int i = 0; i < recipients.Length; i++)
                await VerifyNoHitAsync(recipients[i], sample, hitsStart[i]);
            await MoveAsync(InZ);
        }

        foreach (var sample in Samples)
        {
            int rpcStart = actor.World.RpcSnapshot().Count;
            int mainStart = enemy.World.RpcSnapshot().Count;
            int nearStart = near.World.RpcSnapshot().Count;
            int farStart = far.World.RpcSnapshot().Count;
            await BeginAsync(sample);
            await VerifyCostAndReuseAsync(sample, rpcStart);
            await VerifyHitAsync(enemy, sample, mainStart);
            if (sample.Id == 60) await VerifyHitAsync(near, sample, nearStart);
            else await VerifyNoHitAsync(near, sample, nearStart);
            await VerifyNoHitAsync(far, sample, farStart);
        }
        Check.Equal(0, actor.World.Problems.Count + recipients.Sum(player => player.World.Problems.Count), "Hacker cast target wire framing");
        Console.WriteLine("STARTER VIRAL CAST TARGET PASS 53/57/58/59/60");
        return Array.Empty<byte>();

        async Task BeginAsync(Sample sample)
        {
            actor.SkipReceived();
            actor.SendRpc(StarterHackerControlTargetMode.WithHandle(sample.Capture, handle));
            await EvidenceRetestWire.CastBarAsync(actor, sample.Id, sample.Cast);
        }

        async Task MoveAsync(int z)
        {
            byte[] position = new[] { (float)X, (float)Y, (float)z }.SelectMany(BitConverter.GetBytes).ToArray();
            int cursor = actor.World.ViewSnapshot().Count;
            enemy.World.SendViewList(Convert.FromHexString("02000108").Concat(position).Concat(new byte[] { 0, 0 }).ToArray());
            await EvidenceRetestWire.ViewSinceAsync(actor, cursor, view.ViewId, $"01 08 {Convert.ToHexString(position)}");
        }

        async Task VerifyCostAndReuseAsync(Sample sample, int cursor)
        {
            remainingIs -= sample.Cost;
            await actor.WaitUntilAsync(() => actor.World.ViewSnapshot().SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates)
                .Contains((ushort)remainingIs), $"Stock {sample.Id} cost leaves {remainingIs} IS", HeadlessPlayer.DefaultTimeout);
            await EvidenceRetestWire.TimerAsync(actor, cursor, sample.Id, AbilityCooldowns.OwnTimer, sample.Reuse);
            await EvidenceRetestWire.TimerAsync(actor, cursor, Family, AbilityCooldowns.GroupTimer, sample.SharedReuse);
            await EvidenceRetestWire.TimerAsync(actor, cursor, sample.Id, AbilityCooldowns.OwnTimer, sample.Reuse, ended: true);
            await EvidenceRetestWire.TimerAsync(actor, cursor, Family, AbilityCooldowns.GroupTimer, sample.SharedReuse, ended: true);
        }

        Task<RpcRecord> VerifyHitAsync(HeadlessPlayer receiver, Sample sample, int cursor)
        {
            byte[] expected = AbilityMessages.Damage(false, sample.Id, actor.Handle, receiver.Handle, sample.BaseDamage, 0);
            return StockAbilityProfile.ObserveAsync(receiver, cursor, frame => frame.Bytes.AsSpan().SequenceEqual(expected),
                $"{sample.Id} hits {receiver.Handle} for {sample.BaseDamage} after it returns to range");
        }

        async Task VerifyNoHitAsync(HeadlessPlayer receiver, Sample sample, int cursor)
        {
            receiver.SendRpc("81 48");
            await StockAbilityProfile.ObserveAsync(receiver, cursor, frame => frame.Opcode == 0x149, "Recipient stream advances beyond the cast");
            byte[] prefix = AbilityMessages.Damage(false, sample.Id, actor.Handle, receiver.Handle, 0, 0)[..^8];
            Check.That(!receiver.World.RpcSnapshot().Skip(cursor).Any(frame => frame.Bytes.AsSpan().StartsWith(prefix)),
                $"{sample.Id} leaves excluded helper {receiver.Handle} unharmed");
        }
    }
}
