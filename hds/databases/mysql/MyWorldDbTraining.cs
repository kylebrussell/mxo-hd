using System;
using System.Collections.Generic;
using System.Linq;
using MySqlConnector;

namespace hds.databases
{
    public partial class MyWorldDbAccess
    {
        public AbilityTraining.Result TrainOwnedAbility(uint character,AbilityProgression.UpgradeRequest request)
        {
            AbilityTraining.Result Refuse(string reason,ushort level=0)=>new AbilityTraining.Result(false,request.Ability,level,0,0,reason);
            if(character==0||request.Levels==0||request.Slot>=CharacterAttributes.FirstSlot||!AbilityTraining.Ordinary(request.Ability))
                return Refuse("That training request is not available.");
            using var connection=new MySqlConnection(connectionString);connection.Open();
            using(var engine=connection.CreateCommand())
            {
                engine.CommandText="SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME IN ('characters','char_abilities') AND ENGINE='InnoDB'";
                if(Convert.ToInt32(engine.ExecuteScalar())!=2)throw new InvalidOperationException("Training requires transactional character and archive tables");
            }
            using var transaction=connection.BeginTransaction();using var command=connection.CreateCommand();command.Transaction=transaction;
            command.Parameters.AddWithValue("@character",character);
            command.CommandText="SELECT cash,exp FROM characters WHERE charId=@character FOR UPDATE";
            long cash,experience;
            using(var reader=command.ExecuteReader())
            {if(!reader.Read())return Refuse("Character not found.");cash=reader.GetInt64(0);experience=reader.GetInt64(1);}
            command.CommandText="SELECT id,slot,ability_id,level,is_loaded FROM char_abilities WHERE char_id=@character ORDER BY id FOR UPDATE";
            var rows=new List<AbilityTraining.ArchiveRow>();
            using(var reader=command.ExecuteReader())while(reader.Read())rows.Add(new AbilityTraining.ArchiveRow(reader.GetUInt32(0),reader.GetInt32(1),reader.GetInt64(2),reader.GetInt64(3),reader.GetBoolean(4)));
            //Refuse corrupt loaded data rather than replacing unrelated live passives with a partial snapshot.
            if(rows.Any(r=>r.Loaded && r.Slot<CharacterAttributes.FirstSlot &&
                (r.Slot<0||r.Descriptor<int.MinValue||r.Descriptor>uint.MaxValue||r.Level<0||r.Level>ushort.MaxValue)))
                return Refuse("A loaded archive row is invalid; nothing was charged.");
            var owned=rows.Where(r=>r.Slot==request.Slot).ToArray();
            if(owned.Length!=1)return Refuse("Training requires one unambiguous owned archive slot.");
            //80b8 carries only ID/new level; native resolves its archive slot by ID, so cross-slot duplicates are ambiguous.
            if(!AbilityTraining.UniqueOwnedAbility(rows,request.Ability))
                return Refuse("Training requires one unambiguous owned ability.");
            var row=owned[0];ushort old=row.Level>=0&&row.Level<=ushort.MaxValue?(ushort)row.Level:(ushort)0;
            if(!AbilityTraining.Admit(request,row,experience,out ushort next,out uint cost))return Refuse("You cannot train that owned ability to this level.",old);
            if(cash<0||cash<cost)return Refuse("You do not have enough information for that upgrade.",old);
            if(!AbilityTraining.TryRemainingInfo(cash,cost,out long remaining))return Refuse("The resulting information balance cannot be presented; nothing was changed.",old);
            command.Parameters.AddWithValue("@identity",row.Identity);command.Parameters.AddWithValue("@slot",row.Slot);
            command.Parameters.AddWithValue("@descriptor",row.Descriptor);command.Parameters.AddWithValue("@old",row.Level);
            command.Parameters.AddWithValue("@next",next);command.Parameters.AddWithValue("@cost",cost);
            command.CommandText="UPDATE char_abilities SET level=@next WHERE id=@identity AND char_id=@character AND slot=@slot AND ability_id=@descriptor AND level=@old";
            if(command.ExecuteNonQuery()!=1)return Refuse("The owned training row changed; nothing was charged.",old);
            command.CommandText="UPDATE characters SET cash=cash-@cost WHERE charId=@character AND cash>=@cost";
            if(command.ExecuteNonQuery()!=1)return Refuse("Training could not be charged; nothing was changed.",old);
            var snapshot=rows.Select(r=>r.Identity==row.Identity?new AbilityTraining.ArchiveRow(r.Identity,r.Slot,r.Descriptor,next,r.Loaded):r).ToArray();
            var result=new AbilityTraining.Result(true,request.Ability,next,cost,remaining,"",AbilityTraining.LoadedSnapshot(snapshot));
            transaction.Commit();return result;
        }
    }
}
