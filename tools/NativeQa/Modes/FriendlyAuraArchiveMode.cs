using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class FriendlyAuraArchiveMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 2300;
    private const ushort CombatAura = 42, DeflectVirus = 378;
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, CombatAura, DeflectVirus).Order().ToArray();

    public override string Name => "friendly-aura-archive";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(),
        new[] { "UPDATE characters SET y=495,rotation=128,innerStrC=500,innerStrM=1000 WHERE charId=110" },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        hotbarAbilities: new[] { CombatAura, DeflectVirus }, allowManualHotbarSetup: true,
        readyMessages: new[] {
            "CSR starts at Mara Central hardline 152 (17000,495,2300), facing the booth. Page 2 slots 1/2 are Combat Aura 1.0 and Deflect Virus. Both are loaded with prerequisites. No teleport, helper, selected target or dev command.",
            "Turn on slot 1. After the first 695 child add, use hardline right-click > Abilities and unload Combat Aura. Expect 80 b3 2a 00, parent 42 off, then the most recent 695 child records expire at their original 5-second deadline. Wait at least 6 seconds. Expect no further pulse or upkeep debit. Reload it and close the panel. It stays off.",
            "Turn on slot 2, wait for its 2-second cast and the first 693 child add. Use the same hardline panel to unload Deflect Virus while on. Expect 80 b3 7a 01, linked parent 378 removals and exclusive off. The latest 693 child expires on schedule. Wait at least 6 seconds, confirm no later pulse or upkeep, then reload. It stays off."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string data = Path.Combine(context.RepositoryRoot!, "data");
        NavMesh.UseDirectory(Path.Combine(data, "navmesh"));
        HardlineNetwork.Load(Path.Combine(data, "hardline_network.csv"));
        var booth = HardlineNetwork.Get(1, 152);
        Check.That(booth != null && booth.spotX == X && booth.spotY == Y && booth.spotZ == Z &&
            NavMesh.Locate(1, X, Y, Z, out _), "CSR faces Mara Central from its verified stand");
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(50), "The ability profile fits memory");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        DisablingShotMode.CheckProfile(actor, Loadout, 50);
        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        int start = actor.World.RpcSnapshot().Count;
        actor.SendRpc("80b92a00000000000000000000000000000000000000000000000000d557377c20000000086b0000000000");
        await actor.WaitForRpcAsync("80 bd 05 2a 00 00 00 00 00 00 01");
        await StockAbilityProfile.ObserveAsync(actor, start, packet => Child(packet, remove: false), "Combat Aura first child");
        int unloaded = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { CombatAura }, load: false);
        await actor.WaitForRpcAsync("80 bd 05 2a 00 00 00 00 00 00 00");
        await StockAbilityProfile.ObserveAsync(actor, unloaded, packet => Child(packet, remove: true), "The pending child expires");
        Check.That(!actor.World.RpcSnapshot().Skip(unloaded).Any(packet => Child(packet, remove: false)), "Unload stops refreshes");
        await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { CombatAura }, load: true);
        return Array.Empty<byte>();

        static bool Child(RpcRecord packet, bool remove) =>
            ManageBonusRecord.TryDecode(packet.Bytes, out var row) && row!.SourceId == 695 && row.TargetId == 522 &&
            row.Duration == 5 && row.AddRemoveType == (remove ? 1 : 0);
    }
}
