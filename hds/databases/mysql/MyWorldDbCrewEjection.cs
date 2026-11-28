using MySqlConnector;

namespace hds.databases
{
    public partial class MyWorldDbAccess
    {
        public bool EjectCrewMember(uint captain, uint crew, uint member)
        {
            if (crew == 0 || member == captain) return false;

            using var connection = new MySqlConnection(connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();
            using var command = new MySqlCommand(null, connection, transaction);
            command.Parameters.AddWithValue("@captain", captain);
            command.Parameters.AddWithValue("@crew", crew);
            command.Parameters.AddWithValue("@member", member);
            command.CommandText = "SELECT c.id FROM crews c JOIN characters p " +
                "ON p.charId=@captain AND p.crewId=c.id AND p.handle=c.master_player_handle " +
                "WHERE c.id=@crew AND (c.deleted_at IS NULL OR c.deleted_at < '1971-01-01') AND p.is_deleted=0 FOR UPDATE";
            if (command.ExecuteScalar() == null) return false;

            command.CommandText = "DELETE FROM crew_members WHERE crew_id=@crew AND char_id=@member";
            if (command.ExecuteNonQuery() != 1) return false;
            command.CommandText = "UPDATE characters SET crewId=0,factionId=0 WHERE charId=@member AND crewId=@crew AND is_deleted=0";
            if (command.ExecuteNonQuery() != 1) return false;

            transaction.Commit();
            return true;
        }
    }
}
