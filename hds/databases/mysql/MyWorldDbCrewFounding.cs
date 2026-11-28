using System;
using System.Data;
using MySqlConnector;

namespace hds.databases
{
    public partial class MyWorldDbAccess
    {
        public (uint CrewId, uint Refusal) FoundCrew(uint founder, uint invitee, bool inviteeIsBot, string name, byte org)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > 31)
                return (0, SocialPackets.CrewCreateInvalidName);
            if (founder == invitee) return (0, SocialPackets.CrewInvalidCharacterName);

            using var connection = new MySqlConnection(connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction(IsolationLevel.RepeatableRead);
            using var command = new MySqlCommand(null, connection, transaction);
            command.Parameters.AddWithValue("@founder", founder);
            command.Parameters.AddWithValue("@invitee", inviteeIsBot ? DBNull.Value : (object) invitee);
            command.Parameters.AddWithValue("@name", name.Trim());
            command.CommandText = "SELECT charId,handle,crewId,factionId FROM characters " +
                "WHERE charId IN (@founder,@invitee) AND is_deleted=0 ORDER BY charId FOR UPDATE";
            string captain = null;
            bool inviteeFound = inviteeIsBot;
            using (var row = command.ExecuteReader())
            {
                while (row.Read())
                {
                    if (row.GetUInt32(2) != 0 || row.GetUInt32(3) != 0)
                        return (0, SocialPackets.CrewCreateAlreadyMember);
                    if (row.GetUInt32(0) == founder) captain = row.GetString(1);
                    else inviteeFound = true;
                }
            }
            if (captain == null || !inviteeFound) return (0, SocialPackets.CrewInvalidCharacterName);

            command.CommandText = "SELECT id FROM crews WHERE crew_name=@name " +
                "AND (deleted_at IS NULL OR deleted_at < '1971-01-01') LIMIT 1 FOR UPDATE";
            if (command.ExecuteScalar() != null) return (0, SocialPackets.CrewCreateInvalidName);
            command.Parameters.AddWithValue("@captain", captain);
            command.Parameters.AddWithValue("@org", org);
            command.CommandText = "INSERT INTO crews(crew_name,master_player_handle,money,org,faction_id,faction_rank,created_at,deleted_at) " +
                "VALUES(@name,@captain,0,@org,0,0,NOW(),NULL)";
            command.ExecuteNonQuery();
            uint crew = checked((uint) command.LastInsertedId);

            command.Parameters.AddWithValue("@crew", crew);
            command.CommandText = "INSERT INTO crew_members(crew_id,char_id,is_captain,is_first_mate,created_at) " +
                "VALUES(@crew,@founder,1,0,NOW())" + (inviteeIsBot ? "" : ",(@crew,@invitee,0,0,NOW())");
            command.ExecuteNonQuery();
            command.CommandText = "UPDATE characters SET crewId=@crew WHERE charId IN (@founder,@invitee)";
            if (command.ExecuteNonQuery() != (inviteeIsBot ? 1 : 2)) return (0, SocialPackets.CrewCreateServerError);

            transaction.Commit();
            return (crew, SocialPackets.ResultOk);
        }
    }
}
