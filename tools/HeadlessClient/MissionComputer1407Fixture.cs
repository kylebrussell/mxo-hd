namespace HeadlessClient;
public static class MissionComputer1407Fixture
{
    public static byte[]? AutomaticExitSince(IEnumerable<RpcRecord> responses,int cursor)
    {
        if(cursor<0)return null;var window=responses.Skip(cursor).Select(r=>r.Bytes).ToArray();
        int tick=Array.FindIndex(window,b=>b.AsSpan().StartsWith(new byte[]{0x80,0xa0,1,6,0,1}));
        var ends=window.Select((b,i)=>(b,i)).Where(x=>x.b.Length>=19&&x.b[0]==0x80&&x.b[1]==0xa2&&x.b[2]==0).ToArray();
        return tick>=0&&ends.Length==1&&ends[0].i>tick?ends[0].b:null;
    }
    // Match a newly received free proxy to the committed pose; historical source/native coordinates are not authority.
    public static ViewCreation? ComputerSince(IEnumerable<ViewCreation> creations,int cursor,double x,double y,double z,string quaternion)
    {
        if(cursor<0||!double.IsFinite(x)||!double.IsFinite(y)||!double.IsFinite(z))return null;
        return creations.Skip(cursor).LastOrDefault(v=>v.Goid==3&&
            v.Attributes.TryGetValue("Position",out var p)&&p.Length==24&&BitConverter.ToDouble(p)==x&&BitConverter.ToDouble(p,8)==y&&BitConverter.ToDouble(p,16)==z&&
            v.Attributes.TryGetValue("Orientation",out var q)&&q.Length==16&&Convert.ToHexString(q).Equals(quaternion,StringComparison.OrdinalIgnoreCase));
    }
    // This fixture's Office exit is >110m from Digger. A same-session return must wait for a NEW contact creation.
    public static ViewCreation? ContactSince(IEnumerable<ViewCreation> creations,int cursor) => creations.Skip(cursor).LastOrDefault(v=>
        v.Goid==6520 && v.Attributes.TryGetValue("ContactID",out var id) && id.Length==4 && BitConverter.ToUInt32(id)==Contact);
    public static RpcRecord RetrievalSince(IEnumerable<RpcRecord> responses,int cursor) => responses.Skip(cursor)
        .Where(r=>r.Bytes.Length>8&&r.Bytes[0]==0x80&&r.Bytes[1]==0x96).Single(r=>
        {
            var b=r.Bytes;int at=BitConverter.ToUInt16(b,6);
            if(at+2>b.Length)return false;int size=BitConverter.ToUInt16(b,at);
            return size>0&&at+2+size<=b.Length&&b[at+1+size]==0&&
                System.Text.Encoding.ASCII.GetString(b,at+2,size-1)=="Retrieval";
        });
    public const int Contact=8,Mission=3082,SiteSeed=2,X=33000,Y=-305,Z=-100949;
    public const string NativeActorSql="UPDATE characters SET level=10,exp=1282500,alignment=1,healthC=2000,healthM=2000,innerStrC=1000,innerStrM=1000,y=-305,rotation=64 WHERE charId=110";
    public static IReadOnlyDictionary<string,string> Environment=>new Dictionary<string,string>(NeighbourhoodMissionFixture.Environment){["MXO_MISSION_SITE_QA_SEED"]="2"};
}
