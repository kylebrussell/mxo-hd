using HeadlessClient;
using System.Buffers.Binary;

namespace NativeQa.Modes;

/// <summary>Known level-50 caster and ordinary stock casts against nearby helpers.</summary>
public sealed class AbilityHealthParityMode : NativeQaMode
{
    public override string Name => "ability-health-parity";

    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2, 17, 38, 40, 45, 46, 51, 52, 54, 55, 56, 57, 58, 60, 62, 67, 70, 71, 75, 77, 80,
            95, 97, 98, 169, 170, 233, 234, 235, 236, 327, 398, 399, 401, 404, 409, 416, 461, 466, 1006 },
        24800, -1498, Array.Empty<string>(), Array.Empty<string>(),
        new[] { "UPDATE characters SET innerStrC=1000,innerStrM=1000 WHERE charId IN (110,6111)",
            "DELETE FROM inventory WHERE charId=110" },
        DevCommands: true,
        environmentOverrides: new() { ["MXO_PVP_SERVER"] = "1" },
        readyMessages: new[]
        {
            "On wounded QAFriend, Repair RSI 1.0 expects +10 HP with +15% healing influence. Emergency Repairs 1.0 expects five +3 HP ticks at 2.5 s intervals. Count the effect ticks separately from natural regeneration.",
            "Flag CSR and cast Plague Zone 2.0 on QAEnemy. After a hit, expect five ticks at 2.5 s; a deflection applies no DOT. Read the actual total and resistances in server logs.",
            "With QAFriend ordinarily dead, cast Rejuvenate RSI from the stock client and approve via the helper. Expect HP 500 and IS 1000 and an in-place stand. See docs/ABILITY-HEALTH-PARITY.md."
        });

    internal static byte[] RejuvenateReply(byte[] pending)
    {
        Check.That(pending.Length >= 6 && BinaryPrimitives.ReadUInt32LittleEndian(pending.AsSpan(2)) == 416,
            "Health parity fixture expects a Rejuvenate RSI offer");
        return Scenarios.NativeRejuvenateConsent();
    }
}
