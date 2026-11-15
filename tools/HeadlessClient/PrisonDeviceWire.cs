namespace HeadlessClient;
/// <summary>Only the two reviewed equipped devices; ordinary native activation uses flag0.</summary>
public static class PrisonDeviceWire
{
    public static byte[] Activate(ushort ability,byte slot=106)
    {
        if(ability!=949&&ability!=957)throw new ArgumentOutOfRangeException(nameof(ability));
        var rpc=new byte[43];rpc[0]=0x80;rpc[1]=0xb9;BitConverter.GetBytes(ability).CopyTo(rpc,2);
        rpc[36]=1; // body34 is the separate state/tactic byte, not the tool flag
        rpc[37]=slot; // body35: actual inventory/equipment slot
        rpc[38]=0; // body36: ordinary equipped activation (621ed630), not compiler close policy
        return rpc;
    }
}
