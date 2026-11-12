using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using hds.shared;
namespace hds
{
    /// <summary>Hardline filename ordinals advertised by Margin09. The six appended worlds/order are OURS policy;
    /// Universe IDs come from shipped universe.btb. This is NOT the 06/PvP construct namespace.</summary>
    public static class HardlineRegistration
    {
        private static readonly ushort[] maps = {1,2,3,12,13,14,15,16,17};
        private static readonly ushort[] universes = {12,7,169,379,384,377,382,383,381};
        public static bool TryMap(uint logical, out ushort map)
        { map=0; if(logical<1 || logical>maps.Length)return false;map=maps[logical-1];return true; }
        public static bool TryLogical(ushort map,out byte logical)
        {int index=Array.IndexOf(maps,map);logical=(byte)(index+1);return index>=0;}
        public static byte[] LoginReply()
        {
            using(var stream=new MemoryStream())using(var w=new BinaryWriter(stream))
            {w.Write(new byte[]{9,0,0,0,0,0,0,0,0,0x3e,0x17,0x26,0x70,15,0});
             w.Write((ushort)universes.Length);foreach(ushort id in universes)w.Write(id);return stream.ToArray();}
        }
        public static List<ushort> Known(uint character,ushort map)
        {
            if(!TryLogical(map,out _))return new List<ushort>();
            var existing=HardlineNetwork.Ids(map);
            return Store.worldConfig.HardlineUploadRequired
                ? Store.dbManager.WorldDbHandler.GetUploadedHardlines(character,map).Where(existing.Contains).Distinct().OrderBy(x=>x).ToList()
                : existing;
        }
        public static bool Destination(uint character,uint logical,uint hid,out ushort map)
        {return Destination(logical,hid,m=>Known(character,m),out map);}
        public static bool Destination(uint logical,uint hid,Func<ushort,IEnumerable<ushort>> known,out ushort map)
        {map=0;return hid>0 && hid<=ushort.MaxValue && TryMap(logical,out map) && known(map).Contains((ushort)hid);}
        public static byte[] KnownCache(uint character)
        {return Cache(map=>Known(character,map));}
        public static byte[] Cache(Func<ushort,IEnumerable<ushort>> known)
        {
            using(var stream=new MemoryStream())using(var w=new BinaryWriter(stream))
            {for(int i=0;i<maps.Length;i++)foreach(ushort hid in known(maps[i]).Distinct().OrderBy(x=>x))
                {w.Write((byte)(i+1));w.Write((uint)hid);}return stream.ToArray();}
        }
    }
}
