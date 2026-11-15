using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using hds;
using MxoHd.PacketResearch;
using HeadlessClient;
namespace hds.Tests;

[Collection("DelayedActions")]
public class SneakScaledGrantTests
{
    static string Root {get{var p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds.sln")))p=Path.GetDirectoryName(p)!;return p;}}
    static WorldClient Client(bool world=false)
    {
        var c=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));c.playerData=new();c.playerInstance=new();c.messageQueue=new();
        c.playerData.setDistrictId(1);c.playerData.setOnWorld(world);c.playerInstance.Level.setValue((byte)50);
        c.playerInstance.Health.setValue((ushort)500);c.playerInstance.MaxHealth.setValue((ushort)500);
        c.playerInstance.InnerStrengthMax.setValue((ushort)200);c.playerInstance.InnerStrengthAvailable.setValue((ushort)200);
        typeof(WorldClient).GetField("lastFlushTick",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(c,long.MaxValue);
        typeof(WorldClient).GetField("<TransportFence>k__BackingField",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(c,new WorldSendFence());return c;
    }
    static byte[][] Packets(WorldClient c)=>c.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>().Select(m=>m.content.Skip(1).ToArray()).ToArray();
    [Fact] public void OriginalStoredRowsCloseOnlyTheLearnedLevelGrantAndIts209Consumer()
    {
        var parent=AbilityCatalog.Find(151);var grant=Assert.Single(parent.Modifiers.Where(m=>m.Has("TargetAbilityGrant")));
        Assert.Equal(2148621312d,grant.Number("TargetAbilityGrant"));Assert.Equal(2,grant.Number("ValueFunction"));
        Assert.Equal(0,grant.Number("WhenEnabled"));Assert.Equal(2,grant.Number("DurationType"));Assert.Equal(50,parent.Number("Ability/AbilityLevelCap"));
        foreach(string field in new[]{"MinValue","MaxValue","ValueScale"})Assert.False(grant.Has(field));
        var child=AbilityCatalog.Find(1111);var stealth=Assert.Single(child.Modifiers.Where(m=>m.Target==209));
        Assert.Equal(2,stealth.Number("ValueFunction"));Assert.Equal(2,stealth.Number("WhenEnabled"));
        Assert.False(child.Has("Ability/InventoryMemCost"));Assert.False(child.Has("Ability/RequiresTool"));Assert.Equal(0,child.Number("Ability/CastValidTarget",-1));
        Assert.Equal((ushort)155,AbilityCatalog.IdOf((uint)child.Number("Ability/DisciplineBelongsTo")));
    }
    [Theory][InlineData(2)][InlineData(17)][InlineData(50)]
    public void RealPassiveClosureAndSneakSnapshotUseLearnedLevelAtCharacter50(ushort learned)
    {
        var c=Client();AbilityPassives.SetLoaded(c,new (ushort,ushort)[]{(2,50),(113,50),(130,50),(143,50),(151,learned),(185,50)});
        Assert.Equal(learned,AbilityPassives.Level(c,151));Assert.Equal(learned,AbilityPassives.Level(c,1111));
        Assert.Equal(learned,Stealth.AbilityLevel(c,1111));Assert.Equal(50,c.playerInstance.Level.getValue()[0]);
        var snapshot=Stealth.ModifiersFor(Stealth.Defs[1111],Stealth.AbilityLevel(c,1111),50);
        Assert.Contains(snapshot,m=>m.Kind==AbilityBuffs.KindStat&&m.Stat==209&&m.Value==learned);
        Assert.Contains(snapshot,m=>m.Stat==115&&m.Value==-200);Assert.Contains(snapshot,m=>m.Stat==509&&m.Value==10);
        // Character50/learned50 already received50 from the previous fallback, not server stealth0.
    }
    [Fact] public void SourceCapIsSafeAndUnseenLegacyAndZeroPoliciesRemainSeparate()
    {
        Assert.Equal((ushort)50,AbilityProgression.GrantLevel(151,ushort.MaxValue));Assert.Equal((ushort)0,AbilityProgression.GrantLevel(151,0));
        Assert.Equal((ushort)17,AbilityProgression.GrantLevel(2,17));Assert.Equal((ushort)0,AbilityProgression.GrantLevel(37,50));
        Assert.Equal((ushort)0,AbilityProgression.GrantLevel(277,50));Assert.Equal((ushort)0,AbilityProgression.GrantLevel(175,50));
        Assert.Equal(25,AbilityGrantRegistration.ObservedPairs.Count);Assert.Equal((151,1111),(intPair(AbilityGrantRegistration.ObservedScaledPairs.Single())));
        Assert.False(AbilityGrantRegistration.Handles(151,1116));Assert.False(AbilityGrantRegistration.Handles(120,1114));
        static (int,int) intPair((ushort Source,ushort Target) p)=>(p.Source,p.Target);
    }
    [Fact] public void NormalOwnedPrerequisiteBatchFitsAndNeverOwnsTheHiddenChild()
    {
        var loadout=SneakScaledGrantFixture.Loadout(17);
        var rows=loadout.Select((r,i)=>new AbilityMemory.Row((uint)i+1,(ushort)(400+i),0x80000000u|((uint)r.Ability<<10),r.Level,false)).ToArray();
        var request=rows.Select(r=>new AbilityProgression.LoadoutEntry{Slot=r.Slot,Ability=r.Ability,Level=r.Level}).ToArray();
        var plan=AbilityMemory.Plan(rows,request,false,PlayerProgression.ExperienceForLevel(50),50);
        Assert.Null(plan.Refusal);Assert.Equal(6,plan.Accepted.Length);Assert.Equal(17,plan.Used);Assert.Equal(100,plan.Maximum);
        Assert.Contains((ushort)1111,AbilityMemory.Membership(plan.Loaded));Assert.DoesNotContain(rows,r=>r.Ability==1111);Assert.All(rows,r=>Assert.False(r.Loaded));
        var code=new AbilityMemory.Row(99,500,0x84025c00,17,true);Assert.False(AbilityMemory.IsAbility(code.Descriptor));
        Assert.DoesNotContain((ushort)151,AbilityMemory.Membership(new[]{code}));Assert.DoesNotContain((ushort)1111,AbilityMemory.Membership(new[]{code}));
        var mode=NativeQa.NativeQaModes.Find(SneakScaledGrantFixture.Name);
        Assert.Equal(SneakScaledGrantFixture.Parents,mode.Plan.Abilities);Assert.DoesNotContain((ushort)1111,mode.Plan.Abilities);Assert.DoesNotContain((ushort)1112,mode.Plan.Abilities);
        Assert.Single(mode.Plan.AfterLoadout);Assert.False(mode.Plan.GmCharacter110);Assert.False(mode.Plan.FixedCallbackRolls);
    }
    [Fact] public void ActualSchemasDistinguishEarlyZeroBaseAndSourceGrantFromActive209()
    {
        var oldZero=Convert.FromHexString("80B2570400000802");var native50=Convert.FromHexString("80B2570432000802");
        Assert.True(SneakScaledGrantFixture.ChildBase(oldZero));Assert.False(SneakScaledGrantFixture.Base(oldZero,17));Assert.True(SneakScaledGrantFixture.Base(native50,50));
        var captured=Convert.FromHexString("80BC4503570400970000005704320000000000000000");
        Assert.True(SneakScaledGrantFixture.ChildMembership(captured));Assert.True(SneakScaledGrantFixture.Membership(captured,50));Assert.False(SneakScaledGrantFixture.Membership(captured,17));
        var active=AbilityBuffs.BuildModifier(AbilityBuffs.KindStat,1,1111,209,17,false,0);
        Assert.True(SneakScaledGrantFixture.Stealth209(active,17));Assert.False(SneakScaledGrantFixture.ChildMembership(active));
        Assert.False(SneakScaledGrantFixture.Stealth209(AbilityBuffs.BuildModifier(0x45,1,1111,209,17,false,0),17));
    }
    [Theory][InlineData(null)][InlineData("")][InlineData("bad")][InlineData("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb")]
    public void FreshCannotUseUnpinnedOrWrongRuntime(string? expected)=>Assert.Throws<InvalidDataException>(()=>SneakScaledGrantFixture.RequireSource(expected,new string('a',64)));
    [Theory][InlineData("target")][InlineData("function")][InlineData("scale")][InlineData("min")][InlineData("max")][InlineData("duration")][InlineData("enabled")][InlineData("cap")][InlineData("extra-grant")]
    public void UnprovedChangedStoredShapesAreNotAnotherScaledGrant(string fault)
    {
        using var source=JsonDocument.Parse(File.ReadAllText(Path.Combine(Root,"data/ability_catalog.json")));
        var row=source.RootElement.GetProperty("abilities").EnumerateArray().Single(r=>r.GetProperty("id").GetInt32()==151);
        var fields=JsonNode.Parse(row.GetProperty("fields").GetRawText())!;
        switch(fault)
        {
            case "target":fields["StatModifier/TargetAbilityGrant"]=2148626432u;break;
            case "function":fields["StatModifier/ValueFunction"]=3;break;
            case "scale":fields["StatModifier/ValueScale"]=1;break;
            case "min":fields["StatModifier/MinValue"]=0;break;
            case "max":fields["StatModifier/MaxValue"]=0;break;
            case "duration":fields["StatModifier/DurationType"]=1;break;
            case "enabled":fields["StatModifier/WhenEnabled"]=2;break;
            case "cap":fields["Ability/AbilityLevelCap"]=51;break;
            case "extra-grant":fields["Extra/WhenEnabled"]=0;fields["Extra/TargetAbilityGrant"]=2148621312u;break;
        }
        using var changed=JsonDocument.Parse(fields.ToJsonString());var entry=new AbilityCatalog.Entry{Id=151,Fields=changed.RootElement.Clone()};
        Assert.False(AbilitySneakGrant.TryLevel(entry,17,out _));
    }
    [Fact] public void RealReadinessAndSourceLevelReplacementPublishRemoveBeforeNewBaseAndAdd()
    {
        var c=Client(true);c.playerData.passives.Loaded[151]=17;
        AbilityGrantRegistration.LoadedChanged(c);Assert.Empty(Packets(c));
        AbilityGrantRegistration.LoadingComplete(c,new byte[]{1});
        Assert.Equal(ServerPackets.BuildAbilityLoad(1111,17,520),Packets(c)[0]);Assert.Equal(AbilityGrantRegistration.Record(new(151,1111,17),false),Packets(c)[1]);
        AbilityGrantRegistration.LoadingComplete(c,new byte[]{1});Assert.Equal(2,Packets(c).Length);
        c.playerData.passives.Loaded[151]=50;AbilityGrantRegistration.LoadedChanged(c);var changed=Packets(c).Skip(2).ToArray();
        Assert.Equal(3,changed.Length);Assert.Equal(AbilityGrantRegistration.Record(new(151,1111,17),true),changed[0]);
        Assert.Equal(ServerPackets.BuildAbilityLoad(1111,50,520),changed[1]);Assert.Equal(AbilityGrantRegistration.Record(new(151,1111,50),false),changed[2]);
        c.playerData.passives.Loaded.Remove(151);AbilityGrantRegistration.LoadedChanged(c);Assert.Equal(ServerPackets.BuildAbilityUnload(1111),Packets(c)[^2]);
        Assert.Equal(AbilityGrantRegistration.Record(new(151,1111,50),true),Packets(c)[^1]);
    }
    [Fact] public void StaleWorldTransportPendingAndNewWorldKeepExistingReadyFence()
    {
        var c=Client(true);c.playerData.passives.Loaded[151]=17;var old=new AbilityGrantRegistration.Stamp(c.playerData.worldGeneration,c.TransportFence.Capture());
        c.playerData.worldGeneration++;AbilityGrantRegistration.Apply(c,true,old);Assert.Empty(Packets(c));
        var beforeReset=new AbilityGrantRegistration.Stamp(c.playerData.worldGeneration,c.TransportFence.Capture());
        c.TransportFence.PublishIncoming(true,()=>{});AbilityGrantRegistration.Apply(c,true,beforeReset);Assert.Empty(Packets(c));
        c.TransportFence.Resume(c.TransportFence.Capture());c.playerData.worldChangePending=true;AbilityGrantRegistration.LoadingComplete(c,new byte[]{1});Assert.Empty(Packets(c));
        c.playerData.worldChangePending=false;AbilityGrantRegistration.LoadingComplete(c,new byte[]{1});Assert.Equal(2,Packets(c).Length);
        c.playerData.worldGeneration++;AbilityGrantRegistration.LoadedChanged(c);Assert.Equal(2,Packets(c).Length);
        AbilityGrantRegistration.LoadingComplete(c,new byte[]{1});Assert.Equal(4,Packets(c).Length);
    }
    [Fact] public void EveryLiteralObservationIsFramedAndTheHistoricalManifestIsNotExpanded()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(Root,"research/protocol/sneak-scaled-grant-membership.json")));
        int bases=0,members=0;
        foreach(var file in doc.RootElement.GetProperty("Files").EnumerateArray())
        {
            string path=Path.Combine(Root,file.GetProperty("File").GetString()!);byte[] raw=File.ReadAllBytes(path);
            string hash=Convert.ToHexString(SHA256.HashData(raw)),lf=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(raw).Replace("\r\n","\n"))));
            Assert.Contains(hash,new[]{file.GetProperty("FileSha256").GetString(),file.GetProperty("FileLfSha256").GetString()});Assert.Equal(file.GetProperty("FileLfSha256").GetString(),lf);
            var framed=PacketResearcher.SummarizePacketDump(path,Array.Empty<RpcHeaderEntry>(),Root);
            foreach(var sample in file.GetProperty("Bases").EnumerateArray())
            {
                Assert.Contains(framed.PlayerAttributePayloads,p=>p.Line==sample.GetProperty("Line").GetInt32()&&p.PayloadHex==sample.GetProperty("Payload").GetString()&&p.NativeFields is{Id:1111,Value:50,CsSlot:520});bases++;
                Assert.Equal(Convert.FromHexString("80b2"+sample.GetProperty("Payload").GetString()!.Replace(" ","")),ServerPackets.BuildAbilityLoad(1111,50,520));
            }
            foreach(var sample in file.GetProperty("Memberships").EnumerateArray())
            {
                Assert.Contains(framed.ManageBonusPayloads,p=>p.Line==sample.GetProperty("Line").GetInt32()&&p.PayloadHex==sample.GetProperty("Payload").GetString()&&p.NativeFields is{SourceId:151,TargetId:1111,ValueBits:50,DurationBits:0} n&&n.Locator==(1111u<<8|3));members++;
                Assert.Equal(Convert.FromHexString("80bc"+sample.GetProperty("Payload").GetString()!.Replace(" ","")),AbilityGrantRegistration.Record(new(151,1111,50),sample.GetProperty("Operation").GetInt32()==1));
            }
        }
        Assert.Equal(6,bases);Assert.Equal(19,members);Assert.Equal(25,AbilityGrantRegistration.ObservedPairs.Count);
    }
}
