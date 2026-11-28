using System;
using MySqlConnector;

namespace hds.databases
{
    public partial class MyWorldDbAccess
    {
        public bool ChangeCrewFirstMate(uint crew, uint captain, uint target, bool captainIsBot, bool targetIsBot)
        {
            if (crew == 0 || target == captain) return false;

            using var connection = new MySqlConnection(connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();
            using var command = new MySqlCommand(null, connection, transaction);
            command.Parameters.AddWithValue("@crew", crew);
            command.Parameters.AddWithValue("@captain", captain);
            command.Parameters.AddWithValue("@target", target);
            command.CommandText = "SELECT master_player_handle FROM crews WHERE id=@crew " +
                "AND (deleted_at IS NULL OR deleted_at < '1971-01-01') FOR UPDATE";
            if (!(command.ExecuteScalar() is string master)) return false;

            command.Parameters.AddWithValue("@master", master);
            if (captainIsBot)
            {
                if (!string.Equals(master, SocialBot.Name, StringComparison.OrdinalIgnoreCase)) return false;
            }
            else
            {
                command.CommandText = "SELECT p.charId FROM characters p JOIN crew_members m " +
                    "ON m.char_id=p.charId AND m.crew_id=p.crewId WHERE p.charId=@captain " +
                    "AND p.crewId=@crew AND p.handle=@master AND p.is_deleted=0 FOR UPDATE";
                if (command.ExecuteScalar() == null) return false;
            }

            if (target != 0 && !targetIsBot)
            {
                command.CommandText = "SELECT p.charId FROM characters p JOIN crew_members m " +
                    "ON m.char_id=p.charId AND m.crew_id=p.crewId WHERE p.charId=@target AND p.crewId=@crew " +
                    "AND m.is_captain=0 AND p.handle<>@master AND p.is_deleted=0 FOR UPDATE";
                if (command.ExecuteScalar() == null) return false;
            }

            command.CommandText = "UPDATE crew_members SET is_first_mate=0 WHERE crew_id=@crew AND is_first_mate<>0";
            command.ExecuteNonQuery();
            if (target != 0 && !targetIsBot)
            {
                command.CommandText = "UPDATE crew_members SET is_first_mate=1 WHERE crew_id=@crew AND char_id=@target";
                if (command.ExecuteNonQuery() != 1) return false;
            }

            transaction.Commit();
            return true;
        }
    }
}
