using HeadlessClient;
using hds;

namespace NativeQa.Modes;

/// <summary>Disposable fixture configuration; loaded by NativeQaModes without a shared mode switch.</summary>
public sealed class DeathPresentationMode : NativeQaMode
{
    private const int StreetX = 17000, StreetY = 495, StreetZ = 2300;
    private const int RejuvenateRsiCost = 150;
    private const int HelperInnerStrength = 3 * RejuvenateRsiCost;

    public override string Name => "death-presentation";
    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2,17,325,326,375,416,967,10,1064,133 }, StreetX, StreetZ,
        Array.Empty<string>(), new[] { $"UPDATE characters SET y={StreetY} WHERE charId=110;" },
        new[] { $"UPDATE characters SET innerStrC={HelperInnerStrength},innerStrM={HelperInnerStrength} WHERE charId BETWEEN 6111 AND 6114;" },
        DevCommands: true,
        readyMessages: new[] { "Death QA starts outdoors. Each helper has 450 Inner Strength for repeat 416 casts." });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var database = context.OwnedDatabase ?? throw new InvalidOperationException("Death QA requires a disposable database.");
        var server = context.ActualServer ?? throw new InvalidOperationException("Death QA requires a live server.");
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Death QA requires repository data.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        Check.That(NavMesh.Locate(1, StreetX, StreetY, StreetZ, out var street) && !street.indoors &&
            Math.Abs(street.floorY + 95 - StreetY) < 1, "CSR_Tester starts on open street NAV");
        foreach (string name in context.HelperNames)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, name, name);
            context.Helpers.Add(name, helper);
            await helper.EnterWorldAsync();
            Check.That(helper.World.CurrentOwnViewLife().OwnViewReady && helper.World.Problems.Count == 0,
                $"{name} entered the fixture");
            Check.Equal(1L, await database.LongAsync(
                $"SELECT COUNT(*) FROM characters JOIN char_abilities ON char_id=charId WHERE charId={helper.CharId} " +
                $"AND healthC>0 AND is_online=1 AND innerStrC>={2 * RejuvenateRsiCost} AND innerStrM>={2 * RejuvenateRsiCost} " +
                $"AND ability_id={0x80000000L | 416L << 10} AND is_loaded=1"),
                $"{name} is alive and can cast 416 twice");
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }
        await context.Helpers["QAFriend"].WaitForViewNamedAsync("QAOther");
        await context.Helpers["QAOther"].WaitForViewNamedAsync("QAFriend");
        return Array.Empty<byte>();
    }
}
