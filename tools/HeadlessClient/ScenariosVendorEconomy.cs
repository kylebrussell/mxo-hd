using hds;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task VendorSellRecycleEconomy(ScenarioContext context)
    {
        var p=(await context.AddPlayersInWorldAsync(new CharacterSeed("Economy") {X=19155,Y=495,Z=5313,Info=1000,
            Items=new[]{(11,11666u,1),(12,7332u,8),(13,0x80001c03u,1),(14,42709u,1),(15,11666u,1)}}))[0];
        Task<long> Cash()=>context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={p.CharId}");
        Task<long> Items(int slot)=>context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND slot={slot}");
        p.SendRpc("81 11 01 0b");await p.WaitForTextAsync("Open a vendor");
        p.SendRpc("80 c8 01 00 f0 04 02 00");await p.WaitForRpcAsync("81 0d"); // actual data-loaded vendor82837505
        p.SendRpc("81 11 01 0b");await p.WaitForRpcAsync("81 12 01 92 2d 00 00 00 00 00 10 a8 02 00 00");
        Check.Equal(1000L,await Cash(),"Appraise keeps cash");Check.Equal(1L,await Items(11),"Appraise keeps item");
        p.SendRpc("81 11 01 0d");var ability=await p.WaitForRpcAsync("81 12 01 2e 00 00 00");
        Check.Equal(12600u,BitConverter.ToUInt32(ability.Bytes,11),"Source leveled full-price ability appraisal");
        p.SendRpc("81 11 00 0b");await p.WaitForRpcAsync("81 12 00 92 2d 00 00");
        Check.Equal(1680L,await Cash(),"Actual sale paid once");Check.Equal(0L,await Items(11),"Selected byte slot removed");
        p.SendRpc("81 11 00 0b");await p.WaitForTextAsync("nothing to sell");Check.Equal(1680L,await Cash(),"Duplicate sale cannot pay");
        p.SendRpc("5d 0c 00 01 01");await p.WaitForRpcAsync("5f 0c 00 01");Check.Equal(1720L,await Cash(),"All eight ordinary Code Bits recycle for40");
        p.SendRpc("5d 0d 00 01 01");await p.WaitForRpcAsync("5f 0d 00 01");Check.Equal(2220L,await Cash(),"Leveled ability recycle uses base VendorPrice, not buyback premium");
        p.SendRpc("5d 0e 00 01 00");await p.WaitForTextAsync("Non-recycle removal is not supported");Check.Equal(1L,await Items(14),"Unproven flag0 is nondestructive");
        p.SendRpc("5d 0e 00 01 01");await p.WaitForRpcAsync("5f 0e 00 01");Check.Equal(2220L,await Cash(),"Explicit VendorPrice0 remains0");
        await context.Database.ExecuteAsync($"ALTER TABLE characters ADD CONSTRAINT economy_reject_credit CHECK(handle<>'Economy' OR cash<2221)");
        p.SendRpc("81 11 00 0f");await p.WaitForTextAsync("sale could not be completed");
        Check.Equal(1L,await Items(15),"Rejected account credit rolls inventory removal back");Check.Equal(2220L,await Cash(),"Rejected sale leaves cash");
        await context.Database.ExecuteAsync("ALTER TABLE characters DROP CONSTRAINT economy_reject_credit");
        p.SendRpc("81 11 00 0f");await p.WaitForRpcAsync("81 12 00");Check.Equal(2900L,await Cash(),"Same item can sell after rollback");
        // Account was deliberately changed behind the live cache: transaction must use authoritative signed DB cash.
        await context.Database.ExecuteAsync($"UPDATE characters SET cash=2147483642 WHERE charId={p.CharId}; INSERT INTO inventory(charId,goid,slot,count) VALUES({p.CharId},11666,16,1)");
        p.SendRpc("81 11 00 10");await p.WaitForRpcAsync("81 12 00 92 2d 00 00 00 00 00 10 05 00 00 00");
        Check.Equal((long)int.MaxValue,await Cash(),"Signed account saturates without wrap");
        await context.Database.ExecuteAsync($"INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES({p.CharId},400,{CodeStorage.BaseValue(7332)},7,0)");
        await context.Database.ExecuteAsync($"UPDATE characters SET cash=1000 WHERE charId={p.CharId}");
        await GoToLoadingAreaAsync(p);
        p.SendRpc("5d 90 01 00 01");await p.WaitForRpcAsync("5f 90 01 00");
        Check.Equal(0L,await context.Database.LongAsync($"SELECT COUNT(*) FROM char_abilities WHERE char_id={p.CharId} AND slot=400"),"Storage shares atomic payout without a backend fork");
        Check.Equal(1005L,await Cash(),"Native type2e storage quote remains one unit, not bag count multiplier");
        Check.Equal(0,p.World.Problems.Count,"Economy wire protocol");
    }
}
