using hds;

namespace NativeQa.Modes;

/// <summary>
/// Stock-client check for the audit rows that carry no executor of their own (docs/ABILITY-COSMETIC-ROWS.md):
/// Coder 18, Game Master 397, Data Miner 537, Data Node Goggles 538, Data Tap 539, Organization 1199 and Resist Infection 75.
/// No stat, grant, cost or outcome is changed; the fixture only owns and loads the rows with their stored prerequisites.
/// </summary>
public sealed class MetadataRowsMode : NativeQaMode
{
    internal static readonly ushort[] Rows = { 18, 397, 537, 538, 539, 1199, 75 };

    public override string Name => "ability-parity-metadata-rows";

    public override NativeQaModePlan Plan { get; } = new(StockAbilityProfile.Profile(Rows).Order().ToArray(), 17000, 2300,
        Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), DevCommands: true,
        extraQaMetadata: new() { ["ScopedStockRows"] = "18,397,537,538,539,1199,75", ["NativeCloseableRows"] = 7 },
        readyMessages: new[]
        {
            "1. Open Status. Expect one row from Resist Infection 75: Debuff Area Radius Bonus +25. Expect no row sourced from Coder 18, Game Master 397, Data Miner 537, Data Node Goggles 538, Data Tap 539 or Organization 1199. PacketLog: no 80 bc modifier record with source 18/397/537/538/539/1199.",
            "2. ?qaunload 397, then ?qaload 397. Repeat for 1199, then 539. Each gives the stock load/unload acknowledgement (80 b3 / 80 b2) and no Status or 80 bc change.",
            "3. Record PASS/FAIL per row with the PacketLog line numbers and the full head. No effect, cost or radius claim is made here; the 1055 radius consumer is the separate howitzer-parent-radius witness."
        });
}
