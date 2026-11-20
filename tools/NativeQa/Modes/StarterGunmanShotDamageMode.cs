using System.Buffers.Binary;
using System.Text.Json;
using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class StarterGunmanShotDamageMode : NativeQaMode
{
    private const int Level = 19;
    private const ushort BodyShot = 105, CoveringFire = 176, Rifles = 147, SubmachineGuns = 158;
    private const string BodyShotCapture = "80b9690004000200000000000000000000000000000000000000000069000000006b0000086b0000000000";
    private const string CoveringFireCapture = "80b9b000050003000000000000000000000000000000000000000000b0000000006b0000086b0000000000";
    private static readonly ushort[] Roots = { 2, 17, BodyShot, CoveringFire, Rifles, SubmachineGuns };
    private static readonly ushort[] Loadout = Roots.Concat(Roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
        .Distinct().Order().ToArray();

    public override string Name => "ability-parity-starter-gunman-shot-bases";
    public override NativeQaModePlan Plan { get; } = new(Loadout, 24800, -1498,
        Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            CombatFixtureLoadout.SeedWeaponsFor(CombatFixtureWeapons.Rifle),
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y=495," +
                "healthC=3000,healthM=3000,innerStrC=1000,innerStrM=1000,alignment=1 WHERE charId=110",
            $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110",
            $"UPDATE characters SET x=25101,y=495,z=-1498,alignment=2,level=10,exp={PlayerProgression.ExperienceForLevel(10)},pvpflag=0,healthC=3000,healthM=3000 WHERE charId=6114",
            "DELETE FROM char_abilities WHERE char_id=6114 AND ability_id NOT IN (2147485696,2147501056); UPDATE char_abilities SET level=1 WHERE char_id=6114"
        }, DevCommands: true, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "6",
            [CombatFixtureWeapons.WeaponMode] = "1"
        },
        readyMessages: new[]
        {
            "Level 19, ordinary rifle equipped. Open interlock using Close Combat on QAEnemy and reselect Rifles (147). Use Body Shot (105). Capture the landed move 11364, ballistic base 8 and the two -5 resistance records for 10 s.",
            "After the Body Shot modifiers expire, leave interlock and equip the single SMG with ?combatdepth fixture-weapon smg. Reopen Close Combat and reselect Submachine Guns (158). Use Covering Fire (176) in interlock and capture move 21083 and ballistic base 17 before style influence. A single cast does not measure its root probability."
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "105,176", ["NativeCloseableRows"] = 0 });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(enemy.Handle, enemy);
        await enemy.EnterWorldAsync();
        if (context.Server == null) return Array.Empty<byte>();

        var server = context.ActualServer ?? throw new InvalidOperationException("Expected a disposable server.");
        var database = context.OwnedDatabase ?? throw new InvalidOperationException("Expected a disposable database.");
        using var caster = await HeadlessPlayer.LoginAsync(context.Address, "CSR_AbilityQA", "CSR_Tester", stockCharacterLoad: true);
        var archive = AbilityMemoryUiFixture.Decode(caster.CharacterData[6]);
        Check.That(archive.Select(row => AbilityCatalog.IdOf(row.Descriptor)).ToHashSet().SetEquals(Loadout),
            "The stock character receives the prerequisite-closed starter shot loadout");
        Check.Equal(36, Loadout.Sum(AbilityMemory.Cost), "Both starter shot chains fit in level-19 memory");
        await CombatFixtureLoadout.VerifyWeaponAtLoginAsync(caster, CombatFixtureWeapons.Rifle);
        var target = await caster.WaitForViewNamedAsync(enemy.Handle);
        var proof = new List<object>();

        await StarterInterlockDamageMode.Open(caster, target);
        await StarterInterlockDamageMode.Select(caster, target, Rifles);
        proof.Add(await StyleInfluenceAsync(caster, Rifles));
        int effectStart = enemy.World.RpcSnapshot().Count;
        proof.Add(await LandAsync(caster, enemy, target, server, BodyShot, 8, 11364, BodyShotCapture));
        foreach (bool remove in new[] { false, true })
        foreach (ushort stat in new ushort[] { 180, 181 })
        {
            var record = await StockAbilityProfile.ObserveAsync(enemy, effectStart,
                packet => StockAbilityProfile.Bonus(packet, BodyShot, stat, -5, 10, remove),
                $"Body Shot {stat} -5 for 10 seconds (remove={remove})");
            proof.Add(new { Ability = BodyShot, Stat = stat, Remove = remove, Record = record.Hex });
        }

        string handle = WireFormat.ToHex(BitConverter.GetBytes(DuelPackets.Handle(target.ViewId, target.SpawnId)));
        caster.SkipReceived();
        caster.SendRpc("44 00");
        enemy.SendRpc("44 00");
        await caster.WaitForRpcAsync($"46 {handle} 00");
        await CombatFixtureLoadout.VerifyWeaponSwapAsync(caster, database, "smg", CombatFixtureWeapons.Smg);

        await StarterInterlockDamageMode.Open(caster, target);
        await StarterInterlockDamageMode.Select(caster, target, SubmachineGuns);
        proof.Add(await StyleInfluenceAsync(caster, SubmachineGuns));
        proof.Add(await LandAsync(caster, enemy, target, server, CoveringFire, 18, 21083, CoveringFireCapture));
        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count, "Starter gun shot framing");
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "starter-gunman-shot-bases.json"),
            JsonSerializer.Serialize(proof, new JsonSerializerOptions { WriteIndented = true }));
        return Array.Empty<byte>();
    }

    private static async Task<object> StyleInfluenceAsync(HeadlessPlayer caster, ushort style)
    {
        var modifier = await StockAbilityProfile.ObserveAsync(caster, 0,
            packet => StockAbilityProfile.Bonus(packet, style, 171, 6, 0, false), $"style {style} adds six ballistic damage influence");
        return new { Style = style, Modifier = modifier.Hex };
    }

    private static async Task<object> LandAsync(HeadlessPlayer caster, HeadlessPlayer enemy, ViewCreation target,
        ServerInstance server, ushort id, int amount, int animation, string capture)
    {
        byte[] request = Convert.FromHexString(capture);
        BinaryPrimitives.WriteUInt32LittleEndian(request.AsSpan(4), DuelPackets.Handle(target.ViewId, target.SpawnId));
        byte[] damage = AbilityMessages.Damage(false, id, caster.Handle, enemy.Handle, amount, 0);
        for (int attempt = 0; attempt < 4; ++attempt)
        {
            int start = enemy.World.RpcSnapshot().Count;
            int logStart = server.DebugLog().Length;
            caster.SendRpc(request);
            try
            {
                var hit = await StockAbilityProfile.ObserveAsync(enemy, start,
                    packet => packet.Bytes.AsSpan().SequenceEqual(damage), $"interlock {id} lands for {amount}");
                Check.That(server.DebugLog()[logStart..].Contains($"animation {animation} (", StringComparison.Ordinal),
                    $"Interlock {id} uses animation {animation}");
                return new { Ability = id, Damage = amount, Animation = animation, Request = Convert.ToHexString(request), Hit = hit.Hex };
            }
            catch (TimeoutException) when (attempt < 3)
            {
                string name = AbilityCatalog.Find(id).Name;
                if (server.DebugLog()[logStart..].Contains($"[PVP] {caster.Handle}'s {name} lands ", StringComparison.Ordinal))
                    throw;
            }
        }
        throw new InvalidOperationException($"No landed evidence for {id}.");
    }
}
