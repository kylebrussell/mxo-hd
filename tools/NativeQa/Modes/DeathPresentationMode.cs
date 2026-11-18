using HeadlessClient;

namespace NativeQa.Modes;

/// <summary>Disposable fixture configuration; loaded by NativeQaModes without a shared mode switch.</summary>
public sealed class DeathPresentationMode : NativeQaMode
{
    public override string Name => "death-presentation";
    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2,17,325,326,375,416,967,10,1064,133 }, 23828, 13984,
        new string[] {  },
        new string[] {  },
        new string[] {  },
        DevCommands: true, GmCharacter110: false, FixedCallbackRolls: false, SeedDisguiseTool: false,
        Bootstrap: "", StealthShieldQa: false, HealthCallbackQa: false, CompileItemsQa: false);

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var database = context.OwnedDatabase ?? throw new InvalidOperationException("Death QA requires a disposable database.");
        foreach (string name in context.HelperNames)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, name, name);
            context.Helpers.Add(name, helper);
            await helper.EnterWorldAsync();
            Check.That(helper.World.CurrentOwnViewLife().OwnViewReady && helper.World.Problems.Count == 0,
                $"{name} entered the fixture");
            Check.Equal(1L, await database.LongAsync(
                $"SELECT COUNT(*) FROM characters JOIN char_abilities ON char_id=charId WHERE charId={helper.CharId} " +
                $"AND healthC>0 AND is_online=1 AND ability_id={0x80000000L | 416L << 10} AND is_loaded=1"),
                $"{name} is alive and has the resurrection ability loaded");
        }
        return Array.Empty<byte>();
    }
}
