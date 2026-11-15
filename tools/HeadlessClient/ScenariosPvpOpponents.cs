using System.Security.Cryptography;
using System.Text.Json;
using hds;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task PvpOpponentsEntry(ScenarioContext context)
    {
        var players=new List<HeadlessPlayer>();var entries=new List<object>();var observations=new List<object>();var requests=new List<object>();
        string step="seed-zero-flags";Exception? failure=null;bool passed=false;
        void Send(HeadlessPlayer p,string label,string raw){requests.Add(new{Label=label,p.Handle,Raw=raw,RpcCursor=p.World.RpcSnapshot().Count,ViewCursor=p.World.ViewSnapshot().Count});p.SendRpc(raw);}
        try
        {
            foreach(var item in new[]{("CsrOpponent",PvpOpponentsFixture.X,(byte)1),("EnemyOpponent",PvpOpponentsFixture.EnemyX,(byte)2),("FriendOpponent",PvpOpponentsFixture.FriendX,(byte)1)})
            {
                var p=await context.AddPlayerAsync(new CharacterSeed(item.Item1){X=item.Item2,Y=PvpOpponentsFixture.Y,Z=PvpOpponentsFixture.Z,Rotation=0,Level=50,
                    Experience=checked((int)PlayerProgression.ExperienceForLevel(50)),Organization=item.Item3,Health=5000,MaxHealth=5000,InnerStrength=2000,MaxInnerStrength=2000,
                    OwnedLoadedAbilities=new[]{((ushort)2,(ushort)50),((ushort)17,(ushort)50)}});
                players.Add(p);Check.Equal(0L,await context.Database.LongAsync($"SELECT pvpflag FROM characters WHERE charId={p.CharId}"),"Database starts unflagged; no seeded opponent shortcut");
                entries.Add(new{BeforeEntryDb=await context.Database.ExecuteAsync($"SELECT level,exp,alignment,pvpflag,crewId,x,y,z FROM characters WHERE charId={p.CharId}"),Wire=await PvpOpponentsFixture.EnterAsync(p)});
            }
            var csr=players[0];var enemy=players[1];var friend=players[2];
            step="mutual-live-entry-views";
            foreach(var observer in players)foreach(var target in players.Where(p=>p!=observer))
                observations.Add(new{Observer=observer.Handle,Target=target.Handle,View=PvpOpponentsFixture.Observe(await observer.WaitForViewNamedAsync(target.Handle),(byte)(target==enemy?2:1),target.CharId)});
            var friendView=await csr.WaitForViewNamedAsync(friend.Handle);var enemyView=await csr.WaitForViewNamedAsync(enemy.Handle);var csrForEnemy=await enemy.WaitForViewNamedAsync(csr.Handle);var enemyForFriend=await friend.WaitForViewNamedAsync(enemy.Handle);
            string friendHandle=HandleOf(friendView),enemyHandle=HandleOf(enemyView),csrHandle=HandleOf(csrForEnemy);
            int friendHealthCursor=friend.World.ViewSnapshot().Count;ushort friendHealth=BitConverter.ToUInt16(friend.World.CreationSnapshot().Last(v=>v.ViewId==2).Attributes["Health"]);
            step="same-org-negative";int refusalStart=csr.World.RpcSnapshot().Count;csr.SkipReceived();Send(csr,step,$"40 {friendHandle} 01 6b");
            await csr.WaitForRpcAsync($"4d 18 00 00 00 {friendHandle}");csr.SendRpc("81 48");await csr.WaitForRpcAsync("81 49");
            Check.Equal(0,csr.World.RpcSnapshot().Skip(refusalStart).Count(r=>r.Bytes[0]==0x46||r.Bytes[0]==0x45),"Same-org attack did not enter combat despite both flags");
            Check.That(PvpOpponentsFixture.HealthHistory(friend.World.ViewSnapshot(),friendHealthCursor,2,true).All(h=>h>=friendHealth),"Same-org refusal does not damage the friend");
            observations.Add(new{SameOrgRpcStart=refusalStart,FriendHealthCursor=friendHealthCursor,Before=friendHealth,History=PvpOpponentsFixture.HealthHistory(friend.World.ViewSnapshot(),friendHealthCursor,2,true)});
            int ownStart=enemy.World.ViewSnapshot().Count,seenStart=csr.World.ViewSnapshot().Count,friendStart=friend.World.ViewSnapshot().Count;
            ushort before=BitConverter.ToUInt16(enemy.World.CreationSnapshot().Last(v=>v.ViewId==2).Attributes["Health"]);
            step="normal-nonduel-opening";int start=csr.World.RpcSnapshot().Count;csr.SkipReceived();enemy.SkipReceived();Send(csr,step,$"40 {enemyHandle} 01 6b");
            await csr.WaitForRpcAsync($"46 {enemyHandle} 01");var round=await csr.WaitForRpcAsync("45");Check.Equal(89,round.Bytes.Length,"Actual round layout");
            ushort damage=DamageToOpponent(round);Check.That(damage>0&&damage<before,"Existing opening opportunity attack produces bounded positive damage");
            ushort expected=(ushort)(before-damage);
            await enemy.WaitUntilAsync(()=>PvpOpponentsFixture.HealthHistory(enemy.World.ViewSnapshot(),ownStart,2,true).Contains(expected),"Exact current-opening damage in victim HP history",TimeSpan.FromSeconds(10));
            await csr.WaitUntilAsync(()=>PvpOpponentsFixture.HealthHistory(csr.World.ViewSnapshot(),seenStart,enemyView.ViewId,false).Contains(expected),"Attacker sees exact opening HP",TimeSpan.FromSeconds(10));
            await friend.WaitUntilAsync(()=>PvpOpponentsFixture.HealthHistory(friend.World.ViewSnapshot(),friendStart,enemyForFriend.ViewId,false).Contains(expected),"Unrelated observer sees same exact opening HP",TimeSpan.FromSeconds(10));
            observations.Add(new{OpeningRpcStart=start,Opening=Convert.ToHexString(round.Bytes),Before=before,Damage=damage,ExpectedHealth=expected,VictimCursor=ownStart,AttackerCursor=seenStart,ObserverCursor=friendStart,
                VictimHealthHistory=PvpOpponentsFixture.HealthHistory(enemy.World.ViewSnapshot(),ownStart,2,true),AttackerHealthHistory=PvpOpponentsFixture.HealthHistory(csr.World.ViewSnapshot(),seenStart,enemyView.ViewId,false)});
            step="ordinary-paired-withdrawal";csr.SkipReceived();enemy.SkipReceived();Send(csr,step,"44 00");Send(enemy,step,"44 00");
            //Both ordinary withdrawal intentions make the existing next-round bothWithdraw branch deterministic.
            await csr.WaitForRpcAsync($"46 {enemyHandle} 00",TimeSpan.FromSeconds(20));await enemy.WaitForRpcAsync($"46 {csrHandle} 00",TimeSpan.FromSeconds(20));
            foreach(var p in players)
            {
                Check.Equal(0,p.World.RpcSnapshot().Count(r=>r.Bytes[0]==0x55),"No duel begin/assigned duel path");
                Check.Equal(0,p.World.Problems.Count,"Each player's parser clean");
                Check.Equal(0L,await context.Database.LongAsync($"SELECT pvpflag FROM characters WHERE charId={p.CharId}"),"Runtime entry flags were not database seeds or a flag command");
            }
            Check.That(!context.Server.DebugLog().Contains("[DUEL]"),"No duel-handler path");
            step="complete";passed=true;
        }
        catch(Exception error){failure=error;throw;}
        finally
        {
            string stem=Path.Combine(Path.GetTempPath(),"mxo-pvp-opponents-"+context.Database.Name);
            try
            {
                string dll=context.Server.SourceAssembly;
                await File.WriteAllTextAsync(stem+".json",JsonSerializer.Serialize(new{Passed=passed,Step=step,Failure=failure?.ToString(),Database=context.Database.Name,
                    SourceAssembly=dll,SourceConfiguration=Directory.GetParent(Path.GetDirectoryName(dll)!)!.Name,SourceSha256=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(dll))),
                    Entries=entries,Requests=requests,Observations=observations,Sessions=players.Select(p=>new{p.Handle,p.CharId,Rpcs=p.World.RpcSnapshot().Select(r=>r.Hex).ToArray(),Views=p.World.ViewSnapshot().Select(Convert.ToHexString).ToArray(),Problems=p.World.Problems.ToArray()}),
                    Debug=context.Server.DebugLog(),Output=context.Server.FullOutput(),NativeCsrEntryProven=false,ConditionalPowerlessPvpProven=false},new JsonSerializerOptions{WriteIndented=true}));
                Console.WriteLine("PvP opponent proof before teardown: "+stem+".json");
            }
            catch(Exception proofError){if(failure==null)throw;Console.Error.WriteLine("Proof failure preserving original: "+proofError);}
        }
    }
}
