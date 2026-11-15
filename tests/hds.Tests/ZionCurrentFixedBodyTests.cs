using hds;
using HeadlessClient;
namespace hds.Tests;
public class ZionCurrentFixedBodyTests
{
    const string Q="0000000000000000000000000000803f";
    static MissionProgress.SavedObject Object()=>new(){Id=1,Goid=548,OpenFloor=new MissionOpenFloorPlacement(4,1,101217,3,1,5,1412,548,new string('a',64),new string('b',64),Q,new double[]{1,2,3},new double[]{1,2,3},new double[]{1,2,3},Array.Empty<ushort>(),Array.Empty<ushort>(),Array.Empty<ushort>())};
    static byte[] Body(ushort view=5,byte spawn=2,uint key=0)
    {
        var values=new Dictionary<string,byte[]>{{"Position",new[]{1d,2,3}.SelectMany(BitConverter.GetBytes).ToArray()},{"Orientation",Convert.FromHexString(Q)},{"MissionKey",new byte[]{255,42,0,2}}};
        var definition=ObjectDefinitions.CreationAttributes(548)!;var bytes=new List<byte>{1,0,0x0c,0x24,2};bytes.AddRange(new byte[]{spawn,0xcd,0xab,2});
        int last=Enumerable.Range(0,definition.Count).Where(i=>values.ContainsKey(definition[i].getName())).Max()/7;bytes[^1]=checked((byte)definition.Count(a=>values.ContainsKey(a.getName())));
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


    [Fact] public void ExistingCurrentLifeBirthNeedsNoPostPhaseRecreation(){var log=Stream(Body());Assert.NotNull(ZionCurrentFixedBody.Find(log.Snapshot(),Object(),42));}
    [Fact] public void WrongInstanceNeverBorrowsAConvenientBody(){Assert.Null(ZionCurrentFixedBody.Find(Stream(Body()).Snapshot(),Object(),43));}
    [Fact] public void DeletedBodyCannotBeReadFromCreationHistory(){Assert.Null(ZionCurrentFixedBody.Find(Stream(Body(),Convert.FromHexString("01000101000500")).Snapshot(),Object(),42));}
    [Fact] public void RetiredGenerationCannotBeRevived(){Assert.Throws<InvalidDataException>(()=>ZionCurrentFixedBody.Find(Stream(Body(),Convert.FromHexString("01000101000500"),Body()).Snapshot(),Object(),42));}
    [Fact] public void DuplicateExactLiveLeasesAreAmbiguous(){Assert.Throws<InvalidDataException>(()=>ZionCurrentFixedBody.Find(Stream(Body(),Body(view:6)).Snapshot(),Object(),42));}
    [Fact] public void UnknownCurrentFrameIsNeverSkipped(){Assert.Throws<InvalidDataException>(()=>ZionCurrentFixedBody.Find(Stream(Body(),new byte[]{0xfe,0xed}).Snapshot(),Object(),42));}
    [Fact] public void PreviousOwnLifeCannotAuthorizeAFreeBody(){var log=Stream(Body());var old=log.Snapshot();log.Observe(true,null,2,Array.Empty<ViewCreation>());Assert.Throws<InvalidDataException>(()=>ZionCurrentFixedBody.Find(old,Object(),42));}
    [Theory][InlineData("Position")][InlineData("Orientation")]
    public void WrongFixedPoseNeverMatches(string attribute){byte[] b=Body();var v=WireFormat.FindCreations(b).Single();int at=b.AsSpan().IndexOf(v.Attributes[attribute]);b[at]^=1;Assert.Null(ZionCurrentFixedBody.Find(Stream(b).Snapshot(),Object(),42));}
}
