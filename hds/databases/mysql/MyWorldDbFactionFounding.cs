using System;
using System.Data;
using MySqlConnector;

namespace hds.databases
{
    public partial class MyWorldDbAccess
    {
        public (uint FactionId, uint Refusal) FoundFaction(uint founder, uint crew, string name)
        {
            if (crew == 0) return (0, SocialPackets.FactionInvalidFounder);
            if (string.IsNullOrWhiteSpace(name) || name.Length > 41)
                return (0, SocialPackets.FactionInvalidTeamName);

            using var connection = new MySqlConnection(connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction(IsolationLevel.RepeatableRead);
            using var command = new MySqlCommand(null, connection, transaction);
            command.Parameters.AddWithValue("@founder", founder);
            command.Parameters.AddWithValue("@crew", crew);
            command.CommandText = "SELECT handle FROM characters WHERE charId=@founder AND crewId=@crew " +
                "AND factionId=0 AND is_deleted=0 FOR UPDATE";
            if (!(command.ExecuteScalar() is string handle)) return (0, SocialPackets.FactionInvalidFounder);

            command.CommandText = "SELECT master_player_handle FROM crews WHERE id=@crew AND faction_id=0 " +
                "AND (deleted_at IS NULL OR deleted_at < '1971-01-01') FOR UPDATE";
            if (!(command.ExecuteScalar() is string captain) ||
                !string.Equals(handle, captain, StringComparison.OrdinalIgnoreCase))
                return (0, SocialPackets.FactionInvalidFounder);

            // The locking name check also prevents two founders from reserving the same unused name.
            command.Parameters.AddWithValue("@name", name);
            command.CommandText = "SELECT id FROM factions WHERE name=@name AND deleted_at IS NULL LIMIT 1 FOR UPDATE";
            if (command.ExecuteScalar() != null) return (0, SocialPackets.FactionInvalidTeamName);

            command.Parameters.AddWithValue("@master", handle);
            command.CommandText = "INSERT INTO factions(name,master_player_handle,money) VALUES (@name,@master,0)";
            command.ExecuteNonQuery();
            uint faction = checked((uint)command.LastInsertedId);

            command.Parameters.AddWithValue("@faction", faction);
            command.Parameters.AddWithValue("@rank", SocialPackets.TopFactionRank);
            command.CommandText = "UPDATE crews SET faction_id=@faction,faction_rank=@rank WHERE id=@crew";
            command.ExecuteNonQuery();
            command.CommandText = "UPDATE characters SET factionId=@faction WHERE crewId=@crew";
            command.ExecuteNonQuery();
            transaction.Commit();
            return (faction, SocialPackets.ResultOk);
        }
    }
}
