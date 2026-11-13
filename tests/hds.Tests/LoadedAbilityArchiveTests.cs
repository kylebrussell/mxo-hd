using hds;
namespace hds.Tests;
[Collection("DelayedActions")]
public class LoadedAbilityArchiveTests
{
    private static MarginAbilityItem Row(uint descriptor,ushort level=50,bool loaded=true)=>new(){AbilityID=unchecked((int)descriptor),level=level,isLoaded=loaded};
    [Fact] public void InitialArchiveSourceRejectsCodeBeforeIdNarrowingAndPreservesTypedAliases()
    {
        uint code64=unchecked((uint)CodeStorage.BaseValue(512)),code304=unchecked((uint)CodeStorage.BaseValue(2435));
        Assert.Equal(0x84010000u,code64);Assert.Equal(0x8404c180u,code304);Assert.True(CodeStorage.IsCode(code64));Assert.True(CodeStorage.IsCode(code304));
        Assert.Equal((ushort)64,AbilityCatalog.Canonical(AbilityCatalog.IdOf(code64)));Assert.Equal((ushort)304,AbilityCatalog.Canonical(AbilityCatalog.IdOf(code304)));
        var rows=new[]{Row(code64),Row(code304),Row(0x80459000,17),Row(0x80022400),Row(0x80010000,50,false),Row(64),Row(0x88010000)};
        Assert.Equal(new[]{((ushort)4452,(ushort)17),((ushort)137,(ushort)50)},AbilityPassives.LoadedArchiveAbilities(rows));
        Assert.True(rows[0].getLoaded());Assert.True(rows[1].getLoaded()); // no mutation/drop from the archive itself
    }
    [Fact] public void ActualPassiveHolderAndReadyGrantPublicationNeverActivateCodeParents()
    {
        var c=(WorldClient)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));c.playerData=new ClientData();c.playerData.setDistrictId(1);c.playerData.setOnWorld(true);c.playerInstance=new PlayerCharacter();c.playerInstance.Level.setValue((byte)50);c.playerInstance.Health.setValue((ushort)100);c.playerInstance.MaxHealth.setValue((ushort)500);c.playerInstance.InnerStrengthMax.setValue((ushort)500);c.playerInstance.InnerStrengthAvailable.setValue((ushort)100);c.messageQueue=new MessageQueue();
        typeof(WorldClient).GetField("lastFlushTick",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.SetValue(c,long.MaxValue);
        typeof(WorldClient).GetField("<TransportFence>k__BackingField",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.SetValue(c,new WorldSendFence());
        AbilityPassives.SetLoaded(c,AbilityPassives.LoadedArchiveAbilities(new[]{Row(unchecked((uint)CodeStorage.BaseValue(512))),Row(unchecked((uint)CodeStorage.BaseValue(2435))),Row(0x80459000,17),Row(0x80022400)}));
        Assert.False(c.playerData.passives.Loaded.ContainsKey(64));Assert.False(c.playerData.passives.Loaded.ContainsKey(304));Assert.Equal((ushort)17,AbilityPassives.Level(c,356));Assert.Equal((ushort)50,AbilityPassives.Level(c,137));
        AbilityGrantRegistration.LoadingComplete(c,new byte[]{1});var packets=c.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>().Select(m=>m.content.Skip(1).ToArray()).ToArray();
        Assert.Contains(packets,p=>p.SequenceEqual(ServerPackets.BuildAbilityLoad(1087,0,520)));Assert.Contains(packets,p=>p.SequenceEqual(ServerPackets.BuildAbilityLoad(1062,0,520)));
        Assert.DoesNotContain(packets,p=>p.SequenceEqual(ServerPackets.BuildAbilityLoad(1078,0,520)));
    }
}
