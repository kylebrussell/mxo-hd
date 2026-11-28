using System;
using hds.world.Structures;
using MySqlConnector;

namespace hds.databases
{
    public partial class MyWorldDbAccess
    {
        public (uint FactionId, bool ClearedFirstMate)? TransferCrewCaptain(uint crew, uint captain,
            uint replacement, bool captainIsBot, CrewMember botReplacement)
        {
            if (crew == 0 || replacement == captain || replacement == 0) return null;

            using var connection = new MySqlConnection(connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();
            using var command = new MySqlCommand(null, connection, transaction);
            command.Parameters.AddWithValue("@crew", crew);
            command.Parameters.AddWithValue("@captain", captain);
            command.Parameters.AddWithValue("@replacement", replacement);
            command.CommandText = "SELECT master_player_handle,faction_id FROM crews WHERE id=@crew " +
                "AND (deleted_at IS NULL OR deleted_at < '1971-01-01') FOR UPDATE";
            string master;
            uint faction;
            using (var row = command.ExecuteReader())
            {
                if (!row.Read() || row.IsDBNull(0)) return null;
                master = row.GetString(0);
                faction = row.GetUInt32(1);
            }

            command.Parameters.AddWithValue("@master", master);
            if (captainIsBot)
            {
                if (!string.Equals(master, SocialBot.Name, StringComparison.OrdinalIgnoreCase)) return null;
            }
            else
            {
                command.CommandText = "SELECT p.charId FROM characters p JOIN crew_members m " +
                    "ON m.char_id=p.charId AND m.crew_id=p.crewId WHERE p.charId=@captain " +
                    "AND p.crewId=@crew AND p.handle=@master AND p.is_deleted=0 FOR UPDATE";
                if (command.ExecuteScalar() == null) return null;
            }

            // The dev-only SocialBot has a live roster entry but no persistent character row.
            string nextHandle = SocialBot.Name;
            bool clearFirstMate = botReplacement?.isFirstMate ?? false;
            if (botReplacement == null)
            {
                command.CommandText = "SELECT p.handle,m.is_first_mate FROM characters p JOIN crew_members m " +
                    "ON m.char_id=p.charId AND m.crew_id=p.crewId WHERE p.charId=@replacement " +
                    "AND p.crewId=@crew AND p.is_deleted=0 FOR UPDATE";
                using var row = command.ExecuteReader();
                if (!row.Read()) return null;
                nextHandle = row.GetString(0);
                clearFirstMate = row.GetBoolean(1);
                if (row.Read()) return null;
            }

            if (!WriteCrewCaptain(command, nextHandle, clearFirstMate)) return null;
            TransferFactionMaster(command, faction, master, nextHandle);
            transaction.Commit();
            return (faction, clearFirstMate);
        }

        private static bool WriteCrewCaptain(MySqlCommand command, string nextHandle, bool clearFirstMate)
        {
            command.Parameters.AddWithValue("@next", nextHandle);
            command.Parameters.AddWithValue("@clearFirstMate", clearFirstMate);
            command.CommandText = "UPDATE crew_members SET is_captain=(char_id=@replacement)," +
                "is_first_mate=IF(@clearFirstMate,0,is_first_mate) WHERE crew_id=@crew";
            command.ExecuteNonQuery();
            command.CommandText = "UPDATE crews SET master_player_handle=@next WHERE id=@crew";
            return command.ExecuteNonQuery() == 1;
        }

        private static bool TransferFactionMaster(MySqlCommand command, uint faction, string previous, string next)
        {
            if (faction == 0) return false;
            command.Parameters.AddWithValue("@faction", faction);
            command.Parameters.AddWithValue("@previousFactionMaster", previous);
            command.Parameters.AddWithValue("@nextFactionMaster", next);
            command.CommandText = "UPDATE factions SET master_player_handle=@nextFactionMaster WHERE id=@faction " +
                "AND master_player_handle=@previousFactionMaster AND (deleted_at IS NULL OR deleted_at < '1971-01-01')";
            return command.ExecuteNonQuery() == 1;
        }
    }
}
