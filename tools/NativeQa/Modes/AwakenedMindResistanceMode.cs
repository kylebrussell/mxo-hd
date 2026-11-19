using HeadlessClient;
using hds;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class AwakenedMindResistanceMode : NativeQaMode
{
    private const ushort CalmMind = 5, EmptyMind = 8, ThrownAttack = 1135, ViralAttack = 330, ViralDot = 474;
    private const ushort FullInnerStrength = 2000;

    public override string Name => "awakened-mind-resistance";
    public override NativeQaModePlan Plan { get; } = new(TypedResistanceFixture.Abilities, 23828, 13984,
        Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            "UPDATE characters SET level=50,y=495,alignment=CASE WHEN charId=6114 THEN 2 ELSE 1 END,pvpflag=1,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId IN (110,6111,6112,6113,6114)"
        }, DevCommands: true, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0",
            ["MXO_TYPED_RESISTANCE_QA"] = "1", ["MXO_TYPED_RESISTANCE_QA_DAMAGE_DRAW"] = "65",
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766",
            ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "5,8", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            "Rows 5 and 8, level 50. Enter ?pvpflag on and confirm the own flag. Activate Calm Mind, Calm Body with QAEnemy selected. Capture +75 thrown resistance for 20 s, a 20 IS debit and 60 s reuse. Request QAEnemy 1135 during the buff and after expiry. The current model gives 68 damage/15 resisted during and 83/0 after.",
            "Activate Empty Mind with QAEnemy selected. Capture +75 viral resistance for 20 s, a 20 IS debit and 60 s reuse. Request QAEnemy 330 during this buff and again after it expires. The current model gives 348/76 during and 424/0 after.",
            "Before Empty Mind expires, have QAEnemy cast 474 once. Keep a damage tick on each side of that expiry in the same infection. Only the existing disposable 1135 damage draw and eligible viral contest draws are fixed. Retail resistance/source coefficients remain open. See docs/ABILITY-AWAKENED-MIND-RESISTANCE.md."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        foreach (string name in context.HelperNames)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, name, name);
            context.Helpers.Add(name, helper);
            await helper.EnterWorldAsync();
        }
        var enemy = context.Helpers["QAEnemy"];
        await ConcentrationFixture.PrepareEnemyAsync(enemy);
        if (context.Server == null) return Array.Empty<byte>();

        using var defender = await HeadlessPlayer.LoginAsync(context.Address, "CSR_AbilityQA", "CSR_Tester", stockCharacterLoad: true);
        await defender.EnterWorldAsync();
        defender.SendChat("?pvpflag on");
        await defender.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 80 01 00 00 00 01");
        var enemyView = await defender.WaitForViewNamedAsync(enemy.Handle);
        var defenderView = await enemy.WaitForViewNamedAsync(defender.Handle);

        int calm = await Activate(defender, CalmMind, 1033, enemyView.ViewId);
        await Hit(enemy, defender, ThrownAttack, defenderView.ViewId, 68, 15);
        await Modifier(defender, CalmMind, 1033, calm, remove: true);
        await Hit(enemy, defender, ThrownAttack, defenderView.ViewId, 83, 0);

        int empty = await Activate(defender, EmptyMind, 157, enemyView.ViewId);
        await Hit(enemy, defender, ViralAttack, defenderView.ViewId, 348, 76);
        int beforeDot = defender.World.RpcSnapshot().Count;
        Cast(enemy, ViralDot, defenderView.ViewId);
        var early = await DotTick(defender, beforeDot, resisted: true);
        await Modifier(defender, EmptyMind, 157, empty, remove: true);
        var late = await DotTick(defender, defender.World.RpcSnapshot().Count, resisted: false);
        Check.Equal(late.Damage, early.Damage + early.Resisted, "The same infection has the same underlying damage before and after Empty Mind expiry");
        await Hit(enemy, defender, ViralAttack, defenderView.ViewId, 424, 0);
        Console.WriteLine("  Both ordinary Awakened casts paid 20 IS and emitted sourced add/off and reuse. Typed direct hits and one infection crossed natural expiry.");
        return Array.Empty<byte>();
    }

    private static void Cast(HeadlessPlayer actor, ushort ability, ushort view) =>
        actor.SendRpc($"80 b9 {WireFormat.ToHex(BitConverter.GetBytes(ability))} {WireFormat.ToHex(BitConverter.GetBytes(view))}");

    private static async Task<int> Activate(HeadlessPlayer actor, ushort id, ushort stat, ushort enemyView)
    {
        int cursor = actor.World.RpcSnapshot().Count;
        int viewCursor = actor.World.ViewSnapshot().Count;
        Cast(actor, id, enemyView);
        await Modifier(actor, id, stat, cursor, remove: false);
        byte[] timer = AbilityCooldowns.BuildTimer(id, AbilityCooldowns.OwnTimer, 60, true);
        await actor.WaitUntilAsync(() => actor.World.RpcSnapshot().Skip(cursor)
            .Any(rpc => rpc.Bytes.AsSpan().SequenceEqual(timer)), $"{id} ordinary 60-second reuse", HeadlessPlayer.DefaultTimeout);
        byte[] strength = WireFormat.FromHex("02 00 02 80 80 80 10");
        await actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(viewCursor).Any(section =>
        {
            for (int at = BytePattern.IndexOf(section, strength, 0); at >= 0; at = BytePattern.IndexOf(section, strength, at + 1))
                if (at + strength.Length + 2 <= section.Length &&
                    BitConverter.ToUInt16(section, at + strength.Length) == FullInnerStrength - 20)
                    return true;
            return false;
        }), $"{id} ordinary 20 Inner Strength debit", HeadlessPlayer.DefaultTimeout);
        return cursor;
    }

    private static Task Modifier(HeadlessPlayer actor, ushort id, ushort stat, int cursor, bool remove) =>
        actor.WaitUntilAsync(() => actor.World.RpcSnapshot().Skip(cursor).Any(rpc =>
            ManageBonusRecord.TryDecode(rpc.Bytes, out var mod) && mod!.Family == 0x15 &&
            mod.SourceId == id && mod.TargetId == stat && mod.SignedValue == 75 &&
            mod.Duration == 20 && mod.AddRemoveType == (remove ? 1 : 0)),
            $"{id} sourced +75 resistance for 20 seconds, remove={remove}", TimeSpan.FromSeconds(24));

    private static async Task Hit(HeadlessPlayer attacker, HeadlessPlayer defender, ushort ability, ushort view, int damage, int resisted)
    {
        int cursor = defender.World.RpcSnapshot().Count;
        byte[] expected = AbilityMessages.Damage(false, ability, attacker.Handle, defender.Handle, damage, resisted);
        Cast(attacker, ability, view);
        await defender.WaitUntilAsync(() => defender.World.RpcSnapshot().Skip(cursor).Any(rpc => rpc.Bytes.AsSpan().SequenceEqual(expected)),
            $"{ability} ordinary hit on {defender.Handle}, {damage} damage / {resisted} resisted", HeadlessPlayer.DefaultTimeout);
    }

    private static async Task<(int Damage, int Resisted)> DotTick(HeadlessPlayer actor, int cursor, bool resisted)
    {
        (int Damage, int Resisted)? observed = null;
        await actor.WaitUntilAsync(() =>
        {
            foreach (var rpc in actor.World.RpcSnapshot().Skip(cursor))
            {
                var bytes = rpc.Bytes;
                if (bytes.Length < 32 || bytes[0] != 0x81 || bytes[1] != 0x67 ||
                    BitConverter.ToUInt32(bytes, 6) != AbilityMessages.DamageTarget ||
                    BitConverter.ToUInt32(bytes, 15) != AbilityMessages.AbilityGameObject(ViralDot))
                    continue;
                int damage = BitConverter.ToInt32(bytes, bytes.Length - 8);
                int reduction = BitConverter.ToInt32(bytes, bytes.Length - 4);
                if (damage > 0 && (reduction > 0) == resisted)
                {
                    observed = (damage, reduction);
                    return true;
                }
            }
            return false;
        }, $"ordinary 474 tick with resistance={resisted}", HeadlessPlayer.DefaultTimeout);
        return observed!.Value;
    }
}
