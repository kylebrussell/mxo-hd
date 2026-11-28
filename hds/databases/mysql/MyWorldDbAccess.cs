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
		[ThreadStatic] private static string threadConnectionString;
		[ThreadStatic] private static bool threadConnectionOwned;
		[ThreadStatic] private static IDbCommand threadCommand;
		[ThreadStatic] private static IDataReader threadReader;
		[ThreadStatic] private static bool threadReaderOwned;
		private readonly string connectionString;
		private readonly Func<string, IDbConnection> connectionFactory;

		private IDbConnection conn
		{
			get
			{
				if (threadConnection == null ||
					(connectionString != null && threadConnectionOwned &&
					 !string.Equals(threadConnectionString, connectionString, StringComparison.Ordinal)))
				{
					ResetThreadConnection();
					Func<string, IDbConnection> factory = connectionFactory ?? (cs => new MySqlConnection(cs));
					threadConnection = factory(connectionString);
					threadConnectionString = connectionString;
					threadConnectionOwned = true;
				}
				return threadConnection;
			}
		}
		private IDbCommand queryExecuter { get => threadCommand; set => threadCommand = value; }
		private IDataReader dr
		{
			get => threadReader;
			set
			{
				threadReader = value;
				threadReaderOwned = value != null;
			}
		}

		public MyWorldDbAccess(){
            var config = Store.config;
			/* Params: Host, port, database, user, password */
            connectionString = ("Server=" + config.dbParams.Host + ";" +
                "Port=" + config.dbParams.Port + ";" +
                "Database=" + config.dbParams.DatabaseName + ";" +
                "User ID=" + config.dbParams.Username + ";" +
                "Password=" + config.dbParams.Password + ";" +
                "Pooling=true;");
            connectionFactory = cs => new MySqlConnection(cs);
		}

		internal MyWorldDbAccess(string connectionString, Func<string, IDbConnection> connectionFactory)
		{
			this.connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
			this.connectionFactory = connectionFactory ?? (cs => new MySqlConnection(cs));
		}

		internal static IDbConnection CurrentThreadConnectionForTesting => threadConnection;

		internal static void ClearThreadStateForTesting()
		{
			ResetThreadConnection();
		}

		internal static void SetAbandonedReaderForTesting(IDataReader reader, IDbCommand command = null)
		{
			threadReader = reader;
			threadReaderOwned = reader != null;
			threadCommand = command;
		}

		private static void DisposeActiveReaderAndCommand()
		{
			if (threadReaderOwned)
			{
				IDataReader activeReader = threadReader;
				threadReader = null;
				threadReaderOwned = false;
				if (activeReader != null)
				{
					try { activeReader.Close(); } catch { }
					try { activeReader.Dispose(); } catch { }
				}
			}

			IDbCommand activeCommand = threadCommand;
			threadCommand = null;
			if (activeCommand != null)
			{
				try { activeCommand.Dispose(); } catch { }
			}
		}

		private static void ResetThreadConnection()
		{
			DisposeActiveReaderAndCommand();
			IDbConnection existing = threadConnection;
			threadConnection = null;
			threadConnectionString = null;
			threadConnectionOwned = false;
			if (existing != null)
			{
				try { existing.Close(); } catch { }
				try { existing.Dispose(); } catch { }
			}
		}

		public void OpenConnection()
		{
			// Recover from any prior unhandled exception on this thread that left an owned reader/command open
			// or left our pooled connection in an Open/Broken state, and handle DB swaps cleanly.
			bool hadAbandonedOwnedReader = threadReaderOwned && threadReader != null;
			DisposeActiveReaderAndCommand();

			if (connectionString != null && threadConnection != null &&
				(threadConnection.State == ConnectionState.Broken ||
				 (threadConnectionOwned &&
				  (hadAbandonedOwnedReader ||
				   !string.Equals(threadConnectionString, connectionString, StringComparison.Ordinal) ||
				   threadConnection.State != ConnectionState.Closed))))
			{
				ResetThreadConnection();
			}

			IDbConnection connection = conn;
			if (connection.State == ConnectionState.Closed)
			{
				connection.Open();
			}
		}

		public void CloseConnection()
		{
			DisposeActiveReaderAndCommand();
			IDbConnection existing = threadConnection;
			if (existing == null)
			{
				return;
			}

			if (existing.State == ConnectionState.Broken)
			{
				ResetThreadConnection();
				return;
			}

			if (existing.State != ConnectionState.Closed)
			{
				try
				{
					existing.Close();
				}
				catch
				{
					ResetThreadConnection();
				}
			}
		}
		
		public void ExecuteNonResultQuery(string query)
		{
			OpenConnection();
			try
			{
				using (IDbCommand cmd = conn.CreateCommand())
				{
					queryExecuter = cmd;
					cmd.CommandText = query;
					cmd.ExecuteNonQuery();
				}
			}
			finally
			{
				CloseConnection();
			}
		}
		
		private static readonly HashSet<string> AllowedReputationColumns = new HashSet<string>(StringComparer.Ordinal)
		{
			"repMero", "repMachine", "repNiobe", "repEPN", "repCYPH", "repGM", "repZion"
		};

		internal static bool IsAllowedReputationColumn(string column) =>
			!string.IsNullOrEmpty(column) && AllowedReputationColumns.Contains(column);

		private volatile bool missionTableEnsured;

		private void QueryRows(string sql, Action<IDataReader> onRow, params KeyValuePair<string, object>[] parameters)
		{
			OpenConnection();
			try
			{
				using (IDbCommand cmd = conn.CreateCommand())
				{
					queryExecuter = cmd;
					cmd.CommandText = sql;
					AddParameters(cmd, parameters);
					using (IDataReader reader = cmd.ExecuteReader())
					{
						dr = reader;
						while (reader.Read())
						{
							onRow(reader);
						}
					}
				}
			}
			finally
			{
				CloseConnection();
			}
		}

		public UInt32 GetUserIdForCharId(byte[] charIdHex)
		{
			UInt32 charId = NumericalUtils.ByteArrayToUint32(charIdHex, 1);
			Output.OptWriteLine("[WORLD] Checking from DB:" + charId);
			UInt32 userId = 0;
			QueryRows("SELECT userid FROM characters WHERE charid = @charId",
				reader => userId = (UInt32)Convert.ToDecimal(reader.GetValue(0)),
				P("@charId", charId));
			return userId;
		}

		public void AddHandleToFriendList(string handleToAdd, UInt32 charId)
		{
			UInt32 friendId = GetCharIdByHandle(handleToAdd);
			NonQuery("INSERT INTO buddylist SET charId = @charId, friendId = @friendId, is_ignored = 0",
				P("@charId", charId), P("@friendId", friendId));
		}

		public void RemoveHandleFromFriendList(string handleToRemove, UInt32 charId)
		{
			UInt32 friendId = GetCharIdByHandle(handleToRemove);
			NonQuery("DELETE FROM buddylist WHERE charId = @charId AND friendId = @friendId",
				P("@charId", charId), P("@friendId", friendId));
		}

		public ArrayList FetchPlayersWhoAddedMeToBuddylist(UInt32 charId)
		{
			ArrayList friends = new ArrayList();
			QueryRows("SELECT C.handle, C.charId, C.is_online, B.friendId FROM buddylist B LEFT JOIN characters C ON B.charId=C.charId WHERE B.friendId = @charId",
				reader =>
				{
					Hashtable data = new Hashtable();
					data.Add("handle", reader.GetString(0));
					data.Add("online", reader.GetInt16(1));
					friends.Add(data);
				},
				P("@charId", charId));
			return friends;
		}

		public ArrayList FetchFriendList(UInt32 charId)
		{
			ArrayList friends = new ArrayList();
			QueryRows("SELECT C.handle, C.is_online, B.friendId FROM buddylist B LEFT JOIN characters C ON B.friendId=C.charId WHERE B.charId = @charId",
				reader =>
				{
					Hashtable data = new Hashtable();
					data.Add("handle", reader.GetString(0));
					data.Add("online", reader.GetInt16(1));
					friends.Add(data);
				},
				P("@charId", charId));
			return friends;
		}

		public Faction FetchFaction(uint factionId)
		{
			Faction factionData = new Faction();
			QueryRows("SELECT f.id, f.name, f.master_player_handle, f.money FROM factions f LEFT JOIN characters c ON f.id=c.factionId WHERE f.id = @factionId LIMIT 1",
				reader =>
				{
					factionData.factionId = (UInt32)reader.GetInt32(0);
					factionData.name = reader.GetString(1);
					factionData.masterPlayerHandle = reader.GetString(2);
					factionData.money = (UInt32)reader.GetInt32(3);
				},
				P("@factionId", factionId));

			factionData.crews = GetCrewsForFaction(factionData.factionId);
			return factionData;
		}

		public bool IsHandleCaptainOfACrew(string handle)
		{
			bool isCaptainOfCrew = false;
			QueryRows("SELECT id FROM crews WHERE master_player_handle = @handle LIMIT 1",
				_ => isCaptainOfCrew = true,
				P("@handle", (handle ?? "").Trim()));
			return isCaptainOfCrew;
		}

		public UInt32 createFaction(string factionname, Crew masterCrew, Crew secondCrew)
		{
			string trimmedName = (factionname ?? "").Trim();
			NonQuery("INSERT INTO factions SET name = @name, master_player_handle = @master, money = 0, created_at = NOW()",
				P("@name", trimmedName), P("@master", masterCrew.characterMasterName));

			UInt32 factionId = 0;
			QueryRows("SELECT id FROM factions WHERE name = @name LIMIT 1",
				reader => factionId = (UInt32)reader.GetInt32(0),
				P("@name", trimmedName));

			if (factionId > 0)
			{
				NonQuery("UPDATE crews SET faction_id = @factionId WHERE master_player_handle = @master LIMIT 1",
					P("@factionId", factionId), P("@master", masterCrew.characterMasterName));
				NonQuery("UPDATE crews SET faction_id = @factionId WHERE master_player_handle = @master LIMIT 1",
					P("@factionId", factionId), P("@master", secondCrew.characterMasterName));

				List<UInt32> crewIds = new List<uint>();
				QueryRows("SELECT id FROM crews WHERE faction_id = @factionId",
					reader => crewIds.Add((UInt32)reader.GetInt32(0)),
					P("@factionId", factionId));

				foreach (UInt32 crewId in crewIds)
				{
					NonQuery("UPDATE characters SET factionId = @factionId WHERE crewId = @crewId",
						P("@factionId", factionId), P("@crewId", crewId));
				}
			}

			return factionId;
		}

		public Crew GetCrewData(UInt32 crewId)
		{
			Crew theCrew = new Crew();
			QueryRows("SELECT id, crew_name, master_player_handle, money, org, faction_id, faction_rank FROM crews WHERE id = @crewId LIMIT 1",
				reader =>
				{
					theCrew.crewId = (UInt32)reader.GetInt32(0);
					theCrew.crewName = reader.GetString(1);
					theCrew.characterMasterName = reader.GetString(2);
					theCrew.money = Convert.ToUInt32(reader.GetValue(3));
					theCrew.org = (ushort)reader.GetInt16(4);
					theCrew.factionId = (UInt32)reader.GetInt32(5);
					theCrew.factionRank = (ushort)reader.GetInt32(6);
				},
				P("@crewId", crewId));
			return theCrew;
		}

		public List<Crew> GetCrewsForFaction(UInt32 factionID)
		{
			List<Crew> crews = new List<Crew>();
			QueryRows(
				"SELECT cr.id, cr.crew_name, cr.master_player_handle, cr.faction_id, cr.faction_rank, c.is_online, c.charId " +
				"FROM crews cr LEFT JOIN characters c ON cr.master_player_handle=c.handle WHERE cr.faction_id = @factionId",
				reader =>
				{
					Crew theCrew = new Crew();
					theCrew.crewId = (UInt32)reader.GetInt32(0);
					theCrew.crewName = reader.GetString(1);
					theCrew.characterMasterName = reader.GetString(2);
					theCrew.factionId = (UInt32)reader.GetInt32(3);
					theCrew.factionRank = (ushort)reader.GetInt16(4);
					theCrew.masterIsOnline = reader.IsDBNull(5) ? (ushort)0 : (ushort)reader.GetInt16(5);
					theCrew.masterPlayerCharId = reader.IsDBNull(6) ? 0u : (UInt32)reader.GetInt32(6);
					crews.Add(theCrew);
				},
				P("@factionId", factionID));

			return crews;
		}

		public List<CrewMember> GetCrewMembersForCrewId(UInt32 crewId)
		{
			List<CrewMember> crewMembers = new List<CrewMember>();
			QueryRows(
				"SELECT cm.char_id, cm.is_captain, cm.is_first_mate, c.handle, c.is_online " +
				"FROM crew_members cm JOIN characters c ON cm.char_id=c.charId WHERE cm.crew_id = @crewId",
				reader =>
				{
					CrewMember member = new CrewMember();
					member.charId = (UInt32)reader.GetInt32(0);
					member.isCaptain = reader.GetBoolean(1);
					member.isFirstMate = reader.GetBoolean(2);
					member.handle = reader.GetString(3);
					member.isOnline = (ushort)reader.GetInt16(4);
					crewMembers.Add(member);
				},
				P("@crewId", crewId));

			return crewMembers;
		}

		public string GetPathForDistrictKey(string key)
		{
			string path = "";
			QueryRows("SELECT path FROM districts WHERE districts.key = @key",
				reader => path = reader.GetString(0),
				P("@key", key ?? ""));
			return path;
		}

		public void updateSourceHlForObjectTracking(UInt16 sourceDistrict, UInt16 sourceHl, UInt32 lastObjectId)
		{
			UInt16 theId = 0;
			UInt16 hardlineId = 0;
			UInt16 districtId = 0;
			UInt32 objectId = 0;

			QueryRows("SELECT id, HardlineId, DistrictId, objectId FROM data_hardlines WHERE DistrictId = @district AND HardlineId = @hardline LIMIT 1",
				reader =>
				{
					theId = (UInt16)reader.GetInt16(0);
					hardlineId = (UInt16)reader.GetInt16(1);
					districtId = (UInt16)reader.GetInt16(2);
					objectId = (UInt32)reader.GetInt32(3);
				},
				P("@district", sourceDistrict), P("@hardline", sourceHl));

			if (objectId == 0 || objectId != lastObjectId)
			{
				NonQuery("UPDATE data_hardlines SET objectId = @objectId WHERE id = @id LIMIT 1",
					P("@objectId", lastObjectId), P("@id", theId));
				#if DEBUG
				Output.WriteLine("[WORLD DB] UPDATE Hardline " + hardlineId.ToString() + " in District " + districtId.ToString() + " with Object ID : " + lastObjectId.ToString());
				#endif
			}
		}

		public UInt16 GetNearestHardline(UInt16 district, double x, double z)
		{
			UInt16 nearest = 0;
			QueryRows(
				"SELECT HardLineId FROM data_hardlines WHERE DistrictId = @district " +
				"ORDER BY (X - @x)*(X - @x) + (Z - @z)*(Z - @z) LIMIT 1",
				reader => nearest = Convert.ToUInt16(reader.GetValue(0)),
				P("@district", district), P("@x", x), P("@z", z));
			return nearest;
		}

		public List<UInt16> GetHardlineIdsForDistrict(UInt16 district)
		{
			List<UInt16> ids = new List<UInt16>();
			QueryRows("SELECT HardLineId FROM data_hardlines WHERE DistrictId = @district ORDER BY HardLineId",
				reader => ids.Add(Convert.ToUInt16(reader.GetValue(0))),
				P("@district", district));
			return ids;
		}

		/// <summary>Hardlines of a district the character has uploaded (char_hardlines).</summary>
		public List<UInt16> GetUploadedHardlines(UInt32 charId, UInt16 district)
		{
			List<UInt16> ids = new List<UInt16>();
			QueryRows("SELECT hl_id FROM char_hardlines WHERE char_id = @charId AND district_id = @district ORDER BY hl_id",
				reader => ids.Add(Convert.ToUInt16(reader.GetValue(0))),
				P("@charId", charId), P("@district", district));
			return ids;
		}

		/// <summary>Records an uploaded hardline; false if the character already had it.</summary>
		public bool AddUploadedHardline(UInt32 charId, UInt16 district, UInt16 hardline)
		{
			if (GetUploadedHardlines(charId, district).Contains(hardline))
			{
				return false;
			}

			NonQuery("INSERT INTO char_hardlines (char_id, hl_id, district_id, added) VALUES (@charId, @hardline, @district, NOW())",
				P("@charId", charId), P("@hardline", hardline), P("@district", district));
			return true;
		}

		public void updateLocationByHL(UInt16 district, UInt16 hardline)
		{
			QueryRows(
				"SELECT DH.X, DH.Y, DH.Z, DH.ROT, DIS.key, DH.DistrictId FROM data_hardlines AS DH, districts AS DIS " +
				"WHERE DH.DistrictId = @district AND DH.HardLineId = @hardline AND DH.DistrictId = DIS.id",
				reader =>
				{
					double x = (double)(reader.GetFloat(0));
					double y = (double)(reader.GetFloat(1));
					double z = (double)(reader.GetFloat(2));
					string disKey = reader.GetString(4);
					Store.currentClient.playerData.setDistrict(disKey);
					Store.currentClient.playerData.setDistrictId((uint)reader.GetInt16(5));
					Store.currentClient.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(x, y, z));
				},
				P("@district", district), P("@hardline", hardline));
			SavePlayer(Store.currentClient);
		}

        public void setBackground(string backgroundText)
        {
            UInt32 charID = Store.currentClient.playerData.getCharID();
            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "UPDATE characters SET background = @background WHERE charId = @charId LIMIT 1";
                    cmd.Parameters.AddWithValue("@background", backgroundText ?? "");
                    cmd.Parameters.AddWithValue("@charId", charID);
                    cmd.ExecuteNonQuery();
                }
            }
        }
		
		public void SetPlayerValues()
		{
            IDataReader reader = null;
            try
            {
			OpenConnection();
            UInt32 charID = Store.currentClient.playerData.getCharID();
			// Promote FLOAT positions before text-protocol conversion so loading preserves their stored binary values.
			string sqlQuery="Select handle,x+0e0 AS x,y+0e0 AS y,z+0e0 AS z,rotation,healthC,healthM,innerStrC,innerStrM,level,profession,alignment,pvpflag,firstName,lastName,exp,cash,district,districtId,factionId,crewId, repMero, repMachine, repNiobe, repEPN, repCYPH, repGM, repZion from characters where charId='"+charID+"'";
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
			UInt32 charId = 0;
			QueryRows("SELECT charId FROM characters WHERE handle = @handle LIMIT 1",
				reader => charId = (UInt32)reader.GetInt32(0),
				P("@handle", (handle ?? "").Trim()));
			return charId;
		}

		public Hashtable GetCharInfo(UInt32 charId)
		{
			Hashtable data = new Hashtable();
			QueryRows(
				"SELECT firstName, lastName, background, district, repMero, repMachine, repNiobe, repEPN, repCYPH, repGM, repZion, exp, cash " +
				"FROM characters WHERE charId = @charId LIMIT 1",
				reader =>
				{
					data.Add("firstname", reader.GetString(0));
					data.Add("lastname", reader.GetString(1));
					data.Add("background", reader.GetString(2));
					data.Add("district", reader.GetString(3));
					data.Add("repMero", reader.GetInt16(4));
					data.Add("repMachine", reader.GetInt16(5));
					data.Add("repNiobe", reader.GetInt16(6));
					data.Add("repEPN", reader.GetInt16(7));
					data.Add("repCYPH", reader.GetInt16(8));
					data.Add("repGM", reader.GetInt16(9));
					data.Add("repZion", reader.GetInt16(10));
					data.Add("exp", reader.GetInt32(11));
					data.Add("cash", reader.GetInt32(12));
				},
				P("@charId", charId));
			return data;
		}

		public void SetReputation(uint charId, string reputationColumn, int value)
		{
			if (!IsAllowedReputationColumn(reputationColumn))
			{
				throw new ArgumentException("Unknown reputation column: " + reputationColumn, nameof(reputationColumn));
			}
			NonQuery("UPDATE characters SET `" + reputationColumn + "` = @value WHERE charId = @charId LIMIT 1",
				P("@value", value), P("@charId", charId));
		}

		public void SetOrgId(uint charId, int orgId)
		{
			NonQuery("UPDATE characters SET alignment = @orgId WHERE charId = @charId LIMIT 1",
				P("@orgId", orgId), P("@charId", charId));
		}

		public void SetPvpFlag(uint charId, bool flagged)
		{
			NonQuery("UPDATE characters SET pvpflag = @flagged WHERE charId = @charId LIMIT 1",
				P("@flagged", flagged ? 1 : 0), P("@charId", charId));
		}

		public Hashtable GetCharInfoByHandle(string handle)
		{
			Hashtable data = new Hashtable();
			if (string.IsNullOrEmpty(handle))
			{
				return data;
			}
			handle = handle.Substring(0, handle.Length - 1);
			QueryRows(
				"SELECT c.charId, c.firstName, c.lastName, c.background, c.alignment, c.conquest_points, f.name as faction_name, fc.crew_name as crew_name " +
				"FROM characters c LEFT JOIN crews fc ON c.crewId=fc.id LEFT JOIN factions f ON c.factionId=f.id WHERE c.handle = @handle LIMIT 1",
				reader =>
				{
					data.Add("charId", (UInt32)reader.GetInt32(0));
					data.Add("firstname", reader.GetString(1));
					data.Add("lastname", reader.GetString(2));
					data.Add("background", reader.GetString(3));
					data.Add("alignment", (ushort)reader.GetInt16(4));
					data.Add("conquest_points", (UInt32)reader.GetInt32(5));
					if (!reader.IsDBNull(6))
					{
						data.Add("faction_name", reader.GetString(6));
					}
					if (!reader.IsDBNull(7))
					{
						data.Add("crew_name", reader.GetString(7));
					}
					data.Add("handle", handle);
				},
				P("@handle", handle.Trim()));
			return data;
		}

        public void SetRsiValues()
        {
            var data = Store.currentClient.playerData;
            lock (data.rsiSync)
            {
                OpenConnection();
                try
                {
                    using var command = conn.CreateCommand();
                    command.CommandText = "SELECT " + RsiColumns + " FROM rsivalues WHERE charId=" + data.getCharID();
                    using var row = command.ExecuteReader();
                    int[] values = new int[22];
                    if (row.Read())
                        for (int i = 0; i < values.Length; i++) values[i] = row.GetInt16(i);
                    data.setRsiValues(values);
                }
                finally { CloseConnection(); }
            }
        }


		public void SetOnlineStatus(uint charId, ushort isOnline)
		{
			NonQuery("UPDATE characters SET is_online = @isOnline WHERE charid = @charId LIMIT 1",
				P("@isOnline", isOnline), P("@charId", charId));
		}

		public void ResetOnlineStatus()
		{
			NonQuery("UPDATE characters SET is_online = 0");
		}

		public void UpdateRsiPartValue(string part, uint value, UInt32 charId)
        {
            if (!MyMarginDBAccess.IsAllowedRsiColumn(part))
            {
                throw new ArgumentException("Unknown RSI column: " + part, nameof(part));
            }
            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "UPDATE rsivalues SET `" + part + "` = @value WHERE charid = @charId LIMIT 1";
                    cmd.Parameters.AddWithValue("@value", value);
                    cmd.Parameters.AddWithValue("@charId", charId);
                    cmd.ExecuteNonQuery();
                }
            }
        }
		
		public void SavePlayer(WorldClient client){
            UInt32 charID = NumericalUtils.ByteArrayToUint32(client.playerInstance.CharacterID.getValue(),1);
			string handle = StringUtils.charBytesToString_NZ(client.playerInstance.CharacterName.getValue());
			
			double x=0;double y=0;double z=0;
			byte[] Ltvector3d = client.playerInstance.Position.getValue();
			NumericalUtils.LtVector3dToDoubles(Ltvector3d,ref x,ref y,ref z);
			
			int rotation =client.playerInstance.YawInterval.getValue()[0];

			using (var connection = new MySqlConnection(connectionString))
			{
				connection.Open();

				// The Loading Area (location 0x385) is a waiting room between death and a hardline: it doesn't fit the
				// tinyint districtId column and isn't a place to log back in to, so keep the last real location.
				if (!client.playerData.DistrictIsLA())
				{
					using (var posCmd = connection.CreateCommand())
					{
						posCmd.CommandText = "UPDATE characters SET x = @x, y = @y, z = @z, rotation = @rotation, districtId = @districtId WHERE handle = @handle";
						posCmd.Parameters.AddWithValue("@x", x);
						posCmd.Parameters.AddWithValue("@y", y);
						posCmd.Parameters.AddWithValue("@z", z);
						posCmd.Parameters.AddWithValue("@rotation", rotation);
						posCmd.Parameters.AddWithValue("@districtId", client.playerData.getDistrictId());
						posCmd.Parameters.AddWithValue("@handle", handle);
						posCmd.ExecuteNonQuery();
					}
				}

				// Serialize the legacy whole-RSI save with a pill's persistence and in-memory update.
				lock (client.playerData.rsiSync)
				{
					int[] rsiValues = client.playerData.getRsiValues();
					using (var rsiCmd = connection.CreateCommand())
					{
						rsiCmd.CommandText = "UPDATE rsivalues SET sex=@p0,body=@p1,hat=@p2,face=@p3,shirt=@p4,coat=@p5,pants=@p6,shoes=@p7," +
						                     "gloves=@p8,glasses=@p9,hair=@p10,facialdetail=@p11,shirtcolor=@p12,pantscolor=@p13,coatcolor=@p14," +
						                     "shoecolor=@p15,glassescolor=@p16,haircolor=@p17,skintone=@p18,tattoo=@p19,facialdetailcolor=@p20,leggins=@p21 " +
						                     "WHERE charId=@charId";
						for (int i = 0; i < 22; i++)
						{
							rsiCmd.Parameters.AddWithValue("@p" + i, rsiValues[i]);
						}
						rsiCmd.Parameters.AddWithValue("@charId", charID);
						rsiCmd.ExecuteNonQuery();
					}
				}
			}
		}



		public void UpdateInventorySlot(UInt16 sourceSlot, UInt16 destSlot, UInt32 charId)
		{
			NonQuery("UPDATE inventory SET slot = @destSlot WHERE slot = @sourceSlot AND charID = @charId LIMIT 1",
				P("@destSlot", destSlot), P("@sourceSlot", sourceSlot), P("@charId", charId));
		}

		public bool IsSlotinUseByItem(UInt16 slotId)
		{
			bool isSlotInUse = false;
			UInt32 charID = Store.currentClient.playerData.getCharID();
			QueryRows("SELECT slot FROM inventory WHERE slot = @slotId AND charID = @charId LIMIT 1",
				_ => isSlotInUse = true,
				P("@slotId", slotId), P("@charId", charID));
			return isSlotInUse;
		}

		public UInt16 GetCrewMemberCountByCrewName(string crewName)
		{
			UInt16 countCrewMembers = 0;
			QueryRows(
				"SELECT COUNT(cm.id) as count_members FROM crew_members cm LEFT JOIN crews c ON cm.crew_id=c.id WHERE c.crew_name = @crewName LIMIT 1",
				reader => countCrewMembers = Convert.ToUInt16(reader.GetValue(0)),
				P("@crewName", crewName ?? ""));
			return countCrewMembers;
		}

		public UInt32 GetCrewIdByCrewMasterHandle(string playerHandle)
		{
			UInt32 crewId = 0;
			QueryRows(
				"SELECT c.id, c.crew_name FROM crew_members cm LEFT JOIN crews c ON cm.crew_id=c.id WHERE c.master_player_handle = @handle LIMIT 1",
				reader => crewId = (UInt32)reader.GetInt32(0),
				P("@handle", playerHandle ?? ""));
			return crewId;
		}

		public uint GetCrewIdByInviterHandle(string playerHandle)
		{
			UInt32 crewId = 0;
			QueryRows("SELECT crewId FROM characters WHERE handle = @handle LIMIT 1",
				reader => crewId = (UInt32)reader.GetInt32(0),
				P("@handle", playerHandle ?? ""));
			return crewId;
		}

		public string GetFactionNameById(uint factionId)
		{
			string factionName = "";
			QueryRows("SELECT name FROM factions WHERE id = @factionId LIMIT 1",
				reader => factionName = reader.GetString(0),
				P("@factionId", factionId));
			return factionName;
		}

		public bool IsCrewNameAvailable(string crewName)
		{
			bool found = false;
			UInt32 crewId = 0;
			DateTime deletedAt = default;

			// Read and close the reader before issuing follow-up queries on the same thread connection.
			QueryRows("SELECT id, created_at, deleted_at FROM crews WHERE crew_name = @crewName AND deleted_at > NOW() LIMIT 1",
				reader =>
				{
					found = true;
					crewId = (UInt32)reader.GetInt32(0);
					deletedAt = reader.GetDateTime(2);
				},
				P("@crewName", crewName ?? ""));

			if (!found)
			{
				return true;
			}

			UInt16 memberCount = GetCrewMemberCountByCrewName(crewName);
			if ((DateTime.Now - deletedAt).TotalHours > 24 && memberCount < 2)
			{
				DeleteCrew(crewId);
				return true;
			}

			return false;
		}

		public void DeleteCrew(UInt32 crewId)
		{
			NonQuery("UPDATE crews SET deleted_at = NOW() WHERE id = @crewId LIMIT 1", P("@crewId", crewId));
			NonQuery("UPDATE characters SET crewId = 0 WHERE crewId = @crewId", P("@crewId", crewId));
			NonQuery("DELETE FROM crew_members WHERE crew_id = @crewId", P("@crewId", crewId));
		}

		private object ScalarQuery(string sql, params KeyValuePair<string, object>[] parameters)
		{
			OpenConnection();
			try
			{
				using (IDbCommand cmd = conn.CreateCommand())
				{
					queryExecuter = cmd;
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
					queryExecuter = cmd;
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
			if (parameters == null || parameters.Length == 0)
			{
				return;
			}
			foreach (KeyValuePair<string, object> parameter in parameters)
			{
				IDbDataParameter p = cmd.CreateParameter();
				p.ParameterName = parameter.Key;
				p.Value = parameter.Value ?? DBNull.Value;
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

        public void SetCrewCaptain(UInt32 crewId, UInt32 charId, string handle)
        {
	        NonQuery("UPDATE crew_members SET is_captain = (char_id = @char) WHERE crew_id = @crew",
		        P("@char", charId), P("@crew", crewId));
	        NonQuery("UPDATE crews SET master_player_handle = @handle WHERE id = @crew",
		        P("@handle", handle), P("@crew", crewId));
        }

        public bool IsOnFriendList(UInt32 charId, UInt32 friendId)
        {
	        object found = ScalarQuery("SELECT id FROM buddylist WHERE charId = @char AND friendId = @friend LIMIT 1",
		        P("@char", charId), P("@friend", friendId));
	        return found != null && found != DBNull.Value;
        }


		public void AddCrew(string crewName, string masterHandle)
		{
			NonQuery("INSERT INTO crews SET crew_name = @crewName, created_at = NOW()",
				P("@crewName", crewName ?? ""));
		}

		// char_missions may be missing from a database made before missions were tracked.
		private void EnsureMissionTable()
		{
			if (missionTableEnsured)
			{
				return;
			}
			NonQuery("CREATE TABLE IF NOT EXISTS char_missions (charId INT UNSIGNED NOT NULL, missionId INT NOT NULL, " +
			         "completions INT NOT NULL DEFAULT 1, PRIMARY KEY (charId, missionId))");
			missionTableEnsured = true;
		}

		public HashSet<int> GetCompletedMissions(UInt32 charId)
		{
			EnsureMissionTable();
			HashSet<int> completed = new HashSet<int>();
			QueryRows("SELECT missionId FROM char_missions WHERE charId = @charId",
				reader => completed.Add(reader.GetInt32(0)),
				P("@charId", charId));
			return completed;
		}

		public void SaveMissionCompleted(UInt32 charId, int missionId)
		{
			EnsureMissionTable();
			NonQuery("INSERT INTO char_missions (charId, missionId) VALUES (@charId, @missionId) " +
			         "ON DUPLICATE KEY UPDATE completions = completions + 1",
				P("@charId", charId), P("@missionId", missionId));
		}

		public UInt16 GetFirstNewSlot()
		{
			UInt32 charID = Store.currentClient.playerData.getCharID();
			var used = new System.Collections.Generic.HashSet<int>();
			// The lowest free bag slot, 1-96 (worn items start at 97).
			QueryRows("SELECT slot FROM inventory WHERE charId = @charId AND slot BETWEEN 1 AND @lastBagSlot",
				reader => used.Add(Convert.ToInt32(reader.GetValue(0))),
				P("@charId", charID), P("@lastBagSlot", LastBagSlot));
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

		public void AddItemToInventory(UInt16 slotId, UInt32 itemGoID)
		{
			UInt32 charID = Store.currentClient.playerData.getCharID();
			NonQuery("INSERT INTO inventory SET charId = @charId, goid = @goid, slot = @slotId, created = NOW()",
				P("@charId", charID), P("@goid", itemGoID), P("@slotId", slotId));
		}

		public void RemoveItemAtInventorySlot(UInt16 slotId, UInt32 charId)
		{
			NonQuery("DELETE FROM inventory WHERE charId = @charId AND slot = @slotId LIMIT 1",
				P("@charId", charId), P("@slotId", slotId));
		}

		public UInt32 GetItemGOIDAtInventorySlot(UInt16 slotId)
		{
			UInt32 charID = Store.currentClient.playerData.getCharID();
			UInt32 GoID = 0;
			QueryRows("SELECT goid FROM inventory WHERE charID = @charId AND slot = @slotId LIMIT 1",
				reader =>
				{
					// goid is a bigint; ability codes such as 0x80001c00 (2147490816) don't fit in an
					// Int32, and GetInt32 threw OverflowException, which crashed the server on recycle.
					GoID = Convert.ToUInt32(Convert.ToInt64(reader.GetValue(0)));
				},
				P("@charId", charID), P("@slotId", slotId));
			return GoID;
		}

		/// <summary>Every inventory row of a character as (slot, goid).</summary>
		public List<Crafting.InventoryItem> GetInventoryItems(UInt32 charId)
		{
			List<Crafting.InventoryItem> items = new List<Crafting.InventoryItem>();
			QueryRows("SELECT slot, goid, count FROM inventory WHERE charId = @charId ORDER BY slot",
				reader => items.Add(new Crafting.InventoryItem(
					Convert.ToUInt16(reader.GetValue(0)),
					Convert.ToUInt32(Convert.ToInt64(reader.GetValue(1))),
					ItemStacks.CountFromDb(Convert.ToInt32(reader.GetValue(2))))),
				P("@charId", charId));
			return items;
		}

		/// <summary>Sets how many items the stack in a slot holds (inventory.count).</summary>
		public void SetInventoryCount(UInt16 slotId, UInt32 charId, int count)
		{
			NonQuery("UPDATE inventory SET count = @count WHERE charId = @charId AND slot = @slotId LIMIT 1",
				P("@count", count), P("@charId", charId), P("@slotId", slotId));
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
			NonQuery("INSERT INTO inventory SET charId = @charId, goid = @goid, slot = @slotId, count = @count, created = NOW()",
				P("@charId", charID), P("@goid", itemGoID), P("@slotId", slotId), P("@count", count));
		}

		/// <summary>
		/// Code storage rows: char_abilities rows whose ability_id is CodeStorage.BaseValue(goid) and whose level
		/// holds count - 1, so the margin's known-abilities list (ability_id + level) sends them as retail did.
		/// </summary>
		public List<CodeStorage.Entry> GetCodeStorage(UInt32 charId)
		{
			List<CodeStorage.Entry> entries = new List<CodeStorage.Entry>();
			QueryRows("SELECT slot, ability_id, level FROM char_abilities WHERE char_id = @charId AND (ability_id & 0xFC000000) = 0x84000000 ORDER BY slot",
				reader =>
				{
					UInt32 value = unchecked((UInt32)Convert.ToInt64(reader.GetValue(1)));
					entries.Add(new CodeStorage.Entry(Convert.ToUInt16(reader.GetValue(0)), CodeStorage.GoidOf(value),
						Convert.ToInt32(reader.GetValue(2)) + 1));
				},
				P("@charId", charId));
			return entries;
		}

		/// <summary>The slot after the highest ability or code storage slot of a character.</summary>
		public UInt16 GetNextAbilitySlot(UInt32 charId)
		{
			object scalar = ScalarQuery(
				"SELECT COALESCE(MAX(slot), -1) + 1 FROM char_abilities WHERE char_id = @charId AND slot < @firstSlot",
				P("@charId", charId), P("@firstSlot", CharacterAttributes.FirstSlot));
			return Convert.ToUInt16(scalar);
		}

		public System.Collections.Generic.HashSet<int> GetAbilityInventorySlots(UInt32 charId)
		{
			var slots = new System.Collections.Generic.HashSet<int>();
			QueryRows("SELECT slot FROM char_abilities WHERE char_id = @charId AND slot < @firstSlot",
				reader => slots.Add(Convert.ToInt32(reader.GetValue(0))),
				P("@charId", charId), P("@firstSlot", CharacterAttributes.FirstSlot));
			return slots;
		}

		public void AddCodeStorage(UInt32 charId, CodeStorage.Entry entry)
		{
			NonQuery("INSERT INTO char_abilities SET char_id = @charId, slot = @slot, ability_id = @abilityId, level = @level, is_loaded = 0, ability_name = @name, added = NOW()",
				P("@charId", charId),
				P("@slot", entry.Slot),
				P("@abilityId", CodeStorage.BaseValue(entry.Goid)),
				P("@level", entry.Count - 1),
				P("@name", "Code storage " + entry.Goid));
		}

		public void SetCodeStorageCount(UInt32 charId, UInt16 slot, int count)
		{
			NonQuery("UPDATE char_abilities SET level = @level WHERE char_id = @charId AND slot = @slot AND (ability_id & 0xFC000000) = 0x84000000 LIMIT 1",
				P("@level", count - 1), P("@charId", charId), P("@slot", slot));
		}

		public void RemoveCodeStorage(UInt32 charId, UInt16 slot)
		{
			NonQuery("DELETE FROM char_abilities WHERE char_id = @charId AND slot = @slot AND (ability_id & 0xFC000000) = 0x84000000 LIMIT 1",
				P("@charId", charId), P("@slot", slot));
		}

		public void SaveInfo(WorldClient client, long cash)
		{
			NonQuery("UPDATE characters SET cash = @cash WHERE charId = @charId LIMIT 1",
				P("@cash", cash), P("@charId", client.playerData.getCharID()));
		}

		public void UpdateAbilityLoadOut(List<UInt16> abilitySlots, uint loaded)
		{
			if (abilitySlots == null || abilitySlots.Count == 0)
			{
				return;
			}

			UInt32 charID = Store.currentClient.playerData.getCharID();
			OpenConnection();
			try
			{
				using (IDbCommand cmd = conn.CreateCommand())
				{
					queryExecuter = cmd;
					cmd.CommandText = "UPDATE char_abilities SET is_loaded = @loaded WHERE char_id = @charId AND slot = @slot";
					IDbDataParameter pLoaded = cmd.CreateParameter(); pLoaded.ParameterName = "@loaded"; pLoaded.Value = loaded; cmd.Parameters.Add(pLoaded);
					IDbDataParameter pCharId = cmd.CreateParameter(); pCharId.ParameterName = "@charId"; pCharId.Value = charID; cmd.Parameters.Add(pCharId);
					IDbDataParameter pSlot = cmd.CreateParameter(); pSlot.ParameterName = "@slot"; pSlot.Value = (ushort)0; cmd.Parameters.Add(pSlot);
					foreach (ushort slot in abilitySlots)
					{
						pSlot.Value = slot;
						cmd.ExecuteNonQuery();
					}
				}
			}
			finally
			{
				CloseConnection();
			}
		}

		public UInt16 GetCurrentAbilityLevel(Int32 abilityGoId, UInt32 charId)
		{
			UInt16 level = 0;
			QueryRows("SELECT level FROM char_abilities WHERE char_id = @charId AND ability_id = @abilityId LIMIT 1",
				reader => level = (UInt16)reader.GetInt16(0),
				P("@charId", charId), P("@abilityId", abilityGoId));
			return level;
		}

		public int? GetAbilityLevelAtSlot(UInt32 charId, UInt16 slot, Int32 abilityGoId)
		{
			object level = ScalarQuery(
				"SELECT level FROM char_abilities WHERE char_id = @charId AND slot = @slot AND ability_id = @abilityId LIMIT 1",
				P("@charId", charId), P("@slot", slot), P("@abilityId", abilityGoId));
			return level == null || level == DBNull.Value ? (int?)null : Convert.ToInt32(level);
		}

		public bool HasAbility(UInt32 charId, Int32 abilityGoId)
		{
			object count = ScalarQuery(
				"SELECT COUNT(*) FROM char_abilities WHERE char_id = @charId AND ability_id = @abilityId",
				P("@charId", charId), P("@abilityId", abilityGoId));
			return Convert.ToInt64(count) > 0;
		}

		public void AddAbility(UInt32 charId, UInt16 slot, Int32 abilityGoId, int level)
		{
			NonQuery("INSERT INTO char_abilities SET char_id = @charId, slot = @slot, ability_id = @abilityId, level = @level, is_loaded = 0, ability_name = @name, added = NOW()",
				P("@charId", charId), P("@slot", slot), P("@abilityId", abilityGoId), P("@level", level), P("@name", "Ability " + abilityGoId));
		}

		public void SetAbilityLevel(UInt32 charId, UInt16 slot, int level)
		{
			NonQuery("UPDATE char_abilities SET level = @level WHERE char_id = @charId AND slot = @slot",
				P("@level", level), P("@charId", charId), P("@slot", slot));
		}

		public void SaveLevel(UInt32 charId, int level, UInt16 health, UInt16 maxHealth, UInt16 innerStrength, UInt16 maxInnerStrength)
		{
			NonQuery("UPDATE characters SET level = @level, healthC = @health, healthM = @maxHealth, innerStrC = @innerStrength, innerStrM = @maxInnerStrength WHERE charId = @charId LIMIT 1",
				P("@level", level), P("@health", health), P("@maxHealth", maxHealth), P("@innerStrength", innerStrength), P("@maxInnerStrength", maxInnerStrength), P("@charId", charId));
		}

        public Dictionary<byte, UInt16> GetCharacterAttributes(UInt32 charId)
        {
            using var connection=new MySqlConnection(connectionString);connection.Open();using var command=connection.CreateCommand();
            command.Parameters.AddWithValue("@character",charId);
            command.CommandText="SELECT id,slot,ability_id,level FROM char_abilities WHERE char_id=@character AND slot IN (598,599,601,602,604) ORDER BY id";
            using var reader=command.ExecuteReader();var rows=new List<AttributeAllocation.Row>();
            while(reader.Read())rows.Add(new AttributeAllocation.Row(reader.GetUInt32(0),reader.GetInt32(1),reader.GetInt64(2),reader.GetInt64(3)));
            return AttributeAllocation.ReadValues(rows);
        }


		public void SetCharacterAttribute(UInt32 charId, byte attribute, UInt16 value)
		{
			UInt16 slot = CharacterAttributes.SlotOf(attribute);
			OpenConnection();
			try
			{
				using (IDbCommand del = conn.CreateCommand())
				{
					queryExecuter = del;
					del.CommandText = "DELETE FROM char_abilities WHERE char_id = @charId AND slot = @slot";
					AddParameters(del, new[] { P("@charId", charId), P("@slot", slot) });
					del.ExecuteNonQuery();
				}
				using (IDbCommand ins = conn.CreateCommand())
				{
					queryExecuter = ins;
					ins.CommandText = "INSERT INTO char_abilities SET char_id = @charId, slot = @slot, ability_id = @abilityId, level = @level, is_loaded = 0, ability_name = @name, added = NOW()";
					AddParameters(ins, new[]
					{
						P("@charId", charId),
						P("@slot", slot),
						P("@abilityId", CharacterAttributes.RowValueOf(attribute)),
						P("@level", value),
						P("@name", "Attribute " + attribute)
					});
					ins.ExecuteNonQuery();
				}
			}
			finally
			{
				CloseConnection();
			}
		}

		public void SaveExperience(WorldClient client, long exp)
		{
			NonQuery("UPDATE characters SET exp = @exp WHERE charId = @charId LIMIT 1",
				P("@exp", exp), P("@charId", client.playerData.getCharID()));
		}
    }
}

