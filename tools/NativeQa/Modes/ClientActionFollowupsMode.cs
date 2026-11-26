using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class ClientActionFollowupsMode : NativeQaMode
{
    private const uint HealthPill = 2399;
    private const uint Revolver = 700;
    private const int ArchiveSlot = 400;

    public override string Name => "client-action-followups";
    public override NativeQaModePlan Plan { get; } = new(
        StockAbilityProfile.Profile(2, 17, 37, 276, 277).Order().ToArray(), 23828, 13984,
        Array.Empty<string>(),
        new[]
        {
            "DELETE FROM inventory WHERE charId=110 AND slot BETWEEN 1 AND 96",
            $"INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,613,3,1,0),(110,{HealthPill},5,2,0)",
            "UPDATE characters SET y=495,cash=250 WHERE charId=110",
            $"DELETE FROM char_abilities WHERE char_id=110 AND slot IN ({ArchiveSlot},{ArchiveSlot + 1})",
            $"INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES(110,{ArchiveSlot},{HealthPill << 7},2,0)," +
                $"(110,{ArchiveSlot + 1},{Revolver << 7},2,0)"
        },
        new[] { $"UPDATE char_abilities SET level=18 WHERE char_id=110 AND ability_id={AbilityMessages.AbilityGameObject(276)}" },
        DevCommands: false, GmCharacter110: false, FixedCallbackRolls: false,
        readyMessages: new[]
        {
            "Start at Mara Central (23828,495,13984). Bag slot 3 is the Item Decompiler, slot 5 has two Health Pills, and Info is 250. No compiler, hotbar or roll aid is needed.",
            "1. Bag slot 3 > Activate Item > select the slot-5 pills. [DECOMPILE] item=2399 skill=18 complexity=1 success=True, then 81 30 and 81 2f success. Slot 5 goes from 2 to 1. After 10 seconds repeat and confirm slot 5 disappears. Both passes preserve Info.",
            "2. Mara hardline radial > Loading Area > code archive. Compile the Health Pill code (slot 400) twice. 60 FromCrafting=0, 61 00, [COMPILE60] item=2399 result=0 cost=9, remaining 1 then 0, two pills in the bag and Info 250 to 241 to 232. Then compile the Revolver code (slot 401) twice. First: 61 00, one Revolver, one charge left, 112 Info. Second: 61 04, same charge and Info. Direct archive Compile needs no tool or skill under the local policy.",
            "3. Select the Tutorial from the Loading Area picker and enter it. Click Skip Tutorial and confirm the exit. The stock handoff should select Richland hardline 153 and load the city without another hardline pick. Check [TUTORIAL] Jacking into Richland at hardline 153 and report whether 81 01 or the existing 05 path initiated it. An ordinary Loading Area exit afterwards should still wait at the picker."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var db = context.OwnedDatabase ?? throw new InvalidOperationException("An owned database is required.");
        Check.Equal(2L, await db.LongAsync($"SELECT count FROM inventory WHERE charId=110 AND slot=5 AND goid={HealthPill}"),
            "Two pills in the decompile source slot");
        Check.Equal(1L, await db.LongAsync("SELECT count FROM inventory WHERE charId=110 AND slot=3 AND goid=613"),
            "An owned Item Decompiler in slot 3");
        Check.Equal(2L, await db.LongAsync($"SELECT level FROM char_abilities WHERE char_id=110 AND slot={ArchiveSlot} " +
            $"AND ability_id={HealthPill << 7} AND is_loaded=0"), "Two ordinary code charges in archive");
        Check.Equal(2L, await db.LongAsync($"SELECT level FROM char_abilities WHERE char_id=110 AND slot={ArchiveSlot + 1} " +
            $"AND ability_id={Revolver << 7} AND is_loaded=0"), "Two ordinary Revolver code charges in archive");
        return Array.Empty<byte>();
    }
}
