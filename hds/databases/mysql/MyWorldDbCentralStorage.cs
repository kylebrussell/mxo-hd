using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using hds.shared;
using MySqlConnector;

namespace hds.databases
{
    public partial class MyWorldDbAccess
    {
        public CentralStorage.Receipt MoveCentralStorage(uint character, CentralStorage.Move move) =>
            TransferStorage(character, move, move.Source, move.Dest, null);

        public CentralStorage.Receipt MoveCentralStorageStack(uint character, ushort source, ushort dest, uint? splitAmount) =>
            TransferStorage(character, null, source, dest, splitAmount);

        private CentralStorage.Receipt TransferStorage(uint character, CentralStorage.Move move,
            ushort source, ushort dest, uint? splitAmount)
        {
            CentralStorage.Receipt Failed() => new CentralStorage.Receipt(
                CentralStorage.Plan.Refuse(CentralStorage.InvalidTransaction), rolledBack: true);

            try
            {
                using var connection = new MySqlConnection(connectionString);
                connection.Open();
                // The existing startup checks require these tables to be transactional.
                using var tx = connection.BeginTransaction();
                using (var owner = new MySqlCommand("SELECT charId FROM characters WHERE charId=@character FOR UPDATE", connection, tx))
                {
                    owner.Parameters.AddWithValue("@character", character);
                    if (owner.ExecuteScalar() == null) return Failed();
                }

                var bag = move == null ? null : ReadLockedBagStacks(tx, character);
                var archive = ReadLockedArchive(tx, character);
                if (move != null && bag == null) return Failed();

                CentralStorage.Entry StoredAt(int slot) => archive.TryGetValue(slot, out var row) ? row.Entry : default;
                StackMoves.Stack BagAt(int slot) => bag.TryGetValue(slot, out var row) ? row.Item : default;
                int? StorageFree() => CentralStorage.FirstFreeSlot(archive.Keys);
                int? BagFree() => TradeRules.FreeBagSlots(bag.Keys).Select(slot => (int?)slot).FirstOrDefault();

                CentralStorage.Plan plan;
                uint goid;
                if (move == null)
                {
                    plan = splitAmount.HasValue
                        ? CentralStorage.PlanSplit(source, dest, splitAmount.Value, StoredAt, StorageFree)
                        : CentralStorage.PlanMerge(source, dest, StoredAt);
                    goid = StoredAt(source).Goid;
                }
                else if (move.FromBag)
                {
                    plan = CentralStorage.PlanUpload(move, BagAt, StoredAt, StorageFree, value =>
                        archive.Values.Any(row => row.Value == value));
                    goid = BagAt(source).Goid;
                }
                else
                {
                    plan = CentralStorage.PlanDownload(move, BagAt, StoredAt, Enumerable.Range(1, LastBagSlot), BagFree);
                    goid = StoredAt(source).Goid;
                }
                if (plan.Refused || plan.LearnAbility) return new CentralStorage.Receipt(plan, goid);

                using var bagCommand = new MySqlCommand(null, connection, tx);
                if (move == null)
                {
                    if (!WriteStorageCount(tx, character, source, archive[source].Id, goid, plan.SourceCount) ||
                        !WriteStorageCount(tx, character, plan.StorageSlot,
                            plan.StorageIsNew ? (uint?)null : archive[plan.StorageSlot].Id, goid, plan.StorageCount))
                        return Failed();
                }
                else if (move.FromBag)
                {
                    if (!WriteBagCount(bagCommand, bag[plan.BagSlot].Id, plan.BagCount) ||
                        !WriteStorageCount(tx, character, plan.StorageSlot,
                            plan.StorageIsNew ? (uint?)null : archive[plan.StorageSlot].Id, goid, plan.StorageCount))
                        return Failed();
                }
                else
                {
                    if (plan.BagIsNew && bag.ContainsKey(plan.BagSlot)) return Failed();
                    if (!WriteStorageCount(tx, character, plan.StorageSlot, archive[plan.StorageSlot].Id, goid, plan.StorageCount))
                        return Failed();
                    bool wrote = plan.BagIsNew ? InsertBagStack(bagCommand, character, plan.BagSlot, goid, plan.BagCount, 0) :
                        WriteBagCount(bagCommand, bag[plan.BagSlot].Id, plan.BagCount);
                    if (!wrote) return Failed();
                }

                tx.Commit();
                return new CentralStorage.Receipt(plan, goid);
            }
            catch (DbException error)
            {
                Output.WriteDebugLog($"[CENTRAL STORAGE] Move rolled back char={character} source={source} dest={dest}: {error.Message}");
                return Failed();
            }
        }

        private static Dictionary<int, (uint Id, uint Value, CentralStorage.Entry Entry)> ReadLockedArchive(
            MySqlTransaction tx, uint character)
        {
            var archive = new Dictionary<int, (uint Id, uint Value, CentralStorage.Entry Entry)>();
            using var read = new MySqlCommand(
                "SELECT id,slot,ability_id,level FROM char_abilities WHERE char_id=@character AND slot BETWEEN 0 AND @last " +
                "ORDER BY slot,id FOR UPDATE", tx.Connection, tx);
            read.Parameters.AddWithValue("@character", character);
            read.Parameters.AddWithValue("@last", CentralStorage.Slots - 1);
            using var rows = read.ExecuteReader();
            while (rows.Read())
            {
                int slot = rows.GetInt32(1);
                AbilityAcquisition.TryDescriptor(rows.GetInt64(2), out uint value);
                long level = rows.GetInt64(3);
                uint goid = CodeStorage.GoidOf(value);
                var entry = CodeStorage.IsCode(value) && goid != 0 && level >= 0 && level < CodeStorage.MaxCount
                    ? CentralStorage.Entry.Code(goid, (int)level + 1)
                    : CentralStorage.Entry.Ability;
                // Legacy characters can have repeated known slots. Reserve an ambiguous slot
                // without letting it block an unrelated move or select an arbitrary row.
                if (!archive.TryAdd(slot, (rows.GetUInt32(0), value, entry)))
                    archive[slot] = (0, 0, CentralStorage.Entry.Ability);
            }
            return archive;
        }

        private static bool WriteStorageCount(MySqlTransaction tx, uint character, ushort slot, uint? id, uint goid, int count)
        {
            using var change = new MySqlCommand(null, tx.Connection, tx);
            if (id.HasValue)
            {
                change.CommandText = count == 0 ? "DELETE FROM char_abilities WHERE id=@id" :
                    "UPDATE char_abilities SET level=@level WHERE id=@id";
                change.Parameters.AddWithValue("@id", id.Value);
            }
            else
            {
                change.CommandText = "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded,ability_name) " +
                    "VALUES(@character,@slot,@code,@level,0,'Stored code')";
                change.Parameters.AddWithValue("@character", character);
                change.Parameters.AddWithValue("@slot", slot);
                change.Parameters.AddWithValue("@code", CodeStorage.BaseValue(goid));
            }
            if (count > 0) change.Parameters.AddWithValue("@level", count - 1);
            return change.ExecuteNonQuery() == 1;
        }
    }
}
