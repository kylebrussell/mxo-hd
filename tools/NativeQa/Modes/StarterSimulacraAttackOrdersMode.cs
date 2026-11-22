using System.Text;
using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterSimulacraAttackOrdersMode : NativeQaMode
{
    private const int Level = 18, X = 17440, Y = 495, Z = 2190, TargetX = 17900, TargetZ = 1900;
    private const ushort Coder = 1008, Proxy = 27, RifleProxy = 28, Daemon = 22;
    private const string TargetName = "QA Pet Target";
    private const string Refusal = "[PET] Order refused: Choose a hostile NPC within your simulacrum's leash.";
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Coder, Proxy, RifleProxy, Daemon).Order().ToArray();
    private static readonly int Memory = Loadout.Sum(AbilityMemory.Cost);
    // Complete stock summons from PR593 (27 and 22) and PR603 (28).
    private static readonly (ushort Id, string Wire, byte Level, ushort Hp, int Reuse, int IsAfter)[] Summons =
    {
        (Proxy, "80b91b00000000000000000000000000000000000000000000000000585cb40001000000086b0000000000", 4, 224, 8, 250),
        (RifleProxy, "80b91c00000000000000000000000000000000000000000000000000585ca80001000000086b0000000000", 7, 337, 8, 200),
        (Daemon, "80b916000000000000000000000000000000000000000000000000000090767241086b0000000000", 7, 337, 10, 150)
    };

    public override string Name => "ability-parity-starter-simulacra-attack-orders";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" },
        new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z},rotation={MobAggro.YawTowards(X,Z,TargetX,TargetZ)}," +
                "innerStrC=300,innerStrM=1000 WHERE charId=110"
        }, DevCommands: true, hotbarAbilities: new[] { Coder, Proxy, RifleProxy, Daemon },
        testMobFixture: "starter-pet-consumers.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1",
            [CoderPets.RifleProxyQa] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "22,27,28", ["NativeCloseableRows"] = 0, ["MemoryUsed"] = Memory },
        readyMessages: new[]
        {
            $"CSR ({X},{Y},{Z}) faces QA Pet Target ({TargetX},{Y},{TargetZ}) 5.44 m away. The Proxy/Nuker spawn at ({X + 150},{Y},{Z}), off the target's centerline. All three stands are on clear outdoor NAV floor 400 away from WinterFrost. Level {Level}, {Memory}/{AbilityMemory.ForLevel(Level)} memory with all prerequisites. Page 2 is Coder Style/27/28/22. No bag or helpers.",
            "Use slots 1 and 2 with no selection. Expect one Simulacra window, L4/224 HP, 8 s cast and own reuse, IS 300 to 250. Type ?pet passive and ?pet stay as setup. Clear the target on empty ground. Click Attack in the Simulacra window once. Then select your own pet and try Attack once. The client may suppress those requests. If it sends 81 7b, expect [PET] Order refused: Choose a hostile NPC within your simulacrum's leash. In both cases there must be no hit or fallback to QA Pet Target.",
            "Single-click the humanoid QA Pet Target and confirm the name in the target panel. In the Simulacra window click Attack (SAtk). If no 81 7b is sent, try typing /ordersimulacra All Attack and press Enter. If neither sends it, record the selected name, control tooltip and whether the request was suppressed. Use the stock control only for this check.",
            "Save the first complete stock 81 7b hex, plus the preceding 81 51 selection or matching view creation with view and spawn. Expect [PET] Order 17b id 254 command Attack target <full field>, followed by [PET] <pet> hits QA Pet Target for N. Damage varies. Click Stay (81 7a fe 00) and check no further hit after that order. If the control is inaccessible, ?pet stay can stop the pet for the next stage.",
            "Clear selection and repeat the positive Attack and Stay on slots 3 and 4. Each removes only the old window, then shows a new L7/337 HP pet (Rifle and Nuker). Both casts take 8 s. Own reuse is 8 s for 28 and 10 s for 22. IS 250 to 200 to 150. No owner [PVP] rounds or max [VITALS] changes. This batch verifies Attack orders. No row is projected to move to DONE."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Pet orders need repository NAV data.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (x, z) in new[] { (X, Z), (X + 150, Z), (TargetX, TargetZ) })
            Check.That(NavMesh.Locate(1, x, Y, z, out var at) && !at.indoors && Math.Abs(at.floorY + 95 - Y) < 1,
                $"Pet order stand ({x},{Y},{z}) is on outdoor NAV");
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, TargetX, Y, TargetZ) &&
            NavMesh.HasSightPath(1, X + 150, Y, Z, TargetX, Y, TargetZ), "CSR and the pet both see the target");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var server = context.ActualServer!;
        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(Memory <= AbilityMemory.ForLevel(Level) && archive.Keys.ToHashSet().SetEquals(Loadout) &&
            Loadout.All(id => AbilityPrerequisites.Current.Closure(id).All(need =>
                archive.TryGetValue(need.Key, out ushort rank) && rank >= need.Value)), "The full ranked pet order profile fits");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var target = await actor.WaitForViewNamedAsync(TargetName);
        Check.That(EngageFoesFixture.Position(target).SequenceEqual(new double[] { TargetX, Y, TargetZ }),
            "The selectable NPC is alive on its planned stand");
        actor.SendRpc(StarterCoderPetBuffsAndDetonateMode.CoderStock);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");
        string[] orders = new string[Summons.Length];
        byte previousId = 0;
        for (int i = 0; i < Summons.Length; i++)
        {
            var summon = Summons[i];
            Select(0);
            int cursor = actor.World.RpcSnapshot().Count;
            int views = actor.World.ViewSnapshot().Count;
            actor.SkipReceived();
            actor.SendRpc(summon.Wire);
            await EvidenceRetestWire.CastBarAsync(actor, summon.Id, 8);
            await EvidenceRetestWire.TimerAsync(actor, cursor, summon.Id, AbilityCooldowns.OwnTimer, summon.Reuse);
            var window = await StockAbilityProfile.ObserveAsync(actor, cursor, packet => packet.Opcode == 0x176, "Fresh pet window");
            byte id = window.Bytes[3];
            if (previousId != 0)
            {
                Check.That(previousId != id, "Pet replacement gets a fresh window");
                await StockAbilityProfile.ObserveAsync(actor, cursor, packet => packet.Bytes is [0x81, 0x77, var removed] &&
                    removed == previousId, "Only the previous window was removed");
            }
            previousId = id;
            ViewCreation? pet = null;
            await actor.WaitUntilAsync(() => (pet = actor.World.CreationSnapshot().LastOrDefault(view =>
                view.Goid == CoderPets.SimulacraGoId && view.Attributes["SimulacraID"][0] == id)) != null,
                "Visible new pet", HeadlessPlayer.DefaultTimeout);
            Check.That(pet!.Attributes["Level"][0] == summon.Level &&
                BitConverter.ToUInt16(pet.Attributes["MaxHealth"]) == summon.Hp, "Pet level and max HP remain at the shipped fixture values");
            await actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(views).SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates)
                .Any(value => value == summon.IsAfter), $"{summon.Id} leaves {summon.IsAfter} IS", HeadlessPlayer.DefaultTimeout);
            actor.SendRpc("81 7c fe 05 02");
            actor.SendRpc("81 7a fe 00");
            await StatusAsync(", Stay, Passive", "fighting ");
            string hit = $"[PET] {pet.Name} hits {TargetName} for ";
            if (i == 0)
            {
                int noTargetAt = server.DebugLog().Length;
                await RefuseAsync(0);
                await RefuseAsync(Handle(pet));
                await StatusAsync(", Stay, Passive", "fighting ");
                Check.That(!server.DebugLog()[noTargetAt..].Contains(hit), "Missing and own-pet orders do not start a fallback fight");
            }

            uint handle = Handle(target);
            Select(handle);
            // The older replay zero-extends the live view. Also pass the full selected-object field through dispatch.
            string order = Attack(i == 0 ? target.ViewId : handle);
            orders[i] = order;
            int attackAt = server.DebugLog().Length;
            actor.SendRpc(order);
            await StatusAsync($", fighting {TargetName}");
            if (i == 0)
            {
                await RefuseAsync(Handle(pet));
                await StatusAsync($", fighting {TargetName}");
            }
            await actor.WaitUntilAsync(() => server.DebugLog()[attackAt..].Contains(hit),
                $"{pet.Name} hits the selected NPC", TimeSpan.FromSeconds(22));
            actor.SendRpc("81 7a fe 00");
            await StatusAsync(", Stay, Passive", "fighting ");
        }
        Check.That(!server.DebugLog().Contains("[PVP] Round"), "The owner did not enter an interlock");
        Check.Equal(0, actor.World.Problems.Count, "Pet order replay framing");
        await File.WriteAllLinesAsync(Path.Combine(context.OutputDirectory, "starter-simulacra-attack-orders.txt"), orders);
        Console.WriteLine("SIMULACRA ATTACK ORDERS PASS: selectionless and own-pet refusals, live NPC hits for 27/28/22, and Stay.");
        return Array.Empty<byte>();

        void Select(uint handle) => actor.SendRpc("81 51 " + Hex(handle));

        async Task RefuseAsync(uint handle)
        {
            Select(handle);
            int cursor = server.DebugLog().Length;
            actor.SendRpc(Attack(handle));
            await actor.WaitUntilAsync(() => server.DebugLog()[cursor..].Contains(Refusal), "Invalid explicit target is refused",
                HeadlessPlayer.DefaultTimeout);
        }

        async Task StatusAsync(string required, string? forbidden = null)
        {
            actor.SkipReceived();
            actor.SendChat("?pet");
            var status = await actor.WaitForTextAsync(required);
            Check.That(forbidden == null || !Encoding.ASCII.GetString(status.Bytes).Contains(forbidden),
                "The pet did not start a fight or retain its old target");
        }
    }

    private static uint Handle(ViewCreation view) => DuelPackets.Handle(view.ViewId, view.SpawnId);
    private static string Hex(uint field) => WireFormat.ToHex(BitConverter.GetBytes(field));
    private static string Attack(uint field) => "81 7b fe 02 " + Hex(field);
}
