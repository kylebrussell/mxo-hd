using System.Buffers.Binary;
using System.Text.Json;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class AbilityParityCoderAdvancedMode : NativeQaMode
{
    private static readonly (ushort Ability, ushort Stat, int Value)[] Passives =
    {
        (518, 654, 50), (521, 656, 50), (585, 1031, 10), (593, 1146, 10), (593, 1148, 10),
        (599, 1053, 5), (600, 996, 5), (600, 997, 5), (614, 1140, 5), (614, 1143, 5),
    };

    private static readonly (ushort Ability, byte Level, string? Name, float Reuse)[] Summons =
    {
        (586, 34, null, 8), (594, 50, null, 8), (601, 17, "Nuker Simulacrum", 10),
        (602, 22, "Healer Simulacrum", 10), (616, 50, "Nuker Simulacrum", 10),
        (617, 50, "Healer Simulacrum", 10),
    };

    // Native PR312, PacketLog 88a1d93e477a:3167. Only the compact summon ability ID changes.
    private const string StockSummon =
        "80b95202000000000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";

    public override string Name => "ability-parity-coder-advanced";

    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2, 17, 1008 }.Concat(Passives.Select(passive => passive.Ability))
            .Concat(Summons.Select(summon => summon.Ability)).Distinct().ToArray(),
        24800, -1498, Array.Empty<string>(), Array.Empty<string>(),
        new[] { "UPDATE characters SET y=495,innerStrC=2000,innerStrM=2000 WHERE charId IN (110,6111)" },
        readyMessages: new[]
        {
            "In Status or PacketLog capture Run Advanced Program (585) +10 Viral Damage Bonus, Close Combat Training (600) +5 each accuracy bonus, and Enhance Daemon (614) +5 each melee defense and ballistic resistance bonus.",
            "Additional passives: Tool Maker 518 and Upgrade Maker 521 contribute +50 to their respective skills. Transmit Code contributes +10 Viral Resistance Bonus and +10 Thrown Resistance Bonus. Proxy Master 599 contributes +5 Inner Strength Cost Modifier Bonus.",
            "At level 50, summon Remote Proxy 4.0 and 5.0, Logic Daemon 2.0 and 4.0, and Patch Daemon 1.0 and 3.0. Expected levels 34, 50, 17, 50, 22, 50. Capture the appearance, registration, window and deactivation for each rank. Proxy sheets may vary."
        }, loadAbilityAncestors: true);

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer ?? throw new InvalidOperationException("The Coder sweep requires a disposable server.");
        using var owner = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend", stockCharacterLoad: true);
        await owner.EnterWorldAsync();
        await owner.WaitUntilAsync(() => Passives.All(expected => owner.World.RpcSnapshot().Any(record =>
            ManageBonusRecord.TryDecode(record.Bytes, out var bonus) && bonus!.Family == 0x45 &&
            bonus.SourceId == expected.Ability && bonus.TargetId == expected.Stat && bonus.SignedValue == expected.Value &&
            bonus.AddRemoveType == 0)), "all advanced Coder passive records arrived with the shipped rank-50 values",
            HeadlessPlayer.DefaultTimeout);

        var observations = new List<object>();
        foreach (var sample in Summons)
        {
            int viewCursor = owner.World.CreationSnapshot().Count;
            byte[] request = Convert.FromHexString(StockSummon);
            BinaryPrimitives.WriteUInt16LittleEndian(request.AsSpan(2), sample.Ability);
            owner.SendRpc(request);
            string ability = WireFormat.ToHex(BitConverter.GetBytes(sample.Ability));
            var cast = await owner.WaitForRpcAsync($"80 ac {ability}");
            Check.Equal(8f, BitConverter.ToSingle(cast.Bytes, cast.Bytes.Length - 4), "The summon uses the stored eight-second cast");
            var registration = await owner.WaitForRpcAsync("81 76", TimeSpan.FromSeconds(18));
            Check.Equal(46, registration.Bytes.Length, "The owner gets the stock registration frame");
            byte petId = registration.Bytes[3];
            ViewCreation? pet = null;
            await owner.WaitUntilAsync(() =>
            {
                pet = owner.World.CreationSnapshot().Skip(viewCursor).FirstOrDefault(view =>
                    view.Goid == 40376 && view.Attributes["SimulacraID"][0] == petId);
                return pet != null;
            }, "The summoned view is visible and matches its window ID", HeadlessPlayer.DefaultTimeout);
            Check.Equal(owner.CharId, BitConverter.ToUInt32(pet!.Attributes["OwnerCharacterID"]), "The pet belongs to the caster");
            Check.Equal(owner.CharId, BitConverter.ToUInt32(pet.Attributes["MasterCharacterID"]), "The caster is the pet's master");
            Check.Equal(sample.Level, pet.Attributes["Level"][0], "The summon level follows the shipped clamp");
            Check.That(sample.Name == null
                ? hds.CoderPets.ProxySheets.Any(sheet => pet.Name == $"{sheet} Simulacrum")
                : pet.Name == sample.Name, "The pet uses a sheet from the shipped callback");
            string reuse = WireFormat.ToHex(BitConverter.GetBytes(sample.Reuse));
            await owner.WaitForRpcAsync($"80 bd 01 {ability} 04 {reuse} 01");
            owner.SendRpc("81 7a fe 01");
            var removal = await owner.WaitForRpcAsync($"81 77 {petId:x2}");
            observations.Add(new { sample.Ability, sample.Level, pet.Name, Activation = Convert.ToHexString(request),
                Registration = registration.Hex, Removal = removal.Hex });
        }

        Check.Equal(0, owner.World.Problems.Count, "Advanced Coder frames decode cleanly");
        NativeQaReadiness.RequireNoAmbientAggro(server, owner);
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "coder-advanced-bootstrap.json"),
            JsonSerializer.Serialize(observations, new JsonSerializerOptions { WriteIndented = true }));
        return Array.Empty<byte>();
    }
}
