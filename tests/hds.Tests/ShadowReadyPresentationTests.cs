using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using hds;
using MxoHd.PacketResearch;
using NativeQa.Modes;
namespace hds.Tests;

[Collection("DelayedActions")]
public class ShadowReadyPresentationTests
{
    static readonly byte[] Derived=Convert.FromHexString("80B25D0400000802");
    static readonly byte[] Add=Convert.FromHexString("80BC45035D0400220100005D04000000000000000000");
    static readonly byte[] Off=Convert.FromHexString("80BC45035D0400220100005D04000000000100000000");
    static readonly AbilityGrantRegistration.Stamp Stamp=new(1,0);
    sealed class ClientState
    {
        internal (ushort Level,ushort Slot)? Base;internal bool Member;
        internal void Apply(IEnumerable<byte[]> packets)
        {
            foreach(var b in packets)
            {
                if(b.Length==8&&b[0]==0x80&&b[1]==0xb2&&BitConverter.ToUInt16(b,2)==1117)Base=(BitConverter.ToUInt16(b,4),BitConverter.ToUInt16(b,6));
                if(b.Length==4&&b[0]==0x80&&b[1]==0xb3&&BitConverter.ToUInt16(b,2)==1117)Base=null;
                if(b.Length==22&&b[0]==0x80&&b[1]==0xbc&&BitConverter.ToUInt16(b,2)==0x345&&BitConverter.ToUInt16(b,11)==1117)Member=b[17]==0;
            }
        }
    }
    static WorldClient Client()
    {
        var c=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));c.playerData=new();c.messageQueue=new();c.playerInstance=new();
        c.playerInstance.Level.setValue((byte)50);c.playerInstance.Health.setValue((ushort)500);c.playerInstance.MaxHealth.setValue((ushort)500);
        c.playerInstance.InnerStrengthMax.setValue((ushort)200);c.playerInstance.InnerStrengthAvailable.setValue((ushort)200);
        typeof(WorldClient).GetField("lastFlushTick",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(c,long.MaxValue);
        typeof(WorldClient).GetField("<TransportFence>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(c,new WorldSendFence());
        c.playerData.setDistrictId(1);c.playerData.setOnWorld(true);return c;
    }
    static byte[][] Packets(WorldClient c)=>c.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>().Select(m=>m.content.Skip(1).ToArray()).ToArray();
    static byte[][] ShadowPackets(WorldClient c)=>Packets(c).Where(b=>
        b.Length>=4&&b[0]==0x80&&new byte[]{0xb2,0xb3}.Contains(b[1])&&BitConverter.ToUInt16(b,2)==1117||
        b.Length==22&&b[0]==0x80&&b[1]==0xbc&&BitConverter.ToUInt32(b,7)==290&&BitConverter.ToUInt16(b,11)==1117).ToArray();
    static AbilityShadowPresentation.Snapshot Snap(ClientData d,bool parent,ushort? level=null,ushort slot=405,byte[]? emitted=null)
    {
        var owned=AbilityShadowPresentation.Owned.From(level.HasValue?new[]{new AbilityShadowPresentation.Base(level.Value,slot)}:Array.Empty<AbilityShadowPresentation.Base>());
        return new(d,parent,owned,AbilityShadowPresentation.Emitted.FromPacket(emitted));
    }
    [Fact] public void LiteralOriginalZeroGrantStaysSeparateFromHistoricalToolAndScaledScopes()
    {
        Assert.True(AbilityShadowPresentation.Qualifies(AbilityCatalog.Find(290)));Assert.True(AbilityGrantRegistration.Handles(290,1117));
        Assert.Equal((ushort)0,AbilityProgression.GrantLevel(290,50));Assert.Equal(25,AbilityGrantRegistration.ObservedPairs.Count);
        Assert.Equal((290,1117),( (int)AbilityGrantRegistration.ReadyPresentationPairs.Single().Source,(int)AbilityGrantRegistration.ReadyPresentationPairs.Single().Target));
        Assert.DoesNotContain(((ushort)290,(ushort)1117),AbilityGrantRegistration.RegisteredPairs);Assert.False(AbilityGrantRegistration.Handles(290,1116));
    }
    [Theory][InlineData("function")][InlineData("value")][InlineData("duration")][InlineData("enabled")][InlineData("duplicate")]
    public void ChangedOrAmbiguousStoredGrantDoesNotQualify(string change)
    {
        var original=AbilityCatalog.Find(290);var f=JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(original.Fields.GetRawText())!;
        string component=original.Modifiers.Single(m=>AbilityCatalog.IdOf((uint)m.Number("TargetAbilityGrant"))==1117).Component;
        void Set(string key,int value)=>f[key]=JsonSerializer.SerializeToElement(value);
        switch(change){case "function":Set(component+"/ValueFunction",2);break;case "value":Set(component+"/MinValue",1);break;case "duration":Set(component+"/DurationType",1);break;case "enabled":Set(component+"/WhenEnabled",2);break;case "duplicate":foreach(var field in original.Fields.EnumerateObject().Where(p=>p.Name.StartsWith(component+"/")))f["Other/"+field.Name[(component.Length+1)..]]=field.Value;break;}
        Assert.False(AbilityShadowPresentation.Qualifies(new AbilityCatalog.Entry{Id=290,Fields=JsonSerializer.SerializeToElement(f)}));
    }
    [Fact] public void ExactReadyPublishesLateBaseBeforeMemberOnceAndReplaysNewWorld()
    {
        var c=Client();c.playerData.passives.Loaded[290]=50;AbilityGrantRegistration.LoadedChanged(c);Assert.Empty(Packets(c));
        foreach(byte[] bad in new[]{Array.Empty<byte>(),new byte[]{0},new byte[]{1,0}})AbilityGrantRegistration.LoadingComplete(c,bad);
        Assert.Empty(Packets(c));AbilityGrantRegistration.LoadingComplete(c,new byte[]{1});Assert.Equal(new[]{Derived,Add},Packets(c));
        AbilityGrantRegistration.LoadingComplete(c,new byte[]{1});Assert.Equal(2,Packets(c).Length);
        c.messageQueue.RPCMessagesQueue.Clear();c.playerData.setOnWorld(false);c.playerData.setOnWorld(true);
        AbilityGrantRegistration.LoadedChanged(c);Assert.Empty(Packets(c));AbilityGrantRegistration.LoadingComplete(c,new byte[]{1});Assert.Equal(new[]{Derived,Add},Packets(c));
    }
    [Fact] public void OwnedBaseAndMembershipAreReconciledSeparatelyAcrossEveryNormalTransition()
    {
        var d=new ClientData();var state=new AbilityShadowPresentation.Presentation();var client=new ClientState();
        var first=state.Reconcile(d,Stamp,true,Snap(d,true));Assert.Equal(new[]{Derived,Add},first);client.Apply(first);
        byte[] owned=ServerPackets.BuildAbilityLoad(1117,50,405);client.Apply(new[]{owned});
        var toOwned=state.Reconcile(d,Stamp,false,Snap(d,true,50,405,owned));client.Apply(toOwned);
        Assert.Empty(toOwned);Assert.Equal(((ushort)50,(ushort)405),client.Base!.Value);Assert.True(client.Member);
        var parentOff=state.Reconcile(d,Stamp,false,Snap(d,false,50));Assert.Equal(new[]{Off},parentOff);client.Apply(parentOff);
        Assert.Equal(((ushort)50,(ushort)405),client.Base!.Value);Assert.False(client.Member);
        var parentOn=state.Reconcile(d,Stamp,false,Snap(d,true,50));Assert.Equal(new[]{Add},parentOn);client.Apply(parentOn);
        var toDerived=state.Reconcile(d,Stamp,false,Snap(d,true));Assert.Equal(new[]{Derived},toDerived);client.Apply(toDerived);
        Assert.Equal(((ushort)0,AbilityProgression.HiddenSlot),client.Base!.Value);Assert.True(client.Member);
        client.Apply(state.Reconcile(d,Stamp,false,Snap(d,false)));Assert.Null(client.Base);Assert.False(client.Member);
    }
    [Fact] public void OwnedCoexistenceReplaysActualLevelAndSlotAfterEarlyLoadAndReady()
    {
        var d=new ClientData();var state=new AbilityShadowPresentation.Presentation();byte[] owned=ServerPackets.BuildAbilityLoad(1117,50,407);
        Assert.Empty(state.Reconcile(d,Stamp,false,Snap(d,true,50,407,owned)));
        Assert.Equal(new[]{owned,Add},state.Reconcile(d,Stamp,true,Snap(d,true,50,407)));
        Assert.Empty(state.Reconcile(d,Stamp,true,Snap(d,true,50,407)));
        Assert.Equal(new[]{owned,Add},state.Reconcile(d,new(2,1),true,Snap(d,true,50,407,owned)));
    }
    [Fact] public void OrdinaryOwnedUnloadWithoutParentIsNotFollowedByAnotherUnload()
    {
        var d=new ClientData();var state=new AbilityShadowPresentation.Presentation();state.Reconcile(d,Stamp,true,Snap(d,false,50));
        byte[] normal=ServerPackets.BuildAbilityUnload(1117);Assert.Empty(state.Reconcile(d,Stamp,false,Snap(d,false,emitted:normal)));
    }
    [Fact] public void ArchiveOnlyRowsAndDerivedClosureNeverBecomeExplicitOwnership()
    {
        var margin=new MarginAbilityItem{AbilityID=unchecked((int)(0x80000000u|(1117u<<10))),level=50,slot=409,isLoaded=false};
        Assert.Null(AbilityShadowPresentation.FromArchive(new[]{margin}).Value);
        margin.isLoaded=true;var actual=AbilityShadowPresentation.FromArchive(new[]{margin}).Value;Assert.Equal((ushort)409,actual!.Value.Slot);Assert.Equal((ushort)50,actual.Value.Level);
        Assert.True(AbilityShadowPresentation.FromArchive(new[]{margin,margin}).Ambiguous);
        var c=Client();AbilityPassives.SetLoaded(c,new (ushort,ushort)[]{(290,50)});
        Assert.True(c.playerData.passives.Loaded.ContainsKey(1117));Assert.Null(c.playerData.passives.ShadowOwned.Value);
    }
    [Fact] public void AuthoritativeNormalRowsAndTrainingRetainActualOwnedSlotWithoutChangingClosurePolicy()
    {
        var c=Client();var rows=new[]{new AbilityMemory.Row(1,404,0x80000000u|(290u<<10),50,true),new AbilityMemory.Row(2,409,0x80000000u|(1117u<<10),49,true)};
        AbilityPassives.SetLoadedFromRows(c,rows);Assert.Equal((ushort)409,c.playerData.passives.ShadowOwned.Value!.Value.Slot);
        AbilityPassives.SetLoaded(c,new (ushort,ushort)[]{(290,50),(1117,50)});
        Assert.Equal((ushort)409,c.playerData.passives.ShadowOwned.Value!.Value.Slot);Assert.Equal((ushort)50,c.playerData.passives.ShadowOwned.Value.Value.Level);
        AbilityPassives.SetLoadedFromRows(c,new[]{rows[0]});Assert.Null(c.playerData.passives.ShadowOwned.Value);
    }
    [Fact] public void ActualQueuedNormalOwnedLoadSurvivesTheRegistrationCallbackAndParentUnload()
    {
        var c=Client();var parent=new AbilityMemory.Row(1,404,0x80000000u|(290u<<10),50,true);
        var owned=new AbilityMemory.Row(2,409,0x80000000u|(1117u<<10),50,true);var observed=new ClientState();
        AbilityPassives.SetLoadedFromRows(c,new[]{parent});Assert.Empty(ShadowPackets(c));
        AbilityGrantRegistration.LoadingComplete(c,new byte[]{1});observed.Apply(ShadowPackets(c));Assert.True(observed.Member);
        c.messageQueue.RPCMessagesQueue.Clear();byte[] normal=ServerPackets.BuildAbilityLoad(owned.Ability,owned.Level,owned.Slot);
        c.messageQueue.addRpcMessage(normal);AbilityPassives.SetLoadedFromRows(c,new[]{parent,owned},normal);
        Assert.Equal(new[]{normal},ShadowPackets(c));observed.Apply(ShadowPackets(c));Assert.Equal(((ushort)50,(ushort)409),observed.Base!.Value);
        c.messageQueue.RPCMessagesQueue.Clear();AbilityPassives.SetLoadedFromRows(c,new[]{owned});
        Assert.Equal(new[]{Off},ShadowPackets(c));observed.Apply(ShadowPackets(c));Assert.Equal(((ushort)50,(ushort)409),observed.Base!.Value);Assert.False(observed.Member);
        c.messageQueue.RPCMessagesQueue.Clear();normal=ServerPackets.BuildAbilityUnload(1117);c.messageQueue.addRpcMessage(normal);
        AbilityPassives.SetLoadedFromRows(c,Array.Empty<AbilityMemory.Row>(),normal);Assert.Equal(new[]{normal},ShadowPackets(c));observed.Apply(ShadowPackets(c));Assert.Null(observed.Base);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LosingTheLastShadowGrantRemovesOnlyTheActiveUpgradeWithoutRenewingReuse(bool ownsLoadedChild)
    {
        const ushort shadow = 290, upgrade = 1117, independentBuff = 1;
        var client = Client();
        var parent = new AbilityMemory.Row(1, 404, AbilityMessages.AbilityGameObject(shadow), 50, true);
        var child = new AbilityMemory.Row(2, 409, AbilityMessages.AbilityGameObject(upgrade), 50, true);
        AbilityMemory.Row[] remaining = ownsLoadedChild ? new[] { child } : Array.Empty<AbilityMemory.Row>();
        var initial = remaining.Append(parent).ToArray();
        try
        {
            AbilityPassives.SetLoadedFromRows(client, initial);
            AbilityGrantRegistration.LoadingComplete(client, new byte[] { 1 });
            AbilityCooldowns.Start(client, upgrade, TimeUtils.GetSimTimeSeconds());
            double initialReuse = client.playerData.cooldownEndsAt[upgrade];
            Assert.True(AbilityBuffs.Apply(client, independentBuff));
            Assert.True(AbilityBuffs.Apply(client, upgrade));
            var added = UpgradeRecords();
            Assert.Equal(4, added.Length);
            Assert.All(added, record => Assert.Equal(0, record.AddRemoveType));
            client.messageQueue.RPCMessagesQueue.Clear();

            AbilityPassives.SetLoadedFromRows(client, remaining);

            Assert.Equal(ownsLoadedChild, AbilityPassives.IsLoaded(client, upgrade));
            Assert.Equal(ownsLoadedChild, AbilityBuffs.IsActive(client, upgrade));
            Assert.True(AbilityBuffs.IsActive(client, independentBuff));
            Assert.Equal(initialReuse, client.playerData.cooldownEndsAt[upgrade]);
            Assert.DoesNotContain(Packets(client), packet => packet is [0x80, 0xbd, 0x01, 0x5d, 0x04, ..]);
            Assert.Equal(ownsLoadedChild ? Array.Empty<ManageBonusRecord>() : added.Select(record => record with { AddRemoveType = 1 }),
                UpgradeRecords());
            Assert.Equal(ownsLoadedChild ? 0 : 1, Packets(client).Count(packet =>
                packet.AsSpan().SequenceEqual(AbilityBuffs.BuildExclusive(upgrade, false))));

            client.messageQueue.RPCMessagesQueue.Clear();
            AbilityPassives.SetLoadedFromRows(client, initial);
            Assert.True(AbilityPassives.IsLoaded(client, upgrade));
            Assert.Equal(ownsLoadedChild, AbilityBuffs.IsActive(client, upgrade));
            Assert.Empty(UpgradeRecords());
            Assert.Equal(initialReuse, client.playerData.cooldownEndsAt[upgrade]);
        }
        finally { AbilityBuffs.RemoveAll(client); }

        ManageBonusRecord[] UpgradeRecords() => Packets(client).Select(packet =>
            ManageBonusRecord.TryDecode(packet, out var record) ? record : null)
            .OfType<ManageBonusRecord>().Where(record => record.SourceId == upgrade && record.Type == 0x15).ToArray();
    }

    [Fact] public void OldLoadedSnapshotAndSameCounterNewOwnerCannotPublish()
    {
        var d=new ClientData();var state=new AbilityShadowPresentation.Presentation();var old=Snap(d,true);d.passives.Generation++;
        Assert.Empty(state.Reconcile(d,Stamp,true,old));
        state.Reconcile(d,Stamp,true,Snap(d,true));var replacement=new ClientData();
        Assert.Empty(state.Reconcile(replacement,Stamp,false,Snap(replacement,true)));
        Assert.Equal(new[]{Derived,Add},state.Reconcile(replacement,Stamp,true,Snap(replacement,true)));
    }
    [Theory][InlineData("world")][InlineData("transport")][InlineData("pending")][InlineData("shutdown")][InlineData("la")]
    public void CurrentWorldAndTransportGuardsSuppressStaleReady(string kind)
    {
        var c=Client();c.playerData.passives.Loaded[290]=50;var old=new AbilityGrantRegistration.Stamp(c.playerData.worldGeneration,c.TransportFence.Capture());
        switch(kind){case "world":c.playerData.worldGeneration++;break;case "transport":c.TransportFence.PublishIncoming(true,()=>{});break;case "pending":c.playerData.worldChangePending=true;break;case "shutdown":c.playerData.waitForRPCShutDown=true;break;case "la":c.playerData.setDistrictId((uint)MxOLocations.LA);break;}
        AbilityGrantRegistration.Apply(c,true,old);Assert.Empty(Packets(c));
    }
    [Fact] public void NativeToolAndScaledSneakPacketSequencesStayByteIdentical()
    {
        var c=Client();c.playerData.passives.Loaded[175]=50;c.playerData.passives.Loaded[151]=17;
        AbilityGrantRegistration.LoadingComplete(c,new byte[]{1});
        var expected=new[]{"80B2570411000802","80BC4503570400970000005704110000000000000000","80B25C0400000802","80BC45035C0400AF0000005C04000000000000000000"}.Select(Convert.FromHexString).ToArray();
        Assert.Equal(expected,Packets(c));
    }
    [Fact] public void NativeFixtureNeverArchivesTheGrantedActionAndStopsAtNormalListingGate()
    {
        var plan=new ShadowReadyPresentationMode().Plan;Assert.Contains((ushort)290,plan.Abilities);Assert.DoesNotContain((ushort)1117,plan.Abilities);
        Assert.True(plan.DevCommands); // Optional NAV return only. Stock Archive drives the loadout.
        Assert.False(plan.GmCharacter110);Assert.False(plan.FixedCallbackRolls);Assert.False(plan.HasHotbar);
        Assert.DoesNotContain(plan.BeforeLoadout.Concat(plan.AfterLoadout).Concat(plan.AfterHelpers),sql=>sql.Contains("1117"));
    }
}
