using System;
using System.Collections.Generic;
using System.Linq;
using MySqlConnector;
namespace hds.databases
{
    public partial class MyWorldDbAccess
    {
        public AttributeAllocation.Result AllocateAttributes(uint character,IReadOnlyList<CharacterAttributes.Raise> raises)
        {
            AttributeAllocation.Result Refuse(string reason)=>new AttributeAllocation.Result(false,reason);
            if(character==0||raises==null||raises.Count<1||raises.Count>5)return Refuse("That attribute request is unavailable.");
            using var connection=new MySqlConnection(connectionString);connection.Open();
            using(var engine=connection.CreateCommand())
            {
                engine.CommandText="SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME IN ('characters','char_abilities') AND ENGINE='InnoDB'";
                if(Convert.ToInt32(engine.ExecuteScalar())!=2)throw new InvalidOperationException("Attribute allocation requires transactional tables");
            }
            using var transaction=connection.BeginTransaction();using var command=connection.CreateCommand();command.Transaction=transaction;
            command.Parameters.AddWithValue("@character",character);command.CommandText="SELECT exp FROM characters WHERE charId=@character FOR UPDATE";
            object xp=command.ExecuteScalar();if(xp==null||xp==DBNull.Value)return Refuse("Character not found.");
            command.CommandText="SELECT id,slot,ability_id,level FROM char_abilities WHERE char_id=@character AND slot IN (598,599,601,602,604) ORDER BY id FOR UPDATE";
            var rows=new List<AttributeAllocation.Row>();
            using(var reader=command.ExecuteReader())while(reader.Read())rows.Add(new AttributeAllocation.Row(reader.GetUInt32(0),reader.GetInt32(1),reader.GetInt64(2),reader.GetInt64(3)));
            if(!AttributeAllocation.TryPlan(rows,raises,Convert.ToInt64(xp),out var values))return Refuse("That allocation exceeds your available points or attribute cap, or the saved attribute rows are ambiguous.");
            foreach(byte attribute in CharacterAttributes.All)
            {
                int slot=CharacterAttributes.SlotOf(attribute);var owned=rows.Where(r=>r.Slot==slot).ToArray();
                command.Parameters.Clear();command.Parameters.AddWithValue("@character",character);command.Parameters.AddWithValue("@slot",slot);command.Parameters.AddWithValue("@next",values[attribute]);
                if(owned.Length==1)
                {
                    var row=owned[0];if(row.Value==values[attribute])continue;
                    command.Parameters.AddWithValue("@identity",row.Identity);command.Parameters.AddWithValue("@raw",row.Descriptor);command.Parameters.AddWithValue("@old",row.Value);
                    command.CommandText="UPDATE char_abilities SET level=@next WHERE id=@identity AND char_id=@character AND slot=@slot AND ability_id=@raw AND level=@old";
                }
                else
                {
                    command.Parameters.AddWithValue("@raw",CharacterAttributes.RowValueOf(attribute));
                    command.CommandText="INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded,ability_name,added) SELECT @character,@slot,@raw,@next,0,'Attribute',NOW() WHERE NOT EXISTS(SELECT 1 FROM char_abilities WHERE char_id=@character AND slot=@slot)";
                }
                if(command.ExecuteNonQuery()!=1)return Refuse("The saved attribute row changed; nothing was allocated.");
            }
            var result=new AttributeAllocation.Result(true,"",values);transaction.Commit();return result;
        }
    }
}
