using System;
using hds.world.Structures;
using MySqlConnector;

namespace hds.databases
{
    public partial class MyWorldDbAccess
    {
        public (uint FactionId, CrewMember Successor, bool FactionLeaderChanged, bool Archived, string Refusal)
            LeaveCrew(uint crew, uint member)
        {
            const string unavailable = "That crew membership is no longer available.";
            if (crew == 0) return (0, null, false, false, unavailable);

            using var connection = new MySqlConnection(connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();
            using var command = new MySqlCommand(null, connection, transaction);
            command.Parameters.AddWithValue("@crew", crew);
            command.Parameters.AddWithValue("@member", member);
            command.CommandText = "SELECT master_player_handle,faction_id FROM crews WHERE id=@crew " +
                "AND (deleted_at IS NULL OR deleted_at < '1971-01-01') FOR UPDATE";
            string master;
            uint faction;
            using (var row = command.ExecuteReader())
            {
                if (!row.Read()) return (0, null, false, false, unavailable);
                master = row.IsDBNull(0) ? "" : row.GetString(0);
                faction = row.GetUInt32(1);
            }

            command.CommandText = "SELECT handle FROM characters WHERE charId=@member AND crewId=@crew AND is_deleted=0 FOR UPDATE";
            if (!(command.ExecuteScalar() is string leaving)) return (0, null, false, false, unavailable);

            command.Parameters.AddWithValue("@master", master);
            command.CommandText = "SELECT p.charId,p.handle,m.is_first_mate FROM crew_members m JOIN characters p " +
                "ON p.charId=m.char_id AND p.crewId=m.crew_id WHERE m.crew_id=@crew AND p.charId<>@member AND p.is_deleted=0 " +
                "ORDER BY (p.handle=@master) DESC,m.is_captain DESC,m.is_first_mate DESC,m.id LIMIT 1 FOR UPDATE";
            CrewMember next = null;
            using (var row = command.ExecuteReader())
            {
                if (row.Read())
                    next = new CrewMember { charId = row.GetUInt32(0), handle = row.GetString(1), isFirstMate = row.GetBoolean(2) };
            }

            if (next == null)
            {
                command.CommandText = "SELECT charId FROM characters WHERE crewId=@crew AND charId<>@member AND is_deleted=0 LIMIT 1 FOR UPDATE";
                if (command.ExecuteScalar() != null)
                    return (faction, null, false, false, "The crew roster could not be reconciled. Your membership was kept.");
                if (CrewContainsFactionLeader(command, faction))
                    return (faction, null, false, false, "Disband the faction before leaving its last crew membership.");
                if (!ArchiveCrew(command)) return (faction, null, false, false, unavailable);
                transaction.Commit();
                return (faction, null, false, true, null);
            }

            command.CommandText = "DELETE FROM crew_members WHERE crew_id=@crew AND char_id=@member";
            command.ExecuteNonQuery();
            command.CommandText = "UPDATE characters SET crewId=0,factionId=0 WHERE charId=@member AND crewId=@crew";
            if (command.ExecuteNonQuery() != 1) return (faction, null, false, false, unavailable);
            bool captainChanged = !string.Equals(next.handle, master, StringComparison.OrdinalIgnoreCase);
            if (captainChanged)
            {
                command.Parameters.AddWithValue("@replacement", next.charId);
                if (!WriteCrewCaptain(command, next.handle, next.isFirstMate))
                    return (faction, null, false, false, unavailable);
            }

            bool factionLeaderChanged = TransferFactionMaster(command, faction, leaving, next.handle);
            transaction.Commit();
            return (faction, captainChanged ? next : null, factionLeaderChanged, false, null);
        }
    }
}
