using System;
using MySqlConnector;

namespace hds.databases
{
    public partial class MyWorldDbAccess
    {
        public (uint FactionId, uint Refusal) AdmitCrewMember(uint crew, uint inviter, uint invitee,
            bool inviterIsBot, bool inviteeIsBot, string foundingName)
        {
            if (crew == 0 || inviter == invitee) return (0, SocialPackets.CrewRequestError);

            using var connection = new MySqlConnection(connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();
            using var command = new MySqlCommand(null, connection, transaction);
            command.Parameters.AddWithValue("@crew", crew);
            command.Parameters.AddWithValue("@inviter", inviter);
            command.Parameters.AddWithValue("@invitee", invitee);
            command.Parameters.AddWithValue("@foundingName", (object) foundingName ?? DBNull.Value);
            command.CommandText = "SELECT faction_id FROM crews WHERE id=@crew " +
                "AND (deleted_at IS NULL OR deleted_at < '1971-01-01') " +
                "AND (@foundingName IS NULL OR crew_name=@foundingName) FOR UPDATE";
            object affiliation = command.ExecuteScalar();
            if (affiliation == null) return (0, SocialPackets.CrewRequestError);
            uint faction = Convert.ToUInt32(affiliation);

            if (!inviterIsBot)
            {
                command.CommandText = "SELECT charId FROM characters WHERE charId=@inviter " +
                    "AND crewId=@crew AND is_deleted=0 FOR UPDATE";
                if (command.ExecuteScalar() == null) return (0, SocialPackets.CrewRequestError);
            }

            // SocialBot's membership is held in memory. It has no character or roster row to write.
            if (!inviteeIsBot)
            {
                command.CommandText = "SELECT charId FROM characters WHERE charId=@invitee " +
                    "AND crewId=0 AND factionId=0 AND is_deleted=0 FOR UPDATE";
                if (command.ExecuteScalar() == null) return (0, SocialPackets.CrewCurrentMember);
                command.CommandText = "SELECT id FROM crew_members WHERE crew_id=@crew AND char_id=@invitee LIMIT 1 FOR UPDATE";
                if (command.ExecuteScalar() != null) return (0, SocialPackets.CrewCurrentMember);
                command.CommandText = "INSERT INTO crew_members(crew_id,char_id,is_captain,is_first_mate,created_at) " +
                    "VALUES(@crew,@invitee,0,0,NOW())";
                command.ExecuteNonQuery();
                command.Parameters.AddWithValue("@faction", faction);
                command.CommandText = "UPDATE characters SET crewId=@crew,factionId=@faction WHERE charId=@invitee";
                if (command.ExecuteNonQuery() != 1) return (0, SocialPackets.CrewRequestError);
            }

            transaction.Commit();
            return (faction, SocialPackets.ResultOk);
        }
    }
}
