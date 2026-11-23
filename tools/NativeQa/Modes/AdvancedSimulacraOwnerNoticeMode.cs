using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static HeadlessClient.AdvancedSimulacraNoticeFixture;

namespace NativeQa.Modes;

public class AdvancedSimulacraOwnerNoticeMode : NativeQaMode
{
    private static readonly int Memory = Abilities.Sum(AbilityMemory.Cost);
    private static readonly ushort[] Hotbar = { 1008, Proxy, Nuker, Healer };
    private const string Rifle = "Rifle";
    private readonly bool useProxyOverride;

    public AdvancedSimulacraOwnerNoticeMode() : this(useProxyOverride: false) { }

    protected AdvancedSimulacraOwnerNoticeMode(bool useProxyOverride)
    {
        this.useProxyOverride = useProxyOverride;
        Plan = BuildPlan(useProxyOverride);
    }

    public override string Name => useProxyOverride
        ? "ability-parity-remote-proxy-sheet"
        : "ability-parity-advanced-simulacra-owner-notice";
    public override NativeQaModePlan Plan { get; }

    private static NativeQaModePlan BuildPlan(bool useProxyOverride) => new(
        Abilities, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" },
        new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z}," +
            $"rotation={MobAggro.YawTowards(X,Z,X+150,Z)},innerStrC=600,innerStrM=1000 WHERE charId=110",
            $"UPDATE characters SET x={ObserverX},y={Y},z={Z} WHERE charId=6111"
        },
        DevCommands: true, hotbarAbilities: useProxyOverride ? new ushort[] { 1008, Proxy } : Hotbar, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1",
            [WorldConfig.RemoteProxySheetVariable] = useProxyOverride ? $"Simulacra.{Rifle}" : ""
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = useProxyOverride ? "594" : "594,616,617", ["NativeCloseableRows"] = 1, ["MemoryUsed"] = Memory },
        readyMessages: new[] { Stand }.Concat(useProxyOverride ? new[]
        {
            $"Server setting {WorldConfig.RemoteProxySheetVariable}=Simulacra.{Rifle}. Choose Coder Style on page 2 slot 1. Clear selection and press slot 2 for Remote Proxy 5.0. After 8 s expect level 44 Rifle Simulacrum on both views, one 81 76 and one private 'You summoned a level 44 Rifle Simulacrum.' Expect 120 IS (600->480) and 8 s own reuse.",
            "Type /ordersimulacra All Deactivate. Expect stock 81 7a, one 81 77 for the current pet, window closes and the Rifle body disappears. Once reuse ends, press slot 2 again, confirm a fresh Rifle with a different ID, one notice and another 120 IS (480->360). Deactivate it and confirm its own single removal."
        } : new[]
        {
            "Choose Coder Style on page 2 slot 1. Press slot 2 for Remote Proxy 5.0. After 8 s expect a level-44 proxy from the six-sheet stock pool, one 81 76 window, [ABILITY-COST] paid=120 IS=600->480 and 8 s own reuse. One visible owner notice reads 'You summoned a level 44 <sheet> Simulacrum.' Check the System tab if needed.",
            "Use slot 3 for Logic Daemon 4.0. Retain the complete first stock 616 request. After 8 s expect the old 81 77, one new 81 76, level-44 Nuker, [ABILITY-COST] paid=150 IS=480->330 and 10 s own reuse. The owner sees one 'You summoned a level 44 Nuker Simulacrum.'",
            "Press slot 4 for Patch Daemon 3.0 (617). Retain its complete first stock request. After 8 s expect only the old Nuker 81 77 and one Healer 81 76, level 44 with 1725 max HP, [ABILITY-COST] paid=170 IS=330->160 and 10 s own reuse. Expect exactly one 'You summoned a level 44 Healer Simulacrum.'",
            """Type ?health 100. Wound QAFriend with helper {"actor":"QAFriend","action":"chat","text":"?health 100"}. Verify two [VITALS] char=110 pet=<new ID> ability=617 HP increases around 3 s apart, with no 617 heal on QAFriend. Type /ordersimulacra All Deactivate, verify one 81 77 and the Healer body gone, then confirm no later 617 heal. Projected DONE +1 (617) after stock proof.""",
            "The notice uses the existing private stock-visible system channel and server wording. This mode uses the default Proxy pool. The original localized string ID remains open."
        }).ToArray());

    private static string Stand =>
        $"Level {Level}, legal ranked profile {Memory}/{AbilityMemory.ForLevel(Level)} memory. Verified Mara NAV CSR ({X},{Y},{Z}) faces pet stand ({X+150},{Y},{Z}); observer QAFriend ({ObserverX},{Y},{Z}). No world object selection needed.";

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Advanced summons require repository NAV.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int x in new[] { X, X + 150, ObserverX })
            Check.That(NavMesh.Locate(1, x, Y, Z, out var stand) && !stand.indoors && Math.Abs(stand.floorY + 95 - Y) < 1 &&
                NavMesh.HasSightPath(1, X, Y, Z, x, Y, Z), $"Visible outdoor NAV stand ({x},{Y},{Z})");
        var observer = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend", stockCharacterLoad: true);
        context.Helpers.Add(observer.Handle, observer);
        await observer.EnterWorldAsync();
        var server = context.ActualServer!;
        NativeQaReadiness.RequireNoAmbientAggro(server, observer);
        if (context.StockProbe is not { } owner) return Array.Empty<byte>();

        var archive = AbilityMemoryUiFixture.Decode(owner.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(Memory <= AbilityMemory.ForLevel(Level) && archive.Keys.ToHashSet().SetEquals(Abilities) &&
            Abilities.All(id => AbilityPrerequisites.Current.Closure(id).All(need =>
                archive.TryGetValue(need.Key, out var row) && row.Level >= need.Value)), "Full ranked summon profile");
        await owner.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, owner);
        owner.SendRpc(CoderStyle);
        await owner.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");

        byte first = await SummonAndDismissAsync(480);
        if (useProxyOverride)
        {
            await Task.Delay(TimeSpan.FromSeconds(8.25));
            Check.That(first != await SummonAndDismissAsync(360), "Resummon creates a different window and live view");
        }

        Check.Equal(0, owner.World.Problems.Count + observer.World.Problems.Count, "Advanced summon views and notices decode cleanly");
        Console.WriteLine(useProxyOverride
            ? "REMOTE PROXY OVERRIDE PASS: both stock summons select the configured Rifle and dismiss the exact owned windows and views."
            : "ADVANCED SUMMON NOTICE NATIVE BOOTSTRAP PASS: retained Coder/594/Deactivate frames, one private owner notice. Stock 616 and 617 requests are native steps.");
        return Array.Empty<byte>();

        async Task<byte> SummonAndDismissAsync(ushort expectedIs)
        {
            int rpcAt = owner.World.RpcSnapshot().Count;
            int viewsAt = owner.World.ViewSnapshot().Count;
            owner.SkipReceived();
            owner.SendRpc(Proxy5);
            await EvidenceRetestWire.CastBarAsync(owner, Proxy, 8);
            var window = await StockAbilityProfile.ObserveAsync(owner, rpcAt, frame => frame.Opcode == 0x176, "One new Proxy window");
            byte id = window.Bytes[3];
            var pet = await PetViewAsync(owner, owner.CharId, id);
            var watchedPet = await PetViewAsync(observer, owner.CharId, id);
            Check.Equal((byte)Level, pet.Attributes["Level"][0], "The 594 subject-level clamp");
            Check.Equal(pet.Name, watchedPet.Name, "Both players see the same Proxy");
            Check.That(useProxyOverride ? pet.Name == $"{Rifle} Simulacrum" :
                CoderPets.ProxySheets.Any(sheet => pet.Name == $"{sheet} Simulacrum"), "The override or authored default pool selects the Proxy");
            if (useProxyOverride)
                Check.Equal(CoderPets.WeaponOf(CoderPets.Kind.Proxy, Rifle), BitConverter.ToUInt32(watchedPet.Attributes["EquippedItemID"]),
                    "The selected sheet also equips the Rifle role's item");

            string notice = $"You summoned a level {Level} {pet.Name}.";
            await StockAbilityProfile.ObserveAsync(owner, rpcAt, frame => IsNotice(frame) &&
                System.Text.Encoding.ASCII.GetString(frame.Bytes).Contains(notice, StringComparison.Ordinal), "One new owner notice");
            Check.Equal(1, owner.World.RpcSnapshot().Skip(rpcAt).Count(IsNotice), "A successful summon reports one owner notice");
            Check.That(!observer.World.RpcSnapshot().Any(IsNotice), "The stock-visible private notice only reaches its owner");
            await EvidenceRetestWire.TimerAsync(owner, rpcAt, Proxy, AbilityCooldowns.OwnTimer, 8);
            await owner.WaitUntilAsync(() => owner.World.ViewSnapshot().Skip(viewsAt)
                .SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates).Contains(expectedIs),
                $"594 leaves {expectedIs} IS", HeadlessPlayer.DefaultTimeout);
            Check.Equal(1, owner.World.RpcSnapshot().Skip(rpcAt).Count(frame => frame.Opcode == 0x176),
                "A successful summon registers one owner window");

            int effectsAt = owner.World.RpcSnapshot().Count;
            int deletionsAt = owner.World.ViewSnapshot().Count;
            int watchedDeletionsAt = observer.World.ViewSnapshot().Count;
            owner.SendRpc(Deactivate);
            byte[] removal = { 0x81, 0x77, id };
            await StockAbilityProfile.ObserveAsync(owner, effectsAt, frame => frame.Bytes.AsSpan().SequenceEqual(removal), "Current Proxy window removed");
            await WaitForDeletionAsync(owner, deletionsAt, pet);
            await WaitForDeletionAsync(observer, watchedDeletionsAt, watchedPet);
            Check.Equal(1, owner.World.RpcSnapshot().Skip(effectsAt).Count(frame => frame.Bytes.AsSpan().SequenceEqual(removal)),
                "The current Proxy window is removed once");
            return id;
        }

        static Task WaitForDeletionAsync(HeadlessPlayer watcher, int cursor, ViewCreation pet)
        {
            byte[] deletion = WireFormat.FromHex($"01 00 01 01 00 {StockAbilityProfile.LittleEndianHex(pet.ViewId)}");
            return watcher.WaitUntilAsync(() => watcher.World.ViewSnapshot().Skip(cursor).Any(list => list.AsSpan().IndexOf(deletion) >= 0),
                $"Dismissed Proxy view {pet.ViewId} is deleted for {watcher.Handle}", HeadlessPlayer.DefaultTimeout);
        }
    }
}

public sealed class RemoteProxySheetMode : AdvancedSimulacraOwnerNoticeMode
{
    public RemoteProxySheetMode() : base(useProxyOverride: true) { }
}
