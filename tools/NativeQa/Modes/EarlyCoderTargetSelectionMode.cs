using System.Buffers.Binary;
using System.Text.Json;
using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class EarlyCoderTargetSelectionMode : NativeQaMode
{
    private const int Level = 18;
    private const ushort Lockdown = 21, StaticBlast = 33, CoderStyle = 1008;
    private const string StockTargetedCaster = "80b92100050003000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
    private static readonly (string Name, int X, int Z)[] Stands = { ("QAEnemy", 25200, -1498), ("QAOther", 24800, -2098) };
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Lockdown, StaticBlast, CoderStyle).Order().ToArray();

    public override string Name => "ability-parity-early-coder-target-selection";
    public override NativeQaModePlan Plan { get; } = new(Loadout, 24800, -1498, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y=495,innerStrC=200,innerStrM=200,healthC=1000,healthM=1000,alignment=CASE WHEN charId=110 THEN 1 ELSE 2 END WHERE charId IN (110,6113,6114)",
        $"UPDATE char_abilities SET level={Level} WHERE char_id=110",
        "UPDATE characters SET x=31000+100*(charId-6111),z=-1498 WHERE charId IN (6113,6114)",
        "DELETE FROM char_abilities WHERE char_id IN (6113,6114)"
    }, DevCommands: true, loadAbilityAncestors: true, environmentOverrides: new()
    {
        ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "10",
        ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
    }, readyMessages: new[]
    {
        "After CSR enters, send helper chat ?tp 25200 495 -1498 0 0 0 to QAEnemy and ?tp 24800 495 -2098 0 0 0 to QAOther. Then send ?die to QAOther for the dead-target control. Use Coder Style and stay out of interlock.",
        "With no target and then with dead QAOther selected, try Lockdown 1.0 and Static Blast 1.0. If the client sends an invalid request, expect refusal without IS, cast or reuse.",
        "Select QAEnemy 4 m away and cast Lockdown for a 12 s root, 2 s cast, and 13.5 s own reuse. While the root is up, cast Static Blast on QAEnemy after any shared reuse. It should do 24 damage with a 2 s cast and 6 s own reuse, without breaking the root. Only the disposable viral contest draw is fixed."
    });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        foreach (var stand in Stands)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, stand.Name, stand.Name, stockCharacterLoad: true);
            context.Helpers.Add(stand.Name, helper);
            await helper.EnterWorldAsync();
        }
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();
        await caster.EnterWorldAsync();
        var self = caster.World.CreationSnapshot().Last(view => view.ViewId == 2);
        await StarterFixedDamageMode.SelectStyle(caster, self, CoderStyle, 172);
        foreach (var stand in Stands)
        {
            context.Helpers[stand.Name].SendChat($"?tp {stand.X} 495 {stand.Z} 0 0 0");
            await caster.WaitForViewNamedAsync(stand.Name);
        }
        var enemy = context.Helpers["QAEnemy"];
        var corpse = context.Helpers["QAOther"];
        var target = await caster.WaitForViewNamedAsync(enemy.Handle);
        var dead = await caster.WaitForViewNamedAsync(corpse.Handle);
        corpse.SendChat("?die");
        await caster.WaitForViewUpdateAsync(dead.ViewId, "02 80 80 12 01 00 00");
        uint targetHandle = DuelPackets.Handle(target.ViewId, target.SpawnId);
        var refusals = new List<object>();
        foreach (ushort id in new[] { Lockdown, StaticBlast })
        {
            foreach (var invalid in new[]
            {
                (Reason: "empty", Handle: 0u),
                (Reason: "dead", Handle: DuelPackets.Handle(dead.ViewId, dead.SpawnId)),
                (Reason: "stale", Handle: targetHandle ^ 0x10000u)
            })
            {
                byte[] request = Use(id, invalid.Handle);
                var refusal = await TinkeringTargetAdmissionMode.RefusedAsync(caster, id, request);
                refusals.Add(new { Id = id, invalid.Reason, Request = Convert.ToHexString(request), Refusal = refusal.Hex });
            }
        }

        int casterCursor = caster.World.RpcSnapshot().Count;
        int targetCursor = enemy.World.RpcSnapshot().Count;
        byte[] lockdown = Use(Lockdown, targetHandle);
        caster.SendRpc(lockdown);
        await EvidenceRetestWire.CastBarAsync(caster, Lockdown, 2);
        await EvidenceRetestWire.TimerAsync(caster, casterCursor, Lockdown, 4, 13.5f);
        var root = await StockAbilityProfile.ObserveAsync(enemy, targetCursor,
            packet => RootRecord(packet, remove: false), "Lockdown roots the selected enemy for 12 seconds");
        var held = System.Diagnostics.Stopwatch.StartNew();
        byte[] blast = await StarterFixedDamageMode.Land(caster, enemy, target, StaticBlast, 24, 2, 6, StockTargetedCaster);
        Check.That(!enemy.World.RpcSnapshot().Skip(targetCursor).Any(packet => RootRecord(packet, remove: true)),
            "Static Blast's landed damage does not break Lockdown");
        var expired = await StockAbilityProfile.ObserveAsync(enemy, targetCursor,
            packet => RootRecord(packet, remove: true), "Lockdown's natural expiry");
        Check.That(held.Elapsed.TotalSeconds >= 11, "Lockdown kept its timed root after damage");
        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count + corpse.World.Problems.Count, "Starter Coder selection framing");
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "early-coder-target-selection.json"),
            JsonSerializer.Serialize(new { Refusals = refusals, Lockdown = Convert.ToHexString(lockdown), Root = root.Hex,
                StaticBlast = Convert.ToHexString(blast), Expiry = expired.Hex }));
        return Array.Empty<byte>();
    }

    private static bool RootRecord(RpcRecord packet, bool remove) =>
        ManageBonusRecord.TryDecode(packet.Bytes, out var mod) && mod!.SourceId == Lockdown &&
        mod.Type == TempMods.KindState && mod.TargetId == TempMods.RootedAbility &&
        mod.AddRemoveType == (remove ? 1 : 0) && (remove || mod.Duration == 12);

    private static byte[] Use(ushort id, uint target)
    {
        byte[] request = Convert.FromHexString(StockTargetedCaster);
        BinaryPrimitives.WriteUInt16LittleEndian(request.AsSpan(2), id);
        BinaryPrimitives.WriteUInt32LittleEndian(request.AsSpan(4), target);
        return request;
    }
}
