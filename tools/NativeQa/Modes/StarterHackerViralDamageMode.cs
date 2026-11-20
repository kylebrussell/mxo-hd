using System.Buffers.Binary;
using System.Text.Json;
using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class StarterHackerViralDamageMode : NativeQaMode
{
    private const byte PlayerLevel = 18;
    private const ushort HackerStyle = CombatStyles.HackerStyle;
    private const ushort SharedReuse = 682;
    private static readonly ushort[] Roots = { 2, 17, 57, 58, 59, 60 };
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(Roots).Order().ToArray();
    private static readonly (ushort Id, int Base, int Landed, int Cast, int Reuse)[] Cases =
    {
        (57, 8, 9, 2, 4), (58, 11, 12, 2, 4), (59, 17, 19, 2, 4), (60, 7, 8, 4, 2)
    };

    public override string Name => "ability-parity-starter-hacker-viral-damage";
    public override NativeQaModePlan Plan { get; } = new(Loadout, 24800, -1498,
        Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET level={PlayerLevel},exp={PlayerProgression.ExperienceForLevel(PlayerLevel)},y=495,healthC=1000,healthM=1000,innerStrC=200,innerStrM=200 WHERE charId IN (110,6111,6112,6114)",
            $"UPDATE char_abilities SET level={PlayerLevel} WHERE char_id=110",
            "DELETE FROM char_abilities WHERE char_id IN (6111,6112,6114)",
            "UPDATE characters SET alignment=1 WHERE charId=110",
            "UPDATE characters SET alignment=2,z=-1498,x=CASE charId WHEN 6114 THEN 25200 WHEN 6111 THEN 25400 ELSE 25700 END WHERE charId IN (6111,6112,6114)",
            $"DELETE FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}"
        }, DevCommands: true, loadAbilityAncestors: true, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "10",
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766",
            ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        }, readyMessages: new[]
        {
            "At level 18, select Hacker Style and target QAEnemy. Stay out of interlock and use each stocked ability through Actions, waiting for the shared timer between casts.",
            "Use Logic Barrage 1.0 (57), Logic Blast 1.0 (58), Logic Barrage 2.0 (59) and Code Nuke 1.0 (60). Record the outgoing use requests and damage on QAEnemy. Hacker Style and the required Transmit Virus passive scale fixed bases 8/11/17/7 to expected damage 9/12/19/8.",
            "QAFriend is 2 m beyond QAEnemy and QAFar is 5 m beyond QAEnemy. Verify only Code Nuke also hits QAFriend and QAFar remains untouched. The disposable fixture fixes only the eligible viral contest draw."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        foreach (string name in new[] { "QAEnemy", "QAFriend", "QAFar" })
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, name, name, stockCharacterLoad: true);
            context.Helpers.Add(name, helper);
            await helper.EnterWorldAsync();
        }
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();

        var loaded = AbilityMemoryUiFixture.Decode(caster.CharacterData[6]);
        Check.That(loaded.Select(row => AbilityCatalog.IdOf(row.Descriptor)).ToHashSet().SetEquals(Loadout) &&
            loaded.All(row => row.Level == PlayerLevel), "Stock Margin loads the level-18 prerequisite profile");
        await caster.EnterWorldAsync();
        var self = caster.World.CreationSnapshot().Last(view => view.ViewId == 2);
        var target = await caster.WaitForViewNamedAsync("QAEnemy");
        await caster.WaitForViewNamedAsync("QAFriend");
        await caster.WaitForViewNamedAsync("QAFar");
        var enemy = context.Helpers["QAEnemy"];
        var near = context.Helpers["QAFriend"];
        var far = context.Helpers["QAFar"];

        var passive = await StockAbilityProfile.ObserveAsync(caster, 0,
            packet => StockAbilityProfile.Bonus(packet, 95, 1031, 5, 0, false), "Transmit Virus +5 linked viral damage");
        int styleAt = caster.World.RpcSnapshot().Count;
        caster.SendRpc(new AbilityUseRecord(HackerStyle, DuelPackets.Handle(self.ViewId, self.SpawnId),
            0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode());
        var influence = await StockAbilityProfile.ObserveAsync(caster, styleAt,
            packet => StockAbilityProfile.Bonus(packet, HackerStyle, 172, 12, 0, false), "Hacker Style +12 viral damage");

        var proof = new List<object>();
        foreach (var sample in Cases)
        {
            int sourceAt = caster.World.RpcSnapshot().Count;
            int targetAt = enemy.World.RpcSnapshot().Count;
            int nearAt = near.World.RpcSnapshot().Count;
            int farAt = far.World.RpcSnapshot().Count;
            byte[] use = VirusUse(sample.Id, target);
            caster.SendRpc(use);
            RpcRecord hit = await HitAsync(enemy, targetAt, caster.Handle, sample.Id, sample.Landed);
            string? splash = sample.Id == 60 ? (await HitAsync(near, nearAt, caster.Handle, sample.Id, sample.Landed)).Hex : null;
            byte[] ownTimer = AbilityCooldowns.BuildTimer(sample.Id, AbilityCooldowns.OwnTimer, sample.Reuse, true);
            byte[] reuseOff = AbilityCooldowns.BuildTimer(SharedReuse, AbilityCooldowns.GroupTimer, sample.Reuse, false);
            var bar = BytePattern.Parse($"80 ac {StockAbilityProfile.LittleEndianHex(sample.Id)}");
            var cast = await StockAbilityProfile.ObserveAsync(caster, sourceAt,
                packet => bar.IsMatch(packet.Bytes) && BitConverter.ToSingle(packet.Bytes, packet.Bytes.Length - 4) == sample.Cast,
                $"ordinary cast bar {sample.Id}");
            var timer = await StockAbilityProfile.ObserveAsync(caster, sourceAt,
                packet => packet.Bytes.AsSpan().SequenceEqual(ownTimer), $"own reuse {sample.Id}");
            await StockAbilityProfile.ObserveAsync(caster, sourceAt,
                packet => packet.Bytes.AsSpan().SequenceEqual(reuseOff), $"shared viral reuse complete {sample.Id}");

            if (sample.Id != 60) Check.Equal(0, CountDamage(near, nearAt, caster.Handle, sample.Id), "Single target omits nearby enemy");
            Check.Equal(0, CountDamage(far, farAt, caster.Handle, sample.Id), "Out-of-radius enemy takes no ability hit");
            proof.Add(new { sample.Id, sample.Base, sample.Landed, Use = Convert.ToHexString(use), Cast = cast.Hex,
                Timer = timer.Hex, PrimaryDamage = hit.Hex, SecondaryDamage = splash });
        }
        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count + near.World.Problems.Count + far.World.Problems.Count,
            "Starter viral packets remain framed");
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "starter-hacker-viral-damage.json"),
            JsonSerializer.Serialize(new { HackerStyle = influence.Hex, TransmitVirus = passive.Hex, Samples = proof }, new JsonSerializerOptions { WriteIndented = true }));
        return Array.Empty<byte>();
    }

    private static byte[] VirusUse(ushort id, ViewCreation target)
    {
        // Captured normal Logic Barrage request (PR321, PacketLog 7a4fc7efcf1c:1742).
        byte[] request = Convert.FromHexString("80b94a010e000c000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000");
        BinaryPrimitives.WriteUInt16LittleEndian(request.AsSpan(2), id);
        BinaryPrimitives.WriteUInt32LittleEndian(request.AsSpan(4), DuelPackets.Handle(target.ViewId, target.SpawnId));
        return request;
    }

    private static Task<RpcRecord> HitAsync(HeadlessPlayer receiver, int cursor, string caster, ushort id, int final)
    {
        byte[] damage = AbilityMessages.Damage(false, id, caster, receiver.Handle, final, 0);
        return StockAbilityProfile.ObserveAsync(receiver, cursor,
            packet => packet.Bytes.AsSpan().SequenceEqual(damage), $"ordinary {id} hit on {receiver.Handle} for {final}");
    }

    private static int CountDamage(HeadlessPlayer receiver, int cursor, string caster, ushort id)
    {
        byte[] prefix = AbilityMessages.Damage(false, id, caster, receiver.Handle, 0, 0)[..^8];
        return receiver.World.RpcSnapshot().Skip(cursor).Count(packet => packet.Bytes.AsSpan().StartsWith(prefix));
    }
}
