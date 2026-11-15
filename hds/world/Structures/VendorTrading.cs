using System;
namespace hds
{
    /// <summary>Native economy request fields; all prices are information, never code bits.</summary>
    public static class VendorTrading
    {
        // client.dll6210a2a0/6210ab10: Appraise byte then Slot byte, not a two-byte slot.
        public static bool TrySellRequest(byte[] body,out bool appraise,out byte slot)
        {
            appraise=false;slot=0;
            if(body==null || body.Length!=2 || body[0]>1 || !Crafting.IsBagSlot(body[1]))return false;
            appraise=body[0]!=0;slot=body[1];return true;
        }
        // client.dll621d2480: Slot u16LE, PersonalInventoryItem byte, RecycleItem byte.
        public static bool TryRecycleRequest(byte[] body,out ushort slot,out bool personal,out bool recycle)
        {
            slot=0;personal=false;recycle=false;
            if(body==null || body.Length!=4 || body[2]>1 || body[3]>1)return false;
            slot=(ushort)(body[0]|body[1]<<8);personal=body[2]!=0;recycle=body[3]!=0;
            return personal?Crafting.IsBagSlot(slot):CentralStorage.IsStorageSlot(slot);
        }
        // OURS account boundary: legacy characters.cash is SIGNED INT. Quote and actual credited delta are separate.
        public static uint Credit(long cash,uint quote,out uint credited)
        {
            long before=Math.Clamp(cash,0,int.MaxValue);
            uint after=(uint)Math.Min(before+(long)quote,int.MaxValue);
            credited=after-(uint)before;return after;
        }
    }
    public sealed class InventoryPayout
    {
        public uint Cash,Credited;
    }
}
