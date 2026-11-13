using System;
using System.Collections.Generic;
using System.Linq;
using MySqlConnector;
namespace hds.databases
{
    public partial class MyWorldDbAccess
    {
        // Own connection, character -> inventory -> archive. No callbacks or publication inside the transaction.
        public AbilityAcquisition.Receipt AcquireAbility(uint character,uint goid,uint price,ushort? bagSlot=null,ushort? archiveSlot=null,ushort[] reserved=null)
        {
            if(!AbilityAcquisition.Genuine(goid))throw new AbilityAcquisition.Refusal("This item is not a recognized ability code.");
            if(!bagSlot.HasValue&&price!=VendorPricing.GetBuyPrice(goid))throw new AbilityAcquisition.Refusal("The ability price changed; reopen its vendor.");
            using var connection=new MySqlConnection(connectionString);connection.Open();
            using(var engine=connection.CreateCommand())
            {
                engine.CommandText="SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME IN ('characters','inventory','char_abilities') AND ENGINE='InnoDB'";
                if(Convert.ToInt32(engine.ExecuteScalar())!=3)throw new AbilityAcquisition.Refusal("Ability acquisition requires transactional character, inventory and archive tables.");
            }
            using var transaction=connection.BeginTransaction();
            using var command=connection.CreateCommand();command.Transaction=transaction;
            command.Parameters.AddWithValue("@char",character);
            command.CommandText="SELECT cash FROM characters WHERE charId=@char FOR UPDATE";
            object wallet=command.ExecuteScalar();if(wallet==null)throw new AbilityAcquisition.Refusal("Character not found.");
            long cash=Convert.ToInt64(wallet);
            command.CommandText="SELECT invId,slot,goid,count FROM inventory WHERE charId=@char ORDER BY invId FOR UPDATE";
            var bag=new List<AbilityAcquisition.BagRow>();
            using(var reader=command.ExecuteReader())while(reader.Read())bag.Add(new AbilityAcquisition.BagRow(Convert.ToUInt32(reader[0]),Convert.ToInt32(reader[1]),unchecked((uint)Convert.ToInt64(reader[2])),Convert.ToInt32(reader[3])));
            command.Parameters.AddWithValue("@goid",goid);
            if(!bagSlot.HasValue)
            {
                ushort slot=AbilityAcquisition.FreeBag(bag);uint next=AbilityAcquisition.BuyCash(cash,price);
                command.Parameters.AddWithValue("@slot",slot);command.Parameters.AddWithValue("@cash",next);
                command.CommandText="INSERT INTO inventory(charId,goid,slot,count,created) VALUES(@char,@goid,@slot,1,NOW())";command.ExecuteNonQuery();
                command.CommandText="UPDATE characters SET cash=@cash WHERE charId=@char";if(command.ExecuteNonQuery()!=1)throw new InvalidOperationException("Wallet changed during ability purchase.");
                transaction.Commit();return new AbilityAcquisition.Receipt(slot,0,goid,cash,next,price);
            }
            var source=bag.Where(r=>r.Slot==bagSlot.Value).ToArray();
            if(bagSlot<1||bagSlot>96||source.Length!=1||source[0].Goid!=goid||source[0].Count!=1)
                throw new AbilityAcquisition.Refusal("That ability code changed or its bag slot is ambiguous. Nothing was uploaded.");
            command.CommandText="SELECT id,slot,ability_id FROM char_abilities WHERE char_id=@char ORDER BY id FOR UPDATE";
            var archive=new List<AbilityAcquisition.ArchiveRow>();
            using(var reader=command.ExecuteReader())while(reader.Read())archive.Add(new AbilityAcquisition.ArchiveRow(Convert.ToUInt32(reader[0]),Convert.ToInt32(reader[1]),unchecked((uint)Convert.ToInt64(reader[2]))));
            ushort destination=AbilityAcquisition.LearnSlot(archive,goid,archiveSlot,reserved);
            command.Parameters.AddWithValue("@id",source[0].Id);command.Parameters.AddWithValue("@slot",source[0].Slot);
            command.CommandText="DELETE FROM inventory WHERE invId=@id AND charId=@char AND slot=@slot AND goid=@goid AND count=1";
            if(command.ExecuteNonQuery()!=1)throw new InvalidOperationException("Ability code changed during upload.");
            command.Parameters.AddWithValue("@archive",destination);command.Parameters.AddWithValue("@base",unchecked((int)(0x80000000u|((uint)AbilityAcquisition.Identity(goid)<<10))));
            command.CommandText="INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded,ability_name,added) VALUES(@char,@archive,@base,1,0,'Acquired ability',NOW())";command.ExecuteNonQuery();
            transaction.Commit();return new AbilityAcquisition.Receipt(bagSlot.Value,destination,goid,cash,0,0);
        }
    }
}
