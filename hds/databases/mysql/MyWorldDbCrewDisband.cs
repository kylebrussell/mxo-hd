using MySqlConnector;

namespace hds.databases
{
    public partial class MyWorldDbAccess
    {
        public (uint FactionId, string Refusal) DisbandCrew(uint crew, uint? captain)
        {
            const string unavailable = "Only the captain of an active crew can disband it.";
            if (crew == 0) return (0, unavailable);

            using var connection = new MySqlConnection(connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();
            using var command = new MySqlCommand(null, connection, transaction);
            command.Parameters.AddWithValue("@crew", crew);
            command.CommandText = "SELECT master_player_handle,faction_id FROM crews WHERE id=@crew " +
                "AND (deleted_at IS NULL OR deleted_at < '1971-01-01') FOR UPDATE";
            string master;
            uint faction;
            using (var row = command.ExecuteReader())
            {
                if (!row.Read()) return (0, unavailable);
                master = row.IsDBNull(0) ? "" : row.GetString(0);
                faction = row.GetUInt32(1);
            }

            if (captain.HasValue)
            {
                command.Parameters.AddWithValue("@captain", captain.Value);
                command.Parameters.AddWithValue("@master", master);
                command.CommandText = "SELECT charId FROM characters WHERE charId=@captain AND crewId=@crew " +
                    "AND handle=@master AND is_deleted=0 FOR UPDATE";
                if (command.ExecuteScalar() == null) return (0, unavailable);
                if (faction != 0)
                {
                    command.Parameters.AddWithValue("@faction", faction);
                    command.CommandText = "SELECT f.id FROM factions f JOIN characters p ON p.handle=f.master_player_handle " +
                        "WHERE f.id=@faction AND p.crewId=@crew AND p.is_deleted=0 " +
                        "AND (f.deleted_at IS NULL OR f.deleted_at < '1971-01-01') FOR UPDATE";
                    if (command.ExecuteScalar() != null)
                        return (0, "Disband the faction before disbanding its leading crew.");
                }
            }
            else
            {
                // Leaving the last membership can archive an empty crew without a captain.
                command.CommandText = "SELECT charId FROM characters WHERE crewId=@crew AND is_deleted=0 LIMIT 1 FOR UPDATE";
                if (command.ExecuteScalar() != null) return (0, "The crew still has members.");
            }

            command.CommandText = "UPDATE crews SET deleted_at=NOW(),faction_id=0,faction_rank=0 WHERE id=@crew";
            if (command.ExecuteNonQuery() != 1) return (0, unavailable);
            command.CommandText = "DELETE FROM crew_members WHERE crew_id=@crew";
            command.ExecuteNonQuery();
            command.CommandText = "UPDATE characters SET crewId=0,factionId=0 WHERE crewId=@crew";
            command.ExecuteNonQuery();
            transaction.Commit();
            return (faction, null);
        }
    }
}
