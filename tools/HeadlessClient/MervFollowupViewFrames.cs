using hds;
namespace HeadlessClient;

/// <summary>Bounded producer-backed frames at the current cursor only. No resynchronization.</summary>
public static class MervFollowupViewFrames
{
    public enum Kind { Attribute, OwnTeleport, Movement, Death, Delete }
    public sealed record Frame(Kind Type,int Length,ushort View,int PositionOffset=-1);
    public static bool TryRead(byte[] bytes,int at,ushort? goid,out Frame frame)
    {
        frame=null!;
        if(at<0||at+2>bytes.Length)return false;
        ushort view=BitConverter.ToUInt16(bytes,at);
        bool Has(int offset,string hex)=>at+offset+hex.Length/2<=bytes.Length&&bytes.AsSpan(at+offset,hex.Length/2).SequenceEqual(Convert.FromHexString(hex));
        bool Fits(int length)=>at+length<=bytes.Length;
        Frame Make(Kind kind,int length,int position=-1)=>new(kind,length,view,position);
        if(Has(0,"0100010100")&&Fits(7)){frame=new(Kind.Delete,7,BitConverter.ToUInt16(bytes,at+5));return true;}
        if(view==2&&goid==12)
        {
            if(Has(2,"011c")&&Fits(28)){frame=Make(Kind.OwnTeleport,28);return true;}
            // PlayerPackets mode/health, MissionPackets key, SocialPackets team.
            foreach(var shape in new[]{("0280808080808002",11),("0280808040",9),("028040",9),("028020",9)})
                if(Has(2,shape.Item1)&&Fits(shape.Item2)){frame=Make(Kind.Attribute,shape.Item2);return true;}
            // CombatPackets.SendLeaveCombatMode: action0 plus flags attribute.
            if(Has(2,"0301000080808080808001")&&Fits(17)){frame=Make(Kind.Attribute,17);return true;}
            if(Has(2,"0328")&&Has(5,"010c")&&(Has(8,"8100")||Has(8,"0010")))
            {
                if(Has(30,"8084")&&Has(33,"8080808003")&&Fits(43))
                {frame=Make(Kind.Attribute,43);return true;}
                if(Has(30,"8004")&&Fits(33)){frame=Make(Kind.Attribute,33);return true;}
            }
            return false;
        }
        if(goid==599)
        {
            if(Has(2,"0480808080808002")&&Fits(14)){frame=Make(Kind.Attribute,14);return true;}
            if(Has(2,"0480808040")&&Fits(9)){frame=Make(Kind.Attribute,9);return true;}
            if(Has(2,"04808080c0")&&Has(9,"c0")&&Has(14,"01")&&Fits(16))
            {frame=Make(Kind.Attribute,16);return true;}
            if(Has(2,"020e")&&Fits(18)){frame=Make(Kind.Movement,18,6);return true;}
            if(Has(2,"022a")&&Has(5,"010c")&&(Has(8,"8100")||Has(8,"0010"))&&Has(14,"00")&&Fits(27))
            {frame=Make(Kind.Movement,27,15);return true;}
            // Only stance13 has a closed emitting producer. A27-byte prefix is incomplete.
            if(Has(2,"07011d0d00")&&Has(8,"01002b002500")&&Has(27,"c202008080e0010000808003fd07000000000000")&&Fits(47))
            {frame=Make(Kind.Death,47,15);return true;}
            if(Has(2,"0501c202008080e0010000808003fd07000000000000")&&Fits(24))
            {frame=Make(Kind.Death,24);return true;}
            return false;
        }
        if(goid.HasValue&&goid!=12)
        {
            // Exact class-specific CurrentState mask; never treat NPC attributes as static state.
            var state=ObjectStates.BuildStateUpdate(goid.Value,0);
            if(state!=null&&state.Length>=4&&Fits(2+state.Length)&&
                bytes.AsSpan(at+2,state.Length-4).SequenceEqual(state.AsSpan(0,state.Length-4)))
            {frame=Make(Kind.Attribute,2+state.Length);return true;}
            // DoorLifecycle reset is only valid for the door's CurrentState mask.
            if(state!=null&&state.AsSpan(0,state.Length-4).SequenceEqual(Convert.FromHexString("018002"))&&
                Has(2,"01ff000000000000000000000000000300000000")&&Fits(26))
            {frame=Make(Kind.Attribute,26);return true;}
        }
        return false;
    }
}
