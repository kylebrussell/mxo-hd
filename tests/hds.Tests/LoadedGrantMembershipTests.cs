using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Security.Cryptography;
using MxoHd.PacketResearch;
using hds;
namespace hds.Tests;
public class LoadedGrantMembershipTests
{
    private static string Root { get { var p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds.sln")))p=Path.GetDirectoryName(p)!;return p; } }
    [Fact] public void OnlyCaptureQualifiedZeroValuedPairsAreAdmittedAndEncodeExactly()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(Root,"research/protocol/loaded-grant-membership.json")));
        var rows=doc.RootElement.GetProperty("relations").EnumerateArray().ToArray();Assert.Equal(25,rows.Length);
        Assert.Equal(25,AbilityGrantRegistration.ObservedPairs.Count);
        foreach(var row in rows)
        {
            ushort parent=row.GetProperty("parent").GetUInt16(),child=row.GetProperty("child").GetUInt16();
            Assert.True(AbilityGrantRegistration.Handles(parent,child));
            var sample=row.GetProperty("example");byte[] literal=Convert.FromHexString("80bc"+sample.GetProperty("Payload").GetString()!.Replace(" ",""));
            Assert.Equal(literal,AbilityGrantRegistration.Record(new(parent,child,0),sample.GetProperty("Operation").GetInt32()==1));
        }
        foreach(var pair in AbilityGrantRegistration.ObservedPairs)Assert.Contains(rows,r=>r.GetProperty("parent").GetUInt16()==pair.Source&&r.GetProperty("child").GetUInt16()==pair.Target);
        foreach(var pair in new (ushort,ushort)[]{(2,17),(37,970),(277,969),(151,1111),(120,1114),(503,1137),(42769,980),(1236,1238),(143,1061)})
            Assert.False(AbilityGrantRegistration.Handles(pair.Item1,pair.Item2));
        Assert.True(AbilityGrantRegistration.Handles(4452,5183)); // actual ability aliases, not additional ownership
    }
    [Fact] public void ActualReadinessRegistersSeveralParentsOnceAndUnloadsOnlyTheRemovedSource()
    {
        var c=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));c.playerData=new ClientData();c.messageQueue=new();
        typeof(WorldClient).GetField("<TransportFence>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(c,new WorldSendFence());
        c.playerData.setDistrictId(1);c.playerData.setOnWorld(true);
        foreach(ushort parent in new ushort[]{64,137,143,356})c.playerData.passives.Loaded[parent]=50;
        AbilityGrantRegistration.LoadedChanged(c);Assert.Empty(c.messageQueue.RPCMessagesQueue);
        AbilityGrantRegistration.LoadingComplete(c,new byte[]{1});Assert.Equal(8,c.messageQueue.RPCMessagesQueue.Count);
        AbilityGrantRegistration.LoadingComplete(c,new byte[]{1});Assert.Equal(8,c.messageQueue.RPCMessagesQueue.Count);
        c.playerData.passives.Loaded[64]=1;AbilityGrantRegistration.LoadedChanged(c);Assert.Equal(8,c.messageQueue.RPCMessagesQueue.Count);
        c.playerData.passives.Loaded.Remove(143);AbilityGrantRegistration.LoadedChanged(c);Assert.Equal(10,c.messageQueue.RPCMessagesQueue.Count);
        var packets=c.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>().Select(m=>m.content.Skip(1).ToArray()).ToArray();
        Assert.Equal(Convert.FromHexString("80b32404"),packets[^2]);
        Assert.Equal(Convert.FromHexString("80bc45032404008f0000002404000000000100000000"),packets[^1]);
        AbilityGrantRegistration.LoadedChanged(c);Assert.Equal(10,c.messageQueue.RPCMessagesQueue.Count);
    }
    [Fact] public void ObservedPairsCannotBeMutatedAndScaledOrUnseenCatalogPairsStayOut()
    {
        var writable=Assert.IsAssignableFrom<IList<(ushort Source,ushort Target)>>(AbilityGrantRegistration.ObservedPairs);
        Assert.True(writable.IsReadOnly);Assert.Throws<NotSupportedException>(()=>writable[0]=(2,17));
        Assert.Throws<NotSupportedException>(()=>writable.Add((151,1111)));
        var admitted=AbilityCatalog.All.Values.SelectMany(parent=>AbilityProgression.GrantsOf(parent.Id).Where(child=>AbilityGrantRegistration.Handles(parent.Id,child)).Select(child=>(parent.Id,child))).OrderBy(p=>p.Id).ToArray();
        Assert.Equal(AbilityGrantRegistration.ObservedPairs.OrderBy(p=>p.Source).Select(p=>(p.Source,p.Target)),admitted);
        Assert.False(AbilityGrantRegistration.Handles(151,1111));Assert.Equal((ushort)0,AbilityProgression.GrantLevel(151,50)); // existing scaled-grant issue explicitly deferred
    }
    [Fact] public void EveryAdmittedPairHasHashPinnedPositiveFramedCaptureEvidence()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(Root,"research/protocol/loaded-grant-membership.json")));
        var files=new Dictionary<string,PacketDumpFileSummary>();
        foreach(var row in doc.RootElement.GetProperty("relations").EnumerateArray())
        {
            var sample=row.GetProperty("example");string relative=sample.GetProperty("File").GetString()!.Replace('\\',Path.DirectorySeparatorChar);
            string path=Path.Combine(Root,relative);byte[] bytes=File.ReadAllBytes(path);
            string hash=Convert.ToHexString(SHA256.HashData(bytes));string lf=Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(System.Text.Encoding.UTF8.GetString(bytes).Replace("\r\n","\n"))));
            Assert.Contains(hash,new[]{sample.GetProperty("FileSha256").GetString(),sample.GetProperty("FileLfSha256").GetString()});
            Assert.Equal(sample.GetProperty("FileLfSha256").GetString(),lf);
            if(!files.TryGetValue(path,out var framed))files[path]=framed=PacketResearcher.SummarizePacketDump(path,Array.Empty<RpcHeaderEntry>(),Root);
            Assert.Contains(framed.ManageBonusPayloads,p=>p.Line==sample.GetProperty("Line").GetInt32()&&p.PayloadHex.Equals(sample.GetProperty("Payload").GetString(),StringComparison.OrdinalIgnoreCase)&&
                p.NativeFields is {Type:0x45,ValueBits:0,DurationBits:0} n&&n.Locator==((uint)row.GetProperty("child").GetUInt16()<<8|3)&&
                n.SourceId==row.GetProperty("parent").GetUInt16()&&n.TargetId==row.GetProperty("child").GetUInt16()&&n.AddRemoveType==sample.GetProperty("Operation").GetByte());
        }
    }
    [Fact] public void DiagnosticModeLoadsParentsOnlyAndHasNoGmOrRandomOverrides()
    {
        var mode=NativeQa.NativeQaModes.Find("loaded-grant-membership");Assert.Equal("loaded-grant-membership",mode.Name);
        Assert.Equal(new ushort[]{2,17,64,137,143,356},mode.Plan.Abilities);Assert.Empty(mode.Plan.EnvironmentOverrides);
        var env=NativeQa.NativeQaFixturePlan.EnvironmentFor(mode.Name);Assert.Equal("1",env["MXO_DEV_COMMANDS"]);
        foreach(string key in new[]{"MXO_GM_CHARACTER_IDS","MXO_CALLBACK_QA_ROLL15","MXO_CALLBACK_QA_ROLL100"})Assert.Equal("",env[key]);
        Assert.Empty(mode.Plan.BeforeLoadout);Assert.Empty(mode.Plan.AfterLoadout);Assert.Empty(mode.Plan.AfterHelpers);
    }
}
