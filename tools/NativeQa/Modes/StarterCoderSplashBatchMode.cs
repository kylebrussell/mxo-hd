using hds;
using MxoHd.PacketResearch;
using HeadlessClient;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class StarterCoderSplashBatchMode : NativeQaMode
{
    private const int Level = 18;
    private const ushort CoderStyle = 1008, Lockdown = 21;
    // PR409 stock PacketLog: Coder Style at 3277; Lockdown against the live selected target at 5348.
    private const string StockStyle = "80b9f003000000000000000000000000000000000000000000000000585cc70001000000086b0000000000";
    private const string StockLockdown = "80b915000d000b000000000000000000000000000000000000000000585cc70001000000086b0000000000";
    private sealed record Sample(ushort Id, int Damage, float Cast, int Reuse, bool Splash, string Request);
    private static readonly Sample[] Samples =
    {
        // Stock PR312 b0d0eb7ff285:6342–6343, PR389 d3b7112643d9:2786–2787, PR399 3c501e2874a7:3513–3514.
        new(33, 24, 2, 6, false, "80b92100050003000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000"),
        new(36, 4, 6, 30, true, "80b924000400020000000000000000000000000000000000000000007802c50001000000086b0000000000"),
        new(514, 6, 6, 30, true, "80b902020400020000000000000000000000000000000000000000000000000090767241086b0000000000")
    };
    private static readonly ushort[] Roots = { 2, 17, CoderStyle, Lockdown, 33, 36, 514 };
    private static readonly ushort[] Loadout = Roots.Concat(Roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
        .Distinct().Order().ToArray();
    private static readonly (string Name, uint CharId, int X, int Z)[] Targets =
    {
        ("QAEnemy", 6114, 25200, -1498), ("QAFriend", 6111, 25300, -1350), ("QAFar", 6112, 25440, -1610),
        ("QAOther", 6113, 25300, -1650)
    };

    public override string Name => "ability-parity-starter-coder-splash";
    public override NativeQaModePlan Plan { get; } = new(Loadout, 24800, -1498, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y=495,healthC=1000,healthM=1000,innerStrC=300,innerStrM=300,alignment=CASE WHEN charId IN (110,6113) THEN 1 ELSE 2 END,pvpflag=0 WHERE charId IN (110,6111,6112,6113,6114)",
        $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110",
        "DELETE FROM char_abilities WHERE char_id IN (6111,6112,6113,6114)"
    }.Concat(Targets.Select(stand => $"UPDATE characters SET x={stand.X},z={stand.Z} WHERE charId={stand.CharId}")).ToArray(),
        DevCommands: true, loadAbilityAncestors: true, hotbarAbilities: new ushort[] { CoderStyle, Lockdown, 33, 36, 514 }, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "10",
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "21,33,36,514", ["NativeCloseableRows"] = 0 }, readyMessages: new[]
        {
            "Choose Coder Style (page 2 slot 1). Select friendly QAOther and try slots 2–5. Expect one target refusal for any sent request, with no cast bar, IS debit or reuse.",
            "Select QAEnemy 4 m east and use slots 2–5 (Lockdown, Static Blast, Tinkering, Advanced Tinkering). Lockdown roots for 12 s. Static Blast hits only QAEnemy. Both Tinkering casts hit QAEnemy and opposing QAFriend. Friendly QAOther is inside the splash radius and stays unhit, as does far QAFar. Stay out of interlock."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        foreach (var stand in Targets)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, stand.Name, stand.Name, stockCharacterLoad: true);
            context.Helpers.Add(stand.Name, helper);
            await helper.EnterWorldAsync();
            NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, helper);
        }
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level), "The batch fits its starting memory budget");
        await caster.EnterWorldAsync();
        caster.SendRpc(StockStyle);
        await caster.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, caster);
        var enemy = context.Helpers["QAEnemy"];
        var near = context.Helpers["QAFriend"];
        var far = context.Helpers["QAFar"];
        var friendly = context.Helpers["QAOther"];
        var target = await caster.WaitForViewNamedAsync(enemy.Handle);
        foreach (var helper in context.Helpers.Values)
        {
            await caster.WaitForViewNamedAsync(helper.Handle);
            await helper.WaitForViewNamedAsync(caster.Handle);
        }

        var friendlyView = await caster.WaitForViewNamedAsync(friendly.Handle);
        uint friendlyHandle = DuelPackets.Handle(friendlyView.ViewId, friendlyView.SpawnId);
        caster.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(friendlyHandle))}");
        await TinkeringTargetAdmissionMode.RefusedAsync(caster, Lockdown,
            StarterHackerControlTargetMode.WithHandle(StockLockdown, friendlyHandle));
        foreach (var sample in Samples)
            await TinkeringTargetAdmissionMode.RefusedAsync(caster, sample.Id,
                StarterHackerControlTargetMode.WithHandle(sample.Request, friendlyHandle));

        uint targetHandle = DuelPackets.Handle(target.ViewId, target.SpawnId);
        caster.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(targetHandle))}");
        int castAt = caster.World.RpcSnapshot().Count, rootAt = enemy.World.RpcSnapshot().Count;
        caster.SendRpc(StarterHackerControlTargetMode.WithHandle(StockLockdown, targetHandle));
        await EvidenceRetestWire.CastBarAsync(caster, Lockdown, 2);
        await EvidenceRetestWire.TimerAsync(caster, castAt, Lockdown, AbilityCooldowns.OwnTimer, 13.5f);
        await ObserveAsync(enemy, rootAt, frame =>
            ManageBonusRecord.TryDecode(frame.Bytes, out var root) && root!.SourceId == Lockdown &&
            root.Type == TempMods.KindState && root.TargetId == TempMods.RootedAbility &&
            root.AddRemoveType == 0 && root.Duration == 12, "Lockdown roots the opposing player for 12 seconds");

        foreach (Sample sample in Samples)
        {
            int nearCursor = near.World.RpcSnapshot().Count, farCursor = far.World.RpcSnapshot().Count;
            int friendlyCursor = friendly.World.RpcSnapshot().Count;
            await StarterFixedDamageMode.Land(caster, enemy, target, sample.Id, sample.Damage, sample.Cast, sample.Reuse, sample.Request);
            if (sample.Splash)
                await ObserveAsync(near, nearCursor, Hit(near), $"{sample.Id} reaches the hostile secondary inside 2 m");
            else
            {
                await BarrierAsync(near);
                Check.That(!near.World.RpcSnapshot().Skip(nearCursor).Any(Hit(near)), "Static Blast remains single-target");
            }
            await BarrierAsync(far);
            Check.That(!far.World.RpcSnapshot().Skip(farCursor).Any(Hit(far)), $"{sample.Id} excludes the hostile outside 2 m");

            await BarrierAsync(friendly);
            Check.That(!friendly.World.RpcSnapshot().Skip(friendlyCursor).Any(Hit(friendly)),
                $"{sample.Id} leaves the friendly player inside 2 m untouched");

            Func<RpcRecord, bool> Hit(HeadlessPlayer targetPlayer)
            {
                byte[] damage = AbilityMessages.Damage(false, sample.Id, caster.Handle, targetPlayer.Handle, sample.Damage, 0);
                return packet => packet.Bytes.AsSpan().SequenceEqual(damage);
            }
        }
        Check.Equal(0, caster.World.Problems.Count + context.Helpers.Values.Sum(helper => helper.World.Problems.Count), "Coder damage and splash framing");
        return Array.Empty<byte>();
    }

    private static async Task BarrierAsync(HeadlessPlayer observer)
    {
        int cursor = observer.World.RpcSnapshot().Count;
        observer.SendRpc("81 48");
        var barrier = BytePattern.Parse("81 49");
        await ObserveAsync(observer, cursor, packet => barrier.IsMatch(packet.Bytes), "post-landing observer barrier");
    }
}
