using System;
using hds.world.Structures;
using MySqlConnector;

namespace hds.databases
{
    public partial class MyWorldDbAccess
    {
        public bool EjectCrewFromFaction(uint captain, uint captainCrew, uint faction, uint targetCrew)
        {
            if (faction == 0 || captainCrew == 0 || targetCrew == 0 || targetCrew == captainCrew) return false;

            using var connection = new MySqlConnection(connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();
            using var command = new MySqlCommand(null, connection, transaction);
            command.Parameters.AddWithValue("@captain", captain);
            command.Parameters.AddWithValue("@captainCrew", captainCrew);
            command.Parameters.AddWithValue("@faction", faction);
            command.Parameters.AddWithValue("@targetCrew", targetCrew);
            command.CommandText = "SELECT p.handle,c.master_player_handle,c.faction_rank,f.master_player_handle " +
                "FROM characters p JOIN crews c ON c.id=p.crewId AND c.faction_id=p.factionId " +
                "JOIN factions f ON f.id=c.faction_id " +
                "WHERE p.charId=@captain AND p.crewId=@captainCrew AND p.factionId=@faction AND p.is_deleted=0 " +
                "AND (c.deleted_at IS NULL OR c.deleted_at < '1971-01-01') " +
                "AND (f.deleted_at IS NULL OR f.deleted_at < '1971-01-01') FOR UPDATE";
            using (var row = command.ExecuteReader())
            {
                if (!row.Read() || !string.Equals(row.GetString(0), row.GetString(1), StringComparison.OrdinalIgnoreCase))
                    return false;
                bool leadsFaction = string.Equals(row.GetString(1), row.GetString(3), StringComparison.OrdinalIgnoreCase);
                if (FactionRanks.Effective(unchecked((ushort)row.GetInt16(2)), leadsFaction) != SocialPackets.TopFactionRank)
                    return false;
            }

            command.CommandText = "UPDATE crews SET faction_id=0,faction_rank=0 WHERE id=@targetCrew AND faction_id=@faction " +
                "AND (deleted_at IS NULL OR deleted_at < '1971-01-01')";
            if (command.ExecuteNonQuery() != 1) return false;
            command.CommandText = "UPDATE characters SET factionId=0 WHERE crewId=@targetCrew";
            command.ExecuteNonQuery();
            transaction.Commit();
            return true;
        }
    }
}
