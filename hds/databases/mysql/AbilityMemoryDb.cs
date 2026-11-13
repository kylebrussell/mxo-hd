using System;
using System.Collections.Generic;
using MySqlConnector;

namespace hds.databases
{
    public partial class MyWorldDbAccess
    {
        /// <summary>OURS atomic memory batch. No RPCs/callbacks or shared connection helpers inside this transaction.</summary>
        public AbilityMemory.Result CommitAbilityLoadout(uint charId, IReadOnlyList<AbilityProgression.LoadoutEntry> entries,
            bool unload, int characterLevel)
        {
            using var connection=new MySqlConnection(connectionString);
            connection.Open();
            using(var engine=connection.CreateCommand())
            {
                engine.CommandText="SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME IN ('characters','char_abilities') AND ENGINE='InnoDB'";
                if(Convert.ToInt32(engine.ExecuteScalar())!=2) throw new InvalidOperationException("Ability memory admission requires transactional character/archive tables.");
            }
            using var transaction=connection.BeginTransaction();
            using var command=connection.CreateCommand();
            command.Transaction=transaction;
            command.Parameters.AddWithValue("@char",charId);
            command.CommandText="SELECT exp FROM characters WHERE charId=@char FOR UPDATE";
            object experience=command.ExecuteScalar();
            if(experience==null) throw new InvalidOperationException("Character disappeared during ability admission.");
            command.CommandText="SELECT id,slot,ability_id,level,is_loaded FROM char_abilities WHERE char_id=@char AND slot>=0 AND slot<520 ORDER BY id FOR UPDATE";
            var rows=new List<AbilityMemory.Row>();
            using(var reader=command.ExecuteReader()) while(reader.Read())
                rows.Add(new AbilityMemory.Row(reader.GetUInt32(0),reader.GetUInt16(1),unchecked((uint)reader.GetInt64(2)),unchecked((ushort)reader.GetUInt32(3)),reader.GetBoolean(4)));
            var result=AbilityMemory.Plan(rows,entries,unload,Convert.ToInt64(experience),characterLevel);
            if(result.Refusal!=null) return result; // disposal rolls back, nothing was changed
            command.Parameters.AddWithValue("@loaded",!unload);
            command.Parameters.AddWithValue("@id",0u);
            command.CommandText="UPDATE char_abilities SET is_loaded=@loaded WHERE id=@id AND char_id=@char";
            foreach(var row in result.Accepted)
            {
                command.Parameters["@id"].Value=row.Key;
                command.ExecuteNonQuery();
            }
            transaction.Commit();
            return result;
        }
    }
}
