using System;
using System.Linq;
using MySqlConnector;
namespace hds.databases
{
    public partial class MyMarginDBAccess
    {
        private readonly object creationStorageGate=new object();
        private bool creationStorageReady;
        // Idempotent startup storage initialization. ONLY the newly participating RSI table is converted.
        public void EnsureCharacterCreationStorage()
        {
            lock(creationStorageGate)
            {
                if(creationStorageReady)return;
                using var connection=new MySqlConnection(connectionString);connection.Open();using var c=connection.CreateCommand();
                c.CommandText="SELECT @@SESSION.sql_mode";string mode=Convert.ToString(c.ExecuteScalar());
                c.Parameters.AddWithValue("@mode",string.Join(",",mode.Split(',').Where(m=>m!="NO_ZERO_DATE"&&m!="NO_ZERO_IN_DATE")));
                c.CommandText="SET SESSION sql_mode=@mode";c.ExecuteNonQuery();
                try
                {
                    c.CommandText="SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='characters' AND COLUMN_NAME='trait_id'";
                    if(Convert.ToInt32(c.ExecuteScalar())==0){c.CommandText="ALTER TABLE characters ADD COLUMN trait_id INT UNSIGNED NULL DEFAULT NULL";c.ExecuteNonQuery();}
                    c.CommandText="SELECT ENGINE FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='rsivalues'";
                    if(!string.Equals(Convert.ToString(c.ExecuteScalar()),"InnoDB",StringComparison.OrdinalIgnoreCase))
                    {c.CommandText="ALTER TABLE rsivalues ENGINE=InnoDB";c.ExecuteNonQuery();}
                    RequireCreationTables(c);creationStorageReady=true;
                }
                finally{c.Parameters["@mode"].Value=mode;c.CommandText="SET SESSION sql_mode=@mode";c.ExecuteNonQuery();}
            }
        }
        private static void RequireCreationTables(MySqlCommand c)
        {
            c.CommandText="SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME IN ('characters','rsivalues','char_abilities','inventory') AND ENGINE='InnoDB'";
            if(Convert.ToInt32(c.ExecuteScalar())!=4)throw new InvalidOperationException("Character creation requires four transactional tables");
            c.CommandText="SELECT DATA_TYPE,COLUMN_TYPE,IS_NULLABLE FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='characters' AND COLUMN_NAME='trait_id'";
            using var r=c.ExecuteReader();
            if(!r.Read()||r.GetString(0)!="int"||!r.GetString(1).Contains("unsigned",StringComparison.OrdinalIgnoreCase)||r.GetString(2)!="YES")throw new InvalidOperationException("Character trait storage is incompatible");
        }
        private uint ReserveCharacter(string handle,uint account,uint world)
        {
            if(account==0||world!=1||string.IsNullOrEmpty(handle)||handle.Length>31||handle.IndexOf('\0')>=0)return 0;
            using var connection=new MySqlConnection(connectionString);connection.Open();using var c=connection.CreateCommand();RequireCreationTables(c);
            using var tx=connection.BeginTransaction();c.Transaction=tx;
            c.Parameters.AddWithValue("@account",account);c.Parameters.AddWithValue("@handle",handle);c.Parameters.AddWithValue("@world",world);
            // Verify the authenticated account; the existing 0a reservation persists if 0c is abandoned.
            c.CommandText="SELECT userId FROM users WHERE userId=@account FOR UPDATE";if(c.ExecuteScalar()==null)return 0;
            c.CommandText="SELECT charId FROM characters WHERE handle=@handle AND is_deleted=0 FOR UPDATE";if(c.ExecuteScalar()!=null)return 0;
            c.CommandText="INSERT INTO characters(handle,userId,worldId,created) VALUES(@handle,@account,@world,NOW())";if(c.ExecuteNonQuery()!=1)return 0;
            uint character=checked((uint)c.LastInsertedId);c.Parameters.AddWithValue("@character",character);
            c.CommandText="INSERT INTO rsivalues(charid) VALUES(@character)";if(c.ExecuteNonQuery()!=1)throw new InvalidOperationException("RSI reservation shell was not created");
            tx.Commit();return character;
        }
        // Existing starting grants retained literally; no learning/loadout or acquisition policy is added here.
        internal static readonly (int Raw,int Level,int Loaded)[] StartingGrants={
            (-2147481600,1,1),(-2147367936,1,1),(-2147294208,0,1),(-2147281920,0,0),(-2147280896,0,0),(-2147437568,1,1),
            (-2147425280,0,0),(-2147404800,0,0),(-2147445760,0,0),(-2146493440,0,0),(-2146453504,1,0),(-2147472384,20,1),(-2147295232,20,1),(-2147295232,20,1)};
        public bool CompleteCharacterCreation(uint character,uint account,CharacterCreation.InitialState state)
        {
            if(character==0||account==0||state==null||!CharacterPersonality.Valid(state.Request.Trait))return false;
            using var connection=new MySqlConnection(connectionString);connection.Open();using var c=connection.CreateCommand();RequireCreationTables(c);
            using var tx=connection.BeginTransaction();c.Transaction=tx;
            c.Parameters.AddWithValue("@character",character);c.Parameters.AddWithValue("@account",account);
            c.CommandText="SELECT trait_id FROM characters WHERE charId=@character AND userId=@account AND is_deleted=0 FOR UPDATE";
            object assigned=c.ExecuteScalar();if(assigned==null||assigned!=DBNull.Value)return false; // Replay/foreign/current-reservation mismatch.
            c.CommandText="SELECT charid FROM rsivalues WHERE charid=@character FOR UPDATE";if(c.ExecuteScalar()==null)return false;
            c.CommandText="SELECT id FROM char_abilities WHERE char_id=@character FOR UPDATE";using(var rows=c.ExecuteReader())if(rows.Read())return false;
            c.CommandText="SELECT invId FROM inventory WHERE charId=@character FOR UPDATE";using(var rows=c.ExecuteReader())if(rows.Read())return false;
            c.Parameters.AddWithValue("@trait",state.Request.Trait);c.Parameters.AddWithValue("@first",state.Request.FirstName);c.Parameters.AddWithValue("@last",state.Request.LastName);c.Parameters.AddWithValue("@background",state.Request.Background);
            c.Parameters.AddWithValue("@health",PlayerProgression.MaxHealthAtLevel(1));c.Parameters.AddWithValue("@strength",PlayerProgression.MaxInnerStrengthAtLevel(1));
            c.CommandText="UPDATE characters SET trait_id=@trait,firstName=@first,lastName=@last,background=@background,healthC=@health,healthM=@health,innerStrC=@strength,innerStrM=@strength WHERE charId=@character AND userId=@account AND trait_id IS NULL";
            if(c.ExecuteNonQuery()!=1)return false;
            // Keys come exclusively from the validated resolver, never client-provided SQL identifiers.
            foreach(var field in state.Rsi){c.Parameters.AddWithValue("@rsi_"+field.Key,field.Value);}
            c.CommandText="UPDATE rsivalues SET "+string.Join(",",state.Rsi.Keys.Select(k=>k+"=@rsi_"+k))+" WHERE charid=@character";c.ExecuteNonQuery();
            foreach(var entry in state.Clothing)
            {
                c.Parameters.AddWithValue("@slot",entry.Key);c.Parameters.AddWithValue("@goid",entry.Value);
                c.CommandText="INSERT INTO inventory(charId,slot,goid,count,purity) VALUES(@character,@slot,@goid,1,0)";c.ExecuteNonQuery();c.Parameters.RemoveAt("@slot");c.Parameters.RemoveAt("@goid");
            }
            for(int i=0;i<StartingGrants.Length;i++)
            {
                var g=StartingGrants[i];c.Parameters.AddWithValue("@slot",i);c.Parameters.AddWithValue("@raw",g.Raw);c.Parameters.AddWithValue("@level",g.Level);c.Parameters.AddWithValue("@loaded",g.Loaded);
                c.CommandText="INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded,added) VALUES(@character,@slot,@raw,@level,@loaded,NOW())";c.ExecuteNonQuery();
                foreach(string p in new[]{"@slot","@raw","@level","@loaded"})c.Parameters.RemoveAt(p);
            }
            foreach(byte attribute in CharacterAttributes.All)
            {
                c.Parameters.AddWithValue("@slot",CharacterAttributes.SlotOf(attribute));c.Parameters.AddWithValue("@raw",CharacterAttributes.RowValueOf(attribute));c.Parameters.AddWithValue("@level",CharacterAttributes.BaseValue(attribute,state.Request.Trait));
                c.CommandText="INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded,added) VALUES(@character,@slot,@raw,@level,0,NOW())";c.ExecuteNonQuery();
                foreach(string p in new[]{"@slot","@raw","@level"})c.Parameters.RemoveAt(p);
            }
            tx.Commit();return true;
        }
    }
}
