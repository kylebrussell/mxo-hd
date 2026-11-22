using System.Buffers.Binary;
using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class CoderStaticBlastTiersMode : NativeQaMode
{
    private const int Level = 29, X = 17000, Y = 495, Z = 8100, NearZ = 6600, FarZ = 6400, Resist = 43;
    private const ushort Coder = 1008, EmptyMind = 8, Blast = 33, Barrage = 590, HigherBlast = 591;
    private static readonly (string Name, uint Id, int X, int Z, int Organization)[] Stands =
    {
        ("QAEnemy", 6114, X, NearZ, 2),
        ("QAFar", 6112, X + 100, NearZ, 2),
        ("QAFriend", 6111, X, 6950, 1),
        ("QAOther", 6113, X, FarZ, 2)
    };
    private static readonly (ushort Id, ushort Family, int Cost, int Raw, int Resisted)[] Samples =
    {
        (Blast, AbilityCooldowns.VirusDamage1, 15, 26, 21),
        (Barrage, AbilityCooldowns.VirusEnhancedDamage, 18, 39, 32),
        (HigherBlast, AbilityCooldowns.VirusDamage1, 23, 51, 42)
    };
    private static readonly ushort[] Hotbar = Samples.Select(row => row.Id).Prepend(Coder).ToArray();
    private static readonly ushort[] Loadout = Hotbar.Concat(new ushort[] { 2, 17 })
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();
    private static readonly int Memory = Loadout.Sum(AbilityMemory.Cost);
    private static readonly ushort[] Defender = { 2, 17, EmptyMind };

    public override string Name => "ability-parity-coder-static-blast-tiers";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        PvpOpponentsFixture.NativeSql,
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)} WHERE charId IN (110,6111,6112,6113,6114)",
        $"UPDATE characters SET x={X},y={Y},z={Z},rotation={MobAggro.YawTowards(X,Z,X,NearZ)},innerStrC=500,innerStrM=1000 WHERE charId=110",
        $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110",
        "DELETE FROM char_abilities WHERE char_id IN (6111,6112,6113,6114)",
        "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " + string.Join(",", Defender.Select((id, i) =>
            $"(6112,{i},{0x80000000u | ((uint)id << 10)},{Level},1)"))
    }.Concat(Stands.Select(stand =>
        $"UPDATE characters SET x={stand.X},y={Y},z={stand.Z},alignment={stand.Organization} WHERE charId={stand.Id}")).ToArray(),
        DevCommands: true, hotbarAbilities: Hotbar, testMobFixture: "martial-player-controls.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1", ["MXO_FREECAST_VIRAL_QA"] = "1",
            ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "33,590,591", ["NativeCloseableRows"] = 2, ["MemoryUsed"] = Memory },
        readyMessages: new[]
        {
            $"Level {Level}, legal ranked profile {Memory}/{AbilityMemory.ForLevel(Level)} memory. Clear outdoor NAV CSR ({X},{Y},{Z}) faces QAEnemy ({X},{Y},{NearZ}), 15 m away. QAFar ({X+100},{Y},{NearZ}) is the resistance recipient. Allied QAFriend ({X},{Y},6950) and out-of-range QAOther ({X},{Y},{FarZ}) are controls. All are clear of ice.",
            "Page 2 slots 1–4 are Coder Style, 33, 590 and 591. After choosing the style, try both higher tiers without a target, then /target QAFriend, then /target QAOther at 17 m. Client-side suppression is acceptable. If sent, expect the selected-enemy notice or range reason 34 and no cost, timer or hit. IS stays 500.",
            "Use /target QAEnemy and press slots 2, 3 and 4. Retain the first complete stock 590 and 591 requests that are sent. 33 and 591 share 682 / 4 s, so 591 is locally unavailable or reaches a reuse refusal until 682 ends. 590 uses 683 / 4 s and can cast during 682. Each landed cast takes 2 s and starts 6 s own reuse. Expect 26/39/51 damage on QAEnemy only. IS 500->485->467->444.",
            "After own reuse clears, /target QAFar and submit helper action aura-resist. [MODIFIER] char=6112 source=8 stat=157 +43 for 20 s. Repeat slots 2, 3 and 4 while the bonus lasts. Expect typed raw/final 26/21, 39/32 and 51/42, exclusively on QAFar. IS 444->429->411->388. No maximum [VITALS] change or [PVP] interlock round. Projected DONE +2 (590, 591)."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Static Blast tiers need repository NAV data.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (x, z) in Stands.Select(stand => (stand.X, stand.Z)).Append((X, Z)))
            Check.That(NavMesh.Locate(1, x, Y, z, out var at) && !at.indoors && Math.Abs(at.floorY + 95 - Y) < 1 &&
                NavMesh.HasSightPath(1, X, Y, Z, x, Y, z), $"Clear outdoor stand ({x},{Y},{z})");

        var server = context.ActualServer!;
        foreach (var stand in Stands)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, stand.Name, stand.Name, stockCharacterLoad: true);
            context.Helpers.Add(stand.Name, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(Memory <= AbilityMemory.ForLevel(Level) && archive.Keys.ToHashSet().SetEquals(Loadout) &&
            Loadout.All(id => AbilityPrerequisites.Current.Closure(id).All(need =>
                archive.TryGetValue(need.Key, out var row) && row.Level >= need.Value)), "Complete legal rank-29 profile");
        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var targetViews = new Dictionary<string, ViewCreation>();
        foreach (var stand in Stands)
        {
            var view = await actor.WaitForViewNamedAsync(stand.Name);
            Check.That(EngageFoesFixture.Position(view).SequenceEqual(new double[] { stand.X, Y, stand.Z }),
                $"{stand.Name} is on the documented stand");
            targetViews.Add(stand.Name, view);
        }

        actor.SendRpc(StarterCoderSplashBatchMode.StockStyle);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");
        int controlsAt = actor.World.ViewSnapshot().Count;
        foreach (ushort id in new[] { Barrage, HigherBlast })
        {
            await BadSelectionAsync(id, 0);
            await BadSelectionAsync(id, Select("QAFriend"));
            uint far = Select("QAOther");
            int cursor = actor.World.RpcSnapshot().Count;
            actor.SendRpc(Request(id, far));
            byte[] refusal = AbilityRefusals.Encode(new AbilityRefusals.Record(id, AbilityRefusals.TargetOutOfRange));
            await StockAbilityProfile.ObserveAsync(actor, cursor, frame => frame.Bytes.AsSpan().SequenceEqual(refusal), "Out-of-range refusal");
        }
        Check.That(!actor.World.ViewSnapshot().Skip(controlsAt).SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates).Any(),
            "Invalid target requests leave IS unchanged");

        int remaining = 500;
        await CompareAsync("QAEnemy", resisted: false);
        var resistedTarget = context.Helpers["QAFar"];
        await StarterPathogenistAuraResistanceMode.ApplyResistanceAsync(resistedTarget, Resist);
        await CompareAsync("QAFar", resisted: true);
        Check.Equal(388, remaining, "Exactly two legitimate casts of each tier spend 112 IS");
        Check.Equal(0, actor.World.Problems.Count + context.Helpers.Values.Sum(player => player.World.Problems.Count), "Static Blast tiers framing");
        Console.WriteLine("STATIC BLAST TIERS PASS: fixed bases, neutral/resisted landed values, live-target refusals, costs and reuse.");
        return Array.Empty<byte>();

        uint Select(string name)
        {
            var view = targetViews[name];
            uint handle = DuelPackets.Handle(view.ViewId, view.SpawnId);
            actor.SendRpc("81 51 " + WireFormat.ToHex(BitConverter.GetBytes(handle)));
            return handle;
        }

        async Task BadSelectionAsync(ushort id, uint handle)
        {
            actor.SkipReceived();
            actor.SendRpc(Request(id, handle));
            await actor.WaitForTextAsync("Select a living enemy before using this ability.");
        }

        async Task CompareAsync(string target, bool resisted)
        {
            uint handle = Select(target);
            var recipient = context.Helpers[target];
            int sharedAt = 0, lastAt = 0;
            foreach (var row in Samples)
            {
                if (row.Id == HigherBlast)
                    await EvidenceRetestWire.TimerAsync(actor, sharedAt, AbilityCooldowns.VirusDamage1, AbilityCooldowns.GroupTimer, 4, ended: true);
                int rpc = actor.World.RpcSnapshot().Count, vitals = actor.World.ViewSnapshot().Count;
                int targetRpc = recipient.World.RpcSnapshot().Count;
                var excluded = context.Helpers.Values.Where(peer => peer != recipient).ToDictionary(peer => peer, peer => peer.World.RpcSnapshot().Count);
                actor.SendRpc(Request(row.Id, handle));
                await EvidenceRetestWire.CastBarAsync(actor, row.Id, 2);
                await EvidenceRetestWire.TimerAsync(actor, rpc, row.Id, AbilityCooldowns.OwnTimer, 6);
                await EvidenceRetestWire.TimerAsync(actor, rpc, row.Family, AbilityCooldowns.GroupTimer, 4);
                int damage = resisted ? row.Resisted : row.Raw;
                byte[] hit = AbilityMessages.Damage(false, row.Id, actor.Handle, recipient.Handle, damage, row.Raw - damage);
                await StockAbilityProfile.ObserveAsync(recipient, targetRpc, frame => frame.Bytes.AsSpan().SequenceEqual(hit),
                    $"Ability {row.Id} hits the selected recipient for {damage}");
                remaining -= row.Cost;
                await actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(vitals)
                    .SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates).Contains((ushort)remaining),
                    $"Ability {row.Id} leaves {remaining} IS", HeadlessPlayer.DefaultTimeout);

                if (row.Id == Blast)
                {
                    sharedAt = rpc;
                    int refusalAt = actor.World.RpcSnapshot().Count;
                    actor.SendRpc(Request(HigherBlast, handle));
                    byte[] reuseWait = AbilityCooldowns.BuildDenied(HigherBlast, AbilityCooldowns.DeniedTimerRunning);
                    await StockAbilityProfile.ObserveAsync(actor, refusalAt, frame => frame.Bytes.AsSpan().SequenceEqual(reuseWait),
                        "591 cannot pass 33's active shared timer");
                }
                lastAt = rpc;
                foreach (var (peer, cursor) in excluded)
                {
                    peer.SendRpc("81 48");
                    await StockAbilityProfile.ObserveAsync(peer, cursor, frame => frame.Opcode == 0x149, "Excluded peer stream fence");
                    byte[] prefix = AbilityMessages.Damage(false, row.Id, actor.Handle, peer.Handle, 0, 0)[..^8];
                    Check.That(!peer.World.RpcSnapshot().Skip(cursor).Any(frame => frame.Bytes.AsSpan().StartsWith(prefix)),
                        $"Ability {row.Id} does not hit {peer.Handle}");
                }
            }
            if (!resisted)
                await EvidenceRetestWire.TimerAsync(actor, lastAt, HigherBlast, AbilityCooldowns.OwnTimer, 6, ended: true);
        }
    }

    private static byte[] Request(ushort ability, uint target)
    {
        // PR312's complete selected-target 33 capture. The higher tiers retain this proven wire format
        // and substitute the ability id until their first native requests are collected.
        byte[] frame = StarterHackerControlTargetMode.WithHandle(StarterCoderSplashBatchMode.Samples[0].Request, target);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), ability);
        return frame;
    }
}
