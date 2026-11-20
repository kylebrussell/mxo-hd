using System.Text.Json;
using hds;
using HeadlessClient;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class OperativeEngagementRecoveryMode : NativeQaMode
{
    private static readonly ushort[] Passives = { 113, 143, 148 };
    private static readonly (ushort Parent, ushort Child, (ushort Stat, int Value)[] Stats)[] Records =
    {
        (113, 0, new (ushort, int)[] { (1048, 10) }),
        (143, 1060, new (ushort, int)[] { (526, 50) })
    };

    public override string Name => "ability-parity-operative-engagement-recovery";
    public override NativeQaModePlan Plan { get; } = new(
        Profile(2, 17, 113, 143, 148).Order().ToArray(), 17000, 2300,
        Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            CombatFixtureLoadout.SeedWeapons,
            "UPDATE char_abilities SET level=50 WHERE char_id=110",
            "UPDATE characters SET y=495,alignment=1,pvpflag=0,healthC=1000,healthM=1000 WHERE charId=110",
            "UPDATE characters SET x=17120,y=495,z=2300,alignment=2,pvpflag=0,healthC=20000,healthM=20000 WHERE charId=6114",
            "DELETE FROM inventory WHERE charId=6114 AND slot BETWEEN 105 AND 107"
        }, DevCommands: true, hotbarAbilities: Passives, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "6"
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "113,143,148", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            "Page 2 slots 1–3 show the passive IDs 113, 143 and 148. Use Status and the named dev commands to test them. The Handgun is already equipped and the unarmed QAEnemy is 1.2 m away.",
            "Inspect +10 Force Combat Bonus from 113, +50 Sitting Health Regen from 143 and Operative Upgrade in Actions. Set health with ?health 700 and sit. Record the sitting regeneration compared with standing.",
            "With 148 loaded, use radial Fire! on QAEnemy, wait for a shot and holster. At this distance the logged Sharpshooter modifier is 0. Unload 148 and fire again at the same spot: the modifier is -5. Holster before the next step.",
            "Use ?qaunload 143 and ?qaunload 113. Check that the Status records disappear and Operative Upgrade leaves Actions. Repeat the sitting sample. Restore 113, 143 and 148 with ?qaload in that order; expect the records, action and 0 close-range modifier to return."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var target = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(target.Handle, target);
        await target.EnterWorldAsync();
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var server = context.ActualServer!;
        var rows = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        await CombatFixtureLoadout.VerifyWeaponAtLoginAsync(actor, CombatFixtureWeapons.Handgun);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var view = await actor.WaitForViewNamedAsync(target.Handle);
        string handle = WireFormat.ToHex(BitConverter.GetBytes(DuelPackets.Handle(view.ViewId, view.SpawnId)));
        var proof = new List<object>();
        await CheckPassivesAsync(actor, proof, Records, 0, remove: false);
        // The stock Free Attack selection emitted after equipping the logged-in handgun.
        actor.SendRpc("3f 69 00 10 00 00 00");
        await actor.WaitUntilAsync(() => server.DebugLog().Contains("[COMBAT] Free Fire weapon: slot 105, kind 16, goid 2565"),
            "Stock handgun selection", HeadlessPlayer.DefaultTimeout);
        proof.Add(await FireAsync(actor, server, handle, loaded: true, expected: 0));

        int cursor = await ChangeLoadoutAsync(actor, rows, Passives.Reverse().ToArray(), load: false);
        await CheckPassivesAsync(actor, proof, Records, cursor, remove: true);
        proof.Add(await FireAsync(actor, server, handle, loaded: false, expected: -5));

        cursor = await ChangeLoadoutAsync(actor, rows, Passives, load: true);
        await CheckPassivesAsync(actor, proof, Records, cursor, remove: false);
        proof.Add(await FireAsync(actor, server, handle, loaded: true, expected: 0));
        Check.Equal(0, actor.World.Problems.Count + target.World.Problems.Count, "Operative passive fixture framing");
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "operative-engagement-recovery.json"),
            JsonSerializer.Serialize(proof, new JsonSerializerOptions { WriteIndented = true }));
        return Array.Empty<byte>();
    }

    private static async Task<SharpshooterFixture.Input> FireAsync(
        HeadlessPlayer actor, ServerInstance server, string handle, bool loaded, int expected)
    {
        int rpcCursor = actor.World.RpcSnapshot().Count;
        int logCursor = server.DebugLog().Length;
        actor.SendRpc("3d 01");
        actor.SendRpc($"41 {handle}");
        await ObserveAsync(actor, rpcCursor, packet => packet.Bytes.AsSpan().SequenceEqual(new byte[] { 0x4e, 0 }),
            "An ordinary Free Fire shot");
        int stopCursor = server.DebugLog().Length;
        actor.SendRpc("3d 00");
        await actor.WaitUntilAsync(() => server.DebugLog()[stopCursor..].Contains("[COMBAT] Combat stance idle"),
            "Stock holster stops Free Fire before changing the profile", HeadlessPlayer.DefaultTimeout);
        var shots = server.DebugLog()[logCursor..].Split('\n')
            .Select(line => SharpshooterFixture.ReadInput(line.TrimEnd('\r'), actor.Handle))
            .OfType<SharpshooterFixture.Input>().ToArray();
        Check.That(shots.Length > 0 && shots.All(shot => shot.Loaded == loaded && shot.Modifier == expected),
            $"Actual Free Fire uses 148={loaded} and the expected {expected} close-range modifier");
        return shots[0];
    }
}
