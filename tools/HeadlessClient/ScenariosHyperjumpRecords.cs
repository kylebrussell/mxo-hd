using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using hds;
using MxoHd.PacketResearch;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task HyperjumpResourceRecords(ScenarioContext context)
    {
        var actor=await context.AddPlayerAsync(new CharacterSeed("JumpRecords"){Level=10,Experience=(int)PlayerProgression.ExperienceForLevel(10),X=17000,Y=495,Z=2300,InnerStrength=100,MaxInnerStrength=100,OwnedLoadedAbilities=new (ushort,ushort)[]{(2,10),(184,1),(11,10)}});
        var sessions=new List<HeadlessPlayer>{actor};var operations=new List<object>();ServerInstance? relog=null;bool passed=false;string step="entry";Exception? failure=null;
        static byte[] Request(bool start,uint sample)
        {
            var b=new byte[start?41:32];b[0]=0x80;b[1]=start?(byte)0xc2:(byte)0xc4;
            BitConverter.GetBytes(start?17100d:17000d).CopyTo(b,2);BitConverter.GetBytes(495d).CopyTo(b,10);BitConverter.GetBytes(2300d).CopyTo(b,18);
            BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(26),0xffff);BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(28),0xfffe);BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(30),0xfffd);
            if(start){BitConverter.GetBytes(197f).CopyTo(b,32);b[36]=1;BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(37),sample);}return b;
        }
        static async Task Barrier(HeadlessPlayer p){p.SkipReceived();p.SendRpc("81 48");await p.WaitForRpcAsync("81 49");}
        static List<ushort> JumpResources(HeadlessPlayer p,int cursor)
        {
            // Independent literal self mask: peak/flags/end/destination, then group3 IS available.
            byte[] prefix=Convert.FromHexString("0200030108008080bc");var values=new List<ushort>();
            foreach(byte[] list in p.World.ViewSnapshot().Skip(cursor))
                for(int at=BytePattern.IndexOf(list,prefix,0);at>=0;at=BytePattern.IndexOf(list,prefix,at+1))
                {
                    int group=at+prefix.Length+4+2+4+24;
                    if(group+3<=list.Length&&list[group]==0x10)values.Add(BitConverter.ToUInt16(list,group+1));
                }
            return values;
        }
        async Task Exercise(HeadlessPlayer p,uint sample)
        {
            await Barrier(p);int rpc=p.World.RpcSnapshot().Count,view=p.World.ViewSnapshot().Count;
            byte[] start=Request(true,sample),cancel=Request(false,0);p.SkipReceived();p.SendRpc(start);
            var expected=new byte[]{0x80,0xc3}.Concat(BitConverter.GetBytes(sample)).ToArray();await p.WaitForRpcAsync(Convert.ToHexString(expected));
            await p.WaitUntilAsync(()=>JumpResources(p,view).Count>0,"Actual combined jump resource record",TimeSpan.FromSeconds(10));
            p.SendRpc(cancel);await Barrier(p);
            var received=p.World.RpcSnapshot().Skip(rpc).ToArray();
            Check.Equal(1,received.Count(r=>r.Bytes.SequenceEqual(expected)),"One exact start clock echo");
            Check.Equal(1,received.Count(r=>r.Bytes.Length>=2&&r.Bytes[0]==0x80&&r.Bytes[1]==0xc3),"Cancel has no fabricated clock echo");
            var resources=JumpResources(p,view);Check.That(resources.Count>0&&resources.All(v=>v==100),"Jump serializes current IS100; no literal284 or new fee");
            Check.That(p.World.ViewSnapshot().Skip(view).Any(v=>Convert.ToHexString(v).Contains(Convert.ToHexString(BitConverter.GetBytes(17000d)))),"Existing cancel destination response retained");
            int cancelRpc=p.World.RpcSnapshot().Count;p.SendRpc(cancel);await Barrier(p);
            Check.Equal(0,p.World.RpcSnapshot().Skip(cancelRpc).Count(r=>r.Bytes.Length>=2&&r.Bytes[0]==0x80&&r.Bytes[1]==0xc3),"Cancel alone does not sample a clock");
            operations.Add(new{p.Handle,Sample=sample,RpcCursor=rpc,ViewCursor=view,SentStart=Convert.ToHexString(start),SentCancel=Convert.ToHexString(cancel),Replies=received.Select(r=>r.Hex),Resources=resources,CancelOnlyCursor=cancelRpc});
        }
        try
        {
            await actor.EnterWorldAsync();actor.SendRpc("0c 01");step="ordinary-start-and-cancel";await Exercise(actor,0xfffffff0);
            string user=await context.Database.ScalarAsync($"SELECT username FROM users JOIN characters USING(userId) WHERE charId={actor.CharId}");
            actor.Dispose();context.Server.Dispose();step="cold-relog";
            using var server=await ServerInstance.StartAsync(context.RepositoryRoot,context.Database.Name,StillNpcs);relog=server;
            using var again=await HeadlessPlayer.LoginAsync(server.Address,user,actor.Handle);sessions.Add(again);await again.EnterWorldAsync();again.SendRpc("0c 01");await Exercise(again,0x80000001);
            foreach(var p in sessions)Check.Equal(0,p.World.Problems.Count,"Jump session parser clean");step="complete";passed=true;
        }
        catch(Exception error){failure=error;throw;}
        finally
        {
            string dll=context.Server.SourceAssembly;string proof=Path.Combine(Path.GetTempPath(),"mxo-hyperjump-records-"+context.Database.Name+".json");
            try{await File.WriteAllTextAsync(proof,JsonSerializer.Serialize(new{Passed=passed,Step=step,Failure=failure?.ToString(),SourceAssembly=dll,SourceConfiguration=Directory.GetParent(Path.GetDirectoryName(dll)!)!.Name,SourceSha256=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(dll))),Operations=operations,Sessions=sessions.Select(p=>new{p.Handle,p.CharId,Rpc=p.World.RpcSnapshot().Select(r=>r.Hex),Views=p.World.ViewSnapshot().Select(Convert.ToHexString),Problems=p.World.Problems.ToArray()}),Debug=context.Server.DebugLog(),Output=context.Server.FullOutput(),RelogDebug=relog?.DebugLog(),RelogOutput=relog?.FullOutput(),NativeUiProven=false,RetailFeeParityProven=false},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("Jump proof before teardown: "+proof);}
            catch(Exception proofError){if(failure==null)throw;Console.Error.WriteLine(proofError);}
        }
    }
}
