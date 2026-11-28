using MySqlConnector;

namespace hds.databases
{
    public partial class MyWorldDbAccess
    {
        public string LeaveFaction(uint captain, uint crew, uint faction)
        {
            const string unavailable = "Only the captain of a crew in the faction can leave it.";
            if (crew == 0 || faction == 0) return unavailable;

            using var connection = new MySqlConnection(connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();
            using var command = new MySqlCommand(null, connection, transaction);
            command.Parameters.AddWithValue("@captain", captain);
            command.Parameters.AddWithValue("@crew", crew);
            command.Parameters.AddWithValue("@faction", faction);
            command.CommandText = "SELECT p.charId FROM characters p JOIN crews c ON c.id=p.crewId " +
                "AND c.master_player_handle=p.handle AND c.faction_id=p.factionId JOIN factions f ON f.id=c.faction_id " +
                "WHERE p.charId=@captain AND p.crewId=@crew AND p.factionId=@faction AND p.is_deleted=0 " +
                "AND (c.deleted_at IS NULL OR c.deleted_at < '1971-01-01') " +
                "AND (f.deleted_at IS NULL OR f.deleted_at < '1971-01-01') FOR UPDATE";
            if (command.ExecuteScalar() == null) return unavailable;
            if (CrewContainsFactionLeader(command, faction)) return "Disband the faction before its leading crew leaves.";
            if (!DetachCrewFromFaction(command, crew)) return unavailable;
            transaction.Commit();
            return null;
        }
    }
}
