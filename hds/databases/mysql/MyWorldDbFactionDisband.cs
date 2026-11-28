using MySqlConnector;

namespace hds.databases
{
    public partial class MyWorldDbAccess
    {
        public bool DisbandFaction(uint leader, uint faction)
        {
            if (faction == 0) return false;

            using var connection = new MySqlConnection(connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();
            using var command = new MySqlCommand(null, connection, transaction);
            command.Parameters.AddWithValue("@leader", leader);
            command.Parameters.AddWithValue("@faction", faction);
            command.CommandText = "SELECT f.id FROM factions f JOIN characters p " +
                "ON p.charId=@leader AND p.factionId=f.id AND p.handle=f.master_player_handle " +
                "WHERE f.id=@faction AND (f.deleted_at IS NULL OR f.deleted_at < '1971-01-01') AND p.is_deleted=0 FOR UPDATE";
            if (command.ExecuteScalar() == null) return false;

            command.CommandText = "UPDATE factions SET deleted_at=NOW() WHERE id=@faction";
            if (command.ExecuteNonQuery() != 1) return false;
            command.CommandText = "UPDATE crews SET faction_id=0,faction_rank=0 WHERE faction_id=@faction";
            command.ExecuteNonQuery();
            command.CommandText = "UPDATE characters SET factionId=0 WHERE factionId=@faction";
            command.ExecuteNonQuery();
            transaction.Commit();
            return true;
        }
    }
}
