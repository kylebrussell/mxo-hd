using System;
using MySqlConnector;
namespace hds.databases
{
    public partial class MyWorldDbAccess
    {
        /// <summary>Compare the selected row and atomically remove it and credit the signed-INT account.</summary>
        public InventoryPayout ExchangeInventoryForInfo(uint character,ushort slot,uint expectedGoid,int expectedCount,uint quote,bool storage=false)
        {
            if((storage?!CentralStorage.IsStorageSlot(slot):!Crafting.IsBagSlot(slot))||expectedGoid==0)return null;
            // Inventory and characters are converted by the existing startup migrations before listeners open.
            using var connection=new MySqlConnection(connectionString);connection.Open();
            using var tx=connection.BeginTransaction();using var c=connection.CreateCommand();c.Transaction=tx;
            c.Parameters.AddWithValue("@char",character);c.Parameters.AddWithValue("@slot",slot);
            c.Parameters.AddWithValue("@goid",expectedGoid);c.Parameters.AddWithValue("@count",Math.Max(1,expectedCount));
            if(storage)
            {
                c.Parameters.AddWithValue("@code",CodeStorage.BaseValue(expectedGoid));
                c.CommandText="DELETE FROM char_abilities WHERE char_id=@char AND slot=@slot AND ability_id=@code AND level=@count-1 LIMIT 1";
            }
            else c.CommandText="DELETE FROM inventory WHERE charId=@char AND slot=@slot AND goid=@goid AND GREATEST(count,1)=@count LIMIT 1";
            if(c.ExecuteNonQuery()!=1)return null;
            c.CommandText="SELECT cash FROM characters WHERE charId=@char FOR UPDATE";
            object raw=c.ExecuteScalar();if(raw==null||raw==DBNull.Value)return null;
            uint cash=VendorTrading.Credit(Convert.ToInt64(raw),quote,out uint credited);
            c.Parameters.AddWithValue("@cash",cash);
            c.CommandText="UPDATE characters SET cash=@cash WHERE charId=@char";c.ExecuteNonQuery();
            tx.Commit();return new InventoryPayout{Cash=cash,Credited=credited};
        }
    }
}
