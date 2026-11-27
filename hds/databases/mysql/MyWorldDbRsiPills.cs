using System;
using MySqlConnector;

namespace hds.databases
{
    public partial class MyWorldDbAccess
    {
        private const string RsiColumns = "sex,body,hat,face,shirt,coat,pants,shoes,gloves,glasses,hair,facialdetail," +
            "shirtcolor,pantscolor,coatcolor,shoecolor,glassescolor,haircolor,skintone,tattoo,facialdetailcolor,leggins";

        /// <summary>Persist a permanent RSI change and spend exactly one matching item in one transaction.</summary>
        public (int[] Rsi, int Remaining) ConsumeRsiPill(uint charId, ushort slot, uint goid)
        {
            if (slot < 1 || slot > LastBagSlot || !RsiPills.TryGet(goid, out var pill)) return default;
            // Startup prepares the transactional inventory and rsivalues tables before any client can use an item.
            using var connection = new MySqlConnection(connectionString);
            connection.Open();
            using var tx = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = tx;
            int remaining = LockOwnedBagItem(command, charId, slot, goid, out uint id);
            if (remaining < 0) return default;

            command.CommandText = "SELECT " + RsiColumns + " FROM rsivalues WHERE charid=@char FOR UPDATE";
            var rsi = new int[22];
            using (var row = command.ExecuteReader())
            {
                if (!row.Read()) return default;
                for (int i = 0; i < rsi.Length; i++) rsi[i] = row.GetInt16(i);
            }
            int value = pill.Resolve(rsi[0], DataLoader.getInstance().newRSIItemsDb);
            if (value < 0) return default;

            if (!WriteBagCount(command, id, remaining)) return default;

            int index = (int)pill.Field;
            if (rsi[index] != value)
            {
                string column = pill.Field.ToString().ToLowerInvariant();
                command.Parameters.AddWithValue("@value", value);
                command.CommandText = "UPDATE rsivalues SET " + column + "=@value WHERE charid=@char";
                if (command.ExecuteNonQuery() != 1) return default;
                rsi[index] = value;
            }
            tx.Commit();
            return (rsi, remaining);
        }
    }
}
