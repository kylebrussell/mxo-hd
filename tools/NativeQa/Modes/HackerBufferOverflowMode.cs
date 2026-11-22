using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class HackerBufferOverflowMode : NativeQaMode
{
    private const ushort Overflow = 395, Enhancement = 173, Firewall = 319, Style = CombatStyles.HackerStyle;
    private const int Level = 22, SupportLevel = 30, X = 17000, Y = 495, Z = 8100, TargetZ = 7900, FarZ = 6600;
    private static readonly ushort[] Loadout = new ushort[] { 2, 17, Style, Overflow }
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();
    private static readonly ushort[] SupportLoadout = StockAbilityProfile.Profile(2, 17, Style, Enhancement, Firewall).Order().ToArray();
    // Retained stock PR773 Combat Enhancement and PR831 Personal Firewall 2.0. Only view and spawn are rebound.
    private const string StockEnhancement = "80b9ad000400020000000000000000000000000000000000000000001060bf0001000000086b0000000000";
    private const string StockFirewall = "80b93f010400020000000000000000000000000000000000000000001060c30001000000086b0000000000";
    private static readonly (string Name, int Id, int X, int Z, int Team, int Level)[] Stands =
    {
        ("QAEnemy", 6114, X, TargetZ, 2, 1),
        ("QAFriend", 6111, X + 100, Z, 1, Level),
        ("QAFar", 6112, X, TargetZ - 200, 2, SupportLevel)
    };

    public override string Name => "ability-parity-hacker-buffer-overflow";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" }, new[]
        {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET x={X},y={Y},z={Z},level={Level},exp={PlayerProgression.ExperienceForLevel(Level)}," +
                $"rotation={MobAggro.YawTowards(X,Z,X,TargetZ)},alignment=1,innerStrC=500,innerStrM=500 WHERE charId=110",
            "DELETE FROM char_abilities WHERE char_id IN (6111,6112,6114)",
            "INSERT INTO char_abilities (char_id,slot,ability_id,level,is_loaded) VALUES " +
                string.Join(",", SupportLoadout.Select((id, slot) => $"(6112,{400 + slot},{AbilityMessages.AbilityGameObject(id)},{SupportLevel},1)"))
        }.Concat(Stands.Select(stand => $"UPDATE characters SET x={stand.X},y={Y},z={stand.Z},alignment={stand.Team}," +
            $"level={stand.Level},exp={PlayerProgression.ExperienceForLevel(stand.Level)} WHERE charId={stand.Id}")).ToArray(),
        DevCommands: true, hotbarAbilities: new[] { Style, Overflow }, testMobFixture: "martial-player-controls.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "0",
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766",
            ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "395", ["NativeCloseableRows"] = 1 },
        readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. CSR ({X},{Y},{Z}) faces QAEnemy ({X},{Y},{TargetZ}). QAFriend ({X + 100},{Y},{Z}) is allied. QAFar ({X},{Y},{TargetZ - 200}) supports QAEnemy. Verified outdoor NAV with sight.",
            "Choose Hacker Style in slot 1. Press Buffer Overflow in slot 2 with no target and then with QAFriend selected. If sent, both get reason 24 and no cost, cast or timer. Move QAEnemy to the 15 m stand, attempt again for reason 34, and return it to the near stand.",
            $$"""Far request: {"actor":"QAEnemy","action":"move","x":{{X}},"y":{{Y}},"z":{{FarZ}}}. Near request: {"actor":"QAEnemy","action":"move","x":{{X}},"y":{{Y}},"z":{{TargetZ}}}.""",
            "Back at the near stand, /target QAEnemy and cast slot 2. Save the first full stock request. Expect 38 IS, 2 s cast, 30 s own reuse and one 80-point reservation for 60 s. Combat Enhancement stays active on QAEnemy.",
            "While Overflow is active, submit the helper firewall request. QAFar gets 'The upgrade buffer is full.' QAEnemy gets no 319 modifier, and QAFar pays no IS and starts no reuse. After 395 expires send the same helper request again. Expect +80 viral, +20/+20/+20, and 13 s own reuse on the helper. No target HP damage or interlock rounds from 395.",
            "Helper firewall request: {\"actor\":\"QAFar\",\"action\":\"buffer-firewall\"}."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Buffer Overflow needs repository NAV data.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (x, z) in Stands.Select(stand => (stand.X, stand.Z)).Append((X, Z)).Append((X, FarZ)))
            Check.That(NavMesh.Locate(1, x, Y, z, out var floor) && !floor.indoors &&
                Math.Abs(floor.floorY + 95 - Y) < 1 && NavMesh.HasSightPath(1, X, Y, Z, x, Y, z),
                $"Verified Buffer Overflow NAV and sight ({x},{Y},{z})");
        var server = context.ActualServer!;
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
        await SendSupportAsync(support, recipient, StockEnhancement);
        await StockAbilityProfile.ObserveAsync(recipient, 0, frame => StockAbilityProfile.Bonus(frame, Enhancement, 522, 26, 300, false),
            "QAEnemy has the normal 10-point buffer use from Combat Enhancement");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) && Loadout.All(id => archive.ContainsKey(id) &&
            AbilityPrerequisites.Current.Closure(id).All(need => archive.TryGetValue(need.Key, out var rank) && rank >= need.Value)),
            "The full Buffer Overflow prerequisite profile fits level 22");
        await actor.EnterWorldAsync();
        await actor.WaitForViewNamedAsync(recipient.Handle);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        await SendFirewallAsync(support, recipient);
        await StockAbilityProfile.ObserveAsync(recipient, 0, frame => StockAbilityProfile.Bonus(frame, Firewall, 157, 80, 300, false),
            "The exact helper firewall fits normally before any Buffer Overflow reservation");
        Check.Equal(0, actor.World.Problems.Count + recipient.World.Problems.Count + support.World.Problems.Count, "Buffer Overflow fixture protocol");
        return Array.Empty<byte>();
    }

    internal static Task SendFirewallAsync(HeadlessPlayer support, HeadlessPlayer recipient)
    {
        Check.That(support.Handle == "QAFar" && recipient.Handle == "QAEnemy", "The firewall control is QAFar → QAEnemy only");
        return SendSupportAsync(support, recipient, StockFirewall);
    }

    private static async Task SendSupportAsync(HeadlessPlayer support, HeadlessPlayer recipient, string request)
    {
        var view = await support.WaitForViewNamedAsync(recipient.Handle);
        support.SendRpc(StarterHackerControlTargetMode.WithHandle(request, DuelPackets.Handle(view.ViewId, view.SpawnId)));
    }
}
