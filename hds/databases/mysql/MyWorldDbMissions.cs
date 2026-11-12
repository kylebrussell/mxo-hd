using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using MySqlConnector;

namespace hds.databases
{
    public partial class MyWorldDbAccess
    {
        private readonly object missionMigrationGate=new object();
        private bool missionsTransactional;
        public void EnsureMissionPersistence()
        {
            lock(missionMigrationGate)
            {
                if(missionsTransactional)return;
                using var connection=new MySqlConnection(connectionString);connection.Open();
                using var c=connection.CreateCommand();
                c.CommandText="CREATE TABLE IF NOT EXISTS char_missions(charId INT UNSIGNED NOT NULL,missionId INT NOT NULL,completions INT NOT NULL DEFAULT 1,PRIMARY KEY(charId,missionId)) ENGINE=InnoDB";c.ExecuteNonQuery();
                c.CommandText="CREATE TABLE IF NOT EXISTS char_active_missions(charId INT UNSIGNED NOT NULL PRIMARY KEY,payload LONGTEXT NULL,receipt LONGTEXT NULL) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4";c.ExecuteNonQuery();
                c.CommandText="SELECT @@SESSION.sql_mode";string mode=Convert.ToString(c.ExecuteScalar());
                c.Parameters.AddWithValue("@mode",string.Join(",",mode.Split(',').Where(m=>m!="NO_ZERO_DATE"&&m!="NO_ZERO_IN_DATE")));
                c.CommandText="SET SESSION sql_mode=@mode";c.ExecuteNonQuery();
                try
                {
                    foreach(string table in new[]{"characters","char_missions"})
                    {
                        c.CommandText="SELECT ENGINE FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='"+table+"'";
                        if(!string.Equals(Convert.ToString(c.ExecuteScalar()),"InnoDB",StringComparison.OrdinalIgnoreCase))
                        { c.CommandText="ALTER TABLE "+table+" ENGINE=InnoDB";c.ExecuteNonQuery(); }
                    }
                }
                finally {c.Parameters["@mode"].Value=mode;c.CommandText="SET SESSION sql_mode=@mode";c.ExecuteNonQuery();}
                missionsTransactional=true;
            }
        }
        private void EnsureActiveMissions()=>EnsureMissionPersistence();
        public string LoadActiveMission(uint character)
        {
            EnsureActiveMissions();
            object value=ScalarQuery("SELECT payload FROM char_active_missions WHERE charId=@char",P("@char",character));
            return value==null||value==DBNull.Value?null:Convert.ToString(value);
        }
        // Snapshot and its exact owned inventory delta commit together. A stale session cannot replace a newer checkpoint.
        public void CommitActiveMission(uint character, string expected, string next, ushort fromSlot = 0, ushort toSlot = 0)
        {
            EnsureActiveMissions(); EnsureTransactionalInventory(); OpenConnection();
            try
            {
                using var tx=conn.BeginTransaction();using var c=conn.CreateCommand();c.Transaction=tx;
                void Param(string key,object value){var p=c.CreateParameter();p.ParameterName=key;p.Value=value??DBNull.Value;c.Parameters.Add(p);}
                Param("@char",character);Param("@next",next);
                c.CommandText="INSERT IGNORE INTO char_active_missions(charId,payload) VALUES(@char,NULL)";c.ExecuteNonQuery();
                c.CommandText="SELECT payload FROM char_active_missions WHERE charId=@char FOR UPDATE";
                object raw=c.ExecuteScalar();string current=raw==null||raw==DBNull.Value?null:Convert.ToString(raw);
                if(current!=expected)throw new InvalidOperationException("Mission checkpoint changed in another session");
                var before=current==null?new Dictionary<uint,ushort>():JsonSerializer.Deserialize<MissionProgress.SavedState>(current,MissionProgress.SaveJson).Items;
                var after=next==null?new Dictionary<uint,ushort>():JsonSerializer.Deserialize<MissionProgress.SavedState>(next,MissionProgress.SaveJson).Items;
                Param("@goid",0u);Param("@slot",0);
                if(fromSlot!=0)
                {
                    var owned=before.FirstOrDefault(p=>p.Value==fromSlot);
                    if(owned.Key==0||!after.TryGetValue(owned.Key,out ushort moved)||moved!=toSlot||toSlot<1||toSlot>LastBagSlot)
                        throw new InvalidOperationException("Invalid owned mission item move");
                    ((System.Data.IDataParameter)c.Parameters["@slot"]).Value=toSlot;
                    c.CommandText="SELECT COUNT(*) FROM inventory WHERE charId=@char AND slot=@slot FOR UPDATE";
                    if(Convert.ToInt32(c.ExecuteScalar())!=0)throw new InvalidOperationException("Mission destination occupied");
                    Param("@from",fromSlot);
                    ((System.Data.IDataParameter)c.Parameters["@goid"]).Value=owned.Key;
                    c.CommandText="UPDATE inventory SET slot=@slot WHERE charId=@char AND slot=@from AND goid=@goid";
                    if(c.ExecuteNonQuery()!=1)throw new InvalidOperationException("Owned mission item missing");
                }

                foreach(var item in before)
                {
                    if(after.TryGetValue(item.Key,out ushort slot)&&slot==item.Value)continue;
                    ((System.Data.IDataParameter)c.Parameters["@goid"]).Value=item.Key;((System.Data.IDataParameter)c.Parameters["@slot"]).Value=item.Value;
                    c.CommandText="DELETE FROM inventory WHERE charId=@char AND slot=@slot AND goid=@goid";c.ExecuteNonQuery();
                }
                foreach(var item in after)
                {
                    if(item.Value<1||item.Value>LastBagSlot)throw new InvalidOperationException("Invalid mission item slot");
                    ((System.Data.IDataParameter)c.Parameters["@goid"]).Value=item.Key;((System.Data.IDataParameter)c.Parameters["@slot"]).Value=item.Value;
                    c.CommandText="SELECT goid FROM inventory WHERE charId=@char AND slot=@slot FOR UPDATE";
                    object existing=c.ExecuteScalar();
                    if(existing!=null&&existing!=DBNull.Value)
                    {if(Convert.ToUInt32(existing)!=item.Key)throw new InvalidOperationException("Mission item slot occupied");continue;}
                    // An already-owned missing item was deliberately removed elsewhere; never re-grant it during checkpointing.
                    if(before.TryGetValue(item.Key,out ushort old)&&old==item.Value)continue;
                    c.CommandText="INSERT INTO inventory(charId,slot,goid,created) VALUES(@char,@slot,@goid,NOW())";c.ExecuteNonQuery();
                }
                c.CommandText="UPDATE char_active_missions SET payload=@next WHERE charId=@char";
                c.ExecuteNonQuery();tx.Commit();
            }
            finally{CloseConnection();}
        }
        public MissionCompletion CompleteActiveMission(uint character,string expected,uint experience,uint info)
        {
            EnsureMissionPersistence();EnsureTransactionalInventory();
            using var connection=new MySqlConnection(connectionString);connection.Open();
            using var transaction=connection.BeginTransaction();using var c=connection.CreateCommand();c.Transaction=transaction;
            c.Parameters.AddWithValue("@char",character);
            c.CommandText="SELECT payload,receipt FROM char_active_missions WHERE charId=@char FOR UPDATE";
            string current=null,receipt=null;
            using(var r=c.ExecuteReader()){if(r.Read()){current=r.IsDBNull(0)?null:r.GetString(0);receipt=r.IsDBNull(1)?null:r.GetString(1);}}
            if(current!=expected)
            {
                if(current==null&&expected!=null&&receipt==expected)return new MissionCompletion {AlreadyCommitted=true};
                throw new InvalidOperationException("Mission completion checkpoint changed");
            }
            if(expected==null)throw new InvalidOperationException("No active mission to complete");
            var state=JsonSerializer.Deserialize<MissionProgress.SavedState>(expected,MissionProgress.SaveJson);
            var mission=DataLoader.getInstance().missions.Find(m=>m.id==state.Mission)??throw new InvalidOperationException("Mission definition missing");
            CollectorExchange exchange=null;
            if(mission.entryItem!=0&&(!state.Flags.TryGetValue("entryItemExchanged",out bool exchanged)||!exchanged))
            {
                exchange=ExchangeCollectorItems(connection,transaction,character,mission.entryItem,1,mission.completionItem);
                if(exchange==null)return null;
            }
            c.Parameters.AddWithValue("@slot",0);c.Parameters.AddWithValue("@goid",0u);
            foreach(var item in state.Items)
            {
                c.Parameters["@slot"].Value=item.Value;c.Parameters["@goid"].Value=item.Key;
                c.CommandText="DELETE FROM inventory WHERE charId=@char AND slot=@slot AND goid=@goid";c.ExecuteNonQuery();
            }
            c.Parameters.AddWithValue("@xp",experience);c.Parameters.AddWithValue("@info",info);c.Parameters.AddWithValue("@mission",mission.id);c.Parameters.AddWithValue("@receipt",expected);
            c.CommandText="UPDATE characters SET exp=exp+@xp,cash=LEAST(cash+@info,4294967295) WHERE charId=@char";
            if(c.ExecuteNonQuery()!=1)throw new InvalidOperationException("Mission owner missing");
            c.CommandText="INSERT INTO char_missions(charId,missionId) VALUES(@char,@mission) ON DUPLICATE KEY UPDATE completions=completions+1";c.ExecuteNonQuery();
            c.CommandText="UPDATE char_active_missions SET payload=NULL,receipt=@receipt WHERE charId=@char";c.ExecuteNonQuery();
            c.CommandText="SELECT exp,cash FROM characters WHERE charId=@char";
            var result=new MissionCompletion {Exchange=exchange};
            using(var r=c.ExecuteReader()){r.Read();result.ExperienceTotal=r.GetInt64(0);result.InfoTotal=r.GetInt64(1);}
            // Persist the level implied by committed XP too: a process exit before feedback must not lose progression.
            c.CommandText="SELECT level FROM characters WHERE charId=@char";int oldLevel=Convert.ToInt32(c.ExecuteScalar());
            int newLevel=PlayerProgression.LevelForExperience(oldLevel,result.ExperienceTotal);
            if(newLevel>oldLevel)
            {
                c.Parameters.AddWithValue("@level",newLevel);
                c.Parameters.AddWithValue("@healthGain",PlayerProgression.MaxHealthAtLevel(newLevel)-PlayerProgression.MaxHealthAtLevel(oldLevel));
                c.Parameters.AddWithValue("@isGain",PlayerProgression.MaxInnerStrengthAtLevel(newLevel)-PlayerProgression.MaxInnerStrengthAtLevel(oldLevel));
                c.CommandText="UPDATE characters SET level=@level,healthC=healthC+@healthGain,healthM=healthM+@healthGain,innerStrC=innerStrC+@isGain,innerStrM=innerStrM+@isGain WHERE charId=@char";
                c.ExecuteNonQuery();
            }
            transaction.Commit();return result;
        }
    }
}
