using System;
using System.Collections;
using System.Data;

using hds.databases.interfaces;
using hds.shared;
using System.Collections.Generic;
using hds.world.Structures;
using MySqlConnector;

namespace hds.databases{
	
    public partial class MyWorldDbAccess: IWorldDBHandler{

		// One per thread, as Store.currentClient is. These used to be one connection, command and reader shared by every
		// player and timer thread: under load a query failed with "Connection must be Open" (and the player stuck at
		// login, found by tools/HeadlessClient), or a reader could be replaced mid-read by another player's query.
		[ThreadStatic] private static IDbConnection threadConnection;
		[ThreadStatic] private static IDbCommand threadCommand;
		[ThreadStatic] private static IDataReader threadReader;
		private readonly string connectionString;

		private IDbConnection conn => threadConnection ??= new MySqlConnection(connectionString);
		private IDbCommand queryExecuter { get => threadCommand; set => threadCommand = value; }
		private IDataReader dr { get => threadReader; set => threadReader = value; }

		public MyWorldDbAccess(){
            var config = Store.config;
			/* Params: Host, port, database, user, password */
            connectionString = ("Server=" + config.dbParams.Host + ";" +
                "Port=" + config.dbParams.Port + ";" +
                "Database=" + config.dbParams.DatabaseName + ";" +
                "User ID=" + config.dbParams.Username + ";" +
                "Password=" + config.dbParams.Password + ";" +
                "Pooling=true;");
		}

		public void OpenConnection()
		{
			if (conn.State == ConnectionState.Closed)
			{
				conn.Open();
			}
		}

		public void CloseConnection()
		{
			if (conn.State != ConnectionState.Closed)
			{
				conn.Close();
			}   
		}
		
		public void ExecuteNonResultQuery(string query)
		{
			OpenConnection();
			string updateQuery = query;
			queryExecuter = conn.CreateCommand();
			queryExecuter.CommandText = updateQuery;
			queryExecuter.ExecuteNonQuery();
			CloseConnection();
		}
		
		
		public UInt32 GetUserIdForCharId(byte[] charIdHex){
			OpenConnection();	 
			UInt32 charId = NumericalUtils.ByteArrayToUint32(charIdHex,1);
			Output.OptWriteLine("[WORLD] Checking from DB:"+charId);
			string sqlQuery="Select userid from characters where charid ='"+charId+"'";
			queryExecuter= conn.CreateCommand();
			queryExecuter.CommandText = sqlQuery;					
			dr= queryExecuter.ExecuteReader();
			
			UInt32 userId = new UInt32();
			
			while(dr.Read()){
				userId = (UInt32)Convert.ToDecimal(dr.GetValue(0));	
			}
			
			dr.Close();
			CloseConnection();
			return userId;
			
		}

		public void AddHandleToFriendList(string handleToAdd, UInt32 charId)
		{
			UInt32 friendId = GetCharIdByHandle(handleToAdd);
			ExecuteNonResultQuery("INSERT INTO buddylist SET charId='" + charId +
			                      "', friendId='" + friendId + "', is_ignored=0 ");
			
		}

		public void RemoveHandleFromFriendList(string handleToRemove, UInt32 charId)
		{
			UInt32 friendId = GetCharIdByHandle(handleToRemove);
			ExecuteNonResultQuery(
				"DELETE FROM buddylist WHERE charId='" + charId + "' AND friendId='" + friendId + "' ");
			
		}

		public ArrayList FetchPlayersWhoAddedMeToBuddylist(UInt32 charId)
		{
			OpenConnection();
			ArrayList friends = new ArrayList();
			string query = "SELECT C.handle, C.charId, C.is_online, B.friendId FROM buddylist B LEFT JOIN characters C ON B.charId=C.charId WHERE B.friendId = '" + charId + "' ";
			queryExecuter = conn.CreateCommand();
			queryExecuter.CommandText = query;
			dr = queryExecuter.ExecuteReader();

			while (dr.Read())
			{
				Hashtable data = new Hashtable();
				data.Add("handle", dr.GetString(0));
				data.Add("online", dr.GetInt16(1));
				friends.Add(data);
			}

			dr.Close();
			CloseConnection();
			return friends;
		}
		
        public ArrayList FetchFriendList(UInt32 charId)
        {
	        OpenConnection();
            ArrayList friends = new ArrayList();
            string query = "SELECT C.handle, C.is_online, B.friendId FROM buddylist B LEFT JOIN characters C ON B.friendId=C.charId WHERE B.charId = '" + charId + "' ";
            queryExecuter = conn.CreateCommand();
            queryExecuter.CommandText = query;
            dr = queryExecuter.ExecuteReader();

            while (dr.Read())
            {
                Hashtable data = new Hashtable();
                data.Add("handle", dr.GetString(0));
                data.Add("online", dr.GetInt16(1));
                friends.Add(data);
            }

            dr.Close();
	        CloseConnection();
	        return friends;
            
        }


        public Faction FetchFaction(uint factionId)
        {
	        Faction factionData = new Faction();
	        OpenConnection();
	        string query = "SELECT f.id, f.name, f.master_player_handle, f.money FROM factions f LEFT JOIN characters c ON f.id=c.factionId WHERE f.id= '" + factionId + "' LIMIT 1";
	        queryExecuter = conn.CreateCommand();
	        queryExecuter.CommandText = query;
	        dr = queryExecuter.ExecuteReader();

	        while (dr.Read())
	        {
		        factionData.factionId = (UInt32)dr.GetInt32(0);
		        factionData.name = dr.GetString(1);
		        factionData.masterPlayerHandle = dr.GetString(2);
		        factionData.money = (UInt32) dr.GetInt32(3);
	        }

	        dr.Close();
	        CloseConnection();
	        
	        factionData.crews = GetCrewsForFaction(factionData.factionId);
	        return factionData;
        }

        public bool IsFactionnameAvailable(string factionname)
        {
	        // Parameterized: faction names may contain quotes (retail had "'Dark Mavericks'").
	        object found = ScalarQuery("SELECT id FROM factions WHERE name = @name AND deleted_at IS NULL LIMIT 1",
		        P("@name", factionname.Trim()));
	        return found == null || found == DBNull.Value;
        }

        public bool IsHandleCaptainOfACrew(string handle)
        {
	        bool isCaptainOfCrew = false;
	        OpenConnection();
	        string query = "SELECT id FROM crews WHERE master_player_handle = '" + handle.Trim() + "' LIMIT 1";
	        queryExecuter = conn.CreateCommand();
	        queryExecuter.CommandText = query;
	        dr = queryExecuter.ExecuteReader();

	        while (dr.Read())
	        {
		        // if we have just one entry - we have found a faction
		        isCaptainOfCrew = true;
	        }
	        CloseConnection();
	        return isCaptainOfCrew;
        }

        public UInt32 createFaction(string factionname, Crew masterCrew, Crew secondCrew)
        {
	        ExecuteNonResultQuery("INSERT INTO factions SET name='" + factionname.Trim() + "', master_player_handle='" + masterCrew.characterMasterName + "', money=0, created_at=NOW() ");
	        
	        OpenConnection();
	        string query = "SELECT id FROM factions WHERE name='" + factionname.Trim() + "' LIMIT 1";
	        queryExecuter = conn.CreateCommand();
	        queryExecuter.CommandText = query;
	        dr = queryExecuter.ExecuteReader();

	        UInt32 factionId = 0;
	        while (dr.Read())
	        {
		        factionId = (UInt32) dr.GetInt32(0);
	        }
	        CloseConnection();
	        
	        if (factionId > 0)
	        {
		        ExecuteNonResultQuery("UPDATE crews SET faction_id= '" + factionId +
		                              "' WHERE master_player_handle = '" + masterCrew.characterMasterName +
		                              "' LIMIT 1");
		        ExecuteNonResultQuery("UPDATE crews SET faction_id= '" + factionId +
		                              "' WHERE master_player_handle = '" + secondCrew.characterMasterName +
		                              "' LIMIT 1");
		        
		        OpenConnection();
		        string crewIdsQuery = "SELECT id FROM crews WHERE faction_id='" + factionId + "' ";
		        queryExecuter = conn.CreateCommand();
		        queryExecuter.CommandText = crewIdsQuery;
		        dr = queryExecuter.ExecuteReader();
		        
		        
				List<UInt32> crewIds = new List<uint>();
		        while (dr.Read())
		        {
			        crewIds.Add((UInt32) dr.GetInt32(0));

		        }
		        CloseConnection();

		        foreach (UInt32 crewId in crewIds)
		        {
			        ExecuteNonResultQuery("UPDATE characters SET factionId = '" + factionId + "' WHERE crewId = '" + crewId + "' ");
		        }
	        }

	        return factionId;
        }

        public void IncreaseCrewMoney(UInt32 crewId, UInt32 amount)
        {
	        ExecuteNonResultQuery("UPDATE crews SET money = money + '" + amount + "' WHERE id = '" + crewId.ToString() + "' LIMIT 1");
	       
        }
        
        public void DecreaseCrewMoney(UInt32 crewId, UInt32 amount)
        {
	        ExecuteNonResultQuery("UPDATE crews SET money = money - '" + amount + "' WHERE id = '" + crewId.ToString() +
	                              "' LIMIT 1");
	        
        }
        
        public void IncreaseFactionMoney(UInt32 crewId, UInt32 amount)
        {
	        ExecuteNonResultQuery("UPDATE factions SET money = money + '" + amount + "' WHERE id = '" +
	                              crewId.ToString() + "' LIMIT 1");
        }
        
        public void DecreaseFactionMoney(UInt32 crewId, UInt32 amount)
        {
	        ExecuteNonResultQuery("UPDATE factions SET money = money - '" + amount + "' WHERE id = '" +
	                              crewId.ToString() + "' LIMIT 1");
        }

        public Crew GetCrewData(UInt32 crewId)
        {
	        Crew theCrew = new Crew();
	        OpenConnection();
	        string query = "SELECT id, crew_name, master_player_handle, money, org, faction_id, faction_rank FROM crews WHERE id='" +
	                       crewId + "' LIMIT 1";
	        queryExecuter = conn.CreateCommand();
	        queryExecuter.CommandText = query;
	        dr = queryExecuter.ExecuteReader();

	        while (dr.Read())
	        {
		        theCrew.crewId = (UInt32) dr.GetInt32(0);
		        theCrew.crewName = dr.GetString(1);
		        theCrew.characterMasterName = dr.GetString(2);
		        theCrew.money = (UInt32) dr.GetInt32(3);
		        theCrew.org = (ushort) dr.GetInt16(4);
		        theCrew.factionId = (UInt32) dr.GetInt32(5);
		        theCrew.factionRank = (ushort) dr.GetInt32(6);
	        }

	        dr.Close();
	        CloseConnection();
	        return theCrew;
        }

        public List<Crew> GetCrewsForFaction(UInt32 factionID)
        {
	        List<Crew> tmpCrews = new List<Crew>();
			
	        OpenConnection();
	        string query =
		        "SELECT cr.id, cr.crew_name, cr.master_player_handle, cr.faction_id, cr.faction_rank, c.is_online FROM crews cr LEFT JOIN characters c ON cr.master_player_handle=c.handle WHERE cr.faction_id = '" +
		        factionID + "' ";
	        
	        queryExecuter = conn.CreateCommand();
	        queryExecuter.CommandText = query;
	        dr = queryExecuter.ExecuteReader();
	        while (dr.Read())
	        {
		        Crew theCrew = new Crew();
		        theCrew.crewId = (UInt32)dr.GetInt32(0);
		        theCrew.crewName = dr.GetString(1);
		        theCrew.characterMasterName = dr.GetString(2);
		        theCrew.factionId  = (UInt32) dr.GetInt32(3);
		        theCrew.factionRank = (ushort) dr.GetInt16(4);
		        theCrew.masterIsOnline = (ushort) dr.GetInt16(5);
		        tmpCrews.Add(theCrew);
	        }
	        dr.Close();
	        CloseConnection();

	        List<Crew> crews = new List<Crew>();
	        foreach (Crew theCrew in tmpCrews)
	        {
		        theCrew.masterPlayerCharId = GetCharIdByHandle(theCrew.characterMasterName);
		        crews.Add(theCrew);
	        }
	        
	        return crews;
        }

        public List<CrewMember> GetCrewMembersForCrewId(UInt32 crewId)
        {
	        List<CrewMember> crewMembers = new List<CrewMember>();
	        OpenConnection();
	        string query =
		        "SELECT cm.char_id, cm.is_captain, cm.is_first_mate, c.handle, c.is_online " +
                "FROM crew_members cm JOIN characters c ON cm.char_id=c.charId WHERE cm.crew_id = '" + crewId + "'";
	        
	        queryExecuter = conn.CreateCommand();
	        queryExecuter.CommandText = query;
	        dr = queryExecuter.ExecuteReader();
	        while (dr.Read())
	        {
		        UInt32 charId = (UInt32)dr.GetInt32(0);
		        bool isCaptain = dr.GetBoolean(1);
		        bool isFirstMate = dr.GetBoolean(2);
		        string handle = dr.GetString(3);
		        ushort isOnline = (ushort) dr.GetInt16(4);
		        
		        CrewMember member = new CrewMember();
		        member.charId = charId;
		        member.handle = handle;
		        member.isCaptain = isCaptain;
		        member.isFirstMate = isFirstMate;
		        member.isOnline = isOnline;
		        crewMembers.Add(member);
	        }
	        dr.Close();
	        CloseConnection();

	        return crewMembers;
        }

        public void AddMemberToCrew(UInt32 charId, UInt32 crewId, UInt32 factionId, ushort isCaptain, ushort isFirstMate)
        {
	        ExecuteNonResultQuery("INSERT INTO crew_members SET char_id='" + charId + "', crew_id='" + crewId + "', is_first_mate=" + isFirstMate + ", is_captain=" + isCaptain + ", created_at=NOW() ");
	        ExecuteNonResultQuery("UPDATE characters SET crewId='" + crewId + "', factionId='" + factionId + "' WHERE charId='" + charId + "' ");
        }

        public void RemoveMemberFromCrew(uint charId, uint crewId)
        {
	        ExecuteNonResultQuery("DELETE FROM crew_members WHERE crew_id = '" + crewId + "' AND char_id ='" +charId+ "' LIMIT 1");
	        ExecuteNonResultQuery("UPDATE characters SET crewId = 0, factionId = 0 WHERE charId='" + charId + "' ");
        }

        public void AddCrewToFaction(uint factionId, uint crewId)
        {
	        ExecuteNonResultQuery("UPDATE crews SET faction_id = '" + factionId + "' WHERE id='" + crewId + "' ");
        }

        public void RemoveCrewFromFaction(uint factionId, uint crewId)
        {
	        ExecuteNonResultQuery("UPDATE crews SET faction_id = '0' WHERE id='" + crewId + "' AND faction_id = '" + factionId + "' ");
        }


        public string GetPathForDistrictKey(string key){
	        OpenConnection();
			string sqlQuery="select path from districts where districts.key = '"+key+"'";
			queryExecuter= conn.CreateCommand();
			queryExecuter.CommandText = sqlQuery;					
			dr= queryExecuter.ExecuteReader();
			
			string path = "";
			
			while(dr.Read()){
				path = (string)dr.GetString(0);	
			}
			
			dr.Close();
			CloseConnection();
			return path;
			
		}


        public void updateSourceHlForObjectTracking(UInt16 sourceDistrict, UInt16 sourceHl, UInt32 lastObjectId)
        {
	        OpenConnection();
            string sqlQuery = "SELECT id,HardlineId, DistrictId, objectId FROM data_hardlines WHERE DistrictId = '" + sourceDistrict.ToString() + "' AND HardlineId = '" + sourceHl.ToString() + "' LIMIT 1";
            queryExecuter = conn.CreateCommand();
            queryExecuter.CommandText = sqlQuery;
            dr = queryExecuter.ExecuteReader();

            UInt16 theId = 0;
            UInt16 hardlineId = 0;
            UInt16 districtId = 0;
            UInt32 objectId = 0;

            while (dr.Read())
            {
                theId = (UInt16)dr.GetInt16(0);
                hardlineId = (UInt16)dr.GetInt16(1);
                districtId = (UInt16)dr.GetInt16(2);
                objectId = (UInt32)dr.GetInt32(3);

                
            }

            dr.Close();
            CloseConnection();

            if (objectId == 0 || objectId != lastObjectId)
            {
	            ExecuteNonResultQuery("UPDATE data_hardlines SET objectId = '" + lastObjectId.ToString() +
	                                  "' WHERE id = '" + theId.ToString() + "' LIMIT 1");
				#if DEBUG
				Output.WriteLine("[WORLD DB] UPDATE Hardline " + hardlineId.ToString() + " in District " + districtId.ToString() + " with Object ID : "+lastObjectId.ToString());
				#endif
			}
	        
            
        }

        public UInt16 GetNearestHardline(UInt16 district, double x, double z)
        {
	        UInt16 nearest = 0;
	        OpenConnection();
	        queryExecuter = conn.CreateCommand();
	        queryExecuter.CommandText = "SELECT HardLineId FROM data_hardlines WHERE DistrictId = '" + district +
	                                    "' ORDER BY (X - (" + x.ToString(System.Globalization.CultureInfo.InvariantCulture) +
	                                    "))*(X - (" + x.ToString(System.Globalization.CultureInfo.InvariantCulture) +
	                                    ")) + (Z - (" + z.ToString(System.Globalization.CultureInfo.InvariantCulture) +
	                                    "))*(Z - (" + z.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")) LIMIT 1";
	        dr = queryExecuter.ExecuteReader();
	        if (dr.Read())
	        {
		        nearest = Convert.ToUInt16(dr.GetValue(0));
	        }

	        dr.Close();
	        CloseConnection();
	        return nearest;
        }

        public List<UInt16> GetHardlineIdsForDistrict(UInt16 district)
        {
	        List<UInt16> ids = new List<UInt16>();
	        OpenConnection();
	        queryExecuter = conn.CreateCommand();
	        queryExecuter.CommandText =
		        "SELECT HardLineId FROM data_hardlines WHERE DistrictId = '" + district + "' ORDER BY HardLineId";
	        dr = queryExecuter.ExecuteReader();
	        while (dr.Read())
	        {
		        ids.Add(Convert.ToUInt16(dr.GetValue(0)));
	        }

	        dr.Close();
	        CloseConnection();
	        return ids;
        }

        /// <summary>Hardlines of a district the character has uploaded (char_hardlines).</summary>
        public List<UInt16> GetUploadedHardlines(UInt32 charId, UInt16 district)
        {
	        List<UInt16> ids = new List<UInt16>();
	        OpenConnection();
	        queryExecuter = conn.CreateCommand();
	        queryExecuter.CommandText = "SELECT hl_id FROM char_hardlines WHERE char_id = '" + charId +
	                                    "' AND district_id = '" + district + "' ORDER BY hl_id";
	        dr = queryExecuter.ExecuteReader();
	        while (dr.Read())
	        {
		        ids.Add(Convert.ToUInt16(dr.GetValue(0)));
	        }

	        dr.Close();
	        CloseConnection();
	        return ids;
        }

        /// <summary>Records an uploaded hardline; false if the character already had it.</summary>
        public bool AddUploadedHardline(UInt32 charId, UInt16 district, UInt16 hardline)
        {
	        if (GetUploadedHardlines(charId, district).Contains(hardline))
	        {
		        return false;
	        }

	        ExecuteNonResultQuery("INSERT INTO char_hardlines (char_id, hl_id, district_id, added) VALUES ('" + charId +
	                              "', '" + hardline + "', '" + district + "', NOW())");
	        return true;
        }

        public void updateLocationByHL(UInt16 district, UInt16 hardline)
        {
	        OpenConnection();
            string sqlQuery = "SELECT DH.X,DH.Y,DH.Z,DH.ROT,DIS.key,DH.DistrictId FROM data_hardlines AS DH, districts as DIS WHERE DH.DistrictId = '" + district.ToString() + "' AND DH.HardLineId = '" + hardline.ToString() + "' AND DH.DistrictId=DIS.id ";
            queryExecuter = conn.CreateCommand();
            queryExecuter.CommandText = sqlQuery;
            dr = queryExecuter.ExecuteReader();

            while (dr.Read()){
                double x = (double)(dr.GetFloat(0));
                double y = (double)(dr.GetFloat(1));
                double z = (double)(dr.GetFloat(2));
                string disKey = dr.GetString(4);
                Store.currentClient.playerData.setDistrict(disKey);
                Store.currentClient.playerData.setDistrictId((uint)dr.GetInt16(5));
                Store.currentClient.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(x, y, z));
                //Store.currentClient.playerInstance.YawInterval.setValue((byte)Convert.ToDecimal(dr.GetValue(3)));
            }
            dr.Close();
	        CloseConnection();
            SavePlayer(Store.currentClient);
        }

        public void setBackground(string backgroundText)
        {
	        UInt32 charID = Store.currentClient.playerData.getCharID();

            ExecuteNonResultQuery("UPDATE characters SET background = '" + backgroundText + "' WHERE charId = '" +
                                  charID + "' LIMIT 1");
            
        }
		
		public void SetPlayerValues()
		{
            IDataReader reader = null;
            try
            {
			OpenConnection();
            UInt32 charID = Store.currentClient.playerData.getCharID();
			string sqlQuery="Select handle,x,y,z,rotation,healthC,healthM,innerStrC,innerStrM,level,profession,alignment,pvpflag,firstName,lastName,exp,cash,district,districtId,factionId,crewId, repMero, repMachine, repNiobe, repEPN, repCYPH, repGM, repZion from characters where charId='"+charID+"'";
			queryExecuter= conn.CreateCommand();
			queryExecuter.CommandText = sqlQuery;					
			reader= queryExecuter.ExecuteReader();
			
			while(reader.Read()){
				
				Store.currentClient.playerInstance.CharacterID.setValue(charID);
				Store.currentClient.playerInstance.CharacterName.setValue(reader.GetString(0));

				double x = reader.GetDouble(1);
				double y = reader.GetDouble(2);
				double z = reader.GetDouble(3);

				Store.currentClient.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(x,y,z));
				Store.currentClient.playerInstance.YawInterval.setValue(PersistedYawInterval(reader.GetValue(4)));
				Store.currentClient.playerInstance.Health.setValue((UInt16)Convert.ToDecimal(reader.GetValue(5)));	
				Store.currentClient.playerInstance.MaxHealth.setValue((UInt16)Convert.ToDecimal(reader.GetValue(6)));	
				
				Store.currentClient.playerInstance.InnerStrengthAvailable.setValue((UInt16)Convert.ToDecimal(reader.GetValue(7)));	
				Store.currentClient.playerInstance.InnerStrengthMax.setValue((UInt16)Convert.ToDecimal(reader.GetValue(8)));	
				
				Store.currentClient.playerInstance.Level.setValue((byte)Convert.ToDecimal(reader.GetValue(9)));
				
   				Store.currentClient.playerInstance.TitleAbility.setValue((UInt32) Convert.ToDecimal(reader.GetValue(10)));
				Store.currentClient.playerInstance.OrganizationID.setValue((byte)Convert.ToDecimal(reader.GetValue(11)));

				// pvpflag: non-zero is flagged (PvpFlag); a hostile server overrides it at world entry
				Store.currentClient.playerData.pvpFlag = Convert.ToDecimal(reader.GetValue(12)) != 0;
				Store.currentClient.playerInstance.RealFirstName.setValue(reader.GetString(13));
				Store.currentClient.playerInstance.RealLastName.setValue(reader.GetString(14));

				Store.currentClient.playerData.setExperience((long)Convert.ToDecimal(reader.GetValue(15)));
				Store.currentClient.playerData.setInfo((long)Convert.ToDecimal(reader.GetValue(16)));
				Store.currentClient.playerData.setDistrict(reader.GetString(17));
                Store.currentClient.playerData.setDistrictId((uint)reader.GetInt16(18));
                
                
                
				UInt32 factionId = (uint) reader.GetInt16(19);
				UInt32 crewId = (uint) reader.GetInt16(20);

				Store.currentClient.playerInstance.FactionID.enable();
				Store.currentClient.playerInstance.FactionID.setValue(factionId);
				
				Store.currentClient.playerInstance.CrewID.enable();
				Store.currentClient.playerInstance.CrewID.setValue(crewId);

				// Reputation attributes carry the two's-complement bytes of the signed standings.
				Store.currentClient.playerInstance.ReputationMerovingian.setValue(unchecked((UInt16)reader.GetInt32(21)));
				Store.currentClient.playerInstance.ReputationMachines.setValue(unchecked((UInt16)reader.GetInt32(22)));
				Store.currentClient.playerInstance.ReputationNiobe.setValue(unchecked((UInt16)reader.GetInt32(23)));
				Store.currentClient.playerInstance.ReputationPluribusNeo.setValue(unchecked((UInt16)reader.GetInt32(24)));
				Store.currentClient.playerInstance.ReputationCypherites.setValue(unchecked((UInt16)reader.GetInt32(25)));
				Store.currentClient.playerInstance.ReputationGMOrganization.setValue(unchecked((UInt16)reader.GetInt32(26)));
				Store.currentClient.playerInstance.ReputationZionMilitary.setValue(unchecked((UInt16)reader.GetInt32(27)));
				

			}
			
            }
            finally
            {
                // Conversion/query failures must not poison this thread's next login.
                try { reader?.Close(); }
                finally { CloseConnection(); }
            }
			
		}

		public UInt32 GetCharIdByHandle(string handle)
		{
			OpenConnection();
			string sqlQuery = "SELECT charId FROM characters WHERE handle = '"+handle.Trim()+"' LIMIT 1";
			queryExecuter = conn.CreateCommand();
			queryExecuter.CommandText = sqlQuery;

			dr = queryExecuter.ExecuteReader();

			UInt32 charId = 0;
			while (dr.Read())
			{
				charId = (UInt32) dr.GetInt32(0);
			}
	        
			dr.Close();
			CloseConnection();

			return charId;
		}
		
        public Hashtable GetCharInfo(UInt32 charId)
        {
	        OpenConnection();
            string sqlQuery = "SELECT firstName,lastName,background, district, repMero, repMachine, repNiobe, repEPN, repCYPH, repGM, repZion, exp, cash FROM characters WHERE charId = '" + charId.ToString() + "' LIMIT 1";
            queryExecuter = conn.CreateCommand();
            queryExecuter.CommandText = sqlQuery;

            dr = queryExecuter.ExecuteReader();

            Hashtable data = new Hashtable();
            while (dr.Read())
            {
                data.Add("firstname", dr.GetString(0));
                data.Add("lastname", dr.GetString(1));
                data.Add("background", dr.GetString(2));
                data.Add("district", dr.GetString(3));
                data.Add("repMero",dr.GetInt16(4));
                data.Add("repMachine", dr.GetInt16(5));
                data.Add("repNiobe", dr.GetInt16(6));
                data.Add("repEPN", dr.GetInt16(7));
                data.Add("repCYPH", dr.GetInt16(8));
                data.Add("repGM", dr.GetInt16(9));
                data.Add("repZion", dr.GetInt16(10));
                data.Add("exp", dr.GetInt32(11));
                data.Add("cash", dr.GetInt32(12));
            }
            dr.Close();
	        CloseConnection();

            return data;
        }

        public void SetReputation(uint charId, string reputationColumn, int value)
        {
	        OpenConnection();
	        string sqlQuery = "UPDATE characters SET " + reputationColumn + "=" + value + " WHERE charId= " + charId +
	                          " LIMIT 1";
	        ExecuteNonResultQuery(sqlQuery);
	        
        }

        public void SetOrgId(uint charId, int orgId)
        {
	        OpenConnection();
	        string sqlQuery = "UPDATE characters SET alignment = " + orgId + " WHERE charId= " + charId + " LIMIT 1";
	        ExecuteNonResultQuery(sqlQuery);
        }

        public void SetPvpFlag(uint charId, bool flagged)
        {
	        OpenConnection();
	        ExecuteNonResultQuery("UPDATE characters SET pvpflag = " + (flagged ? 1 : 0) + " WHERE charId= " + charId + " LIMIT 1");
        }

        public Hashtable GetCharInfoByHandle(string handle)
        {
	        OpenConnection();
	        handle = handle.Substring(0, handle.Length - 1);
	        string sqlQuery = "SELECT c.charId, c.firstName, c.lastName, c.background, c.alignment, c.conquest_points, f.name as faction_name, fc.crew_name as crew_name FROM characters c LEFT JOIN crews fc ON c.crewId=fc.id LEFT JOIN factions f ON c.factionId=f.id WHERE c.handle = '"+handle.Trim()+"' LIMIT 1";
	        queryExecuter = conn.CreateCommand();
	        queryExecuter.CommandText = sqlQuery;

	        dr = queryExecuter.ExecuteReader();

	        Hashtable data = new Hashtable();
	        
	        while (dr.Read())
	        {
		        data.Add("charId", (UInt32)dr.GetInt32(0));
		        data.Add("firstname", dr.GetString(1));
		        data.Add("lastname", dr.GetString(2));
		        data.Add("background", dr.GetString(3));
		        data.Add("alignment",(ushort)dr.GetInt16(4));
		        data.Add("conquest_points", (UInt32)dr.GetInt32(5));
		        if (!dr.IsDBNull(6))
		        {
			        data.Add("faction_name", dr.GetString(6));    
		        }
		        
		        if (!dr.IsDBNull(7))
		        {
			        data.Add("crew_name", dr.GetString(7));    
		        }

		        data.Add("handle", handle);
	        }
	        
	        dr.Close();
	        CloseConnection();

	        return data;
        }

		public void SetRsiValues(){
			OpenConnection();
			int charID = (int) Store.currentClient.playerData.getCharID();
			string sqlQuery="Select sex,body,hat,face,shirt,coat,pants,shoes,gloves,glasses,hair,facialdetail,shirtcolor,pantscolor,coatcolor,shoecolor,glassescolor,haircolor,skintone,tattoo,facialdetailcolor,leggins from rsivalues where charId='"+charID+"'";
			queryExecuter= conn.CreateCommand();
			queryExecuter.CommandText = sqlQuery;					
			dr= queryExecuter.ExecuteReader();
			int[] rsiValues  = new int[22];
			
			while(dr.Read()){
				//we will read just one row
				for(int i = 0;i<rsiValues.Length;i++){
					int temp =(int) Convert.ToDecimal(dr.GetValue(i)); //int to int
					rsiValues[i]=temp;
				}
			}

			Store.currentClient.playerData.setRsiValues(rsiValues);
			dr.Close();
			CloseConnection();
		}

		public void SetOnlineStatus(uint charId, ushort isOnline)
		{
			ExecuteNonResultQuery("UPDATE characters SET is_online = '" + isOnline + "' WHERE charid= '" + charId +
			                      "' LIMIT 1");
			
		}

		public void ResetOnlineStatus()
		{
			ExecuteNonResultQuery("UPDATE characters SET is_online = '0' ");
		}


		public void UpdateRsiPartValue(string part, uint value, UInt32 charId)
        {
	        ExecuteNonResultQuery("UPDATE rsivalues SET " + part + "=" + value + " WHERE charid= '" + charId + "' LIMIT 1");
            
        }
		
		public void SavePlayer(WorldClient client){
			System.Globalization.CultureInfo customCulture = (System.Globalization.CultureInfo)System.Threading.Thread.CurrentThread.CurrentCulture.Clone();
            customCulture.NumberFormat.NumberDecimalSeparator = ".";
            System.Threading.Thread.CurrentThread.CurrentCulture = customCulture;

            UInt32 charID = NumericalUtils.ByteArrayToUint32(client.playerInstance.CharacterID.getValue(),1);
			string handle = StringUtils.charBytesToString_NZ(client.playerInstance.CharacterName.getValue());
			
			int [] rsiValues = client.playerData.getRsiValues();
			
			double x=0;double y=0;double z=0;
			byte[] Ltvector3d = client.playerInstance.Position.getValue();
			NumericalUtils.LtVector3dToDoubles(Ltvector3d,ref x,ref y,ref z);
			
			int rotation =client.playerInstance.YawInterval.getValue()[0];

			// The Loading Area (location 0x385) is a waiting room between death and a hardline: it doesn't fit the
			// tinyint districtId column and isn't a place to log back in to, so keep the last real location.
			if (!client.playerData.DistrictIsLA())
			{
				string positionQuery = "update characters set x =" + x + " ,y=" + y + " ,z=" + z + " , rotation=" +
				                       rotation + ", districtId=" + client.playerData.getDistrictId() + " where handle='" +
				                       handle + "' ";
				ExecuteNonResultQuery(positionQuery);
				Output.writeToLogForConsole("[WORLD DB ACCESS ]" + positionQuery);
			}

			string rsiQuery = "update rsivalues set sex='" + rsiValues[0] + "',body='" + rsiValues[1] + "',hat='" +
			                  rsiValues[2] + "',face='" + rsiValues[3] + "',shirt='" + rsiValues[4] + "',coat='" +
			                  rsiValues[5] + "',pants='" + rsiValues[6] + "',shoes='" + rsiValues[7] +
			                  "',gloves='" + rsiValues[8] + "',glasses='" + rsiValues[9] + "',hair='" +
			                  rsiValues[10] + "',facialdetail='" + rsiValues[11] + "',shirtcolor='" +
			                  rsiValues[12] + "',pantscolor='" + rsiValues[13] + "',coatcolor='" + rsiValues[14] +
			                  "',shoecolor='" + rsiValues[15] + "',glassescolor='" + rsiValues[16] +
			                  "',haircolor='" + rsiValues[17] + "',skintone='" + rsiValues[18] + "',tattoo='" +
			                  rsiValues[19] + "',facialdetailcolor='" + rsiValues[20] + "',leggins='" +
			                  rsiValues[21] + "' where charId='" + charID + "';";
            ExecuteNonResultQuery(rsiQuery);
		
            Output.writeToLogForConsole("[WORLD DB ACCESS ]" + rsiQuery);
        
			
		}


        public void UpdateInventorySlot(UInt16 sourceSlot, UInt16 destSlot, UInt32 charId)
        {
	        ExecuteNonResultQuery("UPDATE inventory SET slot = '" + destSlot.ToString() + "' WHERE slot = '" + sourceSlot.ToString() + "' AND charID = '" + charId + "' LIMIT 1");
        }

        public bool IsSlotinUseByItem(UInt16 slotId)
        {
	        OpenConnection();
            bool isSlotInUse = false;
            UInt32 charID = Store.currentClient.playerData.getCharID();
            string sqlQuery = "SELECT slot FROM inventory WHERE slot = '" + slotId.ToString() + "' AND charID = '" +
                              charID.ToString() + "' LIMIT 1";

            queryExecuter = conn.CreateCommand();
            queryExecuter.CommandText = sqlQuery;
            dr = queryExecuter.ExecuteReader();

            if (dr.Read())
            {
                isSlotInUse = true;
            }

            dr.Close();
	        CloseConnection();
            return isSlotInUse;

        }

        public UInt16 GetCrewMemberCountByCrewName(string crewName)
        {
	        OpenConnection();
            string sqlQuery = "SELECT COUNT(cm.id) as count_members FROM crew_members cm LEFT JOIN crews c ON cm.crew_id=c.id WHERE c.crew_name='" + crewName + "' LIMIT 1";
            queryExecuter = conn.CreateCommand();
            queryExecuter.CommandText = sqlQuery;
            dr = queryExecuter.ExecuteReader();

            UInt16 countCrewMembers = 0;

            if (dr.Read())
            {
                countCrewMembers = (UInt16)dr.GetInt16(0);

            }

            dr.Close();
	        CloseConnection();
            return countCrewMembers;
        }

	    public UInt32 GetCrewIdByCrewMasterHandle(string playerHandle)
	    {
		    // ToDo: we need to proove if this can work this way 
		    // ToDo: can only the master invite other players ? i am not sure then we need to change this
		    // ToDo: make DB easier
		    OpenConnection();
		    string sqlQuery = "SELECT c.id, c.crew_name FROM crew_members cm LEFT JOIN crews c ON cm.crew_id=c.id WHERE c.master_player_handle='" + playerHandle + "' LIMIT 1";
		    queryExecuter = conn.CreateCommand();
		    queryExecuter.CommandText = sqlQuery;
		    dr = queryExecuter.ExecuteReader();

		    UInt16 countCrewMembers = 0;

		    if (dr.Read())
		    {
			    countCrewMembers = (UInt16)dr.GetInt16(0);

		    }

		    dr.Close();
		    CloseConnection();
		    return countCrewMembers;
	    }

	    public uint GetCrewIdByInviterHandle(string playerHandle)
	    {
		    UInt32 crewId = 0;
		    OpenConnection();
		    string sqlQuery = "SELECT crewId FROM characters WHERE handle ='" + playerHandle + "' LIMIT 1";
		    queryExecuter = conn.CreateCommand();
		    queryExecuter.CommandText = sqlQuery;
		    dr = queryExecuter.ExecuteReader();

		    if (dr.Read())
		    {

			    crewId = (UInt32) dr.GetInt32(0);

		    }
		    dr.Close();
		    CloseConnection();
		    return crewId;
	    }

	    public string GetFactionNameById(uint factionId)
	    {
		    OpenConnection();
		    string sqlQuery = "SELECT name FROM factions WHERE id =" + factionId + " LIMIT 1";
		    queryExecuter = conn.CreateCommand();
		    queryExecuter.CommandText = sqlQuery;
		    dr = queryExecuter.ExecuteReader();

		    string factionName = "";

		    if (dr.Read())
		    {

			    factionName = dr.GetString(0);

		    }
		    dr.Close();
		    CloseConnection();
		    return factionName;
	    }

	    public bool IsCrewNameAvailable(string crewName)
        {
	        OpenConnection();
            bool isCrewNameAvailable = true;
	        crewName = crewName.Replace("'", @"\'");

            string sqlQuery = "SELECT id,created_at,deleted_at FROM crews WHERE crew_name='" + crewName + "' AND deleted_at > NOW() LIMIT 1";
            queryExecuter = conn.CreateCommand();
            queryExecuter.CommandText = sqlQuery;
            dr = queryExecuter.ExecuteReader();

            if (dr.Read())
            {
                UInt16 memberCount = GetCrewMemberCountByCrewName(crewName);

                UInt32 crewId = (UInt32)dr.GetInt32(0);
                DateTime createdAt = dr.GetDateTime(1);
                DateTime deletedAt = dr.GetDateTime(2);

                isCrewNameAvailable = false;

	            // Lets check if more than one player is in the crew and if created_at is greater than one day
                if ((DateTime.Now - deletedAt).TotalHours > 24 && memberCount < 2)
                {
                    DeleteCrew(crewId);
	                isCrewNameAvailable = true;
                }


            }

            dr.Close();
	        CloseConnection();
            return isCrewNameAvailable;
        }

        public void DeleteCrew(UInt32 crewId)
        {
	        // Delete the Crew
            ExecuteNonResultQuery("UPDATE crews SET deleted_at = NOW() WHERE id=" + crewId + " LIMIT 1");
            
	        // Delete the Player Crew ID
	        ExecuteNonResultQuery("UPDATE characters SET crewId = 0 WHERE crewId = " + crewId);
	        
	        // Delete the Player Crew ID
	        ExecuteNonResultQuery("DELETE FROM crew_members WHERE crew_id = " + crewId);
        }

        private object ScalarQuery(string sql, params KeyValuePair<string, object>[] parameters)
        {
	        OpenConnection();
	        try
	        {
		        using (IDbCommand cmd = conn.CreateCommand())
		        {
			        cmd.CommandText = sql;
			        AddParameters(cmd, parameters);
			        return cmd.ExecuteScalar();
		        }
	        }
	        finally
	        {
		        CloseConnection();
	        }
        }

        private void NonQuery(string sql, params KeyValuePair<string, object>[] parameters)
        {
	        OpenConnection();
	        try
	        {
		        using (IDbCommand cmd = conn.CreateCommand())
		        {
			        cmd.CommandText = sql;
			        AddParameters(cmd, parameters);
			        cmd.ExecuteNonQuery();
		        }
	        }
	        finally
	        {
		        CloseConnection();
	        }
        }

        private static void AddParameters(IDbCommand cmd, KeyValuePair<string, object>[] parameters)
        {
	        foreach (KeyValuePair<string, object> parameter in parameters)
	        {
		        IDbDataParameter p = cmd.CreateParameter();
		        p.ParameterName = parameter.Key;
		        p.Value = parameter.Value;
		        cmd.Parameters.Add(p);
	        }
        }

        private static KeyValuePair<string, object> P(string name, object value) =>
	        new KeyValuePair<string, object>(name, value);

        /// <summary>A crew that was not disbanded already uses this name.</summary>
        public bool CrewNameExists(string crewName)
        {
	        // Old rows carry a zero date instead of NULL for "not deleted".
	        object found = ScalarQuery("SELECT id FROM crews WHERE crew_name = @name AND (deleted_at IS NULL OR deleted_at < '1971-01-01') LIMIT 1",
		        P("@name", crewName.Trim()));
	        return found != null && found != DBNull.Value;
        }

        /// <summary>Creates a crew with the captain as its first member and returns its id.</summary>
        public UInt32 CreateCrew(string crewName, string captainHandle, ushort org)
        {
	        object id = ScalarQuery(
		        "INSERT INTO crews (crew_name, master_player_handle, money, org, faction_id, faction_rank, created_at, deleted_at) " +
		        "VALUES (@name, @captain, 0, @org, 0, 0, NOW(), NULL); SELECT LAST_INSERT_ID();",
		        P("@name", crewName.Trim()), P("@captain", captainHandle), P("@org", (int) org));
	        return Convert.ToUInt32(id);
        }

        public void SetCrewCaptain(UInt32 crewId, UInt32 charId, string handle)
        {
	        NonQuery("UPDATE crew_members SET is_captain = (char_id = @char) WHERE crew_id = @crew",
		        P("@char", charId), P("@crew", crewId));
	        NonQuery("UPDATE crews SET master_player_handle = @handle WHERE id = @crew",
		        P("@handle", handle), P("@crew", crewId));
        }

        /// <summary>Marks the crew deleted and clears crew and faction of its members.</summary>
        public void DisbandCrew(UInt32 crewId)
        {
	        NonQuery("UPDATE crews SET deleted_at = NOW(), faction_id = 0 WHERE id = @crew", P("@crew", crewId));
	        NonQuery("UPDATE characters SET crewId = 0, factionId = 0 WHERE crewId = @crew", P("@crew", crewId));
	        NonQuery("DELETE FROM crew_members WHERE crew_id = @crew", P("@crew", crewId));
        }

        public void SetFactionOfCrewMembers(UInt32 crewId, UInt32 factionId)
        {
	        NonQuery("UPDATE characters SET factionId = @faction WHERE crewId = @crew",
		        P("@faction", factionId), P("@crew", crewId));
        }

        /// <summary>Marks the faction deleted and takes every crew and character out of it.</summary>
        public void DisbandFaction(UInt32 factionId)
        {
	        NonQuery("UPDATE factions SET deleted_at = NOW() WHERE id = @faction", P("@faction", factionId));
	        NonQuery("UPDATE crews SET faction_id = 0 WHERE faction_id = @faction", P("@faction", factionId));
	        NonQuery("UPDATE characters SET factionId = 0 WHERE factionId = @faction", P("@faction", factionId));
        }

        public UInt32 CreateFaction(string factionName, string masterHandle)
        {
	        object id = ScalarQuery(
		        "INSERT INTO factions (name, master_player_handle, money, created_at, deleted_at) VALUES (@name, @master, 0, NOW(), NULL); SELECT LAST_INSERT_ID();",
		        P("@name", factionName.Trim()), P("@master", masterHandle));
	        return Convert.ToUInt32(id);
        }

        /// <summary>Makes the member the crew's first mate (0: nobody).</summary>
        public void SetCrewFirstMate(UInt32 crewId, UInt32 charId)
        {
	        NonQuery("UPDATE crew_members SET is_first_mate = (char_id = @char) WHERE crew_id = @crew",
		        P("@char", charId), P("@crew", crewId));
        }

        /// <summary>The crew's rank in its faction, 1 (leading) to 5.</summary>
        public void SetCrewFactionRank(UInt32 crewId, ushort rank)
        {
	        NonQuery("UPDATE crews SET faction_rank = @rank WHERE id = @crew", P("@rank", (int) rank), P("@crew", crewId));
        }

        public void SetFactionMaster(UInt32 factionId, string handle)
        {
	        NonQuery("UPDATE factions SET master_player_handle = @handle WHERE id = @faction",
		        P("@handle", handle), P("@faction", factionId));
        }

        public bool IsOnFriendList(UInt32 charId, UInt32 friendId)
        {
	        object found = ScalarQuery("SELECT id FROM buddylist WHERE charId = @char AND friendId = @friend LIMIT 1",
		        P("@char", charId), P("@friend", friendId));
	        return found != null && found != DBNull.Value;
        }

        public void AddCrew(string crewName, string masterHandle)
        {
	        crewName = crewName.Replace("'", @"\'");
            ExecuteNonResultQuery("INSERT INTO crews SET crew_name= '" + crewName + "', created_at =NOW() ");
        }

        // char_missions may be missing from a database made before missions were tracked.
        private void EnsureMissionTable()
        {
            OpenConnection();
            queryExecuter = conn.CreateCommand();
            queryExecuter.CommandText = "CREATE TABLE IF NOT EXISTS char_missions (charId INT UNSIGNED NOT NULL, missionId INT NOT NULL, " +
                                        "completions INT NOT NULL DEFAULT 1, PRIMARY KEY (charId, missionId))";
            queryExecuter.ExecuteNonQuery();
        }

        public HashSet<int> GetCompletedMissions(UInt32 charId)
        {
            EnsureMissionTable();
            HashSet<int> completed = new HashSet<int>();
            queryExecuter = conn.CreateCommand();
            queryExecuter.CommandText = "SELECT missionId FROM char_missions WHERE charId = '" + charId + "'";
            dr = queryExecuter.ExecuteReader();
            while (dr.Read())
            {
                completed.Add(dr.GetInt32(0));
            }

            dr.Close();
            CloseConnection();
            return completed;
        }

        public void SaveMissionCompleted(UInt32 charId, int missionId)
        {
            EnsureMissionTable();
            queryExecuter = conn.CreateCommand();
            queryExecuter.CommandText = "INSERT INTO char_missions (charId, missionId) VALUES ('" + charId + "', '" + missionId + "') " +
                                        "ON DUPLICATE KEY UPDATE completions = completions + 1";
            queryExecuter.ExecuteNonQuery();
            CloseConnection();
        }

        public UInt16 GetFirstNewSlot()
        {
	        OpenConnection();
            UInt32 charID = Store.currentClient.playerData.getCharID();

            // The lowest free bag slot, 1-96 (worn items start at 97). The old query returned the slot after the first
            // used slot whose successor was free: 97 (a worn slot) when 96 was used, 0 for an empty bag.
            string sqlQuery = "SELECT slot FROM inventory WHERE charId = '" + charID.ToString() + "' AND slot BETWEEN 1 AND " + LastBagSlot;
            queryExecuter = conn.CreateCommand();
            queryExecuter.CommandText = sqlQuery;
            dr = queryExecuter.ExecuteReader();

            var used = new System.Collections.Generic.HashSet<int>();
            while (dr.Read()){
                used.Add(Convert.ToInt32(dr.GetValue(0)));
            }
            dr.Close();
	        CloseConnection();
            return LowestFreeBagSlot(used);
        }

        /// <summary>Last bag slot; 97 and up are worn items.</summary>
        public const int LastBagSlot = 96;

        /// <summary>The lowest bag slot (1-96) not in <paramref name="used"/>, or 0 when the bag is full.</summary>
        public static UInt16 LowestFreeBagSlot(System.Collections.Generic.ICollection<int> used)
        {
            for (int slot = 1; slot <= LastBagSlot; slot++)
            {
                if (!used.Contains(slot))
                {
                    return (UInt16)slot;
                }
            }
            return 0;
        }

        public void AddItemToInventory (UInt16 slotId, UInt32 itemGoID)
        {
	        UInt32 charID = Store.currentClient.playerData.getCharID();
            ExecuteNonResultQuery("INSERT INTO inventory SET charId = '" + charID.ToString() + "' , goid = '" +
                                  itemGoID + "', slot = '" + slotId.ToString() + "', created = NOW() ");
        }

        public void RemoveItemAtInventorySlot(UInt16 slotId, UInt32 charId)
        {
	        // Mirrors AddItemToInventory / UpdateInventorySlot: same `inventory` table, same
	        // charId+slot keying, same string-concat style used everywhere else in this class.
	        ExecuteNonResultQuery("DELETE FROM inventory WHERE charId = '" + charId.ToString() +
	                              "' AND slot = '" + slotId.ToString() + "' LIMIT 1");
        }

        public UInt32 GetItemGOIDAtInventorySlot(UInt16 slotId)
        {
	        OpenConnection();
            UInt32 charID = Store.currentClient.playerData.getCharID();

            string sqlQuery = "SELECT goid FROM inventory WHERE charID = '" + charID + "' AND slot = '" + slotId + "' LIMIT 1";
            queryExecuter = conn.CreateCommand();
            queryExecuter.CommandText = sqlQuery;
            dr = queryExecuter.ExecuteReader();

            UInt32 GoID = 0;
            while (dr.Read()){
                // goid is a bigint; ability codes such as 0x80001c00 (2147490816) don't fit in an
                // Int32, and GetInt32 threw OverflowException, which crashed the server on recycle.
                GoID  = Convert.ToUInt32(Convert.ToInt64(dr.GetValue(0)));
            }
            dr.Close();
	        CloseConnection();
            return GoID;
        }


        /// <summary>Every inventory row of a character as (slot, goid).</summary>
        public List<Crafting.InventoryItem> GetInventoryItems(UInt32 charId)
        {
	        OpenConnection();
            queryExecuter = conn.CreateCommand();
            queryExecuter.CommandText = "SELECT slot, goid, count FROM inventory WHERE charId = '" + charId + "' ORDER BY slot";
            dr = queryExecuter.ExecuteReader();

            List<Crafting.InventoryItem> items = new List<Crafting.InventoryItem>();
            while (dr.Read())
            {
                items.Add(new Crafting.InventoryItem(Convert.ToUInt16(dr.GetValue(0)),
                    Convert.ToUInt32(Convert.ToInt64(dr.GetValue(1))), ItemStacks.CountFromDb(Convert.ToInt32(dr.GetValue(2)))));
            }
            dr.Close();
	        CloseConnection();
            return items;
        }

        /// <summary>Sets how many items the stack in a slot holds (inventory.count).</summary>
        public void SetInventoryCount(UInt16 slotId, UInt32 charId, int count)
        {
	        ExecuteNonResultQuery("UPDATE inventory SET count = '" + count + "' WHERE charId = '" + charId +
	                              "' AND slot = '" + slotId + "' LIMIT 1");
        }

        private readonly object inventoryMigrationLock = new object();
        private bool inventorySupportsTransactions;

        /// <summary>Upgrades old inventory tables before the network listeners accept players.</summary>
        public void EnsureTransactionalInventory()
        {
            using MySqlConnection connection = new MySqlConnection(connectionString);
            connection.Open();
            EnsureTransactionalInventory(connection);
        }

        private void EnsureTransactionalInventory(MySqlConnection connection)
        {
            lock (inventoryMigrationLock)
            {
                if (inventorySupportsTransactions)
                {
                    return;
                }
                using MySqlCommand command = connection.CreateCommand();
                command.CommandText = "SELECT ENGINE FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'inventory'";
                bool transactional = string.Equals(command.ExecuteScalar() as string, "InnoDB", StringComparison.OrdinalIgnoreCase);
                command.CommandText = "SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='inventory' AND INDEX_NAME='inventory_character_slot'";
                bool indexed = Convert.ToInt32(command.ExecuteScalar()) > 0;
                if (!transactional || !indexed)
                {
                    // The original dump contains zero timestamps. Keep those values during the engine conversion.
                    command.CommandText = "SELECT @@SESSION.sql_mode";
                    string originalMode = (string)command.ExecuteScalar();
                    List<string> modes = new List<string>(originalMode.Split(','));
                    modes.Remove("NO_ZERO_DATE");
                    modes.Remove("NO_ZERO_IN_DATE");
                    command.CommandText = "SET SESSION sql_mode = @mode";
                    command.Parameters.AddWithValue("@mode", string.Join(",", modes));
                    command.ExecuteNonQuery();
                    try
                    {
                        command.CommandText = "ALTER TABLE inventory ENGINE=InnoDB" +
                            (indexed ? "" : ", ADD KEY inventory_character_slot (charId, slot)");
                        command.ExecuteNonQuery();
                    }
                    finally
                    {
                        command.CommandText = "SET SESSION sql_mode = @mode";
                        command.Parameters["@mode"].Value = originalMode;
                        command.ExecuteNonQuery();
                    }
                }
                inventorySupportsTransactions = true;
            }
        }

        public CollectorExchange ExchangeCollectorItems(UInt32 charId, UInt32 requiredItem, int quantity, UInt32 rewardItem, bool uniqueReward = false)
        {
            using MySqlConnection connection = new MySqlConnection(connectionString);
            connection.Open();
            EnsureTransactionalInventory(connection);
            using MySqlTransaction transaction = connection.BeginTransaction();
            CollectorExchange result=ExchangeCollectorItems(connection,transaction,charId,requiredItem,quantity,rewardItem,uniqueReward);
            if(result!=null)transaction.Commit();
            return result;
        }

        private CollectorExchange ExchangeCollectorItems(MySqlConnection connection, MySqlTransaction transaction,
            UInt32 charId, UInt32 requiredItem, int quantity, UInt32 rewardItem, bool uniqueReward = false)
        {
            using MySqlCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SELECT invId, slot, goid, count FROM inventory WHERE charId=@char ORDER BY slot FOR UPDATE";
            command.Parameters.AddWithValue("@char", charId);

            HashSet<int> occupied = new HashSet<int>();
            List<(UInt32 Id, UInt16 Slot, int Remaining)> changes = new List<(UInt32, UInt16, int)>();
            using (MySqlDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    if (uniqueReward && reader.GetUInt32(2) == rewardItem)
                    {
                        return null;
                    }
                    UInt16 slot = reader.GetUInt16(1);
                    if (slot < 1 || slot > 96)
                    {
                        continue;
                    }
                    if (quantity == 0 || reader.GetUInt32(2) != requiredItem)
                    {
                        occupied.Add(slot);
                        continue;
                    }

                    int count = ItemStacks.CountFromDb(reader.GetInt32(3));
                    int consumed = Math.Min(quantity, count);
                    quantity -= consumed;
                    int remaining = count - consumed;
                    changes.Add((reader.GetUInt32(0), slot, remaining));
                    if (remaining > 0)
                    {
                        occupied.Add(slot);
                    }
                }
            }

            UInt16 rewardSlot = LowestFreeBagSlot(occupied);
            if (quantity != 0 || rewardSlot == 0)
            {
                return null;
            }

            command.Parameters.AddWithValue("@row", 0u);
            command.Parameters.AddWithValue("@remaining", 0);
            foreach (var change in changes)
            {
                command.Parameters["@row"].Value = change.Id;
                command.Parameters["@remaining"].Value = change.Remaining;
                command.CommandText = change.Remaining == 0
                    ? "DELETE FROM inventory WHERE invId=@row"
                    : "UPDATE inventory SET count=@remaining WHERE invId=@row";
                command.ExecuteNonQuery();
            }

            command.CommandText = "INSERT INTO inventory (charId, goid, slot, count) VALUES (@char, @reward, @slot, 1)";
            command.Parameters.AddWithValue("@reward", rewardItem);
            command.Parameters.AddWithValue("@slot", rewardSlot);
            command.ExecuteNonQuery();
            return new CollectorExchange(rewardSlot, changes.ConvertAll(change => (change.Slot, change.Remaining)));
        }

        /// <summary>Adds a stack of <paramref name="count"/> items to a slot.</summary>
        public void AddItemToInventory(UInt16 slotId, UInt32 itemGoID, int count)
        {
	        UInt32 charID = Store.currentClient.playerData.getCharID();
            ExecuteNonResultQuery("INSERT INTO inventory SET charId = '" + charID + "' , goid = '" + itemGoID +
                                  "', slot = '" + slotId + "', count = '" + count + "', created = NOW() ");
        }

        /// <summary>
        /// Code storage rows: char_abilities rows whose ability_id is CodeStorage.BaseValue(goid) and whose level
        /// holds count - 1, so the margin's known-abilities list (ability_id + level) sends them as retail did.
        /// </summary>
        public List<CodeStorage.Entry> GetCodeStorage(UInt32 charId)
        {
	        OpenConnection();
            queryExecuter = conn.CreateCommand();
            queryExecuter.CommandText = "SELECT slot, ability_id, level FROM char_abilities WHERE char_id = '" + charId +
                                        "' AND (ability_id & 0xFC000000) = 0x84000000 ORDER BY slot";
            dr = queryExecuter.ExecuteReader();

            List<CodeStorage.Entry> entries = new List<CodeStorage.Entry>();
            while (dr.Read())
            {
                UInt32 value = unchecked((UInt32)Convert.ToInt64(dr.GetValue(1)));
                entries.Add(new CodeStorage.Entry(Convert.ToUInt16(dr.GetValue(0)), CodeStorage.GoidOf(value),
                    Convert.ToInt32(dr.GetValue(2)) + 1));
            }
            dr.Close();
	        CloseConnection();
            return entries;
        }

        /// <summary>The slot after the highest ability or code storage slot of a character.</summary>
        public UInt16 GetNextAbilitySlot(UInt32 charId)
        {
	        OpenConnection();
            queryExecuter = conn.CreateCommand();
            queryExecuter.CommandText = "SELECT COALESCE(MAX(slot), -1) + 1 FROM char_abilities WHERE char_id = '" + charId +
                                        "' AND slot < " + CharacterAttributes.FirstSlot;
            UInt16 slot = Convert.ToUInt16(queryExecuter.ExecuteScalar());
	        CloseConnection();
            return slot;
        }

        public System.Collections.Generic.HashSet<int> GetAbilityInventorySlots(UInt32 charId)
        {
	        OpenConnection();
            queryExecuter = conn.CreateCommand();
            queryExecuter.CommandText = "SELECT slot FROM char_abilities WHERE char_id = '" + charId + "' AND slot < " +
                                        CharacterAttributes.FirstSlot;
            dr = queryExecuter.ExecuteReader();
            var slots = new System.Collections.Generic.HashSet<int>();
            while (dr.Read())
            {
                slots.Add(Convert.ToInt32(dr.GetValue(0)));
            }
            dr.Close();
	        CloseConnection();
            return slots;
        }

        public void AddCodeStorage(UInt32 charId, CodeStorage.Entry entry)
        {
	        ExecuteNonResultQuery("INSERT INTO char_abilities SET char_id = '" + charId + "', slot = '" + entry.Slot +
	                              "', ability_id = '" + CodeStorage.BaseValue(entry.Goid) + "', level = '" + (entry.Count - 1) +
	                              "', is_loaded = 0, ability_name = 'Code storage " + entry.Goid + "', added = NOW()");
        }

        public void SetCodeStorageCount(UInt32 charId, UInt16 slot, int count)
        {
	        ExecuteNonResultQuery("UPDATE char_abilities SET level = '" + (count - 1) + "' WHERE char_id = '" + charId +
	                              "' AND slot = '" + slot + "' AND (ability_id & 0xFC000000) = 0x84000000 LIMIT 1");
        }

        public void RemoveCodeStorage(UInt32 charId, UInt16 slot)
        {
	        ExecuteNonResultQuery("DELETE FROM char_abilities WHERE char_id = '" + charId + "' AND slot = '" + slot +
	                              "' AND (ability_id & 0xFC000000) = 0x84000000 LIMIT 1");
        }

	    public void SaveInfo(WorldClient client, long cash)
	    {
		    ExecuteNonResultQuery("UPDATE characters SET cash =" + cash + " WHERE charId= " +
		                          client.playerData.getCharID().ToString() + " LIMIT 1");
		    
	    }

	    public void UpdateAbilityLoadOut(List<UInt16> abilitySlots, uint loaded)
        {
	        UInt32 charID = Store.currentClient.playerData.getCharID();
            string sqlQuery = "";
            foreach(ushort slot in abilitySlots)
            {
                sqlQuery += "UPDATE char_abilities SET is_loaded = " + loaded.ToString() + " WHERE char_id = " + charID.ToString() + " AND slot = " + slot.ToString() + ";";
            }

			ExecuteNonResultQuery(sqlQuery);
        }

	    public UInt16 GetCurrentAbilityLevel(Int32 abilityGoId, UInt32 charId)
	    {
		    OpenConnection();

		    string sqlQuery = "SELECT level FROM char_abilities WHERE char_id = '" + charId + "' AND ability_id = '" + abilityGoId + "' LIMIT 1";
		    queryExecuter = conn.CreateCommand();
		    queryExecuter.CommandText = sqlQuery;
		    dr = queryExecuter.ExecuteReader();

		    UInt16 level = 0;
		    while (dr.Read()){
			    level = (UInt16)dr.GetInt16(0);
		    }
		    dr.Close();
		    CloseConnection();
		    return level;
	    }

	    public int? GetAbilityLevelAtSlot(UInt32 charId, UInt16 slot, Int32 abilityGoId)
	    {
		    OpenConnection();
		    queryExecuter = conn.CreateCommand();
		    queryExecuter.CommandText = "SELECT level FROM char_abilities WHERE char_id = '" + charId + "' AND slot = '" + slot +
		                                "' AND ability_id = '" + abilityGoId + "' LIMIT 1";
		    object level = queryExecuter.ExecuteScalar();
		    CloseConnection();
		    return level == null ? (int?)null : Convert.ToInt32(level);
	    }

	    public bool HasAbility(UInt32 charId, Int32 abilityGoId)
	    {
		    OpenConnection();
		    queryExecuter = conn.CreateCommand();
		    queryExecuter.CommandText = "SELECT COUNT(*) FROM char_abilities WHERE char_id = '" + charId + "' AND ability_id = '" + abilityGoId + "'";
		    long rows = Convert.ToInt64(queryExecuter.ExecuteScalar());
		    CloseConnection();
		    return rows > 0;
	    }

	    public void AddAbility(UInt32 charId, UInt16 slot, Int32 abilityGoId, int level)
	    {
		    ExecuteNonResultQuery("INSERT INTO char_abilities SET char_id = '" + charId + "', slot = '" + slot + "', ability_id = '" + abilityGoId +
		                          "', level = '" + level + "', is_loaded = 0, ability_name = 'Ability " + abilityGoId + "', added = NOW()");
	    }

	    public void SetAbilityLevel(UInt32 charId, UInt16 slot, int level)
	    {
		    ExecuteNonResultQuery("UPDATE char_abilities SET level = '" + level + "' WHERE char_id = '" + charId + "' AND slot = '" + slot + "'");
	    }

	    public void SaveLevel(UInt32 charId, int level, UInt16 health, UInt16 maxHealth, UInt16 innerStrength, UInt16 maxInnerStrength)
	    {
		    ExecuteNonResultQuery("UPDATE characters SET level = '" + level + "', healthC = '" + health + "', healthM = '" + maxHealth +
		                          "', innerStrC = '" + innerStrength + "', innerStrM = '" + maxInnerStrength + "' WHERE charId = '" + charId + "' LIMIT 1");
	    }

	    public Dictionary<byte, UInt16> GetCharacterAttributes(UInt32 charId)
	    {
		    OpenConnection();
		    queryExecuter = conn.CreateCommand();
		    queryExecuter.CommandText = "SELECT slot, level FROM char_abilities WHERE char_id = '" + charId + "' AND slot >= " + CharacterAttributes.FirstSlot;
		    dr = queryExecuter.ExecuteReader();

		    Dictionary<byte, UInt16> attributes = new Dictionary<byte, UInt16>();
		    while (dr.Read())
		    {
			    attributes[(byte)(Convert.ToInt32(dr.GetValue(0)) - CharacterAttributes.FirstSlot)] = Convert.ToUInt16(dr.GetValue(1));
		    }
		    dr.Close();
		    CloseConnection();
		    return attributes;
	    }

	    public void SetCharacterAttribute(UInt32 charId, byte attribute, UInt16 value)
	    {
		    UInt16 slot = CharacterAttributes.SlotOf(attribute);
		    ExecuteNonResultQuery("DELETE FROM char_abilities WHERE char_id = '" + charId + "' AND slot = '" + slot + "'; " +
		                          "INSERT INTO char_abilities SET char_id = '" + charId + "', slot = '" + slot +
		                          "', ability_id = '" + CharacterAttributes.RowValueOf(attribute) + "', level = '" + value +
		                          "', is_loaded = 0, ability_name = 'Attribute " + attribute + "', added = NOW()");
	    }

        public void SaveExperience(WorldClient client, long exp)
        {
	        ExecuteNonResultQuery("UPDATE characters SET exp =" + exp + " WHERE charId= " + client.playerData.getCharID().ToString() + " LIMIT 1");
        }
    }
}

