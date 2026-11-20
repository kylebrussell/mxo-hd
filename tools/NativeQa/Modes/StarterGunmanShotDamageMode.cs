using System.Buffers.Binary;
using System.Text.Json;
using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public abstract class StarterGunmanShotDamageMode : NativeQaMode
{
    private const int Level = 19;
    private readonly ushort ability;
    private readonly ushort style;
    private readonly uint weapon;
    private readonly int landedDamage;
    private readonly int animation;
    private readonly int memory;
    private readonly string capture;
    private readonly ushort[] loadout;

    public override string Name { get; }
    public override NativeQaModePlan Plan { get; }

    protected StarterGunmanShotDamageMode(string name, ushort ability, ushort style, uint weapon,
        int landedDamage, int animation, int memory, string capture, string instruction)
    {
        Name = name;
        this.ability = ability;
        this.style = style;
        this.weapon = weapon;
        this.landedDamage = landedDamage;
        this.animation = animation;
        this.memory = memory;
        this.capture = capture;

        ushort[] roots = { 2, 17, ability, style };
        loadout = roots.Concat(roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
            .Distinct().Order().ToArray();
        Plan = new(loadout, 24800, -1498,
            Array.Empty<string>(), Array.Empty<string>(), new[]
            {
                CombatFixtureLoadout.SeedWeaponsFor(weapon),
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
            readyMessages: new[] { $"Level 19, {memory}/50 memory. Weapon {weapon} is equipped at login.", instruction },
            extraQaMetadata: new() { ["ScopedStockRows"] = ability.ToString(), ["NativeCloseableRows"] = 0 });
    }

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(enemy.Handle, enemy);
        await enemy.EnterWorldAsync();
        if (context.Server == null) return Array.Empty<byte>();

        var server = context.ActualServer ?? throw new InvalidOperationException("Expected a disposable server.");
        using var caster = await HeadlessPlayer.LoginAsync(context.Address, "CSR_AbilityQA", "CSR_Tester", stockCharacterLoad: true);
        var archive = AbilityMemoryUiFixture.Decode(caster.CharacterData[6]);
        Check.That(archive.Select(row => AbilityCatalog.IdOf(row.Descriptor)).ToHashSet().SetEquals(loadout),
            "The stock character receives the prerequisite-closed starter shot loadout");
        Check.Equal(memory, loadout.Sum(AbilityMemory.Cost), "The starter shot chain fits in level-19 memory");
        await CombatFixtureLoadout.VerifyWeaponAtLoginAsync(caster, weapon);
        var target = await caster.WaitForViewNamedAsync(enemy.Handle);
        var proof = new List<object>();

        await StarterInterlockDamageMode.Open(caster, target);
        await StarterInterlockDamageMode.Select(caster, target, style);
        proof.Add(await StyleInfluenceAsync(caster, style));
        int effectStart = enemy.World.RpcSnapshot().Count;
        proof.Add(await LandAsync(caster, enemy, target, server, ability, landedDamage, animation, capture));
        if (ability == 105)
        {
            foreach (bool remove in new[] { false, true })
            foreach (ushort stat in new ushort[] { 180, 181 })
            {
                var record = await StockAbilityProfile.ObserveAsync(enemy, effectStart,
                    packet => StockAbilityProfile.Bonus(packet, ability, stat, -5, 10, remove),
                    $"Body Shot {stat} -5 for 10 seconds (remove={remove})");
                proof.Add(new { Ability = ability, Stat = stat, Remove = remove, Record = record.Hex });
            }
        }

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

public sealed class StarterBodyShotMode() : StarterGunmanShotDamageMode(
    "ability-parity-starter-body-shot", 105, 147, CombatFixtureWeapons.Rifle, 8, 11364, 32,
    "80b9690004000200000000000000000000000000000000000000000069000000006b0000086b0000000000",
    "Open Close Combat on QAEnemy, reselect Rifles (147) and use Body Shot (105). Capture move 11364, landed 8 and both -5 resistance records for 10 seconds.");

public sealed class StarterCoveringFireMode() : StarterGunmanShotDamageMode(
    "ability-parity-starter-covering-fire", 176, 158, CombatFixtureWeapons.Smg, 18, 21083, 34,
    "80b9b000050003000000000000000000000000000000000000000000b0000000006b0000086b0000000000",
    "Open Close Combat on QAEnemy, reselect Submachine Guns (158) and use Covering Fire (176). Capture move 21083 and landed 18 from ballistic base 17 plus style influence.");
