using HeadlessClient;
using hds;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class DisguiseGrantMode : NativeQaMode
{
    private const ushort SpyStyle = CombatStyles.SpyStyle;
    private const ushort DisguiseParent = 120, DisguiseGrant = 1114;
    private const uint SwatDisguiseToolGoid = 1268;
    private const byte ToolBagSlot = 40;
    private const byte Level = 22, Rank = 22;
    private static readonly ushort[] Roots = { 2, 17, SpyStyle, DisguiseParent };
    private static readonly ushort[] Loadout = Roots.Concat(Roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
        .Distinct().ToArray();

    public override string Name => "ability-parity-disguise-grant";

    public override NativeQaModePlan Plan { get; } = new(
        Loadout, 24800, -1498, Array.Empty<string>(),
        new[]
        {
            $"DELETE FROM inventory WHERE charId=110 AND (slot={ToolBagSlot} OR goid={SwatDisguiseToolGoid}); " +
            $"INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,{SwatDisguiseToolGoid},{ToolBagSlot},1,0)"
        },
        new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y=495," +
                "healthC=2000,healthM=2000,innerStrC=1000,innerStrM=1000 WHERE charId=110",
            $"UPDATE char_abilities SET level={Rank} WHERE char_id=110",
            $"DELETE FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}"
        }, DevCommands: true,
        hotbarAbilities: new ushort[] { SpyStyle },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[]
        {
            "Level 22 with rank-22 Disguise (120) and SWAT Disguise tool ('Disguise Mask', GOID 1268, requires Disguise 20 / ability 1114) in bag slot 40 (no hostile NPC within 8 m). Disguise (1114) is a tool-invoked grant like Invisibility (1116), not an Actions > Abilities icon.",
            "Step A: Press page 2 slot 1 (Spy Style 1112). Open Actions > Tools, add 'Disguise Mask' (bag slot 40) to the hotbar, and press it. Expect no client-local refusal, 2.0 s cast (80 ac), '[STEALTH] CSR_Tester 1114 on (bits 8, ...)' for 42 s (31 + 22/2) with SWAT disguise model (1308623188), and pressing it again toggles it off ('1114 off (switched off)').",
            "Step B: '?qaunload 120' unloads the hidden 1114 grant so pressing Disguise Mask is refused; '?qaload 120' restores rank-22 1114 once and Disguise Mask activates again."
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "120,1114", ["NativeCloseableRows"] = 0,
            ["RegistrationSource"] = DisguiseParent, ["RegistrationChild"] = DisguiseGrant,
            ["NativeEntry"] = "Actions > Tools > double-click SWAT Disguise (GOID 1268, bag slot 40); hidden child 1114 is not archived",
            ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost), ["MemoryCapacity"] = AbilityMemory.ForLevel(Level)
        });

    private static byte[] ToolRequest()
    {
        // Shared normal tool producer 621ecf10: 43 bytes, bag slot at 37, ordinary flag 0 at 38.
        var b = new byte[43];
        b[0] = 0x80; b[1] = 0xb9;
        BitConverter.GetBytes(DisguiseGrant).CopyTo(b, 2);
        BitConverter.GetBytes((ushort)2).CopyTo(b, 4);
        b[36] = 1; b[37] = ToolBagSlot;
        return b;
    }

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();
        var server = context.ActualServer ?? throw new InvalidOperationException("Expected disposable server.");

        await caster.EnterWorldAsync();
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level), "Legal level-22 memory");

        var rank22 = StockAbilityProfile.GrantRecord(DisguiseGrant, remove: false, rank: Rank);
        await StockAbilityProfile.ObserveAsync(caster, 0, rank22, "1114 rank-22 grant published at world readiness");
        await StockAbilityProfile.ObserveAsync(caster, 0, packet =>
            ManageBonusRecord.TryDecode(packet.Bytes, out var record) && record!.Type == 0x45 &&
            record.Locator == ((uint)DisguiseGrant << 8 | 3) && record.SourceId == DisguiseParent &&
            record.TargetId == DisguiseGrant && record.ValueBits == Rank && record.AddRemoveType == 0,
            "1114 rank-22 80 bc 45 membership emitted at login");

        caster.SendRpc(new AbilityUseRecord(SpyStyle, 0, 0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode());

        int onStart = server.DebugLog().Length;
        caster.SendRpc(ToolRequest());
        await caster.WaitUntilAsync(() => server.DebugLog()[onStart..].Contains("[STEALTH] CSR_Tester 1114 on", StringComparison.Ordinal),
            "Disguise (1114) activates via SWAT Disguise tool (bag slot 40)", TimeSpan.FromSeconds(5));

        int offStart = server.DebugLog().Length;
        caster.SendRpc(ToolRequest());
        await caster.WaitUntilAsync(() => server.DebugLog()[offStart..].Contains("1114 off (switched off)", StringComparison.Ordinal),
            "Disguise (1114) toggles off cleanly", TimeSpan.FromSeconds(5));

        Console.WriteLine("  Disguise (120 -> 1114) published rank-12 registration at login and toggled on/off via GOID 1268 bag slot 40");
        return Array.Empty<byte>();
    }
}
