using System.Security.Cryptography;
using System.Text.Json;
using hds;
using hds.shared;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task VendorBuybackAuthority(ScenarioContext context)
    {
        HeadlessPlayer? p=null;Exception? failure=null;string step="entry";var proof=new List<object>();var sessions=new List<object>();
        Task<long> Cash()=>context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={p!.CharId}");
        Task<long> Receipt()=>context.Database.LongAsync($"SELECT COUNT(*) FROM vendor_last_sale WHERE char_id={p!.CharId}");
        void Record()=>sessions.Add(new{p!.Handle,p.CharId,Rpc=p.World.RpcSnapshot().Select(r=>Convert.ToHexString(r.Bytes)).ToArray(),Views=p.World.ViewSnapshot().Select(Convert.ToHexString).ToArray(),Problems=p.World.Problems.ToArray()});
        async Task Open(){p!.SendChat("?clientactions vendor");await p.WaitForRpcAsync("81 0d");}
        async Task<byte[]> Operation(string rpc,bool buy,bool accepted)
        {
            int cursor=p!.World.RpcSnapshot().Count;p.SendRpc(rpc);
            bool Match(byte[] b)=>b.Length>=2&&b[0]==0x81&&b[1]==(accepted?(buy?0x0f:0x12):0x10);
            await p.WaitUntilAsync(()=>p.World.RpcSnapshot().Skip(cursor).Any(r=>Match(r.Bytes)),"Current vendor operation",TimeSpan.FromSeconds(10));
            var window=p.World.RpcSnapshot().Skip(cursor).Select(r=>r.Bytes).ToArray();
            var reply=window.Last(Match);
            if(accepted&&buy)Check.Equal((byte)1,reply[^1],"Buyback final1 clears last-sale UI");
            if(!accepted)Check.That(!window.Any(b=>b.Length>1&&(b[0]==0x5e||b[0]==0x5f||(b[0]==0x80&&(b[1]==0xe7||b[1]==0xe4))||(b[0]==0x81&&(b[1]==0x0f||b[1]==0x12)))),"Refusal has no optimistic inventory/cash/success records");
            proof.Add(new{Step=step,Cursor=cursor,Rpc=rpc,Accepted=accepted,Window=window.Select(Convert.ToHexString).ToArray()});return reply;
        }
        try
        {
            p=(await context.AddPlayersInWorldAsync(new CharacterSeed("BuybackAuthority"){X=17000,Y=495,Z=2300,Info=1000,Items=new[]{(11,11666u,1),(12,7332u,8),(13,0x80001c03u,1),(14,11666u,1)}}))[0];
            await context.Database.ExecuteAsync($"UPDATE inventory SET count=0,purity=872415232 WHERE charId={p.CharId} AND slot=11");
            await context.Database.InstallVendorBuybackFailureAsync(p.CharId,false);await context.Database.RemoveVendorBuybackFailureAsync(false);
            await context.Database.InstallVendorBuybackFailureAsync(p.CharId,true);await context.Database.RemoveVendorBuybackFailureAsync(true);
            await Open();step="raw-zero-sale";
            proof.Add(new{Step="schema",Engines=await context.Database.ScalarAsync("SELECT GROUP_CONCAT(CONCAT(TABLE_NAME,':',ENGINE) ORDER BY TABLE_NAME) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME IN('characters','inventory','vendor_last_sale')"),Isolation=await context.Database.ScalarAsync("SELECT @@transaction_isolation")});
            string original=await context.Database.ScalarAsync($"SELECT CONCAT(goid,':',count,':',purity) FROM inventory WHERE charId={p.CharId} AND slot=11");
            var sell=await Operation("81 11 00 0b",false,true);Check.Equal(680u,BitConverter.ToUInt32(sell,11),"Same actual paid sale total");
            Check.Equal(1680L,await Cash(),"Sale credits once");Check.Equal(1L,await Receipt(),"Sale and receipt committed together");
            step="same-vendor-close-reopen";p.SendRpc("81 10 00 00 00 00 01");await Open();
            await Operation("81 0e 00 00 00 00 01",true,true);Check.Equal(1000L,await Cash(),"Repurchase exact paid total");
            Check.Equal(original,await context.Database.ScalarAsync($"SELECT CONCAT(goid,':',count,':',purity) FROM inventory WHERE charId={p.CharId} AND goid=11666 AND purity=872415232"),"Exact raw count0/purity restored");
            Check.Equal(0L,await Receipt(),"Receipt consumed");step="duplicate-buyback";await Operation("81 0e 00 00 00 00 01",true,false);
            step="stack-sale";await Operation("81 11 00 0c",false,true);long stackCash=await Cash();
            string session=await context.Database.ScalarAsync($"SELECT session_token FROM vendor_last_sale WHERE char_id={p.CharId}");
            step="poor";await context.Database.ExecuteAsync($"UPDATE characters SET cash=0 WHERE charId={p.CharId}");
            await Operation("81 0e 00 00 00 00 01",true,false);Check.Equal(1L,await Receipt(),"Poor refusal preserves receipt");
            await context.Database.ExecuteAsync($"UPDATE characters SET cash={stackCash} WHERE charId={p.CharId}");
            step="full";await context.Database.ExecuteAsync(string.Join(";",Enumerable.Range(1,96).Select(s=>$"INSERT INTO inventory(charId,goid,slot,count,purity) SELECT {p.CharId},11666,{s},0,0 WHERE NOT EXISTS(SELECT 1 FROM inventory WHERE charId={p.CharId} AND slot={s})")));
            await Operation("81 0e 00 00 00 00 01",true,false);Check.Equal(stackCash,await Cash(),"Full bag spends nothing");Check.Equal(1L,await Receipt(),"Full bag keeps receipt");
            await context.Database.ExecuteAsync($"DELETE FROM inventory WHERE charId={p.CharId} AND slot=12");
            step="buyback-late-rollback";await context.Database.InstallVendorBuybackFailureAsync(p.CharId,true);
            try{await Operation("81 0e 00 00 00 00 01",true,false);Check.Equal(stackCash,await Cash(),"Late receipt-delete failure rolls cash back");Check.Equal(0L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND slot=12"),"Late receipt-delete failure rolls insertion back");Check.Equal(1L,await Receipt(),"Failed consume preserves receipt");}
            finally{await context.Database.RemoveVendorBuybackFailureAsync(true);}
            step="stack-buyback";await Operation("81 0e 00 00 00 00 01",true,true);Check.Equal(8L,await context.Database.LongAsync($"SELECT count FROM inventory WHERE charId={p.CharId} AND slot=12"),"Whole stack restored without merge");
            await context.Database.ExecuteAsync($"DELETE FROM inventory WHERE charId={p.CharId} AND slot NOT IN(1,12,13,14)");
            step="latest-receipt-baseline";await Operation("81 11 00 01",false,true);
            string priorReceipt=await context.Database.ScalarAsync($"SELECT CONCAT(original_id,':',raw_goid,':',raw_count,':',purity,':',paid) FROM vendor_last_sale WHERE char_id={p.CharId}");
            step="sale-late-rollback";await context.Database.InstallVendorBuybackFailureAsync(p.CharId,false);long before=await Cash();
            try{await Operation("81 11 00 0e",false,false);Check.Equal(before,await Cash(),"Failed receipt write rolls sale credit back");Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND slot=14"),"Failed receipt write restores sold item");Check.Equal(priorReceipt,await context.Database.ScalarAsync($"SELECT CONCAT(original_id,':',raw_goid,':',raw_count,':',purity,':',paid) FROM vendor_last_sale WHERE char_id={p.CharId}"),"Failed second sale preserves exact previous receipt");}
            finally{await context.Database.RemoveVendorBuybackFailureAsync(false);}
            step="ability-sale";await context.Database.ExecuteAsync($"UPDATE inventory SET goid=-2147476477 WHERE charId={p.CharId} AND slot=13");await Operation("81 11 00 0d",false,true);
            session=await context.Database.ScalarAsync($"SELECT session_token FROM vendor_last_sale WHERE char_id={p.CharId}");
            Check.Equal(-2147476477L,await context.Database.LongAsync($"SELECT raw_goid FROM vendor_last_sale WHERE char_id={p.CharId}"),"Next successful sale replaces previous receipt with exact signed descriptor");
            // Independent connections use the same authoritative API; no queue or gameplay callback is invoked inside SQL.
            var config=new ServerConfig(Path.Combine(context.RepositoryRoot,"hds","bin","Debug","net8.0","Config.xml.dist"));config.LoadDbParams();config.dbParams.DatabaseName=context.Database.Name;
            var previous=Store.config;Store.config=config;
            try
            {
                var db=new hds.databases.MyWorldDbAccess();step="cross-vendor-session";
                Check.That(!db.BuyBackVendorItem(p.CharId,session,99).Committed,"Other vendor cannot claim receipt");
                Check.That(!db.BuyBackVendorItem(p.CharId,new string('f',32),98).Committed,"Other session cannot claim receipt");
                step="two-connection-buyback";long cash=await Cash();using var barrier=new Barrier(2);
                Task<VendorBuyback.Result> Buy()=>Task.Run(()=>{Check.That(barrier.SignalAndWait(TimeSpan.FromSeconds(10)),"Buyback race barrier");return db.BuyBackVendorItem(p.CharId,session,98);});
                var result=await Task.WhenAll(Buy(),Buy());Check.Equal(1,result.Count(r=>r.Committed),"One receipt may be consumed only once");
                Check.Equal(cash-result.Single(r=>r.Committed).Paid,await Cash(),"Race debits exactly once");
                Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND goid=-2147476477 AND count=1"),"Ability descriptor retained exactly");
                step="two-connection-sale";using var saleBarrier=new Barrier(2);
                Task<VendorBuyback.Result> Sell()=>Task.Run(()=>{Check.That(saleBarrier.SignalAndWait(TimeSpan.FromSeconds(10)),"Sale race barrier");return db.SellVendorItem(p.CharId,14,session,98);});
                var sales=await Task.WhenAll(Sell(),Sell());Check.Equal(1,sales.Count(r=>r.Committed),"One exact row may be sold once");
                proof.Add(new{Step="independent-races",BuyAccepted=result.Count(r=>r.Committed),SellAccepted=sales.Count(r=>r.Committed)});
            }
            finally{Store.config=previous;}
            step="relog-expiry";uint id=p.CharId;p.SendRpc("80 fe 00");await p.WaitForRpcAsync("80 fd");Record();Check.Equal(0,p.World.Problems.Count,"First session parser clean before relog");p.Dispose();p=null;
            await Task.Delay(TimeSpan.FromSeconds(33));p=await HeadlessPlayer.LoginAsync(context.Server.Address,"user"+id,"BuybackAuthority");await p.EnterWorldAsync();await Open();
            long relogCash=await Cash();await Operation("81 0e 00 00 00 00 01",true,false);Check.Equal(relogCash,await Cash(),"Old world-session receipt cannot refund after relog");
            Check.Equal(0,p.World.Problems.Count,"Buyback parser clean");step="complete";
        }
        catch(Exception e){failure=e;throw;}
        finally
        {
            try
            {
                if(p!=null){Record();Check.Equal(0,p.World.Problems.Count,"Final buyback parser clean");}
                string dll=context.Server.SourceAssembly;string stem=Path.Combine(Path.GetTempPath(),"mxo-vendor-buyback-"+context.Database.Name);
                File.WriteAllText(stem+".json",JsonSerializer.Serialize(new{Step=step,Failure=failure?.ToString(),Database=context.Database.Name,Proof=proof,Sessions=sessions,SourceAssembly=dll,SourceConfiguration=Directory.GetParent(Path.GetDirectoryName(dll)!)!.Name,SourceSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(dll))),NativeUiProven=false,RetailLifetimeOrPriceProven=false},new JsonSerializerOptions{WriteIndented=true}));
                File.WriteAllText(stem+".log",context.Server.FullOutput()+"\n---Debug---\n"+context.Server.DebugLog());Console.WriteLine("  BUYBACK evidence: "+stem+" step="+step);
            }
            finally{p?.Dispose();}
        }
    }
}
