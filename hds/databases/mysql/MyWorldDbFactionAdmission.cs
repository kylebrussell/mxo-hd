using System;
using MySqlConnector;

namespace hds.databases
{
    public partial class MyWorldDbAccess
    {
        public bool AdmitCrewToFaction(uint captain, uint crew, uint inviter, uint faction)
        {
            if (crew == 0 || faction == 0) return false;

            using var connection = new MySqlConnection(connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();
            using var command = new MySqlCommand(null, connection, transaction);
            command.Parameters.AddWithValue("@captain", captain);
            command.Parameters.AddWithValue("@crew", crew);
            command.Parameters.AddWithValue("@inviter", inviter);
            command.Parameters.AddWithValue("@faction", faction);

            command.CommandText = "SELECT handle FROM characters WHERE charId=@captain AND crewId=@crew " +
                "AND factionId=0 AND is_deleted=0 FOR UPDATE";
            if (!(command.ExecuteScalar() is string captainHandle)) return false;
            command.CommandText = "SELECT master_player_handle FROM crews WHERE id=@crew AND faction_id=0 " +
                "AND (deleted_at IS NULL OR deleted_at < '1971-01-01') FOR UPDATE";
            if (!(command.ExecuteScalar() is string crewMaster) ||
                !string.Equals(captainHandle, crewMaster, StringComparison.OrdinalIgnoreCase)) return false;

            command.CommandText = "SELECT f.id FROM factions f JOIN characters leader " +
                "ON leader.charId=@inviter AND leader.factionId=f.id AND leader.handle=f.master_player_handle " +
                "WHERE f.id=@faction AND (f.deleted_at IS NULL OR f.deleted_at < '1971-01-01') AND leader.is_deleted=0 FOR UPDATE";
            if (command.ExecuteScalar() == null) return false;

            command.CommandText = "UPDATE crews SET faction_id=@faction,faction_rank=@rank WHERE id=@crew";
            command.Parameters.AddWithValue("@rank", SocialPackets.BottomFactionRank);
            command.ExecuteNonQuery();
            command.CommandText = "UPDATE characters SET factionId=@faction WHERE crewId=@crew";
            command.ExecuteNonQuery();
            transaction.Commit();
            return true;
        }
    }
}
