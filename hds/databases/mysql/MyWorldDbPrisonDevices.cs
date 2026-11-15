using System;
using System.Collections.Generic;
using MySqlConnector;

namespace hds.databases
{
    public partial class MyWorldDbAccess
    {
        public PrisonDevices.Owned ReadPrisonDevice(uint character, ushort slot)
        {
            using var connection=new MySqlConnection(connectionString); connection.Open();
            using var command=connection.CreateCommand();
            command.CommandText="SELECT i.invId,i.goid,i.count,c.level,COALESCE((SELECT MAX(a.level) FROM char_abilities a WHERE a.char_id=c.charId AND a.is_loaded=1 AND (a.ability_id & 0xfc000000)=0x80000000 AND ((a.ability_id & 0x7fffffff)>>10)=2),0) FROM inventory i JOIN characters c ON c.charId=i.charId WHERE i.charId=@char AND i.slot=@slot";
            command.Parameters.AddWithValue("@char",character);command.Parameters.AddWithValue("@slot",slot);
            using var reader=command.ExecuteReader();
            if(!reader.Read()) return null;
            var row=new PrisonDevices.Owned(reader.GetUInt32(0),slot,reader.GetUInt32(1),reader.GetInt32(2),reader.GetInt32(3),reader.GetInt32(4));
            return reader.Read()?null:row; // ambiguous slots never adopt an arbitrary first row
        }

        /// <summary>Narrow exact-device move. All SQL completes before presentation/buff/fence locks.</summary>
        public bool MovePrisonDevice(uint character, PrisonDevices.Owned expected, ushort destination)
        {
            if(expected==null || !PrisonDevices.Device(expected.Goid) || expected.Count<0 || expected.Count>1 ||
                !(expected.Slot==PrisonDevices.Slot && destination>=1 && destination<96 ||
                  destination==PrisonDevices.Slot && expected.Slot>=1 && expected.Slot<96)) return false;
            using var connection=new MySqlConnection(connectionString);connection.Open();
            using var command=connection.CreateCommand();
            // No schema changes: require the transaction contract, as other atomic inventory paths do.
            command.CommandText="SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME IN ('characters','inventory','char_abilities') AND ENGINE='InnoDB'";
            if(Convert.ToInt32(command.ExecuteScalar())!=3) throw new InvalidOperationException("Prison equipment requires transactional inventory/character/ability tables.");
            using var transaction=connection.BeginTransaction();command.Transaction=transaction;
            command.Parameters.AddWithValue("@char",character);command.Parameters.AddWithValue("@source",expected.Slot);
            command.Parameters.AddWithValue("@dest",destination);command.Parameters.AddWithValue("@id",expected.Identity);
            command.Parameters.AddWithValue("@goid",expected.Goid);command.Parameters.AddWithValue("@count",expected.Count);
            command.CommandText="SELECT level FROM characters WHERE charId=@char FOR UPDATE";
            object characterLevel=command.ExecuteScalar();if(characterLevel==null)return false;
            command.CommandText="SELECT invId,slot,goid,count FROM inventory WHERE charId=@char AND slot IN (@source,@dest) FOR UPDATE";
            var rows=new List<(uint Id,ushort Slot,uint Goid,int Count)>();
            using(var reader=command.ExecuteReader())while(reader.Read())rows.Add((reader.GetUInt32(0),reader.GetUInt16(1),reader.GetUInt32(2),reader.GetInt32(3)));
            if(rows.Count!=1 || rows[0].Id!=expected.Identity || rows[0].Slot!=expected.Slot ||
                rows[0].Goid!=expected.Goid || rows[0].Count!=expected.Count)return false;
            if(destination==PrisonDevices.Slot)
            {
                int required=PrisonDevices.RequiredLevel(expected.Goid==42722?(ushort)949:(ushort)957);
                command.CommandText="SELECT level FROM char_abilities WHERE char_id=@char AND is_loaded=1 AND (ability_id & 0xfc000000)=0x80000000 AND ((ability_id & 0x7fffffff)>>10)=2 FOR UPDATE";
                int skill=0;using(var reader=command.ExecuteReader())while(reader.Read())skill=Math.Max(skill,reader.GetInt32(0));
                if(Convert.ToInt32(characterLevel)<required || skill<required)return false;
            }
            command.CommandText="UPDATE inventory SET slot=@dest WHERE invId=@id AND charId=@char AND slot=@source AND goid=@goid AND count=@count";
            if(command.ExecuteNonQuery()!=1)return false;
            transaction.Commit();return true;
        }
    }
}
