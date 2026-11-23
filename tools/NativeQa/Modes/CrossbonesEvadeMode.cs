using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class CrossbonesEvadeMode : NativeQaMode
{
    private const int X = 97619, Y = 695, Z = 10761, ActorX = 97769, ObserverZ = 10911;
    private const int ControlX = 105371, ControlZ = 6654, ControlStandX = 105221;
    private const ushort Level = 7;
    private const string NpcName = "Crossbones Tar";
    private static readonly string ControlTeleport =
        $"?tp {ControlStandX} {Y} {ControlZ} 465 c 8c - {MobAggro.YawTowards(ControlStandX, ControlZ, ControlX, ControlZ):x2}";

    public override string Name => "npc-evade-crossbones";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2, 17 }, ActorX, Z,
        Array.Empty<string>(),
        new[] { "UPDATE char_abilities SET level=LEAST(level,7) WHERE char_id IN (110,6111)" },
        new[]
        {
            Seed(110, ActorX, Z), Seed(6111, X, ObserverZ),
            "DELETE FROM inventory WHERE charId IN (110,6111) AND slot=107"
        }, DevCommands: true, testMobFixture: "npc-evade-crossbones.csv", loadAbilityAncestors: true,
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            ["MXO_MOB_CORPSE_SECONDS"] = "3", ["MXO_MOB_RESPAWN_SECONDS"] = "8"
        }, readyMessages: new[]
        {
            $"At level 7, CSR ({ActorX},{Y},{Z}) faces the captured Crossbones Tar ({X},{Y},{Z}). QAFriend observes from ({X},{Y},{ObserverZ}). Outdoor NAV 15b/4/0. Start with the unarmed stock Close Combat action from the target radial. Save the first full 40 request and the NPC creation's ff shield meter, mode 12 and Evading flag 00080000.",
            "Repeat Close Combat three times on that same live view. Expect 4d reason 14 each time, [NPC EVASION] shield 255->166->77->0 with damage=35, same-view meter updates and no 46 start. The last refusal clears the shield and restores mode 22. Next try opens ordinary close combat. Repeat this stage with the unarmed radial control if a double-click selected another stock action.",
            "Before the ordinary attempt you can use ?weaken 1 for a one-hit kill. Wait for the Tar to respawn after eight seconds. Its new view/spawn has full HP 337 and shield ff again. One Close Combat request now gives 255->166 on the fresh view.",
            $"Travel to the non-evading control with {ControlTeleport}. The L7 Tar at ({ControlX},{Y},{ControlZ}) has the same name and RSI, starts in mode 22 and admits Close Combat on the first request with no shield update."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Crossbones QA requires repository NAV.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (x, z, tx, tz) in new[] { (ActorX, Z, X, Z), (X, ObserverZ, X, Z), (ControlStandX, ControlZ, ControlX, ControlZ) })
        {
            Check.That(NavMesh.Locate(1, x, Y, z, out var stand) && !stand.indoors && stand.floorY + 95 == Y &&
                NavMesh.Locate(1, tx, Y, tz, out var target) && !target.indoors && target.floorY + 95 == Y &&
                NavMesh.HasSightPath(1, x, Y, z, tx, Y, tz), "Crossbones tester and target stands share clear outdoor NAV");
        }

        var observer = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend", stockCharacterLoad: true);
        context.Helpers.Add(observer.Handle, observer);
        await observer.EnterWorldAsync();
        var watchedNpc = await FindAsync(observer, X, Z);
        CheckCreation(watchedNpc, active: true);
        var server = context.ActualServer ?? throw new InvalidOperationException("Crossbones QA requires a disposable server.");
        NativeQaReadiness.RequireNoAmbientAggro(server, observer);
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        await actor.EnterWorldAsync();
        var npc = await FindAsync(actor, X, Z);
        CheckCreation(npc, active: true);
        var self = actor.World.CreationSnapshot().Last(view => view.ViewId == 2);
        Check.Equal((byte)Level, self.Attributes["Level"][0], "Legal level 7 entry profile");
        var profile = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor)).ToArray();
        Check.That(profile.Select(row => AbilityCatalog.IdOf(row.Descriptor)).ToHashSet().SetEquals(new ushort[] { 2, 17 }) &&
            profile.All(row => row.Level is > 0 and <= Level), "Awakened and Self Defense fit the level 7 archive");
        foreach (byte meter in new byte[] { 166, 77, 0 })
        {
            string handle = Handle(npc);
            actor.SkipReceived();
            actor.SendRpc($"40 {handle} 01 6b"); // Retained stock Close Combat layout, rebound to the live handle.
            await actor.WaitForRpcAsync($"4d 0e 00 01 00 {handle}");
            string update = meter == 0
                ? "04 80 80 80 80 90 00 80 06 00 00 00 00 22"
                : $"04 80 80 80 80 10 {meter:x2}";
            await actor.WaitForViewUpdateAsync(npc.ViewId, update);
            await observer.WaitForViewUpdateAsync(watchedNpc.ViewId, update);
            Check.That(!actor.World.RpcSnapshot().Any(record => EnrageNpcCloseCombatFixture.CombatFlag(record, npc, 1)),
                "The absorb attempt never opens a combat view");
        }

        int woundStart = server.DebugLog().Length;
        actor.SendChat("?weaken 1");
        await actor.WaitUntilAsync(() => server.DebugLog()[woundStart..].Contains("[DEV] Weakened 1 NPCs to 1 health", StringComparison.Ordinal),
            "Only the original target is wounded for respawn", HeadlessPlayer.DefaultTimeout);
        await OpenAsync(actor, npc);
        var respawn = await FindAsync(actor, X, Z, npc.SpawnId);
        CheckCreation(respawn, active: true);
        actor.SkipReceived();
        actor.SendRpc($"40 {Handle(respawn)} 01 6b");
        await actor.WaitForRpcAsync($"4d 0e 00 01 00 {Handle(respawn)}");
        await actor.WaitForViewUpdateAsync(respawn.ViewId, "04 80 80 80 80 10 a6");

        actor.SendChat(ControlTeleport);
        var control = await FindAsync(actor, ControlX, ControlZ);
        CheckCreation(control, active: false);
        await OpenAsync(actor, control);
        Check.Equal(0, actor.World.Problems.Count + observer.World.Problems.Count, "NPC shield replay framing");
        Console.WriteLine("CROSSBONES EVADE NATIVE BOOTSTRAP PASS: fortified placement absorbs three entries, respawns with a fresh shield, and the ordinary placement opens normally.");
        return Array.Empty<byte>();
    }

    private static string Seed(int id, int x, int z) =>
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={x},y={Y},z={z}," +
        $"rotation={MobAggro.YawTowards(x, z, X, Z)},healthC=750,healthM=750 WHERE charId={id}";

    private static string Handle(ViewCreation view) => WireFormat.ToHex(BitConverter.GetBytes(DuelPackets.Handle(view.ViewId, view.SpawnId)));

    private static async Task OpenAsync(HeadlessPlayer actor, ViewCreation view)
    {
        actor.SkipReceived();
        actor.SendRpc($"40 {Handle(view)} 01 6b");
        await actor.WaitForRpcAsync($"46 {Handle(view)} 01");
        await actor.WaitForRpcAsync("45");
    }

    private static async Task<ViewCreation> FindAsync(HeadlessPlayer player, int x, int z, ushort? previousSpawn = null)
    {
        ViewCreation? result = null;
        await player.WaitUntilAsync(() => (result = player.World.CreationSnapshot().LastOrDefault(view =>
            view.Goid == 599 && view.Name == NpcName && view.SpawnId != previousSpawn &&
            EngageFoesFixture.Position(view).SequenceEqual(new double[] { x, Y, z }))) != null,
            $"Crossbones Tar at ({x},{Y},{z}) with a live spawn", TimeSpan.FromSeconds(22));
        return result!;
    }

    private static void CheckCreation(ViewCreation view, bool active)
    {
        Check.Equal((byte)(active ? 0x12 : 0x22), view.Attributes["CombatantMode"][0], "Crossbones combat mode");
        Check.Equal((ushort)337, BitConverter.ToUInt16(view.Attributes["Health"]), "Live Crossbones HP");
        Check.Equal(active, view.Attributes.ContainsKey("EvadeShieldHealth"), "Shield is specific to the captured placement");
        if (active)
        {
            Check.Equal(byte.MaxValue, view.Attributes["EvadeShieldHealth"][0], "Full initial shield meter");
            Check.Equal(0x4023d70au, BitConverter.ToUInt32(view.Attributes["EvadeShieldDamageScale"]), "Archived meter scale");
            Check.Equal(EvasionShield.StateFlag, BitConverter.ToUInt32(view.Attributes["ConditionStateFlags"]), "Evading condition");
        }
    }
}
