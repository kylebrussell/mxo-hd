using hds;
using HeadlessClient;
namespace hds.Tests;
public class ZionCurrentUploadBoothTests
{
    const string Q="0000000000000000000000000000803f";
    static byte[] Booth(ushort view=5,byte spawn=2,uint key=ZionCurrentUploadBooth.StaticKey)
    {
        var values=new Dictionary<string,byte[]>{{"Position",new[]{17000d,530,2200}.SelectMany(BitConverter.GetBytes).ToArray()},{"Orientation",Convert.FromHexString(Q)}};
        var definition=ObjectDefinitions.CreationAttributes(592)!;var bytes=new List<byte>{1,0,8,0x50,2};bytes.AddRange(BitConverter.GetBytes(key));bytes.AddRange(new byte[]{spawn,0xcd,0xab,2});
        int last=Enumerable.Range(0,definition.Count).Where(i=>values.ContainsKey(definition[i].getName())).Max()/7;
        for(int group=0;group<=last;group++)
        {
            var present=Enumerable.Range(group*7,Math.Min(7,definition.Count-group*7)).Where(i=>values.ContainsKey(definition[i].getName())).ToArray();
            byte flags=(byte)(group<last?0x80:0);foreach(int i in present)flags|=(byte)(1<<(i%7));bytes.Add(flags);
            foreach(int i in present)bytes.AddRange(values[definition[i].getName()]);
        }
        bytes.AddRange(BitConverter.GetBytes(view));bytes.Add(0);return bytes.ToArray();
    }
    static OwnViewLifeLog Stream(params byte[][] sections)
    {
        var log=new OwnViewLifeLog();byte[] own=Convert.FromHexString("01000C0C0001CDAB00020000");
        log.Observe(false,own,0,WireFormat.FindCreations(own));int n=0;
        foreach(var section in sections)log.Observe(false,section,++n,WireFormat.FindCreations(section));return log;
    }

    [Fact] public void ActualRetainedBoothBornBeforeReturnRemainsUsableInCurrentLife()
    {
        byte[] original=Convert.FromHexString("0100085002EA01303902CDAB010100000000009AD0400000000000908040000000000030A140040000");
        var log=Stream(original,Convert.FromHexString("0200011C020000000000000000D084460080F7430080094537310200"));
        var actual=ZionCurrentUploadBooth.Find(log.Snapshot());Assert.NotNull(actual);Assert.Equal((ushort)4,actual.ViewId);Assert.Equal((byte)2,actual.SpawnId);Assert.Equal(ZionCurrentUploadBooth.StaticKey,actual.RelatedStaticObjectId);
    }
    [Fact] public void AbsentBoothWaitsWithoutBorrowingAnOldSession(){Assert.Null(ZionCurrentUploadBooth.Find(Stream().Snapshot()));}
    [Fact] public void ExactStaticIdentityCannotBeReplacedBySpatialAlias(){Assert.Null(ZionCurrentUploadBooth.Find(Stream(Booth(key:99)).Snapshot()));}
    [Fact] public void DeletedBoothIsNotReadFromCreationHistory(){Assert.Null(ZionCurrentUploadBooth.Find(Stream(Booth(),Convert.FromHexString("01000101000500")).Snapshot()));}
    [Fact] public void ChangedViewGenerationUsesOnlyTheCurrentIdentity(){var log=Stream(Booth(),Booth(spawn:3));Assert.Equal((byte)3,ZionCurrentUploadBooth.Find(log.Snapshot())!.SpawnId);}
    [Fact] public void RetiredGenerationCannotBeRevived(){Assert.Throws<InvalidDataException>(()=>ZionCurrentUploadBooth.Find(Stream(Booth(),Convert.FromHexString("01000101000500"),Booth()).Snapshot()));}
    [Fact] public void DuplicateLiveExactStaticBindingsRefuse(){Assert.Throws<InvalidDataException>(()=>ZionCurrentUploadBooth.Find(Stream(Booth(),Booth(view:6)).Snapshot()));}
    [Fact] public void UnknownCurrentBytesCannotBeSkipped(){Assert.Throws<InvalidDataException>(()=>ZionCurrentUploadBooth.Find(Stream(Booth(),new byte[]{0xfe,0xed}).Snapshot()));}
    [Fact] public void TruncatedOwnTeleportCannotBeSkipped(){Assert.Throws<InvalidDataException>(()=>ZionCurrentUploadBooth.Find(Stream(Booth(),Convert.FromHexString("0200011C")).Snapshot()));}
    [Fact] public void OldSnapshotRejectsAfterOwnerReset(){var log=Stream(Booth());var old=log.Snapshot();log.Observe(true,null,2,Array.Empty<ViewCreation>());Assert.Throws<InvalidDataException>(()=>ZionCurrentUploadBooth.Find(old));}
    [Fact] public void WrongExactBoundPoseRefuses(){byte[] raw=Booth();var birth=WireFormat.FindCreations(raw).Single();var position=birth.Attributes["Position"];int at=raw.AsSpan().IndexOf(position);BitConverter.GetBytes(17001d).CopyTo(raw,at);Assert.Throws<InvalidDataException>(()=>ZionCurrentUploadBooth.Find(Stream(raw).Snapshot()));}
}
