using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterSelectivePhageMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 2300, EnemyX = 17300;
    private const ushort Phage = 83, Upgrade = 1082, Style = 1006, Barrage = 57, DamageControl = 330;
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Style, Phage, Barrage, DamageControl).Order().ToArray();
    private static readonly ushort[] HelperLoadout = StockAbilityProfile.Profile(2, 17, Style, DamageControl).Order().ToArray();

    public override string Name => "ability-parity-starter-selective-phage";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        PvpOpponentsFixture.NativeSql,
        $"UPDATE characters SET x={X},y={Y},z={Z},rotation=64 WHERE charId=110",
        $"UPDATE characters SET x={EnemyX},y={Y},z={Z} WHERE charId=6114",
        "DELETE FROM char_abilities WHERE char_id=6114",
        "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " +
            string.Join(",", HelperLoadout.Select((id, i) => $"(6114,{400 + i},{AbilityMessages.AbilityGameObject(id)},50,1)"))
    }, DevCommands: true, hotbarAbilities: new[] { Style, Upgrade, Barrage, DamageControl },
        testMobFixture: "martial-player-controls.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "57,83,1082", ["NativeCloseableRows"] = 1, ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost) },
        readyMessages: new[]
        {
            $"Level 50 for the higher-damage control, rank-15 Selective Phage is the starter row. Complete {Loadout.Sum(AbilityMemory.Cost)}/100 memory. CSR ({X},{Y},{Z}) faces QAEnemy ({EnemyX},{Y},{Z}) on the clear street. Page 2: Hacker, Selective Phage Upgrade, Barrage 57 and Barrage 330. 83 is already loaded and grants 1082. Select Hacker, then type /target QAEnemy if needed. Stay out of interlock.",
            """Before using 1082, cast slot 4 on QAEnemy. Expect [FREECAST-VIRAL] inputs=80/42 and [TYPED-RESISTANCE OURS] raw640. Request the same incoming 330 with {"actor":"QAEnemy","action":"cast","ability":330,"target":"CSR_Tester"} via the helper request file. Expect incoming 80/42 and raw640 too. Keep Speed and wait 4 s between successive casts from the same actor.""",
            "Use page 2 slot 2 with no selection. If needed look for Selective Phage Upgrade in Actions. Preserve the first complete outgoing 80 b9 3a 04 request. Expect 20 IS, 30 s own reuse and [MODIFIER] char=110 source=1082 stat=183 +2, 172 +2 and 342 +4, all duration=0s. Reselect QAEnemy.",
            "Cast slot 4 and request the same incoming 330 again. Expect outgoing inputs 85/42 and raw648, incoming inputs 80/44 and raw640. Type ?dismissbuff 1082 to remove all three records, then repeat both casts to recover inputs 80/42 and raw640 in both directions. Slot 3 is an optional small known-damage control. No interlock [PVP] rounds or maximum [VITALS] changes."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer!;
        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(enemy.Handle, enemy);
        await PvpOpponentsFixture.EnterAsync(enemy);
        NativeQaReadiness.RequireNoAmbientAggro(server, enemy);
        await SelectStyleAsync(enemy);
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(50) && Loadout.All(id => archive.ContainsKey(id) &&
            AbilityPrerequisites.Current.Closure(id).All(need => archive.TryGetValue(need.Key, out var row) && row.Level >= need.Value)),
            "The full ranked stock profile fits memory");
        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        await StockAbilityProfile.ObserveAsync(actor, 0, StockAbilityProfile.GrantRecord(Upgrade, false), "83 grants the stock action 1082");
        await SelectStyleAsync(actor);
        var view = await actor.WaitForViewNamedAsync(enemy.Handle);
        uint handle = DuelPackets.Handle(view.ViewId, view.SpawnId);
        var outgoing = await Observe330Async(enemy, actor.Handle, () =>
        {
            actor.SendRpc(StarterHackerControlTargetMode.WithHandle(StarterTransmitVirusMode.IncomingStock, handle));
            return Task.CompletedTask;
        });
        var incoming = await Observe330Async(actor, enemy.Handle, () =>
            StarterTransmitVirusMode.SendProbeAsync(enemy, actor.Handle, DamageControl));
        Check.Equal((640, 640), (outgoing, incoming), "Both captured stock baseline casts deal their reference raw damage");
        Check.Equal(0, actor.World.Problems.Count + enemy.World.Problems.Count, "Selective Phage setup and stock control framing");
        Console.WriteLine($"SELECTIVE PHAGE BASELINE PASS: outgoing {outgoing}, incoming {incoming}. Native 1082 capture pending.");
        return Array.Empty<byte>();

        async Task<int> Observe330Async(HeadlessPlayer recipient, string source, Func<Task> send)
        {
            int cursor = recipient.World.RpcSnapshot().Count;
            byte[] prefix = AbilityMessages.Damage(false, DamageControl, source, recipient.Handle, 0, 0);
            await send();
            var hit = await StockAbilityProfile.ObserveAsync(recipient, cursor, frame => frame.Bytes.Length == prefix.Length &&
                frame.Bytes.AsSpan().StartsWith(prefix.AsSpan(0, prefix.Length - 8)), $"Stock 330 lands from {source}");
            return BitConverter.ToInt32(hit.Bytes, hit.Bytes.Length - 8) + BitConverter.ToInt32(hit.Bytes, hit.Bytes.Length - 4);
        }
    }

    private static async Task SelectStyleAsync(HeadlessPlayer actor)
    {
        actor.SendRpc(StarterHackerViralSightMode.StockStyle);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");
    }
}
