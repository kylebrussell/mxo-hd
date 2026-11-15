using hds;
namespace hds.Tests;
public class HardlineRegistrationTests
{
    [Fact] public void WorldLoadAndKnownCacheShareTheNativeCurrentConstructKey()
    {
        for(uint map=12;map<=17;map++)Assert.Equal(map-8,HardlineRegistration.WorldConstructId(map,1000+map));
        // Keep native/private IDs for worlds outside this appended hardline namespace; never truncate aliases.
        foreach(uint map in new uint[]{0,1,2,3,4,5,6,7,8,9,10,11,18,800,901,65552,uint.MaxValue})
            Assert.Equal(9876u,HardlineRegistration.WorldConstructId(map,9876));
        for(int book=1;book<=8;book++)
        {
            uint map=PvpArchives.MapFor(book),instance=PvpArchives.ConstructId(map);
            Assert.Equal(instance,HardlineRegistration.WorldConstructId(map,instance));
        }
        Assert.Equal(Convert.FromHexString("0802000000"),HardlineRegistration.Cache(map=>map==16?new ushort[]{2}:Array.Empty<ushort>()));
    }
    [Fact] public void IncrementalDiscoveryMatchesRetailAndUsesRegisteredOrdinals()
    {
        Assert.Equal(Convert.FromHexString("80fa030000001100000001"),HardlineBooths.BuildSynchronized(3,17));
        Assert.Equal(Convert.FromHexString("80fa080000000200000001"),HardlineBooths.BuildSynchronized(16,2));
        Assert.Equal((byte)8,HardlineBooths.ActionSynchronize);
        Assert.Null(HardlineBooths.BuildSynchronized(16,0));
        Assert.Null(HardlineBooths.BuildSynchronized(16,ushort.MaxValue));
        Assert.Null(HardlineBooths.BuildSynchronized(901,2));
    }
    [Fact] public void LoginReplyPreservesOpaqueHeaderAndRegistersOrderedUniverseIds()
    {
        byte[] p=HardlineRegistration.LoginReply();
        Assert.Equal(Convert.FromHexString("0900000000000000003E1726700F00"),p[..15]);
        int offset=BitConverter.ToUInt16(p,13),count=BitConverter.ToUInt16(p,offset);
        Assert.Equal(9,count);Assert.Equal(offset+2+count*2,p.Length);
        Assert.Equal(new ushort[]{12,7,169,379,384,377,382,383,381},Enumerable.Range(0,count).Select(i=>BitConverter.ToUInt16(p,offset+2+2*i)));
    }
    [Theory][InlineData(0u)][InlineData(10u)][InlineData(12u)][InlineData(17u)][InlineData(65537u)][InlineData(uint.MaxValue)]
    public void UnknownLogicalIdsCannotAliasInternalMaps(uint id){Assert.False(HardlineRegistration.TryMap(id,out _));}
    [Fact] public void MappingIsSeparateFromWorldAndPvpConstructNamespace()
    {
        ushort[] maps={1,2,3,12,13,14,15,16,17};
        for(byte i=1;i<=maps.Length;i++){Assert.True(HardlineRegistration.TryMap(i,out ushort map));Assert.Equal(maps[i-1],map);Assert.True(HardlineRegistration.TryLogical(map,out byte logical));Assert.Equal(i,logical);}
        foreach(ushort map in new ushort[]{0,4,5,6,7,8,9,10,11,800,901})Assert.False(HardlineRegistration.TryLogical(map,out _));
        for(int id=1;id<=8;id++)Assert.Equal((uint)(10+id),PvpArchives.ConstructId(PvpArchives.MapFor(id)));
        Assert.Equal(16u,PvpArchives.ConstructId(16));Assert.Equal(901u,PvpArchives.ConstructId(901));
    }
    [Fact] public void CacheUsesFiveByteLogicalAndFullHidRecords()
    {
        var p=HardlineRegistration.Cache(map=>map==1?new ushort[]{153,48,48}:map==16?new ushort[]{2}:Array.Empty<ushort>());
        Assert.Equal(Convert.FromHexString("013000000001990000000802000000"),p);
    }
    [Fact] public void DiscoveryCannotBeBypassedByUnknownOrTruncatedDestination()
    {
        IEnumerable<ushort> Known(ushort map)=>map==16?new ushort[]{2}:Array.Empty<ushort>();
        Assert.True(HardlineRegistration.Destination(8,2,Known,out ushort map));Assert.Equal(16,map);
        foreach(var pair in new (uint logical,uint hid)[]{(8,65538),(65544,2),(16,2),(8,0),(8,3),(9,1),(uint.MaxValue,2)})
            Assert.False(HardlineRegistration.Destination(pair.logical,pair.hid,Known,out _));
        Assert.False(HardlineRegistration.Destination(8,2,_=>Array.Empty<ushort>(),out _));
    }
    [Fact] public void NativeModeUsesSourceArrivalAndSafeHeadingWithoutAuthority()
    {
        var m=new NativeQa.Modes.LaHardlineRegistrationMode().Plan;
        Assert.False(m.DevCommands);Assert.False(m.GmCharacter110);Assert.False(m.FixedCallbackRolls);
        Assert.Equal(37700,m.X);Assert.Equal(31700,m.Z);Assert.Contains("rotation=192",Assert.Single(m.AfterLoadout));
    }
}
