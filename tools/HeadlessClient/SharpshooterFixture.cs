using System.Text;
using System.Text.RegularExpressions;
using hds;
namespace HeadlessClient;
public static class SharpshooterFixture
{
    public const uint Pistol=537;
    public static ushort[] Abilities=>AbilityPrerequisites.Current.Closure(148).Keys.Concat(new ushort[]{2,17,148}).Distinct().Order().ToArray();
    public static ushort Slot=>(ushort)(400+Array.IndexOf(Abilities,(ushort)148));
    public static Dictionary<string,string> Environment=>new(PvpOpponentsFixture.Environment){["MXO_PACKET_LOG"]="1"};
    public static CharacterSeed Seed(string name,double x,double z,bool shooter=false,bool gun=false)=>new(name)
    {
        X=x,Y=495,Z=z,Level=50,Experience=(int)PlayerProgression.ExperienceForLevel(50),Organization=shooter?1:2,
        Health=20000,MaxHealth=20000,InnerStrength=2000,MaxInnerStrength=2000,
        OwnedLoadedAbilities=(shooter?Abilities:new ushort[]{2,17}).Select(id=>(id,(ushort)50)).ToArray(),
        Items=shooter||gun?new[]{(10,Pistol,1)}:Array.Empty<(int,uint,int)>()
    };
    public sealed record Input(byte Class,bool DefenderGun,string Metres,int Opponents,bool CountSampled,bool Loaded,int Modifier,int Before,int After);
    public static Input? ReadInput(string line,string actor)
    {
        var m=Regex.Match(line,@"^\[SHARPSHOOTER OURS\] actor="+Regex.Escape(actor)+@" class(\d+) defenderGun(True|False) metres(\S+) opponents(\d+) countSampled(True|False) loaded148(True|False) modifier(-?\d+) attackBonus(-?\d+)->(-?\d+)$");
        if(!m.Success||!byte.TryParse(m.Groups[1].Value,out byte cls)||!int.TryParse(m.Groups[4].Value,out int opponents)||
            !int.TryParse(m.Groups[7].Value,out int modifier)||!int.TryParse(m.Groups[8].Value,out int before)||!int.TryParse(m.Groups[9].Value,out int after)||
            (long)before+modifier!=after)return null;
        return new(cls,bool.Parse(m.Groups[2].Value),m.Groups[3].Value,opponents,bool.Parse(m.Groups[5].Value),bool.Parse(m.Groups[6].Value),modifier,before,after);
    }
    public sealed record Shot(bool Hit,ushort Damage,short Absorbed,ushort Attack,ushort Defense);
    public static bool ReadShot(byte[] b,string shooter,string target,bool attackerIsYou,out Shot? shot)
    {
        shot=null;if(b.Length<29||b[0]!=0x81||b[1]!=0x68||BitConverter.ToUInt16(b,4)!=29||
            BitConverter.ToUInt32(b,18)!=0||b[22]!=(attackerIsYou?1:0)||BitConverter.ToUInt32(b,23)!=Pistol)return false;
        uint result=BitConverter.ToUInt32(b,6);if(result!=3&&result!=4)return false;
        int second=BitConverter.ToUInt16(b,2);
        bool Name(int at,int end,string name)=>at>=29&&at+2<=b.Length&&end<=b.Length&&BitConverter.ToUInt16(b,at)>0&&
            at+2+BitConverter.ToUInt16(b,at)==end&&b[end-1]==0&&Encoding.ASCII.GetString(b,at+2,BitConverter.ToUInt16(b,at)-1)==name;
        if(!Name(29,second,target)||!Name(second,b.Length,shooter))return false;
        ushort damage=BitConverter.ToUInt16(b,10);short absorbed=BitConverter.ToInt16(b,12);if(result==4&&(damage!=0||absorbed!=0))return false;
        shot=new(result==3,damage,absorbed,BitConverter.ToUInt16(b,14),BitConverter.ToUInt16(b,16));return true;
    }
    public static bool CompleteStage(IEnumerable<byte[]> own,int ownCursor,IEnumerable<byte[]> target,int targetCursor,
        string shooterName,string targetName,int expected,out Shot[] shots)
    {
        shots=Array.Empty<Shot>();if(expected<1||ownCursor<0||targetCursor<0)return false;
        var self=own.Skip(ownCursor).ToArray();var other=target.Skip(targetCursor).ToArray();
        Shot[] Values(IEnumerable<byte[]> bytes,bool isYou)=>bytes.Select(b=>ReadShot(b,shooterName,targetName,isYou,out var value)?value:null).Where(v=>v!=null).Cast<Shot>().ToArray();
        var a=Values(self,true);var b=Values(other,false);
        if(self.Count(r=>r.Length==2&&r[0]==0x4e&&r[1]==0)!=expected||a.Length!=expected||b.Length!=expected||!a.SequenceEqual(b))return false;
        shots=a;return true;
    }
}
