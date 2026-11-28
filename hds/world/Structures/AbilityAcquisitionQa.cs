using System;
using hds.shared;
namespace hds
{
    /// <summary>Opens one existing vendor only; normal buy and upload handlers own all mutations.</summary>
    public static class AbilityAcquisitionQa
    {
        public const uint Vendor=277874480;
        public static bool Allowed(string opt,string dev,string database,uint character,bool ready,bool dead) =>
            opt=="1"&&character==110&&QaLoadout.Allowed(dev,database,character,ready,dead);
        public static bool Dev(WorldClient client,string[] words)
        {
            if (words.Length == 0 || (words[0] != "?abilityacquisition" && words[0] != "?abvendor")) return false;
            bool vendorRequest = words[0] == "?abvendor" ? words.Length == 1 : words.Length == 2 && words[1] == "vendor";
            var data=client.playerData;
            bool allowed=Allowed(Environment.GetEnvironmentVariable("MXO_ABILITY_ACQUISITION_QA"),Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"),
                Store.config?.dbParams?.DatabaseName,data.getCharID(),client.Alive&&data.getOnWorld()&&!data.worldChangePending&&!data.DistrictIsLA(),data.isDead);
            if(!ReferenceEquals(client,Store.currentClient)||!allowed||!vendorRequest||data.getDistrictId()!=1)
            {ClientActionsHandler.Notice(client,"Ability acquisition QA requires its explicit disposable CSR110 mode in the live Slums. Usage: ?abvendor");return true;}
            byte[] id=BitConverter.GetBytes(Vendor);new ObjectInteractionHandler().processVendorOpen(ref id);
            return true;
        }
    }
}
