using System;
using hds.world.Structures;
using MySqlConnector;

namespace hds.databases
{
    public partial class MyWorldDbAccess
    {
        public (uint Status, ushort Rank) ChangeFactionRank(uint captain, uint crew, uint faction, uint targetCrew, bool promote)
        {
            if (faction == 0 || crew == 0) return (SocialPackets.FactionPromoterNotFound, 0);

            using var connection = new MySqlConnection(connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();
            using var command = new MySqlCommand(null, connection, transaction);
            command.Parameters.AddWithValue("@captain", captain);
            command.Parameters.AddWithValue("@crew", crew);
            command.Parameters.AddWithValue("@faction", faction);
            command.Parameters.AddWithValue("@target", targetCrew);
            command.CommandText = "SELECT c.faction_rank,c.master_player_handle,f.master_player_handle " +
                "FROM characters p JOIN crews c ON c.id=p.crewId AND c.master_player_handle=p.handle AND c.faction_id=p.factionId " +
                "JOIN factions f ON f.id=c.faction_id WHERE p.charId=@captain AND p.crewId=@crew AND p.factionId=@faction " +
                "AND p.is_deleted=0 AND (c.deleted_at IS NULL OR c.deleted_at < '1971-01-01') " +
                "AND (f.deleted_at IS NULL OR f.deleted_at < '1971-01-01') FOR UPDATE";
            ushort actorRank;
            string factionLeader;
            using (var row = command.ExecuteReader())
            {
                if (!row.Read()) return (SocialPackets.FactionPromoterNotFound, 0);
                factionLeader = row.GetString(2);
                actorRank = FactionRanks.Effective(unchecked((ushort)row.GetInt16(0)),
                    string.Equals(row.GetString(1), factionLeader, StringComparison.OrdinalIgnoreCase));
            }

            command.CommandText = "SELECT faction_rank,master_player_handle FROM crews WHERE id=@target AND faction_id=@faction " +
                "AND (deleted_at IS NULL OR deleted_at < '1971-01-01') FOR UPDATE";
            ushort targetRank;
            using (var row = command.ExecuteReader())
            {
                if (!row.Read()) return (SocialPackets.FactionPromoteeNotFound, 0);
                targetRank = FactionRanks.Effective(unchecked((ushort)row.GetInt16(0)),
                    string.Equals(row.GetString(1), factionLeader, StringComparison.OrdinalIgnoreCase));
            }

            uint status = FactionRanks.Change(actorRank, targetRank, promote, out ushort rank);
            if (status != SocialPackets.ResultOk) return (status, 0);

            command.Parameters.AddWithValue("@rank", rank);
            command.CommandText = "UPDATE crews SET faction_rank=@rank WHERE id=@target AND faction_id=@faction";
            if (command.ExecuteNonQuery() != 1) return (SocialPackets.FactionPromoteeNotFound, 0);

            transaction.Commit();
            return (SocialPackets.ResultOk, rank);
        }
    }
}
