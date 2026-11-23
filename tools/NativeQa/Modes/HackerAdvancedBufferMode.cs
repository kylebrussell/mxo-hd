using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class HackerAdvancedBufferMode : NativeQaMode
{
    private const ushort Overflow = 481, Cancel = 488, Enhancement = 173, Firewall = 319, Style = CombatStyles.HackerStyle;
    private const int Level = 50, SupportLevel = 30, X = 17000, Y = 495, Z = 8100, TargetZ = 7900, FarZ = 6400;
    private static readonly ushort[] Loadout = new ushort[] { 2, 17, Style, Overflow, Cancel }
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();
    private static readonly ushort[] SupportLoadout = StockAbilityProfile.Profile(2, 17, Style, Enhancement, Firewall).Order().ToArray();
    private static readonly (string Name, int Id, int X, int Z, int Team, int Level)[] Stands =
    {
        ("QAEnemy", 6114, X, TargetZ, 2, 1), ("QAFriend", 6111, X + 100, Z, 1, Level),
        ("QAFar", 6112, X, TargetZ - 200, 2, SupportLevel)
    };

    public override string Name => "ability-parity-hacker-advanced-buffer";

    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        PvpOpponentsFixture.NativeSql,
        $"UPDATE characters SET x={X},y={Y},z={Z},level={Level},exp={PlayerProgression.ExperienceForLevel(Level)}," +
            $"rotation={MobAggro.YawTowards(X,Z,X,TargetZ)},alignment=1,innerStrC=500,innerStrM=1000 WHERE charId=110",
        "DELETE FROM char_abilities WHERE char_id IN (6111,6112,6114)",
        "INSERT INTO char_abilities (char_id,slot,ability_id,level,is_loaded) VALUES " +
            string.Join(",", SupportLoadout.Select((id, slot) => $"(6112,{400 + slot},{AbilityMessages.AbilityGameObject(id)},{SupportLevel},1)"))
    }.Concat(Stands.Select(stand => $"UPDATE characters SET x={stand.X},y={Y},z={stand.Z},alignment={stand.Team}," +
        $"level={stand.Level},exp={PlayerProgression.ExperienceForLevel(stand.Level)} WHERE charId={stand.Id}")).ToArray(),
        DevCommands: true, hotbarAbilities: new[] { Style, Overflow, Cancel }, testMobFixture: "martial-player-controls.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "0",
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766",
            ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "481,488", ["NativeCloseableRows"] = 2, ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost) },
        readyMessages: new[]
        {
            "Level 50, 98/100 memory. CSR (17000,495,8100) faces level-1 QAEnemy (17000,495,7900), which starts with Combat Enhancement 173 from QAFar (17000,495,7700). QAFriend (17100,495,8100) is an ally. Stay on the verified outdoor stand.",
            "Select Hacker Style in page 2 slot 1. Confirm Buffer Overflow 2.0 (481) and Cancel Upgrade 2.0 (488) in stock Actions and slots 2–3. On QAFriend, both refusals are reason 24 if sent. At 17 m on QAEnemy, both are reason 34 if sent. No cost, cast or reuse on either refusal.",
            """Far helper request {"actor":"QAEnemy","action":"move","x":17000,"y":495,"z":6400}. Near request {"actor":"QAEnemy","action":"move","x":17000,"y":495,"z":7900}.""",
            """Use /target QAEnemy at 2 m, then slot 2. Save the first complete stock 481 request. Expect 47 IS, 2 s cast, 30 s own reuse, 173 retained and one [MODIFIER] char=6114 source=481 stat=481 value=+150 duration=60s. The existing 104-point local cap reserves the free 94. Send {"actor":"QAFar","action":"buffer-firewall"} to confirm buffer full and no helper debit. After expiry, send the same action for the successful +80/+20/+20/+20 Firewall on QAEnemy.""",
            """Dismiss Firewall on QAEnemy with {"actor":"QAEnemy","action":"chat","text":"?dismissbuff 319"} after the recovery. Slot 3 on QAEnemy. Save the first complete stock 488 request. Expect 50 IS, 4 s cast, 10 s own reuse, one 150-point record for 50 s, [HOSTILE] 488 removes buff 173 from QAEnemy and no HP damage or IS drain. The local cap reserves 104 after removing 173. If 319 is still cooling down, wait for that timer before the firewall probe. The same probe should be denied during 488 and accepted after it terms.""",
            "The disposable bootstrap verifies the full ranked Archive, the exact 173/319 support controls and all stands. It sends neither 481 nor 488 because the full stock request has not been recovered."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Advanced Buffer Overflow needs repository NAV data.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (x, z) in Stands.Select(stand => (stand.X, stand.Z)).Append((X, Z)).Append((X, FarZ)))
            Check.That(NavMesh.Locate(1, x, Y, z, out var floor) && !floor.indoors &&
                Math.Abs(floor.floorY + 95 - Y) < 1 && NavMesh.HasSightPath(1, X, Y, Z, x, Y, z),
                $"Verified advanced buffer NAV and sight ({x},{Y},{z})");
        var server = context.ActualServer ?? throw new InvalidOperationException("Missing advanced buffer fixture server");
        foreach (var stand in Stands)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, stand.Name, stand.Name, stockCharacterLoad: true);
            context.Helpers.Add(helper.Handle, helper);
            await helper.EnterWorldAsync();
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }

        var recipient = context.Helpers["QAEnemy"];
        var support = context.Helpers["QAFar"];
        await recipient.WaitForViewUpdateAsync(2, PvpOpponentsFixture.SelfFlag);
        support.SendRpc(StarterHackerViralSightMode.StockStyle);
        await support.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");
        await HackerBufferOverflowMode.SendEnhancementAsync(support, recipient);
        await StockAbilityProfile.ObserveAsync(recipient, 0, frame => StockAbilityProfile.Bonus(frame, Enhancement, 522, 26, 300, false),
            "QAEnemy begins with the 10-point Combat Enhancement");

        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(Loadout.Sum(AbilityMemory.Cost) == 98 && 98 <= AbilityMemory.ForLevel(Level) &&
            archive.Keys.ToHashSet().SetEquals(Loadout) && Loadout.All(id => AbilityPrerequisites.Current.Closure(id)
                .All(need => archive.TryGetValue(need.Key, out var rank) && rank >= need.Value)),
            "Every ranked Archive requirement for 481 and 488 fits the 98-memory profile");
        await actor.EnterWorldAsync();
        await actor.WaitForViewNamedAsync(recipient.Handle);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        await HackerBufferOverflowMode.SendFirewallAsync(support, recipient);
        await StockAbilityProfile.ObserveAsync(recipient, 0, frame => StockAbilityProfile.Bonus(frame, Firewall, 157, 80, 300, false),
            "The exact Firewall fits on the same recipient before a reservation");
        Check.Equal(0, actor.World.Problems.Count + recipient.World.Problems.Count + support.World.Problems.Count,
            "Advanced Buffer Overflow fixture protocol");
        return Array.Empty<byte>();
    }
}
