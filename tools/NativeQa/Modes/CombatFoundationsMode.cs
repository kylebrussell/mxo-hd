using HeadlessClient;
using hds;
using System.Buffers.Binary;

namespace NativeQa.Modes;

public sealed class CombatFoundationsMode : NativeQaMode
{
    public override string Name => "combat-foundations";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2, 17, 158, 330, 493, 1006 }, 24800, -1498,
        Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET level=50,exp={PlayerProgression.ExperienceForLevel(50)},healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000,y=495 WHERE charId=110",
            "DELETE FROM inventory WHERE charId=110 AND slot IN (1,2,105); INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,692,1,1,0),(110,7899,2,1,0)"
        }, DevCommands:true, FixedCallbackRolls:false, environmentOverrides:new()
        {
            ["MXO_MOBS"] = "test", ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            ["MXO_COMBAT_FOUNDATIONS_QA"] = "1", ["MXO_FREECAST_VIRAL_QA"] = "1",
            ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        }, readyMessages:new[]
        {
            "Enter ?combatdepth foundations-sight. The fixture equips the rifle and puts CSR at 22700,1095,16100 facing west. Try Free Fire and Logic Barrage 4.0 toward AB1 Blocked across the outdoor gap. Turn east to AB1 Clear and check 525 damage with the default Self Defense style.",
            "Enter ?combatdepth foundations-cone. The fixture equips dual SMGs. Select Submachine Guns and aim Direct Fire at AB1 Aim. Only AB1 Aim and AB1 Inside should take damage. AB1 Resisted holds its effect for 30 minutes. Logic Barrage should deal 431. See docs/COMBAT-FOUNDATIONS-QA.md."
        }, loadAbilityAncestors: true);

    internal static async Task<object> VerifyLogicBarrageAsync(ServerInstance server, HeadlessPlayer actor)
    {
        const ushort clearDamage = 525, resistedDamage = 431;
        const ushort viralDamageBonus = 1031;
        var sources = new[] { (Ability: 95u, Bonus: 15), (Ability: 309u, Bonus: 3),
            (Ability: 310u, Bonus: 1), (Ability: 334u, Bonus: 5) };
        static string Le(ushort value) => WireFormat.ToHex(BitConverter.GetBytes(value));

        await actor.EnterWorldAsync();
        foreach (var (ability, bonus) in sources)
        {
            await actor.WaitForRpcAsync(
                $"80 bc 45 00 ?? ?? 00 {WireFormat.ToHex(BitConverter.GetBytes(ability))} {Le(viralDamageBonus)} " +
                $"{WireFormat.ToHex(BitConverter.GetBytes(bonus))} 00 00 00 00 00");
        }

        async Task VerifyHitAsync(string command, string ready, string target, ushort damage)
        {
            actor.SendChat(command);
            await actor.WaitForTextAsync(ready);
            var view = await actor.WaitForViewNamedAsync(target);
            // PR321 stock CSR_Tester PacketLog 7a4fc7efcf1c:1742 and :3426. Replace only the live target handle.
            byte[] cast = Convert.FromHexString("80b94a010e000c000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000");
            BinaryPrimitives.WriteUInt32LittleEndian(cast.AsSpan(4), (uint)view.ViewId | ((uint)view.SpawnId << 16));
            actor.SendRpc(cast);
            await actor.WaitForViewUpdateAsync(view.ViewId, "04 80 80 80 c0 " + Le((ushort)(5000 - damage)));
        }

        await VerifyHitAsync("?combatdepth foundations-sight", "across a mapped gap", "AB1 Clear", clearDamage);
        await Task.Delay(TimeSpan.FromSeconds(4.5));
        await VerifyHitAsync("?combatdepth foundations-cone", "AB1 Outside stays full", "AB1 Resisted", resistedDamage);
        Check.That(server.DebugLog().Contains("[TYPED-RESISTANCE OURS] 330 type2 total75 raw525 final431"),
            "Logic Barrage uses the same 525 raw damage and applies +75 viral resistance");
        Check.Equal(0, actor.World.Problems.Count, "Logic Barrage stock replay framing");

        Console.WriteLine($"Logic Barrage stock CSR: +{sources.Sum(source => source.Bonus)}% viral damage, " +
            $"{clearDamage} clear, {resistedDamage} with +75 viral resistance.");
        return new { Ability = 330, ViralDamageBonus = sources.Sum(source => source.Bonus),
            ClearDamage = clearDamage, ResistedDamage = resistedDamage, ViralResistance = 75 };
    }
}
