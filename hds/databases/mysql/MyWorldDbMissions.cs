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
        public MissionAccountUpdate CommitActiveMission(uint character, string expected, string next, ushort fromSlot = 0, ushort toSlot = 0)
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
                if(current!=expected)throw new MissionCheckpointException("Mission checkpoint changed in another session");
                var previous=current==null?null:JsonSerializer.Deserialize<MissionProgress.SavedState>(current,MissionProgress.SaveJson);
                var updated=next==null?null:JsonSerializer.Deserialize<MissionProgress.SavedState>(next,MissionProgress.SaveJson);
                var before=previous?.Items??new Dictionary<uint,ushort>();
                var after=updated?.Items??new Dictionary<uint,ushort>();
                Param("@goid",0u);Param("@slot",0);
                if(fromSlot!=0)
                {
                    var owned=before.FirstOrDefault(p=>p.Value==fromSlot);
                    if(owned.Key==0||!after.TryGetValue(owned.Key,out ushort moved)||moved!=toSlot||toSlot<1||toSlot>LastBagSlot)
                        throw new MissionCheckpointException("Invalid owned mission item move");
                    ((System.Data.IDataParameter)c.Parameters["@slot"]).Value=toSlot;
                    c.CommandText="SELECT COUNT(*) FROM inventory WHERE charId=@char AND slot=@slot FOR UPDATE";
                    if(Convert.ToInt32(c.ExecuteScalar())!=0)throw new MissionCheckpointException("Mission destination occupied");
                    Param("@from",fromSlot);
                    ((System.Data.IDataParameter)c.Parameters["@goid"]).Value=owned.Key;
                    c.CommandText="UPDATE inventory SET slot=@slot WHERE charId=@char AND slot=@from AND goid=@goid";
                    if(c.ExecuteNonQuery()!=1)throw new MissionCheckpointException("Owned mission item missing");
                }

                foreach(var item in before)
                {
                    if(after.TryGetValue(item.Key,out ushort slot)&&slot==item.Value)continue;
                    ((System.Data.IDataParameter)c.Parameters["@goid"]).Value=item.Key;((System.Data.IDataParameter)c.Parameters["@slot"]).Value=item.Value;
                    c.CommandText="DELETE FROM inventory WHERE charId=@char AND slot=@slot AND goid=@goid";c.ExecuteNonQuery();
                }
                foreach(var item in after)
                {
                    if(item.Value<1||item.Value>LastBagSlot)throw new MissionCheckpointException("Invalid mission item slot");
                    ((System.Data.IDataParameter)c.Parameters["@goid"]).Value=item.Key;((System.Data.IDataParameter)c.Parameters["@slot"]).Value=item.Value;
                    c.CommandText="SELECT goid FROM inventory WHERE charId=@char AND slot=@slot FOR UPDATE";
                    object existing=c.ExecuteScalar();
                    if(existing!=null&&existing!=DBNull.Value)
                    {
                        bool owned=before.TryGetValue(item.Key,out ushort previousSlot)&&previousSlot==item.Value;
                        bool ownedMove=fromSlot!=0&&previousSlot==fromSlot&&item.Value==toSlot;
                        if(Convert.ToUInt32(existing)!=item.Key||(!owned&&!ownedMove))throw new MissionCheckpointException("Mission item slot occupied");
                        continue;
                    }
                    // An already-owned missing item was deliberately removed elsewhere; never re-grant it during checkpointing.
                    if(before.TryGetValue(item.Key,out ushort old)&&old==item.Value)continue;
                    c.CommandText="INSERT INTO inventory(charId,slot,goid,created) VALUES(@char,@slot,@goid,NOW())";c.ExecuteNonQuery();
                }
                long xp=0,info=0;
                foreach(var actor in updated?.Mobs??new List<MissionProgress.SavedMob>())
                {
                    var old=previous?.Mobs.FirstOrDefault(m=>m.Identity==actor.Identity);
                    xp+=Math.Max(0,(long)actor.RewardExperience-(old?.RewardExperience??0));
                    if(actor.Loot!=null&&old?.Loot!=null)info+=Math.Max(0,(long)old.Loot.Info-actor.Loot.Info);
                }
                MissionAccountUpdate account=null;
                if(xp>0||info>0)
                {
                    c.CommandText="SELECT exp,cash FROM characters WHERE charId=@char FOR UPDATE";
                    long oldXp,oldInfo;using(var r=c.ExecuteReader()){if(!r.Read())throw new MissionCheckpointException("Mission owner missing");oldXp=Convert.ToInt64(r[0]);oldInfo=Convert.ToInt64(r[1]);}
                    Param("@xp",xp);Param("@info",info);
                    c.CommandText="UPDATE characters SET exp=LEAST(CAST(exp AS SIGNED)+@xp,2147483647),cash=LEAST(CAST(cash AS SIGNED)+@info,2147483647) WHERE charId=@char";c.ExecuteNonQuery();
                    account=new MissionAccountUpdate();
                    c.CommandText="SELECT exp,cash FROM characters WHERE charId=@char";
                    using(var r=c.ExecuteReader()){r.Read();account.ExperienceTotal=Convert.ToInt64(r[0]);account.InfoTotal=Convert.ToInt64(r[1]);}
                    account.ExperienceCredited=(uint)Math.Max(0,account.ExperienceTotal-oldXp);account.InfoCredited=(uint)Math.Max(0,account.InfoTotal-oldInfo);
                    PersistMissionLevel(c,account.ExperienceTotal);
                }
                c.CommandText="UPDATE char_active_missions SET payload=@next WHERE charId=@char";
                c.ExecuteNonQuery();tx.Commit();return account;
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
                throw new MissionCheckpointException("Mission completion checkpoint changed");
            }
            if(expected==null)throw new MissionCheckpointException("No active mission to complete");
            var state=JsonSerializer.Deserialize<MissionProgress.SavedState>(expected,MissionProgress.SaveJson);
            var mission=MissionDefinitions.Find(state.Mission)??throw new MissionCheckpointException("Mission definition missing");
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
            c.CommandText="SELECT exp,cash FROM characters WHERE charId=@char FOR UPDATE";
            long oldExperience,oldInfo;
            using(var r=c.ExecuteReader()){if(!r.Read())throw new MissionCheckpointException("Mission owner missing");oldExperience=r.GetInt64(0);oldInfo=r.GetInt64(1);}
            c.CommandText="UPDATE characters SET exp=LEAST(CAST(exp AS SIGNED)+@xp,2147483647),cash=LEAST(CAST(cash AS SIGNED)+@info,2147483647) WHERE charId=@char";
            if(c.ExecuteNonQuery()!=1)throw new MissionCheckpointException("Mission owner missing");
            c.CommandText="INSERT INTO char_missions(charId,missionId) VALUES(@char,@mission) ON DUPLICATE KEY UPDATE completions=completions+1";c.ExecuteNonQuery();
            c.CommandText="UPDATE char_active_missions SET payload=NULL,receipt=@receipt WHERE charId=@char";c.ExecuteNonQuery();
            c.CommandText="SELECT exp,cash FROM characters WHERE charId=@char";
            var result=new MissionCompletion {Exchange=exchange};
            using(var r=c.ExecuteReader()){r.Read();result.ExperienceTotal=r.GetInt64(0);result.InfoTotal=r.GetInt64(1);}
            result.ExperienceCredited=(uint)Math.Max(0,result.ExperienceTotal-oldExperience);result.InfoCredited=(uint)Math.Max(0,result.InfoTotal-oldInfo);
            PersistMissionLevel(c,result.ExperienceTotal);
            transaction.Commit();return result;
        }
        private static void PersistMissionLevel(System.Data.IDbCommand c,long total)
        {
            c.CommandText="SELECT level FROM characters WHERE charId=@char";int oldLevel=Convert.ToInt32(c.ExecuteScalar());
            int level=PlayerProgression.LevelForExperience(oldLevel,total);
            if(level<=oldLevel)return;
            void Param(string name,object value){var p=c.CreateParameter();p.ParameterName=name;p.Value=value;c.Parameters.Add(p);}
            Param("@level",level);Param("@healthGain",PlayerProgression.MaxHealthAtLevel(level)-PlayerProgression.MaxHealthAtLevel(oldLevel));
            Param("@isGain",PlayerProgression.MaxInnerStrengthAtLevel(level)-PlayerProgression.MaxInnerStrengthAtLevel(oldLevel));
            c.CommandText="UPDATE characters SET level=@level,healthC=healthC+@healthGain,healthM=healthM+@healthGain,innerStrC=innerStrC+@isGain,innerStrM=innerStrM+@isGain WHERE charId=@char";c.ExecuteNonQuery();
        }
    }
}
