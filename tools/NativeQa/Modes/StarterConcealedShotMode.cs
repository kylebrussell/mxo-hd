using System.Text.Json;
using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class StarterConcealedShotMode : NativeQaMode
{
    private const int Level = 19;
    private const ushort Rifles = 147, Concealment = 503, ConcealedShot = 504;
    // PR326 PacketLog 05837386fab5:5004, stock selected target and out-of-combat Rifle opener.
    private const string RifleOpener = "80b9fe00050003000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
    private static readonly ushort[] Roots = { 2, 17, Rifles, Concealment, ConcealedShot };
    private static readonly ushort[] Loadout = Roots.Concat(Roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
        .Distinct().Order().ToArray();

    public override string Name => "ability-parity-starter-concealed-shot";
    public override NativeQaModePlan Plan { get; } = new(Loadout, 24800, -1498,
        Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            CombatFixtureLoadout.SeedWeaponsFor(CombatFixtureWeapons.Rifle),
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y=495,healthC=1000,healthM=1000,innerStrC=200,innerStrM=200,alignment=1 WHERE charId=110",
            $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110",
            $"UPDATE characters SET x=25200,y=495,z=-1498,rotation=64,alignment=2,level=10,exp={PlayerProgression.ExperienceForLevel(10)},pvpflag=0,healthC=1000,healthM=1000 WHERE charId=6114",
            "DELETE FROM char_abilities WHERE char_id=6114 AND ability_id NOT IN (2147485696,2147501056); UPDATE char_abilities SET level=1 WHERE char_id=6114"
        }, DevCommands: true, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "6"
        },
        readyMessages: new[]
        {
            "Level 19 with the ordinary 42/49 memory chain. The rifle starts in hand. Select Rifles (147) and activate Concealment, granted by loaded ability 503. QAEnemy stands 4 m east facing away.",
            "While concealed and outside interlock, target QAEnemy and use Concealed Shot (504) from Actions. Save the full outgoing request, six-second bar, landed damage and 45-second reuse. Expected landed 23 from ballistic base 22 and the existing style/passive influence. A single cast may or may not produce the separate stun.",
            "If the granted Concealment action is unavailable in the stock UI, record that and use ?stealth conceal as a fixture aid for the shot only."
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "504", ["NativeCloseableRows"] = 0 });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(enemy.Handle, enemy);
        await enemy.EnterWorldAsync();
        if (context.Server == null) return Array.Empty<byte>();

        var server = context.ActualServer ?? throw new InvalidOperationException("Expected a disposable server.");
        using var caster = await HeadlessPlayer.LoginAsync(context.Address, "CSR_AbilityQA", "CSR_Tester", stockCharacterLoad: true);
        var archive = AbilityMemoryUiFixture.Decode(caster.CharacterData[6]).ToDictionary(item => AbilityCatalog.IdOf(item.Descriptor));
        Check.That(archive.Keys.ToHashSet().SetEquals(Loadout), "Exact prerequisite-closed visible Concealed Shot loadout");
        Check.Equal(42, Loadout.Sum(AbilityMemory.Cost), "The Concealed Shot chain uses 42 of 49 memory at level 19");
        Check.Equal((ushort)Level, archive[Concealment].Level, "Concealment supplies the required trained rank");
        await CombatFixtureLoadout.VerifyWeaponAtLoginAsync(caster, CombatFixtureWeapons.Rifle);

        int removed = await StockAbilityProfile.ChangeLoadoutAsync(caster, archive, new[] { ConcealedShot }, load: false);
        int parentRemoved = await StockAbilityProfile.ChangeLoadoutAsync(caster, archive, new[] { Concealment }, load: false);
        var grantRemoved = await StockAbilityProfile.ObserveAsync(caster, parentRemoved,
            StockAbilityProfile.GrantRecord(Stealth.ConcealmentGrantAbility, remove: true), "503 removes its live 1137 grant");
        int parentAdded = await StockAbilityProfile.ChangeLoadoutAsync(caster, archive, new[] { Concealment }, load: true);
        var grantAdded = await StockAbilityProfile.ObserveAsync(caster, parentAdded,
            StockAbilityProfile.GrantRecord(Stealth.ConcealmentGrantAbility, remove: false), "503 restores its live 1137 grant");
        int added = await StockAbilityProfile.ChangeLoadoutAsync(caster, archive, new[] { ConcealedShot }, load: true);
        var unload = await StockAbilityProfile.ObserveAsync(caster, removed,
            packet => BytePattern.Parse("80 b3 f8 01").IsMatch(packet.Bytes), "Ordinary archive unload");
        var load = await StockAbilityProfile.ObserveAsync(caster, added,
            packet => BytePattern.Parse("80 b2 f8 01 13 00").IsMatch(packet.Bytes), "Level-19 ordinary archive load");

        var self = caster.World.CreationSnapshot().Last(view => view.ViewId == 2);
        var target = await caster.WaitForViewNamedAsync(enemy.Handle);
        await enemy.WaitForViewNamedAsync(caster.Handle);
        await StarterFixedDamageMode.SelectStyle(caster, self, Rifles, 171);
        var conceal = new AbilityUseRecord(Stealth.ConcealmentGrantAbility, DuelPackets.Handle(self.ViewId, self.SpawnId),
            0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode();
        caster.SendRpc(conceal);
        var on = await caster.WaitForRpcAsync("80 bd 05 71 04 00 00 00 00 00 01");
        string log = server.DebugLog();
        Check.That(log.Contains("[STEALTH] CSR_Tester 1137 on", StringComparison.Ordinal), "The ordinary granted action enters concealment");

        var use = await StarterFixedDamageMode.Land(caster, enemy, target, ConcealedShot, 23, 6, 45, RifleOpener);
        var off = await caster.WaitForRpcAsync("80 bd 05 71 04 00 00 00 00 00 00");
        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count, "Concealed Shot request framing");
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "starter-concealed-shot.json"),
            JsonSerializer.Serialize(new { ArchiveUnload = unload.Hex, ArchiveLoad = load.Hex,
                GrantRemoved = grantRemoved.Hex, GrantAdded = grantAdded.Hex, ConcealmentOn = on.Hex, ConcealmentOff = off.Hex,
                Shot = Convert.ToHexString(use), Base = 22, Landed = 23, Cast = 6, OwnReuse = 45 },
                new JsonSerializerOptions { WriteIndented = true }));
        return Array.Empty<byte>();
    }
}
