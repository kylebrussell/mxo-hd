using System;
using MySqlConnector;

namespace hds.databases
{
    public partial class MyWorldDbAccess
    {
        public (uint Cash, string Refusal) TransferGroupMoney(uint character, byte type, uint groupId, uint amount, bool deposit)
        {
            const string unavailable = "That group bank is no longer available to you.";
            bool faction = type == SocialPackets.GroupFaction;
            if (groupId == 0 || (!faction && type != SocialPackets.GroupCrew)) return (0, unavailable);
            if (amount == 0) return (0, "Enter an amount greater than zero.");

            using var connection = new MySqlConnection(connectionString);
            connection.Open();
            using var tx = connection.BeginTransaction();
            using var command = new MySqlCommand(null, connection, tx);
            string membership = faction ? "factionId" : "crewId";
            string bankTable = faction ? "factions" : "crews";
            command.CommandText = "SELECT cash," + membership + " FROM characters WHERE charId=@char AND is_deleted=0 FOR UPDATE";
            command.Parameters.AddWithValue("@char", character);
            long cash;
            using (var row = command.ExecuteReader())
            {
                if (!row.Read() || Convert.ToUInt32(row.GetValue(1)) != groupId) return (0, unavailable);
                cash = row.GetInt32(0);
            }

            command.CommandText = "SELECT money FROM " + bankTable + " WHERE id=@group FOR UPDATE";
            command.Parameters.AddWithValue("@group", groupId);
            object balance = command.ExecuteScalar();
            if (balance == null) return (0, unavailable);
            long bank = Convert.ToInt64(balance);
            if (cash < 0 || bank < 0) return (0, "The transfer could not be saved. Your information was kept.");
            if (!FCHandler.CanMoveMoney(deposit, cash, bank, amount))
                return (0, deposit ? "You do not have that much information." : "The bank does not hold that much information.");

            cash += deposit ? -(long)amount : amount;
            bank += deposit ? amount : -(long)amount;
            if (cash > int.MaxValue || bank > (faction ? (long)int.MaxValue : uint.MaxValue))
                return (0, "The destination cannot hold that much information.");

            command.CommandText = "UPDATE " + bankTable + " SET money=@bank WHERE id=@group";
            command.Parameters.AddWithValue("@bank", bank);
            if (command.ExecuteNonQuery() != 1) return (0, unavailable);
            command.CommandText = "UPDATE characters SET cash=@cash WHERE charId=@char";
            command.Parameters.AddWithValue("@cash", cash);
            if (command.ExecuteNonQuery() != 1) return (0, unavailable);
            tx.Commit();
            return ((uint)cash, null);
        }
    }
}
